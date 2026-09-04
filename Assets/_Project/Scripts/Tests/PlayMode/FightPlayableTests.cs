using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // Open the scene, and there is a fight in it.
    //
    // Everything else in the fight suite hands the controller a fixture it built
    // itself. This is the only test that opens the scene the way a PLAYER does
    // and asserts something real came out of authored content -- real sprites on
    // the stage, real numbers on the plates, a turn that can be taken.
    //
    // Deliberately headless-safe: Resources.Load needs no graphics device, so
    // "did the art load" is answerable in the commit gate. Only "does it look
    // right" needs a GPU, and that is the screenshot tools' job.
    public class FightPlayableTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private IEnumerator OpenTheScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
        }

        [UnityTest]
        public IEnumerator OpeningTheSceneStartsAFight()
        {
            yield return OpenTheScene();

            Assert.IsTrue(_fight.HasSession,
                "FightBootstrap did not run, or found no content to build a fight from");
        }

        [UnityTest]
        public IEnumerator TheStageIsPopulatedFromRealContent()
        {
            yield return OpenTheScene();

            Assert.IsTrue(Named("Enemy0Slot").activeSelf);
            Assert.IsFalse(string.IsNullOrEmpty(Named("Enemy0Nameplate").GetComponent<TMPro.TMP_Text>().text),
                "a monster from enemies.json should be standing there");
            Assert.IsTrue(Named("Party0Slot").activeSelf);
        }

        [UnityTest]
        public IEnumerator TheActorsWearTheirOwnArt_NotTheFallbackPlate()
        {
            // The point of the whole stage-visuals port. If this fails the screen
            // still "works" -- it just shows crimson rectangles where the
            // characters should be, which is exactly the state it was in before.
            yield return OpenTheScene();

            var fallback = Named("EnemyPlate0") == null ? null : _fight.FallbackSprite;

            var enemy = Named("Enemy0Sprite").GetComponent<Image>();
            Assert.IsNotNull(enemy.sprite, "an Image with no sprite renders as a solid white quad");
            Assert.AreNotSame(fallback, enemy.sprite,
                "the monster fell back to the plate - its sheet did not load from Resources");

            var hero = Named("Party0Sprite").GetComponent<Image>();
            Assert.IsNotNull(hero.sprite);
            Assert.AreNotSame(fallback, hero.sprite,
                "the party member fell back to the plate - BindPartyArt never reached the stage");

            // AND THAT ANYTHING IS ACTUALLY DRAWN.
            //
            // The version of this test without these two lines passed while the
            // stage rendered nothing at all: the sprite nodes are built inactive,
            // and setting `.sprite` on a component whose GameObject is inactive
            // is perfectly legal and completely invisible. "The art loaded" and
            // "the actor is on screen" are different claims and only the second
            // one is the point.
            Assert.IsTrue(enemy.gameObject.activeInHierarchy, "the monster is loaded but not on screen");
            Assert.IsTrue(hero.gameObject.activeInHierarchy, "the party member is loaded but not on screen");
        }

        [UnityTest]
        public IEnumerator TheTwoSidesFaceEachOther()
        {
            // A combatant always looks ACROSS the stage. Shawn's art faces right
            // and he stands left, so he needs no flip; a monster on the right
            // whose art also faces right must be mirrored.
            yield return OpenTheScene();

            var hero = Named("Party0Sprite").GetComponent<Image>();
            Assert.AreEqual(1f, hero.rectTransform.localScale.x, 0.001f,
                "art that already faces the right way must not be flipped");
        }

        [UnityTest]
        public IEnumerator TheFigureIsStoodOnTheGroundLine()
        {
            // The manifest's drop, applied. Every actor with a sheet has one
            // authored, and a zero here means the grounding never ran -- which is
            // the bug that made the golem fly.
            yield return OpenTheScene();

            var enemy = Named("Enemy0Sprite").GetComponent<Image>();

            Assert.AreEqual(enemy.rectTransform.offsetMin.y, enemy.rectTransform.offsetMax.y, 0.001f,
                "the sprite is nudged as a whole, not stretched");
            Assert.LessOrEqual(enemy.rectTransform.offsetMin.y, 0f,
                "the drop moves the figure DOWN onto the floor, never up off it");
        }

        [UnityTest]
        public IEnumerator ATurnCanActuallyBeTaken()
        {
            // Click ATTACK, click a monster, watch the round resolve. If this
            // passes the screen is playable in the ordinary sense of the word.
            yield return OpenTheScene();

            var front = _fight.Session.Encounter.Enemies[0];
            int before = front.CurrentHealth;

            Click("Verb0");
            Click("EnemyPlate0");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Less(front.CurrentHealth, before, "the swing never landed");
            Assert.AreEqual("C O M M A N D", Named("Breadcrumb").GetComponent<TMPro.TMP_Text>().text,
                "the menu did not return to the root, so no second turn could be taken");
        }

        // A STILL DRAWING STILL HAS TO SWING, and the swing is the only thing
        // that can carry it: there are no frames to step, so if the transform
        // does not move the figure then nothing happened at all.
        //
        // Sampled while the beat is mid-flight, because the round ends back on
        // the mark either way -- checking afterwards would pass whether or not
        // the figure ever left it. The same shape StaticPilotStageCaptureTests
        // records over a whole beat, asserted here on the party leader.
        [UnityTest]
        public IEnumerator AStillDrawingSwingCarriesTheFigureAndBringsItBack()
        {
            yield return OpenTheScene();

            var hero = _fight.Session.Encounter.PlayerParty[0];

            // ASSERTED, not skipped: a party leader with no attack drawing
            // would make this pass by never being asked to swing.
            Assert.IsNotNull(_fight.StanceSpriteFor(hero, FightSession.Stances.Attack),
                "the party leader has no attack drawing, so there is nothing for a beat to show");

            var slot = (RectTransform)Named("Party0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the party slot has no animator, so nothing can lunge");

            Click("Verb0");
            Click("EnemyPlate0");

            float travelled = 0f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                travelled = Mathf.Max(travelled,
                    Vector2.Distance(slot.anchoredPosition, animator.Home));
                yield return null;
            }

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Greater(travelled, 1f,
                "the attacker never left its mark, so a single-drawing swing showed nothing at all");
            Assert.Less(Vector2.Distance(slot.anchoredPosition, animator.Home), 1f,
                "the attacker never came home, so it fights the next round from wherever it stopped");
        }
    
        // ---- the one that has to be looked at ------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheLiveStage()
        {
            // Not an assertion so much as a DELIVERABLE. The Edit Mode
            // screenshot tool renders the scene as authored, and the bootstrap
            // only runs in Play Mode -- so the tool's FightPanel.png shows an
            // empty stage no matter how well any of this works. This is the only
            // way to see the actors.
            //
            // Graphics-gated like every other pixel capture; run it with
            // tools/graphics_tests.ps1.
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            yield return OpenTheScene();

            var canvas = _fight.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas);

            string dir = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));
            Directory.CreateDirectory(dir);

            string resting = Path.Combine(dir, "Fight_resting.png");
            CanvasCapture.RenderToFile(canvas, resting);
            FileAssert.Exists(resting);

            // And again mid-swing, which is the only state that shows the attack
            // pose, the lunge and the flash at once.
            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;
            yield return null;

            string swinging = Path.Combine(dir, "Fight_midswing.png");
            CanvasCapture.RenderToFile(canvas, swinging);
            FileAssert.Exists(swinging);
        }
    
        [UnityTest]
        public IEnumerator NothingOnScreenIsAWhiteQuad()
        {
            // The bug class this project has shipped more than once. Every Image
            // that is VISIBLE must have a sprite; an Image with none renders as a
            // solid white rectangle, not as nothing. Six of them sat across the
            // initiative tracker because "show the slot" was checked and "has a
            // portrait" was not.
            yield return OpenTheScene();

            // SPRITE-LESS AND STILL WHITE is the actual symptom, and the "still
            // white" half is load-bearing. Plenty of Images here are meant to be
            // flat rectangles -- health bars, mana fills, wool pips, hairlines --
            // and they are fine precisely because someone chose their colour. An
            // Image that was meant to carry ART and has none is the one left at
            // the default white it was constructed with.
            //
            // The first draft of this test omitted the colour check and reported
            // 21 bars and pips as bugs, which is how a sweep like this becomes
            // noise nobody reads.
            var offenders = _fight.GetComponentsInChildren<Image>(includeInactive: false)
                .Where(i => i.enabled && i.sprite == null)
                .Where(i => i.color.a > 0.01f && i.color.r > 0.95f && i.color.g > 0.95f && i.color.b > 0.95f)
                .Select(i => i.name)
                .ToList();

            CollectionAssert.IsEmpty(offenders,
                "these render as solid white quads: " + string.Join(", ", offenders));
        }
}
}
