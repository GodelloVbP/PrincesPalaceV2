using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE DROP-SHADOW COMES ALIVE WITH THE STONE, INSTEAD OF SWITCHING ON.
    //
    // PaintOrbs writes the glow's SETTLED colour the instant a talent is
    // kindled -- see Kindle()'s own comment on why the beat has to already
    // own the stone before that repaint lands. What makes a fresh kindle
    // read as catching alight rather than as a light flicking on is
    // DriveKindling overriding that settled state for its own short window:
    // the glow starts small and transparent and grows out to it. A save
    // that already holds the talent never runs that override at all, so
    // the same repaint is what the player sees on load -- full size, one
    // frame, no ramp.
    public class TalentKindleGlowTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-talent-glow-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TalentController.MotionSpeedMultiplier = 1f;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static IEnumerator OpenTheTree()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Button ButtonNamed(TalentController talents, string name) =>
            talents.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == name);

        private static RectTransform GlowNamed(TalentController talents, string name) =>
            talents.GetComponentsInChildren<Image>(true)
                .Where(i => i.name == name)
                .Select(i => (RectTransform)i.transform)
                .FirstOrDefault();

        private static void Press(TalentController talents, string name)
        {
            var button = ButtonNamed(talents, name);
            Assert.IsNotNull(button, $"the talent screen has no '{name}'");
            Assert.IsTrue(button.gameObject.activeInHierarchy, $"'{name}' is not on screen");
            button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator AFreshKindleGrowsTheGlowFromSmallToFullSize()
        {
            // Opted out of any speed-up: this samples the SHAPE of the beat
            // (small first, 1 at the end), the same reason
            // KindlingOvershootsTheStoneAndSettlesItBack in
            // TalentInvestmentTests runs at real speed rather than fast-
            // forwarded.
            TalentController.MotionSpeedMultiplier = 1f;

            yield return OpenTheTree();

            var talents = Object.FindAnyObjectByType<TalentController>();
            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            talents.Refresh();
            yield return null;

            var glow = GlowNamed(talents, "Orb0_0Glow");
            Assert.IsNotNull(glow, "path 0's root stone has no glow child -- wiring drifted");

            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");

            var scales = new System.Collections.Generic.List<float>();
            float watched = 0f;
            while (watched < ConstellationLayout.KindleSeconds + 0.25f)
            {
                scales.Add(glow.localScale.x);
                watched += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Less(scales[0], 0.3f,
                "the glow's first sampled frame was not small, so kindling still switches it on " +
                "rather than growing it out from the centre");

            Assert.AreEqual(1f, scales.Last(), 0.01f,
                "the glow did not settle back at its full size by the end of the beat");
        }

        [UnityTest]
        public IEnumerator RestoringFromSaveSnapsTheGlowStraightToFullSize()
        {
            // Kindle once, fast-forwarded, purely to get a real unlocked id
            // into the save -- what this test actually checks starts after
            // the reload below.
            TalentController.MotionSpeedMultiplier = 60f;
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

            for (int i = 0; i < 60; i++) yield return null;

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "the fixture kindle did not record, so this test cannot tell a restore from a fresh one");

            // A NEW SCREEN, over a save that already holds the talent -- this
            // is Start()/Refresh() populating from saved state, not Kindle()
            // driving a beat. BeginKindling is never called on this path, so
            // there is nothing for the glow's small-to-full ramp to run.
            yield return OpenTheTree();

            var reopened = Object.FindAnyObjectByType<TalentController>();
            var glow = GlowNamed(reopened, "Orb0_0Glow");
            Assert.IsNotNull(glow, "path 0's root stone has no glow child -- wiring drifted");

            Assert.AreEqual(1f, glow.localScale.x, 0.001f,
                "a talent screen opened over an already-kindled save started the glow small, so " +
                "restoring from a save is animating rather than snapping");
        }

        // ---- paging mid-beat drops the beat too (this bug class) ------------
        //
        // StepCharacter cleared _selectedSlot on a page but left
        // _kindlingSlot/_kindlingPath standing, so a kindle beat started on
        // character A kept running -- DriveKindling has no idea the viewer
        // changed -- and painted A's catch/crust/glow onto this same shared
        // orb index under character B for the rest of the beat's duration.
        [UnityTest]
        public IEnumerator PagingCharacterMidKindleStopsTheBeatFromPaintingTheNewCharacter()
        {
            TalentController.MotionSpeedMultiplier = 1f;

            yield return OpenTheTree();

            var talents = Object.FindAnyObjectByType<TalentController>();
            var squad = SaveSlotManager.CurrentSave.ActiveSquad().Where(c => c != null).ToList();
            Assert.GreaterOrEqual(squad.Count, 2, "need at least two squad members to page between");

            var character = squad[0];
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            talents.Refresh();
            yield return null;

            var glow = GlowNamed(talents, "Orb0_0Glow");
            Assert.IsNotNull(glow, "path 0's root stone has no glow child -- wiring drifted");

            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");

            // A few frames in -- well inside KindleSeconds -- so the beat is
            // demonstrably mid-flight rather than already finished.
            for (int i = 0; i < 3; i++) yield return null;
            Assert.Less(glow.localScale.x, 0.99f,
                "fixture: the glow should still be mid-ramp, not already settled -- nothing to page away from");

            var next = ButtonNamed(talents, "NextCharacterButton");
            Assert.IsNotNull(next, "the talent screen has no NextCharacterButton");
            next.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1f, glow.localScale.x, 0.01f,
                "the previous character's kindle beat is still painting this orb after paging -- the glow " +
                "should have been snapped back to full size by the page, not left mid-ramp under the new " +
                "character");
        }
    }
}
