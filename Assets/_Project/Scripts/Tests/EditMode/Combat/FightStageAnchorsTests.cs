using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // Pins for the combat stage's pixel anchors.
    //
    // Every expected value is a LITERAL, never a re-derivation of the formula
    // being tested (CLAUDE.md gotcha 5). That matters more than usual here:
    // these numbers were tuned against the real art, so a test that recomputed
    // StageLayout's own lerp would happily agree with a stage that had silently
    // moved.
    //
    // NOTHING HERE IS v1-PARITY ANY MORE, and this class has twice claimed to
    // be. First Y went, because the inherited -300/-140 were v1's numbers from
    // BEFORE v1 fixed them and they put every front row's feet behind the HUD.
    // Then X, the scales and the frame size went with the formation change:
    // depth now recedes OUTWARD rather than in, so the back rank is the one
    // furthest from the enemy instead of the one nearest it.
    public class FightStageAnchorsTests
    {
        [Test]
        public void TheAnchorsThemselvesAreWhereTheDerivationPutThem()
        {
            Assert.AreEqual(300f, FightStageAnchors.Near.X, 0.001f);
            Assert.AreEqual(-218f, FightStageAnchors.Near.Y, 0.001f);
            // Far.X 565 -> 660 and StageSize.X 1260 -> 1500, 2026-09-09: the
            // owner-reported party overlap (Odette swallowed by the middle
            // slot) -- see FightStageAnchors' own comment on Far.
            Assert.AreEqual(660f, FightStageAnchors.Far.X, 0.001f);
            Assert.AreEqual(-125f, FightStageAnchors.Far.Y, 0.001f);
            Assert.AreEqual(0.76f, FightStageAnchors.SpriteScale, 0.001f);
            Assert.AreEqual(-34f, FightStageAnchors.NameplateOffset, 0.001f);
            // StageSize.X 1500 -> 1700, 2026-09-09: the party's far anchor
            // moved out to 810 and this frame has to contain it -- see
            // EveryStageSlotFitsInsideTheStageRect below, which is the check
            // that keeps finding this.
            Assert.AreEqual(1700f, FightStageAnchors.StageSize.X, 0.001f);
            Assert.AreEqual(600f, FightStageAnchors.StageSize.Y, 0.001f);

            // The two formations, as magnitudes out from stage centre. The
            // party's far ground line is -64 where the enemy's is -125
            // (FightStageAnchors.PartyFarY's own note): the roster plates
            // stand on the party's half of the floor and the middle slot was
            // landing inside them.
            Assert.AreEqual(300f, FightStageAnchors.Enemy.Near.X, 0.001f);
            Assert.AreEqual(-218f, FightStageAnchors.Enemy.Near.Y, 0.001f);
            Assert.AreEqual(660f, FightStageAnchors.Enemy.Far.X, 0.001f);
            Assert.AreEqual(-125f, FightStageAnchors.Enemy.Far.Y, 0.001f);

            // 360/720 -> 320/810, 2026-09-09: the three party figures still
            // read as a clump at a 180px slot pitch against 197-228px
            // bodies. The party's X endpoints are authored outright now
            // rather than as the enemy's plus a PartyRetreat scalar, which
            // is gone -- see FightStageAnchors.PartyNearX's own note for the
            // measured overlaps and for what caps 810 (Shawn's idle at the
            // back slot reaches 126.5 stage px left of his mark, and the 4:3
            // canvas stops at -960).
            Assert.AreEqual(320f, FightStageAnchors.Party.Near.X, 0.001f);
            Assert.AreEqual(-218f, FightStageAnchors.Party.Near.Y, 0.001f);
            Assert.AreEqual(810f, FightStageAnchors.Party.Far.X, 0.001f);
            Assert.AreEqual(-64f, FightStageAnchors.Party.Far.Y, 0.001f);
        }

        [Test]
        public void ThreeSlots_LandOnTheNearAnchor_TheMidpoint_AndTheFarAnchor()
        {
            // Depths 0, 0.5, 1 across three slots; x lerps 300 -> 660, OUTWARD
            // with depth (widened from 565 2026-09-09, see Far's own
            // comment), and y lerps -218 -> -125.
            var near = FightStageAnchors.SlotOffset(0, 3, mirrored: false);
            Assert.AreEqual(300f, near.X, 0.001f);
            Assert.AreEqual(-218f, near.Y, 0.001f);

            var mid = FightStageAnchors.SlotOffset(1, 3, mirrored: false);
            Assert.AreEqual(480f, mid.X, 0.001f);
            Assert.AreEqual(-171.5f, mid.Y, 0.001f);

            var far = FightStageAnchors.SlotOffset(2, 3, mirrored: false);
            Assert.AreEqual(660f, far.X, 0.001f);
            Assert.AreEqual(-125f, far.Y, 0.001f);
        }

        // WAS MirroringFlipsXAndAddsThePartyRetreat. PartyRetreat is gone --
        // it stopped being one scalar when the party got its own Y
        // (6fedab18) and stopped being one on X when the party's spread had
        // to widen further than the enemy's (+20 near, +150 far). What is
        // left to claim, and all that was ever load-bearing, is that
        // MIRRORING is a sign flip and nothing else: the party formation is
        // authored as magnitudes out from stage centre, exactly like the
        // enemy's, so a future third formation cannot be authored with its
        // signs already flipped.
        //
        // Literals rather than -Party.Far.X, so this fails if SlotOffset
        // stops applying the mirror as well as if the endpoints move.
        [Test]
        public void MirroringOnlyFlipsTheSignOfTheAuthoredMagnitude()
        {
            Assert.AreEqual(-320f, FightStageAnchors.SlotOffset(0, 3, mirrored: true).X, 0.001f);
            Assert.AreEqual(320f, FightStageAnchors.Party.Near.X, 0.001f,
                "the party's endpoints are authored positive, out from stage centre");

            Assert.AreEqual(-810f, FightStageAnchors.SlotOffset(2, 3, mirrored: true).X, 0.001f);
            Assert.AreEqual(810f, FightStageAnchors.Party.Far.X, 0.001f);
        }

        // ---- the party's own line ---------------------------------------------

        // Literals, not a re-derivation: -218 -> -64 across three slots puts
        // the middle party slot's ground line at -141.
        [Test]
        public void ThePartyLineIsSteeperThanTheEnemyLine()
        {
            Assert.AreEqual(-218f, FightStageAnchors.SlotOffset(0, 3, mirrored: true).Y, 0.001f,
                "the two front ranks still share the floor they face each other across");
            Assert.AreEqual(-141f, FightStageAnchors.SlotOffset(1, 3, mirrored: true).Y, 0.001f);
            Assert.AreEqual(-64f, FightStageAnchors.SlotOffset(2, 3, mirrored: true).Y, 0.001f);

            // -320 / -565 / -810, widened from -360 / -540 / -720 on
            // 2026-09-09: at a 180px pitch three 197-228px bodies overlapped
            // by a third each. 245 per step is what makes them touch rather
            // than stack -- FightStageAnchors.PartyNearX carries the
            // measurements.
            Assert.AreEqual(-320f, FightStageAnchors.SlotOffset(0, 3, mirrored: true).X, 0.001f);
            Assert.AreEqual(-565f, FightStageAnchors.SlotOffset(1, 3, mirrored: true).X, 0.001f);
            Assert.AreEqual(-810f, FightStageAnchors.SlotOffset(2, 3, mirrored: true).X, 0.001f);
        }

        // THE DEFECT THIS FORMATION EXISTS TO FIX, pinned where it can fail.
        //
        // The middle party figure's contact ring was drawn behind the roster
        // plates, which the party half of the HUD stands on the floor: the
        // block occupies x -920..-540 with its top edge at y -161, measured
        // off the real 4:3 capture, and BuildRosterPlates is declared after
        // both stages so it paints over whatever is standing there.
        //
        // NOT COVERED BY FightScreenTests' foot-band-versus-HUD scan, which
        // is the audit that ought to own this: that scan skips subtrees
        // declared Inactive, and the roster plates are built Inactive and
        // switched on at runtime by the controller. So the number is pinned
        // here instead, in Domain, where it costs a second.
        //
        // The plate geometry is stated as literals rather than read out of
        // FightScreen: this assembly is Domain-only, and a Domain test that
        // reached into the UiKit screen for private layout constants would
        // be the wrong dependency even if it could. If the roster block
        // moves, this pin is wrong in the safe direction -- it fails and
        // says why.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void NoPartySlotStandsInsideTheRosterPlates(int slot)
        {
            // -540, RE-READ OFF FightScreen 2026-09-09. This said -468 --
            // the right edge of the 452-wide plates centred on -694, which
            // had already been replaced by the 380-wide cards centred on
            // -730 (PartyPlateWidth / PartyPlateCentreX,
            // FightScreenTests.EveryCardInTheHudColumnSharesTheSameVertical-
            // Edges pins -920..-540 from the solved screen). The old value
            // was defended as "the wider block checks MORE slots, never
            // fewer", which is true and still the wrong number to write
            // down: a stale literal that happens to be conservative is
            // indistinguishable from one that has silently stopped
            // describing anything.
            const float PlateLeft = -920f;
            const float PlateRight = -540f;
            const float PlateTop = -161f;
            const float RingDrop = 8f;
            const float Margin = 12f;

            // THE RING'S BAND, not the bare mark. The ring is centred on the
            // actor's FOOT BAND, not on its slot mark
            // (FightController.StageVisuals' FootBandCentreFraction), and on
            // this roster it lands as much as 121px left of the mark and
            // 70px right of it -- Shawn's idle is drawn well left of his own
            // canvas centre. Testing the mark alone let a slot whose mark
            // clears the column by 60px put a third of its ring inside it,
            // which is exactly the failure being pinned. Both numbers are
            // measured off the committed art at the near slot's scale (the
            // largest), so they over-cover the two smaller slots.
            const float RingReachLeft = 121f;
            const float RingReachRight = 70f;

            var offset = FightStageAnchors.SlotOffset(slot, 3, mirrored: true);
            if (offset.X + RingReachRight < PlateLeft || offset.X - RingReachLeft > PlateRight) return;

            Assert.GreaterOrEqual(offset.Y - RingDrop, PlateTop + Margin,
                $"party slot {slot}'s contact ring at y {offset.Y - RingDrop} is inside the roster " +
                "plates (top edge -161), which draw over the stage - see FightStageAnchors.PartyFarY");
        }

        [Test]
        public void SlotScale_ComposesTheDepthCurveWithTheGlobalShrink()
        {
            // ScaleForDepth lerps 0.94 -> 0.82 (was 1.0 -> 0.74, narrowed
            // 2026-09-09 -- the owner's "front one is too big and the back
            // one too small", see StageLayout.NearScale/FarScale), then
            // everything is shrunk by 0.76.
            Assert.AreEqual(0.7144f, FightStageAnchors.SlotScale(0, 3), 0.0001f);
            Assert.AreEqual(0.6688f, FightStageAnchors.SlotScale(1, 3), 0.0001f);
            Assert.AreEqual(0.6232f, FightStageAnchors.SlotScale(2, 3), 0.0001f);
        }

        [Test]
        public void ALoneCombatantSitsFullyForward()
        {
            // A single-slot row does not split the difference - a lone boss
            // should dominate the stage rather than hover at mid-depth.
            var only = FightStageAnchors.SlotOffset(0, 1, mirrored: false);
            Assert.AreEqual(300f, only.X, 0.001f);
            Assert.AreEqual(-218f, only.Y, 0.001f);
            Assert.AreEqual(0.7144f, FightStageAnchors.SlotScale(0, 1), 0.0001f);
        }

        [Test]
        public void EveryStageSlotFitsInsideTheStageRect()
        {
            // The anchors and the stage size are tuned separately, so an anchor
            // tweak can push a slot outside the frame it is declared in — the
            // screen tree's containment audit would fail at build time, and
            // this says so a step earlier, in Domain, where it costs a second.
            //
            // It has already earned that THREE TIMES: moving the far anchor
            // out to 565 put the outermost slot 65px beyond the old
            // 1000-wide frame; C4's PartyRetreat put the retreated party slot
            // 25px beyond the 1200-wide frame that fix landed on -- which is
            // why BOTH sides are checked here, not just the enemy one; and
            // PartyFarX's move to 810 put it 60px beyond the 1500-wide one.
            float halfW = FightStageAnchors.StageSize.X / 2f;
            float halfH = FightStageAnchors.StageSize.Y / 2f;

            for (int i = 0; i < 3; i++)
            {
                foreach (bool mirrored in new[] { false, true })
                {
                    var offset = FightStageAnchors.SlotOffset(i, 3, mirrored);
                    Assert.LessOrEqual(System.Math.Abs(offset.X), halfW, $"slot {i} x (mirrored={mirrored})");
                    Assert.LessOrEqual(System.Math.Abs(offset.Y), halfH, $"slot {i} y (mirrored={mirrored})");
                }
            }
        }

        // ---- the formation adapts to how many actors are actually there ------

        // The shipped bug: a room fielding TWO monsters put them in slots 0 and
        // 1 of a three-slot formation and left slot 2 -- the widest position --
        // empty. They stood 132 apart against a 675px-wide rat sheet, so the
        // back one was 78% hidden and the pair read as a single monster.
        //
        // Literals, not a re-derivation: 300 and 660 are Near.X and Far.X (Far
        // widened from 565 2026-09-09, see Far's own comment), and the point
        // of the test is that a PAIR reaches both ends of that range.
        [Test]
        public void TwoActorsStandAtBothEndsOfTheRange_NotBunchedAtTheNearEnd()
        {
            var front = FightStageAnchors.SlotOffset(0, 2, mirrored: false);
            var back = FightStageAnchors.SlotOffset(1, 2, mirrored: false);

            Assert.AreEqual(300f, front.X, 0.01f, "the front of a pair should sit at the near anchor");
            Assert.AreEqual(660f, back.X, 0.01f, "the back of a pair should reach the FAR anchor, not the midpoint");
            Assert.AreEqual(360f, back.X - front.X, 0.01f,
                "a pair separated by less than the full range is the bunching this test exists to catch");
        }

        [Test]
        public void ASoloActorStandsAtTheFront()
        {
            var only = FightStageAnchors.SlotOffset(0, 1, mirrored: false);

            Assert.AreEqual(300f, only.X, 0.01f);
            Assert.AreEqual(-218f, only.Y, 0.01f, "a lone monster belongs at the near ground line, not floating mid-stage");
        }

        // Whatever the count, the outermost actor must still land at the far
        // anchor -- that is what "spread across the range" means, and it is the
        // property that stops a party of two bunching the same way.
        [TestCase(2)]
        [TestCase(3)]
        public void TheLastActorAlwaysReachesTheFarAnchor(int count)
        {
            var last = FightStageAnchors.SlotOffset(count - 1, count, mirrored: false);

            Assert.AreEqual(660f, last.X, 0.01f, $"with {count} actors the back one stops short of the far anchor");
        }

        // A PARTY OF TWO reaches its own formation's ends, the same way a
        // pair of monsters reaches the enemy one's -- the bunching bug above
        // is a property of the depth curve, not of one side.
        [Test]
        public void APartyOfTwoAlsoStandsAtBothEndsOfItsOwnRange()
        {
            // -320/-810, not -300/-660: the party's endpoints are its own
            // (FightStageAnchors.PartyNearX/PartyFarX), wider than the
            // enemy's because three similar bipeds have to separate on a
            // half of the floor that also carries the HUD. Was -360/-720
            // until 2026-09-09, when the three of them still read as a
            // clump.
            var front = FightStageAnchors.SlotOffset(0, 2, mirrored: true);
            var back = FightStageAnchors.SlotOffset(1, 2, mirrored: true);

            Assert.AreEqual(-320f, front.X, 0.01f);
            Assert.AreEqual(-810f, back.X, 0.01f);
            Assert.AreEqual(490f, front.X - back.X, 0.01f,
                "a pair separated by less than the full range is the bunching this exists to catch");
        }
    }
}
