using System.Linq;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // ONE SEAM for "the squad member this fixture is testing" -- four
    // PlayMode suites (DossierRefundTests, DossierXpBarTests,
    // RewardApplierTests, RewardTrackClaimTests) each declared their own
    // private First(), two of them reading ActiveSquad()[0] rather than
    // skipping a null slot. Extracted once both shapes turned up doing the
    // same job so a fixture squad with a gap in it (a slot the save has not
    // filled yet) fails the same way everywhere instead of only where
    // somebody remembered to guard for it.
    internal static class SquadFixture
    {
        internal static Character FirstLiveMember() =>
            SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
    }
}
