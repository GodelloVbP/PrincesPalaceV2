using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // AN UNATTENDED CAST HAS TO REACH THE END OF THE MENU, however many depths
    // the menu grew since the last time anyone looked.
    //
    // FightController.TryForcedFirstAction is the only caster in the game with
    // no hand on it: tools/preview.ps1 -Spell, -Character and -Launch all cast
    // through it, and so does every capture fixture. It presses SKILL, then the
    // row, then a plate -- and the day skills gained an ELEMENT depth between
    // the row and the plate, that sequence stopped one press short. The cast
    // never happened, `IsBusy` never went true, and the only symptom was a
    // preview that timed out fifteen seconds later saying the caster had
    // "refused" -- which it had not; nobody had finished asking.
    //
    // Written against prismatic_orb because it is the only skill in content
    // that asks, but the assertion is about the FORCED PATH, not the orb: any
    // future depth inserted into the player's flow without teaching this path
    // about it fails here rather than in a picture nobody is looking at.
    public class ForcedFirstActionElementTests
    {
        private const string ElementSkillId = "prismatic_orb";

        // 60x, and it matters: this test ends the instant the cast starts, and
        // at real speed the beat is still mid-flight when the fixture tears
        // the scene down. A beat that outlives its panel tries to start a
        // coroutine on a deactivated GameObject, which Unity logs as an error
        // -- and an unhandled error log fails whichever test happens to be
        // running when it lands. It cost GlossaryTests, alphabetically next,
        // one red run before this pair of lines existed.
        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            if (player != null) player.Flush();

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        [UnityTest]
        public IEnumerator AForcedCastOfAnElementSkillActuallyHappens()
        {
            yield return CastAndAssert(element: null, expected: null);
        }

        // AND IT PRESSES THE ONE THAT WAS ASKED FOR. Reaching the plate proves
        // the forced path got through the element depth; it says nothing about
        // WHICH element it pressed, and "it pressed one" is exactly what was
        // true before AUDIT #107 and exactly what made photographing Wind
        // impossible.
        //
        // THE LAST ELEMENT IN AUTHORED ORDER, deliberately: the default rule is
        // "the first that draws", so an ask that happens to agree with the
        // default would pass against a controller that ignored the ask
        // entirely.
        [UnityTest]
        public IEnumerator AForcedCastPressesTheElementItWasAskedForRatherThanTheDefault()
        {
            var skill = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == ElementSkillId);
            AssertElementSkillStillOffersAChoice(skill);

            var elements = skill.Data.Elements;
            string asked = elements[elements.Length - 1].Type.ToString();

            Assert.AreNotEqual(asked, PreviewFight.PreviewElementOf(skill.Data).Type.ToString(),
                $"'{ElementSkillId}' would cast {asked} anyway with nothing asked for, so this test cannot " +
                "tell an honoured request from an ignored one. Point it at an element the default rule " +
                "does not already pick.");

            yield return CastAndAssert(element: asked, expected: asked);
        }

        // A CONTENT PRECONDITION, stated rather than hoped for (AUDIT #46).
        // Both callers used to Ignore themselves here, so renaming or
        // de-elementing the pinned skill switched this whole class off while
        // the run stayed green.
        private static void AssertElementSkillStillOffersAChoice(SkillDefinition skill)
        {
            Assert.IsNotNull(skill,
                $"content no longer has '{ElementSkillId}', the element-choice skill this class is written " +
                "against -- point ElementSkillId at another skill with elements[]");
            Assert.IsTrue(skill.Data.HasElementChoice,
                $"'{ElementSkillId}' no longer offers an element choice, so there is nothing to force -- point " +
                "ElementSkillId at a skill that does");
        }

        private IEnumerator CastAndAssert(string element, string expected)
        {
            var skill = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == ElementSkillId);
            AssertElementSkillStillOffersAChoice(skill);

            var plan = PreviewFight.ForSpell(ElementSkillId, element);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");

            // The bootstrap's own placeholder fight is mid-beat and holds the
            // controller busy; a forced press arriving while it plays is
            // swallowed. Same flush PreviewCaptureTests makes, for the same
            // reason.
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player, "the fight scene has no beat player");
            player.Flush();
            yield return null;
            yield return null;

            var enemies = PreviewFight.EnemiesWithArt(1);
            Assert.IsNotEmpty(enemies, "no enemies in content to cast at");

            var built = FightEncounterAdapter.Build(
                new List<string> { plan.CasterId },
                enemies,
                new SeededRandom(20260908),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { ElementSkillId });

            Assert.IsNotNull(built?.Session, $"'{ElementSkillId}' could not be built into an encounter");

            PreviewFight.Prepare(built, plan);
            built.Session.Begin();
            fight.Bind(built.Session, EncounterClass.Normal);
            fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            fight.ForceFirstAction(ElementSkillId, plan.Element);

            float armed = Time.realtimeSinceStartup;
            while (!fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;

            Assert.IsTrue(fight.IsBusy,
                $"the forced cast of '{ElementSkillId}' never left the menu. It offers an element choice, so " +
                "pressing SKILL and then the row lands on the element list, not on the target prompt -- the " +
                "forced path has to press an element too.");

            // THE BARK LINE, which is where the preview names the element it
            // pressed and therefore the only place the choice is visible from
            // outside the controller. It is written into the immediate message
            // list an instant before the element row is pressed, and the press
            // drains it into the log, so it is on screen by the time the cast
            // is under way.
            string named = fight.RecentLogForTest.FirstOrDefault(line => line.StartsWith("preview: casting"));

            Assert.IsNotNull(named,
                "the forced path cast without saying which element it chose. The line is the whole report " +
                "for a picture nobody watches being taken: log lines seen were " +
                string.Join(" | ", fight.RecentLogForTest));

            if (expected == null)
            {
                StringAssert.Contains("the first element it offers that draws anything", named);
                yield break;
            }

            StringAssert.Contains(expected, named);
            StringAssert.Contains("the element -Element asked for", named,
                "an honoured -Element and the default rule must not read the same in the log, or a picture " +
                "of the wrong element is indistinguishable from a picture of the right one.");
        }
    }
}
