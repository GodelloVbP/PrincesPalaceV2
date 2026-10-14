# Run map P3: bot report (final)

Plan: `PLAN_RUN_MAP.md` rev 5 section 8, P3. Final re-run 2026-10-14 at commit
`357227a8`, after item phases I2b-I4 (reward pages, chips decide the pages,
honing at the shop and on a Rest page). The preliminary run (before I3, Gear,
Book and Relic chips at weight 0) is superseded; its numbers survive only in
"Changed since preliminary".

## What landed

`MapView` on `RunView.Map` (`Domain/Bot/MapView.cs`, built by
`Core/Bot/BotMapView.cs`): per node ahead of the party, role, pocket
membership, anchor flag, strength 1-3, effective chip kind (Gear and Relic
chips and anchor prizes included, after the empty-pool swap to gold), rooms to
the heart on the shortest and longest route, detour rooms against the best
choice on offer, anchors on the way. `PathRule` (`Domain/Bot/PathRule.cs`)
gates by HP.

Path rules (also in the code comments):

- GreedyAggressive: greedy for prizes. Pocket, anchor and strength-3 fight
  from 50% HP up, the strongest fight first.
- GreedyDefensive: guards the party. Pocket from 75%, strength-3 fight from
  90%, never an anchor, the weakest fight first.
- ProtectTheFront: keeps the front rank alive. Pocket from 60%, strength-3
  from 80%, anchor from 90%, the weakest fight first; GreedyAggressive's room
  order.
- Lookahead2: looks two rooms ahead. Room value plus its best follow-up less
  0.3 per detour room; a fight is prize x strength less strength^2 x
  (1.25 - HP); a rest is 4 x missing HP.
- RandomLegal ignores the map (the fuzzer).

When every room on offer breaks a gate, the cheapest breach stays (pocket 1,
strength-3 fight or anchor 2), and a pocket that opens on a Rest is exempt.
Without a map the old room-type order applies unchanged.

Tooling: `tools/bot.ps1 -StartFloor n` opens the run on floor n; `pathChoice`
in `summary.json` and the plain-text table (`docs/BOT_SUMMARY_SCHEMA.md`).
Choices the shop preference or a forced event settled are not counted: they
are the batch's, not the policy's.

## Archetypes bug

`-Archetypes A,B` bound as the string "A B" (PowerShell joins an array with a
space), `Start-Process` split it into two argv entries, and the runner saw only
A. `tools/bot.ps1` now re-joins on commas. Verified: both archetypes produce
rows.

## Method

Same settings as the preliminary: one realm per run (`-DepthCap 8 -StartFloor
n`), seed 1, five archetypes. Floor 1 Fresh 1500 runs per cell, floor 10 Mid 250,
floor 30 Late 250. Batches (uncommitted) `reports/bot/20261014-110656` (floor 1),
`-111016` (floor 10), `-111246` (floor 30). HP bands are party HP at the choice:
`<50`, `50-79`, `>=80`. Pocket take = pocket room taken over choices offering a
pocket beside a spine room. Strength-3 take = strength-3 non-anchor fight taken
over choices offering one beside any other room. Cells show `rate
(taken/offered)`.

## Pocket-take rate by HP band

