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
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
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

        // ---- a book this character already has (AUDIT #116) ----------------
        //
        // docs/PLAN_SHOP.md 1d: "Refuse a duplicate ... not a silent success
        // either", and 2d (revision 2026-09-03) says the player "only finds
        // out at assignment time, via 1d's duplicate refusal". PressSlot
        // discarded `result.Reason`, so the refusal happened and nothing on
        // the screen moved -- and worse, the green would-fill preview lit on
        // every empty slot including the one this press could never fill.
        //
        // Reachable by design, not hypothetically: AvailableBookOptions
        // excludes a book only when EVERY squad member has learned it, so
        // buying a second copy of a book one character already carries is an
        // ordinary purchase.
        [UnityTest]
        public IEnumerator PressingAnEmptySlotWithABookThisCharacterAlreadyKnowsSaysSo()
        {
            const string Book = "mud_burst";

            // The run has to exist before the scene loads: the panel's
            // unassigned rows are painted from run.unassignedSpellBooks.
            RunManager.StartRun(20260911UL);

            var owner = SaveSlotManager.CurrentSave?.ActiveSquad()?.FirstOrDefault(c => c != null);
            Assert.IsNotNull(owner, "no squad member for the dossier to open on");
            Assert.IsTrue(ContentDatabase.CanHoldSpellBooks(owner.definitionId),
                $"'{owner.definitionId}' refuses spell books, so this fixture cannot reach the refusal");

            var run = RunManager.Run;
            run.learnedSpells.Clear();
            run.learnedSpells.Add(new LearnedSpellEntry
            {
                characterId = owner.definitionId,
                skillId = Book,
                slot = 0,
            });
            run.unassignedSpellBooks.Clear();
            run.unassignedSpellBooks.Add(Book);
            SaveSlotManager.SaveCurrent();

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

            dossier.ShowSpells(true);
            dossier.Refresh();
            yield return null;

            var row = Named(dossier, "DossierUnassigned0");
            Assert.IsNotNull(row, "DossierUnassigned0 is missing from the built scene");
            Assert.IsTrue(row.activeInHierarchy, "the unassigned copy is not offered, so nothing can be selected");
            row.GetComponent<Button>().onClick.Invoke();
            yield return null;

            // BEFORE THE PRESS. The green preview is the screen promising a
            // slot this book can go into, and slot 1 is not one.
            var preview = Named(dossier, "DossierSpellSlot1Selection");
            Assert.IsNotNull(preview, "DossierSpellSlot1Selection is missing from the built scene");
            Assert.IsFalse(preview.activeInHierarchy,
                "the would-fill preview lit on a slot this book cannot fill, so the screen promised a " +
                "placement it then refused");

            var slot = Named(dossier, "DossierSpellSlot1");
            Assert.IsNotNull(slot, "DossierSpellSlot1 is missing from the built scene");
            slot.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, RunManager.Run.learnedSpells.Count,
                "the duplicate was learned into a second slot");

            var line = Named(dossier, "DossierSpellsNoBooksLine");
            Assert.IsNotNull(line, "DossierSpellsNoBooksLine is not in the built scene");
            Assert.IsTrue(line.activeInHierarchy,
                "the press was refused and the panel said nothing, which reads as a dead slot");
            Assert.AreEqual("You already have this spell prepared",
                line.GetComponent<TMPro.TMP_Text>()?.text);
        }

        private static GameObject Named(Component root, string name) =>
            root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
    }
}
