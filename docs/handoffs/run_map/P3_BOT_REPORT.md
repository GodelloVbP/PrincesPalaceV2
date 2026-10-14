# Run map P3: bot report

Plan: `PLAN_RUN_MAP.md` rev 5 section 8, P3. Preliminary: Gear, Book and Relic
chips are weight 0 until item phase I3, so re-run after I3 (and after P5 for
the boss-fight win rate by anchors left).

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

One realm per run (`-DepthCap 8 -StartFloor n`), seed 1, five archetypes.
Floor 1 Fresh 1500 runs per cell, floor 10 Mid 250, floor 30 Late 250 (a Fresh
party at floor 30 only measures how fast it dies). HP bands are party HP at the
choice: `<50`, `50-79`, `>=80`. Pocket take = pocket room taken over choices
offering a pocket beside a spine room. Strength-3 take = strength-3 non-anchor
fight taken over choices offering one beside any other room. Cells show
`rate (taken/offered)`.

## Pocket-take rate by HP band

| Floor | Archetype | <50 | 50-79 | >=80 |
|---|---|---|---|---|
| 1 | all | 26% (77/301) | 23% (334/1440) | 16% (701/4337) |
| 1 | GreedyAggressive | 0% (0/39) | 20% (51/249) | 22% (207/937) |
| 1 | GreedyDefensive | 0% (0/33) | 1% (2/204) | 5% (49/988) |
| 1 | Lookahead2 | 28% (13/46) | 11% (27/252) | 1% (12/927) |
| 1 | ProtectTheFront | 0% (0/29) | 4% (11/258) | 15% (141/938) |
| 1 | RandomLegal | 42% (64/154) | 51% (243/477) | 53% (292/547) |
| 10 | all | 36% (239/668) | 50% (56/113) | 46% (127/275) |
| 10 | GreedyAggressive | 35% (59/169) | 75% (15/20) | 60% (30/50) |
| 10 | GreedyDefensive | 33% (52/158) | 44% (12/27) | 63% (39/62) |
| 10 | Lookahead2 | 36% (50/137) | 39% (12/31) | 18% (12/68) |
| 10 | ProtectTheFront | 37% (70/189) | 69% (11/16) | 59% (30/51) |
| 10 | RandomLegal | 53% (8/15) | 32% (6/19) | 36% (16/44) |
| 30 | all | no choices | 38% (36/95) | 53% (90/170) |
| 30 | GreedyAggressive | no choices | 57% (8/14) | 88% (21/24) |
| 30 | GreedyDefensive | no choices | 25% (5/20) | 73% (30/41) |
| 30 | Lookahead2 | no choices | 27% (7/26) | 13% (6/47) |
| 30 | ProtectTheFront | no choices | 57% (8/14) | 88% (21/24) |
| 30 | RandomLegal | no choices | 38% (8/21) | 35% (12/34) |

## Strength-3 take rate by HP band

| Floor | Archetype | <50 | 50-79 | >=80 |
|---|---|---|---|---|
| 1 | all | 67% (14/21) | 73% (59/81) | 86% (114/133) |
| 1 | GreedyAggressive | 100% (3/3) | 94% (16/17) | 100% (28/28) |
| 1 | GreedyDefensive | 100% (2/2) | 86% (12/14) | 88% (28/32) |
| 1 | Lookahead2 | 50% (2/4) | 62% (8/13) | 87% (27/31) |
| 1 | ProtectTheFront | 100% (2/2) | 76% (13/17) | 93% (27/29) |
| 1 | RandomLegal | 50% (5/10) | 50% (10/20) | 31% (4/13) |
| 10 | all | 41% (218/526) | 52% (44/84) | 74% (343/462) |
| 10 | GreedyAggressive | 40% (53/132) | 44% (7/16) | 81% (73/90) |
| 10 | GreedyDefensive | 41% (51/123) | 43% (9/21) | 75% (75/100) |
| 10 | Lookahead2 | 45% (48/106) | 61% (14/23) | 77% (77/100) |
| 10 | ProtectTheFront | 39% (59/150) | 58% (7/12) | 81% (74/91) |
| 10 | RandomLegal | 47% (7/15) | 58% (7/12) | 54% (44/81) |
| 30 | all | no choices | 55% (39/71) | 81% (390/479) |
| 30 | GreedyAggressive | no choices | 55% (6/11) | 92% (82/89) |
| 30 | GreedyDefensive | no choices | 67% (10/15) | 88% (90/102) |
| 30 | Lookahead2 | no choices | 61% (11/18) | 85% (88/104) |
| 30 | ProtectTheFront | no choices | 55% (6/11) | 92% (82/89) |
| 30 | RandomLegal | no choices | 38% (6/16) | 51% (48/95) |

