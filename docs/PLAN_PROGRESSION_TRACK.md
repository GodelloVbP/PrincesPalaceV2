# Plan — building the level reward track

Companion to `HANDOVER_PROGRESSION_TRACK.md`, which is the design record. This is
the build order, and it starts by disagreeing with that document about how much
of this work is the track.

Written against `79bcb49`. Every file:line below was read, not remembered.

---

## 0. Four things the handover does not say

Each one changes what gets built. Two of them change it a lot.

### F1. The track is mostly six new systems, not ten rewards

The handover reads as a table of grants to wire up. Checked against the code,
four of the twelve milestones sit on machinery that exists:

| Level | Reward | Exists today? |
|---|---|---|
| 10 | Favor +5 | the *reader* does — no writable Favor. F2 |
| 20 | Free respec | **no.** No respec anywhere; `Character.cs:30` mentions one hypothetically |
| 25/45 | Relic slots 2, 3 | **no cap exists to lift** — `RunSnapshot.relicIds` is a list, design is "infinite slots per run". `AUDIT.md` #50, #51 |
| 30 | Rest before every boss | **no.** Rest is a room type the map may or may not lay down |
| 40 | Offer reroll | **no** |
| 50 | Wider offer | constant exists, but see F3 |
| 60 | Start with 2 relics | draft is `RelicPool.OfferCount = 3` pick-one, pre-run in the hub |
| 70 | Choose starting relics | **no.** The draft is the random offer |
| 80 | Elites always drop a relic | **no.** Relics are drafted in the hub, never dropped in a fight |
| 90/100 | Second life | **no.** Nothing named revive; `FightController.Input.cs:470` goes straight to `OpenDefeat()` |

So the honest estimate is: the 100-node table is the small half. Plan it as
"build the systems, then hang a track on them", and the track can ship
incrementally as each system lands. Planning it as one deliverable means nothing
is playable until all of it is.

### F2. There is no mutable Prince's Favor to add to

`princesFavor` is a field on **`CharacterDefinition`** — authored content,
immutable at runtime (`Core/Content/CharacterDefinition.cs:70`).
`ItemOfferRoll.SquadFavor` takes `IEnumerable<CharacterDefinition>` and reads it
off the definition (`Core/ItemOfferRoll.cs:51`).

The track grants Favor at **21 of its 100 nodes** (the level-10 +5 and 20x +2).
None of them has a field to write to.

The seam is one function. `ItemOfferRoll.CurrentSquadFavor` (`:70`) is the only
place a save-side `Character` meets a definition-side favor — it maps ids to
definitions and hands them over. Add `Character.earnedFavor` and do the addition
there, per member, before the max. Leave
`SquadFavor(IEnumerable<CharacterDefinition>)` alone: it is pure and the EditMode
tests call it directly.

Do **not** push the addition down into `LootLadder` or `RarityTable`. They take
an `int favor` and should keep taking one.

### F3. Offer width is a compile-time constant inside a generated screen

`ItemOfferTable.OfferCount = 3` (`Domain/Rewards/ItemOffer.cs:45`).
`ReckoningScreen.cs:249` emits exactly that many card nodes **at scene-build
time**, and `:689` centres them with `(index - (OfferCount - 1) * 0.5f)`.

Scenes are generated once, so level 50 cannot widen a constant at runtime. The
tree has to emit the maximum and hide the surplus, and the row has to re-centre
on the *visible* count or every ordinary 3-offer set sits off-axis. `UiAudit`
re-solves the built tree, so it will not see a runtime hide — this one is on us.

`ReckoningScreenTests.cs:170-172` pin node counts against `OfferCount`. They are
the right tests; they need to start saying "max width".

### F4. There is already a profile-wide unlock list, and it has a trap

`SaveData.purchasedUpgradeIds` + `UpgradeDefinition` ("permanent, account-wide").
Exactly the shape a track grant wants.

But `SaveData.Reconcile:482` runs
`purchasedUpgradeIds.RemoveAll(id => ContentDatabase.GetUpgrade(id) == null)` —
an id with no authored definition is **silently deleted on every load**. Reuse
the list and every grant needs a real `UpgradeDefinition` behind it or it
evaporates between sessions, with nothing reporting it.

Recommendation: do not reuse it. A track grant is earned, not purchased, and
folding them together means a reader like `EffectiveMaxSquadSize:153` can no
longer tell why something is on. Give the track its own list, and extend
`Reconcile` to validate it against the track instead — same tolerant posture,
separate reason.

---

## 1. The decision that outranks 4a

**The handover's three open decisions are missing the one that gates them: is
`Character.level` per-run or per-profile?** The code contradicts itself.

- `Character.cs:58` — "PER-RUN progression, earned from combat and reset by
  StartRun", and at `:67`, "one rule that has to hold: StartRun resets all four.
  There is a test whose whole job is to fail if it ever stops."
- `RunManager.StartRun` (`Core/RunManager.cs:133`) replaces `save.activeRun` and
  calls `Forget()`/`Persist()`. It never touches `save.roster`. Nothing in
  non-test code assigns `level = 1`, `exp = 0`, or clears `unspentStatPoints` —
  the field initialiser is the only `level = 1` outside a default parameter in
  `FightEncounterAdapter`. **There is no such test.**
- `RewardApplier.cs:12` states the opposite and matches reality: "GOLD belongs to
  the run and is lost with it, EXPERIENCE belongs to the characters and
  survives."

