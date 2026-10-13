Written by Claude (research subagent, Sonnet 5.5), 2026-10-13. Read-only research for the item redesign; nothing built.

**Confidence key.** Numbers marked [V] were read from a cited page this session. Items marked [M] are from memory or a search summary and are unverified; check before using them as a spec. Several wikis (Fandom) returned HTTP 402/403, so some per-game detail is thin. Gaps are stated, not filled.

Project baseline (from `README.md`, the audit): fights give a forced 3-card gear pick with no skip/reroll, 69% of cards bare; items are stat sticks with no identity; relics hold all build-defining mechanics but arrive once per run or by shop; offers ignore who can wear them; plus is unowned luck with no sink; no inventory pressure; the Necklace slot has no content.

---

## 1. Per-game notes

### Slay the Spire
- **Moments:** card reward after each fight; relic from elites/bosses/chests/shops; campfire (rest vs smith); shop. 
- **Choice shape:** pick 1 of 3 cards or skip [V, Card Rewards page lists 3 distinct cards; skip is standard]. Relics from elites are forced single drops; boss gives pick 1 of 3 [M].
- **Randomness/pity:** per-card rarity roll: normal 3% rare / 37% uncommon / 60% common; elite 10/40/50; shop 9/37/54 [V]. Hidden pity offset: starts at -5%, +1% per common rolled, resets on a rare, caps at +40% [V]. First floor of each act can never have a rare [V].
- **Power axes:** card rarity, upgraded or not. One axis each, readable.
- **Surplus/sink:** deck bloat is the sink. Skipping, removal at shops, and Singing Bowl (+2 max HP instead of a card, [V] for effect) turn "nothing good" into a real option.
- **Build identity:** cards define the build; relics modify it. Campfire is the upgrade-as-choice: Rest heals 30% max HP, Smith upgrades a card [V]; relics add or lock options (Girya lift, Peace Pipe toke, Shovel dig; Dream Catcher adds a card reward on rest) [V].
- **Lesson:** a modest choice every fight works because skip has value and the upgrade costs real HP-economy (rest vs smith).

### Hades / Hades II
- **Moments:** every cleared chamber shows its reward on the door before you enter (boon god, Pom, gold, hammer, etc.) [M, well known; Fandom boons page confirms door marks and skull rooms for boosted rarity [V]]. 
- **Choice shape:** pick 1 of up to 3 boons from the god you chose by door. Rerolls are scarce and meta-unlocked (Fated Authority rerolls Infernal Gate rewards [V]; standard offer rerolls not confirmed).
- **Randomness/pity:** four rarity tiers Common/Rare/Epic/Heroic; rarity and level are separate axes [V]. Skull-marked rooms, elite and mini-boss rewards raise rarity odds [V]. Charon's 450-obol god boons use boosted odds: 65% Rare, 25% Epic, 10% Legendary [V].
- **Power axes:** god/slot identity, rarity, level (Pom of Power: +1 level to one boon you pick; +2 from Gates/elites/Charon; Duo/Legendary cannot level) [V]. Hades II Sacrifice swaps a core boon and bumps rarity one tier, keeping level [V].
- **Surplus/sink:** Pom of Power and keepsakes; no inventory.
- **Build identity:** boon-per-slot (attack, special, cast, dash) plus duo/legendary boons that need prerequisites. The identity is combinatorial but each slot is legible.
- **Lesson:** reward-preview-on-door turns every route into a build decision; rarity boosted by room difficulty gives risk a visible payout.

### Balatro
- **Moments:** shop between blinds; booster packs; skip tags when you skip a blind [M, Fandom page 402].
- **Choice shape:** shop shows 2 cards + 2 booster packs + 1 voucher; reroll starts $5, +$1 per reroll, resets each shop [V]. Packs cost $4/$6/$8; first shop guarantees a Buffoon pack [V]. Sell value = floor(cost/2), min $1 [V].
- **Randomness:** joker rarity Common 70 / Uncommon 25 / Rare 5 [V].
- **Power axes:** joker rarity is mostly identity, not magnitude; scaling comes from joker interactions, editions, and card enhancements.
- **Surplus/sink:** interest on money creates a saving-vs-buying tension; slot cap on jokers (5, [M]) is the hard sink; selling at half.
- **Skip economy:** skipping a blind forfeits its reward and gives a tag (free pack, reroll, etc.) [M].
- **Lesson:** a hard slot cap plus an escalating, resetting reroll makes every purchase a trade-off.

