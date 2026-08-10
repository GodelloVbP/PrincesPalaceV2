using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // Where the combat stage's actors sit, in canvas pixels.
    //
    // These were SceneBuilder.FightStage.cs constants in v1, and StageLayout's
    // own header said the anchors "stay in SceneBuilder" because Domain had no
    // business knowing canvas pixels. **That note is formally superseded**: in
    // v2 the screen TREE lives in Domain, so a position it needs has to be
    // reachable from Domain or it goes back to being arithmetic at a
    // construction site -- which is the thing the whole construction layer
    // exists to end.
    //
    // StageLayout still owns the depth CURVE (what fraction of the way back a
    // slot sits, and how much smaller that makes it). This owns the two
    // endpoints that curve is measured between, and composes the two.
    public static class FightStageAnchors
    {
        // The nearest slot's offset from stage centre, and the farthest slot's.
        // Everything between is interpolated by depth.
        public static readonly UiVec Near = new UiVec(470f, -300f);
        public static readonly UiVec Far = new UiVec(250f, -140f);

        // Applied on top of StageLayout.ScaleForDepth. The art is authored
        // larger than it is shown, so this is the one global shrink.
        public const float SpriteScale = 0.78f;

        // How far below a slot's own origin its nameplate hangs.
        public const float NameplateOffset = -34f;

        public static readonly UiVec StageSize = new UiVec(1000f, 600f);

        public const float InitiativeIconSize = 74f;
        public const float InitiativeIconGap = 8f;
        public const float InitiativeRingPadding = 10f;

        // The 8-bit encodings of v1's float colours, accurate to 1/255 -- close
        // enough for a translucent ground shadow, and the DSL speaks hex.
        public const string AllyShadowColor = "#59D966D9";
        public const string EnemyShadowColor = "#E63340D9";

        // A slot's offset from stage centre. `mirrored` flips X for the party
        // side, which faces the other way.
        public static UiVec SlotOffset(int slotIndex, int slotCount, bool mirrored)
        {
            float depth = StageLayout.DepthForSlot(slotIndex, slotCount);
            float x = StageLayout.PositionForDepth(Near.X, Far.X, depth);
            float y = StageLayout.PositionForDepth(Near.Y, Far.Y, depth);
            return new UiVec(mirrored ? -x : x, y);
        }

        // The scale a slot's sprite is drawn at: the depth curve times the one
        // global shrink. v1 composed these at the construction site; composing
        // them here is what lets a test pin the result.
        public static float SlotScale(int slotIndex, int slotCount)
        {
            return StageLayout.ScaleForDepth(StageLayout.DepthForSlot(slotIndex, slotCount)) * SpriteScale;
        }
    }
}
