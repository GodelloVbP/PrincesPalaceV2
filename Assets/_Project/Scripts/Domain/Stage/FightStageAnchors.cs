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
        //
        // Y WAS -300/-140, INHERITED FROM v1 FROM BEFORE v1 FIXED IT. At -300
        // every front-row figure stood shin-deep in the HUD: a slot's pivot is
        // (0.5, 0), so Near.Y IS the ground line, and it sat 16 below the party
        // plate's top edge (-284) and 52 below the verb column's (-248), with
        // the contact ring hanging 8 lower still. The whole ground-contact
        // system -- ring, bloom, shadow -- was drawn underneath opaque panels.
        // It reads as an art problem and is a layout one.
        //
        // RE-DERIVED for v2 rather than pasted from v1, because the binding
        // constraint is whichever panel tops out highest HERE -- and v1's own
        // numbers do not close on v2's geometry: -170 puts the golem's head at
        // 130, well inside the bottom enemy plate.
        //
        // The band, measured rather than argued (tools/measure_stage.py):
        //
        //   floor    verb column top -248, +8 for the ring, +12 so it reads as
        //            clearance rather than as touching     -> Near.Y >= -228
        //   ceiling  the golem is the tallest actor at 384px above its own
        //            manifest ground line, so at the near slot's 0.78 it needs
        //            300 and its head lands at 72 -- which is why the bottom
        //            enemy plate had to rise with it (PlateFirstY 332 -> 380,
        //            putting that plate's lower edge at 96)
        //
        // Far moves by the same +72, so the depth spread is untouched. These
        // are the endpoints StageLayout interpolates between; changing their
        // separation would quietly re-tune the perspective while claiming to
        // fix an occlusion.
        //
        // KNOWN AND DELIBERATE: the detail column (top -186, x 308..648) still
        // covers the front enemy's feet while a submenu is open. Clearing that
        // too needs Near.Y >= -166, which does not fit -- the golem would then
        // need the plates at 430 and the ENEMIES heading 24px from the canvas
        // edge. The rule applied here is the one the handover states: clear
        // every ALWAYS-visible panel. Recorded as AUDIT #45.
        public static readonly UiVec Near = new UiVec(470f, -228f);
        public static readonly UiVec Far = new UiVec(250f, -68f);

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