So levels persist today, and a comment that reads like a load-bearing invariant
is describing an intention nobody implemented. That is worth an `AUDIT.md` entry
regardless of this track.

It decides the track because **six milestones are statements about how a run
begins** — start with 2 relics, choose them, guaranteed rest before every boss,
free respec, second life, wider offer. If level reset at `StartRun` you could
never be holding them at the moment they apply. The track as designed only exists
if levels are meta.

**Close this first. It is one commit either way and every later estimate depends
on it.** Recommended: make meta official — delete the stale comment and write the
test that was claimed but inverted (levels survive `StartRun`).
`investedAbilityScores` is the field with a real argument for resetting, and that
is a separate question from this one.

### And it reframes 4a's arithmetic

The handover models XP income *within one run* (leg 10 → 134,842 cumulative). If
levels are meta, income is that figure **times however many runs**, and the
comparison that matters is runs-to-level-100:

| Curve | Cumulative to 100 | At leg 5/run | At leg 8/run | At leg 10/run |
|---|---|---|---|---|
| current, `level * 100` | 495,000 | 75 runs | 12 runs | **3.7 runs** |
| proposed, permille 90 | ~6.4M | 970 runs | 156 runs | 47 runs |

Level 100 in four deep runs is the actual problem, and it is a sharper argument
for changing the curve than "one deep leg pays more than levels 1-35 combined".
47 deep runs for a 100-node track is a defensible battle pass. 970 is not — which
is the handover's own point about deciding the depth ceiling first, now with a
number attached to each answer.

---

## 2. Build order

Each phase ends green and committable. Nothing below depends on a phase after it.

**A — Settle level scope (§1).** Comment + test + `AUDIT.md` entry. No behaviour
change. *Know it worked:* a test that starts a run and asserts level survives.

**B — The curve.** `Character.ExpToNextLevel` only, in the integer-permille idiom
`DifficultyCurve` already uses. Its own comment invites this: "Tunable later
without touching callers." *Must not change:* `AddExperience`'s multi-level loop,
which is already correct and already tested. *Know it worked:* pinned literal
expectations at levels 1/10/50/100, never recomputed from the formula
(`CLAUDE.md` gotcha 5). Watch `DossierXpBarTests` and `ReckoningComparisonTests`.

**C — The Favor seam (F2).** `Character.earnedFavor`, purely additive so
`CurrentVersion` does not move, plus the addition in `CurrentSquadFavor`.
*Know it worked:* an EditMode test that earned Favor raises the squad max, and
that the max is still max-not-sum.

**D — The track spine.** Storage (a per-character claimed-level watermark, not a
list of ids), the "what does level N grant" lookup, and the claim call sited in
`RewardApplier.Apply`, where `AddExperience` already reports `levelsGained`. No
new reward kinds yet — grant stat points and Favor only, which B and C have made
real. *This is the first phase that is playable.*

**E — Reward kinds, cheapest first.** ~~Relic slots (25/45) first~~ — **that
ordering was wrong and the reward itself is blocked; see `AUDIT.md` #50 and
#51.** `RelicLoadout` has no production reader at all (relics in play live on
`RunSnapshot.relicIds`), and there is no slot cap to lift — `relicIds` is a list
whose header states the design as "infinite slots per run". Nothing else depends
on 25/45: level 60 is drafting twice, level 80 is `relicIds.Add`. Those two
levels need a design call before anything is built.

Remaining order: offer width (50, per F3), then reroll (40), then relic drops
(80, now the cheapest of the new systems rather than one of the dearest), then
respec (20), rest access (30), starting-relic choice (70), second life (90/100).
Each is its own commit and its own decision about whether it is worth building.

**F — The screen.** Seven touch points, six mechanically caught
(`architecture_audit.md` Part IV). Cheap, and last: a track you cannot see is
still a track that works, and the layout question is easier once the reward kinds
are known.

**G — Author the 100 nodes.** Only meaningful after E decides which kinds exist.
Gold nodes stay held (handover 4b) — the shop is not built, so they would pay a
number with no sink.

---

## 3. Open decisions, and how to close them

- **Level scope.** §1. Close by fiat: meta. One commit.
- **4a, the curve.** Blocked on the depth ceiling. Closeable with data —
  `SaveData.lifetimeDeepestStep:117` is already recorded, so the answer is
  observable rather than guessed.
- **The deeper 4a question — decouple XP from `ScaleReward`?** Defer. It is a
  separate commit with its own argument, `ScaleReward:99`'s comment already
  demands it be deliberate, and phase B works either way.
- **4b, gold nodes.** Held. Refill with stat points and Favor.
- **4c, run-scoped rewards on a per-character track.** Moot at solo squad. But F1
  changes the framing: most of these are new systems, so "global the moment any
  character reaches it" vs "only while that character is fielded" is a decision
  made *while building each one*, not retrofitted afterwards. Write the answer
  down before phase E, not after.

---

## 4. Verification

Per phase: `tools/test.ps1 -Changed`. Areas this touches: `run`, `content`,
`combat`, `ui`.

Before each commit: `tools/run_tests_parallel.ps1` — full, and it builds the
scenes. Any phase touching `ReckoningScreen` (E's offer width) needs
`-BuildScenes` when the scenes are meant to be committed, or `UiAudit` is
checking whatever was last emitted rather than what the source now says.

`architecture_audit.md` F17 before writing tests here: **a test may not do for
production what production must do for itself.** Phase D sites the claim call in
production code for exactly that reason — the temptation will be to let the test
grant the reward.
