# Review R2, warm reviewer (resumed R1 warm)

R1 points: fixed w1-w9, w11-w14, w16-w20; partly w10 (cost), w15 (sources). Verdict: approve-with-changes (1-4 before launch, 5 before cost goes to owner).

1. [blocker] Sparse cone breaks [D] classes: 32 EditMode files read Art/, Resources/, Scenes via RepoTree/PngAlpha/dataPath (~70 literal paths). csproj itself only globs Domain + Tests/EditMode. P0 W proof must use a disk-reading class; add subtrees or tag those classes integrator-only. Each worktree does its own restore/build.
2. [major] Tests/** owned by no lane but L10, yet every BUG fix writes a test. Own tests by mirrored directory; name an owner for Tests/*/Shared (full-run trigger, test_areas.ps1:721).
3. [major] [U] two-commit rule puts red on main (breaks "commit only passing"), misleads bisect, doubles Unity runs, leaves an unproven fix on main. Land as one commit; integrator proves red with the fix hunk removed before merging.
4. [major] Integrator loop: revert conflicts deadlock with "never resolve/reset"; bisect steps are serial Unity runs; lanes need a re-sync rule after revert; a stray finder edit stalls the integrator forever ("nobody reverts").
5. [major] Cost: baseline is ~85 turns x ~212K (~18M/agent) -> ~1.5B for finders, not 490M. Count is 82 not 81.
6. [minor] Sources: #124, #125 not struck; #125 is styleWeights, cite the item redesign plan; party max needs a better source; Co-Authored-By names Opus for Sonnet agents.
7. [minor] PcPlateSprites/BeaconPulse/StaticSwing in L2 without evidence (PcPlateSprites looks like party UI). docs/handoffs/cleanup_sweep/ falls in L11 but the script author writes there; lanes.py scope misses root CLAUDE.md/AUDIT.md.
8. [minor] test_select promotes at n > 5, so with 5-commit batches full runs land about every 10 commits; say so.
