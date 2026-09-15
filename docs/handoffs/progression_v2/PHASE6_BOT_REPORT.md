# Phase 6 bot report — career simulation, stuck player, Bjorn's fury tiers, downed pay, real time

Written against `451ea993` (main, branch `prismatic-orb`), against the BUILT
content now on the branch (40-level tracks per `PLAN_PROGRESSION_V2.md` §4,
25 permille experience, the section-3 per-level cost table). Answers
`PLAN_PROGRESSION_V2.md` §7 phase 6's four measurable items (full suite and
owner sign-offs are out of scope for a bot report). Method follows
`PHASE1_BOT_REPORT.md`'s precedent: every number below is read from a batch's
`summary.json` / `runs.jsonl` / `traces.jsonl`, every command is the exact
one run, nothing under `Assets/` or `tools/` was edited.

## 0. The one batch this report is built from

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 24 -Seed 1 \
  -Archetypes RandomLegal,GreedyAggressive,GreedyDefensive,Lookahead2,ProtectTheFront \
  -Profiles Fresh,Mid,Late -DepthCap 80 -Shards 1
```

One foreground call, 502.6s (~8m23s), all five archetypes crossed with all
three profiles at once (`-Archetypes` x `-Profiles` is a full cross product
per `BalanceBotRunner.RunBatchCore`) — 15 cells x 24 runs = 360 runs, seeds
1-24 reused identically across every cell. `-Shards 1` deliberately: the
`-Bot1..-BotN` copies `tools/bot.ps1`'s own header describes (lines 28-39)
do not exist yet on this machine, and creating one pays a 10-20 minute first
import that risks the 600s foreground budget for no benefit at this run
count (`-TestRunner2` already exists and is unlocked). Confirmed from the
header: a sharded run would create `C:\Games\Prince's Palace-v2-Bot1`..`-BotN`
and robocopy `Assets`/`Packages`/`ProjectSettings` from this source tree into
each (`tools/bot.ps1` lines 120-127, 185-198) — not needed for this batch.

Result: `reports/bot/20260915-233830/` (gitignored, nothing under it
committed). `summary.json.batch`: `commitSha "451ea993"`, `depthCapSteps 80`,
`shards 1`. **0 bugs, 0 determinism mismatches** (45 of 360 runs replayed per
the default 10% `-ReplayShare`, `determinism.mismatches: []`).

---

## 1. Career simulation

**The bot does not support a career.** Checked both flags the brief named:

- `-Profiles` (`Fresh`/`Mid`/`Late`) selects which of three **presets**
  `ProfilePresets.Build` constructs from scratch for that run — level 1 /
  20 / 60, with `Character.ClaimTrackRewards` called immediately up to that
  level, stat points spent and talents kindled by the archetype's own
  `IRunPolicy` (`ProfilePresets.cs:123-174`). It is not a save slot and
  carries nothing from a previous run.
- `-InMemorySaves` (`-botInMemorySaves`) only chooses whether the run's save
  writes to a throwaway disk path or stays in RAM — a performance /
  equivalence toggle, not persistence. Every single call to
  `BotRunDriver.PlayRun` calls `SaveSystem.ClearMemory()` both before
  building the profile and again in `finally` after the run ends
  (`BotRunDriver.cs:208-209, 233-235`), specifically so nothing leaks
  between runs. `RunBatchCore`'s run loop (`BalanceBotRunner.cs:282-325`)
  calls `PlayRun` once per `(profile, archetype, seed)` with no save handle
  threaded between iterations.

So every one of the 360 runs in the batch above is an **independent single
life**, seeded and reset to its profile's exact preset state before it
starts. There is no flag, in this build, that carries `Character.level`/`exp`
from one `-botRuns` iteration into the next the way the real game's
`RunManager.EndRun` leaves `level`/`exp` on the save (`PLAN_BALANCE_BOT.md`
F5) while clearing gear and stockpile. Building that would mean writing a
new driver loop outside `BalanceBotRunner`/`BotRunDriver` (a change under
`Assets/`), which this task's constraints rule out. Per the brief's own
fallback clause, **hand-carrying a save file between 24 separate `-Runs 1`
invocations was not attempted** — it is exactly the "not acceptable"
manual-carry path the brief names, and would also be far less reliable than
a proper driver change (each invocation reboots Unity).

