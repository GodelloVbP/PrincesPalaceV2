# Item and reward design theory: research by Claude (Sonnet 5.5), 2026-10-13

Scope: theory for the item redesign. Audit refs (D1-D10, C1-C7) are from `README.md` in this folder. "[own]" marks my reasoning, not a sourced claim. Search coverage was thin: no GDC Vault talk on Hades boons or Slay the Spire reward design turned up, so those points rest on wikis and interviews and are marked.

## 1. Meaningful choice

- Meier's line, as elaborated in design writing: an interesting choice has no clearly superior option, options are about equally attractive, and the player can make an informed pick. A dominant option makes the choice trivial. ([Designing interesting decisions in games and when not to](https://www.gamedeveloper.com/design/designing-interesting-decisions-in-games-and-when-not-to-); origin of the quote is murky, see also [Lessons from Sid Meier](https://www.antoinebuteau.com/lessons-from-sid-meier/).)
- A decision also needs a trade-off with an upside and a downside, else it does not affect winning or losing. Same Game Developer article.
- [own] A 1-of-3 pick is real when the three differ in *kind* (different plans they enable) and the player can tell which plan each serves. It is false when the three differ in *size* along one axis (the best is obvious), or when they differ but the player cannot read the difference without a tooltip, so they pick by colour. Three same-tier stat sticks are false on both counts. The only live question is "which slot", which the player already knows from what is empty.
- [own] A pick also needs an opportunity cost: taking X must mean not taking Y. Without skip, reroll or alternate use, the pick is "which of three free things", and the cost is zero.

## 2. Build-defining vs incremental

