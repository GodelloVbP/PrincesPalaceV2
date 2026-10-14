# I2b remaining: screen, bot, tests, docs

Owner: a Sonnet `implementer`. The core seam landed in `24d4a57b`:
`rewards.json`, `GearPage` (steps 1-8), `RelicPage`,
retuned `RarityTable.RollTier`, `PendingPage`, `RewardPages`
(Settle / Take / HoneInstead / Leave). Plan: `docs/PLAN_ITEMS.md` rev 4,
1.2, 1.4, 2.2, I2b row of 4. Paths under `Assets/_Project/Scripts/`.

## The contract you build on

- `RewardPages.Settle(save, step, nodeId, strength, gearChips, relicChips, fieldedIds)`
  rolls the pages once on `RngStreams.Reward` keyed `(step, nodeId, page)`
  and replaces `run.pendingPages` (`[NonSerialized]`, memory only). Gear page
  first, relic second. Updates `run.legendaryPity` / `run.lastPageBestCommon`.
  It does not save; the caller saves after each player action.
- `RewardPages.Current(run)`: the first page with `ChoicesLeft > 0`.
- `Take(save, page, i)`: gear equips on `card.RecipientId` in `card.Slot`
  (`EquipmentOps.Equip`, `MenuContext.Descent`); the displaced copy is in the
  bag and on `card.Displaced`. Relic joins `run.relicIds`.
- `HoneInsteadFor(save, page, i)` -> `Refused | Hone | Gold`. Show "Hone
  instead" only for `Hone`, or `Gold` (label it with
  `RewardPages.SalePrice(card.Copy, page.Step)`). `HoneInstead` applies it.
- `Leave(save, page)`: pays unused choices (gear: `page.UnusedHones`, relic:
  gold), sets `Closed`. Returns the gold.
- Card face data: `card.Copy` (name `ContentDatabase.NameOf`, quality colour
  `ContentDatabase.QualityOf(copy)` -> `ItemQuality.HexColor`, tier badge
  `copy.Tier`), `card.Inert` ("inert until ..."), `card.RecipientId`,
  `card.Slot`. `card.Rolled` is the ladder's quality; until I2c a Legendary
  roll is a Rare copy with three affixes, and the face shows the copy's
  quality. Relic card: `RelicId`, `Rarity`, `AddsNothingFor` (member ids).

## Items

1. **Fight end opens pages** -- `Core/FightController.Input.cs:1246-1263`
   (`RollOffers()` / `ReckoningRequest.Full`). Until I3, every won fight with
   a reward settles one gear page: `gearChips = [session.EncounterClass]`,
   `relicChips = [Elite]` for an anchor and `[Boss]` for a boss, else empty;
   strength `RealmOpening.StrengthOf(run, nodeId)`; fielded ids from the
   session party (`RunOrchestrator.TallyOf(session)` ids). Remove the
   per-fight `RollOffers` path, `RunOrchestrator.RollOffers`/`TakeOffer`/
   `AutoEquipIntoAnEmptySlot` (`Core/Bot/RunOrchestrator.cs:782-870`, also
   its stale `TierFloorFor` comment at :805), and `ItemOfferRoll.Roll` once
   nothing calls it. `ReckoningRequest.Offers` becomes the page list.
   FightController and RunOrchestrator are run-map P5 files: commit only your
   hunks. Verify: `test.ps1 run`, `ui`.
2. **Reckoning screen** -- `Domain/UiKit/Screens/ReckoningScreen.cs`,
   `Core/ReckoningController.cs`, `Editor/SceneBuilder/ScreenRegistry.cs`.
   One page at a time, gear then relic. Header: page class, "k choices
   left", and the Favor line: squad Favor (`ItemOfferRoll.CurrentSquadFavor`)
   and its effect, tier `+0.006/pt` toward 0.45 and quality `+0.006/pt`
   toward 0.55 (`RarityTable.TierFavorPerPoint`/`TierMaxStep`,
   `GearPage.QualityFavorPerPoint`/`QualityMaxStep`; strings in
   `UiStrings`). Gear card: name, tier badge, recipient, frame colour =
   quality, the one largest stat delta against what the recipient wears in
   `card.Slot` (`ItemDescription.SimulateEquip` + the comparison the squad
   tooltip uses), one line (strongest affix; the trait from I2c), and when the
   slot is occupied a "loses: <affix>" line for an affix the worn copy has and
   the card lacks. Buttons: Take, Hone instead (per `HoneInsteadFor`). The
   page lists displaced copies. Relic cards through `OfferRowLayout` (width
   from `k + 2`), naming `AddsNothingFor`. Leave pays unused choices and
   shows what they paid. Never hand-edit a scene. Verify: `test.ps1 ui`;
   `-BuildScenes`.
