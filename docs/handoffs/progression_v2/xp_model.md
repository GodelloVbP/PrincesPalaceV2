# XP / pacing model for a progression redesign

All figures computed from the live content (`Assets/_Project/ContentData/enemies.json`) and the
live formulas (`DifficultyCurve.cs`, `LevelCurve.cs`, `DescentMap.cs`, `VictoryRewards.cs`). Script:
`scratchpad/xp_model.py`. Every number below is reproducible by running it.

---

## Part A — what a fight pays today

### A1. The facts, cited

**Enemy roster** (`Assets/_Project/ContentData/enemies.json`) — only `active:true` rows can spawn
(`RawEnemyEntry.cs:167-174`: "Benched, not deleted... no asset is built for it, so it cannot
spawn"; `ContentDatabase.Enemies` at `ContentDatabase.cs:150-153` reads only the built catalogue,
and `RunEncounter.Pool()` at `RunEncounter.cs:95-99` draws straight from it with no further active
filter — inactive entries are simply absent):

| id | expReward | minFloor | role |
|---|---:|---:|---|
| rat | 15 | 1 | normal |
| beetle | 24 | 1 | normal |
| bog_witch | 22 | 2 | normal |
| treant | 40 | 2 | normal (slotSpan 2, `enemies.json:271`) |
| golem | 35 | 5 | normal |
| forest_warden | 100 | 1 | boss (only active boss; `hollow_choir` is benched) |

