# I2a remaining work (implementer)

The core seam is committed (d7403c35). This file is
the rest of the I2a row of `docs/PLAN_ITEMS.md` §4. Brief it to one
`implementer`; no step needs a design call. Delete this file in the last
I2a commit.

## The contract already in code

| Piece | Where | Rule |
|---|---|---|
| Base curve | `Domain/Content/GearCurve.cs` | `curve.At(tier)` → `GearStats`: stats, scores, requirements, grades, name, icon. Extrapolates past `maxTier` (the art ladder); grades stop at their authored top; icon stays on the top frame. |
| Per-tier assets | `ItemSetEntryResolver`, `WeaponEntryResolver` | Build every `_pN` asset from `curve.At(tier)`; `ContentBuilder` writes `baseId` + `curve` on every equippable. |
| Stat accessor | `ContentDatabase.Effective(copy, honing)` (`Core/Content/ContentDatabase.Gear.cs`) | The one reader of gear numbers. Per-tier id → that asset's tier (bridge); base id → `copy.Tier`. `GetBase(baseId)`. |
| Copy | `ItemInstance.Of(baseId, tier, mods, provenance)` | `Tier` 0..`TierCap` (12), clamped. `SameStack` keys on tier too. `Plus`, `RiftTier`, old ctor are `[Obsolete]`: every CS0618 warning is a caller to move. |
| Entries | `InventoryEntry`, `EquipmentSlotEntry`, `ShopStockEntry` | Carry `tier`, round-trip through `.Instance` / `GearInstance()`. |
| Quality | `Domain/Content/ItemQuality.cs`, `ContentDatabase.QualityOf` | Derived: trait → Legendary, 2+ affixes → Rare, 1 → Uncommon. Trait count is 0 until I2c's `trait` flag. Colours keep Rarity's hexes. |
| Honing | `Domain/Equipment/SlotHoning.cs`, `RunSnapshot.slotHoning` | Per (member, slot), +1 per hone, cap `ItemUpgrade.MaxPlus`. `offerRerollsUsed` is gone. |
| Prices | `ShopPricing.ScalePrice(amount, step)` | `= DifficultyCurve.ScaleReward`. No price calls it yet. |
| Gold | `VictoryRewards.GoldIncomePermille = 1000`, `Income()` | Applied to fight gold (before `ScaleReward`) and Treasure. |
| Save | no version bump (plan 2.5) | `CopyModelSaveCompatTests` loads a pre-copy-model save. |

Decisions taken where the plan was silent: `EquipmentSlotEntry` also gets
`tier` (a worn copy must keep its tier); requirements and ability scores
extrapolate past `maxTier` with the stats; hand-authored equippables from
`items.json` get a flat curve (`GearCurve.Flat`).

## Order

Each step ends green on its own gate, so it can be its own commit.

### 1. Move readers onto `Effective` (behaviour unchanged)

Per-tier ids still exist, so `Effective(entry.Instance, honing)` returns
today's numbers. For now pass the copy's own `plus` as `honing`; step 3
swaps it for `SlotHoning.PlusOf`.

| File | What reads baked fields today |
|---|---|
| `Core/Content/ContentDatabase.Effective.cs` | `ActiveLoadout`, `EffectiveStats`, `EquippedWeapon(Power)`, `EffectiveWeaponScaling/SkillScaling`, requirement checks: `StatBonusAt`, `WeaponPowerAt`, `abilityScoreBonus`, `requirements`, `scaling`, `spellScaling`, `.tier` (modifier magnitude) |
| `Core/ItemDescription.cs` | tooltip stats, name, tier |
| `Core/CharacterDossierController.Loadout.cs` | stat rows, rarity colour, tier |
| `Core/ShopController.cs`, `Core/Bot/RunOrchestrator.Shop.cs` | price from `.tier`, rarity |
| `Core/ItemOfferRoll.cs`, `Core/DebugMenuController.cs`, `Core/RarityColors.cs`, `Core/GlossaryEntries.cs` | `.tier` |
| `Core/Content/ContentDatabase.Validation.cs` | baked-field validation → validate the curve instead |
| `Domain/Content/ModifierMagnitude.cs` | item tier input → the copy's tier |
| `Domain/Audio/MusicLayerResolver.cs` | check: `.tier` may not be an item tier at all |
| `Core/Bot/GearEvaluator.cs` | reads plus/tier for scoring |

Verify: `tools/test.ps1 content`, `hub`, `ui`; `GearStatSeamTests` stays green.

### 2. Stat fields leave `ItemDefinition`

