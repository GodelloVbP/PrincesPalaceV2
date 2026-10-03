using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // A capture scenario's `party`, mixed `vs` rack and `target` resolve against the
    // real encounter the adapter builds (CaptureScenarioRunner.ResolveTarget).
    public class CaptureTargetTests
    {
        private static FightEncounterAdapter.BuiltFight Build() =>
            FightEncounterAdapter.Build(
                new List<string> { "sheep", "owl", "bear" },
                new List<string> { "rat", "treant", "beetle" },
                new SeededRandom(CaptureScenarioRunner.Seed),
                relicIds: null,
                depthStep: 0);

        [Test]
        public void ACharacterIdNamesItsPartySeat()
        {
            var built = Build();

            Assert.AreSame(built.Party[1], CaptureScenarioRunner.ResolveTarget(built, "owl"));
            Assert.AreSame(built.Party[2], CaptureScenarioRunner.ResolveTarget(built, "bear"));
        }

        [Test]
        public void ANumberNamesAnEnemySlotFromTheFront()
        {
            var built = Build();
            var foes = built.Session.Encounter.Enemies;

            Assert.AreSame(foes[0], CaptureScenarioRunner.ResolveTarget(built, "1"));
            Assert.AreSame(foes[2], CaptureScenarioRunner.ResolveTarget(built, "3"));
        }

        [Test]
        public void NoTargetKeepsTheFirstPick()
        {
            Assert.IsNull(CaptureScenarioRunner.ResolveTarget(Build(), ""));
        }
    }
}