## Verdict

Pass, narrowly. No cell is stuck at 0% or 100% across bands and the policy
cells move with HP: the three greedy archetypes take 0% of pockets below 50% at
floor 1 and 1-22% above, and the strength-3 rate climbs with HP at floors 10
and 30 (overall 41 to 74% and 55 to 81%). RandomLegal is flat by design.

Weak spots:

- Floor 1 pocket rate overall is 26 / 23 / 16%: it falls with HP because
  Lookahead2 values a high-HP pocket low (1% at >=80) and RandomLegal is a
  large share of the low bands. Floor 1 strength-3 below 50% has 21 choices;
  the other bands carry the signal.
- Floor 30 has no choice below 50% HP (the Late party heals between realms and
  rarely drops), so only two bands exist there.
- Floor 1 strength-3 take is 86-100% for the greedy archetypes at high HP:
  few strength-3 fights stand beside a rival that is cheaper under the rule.
  The band signal at floor 1 is the Lookahead2 row and the overall 67 to 86%.
- Floors 10 and 30 report `TooManyCommands` hits (48 and 8, every archetype):
  a fight stalls past 2000 commands. Not map related; `-StartFloor` reaches
  fights the ordinary 40-step batches do not. Open for the fight owner.
- Fresh bots at floor 1 meet no `<50` choice often (301 of 6078): the party is
  rarely hurt that far in one realm.

## Tuning done

Only the strength thresholds. At the sheet's `T1` 0.34 / `T2` 0.61 the starting
squad (weight 5760, not the sheet's 4560) read floor 1 as 79 / 21 / 0: no
strength-3 fight existed, so the first batch offered none at floor 1. Rescaled
per the sheet's own rule (`T = 0.34 x 4560 / 5760`): `T1` 0.27, `T2` 0.48,
`ReferencePartyWeight` 5760. Floor-1 mix is now 60 / 35 / 4 over seeds 1-100,
matching the sheet's 64 / 32 / 4. Prize multipliers, pocket length, shop
chance and anchor count were not touched.

## P5 re-run: boss-fight win rate by heart anchors

Run 2026-10-14 after the P5 leader rule landed (commit `485bcd4e`, plus the
`endReason` field). `tools/bot.ps1 -Runs 250 -DepthCap 8 -StartFloor n -Profiles
Mid|Late`, seed 1, five archetypes. Anchors are counted at the start of the boss
fight (fielded plus queued, `roster.Anchors.Count`). That is the number of
anchors the run left unslain on the map; the leader rule kills the rest with the
boss. `bossByAnchors` in `summary.json`; a fight stopped by the runner (a stall,
`endReason` None) is counted apart so it cannot read as a lost boss. `-Runs` was
read from a clean `git archive` of `485bcd4e` because another session's
uncommitted map screen did not compile at the time.

Floor 10, Mid, 1250 runs (won/fights):

| Anchors at the start | All | GreedyAggressive | GreedyDefensive | Lookahead2 | ProtectTheFront | RandomLegal |
|---|---|---|---|---|---|---|
| 0 | no fights | none | none | none | none | none |
| 1 | no fights | none | none | none | none | none |
| 2 | 6/9 (67%) | 2/3 | 1/2 | 1/1 | 2/3 | none |
| 3+ | 1/43 (2%), 1 stalled | 0/12 | 1/10 | 0/9, 1 stalled | 0/12 | none |

Floor 30 (Late and Mid, 250 runs per cell each): no boss fight is reached. Every
run is doomed by step 233 to 234, one or two rooms into the realm, in every
archetype and both profiles, so the table is empty. That is the floor-30 party
being outclassed, not the anchors.

Reading against the target (win rate falls as anchors rise, without reaching
about 0% at 2): the direction holds, 67% at 2 anchors and 2% at 3 or more, but
the cliff is at 3, not 2. Bucket 2 is nine fights and 0 and 1 have none: with
three to six anchors on the map and the bot taking most elites, the heart has
three or more waiting in nearly every run (43 of 52 fights). Three data gaps
stop a tuning call on `HeartAnchors.HealthPermille` (500): too few fights at 2,
none at 0 or 1, and none at floor 30. A floor-1 batch (which reaches the heart
more often) and a profile that survives floor 30 would fill them; the literal is
the orchestrator's call and is unchanged. 44 runs at floor 10 logged
`TooManyCommands`; only one of those was a boss fight, and the stall is being
diagnosed separately.
