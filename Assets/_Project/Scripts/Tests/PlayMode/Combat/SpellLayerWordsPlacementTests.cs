using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // WHERE THE LAYER WORDS PUT THINGS on a live stage:
    // `overhang` on a lone body's span, `follow` through a displacement, the
    // `counterpart` placements and the `whenConsumed` gate. Read off the
    // placed-but-not-begun cast (PlacedCastForTest), so no clock is raced.
    public class SpellLayerWordsPlacementTests
    {
        private const string Frames = "Spells/cinderfault_ground";

        private FightController _fight;
        private CombatantState _hero;
        private List<CombatantState> _foes;

        private IEnumerator LoadFight(int enemyCount)
        {
            yield return SharedScene.EnsureFight();
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _foes = Enumerable.Range(0, enemyCount)
                .Select(i => new CombatantState($"Foe{i}", false, 5000, 10, 8, 4)).ToList();

            var kit = PlayModeSparkFixture.Kit();
            var enemyKits = _foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name, new StatBlock(), 5, 3,
                    false, DamageType.Physical, DamageType.Physical, 0, spritePath: "", stageScale: 1f), false))
                .ToList();

            var session = new FightSession(new CombatEncounter(new[] { _hero }, _foes.ToArray()),
                new List<PlayerKit> { kit }, enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private SpellPerformance Place(CombatBeat beat, params SpellLayer[] layers)
        {
            beat.Actor = _hero;
            beat.Target = _foes[0];
            beat.Vfx = new SpellPresentation { layerFormat = 1, hitCueSeconds = 0.2f, layers = layers };
            return _fight.PlacedCastForTest(beat);
        }

        private static SpellLayer Layer(string id, string place, System.Action<SpellLayer> edit = null)
        {
            var layer = new SpellLayer { id = id, render = "sprite", place = place, path = Frames, seconds = 0.5f };
            edit?.Invoke(layer);
            return layer;
        }

        [UnityTest]
        public IEnumerator ALoneBodysFaultIsOneAndAHalfTimesAsWideWithAnOverhangOfHalf()
        {
            yield return LoadFight(1);

            float Width(float overhang) => Place(new CombatBeat(),
                Layer("fault", "formation", l => { l.align = "span"; l.overhang = overhang; }))
                .Instances[0].Box.X;

            float flush = Width(0f);
            float reaching = Width(0.5f);

            Assert.Greater(flush, 0f);
            Assert.AreEqual(1.5f, reaching / flush, 0.01f,
                "half a half-width past each edge of one body is half a body wider in all");
        }

        [UnityTest]
        public IEnumerator AFollowingLayerMovesWithItsBodyAndAnotherDoesNot()
        {
            yield return LoadFight(1);

            var performance = Place(new CombatBeat(),
                Layer("bind", "target-centre", l => l.follow = true),
                Layer("still", "target-centre"));

            var slot = _fight.SlotForTest(_foes[0]);
            var bind = performance.Instances[0];
            var still = performance.Instances[1];
            Assert.IsNull(still.Displacement, "a layer that does not follow carries no delegate");
            Assert.AreEqual(0f, bind.Displacement().X, 0.01f, "nothing has moved yet");

            slot.anchoredPosition += new Vector2(120f, 0f);

            Assert.Greater(bind.Displacement().X, 60f, "the bind rides the figure the pull dragged right");
            Assert.AreEqual(0f, bind.Displacement().Y, 0.5f);
        }

        [UnityTest]
        public IEnumerator ACounterpartLayerStandsOnTheOtherEndOfTheMove()
        {
            yield return LoadFight(2);

            var beat = new CombatBeat { Counterpart = _foes[1] };
            var performance = Place(beat,
                Layer("here", "target-centre"),
                Layer("there", "counterpart-centre"));

            Assert.IsTrue(performance.Instances[1].Placed);
            Assert.Greater(Mathf.Abs(performance.Instances[0].To.X - performance.Instances[1].To.X), 1f,
                "the second door is on the second seat, not on top of the first");
        }

        [UnityTest]
        public IEnumerator ACounterpartLayerDrawsNothingWhenTheMoveHadNoOtherEnd()
        {
            yield return LoadFight(1);

            var performance = Place(new CombatBeat(), Layer("there", "counterpart-centre"));

            Assert.IsFalse(performance.Instances[0].Placed);
        }

        [UnityTest]
        public IEnumerator AGatedLayerPlaysOnlyWhenTheCastSpentThatStatus()
        {
            yield return LoadFight(1);

            var gated = Layer("burst", "target-centre", l => l.whenConsumed = "Poison");

            Assert.IsFalse(Place(new CombatBeat(), gated).Instances[0].Placed,
                "nothing was detonated, so there is no burst");

            var spent = new CombatBeat();
            spent.ConsumedStatuses.Add(StatusEffectType.Poison);
            Assert.IsTrue(Place(spent, gated).Instances[0].Placed);

            var other = new CombatBeat();
            other.ConsumedStatuses.Add(StatusEffectType.Burn);
            Assert.IsFalse(Place(other, gated).Instances[0].Placed, "a different status is not a Poison detonation");
        }
    }
}
