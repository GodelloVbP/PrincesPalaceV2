// FIXTURE, not a test. See README.md.
//
// Sitting DIRECTLY in EditMode/, in no area folder at all. This is the first
// of the five structural refusals and the direct successor to the old orphan
// gate: a class here matches no area, so no `tools/test.ps1 <area>` slice and
// no -Changed run would ever reach it, and the only thing that would run it
// is the full suite -- which is exactly the invisibility the areas-are-folders
// scheme was chosen to make impossible.
//
// Get-TestIndex walks the area folders and nothing else, so this class is not
// discovered either. That is the point: the file is refused rather than
// adopted, because there is no correct area to adopt it into.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class LooseFixtureTests
    {
        [Test]
        public void InNoArea() { }
    }
}
