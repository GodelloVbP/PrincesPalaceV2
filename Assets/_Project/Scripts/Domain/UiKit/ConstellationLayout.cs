namespace PrincesPalace.Domain.UiKit
{
    // Where a talent sits when its tree is drawn as a constellation, and how
    // paging between trees moves.
    //
    // Pure arithmetic, in Domain for the same reason MapLayout and
    // FightSubmenuLayout are: the SCREEN TREE places the star pool at build time
    // and the CONTROLLER re-anchors it per tree at runtime, and those two have to
    // be the same function or the sky drifts from what it claims.
    public static class ConstellationLayout
    {
        // ---- the handoff's canvas, converted once --------------------------------
        //
        // THE DESIGN IS AUTHORED AT 1600x900 AND THIS GAME RENDERS AT 1920x1080,
        // which is exactly x1.2 with no distortion -- both are 16:9. The handoff
        // says it plainly: "1600x900 is the authoring canvas, not a stage inside
        // a larger one. Multiply everything by 1.2." Every number below is that
        // multiplication already done, ROUNDED ONCE AT THE END rather than
        // per-term, which is also the handoff's instruction and is why the spine
        // ladder lands on whole pixels while the plots do not always.
        //
        // The design also measures y DOWNWARD from the canvas top-left, and this
        // project measures it up from the centre. Both conversions happen here
        // and nowhere else, so every coordinate below can be checked against the
        // document it came from.
        public const float Scale = 1.2f;

        // The sky the stars are placed in: the full canvas height, and the
        // canvas width less the panel that sits over its right-hand end.
        //
        // DERIVED FROM PanelWidth NOW, not authored. Both of these used to be
        // frozen at the handoff's 456-wide panel (1920 - 456 = 1464, and
        // -456/2 = -228) and stayed there when the panel first narrowed to
        // 440.96, which was harmless while the panel only got NARROWER. The
        // 2026-09-07 kit repin makes it wider (607.5, see PanelWidth), and a
        // frozen sky would have put the forward paging arrow at x 437 under a
        // panel starting at 352.5 -- the exact "a live button drawn beneath
        // an opaque column" defect ConstellationLayoutTests.NeitherPagingArrow
        // SitsUnderThePanel exists to catch. Written as the relationship they
        // always meant instead.
        public static float SkyWidth => UiFrames.Reference.X - PanelWidth;  // 1312.5, was 1464
        public const float SkyHeight = 1080f;   // 900 authored

        // The detail panel, over the sky rather than beside it.
        //
        // WIDTH IS DERIVED, NOT AUTHORED, and this column is HEIGHT-BOUND:
        // PanelHeight is the full canvas height and every row inside it
        // (PanelHeaderY down to the respec button) is authored against that,
        // so the width is what follows from the kit's aspect. The handoff's
        // 380 at x1.2 (456) against a 1080 height is 0.4222, which no
        // delivery of the 9:16 art has ever matched.
        //
        // 607.5 NOW, WAS 440.96. The 2026-09-07 regeneration put the kit at
        // its true nominal aspect (0.5625, where the spliced delivery
        // measured 0.4083), so the same full-height column is 166px wider and
        // the sky it sits over is 166px narrower. Narrowing the panel's
        // HEIGHT to keep the old width was the alternative and it is much the
        // worse one: a 456-wide 9:16 panel is 810.7 tall, and every row in it
        // is placed against a 1080-tall column.
        public const float PanelWidth = PanelHeight * ContainerArt.ContainerAspect9x16; // 607.5, was 440.96
        public static float PanelCentreX => UiFrames.Reference.X * 0.5f - PanelWidth * 0.5f;

        // Where slot 0 sits, in this project's own frame: the handoff's
        // (610, 856) on a 1600x900 canvas, scaled and flipped.
        //
        // Horizontally that is the SKY STAGE's centre rather than the canvas's,
        // which is the whole reason the tree is not centred on screen -- the
        // panel takes the right-hand column and the constellation keeps the
        // middle of what is left. Derived from PanelWidth for the same reason
        // SkyWidth is, above.
        public static float TreeOriginX => -PanelWidth * 0.5f;   // -303.75, was -228
        // 540 - 1027 authored, then raised 11px. The figure is 1071 tall once
        // the root's NAME and the capstone's gate reading are counted -- 8px of
        // slack in a 1080 canvas -- and the authored origin spent all of it at
        // the top, clipping the root's name off the bottom edge. Centred here
        // instead, and held there by
        // ConstellationLayoutTests.TheWholeFigureFitsTheCanvas.
        public const float TreeOriginY = -476f;

        // The stage arrows, at the sky's own edges rather than the canvas's --
        // and MEASURED FROM THE TREE'S CENTRE, not the canvas's.
        //
        // Symmetric about the screen they landed the right-hand one at x 665,
        // underneath a panel that spans 504 to 960: an arrow drawn beneath an
        // opaque column, taking clicks meant for whatever is under it. The sky
        // is offset left by half the panel width for exactly this reason and
        // the arrows have to follow it, which makes them an asymmetric pair
        // about the canvas and a symmetric one about the thing they page.
        public const float ArrowWidth = 53f;      // 44 authored
        public const float ArrowHeight = 115f;    // 96 authored
        private const float ArrowInset = 67f;

        public static float ArrowRightX => TreeOriginX + SkyWidth * 0.5f - ArrowInset;

        public static float ArrowLeftX => TreeOriginX - SkyWidth * 0.5f + ArrowInset;

        // ---- the detail panel ----------------------------------------------------
        //
        // A RIGHT-HAND COLUMN, NOT A BOTTOM PLATE, and the handoff gives the
        // reason: the figure is tall. A plate along the bottom would either
        // crop the capstone or shrink the stones below the size the art needs.
        //
        // It sits OVER the sky rather than beside it -- the sky is the full
        // canvas and the panel covers its right-hand end -- which is why the
        // tree's origin is the sky STAGE's centre and not the screen's.
        public const float PanelHeight = 1080f;

        // Matches the Violet 9:16 container's own measured content inset
        // (ContainerArt.Inset, left/right 0.083 of the frame's width -- was
        // 0.08 before the 2026-09-07 repin) rather than the handoff's
        // 28-authored pad, so a label can never sit under the painted border
        // -- see Ui.ContainerContent, which is what actually places content
        // at this margin now.
        public const float PanelPad = PanelWidth * 0.083f;

        public static float PanelInnerWidth => PanelWidth - PanelPad * 2f;

        // The rows, from the panel's own centre. Authored sizes x1.2 come from
        // the handoff's type table; these are where the boxes sit.
        //
        // HEADER 464, PATH 422, RESPEC -459 -- each one nudged in again at the
        // 2026-09-07 kit repin, for the same reason they were nudged in from
        // 486/430/-480 before it: the 9:16 container's own top/bottom inset
        // is what bounds this column, and it grew from 0.04 to 0.047/0.048 of
        // 1080 (43.2px a side to 50.8/51.8) when the border was measured
        // properly. At 472 the header's 44-tall box topped out at 494, past
        // the 489.2 the new inset allows; at -468 the respec button's bottom
        // reached -494 against a -488.2 bound. The path row follows the
        // header down by the same 8px so the 3px gap between them survives --
        // and everything from the kicker down is a running stack off
        // PanelPathY, so it follows for free. Nothing below the refusal line
        // moves except respec itself.
        public const float PanelHeaderY = 464f;   // was 472, 9x16 container's top inset

        // RE-DERIVED AS A TOP-DOWN STACK (balance-bot item 3, 2026-09-03).
        // The flat authored numbers below had two real defects the 456->440.96
        // width narrowing (05a8bcc) never touched, because neither is a width
        // problem: (1) PanelKickerY (244) sat BELOW PanelNameY (322) even
        // though the kicker's own comment says it reads "above the name" --
        // rows were declared in reading order but not in on-screen order, so
        // the state word printed under the talent's name instead of over it;
        // (2) PanelBodyHeight (240) was a flat guess that left ~120px of the
        // rail's own height between the price row and the refusal line
        // unclaimed by anything -- "does not use the container space".
        //
        // Same header/path rows as before (unaffected); everything from the
        // kicker down is now one running stack, each row's centre computed
        // from the edge the row above it left, with a named gap between them
        // -- so a future row insertion (or a font-size change moving a box's
        // own height) cannot silently reopen either defect the way two
        // independently-authored constants could.
        public const float PanelPathY = 422f;   // was 430, follows PanelHeaderY down
        private const float PanelHeaderHeight = 44f;
        private const float PanelPathHeight = 34f;
        private const float PanelKickerHeight = 22f;
        private const float PanelNameHeight = 96f;
        private const float PanelPriceHeight = 26f;
        private const float PanelRefusalHeight = 60f;

        private const float GapPathToKicker = 16f;
        private const float GapKickerToName = 10f;
        private const float GapNameToPrice = 12f;
        private const float GapPriceToBody = 16f;
        private const float GapBodyToRefusal = 16f;

        private static float PathBottomEdge => PanelPathY - PanelPathHeight * 0.5f;

        // KICKER NOW ABOVE THE NAME, matching its own "the state, above the
        // name" comment instead of contradicting it.
        public static float PanelKickerY =>
            PathBottomEdge - GapPathToKicker - PanelKickerHeight * 0.5f;
        private static float KickerBottomEdge => PanelKickerY - PanelKickerHeight * 0.5f;

        public static float PanelNameY =>
            KickerBottomEdge - GapKickerToName - PanelNameHeight * 0.5f;
        private static float NameBottomEdge => PanelNameY - PanelNameHeight * 0.5f;

        public static float PanelPriceY =>
            NameBottomEdge - GapNameToPrice - PanelPriceHeight * 0.5f;
        private static float PriceBottomEdge => PanelPriceY - PanelPriceHeight * 0.5f;

        // THE BODY CLAIMS EVERYTHING DOWN TO THE REFUSAL LINE, rather than a
        // flat authored guess -- PanelRefusalY stays where it was (the bottom
        // cluster - refusal/invest/meter/respec - was never part of this
        // report and is left exactly as built), so the body's own height is
        // whatever's actually left between the price row and it.
        private static float BodyTopEdge => PriceBottomEdge - GapPriceToBody;
        private static float BodyBottomEdge => PanelRefusalY + PanelRefusalHeight * 0.5f + GapBodyToRefusal;

        public static float PanelBodyHeight => BodyTopEdge - BodyBottomEdge;
        public static float PanelBodyY => (BodyTopEdge + BodyBottomEdge) * 0.5f;

        public const float PanelRefusalY = -132f;
        public const float PanelActionY = -320f;
        public const float PanelMeterY = -420f;
        public const float PanelRespecY = -459f;  // was -468, 9x16 container's bottom inset

        public const float PanelActionWidth = 320f;
        public const float PanelActionHeight = 72f;

        // ---- gate collars --------------------------------------------------------
        //
        // Drawn in EVERY state, including unreachable, because the collar is how
        // a player reads how far a path is from its gate long before the shape
        // lets them buy anything. A ring that only appeared once it was
        // satisfied would answer a question nobody could still be asking.
        public const float CollarInset = 17f;      // 14 authored

        public static float CollarSize(string kind) => OrbSize(kind) + CollarInset * 2f;

        // ---- labels on the sky ---------------------------------------------------
        //
        // A stone's name sits under it and its price under that. Prices appear
        // only where a number changes a decision -- the two gates, and any stone
        // reachable, too expensive, hovered or selected. Labelling all 21 turned
        // the sky back into a spreadsheet.
        public const float LabelWidth = 240f;
        public const float LabelHeight = 22f;
        public const int LabelFont = 16;           // 13 authored
        public const int PriceFont = 13;           // 11 authored

        public static float LabelY(string kind) => -(OrbSize(kind) * 0.5f + 20f);

        public static float PriceY(string kind) => LabelY(kind) - LabelHeight;

        // ---- the orbs -----------------------------------------------------------
        //
        // SIZING MARKS ROLE, NOT POINT COST, and never depth and never state.
        // The convergence and the capstone are the two slots whose SHAPE is
        // their meaning; a build where every orb was one size left the capstone
        // at the top of an eight-tier climb looking exactly like the first
        // chain node above the root.
        //
        // 58 / 74 / 91 is the handoff's 48 / 62 / 76 at x1.2, and its own §12
        // states those three as the intent rather than leaving them derived.
        public const float OrbNormal = 58f;
        public const float OrbMerge = 74f;
        public const float OrbCap = 91f;

        // Kept as the old name so nothing that only wants "about an orb wide"
        // has to care which kind it is asking about.
        public const float StarSize = OrbNormal;

        // ---- what a lit stone wears ---------------------------------------------
        //
        // MEASURED OFF THE DESIGN'S OWN RECORDING rather than guessed. Its sky
        // sits at luma 6.5 and its ember's hot centre reaches 252 -- so the
        // halo is allowed to be tight, because it has a black room to be bright
        // in. Drawn at the sizes this used to carry (2.4x glow, 1.7x aura, 0.52x
        // core) over a nebula at luma 34, the same stone read as a smear.
        //
        // The core is the one that mattered. The delivered art already IS the
        // molten sphere; a pale disc at 52% of the stone laid over the veins
        // erased the thing the art was for.
        public const float GlowScale = 1.8f;
        public const float AuraScale = 1.3f;
        public const float CoreScale = 0.34f;

        public static float OrbSize(string kind)
        {
            switch (kind)
            {
                case "cap": return OrbCap;
                case "merge": return OrbMerge;
                default: return OrbNormal;
            }
        }

        // ---- the plots: each path draws its own beast ----------------------------
        //
        // THE TREE STOPPED BEING A LATTICE. It was dx x depth -- a signed column
        // times a fixed pitch -- which is a grid however it is dressed, and a
        // grid reads as a spreadsheet. Every slot is now hand-plotted per path,
        // so The Black Ram is a ram's head and The Fragile Lamb a lamb's: the
        // face climbs the spine, and the branch above the waist sweeps wide and
        // curls back in as horns, or falls wide and flat as ears.
        //
        // THE DOMAIN IS UNTOUCHED. Same 21 slots, same nine tiers, same parents,
        // same prices, same two gates -- only the coordinates changed. A fourth
        // path is a fourth table here and nothing else.
        //
        // Authored numbers x1.2, rounded once. Two properties survive the
        // scaling and are what the tests check rather than the individual pairs:
        //
        //   THE SPINE LADDER IS SHARED. All three tables put their centre column
        //   on 0, -130, -242, -355, -468, -582, -696, -810, -924. That is what
        //   guarantees the separation floor -- the figures differ only in how
        //   far their side nodes swing out, so a new plot only has to respect
        //   the horizontal clearances.
        //
        //   y IS THE HANDOFF'S OWN SIGN, negative climbing, because the design
        //   measures down from the canvas top. StarY flips it once. Storing the
        //   document's numbers verbatim is what lets a reader check this table
        //   against the table it came from without doing arithmetic in their
        //   head.
        private static readonly float[,] BlackRam =
        {
            { 0f, 0f },            // root
            { -149f, -118f },      // tier 1
            { 0f, -130f },
            { 149f, -118f },
            { -211f, -230f },      // tier 2
            { 0f, -242f },
            { 211f, -230f },
            { -247f, -348f },      // tier 3
            { 0f, -355f },
            { 247f, -348f },
            { 0f, -468f },         // the convergence
            { -302f, -528f },      // tier 4 -- the horns begin their sweep
            { 0f, -582f },
            { 302f, -528f },
            { -422f, -641f },      // tier 5 -- widest, +/-422
            { 0f, -696f },
            { 422f, -641f },
            { -382f, -778f },      // tier 6 -- curling back in
            { 0f, -810f },
            { 382f, -778f },
            { 0f, -924f },         // the capstone, the crown
        };

        private static readonly float[,] FragileLamb =
        {
            { 0f, 0f },            // root
            { -134f, -120f },      // tier 1 -- a narrower face
            { 0f, -130f },
            { 134f, -120f },
            { -180f, -233f },      // tier 2
            { 0f, -242f },
            { 180f, -233f },
            { -202f, -350f },      // tier 3
            { 0f, -355f },
            { 202f, -350f },
            { 0f, -468f },         // the convergence
            { -286f, -518f },      // tier 4 -- ears, falling wide and flat
            { 0f, -582f },
            { 286f, -518f },
            { -408f, -569f },      // tier 5 -- widest and shallowest
            { 0f, -696f },
            { 408f, -569f },
            { -331f, -703f },      // tier 6
            { 0f, -810f },
            { 331f, -703f },
            { 0f, -924f },         // the capstone
        };

        private static readonly float[,] Unwritten =
        {
            { 0f, 0f },            // root
            { -132f, -125f },      // tier 1
            { 0f, -130f },
            { 132f, -125f },
            { -142f, -238f },      // tier 2
            { 0f, -242f },
            { 142f, -238f },
            { -151f, -353f },      // tier 3
            { 0f, -355f },
            { 151f, -353f },
            { 0f, -468f },         // the convergence
            { -175f, -526f },      // tier 4
            { 0f, -582f },
            { 175f, -526f },
            { -204f, -641f },      // tier 5 -- +/-204 at its widest
            { 0f, -696f },
            { 204f, -641f },
            { -180f, -778f },      // tier 6
            { 0f, -810f },
            { 180f, -778f },
            { 0f, -924f },         // the capstone
        };

        // A spire rather than a third animal, and that is the design saying
        // something with a shape: an unauthored path should read as a thin
        // unfinished thing, not as a beast nobody has written yet.
        private static readonly float[][,] Plots = { BlackRam, FragileLamb, Unwritten };

        public static int PlotCount => Plots.Length;

        // A path with no plot of its own falls back to the spire rather than
        // throwing or stacking every stone on the origin. Graceful degradation
        // is the house style, and an unplotted path is exactly the case the
        // spire was drawn for.
        private static float[,] PlotFor(int path) =>
            path >= 0 && path < Plots.Length ? Plots[path] : Unwritten;

        public static float StarX(int path, int slot)
        {
            var plot = PlotFor(path);
            return slot >= 0 && slot < plot.GetLength(0) ? plot[slot, 0] : 0f;
        }

        // THE ONE PLACE THE HANDOFF'S y IS FLIPPED. Its numbers climb negative
        // because the design measures down from the canvas top; depth runs UP
        // the screen here, because a constellation that grew downward would read
        // as falling.
        public static float StarY(int path, int slot)
        {
            var plot = PlotFor(path);
            return slot >= 0 && slot < plot.GetLength(0) ? -plot[slot, 1] : 0f;
        }

        // ---- what the plots guarantee -------------------------------------------
        //
        // The floors the design set, at x1.2. Not used to PLACE anything -- the
        // tables above are placement -- but asserted against every pair, so a
        // hand-plotted figure cannot quietly put two stones on top of each
        // other. Hand-plotted is the whole point and also the whole risk.
        // 112, NOT THE HANDOFF'S 113, and the missing pixel is worth explaining.
        //
        // The design's floor is 94 authored and it sits exactly ON it: the
        // tightest pair in all three figures is the spine's own -108 and -202,
        // 94 apart to the pixel. x1.2 makes that 112.8, the handoff rounds it up
        // to 113 in prose, and the ladder rounds to -130 and -242 -- so the
        // shipped plots measure 112 and no integer plot at this scale can do
        // better without moving the ladder.
        //
        // Stated as what the plots actually hold rather than as what the
        // document rounds to, because a constant the data provably violates is
        // worse than none: the alternative was a tolerance on the assertion,
        // and a loose test against an aspirational number catches less than a
        // strict test against a true one.
        public const float SeparationFloor = 112f;   // 94 authored, x1.2, rounded down
        public const float SideClearance = 130f;     // 108 authored

        // How wide and tall the widest plot actually is, orbs included.
        public static float TreeWidth => 422f * 2f + OrbCap;

        public static float TreeHeight => 924f + OrbCap;

        // ---- edges ---------------------------------------------------------------
        //
        // TEN, not three. v2 drew the connections as 3px hairlines, which read
        // as a wiring diagram; v1's are limbs with a lit crack down them, and
        // the width is what makes an edge look grown rather than drawn.
        public const float EdgeWidth = 10f;

        // THE UNLIT CONNECTION IS A HAIRLINE. The lit one is not.
        //
        // Both were drawn at EdgeWidth, on the argument recorded below that a
        // 3px line reads as a wiring diagram rather than as a limb. Measured off
        // the design's recording, its unlit connections run 3 to 4px and its lit
        // ones 5 to 21 -- so the argument holds, and it holds for the LIT edge
        // only. An unlit connection is meant to recede until something travels
        // it; ten pixels of it between every pair of ash stones is a lattice
        // drawn over the sky at the same weight as the constellation.
        public const float EdgeDimWidth = 4f;

        // The lit layers, as multiples of the base so they cannot drift from
        // it. v1's numbers: a wide soft halo, a thin bright core down its
        // middle.
        public const float EdgeGlowWidth = EdgeWidth * 2.6f;
        public const float EdgeCoreWidth = EdgeWidth * 0.45f;

        // The travelling dot. Wider than the limb it runs along, so it reads
        // as something moving THROUGH the line rather than as a bright patch
        // of it.
        public const float EdgeSparkSize = EdgeWidth * 1.8f;

        // ---- paging ------------------------------------------------------------

        // Where a tree sits horizontally while the sky slides between them.
        //
        // A full screen width apart, so exactly one is ever centred and the
        // neighbours are genuinely off-stage rather than peeking. The slide is
        // what makes three trees read as three PLACES rather than three tabs.
        // ONE STAGE WIDE, which is what makes a page change read as the sky
        // sliding rather than as the tree jumping.
        //
        // THE SCREEN, NOT THE STAGE, and the sky being narrower than the screen
        // is not a reason to shorten it.
        //
        // This was briefly SkyWidth, on the argument that the stage is what
        // slides and the panel covers the rest. Half of that is true: the panel
        // covers the neighbour on the RIGHT. Nothing covers the one on the
        // LEFT, and a 1464 stride on a 1920 canvas leaves 228px of the previous
        // constellation standing in the open beside the current one -- which is
        // exactly what ConstellationLayoutTests.TheNeighboursAreGenuinelyOffStage
        // measures, and how it was caught.
        public static float PageStride => UiFrames.Reference.X;

        public static float PageX(int index, int current) => (index - current) * PageStride;

        // How far through a slide, 0..1, eased.
        //
        // A pure static seam like every other animation curve in this project:
        // the shape can be pinned by an EditMode test with no scene, no
        // coroutine and no frame.
        // 620ms, the handoff's own figure, up from 420. A stage-wide slide is
        // a longer journey than the screen-wide one this was tuned for and
        // wants the time; the curve below is the ease it asks for.
        public const float SlideSeconds = 0.62f;

        // No divide-by-zero guard here, deliberately. SlideSeconds is a const,
        // so the compiler folded that branch away and warned it was
        // unreachable (CS0162) -- a guard that cannot fire is not protection,
        // it is noise that hides the next real unreachable-code warning.
        //
        // What it was guarding against is real, though: at SlideSeconds 0 and
        // elapsed 0 the division is NaN, every comparison below is false, and
        // this returns NaN rather than a progress. So the guarantee moved to
        // where it can actually hold -- ConstellationLayoutTests asserts the
        // constant is positive, which fails at test time instead of producing
        // a NaN slide at play time.
        public static float SlideProgress(float elapsed)
        {
            float t = elapsed / SlideSeconds;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            // Ease in-out. A slide that starts and stops abruptly reads as a cut
            // with extra steps; the whole point of moving is that the player
            // keeps their bearings.
            return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
        }

        // The sky's own offset partway through a slide from `from` to `to`.
        public static float SlideOffset(int from, int to, float progress) =>
            -PageStride * (from + (to - from) * progress);

        // ---- which tree ----------------------------------------------------------

        // Paging is a CLAMPED line, not a loop.
        //
        // Wrapping from the last tree back to the first would make the arrows
        // lie about where the ends are, and a player who has paged three times
        // to the right should be able to tell they are at the edge without
        // counting. The arrow simply stops being offered.
        public static int Step(int current, int direction, int count)
        {
            if (count <= 0) return 0;

            int next = current + (direction < 0 ? -1 : direction > 0 ? 1 : 0);
            if (next < 0) return 0;
            if (next >= count) return count - 1;
            return next;
        }

        public static bool CanStep(int current, int direction, int count) =>
            Step(current, direction, count) != current;

        // ---- motion ----------------------------------------------------------
        //
        // Every period the screen animates on, in one place, because the whole
        // point of them is that they DISAGREE. Nine loops running at 2.4, 3.2,
        // 4.6, 5.0, 6.0, 7.2, 11, 15, 41, 58, 74, 96, 112 and 148 seconds
        // share no common multiple worth reaching, so the sky never repeats a
        // frame inside a session -- which is the only thing that separates
        // "alive" from "looping". Scattered across the controller as literals
        // that property is invisible and one careless round to 5.0 destroys it.

        // The stagger. A tree of stones that all breathe together is a tree
        // that blinks; 491ms against a 3200ms cycle walks the phase around
        // without ever landing twice in 21 slots.
        public const float RestingStrideMs = 491f;
        public const float RestingCycleMs = 3200f;

        public static float RestingPhase(int slot) =>
            slot * RestingStrideMs % RestingCycleMs / 1000f;

        public const float CoreFlickerSeconds = 3.2f;

        // ---- the ember's own flicker ---------------------------------------------
        //
        // A FIRE, NOT A BREATHE, and the difference is measurable. Frame-by-frame
        // off the design's recording, the white-hot centre of a kindled stone
        // swings 131 to 313 pixels of area -- 2.4x, so about 1.55x across --
        // with peaks landing 0.6 to 1.0 seconds apart and never on a beat. The
        // body of the sphere moves by 4% over the same stretch and is not what
        // the eye is reading.
        //
        // A single smooth 3.2s cycle produced a stone that swelled and sank.
        // Two incommensurate sines summed produce something whose peaks are
        // irregular, whose troughs are uneven, and which does not repeat inside
        // a session -- which is the whole of what makes a flame look like one.
        public const float EmberFastSeconds = 0.62f;
        public const float EmberSlowSeconds = 0.97f;

        public const float EmberCoreMinScale = 0.78f;
        public const float EmberCoreMaxScale = 1.34f;
        // ONLY THE VERY CENTRE IS WHITE. The delivered sphere already runs from
        // deep red rock through orange veins to a white heart; laid on at full
        // strength this overlay flattened that gradient into one bright orange
        // mass. It is a highlight on the heart, not a second light source.
        public const float EmberCoreMinAlpha = 0.18f;
        public const float EmberCoreMaxAlpha = 0.72f;

        public static float EmberFlicker(float time, float phase)
        {
            float fast = Breathe(Cycle(time, EmberFastSeconds, phase));
            float slow = Breathe(Cycle(time, EmberSlowSeconds, phase * 1.7f));

            return Clamp01(fast * 0.55f + slow * 0.45f);
        }
        // Doubled from 4.6 to 9.2, i.e. 50% slower (session brief,
        // 2026-09-03): the earlier "halve the crackle" ask was aimed at this
        // halo crackle on the orb auras, not TalentEdgeCrackle's edge
        // flicker -- that got reverted back to 17/29. This is the one the
        // user actually meant slowed down.
        public const float HaloCrackleSeconds = 9.2f;
        public const float ReadyPulseSeconds = 2.4f;
        public const float CapCoronaSeconds = 5.0f;

        // ---- the kindling beat ----------------------------------------------
        //
        // 1.1 SECONDS, IN FOUR OVERLAPPING PARTS. They overlap on purpose: the
        // crust cracks while the stone is still catching, and the motes leave
        // before either has finished. Played in sequence it reads as a machine
        // completing steps; played over itself it reads as something igniting.
        public const float KindleSeconds = 1.12f;
        public const float KindleCrustSeconds = 0.52f;
        public const float KindleCatchSeconds = 0.90f;
        public const float KindleEdgeSeconds = 0.62f;
        public const float KindleMoteSeconds = 0.70f;
        public const int KindleMoteCount = 6;

        // The six motes leave unevenly -- the gaps widen, 90/70/80/90/90 --
        // because six embers on an even beat is a metronome.
        private static readonly float[] MoteDelays = { 0f, 0.09f, 0.16f, 0.24f, 0.33f, 0.42f };

        public static float MoteDelay(int index) =>
            index < 0 || index >= MoteDelays.Length ? 0f : MoteDelays[index];

        // Each part clamps at its own end rather than at the beat's, so a part
        // that finishes early STAYS finished instead of easing back.
        public static float Phase(float elapsed, float length) =>
            length <= 0f ? 1f : Clamp01(elapsed / length);

        // The stone's own catch: overshoots to 1.18 and settles. The overshoot
        // is the difference between a stone lighting up and a stone catching.
        public static float CatchScale(float phase)
        {
            if (phase <= 0f) return 1f;
            if (phase >= 1f) return 1f;

            // Peak a third of the way in, then ease back down over the rest.
            const float peak = 0.34f;
            return phase < peak
                ? 1f + 0.18f * (phase / peak)
                : 1f + 0.18f * (1f - Ease((phase - peak) / (1f - peak)));
        }

        // ---- the push-in -----------------------------------------------------
        //
        // Selecting a stone leans the sky towards it: 2% larger, and shifted
        // by a seventh of how far off-centre the stone is. Deliberately far
        // less than centring it -- a camera that snapped the selection to the
        // middle would move the whole tree under a pointer that is trying to
        // compare two stones next to each other.
        public const float PushInScale = 1.02f;
        public const float PushInFraction = 0.14f;
        public const float PushInSeconds = 0.62f;

        public static float PushInOffset(float starX) => -starX * PushInFraction;

        // ---- the backdrop ----------------------------------------------------
        //
        // Two pan tracks, unequal and opposed. That inequality IS the parallax:
        // near drifts further and slower than far, so the fields separate
        // rather than sliding as one painting.
        public const float StarPanNearSeconds = 148f;
        public const float StarPanFarSeconds = 96f;
        public static readonly UiVec StarPanNear = new UiVec(-30f, 12f);
        public static readonly UiVec StarPanFar = new UiVec(11f, -7f);

        public const float CloudBreatheSeconds = 41f;
        private static readonly float[] CloudDriftSeconds = { 58f, 74f, 112f };

        public static float CloudDrift(int index) =>
            index < 0 || index >= CloudDriftSeconds.Length
                ? CloudDriftSeconds[CloudDriftSeconds.Length - 1]
                : CloudDriftSeconds[index];

        public const float DustRiseSeconds = 9.4f;
        public const float DustRise = 340f;

        // TWO STREAKS, IDLE FOR ALMOST ALL OF THEIR CYCLE. This is the one
        // EVENT in the backdrop and it has to stay rare to read as one: 38s and
        // 57s apart with a 23s offset between them, so they never arrive
        // together and the sky is empty of them nearly all the time.
        private static readonly float[] StreakSeconds = { 38f, 57f };
        public const float StreakOffsetSeconds = 23f;
        public const float StreakTravelSeconds = 0.9f;
        public const float StreakTravel = 620f;

        public static float StreakPeriod(int index) =>
            index < 0 || index >= StreakSeconds.Length ? StreakSeconds[0] : StreakSeconds[index];

        // A sawtooth on [0,1) for a loop of the given period. One function
        // rather than a phase field per layer: none of these loops has any
        // state worth keeping, so time is the only input they need.
        public static float Cycle(float time, float period, float offset = 0f) =>
            period <= 0f ? 0f : Repeat(time + offset, period) / period;

        // A 0-1-0 triangle, eased. What every breathe, pulse and flicker on
        // this screen is made of.
        public static float Breathe(float cycle) =>
            Ease(1f - Abs(cycle * 2f - 1f));

        private static float Ease(float t) => t * t * (3f - 2f * t);

        // ENGINE-FREE, like the rest of this file. Domain cannot reach Mathf,
        // and these three are the whole of what the motion arithmetic needs.
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Abs(float v) => v < 0f ? -v : v;

        private static float Repeat(float v, float period)
        {
            if (period <= 0f) return 0f;

            float r = v - (int)(v / period) * period;
            return r < 0f ? r + period : r;
        }

    }
}
