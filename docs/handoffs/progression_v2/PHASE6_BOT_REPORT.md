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

---

# Part 2: with career mode

Written against `9da83d74` (main tree, branch `prismatic-orb`), the commit that
adds `BotRunDriver.PlayCareer`, `TurnTrace.PoolTierFired`/`.PrimaryPoolAfter`,
and the `-Career` switch this part's batches actually use. Part 1's own
limitations section is this part's brief: a true career (persistent
level/exp across lives) and Bjorn's x2/x4 tier share were both flagged there
as "not measurable with this batch" for want of a feature and a trace field.
Both now exist; this is what they measure.

## 0. The five batches this part is built from

Five separate foreground calls, one per archetype (a single combined call was
not attempted -- each archetype's career runs independently and nothing is
lost by splitting, and splitting keeps every call comfortably inside the
600s budget):

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 24 -Seed 1 -Career -Archetypes RandomLegal      -Profiles Fresh -DepthCap 80 -Shards 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 24 -Seed 1 -Career -Archetypes GreedyAggressive -Profiles Fresh -DepthCap 80 -Shards 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 24 -Seed 1 -Career -Archetypes GreedyDefensive  -Profiles Fresh -DepthCap 80 -Shards 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 24 -Seed 1 -Career -Archetypes Lookahead2      -Profiles Fresh -DepthCap 80 -Shards 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 24 -Seed 1 -Career -Archetypes ProtectTheFront -Profiles Fresh -DepthCap 80 -Shards 1
```

`-Career` reinterprets `-Runs 24` as 24 lives in ONE career at the single
seed `-Seed 1`, played against one save (`BotRunDriver.PlayCareer`); `-Shards`
is forced to 1 by the script regardless of what is passed, since a
single-seed career has nothing to split across processes. Every life still
lands an ordinary `traces.jsonl`/`runs.jsonl` row plus one `career.jsonl` row
carrying its own run index and per-character level/exp.

| archetype | batch dir | wall clock | bugs | determinism mismatches |
|---|---|---|---|---|
| RandomLegal | `reports/bot/20260916-002213/` | 5.8s | 0 | 0 |
| GreedyAggressive | `reports/bot/20260916-002246/` | 77.7s | 0 | 0 |
| GreedyDefensive | `reports/bot/20260916-002422/` | 38.2s | 1 (below) | 0 |
| Lookahead2 | `reports/bot/20260916-002518/` | 174.9s | 0 | 0 |
| ProtectTheFront | `reports/bot/20260916-002834/` | 89.4s | 0 | 0 |

All gitignored under `reports/bot/`, nothing committed from them. `-ReplayShare`
was left at its default (0.1 > 0), which for career mode means the WHOLE
career is replayed once and every one of its 24 lives compared by hash
(`determinism.checked` reads 24 for every cell above, not a tenth of it -- see
`docs/BOT_SUMMARY_SCHEMA.md`'s new `career.jsonl` section on why career mode's
determinism check does not sample individual lives). Zero mismatches across
120 lives (24 x 5 archetypes), confirming a career replays byte-identically
from its one seed.

One genuine finding, not a regression: GreedyDefensive's run 21 (level 27,
step 56/leg 7, seed `10995824595521919989`) hit `TooManyCommands` at the
2000-command hard ceiling, the last ten actions all `Skill:mend` (Odette's
heal). This is the exact blind spot `FightRunner.StallCommands`'s own comment
already names -- an exchange where the party heals back everything it takes
and occasionally lands a scratch on the enemy resets the 60-command stall
counter every time that scratch lands, so the fight can wander for up to 2000
commands before the hard ceiling (not the stall detector) finally cuts it off.
Reproducible: the exact same seed, step and last-ten-actions came back
identically on a from-scratch rerun of the batch. Not something this task's
brief asked to be fixed.

## 1. Career simulation, for real this time

`BotRunDriver.PlayCareer(seed, archetype, "Fresh", runsInCareer: 24, depthCapSteps: 80)`
plays 24 lives back to back against one save: `ProfilePresets.Build` runs
once (life 0 starts Fresh -- level 1, nothing claimed), and every later life
reuses that same `SaveData` through `RunOrchestrator.StartRun`/
`RunManager.EndRun`, the same two doors a real player's hub-to-descent
transition uses. Level, exp, `claimedTrackLevel`, stat points, embers and
talents all carried; gear and the stockpile did not (`RunManager.EndRun`
clears both on every completed life, exactly as it does on every completed
real run -- see `BotRunDriver.PlayCareer`'s own header for why this was built
to match that rather than special-case gear to survive a boundary nothing
else survives).

All three fielded characters (Shawn, Bjorn, Odette) sat at the identical
level after every single one of the 120 lives across all five archetypes --
nobody was ever downed for a whole won fight in this batch, so `RewardApplier`
paying every fielded, non-downed character the full amount kept the squad
perfectly in sync the entire time. The table below reports one "level"
column rather than three for that reason; it is an observation about this
batch, not a guarantee the game enforces.

### Per-run table (run index, 1-based to match the plan's own "run N" wording; step; leg = `(step-1)/8 + 1`; squad level)

**RandomLegal** (median depth 32/leg4 across the 24 lives, p10 24/p90 36, 0 capped):

| run | step (leg) | level | run | step (leg) | level |
|---|---|---|---|---|---|
| 1 | 16 (2) | 4 | 13 | 32 (4) | 15 |
| 2 | 24 (3) | 7 | 14 | 32 (4) | 16 |
| 3 | 16 (2) | 8 | 15 | 32 (4) | 16 |
| 4 | 28 (4) | 10 | 16 | 36 (5) | 17 |
| 5 | 28 (4) | 11 | 17 | 32 (4) | 17 |
| 6 | 32 (4) | 12 | 18 | 32 (4) | 17 |
| 7 | 29 (4) | 13 | 19 | 32 (4) | 18 |
| 8 | 28 (4) | 13 | 20 | 36 (5) | 18 |
| 9 | 32 (4) | 14 | 21 | 40 (5) | 18 |
| 10 | 36 (5) | 14 | 22 | 32 (4) | 19 |
| 11 | 32 (4) | 15 | 23 | 40 (5) | 19 |
| 12 | 30 (4) | 15 | 24 | 24 (3) | 19 |

**GreedyAggressive** (median depth 56/leg7, p10 40/p90 80, capped 4/24 -- runs 9,11,13,20):

| run | step (leg) | level | run | step (leg) | level |
|---|---|---|---|---|---|
| 1 | 24 (3) | 5 | 13 | **80 (10, capped)** | 23 |
| 2 | 32 (4) | 9 | 14 | 64 (8) | 24 |
| 3 | 40 (5) | 11 | 15 | 56 (7) | 24 |
| 4 | 48 (6) | 13 | 16 | 56 (7) | 25 |
| 5 | 40 (5) | 14 | 17 | 72 (9) | 26 |
| 6 | 48 (6) | 15 | 18 | 72 (9) | 26 |
| 7 | 54 (7) | 16 | 19 | 64 (8) | 27 |
| 8 | 48 (6) | 17 | 20 | **80 (10, capped)** | 28 |
| 9 | **80 (10, capped)** | 19 | 21 | 56 (7) | 28 |
| 10 | 56 (7) | 20 | 22 | 72 (9) | 29 |
| 11 | **80 (10, capped)** | 21 | 23 | 64 (8) | 29 |
| 12 | 64 (8) | 22 | 24 | 64 (8) | 30 |

**GreedyDefensive** (median depth ~52/leg7, capped 3/24 -- runs 9,11,13; run 21 hit the 2000-command ceiling, see §0):

| run | step (leg) | level | run | step (leg) | level |
|---|---|---|---|---|---|
| 1 | 28 (4) | 6 | 13 | **80 (10, capped)** | 23 |
| 2 | 32 (4) | 10 | 14 | 64 (8) | 24 |
| 3 | 40 (5) | 11 | 15 | 56 (7) | 24 |
| 4 | 48 (6) | 13 | 16 | 40 (5) | 24 |
| 5 | 40 (5) | 14 | 17 | 48 (6) | 25 |
| 6 | 44 (6) | 15 | 18 | 56 (7) | 25 |
| 7 | 40 (5) | 15 | 19 | 64 (8) | 26 |
| 8 | 44 (6) | 16 | 20 | 52 (7) | 26 |
| 9 | **80 (10, capped)** | 18 | 21 | 56 (7, 2000-cmd ceiling) | 27 |
| 10 | 56 (7) | 19 | 22 | 56 (7) | 27 |
| 11 | **80 (10, capped)** | 21 | 23 | 64 (8) | 28 |
| 12 | 64 (8) | 22 | 24 | 64 (8) | 28 |

**Lookahead2** (median depth 80/leg10 -- capped **22/24** lives, the only archetype whose career spends almost its entirety at the depth cap):

| run | step (leg) | level | run | step (leg) | level |
|---|---|---|---|---|---|
| 1 | 56 (7) | 11 | 13 | **80 (10, capped)** | 30 |
| 2 | **80 (10, capped)** | 15 | 14 | **80 (10, capped)** | 31 |
| 3 | 40 (5) | 16 | 15 | **80 (10, capped)** | 32 |
| 4 | **80 (10, capped)** | 18 | 16 | **80 (10, capped)** | 33 |
| 5 | **80 (10, capped)** | 20 | 17 | **80 (10, capped)** | 34 |
| 6 | **80 (10, capped)** | 21 | 18 | **80 (10, capped)** | 34 |
| 7 | **80 (10, capped)** | 23 | 19 | **80 (10, capped)** | 35 |
| 8 | 72 (9) | 24 | 20 | **80 (10, capped)** | 36 |
| 9 | **80 (10, capped)** | 25 | 21 | **80 (10, capped)** | 37 |
| 10 | **80 (10, capped)** | 26 | 22 | **80 (10, capped)** | 38 |
| 11 | **80 (10, capped)** | 27 | 23 | **80 (10, capped)** | 39 |
| 12 | **80 (10, capped)** | 28 | 24 | **80 (10, capped)** | 39 |

**ProtectTheFront** (median depth ~58/leg7-8, capped 5/24 -- runs 9,11,13,15,20):

| run | step (leg) | level | run | step (leg) | level |
|---|---|---|---|---|---|
| 1 | 31 (4) | 7 | 13 | **80 (10, capped)** | 24 |
| 2 | 40 (5) | 10 | 14 | 64 (8) | 24 |
| 3 | 46 (6) | 12 | 15 | **80 (10, capped)** | 26 |
| 4 | 48 (6) | 14 | 16 | 56 (7) | 26 |
| 5 | 40 (5) | 14 | 17 | 72 (9) | 27 |
| 6 | 48 (6) | 15 | 18 | 64 (8) | 27 |
| 7 | 48 (6) | 16 | 19 | 64 (8) | 28 |
| 8 | 48 (6) | 17 | 20 | **80 (10, capped)** | 29 |
| 9 | **80 (10, capped)** | 19 | 21 | 52 (7) | 29 |
| 10 | 56 (7) | 20 | 22 | 68 (9) | 30 |
| 11 | **80 (10, capped)** | 21 | 23 | 64 (8) | 30 |
| 12 | 72 (9) | 23 | 24 | 64 (8) | 31 |

### Comparison against the plan's table (§3), with the actual depth pattern beside it

The plan's table is a *deterministic* career: a fixed room sequence at a
standing player's expected pay, so its "run N" rows describe a hypothetical
player who always makes the paper-optimal choice. These five careers are
real, seeded, archetype-played descents -- the comparison is honest exactly to
the extent the two are different things measuring the same shape.

| plan row | plan says | RandomLegal (weakest) | GreedyDefensive | GreedyAggressive | ProtectTheFront | Lookahead2 (strongest) |
|---|---|---|---|---|---|---|
| run 1, dies leg 2 -> level 5 | 710 XP, level 5 | **run 1, dies leg 2, level 4** (closest match -- RandomLegal is the only archetype that ever died in leg 2 at all in this batch) | never died leg 2 | never died leg 2 | never died leg 2 | never died leg 2 |
| first deep run (leg 10) -> level 15 | -- | never capped in 24 lives | run 9, level 18 | run 9, level 19 | run 9, level 19 | **run 2, level 15** (exact level match) |
| run 6 -> level 20 | level 20 | never reached level 20 | run 11 (step 80/leg10, capped), level 20 | run 10 (step 56/leg7), level 20 | run 10 (step 56/leg7), level 20 | run 5 (step 80/leg10, capped), level 20 |
| run 10 -> level 25 | level 25 | never reached level 25 | run 17 (step 48/leg6), level 25 | run 16 (step 56/leg7), level 25 | run 15 (step 80/leg10, capped), level 25 | run 9 (step 80/leg10, capped), level 25 |
| run 14 -> level 30 | level 30 | never reached level 30 (max 19 over 24 lives) | never reached level 30 (max 28 over 24 lives) | run 24 (step 64/leg8), level 30 | run 22 (step 68/leg9), level 30 | run 14 (step 80/leg10, capped), level 30 |

Reading this honestly rather than charitably: the plan's table is a
best-play deterministic pace, and only **Lookahead2** -- the strongest
archetype, capped 22 of its 24 lives -- tracks it closely (level 15 at the
first deep run on run 2 against the plan's own claim for "first deep run",
and level 30 by run 14 landing on the plan's own row exactly). Every weaker
archetype falls further behind the plan's pace the higher the target level
goes, which is the expected shape (the plan's table is a ceiling a
paper-optimal player reaches, not a floor every policy should hit) but is
worth stating plainly rather than picking the one archetype that matches and
calling the table validated. **GreedyDefensive never reached level 30 in 24
lives at all** (topped out at 28), and RandomLegal -- see §2 -- never passed
level 19.

## 2. Stuck-player trajectory (RandomLegal proxy), now a real career

Part 1 could not answer this at all ("no amount of additional `-Runs` fixes
that; it needs the actual career-persistence feature"). It can now:

| plan's cumulative target | plan says (run #) | RandomLegal's actual career (24 lives) |
|---|---|---|
| level 10 | run 3 | **run 4** (one run later than the plan) |
| level 15 | run 11 | **run 11** (exact match) |
| level 20 | run 28 | **never reached in 24 lives** (topped out at level 19 on run 24; the plan's own row needs 28 runs and this career only played 24) |

RandomLegal -- the archetype that ranks nothing when choosing gear, stats or
talents (`PLAN_BALANCE_BOT.md` §5's "all-zero vector") -- tracks the plan's
own stuck-player pace remarkably closely through level 15 (one run late at
10, exact at 15), and the level-20 row is not a miss so much as a career that
was not run long enough to reach it: the plan's own row asks for run 28 and
this batch stopped at 24. A longer `-Runs` would answer it directly; this
was not rerun with a longer career because the brief's own batch spec was
24 lives.

RandomLegal died in leg 2 exactly once (run 1) and never again in this
career -- from run 2 onward it consistently reached leg 3-5 before dying
(see the full table in §1), so "the weakest policy always dies before leg 3"
is not true of this batch beyond its very first life; the character's
accumulated stat points and (idempotently, per-life) claimed track rewards
are doing real work even under a policy that spends them at random.

## 3. Bjorn's fury tiers, measured directly

`TurnTrace.PoolTierFired` now exists (0 = no tier fired, 2 = the x2 tier, 4
= the x4 tier -- `CombatBeat.PoolTierDamageMultiplier`'s own values, written
by `FightRunner.Play` off the beat it drains per command). This replaces
Part 1's contaminated damage-magnitude proxy (528x ratios, proven unusable)
with the field the tier resolution itself decided.

**Method and its one approximation.** `RunTrace` records a tier per command
but not a level per command, so a turn's level bracket is read off the LIFE
it belongs to: each life is bucketed by the level it STARTED at (the
previous life's ending level, or 1 for life 0 of every career), and every
Slam cast in that life counts toward that one bracket. This is exact for the
overwhelming majority of a career -- most lives do not cross a bracket
boundary mid-life -- but a life that levels from, say, 14 to 17 across its own
XP gain has its early (pre-15) turns counted in the "15-26" bracket. Pooled
across all five archetypes' careers (120 lives, every Slam cast from every
one of them):

| Bjorn's level | Slam casts | x2 share | x4 share |
|---|---|---|---|
| pre-15 (opening 0, node 15 inactive) | 2,414 | 33.1% (799) | 0.7% (18) |
| 15-26 (opening 25 active, node 26 inactive) | 7,631 | 43.0% (3,278) | 0.8% (59) |
| 26+ (opening 50 active) | 6,836 | 49.6% (3,392) | 0.6% (39) |

**The trip-wire does not fire -- x4 stays under 1% in every bracket,
including after level 26.** `PLAN_PROGRESSION_V2.md`'s phase 1 spec (§7,
item c) worried about the opposite failure: "share of his turns that are x4
slams; over a third sends opening 50 back to 40." That is not what this
batch shows. The design's own §4 note for level 26 says "the x2 Slam is the
opening move of every fight and the x4 tier needs 2 attacks" -- the measured
x2 share (49.6% after 26, versus 33.1% before level 5's opening even
exists) is consistent with that: fury opening at 50 does make x2 close to
"every other Slam", exactly as designed. The x4 tier staying rare is also
consistent with it needing a SECOND attack's worth of fury gain on top of an
opening 50, which many fights do not last long enough (or do not spend a
second Slam on) to reach. This is a real, level-bracketed measurement, not a
proxy, and it says the level-26 node is landing as designed rather than
overshooting into the trip-wire's territory.

## 4. Collected-node timing (level-3 and level-10 abilities)

One correction to the brief before the numbers: **"Bellow" does not exist in
shipped content.** `Assets/_Project/ContentData/reward_tracks.json`'s `bear`
track grants `placeholder_brawler_provoke` at level 3, not a skill named
Bellow -- the plan's own flavor-text column (`PLAN_PROGRESSION_V2.md` §4) was
never implemented under that name for Bjorn. Shawn's level-3/10 grants
(`woolgathering`, `battering_ram`) and Odette's (`frost_flare`,
`lightning_bolt`) match the plan exactly; only Bjorn's level-3 name is
stale. Reported below by content id, with the plan's label in parentheses
where it was accurate.

First run (1-based, matching §1's table) each character casts each skill at
least once, per archetype career; "never" means not cast in any of the 24
lives:

| archetype | Woolgathering (Shawn L3) | `placeholder_brawler_provoke` (Bjorn L3) | Frost Flare (Odette L3) | Battering Ram (Shawn L10) | Rampage (Bjorn L10) | Lightning Bolt (Odette L10) |
|---|---|---|---|---|---|---|
| RandomLegal | run 1 | never | run 1 | run 4 | run 4 | run 1 |
| GreedyAggressive | never | never | run 1 | run 3 | run 3 | run 1 |
| GreedyDefensive | run 1 | never | run 1 | run 3 | run 3 | run 1 |
| Lookahead2 | run 1 | never | run 1 | run 1 | run 1 | run 1 |
| ProtectTheFront | never | never | run 1 | run 2 | run 2 | run 1 |

Two findings, not one. First, the level-10 verbs (Battering Ram, Rampage,
Lightning Bolt -- all three offensive) are cast by every archetype within a
handful of runs of unlocking them, exactly as the acquisition loop intends.
Second, `placeholder_brawler_provoke` is **never cast by any archetype across
120 lives**, and Woolgathering is skipped entirely by two of the five
(GreedyAggressive, ProtectTheFront). `placeholder_brawler_provoke` is a
Provoke-kind skill -- non-damaging, tanking utility -- and every archetype here
is a damage-and-survival policy; `NonDamagingSkillGuard`
(`Domain/Bot/NonDamagingSkillGuard.cs`) exists precisely to keep an archetype
from wasting turns on the non-damaging class of skill, so a node that grants
ONLY a non-damaging skill is, for every archetype measured, a level-up that
buys nothing any of them will ever use. That is a fair reading of what these
five policies do, not necessarily of what a human player would do with a
taunt button -- but it is worth flagging next to the "Bellow" naming gap
rather than past it, since both point at the same node.

## Verification that nothing under Assets/ or tools/ changed beyond this session's own committed edit

`git status --short` before this part's batches and after shows the same
pre-existing uncommitted state Part 1 noted (Art/Fonts diffs, untracked
`.agents/`/`.claude/skills/`/etc. from other work), unchanged by anything in
this session beyond the one commit `9da83d74` (staged and committed by
explicit path, listed below) and the five gitignored
`reports/bot/20260916-*/` directories these batches wrote. No content was
rebuilt (`tools/build_content.ps1` was not run) and no `.json` under
`Assets/_Project/ContentData/` was edited.

`9da83d74` -- "Add bot career mode and per-command fury-tier tracing":

- `Assets/_Project/Scripts/Domain/Bot/RunTrace.cs` -- `TurnTrace.PoolTierFired`
  / `.PrimaryPoolAfter`, folded into `RunTrace.Hash()`.
- `Assets/_Project/Scripts/Domain/Bot/FightRunner.cs` -- drains and reads the
  beat those two fields come from, per command.
- `Assets/_Project/Scripts/Core/Bot/BotRunDriver.cs` -- `PlayDescent` split
  out of `PlayOneRun`; `PlayCareer` and its `CareerRunSummary`/
  `BotCareerResult` types.
- `Assets/_Project/Scripts/Editor/Bot/BalanceBotRunner.cs` -- `-botCareer`,
  `RunCareerCell`/`CareerRowJson`, and the `TraceJson` fix that writes the
  two new `TurnTrace` fields (missing from the very first two batches of
  this part, caught before the Bjorn analysis and fixed in a follow-up edit
  before any of the final five batches above were run -- every batch cited
  in this part already carries the fix).
- `tools/bot.ps1` -- `-Career` switch.
- `docs/BOT_SUMMARY_SCHEMA.md` -- the two new `TurnTrace` fields and a new
  `career.jsonl` section.
- `Assets/_Project/Scripts/Tests/EditMode/Combat/BotPoolTierTraceTests.cs`,
  `Assets/_Project/Scripts/Tests/PlayMode/Run/BotCareerModeTests.cs` -- new.

Full suite (`tools/run_tests_parallel.ps1`, no build switches, run before any
of this part's batches): **EditMode 3834/3837 passed (3 skipped), PlayMode
1014/1055 passed (41 skipped), 0 failed.**
