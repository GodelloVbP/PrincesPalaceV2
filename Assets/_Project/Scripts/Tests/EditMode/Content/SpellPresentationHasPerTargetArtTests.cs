using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // WHAT FightBeatPlayer.WantsContactFx ACTUALLY NEEDS TO ASK: does
    // anything land ON THE STRUCK TARGET, as against the ground beneath the
    // whole formation. HasArt answers a different question -- "does this
    // block draw anything at all" -- and a bare `groundPath` satisfies that
    // while leaving the target itself with no impact language, which is the
    // finding this pins.
    public class SpellPresentationHasPerTargetArtTests
    {
        [Test]
        public void GroundPathAloneHasNoPerTargetArt()
        {
            var vfx = new SpellPresentation { groundPath = "Spells/cinderfault_ground" };

            Assert.IsTrue(vfx.HasArt, "fixture: a ground-only block should still count as having art");
            Assert.IsFalse(vfx.HasPerTargetArt,
                "a ground fault puts nothing on the struck target -- the house contact arc should still play");
        }

        [Test]
        public void APreLayerPathHasPerTargetArt()
        {
            var vfx = new SpellPresentation { path = "Spells/frost_flare" };

            Assert.IsTrue(vfx.HasPerTargetArt,
                "a pre-layer sheet draws directly on the target -- the house arc must stay out of the way");
        }

        [Test]
        public void ALayerWithAPerTargetPlacementHasPerTargetArt()
        {
            var vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = new[]
                {
                    new SpellLayer { render = "sprite", place = "target", path = "Spells/frost_flare" },
                },
            };

            Assert.IsTrue(vfx.HasPerTargetArt,
                "a target-placed layer draws on the struck target just like a pre-layer path does");
        }

        [Test]
        public void ACastLevelOnlyLayeredBlockHasNoPerTargetArt()
        {
            var vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = new[]
                {
                    new SpellLayer { render = "sprite", place = "formation", path = "Spells/cinderfault_ground" },
                },
            };

            Assert.IsFalse(vfx.HasPerTargetArt,
                "every authored layer is cast-level -- nothing here draws on the struck target either");
        }
    }
}
