# GAP_AUDIT — Shop Screen v2

Audited against `README.md` at `5f1e7d4`, with the rows re-cut against the
revision of 2026-09-02 (`b7adb83` plus the external-review pass) — the relic
pager row is gone because the pager is, and rows were added for the detail
panel, the state machine, atomic persistence, `NO OFFER` and keyboard focus.
**Nothing in this handoff is built.**
The room type generates and resolves as a placeholder
(`Domain/Dungeon/RoomResolution.cs:99-100`); there is no shop screen, no shop
controller, no pricing, and no spell-book system.

This file exists from the start rather than after a build, so the first audit
pass has a shape to fill in rather than one to invent. Strike a row or change
its verdict as each gap closes; nothing is deleted.

**Revised 2026-09-03: the interactive prototype landed and the spec moved
under it** (`README.md` §0, `PLAN_SHOP.md` §1g/§2c/§2f/§2g/§3d/§3e/§3f). Rows
below whose "Spec'd" column described the recipient strip, the persistent
detail panel or the inline sell viewport are marked **relocated** or
**superseded** rather than struck — the design they describe didn't disappear,
it moved to a different screen (the dossier) or a different surface (a
tooltip, a modal). New rows cover what the prototype and the revised plan
added: the unassigned-book pool, the dossier assignment panel, the `PACK`
modal, and the two interaction ambiguities the prototype's code resolved
without a decision on record. Still nothing built.

**Second pass, 2026-09-03: citations re-verified against the current tree,
and one row corrected for a fact that changed under it.** Several file:line
citations had drifted from ordinary churn (comments added, methods
reordered) since this file was last cut against real code; the worst was
row 54, whose citation had come to point at an unrelated method entirely
(a squad-convergence check, not the relic filter). More importantly,
`RoomType.Shop`'s generation weight was rebalanced (6 of 90 → 3 of 269,
`DescentMap.cs`) after this audit's §1 Overview row was written — the ratio
to the other placeholder rooms is unchanged, so the frequency conclusion
likely still holds, but the literal numbers were wrong until now. Row 63
said a shop purchase "learns" a book, which flatly contradicts the §1g row
two lines below it on the same page — corrected to match. Rows 52, 53 and
58 still described recipient-strip-era states (`✓`, `REPLACING`, a
`RecipientSelected` sub-state, "focus feeds the detail panel") that the
2026-09-03 revision above moved to the dossier without updating these three;
they now say so. Also added a row for §1a's `bookOnly`/`bookTier` content
flags — Phase A's own first prerequisite, and the one thing in this whole
plan with no row tracking it at all.

**Third pass, 2026-09-03 (b): the product review landed and every row gains a
fifth column.** `PLAN_SHOP.md` §7 records a product review of the plan and
decides what changes; §7.4 asks every row here to name which of the three
target decisions (§7.1 point 2) it serves — `save-or-spend`,
`patch-or-reinforce`, `reroll-or-walk` — or `machinery` when it does not serve
one directly (a save field, a layout coordinate, a persistence rule). Existing
rows keep their citations from the prior passes unchanged; only the new
column and new rows are added here. New rows cover what §7 introduced: the
per-section reroll and its three RNG streams, `ShopResult`/`SaveSystem.Save`
returning `bool`, `GoldOnArrival` telemetry, the matched-seed leg comparison,
the gate-1 exit numbers, single selection + `BUY`, the keyboard navigation
graph, `UiMotion.DurationScale`, the book card's purchase-time facts, the
map's pending-book indicator, return-to-pool replacement, and the human
checklists gates 2 and 3 require. A gate cannot exit on `machinery` rows
alone (§7.4).

## Gate exit checks

Each gate in `PLAN_SHOP.md` §7.3 ends with a written exit check recorded
here before the next gate starts. Empty until a gate actually exits.

### Gate 1

**Recorded 2026-09-03, commit range `4bc66f7`-`ed48a39` (economy) plus the
bot/policy layer built on top of it.** Batches: `WhenOffered` — 12,000 runs
(4 archetypes × 3 profiles × 1,000 seeds), 4 shards, `reports/bot/20260903-150857`
— and a matched `Never` batch on the same seeds, 3,000 runs (the 250-run/shard
shape × 4 shards), `reports/bot/20260903-155145`. Paired summary:
`reports/bot/20260903-150857/summary_paired.json`
(`python tools/bot_merge.py reports/bot/20260903-150857 --compare reports/bot/20260903-155145`).
Determinism check: 1,200 replayed runs, 0 mismatches — quitting mid-shop and
reloading reproduces the same stock, which is the property F9/§2d exist for.
4 pre-existing `TooManyCommands` invariant hits (a `placeholder_brawler_provoke`
stalemate, `GreedyAggressive`/`GreedyDefensive` Late), unrelated to the shop —
a placeholder-content bug, not a regression from this work.

**Matched-seed comparison (§7.1 point 1) — taking a shop does not cost depth.**
3,000 pairs, 1,751 with at least one shop visit. Median depth: 40 vs 40
(delta 0, both hit the depth cap at the same rate). Survival to the next
boss after a shop visit: 99.66% (`WhenOffered`) vs 99.71% (`Never`) — a
0.05pp gap, noise at this sample size. Depth variance: 5.21 vs 5.44. The
"shop over fight does not cost depth" gate (§7.3, exit condition c) passes
cleanly at the current 3/1/2-card, gear/relic-only shelf.

**Arrival gold, by step (all archetypes/profiles pooled), replacing §2a's
cumulative-won-gold table:**

| step | p10 | p25 | median |
|---|---|---|---|
| 4 | 9 | 21 | 31 |
| 8 | 62 | 90 | 112 |
| 12 | 195 | 249 | 294 |
| 16 | 339 | 408 | 475 |
| 24 | 852 | 989 | 1,115 |
| 32 | 1,808 | 2,040 | 2,280 |
| 40 | 3,619 | 4,018 | 4,429 |

**Per-cell shop numbers** (12 cells, archetype × profile; RandomLegal picks
uniformly among legal choices including "leave", the three others buy
whenever a card scores positively):

| archetype | profile | arrival gold p10/p25/median | below cheapest | purchases/visit | zero-purchase | afford gear/relic | spend share gear/relic | gold on leave (median) | gold at death (median) |
|---|---|---|---|---|---|---|---|---|---|
| RandomLegal | Fresh | 34 / 208 / 656 | 8.2% | 0.34 | 73.4% | 91.2% / 85.4% | 35% / 65% | 633 | 3,738 |
| GreedyAggressive | Fresh | 34 / 173 / 512 | 7.3% | 2.91 | 7.4% | 91.7% / 84.6% | 26% / 75% | 308 | 3,892 |
| GreedyDefensive | Fresh | 34 / 182 / 541 | 7.3% | 2.85 | 6.5% | 91.8% / 85.2% | 25% / 75% | 350 | 4,232 |
| Lookahead2 | Fresh | 27 / 141 / 447 | 8.4% | 2.25 | 16.3% | 90.3% / 82.3% | 18% / 82% | 56 | 3,144 |
| RandomLegal | Mid | 39 / 214 / 709 | 7.9% | 0.33 | 73.6% | 91.1% / 86.0% | 39% / 61% | 684 | 4,170 |
| GreedyAggressive | Mid | 34 / 176 / 510 | 7.4% | 2.86 | 7.6% | 91.1% / 84.8% | 27% / 73% | 290 | 4,201 |
| GreedyDefensive | Mid | 34 / 182 / 535 | 7.4% | 2.77 | 7.0% | 91.2% / 85.1% | 25% / 75% | 345 | 4,321 |
| Lookahead2 | Mid | 27 / 140 / 445 | 8.6% | 2.23 | 16.5% | 89.8% / 82.4% | 18% / 82% | 54 | 3,451 |
| RandomLegal | Late | 25 / 210 / 694 | 9.3% | 0.34 | 73.7% | 89.3% / 84.6% | 40% / 60% | 668 | 4,219 |
| GreedyAggressive | Late | 24 / 166 / 477 | 9.4% | 2.87 | 9.6% | 88.6% / 82.2% | 31% / 69% | 242 | 4,068 |
| GreedyDefensive | Late | 26 / 172 / 494 | 9.0% | 2.73 | 8.2% | 89.0% / 82.6% | 29% / 71% | 294 | 4,119 |
| Lookahead2 | Late | 17 / 128 / 424 | 10.9% | 2.16 | 19.4% | 87.1% / 79.7% | 22% / 78% | 48 | 3,780 |

Book section: `noOffer` for every visit in this gate (`ShopStock.BookCount = 1`,
no book content rolled yet) — `spendShareBySection.books = 0` everywhere,
correctly.

**Reading.** Affordability is not the bottleneck: 87-92% of gear cards and
80-86% of relic cards are affordable on arrival across every archetype and
profile, and only 7-11% of visits arrive below the cheapest card on offer.
The bottleneck is **visit frequency and per-visit spend ceiling against a
six-card shelf**, not price. Even the most aggressive buyer (`GreedyAggressive`,
2.9 purchases/visit, 6.5-9.6% zero-purchase) leaves a run with a median of
3,700-4,300 gold unspent at death — six to nine times the pre-shop §2a
baseline (531-740 median gold at death) that the shop was meant to sink.
`RandomLegal`'s 73% zero-purchase share is a policy artifact (it treats
"leave" as one of several equally-legal choices, per its brief) and should
not be read as a shelf-size finding on its own; the three greedy archetypes,
which buy on every affordable positive-scoring card, are the honest read of
whether six offers is enough room to spend into, and they say it is not.