**Best available: 24 independent single-life runs per archetype on the
`Fresh` preset** (level 1, 0 XP, matching the plan's own "run 1" starting
condition exactly), `-DepthCap 80` so a run can reach leg 10. Auto-collection
was checked directly: **yes, the bot collects automatically, within a single
life.** `BotRunDriver.cs:1133` calls
`character.ClaimTrackRewards(RewardTracks.For(character), character.level)`
after every won fight, and `SpendEveryPoint`/`BuyTalents` immediately spend
what that claims. So every level-up node landed by a run's own XP is
collected and active for the rest of that same run — "earned" and "active"
are the same thing *within one life* here. They are not the same thing
*across* the 24 lives: run 2 does not start with what run 1 collected,
because run 2 is a fresh `Fresh` preset again.

### Per-run table (seeds 1-24, Fresh profile, DepthCap 80)

`levelAtDeath` is `RunTrace.LevelAtDeath`, the **fielded squad's total**
level summed across Shawn+Bjorn+Odette (the schema's own field, not a
per-character read); "≈/char" divides by 3 as a rough per-character estimate,
valid to the extent the squad stayed evenly leveled (RewardApplier pays every
non-downed fielded character the *full* amount per fight, so this is exact
when nobody was downed and an undercount for whichever character absorbed
more downs — see §4).

**RandomLegal** — median depth 14 (leg 2), p10 8 / p90 23, capped 0/24:

| seed | depth (leg) | level (sum / ≈char) | seed | depth (leg) | level (sum / ≈char) |
|---|---|---|---|---|---|
| 1 | 16 (2) | 12 / 4.0 | 13 | 23 (3) | 15 / 5.0 |
| 2 | 16 (2) | 15 / 5.0 | 14 | 23 (3) | 18 / 6.0 |
| 3 | 8 (1) | 9 / 3.0 | 15 | 8 (1) | 6 / 2.0 |
| 4 | 16 (2) | 12 / 4.0 | 16 | 13 (2) | 12 / 4.0 |
| 5 | 13 (2) | 12 / 4.0 | 17 | 8 (1) | 6 / 2.0 |
| 6 | 16 (2) | 12 / 4.0 | 18 | 8 (1) | 6 / 2.0 |
| 7 | 8 (1) | 9 / 3.0 | 19 | 16 (2) | 12 / 4.0 |
| 8 | 24 (3) | 18 / 6.0 | 20 | 24 (3) | 18 / 6.0 |
| 9 | 14 (2) | 12 / 4.0 | 21 | 8 (1) | 6 / 2.0 |
| 10 | 12 (2) | 9 / 3.0 | 22 | 13 (2) | 12 / 4.0 |
| 11 | 8 (1) | 6 / 2.0 | 23 | 14 (2) | 12 / 4.0 |
| 12 | 14 (2) | 12 / 4.0 | 24 | 16 (2) | 12 / 4.0 |

**GreedyAggressive** — median 32 (leg 4), p10 24 / p90 44, capped 1/24 (seed 13):

| seed | depth (leg) | level (sum/≈char) | seed | depth (leg) | level (sum/≈char) |
|---|---|---|---|---|---|
| 1 | 32 (4) | 21/7.0 | 13 | **80 (10, capped)** | 42/14.0 |
| 2 | 24 (3) | 18/6.0 | 14 | 24 (3) | 15/5.0 |
| 3 | 48 (6) | 30/10.0 | 15 | 28 (4) | 18/6.0 |
| 4 | 32 (4) | 21/7.0 | 16 | 29 (4) | 21/7.0 |
| 5 | 40 (5) | 27/9.0 | 17 | 24 (3) | 18/6.0 |
| 6 | 32 (4) | 21/7.0 | 18 | 32 (4) | 21/7.0 |
| 7 | 24 (3) | 18/6.0 | 19 | 32 (4) | 24/8.0 |
| 8 | 32 (4) | 21/7.0 | 20 | 24 (3) | 15/5.0 |
| 9 | 40 (5) | 27/9.0 | 21 | 24 (3) | 18/6.0 |
| 10 | 24 (3) | 15/5.0 | 22 | 40 (5) | 27/9.0 |
| 11 | 32 (4) | 21/7.0 | 23 | 24 (3) | 18/6.0 |
| 12 | 44 (6) | 27/9.0 | 24 | 37 (5) | 27/9.0 |

**GreedyDefensive, Lookahead2, ProtectTheFront** — depth summary only (full
seed table omitted for space; `runs.jsonl` in the batch dir has every row):

| archetype | median depth (leg) | p10/p90 | capped |
|---|---|---|---|
| GreedyDefensive | 32 (4) | 16/48 | 0/24 |
| Lookahead2 | **80 (10)** | 40/80 | **16/24** |
| ProtectTheFront | 28 (4) | 24/40 | 1/24 (seed 13, level 42/14.0) |

