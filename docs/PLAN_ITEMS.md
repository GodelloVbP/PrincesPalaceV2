PLAN_ITEMS.md rev 2, 2026-10-13, nothing built

# Plan: items and item rewards

Inputs: the audit (`docs/handoffs/item_redesign/README.md`, D1-D10, C1-C7),
the draft (`DESIGN.md`), the theory tests (`RESEARCH_THEORY.md`, T1-T15), the
survey (`RESEARCH_GAMES.md`) and both round-1 reviews (`REVIEW_R1_*.md`).
Coupled to `docs/PLAN_RUN_MAP.md` rev 4 (prize chips :91-102, anchors :213,
endless descent :215). Paths are under `Assets/_Project/Scripts/` unless they
start with `docs/` or `ContentData/`. Citations are to `main` at `6ea8abef`.
"(calc)" marks arithmetic from cited constants.

## 1. Behavioural contracts

### 1.1 What each reward moment pays

Every fight still pays exp and gold (`VictoryRewards.For`). Everything else a
fight pays is its map prize chip (run map §2), shown before you enter.

| Moment | Pays | Choice |
|---|---|---|
| Normal fight won | its chip: gold, consumable, **Gear** or **Book** | Gear: pick 1 or skip; others paid without a choice |
| Anchor (elite) dies, on the map or at the heart | its chip from the anchor weights: **Gear** or **Relic** | pick 1 or skip |
| Boss won | **Gear** (tier +1) then **Relic** (Uncommon and up) | each pick 1 or skip |
| Hub gate, run start | relic draft as today, showing held relics (#241) | 1 of 3 or none |
| Shop | gear, books, relics, **honing** (1.5) | buy, reroll, sell, hone |
| Rest | full heal **or** hone one slot +1 | pick one |
| Event | as authored, through the same Grant step (2.2) | as authored |

- Gear: a fight is won → its chip is Gear → a gear pick is queued. Chip not
  Gear → no gear pick. The Reckoning's gear pick after every fight goes.
- Anchors pay Relic or Gear by weight, never a guaranteed relic: anchors
  reach 6 a realm (`PLAN_RUN_MAP.md:213`), so relics per realm are one dial,
  the anchor Relic weight.
- **Book chip** pays one book from the pool and draw `RollSpellDrop` uses
  (`VictoryRewards.cs:144-156`) without its chance gate, into
  `unassignedSpellBooks`, no recipient. The draw excludes books already
  unassigned or learned by every fielded member. The hidden per-fight
  chance goes (Q8).
- **Empty pool.** The map card reads the relic or book pool live, so a chip
  whose pool empties mid-realm shows gold from then on. If it empties between
  entering and settling, the pick pays its skip value (1.2) and says so.

### 1.2 A pick

**Settlement.** When a fight settles, every pick it pays is rolled at once
into `RunSnapshot.pendingPicks`, in this order: the boss gear pick, the boss
relic pick, then each anchor that died in that fight, in map order. The
Reckoning shows them as pages of one screen ("Pick 2 of 4"), with a "skip
the rest" control that pays every remaining skip value. Cards are stored, so
re-entering the screen in the same process shows the same cards whatever
changed since.

**Randomness.** New stream `RngStreams.Reward` (14), opened with
`(step, nodeId, pickIndex)` in `RngStreams.Derive`'s `a, b, c`
(`RngStreams.cs:97`); heart anchors use the heart's `nodeId` and continue
`pickIndex`. Pity is read once at settlement for all picks in it and
advances as each pick resolves. The fight stream is untouched, so fight
replays and bot traces do not shift (why `FightController.Input.cs:1266-1271`
avoided a seeded stream).

**Gear pick, card construction, in order:**

1. **Owners.** The members fielded at settlement, fallen or not, in squad
   order, one card each. Spare cards (squad under 3) go to the member with
   the most empty slots; ties go to the lowest summed item tier, then squad
   order.
2. **Necklace roll.** One roll per pick, chance = source base (`rewards.json`:
   normal 0.05, anchor 0.20, boss 0.35) + 0.05 × `necklacePity`. On success
   one card becomes a necklace, for the first owner without one worn, else
   the first owner. Its trait is drawn from traits the owner can use
   (`requires`, 2.4) and does not already get (a relic reaching them, or a
   worn necklace). No usable trait → no necklace card this pick. Necklaces
   never enter the slot draw.
3. **Weapon guarantee** (boss and anchor, `VictoryRewards.GuaranteesWeapon`,
   `:166`, kept for elites): one non-necklace card becomes a weapon, for the
   owner whose `Weapon1` is empty, else whose `Weapon1` tier is lowest.
4. **Slots** for the rest: empty `Weapon1` → weapon; else weights 3 per empty
   and 1 per filled slot over the owner's non-necklace slots.
5. **Base.** Uniform over the bases for that slot whose `requirements` the
   owner meets, distinct across the pick (as `ItemOfferTable.Choose` does).
   Not biased toward the owner's build: material is the off-build surprise.
   No base fits → redraw the slot without it; no slot fits → the card passes
   to the next owner; no card at all → the pick pays its skip value.
6. **Tier.** One for the pick: `min(step / 16, 10)` (`RarityTable.cs:46`),
   +1 for a boss (still capped at 10).
7. **Quality** per card, by `LootLadder.Climb` with step `p`, 3 rungs
   (P(climb ≥ s) = p^s, `LootLadder.cs`):

   | Class | p | Common | Uncommon | Rare | Epic |
   |---|---|---|---|---|---|
   | Normal | 0.40 | 60% | 24% | 9.6% | 6.4% |
   | Anchor | 0.50 | 50% | 25% | 12.5% | 12.5% |
   | Boss (floor Uncommon) | 0.40 | 0 | 60% | 24% | 16% |

   (calc). All three cards Common: normal 21.6%, anchor 12.5%, boss 0,
   against 32% bare today (audit D1). Not-bare per normal card: 40% vs 31%.
   Squad Favor (`ItemOfferRoll.SquadFavor`, `:140`) raises `p` at the
   existing per-point rate up to `LootLadder`'s cap 0.55. Pinned by a
   fixed-seed test.
8. **Floors**, on the best non-necklace card (highest quality, then first),
   raising it by adding affixes: chip strength 2 → Uncommon, 3 → Rare; boss
   weapon card → Rare; a pick following one whose best card was Common →
   Uncommon. A necklace never satisfies a floor.
9. **Affixes**: that many distinct rows from the slot's pool (`weapon` or
   `armour`), magnitude `base × 1.25^tier`.

