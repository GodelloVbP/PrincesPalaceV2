# Run map P2: what is left after the core commit

Plan: `PLAN_RUN_MAP.md` rev 5 §8 P2 row. The core landed in `9f26fc17`:
realm opening, step/`roomsWalked`, skip heal, frozen strength, chip payout,
save 8 -> 9 with its tests, column projection, every test off `DescentMap`,
`CODE_MAP`/`ARCHITECTURE` rows. Run and ui areas green; full run green except
`ContentFreshnessTests` (item phase I6's uncommitted content inputs, not P2).

Shared tree: item phase I6 has uncommitted edits in `FightEncounterAdapter.cs`
and `GreedyDefensivePolicy.cs` (their P2 hunks are committed), the talent and
effect files, `relics.json`, `talents.json`, `CONTENT_SCHEMA.md` and
`Resources/Content/`. Do not stage them. Commit with
`git commit --only -- <paths>`.

## 1. Scenes

- Files: `Assets/_Project/Scenes/Map.unity` (tree changed by
  `Domain/UiKit/Screens/MapScreen.cs` in `9f26fc17`). The tree's `Map.unity`
  is already modified; nobody knows which tree built it.
- Do: `tools/run_tests_parallel.ps1 -Changed -BuildScenes -ChangedPaths
  Assets/_Project/Scripts/Domain/UiKit/Screens/MapScreen.cs`. Commit
  `Map.unity` only. `Talents.unity`, `Hub.unity`, `Fight.unity`,
  `MainMenu.unity` changes are I6's or noise: leave them. Diff `Art/` after
  the build (CLAUDE.md gotcha 3) and commit nothing from it unless the map
  needs it.
- Verify: gate green apart from `ContentFreshnessTests` while I6 is
  uncommitted; `git show --stat` lists `Map.unity` alone.

## 2. Map capture (owner permission only)

- Do: only if the owner allowed `-Runtime` this session:
  `tools/screenshot.ps1 -Runtime -RuntimeFilter MapCaptureTests`. Copy the
  PNGs out before any second run (they are wiped per run).
- Verify by eye: every node visible and clickable, anchors drawn as elites,
  pocket rooms in their rejoin column, no tile overlap in the crowded realm.

## 3. Test selection rows

- File: `tools/test_areas.ps1:890`, row
  `'^Assets/_Project/Scripts/Core/(DescentMapView|Map)'`.
- Do: replace `DescentMapView` with `Realm` so `Core/RealmOpening.cs` and
  `Core/RealmPrizes.cs` select `run` and `ui` (they fall to the `Core/`
  catch-all, `ui` only, today; `RealmPrizes` also reaches
  `Tests/PlayMode/Combat/FightSettlementTests.cs`, so add `combat` too). Add
  `MapColumns` to the `Domain/UiKit/(MapLayout|RewardTrackLayout)` row (`:841`).
- Verify: `tools/test.ps1 -List -SelfCheck` prints `SELF-CHECK: ok`;
  `tools/test.ps1 -Changed` with only `RealmOpening.cs` touched lists
  `RealmRunTests`.

## 4. Stale names

- `tools/process_map_icons.py:2,49`: docstring/comment names
  `DescentMapView`; the reader is `Core/MapController.cs`. Comment-only.
- Phase names in test comments (CLAUDE.md: history goes in commits):
  `Tests/EditMode/Run/RealmP2RulesTests.cs:10`,
  `Tests/EditMode/Ui/MapNodeCardTests.cs:73`,
  `Tests/PlayMode/Combat/FightSettlementTests.cs:104`,
  `Tests/PlayMode/Run/EventFightRunTests.cs:590` ("Stage B's M4 bumped..."),
  `Tests/PlayMode/Run/MapCaptureTests.cs:87`,
  `Tests/PlayMode/Run/RealmRunTests.cs:12`. Say what the code covers, cite
  the plan section, drop "run map P2".
- Verify: `git grep -n "run map P2" -- '*.cs'` is empty; comment-only diff
  passes `python3 tools/check_comment_only_diff.py --cached`.

## 5. Bot smoke

- P2 changed only the policy signatures (`DescentNode` -> `RealmNode`) and
  `BotRunDriver`. `MapView` and path-aware choices are P3.
- Do: one short `tools/bot.ps1` run on floor 1 (few seeds) to prove a whole
  realm walks to the heart and `AdvanceLeg` opens the next.
- Verify: the report completes with no exception and runs reach floor 2.

## Open calls (orchestrator or owner, not the implementer)

- **Strength thresholds.** EV sheet §1 says: if the real party weight is
  `P'`, set `T = 0.34 x 4560 / P'` (0.61 likewise). The starting squad logs
  `P' = 5760` (5900 in one fixture), giving 0.27 / 0.48. `NodeStrength.cs`
  keeps 0.34 / 0.61, and `RealmRunTests.TheStartingSquadsFloorOneStrengthMixIsPinned`
  pins the resulting floor-1 mix at 79/21/0. Rescale or keep is a tuning call
  for the P3 bot report.
- **`restBeforeBoss` is frozen into the profile at realm open**
  (`RealmProfiles.ForRealm`). A mid-realm grant applies from the next realm.
  Nothing grants it today.
- **`ContentFreshnessTests`** fails until I6 commits with `-BuildContent`.