| Floor | Archetype | <50 | 50-79 | >=80 |
|---|---|---|---|---|
| 1 | all | 35% (114/326) | 35% (537/1541) | 33% (1396/4205) |
| 1 | GreedyAggressive | 0% (0/43) | 22% (60/275) | 23% (207/907) |
| 1 | GreedyDefensive | 0% (0/39) | 2% (4/253) | 5% (42/933) |
| 1 | Lookahead2 | 90% (36/40) | 82% (226/277) | 79% (719/908) |
| 1 | ProtectTheFront | 0% (0/34) | 4% (12/274) | 15% (140/917) |
| 1 | RandomLegal | 46% (78/170) | 51% (235/462) | 53% (288/540) |
| 10 | all | 40% (260/647) | 48% (54/113) | 47% (122/259) |
| 10 | GreedyAggressive | 40% (67/166) | 50% (10/20) | 56% (28/50) |
| 10 | GreedyDefensive | 39% (63/161) | 53% (16/30) | 60% (38/63) |
| 10 | Lookahead2 | 38% (50/133) | 40% (12/30) | 23% (12/53) |
| 10 | ProtectTheFront | 42% (72/172) | 67% (10/15) | 56% (28/50) |
| 10 | RandomLegal | 53% (8/15) | 33% (6/18) | 37% (16/43) |
| 30 | all | no choices | 36% (32/88) | 57% (88/155) |
| 30 | GreedyAggressive | no choices | 64% (9/14) | 83% (20/24) |
| 30 | GreedyDefensive | no choices | 20% (4/20) | 69% (29/42) |
| 30 | Lookahead2 | no choices | 16% (3/19) | 18% (5/28) |
| 30 | ProtectTheFront | no choices | 57% (8/14) | 85% (22/26) |
| 30 | RandomLegal | no choices | 38% (8/21) | 34% (12/35) |

## Strength-3 take rate by HP band

| Floor | Archetype | <50 | 50-79 | >=80 |
|---|---|---|---|---|
| 1 | all | 52% (13/25) | 73% (61/84) | 69% (82/118) |
| 1 | GreedyAggressive | 100% (4/4) | 100% (18/18) | 100% (25/25) |
| 1 | GreedyDefensive | 67% (2/3) | 94% (15/16) | 93% (26/28) |
| 1 | Lookahead2 | 0% (0/5) | 8% (1/13) | 14% (4/29) |
| 1 | ProtectTheFront | 75% (3/4) | 88% (15/17) | 96% (25/26) |
| 1 | RandomLegal | 44% (4/9) | 60% (12/20) | 20% (2/10) |
| 10 | all | 38% (198/526) | 52% (50/97) | 69% (316/456) |
| 10 | GreedyAggressive | 38% (52/137) | 67% (12/18) | 82% (73/89) |
| 10 | GreedyDefensive | 39% (51/132) | 39% (11/28) | 74% (75/101) |
| 10 | Lookahead2 | 39% (41/105) | 39% (9/23) | 51% (48/95) |
| 10 | ProtectTheFront | 36% (51/141) | 57% (8/14) | 82% (73/89) |
| 10 | RandomLegal | 27% (3/11) | 71% (10/14) | 57% (47/82) |
| 30 | all | no choices | 53% (31/58) | 69% (331/479) |
| 30 | GreedyAggressive | no choices | 67% (6/9) | 92% (84/91) |
| 30 | GreedyDefensive | no choices | 64% (9/14) | 86% (92/107) |
| 30 | Lookahead2 | no choices | 64% (7/11) | 24% (22/91) |
| 30 | ProtectTheFront | no choices | 56% (5/9) | 90% (84/93) |
| 30 | RandomLegal | no choices | 27% (4/15) | 51% (49/97) |

## Prize mix (what the pages paid)

Counted from the rooms in `runs.jsonl` (`pages`, `encounterClass`), all shards
of each batch. A gold or consumable chip leaves no page, so only Gear, Relic and
Book prizes show here.

Floor 1 Fresh, 7500 runs, 64,896 rooms:

| Page source | Pages | Cards taken | Hones taken instead |
|---|---|---|---|
| normal_gear | 8225 | 8211 | 14 |
| anchor_gear | 2370 | 2369 | 1 |
| anchor_relic | 1011 | 1011 | 0 |
| boss_gear | 6009 | 8190 | 148 |
| boss_relic | 6009 | 6914 | 0 |

- A plain Fight pays a page in 8225 of 41,735 rooms (19.7%), matching the Item
  weight 20 of 100 in `RealmProfile` (Gold 40, Consumable 30, Item 20, Book 10,
  Relic 0).
- Elite rooms: 3381 pages in 3412 rooms, 2370 Gear and 1011 Relic (70 / 30).
- Boss: every boss win pays a gear page and a relic page (6009 of each).
- Hone-instead is rare: 163 of 18,933 gear choices (0.9%); 148 of the 163 are
  on the boss page, which offers more choices (8338 over 6009 pages).
