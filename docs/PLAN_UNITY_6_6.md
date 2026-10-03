# Plan: upgrade to Unity 6.6 (6000.6.2f1)

Rev 4, for independent review. Rev 4 answers the second review (findings 1, 2,
8) and the owner's answer on player builds. Rev 3 answered the first review
(findings 10-12). Repo claims were read on 2026-10-03; engine and package claims
cite their source.

## 1. Decisions taken

1. **Target editor:** 6000.6.2f1, already installed at
   `C:\Program Files\Unity\Hub\Editor\6000.6.2f1`.
2. **Built-in packages** move to the versions that editor bundles (read from its
   `Data/Resources/PackageManager/BuiltInPackages/*/package.json`):

   | Package | From | To |
   |---|---|---|
   | URP | 17.5.0 | 17.6.0 |
   | ugui | 2.5.0 | 2.6.0 |
   | test-framework | 1.7.0 | 1.8.0 |

3. **The two git packages keep their pins unless they break:** UIEffect `5.11.5`
   and ParticleEffectForUGUI `v4.13.3` (`Packages/manifest.json:3-4`).
   - UIEffect 5.11.1 already added 6.6 domain-reload support
     ([releases](https://github.com/mob-sakai/UIEffect/releases)).
   - If UIParticle breaks, bump it within v4 (`v4.14.1`). Never to the 5.0
     previews: 5.0.0-preview.19 changes the baking default view size from 10 to
     1000 ([releases](https://github.com/mob-sakai/ParticleEffectForUGUI/releases)).
4. **The upgrade is built and tested in a standalone local clone, not a linked
   worktree:** `C:\Games\Prince's Palace-v2-u66`, made with `git clone` from the
   main repo.
   - `run_tests_parallel.ps1` refuses linked worktrees outright
     (`tools/run_tests_parallel.ps1:131-138`), and `test.ps1` limits them to
     dotnet classes (`tools/test.ps1:121-126`).
   - A standalone clone has its own `.git`, so `Test-IsLinkedWorktree` does not
     fire. Its runner copies land beside it as `...-u66-TestRunner*`.
   - **Phase U0 proves this** before anything else depends on it.
   - A clone holds only committed files. Anything committed that needs an
     untracked asset is a main bug, reported and not worked around.
5. **The accepted candidate is exactly what merges.** No regeneration happens
   after acceptance. Main moves on while the clone is tested, so acceptance is
   repeated on the final integration base (U6).
6. **Engine comparisons are pinned to identical source.** A visual comparison is
   6.5 against 6.6 at the **same commit** (plus only the upgrade's own diff).
   Gameplay changes on main never show up as engine differences.

## 2. Contracts: what "accepted" means

All of the following hold at the final candidate commit (U6), not at an earlier
one.
1. **Compiles clean.** Warnings in the five project asmdefs are counted at the
   base commit on 6.5 and at the candidate on 6.6; the count does not rise.
2. **The full run is green** (`tools/run_tests_parallel.ps1`), plus
   `tools/graphics_tests.ps1`.
3. **Generators reproduce.** `-BuildScenes -BuildContent` on 6.6 against the same
   regeneration on 6.5 at the same commit differs only in classified
   serialization churn; every other difference is listed and explained.
4. **Visual comparison.** `PLAN_GIF_CAPTURE.md`'s `baseline` manifest is run on
   6.5 at the pinned base commit and on 6.6 at base + the upgrade diff, then
   compared with `-Compare -AllowDiff docs/upgrade_6_6_allowdiff.txt`.
   - Eligibility follows 2.6 there: the content manifests may differ only on the
     reviewed allow-list (`ProjectVersion.txt`, the package files, the
     regenerated scene and content trees, the reviewed code fixes).
   - Paths and editor versions differ by design and are printed.
   - I review every pair. The owner reviews the ones that differ.
   - The coverage table is attached, so what was not compared is visible.
5. **No editor-version mix in any Library the tooling launches** (U1).
6. **No focus stealing:** `tools/focus_check.ps1` (Runtime and Command modes) is
   clean on 6.6.

## 3. Steps, each with its gate

Order: **U1 lands on main first**, so the clone (U0) is born with the guard.

**U0: prove the clone is a supported test home.**
- Clone main after U1, then run `run_tests_parallel.ps1 -Changed` on 6.5. With
  no green-run record in a fresh clone, it promotes itself to a full run.
- Gate: no worktree refusal; runners created beside the clone; green. This is
  done before any upgrade edit, so a failure is about the clone, not the engine.
- The clone's Library ends U0 stamped 6.5 by the guard.

**U1 (on main): Library provenance guard.** The guarantee is narrowed to launches
the tooling controls, all of which pass through `tools/unity_path.ps1` (`:1-6`).
- **Stamp.** After a launch that exits successfully, the tool writes
  `Library/pp_editor.json`: the editor version and the launching tool.
- **Disposable copies** (`-TestRunner*`, `-Capture`, and the whole `-u66`
  family: the clone itself and its runners). The clone counts as disposable
  because it is mine, nobody opens it in the Hub, and it can be re-cloned.
  - Before deleting a Library, the tool takes the copy's existing runner claim
    (`Enter-RunnerClaim`, as `screenshot.ps1:156` does), so it never wipes a copy
    another run is using.
  - Then: no stamp means **unknown provenance**, and the Library is deleted. This
    is a one-time full import per copy, scheduled when the owner is away. A
    mismatching stamp is also deleted. A matching stamp launches.
- **Project trees** (main only):
  - The tooling never deletes their Library. A mismatching stamp refuses the
    launch, naming both versions.
  - No stamp is unknown: the launch proceeds with a warning, and the stamp is
    written after success. Unity itself owns a project Library the Hub opens; the
    tooling does not pretend to know its history.
- **Migration.** `tools/unity_path.ps1 -AcceptLibrary <projectPath>` rewrites a
  project tree's stamp to the version in its `ProjectVersion.txt`. It is run once,
  by the orchestrator, after the owner confirms the Hub opened that project
  cleanly in the new editor (U7).
- Gate: script tests for each row above (disposable/project × none/match/mismatch
  × claimed/unclaimed); `-Changed`.

**U2: first open on 6.6 in the clone.**
- Pre-step: no Editor is open on the clone (checked through
  `tools/unity_lock.ps1`'s lockfile and process check).
- Set `ProjectVersion.txt` to 6000.6.2f1 and bump the three built-ins.
- The guarded launch then sees the 6.5 stamp against 6.6 on a disposable copy,
  takes the claim and deletes the Library. Unity imports from scratch on 6.6.
  After a successful exit the stamp reads 6.6. No 6.5-built Library ever meets
  the 6.6 editor.
- Then a batchmode open-and-quit.
- Gate: no package or compile errors in the log. Whether batchmode needs an
  explicit upgrade flag is checked here, not assumed.

**U3: fix what broke.** Check first:
- `Editor/PipelineBuilder.cs:272-297`: it reaches the private
  `Renderer2DData.m_PostProcessData` by name. Breakage shows as its error log, and
  bloom goes missing.
- `Resources/Shaders/UIHitFlash.shader`, `UISpellGlow.shader`, the TMP URP
  shadergraphs, and the UIEffect and UIParticle shaders.
- Dynamic batching is obsolete in 6.6
  ([What's new](https://docs.unity3d.com/6000.6/Documentation/Manual/WhatsNewUnity66.html)).
  Is it enabled anywhere?
- `ProjectSettings/EditorSettings.asset` play-mode options unchanged (6.6 changes
  only new projects' default).
- Gate: contract 1.

**U4: tests and generators in the clone.**
- Gates: contracts 2, 3 and 6.

**U5: pinned visual comparison.**
- 6.5 run at the clone's base commit, 6.6 run at base + upgrade diff.
- Gate: contract 4.

**U6: final integration and re-acceptance.**
- Merge current main into the clone, and regenerate scenes and content **in the
  clone on 6.6**. The regenerated files are part of the candidate.
- Re-run contracts 1-3 and 6 on that exact commit.
- Contract 4 is re-run only if main changed visual code or content since U5. The
  6.5 side is rerun at the new base, so the comparison stays pinned.
- Other sessions must be idle from U6 until U7, so main does not move again
  under the candidate.
- If it does move, U6 repeats.

**U7: merge, on the owner's word only.**
- **Fast-forward main to the accepted candidate**, which includes the regenerated
  files, so what was tested is what lands. Also in that commit: CLAUDE.md line 3,
  `unity_path.ps1:43`.
- The owner closes any 6.5 Editor and opens main through the Hub in 6.6. Once
  the owner confirms it opened cleanly, I run `-AcceptLibrary` on main (U1).
- Runner Libraries are re-imported by U1 on their next launch.

## 4. Rollback

**Before U7:** delete the clone and its `-u66-TestRunner*` copies. Main is
untouched, apart from U1, which is harmless on 6.5.

**After U7:**
- **Record, at U7:** `M0` = main's sha immediately before the fast-forward;
  `C` = the accepted candidate sha. Because of the U6 idle rule, `C`'s
  integration of main is exactly `M0`, so the upgrade delta is exactly
  `git diff M0 C`. Both shas go into the U7 commit message and `AUDIT.md`.

1. **Reverse exactly that delta.** Not a chronological range, which could undo
   main work.
   - Apply `git diff C M0` to current main with `git apply -3`, but only for
     source paths: `ProjectVersion.txt`, package files and code fixes.
   - Generated scenes and content are not reverse-applied, because main may have
     regenerated them since. They are regenerated on 6.5 after the source revert.
   - I inspect the resulting diff, and it must touch only paths in the U7
     allow-list, before it is committed.
2. **Main's Library is now 6.6 and Unity does not downgrade a Library.** The
   owner deletes main's `Library/` before reopening in 6.5. That is the owner's
   action: the tooling never deletes a project tree's Library (U1). Then
   `-AcceptLibrary` again.
3. Runner and Capture Libraries are wiped by U1 on their next launch (stamp
   mismatch).
4. Gate: full run on 6.5 after the revert.

## 5. Risks

- **URP 17.6's changelog lists only fixes** for 17.5 to 17.6, and its 17.6.0 entry
  was still marked Unreleased when read
  ([changelog](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.6/changelog/CHANGELOG.html)).
  U3-U5 are the check, not the changelog.
- **Test Framework 1.8** may change `-runTests` CLI or timeout behaviour the
  tools depend on; U4 exercises them.
- **Stale worktrees** under `.claude/worktrees/` are out of scope.

## 6. Owner answers

- Player builds: not shipped or tested yet; the game is under heavy
  development and will be exported later (owner, 2026-10-03). No build gate in
  this upgrade. The first export after it needs its own build check.
