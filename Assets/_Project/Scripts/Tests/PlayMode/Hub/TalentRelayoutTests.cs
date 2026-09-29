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
    // THE SKY FOLLOWS THE CHARACTER. The scene bakes Shawn's figures; the pager
    // switches characters inside it, so the stones and edges have to be
    // re-placed at runtime from the same ConstellationLayout the build used.
    public class TalentRelayoutTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-talent-relayout-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static RectTransform Named(TalentController talents, string name) =>
            talents.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == name);

        private static void Press(TalentController talents, string name)
        {
            var button = talents.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"the talent screen has no '{name}'");
            button.onClick.Invoke();
        }

        private static void AssertOrb(TalentController talents, string id, int path, int slot)
        {
            var orb = Named(talents, $"Orb{path}_{slot}");
            Assert.IsNotNull(orb, $"no Orb{path}_{slot}");
            Assert.AreEqual(ConstellationLayout.TreeOriginX + ConstellationLayout.StarX(id, path, slot),
                orb.anchoredPosition.x, 0.01f, $"{id} Orb{path}_{slot} x");
            Assert.AreEqual(ConstellationLayout.TreeOriginY + ConstellationLayout.StarY(id, path, slot),
                orb.anchoredPosition.y, 0.01f, $"{id} Orb{path}_{slot} y");
        }

        // Edge0_{parent}_{slot}: both ends and the rect agree.
        private static void AssertEdge(TalentController talents, string id, int path, int parent, int slot)
        {
            var edge = Named(talents, $"Edge{path}_{parent}_{slot}");
            var glow = Named(talents, $"Edge{path}_{parent}_{slot}Glow");
            Assert.IsNotNull(edge, "edge missing");
            Assert.IsNotNull(glow, "edge glow missing");

            float ax = ConstellationLayout.TreeOriginX + ConstellationLayout.StarX(id, path, parent);
            float ay = ConstellationLayout.TreeOriginY + ConstellationLayout.StarY(id, path, parent);
            float bx = ConstellationLayout.TreeOriginX + ConstellationLayout.StarX(id, path, slot);
            float by = ConstellationLayout.TreeOriginY + ConstellationLayout.StarY(id, path, slot);
            float length = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            float angle = Mathf.Atan2(by - ay, bx - ax) * Mathf.Rad2Deg;

            foreach (var rect in new[] { edge, glow })
            {
                Assert.AreEqual(length, rect.sizeDelta.x, 0.05f, $"{id} {rect.name} length");
                Assert.AreEqual((ax + bx) * 0.5f, rect.anchoredPosition.x, 0.05f, $"{id} {rect.name} mid x");
                Assert.AreEqual((ay + by) * 0.5f, rect.anchoredPosition.y, 0.05f, $"{id} {rect.name} mid y");
                Assert.AreEqual(0f, Mathf.DeltaAngle(angle, rect.localEulerAngles.z), 0.05f, $"{id} {rect.name} angle");
            }
        }

        [UnityTest]
        public IEnumerator SwitchingCharactersMovesTheStonesAndEdgesToTheirFigures()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var talents = Object.FindAnyObjectByType<TalentController>();
            var roster = SaveSlotManager.CurrentSave.roster;
            Assert.GreaterOrEqual(roster.Count, 2, "fixture: the roster needs a second character");
            string first = roster[0].definitionId;
            string second = roster[1].definitionId;
            Assert.AreEqual("sheep", first, "fixture: Shawn opens first");
            Assert.AreEqual("bear", second, "fixture: Bjorn is the second character");

            foreach (int slot in new[] { 0, 10, 20 }) AssertOrb(talents, first, 0, slot);
            AssertEdge(talents, first, 0, 0, 1);

            Press(talents, "NextCharacterButton");
            yield return null;
            foreach (int slot in new[] { 0, 10, 20 }) AssertOrb(talents, second, 0, slot);
            foreach (int slot in new[] { 0, 10, 20 }) AssertOrb(talents, second, 1, slot);
            AssertEdge(talents, second, 0, 0, 1);

            // Slots 0/10/20 sit on the shared spine, so they can agree between
            // figures; the off-spine stones are what tell the characters apart.
            bool anyDiffers = false;
            for (int slot = 0; slot < 21; slot++)
            {
                AssertOrb(talents, second, 0, slot);
                anyDiffers |= ConstellationLayout.StarX(first, 0, slot) != ConstellationLayout.StarX(second, 0, slot);
            }
            Assert.IsTrue(anyDiffers, "fixture: the two characters must draw different figures or this proves nothing");

            Press(talents, "PrevCharacterButton");
            yield return null;
            foreach (int slot in new[] { 0, 10, 20 }) AssertOrb(talents, first, 0, slot);
            AssertEdge(talents, first, 0, 0, 1);
        }
    }
}
