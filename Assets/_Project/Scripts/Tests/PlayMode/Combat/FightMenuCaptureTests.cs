using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // Captures the fight menu in the states a STATIC capture cannot reach.
    //
    // ScreenshotTool renders Edit Mode, where the submenu, the detail column
    // and the target prompt all start inactive -- so the only thing it can ever
    // show of the command menu is its resting state, which is exactly the state
    // nobody has a question about. Every complaint about this menu is about
    // what happens after a press.
    //
    // Runs only with a graphics device, i.e. through tools/screenshot.ps1
    // -Runtime -RuntimeFilter FightMenuCaptureTests.
    public class FightMenuCaptureTests
    {
        private FightController _fight;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureFramerate = 0;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static Canvas RootCanvas() =>
            Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).FirstOrDefault(c => c.isRootCanvas);

        private IEnumerator Shoot(string label)
        {
            // Two frames: one for the click's state change to apply, one for
            // the open animation to have started drawing. Without the second,
            // every column captures at alpha 0 and the shot looks like the
            // panel never opened.
            yield return null;
            yield return null;

            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, $"FightMenu_{label}.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path), $"capture '{label}' was not written");
            Debug.Log($"[FightMenuCapture] wrote {path}");
        }

        [UnityTest]
        public IEnumerator CaptureTheMenuInEveryStateAPlayerActuallySees()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime -RuntimeFilter FightMenuCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            // Let the opening beats drain so the capture is of a settled menu
            // waiting on the player, not of the fight's first animation.
            for (int i = 0; i < 240; i++) yield return null;

            // A FIXED three-monster encounter, so the capture shows the same
            // thing every time. Left to the scene's own bootstrap this varies:
            // with no run it fields three distinct monsters, with a run it
            // fields whatever the room rolled -- which is how a two-rat capture
            // and a three-monster test suite disagreed about a badge bug.
            // WITH ART, not simply the first. Most of the roster has no battle
            // sprite authored, and a party member rendering as the grey fallback
            // plate defeats the whole point of a screenshot -- which is exactly
            // what the first version of this fixture produced.
            var party = ContentDatabase.Characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath))
                .Take(1).Select(c => c.id).ToList();
            var enemies = ContentDatabase.Enemies
                .Where(e => !string.IsNullOrWhiteSpace(e.Data.SpritePath))
                .Take(3).Select(e => e.id).ToList();
            Assert.AreEqual(1, party.Count, "no character has battle art to capture");
            Assert.AreEqual(3, enemies.Count, "fewer than three enemies have art to capture");
            var built = FightEncounterAdapter.Build(party, enemies, new Domain.Rng.SeededRandom(11));
            _fight.Bind(built.Session, EncounterClass.Normal);

            // The party's ART is handed over SEPARATELY from the session, by
            // design (BindPartyArt's own comment: "a caller with no art simply
            // does not call it"). Skip it and every party member draws as the
            // grey fallback plate -- which is what the first version of this
            // fixture captured, and it reads as missing art rather than as a
            // missing call.
            _fight.BindPartyArt(built.Party,
                built.Party.Select(p => ContentDatabase.Characters
                        .FirstOrDefault(c => c.Data.DisplayName == p.Name)?.Data.BattleSpritePath)
                    .ToList());

            yield return null;
            yield return null;

            yield return Shoot("1_resting");

            // SKILL is Verb1 (0=ATTACK, 1=SKILL, 2=ITEM, 3=RUN, 4=HOLD BACK).
            Click("Verb1");
            yield return Shoot("2_skill_submenu");

            // The first skill row THE ACTOR CAN ACTUALLY CAST, which puts the
            // fight into target selection and raises the detail column and the
            // target prompt.
            //
            // It was CharacterSkill0 flat. Shawn opens a fight on 0 wool and
            // most of his kit is priced in it, so row 0 is unaffordable, the
            // press is refused, and this shot and the one after it were
            // silently identical to the one before -- a capture of the screen
            // NOT advancing, which looks exactly like a capture of it advancing
            // if nobody checks the filename against the picture.
            var castable = _fight.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .FirstOrDefault(b => b.name.StartsWith("CharacterSkill") && b.interactable
                                     && b.gameObject.activeInHierarchy);
            Assert.IsNotNull(castable, "the actor has no castable skill to open targeting with");
            castable.onClick.Invoke();
            yield return null;
            yield return Shoot("3_targeting");

            // ITEM, the other submenu, to see whether the two are consistent.
            Click("Verb2");
            yield return Shoot("4_item_submenu");

            // Back out to the root so the stage is unobscured, then hover the
            // first enemy's intent icon. Driven through the component rather
            // than by faking a pointer position, because the capture has no real
            // cursor and a synthetic EventSystem raycast would be testing Unity
            // rather than the tooltip.
            Click("Verb0");
            yield return null;

            var hover = _fight.GetComponentsInChildren<HoverIndex>(includeInactive: true).FirstOrDefault();
            Assert.IsNotNull(hover, "no HoverIndex was attached to any enemy intent icon");
            hover.OnPointerEnter(null);
            yield return Shoot("5_intent_hover");
        }

        // THE ONE STATE NOTHING ELSE CAN SHOW: a list too long for its window,
        // held part-way down it.
        //
        // Every other shot in this file catches a list of one or two rows, so
        // the scroll bar is not merely un-scrolled in them -- it is switched
        // off entirely, which is correct and tells nobody anything about the
        // bar. A twelve-entry satchel is the cheapest list that overflows: the
        // skill branch would need a levelled actor with a talent root behind
        // him to reach the same nine rows.
        //
        // Left at two notches down rather than at either end, because that is
        // the position the bug this shot was added for could not hold: the list
        // was re-anchored on every repaint and sprang back to the top before
        // the wheel had finished.
        [UnityTest]
        public IEnumerator CaptureALongListHeldPartWayDownItsScroll()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime -RuntimeFilter FightMenuCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            for (int i = 0; i < 240; i++) yield return null;

            var party = ContentDatabase.Characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath))
                .Take(1).Select(c => c.id).ToList();
            var enemies = ContentDatabase.Enemies
                .Where(e => !string.IsNullOrWhiteSpace(e.Data.SpritePath))
                .Take(3).Select(e => e.id).ToList();
            var built = FightEncounterAdapter.Build(party, enemies, new Domain.Rng.SeededRandom(11));

            var satchel = new List<Domain.Combat.Session.SatchelStack>();
            for (int i = 0; i < 12; i++)
            {
                satchel.Add(new Domain.Combat.Session.SatchelStack(
                    $"potion_{i}", $"Potion {i + 1}", 3, restoresMana: i % 2 == 1));
            }

            _fight.Bind(built.Session, EncounterClass.Normal, satchel);
            _fight.BindPartyArt(built.Party,
                built.Party.Select(p => ContentDatabase.Characters
                        .FirstOrDefault(c => c.Data.DisplayName == p.Name)?.Data.BattleSpritePath)
                    .ToList());

            yield return null;
            yield return null;

            Click("Verb2");
            yield return null;

            var wheel = _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .First(t => t.name == "SubmenuViewport").GetComponent<ListScroll>();
            Assert.IsNotNull(wheel, "nothing is listening for a wheel over the list");
            wheel.Scrolled(Domain.UiKit.FightSubmenuLayout.RowPitch * 2f);

            // A repaint with nothing about the list changed, standing in for the
            // hover the wheel itself provokes in play. The capture is of the
            // list AFTER that repaint, which is the whole claim.
            _fight.RefreshUi();

            yield return Shoot("6_long_list_scrolled");
        }
    }
}
