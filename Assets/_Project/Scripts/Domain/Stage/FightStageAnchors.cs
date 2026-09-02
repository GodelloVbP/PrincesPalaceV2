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
        // X NOW RUNS OUTWARD WITH DEPTH, 300 -> 565, where it used to run
        // inward, 470 -> 250. Two things were wrong with running inward and
        // only one of them was the obvious one.
        //
        // The obvious one: both armies receded toward a shared vanishing point
        // at screen centre, so "further back" meant "further in" and the two
        // back ranks stood 500 apart while the two front ranks stood 940
        // apart. Whatever a back row is, it was the closest thing to the enemy.
        //
        // The one that actually mattered: OCCLUSION. Measured against the real
        // sheets, the middle enemy was 45% visible -- the golem is 525px wide
        // and swallowed the rat standing behind it -- while the back enemy was
        // fully visible. That reads as one monster being half-hidden for no
        // reason rather than as depth. Running the line outward instead
        // separates the three laterally and takes the worst case from 45% to
        // 67% (tools/measure_stage.py, and scratchpad mockups against the real
        // backdrop before any of this was written).
        //
        // Four formations were composited before this one: the original, a
        // straight reversal, a tight two-rank stagger, and this. The two-rank
        // version was my own suggestion and measured WORST of the four at 28%,
        // because ranking figures up stacks them almost exactly on top of each
        // other. Rendering it was cheaper than arguing about it.
        //
        // Y still rises with depth and still respects the same band:
        //
        //   floor    verb column top -248, +8 for the ring, +12 so it reads as
        //            clearance rather than as touching    -> Near.Y >= -228
        //   ceiling  the tallest actor is 384px above its own manifest ground
        //            line, and the bottom enemy plate's lower edge is at 108
        //
        // At -218/-125 the front ring sits at -226 and the highest head at 90,
        // so both ends have more room than before rather than less.
        //
        // KNOWN AND DELIBERATE: the detail column (top -186, x 308..648) still
        // covers a front enemy's feet while a submenu is open. Clearing it
        // needs Near.Y >= -166, which does not fit. The rule applied here is
        // the one the handover states: clear every ALWAYS-visible panel.
        // Recorded as AUDIT #45.
        public static readonly UiVec Near = new UiVec(300f, -218f);
        public static readonly UiVec Far = new UiVec(565f, -125f);

        // Applied on top of StageLayout.ScaleForDepth. The art is authored
        // larger than it is shown, so this is the one global shrink.
        //
        // 0.76, a hair down from 0.78, because the formation is wider now and
        // the outer slot has further to reach before it meets the enemy plates.
        public const float SpriteScale = 0.76f;

        // How far below a slot's own origin its nameplate hangs.
        public const float NameplateOffset = -34f;

        // The intent icon hangs ABOVE the slot, mirroring the nameplate below
        // it. Anchored to the slot's TOP for the same reason the nameplate is
        // anchored to its bottom: it must not depend on how tall a given
        // monster's sprite happens to be, and monsters differ by a lot.
        // Must exceed HALF the icon's height, or the icon hangs back down into
        // the slot: it is pinned to the slot's top edge by its own CENTRE. At 18
        // against a 46px icon it overlapped the sprite by exactly 5px, and
        // because the icon is a Button declared after the sprite it would have
        // taken the clicks along that strip. 30 - 23 leaves 7px of daylight.
        //
        // 69, UP FROM 46 (x1.5) -- the debuff/buff icon pass, balance-bot
        // 2026-09-02: every status badge on the HUD grew half again as large.
        // IntentIconOffset scaled with it below, so the daylight above the
        // slot's top edge (Offset - half the icon) grew too rather than
        // shrinking toward an overlap.
        public const float IntentIconSize = 69f;

        // 72, UP FROM 48 (x1.5, alongside IntentIconSize). At build time this
        // is measured from the slot's top edge; at RUNTIME PlaceIntentBadge
        // re-measures from the actor's own opaque top, which is a very
        // different distance -- the golem's idle frame carries 123px of empty
        // headroom where the rat's carries 17. 30 left the golem's badge
        // resting on its shoulder while the other two looked right, which is
        // the shape of every bug on this stage: correct for the actor it was
        // eyeballed against.
        public const float IntentIconOffset = 72f;

        // 1200 WIDE, up from 1000. The far anchor is at 565 and this frame is
        // measured from its centre, so a 1000-wide frame put the outermost slot
        // 65px outside the box it is declared in -- which
        // FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect exists to
        // refuse. Widening the frame is free: it draws nothing and takes no
        // clicks, it is a coordinate frame and not a surface.
        public static readonly UiVec StageSize = new UiVec(1200f, 600f);

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
