using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // What the stage is told to draw for a planted shield: intact while more
    // than half the placed size is left, cracked at or below half, broken once
    // on the observation after a break, nothing after an expiry or a Bash.
    //
    // FIXTURE ARITHMETIC as PlantedShieldTests: Bjorn's shield is 130, raw 140
    // physical lands as 100 and raw 70 lands as 50.
    public class PlantedShieldStageTests
    {
        private static (FightSession session, CombatantState bjorn, CombatantState foe) Fight()
        {
            var bjorn = new CombatantState("Bjorn", true, 260,
                new ResourcePool("fury", "Fury", 100, 0, 0, 0), 20, 20)
            {
                CritChancePercent = 0,
                PhysicalDefense = 40,
                MagicalDefense = 12,
            };
            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1) { CritChancePercent = 0 };
            var kits = new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, null, null, null) };
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                kits, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return (session, bjorn, foe);
        }

        [Test]
        public void NothingPlantedShowsNothing()
        {
            var (_, bjorn, _) = Fight();
            Assert.AreEqual(ShieldLook.None, new PlantedShieldWatch().Observe(bjorn.PlantedShield));
        }

        [Test]
        public void AFreshShieldIsIntactAndTheCrackAppearsAtHalf()
        {
            var (session, bjorn, foe) = Fight();
            var watch = new PlantedShieldWatch();

            session.PlantShield(bjorn);
            Assert.AreEqual(ShieldLook.Intact, watch.Observe(bjorn.PlantedShield));

            session.StrikeForTest(foe, bjorn, 70, DamageType.Physical); // 130 -> 80, over half
            Assert.AreEqual(ShieldLook.Intact, watch.Observe(bjorn.PlantedShield));

            session.StrikeForTest(foe, bjorn, 21, DamageType.Physical); // lands 15: 80 -> 65, exactly half
            Assert.AreEqual(ShieldLook.Cracked, watch.Observe(bjorn.PlantedShield));
        }

        [Test]
        public void ABreakIsShownBrokenOnceThenNothing()
        {
            var (session, bjorn, foe) = Fight();
            var watch = new PlantedShieldWatch();
            session.PlantShield(bjorn);
            watch.Observe(bjorn.PlantedShield);

            session.StrikeForTest(foe, bjorn, 280, DamageType.Physical);

            Assert.AreEqual(ShieldLook.Broken, watch.Observe(bjorn.PlantedShield));
            Assert.AreEqual(ShieldLook.None, watch.Observe(bjorn.PlantedShield));
        }

        [Test]
        public void ABashSpendsTheShieldWithoutABreak()
        {
            var (session, bjorn, foe) = Fight();
            var watch = new PlantedShieldWatch();
            session.PlantShield(bjorn);
            session.StrikeForTest(foe, bjorn, 70, DamageType.Physical);
            watch.Observe(bjorn.PlantedShield);

            session.BashPlantedShield(bjorn);

            Assert.AreEqual(ShieldLook.None, watch.Observe(bjorn.PlantedShield));
        }

        [Test]
        public void AShieldwallReadsAsAWallAndBreaksLikeAShield()
        {
            var (session, bjorn, foe) = Fight();
            bjorn.PlantedShield.CoversParty = true;
            var watch = new PlantedShieldWatch();

            session.PlantShield(bjorn);
            Assert.AreEqual(ShieldLook.Wall, watch.Observe(bjorn.PlantedShield));
            Assert.IsTrue(watch.SawWall);

            session.StrikeForTest(foe, bjorn, 700, DamageType.Physical);
            Assert.AreEqual(ShieldLook.Broken, watch.Observe(bjorn.PlantedShield));
            Assert.IsFalse(watch.SawWall);
        }

        [Test]
        public void EveryDrawnLookHasAResourcePath()
        {
            foreach (var look in new[] { ShieldLook.Intact, ShieldLook.Cracked, ShieldLook.Broken, ShieldLook.Wall })
            {
                string path = PlantedShieldArt.ResourceFor(look);
                StringAssert.StartsWith("Props/planted_shield/", path);
                Assert.IsFalse(path.EndsWith(".png"));
            }

            Assert.IsNull(PlantedShieldArt.ResourceFor(ShieldLook.None));
        }
    }
}
