Context review of PLAN_ITEMS.md rev 1. Reviewer: Sonnet 5.5 (`implementer`, read-only), 2026-10-13.

Paths under `Assets/_Project/Scripts/` unless noted. Checked against local `main`.

## Verdict

**Ready after fixes.** The two save claims the orchestrator doubted are correct (finding 12), and the shape of the plan fits the research and the run map. But the reward quality contract makes the central problem (D1, bare picks) worse than today, the boss and anchor settlement has an RNG-key collision plus an undefined multi-pick flow, and the claimed 28/30 is about 5 to 6 points high. None of this needs a redesign, only stated numbers and keys before I2.

## Findings

1. **[blocker] Dropping the plus roll with the affix ladder unchanged makes gear picks barer than today (D1/D4 not fixed).**
   - Plan: 1.2 "Common 0 affixes ... rung chances by encounter class"; 1.5 "The plus roll goes ... `BossBonusPlusRungs`".
   - Evidence: affix ladder `ModifierTable.cs:~95-97` NormalStep 0.12 / EliteStep 0.15 / BossStep 0.18. Plus ladder `LootLadder.cs:41-43` is 0.22/0.30/0.38 and is the other half of "not bare". Audit D1 counts a card bare only if plus 0 AND no affix (69% normal, ~32% of normal fights all three bare).
   - Failure: with plus gone and Common = 0 affixes, a normal card is Common 88% of the time. P(all 3 cards Common) = 0.88^3 = 68% (calc), up from 32%. Elite/anchor: 0.85^3 = 61%. Boss: 0.82 per card, and the old guaranteed +3 on boss gear (`RarityTable.cs:226`) is deleted with nothing replacing it, so boss gear is mostly Common at tier+1. The strength floor (1.2) covers only strength 2-3 chips; strength 1, anchors without a chip strength and bosses get none. Colour is now the whole readable signal (T4), and two thirds of the time it reads "grey, grey, grey".
   - Fix: state the per-class quality distribution as a contract (a P(Common/Uncommon/Rare/Epic) table per class, pinned in a test) instead of leaving it to I6. Add a boss floor (for example the weapon card is at least Rare). Decide whether Common is still allowed on a gear pick at all; the Fewer-better-rewards pattern (research #3, D3 Loot 2.0, D4 Loot Reborn) is "raise the floor", and the plan only raises it on chosen chips.

2. **[blocker] Boss and heart settlements pay several picks under one `(step, nodeId)` key, and the multi-pick Reckoning is not specified.**
   - Plan: 1.1 Boss pays "Gear ... and a Relic pick"; anchors at the heart pay their own chips (run map `PLAN_RUN_MAP.md:286-288`); 1.2 "`RngStreams.Reward` (14) opened on `(step, nodeId)`".
   - Evidence: `RngStreams.cs` takes a third key `c` for exactly this (`ShelfGear` etc. use `KeyOf(event/shelf)` as `c`). `ReckoningRequest.Full(settled.Reward, RollOffers(), receipts)` (`FightController.Input.cs:1246`) carries one offers list, and `ReckoningScreen`/`ReckoningController.cs` paint one row (`_offers`, `LayOutOfferRow`). No relic card exists on that screen.
   - Failure: boss gear pick, boss relic pick and up to 4 heart-anchor chips all open the same stream at the same position, so the cards are identical or correlated. Pity "advances on resolve" while "re-derives the same cards on reopen" is also wrong for pick 2 onward: its pity input depends on pick 1's outcome.
   - Fix: key on `(step, nodeId, pickIndex)` with `pickIndex` in `c`, define pity as read at roll time from a per-run counter snapshot, and specify the Reckoning as an ordered list of picks (gear, relic) with a relic card face. Name the relic-pick UI in I3 (currently I3 is an `implementer` phase with no UI line) or move it to I2's senior scope.

3. **[major] Persisted shop and shelf entries are missing from the save and copy-model change.**
   - Plan: 2.5 lists only `InventoryEntry` and `EquipmentSlotEntry` for `tier`.
   - Evidence: `ShopStockEntry` (`Domain/Rewards/ShopStock.cs:41-58`) has its own `plus`, `riftTier`, `contentId` and builds `new ItemInstance(contentId, plus, modifiers, riftTier, ...)` at `:88`. It is stored in `RunSnapshot.shopStock` (`RunSnapshot.cs:256`) and in `MerchantShelfStock.entries` (`:396`, `:365`).
   - Failure: with tier moved off the id, a shop or shelf entry cannot carry the tier it was priced at; its price ("a pure function of tier/plus/rift", `ShopStock.cs:374-375`) and its bought copy both lose the tier. `ItemInstance.SameStack` (`ItemInstance.cs`, keyed on itemId/plus/modifier set/riftTier) is also not stated to gain `Tier`: today two tiers are different ids and never merge, after the plan a tier-3 and a tier-5 copy of one base would merge into one stack.
   - Fix: add `ShopStockEntry` (and its rift-tier-based reroll/price paths), `MerchantShelfStock` and `SameStack` to 2.3/2.5. The affected-file count is large: `riftTier`/`RiftTier` appears in 38 non-test files, `ItemInstance` in 18, so the I2 gate should name `shop`, `inventory`, `equipment` and `bot` slices, not only `run, content, ui`.

4. **[major] Tier moving onto the instance changes every stat reader, and the plan does not list them.**
   - Plan: 2.3 "`GearScaling` computes stats from base + tier at read time".
   - Evidence: tier-baked stats are read from `ItemDefinition` by `ContentDatabase.Effective.cs`, `ItemDescription.cs`, `ItemComparison`, `EquipmentOps.cs`, `CharacterDossierController.Loadout.cs`, `GearEvaluator.cs:146`, `ShopPricing`, `BagView/BagSort`, `ItemNaming`, and the art path by id. A grep for `.tier`/`.Tier` hits about 30 files.
   - Failure: every reader that takes `ItemDefinition` alone silently reads tier 0. The compile does not catch it, since the fields stay.
   - Fix: make the stat accessor take an `ItemInstance` (one seam: `ContentDatabase.Effective`), remove stat fields from the base definition so a missed reader fails to compile, and list the readers in I2.

5. **[major] D9 (relic draft: 82% common, one decision) and C1 (the `ItemKind` union / `ContentBuilder` hand-copy, AUDIT #262) are dropped silently.**
   - Plan: 1.7 "relic draft shape" unchanged; 2.2 "Relic offers reuse `RelicPool.Available` and its rarity weights".
   - Failure: the boss and each anchor now draw relics with the same 100/45/18/6/2/1 weights (`RelicPool.cs:121-131`) from a 38-relic draftable pool of which 15 are common. Up to 7 relic picks a realm will exhaust commons first and then leave a pool of leftovers; mythic/godlike (weights 2 and 1) essentially never appear, so "relics are the rare, run-defining reward" (T10) leans on a distribution the audit already called broken. C1: the plan adds necklace bases and an amulet set through the same 15-20-field hand-copy in `ContentBuilder` and does not say whether `ItemDefinition` stays a 3-shape union. `rewards.json` also needs `IOrderedContent` and `LoadOrdered` (CLAUDE.md gotcha 4), which 2.4 does not mention.
   - Fix: either list D9 and C1 as out of scope with a reason, or add a relic-weight row per source (`relicRarityMin` exists in 2.4 but has no stated use), and state that the boss and anchors use a shifted table.

6. **[major] Expected theory scores are overrated; the credible total is about 22-23, not 28.**
   - T5 (2): the card face is name, tier, colour, recipient, "one or two largest stat deltas" and one line. That is at least three readable items and two numbers, against the test's "one number, one named behaviour". Credible 1.
   - T8 (2): the test says "seeded and quit-safe". The plan itself says the stream "buys ... not quit safety", and the pity covers only the necklace card, not the streak of Common picks the test is about. Credible 1.
   - T9 (2): "frequent rewards cheap to resolve". A Gear chip still opens a 3-card, per-member, delta-bearing modal with no cap on how often (the Gear weight is an I6 literal). The plan also says relics and necklaces are the rare rewards, but see finding 9. Credible 1.
   - T4 (2): colour ranks quality, but the three cards are different slots on different members, and a necklace card is always Legendary regardless of power. Credible 1.
   - T3 (2): skip pays the 30% sale price of the cheapest, usually Common, card, a few gold; the opportunity cost of taking one of three per-member cards is real but unlabelled. Credible 1-2.
   - T15 (2): anchors pay at the same tier as a normal fight and the elite tier floor (`RarityTable` elite >= 1) is removed, so normal-chip-Gear and anchor-Gear differ only by ladder chance. Credible 1-2.
   - Fix: restate the table at ~22-23 and name the lever for each one-point gap, rather than presenting 28 as the target.

7. **[major] Several rules interact without stated precedence, and several literals are undefined.**
   - Slot policy order: recipient assignment, "empty `Weapon1` wins outright", the boss weapon override, necklace pity weight and the requirements filter are all applied to the same card. The plan never states the order or the fallback when every candidate fails a member's `requirements`; the draw can loop or return nothing. It also does not say whether the three cards must be distinct bases (`ItemOfferTable.Choose` enforces it today).
   - "Lowest summed comparison score (`ItemComparison`)": `ItemComparison` compares an item to squad slots; there is no per-member "summed score" to rank members by. Use `GearEvaluator` or define one.
   - "A necklace slot weight for every card of the next pick is `1 + pity`": the slot weights in the same list are 3 (empty) and 1 (filled). It is unclear whether `1 + pity` replaces 3 and 1 or multiplies them.
   - Strength floor: "a quality floor on the best card" does not say which card is bumped or whether its affixes are rerolled. A Legendary necklace satisfies any floor, so pity can defeat a strength-3 chip's promise.
   - 1.1 "Skip value of 1.4": there is no skip value in 1.4 (the axes table). A relic skip has no defined payout (a relic has a shop price in `ShopPricing.cs:84-89` but no sale price).
   - "Relic pool empty ... The chip shows gold in that case, since the pool is known at realm open": the pool shrinks mid-realm from shop buys, events and earlier relic picks, so a chip frozen at open can lie.
   - Skip "only pays out, so it does not confirm (#225)": a skip also discards the cards, which the #225 proposal ("anything that spends or loses confirms") puts on the confirm side.
   - Fix: add a one-paragraph ordered algorithm for a gear pick, and replace each undefined name with a literal or a defined function.

8. **[major] Honing at Rest is a verb that exists in at most one place per realm, and the Rest choice has no screen or state.**
   - Plan: 1.5 and I5; run map `PLAN_RUN_MAP.md:114-115`: Rest is "at most one per realm", the spine node at offset 7 only when `restBeforeBoss`, which nothing grants today (`RunSnapshot.cs` comment), otherwise a fork sibling.
   - Evidence: `RoomResolution.cs:87-88` resolves Rest instantly as `healsPartyToFull`; `RoomResolver.cs:106` applies it. There is no Rest screen, no pending-choice field on `RunSnapshot`, and no gamepad focus order for one.
   - Failure: the research warns (#6) that rest-vs-smith "needs a cost that scales in an endless run". Here the trade exists once per realm at best, and often zero times; plus is then bought at the shop (0.75 chance per realm, in a pocket). D5 (plus has no agency or sink) is only thinly fixed. I5 is an `implementer` phase gated by `-Changed -BuildScenes` only, but it needs a new screen tree in `Domain/UiKit/Screens/`, `ScreenRegistry.cs` wiring and a saved choice state.
   - Fix: say where honing is reachable per realm (count), give I5 the UI and state scope, and state the hone price curve before I6.

9. **[major] Necklaces are not the rare reward the plan scores them as.**
   - Plan: 1.2 slot weights (empty slot 3, filled 1) plus pity; T9 row "relics and necklaces are the rare rewards".
   - Failure: at the first picks all 8 slots per member are empty (weapon wins outright for the first card of each member only while `Weapon1` is empty). After that, necklace is one empty slot of ~7 at weight 3, about 14% per card, about 36% of picks contain a necklace, and pity pushes it higher. A necklace is a Legendary-labelled card with a base stat budget, so Legendary no longer signals rarity or power, and a trait on 36% of picks is common, not rare.
   - Fix: give the necklace its own draw outside the slot lottery (an explicit per-pick chance, or a source such as anchors and boss only), and rename the quality label, since Legendary collides with the relic `RelicRarity` ladder the plan chose to keep separate (section 6).

10. **[major] Phase and gate holes.**
    - I2 needs necklace bases (2.3 "necklace bases (new)", slot policy, pity, take-equips) but the amulet set and trait pool are built in I4. I2's pity and necklace paths have no content to test against.
    - I3 flips the run map's `Item`/`Book` prize weights and adds `Relic`, plus a second anchor weight list on `RealmProfile`. Those are P1/P2 deliverables (the saved profile, `{kind, weight}` lists, the P2 fuzz test pinning counts), and I3 lands after P2. The plan also renames the run map's `Item` to `Gear`. Put the `Relic` value and the anchor weight list into P1 now, or I3 reopens P1/P2 tests and the v9 profile shape.
    - 2.5 says this plan "takes no number", but a saved `RealmProfile` gaining a list is a persisted-shape change after v9; say what an already-saved profile without the list means (empty weights = anchors pay nothing).
    - I1 gate: "bot batch ... matching a pre-I1 baseline within noise". I1 is declared behaviour-neutral and the bot is seeded, so the gate should be byte-identical `RunTrace` for a fixed `-Seed`/`-Runs`. "Within noise" lets a silent value change through, the very risk named in section 5 ("can change a value silently").
    - I1 also says "No behaviour change" while generating rules text from the catalog; that changes player-visible relic descriptions.
    - 5 "Shop drift ... pins a seed's stock before and after": the plan itself removes the plus roll and the rift tier (`ShopStock` redraws "plus/rift", `ShopStock.cs:374`) and changes item ids, so stock cannot be equal before and after. Redefine the pin as "same section streams, same count, floors hold" with new literals.
    - The shop floors need redefining: `floorNoBare` today means "+1 or one affix" (`ShopStock.cs:~312-316`) and `floorCheapestAtMost` 28g is priced from tier/plus/rift; both depend on a price function the plan does not restate.

11. **[major] The "shared structure" is a real model for stat kinds and reward sources, not for relic mechanics.**
    - Plan: 2.1 says each kind's behaviour "stays one block of C# at one hook"; `EffectCatalog` names the hook; "a test asserts every non-stat kind is read somewhere".
    - Evidence, counts: a new relic mechanic today is `RelicEffect.cs`, a hook block in `FightSession.Relics.cs` (or `.RelicMechanics.cs`), `FightTuning.cs`, plus JSON (audit C2: 3 code files). After: `EffectKind`, `EffectCatalog` row, the same hook block, plus JSON, with the headline number now in JSON: still 3 code files, so no saving for mechanics. A new affix/trait kind drops from 5 files (`ModifierEffect.cs`, `ModifierEffectText.cs`, `ModifierTable.cs`, `FightTuning.cs`, `FightSession.cs`) to about 3, because pool and text move to JSON and the catalog. A new reward source on existing kinds becomes one `rewards.json` row (was code in 5 hand-rolled loops). A new offer kind still touches `Offer.Kind`, candidates, Grant and the card UI (about 4-5 files).
    - Weak points: (a) `Effect {Kind, Magnitude, Threshold}` carries two numbers; relic mechanics have more (Charging Crystal: period 4 and 50%; Phoenix Egg: duration 3, `FightTuning.cs:272`, plus revive amount), so only "headline numbers" move and each relic has two homes for its numbers. (b) the catalog's `hook` is metadata, not dispatch, so a kind can be declared and never read; a reflection test cannot prove "read somewhere", so the claimed test needs a method (source grep or a runtime coverage hook). (c) shop and shelf keep their bespoke affordability redraws, floors and section rerolls, so "one pipeline" is really shared candidates, `RollOne` and Grant, which is still worth doing but is smaller than 2.2 reads.
    - Fix: say that relic mechanics do not get cheaper, claim the real saving (affix/trait kinds, text, reward sources), and name the dispatch test.

12. **[minor, claims verified] "A run does not survive the process" and "no save version bump" are both correct, with caveats the plan should state.**
    - Verified. `RunManager.cs:65-80` is the comment block "A RUN DOES NOT SURVIVE THE PROCESS"; enforcement is `RunManager.SettleOnOpening` (`:109-111`, `if (HasRun) EndRun();`) called from `SaveSlotManager.EnterSlot` (`SaveSlotManager.cs:128-146`). The main menu Continue calls `EnterSlot(_continueSlot)` then goes to the hub (`MainMenuController.cs:103-104`), so it settles the run, never resumes it. `EndRun` clears every roster character's equipment and `stockpiledItems` (`RunManager.cs:279,289`), but only for a run with `DescentIsUnderWay`. `RunSnapshot.cs:114-120` records that the run-scoped prunes in `SaveData.Reconcile` (`:753`, `:796`) are unreachable "for a future resume", and `RunManagerTests.AMidDescentRunOnDiskIsGoneOnceItsSlotIsEntered` (`RunManagerTests.cs:493`) fails the day that changes. `RunOrchestrator` calls `SaveCurrent()` after every pick and shop action, so the disk does hold a mid-run copy of gear and bag; it is just never read back as a run.
    - Caveats: (a) the no-bump decision is therefore a bet on the no-resume rule. State it as that, and say that any resume feature must add a bump and a tier migration at that time. The plan's own "Saved per run" pity and "re-derives on reopen" wording reads like resume reasoning and should be cut to "in-process re-entry". (b) Gear on a profile with no active run is not cleared (`EndRun` discards an un-walked run without clearing); today only `ProfilePresets`/bot/debug paths create that. A surviving copy of an old id that Reconcile keeps would load as tier 0 (new field default), so reusing an existing id for a new base silently downgrades it; give bases ids that cannot collide with the 638 old ones. (c) The v7 note at `SaveData.cs` shows a bump is also used to make an older build refuse a newer save; an I2 save opened by a pre-I2 build drops `tier` and prunes new ids. That is dev-only and acceptable, but the plan should say "no number, on purpose".
    - Fix: add the caveats to 2.5. No change to the "no bump" decision.

13. **[minor] Research use: one precedent is [M], one caution is dropped.**
    - The boss relic "pick 1 of 3" borrows from Slay the Spire, which `RESEARCH_GAMES.md` marks [M] ("boss gives pick 1 of 3 [M]"). The decision stands on the plan's own reasoning (PLAN_SHOP F8 describes elite and boss relic drops), not on that precedent; do not cite it.
    - Pattern #2 (smart loot) says "keep an off-class floor so new builds stay possible"; the plan's "never yields an item whose requirements the member fails" has no floor, and every card is made for its recipient, so the pick can never be a surprise or an off-build item.
    - Pattern #4 warns that skip must pay less than a good pick but more than junk, and that skip-bias is easy to exploit; the plan's skip is 30% of the cheapest card's price with no check that it is not dominated by taking, or that always-skip is not best for gold.
    - Pattern #6 warns the rest choice needs a cost that scales in an endless run; see finding 8.
    - The numeric specs (StS pity shape, Balatro reroll) rest only on [V] items; the plan sets its own literals to I6, which is the right place.

14. **[minor] Small spec slips.**
    - 1.1: "Skip value of 1.4" (see finding 7).
    - 1.5 says the glossary "reads both constants and stays correct"; it does, but its text still says "a weapon's damage" and 1.5 gives no new copy for max +5.
    - Plan citations drift by a few lines (`RunManager.cs:279-289` clears are at `:279` and `:289`; `RunManager.cs:65-80` comment runs `:65-107`); harmless.
    - `HasRelic` call count: 37 call sites in 11 files by grep (plan: about 35 in 10 partials), consistent in size.
    - 1.2 Take: "what it replaces goes to the bag" is `EquipMove` behaviour; say whether a replaced item that the recipient cannot use (inert) still goes to the bag and whether Take is blocked for a member who died or left the squad between roll and take.

## Check coverage of the audit

| Audit item | Plan answer |
|---|---|
| D1 | Partly: chip, skip, per-member; but bare share rises (finding 1) |
| D2 | Necklace trait and boss/anchor relics; non-necklace gear stays stat sticks (stated) |
| D3 | Per-member cards (no off-class floor, finding 13) |
| D4 | Colour = quality, tier fixed; fine once finding 1 is fixed |
| D5 | Honing, but at most one Rest and one shop a realm (finding 8) |
| D6 | Base + tier on copy |
| D7 | Chips, boss weapon card, hidden book roll removed |
| D8 | Recipient on card, take equips |
| D9 | **Dropped silently** (finding 5) |
| D10 | `Fortunate` removed, Favor kept (`ItemOfferRoll.cs:121` reads it: needs a line) |
| C1 | **Dropped silently** (finding 5) |
| C2 | One `EffectKind` (finding 11 on the saving) |
| C3 | `SamplingOps` |
| C4 | `RngStreams.Reward` (finding 2 on the key) |
| C5 | I0 |
| C6 | Out of scope, stated |
| C7 | Section 5 risks; bot blindness in I2 |
