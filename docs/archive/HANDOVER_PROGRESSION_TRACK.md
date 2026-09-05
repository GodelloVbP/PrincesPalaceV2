# Handover — the level reward track

Written to start a fresh session without re-deriving anything. The design is
agreed in outline; three decisions are open and one of them blocks authoring.

Repo state: green at `52cb0c7`, working tree clean apart from
`ChakraPetch-Regular SDF.asset`, which was already modified before this work
began and is not ours.

---

## 1. What this is

**Levels currently grant one stat point and nothing else.** The design is a
battle-pass style reward track: reach a level, get the reward. **No currency and
no purchase screen** — that was considered (relics as a spend) and dropped.

**Per character.** Levels are per character (`Character.level`), stat points are
per character, and Favor is already a per-character stat that the squad reads as
a MAX (`ItemOfferRoll.SquadFavor` — highest, never the sum). So a levelled
character becomes the one you field for loot, which is a real decision.

The multi-character consequences are **deliberately deferred** — with a solo
squad today there is one track. See §5.

---

## 2. The track

100 levels. Milestones on the tens; everything else is filler.

| Level | Milestone |
|---|---|
| 10 | Prince's Favor +5 |
| 20 | Free respec |
| 30 | Guaranteed rest before every boss |
| 40 | Offer reroll |
| 50 | Wider Offer (4 items) |
| 60 | Start every run with 2 relics |
| 70 | Choose your starting relics (instead of a random draft) |
| 80 | ~~Elites always drop a relic~~ **+10 stat points** (see below) |
| 90 | Second life (once per run) — **spec below** |
| 100 | Second life refreshes at every boss |

> Level 80 changed 2026-08-21, by the author's call. "Elites always drop a
> relic" was cut as too strong: relics are run-scoped and uncapped (`AUDIT.md`
> #51), elites recur every 8 steps, and a guaranteed drop on each compounds with
> 60's four starting relics and 70's picking them into a run decided by its
> relic stack before the first boss. Replaced with **10 stat points**, which is
> exactly `AbilityDerivation.CharacterBand` -- the point at which a stat stops
> being worth a flat 20 max health and starts being worth the square of the
> excess, beginning at 1. So it is the most efficient spend the game offers
> rather than merely a large one.
>
> **Second life, specified by the author 2026-08-21.** It is IN-FIGHT and
> nothing more: when a character reaches 0 HP during a fight, they come back at
> 50% of max HP. Not a run-level revive, not a re-entry into a lost fight, not a
> defeat screen that offers a continue.
>
> That is materially cheaper and safer than this document's "second life (once
> per run)" implied, and it is worth saying why: the plan priced 90 as the most
> invasive item left because it appeared to contradict FightController's stated
> invariant that "a run always ends on a loss" and to need a branch in the
> defeat/teardown path -- the path AUDIT.md shows has already produced
> soft-locks. An in-fight revive at 0 HP does not touch that path at all. The
> fight simply does not end, because the party is not wiped.
>
> Level 100 still reads as "the charge comes back at every boss", with the
> charge itself run-scoped.
>
> Levels 25 and 45 also changed; see `docs/archive/PLAN_PROGRESSION_TRACK.md` and
> `AUDIT.md` #50-51. The relic line is now 25 -> 2 at start, 45 -> 3, 60 -> 4,
> 70 -> pick them.

**Mid-tier, not on the tens:** ~~extra relic slot at 25 (slot 2) and 45 (slot
3), ordered because `RelicLoadout` is `characterId -> one relicId` today, so
level 60's "start with 2 relics" needs slot 2 to already exist.~~ **Wrong on
both counts** — `RelicLoadout` has no production reader at all, and there is no
slot cap to lift. 25 and 45 grant an extra **starting relic** each instead, so
the line is 25 -> 2, 45 -> 3, 60 -> 4. `AUDIT.md` #50-51.

**The 90 filler levels:** 30x stat point, 20x Favor +2 (40 total), 15x max HP
+10, 6x signature at fight start, 2x offer reroll. Gold nodes are **held** — see
§4.