### Comparison against the plan's table (§3)

The plan's career table is **cumulative** — level 5 "after run 1, dies leg
2" already assumes nothing (run 1 truly starts at zero), but level 8 "after
run 2" assumes run 1's banked XP too, and so on. Only the **first row is a
fair one-to-one comparison** with these bot runs, because it is the only row
where the plan's own starting condition (level 1, 0 XP) matches what
`ProfilePresets.Build` gives every one of these 360 runs.

- **Plan: run 1 dies leg 2 → level 5** (150 of 300 to next).
  **Bot, single life, dies at leg 2 (depth 9-16):** RandomLegal's 11 leg-2
  deaths land at levels 4 or 5 per character (median 4.0, six of eleven at
  exactly 5.0-adjacent 15÷3). **Within one level of the plan's row**, on the
  weakest archetype, with no cross-run banking at all.
- Every later row in the plan's table (run 2 dies leg 3 → level 8; run 3
  dies leg 5 → level 11; run 4, first deep run → level 15) assumes 1-3 prior
  runs' worth of XP already banked on the character. **A single bot life
  cannot be compared against these rows** — a life that dies at leg 5 in
  this batch has earned only what leg 1-5 alone pays, not leg 1-5 stacked on
  top of two previous descents' XP, and reads correspondingly lower (e.g.
  GreedyDefensive's leg-5/6 deaths land at per-character level 9-10, not the
  plan's cumulative level 11 for "run 3 dies leg 5" — plausible, since that
  row bakes in two earlier runs' XP this single life never had).
- **"First run that reaches leg 10, within one level of level 15":** taken
  literally as "the lowest-seed run in the batch that reaches depth 80" per
  archetype (the closest available reading, since there is no real run
  order): GreedyAggressive's first (only) capped run is seed 13 at
  per-character level **14.0**; Lookahead2's first capped run is seed 1 at
  **13.0**; ProtectTheFront's first (only) capped run is seed 13 at
  **14.0**. All three land **within one to two levels of the plan's 15**,
  which is a genuinely close match — but it compares a single life's XP
  from *diving straight to leg 10 with nothing banked* against the plan's
  number for *arriving at leg 10 on a character that already banked three
  prior deaths' XP*. The two mechanisms produce a similar total only because
  a single leg-1-to-10 dive visits roughly as many rooms (80 steps) as the
  plan's four-run path does cumulatively (8+16+40+80 = 144 steps, discounted
  by lower per-room pay at shallow depth) — coincidentally close, not
  equivalent, and should not be read as validating the cumulative table.
- GreedyDefensive and RandomLegal never reached the depth cap in 24 Fresh
  lives, so they have no "first deep run" row to compare at all.

**Mid/Late profile depth data** (supplementary — not part of a career, but
the closest thing this tool has to "how the game plays once the track has
paid out"): all 15 cells' `depth.median/p10/p90/cappedShare` are in
`summary.json`; Lookahead2 caps out at both Mid (23/24 runs) and Late
(24/24 runs), GreedyDefensive caps 3/24 at Mid rising to 20/24 at Late —
consistent with the track's later nodes (Fury openings, health bumps, +5%
damage) compounding depth reach the way §4's bump analysis predicts,
though this is not what phase 6 asked to be measured here and is reported
only as a sanity check that Mid/Late are not somehow *weaker* than Fresh.

---

## 2. Stuck-player trajectory (RandomLegal proxy)

**RandomLegal never reaches level 10, 15, or 20 in this batch, on any
single life, at any profile.** Its deepest Fresh run (seed 8/20, depth 24 /
leg 3) tops out at per-character level 6.0. This is expected and not a
new finding: reaching the plan's cumulative levels 10/15/20 needs 3, 11 and
28 *runs'* worth of banked XP respectively, and — per §1 — this tool cannot
bank XP across runs. **The cumulative-level comparison the plan asks for is
therefore not answerable with this batch**, and no amount of additional
`-Runs` fixes that; it needs the actual career-persistence feature §1
describes as missing.

**Does RandomLegal die before leg 3 every time?** No — 20 of 24 Fresh runs
die before step 17 (leg 1-2), but 4 reach leg 3 (depth 23-24: seeds 8, 13,
14, 20). So "always dies before leg 3" is not quite true of this archetype
at this depth cap, though it is close (83% of lives).

**Per-run pay**, the requested fallback, computed by summing
`FightTrace.PayoutExp` over every won fight in each life (from
`traces.jsonl`, since `runs.jsonl`'s flat `fights[]` rows omit `payoutExp`
per the schema):

