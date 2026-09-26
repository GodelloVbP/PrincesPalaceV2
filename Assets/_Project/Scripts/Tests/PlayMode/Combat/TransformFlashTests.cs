using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // "HE SHOULD FLASH AND THEN BECOME THE BLACK RAM" -- and flash again when
    // he stops being it.
    //
    // AGAINST THE REAL FIGHT SCENE rather than through FightBeatPlayer's
    // *ForTest doors, deliberately, and that is the difference that matters.
    // FightBeatPhaseStanceTests already pins the ORDER of the calls -- that
    // WearForm is invoked at the impact instant and not at the open -- with a
    // delegate that records a string, and it passed the entire time the flash
    // was invisible in the running game, because what was wrong was downstream
    // of the call: what the overlay's Image was holding when Flash() asked it.
    // Only a real scene has an overlay with an Image on it.
    //
    // NO GRAPHICS NEEDED. Everything asserted here is component state --
    // enabled, colour, sprite -- so this runs in the commit gate rather than
    // only under tools/screenshot.ps1.
    public class TransformFlashTests
    {
        private const string TransformSkill = "black_ram_mode";
        private const string FormFolder = "Characters/sheep_black_ram";

        // What black_ram_mode authors for both its wind-up and its strike, so
        // what changes at the impact instant is the FOLDER rather than the
        // pose -- and therefore the one silhouette only the transform can
        // produce.
        private const string FormStance = "victory";

        // HOW MANY FRAMES ONE ROUND OF PLAYBACK IS ALLOWED, and it is a
        // measurement rather than a round number. It was a literal 1200 at
        // both loops below until 2026-09-19, when the owner's "reeling should
        // happen 4x as slow" took the reel from 0.4175s to 1.505s and
        // FightBeatPlayer.StillReeling started holding a beat whose actor or
        // target is still coming home from an earlier one. A round that used
        // to play inside 1200 frames at this fixture's 12x now costs about
        // 2350 of them (measured here: 1439 / 2349 / 1628 over the three
        // rounds the revert test needs), so the old bound was truncating
        // playback mid-round -- the outer loop then clicked its next turn
        // while the fight was still busy, the click was dropped, and the
        // revert beat was still QUEUED when the fixture gave up on it. The
        // failure read as "the transform never expired", which it had.
        //
        // Sized at roughly 2.5x the measured cost rather than at the measure
        // itself: this bound exists to turn a hang into a failing test, not
        // to pin the reel's length, and anything that pins the reel belongs
        // in FightBeatPacingTests where the numbers are literal.
        private const int RoundFrameBudget = 6000;

        private FightController _fight;
        private FightBeatPlayer _beats;

        [SetUp]
        public void Hurry()
        {
            // The ORDER and the EXISTENCE of a flash are what these assert,
            // never a duration. 12x rather than 60x: the flash is 0.21s, and
            // at 60x that is under one frame at any plausible frame rate --
            // the sampling loop would step straight over the thing it is
            // looking for and report the bug it exists to disprove.
            FightBeatPlayer.BeatSpeedMultiplier = 12f;
        }

        // AUDIT #106, PAID FOR HERE RATHER THAN BY THE NEXT FIXTURE IN THE
        // BATCH.
        //
        // These tests deliberately stop mid-round, so the Fight scene they
        // leave behind still has a playback in flight. Whoever loads a scene
        // next disables it, FightBeatPlayer.OnDisable flushes, and the flush
        // reaches FightController.BeginAppearancePop on a panel that is
        // already inactive -- which logs an error INSIDE that fixture. NUnit
        // runs classes alphabetically and TypographyMigrationTests sorts
        // immediately after this one, so it failed on a log message about a
        // fight it never opened.
        //
        // Ended here instead, while this class is still the current one and
        // the panel is still active: the callback runs with nothing to
        // complain about, and the next fixture inherits a quiet scene.
        [UnityTearDown]
        public IEnumerator Restore()
        {
            LogAssert.ignoreFailingMessages = true;
            if (_beats != null) _beats.EndFight();
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        // ---- the entry ------------------------------------------------------

        // THE BEAT MUST NOT OPEN ON THE RAM. The round resolves in one
        // synchronous pass before a single beat plays, so anything that paints
        // the stage from LIVE state between resolution and the first beat puts
        // the form on screen before the flash meant to cover it -- which is
        // exactly what FightBeatPlayer.Play's supersede-Flush used to do.
        // WearForm then found the folder it was about to wear already worn and
        // took its "nothing actually changed" early return, so there was no
        // flash at all.
        [UnityTest]
        public IEnumerator TheFormIsNotOnScreenBeforeTheBeatThatPutsItThere()
        {
            yield return StandTheFightUp();

            var ramDrawings = FormDrawings();

            _fight.ForceFirstAction(TransformSkill);
            yield return WaitForBusy();

            foreach (var figure in PartyFigures())
            {
                Assert.IsFalse(ramDrawings.Contains(figure.sprite),
                    "the figure was already drawn from '" + FormFolder + "' on the opening frame of the " +
                    "beat that transforms him -- so the swap has already happened by the time anything " +
                    "flashes over it");
            }
        }

        [UnityTest]
        public IEnumerator TheTransformBeatFlashesTheNewFormsSilhouette()
        {
            yield return StandTheFightUp();

            var ramArt = StanceAnimationLibrary.Resolve(FormFolder, FormStance);
            Assert.IsNotNull(ramArt, FormFolder + "/" + FormStance + " is not on disk, so this fixture " +
                                     "cannot tell a missing flash from missing art");

            _fight.ForceFirstAction(TransformSkill);
            yield return WaitForBusy();

            var lit = new HashSet<string>();
            bool everLit = false;
            bool wore = false;

            // BOUNDED BY THE TRANSFORM'S OWN LOG LINE, and that bound is the
            // whole difference between this and a test that cannot fail. A
            // beat's messages are pushed at its IMPACT instant, on the frame
            // its flash lights, so while "becomes the" is the newest line the
            // beat being drawn is the transform's own.
            // Without it, the enemy's reply later in the same round -- which
            // flashes Shawn while he is already in the ram's art -- satisfies
            // the assertion, and it did: this test passed against the bug.
            for (int frame = 0; frame < RoundFrameBudget && _fight.IsBusy; frame++)
            {
                if (NewestLineContains("becomes the"))
                {
                    foreach (var flash in PartyFlashes())
                    {
                        if (flash.sprite == ramArt) wore = true;
                        if (!flash.enabled || flash.color.a <= 0f) continue;
                        everLit = true;
                        lit.Add(flash.sprite == null ? "<none>" : flash.sprite.name);
                        if (flash.sprite == ramArt) yield break;
                    }
                }

                yield return null;
            }

            Assert.IsTrue(wore, "the figure never wore the form's art during its own beat, so the swap " +
                                "itself is what failed rather than the flash over it");
            Assert.IsTrue(everLit, "no party hit-flash overlay was ever lit during the transform's own " +
                                   "beat -- the swap happened with nothing over it");

            Assert.Fail("the transform lit a silhouette, but never one shaped like the form it turned " +
                        "INTO. Lit shapes were: " + string.Join(", ", lit));
        }

        // ---- the exit -------------------------------------------------------

        // LEAVING THE FORM IS AN EVENT TOO -- the owner's overrule of the quiet
        // revert 651c8a79 argued for. The window is bounded by the exit's own
        // log line: messages are pushed at a beat's IMPACT instant, so while that line
        // is the newest one the beat being drawn is the exit's own, which is
        // what stops an enemy's swing at Shawn from satisfying this instead.
        [UnityTest]
        public IEnumerator TheRevertBeatFlashesTheActorsOwnSilhouette()
        {
            yield return StandTheFightUp();

            string ownFolder = "Characters/" + PreviewFight.ForSpell(TransformSkill).CasterId;
            var ownArt = StanceAnimationLibrary.Resolve(ownFolder, FightSession.Stances.Idle);
            Assert.IsNotNull(ownArt, ownFolder + "/idle is not on disk");

            _fight.ForceFirstAction(TransformSkill);
            yield return WaitForBusy();

            bool sawExitLine = false;
            bool flashedOnExit = false;

            // The form runs three of Shawn's turns; eight rounds is slack for
            // whatever the turn order does with a faster or slower rat.
            for (int round = 0; round < 8 && !flashedOnExit; round++)
            {
                for (int frame = 0; frame < RoundFrameBudget && _fight.IsBusy; frame++)
                {
                    if (ExitLineIsNewest())
                    {
                        sawExitLine = true;
                        foreach (var flash in PartyFlashes())
                        {
                            if (flash.enabled && flash.color.a > 0f && flash.sprite == ownArt)
                            {
                                flashedOnExit = true;
                            }
                        }
                    }

                    yield return null;
                }

                if (flashedOnExit || Shawn()?.Transformation == null) break;

                Click("Verb0");
                Click("EnemyPlate0");
                yield return null;
            }

            Assert.IsTrue(sawExitLine,
                "the transform never expired inside eight rounds, so there was no revert to look at");
            Assert.IsTrue(flashedOnExit,
                "the form ran out with no silhouette flash over the change back -- the figure popped " +
                "into his own art with nothing punctuating it");
        }

        // ---- fixture --------------------------------------------------------

        // The sentence TickTransform appends when the timer runs out. Matched
        // on its stable half rather than on the whole line, which carries the
        // transform's authored display name.
        private bool ExitLineIsNewest() => NewestLineContains("no longer");

        // Whether the newest line the fight has pushed is the one named. Beat
        // messages are pushed at a beat's IMPACT instant, so this is "the beat being
        // drawn right now is the one that said this" -- the only window
        // narrow enough to attribute a flash to a particular beat from
        // outside the controller.
        private bool NewestLineContains(string fragment)
        {
            var log = _fight.RecentLogForTest;
            if (log == null || log.Count == 0) return false;
            return log[log.Count - 1].Contains(fragment);
        }

        private CombatantState Shawn() =>
            _fight.SessionForTest?.Encounter.PlayerParty.FirstOrDefault(c => c != null);

        private IEnumerator WaitForBusy()
        {
            float armed = Time.realtimeSinceStartup;
            while (!_fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
            Assert.IsTrue(_fight.IsBusy, "nothing was ever cast -- the log says what was refused");
        }

        // SEARCHED FROM THIS FIGHT'S OWN ROOT, never globally: these fixtures
        // load the same scene repeatedly and a global lookup can hand back the
        // previous load's overlay, still holding the previous load's sprite.
        // Same rule, and the same reason, as FightFlowTests.Named.
        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, "no object named '" + name + "' in the Fight scene");
            go.GetComponent<Button>().onClick.Invoke();
        }

        // Every drawing the form folder holds, so "is the figure wearing the
        // ram" is asked of the whole kit rather than of one pose this fixture
        // guessed the beat would be in.
        private static HashSet<Sprite> FormDrawings()
        {
            var drawings = new HashSet<Sprite>();

            foreach (string stance in new[]
                     {
                         FightSession.Stances.Idle, FightSession.Stances.Attack, FightSession.Stances.Hurt,
                         FightSession.Stances.Cast, FightSession.Stances.Defeated, FormStance,
                     })
            {
                var sprite = StanceAnimationLibrary.Resolve(FormFolder, stance);
                if (sprite != null) drawings.Add(sprite);
            }

            Assert.IsNotEmpty(drawings, FormFolder + " has no drawings on disk at all");
            return drawings;
        }

        private List<Image> PartyFigures()
        {
            var found = _fight.GetComponentsInChildren<Image>(includeInactive: true)
                .Where(i => i != null && i.name.StartsWith("Party") && i.name.EndsWith("Sprite"))
                .ToList();

            Assert.IsNotEmpty(found, "the fight scene has no party stage figures");
            return found;
        }

        // Every party slot's overlay Image. Found through the components rather
        // than off the controller: FightController's arrays are internal and
        // PlayMode has no InternalsVisibleTo grant (Core/AssemblyInfo.cs).
        private List<Image> PartyFlashes()
        {
            var found = _fight.GetComponentsInChildren<StageHitFlash>(includeInactive: true)
                .Where(f => f != null && f.name.StartsWith("Party"))
                .Select(f => f.GetComponent<Image>())
                .Where(i => i != null)
                .ToList();

            Assert.IsNotEmpty(found, "the fight scene has no party hit-flash overlays");
            return found;
        }

        // The same stand-up SpellRuntimeCaptureTests uses, so a failure here
        // and a picture from tools/preview.ps1 are of the same encounter.
        private IEnumerator StandTheFightUp()
        {
            var plan = PreviewFight.ForSpell(TransformSkill);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));

            FightBeatPlayer.PlayerSpeedSource = () => 1f;

            // Loading Fight over a Fight logs AUDIT #106's teardown error;
            // tolerated across the swap and nowhere else, exactly as
            // SpellRuntimeCaptureTests tolerates it.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the fight scene has no controller");

            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the fight scene has no beat player");

            // FightBootstrap's placeholder fight is mid-beat and holds the
            // controller busy, so a forced press would be swallowed.
            _beats.Flush();
            yield return null;
            yield return null;

            var fielded = PreviewFight.EnemiesWithArt(1);
            Assert.IsNotEmpty(fielded, "no enemies in content to stand a fight up against");

            var built = FightEncounterAdapter.Build(
                new List<string> { plan.CasterId },
                fielded,
                new Domain.Rng.SeededRandom(20260909),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { TransformSkill });

            Assert.IsNotNull(built?.Session, TransformSkill + " could not be built into an encounter");

            PreviewFight.Prepare(built, plan);
            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;
        }
    }
}
