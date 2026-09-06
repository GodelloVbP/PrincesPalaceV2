# The dotnet host for the EditMode suite

Runs the EditMode tests without Unity. Same source files, same assertions,
about a tenth of the time.

## Why it works at all

`Assets/_Project/Scripts/Domain/PrincesPalace.Domain.asmdef` declares
`"noEngineReferences": true` and an empty `references` list. Domain is
therefore plain C# over the BCL, and so is nearly all of the EditMode suite
written against it. Nothing about that code needs an editor, a Library
folder, a project copy or an asset import — and those, not the assertions,
were the entire cost.

Measured on 2026-09-02, at 2384 EditMode tests:

| | Unity | dotnet |
|---|---|---|
| the tests themselves | 10.6 s | ~1 s |
| everything else (boot, import, project sync) | ~20 s and up | ~2 s incremental build |
| one class (`test.ps1 wool`) | ~12 s | ~3.7 s |

PlayMode is untouched and stays in Unity. It needs a scene and a running
player loop, and it is where the remaining time in a full run lives (181 s of
the 236 s gate).

## Shared source, never copied

Both `.csproj` files include the real files by path out of `Assets/`. There is
exactly one copy of every Domain file and every test file in this repo. A test
edited for one host is edited for both, because there is only one of it.

- `PrincesPalace.Domain/` compiles `Domain/**/*.cs` with **no exclusions**, at
  `netstandard2.1` and `LangVersion 9` — the ceiling Unity 6 compiles it
  against. A Domain file that needs an exclusion to build here is a layering
  violation (it reached for the engine) and belongs in `Core/`; add the
  exclusion only after moving the file is ruled out, and say why in the
  csproj.
- `PrincesPalace.Domain.Tests/` compiles `Tests/EditMode/**/*.cs` minus the
  files listed below. The glob is recursive, so the per-area subfolders
  (`Tests/EditMode/Combat/`, `.../Content/`, ...) need nothing added when a
  file moves between them — only the exclusion paths below do.

## The exclusion policy

**A test stays Unity-only when it needs the ENGINE, not merely when it
mentions one.** Three files qualify today:

| File | Why |
|---|---|
| `EditMode/Content/EnemyContentPinTests.cs` | `JsonUtility` over the real `enemies.json` |
| `EditMode/Combat/ForestWardenTests.cs` | `UnityEngine.JsonUtility`, fully qualified rather than through a `using` |
| `EditMode/Content/ItemModifierStorageTests.cs` | `JsonUtility` save-compat — the thing under test *is* Unity's serializer |

(A fifth, `RigAnimationContentTests.cs`, was deleted with the rig pipeline in
`a9c0d49`; its exclusion and its row here outlived the file by four months and
went with the folder-per-area move. A fourth, `EditMode/Art/HandAssembledArtTests.cs`,
was excluded for `Application.dataPath` — reading committed art off disk is
fine here; asking the *engine* where the project is was the problem, and
`Shared/RepoTree.cs` already answered it for both hosts.)

`tools/test.ps1 -List` marks every class `[D]` or `[U]`. That marking is read
out of `PrincesPalace.Domain.Tests.csproj`, not maintained separately, so the
csproj stays the single source of truth for the split.

Adding to the list costs coverage in the fast loop. Before you do:

- Reading a **file** off disk is fine here and needs no exclusion. Six test
  classes already walk up from `Directory.GetCurrentDirectory()` to find
  `Assets/_Project/...` and read it with `System.IO`; `dotnet test` runs from
  inside the repo, so that works unchanged.
- `Mathf.Sqrt`/`Mathf.Abs` on the assertion side is fine too — see
  `PrincesPalace.Domain.Tests/MathfShim.cs`, whose header is strict about what
  may ever be added to it. Anything with engine state, time, randomness,
  serialization or assets must be excluded instead, because a shim for those
  lets a test pass here for a reason it would not pass in Unity.

## Where the two hosts can still disagree

Worth knowing, because a green run here is not quite a green run in Unity:

1. **NUnit version.** Unity ships `com.unity.ext.nunit` 2.1.0 —
   `nunit.framework.dll` 3.5.0.0, "based on NUnit 3.5". Nothing on NuGet ships
   3.5 in a form `dotnet test` loads, so this pins **3.14.0**, the last 3.x.
   The difference is additive for the surface this suite uses, but an
   assertion added after 3.5 compiles here and not in Unity.
2. **Test-side BCL.** The test project targets `net10.0` (the only runtime
   installed). The Domain project it references is `netstandard2.1`, so
   production code is still held to Unity's ceiling; test files are not.

Both are caught by `tools/run_tests_parallel.ps1`, which still compiles and
runs everything in Unity. That is why it stays the gate.

## Running it

```
dotnet build tools/domain-tests
dotnet test  tools/domain-tests --no-build
```

Normally you do not: `tools/test.ps1` routes to this host automatically, and
`tools/githooks/pre-commit` builds it whenever `Domain/` or `Tests/EditMode/`
is staged, so a new test file that reaches for the engine fails at commit
rather than at the next full run.