**Card face:** name, tier, recipient, colour = quality, the **one** largest
stat delta against what the recipient wears in that slot, and one line (the
trait, or the strongest affix). The rest is in the tooltip. If the slot is
occupied, the card says what it replaces.

**Take** equips the card on its recipient (`EquipmentOps.Equip`). The
displaced copy goes to the bag and the page lists it. Honing stays with the
slot (1.5), so a replacement loses nothing honed; a displaced necklace keeps
its trait and can be worn again. Roster-order auto-equip
(`RunOrchestrator.cs:794-817`) is deleted.

**Skip** pays the **best** card's sale price (`ShopPricing.SellPrice`,
30% of `GearPrice`), now, with no shop needed. Contract: skip gold ≥ the
gold from take, swap back, sell, which needs a shop the realm may not have
(0.75 chance, in a pocket, `PLAN_RUN_MAP.md:214`). So skip is never
dominated for gold, and take is the only way to the item. The skip button
shows its gold. Confirmation follows #225: if adopted, skip confirms, since
it discards cards.

**Relic pick:** 3 cards from `RelicPool.Available`, weights per source
(2.4). A relic is excluded when every member it reaches already gets its
mechanic; otherwise the card names who it adds nothing for. Skip pays 30% of
the cheapest card's `RelicPrice` (`ShopPricing.cs:80-90`; 18g for Common).

### 1.3 Traits

- A trait is a build-changing effect (the kind relics hold) that works only
  for its wearer, and lives **only on necklaces**, one each. A character
  wears at most one; relics stay party-wide.
- Necklaces get a stat budget like other slots, so a deeper one is still an
  upgrade when its trait is a flag.