### Monster Train
- **Moments:** battle rewards (card draft), merchants for artifacts/upgrades, event artifact picks, boss artifact choices [M]. Upgrade paths between battles: one path upgrades spells/heals/purges 2 cards, another upgrades units/gold/duplicates a card (MT2, search summary) [V-summary].
- **Choice shape:** draft pick 1 of 3 with skip [M]. Upgrades are slot-based: units take limited upgrade slots [M].
- **Build identity:** clan pair plus artifacts (88 artifacts, SfS-relic-like, [V-summary]). 
- **Lesson:** upgrade-slots on the unit (not floating loot) give the "per-character gear" feel with a hard cap; gold is turned into a deliberate choice (upgrade vs relic vs purge).

### Into the Breach
- **Moments:** mission objectives pay reputation, grid power, or Reactor Cores; island clear gives a bonus pick of 1 item, 1 pilot, or +2 grid; Time Pods give a core plus an item/pilot [V-summary]. Rewards are visible on the map before you pick the mission [M].
- **Choice shape:** mission choice shows reward; island shop spends reputation (1 rep = 1 grid; 3 rep = 1 core; weapons on sale at half price, resellable) [V-summary].
- **Randomness:** low; the island shop is the main variance.
- **Power axes:** cores (squad-wide upgrade points), weapon upgrade slots, pilots (character-bound perks).
- **Lesson:** pre-announced rewards plus a scarce shared currency make every mission a risk/reward decision; character gear (pilots) is a deliberate, small catalogue.

### Darkest Dungeon (party with per-character gear)
- **Moments:** trinkets as dungeon loot and quest rewards; Nomad Wagon sells them weekly; boss expeditions always yield a Very Rare trinket [V].
- **Gear shape:** each hero has 2 trinket slots, inventory effectively uncapped (999) [V]; Common up to Ancestral rarity tied to dungeon length/difficulty [V]; generic vs hero-specific trinkets [V]. Wagon stock grows 4/6/8/12 via upgrades [V]. Ancestral/enemy trinkets are unique [V]. Weapon/armour progression is separate (blacksmith levels) [M].
- **Build identity:** trinkets are the build layer (stress, crit, party-wide effects), plus trait/quirk systems.
- **Surplus/sink:** gold from selling, but trinket prices (1125 to 7500 for Common-Ancestral, [V]) make buying the sink.
- **Lesson:** a small slot count with strong, readable effects, plus class-restricted trinkets, makes gear a party-composition decision.

### Risk of Rain 2
- **Moments:** chests on each stage (gold-cost, scales by stage), teleporter boss reward, Lunar coins for lunar items [V].
- **Choice shape:** mostly no choice (a chest gives its item); Category/Adaptive chests and 3D printers/scrappers add control [M, not verified: search pages did not cover printer/scrapper]. Chests cost gold that scales with time/stage [M].
- **Randomness:** Small Chest 79.2% white / 19.8% green / 0.99% red; Large Chest 0% / 80% / 20% [V]. Rarity is the only quality axis; duplicates stack.
- **Surplus/sink:** stacking is the sink (more of the same = linear or hyperbolic scaling). No slots.
- **Build identity:** emergent from stacking; items are small mechanical modifiers.
- **Lesson:** items that stack and each have a legible "one-line" effect make an endless run work without a slot cap; the endless difficulty is escalated by time.

### Dead Cells
- **Moments:** weapons/skills from floor drops and shops; Scrolls of Power (Brutality/Tactics/Survival) at fixed points; cursed chests, forge upgrades [V-summary, thin].
- **Choice shape:** each scroll is a choice among 3 stats; colour-coded items scale with the matching stat [V-summary]. Mutations pick 1 of 3 [M].
- **Power axes:** weapon tier plus affixes, stat colour scaling, level (the numbers vary across sources: do not copy).
- **Lesson:** a 3-colour scaling axis (one stat investment decides which items are good) gives build identity with minimal item text.

