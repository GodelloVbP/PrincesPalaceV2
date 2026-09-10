# Plan — a bot that plays whole runs, for balance reports

A headless player that descends until it dies, thousands of times, and writes a
report about where the fun is and is not. Balance is the goal; bugs are a
by-product it can catch when it trips an invariant, not something it can
"deduce". Output is a report to read, not a CI gate.

Written against `d051b03`. Every file:line below was read, not remembered.

---

## 0. Six things the survey found that decide the shape

### F1. The domain is engine-free, but the *run* is not a domain object

`PrincesPalace.Domain.asmdef` has `noEngineReferences: true`; there is not one
`using UnityEngine` under `Domain/`. `FightSession`, `DescentMapGenerator`,
`DifficultyCurve`, `EncounterRoll`, `RoomResolution`, `VictoryRewards`,
`ItemOfferRoll`, `RelicPool` are all plain C#.

The sequence hub → draft → map → room → fight → reward → next room → death is
**not** in Domain. It is spread over Core statics and MonoBehaviours:

| Step | Where |
|---|---|
| start a run | `RunManager.StartRun(seed)` `Core/RunManager.cs:135` |
| relic draft | `RelicDraftController` (weighted draft at `:165`), a screen |
| pick a room | `MapController.Walk.cs:163` `Arrive` → `RunManager.MoveTo` |
| non-fight room | `RoomResolver.Resolve` `Core/RoomResolver.cs:36` |
| build a fight | `FightBootstrap.BuildRoomFight` `Core/FightBootstrap.cs:143` |
| settle a fight | `FightBootstrap.OnFightEnded` `Core/FightBootstrap.cs:294` (~15 calls) |
| item offer | `FightController.Input.cs:388` `RollOffers` |
| take an item | `ReckoningController.Take` `Core/ReckoningController.cs:704` |
| leg over | `RunManager.LegIsOver()` / `AdvanceLeg()` `RunManager.cs:326/333` |

Consequence: the bot cannot be a standalone .NET app without re-implementing
these rules, and a re-implementation would measure a copy that drifts. Content
is also ScriptableObjects loaded through `Resources` (`ContentDatabase`).

**Decision: the bot runs inside Unity batchmode against the TestRunner copy,
references Core, and uses the real content.** Cost is one Unity launch per
batch (about a minute). A fight is milliseconds, a run is well under a second,
so five thousand runs is minutes, not hours.

### F2. The orchestration must be extracted, not duplicated

`OnFightEnded` is the rulebook for what a won or lost fight does to a run:
fold the ledger, write HP back, spend second lives, record the room and the
boss, end the run on a loss, bank gold and apply XP on a win, clear the room,
advance the leg. Every line has a comment explaining an order that once went
wrong. Copying it into the bot creates a second rulebook.

**Decision: extract a plain `RunOrchestrator` class in Core, and make
`FightBootstrap` call it.** One seam, two callers (the screen and the bot).
This is the only change to the live game path and it is the risky part of the
plan, so it gets characterization tests first (Phase 0) and ships as its own
commit.

### F3. Determinism is free, except for one deliberately unseeded draw

Random draws are derived per position: `RngStreams.Derive(runSeed, stream,
step, node)` (`Domain/Rng/RngStreams.cs:25`). The map, the roster, the fight
and the treasure of a given seed are the same every time, by construction.
Same seed twice must give the same trace, which is a test the bot gets for
free and a leak detector for the future.

The one exception is the post-fight item offer, rolled from
`UnityEngine.Random` on purpose so rerolls do not shift a replay
(`FightController.Input.cs:399`). `ItemOfferRoll.Roll` takes the randomness as
a `Func<int,int>`, so the bot passes a seeded one derived from
`(runSeed, step, node, rerollIndex)`.

`FightSession.DamageVarianceRange` stays at its production value. Zeroing it is
right for unit tests and wrong for balance: variance is part of what is being
measured.

### F4. There is no "win"; legs recur forever