- Necklace quality is shown as its own colour, **"Trait"**, not "Legendary",
  which the relic ladder keeps.

### 1.4 Rarity, colour, tier

`Rarity` from tier bands (`RarityBands.For`, `Domain/Content/Rarity.cs:36-56`,
Mythic at tier 10) is **replaced** by `Quality {Common, Uncommon, Rare, Epic,
Trait}`, the affix count or necklace. Names keep the tier adjective
(`itemsets.json` `tierAdjectives`). Consumers that switch: Reckoning,
shop card and sort, tooltip, bag, dossier, and `FloorTierOf`'s reward
floors. Mythic goes.

| Axis | Set by | Card |
|---|---|---|
| Tier | step (+1 boss) | stated, same across a pick |
| Quality | ladder + floors + Favor | colour |
| Trait / affixes | with quality | one line |
| Honing | the player, per slot | on the slot, not the card |
| Fit | delta vs recipient | one number |

### 1.5 Honing

- Honing belongs to a character's **slot**, not the item:
  `EquipmentSlotEntry.plus` (`Domain/Equipment/EquipmentLoadout.cs:24`)
  keeps its value when the item in it changes or the slot is emptied. Drops,
  stock and bag copies have no plus. It clears with equipment at run end
  (`RunManager.cs:279`).
- The plus roll goes (`ItemOfferRoll.cs:233`), with Favor's plus odds,
  `BossBonusPlusRungs` (`RarityTable.cs:211`) and `PlusFactor` in prices.
- Where, per realm: the shop (0.75 chance), any number of +1s at
  `BaseFor(tier of the slot's item) × (1 + 0.35 × current plus)` gold
  (`ShopPricing.cs:48`, `PlusStep` `:42`); and at most one Rest
  (`PLAN_RUN_MAP.md:115`), which offers full heal (`RoomResolution.cs:87-88`)
  or +1. HP refills only after the boss, so Rest costs the missing HP; at
  full HP the +1 is free, which is intended.
- `ItemUpgrade.MaxPlus` stays 10, +15% each (`ItemUpgrade.cs:25,41`): ×2.5 on
  a slot, about four tiers of budget (1.25^4 = 2.44, calc). The glossary
  entry (`GlossaryEntries.cs:233`) is rewritten for slots.

### 1.6 Deep floors