**Leg structure** (`DescentMapGenerator`, in `DescentMap.cs`):
- A leg is `DefaultLegLength = 8` rooms (`DescentMap.cs:124`), i.e. 8 rooms are actually walked
  per leg (one node per column along the player's chosen road).
- Room 8 of every leg (absolute `step % 8 == 0`) is a **forced Boss** (`DescentMap.cs:139,225-228`).
- Room 4 of every leg (`step % 8 == 4`) is a **forced Elite**, fielding exactly
  `EliteEnemyCount = 2` enemies (`EncounterRoll.cs:61`, `DescentMap.cs:140,254`).
- The other 6 rooms are **rolled** from the `MiddleRooms` weighted table
  (`DescentMap.cs:200-207`); measured over 500 seeds × 5 legs at the shipped table, **Fight lands
  on 54.6% of rolled rooms** after the variety/shop fix-up passes settle
  (`DescentMap.cs:178-186`). The rest (Event/Treasure/Shop/Rest) pay no XP.
- A forced Rest the step before Boss only exists once the reward track's level-30 node is
  collected (`DescentMap.cs:230-254`, `restBeforeBoss` parameter) — **assumption A1**: modelled
  runs are below that unlock, so all 6 non-forced rooms are rolled (none forced to Rest).
- Normal fights field `NextInt(1,3)` enemies — 1 or 2 with equal odds, mean **1.5**
  (`EncounterRoll.cs:62-63,162-164`).
- Enemy content pool is banded by **`floor = RunDepth.FloorFor(run.legStartStep)`**
  (`RunEncounter.cs:74-75`, `RunDepth.cs:21-25`) — i.e. the floor/species band is fixed for an
  *entire leg* (floor = leg number), while reward *scaling* below rides the room's own absolute
  step, not the leg-start step.

**Reward scaling** (`DifficultyCurve.cs`):
- `ScaleReward(amount, step) == ScaleHealth(amount, step)` (`DifficultyCurve.cs:117`) — rewards
  ride the **health** rate, `HealthPermillePerStep = 75` (`DifficultyCurve.cs:69`), i.e.
  `multiplier(step) = 1.075^step`, floored (`DifficultyCurve.cs:124-158`).
- `VictoryRewards.For(defeated, isElite, depthStep)` (`VictoryRewards.cs:51-69`) sums raw
  `expReward` across defeated enemies, multiplies by `EliteRewardMultiplier = 1.56`
  (`VictoryRewards.cs:27`) **only when `isElite`**, rounds away-from-zero, then applies
  `ScaleReward` at `depthStep`. A Boss room is `isBoss:true, isElite:false`
  (`EncounterRoll.cs:145-149`) — **the elite multiplier does not apply to bosses**; only their much
  higher authored `expReward` (100 vs. ~15-40) does the work.
- `depthStep` is `FightSession.Outcome.DepthStep`, set from **`RunManager.Run.step`**
  (`FightBootstrap.cs:284`, `RunOrchestrator.cs:301`) — the room's own absolute step, *not* the
  leg-start step. So reward scaling advances room-by-room within a leg even though the enemy
  species band does not.

**XP goes to every standing fielded character in full; downed get 0** —
`RewardApplier.Apply` (`RewardApplier.cs:38-61`): `isDowned = !fielded.Contains(...)`,
`gained = isDowned ? 0 : payout.Experience` — confirmed, no split across the party
(`VictoryRewards.cs:156-161` says the same thing on the Domain side).

**XP is banked per fight, survives a later wipe** — `RewardApplier.Apply` calls
`character.AddExperience(gained)` then **`SaveSlotManager.SaveCurrent()`** immediately
(`RewardApplier.cs:61,119`), before the run continues. A wipe or quit afterward cannot undo it.

**Retreat/abandon**: there is no separate "retreat" mechanic — the map's Abandon button
(`MapScreen.cs:139,204-227`) and a defeat both route through **`RunSettlement.Settle`**
(`RunSettlement.cs:37-97`). It forfeits **`run.gold`** only (`RunSettlement.cs:25-28,50`:
"Forfeited. Gold lives on the run and is destroyed with it"). Character **XP is never touched** —
it was already written to the character and saved at the end of each fight, before the run object
even existed to be discarded. `run.expEarned` is a display-only running total, not a source of
truth subtracted on abandon.

### A2. Average XP per fight type, and run totals

**Assumption A2** (composition): a normal fight fields the *mean* of 1.5 enemies drawn uniformly
(with replacement) from the active, floor-eligible normal pool; an elite fight fields exactly 2
such draws ×1.56; a boss fight is always `forest_warden` (100 XP raw) since it is the only active
boss. **Assumption A3**: the table below evaluates one fight of each type *at* the listed depth
step, using `floor = FloorFor(step)` (valid because 0, 8, 16, ... 80 are themselves leg-start
steps). **Assumption A4**: `treant`'s `slotSpan: 2` almost never blocks a second enemy at
`StageSlotsPerSide` capacity (`EncounterRoll.cs:170,197`) — ignored as negligible (~4% of elite
rolls would be affected in the worst case).

XP scaling = 75 permille/step (today, `DifficultyCurve.cs:69`):

| depth step | floor | normal fight (avg) | elite fight (avg) | boss fight |
|---:|---:|---:|---:|---:|
| 0 | 1 | 29 | 61 | 100 |
| 8 | 2 | 67 | 140 | 178 |
| 16 | 3 | 120 | 250 | 318 |
| 24 | 4 | 214 | 446 | 567 |
| 32 | 5 | 412 | 858 | 1,011 |
| 40 | 6 | 736 | 1,531 | 1,804 |
| 48 | 7 | 1,313 | 2,731 | 3,218 |
| 56 | 8 | 2,341 | 4,870 | 5,739 |
| 64 | 9 | 4,176 | 8,686 | 10,236 |
| 72 | 10 | 7,448 | 15,492 | 18,256 |
| 80 | 11 | 13,284 | 27,631 | 32,559 |

**Run totals** (cumulative XP at the end of leg N, summing every room of every leg at its own
absolute step — see `leg_expected_xp` in the script for the exact room-by-room sum, not a
leg-boundary snapshot):

| dies after leg... | cumulative XP (75‰) |
|---|---:|
| 2 | 1,189 |
| 3 | 2,622 |
| 5 | 9,954 |
| 7 | 33,675 |
| 10 (full deep run) | 195,369 |

---

## Part B — alternative XP scaling (reward permille decoupled from the health curve)

Same model, `ScaleReward` permille changed to 40 and 25 (everything else — enemy stats, room
composition, elite/boss multipliers — held at today's values):

| dies after leg... | cumulative XP (75‰, today) | cumulative XP (40‰) | cumulative XP (25‰) |
|---|---:|---:|---:|
| 2 | 1,189 | 829 | 710 |
| 3 | 2,622 | 1,528 | 1,221 |
| 5 | 9,954 | 3,863 | 2,642 |
| 7 | 33,675 | 8,327 | 4,801 |
| 10 (deep run) | 195,369 | 23,311 | 10,153 |

Decoupling reward from the health curve flattens the payout enormously at depth: a deep run pays
**8.4x less at 40‰** and **19.2x less at 25‰** than today's 75‰, because 75 permille compounded
over ~80-88 steps is the dominant term in every deep total.

---

## Part C — level curves for a new cap of 40

`cost(L) = c * g^L` fitted per XP scaling against two anchors:
- **Anchor 1**: cumulative XP from 6 *assumed* consecutive normal fights at steps 0-5, floor-1 pool
  only (`rat`, `beetle`, mean raw 19.5) — **assumption A5, stated because the task frames the
  anchor this way**: this ignores that room 4 of a real leg is a forced Elite, and is a
  deliberately simpler proxy than the full leg model used elsewhere. It must equal
  `cost(2) + cost(3)` (XP to go from level 1 to level 3).
- **Anchor 2**: `cost(2) + ... + cost(40)` (XP to reach level 40) must equal 25× the full-deep-run
  (leg 10) total from Part A/B, for that same permille.

Both anchors were satisfiable simultaneously (bisection converged to a residual of ~0 for all
three scalings — no compromise needed):

| permille | c | g | anchor-1 target (lvl 3 XP) | anchor-2 target (lvl 40 XP) |
|---:|---:|---:|---:|---:|
| 75 (today) | 56.09 | 1.27931 | 209 | 4,884,221 |
| 40 | 59.94 | 1.20337 | 191 | 582,770 |
| 25 | 61.48 | 1.17406 | 184 | 253,834 |

For comparison, today's shipped curve is `c=100, g=1.09` to a cap of 100
(`LevelCurve.cs:42,52,123`) — these fitted curves are **much steeper per level** (17-28% per level
vs. 9%) because the same enormous total budget (tens of deep runs' worth of XP) has to fit into 40
levels instead of 100.

### Per-level cost (XP and as a fraction of that scaling's own deep-run total)

**75 permille** (c=56.09, g=1.27931, deep run = 195,369 XP):

| L | cost (XP) | cost (frac. of deep run) |
|---:|---:|---:|
| 2 | 92 | 0.000 |
| 5 | 192 | 0.001 |
| 10 | 659 | 0.003 |
| 15 | 2,257 | 0.012 |
| 20 | 7,734 | 0.040 |
| 25 | 26,503 | 0.136 |
| 30 | 90,819 | 0.465 |
| 35 | 311,212 | 1.593 |
| 40 | 1,066,435 | 5.459 |

**40 permille** (c=59.94, g=1.20337, deep run = 23,311 XP):

| L | cost (XP) | cost (frac. of deep run) |
|---:|---:|---:|
| 2 | 87 | 0.004 |
| 5 | 151 | 0.006 |
| 10 | 382 | 0.016 |
| 15 | 963 | 0.041 |
| 20 | 2,431 | 0.104 |
| 25 | 6,134 | 0.263 |
| 30 | 15,478 | 0.664 |
| 35 | 39,058 | 1.676 |
| 40 | 98,561 | 4.228 |

**25 permille** (c=61.48, g=1.17406, deep run = 10,153 XP):

| L | cost (XP) | cost (frac. of deep run) |
|---:|---:|---:|
| 2 | 85 | 0.008 |
| 5 | 137 | 0.014 |
| 10 | 306 | 0.030 |
| 15 | 683 | 0.067 |
| 20 | 1,523 | 0.150 |
| 25 | 3,396 | 0.335 |
| 30 | 7,577 | 0.746 |
| 35 | 16,902 | 1.665 |
| 40 | 37,705 | 3.714 |

### Career progression: level reached

Sequence: run 1 dies leg 2 → run 2 dies leg 3 → run 3 dies leg 5 → run 4 is the first full deep
run (leg 10) → thereafter only deep runs, counted cumulatively. Each run's XP total is that
scaling's Part A/B leg-N cumulative figure (the depth-scaling curve resets to step 0 every run).

**75 permille:**

| point | total XP | level |
|---|---:|---:|
| (a) after run 1 (dies leg 2) | 1,189 | 7 |
| (b) + run 2 (dies leg 3) | 3,811 | 11 |
| (c) + run 3 (dies leg 5) | 13,765 | 16 |
| (d) + run 4 (1st deep run) | 209,134 | 27 |
| (e) after 2 total deep runs | 404,503 | 29 |
| after 5 | 990,610 | 33 |
| after 10 | 1,967,454 | 36 |
| after 15 | 2,944,298 | 37 |
| after 20 | 3,921,142 | 39 |
| after 25 | 4,897,987 | 40 |

**40 permille:**

| point | total XP | level |
|---|---:|---:|
| (a) after run 1 (dies leg 2) | 829 | 6 |
| (b) + run 2 (dies leg 3) | 2,357 | 11 |
| (c) + run 3 (dies leg 5) | 6,220 | 15 |
| (d) + run 4 (1st deep run) | 29,531 | 23 |
| (e) after 2 total deep runs | 52,841 | 27 |
| after 5 | 122,774 | 31 |
| after 10 | 239,328 | 35 |
| after 15 | 355,882 | 37 |
| after 20 | 472,436 | 38 |
| after 25 | 588,990 | 40 |

**25 permille:**

| point | total XP | level |
|---|---:|---:|
| (a) after run 1 (dies leg 2) | 710 | 6 |
| (b) + run 2 (dies leg 3) | 1,931 | 10 |
| (c) + run 3 (dies leg 5) | 4,574 | 15 |
| (d) + run 4 (1st deep run) | 14,727 | 22 |
| (e) after 2 total deep runs | 24,880 | 25 |
| after 5 | 55,340 | 30 |
| after 10 | 106,107 | 34 |
| after 15 | 156,874 | 37 |
| after 20 | 207,641 | 38 |
| after 25 | 258,408 | 40 |

**Observation, not asked for but visible in the numbers**: by all three fitted curves a player is
already level 6-7 after just a leg-2 death and level ~15-16 after a leg-5 death — because
Anchor 1 only priced in flat normal fights while a real leg 1-5 also pays elite (×1.56) and boss
(100 flat, unscaled by depth-curve multiplier at low steps... but scaled at higher steps) rooms
that Anchor 1 never saw. The two anchors are pulling in different directions on purpose (task's
own framing); flag this tension to the owner rather than silently smoothing it — a curve that
clears a third of its levels within the first three short runs may not be the intended shape for a
40-level track.

---

## Part D — factual checks

### D1. Signature (Wool) absorb

`ResourcePool.Absorb` (`ResourcePool.cs:266-276`), called from
`CombatMath.ApplyDamageDetailed` at `CombatMath.cs:555`
(`int absorbed = target.SignaturePool != null ? target.SignaturePool.Absorb(amount) : 0;`):

```
public int Absorb(int amount)
{
    if (!AbsorbsDamage || amount <= 0 || Current <= 0) return 0;
    int absorbed = Math.Min(Current * AbsorbPerPoint, amount);
    Current -= (absorbed + AbsorbPerPoint - 1) / AbsorbPerPoint;
    return absorbed;
}
```

- **Rate**: `AbsorbPerPoint` is 1 damage per Wool point today
  (`ContentDatabase.Effective.cs:44` `SignatureAbsorbPerPoint = 1`, threaded through
  `ContentDatabase.Effective.cs:315-328`). A partial point rounds up against the holder
  (`ResourcePool.cs:260-264`).
- **Does absorbing spend the Wool the player could otherwise attack with?** Yes — `Absorb`
  decrements the same `Current` counter that talent abilities and Shear spend
  (`ResourcePool.cs:266-276`); there is exactly one Wool bank, and armour and ammunition draw from
  it (the tension the design docs call out).
- **Automatic?** Yes — it fires unconditionally inside the damage pipeline
  (`CombatMath.cs:555`), with no player choice or toggle.
- **Cap?** No cap beyond "however much Wool is currently banked" —
  `Math.Min(Current * AbsorbPerPoint, amount)` caps at the smaller of the incoming hit and what's
  in the bank, nothing more.
- **But it is currently OFF for the only shipped signature resource.** Shawn's row authors
  `signatureAbsorbsDamage: false` (`characters.json:34`; the content readme at `characters.json:2`
  states this explicitly: *"wool does NOT absorb damage today... incoming damage goes straight to
  his health; wool is ammunition only until the track's SignatureAbsorbs reward at level 60
  switches the armour half on"*). `AbsorbsDamage` is OR'd with the reward track's `SignatureAbsorbs`
  one-shot unlock (`ContentDatabase.Effective.cs:305-317`) — so today, absorb is dormant until a
  level-60 track node is collected.

### D2. Manual reward-track collection reachability, and skill-unlock timing

**Reachable mid-run, not just in the hub.** `SystemMenuScreen` is built into both the Map screen
(`MapScreen.cs:150,280-281`) and the Fight screen's own HUD
(`FightScreen.cs:242,382,402`: `hud.Add(systemMenu.Root)`), and its `CharacterInventory` tab opens
`RewardTrackScreen.Build()` (`SystemMenuScreen.cs:42,224,245`). So a player can open the reward
track and collect nodes both between rooms on the map and *during* a fight, from the HUD's system
menu.

**Which list the fight actually reads — not `Character.unlockedSkillIds`.** That list is a
different, dead route: `unlockedSkillIds` is "the seam the mage will use"
(`ContentDatabase.cs:293-301`) for an Event-room grant that has no implementation today — the
codebase readme at `ContentDatabase.cs:286-292` notes nothing production writes it. A reward
track's `UnlockSkill` node instead flows through a **fifth, separate route**: `AvailableSkillsFor`
reads `fromTrack = RewardTracks.For(character).SkillsCollected(character.claimedTrackLevel)`
(`ContentDatabase.cs:373-391`) — computed **live** off the watermark each call, no stored list at
all.

**When is it re-read?** `AvailableSkillsFor` is only called once per fight, at fight setup, through
`FightEncounterAdapter.KitFor` (`FightEncounterAdapter.cs:698-707`) inside `Build`
(`FightEncounterAdapter.cs:418,444`: *"A kit is built fresh for each fight and dies with it"*).
So collecting an `UnlockSkill` node **mid-fight**, via the system menu that is present in the fight
HUD, will not add the skill to the kit already in play for that fight — it takes effect starting
the *next* fight (when `Build`/`KitFor` runs again with the now-higher `claimedTrackLevel`).

### D3. Save versioning, and level beyond `RewardTrack.MaxLevel`

- `SaveData` has a real version/migration mechanism: `CurrentVersion = 5`
  (`SaveData.cs:49,108`), and `Migrate()` (`SaveData.cs:405-474`) walks version-gated steps
  (`version < 2`, `< 3`, `< 4`, `< 5`) up to current, additively, without requiring every save to
  replay every step blindly.
- `RewardTrack.MaxLevel = 100` (`RewardTrack.cs:123`) today, but nothing on `Character` clamps
  `character.level` to it — `AddExperience` (`Character.cs:210-228`) loops
  `while (exp >= ExpToNextLevel(level)) { level++; }` with no upper bound of its own (only
  `LevelCurve.MaxCurvedLevel = 200` flattens the *cost* curve, `LevelCurve.cs:59,75`). So a
  character **can** level past 100 purely through XP income.
- What happens then is graceful, not broken: `RewardTrackDefinition.At(level)` returns
  `TrackEntry.None` for any `level > RewardTrack.MaxLevel` (`RewardTrackDefinition.cs:138-146`),
  and `NextRewardLevel` simply returns 0, "the track has nothing left"
  (`RewardTrackDefinition.cs:148-160`). No exception, no save corruption — XP earned past level
  100 just stops buying anything on the current track, silently. (This is exactly the situation a
  40-level redesign has to decide about explicitly, since XP will keep compounding well past
  whatever the new cap is, per Part C's own tables.)

### D4. Recorded fight/run duration data

**None found.** The balance-bot reports under `reports/bot/*/summary.json` record
`elapsedSeconds` for the whole **simulation batch** (e.g. 403.8s for 200 runs × 5 archetypes ×
2 shards, `reports/bot/20260911-030038/summary.json`) — that is batchmode simulation wall-clock,
not real played time, and the schema doc says as much
(`docs/BOT_SUMMARY_SCHEMA.md:494-495`: "wall-clock time for the whole batch"). The battle-speed
preset doc (`docs/archive/PLAN_BATTLE_SPEED.md`) defines presets (0.5x/1x/1.5x/2x/3x, today's pace
labelled 1.5x, default 1x) and per-cast pacing multipliers, but no aggregate "a fight takes N
real minutes" or "a run takes N real minutes" figure exists anywhere in `docs/` or the bot
reports. **I will assume a figure** for any pacing conversion rather than inventing one here —
flag the number you want used and I'll fold it in.

---

## Assumptions, gathered in one place

- **A1**: modelled runs are below the reward-track level-30 unlock, so `restBeforeBoss` is false
  and all 6 non-forced leg rooms are rolled (none forced to Rest).
- **A2**: normal-fight enemy count uses its exact mean (1.5); elite is always exactly 2; enemies
  are drawn i.i.d. uniformly from the floor-eligible active pool (matches `EncounterRoll.Roll`'s
  actual uniform draw-with-replacement).
- **A3**: the depth-step table (Part A/B) evaluates one fight of each type *at* the listed step
  using `floor = FloorFor(step)`; this is exact for the 11 listed steps because they are
  themselves leg-start steps.
- **A4**: `treant`'s `slotSpan:2` capacity interaction is treated as negligible (ignored).
- **A5**: Part C's level-3 anchor is evaluated as 6 consecutive *normal* fights at steps 0-5 per
  the task's own framing, even though a real leg's room 4 is a forced Elite — a deliberately
  simpler proxy than the full leg model used for run totals and the level-40 anchor.
- **Run totals / career table**: each run's depth-curve step counter resets to 0
  (`RunManager`/`RunSnapshot` semantics — a new descent starts its own step count), so successive
  runs' XP simply adds; only `character.level`/`exp` persist across runs, not `run.step`.
