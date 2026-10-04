using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // THE HARD RULE (docs/SPELL_FEEL.md "The hard rule"): sound, number, badge
    // and accent share one frame. A sprite frame that opens on the cue used to
    // dissolve in over the last 45% of the frame before it, so Thorn Tithe's
    // sap burst showed 1-2 frames ahead of the Thorned badge.
    public class SpellCueDissolveTests
    {
        // A 12-frame sheet playing across a 0.3 s cue, authored inline: the
        // rule is about any animated sprite, and no shipped spell is one now.
        private static SpellPerformance Thorn()
        {
            var vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                hitCueSeconds = 0.3f,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "ritual", render = "sprite", place = "target-centre", at = "release",
                        path = "Spells/test_sheet", seconds = 0.72f, until = "once",
                    },
                },
            };
            return SpellPerformance.Resolve(vfx, 1, path => 12);
        }

        [Test]
        public void TheSapBurstFrameIsNotSeenBeforeTheCue()
        {
            var performance = Thorn();
            var ritual = performance.Instances.Single(i => i.Layer.id == "ritual");

            var before = SpellFrameCursor.SampleOf(ritual, 12, 0.28f);
            Assert.AreEqual(4, before.Index);
            Assert.AreEqual(0f, before.Blend, 1e-6f, "f5 would be 25% faded in here without the cue rule");

            var onCue = SpellFrameCursor.SampleOf(ritual, 12, 0.30f);
            Assert.AreEqual(5, onCue.Index, "the burst opens on the cue tick, float noise included");
        }

        [Test]
        public void FramesBeforeTheCueStillDissolveIntoEachOther()
        {
            var ritual = Thorn().Instances.Single(i => i.Layer.id == "ritual");
            var mid = SpellFrameCursor.SampleOf(ritual, 12, 0.16f);
            Assert.AreEqual(2, mid.Index);
            Assert.Greater(mid.Blend, 0f, "f2 -> f3 at 0.18 s is not the cue's frame");
        }

        [Test]
        public void AnInstanceWithNoCueDissolvesAsBefore()
        {
            var ritual = Thorn().Instances.Single(i => i.Layer.id == "ritual");
            ritual.CueSeconds = -1f;
            Assert.Greater(SpellFrameCursor.SampleOf(ritual, 12, 0.28f).Blend, 0f);
        }
    }
}
