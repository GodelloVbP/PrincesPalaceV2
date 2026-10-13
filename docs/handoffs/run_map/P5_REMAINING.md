# Run map P5: remaining work (implementer)

Plan: `PLAN_RUN_MAP.md` rev 5 §6, §7 (Heart), §8 P5 row; `PLAN_ITEMS.md` §5
amendment 4. Core landed in `a929520c`; the contract is stated in the headers
of `CombatEncounter` (reinforcements and the leader), `FightSession`
(`QueueReinforcement`, `MarkAnchor`, `AdvanceTurnAndSeat`) and
`Domain/Dungeon/HeartAnchors.cs`. Read those, not this file, for the rules.

Every item below is mechanical. Do not change the contract. If an item needs
it changed, stop and report back.

## 1. Heart drawing on the map screen

- **Where:** `Domain/UiKit/Screens/MapScreen.cs` (tree), `Editor/SceneBuilder/ScreenRegistry.cs`
  (wiring), `Core/MapController.cs` (paint).
- **What:** §7 Heart: the boss in the rear, the first `HeartAnchors.FrontCount`
  anchors in front, the rest as a row of smaller portraits behind (at most 4
  at the 6-anchor ceiling). Source: `RunEncounter.LivingAnchors(run)`. It
  already gives formation order and leaves out `slainAnchorIds`. Greybox
  roots run from the heart to each anchor node. A root withers when its id is
  in `slainAnchorIds`.
- **Verify:** `tools/run_tests_parallel.ps1 -Changed -ChangedPaths <paths> -BuildScenes`,
  then diff `Art/`. An EditMode `UiAudit` test of the tree at four aspects. A
  `-Runtime` capture of the map with 0, 2 and 5 living anchors (allowed this
  session; copy the PNGs out before a second run).

## 2. Survivors leave the stage on `LeaderFell`

- **Where:** `Core/FightController.Rounds.cs:281` (the `Survived` fade), and
  the ordinary end path for a fight with no darkness.
- **What:** when `_session.EndReason == FightEndReason.LeaderFell`, play
  `DeathFadeFor(enemy)?.PlayIfNotAlready()` for every living enemy before the
  Reckoning, as `Survived` does. The queued enemies were never on the stage.
- **Verify:** a PlayMode test in `Tests/PlayMode/Combat/`. Kill the leader
  with an anchor standing, then assert the anchor's slot fade has played.

## 3. Tests still owed

All go under `Tests/<Platform>/<Area>/` (`.claude/rules/tests.md`). Pin
literals.

- **Adapter** (PlayMode, `Combat`): `FightEncounterAdapter.Build(isBoss: true, heartAnchors: 4 anchors)`.
  `Encounter.Enemies` is `[a0, a1, boss]`. `Reinforcements` is `[a2, a3]`.
  `Leader` is the boss. Each anchor kit has `IsElite`. An anchor's `MaxHealth`
  equals `HeartAnchors.HealthAtHeart` of the elite room figure: compute the
  room figure from a second `Build(isElite: true)` of the same id, not from
  the formula.
- **Roster** (PlayMode, `Run`): `RunEncounter.For` on the heart node gives
  `Anchors` = every anchor node not in `slainAnchorIds`, in formation order.
  Each anchor's `EnemyId` equals the id that `RunEncounter.Preview(run, node)`
  rolls. A non-boss room gives an empty list.
- **Settlement** (PlayMode, `Run`): a won boss fight where two heart anchors
  died, one carrying a Gold chip and one a Gear (`PrizeKind.Item`) chip, plus
  one that left alive. Both dead ids land in `slainAnchorIds`. The gold chip's
  amount is credited. The live one is not slain. The payout includes the dead
  anchors' elite exp. The Domain half (Gear chip queued, map order) is pinned
  by `HeartAnchorFightTests.AHeartAnchorsGearChip_IsQueuedForTheBossSettlement_InMapOrder`.
- **Stage hand-over** (PlayMode, `Combat`): a reinforcement takes slot 0 after
  the corpse's fade finishes. The slot shows the newcomer at full opacity, and
  its nameplate reads the newcomer's name.

## 4. Doc rows

- `docs/CODE_MAP.md` realm map row: add `Domain/Dungeon/HeartAnchors.cs`,
  `RunEncounter.LivingAnchors`, `RunOrchestrator.PayHeartAnchors`.
- `docs/ARCHITECTURE.md` combat section: one line on enemy list index = stage
  slot, reseat at the turn boundary, and the leader ending. Link the
  `CombatEncounter` header; do not restate it.
- `PLAN_RUN_MAP.md` §8: mark P5 core landed (`a929520c`) and point to this file.

## 5. Final gate

`tools/run_tests_parallel.ps1 -Changed -ChangedPaths <all P5 paths> -BuildScenes`.
`ContentFreshnessTests` fails until item I2a/I6 commits with
`-BuildContent`. It is not a P5 failure, so name it in the commit.

## 6. Bot report re-run (P3 gate, P5 half)

- **What:** the P3 bot report at floors 1, 10 and 30, plus the boss-fight win
  rate bucketed by anchors left alive (0, 1, 2, 3+). The count is
  `roster.Anchors.Count` at `RunOrchestrator.BuildFight` for a boss room. The
  trace field belongs in `Core/Bot/BotRunDriver.cs` (the P3 owner's file;
  coordinate). The report column goes in `tools/bot_report.py`, with a fixture
  in `tools/bot_report_fixtures/`.
- **Tune:** `HeartAnchors.HealthPermille` (500 today, a placeholder). Target:
  win rate falls as anchors left alive rises, without reaching ~0% at 2.
  Report the numbers. Changing the literal is the orchestrator's call.
- **Verify:** `python tools/bot_report_test.py`, then the report itself.
