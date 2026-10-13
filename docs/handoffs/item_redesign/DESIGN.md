# Item redesign: design, rev 1 (draft, nothing built)

Input: the audit in `README.md` (problems D1-D10, C1-C7). Coupled to the run
map plan (`docs/PLAN_RUN_MAP.md` rev 4), which reserves an **Item** prize kind
on fight nodes "waiting on the item redesign" (:96-102) and is parked until
this plan settles. Section 1 is the proposed behaviour; section 2 is what
the owner decides; sections 3-4 are order and gates.

## 0. The two facts the design is built on

1. **The run never ends by design.** Difficulty rises 8 steps per realm
   forever, floor 100 included (`PLAN_RUN_MAP.md:174,205`). So "size the
   catalogue to the run" (D6) cannot mean a fraction of a run: it means the
   tier a step maps to, with the top tiers as a deep-run goal.
2. **Every fight node already shows a prize chip before you enter it**
   (run map §3, "rewards shown before choosing"). Gear can be one of those
   prizes instead of a pick that follows every fight. That one move answers
   D1 (forced, repeated, junk) and the [You] rule at `DIRECTION_BIBLE.md:53`.

## 0b. How other roguelikes pay out (and what this plan takes)

From general knowledge of the games, not a measured survey.

| Game | Pattern | Taken here |
|---|---|---|
| Hades | Each door shows its reward before you enter (boon, Pom, Hammer, gold). Boons are 1 of 3, colour = rarity of the same boon. Poms are a separate "level it up" reward. | Gear as a map prize shown in advance (1.1); colour = quality of this copy (1.2) |
| Slay the Spire | Normal fight: 1 of 3 cards, **always skippable**. Elites guarantee a relic; bosses offer 1 of 3 boss relics. Rarity has a pity offset that climbs each time you see commons. Upgrading is a campfire choice that **competes with healing**. | Skip (1.2); elite/boss relic picks (1.1); pity on Legendary (1.2); hone at Rest as an alternative to the heal (1.4) |
| Darkest Dungeon, Wildfrost | Trinkets and charms go on **one named hero**; some are class-locked. | One card per party member, made for them (1.2) |
| Risk of Rain 2, Balatro | Surplus converts into something useful (scrap → printers); skipping a blind pays a tag, so passing is a real option. | Skip pays (1.2); open question whether gold is enough (Q6) |
| Diablo-likes | Many power axes (item level, rarity, affixes, sockets), saved by upgrade arrows and loot filters. | One visible rank plus the recipient's delta on the card face (1.2) |
| Dead Cells | Item level follows the biome; raw power lives on a separate axis (scrolls); items carry identity. | Tier from depth only (1.2); identity from traits (1.3) |

Where the plan departs from them: Slay the Spire pays a pick after every
fight. This plan pays gear only where the map promised it (Hades). The
reason is that our picks are heavier (slot, recipient, comparison), and
the run map already turns rewards into a routing choice.

## 1. Behavioural contracts (proposed)

### 1.1 What a fight pays

| Fight | Always | Prize (shown on the map) |
|---|---|---|
| Normal | exp, gold, spell-book chance (as today) | its chip: gold, consumable, **or Gear** |
| Elite | as normal | Gear **and** a relic pick |
| Boss | as normal | Gear with a weapon guaranteed among the offers, **and** a relic pick from rare and up |

- The gear pick happens **only** where the chip or fight class says Gear.
  The Reckoning gear pick after every fight goes away.
