# Item / loot / reward design theory: research brief for Prince's Palace

Scope: party-based turn-based roguelike, run map (fights/shops/events), relics, gear with rarity + modifiers.

Source confidence tags:
- [S] = surfaced by search in this session (URL listed; snippet-level, page body not read).
- [K] = from background knowledge, not re-verified this session.

Limitation: WebFetch and curl were blocked (DNS failure / proxy 403 on gamedeveloper.com, mtg.wiki, game-wisdom.com). No page body was read. Every claim rests on search-result summaries or on background knowledge. Verify direct quotes before putting them in a doc. Look for the originals: Meier's GDC 2012 "Interesting Decisions" video, Rosewater's "New World Order" (Making Magic #518, 2011-12-05), and Burgun's "Randomness and Game Design".

---

## 1. Meaningful choice

**Principle.** Sid Meier: a game is "a series of interesting decisions" (GDC 2012 talk "Interesting Decisions"). Soren Johnson's gloss: a choice is interesting only if it is both informed (the player can predict the consequences) and impactful (the consequences matter). Johnson also stresses limited options (leaving some choices behind) as what defines strategy games. Meier's failure modes: a dominant option (always pick X), no right answer discoverable (no information), and a choice that is clearly the same as another (false choice).
Sources: [S] https://www.gamedeveloper.com/design/three-perspectives-on-strategy-game-design ; [S] https://www.gamedeveloper.com/game-platforms/analysis-sid-meier-s-key-design-lessons ; [S] https://www.gamedeveloper.com/design/video-sid-meier-explores-interesting-decisions-in-gameplay ; [S] https://www.gamedeveloper.com/design/designing-interesting-decisions-in-games-and-when-not-to-

**Derived points**
- Dominant option: if one offer is best in nearly every context, the pick is a lookup, not a decision. Cause: offers compared on one axis (raw stat total) with no context cost.
- False choice: two offers where one is strictly better (same slot, higher numbers). A decision exists only if each offer wins in some context (build, party composition, upcoming node, risk state).
- Comparability across axes: if options sit on incomparable axes (damage vs sustain vs economy), the player needs a visible exchange rate or a goal state ("I am short on X") to decide. Too incomparable and it becomes a coin flip. Too comparable and it is a spreadsheet.
- Choice count [K, convention in the genre rather than a cited rule]: the genre default is 3 offers (Slay the Spire card reward, Hades boon room), because 2 collapses to binary and 4+ raises read cost faster than decision value. Pair a small offer with agency valves (skip, reroll, banish) so a bad set is not forced. Larger pools suit shops, where browsing is the point.
- Context pull: an offer is interesting when its value depends on the current build. Offers independent of state are just stat drops.

**Tests**
1. For each offer set, can you name a plausible build state in which each of the 3 options is the correct pick? If one never wins, it is a false choice.
2. Take 20 real offer sets. How often do 10 playtesters agree on the pick? Over about 80% agreement = dominant option.
3. Can the player predict the effect from the tooltip alone (informed)? Does it change the next 3 fights (impactful)?
4. Is there always a legal "none of the above" (skip/convert to gold)?

---

## 2. Build-defining vs incremental items

**Principle.** Incremental items (+5% stat) are value-additive: they never change how you play, they are comparable on one axis, and they are low in decision weight. Rule-changing items (a "build-around") alter what a good action is, so they create archetype pull and run identity. Rosewater's vocabulary [S]: "parasitic" mechanics only function with a subset of other cards. He treats parasitism as a scale, not a binary, and ties it to linearity. A build-around rewards commitment and is dead without support, so the design cost is that it must sit in a pool dense enough to be playable but not so dense that it appears every run.
Sources: [S] https://mtgrocks.com/magic-the-gathering-head-designer-explains-what-is-and-isnt-a-parasitic-mechanic/ ; [S] https://draftsim.com/mtg-parasitic-cards/ ; [S] https://www.hipstersofthecoast.com/2021/07/are-dungeons-a-parasitic-mechanic/ . Primary posts live on Rosewater's Making Magic column / Blogatog (https://magic.wizards.com/en/articles/archive/making-magic/ ), not read.

**Slay the Spire (Mega Crit)**. Giovannetti, per [S] summaries: single-player means strong combos are fine because no opponent feels cheated, so the team rewards synergy mastery and tolerates rare overpowered builds. They brainstormed thousands of designs, cut to those with the most impact, and used a metrics server for balance. The cards and relics that define the game are mostly rule-changers, not number bumps.
Sources: [S] https://www.gamedeveloper.com/disciplines/road-to-the-igf-mega-crit-games-i-slay-the-spire-i- ; [S] https://www.gdcvault.com/browse/gdc-19/play/1025731 ("Slay the Spire: Metrics Driven Design and Balance", GDC 2019).

**Hades [K, Supergiant]**: boons are per-god pools, modify a specific input (attack/special/cast/dash), carry tag-based prerequisites, and unlock duo boons only when two gods are present. This turns a random offer into a directed build: the player chases combinations. Only low-quality sources were found this session; treat as unverified.

**Synergy structure rules of thumb** [K]
- Synergy should be keyed on a keyword/tag the player can read (e.g. "Burn", "Guard"), not on hidden numbers.
- Every archetype needs enablers (common), payoffs (rarer), and a fallback so a half-built archetype is still functional.
- Archetype pull is real when the first two items from an archetype make the third more valuable than a generic pick.

**Tests**
5. Ratio: of all items, what share changes a rule/targeting/trigger vs adds flat numbers? Healthy rough heuristic: a majority of rare+ items rule-changing; common may be numeric. (Heuristic, not sourced.)
6. Remove the item and ask: would the player's next 5 actions be different? If no, it is incremental.
7. For each build-around, count how many other items in the pool support it. Zero = dead card; everywhere = not a build-around.
8. Can a run with no build-around still win (floor), and does a fully supported run feel visibly different (ceiling)?

---

## 3. Complexity budget (design, not engineering)

**Principle.** Rosewater's New World Order (NWO, Making Magic #518, 2011-12-05): commons carry the lowest complexity because new players meet them first. Rules reported in [S] secondary sources: complexity is a budget allocated per set and rarity; a set's theme takes a portion of the commons' budget; commons do not affect more than one other permanent (board interaction at common is limited). Later: R&D "upped the amount of complexity we're allowing" because average players can handle more, and in 2025 he warned of complexity creep from interaction webs even when single mechanics are simple.
Sources: [S] https://articles.starcitygames.com/articles/new-world-order-and-complexity-creep/ ; [S] https://mtg.wiki/page/New_World_Order ; [S] https://magic.wizards.com/en/articles/archive/making-magic/new-new-world-order-2013-03-29 ; [S] https://www.wargamer.com/magic-the-gathering/mark-rosewater-complicated ; [S] https://draftsim.com/mtg-complexity-creep/ ; [S] https://mtgrocks.com/chief-designer-discusses-logistical-difficulties-of-mtg/ (logistical complexity: tracking zones/states).

**Applied to a party-based turn game** [K reasoning]: tracked state multiplies with party size (N heroes x statuses x passive triggers x enemy intents). Distinguish three costs: (a) read cost (understanding the effect), (b) interaction cost (how many other things it touches), (c) logistical cost (counters/states to remember). Rosewater's finding is that (b) and (c) grow worse than linearly in overall difficulty, and that complexity is most expensive at low-rarity because it is met first. Do not state a hard number of trackable effects as fact; use playtest observation (see tests).

**Tests**
9. Budget table: per rarity, count of items with (b)/(c) cost. Commons: none touch other items; rares can.
10. Count simultaneous persistent trackers at turn 1 / mid-run / late-run. Alarm threshold set by playtest (can a tester recite what will happen this turn without looking?).
11. Is there a log/preview that shows the trigger chain on screen, so memory is not the solver?
12. Does a new item interact with more than one existing system (new status + new resource)? Reject unless it replaces something.

---

## 4. Loot psychology

**Principles**
- Variable-ratio reinforcement (Skinner): unpredictable reward arrival yields persistent, extinction-resistant engagement; drops, gacha and slot machines share it. Anticipation is a large share of the pleasure. Pity timers (bad-luck protection) cap dry spells; true randomness produces streaks that feel "broken". Pity is also ethically contested (gambling overlap). Sources: [S] https://game-wisdom.com/?p=33449 ; [S] https://gurugamer.com/reviews/same-machine-different-skin-what-gacha-pulls-and-slot-reels-have-in-common-27099 ; [S] https://track360.io/glossary/pity-system (all secondary; for Skinner see his Schedules of Reinforcement, primary, not read).
- Slay the Spire rarity roll, as [S] community-reported (not official): the rare chance rises with every non-rare roll and resets on a rare, i.e. a pity timer on rarity. Source: [S] Steam discussion https://steamcommunity.com/app/646570/discussions/0/594015710347314793 . Verify against code before citing.
- Diablo 3: vanilla problems were mixed class/attribute gear that was mostly useless to the finder, and an Auction House that made trading more rewarding than playing. Jay Wilson (GDC 2013): "I think we would turn it off if we could"; money became a stronger motivator than killing monsters, damaging the core loop. Sources: [S] https://www.engadget.com/2013/03/28/diablo-3-director-jay-wilson-auction-houses-really-hurt-game/ ; [S] https://www.mcvuk.com/development-news/gdc-13-diablo-iii-auction-houses-hurt-game-admits-director/ .
- Loot 2.0 (Reaper of Souls, patch 2.0.1, 2014) [S, via fan wiki and Game-Wisdom]: quantity traded for quality (rarer but stronger legendaries), smart loot (drops biased to the finder's class, mixed-class/primary-attribute gear removed), Auction House removed. Sources: [S] https://di.diablowiki.net/Loot_2.0 ; [S] https://diablo.fandom.com/wiki/Loot_2.0 ; [S] https://game-wisdom.com/critical/loot-2-0-and-diablos-fourth-revival . Not Blizzard primary.
- Travis Day on Anthem [S, secondary]: reward structure matters more than raw drop rate; equal rewards for unequal effort; "dead" affixes that roll useless stats on an item; players lacked agency in choosing what to farm. Source: [S] https://www.pcgamesn.com/anthem/loot-drop-rate ; https://www.gamestar.de/artikel/anthem-diablo-bioware-loot,3341054.html .

**Loot explosion vs curated draft.** In a short-run roguelike with a party, inventory chore (compare, equip, sell trash, reassign across heroes) is pure friction. The Slay the Spire / Hades model is a curated draft: few offers, each pick matters, no inventory management. Loot explosion needs filters, auto-sort and sell-all to avoid chore. [K reasoning]

**Power creep / treadmill.** In a roguelike the run is the reset, so the treadmill risk sits in meta-progression (see 7) and in enemy scaling needing ever-larger numbers to match. [K]

**Tests**
13. Dead-roll test: for every modifier that can roll on every item slot/hero, what fraction of rolls are irrelevant to the receiving hero (Day's "dead affixes")? Target near zero.
14. Reward/effort test: do rewards scale with node risk/difficulty? Equal reward for unequal cost = players route to the cheap one.
15. Chore audit: count clicks from "got item" to "decision made". Items that cannot be decided in one screen are chore.
16. Dry-spell test: simulate 10,000 runs; what is the P(no rare or better by node N)? Is it within the tolerance you set (pity)?
17. Is there any currency trade path that outpaces fighting (Diablo AH)? In a shop/gold economy, can the player farm gold safely?

---

## 5. Randomness

**Principle.** Keith Burgun (2014): input randomness is what the player learns before deciding (map generation, offered options); output randomness enters after the decision and determines its result (to-hit rolls). He argues output randomness undermines strategy games while input randomness enriches them; critics defend output randomness; he later treated it as a spectrum ("information delay"). A 2021 arXiv study reports that input randomness can reduce satisfaction in collectible card games, so the distinction has trade-offs.
Sources: [S] https://gamedeveloper.com/design/randomness-and-game-design ; [S] https://www.gamedeveloper.com/design/dissection-of-randomness-in-games ; [S] https://keithburgun.net/riot-is-thinking-about-inputoutput-randomness-in-league-of-legends ; [S] https://arxiv.org/pdf/2107.08437v1.pdf . GMTK video on the same topic: [K], not located.

**Application to loot**
- Offer generation is input randomness (good): the player sees the options, then decides. Roll-after-pick (an item that rolls modifiers after you choose it, or a gamble shop) is output randomness; allow it where "gambling" is the point.
- Combat crit/miss is output randomness; large-variance output randomness in turn combat produces unfair-feeling losses that item value cannot fix.
- Agency valves convert randomness to skill: reroll (costed), skip (for gold or an HP/max-HP benefit), banish/remove from the pool (Hades-style) [K], pre-commit via shop visibility.
- Controlled randomness: weighted rarity curves by node depth; pity on rarity; offer de-duplication (no duplicate item in an offer set; no repeat of an item the player already owns unless stackable); pool filtering by party composition/class (smart loot); seed-stable offers for save-scum resistance.

**Tests**
18. Duplicate/near-duplicate rate within one offer: zero for identical items; limit same-slot or same-archetype offers.
19. Does randomness occur before or after the player commits? List every roll and classify as input/output.
20. Is there at least one lever per run to fix a bad offer (skip, reroll, banish)? Does using it cost something?
21. Distribution check: histogram of rarity per node depth and per run; compare to the intended curve.

---

## 6. Run-level arc and economy

**Principle** [K unless cited; this synthesises common genre practice]
- Power curve: runs should have a rising-then-pressured curve: early picks are mostly generic, around the mid-run the build comes "online", and the late game tests the build. Scaling items (grow over time) vs flat items (immediately good): scaling items must cost something early or they dominate (for example, a delayed payoff in exchange for early weakness).
- Rewarding risk: elite/hard nodes pay stronger or rarer rewards, with player-chosen risk (route choice) as the decision. Reward-for-effort parity is the issue Day raised for Anthem [S above].
- Economy sinks: gold needs sinks that compete with each other (healing, removal, upgrades, items). Hoarded gold means sinks are weak or prices are wrong. Removing an item is a strong sink in deck-builders (thinning).
- Value legibility: the tooltip should state the effect in numbers and with its trigger, and compare with the equipped item (delta, not totals). Show the exchange rate for non-comparable stats. Show who benefits in a party (which hero) and what happens at current build state.
- Comparison UI: side-by-side vs equipped with green/red deltas; "why this is offered" is optional.
- Giovannetti's metrics-driven approach: log pick rates, win rates by item, and adjust. Items with extreme pick rate or never-picked are dominant or dead. [S] https://www.gdcvault.com/browse/gdc-19/play/1025731 ; https://www.gamedeveloper.com/design/learn-i-slay-the-spire-i-s-metrics-driven-approach-to-game-balancing-at-gdc-2019

**Tests**
22. Plot win-rate vs node index of the "build online" moment. Is it between a third and half through the run? Very early = no tension; never = no payoff. (Position is a heuristic.)
23. Item pick rate and win-rate contribution per item; flag items above or below set thresholds.
24. Gold at end of run: median unspent. High unspent = weak sinks.
25. Can a new player state the delta between an offered item and the current one in under 5 seconds?

---

## 7. Meta-progression vs run items

**Principle** [K]: Meta-progression (unlocks across runs) has two forms: (a) breadth unlocks (more items enter the pool: more choices, no raw power), and (b) power unlocks (permanent stats). Breadth keeps each run's balance intact, but it dilutes pool density for early players if unlocked items are added without regard to archetype completeness. Power unlocks create meta-creep: the run becomes a formality and later difficulty must be inflated (Hades Mirror, Slay the Spire has none). Heuristics: unlock archetypes as complete packages; lock elements that add complexity (complexity budget doubles as a tutorial); keep any permanent power small and gated by difficulty ladders (Hades Heat, Slay the Spire Ascension).

**Tests**
26. If a new player used the fully unlocked pool, would the average offer set be worse (diluted)? If unlocks are additive, does the early pool still contain complete archetypes?
27. How much of a win is attributable to permanent power vs in-run decisions? If most, the item system matters less than the meta.

---

## 8. Content-authoring structure (design view)

**Principles** [K; the search for authoritative sources found only Path of Exile tag docs]
- Tags/keywords: Path of Exile uses internal tags on modifiers, items, monsters and areas to decide which modifiers can spawn where and to target effects ([S] https://pathofexile.fandom.com/wiki/Tag , wiki, secondary). Tags give the designer a vocabulary ("Burn", "Guard", "Heal") that doubles as the player's synergy vocabulary and as the pool filter for smart loot.
- Data-driven effect composition: express each item as trigger (on turn start / on hit / on kill / on node entered) x condition (tag, HP threshold, party size, target type) x effect (stat modifier, apply status, grant resource, change targeting) x magnitude/stacking rule. A small set of composable triggers, conditions and effects yields many items without per-item code. Bespoke code is reserved for a short, reviewed list of exceptional effects.
- A modifier pool for gear: each modifier has tags, weight, slot/hero-class restrictions and tier ranges, so dead affixes are prevented at the data level (Day's critique).
- A "keyword glossary" that tooltips resolve: define a keyword once, and every item refers to it.
- Linting for design: validate that every tag has producers and consumers; every trigger has a visible UI hook; no item uses an effect type that has fewer than N users unless flagged as bespoke.

**Tests**
28. What share of items is expressed purely as data (trigger x condition x effect)? Count bespoke classes per item.
29. For each tag, count producers and consumers; zero consumers = decorative tag, zero producers = dead payoff.
30. Can a designer add a new item with no code change and have the tooltip text generated or checked?

---

## Scoring checklist: 14 heuristics to score the current system

Score each 0 (fails) / 1 (partial) / 2 (passes); note the evidence.

1. **Each-wins-somewhere**: every offered option is the right pick in some identifiable build/context (no dominant option, no strictly-better pairs). [Meier/Johnson]
2. **Informed and impactful**: the player can predict the effect from the tooltip and it changes the next several fights. [Johnson]
3. **Offer size and valves**: 3 (or deliberate) options with skip/reroll/banish at a cost; a bad set is never forced.
4. **Rule-change share**: a meaningful share of rare+ items change rules, not just numbers; commons may be numeric. [Rosewater, Slay the Spire]
5. **Archetype density**: each build-around has enough supporting items in the pool (>0, not ubiquitous), and the floor of a non-committed run is viable.
6. **Complexity by rarity**: low-rarity items have low interaction and logistical cost; high-interaction items are rare. [NWO]
7. **Trackable state**: a tester can recite what this turn will do; trigger chains are visible in UI/log, and state is bounded for party size.
8. **No dead rolls**: modifiers cannot roll irrelevant to the receiving hero/slot; offers lean toward the party's builds. [Day, Loot 2.0 smart loot]
9. **Randomness placement**: offers and map generation are input randomness (seen before choosing); output randomness is bounded; every roll is classified. [Burgun]
10. **Controlled distribution**: rarity curve by depth, pity/bad-luck protection, no duplicate offers, simulated dry-spell probabilities within tolerance.
11. **Risk-reward parity**: harder or riskier nodes pay proportionally better; no safe gold or reward farm that beats fighting. [Day, Wilson]
12. **Run arc**: build comes online mid-run (measured), scaling items cost something early, late nodes test the build.
13. **Zero chore**: from item acquired to decision made takes one screen; comparison shows deltas against the equipped item; no inventory sorting for its own sake.
14. **Economy sinks**: gold has multiple competing sinks; median unspent gold at run end is low; removal/thinning is available if the system has clutter.
15. **Data-driven**: items are composed from trigger x condition x effect with tags; bespoke code is exceptional and listed; every tag has producers and consumers; pick-rate and win-rate telemetry exists for balancing. [Giovannetti GDC 2019, PoE tags]

(Meta-progression, section 7: score separately if the project has unlock pools: breadth over power, complete archetype packages, no permanent power inflating difficulty.)

---

## Source list (all URLs surfaced by search in this session unless marked)
- Meier / Johnson: https://www.gamedeveloper.com/design/three-perspectives-on-strategy-game-design ; https://www.gamedeveloper.com/game-platforms/analysis-sid-meier-s-key-design-lessons ; https://www.gamedeveloper.com/design/video-sid-meier-explores-interesting-decisions-in-gameplay ; https://www.gamedeveloper.com/design/designing-interesting-decisions-in-games-and-when-not-to-
- Rosewater: https://articles.starcitygames.com/articles/new-world-order-and-complexity-creep/ ; https://mtg.wiki/page/New_World_Order ; https://magic.wizards.com/en/articles/archive/making-magic/new-new-world-order-2013-03-29 ; https://draftsim.com/mtg-parasitic-cards/ ; https://mtgrocks.com/magic-the-gathering-head-designer-explains-what-is-and-isnt-a-parasitic-mechanic/ ; https://draftsim.com/mtg-complexity-creep/ ; https://mtgrocks.com/chief-designer-discusses-logistical-difficulties-of-mtg/ ; https://www.wargamer.com/magic-the-gathering/mark-rosewater-complicated
- Burgun: https://gamedeveloper.com/design/randomness-and-game-design ; https://www.gamedeveloper.com/design/dissection-of-randomness-in-games ; https://keithburgun.net/riot-is-thinking-about-inputoutput-randomness-in-league-of-legends ; https://arxiv.org/pdf/2107.08437v1.pdf
- Diablo 3: https://www.engadget.com/2013/03/28/diablo-3-director-jay-wilson-auction-houses-really-hurt-game/ ; https://www.mcvuk.com/development-news/gdc-13-diablo-iii-auction-houses-hurt-game-admits-director/ ; https://di.diablowiki.net/Loot_2.0 ; https://game-wisdom.com/critical/loot-2-0-and-diablos-fourth-revival ; https://www.pcgamesn.com/anthem/loot-drop-rate ; https://www.gamestar.de/artikel/anthem-diablo-bioware-loot,3341054.html
- Slay the Spire: https://www.gamedeveloper.com/disciplines/road-to-the-igf-mega-crit-games-i-slay-the-spire-i- ; https://www.gdcvault.com/browse/gdc-19/play/1025731 ; https://www.gamedeveloper.com/design/learn-i-slay-the-spire-i-s-metrics-driven-approach-to-game-balancing-at-gdc-2019 ; (rarity pity, community) https://steamcommunity.com/app/646570/discussions/0/594015710347314793
- Reward psychology (secondary): https://game-wisdom.com/?p=33449 ; https://gurugamer.com/reviews/same-machine-different-skin-what-gacha-pulls-and-slot-reels-have-in-common-27099 ; https://track360.io/glossary/pity-system
- Tags: https://pathofexile.fandom.com/wiki/Tag
- Not found / to do: Hades design commentary (Supergiant / Kasavin) beyond low-quality guides; GMTK randomness video; Skinner primary text; Blizzard primary Loot 2.0 dev posts; any hard published limit on trackable effects (do not assert one).
