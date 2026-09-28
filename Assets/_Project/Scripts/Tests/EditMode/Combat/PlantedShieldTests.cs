using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Plan 4a, the Sentinel's planted shield: a ward entry with its own HP
    // rule, lifetime, re-place wait, absorbed tally and break shards;
    // Shieldwall's one pool shared by the party; Thornwall / Spellbreaker's
    // reactive hooks. Everything off by default.
    //
    // FIXTURE ARITHMETIC. Bjorn has PhysicalDefense 40 and MagicalDefense 12,
    // so the funnel lands raw x 100 / 140 of a physical hit and raw x 100 /
    // 112 of a magic one (DamagePipeline's header): raw 140 physical and raw
    // 112 fire both land as 100. His shield is 2 x (40 + 12) + 260 / 10 = 130.
    // The allies and the foe have no defences, so what is thrown at them lands.
    public class PlantedShieldTests
    {
        private sealed class Rig
        {
            public FightSession Session;
            public CombatantState Bjorn;
            public CombatantState Ally1;
            public CombatantState Ally2;
            public CombatantState Foe;
        }

        private static Rig Fight(bool withAllies = false)
        {
            var bjorn = new CombatantState("Bjorn", true, 260,
                new ResourcePool("fury", "Fury", 100, 0, 0, 0), 20, 20)
            {
                CritChancePercent = 0,
                PhysicalDefense = 40,
                MagicalDefense = 12,
            };
            var party = new List<CombatantState> { bjorn };
            var kits = new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, null, null, null) };

            CombatantState ally1 = null, ally2 = null;
            if (withAllies)
            {
                ally1 = new CombatantState("Shawn", true, 200, 0, 10, 10) { CritChancePercent = 0 };
                ally2 = new CombatantState("Odette", true, 200, 0, 10, 10) { CritChancePercent = 0 };
                party.Add(ally1);
                party.Add(ally2);
                kits.Add(new PlayerKit("shawn", CharacterRole.Tank, null, null, null));
                kits.Add(new PlayerKit("odette", CharacterRole.Tank, null, null, null));
            }

            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1) { CritChancePercent = 0 };
            var session = new FightSession(new CombatEncounter(party.ToArray(), new[] { foe }),
                kits, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return new Rig { Session = session, Bjorn = bjorn, Ally1 = ally1, Ally2 = ally2, Foe = foe };
        }

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        // ---- the numbers -------------------------------------------------------------

        [Test]
        public void ShieldHp_IsTwiceTheDefencesPlusAFlooredTenthOfMaxHealth()
        {
            Assert.AreEqual(130, PlantedShield.PointsFor(40, 12, 260), "today's Bjorn: 104 + 26");
            Assert.AreEqual(39, PlantedShield.PointsFor(10, 5, 99), "99 / 10 floors to 9");
            Assert.AreEqual(19, PlantedShield.PointsFor(-20, 5, 199), "negative defences read as 0");
            Assert.AreEqual(1, PlantedShield.PointsFor(0, 0, 5), "never an empty ward");
        }

        [Test]
        public void ShieldwallIsTwiceThePlantedShield()
        {
            var bjorn = new CombatantState("B", true, 260, 0, 1, 1) { PhysicalDefense = 40, MagicalDefense = 12 };
            Assert.AreEqual(260, PlantedShield.ShieldwallPointsFor(bjorn));
        }

        [Test]
        public void FuryForAnAbsorbedHit_IsScaledAndClampedThreeToTwentyFive()
        {
            Assert.AreEqual(25, PlantedShield.FuryForAbsorbedHit(100, 260), "57 clamps to 25");
            Assert.AreEqual(5, PlantedShield.FuryForAbsorbedHit(10, 260), "1500 / 260 floors to 5");
            Assert.AreEqual(3, PlantedShield.FuryForAbsorbedHit(1, 260), "0 clamps up to 3");
            Assert.AreEqual(0, PlantedShield.FuryForAbsorbedHit(0, 260), "nothing absorbed pays nothing");
        }

        [Test]
        public void BashBonus_IsTheFlooredPercentOfWhatWasAbsorbed()
        {
            Assert.AreEqual(24, PlantedShield.BashBonus(80, 30));
            Assert.AreEqual(24, PlantedShield.BashBonus(81, 30), "24.3 floors");
            Assert.AreEqual(0, PlantedShield.BashBonus(0, 30));
        }

        // ---- planting, lifetime, waits ----------------------------------------------

        [Test]
        public void Planting_PutsUpOneWardEntryOfTheShieldsSize()
        {
            var r = Fight();

            Assert.IsTrue(r.Session.PlantShield(r.Bjorn));

            var shield = r.Bjorn.PlantedShield;
            Assert.IsTrue(shield.IsPlaced);
            Assert.IsFalse(shield.IsShieldwall);
            Assert.AreEqual(130, StatusEffects.WardPoints(r.Bjorn));
            Assert.AreSame(shield.Ward, StatusEffects.WardsInDrainOrder(r.Bjorn).Single());
            Assert.AreSame(r.Bjorn, shield.Ward.Source);
            Assert.IsFalse(r.Session.PlantShield(r.Bjorn), "a second shield while one is down is refused");
            Assert.AreEqual(130, StatusEffects.WardPoints(r.Bjorn));
        }

        [Test]
        public void TheShieldLastsThreeOfHisTurns_ThenAThreeTurnWait()
        {
            var r = Fight();
            Assert.AreSame(r.Bjorn, r.Session.Encounter.Current, "fixture: planted on his own turn");
            r.Session.PlantShield(r.Bjorn);
            var shield = r.Bjorn.PlantedShield;

            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn); // the planting turn, not counted
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn); // 1
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn); // 2
            Assert.IsTrue(shield.IsPlaced);
            Assert.AreEqual(130, StatusEffects.WardPoints(r.Bjorn));
            Messages(r.Session);

            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn); // 3
            Assert.IsFalse(shield.IsPlaced);
            Assert.AreEqual(0, StatusEffects.WardPoints(r.Bjorn));
            Assert.Contains("Bjorn lifts his planted shield.", Messages(r.Session));

            Assert.IsFalse(shield.CanPlace);
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
            Assert.IsFalse(shield.CanPlace, "two of the three waiting turns");
            Assert.IsFalse(r.Session.PlantShield(r.Bjorn));
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
            Assert.IsTrue(shield.CanPlace);
            Assert.IsTrue(r.Session.PlantShield(r.Bjorn));
        }

        [Test]
        public void AHitTakesTheShieldBeforeHealth_AndIsTallied()
        {
            var r = Fight();
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical); // lands 100
            Assert.AreEqual(260, r.Bjorn.CurrentHealth);
            Assert.AreEqual(30, StatusEffects.WardPoints(r.Bjorn));
            Assert.AreEqual(100, r.Bjorn.PlantedShield.AbsorbedThisPlacement);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 14, DamageType.Physical); // lands 10
            Assert.AreEqual(110, r.Bjorn.PlantedShield.AbsorbedThisPlacement);
            Assert.AreEqual(20, StatusEffects.WardPoints(r.Bjorn));
        }

        [Test]
        public void ABreakEndsThePlacement_TheRestReachesHealth_AndTheWaitStarts()
        {
            var r = Fight();
            r.Session.PlantShield(r.Bjorn);
            Messages(r.Session);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 280, DamageType.Physical); // lands 200

            var shield = r.Bjorn.PlantedShield;
            Assert.AreEqual(190, r.Bjorn.CurrentHealth, "130 on the shield, 70 on him");
            Assert.IsFalse(shield.IsPlaced);
            Assert.AreEqual(130, shield.AbsorbedThisPlacement, "the tally survives the break until the next placement");
            Assert.AreEqual(3, shield.ReplaceWait.TurnsRemaining);
            Assert.Contains("Bjorn's planted shield breaks!", Messages(r.Session));
        }

        [Test]
        public void BreakShards_HitTheAttackerWhenTheActionSettles()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.BreakShardDamage = 25;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 280, DamageType.Physical);
            Assert.AreEqual(1000, r.Foe.CurrentHealth, "queued, not dealt mid-swing");

            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            Assert.AreEqual(975, r.Foe.CurrentHealth);

            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            Assert.AreEqual(975, r.Foe.CurrentHealth, "paid once");
        }

        [Test]
        public void NoShards_WhenTheShieldIsOnlyDented()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.BreakShardDamage = 25;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.AreEqual(1000, r.Foe.CurrentHealth);
        }

        [Test]
        public void ShieldBash_ConsumesTheShield_ReturnsTheTally_AndStartsTheFullWait()
        {
            var r = Fight();
            r.Session.PlantShield(r.Bjorn);
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 70, DamageType.Physical);  // 50
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 42, DamageType.Physical);  // 30

            Assert.AreEqual(80, r.Session.BashPlantedShield(r.Bjorn));

            var shield = r.Bjorn.PlantedShield;
            Assert.IsFalse(shield.IsPlaced);
            Assert.AreEqual(0, StatusEffects.WardPoints(r.Bjorn));
            Assert.AreEqual(3, shield.ReplaceWait.TurnsRemaining);
            Assert.AreEqual(0, r.Session.BashPlantedShield(r.Bjorn), "nothing to bash");
        }

        [Test]
        public void ShieldBashT2_WaitsOneTurnInstead()
        {
            var r = Fight();
            var shield = r.Bjorn.PlantedShield;
            shield.ShortWaitAfterBash = true;
            r.Session.PlantShield(r.Bjorn);

            r.Session.BashPlantedShield(r.Bjorn); // on his own turn: that turn is not counted
            Assert.AreEqual(1, shield.ReplaceWait.TurnsRemaining);

            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
            Assert.IsFalse(shield.CanPlace);
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
            Assert.IsTrue(shield.CanPlace);
        }

        [Test]
        public void ARemovedEntry_EndsThePlacementWithoutShards()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.BreakShardDamage = 25;
            r.Session.PlantShield(r.Bjorn);

            r.Bjorn.Statuses.Remove(r.Bjorn.PlantedShield.Ward); // Shatter, a dispel
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.IsFalse(r.Bjorn.PlantedShield.IsPlaced);
            Assert.AreEqual(3, r.Bjorn.PlantedShield.ReplaceWait.TurnsRemaining);
            Assert.AreEqual(1000, r.Foe.CurrentHealth);
        }

        // ---- Fury --------------------------------------------------------------------

        [Test]
        public void FuryStillAccrues_WhenThePlantedShieldEatsTheWholeHit()
        {
            var r = Fight();
            r.Bjorn.PrimaryPool.GainOnDamageTaken = 5;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical); // all 100 on the shield

            Assert.AreEqual(260, r.Bjorn.CurrentHealth);
            Assert.AreEqual(5, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void FuryIsHeardOnce_WhenTheShieldEatsPartOfTheHit()
        {
            var r = Fight();
            r.Bjorn.PrimaryPool.GainOnDamageTaken = 5;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 280, DamageType.Physical); // 130 shield, 70 health

            Assert.AreEqual(5, r.Bjorn.PrimaryPool.Current);
        }

        // Pins the existing rule the planted shield departs from: an ordinary
        // ward that eats the whole hit is still a blow the pools never hear.
        [Test]
        public void AnOrdinaryWard_ThatEatsTheWholeHit_StillPaysNoFury()
        {
            var r = Fight();
            r.Bjorn.PrimaryPool.GainOnDamageTaken = 5;
            StatusEffects.ApplyWard(r.Bjorn.Statuses, 500, 3, r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(260, r.Bjorn.CurrentHealth);
            Assert.AreEqual(0, r.Bjorn.PrimaryPool.Current);
        }

        // ---- Shieldwall --------------------------------------------------------------

        [Test]
        public void Shieldwall_IsOnePoolOnTheHolder_TwiceTheShield()
        {
            var r = Fight(withAllies: true);
            r.Bjorn.PlantedShield.CoversParty = true;

            Assert.IsTrue(r.Session.PlantShield(r.Bjorn));

            Assert.IsTrue(r.Bjorn.PlantedShield.IsShieldwall);
            Assert.AreEqual(260, StatusEffects.WardPoints(r.Bjorn));
            Assert.AreEqual(0, StatusEffects.WardPoints(r.Ally1), "not a copy per ally");
            Assert.AreEqual(0, StatusEffects.WardPoints(r.Ally2));
        }

        [Test]
        public void AnAreaHit_DrainsTheOnePoolOncePerAllyInFull()
        {
            var r = Fight(withAllies: true);
            r.Bjorn.PlantedShield.CoversParty = true;
            r.Session.PlantShield(r.Bjorn);

            // One sweep, two allies.
            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);
            r.Session.StrikeForTest(r.Foe, r.Ally2, 100, DamageType.Physical);

            Assert.AreEqual(200, r.Ally1.CurrentHealth);
            Assert.AreEqual(200, r.Ally2.CurrentHealth);
            Assert.AreEqual(60, StatusEffects.WardPoints(r.Bjorn));
            Assert.AreEqual(200, r.Bjorn.PlantedShield.AbsorbedThisPlacement);

            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);
            Assert.AreEqual(160, r.Ally1.CurrentHealth, "60 on the wall, 40 through");
            Assert.IsFalse(r.Bjorn.PlantedShield.IsPlaced, "broken");
            Assert.AreEqual(3, r.Bjorn.PlantedShield.ReplaceWait.TurnsRemaining);
        }

        [Test]
        public void TheAllysOwnWardDrainsBeforeTheWall()
        {
            var r = Fight(withAllies: true);
            r.Bjorn.PlantedShield.CoversParty = true;
            r.Session.PlantShield(r.Bjorn);
            StatusEffects.ApplyWard(r.Ally1.Statuses, 30, 1, r.Ally2);

            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);

            Assert.AreEqual(0, StatusEffects.WardPoints(r.Ally1));
            Assert.AreEqual(190, StatusEffects.WardPoints(r.Bjorn), "the wall took the 70 left over");
            Assert.AreEqual(200, r.Ally1.CurrentHealth);
        }

        [Test]
        public void ShieldwallFury_IsClampedPerHit_AndCappedAtFortyPerTurn()
        {
            var r = Fight(withAllies: true);
            r.Bjorn.PlantedShield.CoversParty = true;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);
            Assert.AreEqual(25, r.Bjorn.PrimaryPool.Current, "the per-hit clamp");

            r.Session.StrikeForTest(r.Foe, r.Ally2, 100, DamageType.Physical);
            Assert.AreEqual(40, r.Bjorn.PrimaryPool.Current, "the turn cap: 25 + 15");

            r.Session.StrikeForTest(r.Foe, r.Ally1, 10, DamageType.Physical);
            Assert.AreEqual(40, r.Bjorn.PrimaryPool.Current, "nothing more this turn");

            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn); // his turn ends: the cap starts over
            r.Session.StrikeForTest(r.Foe, r.Ally1, 10, DamageType.Physical);
            Assert.AreEqual(45, r.Bjorn.PrimaryPool.Current, "10 absorbed pays 5");
        }

        [Test]
        public void TheWallEndsWithItsHolder()
        {
            var r = Fight(withAllies: true);
            r.Bjorn.PlantedShield.CoversParty = true;
            r.Session.PlantShield(r.Bjorn);
            r.Bjorn.CurrentHealth = 0;

            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);

            Assert.AreEqual(100, r.Ally1.CurrentHealth);
            Assert.IsFalse(r.Bjorn.PlantedShield.IsPlaced);
        }

        [Test]
        public void TheWallCoversAnAllyWhoJoinedAfterItWentUp()
        {
            var r = Fight(withAllies: true);
            r.Ally2.CurrentHealth = 0; // down when the wall goes up
            r.Bjorn.PlantedShield.CoversParty = true;
            r.Session.PlantShield(r.Bjorn);

            r.Ally2.CurrentHealth = 200; // back in the fight
            r.Session.StrikeForTest(r.Foe, r.Ally2, 100, DamageType.Physical);

            Assert.AreEqual(200, r.Ally2.CurrentHealth);
            Assert.AreEqual(160, StatusEffects.WardPoints(r.Bjorn));
        }

        // ---- reactive hooks ----------------------------------------------------------

        [Test]
        public void Thornwall_ReturnsItsPercentOfAPhysicalHitFromAPhysicalMove()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ThornsPercent = 15;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical); // 100
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.AreEqual(985, r.Foe.CurrentHealth);
        }

        [Test]
        public void Thornwall_IgnoresAPhysicalHitThatWasNotAPhysicalMove()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ThornsPercent = 15;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.AreEqual(1000, r.Foe.CurrentHealth);
        }

        [Test]
        public void Thornwall_AnswersTheShareOfAnAllysHitTheWallAte()
        {
            var r = Fight(withAllies: true);
            r.Bjorn.PlantedShield.ThornsPercent = 15;
            r.Bjorn.PlantedShield.CoversParty = true;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.AreEqual(985, r.Foe.CurrentHealth);
        }

        [Test]
        public void Spellbreaker_ReflectsItsPercentOfAMagicHit_AsThatHitsType()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ReflectMagicPercent = 15;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire); // 100
            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            Assert.AreEqual(985, r.Foe.CurrentHealth);
            Assert.AreEqual(15, r.Session.Ledger.For("bjorn").OtherDealt);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            Assert.AreEqual(985, r.Foe.CurrentHealth, "physical is not magic");
        }

        [Test]
        public void Spellbreaker_RaisesTheSilenceHookOncePerCasterPerAction()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SilenceCasterOnSpellHit = true;
            var raised = new List<(CombatantState holder, CombatantState caster)>();
            r.Session.SpellHitShieldHolder += (holder, caster) => raised.Add((holder, caster));

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire);
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Ice);
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.AreEqual(1, raised.Count);
            Assert.AreSame(r.Bjorn, raised[0].holder);
            Assert.AreSame(r.Foe, raised[0].caster);
        }

        [Test]
        public void ARealEnemySwing_SettlesTheThornsAfterItsAction()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ThornsPercent = 50;
            r.Foe.Attack = 60;

            // Hand the turn round until the foe has swung at him: its plain
            // attack is a physical move, settled at the enemy seam.
            var said = new List<string>();
            for (int i = 0; i < 40 && !r.Session.IsOver; i++)
            {
                if (r.Session.IsPlayerTurn) r.Session.ExecuteAttack(r.Foe);
                else r.Session.AutoResolveEnemyTurns();

                said.AddRange(Messages(r.Session));
                if (said.Any(m => m.StartsWith("Foe attacks Bjorn"))) break;
            }

            int swing = said.FindIndex(m => m.StartsWith("Foe attacks Bjorn"));
            int thorns = said.FindIndex(m => m.StartsWith("Bjorn's thorns strike Foe for "));
            Assert.GreaterOrEqual(swing, 0, "fixture: the foe swung");
            Assert.Greater(thorns, swing, string.Join(" | ", said));
        }

        // ---- off by default ----------------------------------------------------------

        [Test]
        public void OffByDefault_AHitIsTheWardStepAsItWas()
        {
            var r = Fight(withAllies: true);
            StatusEffects.ApplyWard(r.Bjorn.Statuses, 30, 2, r.Ally1);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);  // 100: 30 ward, 70 health
            r.Session.StrikeForTest(r.Foe, r.Ally1, 100, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            var shield = r.Bjorn.PlantedShield;
            Assert.IsFalse(shield.IsPlaced);
            Assert.IsTrue(shield.CanPlace);
            Assert.AreEqual(0, shield.ThornsPercent + shield.ReflectMagicPercent + shield.BreakShardDamage);
            Assert.IsFalse(shield.SilenceCasterOnSpellHit || shield.CoversParty || shield.ShortWaitAfterBash);
            Assert.AreEqual(190, r.Bjorn.CurrentHealth);
            Assert.AreEqual(100, r.Ally1.CurrentHealth);
            Assert.AreEqual(1000, r.Foe.CurrentHealth);
            Assert.AreEqual(0, r.Bjorn.PrimaryPool.Current);
        }
    }
}
