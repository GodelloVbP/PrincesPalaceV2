using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The ambience movers across a disable, and against the clock they each
    // chose.
    //
    // TEN COMPONENTS, ONE SHAPE. Seven read Time.time (BeaconPulse,
    // HubBuildingLooper, KenBurnsDrift, LanternFlicker, MoteDrift, SlowDrift,
    // StarTwinkle) and three read Time.unscaledTime (EmberFlare,
    // TalentEdgeCrackle, TalentEdgeSpark). All ten capture a base in Awake and
    // write `base + f(clock)` every frame. Nothing accumulates, which is what
    // makes them safe to switch off and on -- and it is a property no test
    // stated, so nothing stops the next one being written the other way.
    //
    // POISONED WHILE DOWN, rather than compared against a twin. Two instances
    // cannot be compared here: StarTwinkle, TalentEdgeCrackle, EmberFlare and
    // (by default) SlowDrift randomise their phase in Awake and expose no
    // setter, deliberately, so a layer does not twinkle in lockstep. So the
    // test displaces the rect or the colour to an absurd value while the mover
    // is disabled and asserts the first frame back is within the curve's own
    // range of the ORIGINAL base. A mover that re-captured its base on enable,
    // or that accumulated onto the previous frame, keeps the poison; one that
    // recomputes from Awake throws it away.
    //
    // HubAmbienceTests counts these components in the real scene. This is the
    // other half: that the ones it counts behave when the screen they sit on
    // is switched off and on.
    //
    // scenarios B22, B29, B30, E7, E8 (docs/hunt/SCENARIOS.md).
    public class AmbienceLifecycleTests
    {
        private GameObject _rig;

        [TearDown]
        public void Restore()
        {
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig);
            TestGlobals.ResetAll();
        }

        // Built INACTIVE so Awake runs only after the fields are set -- every
        // one of these captures its base (and some randomise their phase) in
        // Awake, so a component added to a live object has already read the
        // wrong numbers by the time the test can write them.
        private T Mover<T>(Vector2 basePosition, Action<T> configure = null) where T : Component
        {
            if (_rig == null)
            {
                _rig = new GameObject("AmbienceRig", typeof(Canvas));
                _rig.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_rig.transform, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = basePosition;
            rect.sizeDelta = new Vector2(40f, 40f);

            var image = go.AddComponent<Image>();
            image.color = BaseColour;

            var mover = go.AddComponent<T>();
            configure?.Invoke(mover);

            go.SetActive(true);
            return mover;
        }

        // Not white and not opaque, so a component that writes a literal
        // instead of modulating its captured base is visible as one.
        private static readonly Color BaseColour = new Color(0.8f, 0.55f, 0.25f, 0.75f);

        private static readonly Vector2 Base = new Vector2(120f, -75f);
        private static readonly Vector2 Poison = new Vector2(9000f, 9000f);

        private static IEnumerator Settle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        // ---- B29 / E7: nothing remembers where it stopped -------------------------

        [UnityTest]
        public IEnumerator EveryPositionMoverRecomputesFromItsAwakeBaseRatherThanFromWhereItStopped()
        {
            var ken = Mover<KenBurnsDrift>(Base);
            var slow = Mover<SlowDrift>(Base, d => d.RandomisePhaseOnAwake = true);
            var mote = Mover<MoteDrift>(Base);

            var cases = new List<(string Name, RectTransform Rect, Vector2 Reach)>
            {
                ("KenBurnsDrift", (RectTransform)ken.transform, ken.PanAmplitude),
                ("SlowDrift", (RectTransform)slow.transform, slow.Amplitude),
                // The mote travels a whole height up and sways sideways, so its
                // reach is not symmetric the way the two drifts' are.
                ("MoteDrift", (RectTransform)mote.transform,
                    new Vector2(mote.SwayAmplitude, mote.TravelHeight)),
            };

            yield return Settle(0.2f);

            foreach (var (name, rect, _) in cases)
            {
                rect.gameObject.SetActive(false);
                rect.anchoredPosition = Poison;
            }

            yield return Settle(0.1f);

            foreach (var (name, rect, _) in cases) rect.gameObject.SetActive(true);
            yield return null;

            foreach (var (name, rect, reach) in cases)
            {
                var offset = rect.anchoredPosition - Base;

                Assert.LessOrEqual(Mathf.Abs(offset.x), Mathf.Abs(reach.x) + 1f,
                    $"{name} came back {offset.x} from its authored x, which is outside anything its own " +
                    "curve can reach - so it re-based itself on wherever it was when it stopped");
                Assert.LessOrEqual(Mathf.Abs(offset.y), Mathf.Abs(reach.y) + 1f,
                    $"{name} came back {offset.y} from its authored y, outside its own curve's reach");
            }
        }

        [UnityTest]
        public IEnumerator EveryColourMoverRecomputesItsHueFromItsAwakeBase()
        {
            // Both of these modulate ALPHA around a captured colour and never
            // touch the hue -- which is exactly why poisoning the hue is the
            // sharp test: a re-capture on enable would adopt the poison and
            // keep it forever.
            // LanternFlicker modulates a PUBLIC authored BaseColor (so an
            // Edit-Mode screenshot that never ticks still shows the baked
            // colour); StarTwinkle captures the Image's own colour in Awake.
            // Two different bases, the same claim: neither is re-read from the
            // live Image.
            var lantern = Mover<LanternFlicker>(Base, l => l.BaseColor = BaseColour);
            var star = Mover<StarTwinkle>(Base);

            var images = new List<(string Name, Image Image)>
            {
                ("LanternFlicker", lantern.GetComponent<Image>()),
                ("StarTwinkle", star.GetComponent<Image>()),
            };

            yield return Settle(0.2f);

            foreach (var (_, image) in images)
            {
                image.gameObject.SetActive(false);
                image.color = new Color(0f, 1f, 0f, 1f);
            }

            yield return Settle(0.1f);

            foreach (var (_, image) in images) image.gameObject.SetActive(true);
            yield return null;

            foreach (var (name, image) in images)
            {
                Assert.AreEqual(BaseColour.r, image.color.r, 0.001f,
                    $"{name} adopted the colour it was left holding as its new base");
                Assert.AreEqual(BaseColour.g, image.color.g, 0.001f, $"{name} re-captured its hue on enable");
                Assert.AreEqual(BaseColour.b, image.color.b, 0.001f, $"{name} re-captured its hue on enable");
                Assert.LessOrEqual(image.color.a, BaseColour.a + 0.001f,
                    $"{name} came back brighter than the alpha it was authored at");
            }
        }

        [UnityTest]
        public IEnumerator TheBeaconComesBackScaledFromItsAwakeBaseNotFromThePoisonedScale()
        {
            var beacon = Mover<BeaconPulse>(Base);
            var rect = (RectTransform)beacon.transform;

            yield return Settle(0.2f);

            beacon.gameObject.SetActive(false);
            rect.localScale = new Vector3(9f, 9f, 1f);

            yield return Settle(0.1f);

            beacon.gameObject.SetActive(true);
            yield return null;

            // A base of 1 and a curve that never leaves BeaconPulse's own
            // [MinScale, MaxScale] = [1, 1.14] (private consts, quoted here
            // rather than reached for); the poison is nine.
            Assert.LessOrEqual(rect.localScale.x, 1.14f + 0.01f,
                "the beacon came back at a scale outside its own curve, so it re-based on the " +
                "poisoned value rather than on what Awake captured");
            Assert.GreaterOrEqual(rect.localScale.x, 1f - 0.01f);
        }

        // THE LOOPER HOLDS NO STATE AT ALL between frames -- it writes
        // _image.sprite from a pure function of the absolute clock -- so its
        // disable case is discharged by showing the frame it picks depends
        // only on that clock, never on how long it has been running.
        [UnityTest]
        public IEnumerator TheBuildingLooperPicksItsFrameFromTheAbsoluteClock()
        {
            var looper = Mover<HubBuildingLooper>(Base, l =>
            {
                // The one building that actually ships more than one frame.
                l.FramesFolder = "Hub/talents";
                l.SecondsPerFrame = 0.05f;
                l.PhaseSeconds = 0f;
            });
            var image = looper.GetComponent<Image>();

            yield return null;
            var first = image.sprite;
            Assert.IsNotNull(first, "fixture: the looper never loaded a frame, so nothing below is tested");

            // Long enough to walk off frame 0 at 50ms a frame.
            yield return Settle(0.2f);
            Assert.AreNotSame(first, image.sprite, "fixture: the sequence never advanced");

            looper.gameObject.SetActive(false);
            image.sprite = null;
            yield return Settle(0.1f);

            looper.gameObject.SetActive(true);
            yield return null;

            Assert.AreSame(
                ExpectedFrame(looper),
                image.sprite,
                "the looper's frame after a disable is not the one its own clock arithmetic picks, " +
                "so it is counting elapsed time rather than reading the absolute clock");
        }

        // The frame the looper's OWN public forwarder says belongs to now.
        // Read through FrameAt rather than recomputed here, so this cannot
        // drift from the arithmetic it is checking. Probed f0..fN rather than
        // LoadAll, which this project reserves for ContentDatabase.
        private static Sprite ExpectedFrame(HubBuildingLooper looper)
        {
            int count = 0;
            while (Resources.Load<Sprite>($"Hub/talents/f{count}") != null) count++;
            Assert.Greater(count, 1, "fixture: Hub/talents no longer ships more than one frame");

            int index = HubBuildingLooper.FrameAt(
                Time.time + looper.PhaseSeconds, count, looper.SecondsPerFrame);

            return Resources.Load<Sprite>($"Hub/talents/f{index}");
        }

        // ---- B22: the flare restores on enable, before any frame ticks ---------------

        // The Reckoning's summary phase starts inactive and is switched on
        // mid-fight. EmberFlare is the one mover here with an explicit OnEnable
        // rather than a pure Update, precisely so the first VISIBLE frame is
        // the resting pose rather than wherever the last run left it.
        [UnityTest]
        public IEnumerator TheFlareIsBackAtRestTheInstantItIsEnabled()
        {
            var flare = Mover<EmberFlare>(Base);
            var rect = (RectTransform)flare.transform;
            var image = flare.GetComponent<Image>();

            yield return Settle(0.2f);

            flare.gameObject.SetActive(false);
            rect.localScale = new Vector3(9f, 9f, 1f);
            rect.anchoredPosition = Poison;
            image.color = Color.green;

            yield return Settle(0.1f);

            // NO FRAME BETWEEN. SetActive runs OnEnable synchronously, and the
            // claim is that the restore happens there rather than being left
            // for the first Update -- which is a frame later and one frame of
            // a nine-times-size flare on screen.
            flare.gameObject.SetActive(true);

            Assert.AreEqual(1f, rect.localScale.x, 0.001f,
                "the flare came back at the scale it was left at and waits for a frame to fix itself");
            Assert.AreEqual(Base.x, rect.anchoredPosition.x, 0.001f, "the flare came back displaced");
            Assert.AreEqual(Base.y, rect.anchoredPosition.y, 0.001f, "the flare came back displaced");
            Assert.AreEqual(BaseColour.a, image.color.a, 0.001f, "the flare came back at the wrong alpha");
            Assert.AreEqual(BaseColour.g, image.color.g, 0.001f, "the flare came back the wrong colour");
        }

        // ---- B30 / E8: which clock each one chose, and that it is deliberate ------------

        // THE SCALED ONES FREEZE WHEN THE MENU PAUSES THE GAME, AND THE
        // UNSCALED ONES DO NOT. That split is a decision, not an accident --
        // TalentEdgeCrackle's own header says so in as many words ("decoration
        // that stops when the clock does is a bug waiting for the first thing
        // that pauses it") -- so it is asserted here rather than left for
        // somebody to "tidy" one side into the other.
        [UnityTest]
        public IEnumerator PausingTheGameFreezesTheScaledMoversAndNotTheUnscaledOnes()
        {
            var scaled = Mover<SlowDrift>(Base);
            var crackle = Mover<TalentEdgeCrackle>(Base);
            var spark = Mover<TalentEdgeSpark>(Base, s => s.SetLength(400f));
            var flare = Mover<EmberFlare>(Base);

            var scaledRect = (RectTransform)scaled.transform;
            var sparkRect = (RectTransform)spark.transform;
            var crackleImage = crackle.GetComponent<Image>();
            var flareRect = (RectTransform)flare.transform;

            yield return Settle(0.1f);

            Time.timeScale = 0f;
            yield return null;

            var scaledAt = scaledRect.anchoredPosition;
            var sparkAt = sparkRect.anchoredPosition;
            float crackleAt = crackleImage.color.a;
            float flareAt = flareRect.localScale.x;

            yield return Settle(0.25f);

            Assert.AreEqual(scaledAt.x, scaledRect.anchoredPosition.x, 0.0001f,
                "a Time.time mover kept drifting while the game was paused, so it is not reading " +
                "the scaled clock the hub's ambience is written against");

            Assert.AreNotEqual(sparkAt.x, sparkRect.anchoredPosition.x,
                "the edge spark stopped while the game was paused - it is on the unscaled clock " +
                "deliberately, because the talent screen is an overlay over a pause");
            Assert.AreNotEqual(crackleAt, crackleImage.color.a,
                "the edge crackle stopped while the game was paused");
            Assert.AreNotEqual(flareAt, flareRect.localScale.x,
                "the ember flare stopped while the game was paused, and the Reckoning it sits on " +
                "is an overlay over a fight that may well be paused");
        }

        // B30's own half: disabled and re-enabled WHILE PAUSED, they come back
        // still running on the unscaled clock rather than waiting for a clock
        // that is stopped.
        [UnityTest]
        public IEnumerator TheUnscaledOverlayMoversSurviveADisableWhileTheGameIsPaused()
        {
            var crackle = Mover<TalentEdgeCrackle>(Base);
            var spark = Mover<TalentEdgeSpark>(Base, s => s.SetLength(400f));
            var crackleImage = crackle.GetComponent<Image>();
            var sparkRect = (RectTransform)spark.transform;

            Time.timeScale = 0f;
            yield return Settle(0.1f);

            crackle.gameObject.SetActive(false);
            spark.gameObject.SetActive(false);
            crackleImage.color = Color.green;
            sparkRect.anchoredPosition = Poison;

            yield return Settle(0.1f);

            crackle.gameObject.SetActive(true);
            spark.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(BaseColour.r, crackleImage.color.r, 0.001f,
                "the crackle adopted the colour it was left holding while the game was paused");

            // The spark writes an ABSOLUTE local x along its edge
            // (Lerp(-half, half, t)) rather than an offset from a captured
            // base, so "back on its edge" is |x| <= halfLength.
            float x = sparkRect.anchoredPosition.x;
            Assert.LessOrEqual(Mathf.Abs(x), 201f,
                "the spark came back outside its own edge, so it re-based on where it was poisoned");

            var wasAt = sparkRect.anchoredPosition;
            float alphaWas = crackleImage.color.a;
            yield return Settle(0.25f);

            Assert.AreNotEqual(wasAt.x, sparkRect.anchoredPosition.x,
                "the spark came back frozen - it should still be running on the unscaled clock");
            Assert.AreNotEqual(alphaWas, crackleImage.color.a, "the crackle came back frozen");
        }
    }
}
