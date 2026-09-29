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
    // on the beat that broke it, nothing after an expiry or a Bash. The beat
    // carries the shield it left behind, so the view paints the blow itself.
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
            Assert.AreEqual(ShieldLook.None, new PlantedShieldWatch().Snapshot(bjorn.PlantedShield).Look);
        }

        [Test]
        public void AFreshShieldIsIntactAndTheCrackAppearsAtHalf()
        {
            var (session, bjorn, foe) = Fight();
            var watch = new PlantedShieldWatch();

            session.PlantShield(bjorn);
            Assert.AreEqual(ShieldLook.Intact, watch.Snapshot(bjorn.PlantedShield).Look);

            session.StrikeForTest(foe, bjorn, 70, DamageType.Physical); // 130 -> 80, over half
            Assert.AreEqual(ShieldLook.Intact, watch.Snapshot(bjorn.PlantedShield).Look);

            session.StrikeForTest(foe, bjorn, 21, DamageType.Physical); // lands 15: 80 -> 65, exactly half
            Assert.AreEqual(ShieldLook.Cracked, watch.Snapshot(bjorn.PlantedShield).Look);
        }

        [Test]
        public void ABreakIsShownBrokenOnceThenNothing()
        {
            var (session, bjorn, foe) = Fight();
            var watch = new PlantedShieldWatch();
            session.PlantShield(bjorn);
            watch.Snapshot(bjorn.PlantedShield);

            session.StrikeForTest(foe, bjorn, 280, DamageType.Physical);

            Assert.AreEqual(ShieldLook.Broken, watch.Snapshot(bjorn.PlantedShield).Look);
            Assert.AreEqual(ShieldLook.None, watch.Snapshot(bjorn.PlantedShield).Look);
        }

        [Test]
        public void ABashSpendsTheShieldWithoutABreak()
        {
            var (session, bjorn, foe) = Fight();
            var watch = new PlantedShieldWatch();
            session.PlantShield(bjorn);
            session.StrikeForTest(foe, bjorn, 70, DamageType.Physical);
            watch.Snapshot(bjorn.PlantedShield);

            session.BashPlantedShield(bjorn);

            Assert.AreEqual(ShieldLook.None, watch.Snapshot(bjorn.PlantedShield).Look);
        }

        [Test]
        public void ABeatCarriesTheShieldItLeftBehind()
        {
            var (session, bjorn, foe) = Fight();
            session.PlantShield(bjorn);

            // 130 placed. Raw 70 lands 50 -> 80 left, over half: intact.
            var over = session.BeatAroundForTest(foe, bjorn,
                () => session.StrikeForTest(foe, bjorn, 70, DamageType.Physical));
            var snap = over.Shields[bjorn];
            Assert.IsTrue(snap.Placed);
            Assert.AreEqual(80, snap.Points);
            Assert.AreEqual(130, snap.PlacedPoints);
            Assert.IsFalse(snap.IsWall);
            Assert.IsFalse(snap.Broke);
            Assert.AreEqual(ShieldLook.Intact, snap.Look);

            // Raw 21 lands 15 -> 65, exactly half: the blow that cracks it.
            var crack = session.BeatAroundForTest(foe, bjorn,
                () => session.StrikeForTest(foe, bjorn, 21, DamageType.Physical));
            Assert.AreEqual(65, crack.Shields[bjorn].Points);
            Assert.AreEqual(ShieldLook.Cracked, crack.Shields[bjorn].Look);
            Assert.AreEqual(80, over.Shields[bjorn].Points, "an earlier beat keeps its own snapshot");

            // The blow that empties it: broken on this beat, not placed after.
            var broke = session.BeatAroundForTest(foe, bjorn,
                () => session.StrikeForTest(foe, bjorn, 280, DamageType.Physical));
            var gone = broke.Shields[bjorn];
            Assert.IsFalse(gone.Placed);
            Assert.IsTrue(gone.Broke);
            Assert.AreEqual(ShieldLook.Broken, gone.Look);

            var after = session.BeatAroundForTest(foe, bjorn, () => { });
            Assert.IsFalse(after.Shields.ContainsKey(bjorn), "the break is reported once");
        }

        [Test]
        public void ABeatWithNoShieldCarriesNone()
        {
            var (session, bjorn, foe) = Fight();

            var beat = session.BeatAroundForTest(foe, bjorn, () => { });

            Assert.AreEqual(0, beat.Shields.Count);
        }

        [Test]
        public void AShieldwallIsOneSharedPoolShownAtEveryStandingSeat()
        {
            var (session, bjorn, foe) = Fight();
            var ally = new CombatantState("Ally", true, 100, 0, 1, 1);
            bjorn.PlantedShield.CoversParty = true;
            var party = new List<CombatantState> { bjorn, ally };
            var watch = new PlantedShieldWatch();

            session.PlantShield(bjorn);
            var placed = new Dictionary<CombatantState, ShieldSnapshot> { [bjorn] = watch.Snapshot(bjorn.PlantedShield) };
            Assert.IsTrue(placed[bjorn].IsWall);
            Assert.AreEqual(260, placed[bjorn].PlacedPoints);

            var looks = PlantedShieldStage.Resolve(party, placed, _ => true);
            Assert.AreEqual(ShieldLook.Intact, looks[bjorn]);
            Assert.AreEqual(ShieldLook.Intact, looks[ally]);

            // A fallen ally has no seat to show it at.
            looks = PlantedShieldStage.Resolve(party, placed, m => m == bjorn);
            Assert.IsFalse(looks.ContainsKey(ally));

            // Down to half of the ONE pool: every seat cracks together.
            session.StrikeForTest(foe, bjorn, 280, DamageType.Physical); // lands 200 -> 60 left
            var cracked = new Dictionary<CombatantState, ShieldSnapshot> { [bjorn] = watch.Snapshot(bjorn.PlantedShield) };
            looks = PlantedShieldStage.Resolve(party, cracked, _ => true);
            Assert.AreEqual(60, cracked[bjorn].Points);
            Assert.AreEqual(ShieldLook.Cracked, looks[bjorn]);
            Assert.AreEqual(ShieldLook.Cracked, looks[ally]);

            session.StrikeForTest(foe, bjorn, 700, DamageType.Physical);
            var broke = new Dictionary<CombatantState, ShieldSnapshot> { [bjorn] = watch.Snapshot(bjorn.PlantedShield) };
            Assert.IsTrue(broke[bjorn].Broke);
            Assert.IsTrue(broke[bjorn].IsWall);
            looks = PlantedShieldStage.Resolve(party, broke, _ => true);
            Assert.AreEqual(ShieldLook.Broken, looks[bjorn]);
            Assert.AreEqual(ShieldLook.Broken, looks[ally], "the break plays at every ally's seat");
        }

        [Test]
        public void EveryDrawnLookHasAResourcePathAndTheWallArtIsNotLoaded()
        {
            foreach (var look in new[] { ShieldLook.Intact, ShieldLook.Cracked, ShieldLook.Broken })
            {
                string path = PlantedShieldArt.ResourceFor(look);
                StringAssert.StartsWith("Props/planted_shield/", path);
                Assert.IsFalse(path.EndsWith(".png"));
                StringAssert.DoesNotContain("wall", path);
            }

            Assert.IsNull(PlantedShieldArt.ResourceFor(ShieldLook.None));
        }
    }
}
