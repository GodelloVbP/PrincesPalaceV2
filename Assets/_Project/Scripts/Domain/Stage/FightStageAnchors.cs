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
        // THESE TWO ARE THE ENEMY SIDE'S ENDPOINTS. They were both sides'
        // until 2026-09-09 -- see StageFormation's header for why the party
        // now carries its own, and PartyFarY below for the one number that
        // differs. Kept as bare UiVec literals under these exact names
        // because tools/measure_stage.py greps for them, and a measuring
        // tool that can no longer parse the source is a gate that silently
        // stops firing (this file's own header records that happening).
        public static readonly UiVec Near = new UiVec(300f, -218f);
        public static readonly UiVec Far = new UiVec(660f, -125f);

        // THE PARTY'S OWN X ENDPOINTS, 320 AND 810 -- AUTHORED, NOT DERIVED.
        //
        // PartyRetreat (the "+60 on both endpoints" scalar these two replace)
        // is gone. It had already stopped being one number by 6fedab18, which
        // gave the party its own Y; it stopped being one on X here, and a
        // constant that has to be read as "true of the near endpoint only"
        // is worse than two honest literals.
        //
        // OWNER, 2026-09-09, after 6fedab18: the three party figures "still
        // read as a clump". MEASURED off that commit's own capture
        // (scratchpad formation_after/full_narrowest_fourthree_1920x1440.png,
        // 4:3, 1px = 1 canvas unit), compositing the real idle alpha masks at
        // the real marks and slot scales:
        //
        //   Shawn  opaque -505.0 .. -307.8   (197px wide at slot 0)
        //   Bjorn  opaque -645.0 .. -436.3   (209px at slot 1)
        //   Odette opaque -805.7 .. -577.6   (228px at slot 2)
        //
        // Adjacent bodies overlapped 68.7px and 67.4px -- 35% and 32% of the
        // narrower figure, at torso and leg height, which is why Shawn's
        // cloak sat on Bjorn's leg. The previous pass judged this by an
        // "unoccluded >= 80%" per-pixel criterion that passed at 92%: a
        // measure that counts PIXELS cannot see that the third of a body it
        // is losing is the readable third. The slot pitch was 180 against
        // figures 197-228 wide, so the clump was arithmetic, not an
        // authoring slip.
        //
        // THE SPREAD NEEDED, derived from the art rather than eyeballed. For
        // adjacent bodies to just touch, consecutive marks must be at least
        // (nearer figure's left extent + farther figure's right extent)
        // apart at their own scales: 145.0 + 103.7 = 248.7 between slots 0
        // and 1, and 105.0 + 142.4 = 247.4 between 1 and 2. That is 496 of
        // total range where the old 360 (-360..-720) gave 180 per step.
        //
        // WHERE THE 496 COMES FROM, both ends:
        //
        //   FAR END, capped at 810 by the LEFT EDGE OF THE CANVAS. 4:3 is the
        //   narrowest aspect UiAudit solves and the canvas is 1920 wide there
        //   (Expand scaling: 1920x1440), so x stops at -960. The widest-left
        //   drawing any actor can bring to the back slot is Shawn's idle,
        //   whose opaque box reaches 203px left of his own canvas centre --
        //   126.5 stage pixels at the far slot's 0.6232 scale. 810 + 126.5 =
        //   936.5, which keeps EVERY seating inside the 20px margin. Sized
        //   for the seating, not for today's party order, because Move
        //   reorders the field formation in place (CombatEncounter's
        //   SwapPartySlots) -- so any of the six is reachable mid-fight.
        //
        //   NEAR END, 360 -> 320, which is the 40px the far end could not
        //   pay for. It is spent out of the floor between the front party
        //   figure and the front enemy: Shawn's opaque right edge moves
        //   -307.8 -> -267.8, and against the widest enemy in the near enemy
        //   slot (the beetle, whose mirrored idle puts its near edge at
        //   57.8) the clear floor goes 365.7 -> 325.7. Worth stating plainly
        //   because the brief that asked for this believed there was ~600
        //   there: there is not, and never was -- 660 is the distance
        //   between the two MARKS, and both figures reach a long way in from
        //   their marks.
        //
        // RESULT, same offline composite: adjacent overlaps 3.7px (1.9%) and
        // 2.4px (1.2%), and both contact rings fully clear of the figure in
        // front (24.7 and 34.8 pixels of daylight, where Bjorn's ring used
        // to lose 35px of its right end behind Shawn -- visible in the
        // capture as a green arc cut off square).
        private const float PartyNearX = 320f;
        private const float PartyFarX = 810f;

        // THE PARTY'S FAR GROUND LINE, -64 WHERE THE ENEMY'S IS -125.
        //
        // OWNER, 2026-09-09, after the Far.X widen in cb8fee7c: "the
        // formation is still off", the middle figure half hidden behind the
        // front one "and its foot ring is hidden entirely".
        //
        // The ring was not hidden by another figure. It was hidden by the
        // HUD. Measured off the capture that fix produced
        // (scratchpad formation_now/full_narrowest_fourthree_1920x1440.png):
        // the two roster plates occupy x -920..-540 (FightScreen's
        // PartyPlateWidth 380 centred on -730; the note here said -468 until
        // 2026-09-09, which was the older 452-wide column) and their block's
        // top edge is at y -161, and BuildRosterPlates is declared after both
        // stages, so they paint over anything standing there. With one
        // shared ground line the middle party slot landed at y -171.5 --
        // ten pixels INSIDE that block, ring drop and all -- while the near
        // slot escaped by being to the RIGHT of the plates (x -360) and the
        // far slot escaped by being ABOVE them (y -125). Only the middle of
        // the line passed through the corner of the HUD, which is why one
        // figure of three looked wrong and the other two looked fine.
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
        // WHAT IT COSTS: the two sides no longer share one ground line past
        // the front rank. The FRONT ranks still do (-218 both), which is the
        // rank the two armies actually face each other across; the back
        // ranks now differ by 61px, which reads as the party's line
        // receding further than the enemy's rather than as two floors --
        // they are 1000px apart on screen and never adjacent. Judged
        // cheaper than the alternative, which is moving the HUD, and the
        // HUD is not this file's to move.
        //
        // Headroom checked rather than assumed: the party's ceiling is the
        // initiative tracker, pinned to the canvas TOP-LEFT at -(130+16)
        // with a 74px row, so its underside sits 220 below the top edge --
        // y 320 at the 1080-tall aspects. Odette is the tallest thing this
        // slot can hold once her 70px hover is added, at y 222. 98px clear.
        //
        // ASKED AGAIN 2026-09-09, once the X spread above widened: can the
        // party's back rank come back down to the enemy's -125, so the two
        // sides share one floor again? No, and the reason is not X. The
        // binding number is the MIDDLE slot, whose ring has to stay above
        // the roster block's -161 top edge and cannot get out of the block's
        // x range at any spacing the bodies allow (see the paragraph above);
        // -141 is the lowest it can sit, and a straight line through
        // (-218, -141) arrives at exactly -64. The only way to have both is
        // a per-formation Y CURVE -- authored bias on StageFormation, enemy
        // linear and unchanged -- which would put the party's ladder at
        // -218 / -141 / -125: a 77px step then a 16px one, two ranks 245px
        // apart in X sitting on almost the same line. That reads as a kink
        // in the floor rather than as depth, which is a worse failure than
        // the one it fixes; the even ladder is kept and the shared back
        // floor is not. Revisit if the HUD column ever moves.
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

        // 1700 WIDE, up from 1500 -- PartyFarX's own move to 810 above. The
        // party far anchor is the outermost thing this frame has to contain
        // on either side, and the 1500-wide frame that fit -720 leaves -810
        // 60px outside it -- the same
        // FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect this
        // frame's own history (1000 -> 1200 -> 1260 -> 1500) already exists
        // to catch. 850 of half-width against 810 keeps the 40px of headroom
        // the last three widenings each had to go and find.
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