- No page was left with unused hones or unused gold (both 0 everywhere): the bot
  spends every choice.
- Shops (1768 visits): 805 gear, 159 relic, 79 book, 138 reroll, 66 sell.
  Median arrival gold 52.

Floor 10 Mid, 1250 runs, 3138 rooms: 192 normal_gear pages in 1899 Fights (10%),
77 elite pages in 207 elite rooms (50 anchor_gear, 27 anchor_relic), 9 boss
pages (9 of 45 boss fights won), 2 boss-page hones. Rewards are huge: a won
fight pays a median 4240 gold (max 48,838), shop arrival gold has a median of
6000, a gear card costs 12,960. Shop visits 85: 24 gear, 11 book, 4 relic, 10
reroll. (The growth curve is parked by the owner; numbers only.)

Floor 30 Late, 1250 runs, 1593 rooms: no reward page of any kind (no fight is
won; every run dies in the first one or two rooms, steps 233 to 234). Shop gear
costs 154,945,108 against 0 gold on arrival, so all 19 shop visits are a Leave.

Rest rooms: 0 at floor 1 (0 of 64,896 rooms), 59 at floor 10 (1.9%), 17 at floor
30 (1.1%). The bot always takes the heal (`RestHeal`, the I4 call), so hones at
a Rest are 0 by construction and the Rest rows say nothing about hone appetite;
it also skews any "hones per run" figure low. The bot never calls
`RunOrchestrator.BuyHone` either: shop hones bought are 0 across all 1872
shop visits.

## Verdict

Pass for the path rule and the pages. The three greedy archetypes still take 0%
of pockets below 50% at floor 1 and 2-23% above, and the strength-3 rate climbs
with HP at floors 10 and 30 (overall 38 to 69% and 53 to 69%). RandomLegal is
flat by design. One policy row moved and needs the owner's eye (next two
sections).

Weak spots, mostly unchanged:

- Floor 30 has no choice below 50% HP, so only two bands exist there.
- Floor 1 strength-3 take is 93-100% for the greedy archetypes: few
  strength-3 fights stand beside a cheaper rival.
- `TooManyCommands`: 33 at floor 10 (1250 runs) and 8 at floor 30. A fight
  stalls past 2000 commands. Parked (enemy HP and reward growth curve).
- Fresh bots at floor 1 meet few `<50` choices (326 of 6072).

## Changed since preliminary

Pages and chips are real: the bot now takes Gear, Relic and Book prizes and
plays two-page boss rewards (section above).

| Metric | Preliminary | Final |
|---|---|---|
| Floor 1 pocket take, all, <50 / 50-79 / >=80 | 26 / 23 / 16% | 35 / 35 / 33% |
| Floor 1 Lookahead2 pocket take, same bands | 28 / 11 / 1% | 90 / 82 / 79% |
| Floor 1 Lookahead2 strength-3 take, same bands | 50 / 62 / 87% | 0 / 8 / 14% |
| Floor 1 strength-3 take, all | 67 / 73 / 86% | 52 / 73 / 69% |
| Floor 10 pocket take, all | 36 / 50 / 46% | 40 / 48 / 47% |
| Floor 10 strength-3 take, all | 41 / 52 / 74% | 38 / 52 / 69% |
| Floor 30 pocket take, all | none / 38 / 53% | none / 36 / 57% |
| Floor 30 strength-3 take, all | none / 55 / 81% | none / 53 / 69% |
| Floor 30 Lookahead2 strength-3 take, >=80 | 85% | 24% |
| `TooManyCommands` floor 10 / 30 | 48 / 8 | 33 / 8 |

Only Lookahead2 moved much, and at floor 1 it flipped: it now reaches for
pockets at every HP and avoids strength-3 fights, the reverse of the intended
shape (pocket value falls with HP, strength-3 taken from high HP). The same
shift shows at floor 30 (strength-3 at >=80 from 85% to 24%).

