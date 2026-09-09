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
        //   ceiling  the golem was the tallest actor then, at 331px above its
        //            own manifest ground line, so at the near slot's scale it
        //            needed ~260 -- which is why the bottom enemy plate had to
        //            rise with it (PlateFirstY 332 -> 380). The "384" this
        //            paragraph carried was never measured off the art; see the
        //            re-measurement note under the live band below.
        //
        // X NOW RUNS OUTWARD WITH DEPTH, 300 -> 565 (further widened to 660
        // on 2026-09-09, see the note below the AUDIT #45 paragraph -- the
        // outward DIRECTION this paragraph argues for is what still holds;
        // only the far endpoint moved again since), where it used to run
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
        //   ceiling  the tallest actor is 483px above its own manifest ground
        //            line, and the bottom enemy plate's lower edge is at 215
        //
        // At -218/-125 the front ring sits at -226 and the highest head at 149,
        // so both ends have more room than before rather than less.
        //
        // THAT 149 IS STALE as of the 2026-09-09 depth-scale re-tune below
        // (StageLayout.NearScale/FarScale 1.0/0.74 -> 0.94/0.82): Y here did
        // not move, but the scale multiplying the tallest actor's 483px did,
        // and the old near/far scale happened to put both ends within 3px of
        // each other (149.1 near, 146.6 far) where the new one does not
        // (~127 near, ~176 far -- the far slot is now the taller-head case).
        // Not re-measured by hand again here: UiAudit re-solves every screen
        // at four canvas aspects and refuses a head over the panel ceiling,
        // so that gate is what actually guards this number now rather than a
        // comment restating it after every scale tune.
        //
        // RE-MEASURED 2026-09-08, and both halves of the old note were wrong.
        // The tallest actor is not the golem and never was 384: the roster
        // grew, and tools/measure_stage.py reads forest_warden at 483 above
        // its ground line while the golem measures 331. The plates moved too
        // -- the 2x1 kit container took PlateH from 64 to 110, which drops the
        // bottom row's lower edge from 284 to 215. The band still closes, by
        // 66 units rather than by the 18 the old numbers claimed, but nothing
        // here had been measured since either change; the tool that measures
        // it could not parse the source at all (its PlateX/PlateH/VerbRowW
        // patterns had all rotted), so re-running it was not the check anybody
        // thought it was.
        //
        // KNOWN AND DELIBERATE: the detail column (top -186, x 308..648) still
        // covers a front enemy's feet while a submenu is open. Clearing it
        // needs Near.Y >= -166, which does not fit. The rule applied here is
        // the one the handover states: clear every ALWAYS-visible panel.
        // Recorded as AUDIT #45.
        //
        // Far.X 565 -> 660, OWNER FEEDBACK 2026-09-09: "there is overlap
        // between characters" and specifically Odette (the far/back party
        // slot) "very bulky, overlaps or nudges under other chars." Measured
        // against the real three-party capture
        // (tools/screenshots/runtime/party_formation), the OLD 265px Near-Far
        // range gave three co-present, similarly-sized figures (opaque idle
        // width ~205px each once drawn) only a 132.5px gap between adjacent
        // slots -- less than one figure's own half-width, so the middle slot
        // sat on top of roughly a third of the far slot's body by
        // construction, not as an authoring mistake. Y is untouched (the
        // floor/ceiling band this file's own header measures does not move);
        // widening is X-only, and StageSize.X below grows with it so the
        // mirrored far anchor still lands inside its own coordinate frame
        // (FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect).
        // This does not chase zero overlap -- depth-stacked figures on one
        // receding floor are SUPPOSED to overlap a little (FightScreen's own
        // AllowOverlap on the stage panel says as much) -- it chases the gap
        // back down to something a body can stand in rather than mostly
        // behind.
        public static readonly UiVec Near = new UiVec(300f, -218f);
        public static readonly UiVec Far = new UiVec(660f, -125f);

        // C4: the mirrored (party) side stands this much further back on X
        // than the enemy side's plain mirror image -- party Near/Far X
        // become -360/-625 rather than -300/-565. X ONLY: the two sides
        // still share one ground line (Y untouched), so nothing about
        // vertical clearance (the plate top, the ring's Y, the mini-rows)
        // moves. Widens the gap between the two front actors, which sat
        // exactly as close together as two same-side neighbours despite
        // facing off across the whole stage.
        public const float PartyRetreat = 60f;

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

        // 1500 WIDE, up from 1260 -- Far.X's own move to 660 above. The party
        // far anchor is now at -(660 + 60) = -720, so the 1260-wide frame
        // that exactly fit the OLD +-565 range left the retreated party slot
        // 90px outside it -- the same
        // FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect this
        // frame's own history (1000 -> 1200 -> 1260) already exists to catch.
        // Widening the frame is still free: it draws nothing and takes no
        // clicks, it is a coordinate frame and not a surface -- see
        // FightScreen.BuildStage's own AllowOverlap on this exact panel.
        public static readonly UiVec StageSize = new UiVec(1500f, 600f);

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
            return new UiVec(mirrored ? -(x + PartyRetreat) : x, y);
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
