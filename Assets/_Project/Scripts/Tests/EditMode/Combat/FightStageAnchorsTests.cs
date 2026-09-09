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
            Assert.AreEqual(60f, FightStageAnchors.PartyRetreat, 0.001f);
            Assert.AreEqual(1500f, FightStageAnchors.StageSize.X, 0.001f);
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

            Assert.AreEqual(360f, FightStageAnchors.Party.Near.X, 0.001f);
            Assert.AreEqual(-218f, FightStageAnchors.Party.Near.Y, 0.001f);
            Assert.AreEqual(720f, FightStageAnchors.Party.Far.X, 0.001f);
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

        [Test]
        public void MirroringFlipsXAndAddsThePartyRetreat()
        {
            // C4: no longer a pure mirror -- the party (mirrored) side
            // stands PartyRetreat further back on X than the enemy side's
            // mirror image. STILL TRUE OF X, and X is all this claims now:
            // the two sides' ground lines diverge behind the front rank (see
            // ThePartyLineIsSteeperThanTheEnemyLine below).
            var right = FightStageAnchors.SlotOffset(1, 3, mirrored: false);
            var left = FightStageAnchors.SlotOffset(1, 3, mirrored: true);

            Assert.AreEqual(-(right.X + FightStageAnchors.PartyRetreat), left.X, 0.001f);
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

            Assert.AreEqual(-360f, FightStageAnchors.SlotOffset(0, 3, mirrored: true).X, 0.001f);
            Assert.AreEqual(-540f, FightStageAnchors.SlotOffset(1, 3, mirrored: true).X, 0.001f);
            Assert.AreEqual(-720f, FightStageAnchors.SlotOffset(2, 3, mirrored: true).X, 0.001f);
        }

        // THE DEFECT THIS FORMATION EXISTS TO FIX, pinned where it can fail.
        //
        // The middle party figure's contact ring was drawn behind the roster
        // plates, which the party half of the HUD stands on the floor: the
        // block occupies x -920..-468 with its top edge at y -161, measured
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
            // -468 is the OUTER of the two right edges the roster column has
            // had this week (452-wide plates centred on -694; the narrower
            // 380-wide cards that replaced them stop at -540). Assuming the
            // wider block checks MORE slots, never fewer, so this stays
            // correct against either.
            const float PlateLeft = -920f;
            const float PlateRight = -468f;
            const float PlateTop = -161f;
            const float RingDrop = 8f;
            const float Margin = 12f;

            var offset = FightStageAnchors.SlotOffset(slot, 3, mirrored: true);
            if (offset.X < PlateLeft || offset.X > PlateRight) return;

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
            // It has already earned that TWICE: moving the far anchor out to
            // 565 put the outermost slot 65px beyond the old 1000-wide frame,
            // and C4's PartyRetreat put the retreated party slot 25px beyond
            // the 1200-wide frame that fix landed on -- which is why BOTH
            // sides are checked here now, not just the enemy one.
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

        // Mirroring is the party side, and it must mirror the SPREAD too rather
        // than only the endpoints.
        [Test]
        public void ThePartySideMirrorsTheSameSpreadPlusTheRetreat()
        {
            // C4: -360/-720, not -300/-660 -- the party side's whole spread
            // is pushed back by PartyRetreat, not just its endpoints
            // individually re-tuned. (-720 was -625 before Far.X's 2026-09-09
            // widen to 660, see Far's own comment.)
            var front = FightStageAnchors.SlotOffset(0, 2, mirrored: true);
            var back = FightStageAnchors.SlotOffset(1, 2, mirrored: true);

            Assert.AreEqual(-360f, front.X, 0.01f);
            Assert.AreEqual(-720f, back.X, 0.01f);
        }
    }
}
