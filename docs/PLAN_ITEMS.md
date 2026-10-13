PLAN_ITEMS.md rev 1, 2026-10-13, nothing built

# Plan: items and item rewards

Inputs: the audit (`docs/handoffs/item_redesign/README.md`, problems D1-D10,
C1-C7), the draft (`DESIGN.md` there), the theory tests (`RESEARCH_THEORY.md`,
T1-T15) and the game survey (`RESEARCH_GAMES.md`). Coupled to
`docs/PLAN_RUN_MAP.md` rev 4, which reserves Item and Book prize kinds
(:91-102) and makes the run endless (+8 steps per realm, :215). Paths are
under `Assets/_Project/Scripts/` unless they start with `docs/` or
`ContentData/`. Citations are to `main` at `6ea8abef`.

## 1. Behavioural contracts

### 1.1 What each reward moment pays

Every fight still pays exp and gold (`VictoryRewards.For`). Everything else a
fight pays is its map prize chip (run map §2), shown before you enter.

| Moment | Trigger | Pays | Choice |
|---|---|---|---|
| Normal fight | won | its chip: gold, consumable, **Gear**, or **Book** | Gear: 1 of 3 or skip. Others: no choice. |
| Anchor (elite) | dies, on the map or at the heart | its chip, drawn from the anchor weights: **Gear** or **Relic** (never gold or consumable) | 1 of 3 or skip |
| Boss | won | **Gear** at tier +1 with one weapon card, **and** a **Relic** pick | each 1 of 3 or skip |
| Hub gate | descent starts | relic draft, as today, showing held relics (#241) | 1 of 3 or none |
| Shop | entered | gear, books, relics, plus **honing** (1.5) | buy, reroll, sell, hone |
| Rest | entered | full heal **or** hone one worn item +1 | pick one |
| Event | scripted | grants through the same Grant step (2.4) | as authored |

- Trigger → condition → outcome, gear: a fight is won → its chip is Gear →
  the Reckoning opens a gear pick. Chip not Gear → no gear pick. The
  Reckoning's after-every-fight gear pick goes.
- Anchors get Relic or Gear only because there can be up to 6 per realm
  (`PLAN_RUN_MAP.md:213`): a guaranteed relic per elite, as `DESIGN.md` 1.1
  had, would hand out up to 7 relics a realm. Relic count per realm is the
  profile's anchor prize weight, one dial.
- The hidden per-fight spell-book chance (`VictoryRewards.RollSpellDrop`,
  `VictoryRewards.cs:144`) goes; books arrive only as a visible Book chip or
  from the shop (Q8).
- Relic pool empty → that Relic prize pays gold at the Skip value of 1.4.
  The chip shows gold in that case, since the pool is known at realm open.

### 1.2 A gear pick

- **Cards.** Always 3. One per fielded squad member, labelled with the
  member's name; with fewer than 3 members the spare cards go to the member
  with the lowest summed comparison score (`ItemComparison`), ties by squad
  order.
- **Slot.** Drawn from that member's slots: an empty `Weapon1` wins outright;
  otherwise empty slots weigh 3 and filled slots 1 (literals, tuned in I6).
  The draw never yields an item whose `requirements` the member fails
  (`RequirementResolver`).
- **Boss.** The card for the member whose weapon scores lowest is a weapon
  (`VictoryRewards.GuaranteesWeapon`, `:166`, gets its live caller).
- **Tier.** One tier for the whole pick: `step / 16`
  (`RarityTable.cs:46`), +1 for a boss. The ±1 spread (`ItemOffer.cs:79`)
  and the tier ladder go. The card states the tier.
- **Quality = rarity = the colour.** Common 0 affixes, Uncommon 1, Rare 2,
  Epic 3. A necklace card is Legendary: it carries one trait (1.3) and no
  affixes. The affix count is rolled by `LootLadder` (kept), its rung chances
  by encounter class, plus squad Favor (Q7). No rift multiplier: affix
  magnitude is `base × 1.25^tier` only.
- **Strength.** A Gear chip's strength (run map §2) sets a quality floor on
  the best card: strength 1 none, 2 at least Uncommon, 3 at least Rare.
- **Card face.** Name, tier, colour, recipient, the one or two largest stat
  deltas against what the recipient wears in that slot, and one line: the
  trait, or the strongest affix. Everything else is in the tooltip.
- **Take** equips the card on its recipient (`EquipmentOps.Equip`); what it
  replaces goes to the bag. Roster-order auto-equip
  (`RunOrchestrator.cs:794-817`) is deleted.
- **Skip** pays the shop sale price of the cheapest card
  (`ShopPricing` sell path, 30%). It only pays out, so it does not confirm
  (#225 proposal). No new constant.
- **Pity.** Trigger: a gear pick resolves (take or skip). Condition: no
  necklace card was shown. Outcome: `necklacePity` +1, and the necklace slot
  weight for every card of the next pick is `1 + pity` (literal step, I6).
  A shown necklace card resets it to 0. Saved per run.
- **Randomness.** Rolled once, at settlement, on a new keyed stream
  `RngStreams.Reward` (14) opened on `(step, nodeId)`. The Reckoning re-derives
  the same cards on reopen; pity advances on resolve, never on roll. The
  fight stream is untouched, so fight replays and bot runs do not shift
  (the reason `FightController.Input.cs:1266-1271` used `UnityEngine.Random`).
  A run does not survive the process (`RunManager.cs:65-80`), so this buys
  honest previews and reproducible bots, not quit safety.

### 1.3 Traits

- A **trait** is a build-changing effect (the kind relics hold: strike twice,
  a free attack after a spell) that works only for its wearer.
- Traits live **only on necklaces**, one per necklace. So a character wears
  at most one trait, a necklace card is always the build-changing card, and
  the empty `Necklace` slot (`EquipmentSlot.cs`) gets a job.
- Necklaces carry a normal stat budget from the shared generator (2.3), so a
  deeper necklace is still an upgrade when its trait is a flag.
- Precedence: a trait whose kind the recipient already gets (from a relic
  reaching them, or a worn necklace) is never rolled for them. A relic offer
  that duplicates a worn trait is offered, and its card says who already has
  it.

### 1.4 What the player sees per axis

| Axis | Source | On the card |
|---|---|---|
| Tier | step (+1 boss) | stated, equal across a pick |
| Quality | `LootLadder` + Favor | colour |
| Trait / affixes | rolled with quality | one line; rest in tooltip |
| Plus | honing only, drops are +0 | in the name |
| Fit | comparison vs recipient | delta numbers |

Five rolled axes become two rolled (tier is fixed by step, plus by the player).

### 1.5 Honing

- The plus roll goes (`ItemOfferRoll.cs:233`), with Favor's plus odds and
  `BossBonusPlusRungs` (`RarityTable.cs:211`).
- Shop: hone one item +1 for gold; price rises per plus and per tier
  (literals, I6).
- Rest: full heal (`RoomResolution.cs:87-88`) **or** +1 on one worn item.
  HP refills only after the boss under the run map, so the trade is real
  except at full HP, where honing is free; that is intended.
- `ItemUpgrade.MaxPlus` drops from 10 to 5 (`ItemUpgrade.cs:25`); +15% per
  plus stays (`:41`). The glossary entry (`GlossaryEntries.cs:233`) reads
  both constants and stays correct.

### 1.6 Removed

Per-fight gear pick; tier ladder and spread on drops; plus roll; rift tier;
auto-equip by roster order; the `Fortunate` affix (D10); the 12 element
mirror affixes collapse to 2 (one damage rider, one resist), whose element
follows the item's material: 25 affixes become 15. `offerRerollsUsed`
(`RunSnapshot.cs:193`, dead).

### 1.7 Unchanged

Per-run wipe (`RunManager.cs:279-289`); relics per run and party-wide unless
authored with a `bearer`; relic draft shape; shop stock, floors, section
reroll, sell; `Provenance`/fake wear (C6, out of scope); item sets have no
bonus (#125), but 2.1 names where one would live.

## 2. The shared structure

### 2.1 One effect vocabulary

Today there are **four**, not three: `ModifierEffectType` (affixes),
`RelicEffect` and `RelicModifierType` (relics), and `TalentEffectType`
(`Domain/Combat/TalentEffect.cs:36`). They overlap: lifesteal (affix and relic,
summed at `FightSession.cs:984`), resistance, speed, max mana, kill splash
(`OnKillSplashPercent` vs `KillSplashPercentOfAttack`), an extra action on a
kill (`Bloodlust` vs `ExtraAttackOnKill`), longer transforms
(`BellwethersBell` vs `TransformDurationBonus`).

**Shape.** One record, the one affixes and talents already use:

```
Effect { Kind: EffectKind, Magnitude: int, Threshold: int,
         Against: DamageType, HasAgainst: bool, AgainstMagical: bool }
```

`ModifierEffect` (`ModifierEffect.cs:340`) is this shape today;
`RelicModifier` (`RelicModifier.cs:116`) is it minus Threshold.

**What is data and what is code.** `RelicEffect.cs:3-10` argues that the
mechanics share no numeric shape and a data-driven effect system would be a
fake abstraction. That holds for the behaviour, so the plan does not author
trigger/condition/outcome in JSON (`DESIGN.md` 1.3 did). Each kind's
behaviour stays one block of C# at one hook. Data chooses which kinds a
holder carries and their numbers. What becomes shared is everything around
the behaviour:

| Shared | Today | After |
|---|---|---|
| Kind enum | 4 enums | `EffectKind`, closed, appended only |
| Per-kind facts | scattered comments | `EffectCatalog`: hook, stacking rule, whether tier scales it, which holders may carry it, unit, rules-text template |
| Combatant query | `HasRelic`, `ModifierEffects.Best`, `RelicLifestealPercent`, `TalentEffects.Best` | one `EffectSet` on `CombatantState`: `Has`, `Magnitude`, `Threshold` |
| Headline number | `FightTuning` constants for relics | the effect's `Magnitude` |
| Rules text | hand-typed relic descriptions | generated from catalog + magnitude; authored `description` keeps flavour only |

**Hooks.** The `HasRelic` call sites (about 35, in 10 `FightSession` partials) fall into about
13 hooks: attack landed, cast landed, kill, damage taken, fatal damage, fight
start, turn open, after action, move, enemy summon, transform enter, reach
check, potency. The catalog names each kind's hook; a test asserts every
non-stat kind is read somewhere.

**The seam.** Every relic check already runs through one private method,
`FightSession.HasRelic(actor, effect)` (`FightSession.Riders.cs:358`), which
asks `CombatantKit.HasRelic` (`CombatantKit.cs:162`). That method becomes
`actor.Effects.Has(kind)`; callers change by rename, not redesign.

**Stacking** (catalog field, one rule per kind class):
- Stat kinds (attack, defense, health, mana, speed, resist, lifesteal,
  crit): summed across holders, percents before flats
  (`RelicModifier.cs:239-286`). Affixes of one kind on one wearer count once,
  at their best copy (`ModifierEffectSet.cs:39`). Rolled repeats are luck,
  chosen ones are not. This is today's behaviour, stated as one rule.
- Mechanic kinds: a flag; a second copy on one actor does nothing, which is
  why 1.3 never rolls a duplicate.

**Holders.** Each holder is a list of `Effect` plus a scope:

| Holder | Scope | Scaling |
|---|---|---|
| Relic | party; one member if `bearer` (`CombatantKit.cs:148-158`) | none |
| Affix | wearer | `base × 1.25^tier` for scaling kinds |
| Trait (necklace affix) | wearer | per kind |
| Talent (I7) | owner | none |
| Set bonus (deferred, #125) | wearer with N pieces of one `setId` | none |

The set bonus is named so its home exists; nothing is built (#125).

**Assembly.** `FightEncounterAdapter` builds each combatant's `EffectSet`
once per fight from relics that reach them (`:485-493`), worn affixes and
trait (`ContentDatabase.ModifierEffects`, `:209`), and, after I7, talents.
Stat kinds fold into the combatant's stats where `RelicModifiers.Apply`
runs today (`:215-253`).

### 2.2 One offer pipeline

```
Source row (rewards.json) + context (step, node, class, squad, held)
  → Candidates(kind) → Recipient policy → Slot policy → RollOne (quality, affixes/trait)
  → Floors → Offer[]  (pure Domain, Func<int,int> rng)
  → Grant(offer) or Skip(offers)  (Core: equip, bag, relicIds, books, gold)
```

- One `Offer` record: `Kind` (Gear, Relic, Book, Consumable, Gold),
  payload id, `ItemInstance` for gear, recipient id or none, price or none.
  `ShopEntryKind` (`ShopStock.cs:16`) maps onto `Kind`; the persisted int
  stays (appended-only).
- One sampler: `SamplingOps`. The five hand-rolled copies (C3) go.
- `ItemOfferRoll.RollOne` (`ItemOfferRoll.cs:230`) stays the one
  "roll one copy" for fights, shop and shelf.
- Relic offers reuse `RelicPool.Available` and its rarity weights; `RelicPool`
  keeps the pool rule and loses its own draw loops.
- Shop and shelf move onto the pipeline with `recipients: none`, so they keep
  their behaviour: no auto-equip, the floors, the keyed section streams.

### 2.3 Gear copies: base + tier on the copy

Today tier is baked into 638 generated assets (11 per base,
`itemsets.json` `maxTier`), so tiers 4-10 are content few runs reach (D6),
and the endless run outgrows tier 10 at step 160 (realm ~21).

- A gear definition becomes one **base**: material × piece (55), weapon
  family (3), necklace bases (new). `GearScaling` computes stats from base +
  tier at read time; the budget formula is unchanged.
- `ItemInstance` = `ItemId` (base) + `Tier` + `Plus` + `ModifierIds` (affixes
  or the trait) + `Provenance`. `RiftTier` goes.
- Art and tier adjectives index by `min(tier, maxTier)`; past the top frame
  the number still climbs (Q9).
- Every base is offerable at every depth: no unreachable content.

### 2.4 Content JSON

Fields go in `docs/CONTENT_SCHEMA.md` when built (one home); named here:

| File | Change |
|---|---|
| `relics.json` | `effect` + `modifiers[]` → `effects[]` of `{type, magnitude, threshold, damageType}`; `bearer`, `draftable`, `rarity`, ids unchanged |
| `modifiers.json` | keep `effects[]`; add `pool` (`weapon`, `armour`, `necklace`), replacing the offensive/defensive sets hard-coded in `ModifierTable`; `necklace` rows are the traits |
| `itemsets.json` | `maxTier` now means art frames; one asset per piece; add a one-piece `amulet` set for `Necklace` |
| `weapons.json` | one asset per family |
| `rewards.json` (new) | one row per source: `id`, `kind`, `count`, `tierOffset`, `qualityClass`, `recipients` (`perMember`/`none`), `guaranteeSlot`, `skippable`, `skipPays` (`salePrice`/`none`), `floorNoBare`, `floorCheapestAtMost`, `relicRarityMin`, `stream`, `sortOrder` |
| `reward_tracks.json` / talents | I7 only: talent payloads use `EffectKind` names |

Prize kinds: the run map's enum gains `Relic`; `Item` and `Book` lose weight 0.
Anchor prize weights are a second weight list on `RealmProfile`.

### 2.5 Save data

- `ItemInstance` serialized fields (`InventoryEntry`, `EquipmentSlotEntry`):
  add `tier`; drop `riftTier`. Item ids change to base ids.
- `RunSnapshot`: add `necklacePity` (int); drop `offerRerollsUsed`.
- **No version bump.** Gear and bag clear at run end (`RunManager.cs:279-289`)
  and a run found at startup is settled (`RunManager.cs:65-80`), so no saved
  item outlives a session; unknown item ids are pruned by `Reconcile`
  (`SaveData.cs:753`); removed fields need no bump (`SaveData.cs:39-40`).
  Relic and modifier ids are kept (`.claude/rules/content.md`). The run map
  takes v9 (`PLAN_RUN_MAP.md:382`); this plan takes no number.

## 3. Owner questions

Unanswered ones go ahead on the recommendation.

1. **Gear only from fights whose map chip says Gear (and anchors, bosses),
   instead of after every fight?** Rec: yes. Fewer picks, each one you chose
   to go after.
2. **Per-character or squad-wide fight reward?** (the theory report's open
   question). Options: (a) three cards, one made for each member, you take
   one; (b) three cards for the squad, you choose who wears it; (c) each
   fight's reward belongs to one member in turn. Rec: (a). The "on whom"
   choice is visible and the cards always fit.
3. **Build-changing effects on gear only through the necklace (one per
   character), with relics staying party-wide and anchors and bosses also
   paying relic picks?** Rec: yes.
4. **No random "+" on loot; raise it by honing at the shop or at a Rest
   instead of healing, max +5?** Rec: yes.
5. **Skipping a gear pick pays the cheapest card's sale price?** Rec: yes.
6. **All three cards the same tier, with a boss one tier above (instead of
   today's fixed floor of tier 3)?** Rec: yes. At realm 1 the floor gives a
   piece 3 tiers (×1.95 budget) above anything else, and past realm 7 it does
   nothing.
7. **Favor: keep it as the luck stat that improves affix and necklace odds,
   shown as a number on the gear pick, and drop the `Fortunate` affix?** Rec:
   yes. Favor also feeds events and the track; removing it outright strands
   Shawn's authored 4.
8. **Spell books only as a visible map prize (and the shop), no hidden
   per-fight chance?** Rec: yes; "rewards shown before choosing"
   (`DIRECTION_BIBLE.md:53`). Book pace is set by the chip weight in I6.
9. **Past the top gear tier (realm ~21), does gear keep climbing as a number
   with the top art?** Rec: yes; enemies keep scaling forever.
10. **Necklace art:** placeholder icons until commissioned? Rec: yes; one
    icon sheet per necklace base, through the stills pipeline.

## 4. Phases

Agents per CLAUDE.md routing. Each phase ends green and is committed by the
orchestrator. `-Changed` = `tools/run_tests_parallel.ps1 -Changed`.

| # | Work | Needs | Agent | Gate |
|---|---|---|---|---|
| I0 | `RngStreams.Reward` keyed on `(step, nodeId)` for today's offers; held relics on the draft (#241); stale text: C5, `RelicEffect.cs:8` ("FightController"), `ModifierEffectSet.cs` header ("nothing populates it"); dead branch `RunOrchestrator.cs:813-817` | none | implementer | `tools/test.ps1 rng`, `run`; then `-Changed -BuildScenes` |
| I1 | `EffectKind`, `EffectCatalog`, `EffectSet`; relics and affixes onto it; `HasRelic` seam swapped; relic headline numbers move from `FightTuning` into `relics.json`. **No behaviour change**: existing relic and modifier tests pass unedited except type renames. | none | senior, `Escalation: architecture` | `tools/test.ps1 combat`, `content`; `-Changed -BuildContent` (promotes to full); bot batch on fixed `-Seed`/`-Runs` matching a pre-I1 baseline within noise |
| I2 | Base + tier copies; `rewards.json`; the pipeline; per-member cards, slot policy, quality = colour, skip, pity, take-equips; shop and shelf on the pipeline; Reckoning cards; `GearEvaluator` scores affixes, traits and recipient (`GearEvaluator.cs:146` reads id + plus only) | Q2, Q5, Q6, Q7 | senior, `Escalation: cross-layer` (ItemInstance shape spans ContentBuilder, Domain roll, Core settlement, the save and `ReckoningScreen`) | `tools/test.ps1 run`, `content`, `ui`; `-Changed -BuildContent -BuildScenes`; diff `Art/` |
| I3 | Who pays what: Gear/Book/Relic prize kinds and anchor weights on the run map's chip; boss gear + relic pick; per-fight gear pick and hidden book roll removed | Q1, Q3, Q8; run map P2 | implementer | `-Changed -BuildScenes` |
| I4 | Necklaces: amulet set, trait pool (`pool: necklace`), affix cut to 15, generated rules text | Q3, Q7, Q10 | implementer | `tools/test.ps1 content`; `-Changed -BuildContent` |
| I5 | Honing: shop service, Rest choice, `MaxPlus` 5 | Q4 | implementer | `-Changed -BuildScenes` |
| I6 | Balance from bot batches: slot weights, pity step, hone prices, chip and anchor weights, `MaxStep` (#85), shop floor (#196), past-top-tier curve | I2-I5 | implementer, then verifier | bot report to the owner (not a gate); literals pinned |
| I7 | Talents onto `EffectKind`; the three duplicate pairs merged | I1 | implementer | `tools/test.ps1 combat`, `content`; `-Changed -BuildContent` |

**Order against the run map.**
- I0 and I1 need no answers and can run beside run map P1-P2. I1 touches
  `FightSession` partials and `FightEncounterAdapter`; run map P2 does not.
- I1 must land **before run map P5** (anchors at the heart), which reworks
  `FightSession` enemy lists and kill credit in the same partials.
- I2 can run beside run map P2: it touches the Reckoning and `ItemInstance`,
  not the map or step contract.
- I3 lands **after run map P2** (chip and payout) and **before run map P3**,
  so the P3 bot report measures the real prize mix.
- I6 runs after run map P3, on the same bot.
- The Rest choice (I5) waits for run map P2, which moves Rest placement.

## 5. Risks

- **I1 breadth.** About 35 call sites in 10 partials, plus the adapter. The seam
  keeps it a rename, but moving numbers from `FightTuning` into JSON can
  change a value silently; I1 pins each moved number as a literal in a
  content test before deleting the constant.
- **Stacking rule.** "Best affix, summed with everything else" must match
  today's mixed sums (`FightSession.cs:984`). An I1 test pins a character
  with two lifesteal affixes and Vampire Dentures.
- **Tests that pin tables** (C7: `ShopStock` 18 files, `ItemOfferRoll` 9,
  `RarityTable` 9, ...). I2 rewrites them with literal expectations
  (gotcha 5), not recomputed ones.
- **Fewer gear picks** under Q1 means less gear power per realm, while gear
  carries late power (`OWNER_DECISIONS_2026-09-26.md:141`). The Gear chip
  weight is the dial; I6 checks the curve.
- **Relic inflation** at depth: anchors grow to 6 per realm. The anchor
  Relic weight and pool exhaustion (1.1) bound it; I6 reports relics held
  by floor 10, 20 and 30.
- **Bot blindness** until I2 extends `GearEvaluator`; bot numbers about gear
  mean nothing before then.
- **Necklace art** does not exist; placeholders ship first (Q10).
- **Shop drift.** Moving shop and shelf onto the pipeline can change stock on
  the same seeds; I2 keeps the section streams and pins a seed's stock
  before and after.

### Expected theory-test score

| # | Now | Plan | Why |
|---|---|---|---|
| T1 | 0 | 2 | skip pays the sale price |
| T2 | 0 | 1 | cards differ by recipient and slot; only a necklace differs in plan |
| T3 | 0 | 2 | taking costs the skip gold and the other two members' upgrade |
| T4 | 0 | 2 | same tier, colour ranks quality, delta on the face |
| T5 | 0 | 2 | one delta number, one named line |
| T6 | 2 | 2 | rolled on settlement, shown before choosing; chip shown before the node |
| T7 | 1 | 1 | skip payout and honing, but no reroll on a fight pick |
| T8 | 0 | 2 | necklace pity, keyed stream, saved counter |
| T9 | 0 | 2 | gold and consumable chips auto-pay; gear only on chosen chips; relics and necklaces are the rare rewards |
| T10 | 1 | 2 | anchor/boss relic picks and necklace traits |
| T11 | 0 | 2 | every base offerable at every depth |
| T12 | 1 | 2 | per-member cards, requirements checked |
| T13 | 0 | 2 | recipient on the card, equipped on take |
| T14 | 1 | 2 | skip pays, honing sinks gold, sell stays |
| T15 | 1 | 2 | normal chip vs anchor relic/gear vs boss tier +1, weapon and relic |
| | **11** | **28** | |

## 6. Rejected alternatives

- **Effects authored as trigger/condition/outcome JSON:** a scripting
  language with its own test burden; the mechanics share no shape
  (`RelicEffect.cs:3-10`).
- **Event bus with registered handlers per kind:** handlers need
  `FightSession` internals anyway; a query at the existing hook is the same
  behaviour with less indirection.
- **Traits on Legendary armour too** (`DESIGN.md` 1.3): uncapped traits per
  wearer, duplicates to police, and relics stop being special.
- **Guaranteed relic per elite** (`DESIGN.md` 1.1): up to 6 anchors a realm.
- **Gear pick after every fight, made skippable:** keeps the modal cost on
  every fight (T9) and ignores the chip the run map already shows.
- **Fight-pick reroll:** turns the pick into a search; skip already gives an
  out.
- **Bag capacity:** UI and a new "drop" verb for a problem skip and sell
  already solve.
- **Keep tier per asset and add tiers 11+:** more assets for the same
  formula; the endless run still hits a wall.
- **Merge item and relic rarity ladders:** one is a rolled quality, the other
  an authored power statement; they share the palette, not the enum.
- **Drop Favor entirely:** strands Shawn's base Favor and the events and
  track that grant it.
