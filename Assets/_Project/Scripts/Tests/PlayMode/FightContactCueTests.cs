using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // WHICH BEATS GET THE HOUSE'S OWN CONTACT EFFECTS, and which must not.
    //
    // The attack graphic and the impact burst exist for one class of blow: a
    // melee swing by an actor whose art is a single drawing, which therefore
    // has nothing of its own to show at the moment of contact. Every other
    // beat in the game already draws something -- a spell brings its own
    // sheet, an animated actor brings its own frames -- and adding a second
    // effect on top is two things arguing about what just happened.
    //
    // NOTHING HERE KNOWS WHO IS SWINGING. The gate is the class of beat
    // (approach, whether the stance has motion, what it hit), never the Bog
    // Witch by name, and these are the assertions that keep it that way: the
    // fixture builds combatants with no content behind them at all.
    //
    // Deterministic contracts only -- the effect FIRES or it does not. Where
    // the graphic lands and how it looks are perception questions and belong
    // to a captured pilot, not to a sampled mid-beat frame.
    public class FightContactCueTests
    {
        [UnityTest]
        public IEnumerator AStillDrawingSwingFiresTheContactEffectsExactlyOnce()
        {
            int fired = 0;
            yield return PlayOne(Swing(), FlatStance(), b => fired++);

            Assert.AreEqual(1, fired,
                "a flat-art melee blow drew nothing at the moment it landed, which is the whole " +
                "defect the contact effects exist for");
        }

        [UnityTest]
        public IEnumerator AnActorThatHoldsPositionGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Approach = StageApproach.Hold;

            int fired = 0;
            yield return PlayOne(beat, FlatStance(), b => fired++);

            Assert.AreEqual(0, fired,
                "a rooted blow put a slash arc on its target -- nothing crossed the stage, so " +
                "there is no sweep to draw");
        }

        [UnityTest]
        public IEnumerator ABlowThatDealtNothingGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Amount = 0;

            int fired = 0;
            yield return PlayOne(beat, FlatStance(), b => fired++);

            Assert.AreEqual(0, fired,
                "a beat that dealt no damage still burst on contact; a miss says so with its own " +
                "popup and has no contact to punctuate");
        }

        [UnityTest]
        public IEnumerator AHealGetsNoContactEffects()
        {
            var beat = Swing();
            beat.IsHealing = true;

            int fired = 0;
            yield return PlayOne(beat, FlatStance(), b => fired++);

            Assert.AreEqual(0, fired,
                "a heal landed a slash arc on whoever it mended");
        }

        [UnityTest]
        public IEnumerator ABeatThatBringsItsOwnSpellGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Vfx.path = "Spells/lightning_bolt";

            int fired = 0;
            yield return PlayOne(beat, FlatStance(), b => fired++);

            Assert.AreEqual(0, fired,
                "a cast played its own sheet AND the house's default one, which reads as two " +
                "unrelated effects on one blow");
        }

        [UnityTest]
        public IEnumerator AnAnimatedActorGetsNoContactEffects()
        {
            int fired = 0;
            yield return PlayOne(Swing(), AnimatedStance(), b => fired++);

            Assert.AreEqual(0, fired,
                "an actor with real wind-up frames got the flat-art treatment on top of them; " +
                "multi-frame and rig actors have to stay exactly as they were");
        }

        // ---- fixture -------------------------------------------------------------

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        // A bare player, exactly as FightBeatPacingTests builds one: no scene,
        // no slots, no art. Only PlaybackFor and PlayContactFx are wired,
        // because those two are the whole of what is under test -- every other
        // delegate stays null and the player has to cope, which it must
        // anyway (the controller wires them one screen at a time).
        private static CombatantState Fighter(string name, bool playerSide) =>
            new CombatantState(name, playerSide, 30, 10, 5, 5);

        // A one-frame pose: what every flat-art actor on the roster wears, and
        // the case StaticStancePlayback wraps.
        private static StanceAnimation FlatStance() =>
            new StanceAnimation(new Sprite[] { null }, 0.08f, 1, 1);

        // Two frames is enough to be "animated" as far as HasMotion is
        // concerned, which is the only thing the wrap gate reads.
        private static StanceAnimation AnimatedStance() =>
            new StanceAnimation(new Sprite[] { null, null }, 0.08f, 1, 1);

        private static CombatBeat Swing()
        {
            var beat = new CombatBeat
            {
                Actor = Fighter("Attacker", true),
                Target = Fighter("Victim", false),
                Amount = 7,
                Approach = StageApproach.Lunge,
            };

            beat.Stances[beat.Actor] = FightSession.Stances.Attack;
            return beat;
        }

        private IEnumerator PlayOne(CombatBeat beat, StanceAnimation stance,
                                    System.Action<CombatBeat> onContactFx)
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);

            var player = go.AddComponent<FightBeatPlayer>();

            // Fast, so a beat's waits do not make the test wait them out --
            // the same seam every other beat test uses.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // Through the public seam rather than by assignment: the
            // delegates are internal and Core's InternalsVisibleTo names the
            // Editor assembly only. See WirePlaybackForTest.
            player.WirePlaybackForTest(
                (who, pose, abandon) => new FrameStancePlayback(who, stance, (a, f) => { }, abandon),
                b => onContactFx(b));

            bool finished = false;
            player.Play(new List<CombatBeat> { beat }, () => finished = true);

            // Polled to completion rather than slept for a guessed duration:
            // the beat's own length is what this file is deliberately NOT
            // asserting on, and a fixed wait would couple these to it.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(finished, "the beat never finished, so the count below means nothing");
        }
    }
}