### Diablo III Loot 2.0 (patch 2.0.1, Feb 2014)
- **What it fixed:** too much loot and useless drops; huge stat swings between similar items; class-inappropriate gear; legendaries not actually best [V: Engadget/Blizzard patch notes via search; Game-Wisdom].
- **How:** fewer drops overall; "smart drops" tailored to the finding character's class [V]; narrowed stat ranges (e.g. Strength 140-200 instead of a huge range) [V]; primary/secondary stat groups no longer compete [V]; legendaries buffed so they are mathematically best, plus more skill-specific affixes [V]. A fan-wiki 85% smart-loot rate is not from Blizzard: ignore [V, caveat].
- **Lesson:** reduce quantity, raise the floor, bias to the owner. The smart-loot design is exactly the fix for D3.

### Path of Exile and Diablo IV
- **PoE:** prefix/suffix affix system with random crafting currencies. Community complaints: stacked randomness, no in-game affix tables or weights, weak onboarding, outside tools required [V-summary, forum opinion, not Grinding Gear statements].
- **D4 Season 4 "Loot Reborn" (patch 1.4.0, May 2024):** affixes cut to 3 on Legendary and 2 on Rare, each stronger; conditional affixes replaced by flat ones; many fewer item drops to cut sorting time; Tempering (blacksmith recipe adds a random affix), Masterworking (raise affixes; failure chance later removed), Greater Affixes (higher-roll affix on WT4 Ancestral drops), Codex of Power (aspects stored, not carried) [V].
- **Lesson:** after years of affix bloat both games shrank affix counts and moved agency into crafting verbs. Players who cannot read an item cannot choose between items.

### Backpack Battles
- **Moments:** shop every round; rarity rises by round [V-summary]. Reroll costs gold (1 gold in early rounds per a guide, unverified) [M]. Selling at half; 10% chance an item is on sale at half price [V-summary].
- **Gear shape:** a spatial bag: item adjacency/placement is the build.
- **Pools:** 14 Common, 11 Rare, 7 Epic, 6 Legendary, 7 Godly, 5 Unique; most are class-neutral, some class-specific [V-summary].
- **Lesson:** a small, readable pool with combos and a hard capacity (the bag) gives identity without affixes; capacity is the sink.

### Party RPG pick: Wildfrost
Picked over Pillars and Fear & Hunger because it is the closest small-scale analogue: a party of 3-ish cards whose individual power comes from attachable per-unit gear (charms), within a roguelike run. Per the Wildfrost wiki [V]: each card holds up to 3 charms; once attached a charm cannot be removed or undone; charms come from free dispensers (1 random), a Woolly Snail (up to 3 per visit), a merchant (3 charms for sale plus a card with one attached), and boss "Sun Vault" rewards; some charms are tribe-exclusive; charms do not carry across runs. Not confirmed: post-battle reward shape.
- **Lesson:** irreversible attach plus a hard slot cap makes each gear decision a commitment; "which unit gets it" is half the choice.

---

## 2. Ranked patterns (best fit first)

Fit = party of up to 3, turn-based, endless difficulty, per-character gear, relics party-wide, map nodes already preview rewards (`DIRECTION_BIBLE.md:53`).