## Suspected bugs

1. Lookahead2 inversion (above): a policy-valuation fault rather than balance.
   Likely cause: `BotMapView`'s effective chip kind now returns Gear or Book
   for plain fights and Lookahead2's prize value for those kinds outweighs the
   strength penalty, so a pocket's one or two plain fights beat any single
   fight at every HP. Not diagnosed; policy is `Domain/Bot/Lookahead2Policy.cs`.
2. Honing is never exercised by the bot: `BuyHone` is not called from
   `BotRunDriver` (0 of 1872 shop visits) and `RestHone` is not called (always
   heals; 0 of 76 Rests). A bot gap, not a game bug; the honing price economy
   and the Rest-or-hone choice are unmeasured.
3. Floor 1 never offers a Rest room (0 of 64,896 rooms). `RealmMapGenerator`
   allows a Rest only on a fork sibling and `RealmProfile` gives floor 1 few or
   no forks (not checked beyond that). If realm 1 should offer a Rest this is a
   generator gap; if not, no action.
4. Floor 1 has no 2-or-more-anchor bucket in `bossByAnchors` (`anchors = 1 +
   floor/3` = 1). Expected; noted so an empty bucket is not read as a bug.
5. Never offered or picked in 7500 runs: relics `bellwethers_bell`,
   `forest_wardens_tooth`, `kinship`, `rampaging_bulls_horn`,
   `toll_of_the_flock` (presumably event-only); enemies `bog_witch`, `golem`,
   `treant`. No chip kind or page source is unpaid (Relic chips from plain
   fights are weight 0 by design).
6. No metric except thin cells (Lookahead2 floor-1 strength-3 `<50`, 0/5) sits
   at exactly 0 or 100%, other than the greedy archetypes' designed 0% pocket
   rate below 50%.

No invariant bug other than `TooManyCommands`.

## Tuning done

Only the strength thresholds. At the sheet's `T1` 0.34 / `T2` 0.61 the starting
squad (weight 5760, not the sheet's 4560) read floor 1 as 79 / 21 / 0: no
strength-3 fight existed, so the first batch offered none at floor 1. Rescaled
per the sheet's own rule (`T = 0.34 x 4560 / 5760`): `T1` 0.27, `T2` 0.48,
`ReferencePartyWeight` 5760. Floor-1 mix is now 60 / 35 / 4 over seeds 1-100,
matching the sheet's 64 / 32 / 4. Prize multipliers, pocket length, shop
chance and anchor count were not touched. This re-run changed no tuning.

## P5 re-run: boss-fight win rate by heart anchors

Same batches. `bossByAnchors` in `summary.json`; anchors counted at the start of
the boss fight (fielded plus queued); a fight stopped by the runner is counted
apart. Won/fights.

Floor 10, Mid, 1250 runs:

| Anchors at the start | All | GreedyAggressive | GreedyDefensive | Lookahead2 | ProtectTheFront | RandomLegal |
|---|---|---|---|---|---|---|
| 0 | no fights | none | none | none | none | none |
| 1 | 1/2 (50%) | none | none | 1/2 | none | none |
| 2 | 0/9 (0%) | 0/2 | 0/1 | 0/4 | 0/2 | none |
| 3+ | 8/34 (24%) | 2/10 | 3/10 | 1/4 | 2/10 | none |

Floor 1, Fresh, 7500 runs (one anchor on the map, so buckets 2 and 3+ are
empty): 0 anchors left 2747/3223 (85%), 1 anchor left 3262/3946 (83%). At floor
1 the heart anchor costs about two points.

Floor 30, Late: no boss fight is reached; every run dies by step 233 to 234.

Reading against the target (win rate falls as anchors rise, about 0% at 2): at
floor 10 the 2-anchor bucket is 0 of 9, on target, but 3+ anchors win 24% (8
of 34), above the 2 bucket. Samples are 2, 9 and 34 fights; no tuning call on
`HeartAnchors.HealthPermille` (500) is supported, and the literal is unchanged.
33 floor-10 runs logged `TooManyCommands`.
