using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The lit edge actually moves.
    //
    // Everything else about the edge kit is checked against the emitted TREE,
    // which can say the parts exist and are parented correctly but not that
    // anything happens. These two components are the whole of "a line
    // crackling and flowing with energy", and a tree test cannot tell a driven
    // sprite from a still one.
    public class TalentEdgeEnergyTests
    {
        // LIT FIRST, THEN LEFT ALONE.
        //
        // Both components live on the glow, which an unspent tree builds
        // switched OFF -- so Awake has not run and nothing is driving them.
        // The first version of this test poked Update by hand on the inactive
        // object and measured a spark whose RectTransform was still null,
        // which failed for the right reason and would have passed for the
        // wrong one had the fields happened to be set. Switching the edge on
        // is what a player does, and it is what makes the components real.
        private static GameObject LightAnEdge()
        {
            var spark = Object.FindObjectsByType<TalentEdgeSpark>(FindObjectsInactive.Include)
                .FirstOrDefault();
            Assert.IsNotNull(spark,
                "no spark was attached to any edge, so the wiring step never ran or the tree has none");

            // The glow is the spark's parent and the thing the controller
            // switches; lighting it brings the whole lit layer up.
            var glow = spark.transform.parent.gameObject;
            glow.SetActive(true);

            // ACTIVE IN THE HIERARCHY, not merely active. A component on an
            // object whose ancestor is switched off never gets Update at all,
            // and the failure looks identical to an animation that does not
            // work -- so the chain is named here rather than guessed at.
            if (!glow.activeInHierarchy)
            {
                var offenders = new System.Collections.Generic.List<string>();
                for (var t = glow.transform; t != null; t = t.parent)
                {
                    if (!t.gameObject.activeSelf) offenders.Add(t.name);
                }

                Assert.Fail("the lit layer is not active in the hierarchy, so nothing drives it. " +
                            "Switched off above it: " + string.Join(" <- ", offenders));
            }

            return glow;
        }

        [UnityTest]
        public IEnumerator TheSparkTravelsAlongItsEdge()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var glow = LightAnEdge();

            // Awake, then a real frame so Update has run at least once.
            yield return null;

            var spark = glow.GetComponentInChildren<TalentEdgeSpark>(includeInactive: true);
            var rect = (RectTransform)spark.transform;

            // The glow's own width IS the edge's length -- it is built as
            // (length, EdgeGlowWidth) -- so the travel bound comes from the
            // scene rather than from a number restated here.
            float half = ((RectTransform)glow.transform).rect.width * 0.5f;
            Assert.Greater(half, 0f, "the lit layer has no length, so there is nothing to travel");

            var seen = new System.Collections.Generic.List<float>();
            for (int i = 0; i < 40; i++)
            {
                seen.Add(rect.anchoredPosition.x);
                yield return null;
            }

            Assert.Greater(seen.Distinct().Count(), 1,
                "the spark never moved, so an invested edge is a still bright dot rather than " +
                "energy running through it. Its travel is set at BUILD time, so the usual cause is " +
                "a length that did not survive the scene save.");

            Assert.GreaterOrEqual(seen.Min(), -half - 0.01f, "the spark ran off its own edge");
            Assert.LessOrEqual(seen.Max(), half + 0.01f, "the spark ran off its own edge");
        }

        [UnityTest]
        public IEnumerator TheCoreCracklesRatherThanHoldingStill()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var glow = LightAnEdge();
            yield return null;

            var crackle = glow.GetComponentInChildren<TalentEdgeCrackle>(includeInactive: true);
            Assert.IsNotNull(crackle, "no crackle was attached to any edge core");

            var image = crackle.GetComponent<Image>();

            var alphas = new System.Collections.Generic.List<float>();
            for (int i = 0; i < 40; i++)
            {
                alphas.Add(image.color.a);
                yield return null;
            }

            Assert.Greater(alphas.Distinct().Count(), 1,
                "the core's alpha never changed, so a lit edge is a flat bright line. The core is " +
                "switched by its PARENT glow, so the usual cause is it being switched off on its " +
                "own account as well and never coming back.");

            // Never fully dark: the core is what says the connection is LIVE,
            // and a crack that reaches zero reads as a broken one.
            Assert.Greater(alphas.Min(), 0f,
                "the crackle takes the core to nothing, which reads as the connection failing");
        }

        // ---- the invest reveal ---------------------------------------------------
        //
        // WHAT ANIMATES IS THE MASK, NOT THE ORB. v1 tried scale-and-fade
        // first and recorded why it was wrong: growing the lit orb from zero
        // while its alpha climbed reads as it switching on rather than
        // arriving. The orb inside is held at full size the whole way, so
        // whatever sliver shows is already at full strength -- and that is a
        // claim only the running scene can settle.
        //
        // Reached BY NAME rather than through the component's own field:
        // PlayMode tests have no access to Core's internals by design, and the
        // emitted node name is already a stable contract because UiAudit fails
        // the build on a duplicate one.
        private static RectTransform MaskOf(TalentNodeInvestReveal reveal)
        {
            var mask = reveal.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name.EndsWith("Reveal"));

            Assert.IsNotNull(mask, "the orb has no reveal mask, so there is nothing to grow");
            return (RectTransform)mask;
        }

        [UnityTest]
        public IEnumerator TheRevealGrowsItsMaskAndLeavesTheOrbAlone()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var reveal = Object.FindObjectsByType<TalentNodeInvestReveal>(FindObjectsInactive.Include)
                .FirstOrDefault();
            Assert.IsNotNull(reveal, "no reveal was attached to any orb");

            var mask = MaskOf(reveal);
            var lit = mask.GetComponentInChildren<Image>(includeInactive: true);
            Assert.IsNotNull(lit, "the mask holds no lit medallion");

            var litRect = (RectTransform)lit.transform;
            var litSizeBefore = litRect.sizeDelta;
            float litAlphaBefore = lit.color.a;

            reveal.Play();
            yield return null;

            var sizes = new System.Collections.Generic.List<float>();
            for (int i = 0; i < 40; i++)
            {
                sizes.Add(mask.sizeDelta.x);
                yield return null;
            }

            Assert.Greater(sizes.Distinct().Count(), 1,
                "the mask never grew, so kindling an orb shows nothing happening");
            Assert.Greater(sizes.Last(), sizes.First(),
                "the mask shrank instead of opening out");

            // The point of the whole mechanism: the thing being revealed is
            // never itself animated.
            Assert.AreEqual(litSizeBefore, litRect.sizeDelta,
                "the lit orb was resized - it is supposed to sit still while the mask opens");
            Assert.AreEqual(litAlphaBefore, lit.color.a, 0.001f,
                "the lit orb was faded - every sliver revealed should already be at full strength");
        }

        // EnsureShown is the idempotent "match the current state" call the
        // repaint makes on EVERY redraw. If it could interrupt a Play, kindling
        // an orb would snap it to full size on the very next frame and the
        // reveal would never be seen -- which is exactly what the ordering in
        // Kindle is protecting.
        [UnityTest]
        public IEnumerator ARepaintDoesNotCutTheRevealShort()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var reveal = Object.FindObjectsByType<TalentNodeInvestReveal>(FindObjectsInactive.Include)
                .FirstOrDefault();
            var mask = MaskOf(reveal);

            reveal.Play();
            yield return null;

            float midway = mask.sizeDelta.x;
            Assert.IsTrue(reveal.IsPlaying, "the reveal finished within a frame");

            // What Refresh does to every already-taken orb.
            reveal.EnsureShown();

            Assert.IsTrue(reveal.IsPlaying,
                "a repaint ended the reveal, so kindling an orb snaps it to full size instead of " +
                "showing anything");
            Assert.AreEqual(midway, mask.sizeDelta.x, 0.01f,
                "a repaint jumped the reveal forward");
        }

    }
}
