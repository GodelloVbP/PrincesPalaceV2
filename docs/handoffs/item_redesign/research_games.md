# Item and item-reward design in acclaimed games: brief for Prince's Palace

## Source quality (read first)

- WebFetch was blocked by the egress proxy (403 on every wiki host: fandom, wiki.gg, balatrowiki, fextralife). All evidence comes from WebSearch result summaries only. No GDC talk or postmortem could be retrieved. A GDC Vault search for Casey Yano / Mega Crit, Supergiant, Diablo 3 Loot 2.0 and Subset Games would be the next step once fetch is allowed.
- Tags: **[V]** = stated in a search result this session, with the URL given. **[M]** = from model memory, unverified, treat as approximate. Several exact numbers (Balatro, RoR2, Hades rarity) could not be confirmed. Do not pin them in tests or docs until checked.

---

## 1. Slay the Spire
- **Taxonomy**: cards (the run's main reward), relics (passive, ~common/uncommon/rare/boss/shop/event), potions (3 slots [M]).
- **Card reward**: 3 cards, pick 1 or skip [M]. Rarity per normal fight: Common 60 / Uncommon 37 / Rare 3. Elite: 50 / 40 / 10. Boss: 100% Rare [V].
- **Pity**: a rare offset starts at -5%, +1% per common rolled, resets to -5% on a rare, caps at +40% [V]. Only card-reward rolls move it. Shop and Library cards do not. The first floor of each act cannot offer a Rare [V]. https://slay-the-spire.fandom.com/wiki/Card_Rewards , https://slaythespire.wiki.gg/wiki/Cards
- **Relic rarity**: 50 / 33 / 17 (3:2:1) from most sources. Chests by size: small 75C/25U, medium 35/50/15, large 75U/25R [V]. https://slaythespire.wiki.gg/wiki/Relics
- **Boss relics**: a choice of 3 after each act boss [V per forum, M otherwise]. Many carry a downside (energy +1 with a cost). Players: energy relics are near-mandatory at high Ascension and the non-energy options are often weaker than commons [V, Steam thread]. https://steamcommunity.com/app/646570/discussions/0/2650805212058485290
- **Skip as agency**: Singing Bowl (+2 max HP for skipping) [M].
- **Shop**: 5 coloured cards + 2 colourless + relics + potions, card removal price rising per use [M].
- **Synergy**: emergent from deck archetypes. Relics warp archetypes. There are no tags or sets.
- **Scaling**: power comes from deck-thinning and relic accumulation. Enemy difficulty is fixed per act, with Ascension modifiers.
- **Limits**: potion slots only. Relics are unlimited.
- **Info**: full text on hover, card preview of the upgrade, node map with shop/elite/rest icons so the player plans the route.
- **Praised**: the skip option, the pity offset smoothing rares, route choice trading risk for reward. **Criticised**: the energy-relic monoculture among boss relics.

## 2. Hades / Hades II
- **Taxonomy**: boons (per god, slot-keyed: Attack/Special/Cast/Dash/Call), Daedalus Hammer (weapon modifiers), Pom of Power (levels a boon), duo and legendary boons (cross-god payoff), Chaos boons (curse then reward), keepsakes (meta, one equipped), mirror/arcana meta upgrades.
- **Cadence**: a reward behind nearly every door. Boon offer is 3 choices [M]. Door icons show the reward type and god before entry [V]. https://hades.wiki.fextralife.com/Artemis'+Aid
- **Rarity**: Common / Rare / Epic / Heroic (white, blue, purple, red) [V]. Exact odds not found. Poms raise a level. In Hades II, Poms cannot upgrade Duo, Legendary or Infusion boons [V]. https://rogueranker.com/hades-2-boons/
- **Synergy**: duo needs one prerequisite boon from each of the two gods. Legendary needs a tier-1 plus a tier-2 boon of one god. Neither is guaranteed even when eligible [V]. https://primagames.com/gaming/hades-guide-legendary-heroic-duo-boons . Keepsake from a god raises that god's duo odds, scaling with keepsake rank [V]. Hades II: Arcana "Queen" gives 6% duo chance on offered boons [V]. https://maxroll.gg/hades-2/guides/hades-2-keepsake-guide
- **Randomness controls**: limited rerolls, door preview, keepsakes biasing god/rarity, Hammer offers 3 choices. Charon's shop is random and not reliable [V].
- **Scaling**: slots fill to roughly 5-6 boons plus rarity upgrades. Power is narrative and visible, not a stat pile.
- **Info**: each boon names the god, the slot, and what it changes in the attack. Prerequisites for duos are listed on the boon.
- **Praised**: every boon changes how the attack plays, preview of the next reward, synergy as a visible payoff. **Criticised**: duo and legendary gating feels opaque until learned. Rarity luck can feel swingy.

## 3. Balatro
- **Taxonomy**: jokers (relics with rarity Common/Uncommon/Rare/Legendary and editions Foil/Holo/Polychrome/Negative), consumables (tarot/planet/spectral), vouchers, booster packs, card enhancements and seals.
- **Slot limit**: 5 joker slots as the core inventory pressure [M]. Negative edition adds a slot. Legendary jokers never appear in shops [V]. https://games.gg/balatro/guides/balatro-jokers-guide/
- **Shop**: reroll starts at $5 and rises $1 per reroll within a shop [M; Steam confirms a $5 reroll and complaints about cost, https://steamcommunity.com/app/2379780/discussions/0/4308326918573108841]. Shop joker rarity roughly 70/25/5 [M, unverified]. Edition rates: roughly 4% of shop jokers carry one, Foil most likely [M, unverified].
- **Economy**: interest on held cash makes spending a choice against saving [M]. Sell value is half of cost [M].
- **Synergy**: ordered left-to-right joker resolution plus poker-hand tags. Order is a puzzle in itself.
- **Scaling**: multiplicative stacking (xMult) is the power curve. Ante targets grow exponentially to force it.
- **Info**: joker text is exact and the live score preview is shown.
- **Praised**: scarce slots turn every purchase into a replace-or-keep decision. **Criticised**: reroll feels expensive, and some jokers are traps. Gros Michel odds differ between sources (1 in 6 vs 1 in 4), so verify version data [V].

## 4. Monster Train
- **Taxonomy**: cards, monster upgrades (2 per card [M]), artifacts (relic equivalent, "unlimited and often handed out like sweets", compared to Balatro jokers [V]), champion upgrades. https://www.pcgamer.com/monster-train-review/
- **Clan system**: a run uses a main clan and a supporting clan. Reviewers praise synergies that "feel like you've broken the game" [V].
- **Rewards**: pick 1 of 3 cards [M]. Between battles: clone, upgrade and merchant nodes [V partial]. Card upgrades cost nothing and are applied in-run, capped per card [M].
- **Lesson**: the synergy axis is declared up front (two clans), so reward pools are pre-filtered to a theme. That removes dead offers.
- **Criticised/praised**: artifacts often mirror Slay the Spire relics. Patches redesigned several that were weak or degenerate [V changelog].

## 5. Into the Breach
- **Taxonomy**: weapons (bought with Reputation), power cores (a limited upgrade currency), pilots (XP, passive ability), grid power (global HP).
- **Cadence**: Reputation earned per island, spent at a shop between islands. Pilots dropped or sold for 2 Reputation [V]. https://steamcommunity.com/app/590380/discussions/0/2650805212061856797
- **Randomness**: the game is otherwise "luck-free"; the grid-defense perk is criticised as out of place for that reason [V].
- **Limits**: each upgrade needs cores, so the choice is weapon vs. upgrade vs. grid [V].
- **Numbers stay small**: +1 damage or +1 range changes tactics. The Advanced Edition raised weapon Reputation cost by 1 and narrowed first-island shop options [V]. https://nintendoeverything.com/into-the-breach-advanced-edition-update-out-now-patch-notes/
- **Lesson**: in tactical games a small, deterministic delta that unlocks a new line of play is worth more than a percentage bump.

## 6. Darkest Dungeon I and II
- **Taxonomy**: trinkets (2 per hero), plus camping and consumable items. DD1 trinket rarity scales with expedition length, Common up through higher tiers [V]. DD2 trinkets cost Baubles. Relics are a separate currency for consumables. Rarity names Vague / Distant / Indelible, with Cultist trinkets Unforgettable [V]. https://darkestdungeon.wiki.gg/wiki/Trinkets , https://darkestdungeon2.wiki.fextralife.com/Trinkets
- **Downsides**: trinkets give stat bonuses and penalties together. Players say penalties are hard to justify and few trinkets have "full positive effect" besides the speed stone [V, Steam]. DD2 class-specific trinkets are mostly pure bonuses [V]. High-rarity pieces criticised as weak or designed to kill the hero (Death's Head, Fate's Forteller) [V].
- **Party relevance**: a per-hero slot cap (2) makes gearing a party a distribution problem. Stat swaps change role, not only numbers.
- **Lesson**: downside items need a legible reason to take them. Where the downside was only a tax, players ignored them.

## 7. Risk of Rain 2
- **Taxonomy**: white / green / red (common/uncommon/legendary), yellow boss items (15% when the teleporter boss drops it [V]), lunar items (power with a cost), equipment (one slot), void items. https://rogueranker.com/risk-of-rain-2-items/
- **Chests**: tier weighted by chest size. Exact vanilla weights were not found. The Thunderstore mod example (small 80/18/2, large 20/70/10) is mod data, not vanilla [V]. My memory of vanilla small chests is roughly 80/20/1 [M].
- **Agency**: 3D printers trade a duplicate or lower item for another of the same tier. Scrappers turn unwanted items into scrap for printing [V partial]. Yellow scrap feeds yellow printers [V]. https://rogueranker.com/yellow-items-ror2/
- **Stacking**: items stack with diminishing (hyperbolic) or linear formulas per item [M]. The player's power is the pile, and more of the same item is a legitimate build.
- **Information**: colour tier is instantly readable. Item pickups show stack counts. The wiki-level math is left to the community.
- **Praised**: scrapper/printer removes dead-item frustration. **Criticised**: stacking makes some items strictly better, and balance by tier colour is loose [M].

## 8. Dead Cells
- **Taxonomy**: weapons, skills, amulets, mutations, scrolls (Brutality / Tactics / Survival), blueprints (permanent unlocks), cursed chests.
- **Scaling**: three stats start at 1 and rise via Scrolls of Power. Weapons scale from one or two stats and the larger is used [V]. Dual-stat scrolls are weighted towards your lowest stats [V]. Enemy strength rises with scrolls collected. Community estimate is about 7-8% per scroll, not official [V]. https://deadcells.wiki.gg/wiki/Stats
- **Affixes**: random affixes on weapons are tied to colour (Brutality red, Tactics purple, Survival green) [M]. This puts a build identity on each drop.
- **Lesson**: one stat-axis colour system makes every item self-explaining on pickup. Weighting rolls toward the lowest stat pushes the player to diversify.

## 9. Diablo III Loot 2.0, Diablo IV, Path of Exile
- **D3 Loot 2.0** (patch 2.0.1, Feb 2014): fewer, better drops. Smart drops biased to the class, with the right primary stat [V]. Legendaries were rarer but stronger [V]. Core lesson: a "cool power" legendary was worse than a yellow item with good stats, which made the choice unfun [V]. https://diablo.fandom.com/wiki/Loot_2.0 , https://www.pcgamesn.com/diablo/diablo-3-developers-plan-make-legendaries-more-powerful
- **Affix criticism**: a former lead says useless or nonsensical affixes were the core problem of loot design [V, about Anthem]. https://mein-mmo.de/anthem-diablo-3-entwickler-tipps/
- **D4 aspects**: affix bloat that reduces to damage or damage reduction. Common and Magic items serve only as salvage [V]. https://www.wowhead.com/diablo-4/de/news/the-problems-with-itemization-in-diablo-4-336951 . Aspects seen as bland, as class-tree content moved onto items [V]. https://twinfinite.net/diablo-4/diablo-4s-itemization-still-greatest-weakness/ . Single-use extracted aspects caused stash hoarding versus Diablo Immortal's permanent codex [V]. https://us.forums.blizzard.com/en/d4/t/the-aspect-system/81725
- **PoE**: currency items are the crafting layer and loot filters are the information layer [M]. Depth is loved, opacity is the cost [M]. No PoE source was retrieved.
- **Lesson**: if low-rarity items have no role, rarity inflation follows. A build-defining effect should not lose to a stat stick.

## 10. Backpack Battles / Backpack Hero
- **Taxonomy**: items of different shapes, sizes, prices and rarities. Combine into stronger items [V]. https://store.steampowered.com/app/2427700/Backpack_Battles
- **Synergy**: adjacency and placement in the bag. Weapons used together synthesise [V Gigazine]. https://gigazine.net/gsc_news/en/20240310-backpack-battles
- **Inventory pressure**: the grid is the limit, so shape is the cost. Very Positive on Steam (5,485 reviews) [V].
- **Not found**: shop weights and reroll costs. [M] Battles uses cheap fixed-cost rerolls per shop round.
- **Lesson**: spatial layout is an inventory limit and a synergy engine in one mechanism. It costs UI complexity (drag, rotate).

## 11. Wildfrost (turn-based, party-ish cards)
- **Taxonomy**: companions (the party), items (consumable cards), charms (permanent card modifiers), crowns [V]. https://punishedbacklog.com/wildfrost-review/
- **Charms**: from map charm machines or bought at 50 / 70 / 100 gold. Maximum 3 per card [V]. Reviewers single out the chance element of charms as exceptional [V].
- **Counter system**: units act after N turns, so modifiers that change the counter are the most valued [V]. https://www.pocketgamer.com/wildfrost/review/
- **Lesson**: per-unit slot caps (3) plus priced upgrades make distribution across a party the decision. Rarity is secondary.

(Divinity: Original Sin 2 was not researched in this session.)

---

# 1. Cross-game comparison

| Game | Item types | Choice per reward | Skip / reroll / control | Rarity & pity | Synergy mechanism | Inventory limit | Strongest praise | Strongest criticism |
|---|---|---|---|---|---|---|---|---|
| Slay the Spire | cards, relics, potions | 3 cards [M] | skip, Singing Bowl [M] | 60/37/3 normal, 50/40/10 elite; rare offset -5%..+40% [V] | deck archetype | 3 potions [M] | pity + skip | boss-relic monoculture |
| Hades / II | boons, hammers, keepsakes | 3 [M] | limited reroll, door preview [V] | 4 tiers, odds unverified | duo/legendary prerequisites [V] | slot-keyed | boon rewrites the attack | opaque gating |
| Balatro | jokers, consumables | 2 shop jokers [M] | reroll $5 +$1 [M] | 70/25/5 [M, unverified] | left-to-right order, hand tags | 5 jokers [M] | slot pressure | reroll cost |
| Monster Train | cards, artifacts, upgrades | 3 cards [M] | skip [M] | n/a [M] | two-clan pools [V] | none on artifacts [V] | clan synergy | some artifacts mirror StS |
| Into the Breach | weapons, cores, pilots | shop | none | none (deterministic) [V] | pilot + weapon fit | core budget [V] | small deltas matter | random perk off-tone [V] |
| Darkest Dungeon | trinkets | by expedition | Wagon buy [V] | scales with dungeon length [V] | role/stat swaps | 2 per hero [V] | per-hero loadout | penalties not worth it [V] |
| Risk of Rain 2 | items, equipment, lunar | chest roll | printers/scrappers [V] | by chest size | stacking | one equipment [M] | scrap agency | strict upgrades |
| Dead Cells | weapons, skills, scrolls | 2-3 offers | cursed-chest gamble [M] | colour-tier | 3-colour stat scaling [V] | 2W+2S+amulet [M] | colour = identity | enemy scaling per scroll [V] |
| D3 / D4 / PoE | gear + affixes | drop | filters, crafting | legendary scarcity | smart loot, aspects | stash | Loot 2.0 smart drops | affix bloat, bland aspects [V] |
| Backpack Battles | shaped items | shop | shop reroll [M] | rarity tiers [V] | adjacency + combine [V] | grid | spatial puzzle | UI/learning cost |
| Wildfrost | companions, items, charms | shop/charm | machines [V] | 50/70/100 gold prices [V] | counter modifiers | 3 charms/card [V] | charm chance | unforgiving [V] |

# 2. Transferable patterns, ranked for a party-based turn-based roguelike

1. **Pick-1-of-3 with always-available skip, plus a pity offset on rarity** (StS). The offset is stateful (carry it in the run state, reset on hit), and only reward rolls move it. Cheapest high-value fix against rarity droughts.
2. **Reward preview on map nodes** (Hades door icons, StS map). The player routes for what they need. Fits the run map directly.
3. **Per-hero slot caps, priced upgrades** (Darkest Dungeon 2 trinkets, Wildfrost 3 charms). Distribution across the party becomes the decision. Make the cap small and visible.
4. **A slot-limit relic bag as the pressure point** (Balatro 5 jokers). Replace-or-keep drives choices. Pair with sell value so replacing is not pure loss.
5. **Declared synergy axis that pre-filters pools** (Monster Train clans, Dead Cells colours, Hades gods). Tags on items that match hero class or element reduce dead offers and make build identity readable on pickup.
6. **Explicit synergy payoff tier with prerequisites** (Hades duo/legendary). Show the prerequisite on the item and keep the odds boosters learnable (keepsake-like meta). Do not make them guaranteed.
7. **Scrap/convert agency** (RoR2 scrapper/printer, StS card removal). A way to turn dead items into currency or a chosen tier is the best answer to unwanted rolls.
8. **Small deterministic deltas, not percentages** (Into the Breach). Gear that adds +1 range or a new status changes tactics. Fits turn-based.
9. **Bonus items that carry a legible tradeoff** (StS boss relics, RoR2 lunar). Keep the downside interesting (a cost that changes play), not a flat tax.
10. **Rarity must change function, not only magnitude** (D3 Loot 2.0 lesson). A top-rarity item should rewrite a rule. Low rarities need a role (upgrade material, set filler) or they die.
11. **Single colour/tag per item for readability** (RoR2 tier colours, Dead Cells stat colours). One glance on the loot screen.
12. **Smart bias, not hard filter, on class fit** (D3 smart loot, Dead Cells lowest-stat weighting). Weight offers toward heroes in the current party while keeping off-party items possible.

# 3. Anti-patterns (abandoned or criticised)

- **Flat-penalty downsides** (Darkest Dungeon trinkets): players ignore items whose cost is just a tax [V].
- **Affix bloat with no meaning** (D3, Anthem, D4): niche affixes that reduce to damage or defence [V].
- **Rarity tier with no role** (D4 Common/Magic as vendor trash) [V].
- **"Cool power" worse than a stat stick** (D3 legendaries pre-2.0) [V].
- **Single-use extracted effects** (D4 aspects) that create stash hoarding [V].
- **Non-negotiable boss reward** (StS energy relic) that reduces the 3-pick to one answer [V].
- **Random perks in a deterministic design** (Into the Breach grid defense) [V].
- **Expensive reroll with no fallback** (Balatro, Steam complaints) [V].
- **Opaque gating of the best items** (Hades duos until learned) [V, partly M].
- **Item-driven enemy scaling that punishes pickups** (Dead Cells scrolls, unofficial) [V].
- **Strictly dominant stackers** (RoR2) [M].

## Open questions
- Retrieve primary sources (GDC Vault, developer blogs) once fetch is allowed. Verify Balatro shop weights, edition rates, RoR2 chest weights, Hades rarity odds, Monster Train reward counts, and the D4 Season 4 itemization changes.
- Divinity: Original Sin 2 and Hades II hammer details were not covered.
