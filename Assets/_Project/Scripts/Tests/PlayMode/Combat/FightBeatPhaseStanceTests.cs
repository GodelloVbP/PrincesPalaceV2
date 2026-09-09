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
    // THE ORDER THREE DRAWINGS OF ONE BLOW ACTUALLY GO ON, and the order a
    // transformation's swap goes on relative to the blow it rides.
    //
    // Driven through FightBeatPlayer's *ForTest doors with no scene at all --
    // the shape FightBeatPlayerFixtureTests established, and for the same
    // reason it did: what is asserted here is a SEQUENCE of decisions, and
    // loading the Fight scene to watch one would make the assertion about the
    // scene rather than about the rule.
    //
    // NO REAL ART IS TOUCHED. The stance names below are strings the player
    // hands to a delegate this fixture supplies; nothing resolves them to a
    // sprite. That is deliberate: the bear's rush/overhead/slam and Shawn's
    // black-ram form were being drawn while this was written, and a pin on the
    // ORDER of the calls must not go red because a PNG has not landed yet.
    public class FightBeatPhaseStanceTests
    {
        // ---- three poses of one blow ----------------------------------------

        // BJORN'S SLAM, in the exact words the request arrived in: "he rushes
        // forward in the first frame, then he holds his hammer over his head,
        // then he slams down". Three poses, one beat, in that order -- and the
        // strike lands before the victim is posed, because SetStance is what
        // re-syncs the hit-flash silhouette and a flash shaped like the pose
        // the victim just left is worse than no flash.
        [UnityTest]
        public IEnumerator ACloseBeatPlaysApproachThenWindupThenStrikeThenIdle()
        {
            var player = NewPlayer();
            var (actor, victim) = Fixture(player, out var log);

            var beat = new CombatBeat
            {
                Actor = actor,
                Target = victim,
                Amount = 9,
                Approach = StageApproach.Close,
                ActorApproachStance = "rush",
                ActorWindupStance = "overhead",
            };
            beat.Stances[actor] = "slam";
            beat.Stances[victim] = FightSession.Stances.Hurt;

            yield return PlayOut(player, beat);

            AssertOpens(log,
                new[] { "actor:rush", "actor:overhead", "actor:slam", "victim:hurt" },
                "the slam's three poses did not play in order -- or the strike did not land before the " +
                "victim was posed, which is what re-syncs the hit-flash silhouette");
            AssertEndsIdle(log, 4);
        }

        // THE REGRESSION PIN, and the more important of the two. Every skill,
        // every enemy ability and every plain attack in the game authors no
        // phase poses, and all of them have to make exactly the calls they
        // always made: one stance at the open, one idle at the end, and
        // nothing extra in between.
        [UnityTest]
        public IEnumerator ABeatWithOnlyAStrikeStanceMakesExactlyTheCallsItAlwaysDid()
        {
            var player = NewPlayer();
            var (actor, victim) = Fixture(player, out var log);

            var beat = new CombatBeat
            {
                Actor = actor,
                Target = victim,
                Amount = 9,
                Approach = StageApproach.Close,
            };
            beat.Stances[actor] = "attack";
            beat.Stances[victim] = FightSession.Stances.Hurt;

            yield return PlayOut(player, beat);

            AssertOpens(log, new[] { "actor:attack", "victim:hurt" },
                "an unphased beat grew an extra SetStance -- every pose change repaints the whole stage, " +
                "and the impact instant is not a free place to add one");
            AssertEndsIdle(log, 2);
        }

        // A Hold has no travel, so an approach pose authored on one is ignored
        // -- documented rather than refused, because a skill's approach can be
        // edited without its poses being rewritten.
        [UnityTest]
        public IEnumerator AHoldIgnoresItsApproachPoseAndWearsItsWindupFromTheOpen()
        {
            var player = NewPlayer();
            var (actor, victim) = Fixture(player, out var log);

            var beat = new CombatBeat
            {
                Actor = actor,
                Target = victim,
                Approach = StageApproach.Hold,
                ActorApproachStance = "rush",
                ActorWindupStance = "overhead",
            };
            beat.Stances[actor] = "slam";

            yield return PlayOut(player, beat);

            CollectionAssert.AreEqual(new[] { "actor:overhead", "actor:slam", "actor:idle" }, log,
                "a rooted beat either wore a travel pose it never travelled in, or skipped its wind-up");
        }

        // ---- becoming something else ----------------------------------------

        // THE WHOLE OF REQUEST 2'S TIMING. The round has already resolved by
        // the time a beat plays, so the swap has to happen at the IMPACT
        // INSTANT rather than at the open -- otherwise the ram is on screen
        // before the flash that is supposed to hide the change.
        [UnityTest]
        public IEnumerator AFormSwapLandsAtTheImpactInstantRatherThanAtTheOpen()
        {
            var player = NewPlayer();
            var (actor, _) = Fixture(player, out var log);

            var beat = new CombatBeat
            {
                Actor = actor,
                Target = actor,
                Approach = StageApproach.Hold,
                Shake = 0.7f,
                ActorWindupStance = "victory",
            };
            beat.Stances[actor] = "victory";
            beat.Forms = new Dictionary<CombatantState, string> { [actor] = "Characters/owl" };

            yield return PlayOut(player, beat);

            // The wind-up pose and the strike carry the SAME name here, which
            // is what black_ram_mode itself authors: the FOLDER changes, not
            // the pose, so what is seen is one drawing replaced by another
            // actor's version of it. The player therefore makes no redundant
            // SetStance at impact, and the form call is the next thing after
            // the open -- which is a whole wind-up later.
            CollectionAssert.AreEqual(
                new[] { "actor:victory", "form:actor:Characters/owl", "actor:idle" }, log,
                "the form swap did not land on the impact instant");
        }

        // A round that transforms somebody has to hand the stage back to live
        // state when it ends -- which is also the only place a REVERT can
        // happen, because a transform expires at its holder's turn start and
        // that opens no beat.
        [UnityTest]
        public IEnumerator PlaybackHandsTheWornFormBackToLiveStateWhenItEnds()
        {
            var player = NewPlayer();
            var (actor, victim) = Fixture(player, out _);

            bool finished = false;
            player.Play(new[] { new CombatBeat { Actor = actor, Target = victim } }, () => finished = true);

            // COUNTED FROM HERE, not from zero: Play supersedes whatever was
            // running by calling Flush, which hands the stage back to live
            // state on its own account. This test is about the END of a
            // playback, so the supersede is the baseline rather than a hit.
            int baseline = _resyncs;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(finished, "the beat never finished playing");

            Assert.AreEqual(baseline + 1, _resyncs,
                "playback ended without putting every figure back on live Transformation state, so a " +
                "transform that expired this round would never change back");

            // And an ABANDONED round does the same, beside the formation and
            // the turn queue it already hands back.
            player.Play(new[] { new CombatBeat { Actor = actor, Target = victim } }, () => { });
            yield return null;

            int beforeFlush = _resyncs;
            player.Flush();

            Assert.AreEqual(beforeFlush + 1, _resyncs,
                "a stopped playback left the stage wearing a beat's form");
        }

        // ---- assertions ------------------------------------------------------

        private static void AssertOpens(List<string> log, string[] expected, string because)
        {
            Assert.GreaterOrEqual(log.Count, expected.Length, because + " (too few calls: " +
                string.Join(", ", log) + ")");
            CollectionAssert.AreEqual(expected, log.GetRange(0, expected.Length), because);
        }

        // THE TAIL IS A SET, NOT A SEQUENCE, and only here. The return to idle
        // walks beat.Stances, whose enumeration order is a Dictionary's own
        // business -- a pin on it would be asserting a BCL implementation
        // detail rather than a rule this project owns. Everything before it is
        // ordered on purpose and is asserted as a sequence.
        private static void AssertEndsIdle(List<string> log, int from)
        {
            var tail = log.GetRange(from, log.Count - from);
            CollectionAssert.AreEquivalent(new[] { "actor:idle", "victim:idle" }, tail,
                "the beat did not put both figures back to idle exactly once: " + string.Join(", ", tail));
        }

        // ---- fixture ---------------------------------------------------------

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private int _resyncs;

        [SetUp]
        public void Reset() => _resyncs = 0;

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        private FightBeatPlayer NewPlayer()
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);

            var player = go.AddComponent<FightBeatPlayer>();

            // Fast, so a beat's waits -- including the wind-up a phase pose
            // buys -- do not make the test wait them out. The ORDER is what is
            // being asserted, never the durations.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // A process-wide static: pinned here for the reason
            // FightBeatPacingTests pins it, so a value another fixture in the
            // same batch left behind cannot stretch these waits.
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            return player;
        }

        // Two bare slots, the shape FightBeatPlayerFixtureTests uses -- a Close
        // beat asks for both animators, so both have to be real.
        private (CombatantState actor, CombatantState victim) Fixture(
            FightBeatPlayer player, out List<string> log)
        {
            var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            _spawned.Add(stage.gameObject);

            var actorSlot = NewSlot(stage, "ActorSlot");
            var victimSlot = NewSlot(stage, "VictimSlot");

            var actor = new CombatantState("Actor", true, 300, 30, 40, 10);
            var victim = new CombatantState("Victim", false, 100, 10, 8, 4);

            var recorded = new List<string>();
            log = recorded;

            string Who(CombatantState c) => ReferenceEquals(c, actor) ? "actor" : "victim";

            player.WireStageForTest(
                combatant => ReferenceEquals(combatant, actor) ? actorSlot.rect
                    : ReferenceEquals(combatant, victim) ? victimSlot.rect : null,
                combatant => ReferenceEquals(combatant, actor) ? actorSlot.animator
                    : ReferenceEquals(combatant, victim) ? victimSlot.animator : null);

            player.WireStancesForTest(
                (combatant, stance) => recorded.Add($"{Who(combatant)}:{stance}"),
                (combatant, folder) => recorded.Add($"form:{Who(combatant)}:{folder}"),
                () => _resyncs++);

            return (actor, victim);
        }

        private static (RectTransform rect, StageActorAnimator animator) NewSlot(RectTransform stage, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(StageActorAnimator));
            go.transform.SetParent(stage, false);
            return ((RectTransform)go.transform, go.GetComponent<StageActorAnimator>());
        }

        private static IEnumerator PlayOut(FightBeatPlayer player, CombatBeat beat)
        {
            bool finished = false;
            player.Play(new[] { beat }, () => finished = true);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(finished, "the beat never finished playing");
        }
    }
}