**Two chains, deliberately.** 25/45/60 climb to four starting relics and 70 lets
you pick them; 90 gives a second life and 100 makes it recharge. The first draft of this track was
100 independent grants, which is a checklist rather than a tree — the same thing
`ContentDatabase.OrbCost`'s comment warns about for the talent tree.

---

## 3. Numbers already dug out — do not re-derive these

**Prince's Favor is worth almost nothing per point.**
`LootLadder.FavorPerPoint = 0.006` — one point adds 0.6 percentage points to the
per-rung step chance. On a normal fight that moves expected climb from **0.282
to 0.292 rungs**. The authored characters spanned 1–10 (turtle 1, fly 2, sheep 4,
dog 6, owl 10), so the entire content spread was worth ~0.08 rungs.

> **The spread is gone, and the conclusion is stronger without it.** Fly, Dog,
> Turtle and Owl were removed on 2026-08-22 — they had no art and no
> implementation, and anything else built for them could only have been a
> placeholder. Shawn's 4 is the whole of authored Favor now, so the range this
> paragraph measured is a single point. That does not change what follows: the
> argument was always that the CONTENT spread is negligible against what the
> TRACK grants, and a spread of zero makes it more so.

This is why the track grants **40** Favor rather than 8. At ~44 effective Favor:

| | base | at 44 Favor |
|---|---|---|
| normal | p 0.220, E 0.282 | p 0.484, **E 0.913** |
| elite | p 0.300, E 0.428 | capped, **E 1.161** |
| boss | p 0.380, E 0.608 | capped, **E 1.161** |

**`LootLadder.MaxStep = 0.55` caps at different Favor per class: boss at ~~28~~
**29**, elite at 42, normal at 55.** So the back half of the Favor nodes only
improves ordinary fights. Spread Favor across all ten bands or the last twenty
levels buy nothing.

> Boss corrected 2026-08-21, during phase C: `0.38 + 28 * 0.006` is 0.548, still
> under the cap, so 29 is the first Favor that reaches it. Elite and normal were
> right. All three boundaries are now pinned by
> `ItemOfferFavorTests.FavorStopsBuyingAnythingOnceTheLadderCaps` as
> one-below/at-cap pairs, so this paragraph is no longer the only record of
> them — which is the point, given the section it sits in says not to
> re-derive.

**Favor already drives rarity AND plus.** `RarityTable.RollTier` and
`RollPlus` both call `LootLadder.Climb(encounter, favor, ...)`. Any "better item
quality" node is a second dial on the same thing — this is why "honed offers"
and a rarity floor were both cut.

**Rest already heals the party to full.** `RoomResolution` returns
`Outcome(Kind.Rest, healsPartyToFull: true)`. A "rest heals more" node has
nothing to improve, which is why the rest milestone is about *access* (guaranteed
before a boss) rather than amount.

**Room-weight nodes were cut for the same reason Favor nearly was.** The
`MiddleRooms` table totals 90 (Fight 44, Event 14, Treasure 12, Unknown 8, Shop
6, Rest 6). +1 to Rest moves it from 6.7% to 7.7% — across an 8-room leg,
0.53 rest rooms becomes 0.62. Four such nodes still buy less than one extra rest
per leg.

---

## 4. The open decisions

### 4a. The XP curve — this blocks authoring

`Character.ExpToNextLevel(level) => level * 100`. Cumulative to level N is
`50*N*(N-1)`: **19,000 at level 20, 122,500 at 50, 495,000 at 100.**

**But XP income is exponential in depth.** `DifficultyCurve.ScaleReward` is
`ScaleHealth`, compounding 77 permille per step — the file's own comment says a
step-80 fight pays roughly **370x** a step-0 one. Modelled at ~40 raw XP per
fight:

| Leg reached | XP that leg | Cumulative |
|---|---|---|
| 1 | 290 | 290 |
| 5 | 3,113 | 6,598 |
| 8 | 18,467 | 40,902 |
| 10 | 60,512 | 134,842 |

**So levels currently ACCELERATE, they do not slow down.** One deep leg pays more
than levels 1–35 cost combined. The requested feel — cheap early, slower later —
needs the level cost to outrun the income, which a linear-increment curve cannot
do against an exponential one.

