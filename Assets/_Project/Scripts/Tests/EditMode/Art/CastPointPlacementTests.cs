using NUnit.Framework;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE ONE PIECE OF FightController.SpellVfx.CasterCastPoint THAT IS A
    // FORMULA rather than "ask the transform" -- see CastPointPlacement's own
    // header for why this reduction holds and why it lives here rather than
    // in Core, where every other placement rule in that file does.
    //
    // Literal expected values throughout, pinning what "scaled, mirrored and
    // riding the hover" actually comes out to for Odette's own numbers, so a
    // future change to the formula (or to which sign mirror applies to) has
    // something to fail against besides a screenshot.
    public class CastPointPlacementTests
    {
        // Odette's own authored point (StanceManifest.json), at a slot scale
        // and origin picked to be easy to hand-check: scale 0.76 (roughly
        // FightStageAnchors.SpriteScale at full depth) and an origin that is
        // not the stage's own centre, so a bug that dropped slotOrigin
        // entirely could not pass by accident.
        private static readonly CastPointSpec Odette = new CastPointSpec(201.5f, 135.5f);

        [Test]
        public void AnAuthoredPoint_IsScaledAndOffsetFromTheSlotOrigin()
        {
            var origin = new UiVec(300f, -218f);
            var stage = CastPointPlacement.OnStage(Odette, mirror: 1f, scale: 0.76f, origin);

            // 300 + 201.5 * 1 * 0.76 = 453.14; -218 + 135.5 * 0.76 = -115.02.
            Assert.AreEqual(453.14f, stage.X, 0.01f);
            Assert.AreEqual(-115.02f, stage.Y, 0.01f);
        }

        // MIRRORED FLIPS dx AND ONLY dx -- a book held out in front does not
        // move up or down when the actor is drawn facing the other way, only
        // sideways.
        [Test]
        public void MirroringFlipsOnlyTheHorizontalComponent()
        {
            var origin = new UiVec(300f, -218f);
            var stage = CastPointPlacement.OnStage(Odette, mirror: -1f, scale: 0.76f, origin);

            Assert.AreEqual(300f - 201.5f * 0.76f, stage.X, 0.01f);
            Assert.AreEqual(-218f + 135.5f * 0.76f, stage.Y, 0.01f);
        }

        // RIDING THE HOVER is not a separate step this function takes -- it is
        // the caller folding the hover into `slotOrigin` before asking (the
        // same anchoredPosition SetHover already writes into every frame),
        // and this pins that a change in origin alone -- nothing else -- is
        // what moves the resolved point, at whatever scale and mirror were
        // already in effect. Odette's hover.height of 70 stage pixels is
        // used here as the origin's own Y shift, standing in for what
        // FightController actually reads off her slot.
        [Test]
        public void ChangingOnlyTheSlotOriginMovesTheStageOnlyPointByTheSameAmount()
        {
            var grounded = new UiVec(300f, -218f);
            var hovering = grounded.WithY(grounded.Y + 70f);

            var atGround = CastPointPlacement.OnStage(Odette, mirror: 1f, scale: 0.76f, grounded);
            var atHover = CastPointPlacement.OnStage(Odette, mirror: 1f, scale: 0.76f, hovering);

            Assert.AreEqual(atGround.X, atHover.X, 0.0001f, "hover is vertical only -- it must not touch X.");
            Assert.AreEqual(70f, atHover.Y - atGround.Y, 0.0001f,
                "the resolved point rides the origin exactly -- the DX/DY offset itself is scale-only and does " +
                "not re-derive or damp the caller's hover shift.");
        }

        // A ZERO POINT (dx 0, dy 0) is what an unauthored actor's fallback
        // stands in for at the call site -- CasterCastPoint never actually
        // calls OnStage for one, but this pins that the formula degrades to
        // "exactly the slot origin" if it ever did, which is the sanity
        // check for why the caller-supplied fallback and this formula could
        // never disagree for a (0, 0) point.
        [Test]
        public void AZeroPoint_ResolvesToExactlyTheSlotOrigin()
        {
            var origin = new UiVec(-524f, -178f);
            var stage = CastPointPlacement.OnStage(new CastPointSpec(0f, 0f), mirror: -1f, scale: 0.58f, origin);

            Assert.AreEqual(origin.X, stage.X, 0.0001f);
            Assert.AreEqual(origin.Y, stage.Y, 0.0001f);
        }

        // Shawn's own authored point, at a party-side scale/origin, as the
        // model's second use rather than a second copy of Odette's numbers.
        [Test]
        public void ShawnsOwnPoint_ScaledAtAFrontRowPartyOrigin()
        {
            var shawn = new CastPointSpec(26.5f, 263.8f);
            var origin = new UiVec(-360f, -218f);

            var stage = CastPointPlacement.OnStage(shawn, mirror: 1f, scale: 0.76f, origin);

            Assert.AreEqual(-360f + 26.5f * 0.76f, stage.X, 0.01f);
            Assert.AreEqual(-218f + 263.8f * 0.76f, stage.Y, 0.01f);
        }
    }
}
