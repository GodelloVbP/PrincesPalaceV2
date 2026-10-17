# Review R1, cold reviewer (Sonnet, plan + repo only)

Verdict: approve-with-changes. 3 blockers, 7 major, 5 minor.

1. [blocker] Lane table leaves code unowned: Domain/Audio, Ambience, Stats, Equipment, Inventory, Glossary, Preview, Rng, DebugMenu, CollectionOps.cs; 14 Editor/*.cs; tools/githooks/pre-commit; tools/domain-tests. Nested Core/Bot, Core/Content precedence unstated.
2. [blocker] One shared tree, ~11 writers: Unity compiles every uncommitted edit; -ChangedPaths narrows tests not compilation. No Unity mutex (tools/unity_lock.ps1 exists), no red-gate attribution rule, no stop broadcast; P5 -BuildScenes rewrites scenes under other lanes.
3. [blocker] TESTING.md:32-34: tools/, .asmdef, scenes, generators promote -Changed to a full run. Every L9/L5 commit is a full run; batch them.
4. [major] Coverage in two places (notes coverage line vs MANIFEST); vocab mismatch; hunt_manifest.py re-opens rows just fixed. Pick one, map, run once after last commit.
5. [major] BRIEF_COMMON.md does not exist and has no owner; Decided list lives in the appendix.
6. [major] Merge script and slicer unowned/unspecified; SLICES.md files column is names only, ambiguous for same-named files.
7. [major] ContentData JSON, scenes, pre-commit hook, asmdefs, shaders read by no Sonnet finder. P0 must copy test-profile CSVs (run_tests_parallel.ps1:762 deletes them).
8. [major] SerializeField changes in L2-L4 need -BuildScenes (CLAUDE.md); add hand-off to L5, done withheld until rebuild green.
9. [major] Gates not executable: no check command for coverage, no empty-case rules, no retry cap for partial.
10. [major] "Only asserted clears a BUG" vs untestable Core/presentation bugs; severity table incomplete.
11. [minor] FightController.Hud.cs and StageVisuals are T2 despite yield area.
12. [minor] Cost uncosted; cache-sharing lever weakened by 5-min TTL across waves; give exact wave lists.
13. [minor] Pre-existing dirty files (4 scenes, Velvet Shackles, run_up) unaddressed.
14. [minor] two-failed-cycles must exclude failures caused by others' edits.
15. [minor] H4 numeric-literal pattern not grep-shaped, risks balance numbers; H3 "UiKit" path ambiguous.

Verified OK: run_tests_parallel flags, bot.ps1 -Runs, bc867981/438d6fda resolve, every non-test file appears in SLICES.md, lane prefixes match real files.
