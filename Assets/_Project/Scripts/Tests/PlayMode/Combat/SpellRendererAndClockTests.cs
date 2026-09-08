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
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE RENDERERS, THE CLOCK AND THE POOLS -- the four things that only
    // become answerable once the module owns all three.
    //
    // Every one of these was unaskable before. A coroutine per cast had no
    // instant to be asked about, so "what is drawn at the impact" was a race
    // against however long the next frame took; a renderer reading
    // realtimeSinceStartup could not be frozen by a pause; and a pool handed
    // out by position in a walk had no notion of a member being owned.
    public class SpellRendererAndClockTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;
        private float _clock;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
            Time.timeScale = 1f;
        }

        // The module's own clock, held and stepped by hand. Everything below
        // that asks "what is on screen at t" needs t to be a number rather than
        // whenever the next frame arrived.
        private void HoldTheClock()
        {
            _clock = Time.time;
            SpellPerformancePlayer.ClockOverride = () => _clock;
        }

        private void StepTo(float castSeconds)
        {
            _clock += FightBeatPlayer.Scaled(castSeconds);
            _fight.PerformancePlayerForTest.Tick(_clock);
        }

        private IEnumerator LoadFight(int enemyCount = 3)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = Enumerable.Range(0, Mathf.Max(1, enemyCount))
                .Select(i => new CombatantState(i == 0 ? "Front" : $"Foe{i}", false, 5000, 10, 8, 4))
                .ToArray();
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

        private List<CombatantState> Foes =>
            _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();

        private List<SpellVfxPlayer> Effects =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => !_fight.GroundVfxPlayersForTest.Contains(p))
                .ToList();

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

        private CombatBeat CinderfaultOn(List<CombatantState> struck) => new CombatBeat
        {
            Actor = Hero,
            Target = struck[0],
            SplashTargets = struck.Skip(1).ToList(),
            Vfx = new SpellPresentation
            {
                path = "Spells/cinderfault_eruption",
                seconds = 0.78f,
                impactFrame = 5,
                impactX = 0.5f,
                impactY = 0.129f,
                groundPath = "Spells/cinderfault_ground",
                groundImpactY = 0.063f,
            },
        };

        // ---- the clock -----------------------------------------------------------

        // THE DEFECT THE CLOCK MOVE FIXES, as an assertion. The renderer used
        // to read realtimeSinceStartup while the beat waited on WaitForSeconds,
        // which is scaled engine time -- so opening the system menu mid-cast
        // froze the beat and FAST-FORWARDED the spell: the animation ran on
        // wall time and was over by the time the player resumed.
        [UnityTest]
        public IEnumerator APausedFightFreezesItsSpellsAndResumesThemWhereTheyStopped()
        {
            yield return LoadFight();

            // The real clock this time -- the point is that Time.time is what
            // timeScale stops, so an override would test the override.
            _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            yield return null;

            var image = Effects.First(p => p.Image != null && p.Image.enabled).Image;
            var frozen = image.sprite;
            Assert.IsNotNull(frozen, "nothing was drawing, so a pause proves nothing");

            Time.timeScale = 0f;
            yield return null;
            yield return null;
            yield return null;

            Assert.AreSame(frozen, image.sprite,
                "the spell kept advancing while the fight was paused -- the module is not on Time.time");
            Assert.IsTrue(image.enabled, "the paused spell stopped being drawn rather than freezing");
        }

        // AUTHORED SECONDS BECOME ENGINE SECONDS EXACTLY ONCE, through the same
        // conversion the beat's own waits go through. A schedule holding
        // authored seconds against an engine clock is 60x out under this
        // fixture's own speed-up, and it would look like a spell that never
        // draws rather than like a units bug.
        [UnityTest]
        public IEnumerator ASpellAndItsBeatCrossTheHitCueOnTheSameFrame()
        {
            yield return LoadFight();

            var beat = FlareAt(Foes[0]);
            var performance = SpellPerformance.Resolve(beat.Vfx, 1,
                path => Effects[0].Frames(path)?.Length ?? 0);

            foreach (float multiplier in new[] { 1f, 60f })
            {
                FightBeatPlayer.BeatSpeedMultiplier = multiplier;

                // The beat waits Scaled(ImpactDelayFor); the module converts
                // the same authored number through the same function. An
                // equality on two floats, not a tolerance: they are the same
                // arithmetic or they are not.
                Assert.AreEqual(FightBeatPlayer.Scaled(_fight.ImpactDelayFor(beat)),
                    FightBeatPlayer.Scaled(_fight.PerformancePlayerForTest.HitCueSeconds(performance)),
                    $"the beat and the spell disagree about the cue at {multiplier}x");

                // And the round trip through the module's own inverse returns
                // the authored seconds it started from, which is what makes a
                // held clock mean the same thing at both speeds.
                Assert.AreEqual(performance.HitCueSeconds,
                    FightBeatPlayer.Unscaled(FightBeatPlayer.Scaled(performance.HitCueSeconds)), 1e-5f);
            }
        }

        // ---- the pools -----------------------------------------------------------

        // THE PRIORITY THE OVERFLOW POLICY STATES, and the one assertion that
        // matters in it: an exhausted band costs a PICTURE. The cue is
        // dispatched by the schedule and never by a renderer, so it fires at
        // its authored instant whether or not anything could be drawn.
        [UnityTest]
        public IEnumerator ACastThatCanObtainNoGroundRendererStillLandsItsBlowOnTime()
        {
            yield return LoadFight();

            HoldTheClock();

            // Fill the ground band. Each of these is a real cast holding its
            // own member, which is what makes the next one genuinely unable to
            // obtain one rather than artificially blocked.
            for (int i = 0; i < FightHudSpec.SpellGroundRenderers; i++)
            {
                _fight.PlaySpellVfxForTest(CinderfaultOn(new List<CombatantState> { Foes[0] }));
            }

            Assert.AreEqual(0, _fight.PerformancePlayerForTest.FreeGroundRenderers,
                "the band was not filled, so the next cast is not the exhaustion case");

            int droppedBefore = _fight.PerformancePlayerForTest.DroppedLayers;
            var denied = CinderfaultOn(Foes);

            // The cue is a property of the presentation and of nothing else, so
            // it is the same number before the cast as after it.
            float cue = _fight.ImpactDelayFor(denied);
            _fight.PlaySpellVfxForTest(denied);
            yield return null;

            Assert.AreEqual(droppedBefore + 1, _fight.PerformancePlayerForTest.DroppedLayers,
                "the denied ground layer was not reported");
            Assert.AreEqual(cue, _fight.ImpactDelayFor(denied), 1e-6f,
                "the blow moved because a picture could not be drawn");
            Assert.AreEqual(0.43333334f, cue, 1e-5f,
                "and it is still the instant cinderfault has always landed on");
        }

        // TWO CINDERFAULTS BACK TO BACK, the second opening while the first
        // fault is still cooling. It gets its OWN member: a cast taking over a
        // live one is the restart per-cast ownership exists to remove, and
        // "the fault restarted" and "there are two faults" look identical in a
        // still.
        [UnityTest]
        public IEnumerator ASecondFaultGetsItsOwnMemberRatherThanSupersedingTheFirst()
        {
            yield return LoadFight();

            HoldTheClock();

            var first = _fight.PlaySpellVfxForTest(CinderfaultOn(new List<CombatantState> { Foes[0] }));
            StepTo(0.2f);

            var firstFault = _fight.GroundVfxPlayersForTest
                .First(p => p.Image != null && p.Image.enabled);
            var frameAt02 = firstFault.Image.sprite;

            var second = _fight.PlaySpellVfxForTest(CinderfaultOn(Foes));
            StepTo(0.2f);

            var module = _fight.PerformancePlayerForTest;
            int firstGround = GroundMemberOf(module, first);
            int secondGround = GroundMemberOf(module, second);

            Assert.GreaterOrEqual(firstGround, 0, "the first fault lost its member when the second opened");
            Assert.GreaterOrEqual(secondGround, 0, "the second fault obtained no member of its own");
            Assert.AreNotEqual(firstGround, secondGround, "the second fault superseded the first");

            Assert.AreNotSame(frameAt02, firstFault.Image.sprite,
                "the first fault's frame stopped advancing when the second began, which is the restart");

            Assert.AreEqual(2, _fight.GroundVfxPlayersForTest.Count(p => p.Image != null && p.Image.enabled),
                "two faults are cooling, so two ground members are drawing");
        }

        // A WATER-SHAPED TAIL AND A CINDERFAULT ON ONE TARGET. Neither cast's
        // handle owns a renderer belonging to the other, and the fault holds a
        // GROUND member while the tail holds effects ones -- which is the two
        // bands being genuinely separate rather than one pool with a comment.
        [UnityTest]
        public IEnumerator ACinderfaultOverALiveTailSharesNoRendererWithIt()
        {
            yield return LoadFight();

            HoldTheClock();

            var tail = _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            StepTo(0.1f);
            var fault = _fight.PlaySpellVfxForTest(CinderfaultOn(Foes));
            StepTo(0.05f);

            var module = _fight.PerformancePlayerForTest;
            Assert.IsTrue(module.IsLive(tail), "the tail died when the second cast opened");
            Assert.IsTrue(module.IsLive(fault));

            var tailMembers = EffectMembersOf(module, tail, 1);
            var faultMembers = EffectMembersOf(module, fault, 4);

            CollectionAssert.IsNotEmpty(tailMembers);
            CollectionAssert.IsNotEmpty(faultMembers);
            CollectionAssert.IsEmpty(tailMembers.Intersect(faultMembers).ToList(),
                "the two casts are sharing an effects renderer");

            Assert.GreaterOrEqual(GroundMemberOf(module, fault), 0,
                "the fault took no ground member while the tail held effects ones");
        }

        // ---- restoration ---------------------------------------------------------

        // EVERY PIECE OF STATE A CAST LEAVES ON A POOL MEMBER, PUT BACK. Two
        // defensive SetFacing(1f) calls used to exist at the call sites, each
        // with a comment saying "a pool member keeps whatever facing the last
        // thing to use it left behind". One code path owns restoration now, so
        // this is the test that lets those two lines stay deleted.
        [UnityTest]
        public IEnumerator EveryRendererComesBackWholeWhenItsCastEnds()
        {
            yield return LoadFight();

            HoldTheClock();

            // A mirrored cast, so the facing is genuinely left dirty rather
            // than being 1 already.
            var mirrored = FlareAt(Foes[0]);
            mirrored.Actor = Foes[0];
            mirrored.Target = Hero;

            _fight.PlaySpellVfxForTest(mirrored);
            StepTo(0.1f);

            var used = Effects.FirstOrDefault(p => p.Image != null && p.Image.enabled);
            Assert.IsNotNull(used, "nothing drew, so nothing can be checked for restoration");

            var image = used.Image;
            var pooled = image.rectTransform.parent as RectTransform;

            StepTo(5f);

            Assert.IsFalse(image.enabled, "the member is still drawing");
            Assert.IsNull(image.sprite, "the member kept the last frame it drew");
            Assert.AreEqual(1f, image.color.a, 1e-4f, "the member kept a faded alpha");
            Assert.AreEqual(1f, image.rectTransform.localScale.x, 1e-4f, "the member kept its mirror");
            Assert.AreEqual(Quaternion.identity, image.rectTransform.localRotation);
            Assert.AreSame(pooled, image.rectTransform.parent, "the member was reparented");
            Assert.IsFalse(image.raycastTarget, "the member became a click target");

            var dissolve = used.GetComponentsInChildren<Image>(includeInactive: true)
                .FirstOrDefault(i => i.name.EndsWith("Next"));
            Assert.IsNotNull(dissolve);
            Assert.IsFalse(dissolve.enabled, "the dissolve layer is still drawing a stopped spell's frame");
            Assert.IsNull(dissolve.sprite);
            Assert.AreEqual(1f, dissolve.color.a, 1e-4f);
        }

        // ---- the fade ------------------------------------------------------------

        // AN AUTHORED `fade` HAS TO REACH THE SCREEN, and until this test it
        // did not.
        //
        // SpellFrameCursor has always known how to draw one -- AlphaAt ramps
        // from 1 to 0 across the layer's FadeSeconds once its age passes its
        // lifetime -- but the schedule fired LayerEnd at EndSeconds and Close
        // handed the member straight back, so Paint skipped the instance on
        // every tick the ramp existed for. The whole branch was unreachable
        // from a running fight: the pilot's wake cuts instead of fading, and
        // nothing could see it, because a tenth of a second of alpha is
        // exactly the kind of thing an eye forgives and a test never asked
        // about.
        //
        // The lifetime is unchanged by the fix. A layer still ENDS at its
        // authored end; what moved is when its renderer is reclaimed, from the
        // end to the end plus the ramp -- which is what SpellLayerInstance
        // .ClearedSeconds has meant since M2 and what the cast-level release
        // already used.
        [UnityTest]
        public IEnumerator ALayerWithAFadeKeepsDrawingThroughItAtAFallingAlpha()
        {
            yield return LoadFight();

            HoldTheClock();

            var beat = FlareAt(Foes[0]);
            beat.Vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "fading", render = "sprite", place = "target",
                        at = "release", path = "Spells/frost_flare",
                        seconds = 0.20f, fade = 0.20f,
                    },
                },
            };

            var cast = _fight.PlaySpellVfxForTest(beat);
            var module = _fight.PerformancePlayerForTest;

            // StepTo IS RELATIVE in this fixture, so each of these is a delta
            // and the running total is the instant named beside it.
            StepTo(0.10f); // 0.10s: inside the layer's own lifetime
            int member = module.MemberFor(cast, 0);
            Assert.GreaterOrEqual(member, 0, "the layer never drew at all");
            Assert.AreEqual(1f, Effects[member].Image.color.a, 1e-3f,
                "the layer is inside its own lifetime and already fading");

            // A QUARTER INTO THE RAMP: still drawn, and dimmer.
            StepTo(0.15f); // 0.25s: 0.05s past a 0.20s lifetime, a quarter into a 0.20s ramp
            Assert.AreEqual(member, module.MemberFor(cast, 0),
                "the layer's renderer was reclaimed at its end, so the fade it authors never draws");
            Assert.IsTrue(Effects[member].Image.enabled, "the fade stopped being drawn the instant it began");
            Assert.AreEqual(0.75f, Effects[member].Image.color.a, 1e-2f,
                "0.05s into a 0.20s ramp is three quarters of the way up, not a step to zero");

            // THREE QUARTERS IN: dimmer still, and monotonically.
            StepTo(0.10f); // 0.35s
            Assert.AreEqual(0.25f, Effects[member].Image.color.a, 1e-2f);

            // PAST IT: gone, and the member is back.
            StepTo(0.06f); // 0.41s, past the 0.40s the ramp ends at
            Assert.AreEqual(-1, module.MemberFor(cast, 0), "the faded layer never gave its renderer back");
            Assert.IsFalse(module.IsLive(cast), "the cast outlived the last of its art");
        }

        // ---- draw order ----------------------------------------------------------

        // AUTHORED ARRAY ORDER IS DRAW ORDER, and it is member index that makes
        // it so: a cast allocates from the lowest free member in the order its
        // layers were written, so a spray authored after its splash gets the
        // higher index and draws over it. There is no z-order for content to
        // author and nothing to keep in sync.
        [UnityTest]
        public IEnumerator ACastsLayersTakeMembersInTheOrderTheyWereAuthored()
        {
            yield return LoadFight();

            HoldTheClock();

            var beat = FlareAt(Foes[0]);
            beat.Vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "under", render = "sprite", place = "target",
                        at = "release", path = "Spells/frost_flare", seconds = 0.5f,
                    },
                    new SpellLayer
                    {
                        id = "over", render = "sprite", place = "target",
                        at = "release", path = "Spells/frost_flare", seconds = 0.5f,
                    },
                },
            };

            var cast = _fight.PlaySpellVfxForTest(beat);
            yield return null;

            var module = _fight.PerformancePlayerForTest;
            int under = module.MemberFor(cast, 0);
            int over = module.MemberFor(cast, 1);

            Assert.GreaterOrEqual(under, 0);
            Assert.Greater(over, under,
                "the layer authored second took a lower member, so it would draw UNDER the first");
        }

        // A MEMBER INDEX ONLY MEANS SOMETHING INSIDE ITS OWN BAND: ground
        // member 0 and effects member 0 are different renderers that share an
        // integer, so both helpers ask the module which band an instance drew
        // in rather than inferring it from the adapter's ordering.
        private static int GroundMemberOf(SpellPerformancePlayer module, CastHandle handle)
        {
            for (int i = 0; i < 8; i++)
            {
                if (!module.DrawsInGroundBand(handle, i)) continue;

                int member = module.MemberFor(handle, i);
                if (member >= 0) return member;
            }

            return -1;
        }

        private static List<int> EffectMembersOf(SpellPerformancePlayer module, CastHandle handle,
            int instances)
        {
            var held = new List<int>();
            for (int i = 0; i < instances; i++)
            {
                if (module.DrawsInGroundBand(handle, i)) continue;

                int member = module.MemberFor(handle, i);
                if (member >= 0) held.Add(member);
            }

            return held;
        }
    }
}
