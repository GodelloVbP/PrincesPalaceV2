using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // HUNT 2026-09-11, scenario B29 for the ONE ambience mover in the fight
    // group (docs/hunt/SCENARIOS.md): switched off mid-pulse and back on.
    //
    // The row covers seven Time.time movers; the other six (HubBuildingLooper,
    // KenBurnsDrift, LanternFlicker, MoteDrift, SlowDrift, StarTwinkle) live in
    // the hub group and are not touched here. The shared claim the row makes is
    // "each recomputes from its Awake-captured base rather than from wherever
    // it stopped", and the failure mode it is written against is a base
    // captured in OnEnable instead: the mover would adopt whatever swell it was
    // stopped at as its new rest size, and every cycle would compound. That is
    // not hypothetical in this codebase -- StageActorAnimator shipped exactly
    // it (see ReHomingAFigureMidBreathDoesNotFoldTheBreathIntoItsSize), and it
    // is invisible for about a minute and then obvious.
    //
    // NO SCENE. This mover needs an Image and a rect and nothing else, so a
    // three-line object is the whole rig -- and a rig is what makes the base
    // scale a number this test chose rather than whatever a screen authored.
    public class BeaconPulseLifecycleTests
    {
        private GameObject _host;

        [TearDown]
        public void Cleanup()
        {
            if (_host != null) Object.Destroy(_host);
        }

        // The beacon's own authored ceiling (BeaconPulse.MaxScale): the pulse
        // is a swell from the base to 1.14x it, never below. Restated rather
        // than read, because the constant is private and because a test that
        // recomputes the production number cannot fail when it changes.
        private const float MaxSwell = 1.14f;
        private const float BaseScale = 2f;

        private BeaconPulse ABeacon()
        {
            _host = new GameObject("Beacon", typeof(RectTransform), typeof(Image));

            // SET BEFORE THE COMPONENT EXISTS, so Awake captures this rather
            // than the identity scale a fresh GameObject carries. This is the
            // whole subject of the test: which scale the mover thinks is rest.
            _host.transform.localScale = Vector3.one * BaseScale;

            var beacon = _host.AddComponent<BeaconPulse>();
            beacon.BaseColor = Color.white;
            beacon.PeriodSeconds = 0.2f;
            return beacon;
        }

        [UnityTest]
        public IEnumerator ABeaconSwitchedOffMidPulseComesBackOnTheSizeItStartedFrom()
        {
            var beacon = ABeacon();
            var rect = (RectTransform)_host.transform;
            var image = _host.GetComponent<Image>();

            // WATCHED BY THE CLOCK, NOT BY FRAME COUNT. This mover reads
            // Time.time, and a batchmode test host runs frames far faster
            // than a screen does -- twenty frames here was five milliseconds
            // of a 200ms cycle, which is a mover that looks motionless. A
            // fixture check that it moves at all: a mover that never moved
            // would pass every compounding assertion below.
            float lowest = float.MaxValue;
            float highest = 0f;
            float until = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                lowest = Mathf.Min(lowest, rect.localScale.y);
                highest = Mathf.Max(highest, rect.localScale.y);
            }

            Assert.Greater(highest - lowest, 0.01f, "fixture: the beacon never pulsed at all");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Stopped wherever it happens to be, which is the point: a
                // base captured on the way back in would adopt this swell.
                _host.SetActive(false);
                yield return null;
                yield return null;

                _host.SetActive(true);

                until = Time.realtimeSinceStartup + 0.3f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;

                    Assert.That(rect.localScale.y, Is.InRange(BaseScale - 0.001f,
                            BaseScale * MaxSwell + 0.001f),
                        $"after {cycle + 1} off/on cycles the beacon is at {rect.localScale.y:F4}, " +
                        $"outside the swell its base of {BaseScale} allows -- it re-captured its rest " +
                        "size from wherever the last cycle stopped, and every cycle compounds");

                    Assert.That(image.color.a, Is.InRange(beacon.MinAlpha - 0.001f,
                            beacon.MaxAlpha + 0.001f),
                        "the beacon's alpha left the range its own floor and ceiling allow");
                }
            }
        }
    }
}
