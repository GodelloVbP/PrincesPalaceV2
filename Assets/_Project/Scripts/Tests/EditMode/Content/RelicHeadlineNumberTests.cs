using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Every relic headline number relics.json carries, pinned as a literal
    // (CLAUDE.md gotcha 5). These numbers lived in FightTuning until the
    // effect model moved them to content, so a silent change in the move, or
    // a retune later, fails here by name.
    public class RelicHeadlineNumberTests
    {
        private static RawRelicEntry Row(string id)
        {
            var file = ContentDataFiles.ParseFile<RawRelicFile>(ContentDataFiles.DataPath("relics.json"));
            var row = file.relics.FirstOrDefault(r => r.id == id);
            Assert.IsNotNull(row, $"relics.json has no '{id}'");
            return row;
        }

        private static Effect MechanicOf(string id, EffectKind kind)
        {
            var row = Row(id);
            Assert.IsNotNull(row.effects, $"'{id}' authors no effects");

            foreach (var raw in row.effects)
            {
                Assert.IsTrue(EffectEntryResolver.TryResolve(raw, EffectHolder.Relic, id, out var effect, out string error), error);
                if (effect.Kind == kind) return effect;
            }

            Assert.Fail($"'{id}' does not carry {kind}");
            return default;
        }

        [TestCase("magical_shield", EffectKind.MagicalShield, 20)]
        [TestCase("charging_crystal", EffectKind.ChargingCrystal, 50)]
        [TestCase("ballerinas_slippers", EffectKind.BallerinasSlippers, 10)]
        [TestCase("tinfoil_pipe", EffectKind.TinFoilPipe, 20)]
        [TestCase("toothed_necklace", EffectKind.ToothedNecklace, 30)]
        [TestCase("seven_teeth", EffectKind.BountyHunterContract, 3)]
        [TestCase("drowned_lantern", EffectKind.DrownedLantern, 50)]
        [TestCase("salt_ledger", EffectKind.SaltLedger, 1)]
        [TestCase("long_count", EffectKind.LongCount, 40)]
        [TestCase("magic_marker", EffectKind.MagicMarker, 20)]
        [TestCase("cursed_idol", EffectKind.CursedIdol, 3)]
        [TestCase("amassing_star", EffectKind.AmassingStar, 2)]
        [TestCase("rampaging_bulls_horn", EffectKind.RampagingBullsHorn, 50)]
        [TestCase("sparring_saber", EffectKind.SparringSaber, 30)]
        [TestCase("sparring_buckler", EffectKind.SparringBuckler, 10)]
        [TestCase("essence_siphon", EffectKind.EssenceSiphon, 3)]
        [TestCase("berserkers_vest", EffectKind.BerserkersVest, 1)]
        [TestCase("phoenix_egg", EffectKind.PhoenixEgg, 3)]
        [TestCase("bellwethers_bell", EffectKind.BellwethersBell, 1)]
        [TestCase("toll_of_the_flock", EffectKind.TollOfTheFlock, 15)]
        public void ShippedMagnitude(string id, EffectKind kind, int expected)
        {
            Assert.AreEqual(expected, MechanicOf(id, kind).Magnitude);
        }

        [Test]
        public void WorldEndersCrown_FiresBelowThirtyPercent()
        {
            Assert.AreEqual(30, MechanicOf("world_enders_crown", EffectKind.WorldEndersCrown).Threshold);
        }

        // The fixture relics combat tests build agree with the file, so a
        // mechanic test cannot pass on a number the game does not ship.
        [Test]
        public void TestRelics_CarryTheShippedNumbers()
        {
            var file = ContentDataFiles.ParseFile<RawRelicFile>(ContentDataFiles.DataPath("relics.json"));
            int checkedCount = 0;

            foreach (var row in file.relics)
            {
                foreach (var raw in row.effects ?? new RawEffect[0])
                {
                    Assert.IsTrue(EffectEntryResolver.TryResolve(raw, EffectHolder.Relic, row.id, out var effect, out string error), error);
                    if (EffectCatalog.Of(effect.Kind).Stacking != EffectStacking.Flag) continue;

                    var fixture = TestRelics.Mechanic(effect.Kind);
                    Assert.AreEqual(effect.Magnitude, fixture.Magnitude, $"{row.id} magnitude");
                    Assert.AreEqual(effect.Threshold, fixture.Threshold, $"{row.id} threshold");
                    checkedCount++;
                }
            }

            Assert.GreaterOrEqual(checkedCount, 34, "every relic mechanic is checked");
        }
    }
}
