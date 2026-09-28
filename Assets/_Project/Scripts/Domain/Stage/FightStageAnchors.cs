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
        // Y is bounded by a measured floor/ceiling band
        // (tools/measure_stage.py), not carried over from v1's numbers,
        // which put front-row figures shin-deep in the HUD and do not close
        // on v2's geometry at all:
        //
        //   floor    verb column top -248, +8 for the ring, +12 so it reads as
        //            clearance rather than as touching     -> Near.Y >= -228
        //   ceiling  the tallest actor (forest_warden, 483px above its own
        //            manifest ground line) must clear the bottom enemy
        //            plate's lower edge, at 215
        //
        // X runs outward with depth -- armies separate laterally rather than
        // receding toward a shared vanishing point at screen centre -- because
        // occlusion, not spacing, is the real constraint: with the line
        // running inward the middle enemy is only 45% visible, swallowed by
        // the golem beside it (tools/measure_stage.py). Running the line
        // outward instead takes the worst case to 67%. A tight two-rank
        // stagger measures worse still, at 28%, because ranking figures up
        // stacks them almost exactly on top of each other.
        //
        // At -218/-125 the front ring sits at -226 and the highest head sits
        // at ~127 near / ~176 far (StageLayout.NearScale/FarScale 0.94/0.82),
        // so both ends clear with room to spare. UiAudit re-solves every
        // screen at four canvas aspects and refuses a head over the panel
        // ceiling, so that gate is what guards this number rather than a
        // comment restating it after every scale tune.
        //
        // Known and deliberate: the detail column (top -186, x 308..648)
        // still covers a front enemy's feet while a submenu is open.
        // Clearing it needs Near.Y >= -166, which does not fit; the rule
        // applied here is to clear every always-visible panel instead.
        // Recorded as AUDIT #45.
        //
        // Far.X is 660, wider than the depth band alone calls for, because
        // three co-present, similarly-sized party figures (opaque idle width
        // ~205px each) need more than a half-width's gap between adjacent
        // slots or the middle one sits on top of the far one's body by
        // construction, not as an authoring mistake. Y is untouched -- the
        // floor/ceiling band above does not move; widening is X-only, and
        // StageSize.X below grows with it so the mirrored far anchor still
        // lands inside its own coordinate frame
        // (FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect). This
        // does not chase zero overlap -- depth-stacked figures on one
        // receding floor are supposed to overlap a little (FightScreen's own
        // AllowOverlap on the stage panel says as much) -- it chases the gap
        // back down to something a body can stand in rather than mostly
        // behind.
        //
        // These two are the enemy side's endpoints; see StageFormation's
        // header for why the party carries its own, and PartyFarY below for
        // the one number that differs. Kept as bare UiVec literals under
        // these exact names because tools/measure_stage.py greps for them,
        // and a measuring tool that can no longer parse the source is a gate
        // that silently stops firing.
        public static readonly UiVec Near = new UiVec(300f, -218f);
        public static readonly UiVec Far = new UiVec(660f, -125f);

        // The party's own X endpoints, 320 and 810 -- authored, not derived
        // from a single scalar applied to both ends, because a constant that
        // has to be read as "true of the near endpoint only" is worse than
        // two honest literals.
        //
        // Measured by compositing the real idle alpha masks at the real
        // marks and slot scales:
        //
        //   Shawn  opaque -505.0 .. -307.8   (197px wide at slot 0)
        //   Bjorn  opaque -645.0 .. -436.3   (209px at slot 1)
        //   Odette opaque -805.7 .. -577.6   (228px at slot 2)
        //
        // A per-pixel "unoccluded >= 80%" criterion passes at 92% here
        // despite adjacent bodies overlapping 68.7px and 67.4px -- 35% and
        // 32% of the narrower figure, at torso and leg height -- because it
        // cannot see that the third of a body it is losing is the readable
        // third.
        //
        // The spread needed is derived from the art rather than eyeballed:
        // for adjacent bodies to just touch, consecutive marks must be at
        // least (nearer figure's left extent + farther figure's right
        // extent) apart at their own scales -- 145.0 + 103.7 = 248.7 between
        // slots 0 and 1, and 105.0 + 142.4 = 247.4 between 1 and 2, 496 of
        // total range.
        //
        // Where the 496 comes from, both ends:
        //
        //   Far end, capped at 810 by the left edge of the canvas. 4:3 is the
        //   narrowest aspect UiAudit solves and the canvas is 1920 wide there
        //   (Expand scaling: 1920x1440), so x stops at -960. The widest-left
        //   drawing any actor can bring to the back slot is Shawn's idle,
        //   whose opaque box reaches 203px left of his own canvas centre --
        //   126.5 stage pixels at the far slot's 0.6232 scale. 810 + 126.5 =
        //   936.5, which keeps every seating inside the 20px margin. Sized
        //   for the seating, not for today's party order, because Move
        //   reorders the field formation in place (CombatEncounter's
        //   SwapPartySlots) -- so any of the six is reachable mid-fight.
        //
        //   Near end, 320, spending the 40px the far end could not pay for
        //   out of the floor between the front party figure and the front
        //   enemy: Shawn's opaque right edge sits at -267.8, and against the
        //   widest enemy in the near enemy slot (the beetle, whose mirrored
        //   idle puts its near edge at 57.8) the clear floor is 325.7. 660 is
        //   the distance between the two marks, not the clear floor between
        //   the figures -- both figures reach a long way in from their marks.
        //
        // Result, same offline composite: adjacent overlaps 3.7px (1.9%) and
        // 2.4px (1.2%), and both contact rings fully clear of the figure in
        // front (24.7 and 34.8 pixels of daylight).
        private const float PartyNearX = 320f;
        private const float PartyFarX = 810f;

        // The party's far ground line is -64 where the enemy's is -125,
        // because the middle party slot's contact ring is hidden by the HUD,
        // not by another figure. Measured off a real capture: the two roster
        // plates occupy x -920..-540 (FightScreen's PartyPlateWidth 380
        // centred on -730) and their block's top edge is at y -161, and
        // BuildRosterPlates is declared after both stages, so they paint over
        // anything standing there. With one shared ground line the middle
        // party slot lands at y -171.5 -- ten pixels inside that block, ring
        // drop and all -- while the near slot escapes by being to the right
        // of the plates (x -360) and the far slot escapes by being above them
        // (y -125). Only the middle of the line passes through the corner of
        // the HUD, which is why one figure of three looks wrong and the
        // other two look fine.
        //
        // Nothing about X fixes that: for the middle slot's foot band to
        // clear the plates' right edge it would have to stand at x >= -487
        // (its ring reaches 53 left of its mark), and the near slot is at
        // -320. The line has to be steeper, so the party's far end rises to
        // -64 and the midpoint with it: -141, ring at -149, twelve pixels of
        // daylight over the plate top. That daylight is pinned as a literal in
        // FightStageAnchorsTests, because the audit that ought to have
        // caught this cannot: FightScreenTests' foot-band-versus-HUD scan
        // skips subtrees declared Inactive, and the roster plates are built
        // Inactive and switched on at runtime.
        //
        // What it costs: the two sides no longer share one ground line past
        // the front rank. The front ranks still do (-218 both), which is the
        // rank the two armies actually face each other across; the back
        // ranks differ by 61px, which reads as the party's line receding
        // further than the enemy's rather than as two floors -- they are
        // 1000px apart on screen and never adjacent. Judged cheaper than the
        // alternative, which is moving the HUD, and the HUD is not this
        // file's to move.
        //
        // Headroom checked rather than assumed: the party's ceiling is the
        // initiative tracker, pinned to the canvas top-left at -(130+16)
        // with a 74px row, so its underside sits 220 below the top edge --
        // y 320 at the 1080-tall aspects. Odette is the tallest thing this
        // slot can hold once her 70px hover is added, at y 222. 98px clear.
        //
        // The party's back rank cannot come back down to the enemy's -125 to
        // share one floor again: the binding number is the middle slot,
        // whose ring has to stay above the roster block's -161 top edge and
        // cannot get out of the block's x range at any spacing the bodies
        // allow (see above); -141 is the lowest it can sit, and a straight
        // line through (-218, -141) arrives at exactly -64. The only way to
        // have both is a per-formation Y curve -- authored bias on
        // StageFormation, enemy linear and unchanged -- which would put the
        // party's ladder at -218 / -141 / -125: a 77px step then a 16px one,
        // two ranks 245px apart in X sitting on almost the same line. That
        // reads as a kink in the floor rather than as depth, which is a
        // worse failure than the one it fixes; the even ladder is kept and
        // the shared back floor is not. Revisit if the HUD column ever
        // moves.
        private const float PartyFarY = -64f;

        // The two sides' formations. Authored as magnitudes out from stage
        // centre (see StageFormation); SlotOffset applies the mirror.
        //
        // The enemy's endpoints ARE Near/Far; the party's are its own three
        // constants above, except for the near GROUND LINE, which is shared
        // deliberately -- the two front ranks stand on the one floor they
        // face each other across, and FightScreenTests asserts it from the
        // solved screen.
        public static readonly StageFormation Enemy = new StageFormation(Near, Far);

        public static readonly StageFormation Party = new StageFormation(
            new UiVec(PartyNearX, Near.Y),
            new UiVec(PartyFarX, PartyFarY));

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
        // Every status badge on the HUD is sized to match the debuff/buff
        // icons; IntentIconOffset below scales with it, so the daylight
        // above the slot's top edge (Offset - half the icon) stays clear
        // rather than shrinking toward an overlap.
        public const float IntentIconSize = 69f;

        // At build time this is measured from the slot's top edge; at
        // runtime PlaceIntentBadge re-measures from the actor's own opaque
        // top, which is a very different distance -- the golem's idle frame
        // carries 123px of empty headroom where the rat's carries 17. A
        // build-time-only offset would rest the golem's badge on its
        // shoulder while looking right for smaller actors, which is the
        // shape of every bug on this stage: correct for the actor it was
        // eyeballed against.
        public const float IntentIconOffset = 72f;

        // The party far anchor (PartyFarX, 810) is the outermost thing this
        // frame has to contain on either side, and StageSize.X's half-width
        // must clear it with margin -- checked by
        // FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect. 850
        // of half-width against 810 keeps 40px of headroom.
        // Widening the frame is still free: it draws nothing and takes no
        // clicks, it is a coordinate frame and not a surface -- see
        // FightScreen.BuildStage's own AllowOverlap on this exact panel.
        public static readonly UiVec StageSize = new UiVec(1700f, 600f);

        public const float InitiativeIconSize = 74f;
        public const float InitiativeIconGap = 8f;
        public const float InitiativeRingPadding = 10f;

        // The 8-bit encodings of v1's float colours, accurate to 1/255 -- close
        // enough for a translucent ground shadow, and the DSL speaks hex.
        public const string AllyShadowColor = "#59D966D9";
        public const string EnemyShadowColor = "#E63340D9";

        // A slot's offset from stage centre. `mirrored` picks the PARTY
        // formation and flips its X: the party stands on the left and faces
        // the other way. Still the one place a slot position is computed --
        // StageVisuals.DrawSide/AnchorOne and FightScreen.BuildStage both
        // read it rather than re-deriving.
        public static UiVec SlotOffset(int slotIndex, int slotCount, bool mirrored)
        {
            var formation = mirrored ? Party : Enemy;
            var offset = formation.OffsetForSlot(slotIndex, slotCount);
            return new UiVec(mirrored ? -offset.X : offset.X, offset.Y);
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