`AdvanceLeg` generates the next leg indefinitely. `DifficultyCurve` scales
enemy health by 75‰ and attack by 38‰ per step (`DifficultyCurve.cs:69-70`), so
every run ends in a death. The headline is therefore **depth reached**, not
win rate, and a run needs a depth cap so a broken-strong archetype terminates.
Assumed cap: 5 legs (40 steps). A run that reaches the cap is reported as
"capped", which is itself a finding.

### F5. What makes the player strong, and what survives a run

| Source | Lives on | Survives death? |
|---|---|---|
| level, exp | `Character.level/exp` `Data/Character.cs:87-88` | yes |
| track rewards | claimed manually via `Character.ClaimTrackRewards()` `:408` | yes |
| stat points | `Character.Invest(AbilityScore)` `:214` | yes |
| talents | `Character.unlockedTalentIds` `:24`, bought through `TalentController` with the save wallet | yes |
| equipment | `Character.equipment` | **no** — cleared in `RunManager.EndRun` `RunManager.cs:186` |
| stockpile, consumables | `SaveData.stockpiledItems` | **no** — cleared in `EndRun` |
| relics | `RunSnapshot.relicIds` | no |
| run gold | `RunSnapshot.gold` | banked on settlement |

So a run's power is the profile (level, talents, stats) plus what it picks up
in-run. A balance batch must state which profile it plays. Three presets:
**Fresh** (level 1, nothing), **Mid** and **Late** (a level with track rewards
claimed and points spent by a fixed rule). Exact levels for Mid and Late are a
tuning input, defaulting to 20 and 60. The bot must call `ClaimTrackRewards`
itself; `RewardApplier` deliberately does not (`Core/RewardApplier.cs:~75`).

### F6. The fight API is already bot-shaped

Five commands, each of which resolves the whole enemy reply before returning:
`ExecuteAttack`, `CastSkill`, `ExecuteSkill`, `UseConsumable`, `HoldBack`
(`Domain/Combat/Session/FightSession.cs:187/242`, `Skills.cs:23/123`,
`Items.cs:20`). Legal options come from `SkillOptionsFor(actor)`
(`FightSession.cs:129`), `Encounter.LivingEnemies` and `CanMeleeReach`
(`:100`). End state is `IsOver` / `PlayerWon` (`:96-97`) and `Payout`.
The loop is `Begin()`, then while not over and it is the player's turn, issue
one command. No end-turn call exists; every command ends the turn.

Multi-member parties act through `CombatEncounter.Current`; the policy is
asked per actor, so placeholders in the squad cost nothing extra.

---

## 1. Architecture

Three layers, matching the existing split.

**Domain (`Domain/Bot/`) — the brains, pure C#.**

- `IFightPolicy` — given a `FightSession` and the current actor, return one
  command (a small `FightAction` value: Attack(target) / Skill(index, target) /
  Item(name) / HoldBack).
- `IRunPolicy` — the out-of-fight choices: which node to walk to, which item
  offer to take, which relic to draft, whether to use a consumable, how to spend
  stat points in a profile preset.
- Archetypes as implementations. Phase 2 ships `RandomLegal` (fuzzer, stands in
  for a lost novice) and `GreedyAggressive`. Phase 6 adds `GreedyDefensive` and
  `Lookahead2` (one player action, one enemy reply, pick the best expected
  HP swing). The archetype gap is the difficulty curve in one picture.
- `RunTrace` — a plain record per fight and per run: seed, archetype, profile,
  step, floor, room type, enemies, turns, HP in/out, damage dealt/taken per
  turn, action chosen, payout, offer shown and pick made, death cause.
- `Invariants` — see §3.

**Core (`Core/Bot/`) — the hands.**

- `RunOrchestrator` (F2): `StartRun(seed, profile)`, `Draft(pick)`,
  `Choices()`, `MoveTo(node)`, `ResolveRoom()`, `BuildFight()`,
  `SettleFight(session, won)`, `RollOffers(nextIndex)`, `TakeOffer(index)`,
  `LegOver()/AdvanceLeg()`. The bodies come out of `FightBootstrap`,
  `MapController.Walk`, `FightController.Input` and `ReckoningController`;
  those files keep only their screen work and call the orchestrator.
