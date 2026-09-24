using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // OWNERSHIP IS THE CAST, and these are the five things that makes true.
    //
    // What this replaced handed out pool members by POSITION IN THE STRUCK
    // WALK, so every cast started again at member 0 and a second cast on a live
    // member stopped its coroutine and restarted it -- there is a comment in
    // the renderer defending that restart. Everything below is a case that
    // behaviour got wrong and nothing could see, because "two effects on one
    // target" and "one effect drawn twice" look the same in a still.
    //
    // THE CLOCK IS HELD AT THE CAST for every one of them, for the reason
    // SpellVfxTests spells out: at this fixture's 60x a cinderfault's whole
    // budget is 13ms and a batchmode frame after a scene load was measured at
    // 24ms and 57ms, so "cast, wait a frame, count what is drawn" asks whether
    // the next frame came back in time rather than what the code did.
    public class SpellCastOwnershipTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            // THE SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene): each test
            // rebinds over it, and this stops what a rebind does not.
            FightSceneFixture.QuietForReuse(_fight);
            SharedScene.AfterTest();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
        }

        private static void HoldTheClockAtTheCast()
        {
            float instant = Time.time;
            SpellPerformancePlayer.ClockOverride = () => instant;
        }

        private static void RunTheClockPastTheEnd()
        {
            float past = Time.time + 600f;
            SpellPerformancePlayer.ClockOverride = () => past;
        }

        private IEnumerator LoadFight(int enemyCount = 3)
        {
            yield return SharedScene.EnsureFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);
            Assert.IsNotNull(_beats, "the Fight scene has no FightBeatPlayer");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = Enumerable.Range(0, Mathf.Max(1, enemyCount))
                .Select(i => new CombatantState(i == 0 ? "Front" : $"Foe{i}", false, 5000, 10, 8, 4))
                .ToArray();
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = PlayModeSparkFixture.Kit();
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

        private List<CombatantState> Foes =>
            _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();

        private CombatBeat FlareAt(CombatantState target) => new CombatBeat
        {
            Actor = Hero,
            Target = target,
            Vfx = new SpellPresentation
            {
                path = "Spells/frost_flare",
                seconds = 0.52f,
                impactFrame = 5,
                impactX = 0.5f,
                impactY = 0.18f,
            },
        };

        private IReadOnlyList<SpellVfxPlayer> Effects =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => !_fight.GroundVfxPlayersForTest.Contains(p))
                .ToList();

        private int Drawn => Effects.Count(p => p.Image != null && p.Image.enabled);

        // ---- a tail outlives the round that cast it ------------------------------

        // BRIEF CHANGE 4, expressed against what the code actually does. There
        // never was a beat-END flush; what there was is Flush running from
        // Play, so a tail died the instant the NEXT round's playback began.
        // Combat completion and visual completion are distinct, and a droplet
        // still falling from the last blow is not the next beat's mess.
        [UnityTest]
        public IEnumerator ATailKeepsDrawingWhenTheNextRoundsPlaybackBegins()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            var cast = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            yield return null;

            Assert.IsTrue(_fight.PerformancePlayerForTest.IsLive(cast), "nothing was drawing to begin with");
            Assert.AreEqual(1, Drawn);

            // The next round starting, which is the only thing that ever
            // stopped a living tail.
            _beats.Play(new List<CombatBeat>(), () => { });
            yield return null;

            Assert.IsTrue(_fight.PerformancePlayerForTest.IsLive(cast),
                "the next round's playback killed a tail that had not finished");
            Assert.AreEqual(1, Drawn, "the tail stopped being drawn when the next round opened");
        }

        // ---- two casts on one target ---------------------------------------------

        // THE BRIEF'S "CONCURRENT EFFECTS ON THE SAME TARGET MUST COEXIST".
        // The renderer used to StopCoroutine whatever the member was doing and
        // start again, with a comment defending it -- so the second cast on one
        // rat did not overlap the first, it replaced it.
        [UnityTest]
        public IEnumerator TwoCastsOnOneTargetHoldDifferentRenderersAndBothKeepDrawing()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();

            var first = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            var second = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            yield return null;

            var module = _fight.PerformancePlayerForTest;

            Assert.IsTrue(module.IsLive(first));
            Assert.IsTrue(module.IsLive(second));

            int firstMember = module.MemberFor(first, 0);
            int secondMember = module.MemberFor(second, 0);

            Assert.GreaterOrEqual(firstMember, 0, "the first cast obtained no renderer at all");
            Assert.GreaterOrEqual(secondMember, 0, "the second cast obtained no renderer at all");
            Assert.AreNotEqual(firstMember, secondMember,
                "the second cast took the renderer the first was still using, which is the restart this " +
                "design removes");

            Assert.AreEqual(2, Drawn, "two casts on one target are two drawings");
        }

        // ---- cancellation is per cast and visual only ----------------------------

        [UnityTest]
        public IEnumerator CancellingOneCastMidFlightLeavesTheOtherDrawing()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();

            var doomed = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            var survivor = _fight.PlaySpellVfxForTest(FlareAt(Foes[1]));
            yield return null;

            var module = _fight.PerformancePlayerForTest;
            int survivorMember = module.MemberFor(survivor, 0);

            module.Cancel(doomed);
            yield return null;

            Assert.IsFalse(module.IsLive(doomed), "the cancelled cast still owns something");
            Assert.IsTrue(module.IsLive(survivor), "cancelling one cast tore down the other");
            Assert.AreEqual(survivorMember, module.MemberFor(survivor, 0),
                "the survivor's renderer moved, so cancellation reached across casts");
            Assert.AreEqual(1, Drawn);
        }

        // A HANDLE KEPT PAST ITS CAST ADDRESSES NOTHING. Without the generation
        // counter it would address whatever took the slot next -- a cancel
        // aimed at a spell that finished three beats ago tearing down the one
        // drawing now.
        [UnityTest]
        public IEnumerator AStaleHandleCannotCancelTheCastThatTookItsSlot()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            var stale = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            yield return null;

            var module = _fight.PerformancePlayerForTest;
            module.Cancel(stale);

            var reused = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            yield return null;

            module.Cancel(stale);

            Assert.IsTrue(module.IsLive(reused),
                "a handle from a finished cast reached the cast that took its slot");
            Assert.AreEqual(1, Drawn);
        }

        // ---- the target dies mid-flight ------------------------------------------

        // THE SLOT SURVIVES THE DEATH -- StageDeathFade fades the sprite -- and
        // a layer that sampled its anchor once lands where the target STOOD
        // when the cast began, dead or not. What must never happen is the
        // effect teleporting to the pool's origin, which is what a null-anchor
        // default would do.
        [UnityTest]
        public IEnumerator AnEffectWhoseTargetDiesMidFlightStaysWhereTheBlowWasAimed()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            var victim = Foes[0];
            var cast = _fight.PlaySpellVfxForTest(FlareAt(victim));
            yield return null;

            var module = _fight.PerformancePlayerForTest;
            int member = module.MemberFor(cast, 0);
            Assert.GreaterOrEqual(member, 0);

            var drawnAt = Effects[member].Image.rectTransform.anchoredPosition;
            Assert.AreNotEqual(Vector2.zero, drawnAt, "the effect was already at the pool's origin");

            victim.CurrentHealth = 0;
            Assert.IsFalse(victim.IsAlive, "the fixture failed to kill the target");
            yield return null;
            yield return null;

            Assert.AreEqual(drawnAt.x, Effects[member].Image.rectTransform.anchoredPosition.x, 0.5f,
                "the effect moved when its target died");
            Assert.AreEqual(drawnAt.y, Effects[member].Image.rectTransform.anchoredPosition.y, 0.5f,
                "the effect moved when its target died");
        }

        // ---- nothing survives teardown -------------------------------------------

        [UnityTest]
        public IEnumerator NothingSurvivesEndFight()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            var first = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            var second = _fight.PlaySpellVfxForTest(FlareAt(Foes[1]));
            yield return null;

            Assert.AreEqual(2, Drawn, "nothing was drawing, so this would prove nothing");

            _beats.EndFight();
            yield return null;

            var module = _fight.PerformancePlayerForTest;
            Assert.IsFalse(module.IsLive(first));
            Assert.IsFalse(module.IsLive(second));
            Assert.AreEqual(0, Drawn, "an abandoned fight left an effect frozen over an empty stage");
            Assert.IsFalse(_fight.GroundVfxPlayerForTest.Image.enabled);
        }

        // A CAST RELEASES ITS OWN RENDERERS WHEN IT ENDS, with nothing asked to
        // clean up after it. The clock is run past every lifetime this fixture
        // can author, so the next tick of the module is the one that reclaims.
        [UnityTest]
        public IEnumerator ACastReleasesEverythingItHeldOnceItsLastLayerHasEnded()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            var cast = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            yield return null;

            Assert.AreEqual(1, Drawn);

            RunTheClockPastTheEnd();
            yield return null;
            yield return null;

            Assert.IsFalse(_fight.PerformancePlayerForTest.IsLive(cast));
            Assert.AreEqual(0, Drawn, "a finished cast is still holding a renderer");
        }

        // ---- the M0 baseline, read back through the new path ---------------------

        // THE ONE NUMBER THAT WOULD SHOW A RETIMING. Every shipped spell now
        // reaches the screen through SpellPerformance and the adapter, and the
        // whole value of that commit is that nothing changed -- so the impact
        // delay of each is pinned here as the literal seconds it was before,
        // measured through the live controller rather than through the
        // arithmetic (SpellBaselineTimingTests pins that half in EditMode).
        [UnityTest]
        public IEnumerator EveryBaselineSpellLandsItsBlowAtExactlyTheSecondItAlwaysDid()
        {
            yield return LoadFight();

            var cases = new (string Id, SpellPresentation Vfx, float Delay)[]
            {
                ("lightning_bolt", new SpellPresentation
                {
                    path = "Spells/lightning_bolt", seconds = 0.52f, impactFrame = 5,
                }, 0.28888889f),
                ("frost_flare", new SpellPresentation
                {
                    path = "Spells/frost_flare", seconds = 0.52f, impactFrame = 5,
                }, 0.28888889f),
                ("mud_burst", new SpellPresentation
                {
                    path = "Spells/mud_burst", seconds = 0.65f, impactFrame = 13, departFrame = 9,
                    anchor = "travel-centre",
                }, 0.325f),
                ("cinderfault", new SpellPresentation
                {
                    path = "Spells/cinderfault_eruption", seconds = 0.78f, impactFrame = 5,
                    groundPath = "Spells/cinderfault_ground", groundImpactY = 0.063f,
                }, 0.43333334f),

                // The pilot, which authors nothing at all -- so its blow lands
                // as the beat opens, exactly as a plain swing's does.
                ("prismatic_orb", new SpellPresentation(), 0f),
            };

            foreach (var (id, vfx, delay) in cases)
            {
                var beat = new CombatBeat { Actor = Hero, Target = Foes[0], Vfx = vfx };
                Assert.AreEqual(delay, _fight.ImpactDelayFor(beat), 1e-5f,
                    $"'{id}' lands its blow at a different instant than it did before every spell was " +
                    "routed through the layered path");
            }
        }
    }
}
