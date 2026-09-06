# tools/test_areas_fixture/ -- a deliberately broken test tree

Eight files -- six wrong in exactly one way, two deliberately right -- so that
`tools/test.ps1 -List -SelfCheck` can point discovery at this folder instead
of `Assets/_Project/Scripts/Tests` and watch every refusal in
`tools/test_areas.ps1` fire. A gate nobody has seen fail is a gate nobody
knows still works: each of these cases matches a hole that was real in this
repo, and the check exists so that widening a regex or reshaping a function
cannot quietly turn one of them back off.

**Nothing here is compiled by anything.** It sits under `tools/`, outside
`Assets/`, so Unity never imports it, `PrincesPalace.Domain.Tests.csproj`
never globs it (that glob is rooted at `Assets/.../Tests/EditMode`), and the
real structural gate never sees it. The `.cs` files are valid C# only so they
read like the thing they stand in for; they reference NUnit types that are
not on any compile path here.

| File | What it is wrong about | Which function must say so |
|---|---|---|
| `EditMode/Ui/TestCaseOnlyFixtureTests.cs` | nothing -- it is the control: a fixture whose every case is `[TestCase]`/`[TestCaseSource]`, which the old `$TestAttrPattern` did not count as a test file | `Get-TestIndex` must discover it |
| `EditMode/Ui/InternalFixtureTests.cs` | an `internal` fixture beside a public one, so "any class discovered" passed the file | `Get-DiscoveryBlindSpots` |
| `EditMode/Combat/` + `PlayMode/Combat/DuplicateNameFixtureTests.cs` | one class name, two files, two platforms | `Get-DuplicateClassNames` |
| `EditMode/Run/Nested/DepthTwoFixtureTests.cs` | a folder inside an area folder, and a file two deep | `Get-StructuralViolations` (twice) |
| `EditMode/Shared/SharedSuiteFixtureTests.cs` | a suite parked in `Shared/`, carrying only `[TestCase]` | `Get-StructuralViolations` |
| `EditMode/Art/UnfilterableFixtureTests.cs` | a generic fixture and a public nested one -- NUnit names them `Foo<T>` and `Outer+Inner`, which no filter this repo builds can select | `Get-StructuralViolations` (twice) |
| `EditMode/Shared/QualifiedAttrFixtureTests.cs` | a suite in `Shared/` writing `[NUnit.Framework.Test]` in full, which the unqualified `$TestAttrPattern` could not see at all | `Get-StructuralViolations` |
| `EditMode/Shared/CommentedOutAttrFixtureTests.cs` | nothing -- the second control: a helper whose only `[Test]` is in a comment (and one more in a string), beside an internal class. Neither refusal may fire, because the attribute scans read the code with comments and literals stripped | `Get-StructuralViolations` and `Get-DiscoveryBlindSpots` must both stay quiet |

The self-check asserts the exact COUNT of each list as well as its contents,
so a refusal that starts over-firing fails here too. Adding a case means
adding its expectation in `tools/test.ps1`'s `Invoke-SelfCheck`.
