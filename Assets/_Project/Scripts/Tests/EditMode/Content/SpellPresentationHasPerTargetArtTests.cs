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

        // THE FOLLOWER-CHAIN GAP this pins: a layer's own Travels/Place can
        // both say "not per-target" while it still lands on the struck
        // target, because it rides one that does. A still authors neither
        // -- it is not itself per-target-placed and it does not travel --
        // but `place: layer:core` means it draws wherever `core` ends up,
        // and `core` is a caster-placed sprite that travels TO the target
        // (Travels counts as per-target scope on its own, same as an
        // explicit target/target-centre place). HasPerTargetArt used to
        // check only a layer's own Travels/Place and miss this entirely.
        [Test]
        public void AStillFollowingAPerTargetLayerHasPerTargetArt()
        {
            var vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "core", render = "sprite", place = "caster-centre",
                        path = "Spells/prismatic_orb_water", travelSeconds = 0.25f,
                    },
                    new SpellLayer
                    {
                        id = "wake", render = "still", place = "layer:core",
                        path = "Spells/prismatic_orb_water_wake", until = "hold", fade = 0.1f,
                    },
                },
            };

            Assert.IsTrue(vfx.HasPerTargetArt,
                "the still follows a layer that travels to the target -- it lands on the struck target too, " +
                "even though its own place is a layer reference rather than target/target-centre");
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
