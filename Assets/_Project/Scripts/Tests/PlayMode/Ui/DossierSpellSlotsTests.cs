using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // THE DOSSIER'S SPELL PANEL HAS TWO READINGS, and this fixture pins the
    // one the shipped roster produces (plan P6, gate 3): three slot boxes up,
    // the "cannot carry spell books" line down.
    //
    // WHAT IT CANNOT DO YET, stated rather than skipped. The other reading --
    // slots gone, one line naming the character -- needs a character whose
    // primary pool authors allowsSpellBooks false, and nothing does until
    // Bjorn's `fury` row lands in phase E. The dossier resolves its pool
    // through ContentDatabase from the character's own definition, so unlike
    // the fight HUD (PartyFormationCaptureTests, which hands a fixture pool
    // straight to a live combatant) there is no runtime object to hand a
    // fixture to, and inventing a test-only override on ContentDatabase would
    // be the production flag this project's conventions refuse. So the
    // refusing half is phase E's assertion to add, against real content, and
    // what stands here is the control that would catch the rule shipping
    // inverted -- a line that shows for everybody, or slots that hide for
    // everybody, fails this.
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

        private static GameObject Named(Component root, string name) =>
            root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
    }
}
