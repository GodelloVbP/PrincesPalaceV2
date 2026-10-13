# Item and item-reward audit (read-only, 2026-10-13)

Paths are relative to `Assets/_Project/` unless they start with `docs/` or `AUDIT.md`. `S/` = `Scripts/`.
Numbers marked (calc) are my arithmetic from the cited constants, not measured. I ran no tests and no bot batch.

---

## (a) System map

**Data model: one runtime item class with three kinds, plus neighbours that are not items.**

| Thing | Where | What it is |
|---|---|---|
| `ItemDefinition` | `S/Core/Content/ItemDefinition.cs:13` | One ScriptableObject that is a union of 3 shapes: `ItemKind {Consumable, Weapon, Equipment}` (`S/Core/Content/ItemKind.cs`). Fields mix consumable (`effect`, `amount`), weapon (`attackBonus`, `spellScaling`) and armour (`statBonus`, `abilityScoreBonus`, `requirements`) data. AUDIT #262 (`AUDIT.md:2778`) records this union as the reason items bypass the generic content builder. |
| Armour "item sets" | `ContentData/itemsets.json`, `S/Domain/Content/GearScaling.cs`, `ItemSetEntryResolver.cs` | A GENERATOR, not an item (`itemsets.json:2` readme). 11 materials x 5 slots x 11 tiers = 605 items. Stats derive from `styleWeights` + `statProfile` through a budget `10 x SlotWeight x 1.25^tier` (`GearScaling.cs:47-91`). **There is no set bonus**: `setId` is kept "so a future set bonus can group pieces" (`ResolvedSetPiece.cs:26-29`, `ItemDefinition.cs:69`). |
| Weapons | `ContentData/weapons.json` | Generator: 3 families (sword/staff/dagger) x 11 tiers = 33 items. Attack 10 to 202 (sword). One scaling stat each. |
| One-off items | `ContentData/items.json` | 2 rows: `health_potion`, `mana_potion`. The starting kit was deleted (readme, AUDIT #114). |
| Instance state | `S/Domain/Inventory/ItemInstance.cs` | A copy = itemId + `Plus` + `ModifierIds` + `RiftTier` + `Provenance{lot, fake, fightsLeft}`. Bag stacks key on all of it (`SameStack`). |
| Modifiers ("Rift affixes") | `ContentData/modifiers.json`, `S/Domain/Combat/ModifierEffect.cs`, `S/Domain/Content/ModifierEntryResolver.cs`, `ModifierMagnitude.cs` | Rolled onto item copies. One effect per modifier (readme). Magnitude = base x `1.25^itemTier` x rift multiplier 1.0/1.3/1.6/2.0 (`ModifierMagnitude.cs`). |
| Relics | `ContentData/relics.json`, `S/Domain/Content/RelicEffect.cs`, `RelicModifier.cs`, `S/Domain/Relics/RelicPool.cs`, `S/Domain/Combat/Session/FightSession.Relics.cs`, `.RelicMechanics.cs` | Per-run, party-wide (`S/Core/FightEncounterAdapter.cs:485-493`). Own rarity enum (`RelicRarity.cs`), own weights, own price table. Two implementation routes: a named `RelicEffect` (hard-coded branch in `FightSession`) or a `RelicModifier` stat list. |
| Not item systems | `upgrades.json` (2 profile-wide purchases), `pools.json` (mana/fury), `reward_tracks.json` (per-character level rail 2-40 of stat bumps and skill unlocks) | None hands out items. Listed so the redesign does not mistake them for parallel item systems. |

**Reward and acquisition sources:**

| Source | Entry point |
|---|---|
| Post-fight 3-card pick | `S/Core/FightController.Input.cs:1157,1171-1176` -> `RunOrchestrator.RollOffers` (`S/Core/Bot/RunOrchestrator.cs:744`) -> `ItemOfferRoll.Roll` (`S/Core/ItemOfferRoll.cs:254`) -> `ItemOfferTable.Choose` (`S/Domain/Rewards/ItemOffer.cs:104`) + `RarityTable.RollTier/RollPlus` + `ModifierTable.RollRiftTier/PickModifiers`. UI: `S/Core/ReckoningController.cs` (`Take` :822, `PaintOffers` :729, hover :408). Take = `RunOrchestrator.TakeOffer` (:761) + `AutoEquipIntoAnEmptySlot` (:788). |
| Post-fight spell book | `RunOrchestrator.PayOut` (`RunOrchestrator.cs:664,684`) -> `VictoryRewards.RollSpellDrop` (`S/Domain/Combat/Session/VictoryRewards.cs:139`) |
| Run-start relic draft | `RunOrchestrator.RelicDraftOffer` (`RunOrchestrator.cs:64`), `RelicPool.DraftWeighted` (`S/Domain/Relics/RelicPool.cs:141`), UI `S/Core/RelicDraftController.cs` |
| Shop room | `ShopStock.RollGear/RollBooks/RollRelics` (`S/Domain/Rewards/ShopStock.cs:346,498,450`), `ShopPricing.cs`, `S/Core/Bot/RunOrchestrator.Shop.cs`, UI `ShopController.cs` (1248 lines) |
| Event merchant shelf | `MerchantShelf.cs`, `S/Domain/Content/EventEntryResolver.Shelves.cs`, `RunOrchestrator.Shelf.cs` (re-uses `ShopStock.RollGear`) |
| Event grants | `RunOrchestrator.Event.cs:589` (relic), `events.json:102` (1 potion) |
| Gold only | Treasure room: `S/Domain/Dungeon/RoomResolution.cs:23-24,83` (15-30 gold) |
| Dead code | `VictoryRewards.RollConsumableDrops` (:84) and `GuaranteesWeapon` (:161): grep finds callers only in `Tests/` |

**Lifecycle:** all gear and the bag are wiped at run end (`S/Core/RunManager.cs:270-286`; `S/Data/SaveData.cs:34`). Every run starts with empty slots and an empty bag. There is no cross-run item progression.

---

## (b) Content inventory

| Type | Count | Character |
|---|---|---|
| Armour items (generated) | 605 (11 materials x 5 slots x 11 tiers) | Pure stat sticks: HP, physical/magical defense, speed, mana regen, ability scores. Differ by material split and tier only. Slots covered: Head, Torso, Legs, Gloves, Shoes. |
| Weapons (generated) | 33 (3 families x 11 tiers) | Flat attack + one scaling grade. No effect. 5.2% of the offerable pool (33/638). |
| Consumables | 2 (potions) | Heal 200 / restore 15 mana. |
| Slots with no content | `Necklace` | `S/Domain/Equipment/EquipmentSlot.cs` defines it; no item in any `ContentData/*.json` uses it (only `relics.json` contains the word). `Weapon2` accepts any Weapon1 item. |
| Modifiers | 25 | 6 elemental on-hit riders (Fire/Ice/Nature/Poison/Arcane/Physical +10%), 6 matching resists (+8), 13 others (chill, root, pushback, lifesteal 8%, flat DR 2, shield-break resist, kill splash, Fortunate +2 Favor, +5 max mana, +1 mana regen, 13% skill discount, wardrune, dodge 6). 12 of 25 are the "same number, different element" pattern. No weight field, all equal odds. |
| Relics | 41 = 34 with a named `RelicEffect` branch + 7 with no effect (6 pure stat-modifier relics, e.g. `thick_hide` +40 HP -5% speed, `vampire_dentures` 10% lifesteal, plus mechanic-less `forest_wardens_tooth`) | Rarity: common 15, uncommon 3, rare 11 (3 undraftable, Shawn-only), ultrarare 8, mythic 3, godlike 1. 38 draftable. The 34 effects are build-defining (double strike, extra turn, phoenix egg, free attack after spell). |
| Item sets with bonuses | 0 | See above. |
| Rarity ladders | 2 | Item rarity = tier band only (`S/Domain/Content/Rarity.cs:34-57`, 6 bands); relic rarity = authored, separate enum (`RelicRarity.cs`). |

---

## (c) Reward moments

| Moment | Trigger | Choices | Randomness | Guaranteed | Outcome |
|---|---|---|---|---|---|
| Fight win: gear | Every won fight (`FightController.Input.cs:1157`) | Pick 1 of 3. No skip, no reroll, no sell (`ReckoningController.cs:822-879`) | `UnityEngine.Random` (`FightController.Input.cs:1176`), not a keyed stream. Tier = `depth/16` + ladder climb (0.22/0.30/0.38 per rung, `LootLadder.cs:41-43`). 3 distinct items from tier+-1 of ALL offerable items. Plus: ladder, max 5 rungs (more with Favor, `RarityTable.cs:172-173`). Rift tier (= number of affixes): ladder 0.12/0.15/0.18, max 3 (`ModifierTable.cs:27,95-97`); each affix uniform from a 12-entry weapon pool or 13-entry armour pool | Boss: tier >= 3 (`RarityTable.cs:62-67`), plus +3 (:211). Elite: tier >= 1 | Item to bag; equipped at once on the first squad member with an empty compatible slot (`RunOrchestrator.cs:788-`). |
| Fight win: book | Per fight | none | 10/20/35% (`VictoryRewards.cs:123-125`), keyed stream (`RunOrchestrator.cs:679-684`) | - | Into `run.unassignedSpellBooks` |
| Fight win: gold/exp | Per fight | none | none (depth-scaled) | yes | `VictoryRewards.For` (:51) |
| Descent start: relic | Hub gate | Pick 1 of 3, or take nothing (`RunOrchestrator.cs:130-146`) | Weighted by rarity 100/45/18/6/2/1 (`RelicPool.cs:121-131`); seeded (`RunOrchestrator.cs:72`) | Exactly one round (`RelicPool.cs:54`) | `run.relicIds` |
| Shop room | Shop node | 4 gear, 3 books, 3 relics; buy affordable; reroll per section 15 x 2^n gold (`ShopPricing.cs:142`); sell bag items at 30% (`:127`) | Keyed streams per section; `GearTierBoost 1` (`ShopStock.cs:317`); no bare commons (:429-439); cheapest card <= 28g (:307, `ShopPricing.cs:190`) | the two floors | Gear to bag, no auto-equip (`ShopMutationTests.cs:164`) |
| Event shelf | `rat_caravan` only | Shelf of gear/consumables, some fakes that break after 3 fights (`FakeWear.cs`) | Keyed streams | - | Provenance-stamped copies |
| Event relic / potion | `petting_zoo`, `bell_in_the_fog` (relic); `demo_wishing_well` (potion) | scripted | - | - | |
| Treasure room | Map node | none | gold 15-30 | - | gold only |
| Elite/boss relic | **does not exist** | | | | The `RunSnapshot` relic comment and PLAN_SHOP F8 (`docs/PLAN_SHOP.md:323-345`) describe "mid-run relic rewards from elites or bosses" as the intended shape. `relicIds.Add` sites: draft, shop, event, debug menu, bot. |

---

## (d) Problems, ranked

### DESIGN problems

**D1. (Critical) The only per-fight reward is a forced pick of 1 of 3 near-equal random pieces, most of them bare.**
- Evidence: `OfferCount = 3`, `TierSpread = 1` (`ItemOffer.cs:74,79`), so the three offers sit within one tier of each other. No skip/reroll/salvage in `Take` (`ReckoningController.cs:822-879`). At favor 0: plus = 0 in 78% (normal) / 70% (elite) of copies (`LootLadder.cs:41-42`), zero affixes in 88% / 85% (`ModifierTable.cs:95-96`). (calc) 69% of normal-fight copies and 60% of elite copies are bare; all three offers are bare in about 32% of normal fights. The shop refuses to sell bare commons (`ShopStock.cs:429-439`); fights hand them out.
- Why it hurts: the decision is "which slot gets a slightly better stat stick", repeated every fight, with no way to decline junk and no upside variance.
- Direction: give fights a reward shape other than 3 random same-tier pieces.

**D2. (Critical) Items have no identity; build-defining content lives only in relics, which fights never give.**
- Evidence: 638 of 640 gear items are generated stat blocks (b). Sets have no bonus (`ResolvedSetPiece.cs:26-29`). Affixes are 25 single-number effects, 12 of them element mirrors, appearing on roughly 12-31% of copies. Relics hold all 34 distinct mechanics but arrive once per run (`RelicPool.cs:54`), by purchase at 60-700g (`ShopPricing.cs:84-89`), or from 2 events.
- Why it hurts: the loot loop (fights) and the exciting objects (relics) are disconnected; picks in the loop never change how you play.
- Direction: put build-changing effects on what fights hand out, or let fights hand out relics.

**D3. (High) Offers ignore the party, so many are irrelevant or dead for the squad.**
- Evidence: `ItemOfferRoll.Candidates()` is every generated item, filtered only by "has a tier" (`ItemOfferRoll.cs:52-58`). The squad has up to 3 characters with different stat needs; gear with unmet `requirements` goes inert (`S/Domain/Equipment/RequirementResolver.cs`, `ItemComparison.NewlyInertSlots`). Weapons are 5.2% of candidates (calc: 3 of 58 items per tier), so about 15% of fights offer any weapon (calc) though weapons are the damage source. Necklace has no content.
- Why it hurts: you often choose among three pieces nobody will wear, and the most build-relevant slot is rarely offered.
- Direction: make offers aware of squad slots/needs, or reduce the slot lottery.

**D4. (High) Five power axes, and the visible rarity reads only one.**
- Evidence: tier (x1.25 per tier, `GearScaling.cs:57`), plus (+15% each, max 10, `S/Domain/Stats/ItemUpgrade.cs`), rift tier (affix count and magnitude x1-2, `ModifierMagnitude.cs`), affix identity, slot/material. Colour and the card meta line show rarity and tier only (`Rarity.cs:34-57`; `ReckoningController.cs:760`); plus shows in the name, rift as a ring, affixes and deltas only in the hover tooltip (`ReckoningController.cs:408-450`). A tier-1 +5 (x1.75 plus factor) and a tier-2 +0 are comparable only by opening tooltips.
- Why it hurts: the card cannot say "this is the good one"; every pick needs a tooltip read.
- Direction: collapse or surface the axes so one glance ranks the offers.

**D5. (High) Plus is pure luck, with no agency and no sink.**
- Evidence: plus is rolled (`ItemOfferRoll.cs:233`) and never changed afterwards; no hone/upgrade action exists in `S/Core` (only glossary text promises "honing", `GlossaryEntries.cs:223`). The bag has no capacity (grep `BagCapacity|StashCapacity|MaxBag` over Core/Domain: no hits), so no inventory pressure; surplus converts only by selling at 30% in a shop (`ShopPricing.cs:127`, `ShopController.cs:432-458`).
- Why it hurts: surplus loot is dead weight, and the glossary names a verb the game lacks.
- Direction: add a use for duplicates and surplus, or drop the plus axis.

**D6. (High) Most of the generated catalogue is unreachable in a normal run.**
- Evidence: 16 steps per tier (`RarityTable.cs:46`); PLAN_SHOP §6: "A 40-step run now passes through roughly two and a half tiers" (`docs/PLAN_SHOP.md:2044`). Higher tiers need consecutive ladder successes (0.22^k). Tiers 4-10 are 7 of 11 tiers, about 64% of the 638 offerable items (calc), with art and budgets up to 9.3x tier 0 that few runs reach.
- Direction: size the content ladder to what depth plus ladder actually reach.

**D7. (Medium) Promised reward hierarchy is dead.**
- Evidence: `GuaranteesWeapon` (`VictoryRewards.cs:161`) and `RollConsumableDrops` (:84, `ItemDropChance 0.3` :30) have no production caller. Potions come only from the caravan shelf and one event (`events.json:102`). `items.json:2` still says drops are tiered by `FightController.RollWeaponDrop` and that elites drop from the weaker half; that method no longer exists.
- Why it hurts: elites and bosses advertise nothing beyond a tier floor and a plus bump; consumables are effectively absent.
- Direction: define what each fight class pays, then delete or revive the dead paths.

**D8. (Medium) Auto-equip decides for the player, by roster order.**
- Evidence: `AutoEquipIntoAnEmptySlot` gives the item to the first squad member in list order with a free matching slot, with no check of requirements or fit (`RunOrchestrator.cs:788-`). Every run starts empty (`RunManager.cs:276`), so the first picks all land on whichever member is first.
- Why it hurts: the most formative picks are assigned arbitrarily and must be undone in the dossier.
- Direction: let the player assign, or assign by comparison score.

**D9. (Medium) Relic draft: one decision, weighted toward commons, shown without context.**
- Evidence: single round (`RelicPool.cs:54`); common weight 100 vs mythic 2 (:121-131); (calc) a draw from the draftable pool is common about 82% of the time (15 x 100 of 1834 total weight). The draft does not show relics already held (AUDIT #241, `AUDIT.md:2521`). Relic rarity is "an authored statement" with no numbers behind it (`relics.json:2`; `RelicRarity.cs:12-18`), so rarity need not track power (`dual_wield`, a second strike, is common).
- Why it hurts: the run-defining choice is one early pick from a mostly common menu, and shop price by rarity (`ShopPricing.cs:84`) may not match value.

**D10. (Medium) A luck stat on gear that loops back invisibly.**
- Evidence: `Fortunate` adds Prince's Favor flat (`modifiers.json`), scaled by tier x rift to 6 at tier 5 and 37 at the top (`ModifierTable.cs:65-71`); Favor boosts plus/affix odds (`ItemOfferRoll.cs:116-125`). Authored base Favor is Shawn's 4 only.
- Why it hurts: its value is never visible as a drop-quality number.

### CODE-STRUCTURE problems

**C1. (High) Parallel pipelines per item kind; new kinds touch many files.**
- Evidence: gear, books, relics and consumables each have their own roll, price and persisted entry kind (`ShopStock.cs` `ShopEntryKind`, :346/498/450; separate gear/relic/book tables in `ShopPricing.cs`). `ItemKind` is referenced in 11 non-test files (`ItemEntryResolver`, `ModifierTable`, `ContentBuilder`, `DebugMenuController`, `ItemDescription`, `ItemDefinition`, three `ContentDatabase` files, `ItemOfferRoll`, `BotRunDriver`, `RunOrchestrator`). Items, weapons and item sets each hand-copy 15-20 fields in `ContentBuilder` (AUDIT #262).
- Cost to add: a new armour material = 1 JSON edit (good). A new item kind or a non-stat item = about 10 files.

**C2. (High) Three overlapping effect vocabularies.**
- Evidence: `ModifierEffectType` (19 members besides None, `ModifierEffect.cs`) for affixes; `RelicEffect` (34, `RelicEffect.cs`) and `RelicModifierType` (16, `RelicModifier.cs`) for relics. Same ideas live in two enums: lifesteal (`vampiric` modifier 8% vs `vampire_dentures` relic 10%), resistance (`ResistanceFlat` vs `TypedResistanceFlat`), speed, mana. A relic effect is a hard-coded `HasRelic(actor, RelicEffect.X)` branch across about 10 `FightSession` partials (references: `.RelicMechanics.cs` 19, `.Relics.cs` 17, 8 others 1-2 each).
- Cost to add: new relic mechanic = `RelicEffect.cs`, `FightSession.Relics.cs`, `FightTuning.cs` + JSON (3 code files). New affix effect = `ModifierEffect.cs`, `ModifierEffectText.cs`, `ModifierTable.cs` (offensive/defensive set), `FightTuning.cs`, `FightSession.cs` + JSON (5 code files; `RootChancePercent` is the example).

**C3. (Medium) Sampling duplicated despite a "one home" helper; two rarity systems.**
- Evidence: `SamplingOps` header says the no-repeat draw is "kept here once rather than hand-rolled twice" (`S/Domain/Rewards/SamplingOps.cs:4-8`), yet the same remove-after-pick loop is hand-written in `RelicPool.Draft` (:110) and `DraftWeighted` (:168), `ShopStock.RollBooks` (:516) and three times in `MerchantShelf.cs` (:98,119,143). `RelicPool.Draft` (unweighted) exists for tests and glossary (header :84-90). Item tier-band rarity and `RelicRarity` are separate ladders with separate price tables.

**C4. (Medium) Inconsistent randomness and floors across sources.**
- Evidence: fight offers use `UnityEngine.Random` (`FightController.Input.cs:1176`); the spell drop in the same settlement uses a (step,node)-keyed stream "so quitting ... does not reroll it" (`RunOrchestrator.cs:679-684`); the relic draft uses a seeded stream (:72); shop and shelf use `RngStreams`. What a quit during the Reckoning does to the offer is unverified. The shop applies quality and affordability floors that fight rewards, using the same `RollOne`, lack (`ShopStock.cs:317-439` vs `ItemOfferRoll.cs:254-286`).

**C5. (Medium) Stale and misplaced text.**
- Evidence: `ItemOffer.cs:102` "reward track widens the offer at level 50" (tracks end at 40, `reward_tracks.json` readme); `items.json:2` cites the gone `FightController.RollWeaponDrop`; `ModifierTable.cs:138-148` is the doc block of `PickModifiers`, but it sits above an unrelated block and `PickModifiers` is at :217; `RunOrchestrator.AutoEquipIntoAnEmptySlot` has `if (!Equip(...)) { return; } return;` (a dead branch, ~:802-806).
- Why it matters: comments no longer say what is rule and what is residue.

**C6. (Low) `ItemInstance` carries lot/fake/wear state used by one event.**
- Evidence: `Provenance`, `FakeWear` and lot-aware `SameStack` (`ItemInstance.cs`, `FakeWear.cs`, `ShopStockEntry.GearInstance/ConsumableInstance`) serve `rat_caravan` only (1 shelf event in `events.json`), yet sit in the core item type and the bag stacking rule.

**C7. (Low) Many tests pin current behaviour; the bot is blind to affixes.**
- Evidence (files referencing each type under `S/Tests`): `ShopStock` 18, `RarityTable` 9, `ItemOfferRoll` 9, `VictoryRewards` 9, `GearScaling` 7, `ItemOfferTable` 6, `RelicPool` 6, `ItemSetEntryResolver` 5, `ModifierEntryResolver` 5, `EquipMove` 5, `ShopPricing` 4. Reshaping tier/plus/rift/offer count rewrites many. The bot's `GearEvaluator.ScoreOffer` reads only itemId + plus (`S/Core/Bot/BotRunDriver.cs:1434`, `GearEvaluator.cs:145`), so bot batches cannot value affixes.

---

## (e) Keep

- Pure, engine-free tables with injected `Func<int,int>` RNG (`LootLadder`, `RarityTable`, `ModifierTable`, `SamplingOps`): testable and seedable.
- `LootLadder`'s geometric ladder (rare outcomes cannot saturate, `LootLadder.cs` header) and the hard boss tier floor 3 (`RarityTable.cs:62-67`).
- One shared "roll one copy" (`ItemOfferRoll.RollOne`, :230) used by rewards, shop and merchant shelf.
- Data-driven generators: a new material or weapon family is a JSON edit; the `GearScaling` budget keeps same-tier sets comparable.
- Squad-wide comparison tooltip (`ItemDescription.SquadComparisonBody` :318, `ItemComparison` incl. newly inert/live slots) and the requirement/inert model.
- Shop mechanics: node-keyed stock, quoted prices, quality + affordability floors, escalating section reroll, sell path (`ShopStock.cs`, `ShopPricing.cs`).
- Per-run wipe of gear (`RunManager.cs:270-286`): a clean roguelike boundary.
- Relics applied party-wide, `RelicPool.Available` shared by draft and shop, mid-draft reload safety (`RunOrchestrator.cs:56-73`).
- Bot harness (`GearEvaluator`, `BotRunDriver`) for measuring any redesign.

---

## (f) Stated intent and owner decisions that constrain a redesign

- Item sets: "leave for now, redesign later" (`docs/OWNER_DECISIONS_2026-09-26.md:247`, #125).
- "No gear balance work until the items redesign, your call" for affix jackpot odds `MaxStep` (`OWNER_DECISIONS_2026-09-26.md:244`, #85).
- Starting kit deleted rather than narrowing the roll; a new profile starts with an empty stash (`items.json:2`; AUDIT #114).
- "A run keeps nothing": gear and bag cleared at run end (`S/Data/SaveData.cs:34`, `S/Core/RunManager.cs:270-286`).
- Relics are per-run only, never permanent (`relics.json:2`); "infinite slots per run" with elite/boss relic drops was the stated shape (`S/Data/RunSnapshot.cs` relicIds comment; `docs/PLAN_SHOP.md:323-345`).
- Show what a fight rewards before you pick it (`docs/DIRECTION_BIBLE.md:53`, tagged [You]); not done for loot today.
- Shop gear price floor: ~1.5x asked, 1.75x shipped, owner's call open (`OWNER_DECISIONS_2026-09-26.md:137`, AUDIT #196).
- Weapon card prints a flat ATK that combat may not grant; owner to grant it or change the text (AUDIT #214, `AUDIT.md:2186`). Not verified: `S/Core/Content/ContentDatabase.Effective.cs:184-199` says a weapon's Attack enters combat through weapon power instead.
- Confirmation rule for irreversible actions is undecided; proposal is "anything that spends or loses confirms, anything that only pays out does not" (AUDIT #225, `AUDIT.md:2326`). Affects any new skip, salvage or sell button.
- Spells stop levelling at 9 and levels run to 40, so "spells only grow through the reward track and gear" (`OWNER_DECISIONS_2026-09-26.md:141`): gear is expected to carry late power.
- "Playable elements come first, and any of this can change when gameplay needs it" (`docs/DIRECTION_BIBLE.md:23`).
- Project rules: content from JSON, scenes from `Domain/UiKit/Screens` (CLAUDE.md generation rules); tests never recompute production formulas (gotcha 5); commit gate `tools/run_tests_parallel.ps1 -Changed` with `-BuildContent`/`-BuildScenes`.
