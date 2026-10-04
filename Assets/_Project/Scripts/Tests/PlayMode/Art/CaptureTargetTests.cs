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

        private static CaptureStandRequest Ask(string target, params string[] party) =>
            new CaptureStandRequest { Party = party, Target = target };

        [Test]
        public void AWardIsARealCastOnWhoeverIsChosen()
        {
            var skill = PreviewFight.ForSpell("gilded_aegis").Skill;

            Assert.IsTrue(CaptureScenarioRunner.AllyCastIsReal(skill, Ask("owl", "sheep", "owl", "bear")));
            Assert.IsTrue(CaptureScenarioRunner.AllyCastIsReal(skill, Ask("sheep", "sheep")));
            Assert.IsTrue(CaptureScenarioRunner.AllyCastIsReal(skill, Ask(null)));
        }

        [Test]
        public void AHastenIsRealOnlyOnAnAllyBecauseTheCasterIsAlreadyNext()
        {
            var skill = PreviewFight.ForSpell("borrowed_moment").Skill;

            Assert.IsTrue(CaptureScenarioRunner.AllyCastIsReal(skill, Ask("bear", "sheep", "owl", "bear")));
            Assert.IsFalse(CaptureScenarioRunner.AllyCastIsReal(skill, Ask("sheep", "sheep", "owl", "bear")));
            Assert.IsFalse(CaptureScenarioRunner.AllyCastIsReal(skill, Ask(null, "sheep", "owl", "bear")));
        }
    }
}