- `BotRunDriver` — loops the orchestrator with one `IRunPolicy` and one
  `IFightPolicy`, plays each fight to `IsOver`, checks invariants after every
  command, appends to the `RunTrace`, stops on death or the depth cap. Runs
  under the same throwaway-save harness `FightAfterTheEliteTests.cs:39-48`
  already uses: `SaveSystem.RootOverride`, `Navigation.LoadOverride = _ => {}`,
  `RunManager.ResetForTests()`.
- `ProfilePresets` — builds a `SaveData` for Fresh / Mid / Late.

**Editor + tools — the batch.**

- `Editor/Bot/BalanceBotRunner.cs` — an `-executeMethod` entry reading
  `-botRuns N -botSeed S -botArchetypes a,b -botProfiles p,q -botOut dir`.
  Runs seeds × archetypes × profiles, writes `traces.jsonl` and `summary.json`.
- `tools/bot.ps1` — mirrors `run_tests.ps1`: robocopy main → `-TestRunner2`,
  launch batchmode with the flags above, copy the output back to
  `reports/bot/<timestamp>/`. Pure ASCII, no BOM.
- `tools/bot_report.py` — turns a batch's `summary.json` into one HTML page,
  with a delta column against the previous batch in `reports/bot/`.
  `reports/` is gitignored; takeaways worth keeping go into a dated
  `docs/BALANCE_NOTES.md` entry by hand.

**Tests.** `PlayMode/BalanceBotSmokeTests`: 20 seeds × each archetype on
Fresh, asserting zero invariant hits, zero exceptions, and that the same seed
twice yields an identical trace hash. Add `Bot` to the `combat` and `run`
patterns in `tools/test_areas.ps1` so the class is not an orphan.

---

## 2. What the report measures

Fun-shaped numbers first, win-shaped second. Each is per archetype × profile,
with the delta against the previous batch.

**Headline**

- Depth reached: distribution of death step; median, p10, p90; capped share.
- Death cause: enemy id, room type, floor; and "doomed" runs, where party HP
  never recovered above a threshold for the last N rooms before death.

**Fun**

- Decision pressure: share of fights, offers and drafts where the archetypes
  choose differently. A choice every archetype makes the same way is dead.
- Swing: share of turns where party HP moves by more than 25% of max. Too few
  is a slog; too many is a coin flip.
- Fight length per floor and per room type, and share decided on turn 1.
- Steamroll rate: fights with zero damage taken, per floor.
- Consumable use: how often the satchel is touched at all, and how many
  potions die unused with the run.
- Build diversity: distinct relic and talent sets among the deepest 10% of
  runs; item pick rate per item and per slot.

**Coverage**

- Enemies, relics, items and skills that never appear or are never chosen
  across the batch.

**Archetype gap**

- Depth reached by `RandomLegal` versus `Lookahead2` on the same seeds. This
  is the novice-to-expert spread, and the one graph to look at first.

---

## 3. What counts as a bug

The bot cannot tell a bug from a bad matchup. It can assert things that are
never legitimately true and hand back a seed. Each hit is one row in the
report's bug section: invariant name, seed, archetype, profile, step, node,
and the last ten actions of the trace.

