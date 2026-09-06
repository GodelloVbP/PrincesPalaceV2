using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // FightBootstrap.enemyCount's [SerializeField] default and the private
    // FullFormationCount it feeds into "-Formation full" both used to
    // restate FightHudSpec.StageSlotsPerSide's value (3) as a bare literal --
    // each with a comment NAMING the const rather than referencing it, which
    // is exactly the state a comment update to the wrong number, or none at
    // all, survives silently. Both now reference the const directly; this
    // pins that the reference resolves to the value it always meant to,
    // rather than only proving the two sides of a comparison agree with
    // themselves.
    public class FightBootstrapFormationCountTests
    {
        [Test]
        public void EnemyCountDefaultsToTheStagesSlotCapacity()
        {
            Assert.AreEqual(3, FightHudSpec.StageSlotsPerSide,
                "the stage's own slot capacity moved off 3 -- update this pin along with it, not silently.");

            var go = new GameObject(nameof(FightBootstrapFormationCountTests));
            try
            {
                var bootstrap = go.AddComponent<FightBootstrap>();

                var field = typeof(FightBootstrap).GetField("enemyCount",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "FightBootstrap.enemyCount was renamed -- update this test's reflection target.");

                Assert.AreEqual(FightHudSpec.StageSlotsPerSide, (int)field.GetValue(bootstrap),
                    "FightBootstrap's no-run enemyCount default has drifted from the stage's own slot capacity.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
