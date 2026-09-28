using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    // THE THIRD PROOF SPELL, AND THE CHEAPEST EVIDENCE THAT THE MODEL IS NOT
    // FOUR SLOTS WITH NEW NAMES.
    //
    // Water proves three renderer kinds across two scopes; Cinderfault proves
    // one shared cast-level sequence beside N per-target ones. Neither proves
    // the thing this does: TWO INSTANCES OF ONE RENDERER KIND, on a CASTER
    // anchor, scheduled at DIFFERENT OFFSETS, with OVERLAPPING LIFETIMES -- B
    // opens while A is 75% through. Under the four-block shape that came before
    // (one flight, one trail, one contact, one particles) a second burst on the
    // caster had nowhere to live; here it is one more entry in an array.
    //
    // FIXTURE-ONLY, AND DELIBERATELY. The spell-layers plan says
    // this composition is never authored into skills.json: no spell in the game
    // wants it, and inventing one to prove a scheduler property would put art
    // and a recipe register behind a test. The art is what already ships --
    // Resources/Vfx/impact_burst, the house's own melee burst, six frames.
    //
    // THE PATH IS A LITERAL. ContactCues.ImpactBurstPath is the constant this
    // string duplicates, and ContactCues is internal with InternalsVisibleTo
    // naming the Editor assembly only (.claude/rules/ui.md "Wiring"), so a
    // PlayMode fixture reaches the literal or reaches nothing.
    //
    // NEITHER LAYER AUTHORS sort, until OR facing, also deliberately: this is
    // the case that pins the blank-word defaults through a live cast --
    // effects, once, auto -- which SpellLayerRulesTests pins as rules and
    // nothing pinned as behaviour.
    public class SpellTwoBurstTests
    {
        private const string BurstPath = "Vfx/impact_burst";

        // ContactCues.BurstSeconds, and the offset is 75% of it: B opens with a
        // quarter of A's life left, which is the overlap this fixture is for.
        private const float BurstSeconds = 0.24f;
        private const float SecondOffset = 0.18f;

        private FightController _fight;
        private float _start;
        private float _clock;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
        }

        // THE CLOCK IS HELD AND STEPPED BY HAND, for the reason every fixture
        // in this area states: at 60x this whole cast is seven milliseconds, so
        // "cast, wait a frame, count what is drawn" asks how long the next
        // batchmode frame took rather than what the code did.
        private void HoldTheClock()
        {
            _start = Time.time;
            _clock = _start;
            SpellPerformancePlayer.ClockOverride = () => _clock;
        }

        // ABSOLUTE FROM THE CAST rather than a relative nudge, because every
        // assertion below is about one named instant of the schedule and a
        // chain of deltas would let a rounding error walk between them.
        private void StepTo(float castSeconds)
        {
            _clock = _start + FightBeatPlayer.Scaled(castSeconds);
            _fight.PerformancePlayerForTest.Tick(_clock);
        }

        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

        private CombatantState Foe => _fight.SessionForTest.Encounter.Enemies.First(e => e != null && e.IsAlive);

        private List<SpellVfxPlayer> Effects =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => !_fight.GroundVfxPlayersForTest.Contains(p))
                .ToList();

        private int Drawn => Effects.Count(p => p.Image != null && p.Image.enabled);

        // Section 8's composition, field for field. Both layers are the same
        // renderer kind on the same anchor and differ in exactly two things:
        // when they open and how big they are.
        private CombatBeat TwoBurstFrom(CombatantState caster, CombatantState target) => new CombatBeat
        {
            Actor = caster,
            Target = target,
            Vfx = new SpellPresentation
            {
                layerFormat = 1,
                layers = new[]
                {
                    new SpellLayer
                    {
                        render = "sprite",
                        place = "caster-centre",
                        at = "release",
                        offset = 0f,
                        path = BurstPath,
                        seconds = BurstSeconds,
                        scale = 1.00f,
                    },
                    new SpellLayer
                    {
                        render = "sprite",
                        place = "caster-centre",
                        at = "release",
                        offset = SecondOffset,
                        path = BurstPath,
                        seconds = BurstSeconds,
                        scale = 1.35f,
                    },
                },
            },
        };

        // ---- the proof -----------------------------------------------------------

        [UnityTest]
        public IEnumerator TwoBurstsOnOneCasterOverlapOnDistinctMembersAndGiveThemBothBack()
        {
            yield return LoadFight();

            var module = _fight.PerformancePlayerForTest;

            // THE WHOLE BAND, from the constant rather than from a reading
            // taken before the cast: the owner arrays are built on the first
            // Begin, so "free before" is zero for a reason that has nothing to
            // do with what is owned.
            int band = FightHudSpec.SpellLayerRenderers;
            Assert.GreaterOrEqual(band, 2,
                "the effects band reserves fewer than two members, so nothing below would be " +
                "measuring an overlap");

            HoldTheClock();
            var cast = _fight.PlaySpellVfxForTest(TwoBurstFrom(Hero, Foe));
            Assert.IsTrue(module.IsLive(cast), "the two-burst obtained nothing at all");

            // ---- t = 0.20: both alive, both drawn, different members ----------

            StepTo(0.20f);

            int first = module.MemberFor(cast, 0);
            int second = module.MemberFor(cast, 1);

            Assert.GreaterOrEqual(first, 0, "the first burst holds no renderer 0.20s in");
            Assert.GreaterOrEqual(second, 0,
                "the second burst never opened. It is scheduled at offset " + SecondOffset +
                "s, which is a schedule cue and not an event on the first layer");
            Assert.AreNotEqual(first, second,
                "both bursts are drawing the same pool member, so one instance of a renderer kind is " +
                "being reused as two -- which is the restart this design removes");

            Assert.IsFalse(module.DrawsInGroundBand(cast, 0),
                "an unauthored sort must default to effects, not to the band behind the racks");
            Assert.IsFalse(module.DrawsInGroundBand(cast, 1));

            Assert.AreEqual(2, Drawn, "two overlapping bursts are two drawings");

            // AUTHORED ORDER IS DRAW ORDER, and with an unfragmented pool that
            // means the later layer takes the higher member index and draws over
            // the earlier one. Nothing authors a z-order; there is nothing to
            // keep in sync.
            Assert.Less(first, second,
                "the second layer took a lower member than the first, so authored order stopped being " +
                "draw order");

            // ---- t = 0.26: A has ended, B is still going ----------------------

            StepTo(0.26f);

            Assert.AreEqual(-1, module.MemberFor(cast, 0),
                "the first burst is 0.02s past its 0.24s and still holds a renderer");
            Assert.AreEqual(second, module.MemberFor(cast, 1),
                "the second burst lost or changed its renderer when the first one ended");
            Assert.IsTrue(module.IsLive(cast), "the cast was released while one of its layers was still drawing");
            Assert.AreEqual(1, Drawn, "the ended burst is still on screen, or the living one is not");

            Assert.AreEqual(band - 1, module.FreeEffectRenderers,
                "the first burst's member did not go back to the band the instant its layer ended");

            // ---- t = 0.43: both ended, the pool comes back whole --------------

            StepTo(SecondOffset + BurstSeconds + 0.01f);

            Assert.IsFalse(module.IsLive(cast), "the cast outlived its last layer");
            Assert.AreEqual(0, Drawn, "a finished cast left a burst frozen on the stage");
            Assert.AreEqual(band, module.FreeEffectRenderers,
                "the effects band did not come back whole -- a member is owned by a cast that is over");
        }

        // WHAT THE OVERLAP COSTS THE POOL, stated separately because it is the
        // number the reservation is sized against rather than a property of the
        // schedule: two instances of one layer kind are two members, not one
        // member drawn twice, and the band has to hold both at once.
        [UnityTest]
        public IEnumerator TheOverlapHoldsTwoMembersAtOnceAndNeitherIsTheOthers()
        {
            yield return LoadFight();

            var module = _fight.PerformancePlayerForTest;

            HoldTheClock();
            var cast = _fight.PlaySpellVfxForTest(TwoBurstFrom(Hero, Foe));

            StepTo(0.20f);

            Assert.AreEqual(FightHudSpec.SpellLayerRenderers - 2, module.FreeEffectRenderers,
                "one cast of two overlapping sprite layers should hold exactly two members of the " +
                "effects band while both are alive");

            // AND THEY ARE TWO DIFFERENT DRAWINGS, not one image counted twice:
            // the second burst is authored at scale 1.35, so its box is wider
            // than the first's by that ratio and nothing else.
            var boxes = new[] { module.MemberFor(cast, 0), module.MemberFor(cast, 1) }
                .Select(m => Effects[m].Image.rectTransform.sizeDelta.x)
                .ToList();

            Assert.AreEqual(boxes[0] * 1.35f, boxes[1], 0.5f,
                "the second layer's scale did not reach its box, so the two instances are not " +
                "independently placed");

            module.Cancel(cast);
        }
    }
}