- HP above max, or below zero on a living combatant.
- A fight that is not over, read as NEITHER SIDE'S TOTAL HEALTH FALLING from
  one command to the next for `FightRunner.StallCommands` (60) consecutive
  commands. This was a
  flat "not over after 200 player commands" until the bot started dressing
  itself: a Mid GreedyDefensive at the floor-3 boss took 201 commands to bring
  a Throne Colossus from 794 HP to 35, winning the whole way, and the check cut
  it off ten commands short of the kill -- 3,220 hits in one batch, each of
  them truncating a run the party was about to win into a false death. A
  command count never could have been the test: `DifficultyCurve` compounds
  enemy health 75 permille per step, so any fixed number is eventually too
  small for a legitimate deep fight. `FightInvariants.MaxPlayerCommands` stays,
  raised to 2000, purely as the runaway ceiling that stops a headless batch
  hanging on a session that has stopped answering.
  The first attempt at this asked whether either side had reached a NEW LOW,
  which is not the same question: an enemy that HEALS early sets its low before
  the heal, and a long winning grind afterwards is then measured against a
  floor the fight can no longer touch (seed 2, Mid/GreedyDefensive: the Forest
  Warden went 1112 -> 1853 -> 1557 and every command of the decline read as no
  progress). 1,795 more false rows. The step-to-step reading trades that for a
  false NEGATIVE -- an enemy that fully heals what the party chips off each
  round progresses every command and stalls forever -- which the ceiling still
  catches, and which is the right way round: a false positive silently
  truncates a winning run into a death and corrupts the depth median.
- A player turn with no legal action.
- `Payout` null after a win, or non-null before `IsOver`.
- A won fight after which `RunManager.Choices()` is empty and `LegIsOver()`
  is false.
- An offer with zero options, or an offer whose pick does not land in
  `stockpiledItems`.
- Gold, exp or level decreasing across a won fight.
- Any exception, anywhere, with the stack.
- Non-determinism: the same seed, archetype and profile giving a different
  trace hash. Reported once per batch, not per run.

---

## 4. Build order

Estimates are working time for one session. Total about six days.

### Phase 0 — characterization tests around the seam (half a day)

Pin what `OnFightEnded` does today, through the real `FightBootstrap`, in the
style of `FightAfterTheEliteTests`: a win banks gold and applies XP and clears
the room; a loss ends the run and clears gear; a boss win records the kill;
second lives spent are folded on both outcomes; HP writes back on both.
What changes: nothing in production. What must not change: the suite stays
green. How we know: the new tests pass against `d051b03`.

### Phase 1 — extract `RunOrchestrator` (one day)

Move the bodies listed in §1 out of the screens. `FightBootstrap`,
`MapController.Walk`, `FightController.Input` and `ReckoningController` call
it. The relic draft's weighted roll moves too.
What must not change: Phase 0 tests, `FightAfterTheEliteTests`,
`RunManagerTests`. How we know: full suite green with `-BuildScenes`, and a
manual run of one leg in the editor. Own commit. Update `docs/CODE_MAP.md`.

### Phase 2 — driver, two archetypes, trace, smoke test (one day)

`IFightPolicy`, `IRunPolicy`, `RandomLegal`, `GreedyAggressive`,
`BotRunDriver`, `RunTrace`, `BalanceBotSmokeTests` including the determinism
check. How we know: 20 seeds × 2 archetypes finish under ten seconds in
PlayMode and produce stable hashes across two runs.

### Phase 3 — batch entry and launcher (half a day)

`BalanceBotRunner`, `tools/bot.ps1`, `traces.jsonl`, `summary.json`, and a
plain-text summary printed at the end. How we know: `tools/bot.ps1 -Runs 500`
completes from a cold TestRunner2 and the summary matches the smoke test's
numbers for the shared seeds.

### Phase 4 — invariants and the bug section (half a day)

The §3 list, wired into the driver, with repro rows in the summary. How we
know: a deliberately broken invariant in a test fixture shows up as one row
with the right seed.

### Phase 5 — HTML report with deltas, and profile presets (one day)

`tools/bot_report.py`, previous-batch lookup, the §2 metrics, `ProfilePresets`
with `ClaimTrackRewards` and a fixed stat-point rule. How we know: two batches
on the same seeds show zero deltas; a batch after a content change shows
non-zero ones only where expected.

### Phase 6 — `GreedyDefensive`, `Lookahead2`, archetype-gap graph (one day)

How we know: `Lookahead2` reaches a deeper median than `RandomLegal` on every
profile. If it does not, the policy is wrong, not the game.

