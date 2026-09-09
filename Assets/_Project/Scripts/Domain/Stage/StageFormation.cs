using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // ONE SIDE'S LINE ON THE STAGE FLOOR: where its nearest actor stands and
    // where its farthest one does. Everything between is StageLayout's depth
    // curve run between those two endpoints.
    //
    // WHY THIS EXISTS AS A TYPE rather than as two more constants on
    // FightStageAnchors. The two sides of this stage are not mirror images and
    // never were -- they only looked like it while the difference was small
    // enough to express as one scalar (PartyRetreat, "the party stands 60px
    // further out"). The differences that actually matter:
    //
    //   * The party's floor has the HUD standing on it. The roster plates
    //     occupy x -920..-468 up to y -161 (measured off the real 4:3 capture,
    //     tools/screenshots/runtime/party_formation), and they are declared
    //     AFTER both stages, so they paint OVER a figure standing there. The
    //     enemy's half of the floor is bare down to the bottom of the frame.
    //   * The party is three similar bipeds ~200-230px wide once depth-scaled.
    //     The enemy side fields anything from a 235px-wide rat to a 592px-wide
    //     beetle, so what counts as "enough lateral separation" is a different
    //     number on each side.
    //
    // A formation is DATA, then, and the rule that reads it is code: one
    // depth curve (StageLayout), one composition (FightStageAnchors.SlotOffset),
    // two sets of endpoints. Adding a third side, or a formation for a boss
    // room that fields one huge actor, is another instance rather than another
    // branch.
    //
    // X IS AUTHORED AS A MAGNITUDE, positive outward from stage centre, for
    // both sides. Mirroring is the caller's (FightStageAnchors.SlotOffset's)
    // job, so nothing here has to remember which side it is describing --
    // which is what stops a future third formation being authored with its
    // signs already flipped.
    public readonly struct StageFormation
    {
        // Distance out from stage centre, and ground line, of the nearest slot.
        public readonly UiVec Near;

        // The same for the farthest slot. Farther is further OUT and HIGHER;
        // StageLayout.ScaleForDepth makes it smaller.
        public readonly UiVec Far;

        public StageFormation(UiVec near, UiVec far)
        {
            Near = near;
            Far = far;
        }

        // Where slot `slotIndex` of `slotCount` stands, as a magnitude-and-Y
        // pair in the same convention Near/Far are authored in.
        public UiVec OffsetForSlot(int slotIndex, int slotCount)
        {
            float depth = StageLayout.DepthForSlot(slotIndex, slotCount);
            return new UiVec(StageLayout.PositionForDepth(Near.X, Far.X, depth),
                             StageLayout.PositionForDepth(Near.Y, Far.Y, depth));
        }
    }
}
