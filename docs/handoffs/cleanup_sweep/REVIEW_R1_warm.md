# Review R1, warm reviewer (Sonnet, with past-sweep context)

Verdict: approve-with-changes (items 1-5 before launch).

1. [blocker] test_select.ps1:719-731 Get-FullRunPromotion promotes any -Changed gate to full after 5 commits/24h since .last-full-green: 11 lanes all promote at once. Only the verifier's green should write it, or pause lanes.
2. [blocker] Full run and "red stops lanes" test other lanes' uncommitted edits: false reds, misattribution. Integrate from a clean HEAD (worktree) or require lanes clean.
3. [major] Finders read a moving target: pin base commit, cite symbol + line, re-verify excerpt at HEAD in triage.
4. [major] Unowned: Domain/Equipment, Stats, Inventory, Audio, Ambience, Glossary, Preview, Rng, DebugMenu, Domain root; Editor/*.cs outside SceneBuilder/Bot/ContentBuilder; tools/domain-tests.
5. [major] RunOrchestrator.Spells.cs in Core/ (L3) vs other partials in Core/Bot (L8): one class, two writers.
6. [major] L4 catch-all holds fight-adjacent files (Stance*, Focus*, FrameSequenceLoader, ContactCues, SquadTrack, PreviewFight); FightHud spans L1/L2/L5.
7. [major] L9 owns the gate scripts every lane runs: freeze them, fix last, then runner-trust check.
8. [major] H1-H4, H7 are grep-shaped: a script does them deterministically at zero tokens; Haiku only for H5/H8.
9. [major] Missing: unreferenced assets, assembly/layering checks, a PERF measurement route, test-suite quality beyond H6/H10.
10. [major] Cost model wrong: baseline ~85 turns x ~212K; a 3K slice is 40-50K tokens itself. Expect ~3x and some partials.
11. [major] Simultaneous launches do not share one cache write; launch one, then the rest.
12. [major] Promote S04/S05/S09 (FightController.Hud, StageVisuals) to T1.
13. [major] One LEDGER.tsv with 11 writers races: per-lane ledger files.
14. [minor] Commit recipe omits new .meta; git add then commit --only.
15. [minor] Decided list unsourced; create BRIEF_COMMON.md with sources (warm questioned extra-turn, Shatter, Bloodlust lines).
16. [minor] Finders have Write: check git status after each wave; name who writes the scripts.
17. [minor] MANIFEST hand-update of 74 notes is large; script it or drop D8.
18. [minor] -BuildScenes -BuildContent on every integration run dirties Art metas; only when L5/L6 landed.
19. [minor] 129.5K September figure excluded tools.
20. [minor] Unity concurrency/queue undescribed (unity_lock.ps1).