| | value |
|---|---|
| mean XP earned per life (24 runs) | 467.8 |
| median XP earned per life | 518.0 |
| range | 100 (seed 18) - 997 (seed 8) |

For reference, the plan's run-1 cumulative figure is 710 XP (dies leg 2).
RandomLegal's own leg-2 deaths (11 of 24 runs) average noticeably below
that — a spot check: seeds 1/2/4/6/9/16/19/22/23/24 (leg-2 deaths) earn
441-572 XP each, roughly 20-35% under the plan's 710, consistent with
RandomLegal being the deliberately weakest policy (it ranks nothing when
choosing gear/stats, per `PLAN_BALANCE_BOT.md` §5's "all-zero vector").

---

## 3. Bjorn's fury tiers with the level-15/26 nodes active

**Not reliably measurable from this trace, and the attempted proxy
demonstrates why.** `TurnTrace` (`Domain/Bot/RunTrace.cs:10-17`) carries
`ActorId`/`Action`/`TargetId`/`PartyHpAfter`/`EnemyHpAfter` — nothing that
names which `poolTiers` rung a cast fired. `PHASE1_BOT_REPORT.md` §(c) hit
this exact wall and solved it by adding `TurnTrace.PoolTierFired`, reading
`FightSession.Beats.RecordPoolTier`'s own tier decision directly, then
reverting the instrumentation afterward. This task's constraints forbid
editing anything under `Assets/`, so that door is closed here.

**What was tried as a proxy, and why it was rejected.** Bjorn's Slam
(`placeholder_brawler_slam`, `skills.json:477-493`) is `DamageSingle`,
`flatAmount: 17`, with `poolTiers: [{spend 0.5, x2}, {spend 1.0, x4}]` — so
in principle a cast's *damage magnitude* should cluster near 1x/2x/4x a
per-fight baseline. Damage per Slam turn was reconstructed as the drop in
`EnemyHpAfter` between consecutive turns (F6: one command resolves the whole
exchange, so the delta between two player commands is that command's own
damage), then each fight's Slam casts were normalized against that fight's
own minimum Slam damage. Result, pooled across all 360 runs: **73,909 Slam
turns, ratios ranging up to 528x the fight's own minimum** — impossible for
a system with only 1x/2x/4x rungs, which proves the signal is contaminated
rather than usable. Two contaminants identified: `EnemyHpAfter` is the
**sum of living enemies**, so in a multi-enemy (Elite) fight a Slam that
lands the killing blow on one target while another remains reads as a
different "hit" than one landing on a fresh full-HP target of different
defense; and production damage variance (`FightSession.DamageVarianceRange`,
deliberately left live per plan F3) means even same-tier hits are not equal.
A per-fight minimum is not a reliable x1 baseline when the fight itself
contains zero, one, or several genuinely-tiered hits mixed with
target-dependent noise.

**Nearest honest proxies, reported instead of a false-precision tier
share:**

| profile | Bjorn's Slam-cast share of his own turns | Slam damage: median / p90 / p99 / max |
|---|---|---|
| Fresh (node 15/26 inactive) | 76.6% (14,326 / 18,701) | 25 / 65 / 132 / 345 |
| Mid (node 15 active, 26 inactive) | 62.3% (17,989 / 28,894) | 37 / 98 / 186 / 371 |
| Late (both active) | 47.1% (41,604 / 88,411) | 58 / 156 / 329 / 1,056 |

Read qualitatively only: the median rises with profile (consistent with the
flat-damage and +5% physical bumps §4 authors at levels 12/24/28, which are
active by construction once `ProfilePresets.Build` claims the track), and
the right tail fattens sharply at Late (p99 329, max 1,056 vs Fresh's 132 /
345) — suggestive of the x2/x4 tiers firing more, and more often, as the
Fury economy strengthens. **This is not a tier-share number and must not be
read as one**; it cannot confirm or refute the plan's trip-wire ("x4 over a
third of his turns after level 26"). Bjorn's Slam SHARE of his own turns
actually *falls* with profile (77% → 62% → 47%) because Late profile
Bjorn has more skills unlocked and choices to make (Rampage, Second Wind,
Bulwark all appear only at Mid/Late in the action breakdown) — a real,
measurable finding, just not the one asked for.

**Recommendation:** the trip-wire cannot be evaluated without redoing
Phase 1's `TurnTrace.PoolTierFired` instrumentation (additive, three files,
reverted after use — see `PHASE1_BOT_REPORT.md` §(c) for the exact
mechanism) inside a session permitted to touch `Assets/`.

---

## 4. Downed half pay