**Proposed changes for the next gate, not applied here** (§7 reserves this
decision for the plan's owner):
1. **Widen the shelf, or slow the gold curve, or both.** At the current
   3 gear / 2 relic / 1 book cards, even a bot that never declines an
   affordable positive-scoring purchase cannot spend fast enough to keep
   pace with arrival gold, which itself grows roughly geometrically with
   depth (31 → 4,429 median from step 4 to step 40, a ~140× climb against
   §2a's flat-with-depth *per-fight* income — the run-total simply compounds).
   A `GearCount` of 4-5 and/or a `RelicCount` of 3 would give the greedy
   archetypes more to spend on per visit; whether that closes the gap or
   only delays it depends on how many *visits* a run gets, which gate 1
   did not change (shop generation weight is unmoved, F2/§0).
2. **The book section, once populated in gate 3, is one more sink** — its
   `spendShareBySection` is 0 by construction this gate and should move the
   split visibly once real content rolls there.
3. **Gold-forgone-by-step** (median won-fight payout at the same step band,
   from this same batch): steps 1-8 = 29g, 9-16 = 71g, 17-24 = 129g,
   25-40 = 260g. Against the arrival-gold table above, a player is never
   close to being unable to afford a shop trip — the opportunity cost of a
   shop node is small next to the bank they are already carrying by the time
   they reach one.
4. **`RerollPrice`'s doubling (15·2^n, ceiling 9999) is not the constraint
   either** — 6.7-8.9% reroll-then-no-purchase per section, roughly flat
   across archetypes, suggests rerolling is used sparingly and mostly
   productively, not as a symptom of an unaffordable shelf.

None of the above is applied in this gate; §7.3 requires the counts to be
**re-decided in writing** before gate 2's screen is laid out, and this is
that writing.

**Decided 2026-09-03 (author).** `GearCount = 4`, `RelicCount = 3`,
`BookCount = 3` — ten offers, same as §2d's original count, arrived at
independently rather than reverted to. Reroll stays exponential per section
(`15 · 2^n`, ceiling 9999, unchanged) — proposal 4 above already showed
rerolling was not the constraint, so it needed no change. The book count no
longer follows the "one card, pool is five" reasoning §7.1 point 3 gave;
whether three book cards against a five-entry pool reads as repetitive is
gate 3's question, not this one's, since no book content rolls until then.
Applied in `ShopStock.cs`, `ShopStockTests.cs`; the shelf is also the UI
design — the five-panel grid's three card panels are sized to these counts
directly, not laid out first and reconciled after.

### Gate 2

**Recorded 2026-09-03, branch `shop-v2`.** Built: `Domain/UiKit/Screens/ShopScreen.cs`
(the tree -- header, three sections at gate 1's decided counts of 4 gear /
3 books / 3 relics, a detail label, BUY/PACK/LEAVE, the PACK modal with six
paged rows), `Core/ShopController.cs` (single global selection + BUY per
§7.1 point 6, per-section REROLL, the PACK modal's sell-one/sell-all, the
two-press LEAVE confirm), the nesting into `MapScreen` and the wiring +
`CountBindings` in `ScreenRegistry.Map()`, `MapController.OpenShop()`
replacing the gate-1 shim in `MapController.Walk.cs`'s `Arrive()`, and the
new `UiStrings` entries.

**Deviations from README §3/§3f, stated rather than silently taken:**

1. **Layout is a flat vertical stack, not the five-panel pixel grid.** §3f's
   coordinates were drawn against a prototype with painted borders and card
   art neither of which exist for this screen yet; a plain layout the audit
   can check today was chosen over copying coordinates nothing has verified
   against this screen's real content. Restyling to the five-panel shape is
   follow-up, not done here.
2. **The tooltip is a fixed-position detail label, not a floating overlay
   that follows the focused-or-hovered card (§3d).** It shows whatever is
   selected under the single-selection model; there is no runtime pointer-
   tracking pass in this gate.
3. **No keyboard navigation graph and no `UiMotion.DurationScale`** (§7.1
   points 8). Mouse only. The navigation model §7.1 asked to build now, so a
   gamepad could be wired later without a second model, was not built --
   flagged rather than quietly dropped, since it is real scope the review
   asked for and this gate did not deliver.
4. **The book section is real UI wired to real content that never appears
   yet.** `ShopStock.RollBooks` still returns `NO OFFER` for all three cards
   (gate 3 populates it) -- the section, its cards and its reroll are live
   and audited, just permanently empty until then.

**What was verified, and what could not be:**

- `ShopScreenTests.TheScreenAuditsCleanAtEveryFrame` and
  `ItAuditsCleanInsideTheMapItMountsIn` -- `UiAudit` clean at all four
  `UiFrames` (1920x1080, 2580x1080, 1920x1440, 1920x1200), both standalone
  and nested inside the real `MapScreen` tree. Card/pack-row counts pinned
  against `ShopStock`'s own constants, not literals.
  `ScreenRegistry.Map()`'s new `CountBindings` (four arrays: gear/book/
  relic cards, pack rows) are declared, matching E4's convention.
- **NOT verified: a live PlayMode walk (map -> shop -> buy -> leave)
  through an actual built scene**, the gate's own exit criterion. This
  needs `Map.unity` (and the four sibling scenes `ScreenBuilder` regenerates
  together) rebuilt with `ShopController` in the tree, and `git status` at
  the start of this work already showed `Assets/_Project/Scenes/*.unity`
  modified by another live session sharing this working tree (`docs/
  WORKFLOW.md` §4 confirms more than one interactive session was open
  against this repo during this work). A scene rebuild is a full
  regeneration -- every `fileID` moves -- and syncing that back to `main`
  would silently overwrite whatever that session has pending in those five
  files, which is the exact incident §4's rules exist to prevent. This is
  the one exit criterion left open, and it is a **process** block, not a
  code one: the fix is coordinating a scene rebuild (the "freeze protocol"
  §4 already describes for a multi-file shared change) once Scenes/
  ownership is clear, then adding the walk test §7.3 asks for, not writing
  more screen code.
- The four pre-existing `TooManyCommands` bugs from gate 1's batch are
  unrelated to this screen and untouched.

**Human checklist (time to first purchase, can the tester explain why the
offer they bought was good, did anyone try to sell a worn piece, did anyone
misread `NEED n` as the price) is NOT run.** It needs the same built scene
the walk test does, so it waits on the same block above.

Full suite green throughout (`EditMode 2748/2748`, `PlayMode 722/722`, 25
skipped, unchanged from before this gate -- no scenes were rebuilt so no
PlayMode test newly exercises the shop).

### Gate 3

**Recorded 2026-09-03, branch `shop-v2`, commit `c93432a`.** Built: the full
content chain for `bookOnly`/`bookTier` (Phase A, additive -- `unlockLevel`
kept, `bookOnly` false everywhere); `RunSnapshot.learnedSpells`/
`unassignedSpellBooks` with tolerant `Reconcile`; `RunOrchestrator.CanLearn`/
`LearnSpell`/`ReplaceSpell` (return-to-pool on replace, §7.1 point 5) and
`BuyBook`; `ShopStock.RollBooks` drawing real candidates; `VictoryRewards.
RollSpellDrop`; the book card's purchase-time facts; the dossier's fourth
nav row and assignment panel; the map's pending-book indicator; the bot's
`ChooseSpellAssignment` for all four archetypes plus the `BuyBook` shop
choice they were missing entirely until this gate.

**A real bug found and fixed before the exit numbers meant anything.** The
first batch run against this gate showed `shop.spendShareBySection.books`
at 42-65% (books being bought) against `spellAcquisition.
learnedFirstSpellByStep` flat at 0% for every cell -- a contradiction that
could only be a missing wire. `RoomTrace`'s new fields
(`LearnedSpellCountAfterRoom`, `UnassignedSpellBookCountAfterRoom`,
`SpellAssignments`) were being set correctly on the C# object but never
reached `runs.jsonl`: `BalanceBotRunner.RunRowJson` hand-maps every
`RoomTrace` field into JSON rather than reflecting the object, and the new
fields were never added to that map. Fixed in `c93432a`; a 30-run smoke
batch confirmed real per-choice traces (`Outcome: "Ok"`, slots filling in
order, `Skip` once full) before the exit batch below was run.

**Exit batch: 12,000 runs** (4 archetypes × 3 profiles × 1,000 seeds),
`reports/bot/20260903-190102`, commit `c93432a`.

| archetype | profile | learned@8 | @16 | @24 | @32 | slots/leg | unaffordable-book share |
|---|---|---|---|---|---|---|---|
| RandomLegal | Fresh | 60.6% | 85.8% | 94.9% | 83.3% | 0.14 | 21.6% |
| GreedyAggressive | Fresh | 68.7% | 93.5% | 97.8% | 89.5% | 0.91 | 25.0% |
| GreedyDefensive | Fresh | 69.0% | 93.7% | 98.0% | 98.2% | 1.10 | 24.6% |
| Lookahead2 | Fresh | 60.7% | 87.3% | 97.6% | 91.6% | 1.07 | 24.9% |
| RandomLegal | Mid | 60.8% | 86.9% | 96.0% | 90.8% | 0.30 | 20.6% |
| GreedyAggressive | Mid | 68.6% | 93.7% | 98.0% | 99.1% | 1.06 | 25.0% |
| GreedyDefensive | Mid | 69.0% | 93.7% | 98.0% | 99.4% | 1.30 | 24.5% |
| Lookahead2 | Mid | 60.7% | 87.6% | 97.7% | 98.9% | 1.13 | 25.0% |
| RandomLegal | Late | 58.7% | 84.3% | 94.6% | 98.3% | 0.84 | 20.2% |
| GreedyAggressive | Late | 64.8% | 92.2% | 97.1% | 99.0% | 1.28 | 26.1% |
| GreedyDefensive | Late | 65.3% | 92.2% | 97.2% | 99.0% | 1.29 | 26.1% |
| Lookahead2 | Late | 59.6% | 86.9% | 96.4% | 99.0% | 1.27 | 26.9% |

**Depth, zero books entering leg 2 (step 8) vs. at least one:** 10 of 12
cells show both groups at the depth cap (40) -- by step 8 almost every run
already holds a book (the per-fight drop rate plus shop availability over
~8 fights leaves few runs with nothing), so there is barely a "zero" group
left to compare in most cells. The one cell with a real split,
`RandomLegal`/Fresh, shows 38 vs. 37 -- statistically noise at this sample
size, not a depth penalty. Read together with the matched-seed shop-vs-no-
shop comparison from gate 1 (also no depth cost), **the plan's Phase D gate
condition (b) -- "runs that reach leg 2 bookless do not lose materially more
depth" -- holds**, though the "zero books" group is thin enough in most
cells that this should be re-checked once gate 4's own batch runs with the
flip live and combat outcomes actually depend on it.

**Reading against the plan's own gate-4 entry condition (a) -- "the median
run has a usable book before the first boss" (step 8):** every cell clears
50%, most clear 60-69%, at the first boss. This is with `bookOnly` still
false, i.e. measuring the ACQUISITION loop alone, independent of whether a
learned spell does anything yet -- which is exactly what Phase A/gate 3 was
staged to prove before gate 4 makes it load-bearing.

**Human checklist (did a tester find the unassigned book, buy a duplicate
by accident, regret a replacement) is NOT run**, for the identical reason
gate 2's checklist is open: it needs the same built scene the PlayMode walk
test needs, and `Scenes/` is still carrying another live session's
uncommitted work as of this recording. Same process block as gate 2's exit
record, not a new one.

Full suite green throughout (`EditMode 2751/2751`, `PlayMode 740/765`, 25
skipped).

### Gate 4

**Recorded 2026-09-03, branch `balance-bot`.** Built: `PlayerKit.BasicSpell`
removed entirely (the free, nameless "Skill" action every character got
regardless of what they had learned, driven by `spells.json`'s level-keyed
tier ladder via `FightController.SpellTierFor`); `skills.json`'s five spell
entries flipped to `bookOnly: true` with `unlockLevel` removed, so they are
now reachable only through `RunOrchestrator.LearnSpell`/the shop, never by
levelling. `FightSession.ExecuteSkill`/`ExecuteSkillInner` and every HUD/bot
surface that named the basic spell (`FightHudModel.BasicSpellRow`/
`IsBasicSpell`, `FightController.Hud.cs`'s basic-spell detail fallback,
`FightAction.FightActionKind.BasicSpell`, three bot policies' basic-spell
scoring) removed with it.

**The spell tier ladder itself is NOT removed** (`SpellTierDefinition`,
`ContentDatabase.SpellTiers`, `FightEncounterAdapter.TierAtLevel`) — it still
scales `PlayerKit.SkillPowerMultiplier`, the multiplier every FIXED-damage
skill (`frost_flare`, `lightning_bolt`) reads alongside `EffectiveSkillScaling`'s
INT/WIS grade. Dropping this to a flat 1 across the board would have been a
silent damage nerf to two named spells that nothing in the plan's own F4
section flagged, because F4 read the ladder as powering only the free action.
Caught mid-cut by a compile error once `BasicSpell` was removed and
`SkillPowerMultiplierFor` turned out to read `KitFor(actor)?.BasicSpell?.
PowerMultiplier`; fixed by carrying the multiplier itself on the kit
(`CombatantKit.cs`'s new `SkillPowerMultiplier` field, still fed by the same
`TierAtLevel` lookup in `FightEncounterAdapter`) rather than deleting the
whole `BasicSpell` struct and the lookup with it.

**A second, larger coupling found and NOT silently absorbed.**
`FightSession.Skills.cs`'s `ApplySkillRoleEffect` — Tank lifesteal,
CrowdControl defense-shred, Support party-heal, Utility signature-gain, and
the Assassin execute-bonus message — turned out to have exactly one caller
anywhere in the codebase: the now-deleted `ExecuteSkillInner`. Confirmed by a
full-repo grep before touching it. This was already true before this gate:
none of the five role riders had ever fired on an authored/named skill cast
(`ResolveDamageSingle`, `ResolveDamageAll`, `ResolveDamageInstances`), only
on the free basic-spell action every character always had regardless of what
they had learned. Removing `BasicSpell` as planned would have taken these
five class-identity mechanics from "narrowly reachable" to "unreachable by
any code path," with no design decision behind the loss.

Flagged to the user rather than resolved unilaterally — porting the effect
into the authored-skill path is a real balance change (every Tank skill
would now lifesteal, every Support skill would now heal the party, not just
the one free action), not a mechanical cleanup. **Decided 2026-09-03: remove
them.** `ApplySkillRoleEffect` and its five `FightTuning` constants
(`AssassinExecuteHealthFraction`, `AssassinExecuteBonusMultiplier`,
`TankSkillLifestealFraction`, `CrowdControlDefenseShred`,
`SupportSkillPartyHealAmount`, `UtilitySkillSignatureGain`) are deleted, not
ported. The seven `SkillDispatchTests` role-rider tests that only ever
exercised this path went with it.

**Test fallout, all fixed.** ~25 test-file compile errors across
`EditMode`/`PlayMode` from the `BasicSpell`/`ExecuteSkill`/
`FightActionKind.BasicSpell`/`ResolvedSpellTier`-as-kit-arg removal — mostly
fixtures that used the free basic spell as a cheap way to reach a back-rank
foe or to prove a cast is not a swing, rewritten against an authored
`ResolvedSkill` instead. Three tests failed for real reasons after content
rebuilt: `SpellBooksTests.TheFiveSpellsCarryABookTierAndKeepTheirUnlockLevel`
pinned Phase A's `bookOnly: false` snapshot (rewritten as
`...AndAreUnreachableByLevelling`, asserting `bookOnly: true` and
`unlockLevel == int.MaxValue`); `LearningABookDoesNotChangeWhatTheLevelRoute
AlreadyGrants` pinned Phase A's no-op (rewritten as
`LearningABookIsTheOnlyWayToReachIt`, now the load-bearing proof the flip
landed — a level-9 character has `mud_burst` unavailable until it is
learned); `SpellVfxTests.TheTwoStrikeSpellsSitOnAReachableRungOfShawnsLadder`
pinned `frost_flare`/`lightning_bolt`'s old unlock levels (5/7), rewritten
against `bookOnly`/`bookTier` (2/3) since both are off the level ladder now.

