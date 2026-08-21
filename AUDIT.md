# Prince's Palace — Architecture & Design Audit

Date: 2026-07-28. Audited at commit `6fb70d0` by five parallel reviewers covering
architecture/layering, save+content pipeline, combat mechanics, UI/SceneBuilder, and
test-suite quality. Every finding below was verified against actual code with file:line
references — nothing here is speculative.

**How this register works:** findings stay in place forever, they are never deleted.
A finding that gets fixed is struck through in place with ` — fixed in <short-sha>: <one
line>` appended, so the register stays a complete history of what this project has
actually been wrong about, not just what's currently wrong. New findings (including
open investigations that never got a firm root cause) get appended under whatever
section fits, or under "Open investigations" near the end if none does.

**Cleanup pass, 2026-08-02:** a mechanical simplify/dead-code/bug-fix pass (commits
`8f8200f`..`9166e85`) re-verified this whole register against current code before
touching anything, since it postdates the original audit by several feature phases.
Closed #1, #8, #10, #15, #16, and the first bullet of #23. Found #12 already resolved
(`0e7e0a7`, before this pass) and corrected the stale `Domain/Grid/`/`Domain/Events/`
references in "Dead code inventory" below (both deleted in this pass, confirmed
already unreferenced) and the `"health_potion"` hardcode under "Also worth knowing"
(gone — the party panel's item list is generic now). New findings from this pass are
appended as their own section before "Open investigations".

**Triage pass, 2026-08-04:** every open finding re-verified against current code, then fixed in
priority order (commits `5bf51eb`..`abef141`). The re-verification mattered as much as the fixing:
**five findings were already fixed and had simply never been struck** (#3, #4, #5 by `b19d8a5`,
#18 by `8f7ab4d`, #17 by an unrelated damage-scale change), and several others had drifted far
enough that the recorded fix would have been the wrong one. Two long-standing items were
resolved by finding out *why* rather than by patching what was described: **#24's root cause is
#13** (see both), and **#30 was a real gap, not dead code**.

> ## READ THIS BEFORE ACTING ON ANY FINDING BELOW #37
>
> **This register spans two codebases.** Everything above, and every finding from #1 to
> #36, was written against **v1** — the audit's own base commit `6fb70d0` is not in this
> repository, and neither are the files those findings name. Verified 2026-08-17 by
> `git log --all` over the whole history: `GameplayManager.cs`, `StoreController.cs`,
> `DungeonController.cs`, `SceneBuilder.Widgets.cs` and `FightControllerTests.cs` are all
> **NEVER PRESENT** here. v1 now lives at `C:\Games\Backup Princes palace\Prince's Palace`.
>
> This tree's history begins at `1bd5999`, 2026-08-10, *"Start keeping the rebuild's
> history"*. So the dividing line is a date, and it is sharp:
>
> | Findings | Written against | Status |
> |---|---|---|
> | #1–#36, and the 08-02 / 08-03 / 08-04 passes | v1 | **Each needs re-verifying against v2 before it means anything.** Some describe code that no longer exists; some describe a design v2 kept and therefore still bite (#32 was re-confirmed live on 2026-08-17, at `EnemyEntryResolver.cs:173`). |
> | #37–#45 (2026-08-11, 08-12) | v2 | Current. These reference `UiEmitter`, `ScreenRegistry`, `ReckoningScreen`, `Domain/Economy/` — all v2 files. |
>
> **The failure mode this box exists to stop** is the one that produced this note: reading a
> v1 finding as a live v2 defect and planning work against it. That happened on 2026-08-17
> and got as far as a recommendation before the file list was checked. A v1 finding is not
> wrong and not stale — it is *about somewhere else*, which is worse, because everything
> about its wording says otherwise.
>
> **Carrying a v1 finding across takes evidence, not judgement**: name the v2 file:line that
> still exhibits it, or strike it as obsoleted. The table below has NOT been re-counted since
> the rebuild and describes v1 — treat its numbers as history.

| Section | Open | Struck |
|---|---|---|
| P0 — Live bugs, player-visible | 2 | 6 |
| P1 — Correctness and robustness | **0** | 8 |
| P2 — Tests that give false confidence | 3 | 4 |
| Systems worth building | 3 | 2 |
| Cleanup-pass findings (2026-08-02) | 4 | 1 |
| Actor frame-animation findings | 1 | 1 |
| Pipeline-generalisation findings | 4 | 1 |
| Open investigations | 0 | 1 |

**P1 is now empty** — every correctness-and-robustness finding the original audit raised is
struck. The two remaining P0s and the three remaining P2s are listed below as design calls or
as work deliberately scoped out, not as unexamined items. (Written 2026-08-04, about v1. The
2026-08-17 strikes of #19 and #29 are reflected in the counts above; nothing else here is.)

Still open by DESIGN, not by neglect: **#2** (the economy — numbers re-measured below and now
much worse than recorded, but retuning three interacting dials is the author's call), **#7**
(Store layout, guarded but unchanged), **#31** (delivered stance art with no code path), the
`DelayCurrent`/`RemoveCombatant` keep-or-kill decision under #21, and **#29**'s remaining
`NewUiRect` migration (cosmetic, no defect behind it, and each file costs a scene rebuild plus a
visual check). The two 2026-08-02/03 sections were not re-triaged by this pass — they postdate
the original audit and were written against current code.

**Overall:** the code is structurally sound. The *game* is not yet coherent. `Domain/` is
genuinely engine-free, all 41 `onClick.AddListener` calls are correctly in `Start()` (no
double-subscription anywhere), and the combat HP write-back is correct on all six fight-exit
paths. The problems are concentrated in (a) the currency economy, (b) `FightController` as a
god object, and (c) a cluster of UI hit-testing/layout bugs.

---

## P0 — Live bugs, player-visible

### ~~1. `starting_gold_boost` is an infinite money printer~~ — fixed in `4d5d302`: `RunState.grantedEmbers` records the free grant; `EndRun(bankCurrency: true)` spends it back out before banking, so it's usable in-run but never converts to permanent Gold
`GameplayManager.cs:113-116` grants +50 in-run gold at `StartRun()`. `DungeonController.cs:29`
retreats with `EndRun(bankCurrency: true)`, banking that gold — and Retreat is available on the
Dungeon screen *before entering any room*. So: pay 30 once, then loop `Start Run → Retreat` for
**+50 net gold per two clicks, zero risk, unbounded.** Pays for itself 167% on the first cycle.

By the time this was fixed, `DungeonController` no longer existed — Retreat had moved to
`MapController`, and the exploit mechanism (StartRun grants, EndRun banks unconditionally) was
otherwise unchanged. `Wallet`'s own header comment already claimed to have fixed this; it had
only fixed the unguarded-`+=` half, not this one.

### 2. The whole economy is incoherent by ~2 orders of magnitude
**Open by design — the numbers below were re-measured 2026-08-04; nothing was retuned.** Three
interacting dials (Ember income, the `0.055`/step depth curve, tree size vs `ExpToNextLevel`) plus
"invent two sinks" is a balance decision, not a bug fix.

Every figure in the original finding is now wrong, and **both halves moved the wrong way**:

| | Recorded 2026-07-28 | Measured 2026-08-04 |
|---|---|---|
| Gold per clear | ≈295 | **≈174** (leg 1) / **≈636** (16-step descent) |
| Entire store contents | 80 gold | **110 gold** |
| Runs to afford the store | 0.27 | **0.63** (leg 1) / **0.17** (full descent) |
| Talent nodes per character | 30 | **315** (63 × 5 characters authored) |
| Runs to fill ONE tree | ≈131 | **≈264** |

So gold got *more* abundant relative to the store, and the EXP grind roughly **doubled** — the
tree grew tenfold while the curve did not move. Filling one character is 60 levels ≈ 183,000 EXP.

**New, and not in the original finding: `Relics` is a second sinkless currency.** It is earned
(a boss clear pays 1, straight onto the save so it survives a defeat) and displayed in four
places, but **nothing anywhere spends it** — the three authored relics have no cost and are
simply equipped. Gold at least has a store it exhausts; Relics has no sink at all.

Item redundancy is unchanged: a 30% per-enemy consumable drop plus a guaranteed weapon on every
elite AND boss, against 15-gold potions.

### ~~3. A purchased upgrade doesn't apply until you exit to the Main Menu and come back~~ — fixed in `b19d8a5`: `StoreController` calls `save.Reconcile()` on the purchase itself (`Reconcile` promoted private→public for it), so the squad grows the moment the slot is bought. `MetaProgressionTests` no longer calls `SaveSystem.Load` itself, and `PrincipalityStoreTests` now clicks the real Store button — the two tests that were validating `Reconcile` while assuming the trigger this finding proved absent
`SaveData.Reconcile()` is the only thing that tops up `selectedCharacterIds`, and it runs only
from `SaveSystem.Load` — whose only `Save`-assigning caller is `GameplayManager.Awake`
(`GameplayManager.cs:38`). So buying Extra Recruit Slot (50 gold, 62% of the store's content)
shows "(Owned)" and then **the squad is still 3 characters** for the rest of the session.

`MetaProgressionTests.cs:40-51` passes because it calls `SaveSystem.Load` itself, under a comment
asserting a real-flow trigger "somewhere in the flow" that **does not exist**. The test validated
`Reconcile` and then assumed the thing that was actually broken.

### ~~4. Delete-slot confirmation is black text on an 85%-black panel~~ — fixed in `b19d8a5`: the confirm label gets an explicit white override, and the class of bug was closed alongside the instance — `CreateButton` now whitens every button label rather than leaving each panel to remember the override this one forgot
`SceneBuilder.cs:236-237`. `confirmPanel` forced to `(0,0,0,0.85)`; `confirmLabel` inherits
`CreateText`'s hardcoded `Color.black` (`:998`). Composite luminance ≈38/255. Every other panel
that needed it got an explicit white override (`:313`, `:413`, `:521-523`) — this one was missed,
and it's invisible in the Editor because its initial text is `""`. The unreadable copy is
*"Delete Slot N? This cannot be undone."* — the confirmation gate on a destructive action.

### ~~5. Dialogue "click anywhere to advance" is dead over ~80% of the screen~~ — fixed in `b19d8a5`: `raycastTarget = false` on the box, the text and both portraits, exactly the four this finding named, leaving `ClickCatcher` as the only thing in the layer that takes a click
`SceneBuilder.cs:848-857` creates `ClickCatcher` first (correctly bottom-most), but
`DialogueBox`, both portraits and `DialogueText` are created after it and all keep
`raycastTarget = true`. Unity resolves a click by walking **up** from the topmost hit only —
siblings are never consulted — so a click on the box, either portrait, or the text is swallowed.
Live region is roughly the top band minus the portrait columns. Both `SceneBuilder.cs:845-847`
and `DialogueController.cs:21-23` claim the opposite. Fix: `raycastTarget = false` on those
four, exactly as `AddArrowIcon` already does at `:117` (the only such call in the codebase).

### ~~6. Character Select is already broken at the current roster of 5~~ — fixed in `578ba9e`: ConfirmButton's y is now DERIVED from where the character column actually ends, so no roster size can collide with it, and the new shared `AssertColumnClears` helper is the backstop. Verified by reintroducing the hardcoded y and watching the build fail with this finding's own numbers
`SceneBuilder.cs:289` + `:294`. Character button 4 spans y[−180,−240]; ConfirmButton spans
y[−190,−250] at the same x — **50 of 60px overlapped**, Confirm is the later sibling and takes
the clicks. Deriving the *count* from `ContentDatabase` while leaving the 90px step and Confirm's
y=−220 hardcoded is what created the collision. Currently masked only because nothing calls
`ShowOverlay(CharacterSelect)`.

### 7. Store layout breaks at 5 entries per column — two away — **guard added** in `578ba9e`, layout unchanged
`SceneBuilder.cs:441,451,456`. Entry i sits at y = 20 − 80i, so **i=4 lands dead-centre on the
Back button** with 60px of x-overlap, and Back wins the raycast. There are 2 upgrades and 2 items
today. This is the one system whose stated design point is "adding an upgrade is an asset
change" (`StoreController.cs:7-9`). No scroll, no pagination, step not derived from count.

The COLLISION can no longer ship silently — the Store routes through the shared
`AssertColumnClears` (promoted from its own hand-written copy of the same check), so a fifth
entry fails the build naming both the column and the Back button. The underlying design gap
is unchanged and still open: there is still no scroll and no pagination, so the fix when it
fires is "reduce the spacing or split the column", not "the Store handles it".

### ~~8. Store-bought items are destroyed by a mid-run quit~~ — fixed in `5dbb536`: the stockpile clear + persist moved out of `StartRun` into `EndRun` (both outcomes), exactly the narrow fix this finding names
`GameplayManager.cs:97-103` copies `stockpiledItems` into the run, clears the list, and
persists **immediately at run start**. From that instant they exist only in the non-serialized
`RunState`. Force-quit mid-run ⇒ items gone from disk, never used, unrecoverable. Same for a
voluntary Retreat, which banks the run's *gold* but silently drops unspent *items* — while the
Store labels them "(x5 ready)", which reads as banked. Narrow fix: don't clear the stockpile
until `EndRun`.

---

## P1 — Correctness and robustness

### ~~9. One reachable hard soft-lock~~ — fixed in `8832a72`: `BuildEncounter` now reports whether it managed to field a fight and checks BOTH sides, and the two dead-end paths share one `ShowUnfightableRoom` helper that always leaves Continue as a way out. Non-vacuity proved by reverting the guard and watching three new tests fail with this finding's own `ArgumentException`

Re-verified before fixing, and most of the account above had gone stale — worth recording, because
two thirds of the severity was in the parts that were no longer true. The squad-resolution
disagreement the reachability argument rested on was unified into `SaveData.ActiveSquad()` by
`0e7e0a7` (see #12), so the unresolvable-id route is gone. `IsSquadWiped` had since started
catching the player-side-empty case in `OnEnable` with a graceful Continue. And "only exit is
killing the process" stopped being true when `PauseMenuController` landed — Escape works, it just
costs the run.

What survived was narrower and entirely real: **the enemy side had never been guarded at all.**
Empty enemy content, or a roster where everything is inactive, produces no picks and throws. The
fix guards the player side too, because `IsSquadWiped` deliberately returns false when there is no
run in progress, so a direct dev/test entry with an empty roster still reached the constructor.

### ~~10. `SaveSystem.Save()` is not atomic and a torn write wipes the profile~~ — fixed in `20eb23c`: writes to a sibling `.tmp` then `File.Replace`/`File.Move` onto the real path, wrapped in try/catch that leaves the previous file untouched on failure
`SaveSystem.cs:61-65` is a bare `File.WriteAllText` — no temp-file-then-move, no backup, no
try/catch. `Load()`'s fallback then silently returns `CreateNew()` on a corrupt file with only a
`Debug.LogWarning`. The fallback is right; it's only *safe* if the write couldn't have torn.
`Save()` is called on every fight victory, purchase, and talent click.

### ~~11. Elite scaling collides with flat-subtraction damage~~ — fixed in `8f7ab4d`: defense now scales by its own, much gentler `EliteDefenseMultiplier` (1.15) instead of the full stat multiplier (1.56)
`FightController.cs:26` applies ×1.3 to the whole `StatBlock` **including defense** (Stone Golem
def 8 → 10), and all damage is `max(1, attack − defense)` with no variance and no crit. Against
2 Elite Golems the entire authored roster is floored to 1 damage (Fly 9, Dog 6, Sheep 5, Turtle
4). 92 enemy HP at ~3/round while they out-damage the party. Losable, not soft-locked — but a
purely content-rolled unwinnable grind with no floor or cap on the multiplier.

By the time this was fixed the multiplier had actually been bumped to 1.56 (not the 1.3 measured
here — see commit `5ddb3aa`), making the mechanism worse, not better, and matching playtest
feedback that elites "completely clap you". New `StatBlock.ScaledForElite(multiplier,
defenseMultiplier)` in Domain; elites still scale attack/HP/etc by 1.56x, defense only by 1.15x.
Two new `StatBlockTests` pin the split. The overall multiplier value and whether a damage floor
independent of it is also warranted remain open tuning questions — this fix addresses the
*mechanism* the finding named, not a claim that elite difficulty is now perfectly tuned.

Follow-up, `402f450`: the defense-only softening above closed half of "completely clap you" —
playtesting reported it again after this fix landed, because nothing had softened the OTHER side
of the same flat-subtraction mechanism. An un-softened attack multiplier passes straight through
into what the elite deals just as directly as an un-softened defense multiplier suppresses what
the player deals. `ScaledForElite` now takes a third `attackMultiplier` parameter (also 1.15,
mirroring `EliteDefenseMultiplier`) instead of reusing the general 1.56x for attack.

### ~~12. Squad resolution is spelled 6 times in 3 different ways, and `StartRun` is the odd one out~~ — fixed in `0e7e0a7` (predates the 2026-08-02 cleanup pass, confirmed still holding as of that pass): every call site now resolves through `SaveData.ActiveSquad()`, `StartRun` included
`GameplayManager.cs:74-77` (no roster fallback) vs `FightController.cs:146-153`, `:131-136`,
`DungeonController.cs:85-91`, `TalentController.cs:56-64`, `CharacterSheetController.cs:64-72`
(all fall back to the full roster). With an empty selection, `StartRun` seeds `currentHealth` for
nobody while `BuildEncounter` builds a **5-member party at full HP**, and `IsSquadWiped` can
never fire. None of the six consult `EffectiveMaxSquadSize()`; none dedupe ids.

`DungeonController` no longer exists by the time this was verified fixed — its squad-reading
responsibility moved to `GameplayManager`/`MapController` along with the rest of its duties.

### ~~13. Combat message log silently drops messages the player never saw~~ — fixed in `a64fd47`: `AppendMessage` now attaches post-action lines to the last queued beat instead of writing them straight to the buffer, so a consequence can no longer be older than its cause. The two remedies this finding suggested were both wrong, and it matters why — see below

The cap (4 → 8) and per-beat rendering had already landed and were **not** enough, which is what
makes the two fixes suggested above the wrong ones. The real defect is an ordering inversion, not
a capacity problem.

A turn's beats are queued as it resolves and drained only at the very end of `AdvanceAfterAction`,
by `StartBeatPlayback()`. Everything decided between `CommitBeat()` and that call — Bloodlust's and
Brave's extra turns, and the whole victory block `EndFight` emits — was written into the buffer
*at that moment*, which is **before** playback appends the beat messages describing the blow that
caused it. Since the buffer trims from the front, a consequence was discarded to make room for its
own cause. Raising the cap only buys headroom; the log still told the story backwards, and any
sufficiently eventful turn overflowed it again.

**This is also the root cause of #24**, which is why that investigation kept failing — see it.

Guarded by `CombatLogOrderingTests`, which pins the *ordering* (Victory must read below the kill
that earned it) rather than any one message's survival, precisely so that raising the cap can never
paper over a regression here again.

### ~~14. Missing art renders as a solid white quad, not "nothing"~~ — closed by `fb089b7`, which guarded the last two sites (both in `SceneBuilder.Dialogue.cs`) and added `WhiteQuadTests` to sweep both built scenes for the whole pattern rather than for a list of sites

Most of this had been fixed incrementally: `AddBackground` gained `MissingBackgroundColor` (the
full-screen-white case, and the comment there records that the fallback used to *be* white, which
is how the screens went white-on-white), and the widget factories, the hub slots, the map tiles
and the runtime sites all picked up the rule. `ItemIcons.Apply` is where the shared version lives
now; the "one line the other four want" pointer above has moved to
`CharacterSheetController.cs:130`, which reaches it through `PortraitIcons`.

The two that were left were the dialogue box (a missing frame would have painted an opaque white
bar across the bottom third and covered the text) and the portraits (white rectangles where the
speakers go). Worth recording *how* the portrait was fixed, because the obvious fix was wrong:
`DialogueController.ShowPortrait` owns that slot from the first line onward and drives it with
`SetActive`, never touching `Image.enabled`. Clearing `enabled` at build time would therefore have
left the slot invisible **forever** exactly when real art later arrived — two mechanisms
disagreeing. The builder now deactivates the GameObject, matching the runtime owner.

The sweep is the durable half. This finding has been fixed one site at a time for months; a test
that walks both scenes for "no sprite, opaque white, currently drawn" is what stops an eleventh
site being found by a player instead. It passes on the current scenes, so it is a regression guard
rather than a bug report — its traversal was verified by temporarily dropping the sprite check and
watching it report eleven real Images with their full hierarchy paths.

### ~~15. Two `IndexOutOfRange` crashes on empty content~~ — fixed in `1a32de1`: `CurrentCharacter` returns null out of range in all three (a third site, `RelicsController`, was found alongside the original two during the 2026-08-02 cleanup pass), every caller early-returns on null
`TalentController.cs:76` and `CharacterSheetController.cs:84` index `_squad[_currentIndex]` with
no empty guard, falling back to `save.roster` which can itself be empty. Every other
content-missing path in the codebase is deliberately graceful; these two hard-crash from
`OnEnable`, and the Hub still renders the buttons that trigger them.

### ~~16. Three rounding conventions in one damage pipeline~~ — fixed in `e268739`: unified onto `Domain/Stats/Rounding.AwayFromZero`, the convention `CombatMath` already argued for — not onto `Mathf.RoundToInt`'s to-even, which is what this finding's own wording pointed toward
`CombatMath.cs:54-57` uses `MidpointRounding.AwayFromZero` under a comment calling itself "a
stand-in for `Mathf.RoundToInt`" — which is `ToEven`, i.e. it does the opposite of what it says.
`StatBlock.Scaled` (`:73-76`) uses bare `Math.Round` (ToEven). `FightController.cs:300,365`
use `Mathf.RoundToInt`. Divergence needs `attack ≡ 3 (mod 4)`; no base character hits it *by
luck*, but attack-granting talents will (Sheep 5+2=7 ⇒ 10.5 ⇒ 11 vs 10). Same shape as the flake
already fixed once in `d183899`, one layer over.

The direction taken here diverges from this finding's own implication (that `Mathf.RoundToInt`
was the "correct" convention CombatMath's private helper failed to match). `CombatMath.
ApplyEffectiveness` carries its own detailed, reasoned comment arguing FOR away-from-zero
specifically — a 25-point packet into a resistance is exactly 12.5, the tie case, and the
comment treats rounding that up as the deliberate, correct behavior. That comment predates this
fix and was easy to miss on a first read of just the "stand-in" comment alone; unifying onto
`Mathf.RoundToInt` instead would have overridden a real, already-argued design decision rather
than corrected an oversight. Verified against the full suite with no pinned-literal changes
needed — float32 precision keeps real game multipliers just off the exact `.5` boundary either
way (see `FightControllerTests.expectedEliteMaxHealth`'s own comment) — so `RoundingTests.cs` and
one exact-tie case in `StatBlockTests.cs` were added to cover the tie itself rather than trust
that.

---

## P2 — Tests that give false confidence

**267 passing tests, but:**

### ~~17. Tank lifesteal test asserts `1 == 1`~~ — fixed in `be43a8a`: the arithmetic that made it vacuous stopped holding when `CombatMath.DamageScale` (×10) arrived, so the branch had quietly gone live again with no code change and nothing to announce it. Re-verified by hand and then pinned: damage is 10 (Attack 4 × 1.5 = 6, less Defense 5, × scale 10; Ice into an Arcane-weak/Fire-resistant boss is exactly ×1.0), lifesteal 3, final HP 4. Non-vacuity **proved** by zeroing the lifesteal in production and watching this test — and only this test — fail 4-vs-1

Two further tautologies in the same test were fixed alongside the strike, since both are the shape
this section exists to catch. It computed `expectedDamage` by calling `CombatMath.ComputeSkillDamage`
— the same function production calls — and then applied the 0.3 fraction with `Mathf.RoundToInt`
while production uses `Rounding.AwayFromZero`. The first moved with any change to the formula; the
second was correct only by accident, because damage is always a multiple of `DamageScale` so the
fraction never lands on a `.5` tie. Both are gone: the assertion is now a literal `4` with its
derivation written out for whoever a stat change breaks it for. Note the residue this does *not*
fix — the effectiveness multiplier is 1.0 only because the pinned Warden happens to be neutral to
Turtle's Ice, so changing either element makes the number wrong rather than making the test fail
loudly. The comment says so.

### ~~18. Elite scaling test is a tautology~~ — fixed in `8f7ab4d`: expectations are pinned literals in a dictionary (golem 350→546, bog_witch 160→250, rat 90→140) with a completeness guard that fails if any non-boss enemy lacks an entry, so adding an enemy cannot silently escape the check. The method under test is now `StatBlock.ScaledForElite`, which also picked up direct EditMode coverage. Worth keeping the commit's own warning in view: the literals are **measured, not arithmetic** — this finding's suggested value for the golem (46 at 1.3×) was itself wrong, the real answer was 45

### ~~19. `Assert.Ignore` randomly skips the entire victory-reward path~~ — **OBSOLETED BY THE REBUILD**, struck 2026-08-17. The instance is gone; a different-mechanism residue is refiled as #46
This is a **v1** finding — see the box at the top. `FightControllerTests.cs` has **never existed
in this repository** (`git log --all`), so the specific `Assert.Ignore` at `:339-343`, the losing
draw that tripped it, and the unseeded `UnityEngine.Random` behind it are all v1. v2 seeds its
combat draws independently — `VictoryRewards.RollConsumableDrops` takes a `SeededRandom` and
rolls in roster order specifically so a seeded run reproduces its own loot.

**What did carry across is the *shape*, not the cause,** and it is different enough to deserve its
own number rather than a strikethrough that quietly widens: v2 has 20 `Assert.Ignore` calls, and
none of them are RNG. Refiled as **#46**.

**Original finding, kept:** `FightControllerTests.cs:339-343` bails out when the randomised fight is lost, skipping currency
gain, EXP gain, the victory bark, and the return-to-Dungeon assertions. Its justification
("covered by the defeat-path assertions elsewhere") is **false — there are none**; nothing covers
`EndFight`'s defeat branch or `OnContinuePressed`'s `EndRun(false)`. And losing draws are common
(two Stone Golems is a guaranteed loss, see #11). `UnityEngine.Random` is never seeded anywhere
in the suite.

**Re-verified 2026-08-04, and the coverage half is now closed.** `EndRun(false)` had since gained
coverage via `DungeonRoomTests`' squad-wipe case, and `c64a90b` adds `DefeatPathTests` for the branch
that was genuinely untested: a squad that dies *during* a fight, which is how a player actually
loses a run. It pins both the defeat message and the forfeit — Embers earned in a lost run must
not bank into Gold — which is also the invariant C2's run persistence must not quietly undo.

What remains is the `Assert.Ignore` itself, and it is a symptom rather than the disease: the suite
has 15 of them, all in PlayMode, all because combat RNG cannot be seeded. The fix is the seed
wiring ("Systems worth building" #3), after which these become deterministic setups instead of
retry-and-skip. Note also that the Phase 6 observation of `ContinueAfterVictory` reporting
**Skipped** on one parallel run, filed under #24 at the time, is a sighting of *this* finding — a
randomised defeat hitting the `Assert.Ignore` is exactly what that looks like.

### ~~20. `ContentDatabaseTests` — the canary has a blind spot on 2 of 5 content types~~ — closed by `c64a90b`, which added the last two canaries and put Relics into `ValidateContent` for the first time

The original two (Enemies, Items) had been closed earlier. Re-verifying found the finding had
**quietly reopened on the types added since**: Relics and Skills both arrived with no canary, and
Relics were entirely absent from `ValidateContent` — including its cross-type id-uniqueness check,
which is the one that matters most, since ids are what saves store and a relic colliding with an
item surfaces later as a save resolving to the wrong definition.

Relics were the worse of the two for exactly the reason this finding was written: the only thing
that would have noticed an empty relic tree was `RelicsScreenTests` indexing `Relics[0]` and
throwing `IndexOutOfRange` — the unhelpful failure the file exists to replace with a sentence.
`SpellTiers` also joined the id/uniqueness sweeps.

Worth noting for next time: the shape of this finding is "a rule that has to be re-applied by hand
to every new content type", which is why it reopened rather than staying fixed. It will reopen
again on the next type added. A canary generated per type, rather than written per type, is the
version of this that would actually stay closed.

### 21. ~34% of the EditMode suite tests code with zero production callers
`Domain/Grid/` (8 files, 34 tests), `Domain/Events/EventBus` (13 tests), plus ~17 more on
unreachable API (`TurnOrder.DelayCurrent/PeekNext/RemoveCombatant`, `CombatMath.CanUseSkill`,
most of `SeededRandom`). Verified by grepping all of `Assets/` including scenes: **no hits.** Not
arguing for deletion — but it inflates the number in a way that reads as "combat and movement are
well covered" when there is no movement layer and combat's 665-line driver has the gaps above.

~~One live bug hiding in there: `TurnOrder.DelayCurrent` (`TurnOrder.cs:119-133`) is a **no-op that
returns the same actor** when the current actor is last in the order (remove-then-append leaves
the index pointing at the same element), and never advances the round.~~ — fixed by the
charge-based rewrite of `TurnOrder`, whose own comment names this bug: list position stopped
having anything to do with turn order, so the old remove-then-append "silently did nothing".
`DelayCurrent` now drops the actor's charge below the lowest and re-runs the ready check.

**Re-verified 2026-08-04, and the headline number is badly stale.** `Domain/Grid/` and
`Domain/Events/` no longer exist (deleted in the 2026-08-02 cleanup, recorded under "Dead code
inventory"), `PeekNext` and `CombatMath.CanUseSkill` are gone, and `SeededRandom` now has real
production callers — map generation, and since `8cb8613` every combat draw. The zero-caller share
is roughly **5%** of a 690-test EditMode suite, not 34%.

The residue is two methods: `DelayCurrent` and `RemoveCombatant` still have no production callers.
Keep-or-kill is a design call — `DelayCurrent` is a plausible future mechanic and is now correct —
so it stays recorded rather than acted on.

### 22. Untested critical paths, ranked — **4 of 7 closed, status re-verified 2026-08-04**
1. ~~**In-fight defeat → run forfeited** — only reachable through the `Assert.Ignore`~~ — closed by `c64a90b`'s `DefeatPathTests`, which drives a squad wipe *during* a fight (the pre-existing `DungeonRoomTests` case covers only a squad already dead on arrival) and pins both the message and the forfeit
2. ~~**Victory item drops** — zero coverage; no test ever observes a drop~~ — closed: `FullRunTests` asserts the guaranteed Elite weapon drop. The *chance-based* consumable drop is still uncovered, and now that combat RNG is seeded it is finally pinnable rather than a 30% coin flip
3. **Elite reward multiplier** — still zero coverage; no test compares an elite payout against a normal one
4. **Mana persistence** — `RunState` still has no mana field, so MP fully refills every fight and the 15-gold Mana Potion stays near-worthless. **Deliberately not changed by the run-persistence work** (`58de5a2`): making MP persist is a balance decision, and quietly introducing it while adding save support would have been exactly the kind of silent flip this item warns about. Still asserted nowhere, so either answer can still change unnoticed.
5. ~~**`Reconcile`'s trim branch, null-guards, and `stockpiledItems` pruning**~~ — largely closed: pruning is covered by `PerCharacterTalentTests` and `SaveSystemTests`, and `65cfb11` added prune coverage for a saved run's inventory and health entries
6. **`CursorController.Apply`'s state→texture switch** — still uncovered. `DetermineState` is tested; `Apply` is not, so swapping the hover and click textures remains invisible to the suite
7. ~~**No test loads a save with a *missing field***~~ — closed by `65cfb11`: a save is written, the field is stripped back out of the JSON on disk, and it is loaded again — which is what a save written before that field existed genuinely looks like. The never-bump-`CurrentVersion` policy now rests on an assertion rather than on an assumption

### 23. Test harness leaks
- ~~`GameplayTestBase.cs:22-26` deletes `save_slot_0.json` from the **real** `persistentDataPath` after every test — running the suite destroys the developer's first save slot.~~ — fixed in `9166e85`: `SaveSystem.RootOverride`, a test-only static, redirects every `SaveSystem` call to a temp-directory sandbox while `GameplayTestBase`-derived tests run. `SaveSystemTests.cs` (slot 4, doesn't inherit `GameplayTestBase`) has the same underlying issue on a different slot and was deliberately left unfixed — its `SlotPath` test helper computes the real `persistentDataPath` directly rather than through `SaveSystem.PathForSlot`, so redirecting one without the other would desync what it writes from what `SaveSystem.Load` reads.
- ~~Four statics are never reset (`ContentDatabase`'s caches — `Reset()`/`Initialize()` exist and *no test calls either*; `SaveSlotManager.CurrentSlot`; `GameplayManager.Instance`; `BarkController._instance`). `LoadScene` runs `Awake`→`Load(CurrentSlot)` **before** `EnterHubWithFullSquad` sets `CurrentSlot`, so a test reads whatever the previous one left.~~ — the `ContentDatabase` half was fixed separately (`ContentDatabaseTests` and `ItemDescriptionTests` both call `Reset()` in `[TearDown]`, so "no test calls either" is stale), and the `CurrentSlot` ordering hazard is fixed in `c64a90b`: it is now pinned in `BaseSetUp`, not merely cleared afterwards. That distinction is the whole fix — only setup runs before `LoadScene`, and `LoadScene` is what triggers the `Awake`→`Load(CurrentSlot)` this finding describes. `GameplayManager.Instance` and `BarkController._instance` are deliberately still not nulled: `Awake` reassigns both on every scene load, so the exposure is limited to tests that load no scene, and the one test that did depend on it is fixed below rather than papered over globally
- ~~`BarkControllerTests.cs:96-103` is *defined* by leaked state: it claims to test the null-guard path but `_instance` is always live under the current ordering, so that path is never exercised — and under a different order it points at a *destroyed* object and the test goes red for unrelated reasons (`?.` is reference-null, not Unity's destroyed-object null).~~ — fixed in `c64a90b`: the test now clears `_instance` itself instead of hoping the ordering supplies a null. It never did, so what it had actually been asserting was that `Show()` on a *working* bark controller does not throw — true, but not its own name, and the branch it exists to protect had never once executed

---

## Rebuilds vs patches

**REBUILD 1 — Combat action resolution belongs in `Domain`.** Not because there are two competing
models, but because there is **none**. `CombatEncounter` is a turn cursor; `CombatMath` is six
pure helpers; every actual *rule* lives in a 665-line MonoBehaviour that also owns nine
`[SerializeField]`s and the message log: mana gating (`:246`), execute multiplier (`:291-301`),
all role effects (`:353-387`), enemy AI (`:484-511`), rewards/drops/level-ups (`:534-578`).
Concrete cost today: role behaviour can only be tested by loading a scene and mashing buttons
(`AdvanceUntilItIsThisCharactersTurn` is a 20-iteration loop just to reach one character's turn),
and the next obvious features — per-character skills, status effects with duration, enemy AI
variety — have nowhere to go but more cases in that file. `CombatAction.Item` and `.Run` are
already fiction: never constructed anywhere.

**REBUILD 2 — Role/Skill effects → data.** The four role numbers now exist in **five** places:
`FightController.cs:32-36`, `CharacterSheetController.cs:89-105` (as *prose*, with a comment
admitting the sync is manual), and three hardcoded "must match" copies in tests. Also
`CharacterRole.Utility` and `.Support` both fall through to the same party heal, so Sheep and Owl
have mechanically identical Skills — five roles, four behaviours — and
`SupportSkill_HealsTheWholeLivingSquad` actually drives *Sheep*, so the real Support role has no
coverage at all.

**REBUILD 3 — The economy.** Numbers, not code. See #1, #2. Needs a sink an order of magnitude
larger or ~10× less run income, and the gold-boost/retreat loop closed before either matters.

**KEEP, don't rebuild:** `TurnOrder`'s core (correct, well-tested — just fix or delete the unused
half), `CombatMath`, `CombatantState`, `DungeonGenerator`, `SaveData.Migrate/Reconcile`,
`SaveSystem.Load`'s fallback, and the HP write-back — which is complete on all six exit paths.

---

## Systems worth building (highest leverage first)

1. **Content validation pass**, run from `BuildAllScenes` and asserted in a test: ids non-empty
   and unique *across all five types*, no zero-HP enemies, costs within sane bounds, every
   `grantsStartingItemId` resolves. Would have caught #20 and prevents the whole class.
2. **Layout assertion in `SceneBuilder`** — a shared `CreateButtonStrip(...)` that asserts the
   last element's rect doesn't cross a reserved band. This is the single change that prevents #6,
   #7, and the four other latent count-vs-footprint collisions, rather than fixing one. There are
   11 near-identical strip loops today; two are line-for-line duplicates.
3. ~~**Wire `SeededRandom` through combat and store the run seed on `RunState`.** It was built,
   fully tested, and then not connected to anything — `GameplayManager.cs:105` seeds the dungeon
   from `UnityEngine.Random` and *discards the seed*, so nothing is reproducible. This is also
   what makes #19 fixable.~~ — done across `4494576`, `c1afd84`, `8cb8613`. Partly stale when
   re-verified: `SeededRandom` *was* by then driving map generation, so "not connected to
   anything" had stopped being true; what remained was that the seed itself was rolled into a
   local and dropped, and that all sixteen combat/run draws still went to `UnityEngine.Random`.

   The shape is worth recording because the obvious version does not work. Streams are **derived
   per position** — `RngStreams.Derive(runSeed, purpose, step, node)` — rather than drawn from one
   live run-long generator. A single generator makes every draw depend on the whole play history,
   so resuming would have to persist generator state, and any change to how many numbers a fight
   happens to consume would silently invalidate every existing save. Keyed by position, the state
   *is* the position: the fight at a given node always plays the same, a resumed run cannot replay
   a reward already taken (taking it advanced the step), and a mid-fight quit re-fights rather than
   rerolls. Purposes get disjoint streams so that adding an enemy to content cannot shift the map.

   This is what item 4 below is built on: with the seed kept, a run's map does not need serializing
   at all — it can be regenerated identically from (seed, legStartStep).
4. ~~**Serialize `RunState`** (it's already a flat POCO) + a pause/quit path. Fixes #8 and makes a
   run resumable, which is table stakes for the genre.~~ — done in `65cfb11` and `58de5a2`.

   "Already a flat POCO" was not true and had not been for a while: `RunState` holds a readonly
   `Wallet`, a readonly `HashSet`, a `Dictionary` and a `DescentMap`, none of which JsonUtility
   writes. It stays that way. A separate `RunSnapshot` DTO flattens at the boundary instead,
   because RunState's own header declaring it in-memory-only is a design contract worth keeping
   rather than an obstacle.

   The map is **not** serialized at all — it is regenerated from (`runSeed`, `legStartStep`), which
   is what item 3 above was for. So the resume format is small, and a content update between
   quitting and resuming cannot corrupt an in-progress run's geography.

   Three things that are easy to get wrong and are pinned by tests rather than by care:
   `hasRun` must be an **in-band** flag, because JsonUtility instantiates missing object fields and
   a null `activeRun` is never null again after one round trip. `EndRun` must clear `CurrentRun`
   **before** persisting, or a defeat is written as a resumable run and losing becomes free.
   And the run must be persisted **on arrival** at a room, before it resolves — otherwise quitting
   mid-fight resumes at the fork with both branches open, which is a free reroll of a choice
   already made.

   No `CurrentVersion` bump: the field is additive, and an older save loads with `hasRun` false,
   which is exactly right for a save written before runs were resumable.

   Deliberately out of scope, and recorded rather than fixed: a quit on the reward screen replays
   a fight whose EXP has already been persisted (`ResolveVictory` persists before `AdvanceRoom`
   runs). Bounded to one fight's EXP, and the run-currency stake is unaffected since the snapshot
   is pre-fight. The clean fix drags `PendingReward`/`PendingOffers` into the snapshot for very
   little. Mana is likewise still not persisted — see #22's item 4, which stays open by design.
5. **A `SetField` null-check** (`if (prop == null) throw` naming the field and controller) — and
   note that a throw mid-build leaves the **previous scene on disk**, so a headless pipeline that
   swallows the failure ships a stale scene.

---

## Also worth knowing

- **`ContentBuilder` deletes all of `Resources/Content` with no prompt**, while all five
  `*Definition` types carry `[CreateAssetMenu]` actively inviting a designer to author assets
  into exactly that folder. The "hand-authored assets belong in a folder this tool does not own"
  mitigation named in its own header comment **does not exist** — `ContentDatabase` loads from
  five paths, all inside the deleted tree.
- **Content GUIDs have held across ~5 regenerations but that is luck, not API guarantee** (Unity
  recycles a path→GUID map within one Editor session). Safe *only* because nothing references a
  definition by GUID — verified 0 hits in both scenes. The first `[SerializeField] private
  ItemDefinition` converts a rebuild into a silently-nulled reference. Same mechanism as the
  `dialogue_box.png` bug already fixed in `52e90a8`.
- **`SaveData.exp` is dead** — written by nothing, but still rendered, so every save-slot label
  permanently reads ", 0 exp".
- **`SaveData.version = CurrentVersion`** as a field initializer means a genuinely unversioned
  file deserializes as version 1, so the `version < 1` discard branch can never fire on real
  data. Not bumping `CurrentVersion` for the recent additive fields was the *right* call (all
  additive, benign defaults, and no per-version migration table exists) — but the version field
  itself doesn't do what it looks like it does.
- **`BarkCanvas.sortingOrder = 500` is inert** — a nested Canvas ignores it without
  `overrideSorting = true`, which appears nowhere in the codebase. The bark renders on top purely
  because `BuildBarkController` is called last. Works today; breaks silently if a panel is ever
  appended after it. Its background also blocks clicks for 2.5s (`raycastTarget` left true).
- ~~**`FightController.cs:640` hardcodes `"health_potion"`** for the party panel's potion count, so
  a player holding 3 Mana Potions reads "Health Potions: 0" — and `OnItemPressed:403` drinks
  `inventory.FirstOrDefault()`, so they can't choose which. The "only one item type in practice"
  assumption that comment flags has already expired.~~ — gone as of the 2026-08-02 cleanup pass's
  verification (no fix commit identified; the party panel's item display was rebuilt into the
  generic Satchel/submenu list somewhere in the intervening feature work, which subsumed this
  rather than fixing it directly).
- **Same slot label written 4 different ways** (`SceneBuilder.cs:226`, `SaveSlotController.cs:32`
  and `:37`, `ResetProgressController.cs:50-52` — the last drops exp), so Options and Play
  describe the same slot differently. ~116 user-facing string literals total, split between
  SceneBuilder (initial text) and controllers (runtime text) with nothing tying them together.
- **Dead code inventory:** ~~all of `Domain/Grid/` (~350 lines, 8 files), all of `Domain/Events/`
  (~84 lines)~~ — both directories no longer exist as of the 2026-08-02 cleanup pass (deleted in
  intervening feature work, not by that pass — verified zero references before crediting this).
  `StatType` + `StatBlock`'s indexer, ~60% of `TurnOrder`'s API, most of `SeededRandom` remain as
  described. `ContentDatabase.GetEnemy` no longer belongs on this list — it has a real caller
  (`BossSelectionTests.cs`), verified during that pass. That pass separately deleted `RoomStepper`
  (~65 lines, orphaned per its own header comment) and 8 further zero-caller members; see
  "Cleanup-pass findings, 2026-08-02" below for the full account, including one member
  (`ItemPowerBudget`) an over-eager first scan flagged as dead that turned out to be load-bearing
  content-validation infrastructure (`WeaponBalanceTests.cs`), not a false start worth repeating.

---

## Cleanup-pass findings, 2026-08-02

Found during a mechanical simplify/dead-code/bug-fix pass, not the original five-reviewer audit.
Same rule applies: struck through in place when fixed, never deleted.

### 25. ~~`RoomStepper` and 8 further members had zero callers anywhere~~ — fixed in `8f8200f`
`Domain/Dungeon/RoomStepper.cs` (orphaned per its own header comment, exactly the "tests covering
code with no production callers" shape #21 describes) and its test were deleted outright.
`ContentDatabase.GetSkill`/`EquippedItems`, `EquipmentService.SlotsFor`,
`ItemSlotView.SimulateHoverExit`, `SplashController.HasShownThisLaunch`,
`EquipmentLoadout.ClearAll`, `ScalingGrade.Lowest`, and `SpeedScale.TurnsPerBaselineTurn` had no
caller in production or test code, verified by grepping all of `Assets/`. `ItemPowerBudget` was
flagged the same way by an early automated pass and is **not** on this list — see the correction
above.

### 26. ~~24 public members were reachable only from their own declaring file~~ — fixed in `814c760`
Tightened to `private`. Two the same pass initially flagged (`FightController.StageBeat.
HasSpellAnimation`, `MusicController.Voice.SetVolume`) had to stay `public` — both live on a
`private sealed class` nested inside the type that calls them, and C#'s nested-type access is
one-directional (a nested type reaches its enclosing type's privates, never the reverse), so
`private` there broke the build. Marking them public leaks nothing since the containing type
itself is already private.

### 27. ~~The character-tab switcher was implemented 3 times, byte-identical~~ — fixed in `2a3115e`
`CharacterSheetController`, `RelicsController`, and `TalentController` each carried the same
three loops over `characterTabButtons`/`characterTabLabels` (click-wiring in `Start`, show/hide
in `OnEnable`, colour+label refresh in `Refresh`) — nine loops total, collapsed to one call each
via a new `CharacterTabStrip` static helper. Each screen keeps its own active/inactive colours,
which are genuinely per-screen palette rather than shared state. Runtime counterpart to
`SceneBuilder.CreateCharacterTabsLeftAligned`, which had no matching runtime-side factoring until
now.

### 28. `ContentDatabase.cs` (1121 lines) and `FightController.Hud.cs` (1036 lines) had outgrown one file — partially addressed in `e07306c`, `7f8f643`
Pure-move splits, no behavior change. `ContentDatabase` → root (caches/accessors/lookups) +
`.Validation.cs` (`ValidateContent`, 312 lines, one method) + `.Effective.cs` (the
`EffectiveStats`/`EffectiveAbilityScores`/`ActiveLoadout` family). `FightController.Hud.cs` → the
world-state half (party plate, enemy plates, stage, initiative tracker) stays; the command-menu
half (`RefreshCommandColumns`, submenu rows, detail panel) moves to a new `.HudMenu.cs`. Neither
of these was flagged by the original audit — both simply grew past a comfortable size during the
stat-scaling/equipment/relic phases that followed it. `FightController.Actions.cs` (962 lines) and
`.Hud.cs` even post-split (686 lines) are the next candidates if this gets revisited; not split
here, since neither had an obvious single seam the way Hud's menu/world-state split did.

Re-measured 2026-08-04: `.Actions.cs` **965**, `.Hud.cs` **690**, `.HudMenu.cs` 362,
`ContentDatabase` family **1141** across three files (so the split moved the lines without
reducing them, which was the point), `SceneBuilder.Fight.cs` **840** and `.Widgets.cs` **829** —
both now larger than anything the original finding named. Still not split: everything in this
pass touched `FightController`, and a pure-move split landing in the middle of that would have
made every other diff unreadable for no behavioural gain.

### ~~29. 94 `new GameObject(...)` call sites across `Editor/` share the same 5-6 line rect preamble~~ — **OBSOLETED BY THE REBUILD**, struck 2026-08-17. Not fixed: v2 never had this shape
This is a **v1** finding — see the box at the top. The migration it describes cannot be finished
because the thing being migrated is not here. The 13 files it names as remaining
(`SceneBuilder.Fight.cs`, `.FightStage.cs`, `.Overlays.cs`, `.MainMenu.cs`, `.Map.cs`,
`.Widgets.cs`) have **never existed in this repository**, confirmed by `git log --all`.

What v2 has instead reaches the same end by a different route, which is why the count collapsed
rather than shrank: layout is a declared tree in `Domain/UiKit/Screens/` and `UiEmitter` is the
only thing that turns a node into a GameObject. The rect preamble is therefore written **once**,
in the emitter, instead of 86 times across 14 files. `Editor/SceneBuilder/` now holds nine files
and not one of them lays out a screen — `new GameObject(` appears **9 times in all of `Editor/`**
(`SceneBuilder.cs` 4, `UiEmitter.cs` 4, `ScreenshotTool.cs` 1), against the 86 this finding
last counted.

One trap worth leaving marked: **`NewUiRect` still exists in v2 and is not the same thing.** The
Editor helper this finding is about is gone; the four surviving matches are `Domain/UiKit/Place.cs`,
`UiSize.cs` and two UiKit test files — a Domain-layer concept wearing the same name. Grepping for
the identifier and finding hits is not evidence that the helper survived.
`SetParent` → centre-anchor → pivot → `sizeDelta` → `anchoredPosition`, repeated at every site
that builds a UI element. `SceneBuilder.Widgets.cs` alone had 26. New `NewUiRect` helper (two
overloads: default `(0.5,0.5)` pivot, and an explicit-pivot overload for the
`CreateTopLeftText`/`CreateHudText` style outliers) replaces the 12 sites in `Widgets.cs` that
actually fit the shape. **Not** every site fits it — `AddArrowIcon`, `AddBarSegment`, `CreateBar`'s
own fill, and the internal `Template`/`Viewport`/`Content`/`Item`/`Background`/`Fill`/`Handle`
children of `CreateDropdown`/`CreateSlider` all anchor off-centre or stretch across their parent,
and stay hand-rolled rather than being forced through a helper that assumes centre-anchoring.

Scoped to `Widgets.cs` only (verified with a full `-BuildScenes` test run plus a visual screenshot
check of `OptionsPanel` and `InventoryPanel`), not the other 13 files with call sites
(`SceneBuilder.Fight.cs`, `.FightStage.cs`, `.Overlays.cs`, `.MainMenu.cs`, `.Map.cs`, etc.). Each
remaining file needs the same per-site anchor/pivot verification `Widgets.cs` took, plus its own
`-BuildScenes` + `Art/` diff + visual check — a real but bounded cost per file, deliberately not
attempted in one pass given the risk of a scene-generation regression per file touched. Worth
finishing, not something this pass silently dropped.

Re-counted 2026-08-04: **86** sites, not 94, and `Widgets.cs` is down to 15 (12 converted, the
rest being the off-centre/stretched outliers above). The largest remaining are `.FightStage.cs`
(11), `.Overlays.cs` (8) and `.Fight.cs` (8). **The 2026-08-04 triage pass deliberately did not
take these on** — it is a cosmetic migration with no known defect behind it, and each file costs a
scene rebuild plus a visual check, which is a poor trade against the work that had live bugs
attached. Recorded as still open, still worth finishing, and still not dropped.

---

## Findings from the actor frame-animation work, 2026-08-02

Surfaced while researching and building the actor sprite-frame-animation system
(`Core/StanceAnimation.cs`/`StanceAnimationLibrary.cs`, frame-stepping in
`FightController.Beats.cs`) — recorded here rather than silently fixed, per this
register's own convention, since neither is in scope for that change.

### ~~30. `FightController.FlushBeats()` has zero call sites anywhere in `Assets/`~~ — resolved as a real gap, not dead code, in `<D1>`: wired into a new `OnDisable`, which IS the "panel closes mid-playback" case its comment already described

The finding asked for a decision between the two readings before touching it, and the decision is
evidenced rather than argued: with the wiring removed, two new `FightTeardownTests` fail — a party
figure is left parked off its mark, and a spell effect keeps drawing after the panel closes. Both
are visible defects that survive into the next encounter, because `OnEnable`'s teardown clears
state but never calls `ResetToHome` or `StopImmediately`.

`OnDisable` is guarded on `gameObject.scene.isLoaded`, since it also fires while a scene is being
torn down — at which point the children `FlushBeats` repaints through may already be destroyed and
tidying the display is meaningless anyway.

One test lesson worth keeping: the first version asserted every animator sits at its own `_home`,
and failed on an *unoccupied* slot. `_home` is captured in `Awake`, and a slot the fight never
drives can rest somewhere else entirely. The invariant that actually matters is "whatever moved is
put back", so the test compares against resting position instead.
`FightController.Beats.cs:480-537`. Its own comment claims it is "still needed when a
fight ends or the panel closes mid-playback", but `OnEnable` (`FightController.cs:806-810`)
does its own `StopCoroutine` instead, and nothing else calls it — not production code,
not a test. Consequence: `spellVfx.StopImmediately()` and every `StageActorAnimator`'s
`ResetToHome()` never run in production, so a fight ending mid-playback (a panel closed,
a scene torn down) can leave a figure parked off its mark or a spell VFX still fading, with
nothing to snap it back before the GameObjects it lives on are destroyed. Likely either dead
code that should be deleted, or a real gap where its one remaining caller was removed at
some point and never replaced — worth deciding which before touching it, not folded into
an unrelated change.

### 31. Several enemies carry authored `guard`/`taunt`/`extra` stance art with no code path that ever fires it
`FightController.cs:210-213`'s own comment already flags this ("a sheet's remaining poses
... are authored but have no state to fire them yet"), confirmed now against actual files:
`Resources/Enemies/golem/guard.png` and `Resources/Enemies/rat/{extra,guard}.png` all exist,
ground-aligned and canvas-consistent with their enemy's other poses, and nothing in
`FightController` ever calls `SetCombatantStance(enemy, StanceGuard)` or an equivalent — those
stance names aren't even declared as consts. Not a bug (nothing crashes, nothing looks wrong),
but delivered art sitting unused with no design decision recorded on whether/when it's meant
to fire (a break-shield "guard" reaction? a taunt-skill pose?). Worth a decision, not a
silent fix.

Re-verified unchanged 2026-08-04 (the comment moved to `FightController.cs:210-214`; six stance
consts declared, `guard`/`taunt`/`extra` still not among them). **Left alone on purpose** — this
is the author's design call, and the 2026-08-04 triage pass explicitly excluded design decisions
from what it would fix. The only code that knows these names is test-side: `ActorArtAssertions`
lists all nine, and `StanceAnimationTests` asserts an unfired stance degrades gracefully rather
than throwing — so whichever way the decision goes, nothing breaks in the meantime.

---

## Findings from the pipeline-generalisation work, 2026-08-03

Surfaced while extending the enemy-sprite pattern (committed manifest + visual QA +
invariants over all content + resolve-once caching) to the rest of the game. Recorded
rather than silently fixed, per this register's convention.

### ~~32. Two art-path conventions coexist in content JSON, distinguished only by field name~~ — **FIXED 2026-08-17.** A v1 finding, re-verified live against v2 first, then closed as a class rather than field by field
**Re-verified before being touched**, per the box at the top: this one carried across the rebuild
intact. `EnemyEntryResolver.cs:173` was still `(raw.spritePath ?? string.Empty).Trim()` with no
convention check, and `CharacterEntryResolver` was still the only guarded type — so the finding's
count had actually got worse, not better: **nine path fields across seven resolvers**, of which two
were checked.

The conventions were confirmed from the shipped data rather than from the comments, by sweeping
every path value in `ContentData/*.json`: nine `Assets/`-relative values (`iconPath`,
`portraitPath`, `iconSheet`) against nine Resources-relative ones (`spritePath`,
`battleSpritePath`, `vfxPath`, `sfxPath`). Clean split, no exceptions, so the rule is a lookup on
field name.

**The fix is one table, not one guard per field.** New `Domain/Content/ArtPathConvention.cs` holds
the field→convention map and the error text; the seven resolvers each call `Check`. The two
hand-written checks in `CharacterEntryResolver` were **replaced** by calls rather than left in
place — two statements of one rule is #34's shape, and the copy that does not get updated is the
one that matters.

**Two things the finding did not name, both found by doing it:**

- **`iconSheet` is a path field whose name does not end in `Path`.** `RawWeaponEntry` and
  `RawSetPiece` both carry it, it is `Assets/`-relative, and `ItemSetEntryResolver.IconPathFor`
  appends `/level_N.png` to it — so a Resources-style value there generates up to eleven paths that
  all resolve to nothing. Any sweep keyed on the suffix `Path` alone would have missed it, which is
  why the test matches both spellings and asserts it specifically.
- **A Resources-relative path with a file extension is the second silent failure**, not just an
  `Assets/` prefix. `Resources.Load` takes the path *without* an extension and returns null with
  one. Checked against a list of known extensions rather than "contains a dot", so a folder name
  with a dot in it is not falsely rejected.

Also worth recording: `WeaponEntryResolver` checks **both** `iconSheet` and `iconPath` even though
`IconFor` prefers the sheet whenever it is set. Checking only the one in use would leave a
wrongly-written `iconPath` sitting invisible behind a working sheet until the sheet was removed —
at which point the art would vanish for a reason introduced months earlier.

**What stops it reopening.** #20's closing note is the relevant lesson — *"a rule that has to be
re-applied by hand to every new content type ... will reopen again on the next type added. A canary
generated per type, rather than written per type, is the version of this that would actually stay
closed."* So the durable half here is not the guards: it is
`ArtPathConventionTests.EveryPathShapedFieldOnEveryRawEntryIsClassified`, which reflects over every
`Raw*Entry` and fails on any path-shaped field the table does not classify. A new content type
cannot quietly arrive unchecked.

Non-vacuity **proved, not asserted**: unclassifying `iconSheet` and re-running produced exactly the
two real sites — *"RawSetPiece.iconSheet, RawWeaponEntry.iconSheet"* — with the message telling the
reader where to add them. The resolver-level tests are self-proving in the same way: each asserts a
bad path FAILS to resolve, so an unwired guard fails the test rather than passing it. That
distinction is finding #42's whole lesson, and the tests are written to respect it — the rule is
tested, and separately the wiring is tested.

**Original finding, kept:** `iconPath` is **`Assets/`-relative** (baked into a scene at build time by
`SceneBuilder.LoadSprite` → `AssetDatabase`) while `spritePath`, `vfxPath`, `sfxPath` and
`battleSpritePath` are **`Resources/`-relative** (loaded at runtime, no extension). Nothing
tells you which a given field wants, and getting it wrong fails SILENTLY — the loader returns
null and the slot renders a fallback. `CharacterEntryResolver` now checks both of its own
paths (`2b5132e`), but that is one content type out of eight; `EnemyEntryResolver`,
`SkillEntryResolver`, `TalentEntryResolver`, `RelicEntryResolver`, `ItemEntryResolver` and
`WeaponEntryResolver` still accept either convention in any field. A one-line-per-field guard
in each would close the class.

### 33. `Upgrades` is the last content type still hand-authored in C#
`ContentBuilder.cs`'s `CreateUpgrade` calls. Every other type reads `ContentData/*.json`
through a Domain-layer resolver; Characters was the second-to-last and moved in `2b5132e`.
Only two records, so the cost of leaving it is small — but it is the last thing forcing a
recompile to change content, and `UpgradeDefinition` has no resolver validating it at all.

### 34. The `Resolved* → *Definition` transcription is hand-maintained across 8 content types
`ContentBuilder.cs`, ~120 field assignments in 8 near-identical copy blocks (29 for enemies,
26 for skills, 17 for characters). Adding one field to a content type means editing
`Raw*Entry`, `Resolved*`, `*EntryResolver`, `*Definition` **and** this loop — five places,
with **no compile-time check that the fifth was done**. A forgotten line here is a field that
silently stays at its default in every generated asset. This is the largest manifest-shaped
target left in the codebase, but closing it properly is a code-generation project rather than
another manifest, which is why it was scoped out rather than attempted.

### 36. Shawn's committed portraits are not reproducible by the tool that supposedly made them
`tools/remove_portrait_backgrounds.py` at the documented tolerance produces a 1122x1360 crop
from `Art/Portraits/Sheep/Shawn_neutral.png`, but the committed
`Processed/Shawn_neutral.png` is **1122x1402** — a 42px difference, and all six emotions
differ. So the shipped portraits came from different settings, an older version of the tool,
or a hand edit, and nothing recorded which. The tool's own docstring warned that a batch run
would silently overwrite exactly this and enforced nothing.

Now marked `reproduces: False` in its `PORTRAITS` manifest, which makes the tool REFUSE the
folder rather than replace it, and `--check` renders to scratch to report what would change
without writing (`a4` commit). The art itself is untouched — it is what ships and it looks
right. What is still open is the actual question: nobody knows how it was made, so a future
portrait change to Shawn has no repeatable path.

### 35. ~116 user-facing string literals are split between `SceneBuilder` and controllers
Already noted under "Also worth knowing"; repeated here because the manifest work made the
shape explicit. Initial text is written in `SceneBuilder`, runtime text in the controllers,
with nothing tying the two together — so a label can be changed in one and not the other and
nothing reports it. A string manifest is the obvious tool and none exists.

---

## Findings from the debug-menu / Reckoning work, 2026-08-11

### 37. The economy is built around a voluntary-retreat flow the design does not have
`Domain/Economy/CurrencyType.cs:9-10` states the rule: run Gold is "banked onto the save on a
voluntary retreat", and "a wipe forfeits every unbanked coin". `:43` gives the framing — "Gold
is a wager until it is banked" — and `Wallet.BankFrom(source, from, to, rate)` (`Wallet.cs:96`,
`banked = floor(available * rate)`) is the partial-bank machinery that flow needs. Gold living
on **both** wallets (`RunState`'s at-risk copy and `SaveData`'s banked total) exists to serve
the same design.

**There is no retreat.** Confirmed with the author, 2026-08-11: retreating is not part of the
intended roguelike — you play until you die. So `BankFrom`'s `rate` parameter has no caller
that would ever pass anything but 1f, and the two-wallet split currently has one live purpose
(forfeit on wipe) rather than the two it was designed for.

**The wager itself is kept, and the author likes it.** The stated direction is to move banking
from an exit decision to an in-run one: checkpoints or banks encountered during a descent where
some capped amount of gold can be stored. That reuses `BankFrom` and the two-wallet split more
or less as they stand, so **nothing here should be deleted** — it is misfiled, not dead. The
debt is that the comments describe a mechanic that does not exist and no comment describes the
one that is planned, so the next person to read `CurrencyType` will implement the wrong thing.

Not fixed here because the checkpoint design is not settled and rewriting the comments to
describe an unbuilt mechanic would trade one wrong doc for another.

### ~~39. A tree node named `<button>Label` silently collides with the caption `UiEmitter` generates~~ — found and closed in the same pass, 2026-08-11
`UiEmitter.EmitButton` creates a button's caption as a GameObject named `node.Name + "Label"`. That
object **never exists in the tree**, so `UiAudit`'s A4 duplicate-name check — which only ever walks
the tree — was structurally incapable of seeing a collision with it. A screen declaring a child
called `MapNode3Label` under a button called `MapNode3` produced two GameObjects with one name under
one parent. The wiring bound the tree's node by `NodeRef` and painted it correctly, so it *looked*
fine; every lookup **by name** — PlayMode tests, `ScreenshotTool` — silently took the emitter's
empty one instead.

Three instances had shipped undetected: `MapNode{0..9}Label` (`MapScreen`, 27 audit errors once the
check existed), `InvestButtonLabel` (`TalentScreen`), and `DebugRow{i}Label` in the debug menu being
written when it was found. Renamed to `…Name` / `…Caption`. New check `CheckButtonLabelCollision`
(A4b) added to `UiAudit`, deliberately **before** A4's `Children.Count < 2` early return — a button
with exactly one child is the commonest shape and precisely the colliding one, so gating it behind
"has siblings" would have let the motivating case walk through. Non-vacuity pinned by
`DebugMenuScreenTests.TheCollisionCheckIsNotVacuous`, which builds a deliberate collision and asserts
the audit rejects it.

Worth keeping as a shape, not just a fix: **an audit that reads the source of truth cannot see what a
later stage synthesises.** The same blindness produced #40's sibling — the detail plate is `.AsDecor()`
and therefore exempt from the overlap check, which is why the boots-behind-the-plate collision needed
a direct assertion. Any exemption or any generated node is a hole the audit will not report.

### ~~40. Relics and Embers are two currencies with one source, and one of them does nothing~~ — **FIXED 2026-08-11.** Decided by the author the same day it was raised: Relics are not a resource at all. `CurrencyType.Relics`, `Wallet.relics` and `SaveData.Relics` are gone, the hub wallet line is two entries, and relics became a per-run draft with an authored rarity band instead. The original finding is kept below because the *reasoning* — two currencies sharing one source means one of them is dead — is what settled it
`SaveData.Relics`' own comment says *"Awarded only for clearing a floor's boss, so it measures how
deep a player has actually got."* `CurrencyType.cs:21-24` says the opposite: Relics are dormant,
*"nothing awards them and nothing sells for them"*, and a boss drop *"was the old model and Embers
replaced it."* Both comments are in the shipped codebase and they contradict each other.

The 2026-08-11 design decision — **Embers are paid per unique boss kill** — re-instates exactly what
`SaveData` claims Relics already are. So the game now displays three currencies on the hub plate, of
which one (Relics) has no source, no sink, and a comment describing a mechanic that belongs to a
different one.

Not resolved here because it is the author's call whether Relics get a distinct purpose or get
deleted. Whichever way it goes, `CurrencyType.IsSafe`, the hub currency line and `Wallet` all
already handle three, so the cost of leaving it is display noise rather than breakage.

### 41. `CurrencyType.Embers` and `Wallet.embers` survive with no live reader
Embers moved onto `Character.embers` (2026-08-11) so progression reflects who you actually
field. The wallet field and the enum value are both KEPT, deliberately: deleting the field
makes JsonUtility drop the value on load, which would silently rob every pre-v3 save of
everything it had banked, and `SaveData.MoveEmbersOntoTheRoster` is the only thing that reads
it. `Currencies.IsPersistent` still walks the enum, so removing the member would also change
what `WalletTests.ThereIsExactlyOneCurrencyARunCanCostYou` counts.

This is the exact shape of #40 — a currency member with no live use — and it is recorded
here rather than left to be rediscovered. The difference is that this one has a stated
expiry: once no save older than v3 can plausibly exist, both can go. `EmberOwnershipTests.
EmbersNeverLandOnTheSharedWalletAgain` is what stops anything starting to write to it in the
meantime, because that regression would silently re-share embers and nothing else would
notice.

### ~~42. No relic had ever fired in an actual fight~~ — fixed in this pass, 2026-08-11
`FightEncounterAdapter.KitFor` passed `null` for `PlayerKit`'s relic list, so `kit.Relics` was
always empty and `FightSession.RelicEffectFor` never matched anything. Dual Wield, Magical
Shield and Bloodlust were all implemented, all correct, and all unreachable from play.

**The reason it survived is worth more than the fix.** Every relic test in the suite —
including the flaky Bloodlust one that got four rounds of investigation under #24 — builds its
own `PlayerKit` by hand and hands it straight to a `FightSession`. That is the right shape for
testing a RULE, and it means the entire relic feature was covered by tests that could not
observe the one thing that was broken: nothing anywhere asserted that the ADAPTER supplies what
the session reads. #22's "untested critical paths" is exactly this category, and this is a
worked example of the failure mode — a path can be surrounded by passing tests on both sides
and still be severed in the middle.

Two further consequences of the same gap, both fixed here: `run.relicIds` was written by the
relic draft and read by nothing, so the draft screen was ceremony; and `RelicModifiers.Apply`
had zero callers, so the numeric modifier table was unreachable the day it was written.

Pinned by `RelicsReachCombatTests`, which was verified non-vacuous by reverting the null and
watching it fail.

### ~~38. A wipe does not actually forfeit anything yet~~ — **FIXED**, in `76a4dd1` and `2814cec`; struck 2026-08-17 on re-verification, having been built and never struck
The forfeit is enforced by **structure rather than by a clearing step**, which is why nothing
here looks like the "explicitly clears the at-risk Gold" this finding asked for. Run gold lives
only on `RunSnapshot`; `RunManager.EndRun` (`RunManager.cs:132`) replaces that snapshot with
`new RunSnapshot { hasRun = false }`, so the gold is destroyed with the run it was staked on.
There is no path that moves it to `SaveData`'s wallet, which is the invariant stated as "Gold is
a wager until it is banked".

`RunSettlement.Settle` runs **first**, and the ordering is load-bearing — its own header says so:
it reports `GoldLost` (`RunSettlement.cs:50`) and pays the ember/lifetime side while the evidence
still exists, because `EndRun` is about to throw it away. `76a4dd1` built that, plus
`DefeatController` and `DefeatScreen` — so the "returns the player to the hub with a defeat
animation" half landed too. `2814cec` then folded `Settle` INTO `EndRun`, because settling had
been the caller's job and only the defeat path did it: abandoning a descent from the map silently
binned every ember its bosses had earned, and discarding a snapshot looks identical whether or not
anyone read it first.

The test this finding explicitly asked for — one that fails if the forfeit stops happening —
exists twice over, and both assert the *negative* rather than just the report:
`RunEndingTests.TheRunsUnbankedGoldIsForfeited` (`RunEndingTests.cs:104`, "forfeited gold reached
the banked wallet") and `RunSettlementTests.LostGoldNeverReachesTheBankedWallet`
(`RunSettlementTests.cs:101`). `DefeatScreenWiringTests.cs:109` pins that the number reaches the
screen rather than dying in the settlement object.

Follows from #37, which stays open: the *comments* still describe a voluntary-retreat flow the
game does not have, and the checkpoint design that would replace them is unsettled. Enforcing the
forfeit did not resolve that, and should not be read as having done so.

**Original finding, kept:** Follows from #37 and is the more urgent half. `CurrencyType` documents the forfeit rule, but
the defeat path is unbuilt — the intended behaviour (2026-08-11) is that a defeat returns the
player to the hub with a defeat animation. Until something explicitly clears the at-risk Gold
on a wipe, the documented stake is not enforced by any code, and the "Gold is a wager"
invariant has no test. Whichever way the checkpoint design lands, the forfeit needs a test that
fails if it stops happening.

---

## Findings from the Reckoning polish pass, 2026-08-12

### ~~43. `screenshot.ps1` reports success for a panel it never captured~~ — **FIXED 2026-08-12**, both halves, in the commit that struck this
Asked for `-Panel ReckoningPanel`, the tool printed "Capturing ReckoningPanel to
…\ReckoningPanel.png", then listed the five PNGs already on disk from a previous run and exited
0. `ReckoningPanel.png` was never written and nothing said so.

The cause is stated in the script's own header and is reasonable on its face: "Success is
checked by whether the PNGs exist, **NOT** by `$proc.ExitCode`" — because that exit code came
back empty on runs that demonstrably succeeded. The check it fell back to is existence of *some*
output rather than of *the requested* output, so a stale file from any earlier run satisfies it.

`ReckoningPanel` could never have worked: `ScreenRegistry.All` holds five entries, all top-level
panels (`FightPanel`, `HubPanel`, `MainMenuPanel`, `MapPanel`, `TalentPanel`), and the Reckoning
is a sub-panel inside the fight. So the correct behaviour is to reject the name up front against
that list — the script already documents `ScreenRegistry` as the single source of truth for panel
names and deliberately keeps no second copy, which is right; it just never consults it.

Two separate defects, worth fixing separately: **an unknown panel name should fail loudly**, and
**success should be checked against the file that was asked for, and its timestamp**, not against
whatever is in the directory. The second is the one that generalises — it will silently pass on a
legitimate panel whose capture fails too.

~~Not fixed here because it is a tooling change sitting outside the Reckoning work, and because the
verification it was wanted for was done another way (`tools/measure_bar.py`).~~

**How it was fixed.** Both halves, because they are each other's backstop rather than two
independent tidy-ups.

*Loudly, and early*: `-Panel` is now validated before the robocopy and before Unity boots, against
names **parsed out of `ScreenRegistry.cs`** rather than restated in the script. That distinction is
the whole reason the list was absent in the first place, and it is preserved: deriving is not
copying, so adding a screen to the registry makes it valid to the tool on the same edit. Matching is
case-sensitive, because `ScreenshotTool` compares `PanelName` with `==` and `mainmenupanel` really
is not a screen. If the pattern ever matches nothing — a refactor, not an unknown panel — the check
**defers to Unity** instead of rejecting every name it can no longer recognise.

*And correctly, at the end*: the success check now asks whether **each expected file** exists, by
name. For a single panel that is the requested one. For `-All` it is every name in the registry —
clearing the directory first only rules out leftovers, and four files against five registered
screens is still a non-zero count, so the screen that quietly stopped rendering is exactly what the
old check could not see. On failure the log is filtered to `[ScreenshotTool]` lines, so its verdict
— which names the unknown panel and lists every valid one — is what gets printed rather than being
the sixteenth line of a 40-line Unity tail.

*And the same bug one layer down*: `CaptureAllTo` incremented its counter once per registered
screen regardless of outcome, so a `Capture` that bailed early (no graphics device, a scene with no
Canvas) still logged "wrote 5 screenshot(s)" having written four. It now counts what was written,
confirmed by `File.Exists` rather than by `RenderToFile` not throwing, and reports "wrote N of M".
A failed render exits 1, matching the unknown-panel path, so the exit code means one thing.

Ignoring `$proc.ExitCode` is **kept**. That was always right for the documented reason; checking a
different file than the one requested was never part of that trade.

Verified on all four paths: the original failing invocation now exits 1 without booting Unity, a
case-mismatched name is rejected, a valid panel still captures and now lists only the file it
actually wrote, and the end-of-run backstop reports the missing file plus ScreenshotTool's own
verdict.

### 44. The Reckoning's gain label sits on top of the bar it annotates
`ReckoningScreen.BuildRow`: the row is 64 tall, the name/level line occupies y 4..32, the track
is centred at -14, and the gain label (`ReckoningRow{i}Gain`, 200x22 at x 370, y -21) spans
y -32..-10 and x 270..470. The track spans x ±450, so the label overlaps the bar's right-hand end
by 20px vertically and across its whole width horizontally.

**A1 cannot see it**: both nodes are `.AsDecor()`, and the overlap check skips decoration on
purpose, because ambient art overlaps constantly by design.

It was already true before this pass and got marginally worse — the track grew 26 → 32 to stop
reading as a hole, which is finding 4 of the 2026-08-12 handover. Left alone rather than
half-solved: there is no vertical budget in the row, since a name line, a bar and a gain line want
84px in 64, and every alternative moves something the design settled deliberately. Either the row
pitch grows (which pushes three rows down toward the painted bottom border, and the top row into
the EXPERIENCE heading), or the gain moves onto the top line beside the level, or the overlap is
declared intentional and the label is styled to read as riding the bar. That is the author's call,
not a silent fix.

### 45. The detail column still covers the front enemy's feet while a submenu is open
`FightScreen.BuildDetailColumn` places a 340x300 panel at `(478, CommandBottom + 150)`, so it
spans y -486..-186 across x 308..648. The front enemy slot stands at x 470 with its ground line at
-228, which is inside that box — so whenever the player opens SKILL or ITEM, the nearest enemy's
contact ring, ground shadow and feet are behind the panel.

**Not fixed with the anchors, and the reason is arithmetic rather than preference.** Clearing the
detail column needs `Near.Y >= -166` (its top -186, plus the ring's 8, plus 12 of daylight). The
tallest actor needs 300 units above the ground line at the front slot's 0.78, so its head would
then reach 134, which pushes the enemy plate stack to `PlateFirstY` 430 and the ENEMIES heading to
within 24px of the canvas edge. That trades a conditional occlusion for a permanently cramped
top-right corner.

The rule applied instead is the one the handover states: clear every **always-visible** panel.
`FightScreenTests.NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet` enforces exactly that and
deliberately does not descend into subtrees that start inactive.

Three ways out, none of them free, all of them the author's call: shorten the detail column to 238
so its top clears the ground line (it currently holds a name, a kind, a 76px body, a divider and
four stat rows, so something has to give); move it out from over the stage, which the enemy plates
at x 520..920 leave no room for; or accept it and let the front enemy be occluded during selection.

---

## Findings from the register re-triage, 2026-08-17

The pass that established the v1/v2 split documented at the top. It struck #38 (built, never
struck), #29 and #19 (both obsoleted by the rebuild), and refiled the one piece of #19 that
genuinely carries across.

### 46. Twelve `Assert.Ignore`s skip on CONTENT shape, and three of them guard the regression #42 describes
v2 has 20 `Assert.Ignore` calls across 11 PlayMode files. **Eight are legitimate and should stay** —
they gate on `CanvasCapture.IsSupported` because `camera.Render()` is a no-op under `-nographics`
(`HitFlashPixelTests.cs:109`, `PostProcessingLegibilityTests.cs:178`/`:208`/`:243`,
`CharacterOverlayCaptureTests.cs:25`, `FightPlayableTests.cs:209`, `MapCaptureTests.cs:30`,
`RuntimeScreenshotTests.cs:51`). A pixel test with no pixels has nothing to assert.

The other **twelve skip because content or a generated map did not happen to contain what the test
wanted**, and those quietly stop testing as content drifts:

- `RelicsReachCombatTests.cs:106`, `:129`, `:135` — "content has only one character", "no relic in
  content carries a numeric modifier yet", "that relic does not touch attack"
- `CharacterOverlayTests.cs:230`, `:243`, `:265`, `:314` — "no wearable item in the starting kit",
  "the starting kit holds no consumable on the first page"
- `MapFlowTests.cs:228`, `:273` — "this leg offers no fight from the entry", "only fights"
- `EquipmentReachesCombatTests.cs:187`, `GlossaryTests.cs:167`, `FightPlayableTests.cs:172`

**`RelicsReachCombatTests` is the sharp end, and the reason this is filed rather than tidied.**
That file exists *because of #42*: relics were implemented, correct, and unreachable from actual
play for their entire life, and every relic test passed the whole time because each built its own
`PlayerKit` by hand. It is the guard against that recurring — and it can currently skip itself on
three separate content conditions without failing anything. A guard that silently declines to run
is indistinguishable from a guard that passes.

**This is NOT #19 wearing new clothes, and the difference decides the fix.** #19's skip was driven
by unseeded combat RNG, so seeding fixed it. These are driven by content shape and generated map
shape; seeding does not help, because the draw is not the problem — the *fixture* is. The remedy
is the one #24's fix already established as the house pattern: state the precondition rather than
hope for it. Either assert the content invariant loudly (`"content no longer has a relic with a
numeric modifier — this test has stopped covering #42"`), or build the fixture that guarantees it.
For the two `MapFlowTests` cases there is a third option the others lack: the map is seeded and
reproducible, so a seed known to offer both a fight and a quiet room can be pinned instead.

Cheap to fix, and worth doing before the next content change rather than after.

---

## Findings from the build-speed pass, 2026-08-19

### 47. The dynamic font atlas is tracked in git, so any run that rasterises a glyph dirties it

`Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset` is a TMP font asset with a **dynamic**
atlas: glyphs are rasterised on demand and written back into the asset. It is committed, so its
`m_GlyphTable` is repo state that changes as a side effect of running the game or the tests.
Found on 2026-08-19 sitting at `-2133/+10` lines against `4d3dfc1` — the table had emptied — with
no commit in this session having touched it deliberately.

Nothing is broken by it: the atlas repopulates at runtime, and text renders correctly either way
(verified in the dossier captures, which are full of the glyphs the emptied table lacks). The
cost is noise. It shows up dirty in `git status` for work that never went near a font, which is
exactly the condition under which a real change gets waved through, and it is the kind of file
that `git add -A` would sweep up — which this project already has an incident about.

Left uncommitted rather than decided unilaterally, because the two fixes point in opposite
directions and the choice is the author's:

- **Commit it emptied and stop caring**, accepting that the table will churn again. Cheapest.
- **Untrack it and regenerate on build**, alongside the other generated assets. Consistent with
  rule 2 of `CLAUDE.md`, but the asset is referenced by GUID from every built scene, so it would
  have to be generated *and* synced back like `Resources/Content/` is, and gotcha #2 applies to
  its `.meta`.

Worth noting the same property is what made #48 below land: the atlas being sparse at load time
is not a defect, it is the design.

### ~~48. `EnemyIntentIconTests` asked an arbitrary font whether it could draw a glyph~~ — **FIXED 2026-08-19** in `c2f436a`

Recorded because the *shape* recurs. The test called
`Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault()`, which is the same
incidental-order trap as gotcha #4 — `Resources.LoadAll` returning alphabetical order rather than
authoring order — reached through a different API and in a test rather than in content. Three
font assets are loaded, one of which is a nearly empty fallback atlas, and the day enumeration
order changed the test reported that the game's font could not draw the letter `F`.

Two lessons, both already paid for elsewhere in this register:

1. It had been passing for the wrong reason for its whole life. Any font with a Latin alphabet
   satisfies an all-ASCII fallback table, so it was testing LiberationSans and proving nothing
   about the font the game uses. A test that cannot fail is not evidence.
2. The one-argument `HasCharacter` asks whether a glyph is rasterised *right now*, which on a
   dynamic atlas is a question about what has been drawn so far. The three-argument form asks the
   typeface. The distinction is invisible until the atlas is sparse.

The fix reads the font off a live `TMP_Text` and pins its own premise: U+2694, the character the
intent badges first shipped with and which drew as nothing, must still come back false. If Chakra
Petch ever gains an emoji block, the guard fails and says the check has gone toothless.

## The fight that never started, 2026-08-20

**#46 — A fight deadlocked whenever a monster won initiative. FIXED.**

Reported as "every game after the first elite, none of the buttons respond
anymore": monsters drawn and standing still, no intent icons, every verb dead,
nothing in the log.

`FightSession.Begin()` — whose own docstring reads *"Opens the fight: the first
actor gets its turn start, any monsters faster than the whole party take their
opening swings"* — **had 36 call sites and every one was in `Tests/`.** Nothing
in the game called it.

`AutoResolveEnemyTurns` has exactly two callers: `Begin`, and the path that runs
*after the player acts*. So enemies only ever moved in reply to a move.
`TurnOrder.Start` seeds charge from initiative and gives turn one to the
highest-initiative combatant — and when that was a monster, nothing existed to
resolve its turn. It held the turn forever.

Every symptom follows from that one fact, which is why nothing looked broken:
intents are telegraphed only on the player's turn, `CanAct` requires the
player's turn, no code path throws, and the stranded-turn watchdog added in
`bffe4c5` watches `_isBusy`, which is never set because no playback was ever
started.

**Why after the elite.** Floor 1's monsters are slower than the party, so the
player almost always opened and the deadlock could not occur. Leg 2's pool
admits faster ones, and the first fight a monster opened was the last fight that
worked.

**Why no test caught it.** Every fight test called `Begin()` by hand — including
the four `bffe4c5` added specifically for this report, which is why that commit
ends "WHAT I HAVE NOT DONE IS REPRODUCE IT. All four pass." The suite was
repairing the exact state the game left broken. `FightController.Bind` was doing
the same thing for the telegraph half, as a deliberately narrow fix, with a
comment that stated the missing caller outright and treated it as somebody
else's problem.

*Fixed:* `FightBootstrap` calls `Begin()` before `Bind`; `Begin` is idempotent so
the two doors cannot double-grant; `Bind` no longer commits intents, because a
`Bind` that quietly repairs half an opening is what let this go unnoticed. Three
tests: a monster opening and the fight coming back to the player, `Begin` being
idempotent, and `IsPlayerTurn || IsOver` through the real door — `CanAct`'s third
term, which `bffe4c5` named as the only one never asserted.

**Consequence worth knowing: every fight in the game has been missing its
turn-one `GrantTurnStart`** — opening-turn regen and status ticks never
happened. Restoring `Begin` restores them, so combat numbers move slightly in
every fight rather than only in the deadlocked ones. The suite is green either
way, which means nothing was tuned around the absence.

## Findings from the reward-track planning pass, 2026-08-21

### 49. `Character.cs` documented an invariant that was inverted, unimplemented, and guarded by a test that did not exist — **FIXED** in the commit that added this

`Character.cs:58` opened with *"PER-RUN progression, earned from combat and reset
by StartRun"* and closed with *"one rule that has to hold: StartRun resets all
four. There is a test whose whole job is to fail if it ever stops."*

Three separate claims, all false:

- **`RunManager.StartRun` never touched the roster.** It replaces `save.activeRun`,
  calls `RoomResolver.Reset()`, `Forget()` and `Persist()`. `save.roster` is not
  in the method.
- **Nothing anywhere reset them.** `grep` for `level = 1` / `exp = 0` /
  `unspentStatPoints = 0` across non-test code returns the field initialiser and
  a default parameter in `FightEncounterAdapter`. Nothing else.
- **There was no such test.** No test asserted anything about level across a run
  boundary in either assembly.

`RewardApplier.cs:12` states the true rule and always has — *"GOLD belongs to the
run and is lost with it, EXPERIENCE belongs to the characters and survives"* —
so the codebase carried both answers, in two comments, with the wrong one
attached to the field itself.

**Why this is worse than an ordinary stale comment.** It did not read as
description; it read as a load-bearing invariant with a named guard, in a file
whose house style is that comments carry the reasoning. It is the shape a
planner trusts most. It came within one session of costing a full reward-track
design: six of the track's ten milestones ("start every run with 2 relics",
"guaranteed rest before every boss", "second life") are statements about how a
run *begins*, and under the documented rule none of them can exist, because you
would never be holding the level at the moment the reward applies. Caught only by
reading `StartRun` rather than believing the field.

The same paragraph was also propping up a second decision one screen down:
`Invest`'s *"there is deliberately no matching Refund ... the run ending is
already a full reset, so nobody is stuck with a build forever."* The escape hatch
that argument leans on does not exist, so a placed stat point has always been
placed permanently. Refund is still deliberately absent, now for the half of the
argument that survives.

*Fixed:* both comments say what is true; `RunManagerTests` gains the test that
was claimed, inverted — `StartingARunKeepsEveryCharactersLevel` — plus
`StartingARunStillDiscardsTheRunBeforeIt` for the other half, so a future change
in either direction fails for its own reason. The class of bug (a comment
asserting a guard that does not exist) is not mechanically closable; the
narrowest available fix is that the guard now exists.

### 50. `SaveData.relicLoadout` is written by nothing and read by nothing

`RelicLoadout` is a 160-line class with a careful exclusivity rule ("assigning a
relic that is already in use takes it away from whoever had it"), a `Set` that
returns the displaced relic, `CharacterHolding` for greying out a card on an
assign screen, and eleven EditMode tests. **Nothing in the game calls any of
it.** `save.relicLoadout` appears in exactly two places outside its own file and
its own tests: the field declaration (`SaveData.cs:126`) and `Reconcile`
(`:491-492`), which validates entries that nothing ever creates.

Relics in play live on `RunSnapshot.relicIds` instead, and that file says why:
*"Drafted at the start of a descent and GONE when it ends -- which is why they
live here rather than on SaveData."* `RelicDraftController` writes there,
`FightBootstrap` reads there, `ReckoningController` and `RunStatsController` read
there. The per-character loadout is a v1 shape that survived the rebuild with its
tests attached, which is what kept it looking alive.

Same class as #41 (`CurrencyType.Embers` with no live reader), and the tests are
what make it expensive rather than merely untidy: eleven passing tests over a
type the game never invokes read, from the outside, as coverage.

**This one has already cost planning time.** `docs/PLAN_PROGRESSION_TRACK.md`
priced the track's level-25 and level-45 "extra relic slot" rewards as a
widening of `RelicLoadout` from one entry per character to one per
(characterId, slotIndex) -- following that class's own header, which specifies
the widening as a stated future extension. The widening would have been correct,
tested, and reachable from nothing.

Not deleted yet: see the reward-track note below, because whether these
milestones become real is what decides whether the type has a future.

### 51. The track's "extra relic slot" rewards have nothing to unlock

`RunSnapshot.relicIds:108-111` states the design outright: *"A list rather than a
single id, deliberately. Only one is drafted today, but the design is 'infinite
slots per run' -- mid-run relic rewards from elites or bosses drop straight in
here with no shape change."*

There is no slot cap anywhere. A character can already hold any number of relics
in a run; only one is ever *offered*. So levels 25 and 45 grant capacity that is
already unlimited — the same shape as the two the handover already caught, where
"rest heals more" had nothing to improve because `RoomResolution` already heals
to full, and "honed offers" was a second dial on the Favor already driving
`RollTier` and `RollPlus`.

Three ways out, and it is a design call:

1. **Drop 25 and 45**, refill with filler, exactly as the gold nodes were held.
   Level 60 ("start every run with 2 relics") does not need them — it is drafting
   twice — and level 80 ("elites drop a relic") is `relicIds.Add`, which the
   comment above already anticipates.
2. **Introduce a cap** so the slots have something to lift. This makes the game
   more restrictive before it makes it more generous, and contradicts a design
   statement that is written down rather than assumed.
3. **Repurpose 25 and 45** to grant an extra *starting* relic each, so the relic
   line reads 25 → 2 at start, 45 → 3, 60 → the existing milestone folds in.

Recorded rather than chosen, per this file's standing posture on design
decisions that belong to the author.

### ~~53. Stat points cannot be spent -- `Character.Invest` has no production caller~~ -- **FIXED**: the dossier's attribute cells now carry a "+" that calls `Invest`, shown only when there are points to spend, with a "N POINTS TO SPEND" line beside the section heading so the mechanic is discoverable rather than a 22px glyph nobody looks at

`Character.Invest(AbilityScore)` is implemented, commented at length ("ONE point
at a time on purpose: the UI offers a '+' per score"), and covered by tests.
**Nothing in the game calls it.** `grep` for `Invest` across `Assets/` returns
this method, its tests, and otherwise only the TALENT tree's unrelated
`CanInvest`/`InvestButton`, which spends embers on orbs and never touches an
ability score.

`investedAbilityScores` is assigned in exactly one place -- inside `Invest`
itself (`Character.cs:191`). It is READ correctly, by
`ContentDatabase.Effective.cs:113`, which folds it into `EffectiveAbilityScores`
and out into attack/speed/health. So the pathway works end to end and has no
entrance.

The dossier is where the "+" per score was supposed to live. Its
`attributeCells` are wired to `HoverIndex` -> `OnAttributeHover` only: hovering
an attribute explains what it does, and there is no way to raise one.

**What this costs.** `unspentStatPoints` accumulates and is never spendable, so:

- One stat point per level was the ONLY thing levelling granted before the
  reward track, which means levelling has never granted anything a player could
  use.
- The track's filler is 30 stat points, and level 80 grants 10 more. That is 40
  of the track's rewards doing nothing -- and level 80 was chosen *because* it
  was the safe, already-implemented reward kind (it replaced "elites always drop
  a relic", cut as too strong).
- A respec's stat half refunds a block that is always zero.

Same class as #42 (relics that had never fired in a fight) and
`DifficultyCurve`'s own header ("AND THEN IT WAS NEVER CALLED"): implemented,
tested against inputs the test builds itself, and unreachable from play. The
tests are again what makes it look covered.

*Not fixed here.* The respec work that found it builds both halves so the refund
is correct the day the spend exists, but a "+" per score on the dossier is its
own change.

### 54. `StatBlock.ScaledForElite` has no production caller -- elites get no stat boost at all

`ScaledForElite(multiplier, defenseMultiplier, attackMultiplier)` exists, is
commented at length, and names the playtesting it came out of: *"That
combination -- defense originally uncapped, attack still uncapped even after
defense was fixed -- is what made Elites 'completely clap you' in playtesting,
twice."* It closes by pointing at `FightController.EliteStatMultiplier /
EliteDefenseMultiplier / EliteAttackMultiplier` "for where this is actually
used".

**None of those three constants exists**, and nothing calls `ScaledForElite`.
`grep` returns the method, its tests, and one passing mention in `Rounding`'s
own comment.

So an elite room differs from a normal one in exactly two ways: it fields
`EncounterRoll.EliteEnemyCount` (2) enemies where a normal room rolls 1 or 2,
and it pays `VictoryRewards.EliteRewardMultiplier` (1.56x). Its monsters are
scaled by depth like everything else and by nothing else. Elites are
substantially WEAKER than the design describes, not stronger.

Found while diagnosing "floor 1 after the first elite it already becomes so
difficult" -- where it matters as the thing that is NOT the cause. The report
points at the elite; the elite is nearly a normal room.

Same class as #53, #42 and `DifficultyCurve`'s own "AND THEN IT WAS NEVER
CALLED": implemented, tested against inputs the test builds itself, unreachable
from play, and carrying a comment that describes it as live. Recorded rather
than wired, because switching it on would make the game harder and the reported
problem is that it is already too hard.

## Open investigations

### ~~52. `SystemMenuExitsTests.OnePressOnAnExitDoesNothingButArmIt` flaked once, navigating to `"Hub"` — cause not found~~ — **ROOT CAUSE FOUND AND FIXED, 2026-08-21.** A `HoldToConfirm` left running by an earlier test in the same fixture. `HoldToConfirm` cancels itself `OnDisable`, and its comment explains why — but between two tests nothing disables it: the scene stays loaded until the next `LoadSceneAsync`, so a test that begins a hold and neither completes nor releases it leaves `Update()` advancing that hold into whatever runs next. When it completed it called `Abandon`, which navigates to the hub, and the navigation landed in the *next* test's recorder. The fixture's `TearDown` now cancels every live hold. **What made it findable:** the reward-track panel added ~450 nodes to the Hub scene, frames got long enough that the leftover hold finished inside the very next test every time, and a one-in-four flake became 3-for-3 — including in isolation, which it had never done. The extra nodes did not cause it; they made it reproducible enough to get a stack trace, which named `HoldToConfirm.Update` with no test above it. Guessing had blamed process-wide `Navigation.LoadOverride` state, which was wrong

Seen **once in four full-suite runs**, during the reward track's starting-relic
work (2026-08-21). Passes in isolation (13/13), and passed in three consecutive
targeted runs paired with `RelicDraftTests`, and in the three full runs after it.

```
FAILED: SystemMenuExitsTests.OnePressOnAnExitDoesNothingButArmIt
  one press on an exit left the scene
  Expected: <empty>   But was: < "Hub" >
```

The recorded destination is the interesting part. The test arms `ExitTitle`,
whose second press goes to `Navigation.MainMenu` — so `"Hub"` is not this exit
firing early, it is a **different** navigation arriving inside this test's
recording window. `Navigation.LoadOverride` is a process-wide static that each
fixture installs in `SetUp` and clears in `TearDown`, which is exactly the shape
of thing `GlobalStateLintTests` exists to police, and exactly the shape that
produces a once-in-four failure rather than a consistent one.

**I do not know whether the starting-relic change introduced this.** That work
added four PlayMode tests that load the Hub scene and drive the draft to
completion, which changes both the ordering and the timing of every PlayMode
test after them — enough to surface a latent race without being its cause. It
was not observed before that change, but the change is also the first time this
suite had been run repeatedly in one sitting, so absence of prior sightings is
weak evidence either way. Recorded rather than guessed at.

Worth pairing with #24, the other flake in this register whose root cause was
eventually found to be a real bug (#13) rather than test noise.


### ~~24. `BloodlustRelic_GrantsAnImmediateExtraTurnAfterAKillingBlow` flakes on fresh content/scene builds — root cause not found~~ — **ROOT CAUSE FOUND, 2026-08-04.** It is a symptom of #13, and fixing that fixed this. Reproduced 2 times in 8 runs before, then 0 in 12 after

The investigation below is preserved because most of its ruling-out was correct — it just never
suspected the message *buffer*, only the message *not being emitted*. It was being emitted every
time.

**What was actually happening.** Bloodlust fires in `AdvanceAfterAction`, after `CommitBeat()` and
before `StartBeatPlayback()`. So its line went straight into `_recentMessages`, while the beat
messages for the very swing that earned it ("Shawn attacks…", "…is defeated!") were appended
*later*, during playback. The buffer trims from the front. On an eventful turn — poison ticks, a
Wool soak, two enemy replies — the newer beat lines pushed the Bloodlust line out before the
assertion read the label. Fewer preceding messages and it survived; that is the whole of the
intermittency, and why it correlated with nothing anyone tested.

**Why it looked so mysterious.** The failure output said only "no Bloodlust in the log", which is
consistent with a dozen explanations. Diagnostics were added to the assertion, and the first
reproduction settled it outright: `IsOver = False`, kill landed, `chain count = 1`, `chain actor =
Shawn`, `Current = Shawn`. The extra turn had been granted and recorded. **Only the message was
missing** — so the sole remaining suspect was the buffer. The register's own note that the sibling
`Assert.AreSame` "also failed" was mistaken; it passes, which is exactly what a granted-but-
untold extra turn looks like.

Two things were fixed alongside the strike. The test itself had a **second, independent** failure
mode never reached in practice: `AdvanceUntilItIsThisCharactersTurn` reaches the actor's turn by
attacking up to twenty times, and those attacks can finish the *second* enemy off first — after
which the pinned kill is the last one, `AdvanceAfterAction` takes its `_encounter.IsOver` early
return, and Bloodlust is genuinely never reached. The Elite room was chosen to prevent that but
does not, since it only guarantees two enemies *at the start*. The test now restores the
non-front enemies before the killing blow, stating the precondition rather than hoping for it. And
the diagnostics stay on the assertion permanently — they cost nothing on a green run and turned a
multi-session mystery into a single reproduction.

`Assets/_Project/Scripts/Tests/PlayMode/FightControllerTests.cs:1911`. Failed 4 times in one
night (2026-08-01), always the same way: a full combat sequence up to and including the killing
blow logs correctly, but the expected `"...'s Bloodlust surges — one more turn!"` message
(`FightController.cs:2043`) never appears and `Assert.AreSame(actorsTurn, encounter.Current, ...)`
/ `StringAssert.Contains("Bloodlust", ...)` fails. Every failure ran with a fresh
`-BuildContent -BuildScenes` (which regenerates and re-syncs the whole `Resources/Content` tree
and both scenes across two isolated Unity processes — see `tools/run_tests_parallel.ps1`); every
sync-less rerun of just this test passed. No other test has shown this pattern.

**Ruled out:**
- **Not a crash.** `test-run-PlayMode.log` for a failing run has zero `NullReferenceException`,
  zero `MissingReferenceException`, zero "missing script" warnings — nothing but the standard
  "Native extension not found" noise every batchmode run prints.
- **Not a stale-GUID sync issue**, or at least not one that survives to test time. Added
  `Assert-GuidsMatch` to `run_tests_parallel.ps1` — a hard, hash-independent comparison of every
  `.meta`'s literal `guid:` value between main and each secondary runner's copy, run immediately
  after the post-build re-sync, aborting the whole run loudly and naming the exact asset on any
  mismatch. It has fired zero times across the runs used to investigate this, including a run
  that then went on to fail with the exact Bloodlust symptom — so at the moment the test process
  launches, every asset file on disk genuinely agrees on GUIDs between main and the PlayMode
  runner. Whatever is wrong is not "the wrong file is on disk."
- **Not static/cross-test state leakage** in the mechanism itself. `_bloodlustChainActor` /
  `_bloodlustChainCount` (`FightController.cs:377-378`) and `_playerOwners`
  (`FightController.cs:766`) are all plain instance fields, rebuilt fresh per encounter/scene
  load; `_bloodlustChainCount`'s read at `:2036` already guards on `actor == _bloodlustChainActor`
  so a stale reference from a prior test's (different) actor object can't inflate the chain count.

**Both leading theories were wrong, and the way they were wrong is the useful part.** They were a
Unity asset-import timing race (`RelicEffectFor` resolving a relic against a not-yet-reimported
Library) and an actor-identity mismatch in `_playerOwners`. Both were plausible, both explained
intermittency, both were consistent with every observation — and both were about the relic lookup
FAILING, when in fact it succeeded every time. Four rounds of investigation narrowed the wrong
half of the problem, because the failure output ("no Bloodlust in the log") never distinguished
"the effect did not fire" from "the effect fired and the message was discarded", and nobody
thought to ask which. Adding four values to the assertion answered it on the first reproduction.

Two data points from those rounds are worth keeping and re-filing. The "only after a fresh
`-BuildContent -BuildScenes`" pattern never held up — later occurrences included plain no-flag
runs — which is expected once the cause is a message buffer and has nothing to do with builds.
And the Phase 6 observation of a *different* test, `ContinueAfterVictory…`, showing up as
**Skipped**, was never a Bloodlust data point at all: that test's only skip route is #19's
`Assert.Ignore` on a randomised defeat. It belongs to #19 and is recorded there.

The GUID-consistency assertion added while hardening against theory 1 is kept. It was aimed at
the wrong cause but is a reasonable check in its own right, and removing it now would be
churn.
