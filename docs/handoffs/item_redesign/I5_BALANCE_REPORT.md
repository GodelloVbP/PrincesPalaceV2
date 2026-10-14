# I5 balance report: gold income

Plan: `docs/PLAN_ITEMS.md` I5 row, section 1.6. Landed 2026-10-14. One number
moved: `VictoryRewards.GoldIncomePermille` 1000 -> 150. Everything else the row
names was measured where the bot allows and left alone (below).

## Metric and method

Realm gold income / `ScaleReward(1, realm start step)` (the plan's definition,
column "plan"), plus the same income deflated room by room to step-0 gold
(column "step-0"). Income per room = change in gold on arrival at the next
room + gold spent in the room; the last room adds its fight payout. So it
counts fight gold, chip gold, Treasure, sales and payouts, not fight gold
alone. Only realms whose boss was won; RandomLegal excluded (a fuzzer, not a
route). Full descents: `tools/bot.ps1 -Runs 200 -Seed S -Shards 4 -DepthCap 80
-StartFloor 1 -Profiles Fresh`, four archetypes, ~800 runs a batch; n is the
realms that reached the floor. Seeds 1..200 and 5000..5199 (`-Seed 2` only
shifts the window by one, so it is not an independent seed).

Gold chips are authored net (`RealmPrize.GoldFor`) and the permille does not
touch them: at 250 fight payouts were 53-67% of income, the rest chips, sales
and Treasure.

## Before / after (median plan metric by floor)

| Floor | n | 1000 (before) | 150 seed 1 | 150 seed 5000 |
|---|---|---|---|---|
| 1 | 734 | 254.9 | 55.8 | 59.5 |
| 2 | 693 | 357.8 | 106.4 | 106.4 |
| 3 | 660 | 372.4 | 110.7 | 112.0 |
| 4 | 594 | 397.7 | 124.5 | 127.2 |
| 5 | 531 | 427.9 | 130.8 | 138.2 |
| 6 | 437 | 459.2 | 145.7 | 152.4 |
| 7 | 375 | 461.9 | 154.8 | 151.6 |
| 8 | 355 | 541.5 | 174.9 | 180.4 |
| 9 | 343 | 546.5 | 181.4 | 178.0 |
| 10 | 339 | 570.6 | 189.3 | 193.1 |
| **Floors 1-10 pooled median** | 5061 / 4969 / 4989 | **406.5** | **123.9** | **126.9** |
| Median of floor medians | | 443.5 | 138.3 | 144.9 |

Step-0 view (before / after seed 1): floor 1 179.8 / 40.2, floor 5 313.7 /
100.7, floor 10 421.3 / 144.6; pooled not tabulated.

The plan's start estimate (~260) matched floor 1 (254.9). Sweep on floor 1
alone, 1500 runs: 1000 -> 243, 500 -> 129, 450 -> 119. On the full descent
450 -> pooled 224, 250 -> 157, 150 -> 124. Seeds agree within 3.

## The pooled band holds; the per-floor band does not

Pooled over floors 1-10 the median realm pays 124-127 step-0 gold: in band.
Per floor it climbs 56 -> 190 (3.4x) because realms get longer and richer in
chips as the floors go on, and one flat multiplier cannot flatten that. Floors
2-5 sit inside 100-140; floor 1 is under and floors 6-10 are over. Picking the
floor-1 figure instead (120 at about 450) would leave floors 6-10 at 300-420.

## Numbers changed

| Number | Before | After | Why |
|---|---|---|---|
| `VictoryRewards.GoldIncomePermille` | 1000 | 150 | pooled median realm 406 -> 124-127; pinned by `FightRewardsTests.GoldIncome_IsCutToFifteenPercent` (literals 150, Income(100) = 15, Income(17) = 3) |

Nothing else changed: no chip weight, *p*, tier step, Favor dial, hone price,
`MaxStep`, shop floor or `HeartAnchors.HealthPermille`.

## Dials left alone, and what was seen

- Chip weights (fight Gold 40 / Consumable 30 / Gear 20 / Book 10 / Relic 0),
  quality *p*, tier steps: no target in the plan to tune against, and the
  gold fix does not need them. Pages per run (non-fuzzer, floors 1-10): 26.5,
  33.9 choices; unused gold and unused hones per page 0.0 (the bot spends
  every choice, so the unused-choice rate is 0 and says nothing about
  players).
- Tier steps: offered tier counts, 800 runs: tier 0 4561, 1 7473, 2 9286, 3
  9341, 4 9271, 5 4771, 6 2506, 7 990, 8 421, 9 160, 10 31, 11 17. Mean
  modifiers per offered item 1.23 (traits per member not split out: the run
  trace has no per-member count). Legendary pages per run: not separable in
  the trace (no rarity on `pages`).
- Favor at the first floor-10 room: median 0, mean 0.6 (351 runs). Favor is
  not accruing to 10 under the bot, or is spent first; I did not trace why.
- Relics held at death, by last floor reached: floors 0-4 median 3, 5-9
  median 9, floor 10 median 20. Floors 20 and 30 are out of reach: deep
  floors stall with TooManyCommands (known, parked).
- `HeartAnchors.HealthPermille` (500): boss win rate over the descent is flat
  with anchors, 0 anchors 88.5-90%, 1 90%, 2 88.7-89%, 3+ 90-91% (batch
  `20261014-121732`; gold cut did not move it). The P5 target (falls toward 0
  at 2+) holds only in the floor-10 Mid sample (0/9 at 2, 8/34 at 3+); across
  floors 1-10 anchors do not bite. No change: that is a difficulty call.
- Deep-power split (gear / levels / spells / mechanics through `ToCombatant`
  at steps 40, 80, 160): not measured. It needs a harness, not a bot batch,
  and floors that deep do not run.
- Hone price (`21 x (1 + 0.35 x plus)`), shop floor (AUDIT #196): with income
  at 15% a hone costs about 1/6 of a realm at median; no batch evidence either
  way, left as authored.

## Owner calls

1. Per-floor shape. Pooled median is in band; floor 1 pays ~58, floor 10
   ~190. Flattening it means changing growth per step or realm length, which
   you parked. Alternatives that stay inside scope: lower `GoldChipBase` (5,
   not touched by the permille) if floors 6-10 should read tighter, or accept
   the slope as "later realms pay more".
2. Floor 1 at ~58 buys one cheap gear card or a hone, not the plan's gear +
   hone + part of a relic. Raising the permille to ~450 fixes floor 1 (119)
   and puts the pooled median at 224. Your pick of anchor.
3. Heart anchors do not lower the boss win rate on floors 1-10 (flat ~90%).
   If anchors should cost something there, `HealthPermille` or the count needs
   a decision before it is tuned.
4. Favor held by floor 10 reads 0; whether Favor should survive to depth is a
   design question before a dial question.
5. Metric defined by the plan counts all gold, not only fight gold; the
   permille cuts only fight and Treasure gold. If chip gold should be cut too,
   that is a separate lever.