Proposed, in the same integer-permille idiom `DifficultyCurve` already uses:

```
ExpToNextLevel(level) = 100 * (1 + permille/1000)^level     // permille 90
```

| Level | That level costs | Cumulative |
|---|---|---|
| 1 | 109 | 109 |
| 10 | 237 | 1,800 |
| 30 | 1,327 | 15,000 |
| 50 | 7,435 | 87,000 |
| 75 | 64,000 | 750,000 |
| 100 | 552,000 | 6.4M |

1.09 per level outpaces 1.077 per step, so the track genuinely slows. **Decide
where the game's real depth ceiling is first** — level 100 at 6.4M assumes
routinely reaching leg 10+. If the ceiling is leg 6–8, drop permille to ~70 or
shorten the track, or the last third is decoration.

**The deeper question, and probably the right fix:** should character XP be
depth-scaled at all? The 370x multiplier exists to keep *loot and threat*
coupled. `ScaleReward`'s own comment says it is coupled deliberately so that
decoupling is "a deliberate edit rather than a silent one". Decoupling XP from
`ScaleHealth` would let the level curve be set independently instead of racing
it. **This may be that edit.**

### 4b. Gold nodes are held

17 filler levels were going to be gold % and starting gold. `RoomType.Shop`
exists in the weight table and `RoomResolution` has a case for it, but the shop
is not built — so those levels would reward a number with no sink. Either build
the shop first or refill those levels with stat points and Favor.

### 4c. Run-scoped rewards on a per-character track

Wider Offer, offer reroll and guaranteed-rest are **run-scoped**, not
per-character. On a per-character track they either apply globally the moment any
character reaches the level, or only while that character is fielded. The second
is more interesting and more work. Moot with a solo squad; decide before the
roster grows.

---

## 5. Explicitly out of scope

- **Multi-character behaviour.** Deferred by decision, not oversight.
- **Cut and why:** extra recruit slot, requirement relief, merciful curve, ember
  yield %, XP yield %, honed offers, rarity floor, room-weight nodes, rest-heal
  amount. Favor duplication and the no-op rest heal are the two worth
  remembering, since both look reasonable until you read the constant.
- **Raising `ContentDatabase.EmberSpendCap` (30).** Considered and left alone.
  `OrbCost`'s comment: a full path costs 45 against a 30 budget, "about two
  thirds of one path, which is what makes the tree a choice rather than a
  checklist". Raising it dissolves the tension the talent tree is built on.

---

## 6. Answered, needs no investigation

**"I chose Black Ram but I don't see my ability to turn into the Black Ram."**
Working as designed. The skill exists — `black_ram_mode`, "Black Ram Mode",
`effect: Transform`, 3 turns, +50% attack, +30% speed, costs 7 signature — and
is granted by **`sheep_ram_converge`**, the Ram path's convergence (slot 10,
gated at 9 embers spent in that path). The *root* is the allegiance; the
*convergence* grants the ability. It also carries `unlockLevel: 999`, so level
never grants it — talent only.

**Whether that gate is too deep is a design question, not a bug.** Five talents
carry the Transform-modifying effects (`Wrath`, `Fed on Blood`, `No Way Back`,
`Stampede`, `What He Is Now`), all of them past the convergence — so the whole
Transform sub-system is invisible until a player commits 9 embers to one path.

---

## 7. Landed this session, for context

`8bd597f` a run keeps nothing (gear + delete-save), `e54e67d` equipment sockets
drawn, `94640eb` HEAD/SHOES placement, `52cb0c7` the real inventory cleared,
locked skills hidden, pack header buttons enlarged.

Two live bugs found and fixed on the way, both recorded in `AUDIT.md`:
`FightSession.Begin()` had 36 test call sites and none in the game, which
deadlocked any fight a monster opened; and deleting a save left the cached copy
to be written straight back.

`architecture_audit.md` carries the standing rule that came out of it — **a test
may not do for production what production must do for itself** — which was then
broken twice more in the same session. Worth reading F17 before writing tests
here.