### Later, deliberately not in this plan

- A screen-driving adapter that replays a trace through the real uGUI tree,
  for UI bugs and art QA. Same policies, different hands.
- An LLM archetype that plays slowly and writes down what confused it.
- A meta-loop mode: many runs on one save, spending the wallet in the hub
  between them, to measure progression pacing rather than one descent.

---

## 5. Assumptions made here, to be corrected in passing

- Depth cap 5 legs. Mid and Late presets at levels 20 and 60.
- Item offers seeded from `(runSeed, step, node, rerollIndex)`; the bot never
  rerolls in v1.
- Damage variance left at production value.
- The bot fields whatever `SaveData.ActiveSquad()` returns for the preset;
  Shawn, Bjorn and Odette are the three fielded characters as of 2026-09-07
  (this line originally said only Shawn was real — stale, corrected by the
  2026-09-11 hunt against the bot's own batch data: `EquippedAtStart` carries
  all three on Fresh, and every one of their non-gated skills was cast at
  least once).
- `reports/` is gitignored; nothing generated is committed.
- Rest rooms are taken when party HP is under 50%, else the fight route.
  Treasure is always taken over a plain fight. This is `GreedyAggressive`'s
  map rule; `RandomLegal` picks uniformly.

### What a bot does between fights (added; replaces the two lines that said it does not)

The three lines this section used to carry -- stat points spent round-robin
over the six ability scores, talents never bought, gear never worn -- are all
gone. They were the reason every batch before `<this commit>` measured a
character in starting gear with zero talents, which is not a configuration the
game can produce: a Late profile is a level-60 character fighting in a level-1
kit unless something dresses it.

- **Gear.** `Core/Bot/GearEvaluator` scores a candidate by SIMULATING the
  equip through `ItemDescription.SimulateEquip` -- the same clone-and-resolve
  the character sheet's hover preview runs -- and reading the sheet's own
  numbers back off the clone. Five axes: offence, health, defence
  (physical+magical summed), speed, mana regen. Offence is the sheet's DMG
  line (`WeaponPower.DisplayDamage` of whatever is live in the main hand),
  NOT `StatBlock.attack`: `EffectiveStats` zeroes every worn item's attack
  contribution on purpose (balance redesign D3), so a weapon scored off the
  stat block prices at exactly zero. A candidate that would be inert the
  moment it is worn is not a candidate.
- **Preferences are the archetype's**, as a `GearWeights` vector on
  `IRunPolicy`: aggressive 4:1:2:1:0.5, defensive 1:1.5:8:1:0.5, Lookahead2
  forwards aggressive's, RandomLegal ranks NOTHING (an all-zero vector, so
  every legal candidate ties and the seeded rng draws between them -- which
  is uniform-among-legal falling out of the tie-break the other three already
  use). The same vector prices stat points and talents, deliberately: an
  archetype that fights defensively and levels aggressively is two archetypes,
  and the archetype gap is the one graph the batch exists to draw.
