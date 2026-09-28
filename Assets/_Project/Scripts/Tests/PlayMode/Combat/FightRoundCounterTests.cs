using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // An event fight's HUD on the real Fight scene
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4, M6): the top-centre counter
    // shows only with a round limit and steps as playback reaches each round,
    // the overlay steps its scale per round, and the ambience bed starts with
    // the fight and stops when it is left. FightRoundPresentationTests pins the
    // arithmetic on the fast host; this is the wiring.
    public class FightRoundCounterTests
    {
        // A real clip under Resources, so "the bed is playing" is a fact about
        // an AudioSource and not about a path string.
        private const string RealClip = "Audio/Sfx/button click";

        private FightController _fight;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            FightSceneFixture.QuietForReuse(_fight);
            SoundController.StopAmbience();
            Navigation.Reset();
            SharedScene.AfterTest();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        private static FightRoundPresentation Bell(string ambience = "", string overlayKey = "") =>
            new FightRoundPresentation(roundLimit: 10, label: "Toll", roundSfxPath: "Audio/Sfx/does_not_exist",
                overlayKey: overlayKey, overlayFromScale: 1f, overlayToScale: 1.9f, ambiencePath: ambience);

        // Speed 10 on both sides: each acts once a round, so one attack and
        // the reply carry the fight from round 1 into round 2.
        private FightSession _session;

        private IEnumerator Bind(FightRoundPresentation presentation, int roundLimit)
        {
            yield return SharedScene.EnsureFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            BindOnly(presentation, roundLimit);
            yield return null;
        }

        private void BindOnly(FightRoundPresentation presentation, int roundLimit, int heroSpeed = 10)
        {
            var hero = new CombatantState("Shawn", true, 5000, 30, 10, heroSpeed);
            var foe = new CombatantState("Bell", false, 50000, 10, 5, 10);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("bell", "Bell", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9)) { RoundLimit = roundLimit };
            session.Begin();

            _session = session;
            _fight.Bind(session, EncounterClass.Normal, null, presentation);
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private TMP_Text Counter => Named("RoundCounter").GetComponent<TMP_Text>();

        private Image Overlay => Named("RoundOverlay").GetComponent<Image>();

        private IEnumerator AttackAndPlayOut()
        {
            // WAIT FOR INPUT TO OPEN FIRST. A round-limited fight always
            // queues a RoundStart beat for round 1 (FightSession.Rounds.cs,
            // RecordRoundStart), and FightController.Bind now drains and
            // plays whatever Begin() queued -- the toll's own sound/overlay
            // step included -- before input opens (Core/FightController.cs).
            // A click while that is still playing is silently swallowed
            // (CanAct requires !IsBusy), which used to never matter here
            // because Bind never played anything of its own.
            float openDeadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < openDeadline) yield return null;
            Assert.IsFalse(_fight.IsBusy, "fixture: Bind's own opening playback never finished, so input never opened");

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            float deadline = Time.realtimeSinceStartup + 10f;
            yield return null;
            while (Time.realtimeSinceStartup < deadline && player.IsPlaying) yield return null;
            Assert.IsFalse(player.IsPlaying, "fixture: the round never finished playing");
        }

        // ---- the counter ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARoomFightHidesTheCounterAndTheOverlay()
        {
            yield return Bind(FightRoundPresentation.None, 0);

            Assert.IsFalse(Named("RoundCounter").activeSelf, "a room fight shows no round counter");
            Assert.IsFalse(Named("RoundCounterPlate").activeSelf, "a room fight shows no counter plate");
            Assert.IsFalse(Named("RoundOverlay").activeSelf, "a room fight shows no overlay");
            Assert.IsFalse(SoundController.AmbiencePlaying, "a room fight plays no bed");
        }

        [UnityTest]
        public IEnumerator ALimitedFightShowsTollOneAtOnce_ThenTollTwoWhenPlaybackReachesRoundTwo()
        {
            yield return Bind(Bell(), 10);

            Assert.IsTrue(Named("RoundCounter").activeSelf, "a fight with a round limit shows its counter");
            Assert.IsTrue(Named("RoundCounterPlate").activeSelf, "the counter sits on its dark plate");
            Assert.AreEqual("Toll 1", Counter.text);

            yield return AttackAndPlayOut();

            Assert.AreEqual(2, _session.Round, "fixture: one exchange carries the fight into round 2");
            Assert.AreEqual("Toll 2", Counter.text, "the counter stepped when round 2's beat played");
        }

        // BEGIN CROSSED TWO ROUNDS before Shawn's first turn (speed 2 against
        // the Bell's 10): the counter opens on round 1, not the session's 3,
        // and the queued opening beats step it -- each toll once, in order.
        [UnityTest]
        public IEnumerator WhenBeginCrossesRounds_TheCounterOpensOnOne_AndTheOpeningBeatsStepItThroughEachToll()
        {
            yield return SharedScene.EnsureFight();
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            BindOnly(Bell(), 10, heroSpeed: 2);
            Assert.AreEqual(3, _session.Round, "fixture: Begin carried the fight into round 3");
            Assert.AreEqual("Toll 1", Counter.text, "the counter jumped to the session's round at Bind");

            // Every toll the rest of the opening playback presents, in order.
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            var field = typeof(FightBeatPlayer).GetField("PresentRound", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "FightBeatPlayer.PresentRound was renamed or removed");
            var present = (System.Action<int>)field.GetValue(player);
            var shown = new List<string> { Counter.text };
            field.SetValue(player, (System.Action<int>)(round =>
            {
                present(round);
                if (shown[shown.Count - 1] != Counter.text) shown.Add(Counter.text);
            }));

            float deadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_fight.IsBusy, "fixture: the opening playback never finished");

            CollectionAssert.AreEqual(new[] { "Toll 1", "Toll 2", "Toll 3" }, shown);
        }

        // A second bind on the same scene -- the next fight -- puts a room
        // fight's None back: the counter from the event fight must not linger.
        [UnityTest]
        public IEnumerator ARoomFightAfterAnEventFightHidesTheCounterAgain()
        {
            yield return Bind(Bell(), 10);
            Assert.IsTrue(Named("RoundCounter").activeSelf);

            yield return Bind(null, 0);
            Assert.IsFalse(Named("RoundCounter").activeSelf);
            Assert.IsFalse(Named("RoundCounterPlate").activeSelf, "the plate went with the counter");
        }

        // ---- the overlay ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheOverlayShowsAtFromScale_AndStepsOneStepPerRound()
        {
            const string key = "Assets/_Project/Art/Events/bell_fixture/flock.png";
            var texture = new Texture2D(16, 9);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 9), new Vector2(0.5f, 0.5f));

            yield return SharedScene.EnsureFight();
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            // Nothing in content authors an overlay yet, so the fixture bakes
            // one the way ScreenRegistry would: key -> sprite.
            var field = typeof(FightController).GetField("roundOverlayArt", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "FightController.roundOverlayArt was renamed or removed");
            var baked = (IconEntry[])field.GetValue(_fight);
            field.SetValue(_fight, new[] { new IconEntry(key, sprite) });

            try
            {
                BindOnly(Bell(overlayKey: key), 10);
                yield return null;

                Assert.IsTrue(Named("RoundOverlay").activeSelf, "a baked overlay shows");
                Assert.AreSame(sprite, Overlay.sprite);
                Assert.AreEqual(1.0f, Overlay.rectTransform.localScale.x, 1e-4f, "round 1 is fromScale");

                yield return AttackAndPlayOut();
                Assert.AreEqual(1.1f, Overlay.rectTransform.localScale.x, 1e-4f, "round 2 is one ninth of the way to 1.9");
            }
            finally
            {
                field.SetValue(_fight, baked);
                Overlay.rectTransform.localScale = Vector3.one;
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator AnOverlayWhoseFileIsMissingStaysHidden()
        {
            yield return Bind(Bell(overlayKey: "Assets/_Project/Art/Events/bell_fixture/not_there.png"), 10);

            Assert.IsFalse(Named("RoundOverlay").activeSelf, "no baked sprite hides the layer rather than a white quad");
            Assert.AreEqual("Toll 1", Counter.text, "the counter does not depend on the overlay");
        }

        // THE REAL BELL, AS CONTENT AUTHORS IT, WITH ITS DELIVERED ART AND
        // SOUND (M9a). The fog clearing replaces the class backdrop, the flock
        // overlay shows at its authored fromScale, the wind bed loops from the
        // start, the toll clip resolves, and nothing logs.
        [UnityTest]
        public IEnumerator TheRealBell_ShowsItsBackdropAndFlock_LoopsTheWind_AndLogsNothing()
        {
            var bell = ContentDatabase.Events.Select(e => e.Data).Single(e => e.Id == "bell_in_the_fog");
            var fight = bell.FightById("bellwether");
            var presentation = EncounterRequest.ForEventFight(PrincesPalace.Domain.Dungeon.RoomType.Event,
                bell.Id, fight).Presentation;

            yield return SharedScene.EnsureFight();
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            var backdrop = typeof(FightController).GetField("backgroundImage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(backdrop, "FightController.backgroundImage was renamed or removed");
            var classSprite = ((Image)backdrop.GetValue(_fight)).sprite;

            BindOnly(presentation, fight.SurviveRounds);
            yield return null;
            float openDeadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < openDeadline) yield return null;

            Assert.AreEqual("Toll 1", Counter.text);
            Assert.IsTrue(Named("RoundCounterPlate").activeSelf);

            Assert.IsTrue(Named("RoundOverlay").activeSelf, "flock.png is baked: the overlay shows");
            Assert.IsNotNull(Overlay.sprite);
            Assert.AreEqual("flock", Overlay.sprite.texture.name);
            Assert.AreEqual(0.6f, Overlay.rectTransform.localScale.x, 1e-4f, "round 1 is the authored fromScale");

            var shown = ((Image)backdrop.GetValue(_fight)).sprite;
            Assert.AreNotSame(classSprite, shown, "fog_clearing.png replaces the class backdrop");
            Assert.AreEqual("fog_clearing", shown.texture.name);

            Assert.IsTrue(SoundController.AmbiencePlaying, "the wind loop plays from the start");
            Assert.AreEqual("Audio/Music/Events/bell_in_the_fog/wind", SoundController.AmbiencePath);
            Assert.IsNotNull(Resources.Load<AudioClip>(presentation.RoundSfxPath), "the toll clip resolves");

            yield return AttackAndPlayOut();
            Assert.AreEqual("Toll 2", Counter.text);
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the ambience bed ----------------------------------------------------------

        [UnityTest]
        public IEnumerator TheBedStartsWithTheFight_AndStopsWhenTheFightIsLeft()
        {
            yield return Bind(Bell(ambience: RealClip), 10);

            Assert.IsTrue(SoundController.AmbiencePlaying, "the fight's ambience loops from the start");
            Assert.AreEqual(RealClip, SoundController.AmbiencePath);

            Navigation.LoadOverride = _ => { };
            var leave = typeof(FightController).GetMethod("LeaveFight", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(leave, "FightController.LeaveFight was renamed or removed");
            leave.Invoke(_fight, null);

            Assert.IsFalse(SoundController.AmbiencePlaying, "leaving the fight stops the bed");
            Assert.AreEqual("", SoundController.AmbiencePath);
        }

        [UnityTest]
        public IEnumerator ARoomFightAfterAnEventFightStopsItsBed()
        {
            yield return Bind(Bell(ambience: RealClip), 10);
            Assert.IsTrue(SoundController.AmbiencePlaying, "fixture");

            yield return Bind(FightRoundPresentation.None, 0);
            Assert.IsFalse(SoundController.AmbiencePlaying);
        }

        [UnityTest]
        public IEnumerator AMissingAmbienceClipIsSilence_NotAnError()
        {
            yield return Bind(Bell(ambience: "Audio/Ambience/does_not_exist"), 10);

            Assert.IsFalse(SoundController.AmbiencePlaying);
            Assert.AreEqual("", SoundController.AmbiencePath);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