| # | Pattern | Games | Fit for Prince's Palace | Cost/risk |
|---|---|---|---|---|
| 1 | **Reward preview on the map** (node shows reward type/tier before you enter) | Hades doors [V]; Into the Breach missions [V-summary]; owner's own bible | Already owner-stated and not done for loot; turns routing into a build decision and lets elite/boss visibly pay more | Reward must be fixed or keyed at map generation (not rolled after the fight) to avoid lying; change to `RollOffers`, which uses unkeyed `UnityEngine.Random` (audit C4). Spoils surprise. |
| 2 | **Smart loot: bias the roll to who can use it** | D3 Loot 2.0 [V]; Darkest Dungeon hero-specific trinkets [V] | Directly fixes D3 (offers nobody wears, weapons 5.2% of pool, Necklace empty). Cheap: a candidate-weight function over party slots/requirements | Too strong a bias removes surprise and makes every pick "the obvious upgrade"; must keep an off-class floor so new builds stay possible. Reuse `RequirementResolver`. |
| 3 | **Fewer, better rewards** (cut bare drops; raise floor) | D3 Loot 2.0 [V]; D4 Loot Reborn [V] | Fixes D1 (69% of cards bare). A fight reward that is rarely junk is the whole redesign in one line | Fewer rewards may starve a 3-character party of upgrades; pair with pattern 6. Needs bot re-tune. |
| 4 | **Skip with value** (skip = gold/heal/progress, not nothing) | StS skip + Singing Bowl [V]; Balatro skip tags [M]; ItB rep conversion [V-summary] | A pick-1-of-3 with no decline is the audit's D1/D5. Cheap to add; also gives surplus a use | Must pay less than a good pick but more than a junk one; skip-bias exploits are easy (always skip). Confirmation rule (AUDIT #225) applies. |
| 5 | **Item vs relic split of build identity** (items = numbers, relics = rules; or reverse) | StS cards vs relics [V]; RoR2 stacking items [V]; DD trinkets [V] | Prince's Palace already has all 34 mechanics in relics. Cleanest model: gear = per-character stat/slot and a few capstone effects; relics = party-wide rules. The problem is that fights give only gear | Moving mechanics into gear splits the effect vocabulary again (audit C2). Letting fights drop relics (elite/boss) is the lower-cost route and matches `PLAN_SHOP F8`. |
| 6 | **Upgrade-as-a-choice** (rest node: heal vs hone/smith an item) | StS campfire [V]; Monster Train upgrade paths [V-summary] | Gives plus (D5) an owner and a sink; the Rest node already exists; a visible cost (not healing) | Rest becomes mandatory-tension; with an endless run, heal pressure must scale or the choice is trivial. Needs a "hone" verb the glossary already promises. |
| 7 | **Pity / hidden rarity offset** | StS +1%/common, -5% start, cap +40% [V]; Hades skull rooms [V] | Fixes dry streaks in a 40-step run; a quiet guarantee for "good pick by N fights" | Hidden rules are hard to test (never recompute production formula, gotcha 5); needs a persisted counter in the run snapshot; per-character vs party pity is a design question. |
| 8 | **Single power axis per item, readable on the card** | D3 narrowed ranges [V]; D4 fewer affixes [V]; PoE complaints [V-summary]; StS one rarity axis | Fixes D4 (five axes, tooltip needed). Collapse tier/plus/rift to one visible grade plus 0-2 named effects | Large migration (tests pin tier/plus/rift, audit C7); loses some jackpot variance. |
| 9 | **Hard slot caps and commitment** (few slots, irreversible or costly swap) | Wildfrost 3 charms, no removal [V]; DD 2 trinkets [V]; Balatro joker cap [M]; Monster Train unit slots [V-summary] | Per-character slots exist already (8). The missing piece is a reason to say no: a bag cap or sell/swap cost makes surplus matter (D5) | Irreversibility conflicts with auto-equip (D8) unless the player assigns; a bag cap needs UI and a fix for current uncapped stash. |
| 10 | **Reroll economy with escalating, resetting cost** | Balatro $5 +$1, reset per shop [V]; shop already has 15 x 2^n | Already in the shop; could extend to the fight reward as a limited free reroll (one per node/run, relic-modifiable) | Free rerolls dilute the 1-of-3 decision; keep scarce. Cheap to build. |
| 11 | **Boosted rewards for risk** (skull/elite rooms pay a higher rarity band) | Hades skull rooms and mini-bosses [V]; DD boss always Very Rare [V] | Exists in part (boss tier floor 3); making the elite/boss uplift visible in the map preview ties to pattern 1 | Risk of a "always take elite" meta; endless difficulty must scale elite risk too. |
| 12 | **Identity from a stat/colour axis, not item text** | Dead Cells scroll colours [V-summary]; Hades slots [V] | Cheap identity for generated gear: tag items by an axis (physical/arcane/etc.) and let a relic or reward-track amplify it | Overlaps the existing elemental modifiers (12 of 25 are element mirrors); needs a clear single axis. |
| 13 | **Crafting verbs over rolled affixes** (temper/masterwork) | D4 Tempering/Masterworking [V]; PoE currencies [V-summary] | Gives agency instead of luck; fits "plus is pure luck". Risk of PoE-style opacity | Large new UI and economy; failure chances were removed in D4 because they frustrated players [V]. Probably later phase. |
| 14 | **Stacking items for endless runs** | RoR2 [V] | Endless difficulty needs a late-game power sink that does not bottleneck on slots; stacking relics/gear with diminishing returns could fit | Easy to break balance; the audit's D6 (tiers 4-10 unreachable) says content is already unused. Lower priority. |
| 15 | **Spatial/adjacency gear** (bag grid) | Backpack Battles [V-summary] | Strong identity but needs a new UI for 3 characters x 8 slots; poor fit | High cost; rejected unless the owner wants a new genre of item screen. |

