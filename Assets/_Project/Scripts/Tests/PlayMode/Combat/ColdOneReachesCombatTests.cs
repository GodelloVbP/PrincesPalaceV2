using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.PlayModeTests
{
    // The Cold One's WIRE (docs/PLAN_PETTING_ZOO.md): a fillSpecialPool event
    // buff on the run reaches the fight as FightSession.
    // FillsSpecialPoolAtTurnStart while the run is on the leg it was granted
    // on, and not after. The fill itself is Domain's and pinned by
    // ColdOneFillTests; this is only the join. PlayMode because the adapter
    // reads RunManager and ContentDatabase, both Core.
    public class ColdOneReachesCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-coldone-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            RunManager.ResetForTests();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static bool Built()
        {
            var built = FightEncounterAdapter.Build(
                ContentDatabase.Characters.Take(1).Select(c => c.id).ToList(),
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(11));
            Assert.IsNotNull(built, "fixture: content fields a fight");
            return built.Session.FillsSpecialPoolAtTurnStart;
        }

        [Test]
        public void NoRun_NoFill()
        {
            Assert.IsFalse(Built());
        }

        [Test]
        public void ARunWithNoBuff_NoFill()
        {
            RunManager.StartRun(20260925UL);
            Assert.IsFalse(Built());
        }

        [Test]
        public void ABuffGrantedOnThisLeg_Fills()
        {
            RunManager.StartRun(20260925UL);
            var run = RunManager.Run;
            run.eventBuffs.Add(new EventBuffEntry
                { kind = EventBuffs.FillSpecialPool, legStartStep = run.legStartStep });

            Assert.IsTrue(Built());
        }

        [Test]
        public void ABuffFromAnotherLeg_DoesNotFill()
        {
            RunManager.StartRun(20260925UL);
            var run = RunManager.Run;
            run.eventBuffs.Add(new EventBuffEntry
                { kind = EventBuffs.FillSpecialPool, legStartStep = run.legStartStep + 7 });

            Assert.IsFalse(Built(), "legStartStep differs from the run's: not this leg's buff");
        }

        [Test]
        public void AnotherKindOfBuff_DoesNotFill()
        {
            RunManager.StartRun(20260925UL);
            var run = RunManager.Run;
            run.eventBuffs.Add(new EventBuffEntry
                { kind = EventBuffs.PrincesFavor, amount = 10, legStartStep = EventBuffs.WholeRun });

            Assert.IsFalse(Built());
        }
    }
}
