PLAN_ITEMS.md rev 4, 2026-10-13, owner-answered, nothing built

# Plan: items and item rewards

Inputs: the audit (`docs/handoffs/item_redesign/README.md`, D1-D10, C1-C7),
`DESIGN.md`, `RESEARCH_THEORY.md` (T1-T15), `RESEARCH_GAMES.md` and the
round-1 and round-2 reviews (`REVIEW_R*.md`). The owner's answers are
section 3. Coupled to
`docs/PLAN_RUN_MAP.md` rev 4; the amendments it needs are in section 5.
Paths are under `Assets/_Project/Scripts/` unless they start with `docs/` or
`ContentData/`. Citations are to `main` at `6ea8abef`. "(calc)" marks
arithmetic from cited constants.

Run-map terms used here: a **chip** is the prize a fight node shows before
you enter; **strength** 1-3 is that node's threat against the party; an
**anchor** is an optional elite on the map; the **heart** is the boss node,
where living anchors join the boss fight (run map P5). A realm is 8 steps.

## 1. Behavioural contracts

### 1.1 What each reward moment pays

Every fight still pays exp and gold (`VictoryRewards.For`). Everything else
is its chip.

| Moment | Pays | Choice |
|---|---|---|
| Normal fight won | its chip: gold, consumable, **Gear** or **Book** | Gear: a page (1.2); others paid without a choice |
| Anchor dies (on the map, or at the heart after run map P5) | its chip from the anchor weights: **Gear** or **Relic** | a page |
| Boss won | **Gear** (boss class) and **Relic** (Uncommon relics and up) | a page each |
| Hub gate, run start | relic draft as today, showing held relics (#241) | 1 of 3 or none |
| Shop | gear, books, relics, honing | buy, reroll, sell, hone |
| Rest | full heal **or** +1 honing on one slot | pick one |
| Event | as authored, through the same Grant step (2.2) | as authored |

- A fight is won → its chip is Gear → a gear page. No Gear chip → no page.
  The Reckoning's gear pick after every fight goes (in I3).
- Anchors pay Relic or Gear by weight, never a guaranteed relic: anchors
  reach 6 a realm (`PLAN_RUN_MAP.md:213`).
- **Book chip** pays one book from `RollSpellDrop`'s pool and draw
  (`VictoryRewards.cs:144-156`) without its chance gate, into
  `unassignedSpellBooks`, no recipient, excluding books already unassigned or
  learned by every fielded member. The hidden per-fight chance goes.
- **Empty pool.** The map card reads the relic and book pools live, so a chip
  whose pool empties shows gold from then on. Emptied between entering and
  settling → the page pays gold and says so.

### 1.2 A settlement and its pages

**Pages.** A settlement merges all its Gear chips into **one gear page** and
all its Relic chips into **one relic page**: at most two pages. With *k*
chips of a kind the page shows *k* + 2 cards and grants *k* **choices**.
Order: gear page, then relic page. The page's class is the highest among its
chips (boss > anchor > normal).

**A choice on the gear page** is one of:
- **Take** a card: it is equipped on its recipient (`EquipmentOps.Equip`).
  The displaced copy goes to the bag and the page lists it.
- **Hone instead**: +1 honing on the slot that card would fill, for that
  recipient (1.5), and the card is discarded. Not offered on a slot at
  `MaxPlus`; if every shown slot is at `MaxPlus`, that choice pays the card's
  sale price.

Taking cannot produce a hone, and honing cannot produce an item, so neither
dominates (an always-take player sells displaced copies; an always-hone
player never gets a new tier or affix). Unused choices on leaving the page
hone the first card's slot. Confirmation follows #225 once decided.

**A choice on the relic page** takes a relic. Unused choices pay 30% of the
cheapest shown relic's price (`ShopPricing.cs:80-90,127`) through
`ScalePrice` (1.6). A relic is not shown when every member it reaches
already gets its mechanic; otherwise the card names who it adds nothing for,
including a member whose worn trait already has it.

**Randomness.** New stream `RngStreams.Reward` (14), opened with
`(step, nodeId, page)` in `RngStreams.Derive`'s `a, b, c` (`RngStreams.cs:97`):
`step` is the settlement step (the heart's for a boss), `nodeId` the int
node id, `page` 0 for gear and 1 for relic. Pages are rolled once at
settlement and held in memory (`PendingPage`, 2.5). The fight stream is
untouched, so fight replays and bot traces do not shift.

**Gear page, card construction, in order:**

1. **Owners.** Members fielded at settlement, fallen or not, in squad order,
   cycling until *k* + 2 owners are named (one card each for a squad of 3 on
   a one-chip page). With fewer than 3 members the cycle starts with the
   member who has the most empty slots (ties: lowest summed item tier, then
   squad order).
2. **Weapon guarantee** (boss and anchor pages, `GuaranteesWeapon`,
   `VictoryRewards.cs:166`, kept for elites): one card becomes a weapon card,
   for the owner with an empty `Weapon1`, else the lowest-tier weapon among
   owners.
3. **Slots** for the rest, over all eight slots: empty `Weapon1` → weapon;
   otherwise weight 3 per empty and 1 per filled slot, with `Weapon2`
   eligible only once `Weapon1` is filled. A weapon card fills `Weapon1` if
   empty, else `Weapon2` if empty, else replaces the lower-tier weapon.
   `Necklace` is an ordinary slot (1.3).
4. **Base.** Uniform over the slot's bases whose requirements hold when worn
   on the owner's current loadout, checked through the existing fixpoint
   (`ContentDatabase.Effective.cs:128-148`). None hold → uniform over all of
   them, shown as "inert until …" (the dossier's existing state). Distinct
   bases across the page. Requirements rise with tier and refuse a lone
   piece of an uncommitted material (`GearScaling.cs:273-280`), so deep
   cards lean toward the owner's material.
5. **Tier, per card.** Today's `RarityTable.RollTier` (`RarityTable.cs:99-135`)
   stays, with three changes: the centre is `FloorTier(step)` (`:50`), +1 on
   a boss page; the absolute class floors (`TierFloorFor`: elite 1, boss 3,
   `:62-67`) go; Favor's tier dial rises from 0.002 to 0.006 a point
   (`TierFavorPerPoint`, `:97`; cap `TierMaxStep` 0.45, `:98`). The climb is
   `LootLadder.Climb` at 0.22 / 0.30 / 0.38 by class (`LootLadder.cs:41-43`),
   up to 5 rungs, then capped at tier 12. Cards on one page can differ in
   tier.

   | Class, Favor 0 | +0 | +1 | +2 | +3 or more |
   |---|---|---|---|---|
   | Normal (0.22) | 78% | 17.2% | 3.8% | 1.1% |
   | Anchor (0.30) | 70% | 21% | 6.3% | 2.7% |
   | Boss (0.38, centre +1) | 62% | 23.6% | 9.0% | 5.5% |

   (calc, P(climb ≥ s) = p^s). Favor 20 adds 0.12: a normal card then climbs
   at least one tier 34% of the time instead of 22%. The page header shows
   the squad's Favor and its effect.
6. **Quality, per card**, by `LootLadder.Climb` with step *p*, 3 rungs:

   | Source | p | Favor room to 0.55 | Common | Uncommon | Rare | Legendary |
   |---|---|---|---|---|---|---|
   | Normal, shop, shelf | 0.35 | 33 pts | 65% | 22.8% | 8.0% | 4.3% |
   | Anchor | 0.42 | 22 pts | 58% | 24.4% | 10.2% | 7.4% |
   | Boss (floor Uncommon, 2 rungs) | 0.45 | 17 pts | 0 | 55% | 24.8% | 20.3% |

   (calc). Favor adds 0.006 a point (`LootLadder.cs:52-53`, cap 0.55). At
   least one Legendary card on a one-chip page: normal 12.4%, anchor 20.6%,
   boss 49.4% (calc). All three Common on a normal page: 27.5%, against 32%
   bare today (audit D1).
7. **Floors**, on the best card (highest quality, then highest tier, then
   first), raised by adding affixes or the trait: strength 2 → Uncommon,
   3 → Rare; the boss weapon card → Rare; the first gear page after one whose
   best card was Common → Uncommon. **Legendary pity:** the seventh gear page
   in a row without a Legendary card shown makes its best card Legendary;
   any shown Legendary resets the count (`legendaryPity`).
8. **Affixes and trait.** Uncommon 1 affix, Rare 2, Legendary 2 plus one
   trait. Affixes are distinct rows from the slot's pool (`weapon` or
   `armour`; the necklace uses `armour`). The trait comes from the slot's
   trait pool (1.3), among traits the owner can use (`requires`) and does not
   already get from another worn copy or a relic reaching them. None left →
   the card is Rare with a third affix.

**Card face:** name, a tier badge, recipient, frame colour = quality, the one
largest stat delta against what the recipient wears in that slot, and one
line (the trait, or the strongest affix). The delta is the one-glance rank:
it folds tier and affixes into what the card changes for its wearer. When the slot is occupied, a second line
names a trait or affix the replaced copy has and the card lacks ("loses:
Lifesteal"). The rest is in the tooltip.

### 1.3 Traits

- A trait is a build-changing effect (the kind relics hold) that works only
  for its wearer. It sits on **Legendary copies of any slot**, one per copy.
- **Stacking.** A member's traits differ in kind: the roll never offers a
  kind the member already gets from another worn copy or a relic reaching
  them (replacing the copy that holds it is allowed). If a dossier swap or a
  later relic duplicates one anyway, the second does nothing (flag, 2.1) and
  the dossier marks it "inactive". No cap per member beyond eight slots.
- **The necklace is an ordinary slot**: a one-piece `amulet` set with a stat
  budget, rolled through the same ladders. With traits on every slot, a
  trait-guaranteed necklace would be the best card on every page and need a
  roll and pity of its own.
- **Pools.** Each trait row names pool `weapon` (attack and cast hooks) or
  `armour` (damage taken, turn and move hooks). First content (I2c): 16 rows
  on kinds I1 already implements, wearer-scoped, 8 per pool:
  - weapon: DualWield, SwordInABox, FirstRune, LuckyDeck, IceFingernail,
    MagicMarker, ChargingCrystal, LongCount;
  - armour: MagicalShield, TinFoilPipe, BallerinasSlippers, BerserkersVest,
    SparringBuckler, EssenceSiphon, WorldEndersCrown, DancersAnklet.
  A trait and a relic may share a kind; the dedupe above and on the relic
  page keeps a duplicate from being offered blind. New trait kinds need a
  hook and are outside this plan.

### 1.4 Rarity, colour, tier

`Rarity` from tier bands (`RarityBands.For`, `Domain/Content/Rarity.cs:36-56`)
is **replaced** by `Quality {Common, Uncommon, Rare, Legendary}`, derived
from the copy (affix count, trait), never stored. "Legendary" is free: the
relic ladder runs Common to Godlike without it (`RelicRarity.cs`). Tier is a
separate badge. Names keep the tier adjective (`itemsets.json`
`tierAdjectives`; content adds adjectives for tiers 11-12, art stays on the
top frame). Consumers that switch: Reckoning, shop, tooltip, bag, dossier,
`FloorTierOf`. Mythic goes.

### 1.5 Honing

- Honing belongs to a character's slot: `RunSnapshot.slotHoning`, a list of
  `{characterId, slot, plus}`, independent of what the slot holds.
  `EquipmentLoadout` drops an emptied slot's entry (`EquipmentLoadout.cs:167-181`),
  so honing cannot live on `EquipmentSlotEntry`; its `plus` field goes.
  Weapon1 and Weapon2 are separate slots, so a sword moved between hands
  reads the new hand's honing. A benched member keeps theirs. It ends with
  the run, since the snapshot does.
- Drops, stock and bag copies have no plus. The plus roll goes
  (`ItemOfferRoll.cs:233`), with Favor's plus odds and `BossBonusPlusRungs`
  (`RarityTable.cs:211`).
- Sources: **hone instead** on a gear page; the shop, any number of +1s at
  `21 × (1 + 0.35 × plus)` step-0 gold through `ScalePrice`, the same for every slot
  whether filled or empty, so no cheap item gaming the price; one Rest at
  most (`PLAN_RUN_MAP.md:115`), full heal (`RoomResolution.cs:87-88`) or +1.
  At full HP the Rest +1 is free, accepted.
- `ItemUpgrade.MaxPlus` stays 10, +15% each (`ItemUpgrade.cs:25,41`), so ×2.5
  on a slot. The glossary entry (`GlossaryEntries.cs:233`) is rewritten for
  slots.

### 1.6 Deep floors and gold

Per step, enemy health ×1.075 and attack ×1.038, frozen at step 200
(`DifficultyCurve.cs:46-47, 206`); fight gold rides the health curve
(`:143`). Gear grows 1.4% a step (armour, 1.25^(1/16)) and 1.9% (weapon,
`GearScaling.cs:57,68`) (calc).

| Step (realm) | Armour 1.25^t | Weapon 1.35^t | Enemy HP | Enemy ATK | Fight gold |
|---|---|---|---|---|---|
| 16 (2) | 1.25 | 1.35 | 3.2 | 1.8 | 3.2 |
| 80 (10) | 3.05 | 4.48 | 326 | 19.8 | 326 |
| 160 (20) | 9.31 | 20.1 | 1.06e5 | 391 | 1.06e5 |
| 200 (25) | 14.6 | 36.6 | 1.91e6 | 1738 | 1.91e6 |

- **Tier cap:** the centre follows `FloorTier` (uncapped, `RarityTable.cs:50`)
  and every roll caps at 12, reached at step 192, where enemies are about
  to freeze. This rule replaces content `maxTier` 10 as the cap
  (`RarityTable.cs:100,142`; `ShopStock.cs:352`).
- **Prices:** every price is authored in step-0 gold and charged through one
  function, `ShopPricing.ScalePrice(amount, step)` =
  `DifficultyCurve.ScaleReward(amount, step)`, the curve fight gold uses:
  gear, books, relics, honing, rerolls, sale and the unused-relic payout.
  Today's ratios then hold at every depth.
- **Gold income is cut.** A new literal `VictoryRewards.GoldIncomePermille`
  scales every fight's gold before `ScaleReward`, and Treasure's 15-30
  (`RoomResolution.cs:23-24`) the same. Metric: a realm's gold income ÷
  `ScaleReward(1, realm start step)`, on the bot's median route, floors
  1-10. Target **120 ± 20 step-0 gold a realm**: one shop gear card (32-60),
  one hone (21) and part of a Common relic (60). I2a adds the literal at
  1000 (no change); I5 measures today's figure and sets it. A floor-1 fight
  pays 16-24 today (`ShopPricing.cs:184-189`) over 7-31 fights a realm
  (calc, run map route lengths), so expect about half or less.
- **Gear's share of deep power** is unmeasured: `DifficultyCurve.cs:19-31` and
  `GearScaling.cs:36-46` disagree. I5 measures the split (gear, levels,
  spells, mechanics) through `FightEncounterAdapter.ToCombatant` at steps
  40, 80, 160.

### 1.7 Removed, and unchanged

Removed: the per-fight gear pick; the ±1 tier spread (`ItemOffer.cs:79`)
and absolute tier floors; plus roll; rift tier; roster-order auto-equip
(`RunOrchestrator.cs:794-817`); `Fortunate`; the 12 element mirrors become 2
(a damage rider in the wearer's `AttackType`, `CombatantKit.cs:47`, Physical
if none; a resist with a rolled element), so 25 affixes become 14;
`offerRerollsUsed` (`RunSnapshot.cs:193`).

Unchanged: per-run wipe; relics per run and party-wide unless `bearer`; shop
reroll and sell; `Provenance` (C6 out of scope); item sets have no bonus
(#125), with a home in 2.1; hub draft weights (D9), with #241 in I0 and the
boss relic page Uncommon and up; `RollTier`, retuned (1.2 step 5).

## 2. The shared structure

### 2.1 One effect vocabulary

Four today: `ModifierEffectType`, `RelicEffect`, `RelicModifierType`,
`TalentEffectType` (`Domain/Combat/TalentEffect.cs:36`), overlapping on
lifesteal (summed at `FightSession.cs:984`), resistance, speed, max mana,
kill splash, an extra action on a kill and longer transforms.

**Shape:** `Effect { Kind, Magnitude, Threshold, Against, HasAgainst,
AgainstMagical }`, today's `ModifierEffect` (`ModifierEffect.cs:340`).
Mechanics share no numeric shape (`RelicEffect.cs:3-10`), so behaviour stays
one C# block per kind at its hook; data picks kinds and headline numbers;
secondary numbers (Charging Crystal's period) stay in `FightTuning`.

| Shared | Today | After |
|---|---|---|
| Kind enum | 4 | `EffectKind`, closed, appended only |
| Per-kind facts | comments | `EffectCatalog`: hook, stacking, tier-scaled or not, allowed holders, unit, text template |
| Query | `HasRelic`, `ModifierEffects.Best`, `RelicLifestealPercent`, `TalentEffects.Best` | `CombatantState.Effects`: `Has`, `Magnitude`, `Threshold` |
| Headline number | `FightTuning` | `Magnitude` in JSON, unscaled |
| Rules text | hand-typed | generated (I2c); `description` keeps flavour |

**The seam.** Relic checks go through `FightSession.HasRelic`
(`FightSession.Riders.cs:358`) → `CombatantKit.HasRelic` (`CombatantKit.cs:162`):
about 36 call sites in 11 files (39 grep hits, less the two definitions and
the inner call). They become `actor.Effects.Has(kind)`. A source-scan test
fails when a non-stat kind has no read site.

**Stacking, by holder:**

| Kinds | Affix + affix (one wearer) | Any other pair (affix, trait, relic, talent) |
|---|---|---|
| Stat | best copy only (`ModifierEffectSet.cs:39`) | summed, percents first (`RelicModifier.cs:239-286`) |
| Mechanic | none (affixes hold no mechanics) | flag: a second holder adds nothing; trait rolls avoid it, relic cards name it, the dossier marks it inactive (1.3) |

Tier scaling is applied once, when `FightEncounterAdapter` builds the
`EffectSet` (`:209`, where `ContentDatabase.ModifierEffects` scales today):
`Magnitude × 1.25^tier` for kinds the catalog marks tier-scaled, from the
copy's tier.

**Holders:** relic (party, or `bearer`, `CombatantKit.cs:148-158`); affix
(wearer); trait (wearer, any slot); talent (I6); set bonus (wearer with N pieces of one `setId`; named,
not built, #125). Stat kinds fold where `RelicModifiers.Apply` runs
(`FightEncounterAdapter.cs:215-253`).

**What it saves.** A new relic mechanic: still 3 code files. A new affix or
trait kind: 3 (kind, catalog row, hook), 4 if it has secondary numbers; today
5. A new reward source on existing kinds: one `rewards.json` row.

### 2.2 One offer pipeline

```
rewards.json source + context (step, node, page, class, squad, held, pity)
 → owners → weapon → slots → base → tier → quality → floors → affixes, trait
 → Offer[] (pure Domain, Func<int,int> rng)  → Grant / Hone / Pay (Core)
```

- `Offer`: `Kind` (Gear, Relic, Book, Consumable, Gold, Hone, Heal), payload,
  gear copy, recipient or none, price or none. `ShopEntryKind`
  (`ShopStock.cs:16`) maps onto it; the persisted int stays.
- One sampler, `SamplingOps` (C3). `ItemOfferRoll.RollOne` (`:230`) stays the
  one "roll one copy".
- **Shop and shelf** use steps 4-8 with `recipients: none`: tier centre
  `FloorTier(step) + 1` (`ShopStock.cs:317`) with the normal climb, *p* 0.35,
  every card at least
  Uncommon, except one affordability card at `FloorTier(step)`, Uncommon,
  which is the cheapest legal piece. That replaces the 28g anchor
  (`ShopPricing.cs:190`), which assumed a +1 piece the plan deletes
  (21 × 1.5 = 32 at tier 0, calc). Section streams and rerolls stay. Honing
  is a service row outside stock, not rerolled.
- Rest is a page with a Heal and a Hone card.

### 2.3 Gear copies: base + tier on the copy

- One definition per **base**: material × piece (55), weapon family (3),
  the `amulet` set. Ids drop the `_pN` suffix of the 638 generated ids
  (`Resources/Content/Items/brigandine_boots_p0.asset`), so none collide.
- `ItemInstance` = base id + `Tier` + `ModifierIds` + `Provenance`. `Plus` and
  `RiftTier` go. `SameStack` keys on id, tier, modifier set, provenance.
- Stats: one accessor, `ContentDatabase.Effective`, takes the copy and the
  slot's honing. Stat fields leave the base definition, so the 29 files that
  read tier-baked stats fail to compile until moved.
- Price: `ScalePrice(BaseFor(tier) × (1 + 0.5 × q), step)`, *q* = affixes,
  +2 for a trait (`RiftStep`, `ShopPricing.cs:46`).
- C1: removing baked stats deletes most of `ContentBuilder`'s hand-copied
  fields (AUDIT #262). The `ItemKind` union stays: splitting it touches three
  `ContentDatabase` files and every `GetItem` caller for two potion rows.

### 2.4 Content JSON

Fields go in `docs/CONTENT_SCHEMA.md` when built:

| File | Change |
|---|---|
| `relics.json` | `effect` + `modifiers[]` → `effects[]` of `{type, magnitude, threshold, damageType}`; `requiresConvergenceAbility` → `requires[]`; ids unchanged |
| `modifiers.json` | add `pool` (`weapon`, `armour`) replacing `ModifierTable`'s hard-coded sets, `trait` (bool), `requires[]`; a trait row holds one mechanic effect |
| `itemsets.json`, `weapons.json` | one asset per piece or family; `maxTier` = art frames; `tierAdjectives` gain tiers 11-12; a one-piece `amulet` set for `Necklace` |
| `rewards.json` (new, `IOrderedContent`, `LoadOrdered`) | per source: `id`, `kind`, `cardsPerChoice` (2 extra), `tierOffset`, `ladderStep`, `tierStep`, `qualityFloor`, `weaponGuarantee`, `recipients` (`perMember`/`none`), `relicWeights` (6 ints by `RelicRarity`), `relicRarityMin`, `unusedPays` (`hone`/`cheapestSale`), `affordabilityCard`, `stream` (an `RngStreams` constant name), `sortOrder` |

`requires` is a closed list (`Convergence`, `Spell`, `Transform`) checked
against the member's kit, generalizing the relic convergence gate
(`ResolvedRelic.cs:33`, `RelicPool.cs:18`).

### 2.5 State and save data

- `InventoryEntry`, `ShopStockEntry` (`ShopStock.cs:41-58`, also in
  `MerchantShelfStock.entries`, `RunSnapshot.cs:365,396`): add `tier`; drop
  `plus`, `riftTier`. `EquipmentSlotEntry`: drop `plus`.
- `RunSnapshot`: add `slotHoning`, `legendaryPity`, `lastPageBestCommon`; drop
  `offerRerollsUsed`.
- `PendingPage { sourceClass, kind, nodeId, page, choices, resolved,
  cards[] { copy or relicId, recipientId } }`, held in memory on the run,
  `[NonSerialized]`: a run found on disk is settled, never resumed.
- **No version bump, on purpose, as a bet on the no-resume rule.**
  `RunManager.SettleOnOpening` (`:109-111`) ends any run found on entering a
  slot, and `EndRun` clears equipment and bag (`:279,289`); unknown ids are
  pruned (`SaveData.cs:753`); removed fields need no bump (`:39-40`). A
  resume feature must add a bump and a migration. I2a adds a load test of a
  snapshot carrying old `_pN` ids, `plus` and `riftTier`.

## 3. Decided (owner, 2026-10-13)

1. Deep gear: tier climbs to 12 by step 200 and stops with enemies (1.6).
2. Every price scales with the fight-gold curve, and fight gold income is
   cut to a target (1.6).
3. One card made for each member; you take one (1.2 step 1).
4. Gear only from Gear chips, anchors and bosses (1.1).
5. Honing on the slot: hone instead, shop, Rest; max +10 (1.5).
6. Not necklace-only: the top quality of any piece carries a trait (1.3).
7. Not one tier per page: cards keep tier variance, and Favor improves tier
   as well as quality (1.2 steps 5-6).
8. Books only as a visible map prize and in the shop (1.1).
9. Favor stays the visible luck stat; `Fortunate` goes.
10. Placeholder amulet art until commissioned.

## 4. Phases

`-Changed` = `tools/run_tests_parallel.ps1 -Changed`. Test areas: `art`,
`combat`, `content`, `hub`, `rng`, `run`, `ui`.

| # | Work | Needs | Agent | Gate |
|---|---|---|---|---|
| I0 | today's offers on `RngStreams.Reward`; #241; stale text: C5, `RelicEffect.cs:8`, `ModifierEffectSet.cs` header, `DifficultyCurve.cs:203` ("step 80"; it is 160) | none | implementer | `test.ps1 rng`, `run`; `-Changed -BuildScenes` |
| I1 | `EffectKind`, `EffectCatalog`, `EffectSet`; relics and affixes onto it; seam swapped; headline numbers to JSON; no text change | I0 | senior, `Escalation: architecture` | a literal test per moved number and the stacking test (section 6), written first; `test.ps1 combat`, `content`; `-Changed -BuildContent`; bot `RunTrace` byte-identical to post-I0 on a fixed `-Seed`/`-Runs` |
| I2a | Copy model: bases, tier on copy (cap 12, adjectives 11-12), `Quality`, `slotHoning`, stat accessor seam, `ScalePrice` on every price, `GoldIncomePermille` at 1000, `SameStack`, shop and shelf entries and floors; removes plus roll, rift tier, ±1 spread, absolute tier floors, `BossBonusPlusRungs`, Mythic | I1; run map P2 merged (both edit `RunSnapshot`) | senior, `Escalation: cross-layer` | `test.ps1 content`, `run`, `hub`, `ui`; old-id load test; `-Changed -BuildContent -BuildScenes`; diff `Art/`; shop stock pinned as new literals |
| I2b | Pages: `rewards.json`, pipeline steps 1-8 (a Legendary falls back to Rare + affix until I2c), retuned `RollTier`, Legendary pity, `PendingPage`, take / hone instead, relic page (`OfferRowLayout` relic cards), Favor line on the header, `GearEvaluator` reads tier, quality, affixes, recipient and hone (`GearEvaluator.cs:146`). Pages fire after every fight until I3 (I3 landed: a page only for a Gear or Relic chip). | I2a | senior, `Escalation: architecture` | tier and quality tables pinned on fixed seeds; PlayMode test: boss and two anchors settle, take on page 1, re-enter, leave with unused choices, assert hones, gold and pages; `test.ps1 run`, `ui`, `rng`; `-Changed -BuildContent -BuildScenes` |
| I2c (landed) | Traits: 16 trait rows and their catalog holder flags, `requires`, the trait draw and dedupe, dossier "inactive" mark; amulet set with placeholder art; affixes 25 → 14, `Fortunate` out; generated rules text | I1, I2b | implementer | per pool, a Legendary card carries a usable, non-duplicate trait; the stacking test; `test.ps1 content`, `run`, `combat`; `-Changed -BuildContent` |
| I3 (landed) | Who pays what: chip and anchor weights, boss pages, book chip; per-fight page and hidden book roll removed | I2b; run map P2 with amendments 5.1-5.3 | implementer | test: a Gear chip queues exactly one gear page, a Relic chip one relic page, other chips none; `test.ps1 run`; `-Changed -BuildScenes` |
| I4 | Honing at the shop and the Rest page; glossary | I2b; run map P2 | implementer | `test.ps1 run`, `ui`; `-Changed -BuildScenes` |
| I5 | Balance from bot batches: `GoldIncomePermille` to the 120 ± 20 target, chip weights, *p*, tier steps, Favor dials, hone price, `MaxStep` (#85), shop floor (#196); reports: deep-power split, traits per member, Legendary pages per run, Favor held by floor 10, unused-choice rate, relics held at floors 10/20/30 | I2-I4, run map P3 | implementer, then verifier gold metric inside the band on floors 1-10; report to the owner; literals pinned. **Landed 2026-10-14:** `GoldIncomePermille` 150, pooled median realm 124-127; other dials measured, not changed (`handoffs/item_redesign/I5_BALANCE_REPORT.md`) |
| I6 | Talents onto `EffectKind`, duplicate pairs merged | I1 | implementer | `test.ps1 combat`, `content`; `-Changed -BuildContent` |

**Order.** I0 and I1 run beside run map P1-P2 and land before run map P5,
which reworks the same `FightSession` partials. I2a follows run map P2's
merge. Between I2a and I2b drops are +0 with no honing source; bot numbers
from that window are not balance evidence. I3 follows run map P2 and
precedes run map P3, so P3's bot report sees the real prize mix; if P2
slips, I2b's after-every-fight pages stay live. Heart-anchor payment is
proved only after run map P5 (amendment 5.4).

## 5. Amendments `PLAN_RUN_MAP.md` rev 4 needs

1. **P1, prize kinds** (`:96-102`): add `Relic`. Keep the enum value `Item`
   (shown as "Gear"); `Item`, `Book` and `Relic` stay at weight 0 until I3.
2. **P1, `RealmProfile`** (`:189-201`): add `anchorPrizeWeights`, a
   `{kind, weight}` list like `prizeWeights`; default all weight 0, meaning
   an anchor pays exp and gold only.
3. **P2, payout**: settlement hands `Item`, `Book` and `Relic` chips to the
   item pipeline (a no-op until I3), and the node card reads the relic and
   book pools live for "empty pool → gold" (1.1).
4. **P5, heart**: anchors that die at the heart add their chips to the boss
   settlement, in map order, before pages merge (1.2); its tests include one
   with a heart anchor carrying a Gear chip.
5. **P3, bot**: `MapView` includes Gear and Relic chips and anchor prizes.
6. **P2 and I2a both edit `RunSnapshot`**: P2 merges first; item fields need
   no v9 entry (2.5).
7. **P0 EV sheet**: gold-chip multipliers and detour gold values are set
   against the realm gold target (1.6), and redone once I5 sets
   `GoldIncomePermille`.

## 6. Risks and expected score

- **I1 breadth.** About 36 call sites; numbers moving into JSON can change
  silently, hence literal pins first and the byte-identical trace.
- **Stacking.** Tests pin a member with two lifesteal affixes and Vampire
  Dentures, and one with the same trait kind on two worn copies plus the
  relic, against the 2.1 table.
- **Trait density.** Traits on every slot can pile mechanics on one member;
  traits per member (I5) is the check, the Legendary *p* the dial.
- **Table tests** (C7: `ShopStock` 18 files, `ItemOfferRoll` 9, `RarityTable`
  9) are rewritten with literals (gotcha 5).
- **Relic inflation** at 6 anchors a realm: bounded by the anchor Relic
  weight and pool exhaustion.
- **Requirements at depth** narrow the base draw (1.2 step 5); I5 reports the
  inert-card rate.

| # | Now | Plan | Why, or the lever for a 1 |
|---|---|---|---|
| T1 | 0 | 2 | hone instead is a real alternative to every card |
| T2 | 0 | 1 | only Legendary cards differ in plan (12-49% of pages). Lever: more trait kinds |
| T3 | 0 | 2 | take forgoes a hone, hone forgoes the item |
| T4 | 0 | 1 | tier and quality vary per card; the delta ranks fit across different slots. Lever: one-slot pages |
| T5 | 0 | 1 | tier badge, colour, delta, line. Lever: fold tier into the colour |
| T6 | 2 | 2 | rolled at settlement, shown before choosing |
| T7 | 1 | 1 | no reroll. Lever: a capped reroll |
| T8 | 0 | 1 | Legendary pity, Common-page floor; quit-safe only because a quit ends the run |
| T9 | 0 | 1 | a Gear chip still opens a modal. Lever: Gear weight |
| T10 | 1 | 2 | relic pages from anchors and bosses; Legendary traits |
| T11 | 0 | 2 | every base offerable at every depth |
| T12 | 1 | 2 | per-member cards, requirements checked |
| T13 | 0 | 2 | recipient, displaced copy and lost traits shown |
| T14 | 1 | 2 | hone instead converts unwanted cards; prices scale with gold; income is targeted |
| T15 | 1 | 2 | class tier and quality steps, weapon guarantee, boss floor and relic page |
| | **11** | **24** | |

## 7. Unresolved

- Past step 200 enemies stop growing and levels do not (1.6); whether a run
  still ends is a `DifficultyCurve`/run-map decision this plan does not make.
- Gear's real share of deep power waits on I5's split through `ToCombatant`.
- **Favor sources.** With `Fortunate` gone, Favor comes only from base values
  (Shawn's 4), the reward track and events, so "better tiers for higher
  Favor" matters only if those grant enough. I5 reports Favor held by
  floor 10; adding Favor sources is an owner call.
- **Gold target.** 120 ± 20 step-0 gold a realm is my number from shop
  prices; the owner may set another before I5.
- **Traits per member** are uncapped (1.3); revisit if I5 shows one member
  carrying most of the party's mechanics.

## 8. Rejected alternatives

- **Effects as trigger/condition/outcome JSON, or an event bus:** the
  mechanics share no shape (`RelicEffect.cs:3-10`).
- **Skip for gold:** take, then sell the displaced copy, is never worse,
  since the bag is unbounded and gold is spent only at a shop.
- **Honing on the item, or on `EquipmentSlotEntry`:** a replacement throws it
  away, and an emptied slot drops the entry (`EquipmentLoadout.cs:167-181`).
- **Hone price from the slot's item tier:** a cheap item in the slot makes
  honing cheap.
- **A trait-guaranteed necklace:** with traits on every slot it would be the
  best card on every page and need its own roll and pity.
- **Guaranteed relic per elite** (6 a realm); **one page per chip** (a boss with anchors reaches 8 pages);
  **bag capacity** (hone instead handles surplus); **one rarity ladder for
  items and relics** (rolled quality vs authored statement).