- A relic pick is 1 of 3 from `RelicPool.Available`, skippable, showing held
  relics (#241). This is the "elite/boss relic drops" shape already stated
  (`PLAN_SHOP.md:323-345`).
- `GuaranteesWeapon` gets its live caller (boss); `RollConsumableDrops` gets
  one through the run map's consumable chip. Nothing dead stays dead (D7).

### 1.2 A gear pick

- **One offer per fielded squad member, made for that member.** The card
  shows whose it is. Its slot is drawn from that member's slots, weighted
  toward empty slots and then the weakest by comparison score; it never
  rolls an item whose requirements that member fails. With fewer than 3
  members, the extra cards go to the least-geared member. A member with an
  empty weapon slot gets a weapon. (D3, D8)
- **Take equips it on that member.** Whatever it replaces goes to the bag.
  The dossier can still move it. Roster-order auto-equip is removed.
- **Skip** pays gold (a fixed value per tier, below a sale of the cheapest
  offer). Skipping only pays out, so it does not confirm (#225 proposal).
- **Same tier for every offer in a pick:** tier comes from the step alone
  (`step / StepsPerTier`, plus the elite/boss floors). The tier ladder and
  the ±1 spread on drops are removed; the card states the tier.
- **Rarity is the quality of this copy, and it is the one thing the colour
  shows:** Common = no affix; Uncommon = 1; Rare = 2; Epic = 3; Legendary =
  carries a trait (1.3). The `LootLadder` that rolls it is kept. Since the
  tier is shared, colour alone ranks the three cards (D4).
- **The card face shows the recipient's change**, as the one or two largest
  stat deltas against what they wear now. The tooltip keeps the full list.
- **Pity:** each gear pick with no Legendary card raises the next pick's
  Legendary chance by a step, and a Legendary card resets it. The counter
  is saved per run.
- Rolled on a keyed stream per (step, node), like the spell drop. Quitting
  does not reroll (C4).

### 1.3 Traits: gear that changes how you play

- A **trait** is a build-changing effect (the kind relics have: double strike,
  free attack after a spell, ...) on a single gear copy. It works only on
  whoever wears it; relics stay party-wide.
- Traits appear only on Legendary copies and on every **necklace**. The
  necklace becomes the trait slot, so the empty slot gets a role (D2).
- Traits, affixes and relics draw from **one effect vocabulary** (C2). An
  effect is data: trigger, condition, outcome. A relic is a party-wide
  holder of effects, a trait is a holder on one wearer, and an affix is a
  holder with a magnitude scaled by tier. The `HasRelic(..., RelicEffect.X)`
  branches become per-trigger hooks that ask "which effects does this actor
  hold". This is the architecture piece (P1).

### 1.4 Plus becomes a verb

- Drops are always +0. The plus roll goes, and so do Favor's plus odds.
- **Honing** (the verb the glossary already promises,
  `GlossaryEntries.cs:223`) happens in two places:
  - at the shop, paying gold, with the price rising per plus;
  - at a Rest node, **instead of** its heal. One copy gets +1 for free.
    This is Slay the Spire's campfire choice.
- Max +5 (today +10, never reached by a choice). This gives surplus gold
  a use (D5).
- Selling at 30% stays.

### 1.5 Removed or cut back

- The Reckoning gear pick after every fight; the tier ladder and spread on
  drops; the plus roll; roster-order auto-equip.
- `Fortunate` affix (invisible luck loop, D10), unless the owner keeps it
  (Q8).
- Affix pool: the 12 element mirrors shrink to 2 entries (one "+X% of your
  element" rider and one resist), whose element follows the item's
  material. 25 → 15 distinct affixes, each one a separate idea.
- Stale text listed in C5.

### 1.6 Unchanged

Everything in the audit's "Keep" (e), the per-run wipe, relic draft at the
hub gate, shop stock and floors, `ItemInstance.Provenance` (C6 is low and
out of scope), item sets with no bonus (#125 stays deferred; the effect
vocabulary is where a set bonus would later live).

## 2. Owner questions

Plain language, each with what I'd pick. Answer any number; unanswered ones
go ahead on the recommendation.

1. **Gear only from fights that show a Gear prize (plus elites and bosses),
   not after every fight?** Rec: yes. Fewer, better picks that you chose to
   go after.
2. **One card per party member, made for them, put straight on them when
   you take it?** Rec: yes.
3. **Should some gear change how you play (traits), or keep that for relics
   and just hand out relics more often?** Rec: both. Traits only on
   legendary gear and necklaces; elites and bosses give a relic pick. Risk:
   relics feel less rare, and effects get two homes to balance.
4. **Necklace: the trait slot, or delete the slot?** Rec: trait slot.
5. **No random "+" on loot; you raise it by honing at the shop for gold,
   or at a Rest instead of healing?** Rec: yes, max +5.
6. **Skipping a gear pick pays: gold, a potion, or nothing?** Rec: gold.
7. **All three cards the same tier, with luck going into affixes and traits
   instead of a lucky higher tier?** Rec: yes. Cost: the "jackpot tier"
   moment is gone.
8. **Keep the `Fortunate` affix (better drops), now shown as a number on
   the card, or drop it?** Rec: drop.
9. **How deep should the top gear tier sit?** Today tier 10 is step 160,
   about realm 20. Rec: leave it; deep runs are what the endless map is for.

## 3. Phases

Each phase ends with its gate green and a commit. Agents per CLAUDE.md
routing.

| # | What | Needs answers | Owner (agent) | Gate |
|---|---|---|---|---|
| P0 | Independent fixes: fight offers on a keyed stream; delete stale text (C5) and the dead `AutoEquip` branch; draft shows held relics (#241) | none | implementer | `test.ps1 -Changed`, then `run_tests_parallel.ps1 -Changed` |
| P1 | One effect vocabulary: affixes and relics move onto it with **no behaviour change**; `FightSession` relic branches become trigger hooks | none | senior, `Escalation: architecture` | full run; bot batch (200 runs) within noise of a pre-P1 baseline |
| P2 | Gear pick model: per-member offers, take equips the recipient, skip, shared tier, rarity = affixes/trait, plus never rolled | Q2, Q6, Q7 | senior, `Escalation: cross-layer` (Domain roll + Core settlement + Reckoning screen tree) | `-Changed -BuildScenes`; bot `GearEvaluator` reads affixes first (C7) |
| P3 | Who pays what: gear only on Gear prizes, elite/boss relic picks, boss weapon | Q1, Q3 | implementer, after run map P2 lands the prize chip | `-Changed -BuildScenes` |
| P4 | Traits content and the necklace slot; affix pool cut to 15 | Q3, Q4, Q8 | implementer | `-Changed -BuildContent` |
| P5 | Honing at the shop | Q5 | implementer | `-Changed -BuildScenes` |
| P6 | Balance: `MaxStep` (#85), shop floor (#196), skip value, hone prices, from a bot batch | P2-P5 | implementer | bot report to the owner (not a gate) |

**Order with the run map.** P0 and P1 need no answers and touch nothing the
run map touches. Run map P1 and P2 can run alongside them. Item P3 comes
after run map P2, which builds the prize chip. P2 here changes the
Reckoning screen, which the run map plan does not touch.

## 4. Risks

- **P1 is the large one.** 34 relic branches across about 10 `FightSession`
  partials. It has to be behaviour-preserving, proved by the existing relic
  tests plus a bot baseline, before any new effect is authored on top.
- **Tests that pin today's tables** (C7: about 80 files): P2 rewrites many.
  New expected values are pinned literals (gotcha 5).
- **Bot blindness:** until `GearEvaluator` values affixes and traits, bot
  numbers say nothing about P2-P4. That has to be fixed in P2, not P6.
- **Fewer gear picks (Q1)** means less gear power per realm. Gear is
  expected to carry late power (`OWNER_DECISIONS_2026-09-26.md:141`), so P6
  has to check the curve; a Gear chip weight is the dial.
