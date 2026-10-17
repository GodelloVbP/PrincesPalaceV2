# Review R2, cold reviewer (fresh, plan + repo only)

Verdict: approve-with-changes (3 blockers).

1. [blocker] Worktrees run only named [D] classes (test.ps1:567, run_tests_parallel.ps1:141); [D] = Domain + Tests/EditMode (domain-tests csproj 21, 55). Core (332 files), Data, Editor, Core/Bot, Core/Content get no compile and no test: L2, L3, L4, L7, L8, L13, half of L6.
2. [blocker] Integrator pre-check can never pass: sweep notes/scan/ledger dirs, plan docs, scenes live in the same tree. Commit or move them; define foreign-dirt handling.
3. [blocker] Tests unowned (only L10 "no production change"); Shared and FightRunUpTests shared across lanes.
4. [major] [U] red/green two commits on main: double gates, red on main, compile-error red indistinguishable. Name the failing test; do it off main.
5. [major] -ChangedPaths does not avoid promotion (test_select.ps1:62, run_tests_parallel.ps1:280): 5-commit batches always promote; bisect steps promote too.
6. [major] Bisect/revert: no flake rerun; revert + rebase drops the lane's patch-identical commit silently; no terminal state after senior fails.
7. [major] Two integrators can overlap; single slot with a lock.
8. [major] "No agent waits >5 min" false for integrators; final full run unassigned; integrator budget unsupported.
9. [major] 2>/dev/null hides commit failures; rebase conflicts need abort; Co-Authored-By names Opus for Sonnet agents.
10. [major] P0 W proof too weak: RepoReadPattern (test_select.ps1:70) classes fail in the cone; run full [D] suite.
11. [minor] Run* matches RuntimeNavWiring; Shaders/Art/Audio/Resources/Scenes/asmdefs/Packages/ProjectSettings unowned though X6/G1 report on them.
12. [minor] Ledger location ambiguous (worktree vs main).
13. [minor] SLICES.md stale vs plan (S04/S05/S09 still T2, pre-commit missing, section ref wrong); count 82 not 81.
14. [minor] Base c8d0c631 vs HEAD 8c19867e: say why.
15. [minor] X1/X6 must implement every "dead" reference form; fixtures per form.
16. [minor] "And more" thin: localisation, ProjectSettings, scenes.
