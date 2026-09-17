using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's half of job 1's visual gate: the comparison box opened
    // by SELECTION rather than by a cursor, photographed, and measured
    // against the card it belongs to in world space.
    //
    // ReckoningCaptureTests already photographs the choice phase and a
    // pointer hover; this is the third state, and the only one where the
    // player has no cursor to move the box off its subject with. Its own
    // class rather than a fifth shot in that file: the capture pipeline
    // selects by test class (tools/screenshot.ps1 -RuntimeFilter), so a
    // gamepad-specific picture belongs in a class you can ask for by name.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // ReckoningTooltipCaptureTests.
    public class ReckoningTooltipCaptureTests
    {
        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reck-tip-" + System.Guid.NewGuid().ToString("N"));
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
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn",
                1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        private static List<ItemOffer> Offers()
        {
            var picks = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderByDescending(i => i.tier)
                .Take(3)
                .ToList();

            Assert.AreEqual(3, picks.Count, "need three equippable items with art to offer");

            return picks.Select(i => new ItemOffer(i.id, i.tier, 0)).ToList();
        }

        private static GameObject Node(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid())
                ?.gameObject;

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y,
                corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        [UnityTest]
        public IEnumerator CaptureTheSelectedOffersComparison()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningTooltipCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the fight scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();

            var reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reckoning, "the Reckoning was never wired into the fight scene");

            reckoning.Show(Reward(), Offers());

            // Real seconds, past the 0.34s wipe and the bars -- the picture
            // ReckoningCaptureTests' own note warns about is one taken while
            // the frame is still clipped to a third of its width.
            yield return new WaitForSecondsRealtime(2.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            // THE MIDDLE CARD, which is the interesting one: the outer two
            // have a whole half of the panel to open into, and the middle is
            // where "beside it, and never on it" is an actual constraint.
            var card = Node("ReckoningOffer1");
            Assert.IsNotNull(card, "the Reckoning drew no card named ReckoningOffer1");
            EventSystem.current.SetSelectedGameObject(card);
            yield return new WaitForSecondsRealtime(0.4f);

            var tooltip = Node("ReckoningOfferTooltip");
            Assert.IsNotNull(tooltip, "the Reckoning drew no comparison box");
            Assert.IsTrue(tooltip.activeSelf, "selecting a card did not open its comparison");

            var box = WorldRect((RectTransform)tooltip.transform);
            var subject = WorldRect((RectTransform)card.transform);

            Assert.IsFalse(box.Overlaps(subject),
                $"the comparison box covers the card it describes: box {box}, card {subject}");

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Reckoning_selected_tooltip.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[ReckoningTooltipCapture] wrote {path} -- box {box}, card {subject}, " +
                      $"overlap {box.Overlaps(subject)}");
        }
    }
}