- [own] Incremental items (stat sticks) answer "am I strong enough". Build-defining items (mechanics) answer "how do I play". Stat sticks give the power floor and a legible progression feeling; mechanics give variety and stories. A game of only the first has no run-to-run identity. A game of only the second has no gradient, so every pick is a gamble on synergy.
- [own] Practical split: frequent rewards carry the incremental share and are cheap to evaluate; rare rewards carry mechanics and get more screen time and a real choice. The audit shows the inverse: frequent rewards are stat sticks (D1) and all 34 mechanics sit in a once-per-run draft (D2, D9). A mechanic arriving 1-2 times a run cannot define a build, because the build is mostly formed before it lands.
- [own] Mechanics must hook into something the player already does (spell cast, kill, ward, turn). Modifier mirrors ("+10% fire" vs "+10% ice", 12 of 25 affixes, D2) are stat sticks in costume.
- Hades is the usual reference: boons are mechanic-first, with rarity scaling numbers, not identity ([Wiki-level summary of rarity tiers](https://hades.fandom.com/wiki/Boons?oldid=6594); low-quality secondary source). [own] Note: this is a recalled pattern; I could not confirm a designer statement.

## 3. Input vs output randomness (applied to loot)

- Burgun: input randomness informs the player *before* a decision (map generation, face-up offers); output randomness sits between a decision and its result. He treats them as a spectrum, and argues output randomness does not belong in strategy games. ([Randomness and Game Design](https://gamedeveloper.com/design/randomness-and-game-design); [Three types of bad randomness, and one good one](https://keithburgun.net/three-types-of-bad-randomness-and-one-good-one/)). The attribution to Garfield is not supported by what I found; the frame is Burgun's. Garfield's *Characteristics of Games* does discuss luck and skill, but I could not confirm it uses this split.
- [own] Applied to loot: a displayed offer set is **input randomness** (roll, show, player decides with full information). Fine. Output randomness in loot is anything rolled *after* the choice: an unknown affix rolled on pickup, an upgrade that may fail, a "mystery" item. Rule: roll before showing; the card the player takes is the card they get.
- [own] The audit's offers are mostly input randomness (good) but the visible information is degraded (D4: affixes only in a tooltip), so informed choice is weakened. Plus is rolled and never changed (D5): no output randomness, but also zero agency over it.
- Mark Rosewater's framing, that randomness is best when it creates the problem to solve rather than the result (see [Kind Acts of Randomness](https://magic.wizards.com/en/news/making-magic/kind-acts-randomness-2009-05-04)), is the same idea. I did not read the full column; treat as a pointer.

## 4. Pity, rerolls, skips

- Slay the Spire (wiki, not designer commentary): 3 cards per reward, the player can skip; rarity is rolled per card; elites and bosses shift the table; a rare-card pity offset starts at -5%, rises 1% per common, resets on a rare, caps at +40%. ([Card Rewards](https://slay-the-spire.fandom.com/wiki/Card_Rewards)). No source I found states the design reasoning for skippable rewards.
- [own] Skip is the cheapest agency device: it makes the pick a real decision ("is any of these better than my deck as it is?") and punishes nothing. It matters most when the pool contains clutter, which is true here (D1, ~32% of normal fights offer three bare items, per audit calc).
- [own] A skip with no payout is weak; a skip that pays (gold, a salvage resource, a reroll token) turns junk into progress and fixes the "dead weight" in D5. Anything that spends or loses interacts with the owner's open confirmation rule (audit f, AUDIT #225); a payout skip does not.
- [own] Rerolls: cap them (per run or escalating cost, like the shop's 15 x 2^n, audit c). Unlimited rerolls collapse the choice into a search. Rerolls should preserve the player's ability to see what they are buying: a reroll that replaces all three loses information the player already used.
- [own] Pity: protect against *streaks of nothing*, not against low averages. Concrete forms: a counter that raises rarity chance each non-hit fight and resets on a hit (the StS shape); or a hard guarantee of one notable item per N fights. Pity should be seeded and persist across a quit (audit C4: fight offers use `UnityEngine.Random`, so a quit-and-reenter may reroll).

## 5. Complexity budget

- [own, no source]: a card can carry about **2 power axes** readable without a tooltip: a headline number the player can rank by (or a tier/colour that ranks), and one named behaviour (a short phrase). A third axis needs a tooltip. The test is whether two cards can be ranked or told apart in about two seconds.
- [own] The audit lists 5 axes (tier, plus, rift tier, affixes, slot/material, D4), only rarity visible. Options: collapse tier + plus + rift into one displayed "power" number or colour; show the affix as a one-line text on the card; or retire axes that cannot be seen (Fortunate/Favor, D10).
- [own] Axes that scale one number multiplicatively (tier x1.25, plus +15%, rift x1-2) are mutually fungible and invite "which is bigger" arithmetic. That is homework, not choice. One scalar plus one named behaviour reads better.

## 6. Shape of power across a run

- [own] Power should arrive as a rising curve with occasional spikes. Spikes (elite, boss, event) are where the player re-plans; the slope is what the stat sticks supply. A flat curve of frequent small rewards makes every fight feel identical (D1); spikes with nothing between them feel feast-or-famine.
- [own] Frequency vs size: more frequent rewards must be smaller, simpler and faster to resolve; rare rewards can be large and slow. The audit's per-fight forced pick is a large *time* cost (modal screen, tooltip reads) for a small *power* gain. Either shrink the time (auto-resolve most fights, show a card only at elites/bosses) or raise the power per pick.
- [own] Run length vs content ladder: 11 tiers when a run reaches about 2.5 (D6) means 64% of content is unseen. Sizing tiers to reach is cheaper than a pity timer. If an endless mode is ever added, it needs scaling that does not rely on new tiers (multipliers, repeating affix stacking, or a prestige-like loop), else the top of the ladder is a wall.
- Spells cap at level 9 and levels run to 40, so gear is expected to carry late power (audit f). That means the late curve currently depends on the item system that has the least identity (D2).

## 7. Party and multi-character loot

- [own] The recurring failure: the reward screen asks "which item" but the real question is "which item on whom". Two decisions in one click, with the second hidden. Audit D8 shows the worst version (auto-assign by roster order); D3 shows offers blind to squad needs.
- [own] Options, ordered by friction: (a) party-wide effects (relics are already this, audit a); (b) the reward is *for a named member* ("Odette's reward"), so the player chooses among items for one person; (c) assign on pick with a one-tap per-character target and a default of best fit; (d) bag + dossier assignment later (current, with D8 bug).
- [own] Offers should be filterable to what the squad can use (requirements, inert slots, audit D3) and should show deltas per member. The squad-wide comparison tooltip exists (audit e) and should be on the card, not behind hover.
- [own] Rotating the "owner" of the reward across fights keeps three characters equally invested without a spreadsheet; without it, loot concentrates on whoever is first.

## Tests

Score each 0 (no), 1 (partly), 2 (yes). Max 30.

| # | Test | Source / basis |
|---|---|---|
| T1 | Can the player decline a reward with no loss of agency (skip is a real option)? | Sec 4 |
| T2 | Do the offered options differ in kind (different plans), not just in size along one axis? | Sec 1 |
| T3 | Is there a real cost to each option (opportunity cost, not just "free")? | Sec 1 |
| T4 | Can the player rank two offered cards correctly in about 2 seconds without a tooltip? | Sec 5 |
| T5 | Does a card carry at most 2 power axes (one number, one named behaviour)? | Sec 5 |
| T6 | Is everything random rolled and shown before the choice (what you take is what you get)? | Sec 3 |
| T7 | Does the player have at least one lever on the roll (reroll, skip payout, targeted pick), capped? | Sec 4 |
| T8 | Is there bad-luck protection against streaks of nothing, seeded and quit-safe? | Sec 4 |
| T9 | Do frequent rewards stay cheap to resolve, and rare rewards get build-defining content? | Sec 2, 6 |
| T10 | Does at least one reward source per run change how you *play*, not only how strong you are? | Sec 2 |
| T11 | Is the content ladder sized to what a normal run reaches (little unreachable content)? | Sec 6 |
| T12 | Do offers account for the squad (usable by someone, matching empty slots)? | Sec 7 |
| T13 | Is the "on whom" decision explicit and defaulted sensibly, not by roster order? | Sec 7 |
| T14 | Does surplus or unwanted loot convert into something useful? | Sec 4 |
| T15 | Does each fight class (normal/elite/boss) visibly pay something different? | Sec 6 |

## Current system against the tests

| # | Score | Evidence |
|---|---|---|
| T1 | 0 | No skip/reroll/sell in `Take` (D1, audit c fight row). Relic draft can take nothing, but that is one moment (c). |
| T2 | 0 | Three distinct items within one tier, mostly bare stat sticks (D1, D2). |
| T3 | 0 | Pick-1-of-3 with no alternative use of the reward; the unpicked two vanish with no price attached (D1). The shop has costs, fights do not. |
| T4 | 0 | Card shows rarity and tier only; plus, rift, affixes in hover (D4). |
| T5 | 0 | Five axes (D4). |
| T6 | 2 | Offers are rolled then shown; plus never changes post-pick (D5, `ItemOfferRoll.cs:230`). Keep. |
| T7 | 1 | Shop has escalating reroll (c, shop row); fights have none; no pick lever on plus (D5). |
| T8 | 0 | No pity; luck stat Fortunate invisible (D10). Fight RNG not keyed or quit-safe (C4). |
| T9 | 0 | Frequent reward is a modal pick of small stick; mechanics only in once-per-run relic draft (D1, D2, D9). |
| T10 | 1 | Relics are build-defining, but come once per run, from the shop, or from two events (D2, c). Elite/boss relic drop does not exist (c). |
| T11 | 0 | 64% of 638 offerable items sit in tiers 4-10, rarely reached (D6). |
| T12 | 1 | Squad-wide comparison tooltip exists (e), but candidate pool ignores the squad (D3). |
| T13 | 0 | Auto-equip by roster order (D8). |
| T14 | 1 | Sell at 30% in shops only; no bag cap, no salvage; plus has no sink (D5). |
| T15 | 1 | Elites and bosses get a tier floor and plus bump; promised weapon and consumable hierarchy is dead code (D7). |

Total: 11/30. Strongest keep: T6 and the pure seedable tables (audit e). Weakest cluster: T1-T5 (choice quality and readability), which D1 and D4 describe; fixing those two moves the score most.

## Sources

- [Designing interesting decisions in games and when not to](https://www.gamedeveloper.com/design/designing-interesting-decisions-in-games-and-when-not-to-)
- [Lessons from Sid Meier](https://www.antoinebuteau.com/lessons-from-sid-meier/)
- [Randomness and Game Design (Burgun)](https://gamedeveloper.com/design/randomness-and-game-design)
- [Three types of bad randomness, and one good one (Burgun)](https://keithburgun.net/three-types-of-bad-randomness-and-one-good-one/)
- [Slay the Spire wiki: Card Rewards](https://slay-the-spire.fandom.com/wiki/Card_Rewards)
- [Hades wiki: Boons](https://hades.fandom.com/wiki/Boons?oldid=6594)
- [Making Magic: Kind Acts of Randomness](https://magic.wizards.com/en/news/making-magic/kind-acts-randomness-2009-05-04)
