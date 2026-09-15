using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // THE IDENTITY STRETCH ON THE FIGHT HUD, photographed.
    //
    // Progression v2's levels 31-40 pay in things that are only ever visible:
    // a title under the name, a metal rim round the plate, a tint on the
    // leather. There is no number to assert, and UiAudit can say only that a
    // line is contained -- so the gate on this half of phase 5 is a picture,
    // exactly as it is for the reward track's own rail.
    //
    // TWO PLATES, ONE FRAME. The squad is set to level 40 with the whole track
    // collected, which is every identity node at once: the gold rim (38, and
    // again via Mastery at 40), the emboss, the portrait frame and the line
    // reading TITLE . VICTOR . MASTER. Photographing the maximum is what makes
    // the picture worth taking -- a plate with one silver rim and nothing else
    // proves the rim and says nothing about whether four states stacked on one
    // 452px strip still leave it readable.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // IdentityPlateCaptureTests.
    public class IdentityPlateCaptureTests
    {
        // PartyFormationCaptureTests' seed, for the same reason it uses one: a
        // run that fields a real three-member squad on a real stage.
        private const ulong Seed = 20260904UL;

        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRootWithARealSquad()
        {
            FightController.BreathSpeedMultiplier = 1f;

            _root = Path.Combine(Path.GetTempPath(), "pp-identity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            SaveData.TestSquadOfThreeEnabled = true;
            RunManager.StartRun(Seed);
        }

        [TearDown]
        public void Restore()
        {
            FightController.BreathSpeedMultiplier = 1f;
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator CaptureTheFightHudWearingEveryIdentityNode()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter IdentityPlateCaptureTests");
            }

            // SET BEFORE THE SCENE LOADS, because the plates are painted from
            // the save on their first refresh and there is no second one
            // coming: FightController.RefreshPcPlates runs off the session's
            // own beats, not off a save write.
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = 40;
                character.claimedTrackLevel = 40;
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession, "the opening fight built no session");

            // Long enough for the opening beat to settle and the plates to
            // have been painted at least once.
            yield return new WaitForSecondsRealtime(1.5f);

            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "the Fight scene has no root canvas");

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Fight_identity_plates.png");
            CanvasCapture.RenderToFile(canvas, path);

            Assert.IsTrue(File.Exists(path), $"no capture written to {path}");
            Debug.Log($"[IdentityPlateCapture] wrote {path}");

            yield return null;
        }
    }
}
