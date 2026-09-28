using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M3 CAPTURE (docs/PLAN_BELLWETHER_KIT.md M3): a lone Shawn with
    // Palace Passage, the traveller chosen, the seat pick open -- the two
    // empty seats behind him lit as destinations, the pad's focus on the
    // first. Then the same after one stick step (focus on the rear). A
    // picture, not an assertion: PalacePassagePadTests pins the behaviour.
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.PalacePassageSeatCaptureTests
    // PNGs land in tools/screenshots/runtime/kit_m3/ in the runner copy; the
    // set worth keeping is copied to docs/captures/kit-m3/.
    public class PalacePassageSeatCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "kit_m3"));

        private static readonly (string Name, UiVec Frame)[] Aspects =
        {
            ("16x9", UiFrames.Reference),
            ("4x3", UiFrames.FourThree),
        };

        private FightController _fight;
        private ScriptedBaseInput _input;
        private Canvas _canvas;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        [UnityTest]
        public IEnumerator CaptureTheSoloSeatPick()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.PalacePassageSeatCaptureTests");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module);
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            // THE REAL SHAWN AND A REAL MONSTER, with their art, through the
            // adapter the game's own fights come from -- Palace Passage added
            // to his kit the way the spell preview adds one.
            var built = FightEncounterAdapter.Build(
                new List<string> { "sheep" },
                PreviewFight.EnemiesWithArt(1),
                new SeededRandom(9),
                previewExtraSkillIds: new List<string> { "palace_passage" });
            Assert.IsNotNull(built?.Session, "fixture: the solo fight could not be built");
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return WaitReal(0.5f);

            var shawn = built.Session.Current;
            Assert.IsTrue(shawn.IsPlayerSide, "fixture: Shawn has to open");
            int row = built.Session.SkillOptionsFor(shawn).ToList().FindIndex(o => o.Skill.Id == "palace_passage");
            Assert.GreaterOrEqual(row, 0, "fixture: the Passage is not on his list");

            _canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);

            // SKILL, the Passage row, the traveller: the seat pick is open.
            yield return Stick(1f);
            yield return Submit();
            _fight.HoverRowForTest(row);
            yield return Submit();
            yield return Submit();
            Assert.IsTrue(_fight.IsPickingSeatForTest, "the seat pick did not open");
            yield return WaitReal(0.3f);
            yield return Shoot("a_seat_pick_focus_middle");

            yield return Stick(1f);
            Assert.AreEqual(2, _fight.HoveredSeatForTest);
            yield return WaitReal(0.3f);
            yield return Shoot("b_seat_pick_focus_rear");
        }

        private IEnumerator Frame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Stick(float vertical)
        {
            _input.Vertical = vertical;
            yield return Frame();
            _input.Vertical = 0f;
            yield return Frame();
        }

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return Frame();
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator Shoot(string state)
        {
            foreach (var (name, frame) in Aspects)
            {
                int width = (int)frame.X;
                int height = (int)frame.Y;

                var rig = StageCaptureRig.FullFrame(_canvas, width, height);
                try
                {
                    yield return null;
                    yield return null;
                    yield return null;
                }
                finally
                {
                    rig.Restore();
                }

                Directory.CreateDirectory(OutputDir);
                string path = Path.Combine(OutputDir, $"passage_{state}_{name}.png");
                CanvasCapture.RenderToFile(_canvas, path, width, height);
                FileAssert.Exists(path);
                Debug.Log($"[PalacePassageSeatCapture] wrote {path}");
            }
        }
    }
}
