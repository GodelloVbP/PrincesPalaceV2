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
    // The four things every button in this project carries, under interruption
    // and teardown.
    //
    // ButtonPressAnimator, HoverBox, ThemedButtonState and ColumnOpenAnimator
    // are attached from ONE choke point apiece, to every button and every
    // column in the game. That is what makes a stuck state on one of them
    // worth a test: the failure is not "this button looks odd", it is "some
    // buttons in the game sometimes look odd", which is the shape of bug
    // nobody can reproduce.
    //
    // A NO-SCENE RIG for the three pure animators, the shape
    // FightBeatPlayerFixtureTests and StageAnimationTests already use: these
    // components read nothing but their own transform and their own clock, so
    // a scene would add a minute of load time and nothing else. The themed
    // button is driven through a real screen instead, because its glow colour
    // comes from what Ui.ApplyTheme baked at build time and a synthetic Image
    // would be white by construction -- the one thing B4 is about.
    //
    // scenarios A19, A20, B4, B19, B20, B21, B23 (docs/hunt/SCENARIOS.md).
    public class ButtonStateLifecycleTests
    {
        private GameObject _rig;

        [TearDown]
        public void Restore()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
            TestGlobals.ResetAll();
        }

        // WALL CLOCK, NOT FRAMES. Every animation in this file is measured in
        // Time.unscaledDeltaTime, and a batchmode frame here is about two
        // milliseconds -- forty of them do not cover a 140ms slide, which is
        // exactly how the first version of this file failed. Bounded, so an
        // animation that never lands fails on its own assertion rather than
        // hanging the run.
        private static IEnumerator Settle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        // A bare rect under a canvas, which is all any of these three need.
        private GameObject Rig(string name, params System.Type[] components)
        {
            if (_rig == null)
            {
                _rig = new GameObject("ButtonStateRig", typeof(Canvas));
                _rig.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_rig.transform, worldPositionStays: false);
            foreach (var type in components) go.AddComponent(type);
            return go;
        }

        // ---- B21: a press animator disabled mid-press --------------------------------

        // SetActive(false) skips Update entirely, so a button hidden mid-press
        // cannot ease itself back down -- OnDisable has to reset eagerly, which
        // is what its own comment says it is for. A row swapping out under the
        // cursor (Continue replacing the choice row) is the live case.
        //
        // Owner's call, 2026-09-23: no more press-scale pop either -- this
        // used to assert localScale.x shrank and snapped back; the plate now
        // dims instead (a colour lerp, not a transform change), so this
        // asserts the same "stuck mid-feedback" failure shape against Image.
        // color.
        [UnityTest]
        public IEnumerator APressAnimatorDisabledMidPressComesBackAtRest()
        {
            // Image BEFORE ButtonPressAnimator -- AddComponent runs Awake
            // synchronously, and ButtonPressAnimator.Awake captures its base
            // colour from GetComponent<Image>() once, so the Image has to
            // already be attached when that fires. Image's own default
            // colour is white, which is what the assertions below compare
            // against.
            var go = Rig("Pressed", typeof(Image), typeof(ButtonPressAnimator));
            var image = go.GetComponent<Image>();
            var animator = go.GetComponent<ButtonPressAnimator>();

            animator.OnPointerDown(null);
            yield return Settle(0.30f);

            Assert.Less(image.color.r, Color.white.r - 0.001f,
                "fixture: the press never dimmed the plate, so nothing below is tested");

            go.SetActive(false);

            Assert.AreEqual(Color.white, image.color,
                "a button hidden mid-press stayed dimmed, so it pops back into view already pressed");

            // And it does not come back still believing it is held: one frame
            // after re-enabling, with no pointer anywhere near it, it is at
            // rest rather than easing toward the pressed tint.
            go.SetActive(true);
            yield return null;
            yield return null;

            Assert.AreEqual(Color.white, image.color,
                "the re-enabled button is animating toward a press nobody is making");
        }

        // ---- B20: the hover/focus rim, same shape of bug -----------------------------
        //
        // SubtleHoverScale's own version of this test animated a localScale
        // and is gone with it (owner's 2026-09-23 hover-pop removal); HoverBox
        // has no Update() to leave mid-animation, but it still owns two
        // sticky booleans (_isHovering/_isSelected) that OnDisable has to
        // clear or a row hidden mid-hover comes back showing a rim for a
        // pointer that is no longer there.
        [UnityTest]
        public IEnumerator AHoverBoxDisabledMidHoverComesBackWithTheRimHidden()
        {
            var go = Rig("Hovered", typeof(HoverBox));
            var hover = go.GetComponent<HoverBox>();
            var rim = new GameObject("Rim", typeof(RectTransform));
            rim.transform.SetParent(go.transform, worldPositionStays: false);
            rim.SetActive(false);
            hover.Rim = rim;

            hover.OnPointerEnter(null);

            Assert.IsTrue(rim.activeSelf, "fixture: the hover never showed the rim");

            go.SetActive(false);
            go.SetActive(true);
            yield return null;

            Assert.IsFalse(rim.activeSelf,
                "the re-enabled row is still showing a rim for a hover the pointer already left");
        }

        // ---- A20 / B23: the column's open animation ----------------------------------------

        // THE PAIR. Show.SetShown's guard is the only thing standing between
        // "the submenu is refreshed sixty times a second" and "the submenu
        // replays its slide sixty times a second" -- Show.cs's own header names
        // ColumnOpenAnimator as the component that treats "just activated" as
        // "just opened". Nothing pinned either half.
        [UnityTest]
        public IEnumerator AGuardedReShowDoesNotReplayTheColumnsSlide()
        {
            var go = Rig("Column", typeof(CanvasGroup), typeof(ColumnOpenAnimator));
            var rect = (RectTransform)go.transform;
            var group = go.GetComponent<CanvasGroup>();

            // Settled: the slide has played and landed.
            yield return Settle(0.30f);
            Assert.AreEqual(1f, group.alpha, 0.001f, "fixture: the first open never finished");
            var rest = rect.anchoredPosition;

            // The refresh path: SetShown(true) on something already shown.
            go.SetShown(true);
            yield return null;

            Assert.AreEqual(1f, group.alpha, 0.001f,
                "a guarded re-show replayed the column's fade, so every repaint flickers it");
            Assert.AreEqual(rest.x, rect.anchoredPosition.x, 0.001f,
                "a guarded re-show slid the column back to its start");
        }

        [UnityTest]
        public IEnumerator ARealReActivationReplaysTheSlideFromTheLeftExactlyOnce()
        {
            var go = Rig("Column", typeof(CanvasGroup), typeof(ColumnOpenAnimator));
            var rect = (RectTransform)go.transform;
            var group = go.GetComponent<CanvasGroup>();

            yield return Settle(0.30f);
            var rest = rect.anchoredPosition;

            go.SetShown(false);
            go.SetShown(true);

            // OnEnable has run and Update has not: the start of the slide.
            Assert.AreEqual(0f, group.alpha, 0.001f,
                "a genuine re-open did not restart the fade, so the column appears without opening");
            Assert.Less(rect.anchoredPosition.x, rest.x,
                "a genuine re-open did not slide in from the left");

            yield return Settle(0.30f);

            // AND IT LANDS BACK ON THE SAME REST POSITION. _restPosition is
            // captured once in Awake for exactly this reason: re-reading it in
            // OnEnable would capture the SLID position and walk the column ten
            // pixels left on every open.
            Assert.AreEqual(1f, group.alpha, 0.001f, "the replayed slide never finished");
            Assert.AreEqual(rest.x, rect.anchoredPosition.x, 0.001f,
                "the second open landed somewhere other than the first, so the column walks left " +
                "every time it is shown");
        }

        // ---- A19 / B4: the themed button ---------------------------------------------------

        // Interrupting the fade is the point. UpdateGlow/UpdatePlate stop
        // whatever is running before starting the next one; if they did not,
        // two coroutines would be writing one Image's colour on alternating
        // frames and the button would settle wherever the survivor happened to
        // stop.
        [UnityTest]
        public IEnumerator HoverLeavingMidFadeEndsAtTheRestingLookNotTheInterruptedOne()
        {
            yield return LoadTheHub();

            var themed = AThemedButton();
            var glow = themed.Glow;

            // Warmed up before the timing-sensitive part: the first frames
            // after a scene load are the long ones, and this fade is 120ms.
            yield return Settle(0.05f);

            themed.OnPointerEnter(null);
            yield return null;

            // Mid-fade: on its way up and not there yet.
            Assert.Greater(glow.color.a, 0f, "fixture: the hover never started a fade");
            Assert.Less(glow.color.a, ThemedButtonState.FocusGlowAlpha,
                "fixture: the fade finished in one frame, so there is nothing to interrupt");

            themed.OnPointerExit(null);
            yield return Settle(0.30f);

            float resting = themed.CurrentMenuState == ThemedMenuState.Idle ? 0f : 1f;
            Assert.AreEqual(resting, glow.color.a, 0.01f,
                "the button settled part-way through the fade it was interrupted in, so an " +
                "un-hovered button is left lit");
        }

        // DISABLED WHILE HOVERED. ApplyImmediate is the eager reset, and the
        // flags behind it have to go too -- OnEnable's Refresh reads them, so a
        // stale _isHovering would paint the button lit the moment it comes back.
        //
        // AND THE GLOW COLOUR IS CAPTURED EXACTLY ONCE, which is the opposite
        // of what SCENARIOS.md's B4 row guesses. CaptureThemeGlowColor is
        // idempotent by construction (`if (_themeGlowCaptured ...) return`) and
        // says so: "RGB never changes once captured (only .a ever does)".
        // Asserted here so nobody reads OnEnable's call site as a re-capture
        // and "fixes" the flag away -- the game has no runtime theme swap, and
        // re-reading Glow.color on a later enable would capture whatever alpha
        // and hue a fade had left on it.
        [UnityTest]
        public IEnumerator AThemedButtonDisabledWhileHoveredComesBackUnlitAndKeepsItsCapturedHue()
        {
            yield return LoadTheHub();

            var themed = AThemedButton();
            var glow = themed.Glow;

            // Pinned to Idle first, so the hue read below is the one
            // CaptureThemeGlowColor took at build time rather than Primary's
            // gold on a button some controller had already selected.
            themed.SetMenuState(ThemedMenuState.Idle);
            var hueBefore = new Color(glow.color.r, glow.color.g, glow.color.b, 1f);

            themed.OnPointerEnter(null);
            yield return Settle(0.30f);
            Assert.Greater(glow.color.a, 0.1f, "fixture: the hover never lit the glow");

            themed.gameObject.SetActive(false);

            Assert.AreEqual(0f, glow.color.a, 0.001f,
                "a themed button hidden mid-hover kept its glow, so it reappears already lit");

            // Something writes the glow while the button is down -- a fade that
            // was still running, a paint pass, anything. Re-enabling must not
            // adopt that as the button's theme colour.
            glow.color = new Color(0.1f, 0.9f, 0.3f, 0.7f);

            themed.gameObject.SetActive(true);
            yield return Settle(0.30f);

            Assert.AreEqual(0f, glow.color.a, 0.01f,
                "the button came back lit, so OnDisable left _isHovering set and OnEnable's " +
                "Refresh believed the pointer was still on it");

            // The captured hue is the one from build time, not the one that was
            // sitting on the Image at the moment of re-enable.
            themed.SetMenuState(ThemedMenuState.Open);
            yield return null;

            Assert.AreEqual(hueBefore.r, glow.color.r, 0.01f, "the theme glow hue was re-captured on enable");
            Assert.AreEqual(hueBefore.g, glow.color.g, 0.01f, "the theme glow hue was re-captured on enable");
            Assert.AreEqual(hueBefore.b, glow.color.b, 0.01f, "the theme glow hue was re-captured on enable");
        }

        // ---- B19: a hover reported from teardown ---------------------------------------------

        // HoverIndex fires Changed from its OWN OnDisable, deliberately -- a
        // tooltip anchored to something that vanishes mid-hover would otherwise
        // stay open forever. The cost is that every subscriber is called during
        // teardown, when its siblings may already be destroyed, and that cost
        // has been paid for real once already (RewardTrackController.Motion's
        // CanAnimate, and AUDIT #106's shape).
        //
        // A PlayMode test fails on an unexpected LogError, so the reproduction
        // IS the assertion.
        [UnityTest]
        public IEnumerator AHoveredRowSurvivingIntoASceneUnloadReportsItsExitQuietly()
        {
            yield return LoadTheHub();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            yield return null;
            yield return null;

            var hover = menu.GetComponentsInChildren<HoverIndex>(includeInactive: true).FirstOrDefault();
            Assert.IsNotNull(hover,
                "the system menu's tabs carry no HoverIndex, so this pin is vacuous");

            hover.OnPointerEnter(null);
            yield return null;

            // Out from under it, with the hover still live.
            yield return SceneManager.LoadSceneAsync(Navigation.MainMenu, LoadSceneMode.Single);
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        // ---- fixture -------------------------------------------------------------------------

        private static IEnumerator LoadTheHub()
        {
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // Any real themed button with its glow wired. Found rather than named:
        // which buttons a screen themes is a design decision that moves, and a
        // test that named one would fail as a rename rather than as a defect.
        private static ThemedButtonState AThemedButton()
        {
            var themed = Object.FindObjectsByType<ThemedButtonState>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.Glow != null && t.gameObject.activeInHierarchy);

            Assert.IsNotNull(themed,
                "no active themed button with a wired Glow anywhere in the hub - either the emitter " +
                "stopped wiring them or this screen stopped having any");
            return themed;
        }
    }
}
