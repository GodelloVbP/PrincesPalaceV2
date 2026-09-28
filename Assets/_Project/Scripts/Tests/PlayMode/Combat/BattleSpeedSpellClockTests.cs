using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // T4 and T5: a live cast's cue fires exactly
    // once at every pace on the table, and an OLDER cast keeps ageing on the
    // pace it was born at when a later cast begins at a different one.
    //
    // BOTH TESTS DRIVE THE CLOCK IN ENGINE SECONDS, NOT AUTHORED ONES.
    // SpellRendererAndClockTests' own StepTo(authoredDelta) converts through
    // FightBeatPlayer.Scaled at the LIVE pace, which is exactly right when
    // the pace never changes mid-test and exactly wrong here: T5 changes it
    // twice while a cast is still live, and Scaled at the live pace would
    // silently retime the very thing being asserted. AdvanceEngineTo sets
    // the module's own clock directly instead, and every "when does this
    // cast's cue fire" question is answered in that same clock's absolute
    // terms, worked out from EACH cast's own captured pace.
    public class BattleSpeedSpellClockTests
    {
        private FightController _fight;
        private float _clock;

        [TearDown]
        public void Restore()
        {
            SpellPerformancePlayer.ClockOverride = null;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
        }

        private IEnumerator LoadAndBind()
        {
            yield return FightSceneFixture.LoadFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = new[]
            {
                new CombatantState("Front", false, 5000, 10, 8, 4),
                new CombatantState("Second", false, 5000, 10, 8, 4),
            };
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false, DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
        private List<CombatantState> Foes =>
            _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();

        private void HoldTheClock()
        {
            _clock = Time.time;
            SpellPerformancePlayer.ClockOverride = () => _clock;
        }

        private void AdvanceEngineTo(float targetClock)
        {
            _clock = targetClock;
            _fight.PerformancePlayerForTest.Tick(_clock);
        }

        // The pilot's own shipped presentation -- same lookup
        // SpellRuntimeCaptureTests' ShippedVfx uses, so "a real Water cast"
        // means the same content in both places.
        private static SpellPresentation ShippedVfx(string skillId)
        {
            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            Assert.IsNotNull(definition, $"'{skillId}' is not in the content database");

            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.IsTrue(vfx != null && vfx.HasArt, $"'{skillId}' resolves to a presentation with no art");
            return vfx;
        }

        private static CombatBeat WaterAt(CombatantState caster, CombatantState target) => new CombatBeat
        {
            Actor = caster,
            Target = target,
            Vfx = ShippedVfx("prismatic_orb"),
        };

        // The matrix the plan states: BeatSpeedMultiplier {1, 60} x player
        // {1/3, 2/3, 1, 4/3} -- eight casts, one scene load, each one begun,
        // held one tick short of its own cue, held one tick past it, then
        // run out to its own end.
        [UnityTest]
        public IEnumerator ACastFiresItsCueOnceAtEveryPace()
        {
            yield return LoadAndBind();

            float[] beatSpeeds = { 1f, 60f };
            float[] playerSpeeds = { 1f / 3f, 2f / 3f, 1f, 4f / 3f };

            var module = _fight.PerformancePlayerForTest;

            foreach (float beatSpeed in beatSpeeds)
            {
                FightBeatPlayer.BeatSpeedMultiplier = beatSpeed;

                foreach (float playerSpeed in playerSpeeds)
                {
                    FightBeatPlayer.PlayerSpeedSource = () => playerSpeed;
                    FightBeatPlayer.AdoptPlayerSpeed();

                    string because = $"at BeatSpeedMultiplier {beatSpeed}, player {playerSpeed}";

                    int cueCount = 0;
                    module.HitCueCrossedForTest = _ => cueCount++;

                    HoldTheClock();
                    float startClock = _clock;

                    var beat = WaterAt(Hero, Foes[0]);
                    float authoredCue = _fight.ImpactDelayFor(beat);
                    Assert.Greater(authoredCue, 0f, because + ": fixture assumption -- the pilot's cue is not at t=0");

                    float pace = FightBeatPlayer.Pace;
                    var handle = _fight.PlaySpellVfxForTest(beat);

                    float engineCue = authoredCue / pace;
                    float epsilon = Mathf.Max(engineCue * 0.05f, 0.0001f);

                    AdvanceEngineTo(startClock + engineCue - epsilon);
                    Assert.AreEqual(0, cueCount, because + ": the cue fired before its own instant");

                    AdvanceEngineTo(startClock + engineCue + epsilon);
                    Assert.AreEqual(1, cueCount, because + ": the cue did not fire exactly once at its own instant");

                    AdvanceEngineTo(startClock + engineCue * 4f + 1f);
                    Assert.IsFalse(module.IsLive(handle), because + ": the cast outlived its own end");
                    Assert.AreEqual(1, cueCount, because + ": the cue fired again after the cast ended");

                    module.HitCueCrossedForTest = null;
                }
            }
        }

        // A begins at 1/3; the source moves to 4/3 and is adopted; B begins
        // at 4/3; the source moves back to 1/3 and is adopted while A is
        // STILL LIVE. A's cue must fire once, at the instant 1/3 predicts,
        // undisturbed by either adoption; B's must fire once at the instant
        // 4/3 predicts. If Tick read the live product instead of each
        // cast's own PaceAtStart, A's age would jump the moment either
        // adoption ran and this would fire A's cue at the wrong instant, or
        // twice, or never.
        [UnityTest]
        public IEnumerator AnOlderCastKeepsItsOwnPaceAcrossAPresetChange()
        {
            yield return LoadAndBind();

            var module = _fight.PerformancePlayerForTest;
            var handleA = CastHandle.None;
            var handleB = CastHandle.None;
            int cueCountA = 0;
            int cueCountB = 0;
            module.HitCueCrossedForTest = h =>
            {
                if (handleA.IsLive && h.Equals(handleA)) cueCountA++;
                else if (handleB.IsLive && h.Equals(handleB)) cueCountB++;
            };

            HoldTheClock();

            FightBeatPlayer.PlayerSpeedSource = () => 1f / 3f;
            FightBeatPlayer.AdoptPlayerSpeed();

            var beatA = WaterAt(Hero, Foes[0]);
            float authoredCueA = _fight.ImpactDelayFor(beatA);
            float paceA = FightBeatPlayer.Pace;
            float startA = _clock;
            handleA = _fight.PlaySpellVfxForTest(beatA);
            float tCueA = startA + authoredCueA / paceA;

            // A quarter of the way to its own cue, well before it.
            AdvanceEngineTo(startA + (authoredCueA / paceA) * 0.25f);
            Assert.AreEqual(0, cueCountA, "A fired before its own cue, before any adoption happened at all");

            // Adoption 1, in the FASTER direction: changing the delegate
            // alone changes nothing (revision 3 point 2) -- AdoptPlayerSpeed
            // is what makes it stick.
            FightBeatPlayer.PlayerSpeedSource = () => 4f / 3f;
            FightBeatPlayer.AdoptPlayerSpeed();

            var beatB = WaterAt(Hero, Foes[1]);
            float authoredCueB = _fight.ImpactDelayFor(beatB);
            float paceB = FightBeatPlayer.Pace;
            float startB = _clock;
            handleB = _fight.PlaySpellVfxForTest(beatB);
            float tCueB = startB + authoredCueB / paceB;

            // Adoption 2, back to the SLOWER direction, while A is still
            // live and B has only just begun.
            FightBeatPlayer.PlayerSpeedSource = () => 1f / 3f;
            FightBeatPlayer.AdoptPlayerSpeed();

            float epsilonA = Mathf.Max((authoredCueA / paceA) * 0.02f, 0.0001f);
            AdvanceEngineTo(tCueA - epsilonA);
            Assert.AreEqual(0, cueCountA, "A's own pace was perturbed by an adoption that happened after it began");
            AdvanceEngineTo(tCueA + epsilonA);
            Assert.AreEqual(1, cueCountA, "A's cue did not fire exactly once at A's own (1/3) pace");

            float epsilonB = Mathf.Max((authoredCueB / paceB) * 0.02f, 0.0001f);
            float justBeforeB = Mathf.Max(_clock, tCueB - epsilonB);
            AdvanceEngineTo(justBeforeB);
            if (justBeforeB < tCueB) Assert.AreEqual(0, cueCountB, "B fired before its own cue");
            AdvanceEngineTo(tCueB + epsilonB);
            Assert.AreEqual(1, cueCountB, "B's cue did not fire exactly once at B's own (4/3) pace");

            // Run both out to their own ends.
            AdvanceEngineTo(Mathf.Max(tCueA, tCueB) + 10f);
            Assert.IsFalse(module.IsLive(handleA), "A outlived its own end");
            Assert.IsFalse(module.IsLive(handleB), "B outlived its own end");
            Assert.AreEqual(1, cueCountA, "A's cue fired again after A ended");
            Assert.AreEqual(1, cueCountB, "B's cue fired again after B ended");

            module.HitCueCrossedForTest = null;
        }
    }
}
