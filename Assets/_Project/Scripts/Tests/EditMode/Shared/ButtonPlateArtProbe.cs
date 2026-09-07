using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // ButtonPlateArt is internal to PrincesPalace.Domain and this test
    // assembly carries no InternalsVisibleTo grant to it (same situation
    // KitContainerPlacementTests documents for ContainerArt.Key) -- but
    // Ui.PlateVisiblePad is the same public forwarder FightSubmenuLayout
    // itself calls, so this probe is not a second, possibly-drifting copy of
    // the numbers: it is the one production entry point, named per-shape for
    // readability at its one caller (UiKitVisiblePadTests).
    //
    // Moved to Shared/ (pre-existing structural gap, unrelated to Phase C --
    // found only because this phase's gate was the first run_tests_parallel.ps1
    // pass over this file since it was added): it carries no [Test] of its
    // own, and a helper living inside a test file's own class body is
    // exactly the shape Get-TestIndex's discovery does not recognise --
    // see tools/test_areas.ps1's own header, "a Shared/ folder per platform
    // for helpers that carry no tests of their own."
    internal static class ButtonPlateArtProbe
    {
        internal static ContentInsetFrac VisiblePad(ButtonPlateShape shape) => Ui.PlateVisiblePad(shape);
    }
}