3. **Bot** -- `Core/Bot/GearEvaluator.cs:146` (`ScoreOffer`) and
   `Core/Bot/BotRunDriver.cs:1877-1950`. Score a card for its recipient only:
   `ItemDescription.SimulateEquip(recipient, card.Copy, card.Slot)` with the
   slot's honing, so tier, affixes and quality all count; score "hone
   instead" as the recipient's delta from +1 on that slot. Pick the best of
   Take / Hone per choice, then `Leave`. Trace the page (source id, chosen
   card or hone) in place of the offer trace. Verify: `test.ps1 run`
   (BotGearEvaluatorTests and the bot fixtures).
4. **PlayMode multi-page test (the I2b gate)** --
   `Tests/PlayMode/Run/RewardPagesFlowTests.cs` (new): a boss and two anchors
   settle (`gearChips = [Boss, Elite, Elite]`, `relicChips` the same), take on
   page 1, re-enter the Reckoning (pages survive in memory), leave with
   unused choices; assert hones (`SlotHoning.PlusOf`), gold, pages. Drive the
   screen, not `RewardPages` directly. Verify: `test.ps1 RewardPagesFlowTests`.
5. **Test rewrites** -- re-pin what the retuned tier roll moved:
   `Tests/PlayMode/Content/ItemOfferRollTests.cs`
   (`RollOffers_SameKeyGivesSameOffers_...` literals) until item 1 deletes
   the path; `ReckoningTests`, `ReckoningComparisonTests`,
   `ReckoningPhaseTests`, `ReckoningTabTests`, `ReckoningCaptureTests`,
   `ReckoningGamepadNavigationTests`, `ReckoningLifecycleTests`,
   `FightAfterTheEliteTests` (weapon guarantee), `ItemOfferFavorTests`
   (`LootLadder.StepChanceFor(EncounterClass, ...)` and the class constants
   `NormalStep/EliteStep/BossStep` have no production caller once item 1
   lands: delete them with this test's use). Pin literals (gotcha 5).
   Verify: `test.ps1 run`, `ui`, `content`.
6. **Doc rows** -- `docs/CODE_MAP.md`: Reckoning pages ->
   `Core/RewardPages.cs`, `Domain/Rewards/GearPage.cs`, `RelicPage.cs`,
   `PendingPage.cs`, `ContentData/rewards.json`. `docs/ARCHITECTURE.md` if it
   lists the offer flow. `AUDIT.md`: close what this resolves.
7. **Final gate** -- `tools/run_tests_parallel.ps1 -Changed -ChangedPaths
   <your paths> -BuildContent -BuildScenes`; diff `Art/` after the scene
   build (gotcha 3).

## Decisions already taken in the core (do not re-litigate; flag to the owner if wrong)

- Tier is rolled before base per card, so the requirement check (step 4) runs
  at the card's own tier.
- `weaponFloor` is a `rewards.json` field (boss Rare), not a code constant.
- Weapon tie (both hands same tier) replaces Weapon2.
- Pity counts the rolled quality: a Legendary roll resets it though the copy
  reads Rare until I2c.
- Unused gear choice: the first shown card whose slot can still be honed;
  none left -> the first card's sale price.
- A relic whose reached members all already have its mechanic is dropped;
  a relic reaching no fielded member stays.
- Shop and shelf stay on their I2a path; `recipients` and `affordabilityCard`
  were left out of `rewards.json` until the shop moves onto the pipeline.
- An emptied relic pool settles no relic page (the chip-level "pays gold"
  case belongs to I3).