- **When.** One equip pass after the relic draft (which is also "at run
  start" -- the draft touches relics, never gear), and one after every
  `TakeOffer`. Nowhere else puts anything in the bag. Nothing is ever
  unequipped: `EndRun` clears gear and bag anyway.
- **Offers** are scored by the same evaluator and the score rides on
  `RunView.OfferScores`; `ChooseOffer` ranks on it and falls back to
  tier-then-plus when it is absent. The old tier-then-plus-only rule took a
  tier-3 helm over a tier-2 sword while holding nothing in either hand.
- **Level-ups.** After every won fight the driver calls
  `Character.ClaimTrackRewards()` (idempotent against its own watermark) and
  spends every `unspentStatPoints` one at a time, re-asking the archetype each
  time. Only the four GRANT kinds need claiming; every UNLOCK on the track --
  respec, wider offers, extra starting relics, second life -- is a pure
  function of `level`, and `SquadTrack` reads them straight through, so there
  is nothing else to collect.
- **Talents are PRESET-ONLY, and that is a fact about the game, not a
  shortcut.** Embers are paid only by `RunSettlement`, which runs only inside
  `RunManager.EndRun` -- so they arrive after the descent is over. Spending
  them needs the Talents scene, which `HubController` is the only thing that
  navigates to; the map screen can reach only Hub and Fight. So embers can be
  neither earned nor spent mid-run, and buying after each boss is not a thing
  a player can do either.
- **The ember budget.** One per boss the save had never killed before
  (`EmberPayout.PerUniqueBoss = 1`), recorded against `defeatedBossIds` in the
  same pass -- so the LIFETIME supply is not a rate, it is a fixed number
  equal to the count of live boss definitions. Rule: Fresh holds none (it has
  killed nothing); Mid and Late both hold the lifetime maximum, counted off
  the content rather than hardcoded. Mid and Late come out identical here
  because the game has nothing further to pay after the first of each boss.
  **This is a finding as much as an assumption**: the content ships three live
  bosses, one full talent path costs 45 embers and the per-character lifetime
  cap is 30, so the tree is currently reachable to a depth of about four orbs
  and no further.

## 6. Out of scope

Balance *changes*. The bot reports; the author decides. The first batch's
numbers are not a target, they are the baseline the second batch is compared
against.

## 7. How to run and read it

**Run a batch:**

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 1000 -Profiles Fresh,Mid,Late
```

Defaults to all four archetypes (`RandomLegal,GreedyAggressive,GreedyDefensive,
Lookahead2`), depth cap 40, a 10% determinism-replay sample, and
`Shards = min(4, processors/2)`. `-Runs N` is runs *per cell* (archetype x
profile), not the batch total -- 1000 runs x 4 archetypes x 3 profiles is a
12,000-run batch. Key flags:

- `-Archetypes a,b` / `-Profiles p,q` to narrow a cell.
- `-Shards N` for N parallel Unity instances, each its own project copy
  (`-Bot1`..`-BotN`, created on first use -- pays a full asset import once,
  then boots in ~20s). `-Shards 1` stays on the shared `-TestRunner2` copy and
  skips the copy machinery, for iterating on the bot itself.
- `-ReplayShare 0.3` replays more of the batch to catch non-determinism (costs
  roughly that fraction more wall clock); `1` replays everything.
- `-InMemorySaves 0` forces the save back to disk instead of RAM, only useful
  for proving the fast path did not change a run's hash (see
  `BalanceBotSmokeTests.TheInMemorySaveModeChangesNothingAboutHowARunPlays`).

**Where it lands:** `reports/bot/<timestamp>/` (gitignored) -- one
`shard-N/traces.jsonl` + `summary.json` per shard, merged by `tools/bot_merge.py`
into a top-level `summary.json`, then rendered to `report.html` by
`tools/bot_report.py`. The script prints a plain-text table (depth
median/p10/p90/capped/doomed/swing per archetype x profile), the bug rows (if
any, one per invariant hit with seed/archetype/profile/step/node and the last
ten actions), and the determinism check's mismatch count. `report.html` adds
per-batch deltas against the previous batch in `reports/bot/`. Takeaways worth
keeping go into a dated `docs/BALANCE_NOTES.md` entry by hand -- nothing under
`reports/` is committed.

**The smoke test** (`BalanceBotSmokeTests`, PlayMode, ~6s) is the correctness
gate, run on every full suite: 20 seeds each through RandomLegal and
GreedyAggressive, 10 each through GreedyDefensive and Lookahead2, all on
Fresh, capped at 16 steps. It asserts every run terminates (dies or caps),
trips no invariant beyond the one known pre-existing production fault
(`StalledEnemyTurn`, counted not failed on), and that the same seed replays to
an identical trace hash both across two runs and across the in-memory-vs-disk
save path. It is not a balance signal -- depth numbers only mean something at
batch scale -- it exists so the batch's numbers are worth reading at all.
Run it on its own with `tools/test.ps1 BalanceBotSmoke,BotPolicy`.