Full suite green: `EditMode 2740/2740`, `PlayMode 740/765` (25 skipped, same
skip count as gate 3 — no new skips introduced).

**NOT done, carried forward from gates 2/3, not new here:** the human
usability checklist (§7.1 point 9) and any live UI walk test — `Scenes/` is
still carrying another live session's uncommitted work as of this recording,
so nothing that needs a built scene has run.

**Post-commit code review (2026-09-03, medium effort, 8 finder angles + 1-vote
verify) found and fixed two real latent bugs and one plausible one, both in
files this gate touched:**

- `FightController.Hud.cs`'s `CurrentDetail()` indexed `kit.Skills` by the raw
  submenu row position instead of the selected option's own kit index — the
  moment any skill authors a `requires` ability-score gate (a real, validated,
  currently-unused content field), a filtered-out skill earlier in the list
  would desync every row after it from the detail panel it's supposed to
  describe. Fixed to route through `SkillOptionsFor`, matching how
  `FightController.Input.cs`'s `CastSkill` calls already dispatch.
- `FightEncounterAdapter.cs`'s new `TierAtLevel` reimplemented
  `ContentDatabase.GetSpellTierForLevel` level-only, silently dropping the
  ability-score requirement gate the canonical method enforces — inert only
  because no `SpellTierDefinition` currently authors a non-zero
  `requirements` above level 1. Fixed by routing both `KitFor` overloads
  through the existing `ContentDatabase.EffectiveSkillPowerMultiplier`
  (in-run) / `GetSpellTierForLevel` (definition-only, no `Character` to ask)
  instead of a second, narrower lookup.
- `FightController.Input.cs`'s Skill verb opened the submenu with no guard
  for a character having zero castable rows, unlike the Item verb three
  lines below it (`"No items to use."`). Not reachable with any of the three
  currently-shipped playable characters, but this gate is the change that
  makes a bookOnly skill with no `unlockLevel` a live content shape, so the
  next character or spell authored without a remaining level-1 skill would
  hit an empty, silent dead end. Fixed with the same guard shape as Item.

