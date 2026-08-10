using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // That the Divine Principality actually moves.
    //
    // The counts are the point. Every one of these components is attached by a
    // loop in DressHub, and a loop that silently runs zero times leaves a
    // perfectly still screen with no error anywhere -- which is exactly what the
    // hub was before this.
    public class HubAmbienceTests
    {
        private GameObject _hub;

        private IEnumerator LoadHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>().gameObject;
            Assert.IsNotNull(_hub);
        }

        private T[] All<T>() where T : Component =>
            _hub.GetComponentsInChildren<T>(includeInactive: true);

        [UnityTest]
        public IEnumerator TheSkyTwinkles()
        {
            yield return LoadHub();

            Assert.AreEqual(HubAmbience.Stars.Length, All<StarTwinkle>().Length);
        }

        [UnityTest]
        public IEnumerator OnlyTheBraziersBurn()
        {
            // Fire flickers; enchantment pulses. Attaching LanternFlicker to
            // everything that glows would make the whole hub read as being on
            // fire, which is not what a floating holy city is doing.
            yield return LoadHub();

            Assert.AreEqual(HubAmbience.GateBraziers.Length, All<LanternFlicker>().Length);
        }

        [UnityTest]
        public IEnumerator EveryEnchantedLightPulsesOnItsOwnBeat()
        {
            // Four buildings plus the keystone. Sharing one phase would make the
            // place throb on a single beat, which reads as one machine driving
            // all of it rather than as five separate lights.
            yield return LoadHub();

            var pulses = All<BeaconPulse>();
            Assert.AreEqual(HubAmbience.BuildingGlows.Count + 1, pulses.Length);

            var phases = pulses.Select(p => Mathf.Round(p.PhaseSeconds * 100f)).Distinct().ToList();
            Assert.Greater(phases.Count, 1, "every light shares a phase");
        }

        [UnityTest]
        public IEnumerator EmbersRiseOutOfTheVoid()
        {
            yield return LoadHub();

            var embers = All<MoteDrift>();
            Assert.AreEqual(HubAmbience.EmberOrigins.Length, embers.Length);
            Assert.IsTrue(embers.All(e => e.TravelHeight > 0f), "an ember that travels nowhere is a dot");
        }

        [UnityTest]
        public IEnumerator TheMistDrifts()
        {
            yield return LoadHub();

            var drifts = All<SlowDrift>();
            Assert.AreEqual(HubAmbience.VoidMist.Length, drifts.Length);
            Assert.IsTrue(drifts.All(d => d.PeriodSeconds > 0f),
                "a zero period degrades to no drift at all -- the mist would just sit there");
        }

        [UnityTest]
        public IEnumerator TheWholePlaceBreathes()
        {
            // One KenBurnsDrift, on the world wrapper. Per-element drift would
            // break the parallax the depth staging exists to create.
            yield return LoadHub();

            var breath = All<KenBurnsDrift>();
            Assert.AreEqual(1, breath.Length);
            Assert.AreEqual("HubWorld", breath[0].name);
            Assert.GreaterOrEqual(breath[0].MaxScale, 1f,
                "under 1 the background's edges pull inside the canvas and show bare camera colour");
        }

        [UnityTest]
        public IEnumerator NoGlowEatsAClickMeantForABuilding()
        {
            // The ambience layer sits directly over four buildings and a gate.
            // AsDecor is what clears raycastTarget across the whole subtree, and
            // a single missed one is an unpressable building with no visible
            // cause.
            yield return LoadHub();

            var ambience = _hub.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "HubAmbience");
            Assert.IsNotNull(ambience);

            var greedy = ambience.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                .Where(g => g.raycastTarget)
                .Select(g => g.name)
                .ToList();

            CollectionAssert.IsEmpty(greedy, "these would swallow clicks: " + string.Join(", ", greedy));
        }
    }
}