Delete `statBonus`, `attackBonus` (gear use), `abilityScoreBonus`,
`requirements`, `scaling`, `spellScaling`, `StatBonusAt`, `TotalStatBonus`,
`AbilityScoreBonusAt`, `WeaponPowerAt`, `Rarity`; keep `amount`/`effect`
for consumables. Every compile error is a reader step 1 missed. Delete the
matching `ContentBuilder` writes and `ResolvedSetPiece`/`ResolvedWeapon`
stat fields (AUDIT #262). `GearStatSeamTests.EveryGeneratedPiece_...` then
compares `Effective` against literals instead of baked fields: pin three
bases at tiers 0/5/10/12.

Verify: compiles; `test.ps1 content`; `-Changed -BuildContent`.

### 3. Copies carry base + tier; content collapses to bases

- Resolvers emit ONE asset per base, id = `BaseId` (no `_pN`), `tier` field
  gone from the definition. `ContentBuilder` collision checks stay.
  `weapons.json`/`itemsets.json`: `maxTier` = art frames; `tierAdjectives`
  gain entries for tiers 11-12 (readme text and `docs/CONTENT_SCHEMA.md`
  via `tools/content_schema.ps1`).
- Every writer of a copy uses `ItemInstance.Of(baseId, tier, ...)`:
  `Core/EquipmentOps.cs`, `Domain/Bot/FightRunner.cs`,
  `Domain/Combat/Session/FightHudModel.cs`, `Domain/Equipment/BagView.cs`,
  `Domain/Equipment/EquipMove.cs`, `Domain/Inventory/InventoryOps.cs`,
  `Domain/Rewards/ItemOffer.cs`, `Domain/Rewards/ShopStock.cs`,
  `Core/ItemOfferRoll.cs` (offers roll a tier, then the base).
- Honing: readers take `SlotHoning.PlusOf(run.slotHoning, member, slot)`;
  `EquipmentLoadout.GetPlus`/`Set(plus:)` go.
- Remove from `ContentDatabase.Effective` the per-tier-id branch and from
  `GetBase` the "first (tier 0) asset" wording.
- `CopyModelSaveCompatTests`: flip the last assertion — `sword_p5` no
  longer resolves, Weapon1 is empty — and add
  `Assert.IsNull(ContentDatabase.GetItem("brigandine_boots_p3"))` as the
  fixture guard.

Verify: `test.ps1 content`, `run`, `hub`, `ui`; `-Changed -BuildContent -BuildScenes`; diff `Art/` (gotcha 3).

### 4. Removals (plan §4 I2a row, §1.5, §1.7)

| Remove | Where |
|---|---|
| `Plus`, `RiftTier`, old ctor on `ItemInstance`; `plus`, `riftTier` on all three entries; `SameStack` stops keying on them | `Domain/Inventory/ItemInstance.cs`, `InventoryEntry.cs`, `EquipmentLoadout.cs`, `ShopStock.cs` |
| Plus roll, Favor plus odds, `BossBonusPlusRungs` | `Domain/Rewards/RarityTable.cs:211-242`, `Core/ItemOfferRoll.cs` |
| Rift tier | `Domain/Content/RiftTier.cs`, `RiftTierBands.cs`, `ModifierTable.RollRiftTier`, `Core/RiftTierColors`-style readers, `ShopPricing.RiftFactor/PlusFactor` |
| ±1 tier spread | `Domain/Rewards/ItemOffer.cs:79` `TierSpread` |
| Absolute tier floors | `RarityBands.FloorTierOf` and its callers; shop `ApplyQualityFloor` |
| Content `maxTier` as the roll cap | `RarityTable.cs:100,142`, `ShopStock.cs:352` → `ItemInstance.TierCap` |
| `Rarity` (Mythic, Epic, bands) | `Domain/Content/Rarity.cs`; readers: Reckoning, shop, tooltip, bag (`BagSort`), dossier, `FloorTierOf`, bot policies → `Quality` + tier badge |
| Remaining CS0618 | anything the compiler still lists, tests included (38 test files use plus/rift/`RarityBands`) |

Relic rarity (`RelicRarity`) is a separate ladder and stays.

### 5. Prices onto `ScalePrice` (plan 1.6, 2.3)

Every price authored in step-0 gold, charged through
`ShopPricing.ScalePrice(amount, step)`: gear
`ScalePrice(BaseFor(tier) × (1 + 0.5 × q), step)` with q = affixes, +2 for a
trait; books, relics, honing (`21 × (1 + 0.35 × plus)`), rerolls, sale,
the unused-relic payout. Rewrite the "never of depth" header in
`ShopPricing.cs`. Callers: `Domain/Rewards/ShopStock.cs`,
`Core/Bot/RunOrchestrator.Shop.cs`, `Core/ShopController.cs`.
Shop stock literals in `ShopStockTests`/`ShopPricingTests` are re-pinned
as NEW literals (hand-computed, gotcha 5), not edited to match output.

### 6. Docs

- `docs/CONTENT_SCHEMA.md` (generated), `docs/CODE_MAP.md` rows for
  `GearCurve`, `ContentDatabase.Gear`, `SlotHoning`, `ItemQuality`.
- `GlossaryEntries.cs:233` honing entry rewritten for slots — I4, not here.

## I2a gate (all of it, before the last commit)

- `tools/test.ps1 content`, `run`, `hub`, `ui`
- `CopyModelSaveCompatTests` (old-id load) green with the flipped assertion
- `tools/run_tests_parallel.ps1 -Changed -ChangedPaths <commit paths> -BuildContent -BuildScenes`
- `git status Assets/_Project/Art` after the scene build: commit nothing there that the change did not mean
- shop stock pinned as new literals
