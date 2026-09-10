using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // THE DOSSIER'S SPELL PANEL HAS TWO READINGS, and this fixture pins the
    // one the shipped roster produces (plan P6, gate 3): three slot boxes up,
    // the "cannot carry spell books" line down.
    //
    // BOTH READINGS ARE HERE SINCE PHASE E. The refusing half could not be
    // written before Bjorn's `fury` row shipped: the dossier resolves its pool
    // through ContentDatabase from the character's own definition, so unlike
    // the fight HUD (PartyFormationCaptureTests, which hands a fixture pool
    // straight to a live combatant) there is no runtime object to hand a
    // fixture to, and inventing a test-only override on ContentDatabase would
    // be the production flag this project's conventions refuse. Now it is real
    // content and the second test simply pages to him.
    //
    // NEITHER HALF IS WORTH ANYTHING ALONE. A rule shipped inverted -- a line
    // that shows for everybody, slots that hide for everybody -- passes one of
    // these and fails the other, which is the only reason both are here.
    //
    // It also pins that the new node reaches the built scene at all: the
    // binder matches by field name and a miss is silent in code (UiWiringSweep
    // catches a null field at build, but not a field bound to nothing because
    // the screen never declared the ref).
    public class DossierSpellSlotsTests
    {
        private string _root;

        private static string OutputDir => CaptureOutput.LabelDir("dossier_spells");

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-spells-" + System.Guid.NewGuid().ToString("N"));
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

        [UnityTest]
        public IEnumerator ACharacterWhosePoolReadsBooksGetsThreeSlotsAndNoExplanation()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

            // The premise this whole assertion rests on, stated rather than
            // assumed: whoever the dossier opened on holds a pool that reads
            // books. The day that stops being true for the FIRST squad member,
            // this test should be the one that says so.
            var shown = SaveSlotManager.CurrentSave?.ActiveSquad()?.FirstOrDefault(c => c != null);
            Assert.IsNotNull(shown, "no squad member for the dossier to open on");
            Assert.IsTrue(ContentDatabase.CanHoldSpellBooks(shown.definitionId),
                $"'{shown.definitionId}' refuses spell books, so this fixture is now testing the other reading");

            dossier.ShowSpells(true);
            dossier.Refresh();
            yield return null;

            var line = Named(dossier, "DossierSpellsNoBooksLine");
            Assert.IsNotNull(line,
                "DossierSpellsNoBooksLine is not in the built scene -- the screen tree declared it but the " +
                "scene was not rebuilt, or the node name moved away from the NodeRef");
            Assert.IsFalse(line.activeSelf,
                "the 'cannot carry spell books' line is showing for a character who can");

            for (int i = 0; i < SpellBooks.MaxSpellSlots; i++)
            {
                var slot = Named(dossier, $"DossierSpellSlot{i}");
                Assert.IsNotNull(slot, $"DossierSpellSlot{i} is missing from the built scene");
                Assert.IsTrue(slot.activeInHierarchy,
                    $"DossierSpellSlot{i} is hidden for a character who can hold books");
            }

            if (!CanvasCapture.IsSupported) yield break;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            if (canvas == null) yield break;

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "spell_slots_present.png");
            CanvasCapture.RenderToFile(canvas, path);
            Debug.Log($"[DossierSpells] wrote {path}");
        }

        // THE OTHER READING, against real content: page to Bjorn, whose
        // primaryPoolId is `fury` and whose row authors allowsSpellBooks
        // false. Three things have to agree -- the slots gone, the sentence
        // up naming him, and the collapsed header carrying no fraction -- and
        // the third is the one a player sees without opening the panel at all.
        [UnityTest]
        public IEnumerator TheFuryHolderGetsOneSentenceInsteadOfThreeSlots()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

            var next = menu.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "DossierNextCharacter");
            Assert.IsNotNull(next, "the dossier has no next-character button, so nothing can page to Bjorn");

            var squad = SaveSlotManager.CurrentSave?.ActiveSquad()?.Where(c => c != null).ToList();
            Assert.IsNotNull(squad);
            int bjorn = squad.FindIndex(c => c.definitionId == "bear");
            Assert.GreaterOrEqual(bjorn, 0,
                "Bjorn is not in the fielded squad, so the refusing reading is unreachable from this screen");
            Assert.IsFalse(ContentDatabase.CanHoldSpellBooks("bear"),
                "content says Bjorn CAN hold books, so this fixture is testing the other reading again");

            // Paging from wherever the pane opened, by pressing the button a
            // hand would press rather than by reaching into the controller's
            // index. Bounded by the squad size: the row wraps.
            for (int step = 0; step < squad.Count; step++)
            {
                string shown = Named(dossier, "DossierName")?.GetComponent<TMPro.TMP_Text>()?.text;
                if (shown == "Bjorn") break;

                next.onClick.Invoke();
                yield return null;
            }

            Assert.AreEqual("Bjorn", Named(dossier, "DossierName")?.GetComponent<TMPro.TMP_Text>()?.text,
                "paging the whole squad never reached Bjorn");

            dossier.ShowSpells(true);
            dossier.Refresh();
            yield return null;

            var line = Named(dossier, "DossierSpellsNoBooksLine");
            Assert.IsNotNull(line, "DossierSpellsNoBooksLine is not in the built scene");
            Assert.IsTrue(line.activeInHierarchy,
                "the panel offers Bjorn nothing and says nothing about why");
            Assert.AreEqual("Bjorn cannot carry spell books",
                line.GetComponent<TMPro.TMP_Text>()?.text,
                "the sentence must name the character rather than state a rule in the abstract");

            for (int i = 0; i < SpellBooks.MaxSpellSlots; i++)
            {
                var slot = Named(dossier, $"DossierSpellSlot{i}");
                Assert.IsNotNull(slot, $"DossierSpellSlot{i} is missing from the built scene");
                Assert.IsFalse(slot.activeInHierarchy,
                    $"DossierSpellSlot{i} is still up for a character who can never fill it -- " +
                    "three greyed chips say 'not yet' and the answer here is 'never'");
            }

            // THE COLLAPSED HEADER. "SPELLS 0/3" on a row that can never read
            // 1/3 is the panel's only lie visible without opening it, and the
            // count is what has to go rather than the row.
            var count = Named(dossier, "DossierSpellsRowCount")?.GetComponent<TMPro.TMP_Text>();
            Assert.IsNotNull(count, "DossierSpellsRowCount is missing from the built scene");
            Assert.IsEmpty(count.text ?? "",
                $"the collapsed SPELLS row still reads '{count.text}' for a character with no slots to count");

            if (!CanvasCapture.IsSupported) yield break;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            if (canvas == null) yield break;

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "spell_slots_refused.png");
            CanvasCapture.RenderToFile(canvas, path);
            Debug.Log($"[DossierSpells] wrote {path}");
        }

        private static GameObject Named(Component root, string name) =>
            root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
    }
}
