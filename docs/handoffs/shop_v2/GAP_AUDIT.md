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

### Gate 4

## Audit table

| Handoff section | Spec'd | Built | Verdict | Decision served |
|---|---|---|---|---|
| §1 Overview — Shop is an in-run room spending run gold | A map room that opens a purchase panel over the map | `RoomType.Shop` generates at weight **3**, not 6, in a `MiddleRooms` table now totalling 269 (`Domain/Dungeon/DescentMap.cs:187-194`) — rebalanced since `PLAN_SHOP.md` F2 measured "6 of 90"; the 7:6:3:3 ratio among Event/Treasure/Shop/Rest is unchanged, so F2's *frequency* estimate likely still roughly holds, but its literal weight/total are stale and should be re-derived before a pricing pass leans on them — and resolves to `Kind.ShopNotBuilt` (`RoomResolution.cs:99-100`); `RoomResolver.TryMessage` prints "not built" (`Core/RoomResolver.cs:92-94`) | missing | machinery |
| §1 Overview — run gold has a sink | Gold spent in-run | `RunManager.BankPayout` (`Core/RunManager.cs:396-402`) is the only writer and there is no spender; AUDIT #2 (`docs/AUDIT_V1_ARCHIVE.md:129`) | missing | machinery |
| §1 Overview — the automatic per-level spell is removed | No `PlayerKit.BasicSpell`; spells are learned books | `ResolvedSpellTier` still rides the kit (`Domain/Combat/Session/CombatantKit.cs:48`) and is appended to every skill submenu (`FightHudModel.cs:155-167`) | missing | machinery |
| §1 Overview — 3 spell slots per character per run | `RunSnapshot` carries learned book ids per character | `RunSnapshot` has no such field (`Data/RunSnapshot.cs`) | missing | machinery |
| §1a (PLAN_SHOP, new) — a book is a content flag, not a new asset type | `bookOnly: true` + `bookTier` added to the five spell entries in `skills.json`, `unlockLevel` kept until Phase E (additive) | Not started: `static_fleece` and the other four still carry only `unlockLevel` (e.g. `Assets/_Project/ContentData/skills.json:34`), and `bookOnly`/`bookTier` appear nowhere in content. Nothing downstream of this row (learning, the roll, the price band) has anything to read yet | missing | machinery |
| §1g (new) — a bought book has a home before it is learned | `RunSnapshot.unassignedSpellBooks: List<string>` | Field does not exist | missing | machinery |
| §1g (new) — assignment happens on the dossier, not the shop | `CharacterDossierScreen` gains a spell-books panel reusing README §4/§5.3's recipient/replace-picker states | No such panel; `CharacterDossierScreen.cs`'s existing pack/stats panels are unrelated | missing — and unspecced beyond §1g's pointer; needs its own layout pass before Phase C.5 | machinery |
| §2 Files — designer prototype | `Shop Screen v2.dc.html` + `support.js` | **Produced 2026-09-03**, now at `docs/handoffs/shop_v2/Shop Screen v2.dc.html` + `support.js` + `image-slot.js` (moved from its original location outside `docs/handoffs/`, per `docs/HANDOFF_TEMPLATE.md`'s filing convention) | done — file exists; behavior it specifies is still not built | machinery |
| §2 Files — live v2 reference renders | Screenshots of the hub store and relic screen | Not renderable: `ScreenRegistry.All` is five panels (`Editor/SceneBuilder/ScreenRegistry.cs:55-61`), no store panel exists, and the relic draft is nested inside HubPanel. Committed v1 PNGs used as anchors instead | deliberate-deviation — the screens asked for are not addressable by `tools/screenshot.ps1 -Panel`, whose list is derived from `ScreenRegistry` (`tools/screenshot.ps1:26-33`) | machinery |
| §3 Layout — header, gold chip, reroll button with price | Coordinates at 1920×1080 | No screen tree | not built | reroll-or-walk |
| §3 Layout — spell section, 3 cards | 380×236 at y 156 | No screen tree | not built | patch-or-reinforce |
| §3 Layout — recipient strip, 3 rows × 3 chips | **Relocated 2026-09-03** (`README.md` §0.1, `PLAN_SHOP.md` §1g) — not a shop element any more. Now: a dossier panel, same 3-row count against `EffectiveMaxSquadSize()`, coordinates unspecced (§1g) | No screen tree anywhere. The precedent is `ReckoningScreen.RowCount` (`Domain/UiKit/Screens/ReckoningScreen.cs:94-101`) pinned by `ReckoningTests.cs:183`; the real ceiling is 2 (`Data/SaveData.cs:50`, `:193-199`) | not built | machinery |
| §3 Layout — detail panel | **Superseded 2026-09-03** (`README.md` §0.3, `PLAN_SHOP.md` §3d) — a hover/keyboard-focus tooltip, 320×auto, positioned per §3d, not a persistent 548×112 panel. Contents drop the recipient-delta row (§3d); weapon cards show the damage line, not a flat stat | No screen tree. `ItemDescription.Compare` (`Core/ItemDescription.cs:196-227`) is no longer called from this screen at all (§3d) — the delta it was for doesn't apply once there's no selected recipient | not built | machinery |
| §3f (new) — five-panel grid layout in pixels | 1920×1080 coordinate table for Relics/Spell Books/Shopkeeper/Gear/Shop Actions (`PLAN_SHOP.md` §3f) | No screen tree | not built | machinery |
| §3f (new) — `PACK` modal layout | 640×700 centred, row list, quantity chips (`PLAN_SHOP.md` §3f) | No screen tree. One field flagged undersized against its own worst-case string at spec time (sell-row price, 116px vs. the 148px README §3 already established) and corrected in §3f before any code exists to inherit the mistake | not built | patch-or-reinforce |
| §3 Layout — gear section, 4 cards | 420×204 at y 448 | No screen tree | not built | patch-or-reinforce |
| §3 Layout — relic section, 3 cards, no paging | 380×220 at y 708; `ShopStock.RelicCount = 3` and a section never shows more cards than its constant | No screen tree. The draft's paging (`Core/RelicDraftController.cs:76`, `:129-149`) exists and is deliberately NOT used here | not built | save-or-spend |
| §3 Layout — worst-case string table | Every price field sized for 4 digits plus a suffix; sell meta for `T10 · +5 · 3 AFFIX` | `UiTextFitAudit` (`Editor/SceneBuilder/SceneBuilder.cs:115`) would check it; nothing to check | not built | machinery |
| §3 Layout — sell panel, scrollable bag list with price per row | **Superseded 2026-09-03** (`README.md` §0.4) — a `PACK` modal replaces the inline viewport; see the §3f row above for its layout | No screen tree | not built | patch-or-reinforce |
| §3 Layout — leave button | 320×64 at y 952 | No screen tree | not built | reroll-or-walk |
| §3 Layout — survives the audit at four aspects | Clean at 1920×1080, 2580×1080, 1920×1440, 1920×1200 | `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) would check it; nothing to check | not built | machinery |
| §4 States — affordable / unaffordable with `NEED {n}` | Per-card opacity, border and price-text treatment | No screen tree | not built | machinery |
| §4 States — sold out holds its position | Card stays at index, row does not re-centre | No stock model | not built | machinery |
| §4 States — `NO OFFER` for an undersized pool | Leftover cards render in place, never hidden or duplicated | No stock model | not built | machinery |
| §4 States — text/shape cue on every accented state | **Partially relocated 2026-09-03** — lock + `NEED n` and the focus ring still apply to every shop card; `✓` on the selected recipient, `REPLACING` and `REPLACE REQUIRED` are dossier-only now (`README.md` §0, `PLAN_SHOP.md` §1g) and are not shop cues any more | No screen tree | not built | machinery |
| §4 States — keyboard focus ring and focus order | **Revised 2026-09-03** (`PLAN_SHOP.md` §3d) — relic cards → spell cards → item cards → reroll → pack → leave; no recipient rows in this order any more, and focus-or-hover anchors a tooltip, not the persistent detail panel this row originally described | No screen tree, and no focus-order convention exists on any other screen to inherit | not built | machinery |
| §4 States — relic owned-already is filtered, not drawn | Held relics excluded from the roll | The rule exists in the draft (`Core/Bot/RunOrchestrator.cs:100-107`); no shop roll to apply it in | not built | save-or-spend |
| §4 States — spell slot full / owned-by / would-fill / would-replace | **Relocated 2026-09-03** — these are dossier states now (`README.md` §0.1/§0.2, `PLAN_SHOP.md` §1g). In the shop, a spell card only ever carries the plain affordable/unaffordable/sold/no-offer/armed states, identical to gear | No slot model anywhere | not built | machinery |
| §4 States — selected-for-sell | Row lift, `SELL · {n} G` label | No sell flow | not built | patch-or-reinforce |
| §4 States — rerolled | Whole-shop cross-fade, price doubles in place | No reroll | not built | reroll-or-walk |
| §5.1 Interaction state machine | **Narrowed 2026-09-03** (`README.md` §5.3, revised) — in the shop this is `Idle → Armed → Committed` plus `LeaveConfirm`, arming **per section**; `RecipientSelected`/`ReplaceSlotPicking` no longer occur here and belong to the dossier's assignment panel instead (§1g) | No controller. The deselect rule it generalises is in `RelicDraftController.Select` (`:153-163`) | not built | machinery |
| §5.1 / §5.6 Escape leaves transient states only | Disarm and close-picker; never leaves the shop | No controller | not built | machinery |
| §3f (new) — click-outside disarms on the main screen, not just the modal | A single screen-level handler, any armed section (`PLAN_SHOP.md` §3f) | No controller. The prototype only wires this for the `PACK` modal's own backdrop | not built | machinery |
| §3f (new) — Escape with more than one section armed | Disarms every armed section at once, not the first in priority order (`PLAN_SHOP.md` §3f) | No controller. The prototype's code disarms one section per press, in a fixed order, without this ever being decided | not built | machinery |
| §5.2 Every mutation is atomic | One orchestrator method: validate, apply (cannot fail), persist once | `SaveSystem.Save` already writes via a `.tmp` + `File.Replace` and swallows its own exceptions (`Core/SaveSystem.cs:123-175`), so the boundary that can half-fail is the in-memory apply on `SaveSlotManager`'s cached `SaveData` (`Core/SaveSlotManager.cs:35-46`), not the disk | not built | machinery |
| §5.2 Commit effects per section | **Revised 2026-09-03** (`PLAN_SHOP.md` §2f) — gear/consumable to bag, relic appended, book **not** learned: a book purchase only appends `skillId` to `run.unassignedSpellBooks` (§1g); this row previously said "book learned," which contradicts the §1g row below and is corrected here | `InventoryOps.Add` (`Domain/Inventory/InventoryOps.cs:43`) and `RunOrchestrator.TakeRelic` (`:133-144`) exist and are reusable; no caller | not built | machinery |
| §5.3 Recipients and the replace picker | **Relocated 2026-09-03, unchanged in shape** (`PLAN_SHOP.md` §1g) — this is the dossier assignment panel's interaction spec now, not the shop's. In the shop, a spell purchase commits with no recipient step at all | No spell-book system, on either screen | not built | machinery |
| §1g (new) — buying a book | Commit appends `skillId` to `run.unassignedSpellBooks`; no character named, no slot chosen (`PLAN_SHOP.md` §1, §2f) | No such mutation | not built | patch-or-reinforce |
| §5.4 Sell from the bag, worn gear excluded | List is `stockpiledItems`; no unequip and no dossier on this screen; the path is buy → leave → equip → sell next shop | `stockpiledItems` (`Data/SaveData.cs:163`) and `InventoryOps.TryRemoveAt` (`:113`) exist; `EquipMove.TryEquip` already displaces into the bag (`Domain/Equipment/EquipMove.cs:64`; `TryUnequip` does the same at `:84`, a different method than the one named here); no sell flow | not built | patch-or-reinforce |
| §5.4 Selling a stack asks a quantity | `1` / `ALL` chips, so a stack is two presses | No sell flow | not built | patch-or-reinforce |
| §5.5 Reroll whole shop, price doubles, saturates | 25 / 50 / 100 / 200 … capped at 9999, `n` stored per node | No reroll | not built | reroll-or-walk |
| §5.6 Leave returns to map and clears the room | Node marked cleared, no confirm prompt | `RunManager.ClearCurrentRoom` exists; `ArriveAt` currently clears non-fight rooms on *arrival* (`Core/Bot/RunOrchestrator.cs:210-213`), which is wrong for a shop | not built | reroll-or-walk |
| §5.6 Leave confirms once | `LEAVE` → `LEAVE?`; Escape never leaves | No controller. Reverses the first draft's "nothing is confirmed on the way out" | not built | reroll-or-walk |
| §5.6 Same stock after a quit and reload | Seeded per node from three coordinates | The property exists for treasure (`Core/RoomResolver.cs:43-45`); `RngStreams` has no shop stream and `Derive` takes only two position inputs (`Domain/Rng/RngStreams.cs:29-32`, `:44`) | not built | machinery |
| §7 Engine — price persisted at roll, plus `stockVersion` | A content or pricing change cannot rewrite an open shop | No stock model | not built | machinery |
| §7 Engine — no minimum-resolution policy needed | Settled project-wide by `ScaleWithScreenSize` + `Expand` at a 1920×1080 reference | `Editor/SceneBuilder/SceneBuilder.cs:213-220`; aspects checked are `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) | match — nothing for this screen to add | machinery |
| §6 Design tokens | Palette and type taken from `FightHudPalette.cs` and the two prior handoffs | Palette constants exist (`Domain/UiKit/FightHudPalette.cs:18-81`); nothing consumes them here | not built | machinery |
| §7 Engine — everything is run gold | Chip reads `RunSnapshot.gold`, never `SaveData.Gold` | No reader | not built | machinery |
| §7 Engine — prices from the plan's formula, not `ItemDefinition.cost` | `Domain/Rewards/ShopPricing.cs` | Does not exist. `ItemDefinition.cost` (`Core/Content/ItemDefinition.cs:59-60`) is live v1 residue: 0 on all 33 weapons, median 505 at tier 10 | not built | machinery |
| §7 Engine — fixed card count emitted at build time, declared to the count audit | **Revised 2026-09-03** — four arrays on the shop screen (spell/item/relic cards, sell rows in the `PACK` modal), not five; slot chips move to the dossier as their own binding (`PLAN_SHOP.md` §3a) | No `ScreenDef` for either screen (`ScreenRegistry.cs:24-28` is the mechanism) | not built | machinery |
| §1g (new) — bot: `ChooseSpellAssignment` | A second `IRunPolicy` call, separate from `ChooseShop`'s now-recipient-less `buy-spell(i)` (`PLAN_SHOP.md` §2g) | Neither `ChooseShop` nor `ChooseSpellAssignment` exists on `IRunPolicy` (`Domain/Bot/IRunPolicy.cs:17-48`) yet — `ChooseShop` itself (the primary buy/sell/reroll/leave loop, §2g) has no row of its own above and is worth tracking separately from this assignment-only call | not built | machinery |
| §3d (new) — weapon tooltip shows the damage line, not a flat stat | `ItemDescription`'s existing dossier damage-line builder (`:145-168`), never a stat delta (`PLAN_SHOP.md` §3d) | No screen tree. Flagged because the prototype's own demo data (`iron_crook: '+6 ATK / +2 REACH'`) models the wrong thing and would be easy to copy verbatim | not built | patch-or-reinforce |
| §7 Engine — copy through `UiStrings` | Price labels as templates with numeric arguments | No strings added | not built | machinery |
| §8 Out-of-scope — Events, hub store, drop-tier rework | Deliberately excluded | Correctly excluded; `RoomType.Event` remains a placeholder (`RoomResolution.cs:102-103`) | match | machinery |
| §7.1 point 3 (new) — counts re-decided | `Gear 3 / Relics 2 / Books 1`, not the 3/4/3 above; `ShopStock.SpellCount`/`ItemCount`/`RelicCount` re-set from gate-1's measured affordability before the screen is laid out | Not started — the constants above are unbuilt entirely, so there is nothing to renumber yet; tracked here so the gate-1 exit re-decision has a row to update rather than silently changing the 3/4/3 rows above | not built | machinery |
| §7.1 point 7 (new) — per-section reroll, three RNG streams | `RngStreams.ShopGear = 5`, `ShopBooks = 6`, `ShopRelics = 7`, each opened on `(run.step, run.currentNodeId, rerollsForThatSection)`; `RunSnapshot.shopRerollsUsed` becomes `int[]` (`PLAN_SHOP.md` §2b/§2d/§2e/§7.1 point 7) | Not started. F9's third `Derive` coordinate does not exist yet, so neither do the three streams built on it | not built | reroll-or-walk |
| §7.1 point 10 (new) — `ShopResult` / `SaveSystem.Save` returns `bool` | Every shop mutation returns `ShopResult` (`Ok`, `Refused` with a reason, `AppliedNotPersisted`); `SaveSystem.Save` returns `bool` so a swallowed write is visible to the caller | `SaveSystem.Save` returns `void` today and swallows its own exceptions (`Core/SaveSystem.cs:123-175`, cited at the atomicity row above); no `ShopResult` type exists | not built | machinery |
| §7.1 point 1 (new) — `GoldOnArrival` telemetry | `RoomTrace` gains `GoldOnArrival`, `GoldSpent`, `GoldOnLeave`, per-section purchase counts, `RerollsUsed[]`; gate-1 report prints p10/p25/median arrival gold per profile, affordability share, and gold forgone | Not started. The bot's current `RoomTrace` records fights only, not shop visits (`PLAN_SHOP.md` §7.1 point 1) | not built | machinery |
| §7.1 point 1 (new) — matched-seed leg comparison | Shop-taking policy vs fight-only policy on the same seeds: survival to next boss, power gained, depth variance, replacing the old "shop doesn't cost depth" gate | Not started — no shop exists to compare against a fight-only baseline yet | not built | reroll-or-walk |
| §7.3 (new) — gate-1 exit numbers recorded in writing | Counts and prices re-decided here, in this file, before gate 2 starts (`PLAN_SHOP.md` §7.3) | Not started; this is what the "Gate 1" heading above is for | not built | machinery |
| §7.1 point 6 (new) — single global selection + `BUY` | One selected card at a time, screen-wide; `BUY` in the Shop Actions panel commits, alongside a second press on the card itself | Not started. Replaces the per-section `Armed` model the rows above assume | not built | machinery |
| §7.1 point 8 (new) — keyboard navigation graph | Up/down/left/right neighbours declared per card and action button, over the six primary cards plus three `REROLL`s, `PACK`, `BUY`, `LEAVE`; `Enter` commits, `Escape` clears selection | Not started. `README.md` §3d's simpler focus-order list predates this and needs the graph in its place | not built | machinery |
| §7.1 point 8 (new) — `UiMotion.DurationScale` | One shared Domain constant (default 1) that every animation on the screen reads, so reduced motion is one constant to change later | Does not exist. `README.md` §6's "no reduced-motion setting" note is superseded by this | not built | machinery |
| §7.1 point 4 (new) — book card purchase-time facts | `KNOWN BY {NAME}`/`KNOWN BY {n}`, `ELIGIBLE {k}/{m}`, `ALL SLOTS FULL`, `1 UNASSIGNED COPY` badges on the shop's book card and its tooltip, from run state the shop already has | Not started. Reverses the "no ownership badge" rule the §2d/§4/§5 rows above (and README's own superseded rows) still describe | not built | patch-or-reinforce |
| §7.1 point 4 (new) — map pending-book indicator | Map screen shows an indicator while `run.unassignedSpellBooks` is non-empty; dossier panel header carries the count. No forced assignment on leaving the shop | Not started. No map indicator of any kind exists for this state | not built | machinery |
| §7.1 point 5 (new) — return-to-pool replacement | `LearnSpell`'s replace branch appends the displaced slot's `skillId` back to `run.unassignedSpellBooks` before overwriting — no destroy path, no `REPLACING`-as-warning language | Not started. `PLAN_SHOP.md` §2f's replace row (pre-review) destroys the displaced book; corrected in that file per the marker at that row | not built | patch-or-reinforce |
| §7.1 point 9 (new) — human checklist, gate 2 | Five to eight fresh-player sessions; time to first purchase, can the tester say why the offer they bought was good, did anyone try to sell a worn piece, did anyone misread `NEED n` as the price — recorded in this file's Gate 2 section | Not run — no screen exists to test yet | not built | machinery |
| §7.1 point 9 (new) — human checklist, gate 3 | Five to eight fresh-player sessions; did the tester find the unassigned book, buy a duplicate by accident, or regret a replacement — recorded in this file's Gate 3 section | Not run — no dossier assignment panel exists to test yet | not built | machinery |
