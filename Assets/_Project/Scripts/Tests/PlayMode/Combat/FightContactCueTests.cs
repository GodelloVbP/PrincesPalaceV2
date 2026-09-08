using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
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
    // melee swing that crosses the stage. Every actor's art is a single
    // drawing, so nothing is drawn at the moment of contact unless the house
    // draws it -- and every OTHER kind of beat already draws something. A
    // spell brings its own sheet; a rooted cast never crosses at all. Adding
    // an arc on top of either is two things arguing about what just happened.
    //
    // NOTHING HERE KNOWS WHO IS SWINGING. The gate is the class of beat
    // (approach, what it hit, whether it landed anything), never the Bog Witch
    // by name, and these are the assertions that keep it that way: the fixture
    // builds combatants with no content behind them at all.
    //
    // Deterministic contracts only -- the effect FIRES or it does not. Where
    // the graphic lands and how it looks are perception questions and belong
    // to a captured pilot, not to a sampled mid-beat frame.
    public class FightContactCueTests
    {
        [UnityTest]
        public IEnumerator ASwingFiresTheContactEffectsExactlyOnce()
        {
            int fired = 0;
            yield return PlayOne(Swing(), b => fired++);

            Assert.AreEqual(1, fired,
                "a melee blow drew nothing at the moment it landed, which is the whole defect the " +
                "contact effects exist for");
        }

        [UnityTest]
        public IEnumerator AnActorThatHoldsPositionGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Approach = StageApproach.Hold;

            int fired = 0;
            yield return PlayOne(beat, b => fired++);

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
            yield return PlayOne(beat, b => fired++);

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
            yield return PlayOne(beat, b => fired++);

            Assert.AreEqual(0, fired,
                "a heal landed a slash arc on whoever it mended");
        }

        [UnityTest]
        public IEnumerator ABeatThatBringsItsOwnSpellGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Vfx.path = "Spells/lightning_bolt";

            int fired = 0;
            yield return PlayOne(beat, b => fired++);

            Assert.AreEqual(0, fired,
                "a cast played its own sheet AND the house's default one, which reads as two " +
                "unrelated effects on one blow");
        }

        // THE SAME RULE FOR A LAYERED CAST, which says it brings its own art a
        // different way. A layered block authors no `path` -- the rules refuse
        // a block that authors both -- so a gate reading `path` alone drew the
        // house's slash arc over the Water pilot's own splash.
        [UnityTest]
        public IEnumerator ABeatThatBringsItsOwnLAYEREDSpellGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Vfx.path = "";
            beat.Vfx.layerFormat = 1;
            beat.Vfx.layers = new[]
            {
                new SpellLayer
                {
                    render = "sprite",
                    place = "target-centre",
                    at = "release",
                    path = "Spells/lightning_bolt",
                    seconds = 0.4f,
                },
            };

            int fired = 0;
            yield return PlayOne(beat, b => fired++);

            Assert.AreEqual(0, fired,
                "a layered cast played its own layers AND the house's default contact effects, which " +
                "reads as two unrelated effects on one blow -- the same complaint the pre-layer case " +
                "above already refuses");
        }

        [UnityTest]
        public IEnumerator ABlowAgainstItselfGetsNoContactEffects()
        {
            var beat = Swing();
            beat.Target = beat.Actor;

            int fired = 0;
            yield return PlayOne(beat, b => fired++);

            Assert.AreEqual(0, fired,
                "nothing crosses to a self-targeted beat, so there is no sweep to draw -- and the " +
                "wind-up would be waiting out a travel that never happens");
        }

        // ---- a charge gets the same contact effects a lunge does, once its travel completes --
        //
        // AUDIT.md #59: a flat-art Charge fired these on the very first frame,
        // before the charger had crossed the stage at all. FightBeatPlayer now
        // waits out the charge's own out-tween first -- see IsStaticCharge and
        // FightBeatPacingTests.AChargeWaitsOutItsOwnTravelBeforeTheImpactInstant
        // for the wall-clock pin on HOW LONG; this only pins that the effect
        // fires exactly once, and not on the opening frame.
        [UnityTest]
        public IEnumerator AChargeFiresTheContactEffectsExactlyOnceAndNotOnTheOpeningFrame()
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);
            var player = go.AddComponent<FightBeatPlayer>();

            // Fast, same seam every other test in this file uses -- polled to
            // completion below rather than slept for a guessed duration, so
            // this cannot become a test that samples one mid-beat frame.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            int fired = 0;
            int frameFired = -1;
            int frame = 0;
            player.WireContactFxForTest(b => { fired++; if (frameFired < 0) frameFired = frame; });

            bool finished = false;
            player.Play(new List<CombatBeat> { Charge() }, () => finished = true);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!finished && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                frame++;
            }

            Assert.IsTrue(finished, "the charge beat never finished, so the counts below mean nothing");
            Assert.AreEqual(1, fired,
                "a charge landed the house's contact effects more than once, or not at all");
            Assert.Greater(frameFired, 0,
                "a charge's contact effect fired on the very frame the beat opened -- before the " +
                "charger had crossed the stage, which is the defect AUDIT.md #59 records");
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
        // no slots, no art. Only PlayContactFx is wired, because it is the
        // whole of what is under test -- every other delegate stays null and
        // the player has to cope, which it must anyway (the controller wires
        // them one screen at a time).
        private static CombatantState Fighter(string name, bool playerSide) =>
            new CombatantState(name, playerSide, 30, 10, 5, 5);

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

        private static CombatBeat Charge()
        {
            var beat = new CombatBeat
            {
                Actor = Fighter("Charger", true),
                Target = Fighter("Victim", false),
                Amount = 7,
                Approach = StageApproach.Charge,
            };

            beat.Stances[beat.Actor] = FightSession.Stances.Attack;
            return beat;
        }

        private IEnumerator PlayOne(CombatBeat beat, System.Action<CombatBeat> onContactFx)
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);

            var player = go.AddComponent<FightBeatPlayer>();

            // Fast, so a beat's waits do not make the test wait them out --
            // the same seam every other beat test uses.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // Through the public seam rather than by assignment: the
            // delegate is internal and Core's InternalsVisibleTo names the
            // Editor assembly only. See WireContactFxForTest.
            player.WireContactFxForTest(b => onContactFx(b));

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