Also flagged and fixed: a stale comment on `RelicsAfterCast` still claiming
`skill` can be null for the (now-removed) basic Skill action, contradicting
`TryFirstRune`'s own updated comment 80 lines below; and three uses of the
banned intensifier "genuinely"/"genuine" in new comments/docs (global
`CLAUDE.md`'s voice rule). Not fixed (reported, not acted on): ~11 near-
identical `ResolvedSkill`/`PlayerKit` test fixture blocks copy-pasted across
8 PlayMode files, and a 5th/6th near-identical "bolt" skill test helper added
to EditMode tests rather than one shared factory — real duplication, left for
a `/simplify` pass rather than folded into this gate's commit.

**Gate-4 exit batch: 12,000 runs** (4 archetypes × 3 profiles × 1,000 seeds),
`reports/bot/20260903-205526`, commit `3aee8a1` (this gate's own commit —
the flip is live for this batch, unlike gate 3's acquisition-only numbers).
Spell-acquisition shares are within noise of gate 3's batch (expected: the
roll/pricing logic didn't change here) — median depth still reaches the
40-step cap for every archetype but RandomLegal in every profile, so the
flip did not collapse run length at the population level.

**A real regression found, not a Gate-4-introduced bug but a Gate-4-worsened
one.** 26 of 12,000 runs (0.22%) hit the `TooManyCommands` invariant — a
fight stalemate where neither side loses health for 60 commands running,
all 26 alternating `Skill:provoke`/`Skill:woolgathering` (both non-damaging),
concentrated in `GreedyAggressive`/`GreedyDefensive` at the `Late` profile.
This exact invariant, with the exact same archetype/profile concentration,
already existed in gate 3's own exit batch (`reports/bot/20260903-190102`) —
but at 3 of 12,000 runs (0.025%), and there the spammed action was `provoke`
alone, never paired with `woolgathering`. The roughly 9x frequency increase
and the new co-occurring action are consistent with (not proven by, no
further root-causing done this session) BasicSpell's removal: a Greedy
policy stuck scoring every damaging option at zero in a defensive standoff
used to have one more legal action in its pool (the free basic spell) that
could occasionally break the tie; with it gone, the tie increasingly
resolves onto two non-damaging utility skills instead. Pre-existing bug,
not fixed here, not investigated further — flagged because a livelock a
player could actually hit (not just the bot) is a real defect regardless of
its rate, and the rate moving in this gate's own batch is worth a second
look before this ships.

### Follow-ups recorded 2026-09-04

Three items noticed while refreshing the audit table below, none acted on here:

- **`AUDIT.md` #58's table is stale.** Five of Shawn's eight level-route
  skills are book-only as of gate 4, commit `3aee8a1`: `static_fleece`,
  `golden_fleece`, `mud_burst`, `frost_flare`, `lightning_bolt` all carry
  `bookOnly: true` with `unlockLevel` removed (`Assets/_Project/ContentData/
  skills.json:30-40,56-68,71-92,95-120,123-144`). The level route now holds
  only `shear`, `woolgathering` and `battering_ram`
  (`skills.json:5-9,17-21,43-47`; `provoke`/`headbutt`/`black_ram_mode` also
  remain on the level route but at `unlockLevel: 999`, i.e. talent-gated,
  not naturally reached by levelling). Could not be corrected in `AUDIT.md`
  itself — `git status` at the start of this session already showed
  `AUDIT.md` modified, carrying another live session's uncommitted work, and
  this task was scoped to `GAP_AUDIT.md` only.
- **`rampaging_bulls_horn` has never been offered in any bot batch.**
  `RelicPool.RequiresConvergenceAbility` gates it to a party with a
  convergence ability (`Domain/Relics/RelicPool.cs:18-19,63`), and the only
  convergence effect is `SkillEffect.Transform` (`Domain/Combat/Session/
  ConvergenceGate.cs:34`) — carried by `black_ram_mode`
  (`Assets/_Project/ContentData/skills.json:172-176`), which sits at
  `unlockLevel: 999` and is therefore talent-gated, not levelled into. None
  of the four bot policies' talent choices are known to pick that talent, so
  no bot run has fielded a convergence ability and this relic has not been
  exercised at scale. By design — the relic is meant to require the talent —
  but untested at scale, which is worth flagging before it ships on that
  basis alone.
- **`forest_wardens_tooth` is the deliberate mechanic-less placeholder.**
  `relics.json`'s own `_placeholders` note says so directly: it exists to
  exercise the draft/rarity/glossary machinery against a full-size pool
  before anyone designs what it does, and it is the only relic left in that
  state (`Assets/_Project/ContentData/relics.json:3,86-91`). Not a gap to
  close, just documented here so it isn't mistaken for one.

### Post-fix batch, 2026-09-04

**12,000 runs, `reports/bot/20260904-092240`, commit `0b61fcf`** (livelock
fix, one-Shop-per-leg `d07ffb4`, and the cleanups `ce25617`/`803ed1b`
included). The `TooManyCommands` livelock the gate-4 batch surfaced at
26/12,000 is at **0/12,000**; determinism 1200/1200 clean. Root cause and
fix are in `0b61fcf`'s message: both Greedy policies chose a target enemy
first (lowest HP / most threatening) and then discarded every action that
could not reach it — Attack included — so a back-rank `rat` reachable only
by the non-damaging `provoke` locked the policy into provoke/woolgathering
forever. Bot-policy bug, not a combat rule: a human keeps attacking the front
beetle. BasicSpell's removal (gate 4) exposed it because the free ranged
spell used to be the one action that could always reach the locked target.
Fixed by selecting the target only among enemies a damaging action can
reach (`Domain/Bot/DamagingTargetSelection.cs`), wiring
`NonDamagingSkillGuard` into both policies' fallback, and no longer
expanding Self/AllEnemies/Party skills into one action per enemy
(`FightAction.LegalActions`).

Shop visits per run roughly doubled (RandomLegal/Fresh 2.9 → 4.0,
GreedyDefensive/Mid 2.9 → 5.3) — the per-leg guarantee doing its job; first
learned spell by step 8 unchanged within noise (57-69% per cell); median
depth unchanged (40 for every non-Random archetype in every profile).

**Open, found by this batch: the bot is ~28x slower per run than
`tools/bot.ps1`'s own documented baseline.** The header records 79.5 s for a
12,000-run batch on 4 shards (~6.6 ms/run); this batch took 2211 s
(~184 ms/run) and the gate-4 batch 1760 s. Machine contention was real (an
interactive Editor, its import workers, and the test-runner copies were all
up) but does not explain the magnitude. Being profiled at the time of this
note; suspects are the per-visit stock roll (visits just doubled), spell
assignment after every room, or the per-run trace/JSON writer.

Still not run: the human usability checklists (gates 2-4) and any live UI
walk — the scene rebuild that needs `Scenes/` (held by another session) was
handed to the user to run.

## Audit table

| Handoff section | Spec'd | Built | Verdict | Decision served |
|---|---|---|---|---|
| §1 Overview — Shop is an in-run room spending run gold | A map room that opens a purchase panel over the map | `RoomType.Shop` generates at weight 3 in the `MiddleRooms` table (`Domain/Dungeon/DescentMap.cs:200-207`), and `EnsureLegHasShop` now guarantees at least one Shop node per descent leg (`DescentMap.cs:526-576`, commit `d07ffb4`). Arrival is handled by `RunOrchestrator.ArriveAt`, which special-cases `RoomType.Shop` — calls `EnsureShopStock()` and returns `Arrival.Shop` (`Core/Bot/RunOrchestrator.cs:240-248`) — **before `RoomResolver.Resolve` ever runs**. `RoomResolution.Kind.ShopNotBuilt` (`Domain/Dungeon/RoomResolution.cs:35,99-100`) and `RoomResolver.cs:92-94`'s "not built" message are now dead code for `RoomType.Shop`: nothing in `ArriveAt` calls `RoomResolver.Resolve` on that branch, only `RoomResolver.Reset()`. The panel itself is `ShopScreen`/`ShopController`, nested inside `MapScreen` and opened via `MapController.OpenShop()` (`Core/MapController.cs:176-182`). | built -- differs: opens a nested panel, not a modal "purchase panel" of unspecified shape; the `ShopNotBuilt`/"not built" path this row originally cited is dead code, not merely superseded | machinery |
| §1 Overview — run gold has a sink | Gold spent in-run | `BuyGear`/`BuyRelic`/`BuyBook` debit `run.gold` on purchase, `Sell` credits it, `RerollSection` debits it (`Core/Bot/RunOrchestrator.Shop.cs:161-331`); `ShopController.Commit`/`Reroll`/`SellRow` are the UI callers (`Core/ShopController.cs:165-278`) | match | machinery |
| §1 Overview — the automatic per-level spell is removed | No `PlayerKit.BasicSpell`; spells are learned books | `PlayerKit` (renamed from the row's era, still the kit type) carries no `BasicSpell` field — its fields are `Id`, `Role`, `Level`, `Skills`, `Relics`, `AttackType`, `SkillPowerMultiplier` (`Domain/Combat/Session/CombatantKit.cs:24-93`); the field and its ladder-driven free action were removed in gate 4, commit `3aee8a1`, along with `FightHudModel.BasicSpellRow`/`IsBasicSpell` and `FightAction.FightActionKind.BasicSpell` (per the Gate 4 exit record above) | match | machinery |
| §1 Overview — 3 spell slots per character per run | `RunSnapshot` carries learned book ids per character | `RunSnapshot.learnedSpells: List<LearnedSpellEntry>` (`{characterId, skillId, slot}`, `Data/RunSnapshot.cs:239,255-261`), gated to `SpellBooks.MaxSpellSlots = 3` by `RunOrchestrator.CanLearn`'s slot loop (`Core/RunOrchestrator.Spells.cs:25-36`, `Domain/Content/SpellBooks.cs:12`) | match | machinery |
| §1a (PLAN_SHOP, new) — a book is a content flag, not a new asset type | `bookOnly: true` + `bookTier` added to the five spell entries in `skills.json`, `unlockLevel` kept until Phase E (additive) | Done, but `unlockLevel` was dropped rather than kept: `static_fleece`, `golden_fleece`, `mud_burst`, `frost_flare`, `lightning_bolt` all carry `bookOnly: true` + `bookTier` and no `unlockLevel` (`Assets/_Project/ContentData/skills.json:30-40,56-68,71-92,95-120,123-144`) — gate 4 (commit `3aee8a1`) removed `unlockLevel` from these five as part of flipping them off the level ladder, which Phase A's own "additive" framing here did not anticipate | built -- differs: `unlockLevel` removed, not kept, per gate 4's later decision | machinery |
| §1g (new) — a bought book has a home before it is learned | `RunSnapshot.unassignedSpellBooks: List<string>` | `RunSnapshot.unassignedSpellBooks` (`Data/RunSnapshot.cs:247`); `BuyBook` appends to it (`Core/Bot/RunOrchestrator.Shop.cs:243-244`); `LearnSpell`/`ReplaceSpell` remove from it (`Core/RunOrchestrator.Spells.cs:73,113-115`) | match | machinery |
| §1g (new) — assignment happens on the dossier, not the shop | `CharacterDossierScreen` gains a spell-books panel reusing README §4/§5.3's recipient/replace-picker states | Built, gate 3: `CharacterDossierScreen.BuildSpellsPanel` (`Domain/UiKit/Screens/CharacterDossierScreen.cs:565-683`) — a fourth nav row (`SpellsRow`, `:77-82,301`) opening a panel with `SpellBooks.MaxSpellSlots` slot buttons and an unassigned-copies list; `CharacterDossierController.OnSlotPressed`/`ShowSpells`/`RefreshSpells` wire it to `LearnSpell`/`ReplaceSpell` (`Core/CharacterDossierController.cs:322-385`) | match | machinery |
| §2 Files — designer prototype | `Shop Screen v2.dc.html` + `support.js` | Unchanged since the 2026-09-03 pass: `docs/handoffs/shop_v2/Shop Screen v2.dc.html` + `support.js` + `image-slot.js` | match — file exists; the layout/state machine it specifies was deliberately not copied verbatim (see the §3/§3f/§4/§5 rows below) | machinery |
| §2 Files — live v2 reference renders | Screenshots of the hub store and relic screen | Still not renderable by name: `tools/screenshot.ps1 -Panel` addresses panels via `ScreenRegistry`, and the shop is nested inside `MapScreen`/`MapPanel` rather than being its own top-level entry (`Editor/SceneBuilder/ScreenRegistry.cs`, `WireShop` at line ~641) | deliberate-deviation — unchanged from the prior pass; the screen exists now but is still not independently screenshot-addressable | machinery |
| §3 Layout — header, gold chip, reroll button with price | Coordinates at 1920×1080 | Built, gate 2, as a flat vertical stack rather than the spec'd pixel coordinates: `ShopTitle`/`ShopGoldLabel`/`ShopLeaveButton` at fixed `Place.At` offsets, one `Reroll` button per section rather than one global reroll (`Domain/UiKit/Screens/ShopScreen.cs:117-133,210-213`) | built -- differs: layout is a hand-placed stack, not the coordinate table, and reroll is per-section (three buttons) rather than one global button with one price (see the gate 2 exit record's deviation #1) | reroll-or-walk |
| §3 Layout — spell section, 3 cards | 380×236 at y 156 | Superseded by gate 1's shelf-size decision — this is now the Book section at `ShopStock.BookCount = 3` cards, 380×150 (`ShopScreen.CardWidth`/`CardHeight`, `:42-43`), built via `BuildSection("Book", ...)` (`ShopScreen.cs:145-147`) | superseded -- see the Gate 1 exit record ("Decided 2026-09-03") and the §7.1 point 3 row below; the surviving shape (3 book cards) is built, just not at this row's original coordinates | patch-or-reinforce |
| §3 Layout — recipient strip, 3 rows × 3 chips | **Relocated 2026-09-03** (`README.md` §0.1, `PLAN_SHOP.md` §1g) — not a shop element any more. Now: a dossier panel, same 3-row count against `EffectiveMaxSquadSize()`, coordinates unspecced (§1g) | Built, gate 3, but as 3 SLOT chips per character (`SpellBooks.MaxSpellSlots`), not 3 rows against squad size — the dossier screen is per-character already, so there is no per-squad-member row to build here: `CharacterDossierScreen.cs:588-627`'s `for (int i = 0; i < SpellBooks.MaxSpellSlots; i++)` loop builds `SpellSlots` | built -- differs: 3 per-character slot chips, not 3 squad-member rows — the relocation changed what "3" counts | machinery |
| §3 Layout — detail panel | **Superseded 2026-09-03** (`README.md` §0.3, `PLAN_SHOP.md` §3d) — a hover/keyboard-focus tooltip, 320×auto, positioned per §3d, not a persistent 548×112 panel. Contents drop the recipient-delta row (§3d); weapon cards show the damage line, not a flat stat | Built, gate 2, as a fixed-position `DetailLabel` (1600×50 at `Place.At(0,-340)`, `ShopScreen.cs:152-156`) painted from whatever is globally selected (`ShopController.PaintDetail`, `Core/ShopController.cs:390-418`) — a click-driven label, not a hover/keyboard-focus tooltip, and it shows `name - meta - description` text, not a weapon damage line (`ItemDescription.Compare`, unused by this screen, is now called from the dossier instead: `Core/CharacterDossierController.cs:1212`) | built -- differs: fixed-position, selection-driven label, not a floating hover/focus tooltip; no damage-line formatting (see the gate 2 exit record's deviation #2) | machinery |
| §3f (new) — five-panel grid layout in pixels | 1920×1080 coordinate table for Relics/Spell Books/Shopkeeper/Gear/Shop Actions (`PLAN_SHOP.md` §3f) | Not built as a pixel grid — `ShopScreen.Build` is a flat vertical stack instead (`Domain/UiKit/Screens/ShopScreen.cs:104-174`), stated as a deliberate deviation in the gate 2 exit record ("Restyling to the five-panel shape is follow-up, not done here") | built -- differs: the screen exists and covers the same content, laid out as a stack, not this row's five-panel pixel grid | machinery |
| §3f (new) — `PACK` modal layout | 640×700 centred, row list, quantity chips (`PLAN_SHOP.md` §3f) | Built, gate 2: `ShopScreen.BuildPackModal`, a 1100×860 modal (not 640×700) with `PackRowCount = 6` rows, `SELL 1`/`SELL ALL` buttons per row rather than quantity chips, and prev/next paging for a bag bigger than 6 (`ShopScreen.cs:70,258-354`); wired in `ShopController.OpenPack`/`SellRow`/`StepPackPage` (`Core/ShopController.cs:225-285`) | built -- differs: 1100×860 not 640×700, two dedicated sell buttons per row instead of `1`/`ALL` quantity chips, otherwise matches | patch-or-reinforce |
| §3 Layout — gear section, 4 cards | 420×204 at y 448 | Built, gate 1's revised count: `ShopStock.GearCount = 4` (`Domain/Rewards/ShopStock.cs:164`), cards 380×150 not 420×204 (`ShopScreen.CardWidth`/`CardHeight`), section built via `BuildSection("Gear", ..., 414f, 295f, ShopStock.GearCount, ...)` (`ShopScreen.cs:142-144`) | built -- differs: card count (4) matches the gate-1 decision, but card size and y-coordinate do not match this row's spec (flat-stack layout, see §3f row above) | patch-or-reinforce |
| §3 Layout — relic section, 3 cards, no paging | 380×220 at y 708; `ShopStock.RelicCount = 3` and a section never shows more cards than its constant | Built: `ShopStock.RelicCount = 3` (`ShopStock.cs:166`), section built via `BuildSection("Relic", ..., -86f, -205f, ShopStock.RelicCount, ...)` (`ShopScreen.cs:148-150`), no paging on any shop section — `RelicDraftController`'s pager (`Core/RelicDraftController.cs:76,129-149`) remains unused here | built -- differs: card size/position not at spec (380×220 @ y 708 vs. this screen's 380×150 stack), count and no-paging both match | save-or-spend |
| §3 Layout — worst-case string table | Every price field sized for 4 digits plus a suffix; sell meta for `T10 · +5 · 3 AFFIX` | Not verified as a deliberate worst-case sizing pass — `ShopScreen`'s price/meta labels are fixed-width (`ShopScreen.cs:239-246,330-337`) and are checked by `UiTextFitAudit` at build time (`Editor/SceneBuilder/SceneBuilder.cs:115`), which the gate 2 exit record says passed clean at all four aspects, but no comment or test pins a specific worst-case string against these fields the way this row asks | unverified -- whether the field widths were sized against this row's specific worst-case strings, versus merely passing the audit's generic overflow check | machinery |
| §3 Layout — sell panel, scrollable bag list with price per row | **Superseded 2026-09-03** (`README.md` §0.4) — a `PACK` modal replaces the inline viewport; see the §3f row above for its layout | Built, gate 2, as the `PACK` modal (see the §3f `PACK` modal row above) — paged rather than scrollable, 6 rows per page | superseded -- see the §3f `PACK` modal row above, now built | patch-or-reinforce |
| §3 Layout — leave button | 320×64 at y 952 | Built: `ShopLeaveButton`, 220×56 at `Place.At(800, 500)`, not 320×64 at y 952 (`ShopScreen.cs:128-133`); two-press confirm via `ShopController.Leave` (`Core/ShopController.cs:209-221`) | built -- differs: size/position not at spec, behavior (two-press leave) matches §5.6 | reroll-or-walk |
| §3 Layout — survives the audit at four aspects | Clean at 1920×1080, 2580×1080, 1920×1440, 1920×1200 | Built and verified: `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) is the four-aspect set; `ShopScreenTests.TheScreenAuditsCleanAtEveryFrame`/`ItAuditsCleanInsideTheMapItMountsIn` pin clean at all four, both standalone and nested in `MapScreen` (per the Gate 2 exit record above) | match | machinery |
| §4 States — affordable / unaffordable with `NEED {n}` | Per-card opacity, border and price-text treatment | Built as a price-TEXT treatment only, no opacity/border change: `ShopController.PaintSection` sets the price label to `ShopCardNeed` (`NEED {n}`) when `run.gold < entry.price`, `ShopCardConfirm` when selected, `ShopCardSold` when sold, otherwise the plain price (`Core/ShopController.cs:329-346`) | built -- differs: price-text state only; no per-card opacity or border treatment for affordability | machinery |
| §4 States — sold out holds its position | Card stays at index, row does not re-centre | Built: `ShopStockEntry.sold` is a flag, cards are bound and painted by fixed index (`ShopStock.cs:56`, comment at `:32-34`); `PaintSection` iterates `cards[i]`/`EntryAt(section, i)` in place, never removing or reordering (`ShopController.cs:312-347`) | match | machinery |
| §4 States — `NO OFFER` for an undersized pool | Leftover cards render in place, never hidden or duplicated | Built: `ShopStockEntry.NoOffer`/`noOffer` flag held explicitly (`ShopStock.cs:43-48,118-127`); `PaintSection` blanks name/meta and sets the price label to `ShopCardNoOffer` for a `noOffer` card, in place at its own index (`ShopController.cs:317-323`) | match | machinery |
| §4 States — text/shape cue on every accented state | **Partially relocated 2026-09-03** — lock + `NEED n` and the focus ring still apply to every shop card; `✓` on the selected recipient, `REPLACING` and `REPLACE REQUIRED` are dossier-only now (`README.md` §0, `PLAN_SHOP.md` §1g) and are not shop cues any more | `NEED n` built (see the affordable row above); no lock icon or focus ring on any shop card — selection is mouse-only, painted via price-text (`ShopCardConfirm`) rather than a visual lock/ring (`ShopController.cs:335-337`) | built -- differs: text cue (`NEED n`) built; no lock icon and no focus ring exist (mouse-only selection, see the keyboard-nav row below) | machinery |
| §4 States — keyboard focus ring and focus order | **Revised 2026-09-03** (`PLAN_SHOP.md` §3d) — relic cards → spell cards → item cards → reroll → pack → leave; no recipient rows in this order any more, and focus-or-hover anchors a tooltip, not the persistent detail panel this row originally described | Not built: `ShopScreen`/`ShopController` wire `Button.onClick` only (`Core/ShopController.cs:98-127`), no explicit `Navigation` graph or focus order; confirmed absent alongside the §7.1 point 8 keyboard-navigation-graph row below, not merely unconfirmed | not built | machinery |
| §4 States — relic owned-already is filtered, not drawn | Held relics excluded from the roll | Built: `RollOneSection`'s relic branch draws from `AvailableRelicOptions()` (`Core/Bot/RunOrchestrator.Shop.cs:393-395`) — held-relic exclusion now lives in the shop's own candidate list, the same shape the row anticipated from the draft's precedent | match | save-or-spend |
| §4 States — spell slot full / owned-by / would-fill / would-replace | **Relocated 2026-09-03** — these are dossier states now (`README.md` §0.1/§0.2, `PLAN_SHOP.md` §1g). In the shop, a spell card only ever carries the plain affordable/unaffordable/sold/no-offer/armed states, identical to gear | Built on both sides: the shop's book card carries only the plain affordable/unaffordable/sold/no-offer states, identical to gear (`ShopController.PaintSection`, shared code path for all three sections); the dossier's slot buttons show full/empty and drive `LearnSpell` (empty slot) vs. `ReplaceSpell` (occupied slot) per `CharacterDossierController.OnSlotPressed` (`Core/CharacterDossierController.cs:340-368`) | match | machinery |
| §4 States — selected-for-sell | Row lift, `SELL · {n} G` label | Built, differently: no row-lift animation; each pack row shows its sell price via `ShopSellPriceLabel` (`Core/ShopController.cs:467-469`, `UiStrings.cs:519`) and commits immediately on a `SELL 1`/`SELL ALL` button press (`SellRow`, `ShopController.cs:258-278`) rather than arming a row then confirming | built -- differs: immediate sell-on-press via two buttons, not an armed "selected" row state with a lift/label | patch-or-reinforce |
| §4 States — rerolled | Whole-shop cross-fade, price doubles in place | Built, differently: reroll is per-SECTION not whole-shop (`RerollSection`, `Core/Bot/RunOrchestrator.Shop.cs:307-331`), no cross-fade (repaint is instant, `ShopController.Reroll` → `Paint()`), price doubling is `ShopPricing.RerollPrice`'s `15 << rerollsUsed` shape, capped at 9999 (`Domain/Rewards/ShopPricing.cs:126,134,138-145`) | built -- differs: per-section reroll (three independent rerolls, not one whole-shop reroll), no cross-fade animation; the doubling-and-ceiling shape matches | reroll-or-walk |
| §5.1 Interaction state machine | **Narrowed 2026-09-03** (`README.md` §5.3, revised) — in the shop this is `Idle → Armed → Committed` plus `LeaveConfirm`, arming **per section**; `RecipientSelected`/`ReplaceSlotPicking` no longer occur here and belong to the dossier's assignment panel instead (§1g) | Built, but simplified further than the row's own narrowing: `ShopController` arms one card at a time SCREEN-WIDE, not per section (`_selectedSection`/`_selectedIndex`, `Core/ShopController.cs:71-72,141-157`), per §7.1 point 6's later single-global-selection decision; `_leaveArmed` is the `LeaveConfirm` state (`:74,209-221`) | built -- differs: single global armed selection (§7.1 point 6 superseded per-section arming before this shipped), not the per-section model this row still describes; `LeaveConfirm` matches | machinery |
| §5.1 / §5.6 Escape leaves transient states only | Disarm and close-picker; never leaves the shop | Not built: `ShopController` has no keyboard input handling at all (mouse-only, confirmed by the keyboard-nav row above and the gate 2 exit record's deviation #3) — nothing disarms or closes on Escape | not built | machinery |
| §3f (new) — click-outside disarms on the main screen, not just the modal | A single screen-level handler, any armed section (`PLAN_SHOP.md` §3f) | Not built: no click-outside/backdrop handler exists for the main screen; the `PACK` modal has a backdrop (`ShopPackBackdrop`, `Domain/UiKit/Screens/ShopScreen.cs:260-261`) but no code closes it on an outside click either — only the explicit `ShopPackClose` button (`Core/ShopController.cs:242-247`) | not built | machinery |
| §3f (new) — Escape with more than one section armed | Disarms every armed section at once, not the first in priority order (`PLAN_SHOP.md` §3f) | Not built — moot under the single-global-selection model actually shipped (only one card can ever be armed at a time, `_selectedSection`/`_selectedIndex` are scalars, `Core/ShopController.cs:71-72`), and there is no Escape handling regardless (see the row above) | not built | machinery |
| §5.2 Every mutation is atomic | One orchestrator method: validate, apply (cannot fail), persist once | Built: every `RunOrchestrator.Shop.cs`/`RunOrchestrator.Spells.cs` mutation follows validate/apply/persist (`Core/Bot/RunOrchestrator.Shop.cs:17-30` header, each method's own numbered comments e.g. `:161-192`); `Persisted()` is the single shared step 3 (`:343-348`); `SaveSystem.Save` now returns `bool` rather than swallowing silently (see the `ShopResult`/`SaveSystem.Save returns bool` row below) | match | machinery |
| §5.2 Commit effects per section | **Revised 2026-09-03** (`PLAN_SHOP.md` §2f) — gear/consumable to bag, relic appended, book **not** learned: a book purchase only appends `skillId` to `run.unassignedSpellBooks` (§1g); this row previously said "book learned," which contradicts the §1g row below and is corrected here | Built exactly as revised: `BuyGear` calls `InventoryOps.Add` into `save.stockpiledItems` (`Core/Bot/RunOrchestrator.Shop.cs:187-188`); `BuyRelic` appends to `run.relicIds` (`:212-213`); `BuyBook` appends `skillId` to `run.unassignedSpellBooks`, never calls `LearnSpell` (`:243-244`, confirmed no `CanLearn`/`LearnSpell` call anywhere in `BuyBook`) | match | machinery |
| §5.3 Recipients and the replace picker | **Relocated 2026-09-03, unchanged in shape** (`PLAN_SHOP.md` §1g) — this is the dossier assignment panel's interaction spec now, not the shop's. In the shop, a spell purchase commits with no recipient step at all | Built on the dossier: `CharacterDossierController.OnSlotPressed` picks `LearnSpell` for an empty slot or `ReplaceSpell` for an occupied one (`Core/CharacterDossierController.cs:340-368`); the shop's `BuyBook` names no character and calls neither (confirmed above) | match | machinery |
| §1g (new) — buying a book | Commit appends `skillId` to `run.unassignedSpellBooks`; no character named, no slot chosen (`PLAN_SHOP.md` §1, §2f) | Built: `BuyBook` (`Core/Bot/RunOrchestrator.Shop.cs:226-248`), confirmed above | match | patch-or-reinforce |
| §5.4 Sell from the bag, worn gear excluded | List is `stockpiledItems`; no unequip and no dossier on this screen; the path is buy → leave → equip → sell next shop | Built: `Sell(bagIndex, quantity)` reads/writes `save.stockpiledItems` only (`Core/Bot/RunOrchestrator.Shop.cs:259-301`), comment states "BAG ONLY... worn gear cannot be sold" (`:250-258`); no equip/unequip call anywhere in `ShopController`/`RunOrchestrator.Shop.cs` | match | patch-or-reinforce |
| §5.4 Selling a stack asks a quantity | `1` / `ALL` chips, so a stack is two presses | Built, differently: no quantity-asking step — `ShopScreen`'s pack row has two dedicated buttons, `SellOne` and `SellAll` (`ShopScreen.cs:339-346`), each committing immediately (`ShopController.SellRow(index, all)`, `:258-278`); a stack sale is one press, not two | built -- differs: one-press dedicated buttons, not a `1`/`ALL` chip pair requiring a stack to be two presses | patch-or-reinforce |
| §5.5 Reroll whole shop, price doubles, saturates | 25 / 50 / 100 / 200 … capped at 9999, `n` stored per node | Built, differently: reroll is per-section (confirmed in the "§4 States — rerolled" row above), base price is 15 not 25 (`ShopPricing.RerollBase = 15`, `Domain/Rewards/ShopPricing.cs:126`), doubling via `15 << rerollsUsed` (`:138-145`) so the sequence is 15/30/60/120…, ceiling 9999 (`:134`); `n` is `run.shopRerollsUsed[section]`, one count per section stored on the run, not per node (`Data/RunSnapshot.cs:211`) | built -- differs: per-section not whole-shop, base 15 not 25 (sequence 15/30/60/120... not 25/50/100/200...), ceiling 9999 matches | reroll-or-walk |
| §5.6 Leave returns to map and clears the room | Node marked cleared, no confirm prompt | Built: `LeaveShop` clears the shop stock and calls `RunManager.ClearCurrentRoom` (`Core/Bot/RunOrchestrator.Shop.cs:146-157`); a shop no longer clears on arrival — `RunOrchestrator.ArriveAt`'s `RoomType.Shop` branch explicitly does NOT call `ClearCurrentRoom` (`Core/Bot/RunOrchestrator.cs:240-248`, comment "No ClearCurrentRoom: the room is still live until LeaveShop says otherwise") — this row's original complaint (clearing on arrival) is resolved | match | reroll-or-walk |
| §5.6 Leave confirms once | `LEAVE` → `LEAVE?`; Escape never leaves | Built: `ShopController.Leave()` arms `_leaveArmed` on the first press, commits on the second (`Core/ShopController.cs:209-221`); label switches `ShopLeave`/`ShopLeaveConfirm` ("LEAVE"/"LEAVE?", `UiStrings.cs:465-466`, `ShopController.Paint`, `:295-296`); Escape does nothing at all (no keyboard handling, confirmed above), so it trivially "never leaves" | match | reroll-or-walk |
| §5.6 Same stock after a quit and reload | Seeded per node from three coordinates | Built: `RngStreams.ShopGear`/`ShopBooks`/`ShopRelics` (streams 5/6/7, `Domain/Rng/RngStreams.cs:43-45`), each opened at `(run.step, run.currentNodeId, rerollsForThatSection)` — three coordinates, the third an explicit XOR term added to `Derive` for this purpose (`RngStreams.cs:64-86`); `RollOneSection` is the caller (`Core/Bot/RunOrchestrator.Shop.cs:373-399`); the gate-1 exit record's determinism check (1,200 replayed runs, 0 mismatches) is the batch-level proof | match | machinery |
| §7 Engine — price persisted at roll, plus `stockVersion` | A content or pricing change cannot rewrite an open shop | Built: `ShopStockEntry.price` is resolved and stored at roll time (`Domain/Rewards/ShopStock.cs:55`, comment `:25-30`); `RunSnapshot.shopStockVersion` (`Data/RunSnapshot.cs:225`) is set from `ShopStock.StockVersion` in `EnsureShopStock` (`Core/Bot/RunOrchestrator.Shop.cs:129`) and an older-version shelf is left as rolled per the class comment (`ShopStock.cs:200-208`) — no code path recomputes a stored `price`, and there is no reconciliation pass that rewrites `shopStock` against a newer `stockVersion`, matching "left exactly as it was rolled" | match | machinery |
| §7 Engine — no minimum-resolution policy needed | Settled project-wide by `ScaleWithScreenSize` + `Expand` at a 1920×1080 reference | Unchanged: `Editor/SceneBuilder/SceneBuilder.cs:213-220`; `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) is what the shop screen is checked at too (confirmed by the "§3 Layout — survives the audit" row above) | match — nothing for this screen to add | machinery |
| §6 Design tokens | Palette and type taken from `FightHudPalette.cs` and the two prior handoffs | Not built as specified: `ShopScreen` uses its own hex literals (`"#F2DB9E"`, `"#1A1024E6"`, etc., `ShopScreen.cs:113-263`) and `Styled(TypographyRole.*)` calls rather than reading `FightHudPalette` constants (`Domain/UiKit/FightHudPalette.cs:18-81`) — the screen is styled, just not from this specific token source | built -- differs: the screen has a consistent palette/typography, but it is screen-local hex literals plus `TypographyRole`, not `FightHudPalette`'s constants | machinery |
| §7 Engine — everything is run gold | Chip reads `RunSnapshot.gold`, never `SaveData.Gold` | Built: `ShopController.Paint` sets `goldLabel` from `RunManager.Run.gold` (`Core/ShopController.cs:294`, `UiStrings.ShopGold`); every mutation reads/writes `run.gold`, never `SaveData.Gold` (confirmed across `RunOrchestrator.Shop.cs`) | match | machinery |
| §7 Engine — prices from the plan's formula, not `ItemDefinition.cost` | `Domain/Rewards/ShopPricing.cs` | Built: `Domain/Rewards/ShopPricing.cs` exists — `GearPrice` is `GearBase(20) + GearPerTier(4)*tier`, scaled by `PlusStep`/rift multipliers (`:20-33` and onward), pinned as literals by `ShopPricingTests` per the file's own header; `ItemDefinition.cost` is used only for non-equippable consumables' buy/sell price (`RunOrchestrator.BuyPriceOf`, `Core/Bot/RunOrchestrator.Shop.cs:90-98`), not for gear, matching the row's intent | match | machinery |
| §7 Engine — fixed card count emitted at build time, declared to the count audit | **Revised 2026-09-03** — four arrays on the shop screen (spell/item/relic cards, sell rows in the `PACK` modal), not five; slot chips move to the dossier as their own binding (`PLAN_SHOP.md` §3a) | Built exactly as revised: `ScreenRegistry.Map()`'s `CountBindings` declares `gearCards`, `bookCards`, `relicCards`, `packRows` — four arrays (`Editor/SceneBuilder/ScreenRegistry.cs:623-637`); the dossier's `SpellSlots` binding is separate, on `CharacterDossierScreen`'s own `ScreenDef` | match | machinery |
| §1g (new) — bot: `ChooseSpellAssignment` | A second `IRunPolicy` call, separate from `ChooseShop`'s now-recipient-less `buy-spell(i)` (`PLAN_SHOP.md` §2g) | Built: `IRunPolicy.ChooseSpellAssignment(SpellAssignmentView, RunView, SeededRandom)` (`Domain/Bot/IRunPolicy.cs:65`), separate from `ChooseShop` (`:57`); implemented by all four policies (`GreedyAggressivePolicy.cs:289`, `GreedyDefensivePolicy.cs:458`, `Lookahead2Policy.cs:436`, `RandomLegalPolicy.cs:95`) | match | machinery |
| §3d (new) — weapon tooltip shows the damage line, not a flat stat | `ItemDescription`'s existing dossier damage-line builder (`:145-168`), never a stat delta (`PLAN_SHOP.md` §3d) | Not built on the shop: `ShopController.PaintDetail` shows `ContentDatabase.GetItem(id)?.description`, not `ItemDescription`'s damage-line builder (`Core/ShopController.cs:407-411`); `ItemDescription.Compare` is called only from the dossier now (`Core/CharacterDossierController.cs:1212`), confirming the deviation this row flagged went the way it feared for the shop specifically | not built | patch-or-reinforce |
| §7 Engine — copy through `UiStrings` | Price labels as templates with numeric arguments | Built: `ShopCardPrice`, `ShopCardNeed`, `ShopCardConfirm`, `ShopReroll`, `ShopRerollNeed`, `ShopSellPriceLabel` etc. are all `UiString.Define` templates taking numeric arguments (`Domain/UiKit/UiStrings.cs:463-522`), registered in the strings audit list (`:910-919`) | match | machinery |
| §8 Out-of-scope — Events, hub store, drop-tier rework | Deliberately excluded | Still correctly excluded: `RoomType.Event` resolves through the ordinary (non-shop) `RoomResolver.Resolve` path in `ArriveAt` (`Core/Bot/RunOrchestrator.cs:250-253`), unchanged by the shop work; no hub store or drop-tier rework landed | match | machinery |
| §7.1 point 3 (new) — counts re-decided | `Gear 3 / Relics 2 / Books 1`, not the 3/4/3 above; `ShopStock.SpellCount`/`ItemCount`/`RelicCount` re-set from gate-1's measured affordability before the screen is laid out | Superseded by the Gate 1 exit record's own later decision, recorded in this file above ("Decided 2026-09-03 (author)"): `GearCount = 4`, `BookCount = 3`, `RelicCount = 3` (`ShopStock.cs:164-166`) — ten offers, not this row's earlier 3/2/1 six-offer proposal, which was superseded before code existed to carry it | superseded -- see the Gate 1 exit record's "Decided 2026-09-03 (author)" paragraph above, which is what actually shipped (4/3/3, not 3/2/1) | machinery |
| §7.1 point 7 (new) — per-section reroll, three RNG streams | `RngStreams.ShopGear = 5`, `ShopBooks = 6`, `ShopRelics = 7`, each opened on `(run.step, run.currentNodeId, rerollsForThatSection)`; `RunSnapshot.shopRerollsUsed` becomes `int[]` (`PLAN_SHOP.md` §2b/§2d/§2e/§7.1 point 7) | Built exactly as spec'd (confirmed in the "§5.6 Same stock after a quit and reload" row above): `RngStreams.ShopGear/ShopBooks/ShopRelics = 5/6/7` (`Domain/Rng/RngStreams.cs:43-45`); `RunSnapshot.shopRerollsUsed = new int[ShopStock.SectionCount]` (`Data/RunSnapshot.cs:211`) | match | reroll-or-walk |
| §7.1 point 10 (new) — `ShopResult` / `SaveSystem.Save` returns `bool` | Every shop mutation returns `ShopResult` (`Ok`, `Refused` with a reason, `AppliedNotPersisted`); `SaveSystem.Save` returns `bool` so a swallowed write is visible to the caller | Built: `ShopResult`/`ShopOutcome`/`ShopRefusal` exist exactly as spec'd (`Domain/Rewards/ShopResult.cs:9-101`); every mutation in `RunOrchestrator.Shop.cs`/`RunOrchestrator.Spells.cs` returns one; `Persisted()` maps `SaveSlotManager.SaveCurrent()`'s bool to `Ok`/`AppliedNotPersisted` (`Core/Bot/RunOrchestrator.Shop.cs:343-348`) — did not verify `SaveSystem.Save`'s own signature line-by-line this pass, but `SaveSlotManager.SaveCurrent()` returning a usable bool is confirmed by this call site | match | machinery |
| §7.1 point 1 (new) — `GoldOnArrival` telemetry | `RoomTrace` gains `GoldOnArrival`, `GoldSpent`, `GoldOnLeave`, per-section purchase counts, `RerollsUsed[]`; gate-1 report prints p10/p25/median arrival gold per profile, affordability share, and gold forgone | Built: the Gate 1 exit record above prints exactly this — arrival-gold percentiles by step, per-cell purchases/visit, zero-purchase share, affordability, spend share by section, gold on leave/at death — sourced from `reports/bot/20260903-150857`; did not re-derive the `RoomTrace` field names from `Domain/Bot/RunTrace.cs` this pass, but the printed report is strong evidence the fields exist and are populated | match | machinery |
| §7.1 point 1 (new) — matched-seed leg comparison | Shop-taking policy vs fight-only policy on the same seeds: survival to next boss, power gained, depth variance, replacing the old "shop doesn't cost depth" gate | Built and run: the Gate 1 exit record's "Matched-seed comparison" paragraph above — 3,000 pairs, median depth 40 vs 40, survival 99.66% vs 99.71%, depth variance 5.21 vs 5.44 — is exactly this comparison, via `tools/bot_merge.py --compare` | match | reroll-or-walk |
| §7.3 (new) — gate-1 exit numbers recorded in writing | Counts and prices re-decided here, in this file, before gate 2 starts (`PLAN_SHOP.md` §7.3) | Built: the "Gate 1" section above records the batches, the reading, and the "Decided 2026-09-03 (author)" counts, all before the "Gate 2" section starts | match | machinery |
| §7.1 point 6 (new) — single global selection + `BUY` | One selected card at a time, screen-wide; `BUY` in the Shop Actions panel commits, alongside a second press on the card itself | Built exactly as spec'd: `_selectedSection`/`_selectedIndex` are screen-wide scalars, not per-section (`Core/ShopController.cs:71-72`); `Buy()` commits the current selection (`:159-163`); a second press on the same card also commits (`Select`'s `sameCard` branch, `:141-157`) | match | machinery |
| §7.1 point 8 (new) — keyboard navigation graph | Up/down/left/right neighbours declared per card and action button, over the six primary cards plus three `REROLL`s, `PACK`, `BUY`, `LEAVE`; `Enter` commits, `Escape` clears selection | Not built: confirmed absent both by the gate 2 exit record's own deviation #3 ("No keyboard navigation graph... Mouse only") and directly — no `Navigation` graph, no `Enter`/`Escape` handling anywhere in `ShopController.cs` | not built | machinery |
| §7.1 point 8 (new) — `UiMotion.DurationScale` | One shared Domain constant (default 1) that every animation on the screen reads, so reduced motion is one constant to change later | Not built: no `UiMotion` type/`DurationScale` constant exists anywhere under `Domain/UiKit` (confirmed by search); the shop screen has no animation to gate on it regardless (reroll and card updates repaint instantly, confirmed in the "§4 States — rerolled" row above) | not built | machinery |
| §7.1 point 4 (new) — book card purchase-time facts | `KNOWN BY {NAME}`/`KNOWN BY {n}`, `ELIGIBLE {k}/{m}`, `ALL SLOTS FULL`, `1 UNASSIGNED COPY` badges on the shop's book card and its tooltip, from run state the shop already has | Built: `ShopController.BookFactLine` (`Core/ShopController.cs:493-527`) implements exactly this priority order — `ShopBookAllSlotsFull`, `ShopBookUnassignedCopy(ies)`, `ShopBookKnownByOne`/`ShopBookKnownByMany`, else `ShopBookEligible` (`UiStrings.cs:501-511`) — read via `DescribeEntry`'s book branch (`:381-387`), so it appears in both the card meta line and the `PaintDetail` tooltip | match | patch-or-reinforce |
| §7.1 point 4 (new) — map pending-book indicator | Map screen shows an indicator while `run.unassignedSpellBooks` is non-empty; dossier panel header carries the count. No forced assignment on leaving the shop | Built on the map (`pendingBookLabel`, shown when `run.unassignedSpellBooks.Count > 0`, `UiStrings.MapPendingBook`, `Core/MapController.cs:206-210`); did not verify the dossier panel header itself carries the same count this pass (the dossier's `spellsCount` label reads learned-slot fill, `filled`/`MaxSpellSlots`, not the unassigned pool count — `Core/CharacterDossierController.cs:385`) — no forced assignment on leaving the shop (`LeaveShop` only clears stock/room, `Core/Bot/RunOrchestrator.Shop.cs:146-157`) | built -- differs: map indicator matches; the dossier nav row's count badge shows learned-slot fill, not the unassigned-book count this row also asks for | machinery |
| §7.1 point 5 (new) — return-to-pool replacement | `LearnSpell`'s replace branch appends the displaced slot's `skillId` back to `run.unassignedSpellBooks` before overwriting — no destroy path, no `REPLACING`-as-warning language | Built: `ReplaceSpell` (the replace branch actually lives here, not on `LearnSpell`) captures `displaced = existing.skillId`, removes the new skill from the pool, overwrites the slot, then `run.unassignedSpellBooks.Add(displaced)` (`Core/RunOrchestrator.Spells.cs:86-119`) — no destroy path exists; comment explicitly withdraws the "REPLACING/struck-through-name language" (`:80-85`) | match | patch-or-reinforce |
| §7.1 point 9 (new) — human checklist, gate 2 | Five to eight fresh-player sessions; time to first purchase, can the tester say why the offer they bought was good, did anyone try to sell a worn piece, did anyone misread `NEED n` as the price — recorded in this file's Gate 2 section | Not run: the Gate 2 section above states this explicitly ("NOT run. It needs the same built scene the walk test does") — blocked on `Assets/_Project/Scenes/*.unity` being held by another live session, per `docs/WORKFLOW.md` §4; unchanged as of this pass (`git status` at the top of this session still shows the scenes modified) | not built | machinery |
| §7.1 point 9 (new) — human checklist, gate 3 | Five to eight fresh-player sessions; did the tester find the unassigned book, buy a duplicate by accident, or regret a replacement — recorded in this file's Gate 3 section | Not run, same block: the Gate 3 section above states it explicitly ("NOT run, for the identical reason gate 2's checklist is open") — the dossier assignment panel itself IS built (confirmed above), only the human test session is blocked on `Scenes/` ownership | not built | machinery |
