using System.Collections;
using System.Collections.Generic;
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

        // OutputDir/Named/RootCanvas/Shoot moved to Tests/PlayMode/Shared/
        // HudCaptureRig.cs (S12's review) -- byte-for-byte duplicated in
        // StatusRowCaptureTests before this. Built fresh per test, right
        // after _fight is assigned, since the rig needs a real FightController
        // to walk.
        private HudCaptureRig _rig;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureFramerate = 0;
        }

        private void Click(string name)
        {
            var go = _rig.Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static void FillEveryPool(IEnumerable<Domain.Combat.CombatantState> party)
        {
            foreach (var member in party)
            {
                if (member.PrimaryPool != null) member.PrimaryPool.Current = member.PrimaryPool.Max;
                if (member.SignaturePool != null) member.SignaturePool.Current = member.SignaturePool.Max;
            }
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
            _rig = new HudCaptureRig(_fight, "FightMenu_", "FightMenuCapture");

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

            // Every pool full BEFORE the bind, so step 3 below does not depend
            // on what a pool starts at. It did: Shawn opens on 0/10 Wool, his
            // only skill (Shear) costs 3, and the capture stopped at step 3 with
            // frames 3-5 never written (QA pass 2026-09-26). A fixture that
            // wants "a castable skill" has to make one castable, not hope the
            // content's opening values happen to allow it.
            FillEveryPool(built.Party);
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

            yield return _rig.Shoot("1_resting");

            // SKILL is Verb1 (0=ATTACK, 1=SKILL, 2=ITEM, 3=MOVE).
            Click("Verb1");
            yield return _rig.Shoot("2_skill_submenu");

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
            // The press must have LANDED, not merely been made: a refused press
            // leaves the menu where it was and the next shot silently repeats
            // the previous one.
            var prompt = _rig.Named("TargetPrompt");
            Assert.IsNotNull(prompt, "the Fight scene has no TargetPrompt");
            Assert.IsTrue(prompt.activeInHierarchy, $"pressing {castable.name} did not open targeting");
            yield return _rig.Shoot("3_targeting");

            // ITEM, the other submenu, to see whether the two are consistent.
            Click("Verb2");
            yield return _rig.Shoot("4_item_submenu");

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
            yield return _rig.Shoot("5_intent_hover");
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
            _rig = new HudCaptureRig(_fight, "FightMenu_", "FightMenuCapture");

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

            yield return _rig.Shoot("6_long_list_scrolled");
        }
    }
}