---

## 3. Specific answers to the brief's questions

- **Smart loot:** worth doing, ranked #2. D3 shipped it as class-skewed rolls with narrower ranges, not as a guarantee [V]. For us: weight candidates by slot-need across the squad, with an off-slot floor.
- **Preview on map:** ranked #1; requires deterministic or pre-rolled rewards (audit C4).
- **Skip/reroll:** skip must carry a payout; reroll stays scarce. Both StS and Balatro cost something.
- **Upgrade-as-choice:** only works if rest has a cost that scales in an endless run (heal 30% is StS's number [V]; Prince's Palace needs its own).
- **Pity:** StS formula is the cleanest precedent [V]; a persisted counter and a bot test are required.
- **Item vs relic split:** the cheapest coherent model is gear = per-character numbers and a small set of capstones, relics = party rules, and fights (esp. elite/boss) drop relics. This also removes the `PLAN_SHOP F8` gap.

## 4. Gaps

Not verified this session: Monster Train reward details, Into the Breach exact rates, Dead Cells exact numbers, Hades base rarity table, Backpack Battles reroll/rarity-by-round, Wildfrost post-battle rewards, RoR2 printer/scrapper, PoE official statements. Fandom pages returned 402/403.

## Sources

- Slay the Spire Wiki, Card Rewards: https://slay-the-spire.fandom.com/wiki/Card_Rewards (via search summary)
- Slay the Spire Wiki, Cards: https://slaythespire.wiki.gg/wiki/Cards
- Slay the Spire Wiki, Rest Site: https://slaythespire.wiki.gg/wiki/Rest_Site
- Hades Wiki, Pom of Power: https://hades.fandom.com/wiki/Pom_of_Power ; Boons: https://hades.fandom.com/wiki/Boons ; Boons/Hades II: https://hades.fandom.com/wiki/Boons/Hades_II
- Balatro Wiki, Shop: https://balatrowiki.org/w/Shop
- Risk of Rain 2 Wiki, Chests: https://riskofrain2.wiki.gg/wiki/Chests ; Lunar Coins: https://riskofrain2.wiki.gg/wiki/Lunar_Coins
- Darkest Dungeon Wiki, Trinkets: https://darkestdungeon.wiki.gg/wiki/Trinkets ; Nomad Wagon: https://darkestdungeon.wiki.gg/wiki/Nomad_Wagon
- Wildfrost Wiki, Charms: https://wildfrostwiki.com/Charms
- Diablo Wiki, Loot 2.0: https://diablo.fandom.com/wiki/Loot_2.0 ; Shacknews, patch 2.0.1: https://www.shacknews.com/article/83266/diablo-3-patch-201-deploys-with-loot-20 ; Engadget, Reaper of Souls: https://www.engadget.com/2013-08-21-diablo-3-reaper-of-souls-expansion-revealed.html ; Game Wisdom, Loot 2.0 and Diablo's Fourth Revival: https://game-wisdom.com/critical/loot-2-0-and-diablos-fourth-revival
- Icy Veins, Diablo 4 Season 4 Loot Reborn patch notes: https://www.icy-veins.com/d4/news/diablo-4-season-4-loot-reborn-patch-notes/ ; Dexerto, D4 patch 1.4.0: https://www.dexerto.com/diablo/diablo-4-patch-notes-1-4-0-itemization-crafting-2676111/
- Path of Exile forums, crafting threads: https://www.pathofexile.com/forum/view-thread/3684812 ; https://www.pathofexile.com/forum/view-thread/1891766
- Backpack Battles Fandom, Rarity: https://backpack-battles.fandom.com/wiki/Rarity
- Into the Breach Wiki, Missions: https://intothebreach.fandom.com/wiki/Missions ; PC Gamer tips: https://pcgamer.com/into-the-breach-tips
- Monster Train artifacts guide: https://www.supercheats.com/monster-train-walkthrough-guide/artifacts-guide ; PC Gamer review: https://www.pcgamer.com/monster-train-review/
- Dead Cells Wiki, 0.5: https://deadcells.wiki.gg/wiki/0.5