Confirmed from the code, and illustrated from the trace, since the trace
alone cannot prove the multiplier (no per-character XP field exists on
`RunTrace` — see `PHASE1_BOT_REPORT.md`'s own "what could not be measured"
section for the same gap). Mechanism:
`RewardApplier.Apply` (`Core/RewardApplier.cs:38-68`) computes
`isDowned = !fielded.Contains(character.definitionId)` per squad slot, then
`gained = VictoryRewards.ExperienceFor(payout, isDowned)`
(`Domain/Combat/Session/VictoryRewards.cs:195-201`):
```csharp
if (!isDowned) return payout.Experience;
int experience = payout.Experience;
return experience <= 0 ? 0 : (experience + 1) / 2;   // half, rounded UP
```

**From the traces:** a won fight where a fielded squad member (Shawn, Bjorn
or Odette — all three are the fielded squad, per
`PLAN_BALANCE_BOT.md` §5's 2026-09-11 correction) never appears as an
`ActorId` across the whole fight is the closest trace-visible proxy for
"out for the whole fight" (present in the squad, not in
`session.Encounter.PlayerParty`, per `RunOrchestrator`/`RewardApplier`'s own
`fieldedIds` check). Filtering to fights of 2+ turns (to exclude fights too
short for a full-squad reading to be meaningful): **2,029 of 11,073 won
fights (18.3%) across the batch** show at least one squad member absent the
entire fight.

One concrete example: seed 1, RandomLegal, Fresh, fight at step 13 (floor
2), 35 turns, Odette never acts, `Won: true`, `PayoutExp: 88`,
`PayoutGold: 92` (party HP 340 → 179 across the fight). Per the formula
above, applied to this fight's own recorded `PayoutExp: 88`: Shawn and
Bjorn (fielded) each receive the full **88**; Odette (downed) receives
`(88 + 1) / 2 = 44`. **This number is not independently verifiable from the
trace** — `RunTrace`/`runs.jsonl` record only the fight's shared
`PayoutExp`, never a per-character post-split amount — so "44" is what the
cited code computes for this fight's numbers, not a value read back off a
trace field. A true confirmation would need a per-character XP field on
`TurnTrace`/`FightTrace`, the same class of gap `PHASE1_BOT_REPORT.md`
already flagged for per-character HP.

---

## 5. Real-time

**No minutes measurement exists in this batch, and none can be derived from
it.** `summary.json.batch.elapsedSeconds` (502.6s for this whole 360-run
batch) is wall-clock time for a headless Unity process resolving thousands
of commands with no animation, no input latency, and no UI — it has no
defined relationship to how long a human takes to read a fight screen, pick
a target, or navigate a menu. Nothing in `RunTrace`/`runs.jsonl` records a
per-command or per-room duration in any unit a player experiences. **The
owner's timed first hour (`PLAN_PROGRESSION_V2.md` §7 phase 6) is still
owed** and cannot be produced by any bot batch, by construction — it needs
an actual human play session.

---

## What could not be measured, in one place

- **A true career** (persistent level/exp across 24 runs): the driver has
  no cross-run save-carry path; `-Profiles`/`-InMemorySaves` do not provide
  one (§1). Building it is a `Assets/`-touching change, out of scope here.
- **Cumulative stuck-player levels 10/15/20** for RandomLegal: depends on
  the same missing career feature (§2). Per-run pay is reported instead,
  as the brief's own fallback specifies.
- **Bjorn's x2/x4 Slam tier share**: `RunTrace` carries no pool-tier field;
  the damage-magnitude proxy attempted here produced ratios up to 528x,
  proving contamination rather than signal (§3). Needs Phase 1's
  `PoolTierFired` instrumentation re-added by a session permitted to touch
  `Assets/`.
- **Per-character downed-half-pay verified numerically from the trace**:
  `RunTrace` has no per-character post-split experience field, only the
  shared fight-level `PayoutExp` (§4). The halving is confirmed from the
  production code and illustrated with one fight's numbers, not read back
  off a trace field.
- **Real playtime in minutes**: no such field exists or could exist in a
  headless batch (§5); the owner's timed first hour is still owed.

## Verification that nothing under Assets/ or tools/ changed

```
git status --short
```
run before and after this session's bot batch shows the same pre-existing
uncommitted state noted in the task (Art/Fonts diffs, untracked
`.agents/`/`.claude/skills/`/`Handovers/`/etc. from other work), with no new
entries under `Assets/` or `tools/` — the only new artifact is
`reports/bot/20260915-233830/` (gitignored) and this report file. No content
was rebuilt (`tools/build_content.ps1` was not run) and no `.json` under
`Assets/_Project/ContentData/` was edited.
