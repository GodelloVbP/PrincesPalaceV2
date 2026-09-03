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

        // HALVED, LITERALLY -- 17/29 to 8.5/14.5 (session brief, 2026-09-03).
        // Pinned rather than inferred from measured wave crossings, which
        // would recompute the thing under test against its own random phase
        // offset and could pass at either speed.
        [Test]
        public void TheCrackleFrequenciesAreHalvedFromTheirOriginalValues()
        {
            Assert.AreEqual(8.5f, TalentEdgeCrackle.FrequencyA);
            Assert.AreEqual(14.5f, TalentEdgeCrackle.FrequencyB);
        }
    }
}
