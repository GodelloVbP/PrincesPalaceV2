using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // The talent screen's kindling beat, interrupted -- and what the disk holds
    // while it is still running.
    //
    // TalentInvestmentTests.KindlingOvershootsTheStoneAndSettlesItBack drives
    // ONE beat from end to end and asserts it settles. The beat is not a
    // coroutine: it is Update-driven state on the controller (_kindlingSlot,
    // _kindlingPath, _kindlingElapsed), and a second kindle simply re-points
    // that state at the new stone. Which means the settle -- the explicit
    // scale-back-to-one at the end of DriveKindling -- is the one thing a
    // second kindle can skip, and only for the stone it stops owning.
    //
    // scenarios A18, B27, D5 (docs/hunt/SCENARIOS.md).
    public class TalentLifecycleTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-talent-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- A18: a second stone kindled before the first has settled -----------------

        // TWO STONES IN QUICK SUCCESSION is not an exotic input: a player with
        // embers banked walks up a path, and the beat is 1.12 seconds long.
        //
        // DriveKindling writes the owned stone's localScale every frame from
        // CatchScale and writes it back to 1 only when the beat reaches its
        // end. A second kindle re-points the beat, so BeginKindling settles
        // the stone it stops owning. Nothing else writes an orb's scale
        // (ButtonPressAnimator only tints the plate), so without that the
        // abandoned stone keeps its last mid-catch size; how far off it is
        // depends on the frame rate, which is why this failed only under load.
        //
        // The assertions pin the outcome a player sees.
        [UnityTest]
        public IEnumerator ASecondStoneKindledMidBeatStillSettlesTheFirst()
        {
            // REAL SPEED. This test needs the first beat to be genuinely
            // mid-flight when the second starts, which a multiplier that
            // finishes it inside one frame would skip past entirely -- the same
            // reason KindlingOvershootsTheStoneAndSettlesItBack opts out.
            TalentController.MotionSpeedMultiplier = 1f;

            yield return OpenTheTree();

            var talents = Object.FindAnyObjectByType<TalentController>();
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            talents.Refresh();
            yield return null;

            var first = (RectTransform)ButtonNamed(talents, "Orb0_0").transform;
            var second = (RectTransform)ButtonNamed(talents, "Orb0_1").transform;

            // The root of a path is free and parentless, so it is the one stone
            // that can always be kindled from a fresh save; the one above it is
            // then reachable.
            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;
            yield return null;

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "fixture: the first kindle was refused, so there is no beat to interrupt");
            Assert.Greater(first.localScale.x, 1.001f,
                "fixture: the first stone is not mid-catch, so there is nothing to interrupt");

            // The interruption, well inside the 0.9s catch.
            Press(talents, "Orb0_1");
            yield return null;
            Press(talents, "InvestButton");

            Assert.AreEqual(2, character.unlockedTalentIds.Count,
                "fixture: the second kindle was refused, so nothing interrupted the first beat and " +
                "this test proves nothing");

            // Settled the instant the beat moves on, not eventually: the first
            // stone is mid-catch here, and a frame-rate-dependent residue is
            // what the end-of-test tolerance would otherwise hide.
            Assert.AreEqual(1f, first.localScale.x, 0.0001f,
                "the stone the second kindle took the beat from kept its mid-catch size");

            // Both beats run out.
            yield return Watch(ConstellationLayout.KindleSeconds * 2f + 0.4f);

            Assert.AreEqual(1f, second.localScale.x, 0.01f,
                "the stone that was kindled second never settled");
            Assert.AreEqual(1f, first.localScale.x, 0.01f,
                "the stone whose beat was interrupted by the second kindle was left at the size the " +
                "beat had it at, and BeginKindling did not settle the stone it stopped owning");
        }

        // ---- B27: the screen re-entered after a kindle ------------------------------------

        // A FRESH CONTROLLER EVERY TIME, which is the argument that makes this
        // safe -- and an argument worth pinning rather than restating, because
        // the screen holds its beat in Update-driven fields rather than in a
        // coroutine, and fields are exactly what a re-entered scene would carry
        // if anything about the controller's lifetime changed.
        [UnityTest]
        public IEnumerator ReEnteringTheTreeAfterAKindleArrivesSettledAndAtPageZero()
        {
            TalentController.MotionSpeedMultiplier = 1f;

            yield return OpenTheTree();

            var talents = Object.FindAnyObjectByType<TalentController>();
            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            talents.Refresh();
            yield return null;

            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;
            yield return null;

            // Straight out again, mid-beat.
            yield return OpenTheTree();

            var again = Object.FindAnyObjectByType<TalentController>();
            Assert.AreNotSame(talents, again, "fixture: the scene reload did not rebuild the controller");
            yield return null;

            var orb = (RectTransform)ButtonNamed(again, "Orb0_0").transform;
            Assert.AreEqual(1f, orb.localScale.x, 0.01f,
                "the re-entered screen arrived with a stone mid-beat, so the kindling state outlived " +
                "the screen that owns it");

            var sky = again.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(r => r.name == "TalentSky");
            Assert.IsNotNull(sky, "the talent screen has no TalentSky");

            // The settled x for page 0 is what SlideOffset reports for a
            // completed slide from 0 to 0 -- read off the layout rather than
            // recomputed here.
            yield return Watch(0.3f);
            Assert.AreEqual(ConstellationLayout.SlideOffset(0, 0, 1f), sky.anchoredPosition.x, 1f,
                "the re-entered screen did not settle on the first constellation");
        }

        // ---- D5: what the disk holds while the beat is still playing -----------------------

        // THE EMBER AND THE ID ARE ONE TRANSACTION, and the beat is not part of
        // it. TalentController.Kindle writes the save the instant TalentOps
        // accepts -- "a talent tree that loses a kindled orb to a crash is the
        // single least forgivable thing this screen could do" -- so a reload
        // taken while the stone is still catching must find both halves, never
        // one.
        [UnityTest]
        public IEnumerator ASaveTakenMidKindleHasBothTheEmberAndTheIdOrNeither()
        {
            TalentController.MotionSpeedMultiplier = 1f;

            yield return OpenTheTree();

            var talents = Object.FindAnyObjectByType<TalentController>();
            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            string characterId = character.definitionId;
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            talents.Refresh();
            yield return null;

            // The root is free, so the spend is only visible on the stone
            // ABOVE it -- same reason AKindledTalentChangesTheCharacterItWasSpentOn
            // measures there.
            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            int embersBefore = character.embers;
            int idsBefore = character.unlockedTalentIds.Count;

            Press(talents, "Orb0_1");
            yield return null;
            Press(talents, "InvestButton");

            // Mid-beat, deliberately: one frame in, with the stone still
            // catching.
            yield return null;

            int embersInMemory = character.embers;
            int idsInMemory = character.unlockedTalentIds.Count;
            Assert.Less(embersInMemory, embersBefore, "fixture: the second kindle cost nothing");
            Assert.AreEqual(idsBefore + 1, idsInMemory, "fixture: the second kindle recorded nothing");

            // Off the cache and back off disk, which is what a reload is.
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            var reloaded = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == characterId);

            Assert.AreEqual(embersInMemory, reloaded.embers,
                "the ember spend did not reach disk while the kindling beat was still playing");
            Assert.AreEqual(idsInMemory, reloaded.unlockedTalentIds.Count,
                "the talent id did not reach disk while the kindling beat was still playing, so a " +
                "reload here charges the ember and grants nothing");
        }

        // ---- fixture --------------------------------------------------------------------------

        private static IEnumerator OpenTheTree()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // COUNTED IN SECONDS, NOT FRAMES, for the reason TalentInvestmentTests
        // records: batchmode runs the beat at whatever framerate it can, and a
        // frame budget turned out to be half of a 1.12s beat.
        private static IEnumerator Watch(float seconds)
        {
            TalentController.DeltaOverride = () => 1f / 60f;
            int frames = Mathf.CeilToInt(seconds * 60f);
            for (int i = 0; i < frames; i++) yield return null;
            TalentController.DeltaOverride = null;
        }

        private static Button ButtonNamed(TalentController talents, string name)
        {
            var button = talents.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"the talent screen has no '{name}'");
            return button;
        }

        private static void Press(TalentController talents, string name)
        {
            var button = ButtonNamed(talents, name);
            Assert.IsTrue(button.gameObject.activeInHierarchy, $"'{name}' is not on screen");
            button.onClick.Invoke();
        }
    }
}