Tier stops at 10 (step 160, realm ~21) and honing at +10. Past that, gear does
not grow; the descent is built to end runs ("the player's power is capped and
the dungeon's is not", `Domain/Dungeon/DifficultyCurve.cs:200-205`). Enemy
scaling is ×1.075 health and ×1.038 attack per step, capped at step 200
(`:46-47, :206`):

| Step | Armour budget 1.25^t | Weapon 1.35^t | Enemy HP | Enemy ATK |
|---|---|---|---|---|
| 16 | 1.25 | 1.35 | 3.2 | 1.8 |
| 80 | 3.05 | 4.48 | 326 | 19.8 |
| 160 | 9.31 | 20.1 | 1.06e5 | 391 |
| 320 | 9.31 | 20.1 | 1.91e6 (cap) | 1738 (cap) |

(calc). Gear is a small share of deep power. That contradicts "gear carries
late power" (`OWNER_DECISIONS_2026-09-26.md:141`); I5 measures it and Q9 asks.

### 1.7 Removed, and unchanged

Removed: the per-fight gear pick; tier ladder and ±1 spread
(`ItemOffer.cs:79`); plus roll; rift tier; roster-order auto-equip;
`Fortunate`; the 12 element mirrors become 2 (a damage rider in the
wearer's `AttackType`, `CombatantKit.cs:47`, Physical if none; a resist with
a rolled element), so 25 affixes become 14; `offerRerollsUsed`
(`RunSnapshot.cs:193`).

Unchanged: per-run wipe; relics per run, party-wide unless `bearer`; shop
stock, reroll, sell; `Provenance` (C6 out of scope); item sets have no bonus
(#125), with a home named in 2.1. Hub draft weights stay (D9); its missing
context is fixed by #241 in I0, and the boss draws Uncommon and up.

## 2. The shared structure

### 2.1 One effect vocabulary

There are four today: `ModifierEffectType`, `RelicEffect`,
`RelicModifierType` and `TalentEffectType` (`Domain/Combat/TalentEffect.cs:36`).
They overlap: lifesteal (summed at `FightSession.cs:984`), resistance,
speed, max mana, kill splash, an extra action on a kill, longer transforms.

**Shape:** `Effect { Kind, Magnitude, Threshold, Against, HasAgainst,
AgainstMagical }`, which is `ModifierEffect` (`ModifierEffect.cs:340`) today.

**Data vs code.** `RelicEffect.cs:3-10` is right that mechanics share no
numeric shape, so behaviour stays one block of C# per kind at its hook. Data
chooses kinds and their headline number. Secondary numbers (Charging Crystal's
period, Phoenix Egg's duration) stay in `FightTuning`.

| Shared | Today | After |
|---|---|---|
| Kind enum | 4 | `EffectKind`, closed, appended only |
| Per-kind facts | comments | `EffectCatalog`: hook, stacking, tier-scaled or not, allowed holders, unit, text template |
| Query | `HasRelic`, `ModifierEffects.Best`, `RelicLifestealPercent`, `TalentEffects.Best` | `CombatantState.Effects`: `Has`, `Magnitude`, `Threshold` |
| Headline number | `FightTuning` | `Magnitude` in JSON |
| Rules text | hand-typed | generated (I2c); `description` keeps flavour |

**The seam.** All relic checks go through `FightSession.HasRelic`
(`FightSession.Riders.cs:358`) → `CombatantKit.HasRelic` (`CombatantKit.cs:162`);
37 call sites in 11 files become `actor.Effects.Has(kind)`. A source-scan
test (the shape of the existing lint tests) fails when a non-stat kind has no
read site.

**Stacking.** Stat kinds sum across holders, percents first
(`RelicModifier.cs:239-286`); affixes of one kind on one wearer count once,
at the best copy (`ModifierEffectSet.cs:39`). Mechanic kinds are flags. This
is today's behaviour as one rule.

**Holders:** relic (party, or `bearer`, `CombatantKit.cs:148-158`); affix
(wearer, tier-scaled); trait (wearer); talent (I6); set bonus (wearer with N
pieces of one `setId`; named, not built, #125). `FightEncounterAdapter`
builds each `EffectSet` once per fight from relics that reach the member
(`:485-493`), worn affixes and trait (`:209`); stat kinds fold where
`RelicModifiers.Apply` runs (`:215-253`).

**What it saves.** A new relic mechanic still costs 3 code files (kind,
catalog row, hook block). A new affix or trait kind drops from 5 files to 3
(pool and text move to JSON and the catalog). A new reward source on
existing kinds is one `rewards.json` row.

### 2.2 One offer pipeline

```
rewards.json source + context (step, node, pickIndex, class, squad, held, pity)
 → candidates → owners → necklace → weapon → slots → base → tier → quality → floors → affixes
 → Offer[] (pure Domain, Func<int,int> rng)  → Grant / Skip (Core)
```

- `Offer`: `Kind` (Gear, Relic, Book, Consumable, Gold, Hone, Heal), payload
  id, gear copy, recipient or none, price or none. `ShopEntryKind`
  (`ShopStock.cs:16`) maps onto it; the persisted int stays.
- One sampler, `SamplingOps`; the five hand-rolled loops go (C3).
  `ItemOfferRoll.RollOne` (`:230`) stays the one "roll one copy".
- Shop and shelf share candidates, `RollOne`, prices and Grant, and keep
  their own section streams, floors and rerolls.
- Rest is a pick with a Heal and a Hone card on the same screen.

### 2.3 Gear copies: base + tier on the copy

- One definition per **base**: material × piece (55), weapon family (3),
  necklace bases. Ids drop the `_pN` suffix of the 638 generated ids
  (`Resources/Content/Items/brigandine_boots_p0.asset`), so none collide.
- `ItemInstance` = base id + `Tier` + `ModifierIds` (affixes or trait) +
  `Provenance`. `Plus` and `RiftTier` go. `SameStack` keys on id, tier,
  modifier set and provenance.
- Stats: one accessor, `ContentDatabase.Effective`, takes the copy and the
  slot's plus. Stat fields leave the base definition, so the ~30 readers of
  tier-baked stats (`ItemDescription`, `ItemComparison`, `EquipmentOps`,
  `CharacterDossierController.Loadout`, `GearEvaluator`, `ShopPricing`,
  bag sort, naming) fail to compile until moved.
- Price: `GearPrice(tier, quality) = BaseFor(tier) × (1 + 0.5 × affixes)`
  (today's `RiftStep`, `ShopPricing.cs:46`); a necklace prices as 3.
  `floorNoBare` = at least Uncommon; `floorCheapestAtMost` keeps 28g.
- C1: removing baked stats deletes most of `ContentBuilder`'s 15-20 hand-
  copied fields (AUDIT #262). The `ItemKind` union stays: splitting it
  touches three `ContentDatabase` files and every `GetItem` caller for two
  potion rows.

### 2.4 Content JSON

Fields go in `docs/CONTENT_SCHEMA.md` when built:

| File | Change |
|---|---|
| `relics.json` | `effect` + `modifiers[]` → `effects[]` of `{type, magnitude, threshold, damageType}`; `requiresConvergenceAbility` → `requires[]`; ids unchanged |
| `modifiers.json` | add `pool` (`weapon`, `armour`, `necklace`), replacing `ModifierTable`'s hard-coded sets; add `requires[]`; `necklace` rows are traits |
| `itemsets.json`, `weapons.json` | one asset per piece or family; `maxTier` = art frames and cap; one-piece `amulet` set for `Necklace` |
| `rewards.json` (new, `IOrderedContent`, loaded by `LoadOrdered`) | per source: `id`; `kind`; `count`; `tierOffset`; `ladderStep` (p); `qualityFloor`; `necklaceChance`; `weaponGuarantee`; `recipients` (`perMember`/`none`); `relicWeights` (6 ints by `RelicRarity`); `relicRarityMin`; `skippable`; `skipPays` (`bestSale`/`cheapestSale`/`none`); `floorNoBare`; `floorCheapestAtMost` (gold); `stream` (an `RngStreams` constant name); `sortOrder` |

`requires` is a closed list (`Convergence`, `Spell`, `Transform`) checked
against the member's kit; it generalizes the relic convergence gate
(`ResolvedRelic.cs:33`, `RelicPool.cs:18`).

**Run map coupling.** The prize-kind enum gains `Relic`, and `RealmProfile`
gains an anchor prize weight list. Both should land in run map P1/P2 with
weight 0, so I3 changes weights, not shapes. A saved profile without the
list means anchors pay their exp and gold only.

### 2.5 Save data

- `InventoryEntry`, `ShopStockEntry` (`ShopStock.cs:41-58`, also inside
  `MerchantShelfStock.entries`, `RunSnapshot.cs:365,396`): add `tier`; drop
  `plus`, `riftTier`. `EquipmentSlotEntry.plus` changes meaning to slot
  honing and must survive an empty slot.
- `RunSnapshot`: add `necklacePity`, `lastPickBestCommon`, `pendingPicks`
  (cards stored as `ShopStockEntry`); drop `offerRerollsUsed`.
- **No version bump, on purpose, as a bet on the no-resume rule.**
  `RunManager.SettleOnOpening` (`:109-111`) ends any run found on entering a
  slot, and `EndRun` clears equipment and bag (`:279,289`); unknown ids are
  pruned (`SaveData.cs:753`); removed fields need no bump (`:39-40`). A
  resume feature must add a bump and a migration. A pre-I2 build opening an
  I2 save drops `tier` and prunes new ids: dev-only. I2a adds a load test of
  a snapshot carrying old `_pN` ids and `riftTier`.

## 3. Owner questions

Unanswered ones go ahead on the recommendation.

1. **Gear only from fights whose map chip says Gear (and anchors, bosses),
   instead of after every fight?** Rec: yes.
2. **Per-character or squad-wide fight reward?** (a) one card made for each
   member, take one; (b) three cards, you choose the wearer; (c) the reward
   rotates between members. Rec: (a).
3. **Build-changing gear only through the necklace (one per character);
   anchors and bosses also pay relic picks, the boss Uncommon and up?**
   Rec: yes.
4. **Honing belongs to the slot, not the item: no random "+" on loot, you
   raise a slot at the shop or at a Rest instead of healing, max +10, and a
   new item in that slot keeps it?** Rec: yes.
5. **Skipping pays the best card's sale price, now?** Rec: yes.
6. **Same tier for all cards; a boss one tier above with at least Uncommon
   cards and a Rare weapon, instead of today's floor of tier 3?** Rec: yes.
7. **Favor stays as the visible luck stat for item quality; drop the
   `Fortunate` affix?** Rec: yes.
8. **Spell books only as a visible map prize and in the shop, no hidden
   per-fight chance?** Rec: yes.
9. **Gear stops growing at tier 10 and +10 (realm ~21), as the code intends
   today, though that makes gear a small share of deep power?** Rec: yes for
   now; I5 reports the share before anything changes.
10. **Necklace art: placeholders until commissioned?** Rec: yes.

## 4. Phases

`-Changed` = `tools/run_tests_parallel.ps1 -Changed`. Test areas are the
folders under `Tests/EditMode` and `Tests/PlayMode`: `art`, `combat`,
`content`, `hub`, `rng`, `run`, `ui`.

| # | Work | Needs | Agent | Gate |
|---|---|---|---|---|
| I0 | today's offers on `RngStreams.Reward`; #241; stale text: C5, `RelicEffect.cs:8`, `ModifierEffectSet.cs` header, `DifficultyCurve.cs:203` ("step 80"); dead branch `RunOrchestrator.cs:813-817` | none | implementer | `test.ps1 rng`, `run`; `-Changed -BuildScenes` |
| I1 | `EffectKind`, `EffectCatalog`, `EffectSet`; relics and affixes onto it; seam swapped; headline numbers to JSON, each pinned as a literal first; no text change | none | senior, `Escalation: architecture` | `test.ps1 combat`, `content`; `-Changed -BuildContent`; bot `RunTrace` byte-identical to pre-I1 on a fixed `-Seed`/`-Runs` |
| I2a | Copy model: bases, tier on copy, `Quality`, slot honing storage, stat accessor seam, prices, `SameStack`, shop/shelf entries, old-id load test | Q4, Q6 | senior, `Escalation: cross-layer` | `test.ps1 content`, `run`, `hub`, `ui`; `-Changed -BuildContent -BuildScenes`; diff `Art/`; shop stock pinned as new literals: same streams and counts, floors hold |
| I2b | Pick model: `rewards.json`, pipeline, steps 1-9 minus necklaces, `pendingPicks`, multi-pick Reckoning with relic cards (`OfferRowLayout`), skip, `GearEvaluator` reads quality, affixes and recipient (`GearEvaluator.cs:146`) | Q2, Q5, Q7 | senior, `Escalation: architecture` | `test.ps1 run`, `ui`, `rng`; quality table pinned on fixed seeds; `-Changed -BuildContent -BuildScenes` |
| I2c | Necklaces: amulet set, trait pool, `requires`, necklace roll and pity, affixes 25 → 14, generated rules text | Q3, Q10 | implementer | `test.ps1 content`, `run`; `-Changed -BuildContent` |
| I3 | Who pays what: chip and anchor weights, boss picks, book chip, per-fight pick and hidden book roll removed | Q1, Q8; run map P2 | implementer | `-Changed -BuildScenes` |
| I4 | Honing: shop service, Rest pick, glossary | Q4; run map P2 | implementer | `test.ps1 run`, `ui`; `-Changed -BuildScenes` |
| I5 | Balance from bot batches: weights, `p`, necklace chance, hone price, `MaxStep` (#85), shop floor (#196); report skip gold as a share of realm gold and gear's share of deep power | I2-I4 | implementer, then verifier | report to the owner; literals pinned |
| I6 | Talents onto `EffectKind`, duplicate pairs merged | I1 | implementer | `test.ps1 combat`, `content`; `-Changed -BuildContent` |

**Order against the run map.**
- I0, I1, I2a can run beside run map P1-P2; none touch the map or step
  contract. I1 must land **before run map P5**, which reworks the same
  `FightSession` partials.
- Run map P1/P2 carry the `Relic` value and anchor weight list (2.4).
- I2b before I3; I3 and I4 **after run map P2**; I3 **before run map P3**,
  so its bot report sees the real prize mix. I5 after run map P3.

## 5. Risks

- **I1 breadth.** 37 call sites; numbers moving into JSON can change
  silently, hence the literal pins and the byte-identical trace.
- **Stacking.** A test pins a member with two lifesteal affixes and Vampire
  Dentures against today's total.
- **Table tests** (C7: `ShopStock` 18 files, `ItemOfferRoll` 9, `RarityTable`
  9): rewritten with literals (gotcha 5).
- **Fewer gear picks** under Q1: the Gear chip weight is the dial.
- **Relic inflation** at 6 anchors a realm: anchor Relic weight and pool
  exhaustion bound it; I5 reports relics held at floors 10, 20, 30.
- **Gold.** Fight gold scales ×1.075 a step (`DifficultyCurve.cs:143`) and
  relic prices are flat, so no per-realm skip budget holds deep; skip stays
  at 30% of a card's price, the rule that keeps selling below buying
  (`ShopPricing.cs:123-127`).
- **Necklace art** does not exist; placeholders first.

### Expected theory-test score

| # | Now | Plan | Why, or the lever for a 1 |
|---|---|---|---|
| T1 | 0 | 2 | skip pays the best sale price now |
| T2 | 0 | 1 | cards differ by wearer and slot; only a necklace differs in plan. Lever: more trait kinds |
| T3 | 0 | 2 | taking forgoes the shown skip gold and two members' upgrades |
| T4 | 0 | 1 | colour ranks quality, but cards are for different slots. Lever: none without one-slot picks |
| T5 | 0 | 1 | colour, one delta, one line: three. Lever: drop colour from the face |
| T6 | 2 | 2 | rolled at settlement, stored, shown before choosing |
| T7 | 1 | 1 | skip payout and slot honing, no reroll. Lever: a capped reroll |
| T8 | 0 | 2 | necklace pity and the Common-streak floor; quitting cannot reroll, since the run ends |
| T9 | 0 | 1 | gold and consumables auto-pay, but a Gear chip opens a modal at chip frequency. Lever: Gear weight |
| T10 | 1 | 2 | relic picks from anchors and bosses; necklace traits |
| T11 | 0 | 2 | every base at every depth |
| T12 | 1 | 2 | per-member cards, requirements checked |
| T13 | 0 | 2 | recipient on the card, displaced copy shown |
| T14 | 1 | 2 | skip pays, honing sinks gold, sell stays |
| T15 | 1 | 2 | ladder by class, weapon on anchor and boss, boss floor and relic |
| | **11** | **25** | |

## 6. Rejected alternatives

- **Effects as trigger/condition/outcome JSON, or an event bus:** the
  mechanics share no shape (`RelicEffect.cs:3-10`); handlers need
  `FightSession` internals either way.
- **Traits on top-quality armour too:** uncapped traits per wearer.
- **Guaranteed relic per elite:** up to 6 a realm.
- **Necklaces in the slot lottery:** about 36% of picks would carry one
  (calc, 1 − (6/7)³ with 7 empty slots), so not rare.
- **Plus on the item:** a replacement throws away honed levels, so honing
  and taking gear fight each other.
- **Tiers past 10:** contradicts the capped-power design, and 1.25^t grows
  without bound.
- **A modal cap by merging picks:** "skip the rest" caps the time; each pick
  was earned.
- **Bag capacity:** skip and sell already handle surplus.
- **Splitting `ItemKind`:** cost above, for two potion rows.
- **Merging item and relic rarity ladders:** rolled quality vs authored
  statement; they share a palette, not an enum.
