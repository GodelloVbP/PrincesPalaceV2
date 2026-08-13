using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // A room's depth actually reaches the enemies standing in it.
    //
    // THE SECOND INSTANCE OF THE SAME MISS, and the reason this file is
    // shaped like RelicsReachCombatTests. DifficultyCurve was written to make
    // a deep fight harder, documented as scaling "health, attack and break
    // shields alike", covered by its own EditMode suite -- and the only live
    // caller in the entire project was VictoryRewards. Enemy stats went
    // through no curve at all, so every fight at every depth used the authored
    // baseStats verbatim and the payout was the only thing that knew how far
    // down the player was.
    //
    // The arithmetic was never the problem and is tested next door. Every
    // assertion here is about the WIRE: that Build passes depth to the thing
    // that constructs an enemy, and that the constructed enemy is different
    // because of it.
    public class DepthReachesTheEnemiesTests
    {
        private static List<string> OneParty() =>
            ContentDatabase.Characters.Take(1).Select(c => c.id).ToList();

        private static List<string> OneEnemy() =>
            ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList();

        private static FightEncounterAdapter.BuiltFight At(int depthStep) =>
            FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11), depthStep: depthStep);

        private static Domain.Combat.CombatantState Enemy(int depthStep)
        {
            var built = At(depthStep);
            Assert.IsNotNull(built, "no fight could be built from the current content");
            var enemy = built.Session.Encounter.Enemies.FirstOrDefault();
            Assert.IsNotNull(enemy, "the built fight has no enemies");
            return enemy;
        }

        [Test]
        public void AnEnemyDeepInARun_IsTougherThanTheSameEnemyAtTheSurface()
        {
            var surface = Enemy(0);
            var deep = Enemy(80);

            Assert.Greater(deep.MaxHealth, surface.MaxHealth * 100,
                "depth is not reaching enemy health - the curve is being computed and thrown away");
            Assert.Greater(deep.Attack, surface.Attack * 10,
                "depth is not reaching enemy attack");
        }

        // The two rates are different on purpose, and the wire has to carry
        // BOTH of them rather than one multiplier applied twice.
        [Test]
        public void HealthAndAttackArriveOnTheirOwnCurves()
        {
            var surface = Enemy(0);
            var deep = Enemy(80);

            float healthGrowth = deep.MaxHealth / (float)surface.MaxHealth;
            float attackGrowth = deep.Attack / (float)surface.Attack;

            Assert.Greater(healthGrowth, attackGrowth * 5f,
                "health and attack are arriving on the same curve; enemy health has to track the " +
                "player's damage, which outruns their health by a long way");
        }

        // Speed is a RATE and is deliberately left alone. SpeedScale clamps
        // its tick rate at 2.5x, so a scaled Speed buys nothing and an enemy
        // that scaled it would simply take every turn.
        [Test]
        public void SpeedIsNotScaledByDepth()
        {
            Assert.AreEqual(Enemy(0).Speed, Enemy(80).Speed,
                "Speed has been put on the depth curve; it feeds a scheduler that clamps at 2.5x");
        }

        [Test]
        public void TheSurfaceIsExactlyWhatWasAuthored()
        {
            // Step 0 must change nothing, or every authored number in
            // enemies.json quietly means something else.
            var definition = ContentDatabase.Enemies.First(e => e.id == OneEnemy()[0]);
            var enemy = Enemy(0);

            Assert.AreEqual(definition.baseStats.maxHealth, enemy.MaxHealth);
            Assert.AreEqual(definition.baseStats.attack, enemy.Attack);
            Assert.AreEqual(definition.baseStats.defense, enemy.Defense);
        }

        // Non-vacuity, stated rather than assumed: if the curve were flat this
        // whole file would pass while asserting nothing.
        [Test]
        public void TheCurveItselfIsNotFlat()
        {
            Assert.Greater(DifficultyCurve.HealthMultiplier(80), 100f);
            Assert.Greater(DifficultyCurve.AttackMultiplier(80), 10f);
        }
    }
}
