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
    // The Bellwether kit's engine pieces (docs/PLAN_BELLWETHER_KIT.md M2):
    // Bleed, a skill's own damageType, the seat-sized hit (percent of the
    // target's max health by the seat it stands in) and the Reposition
    // effect. Every figure is a literal; no fixture recomputes a formula.
    // The seat table here, [110, 35, 0], is a fixture, not the knell's
    // content -- M5 authors that.
    public class BellwetherKitMechanicsTests
    {
        private static CombatantState Member(string name, int health = 1000, int speed = 10) =>
            new CombatantState(name, true, health, 10, 20, speed);

        // Attack 0: an Attack-scaled skill's raw figure is then exactly its
        // flatAmount (SkillResolution.Damage: scaled attack + flat + power).
        private static CombatantState Bellwether() => new CombatantState("Bellwether", false, 100000, 0, 0, 1);

        private static FightSession Fight(CombatantState[] party, CombatantState enemy,
            List<PlayerKit> kits = null)
        {
            var session = new FightSession(
                new CombatEncounter(party, new[] { enemy }), kits, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return session;
        }

        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages());

        private static ResolvedSkill MonsterSkill(SkillEffect effect, int flatAmount = 0,
            DamageType? damageType = null, int[] bySeat = null, int toSeat = 0,
            StatusEffectType? appliesStatus = null, bool ignoresDefense = false) =>
            new ResolvedSkill("fixture", "Death Knell", "", "bellwether", 1, effect,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, flatAmount, ignoresDefense,
                null, SpellPresentation.None, 0,
                appliesStatus: appliesStatus, statusMagnitude: appliesStatus.HasValue ? 25 : 0,
                statusDuration: appliesStatus.HasValue ? 2 : 0,
                playerSelectable: false,
                damageType: damageType, damageBySeatMaxHpPercent: bySeat, toSeat: toSeat);

        private static ElementalAffinity Weak(DamageType type) =>
            ElementalAffinity.Of(new[] { type }, new DamageType[0]);

        private static ElementalAffinity Resists(DamageType type) =>
            ElementalAffinity.Of(new DamageType[0], new[] { type });

        // ---- Bleed ---------------------------------------------------------

        [Test]
        public void ABleedTickAgainstNoArmour_DealsItsSnapshot()
        {
            var holder = Member("Shawn");
            holder.PhysicalDefense = 0;
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 20, 3));

            var report = StatusEffects.Tick(holder);

            Assert.AreEqual(20, report.Rows.Single(r => r.Status == StatusEffectType.Bleed).ToHealth);
            Assert.AreEqual(980, holder.CurrentHealth);
        }

        [Test]
        public void ABleedTickMeetsPhysicalDefenceOnTheHitCurve()
        {
            // 20 * 100 / (100 + 100) = 10; magical defence is not asked.
            var holder = Member("Shawn");
            holder.PhysicalDefense = 100;
            holder.MagicalDefense = 999999;
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 20, 3));

            StatusEffects.Tick(holder);

            Assert.AreEqual(990, holder.CurrentHealth);
        }

        [Test]
        public void ABleedTickAgainstHugeArmourStillDealsOne()
        {
            var holder = Member("Shawn");
            holder.PhysicalDefense = 999999;
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 20, 3));

            StatusEffects.Tick(holder);

            Assert.AreEqual(999, holder.CurrentHealth, "floor 1, the same floor a hit has");
        }

        [Test]
        public void ABleedTickOnAPhysicalWeakHolder_IsAffinityFirstThenArmour()
        {
            // 20 x1.5 = 30, then 30 * 100 / 200 = 15.
            var holder = Member("Shawn");
            holder.PhysicalDefense = 100;
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 20, 3));

            StatusEffects.Tick(holder, Weak(DamageType.Physical));

            Assert.AreEqual(985, holder.CurrentHealth);
        }

        [Test]
        public void ABleedTickOnAPhysicalResistantHolder_IsHalvedThenArmoured()
        {
            // 20 x0.5 = 10, then 10 * 100 / 200 = 5.
            var holder = Member("Shawn");
            holder.PhysicalDefense = 100;
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 20, 3));

            StatusEffects.Tick(holder, Resists(DamageType.Physical));

            Assert.AreEqual(995, holder.CurrentHealth);
        }

        [Test]
        public void ABleedTickSpendsNoWardAndIgnoresVulnerable()
        {
            var holder = Member("Shawn");
            holder.PhysicalDefense = 0;
            StatusEffects.ApplyWard(holder.Statuses, 50, 5);
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Vulnerable, 50, 3));
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 20, 3));

            StatusEffects.Tick(holder);

            Assert.AreEqual(980, holder.CurrentHealth, "no Vulnerable markup, nothing off the ward");
            Assert.AreEqual(50, StatusEffects.WardPoints(holder), "the ward is untouched");
        }

        [Test]
        public void BleedLastsItsAuthoredTicksThenGoes()
        {
            var holder = Member("Shawn");
            holder.PhysicalDefense = 0;
            holder.Statuses.Add(new ActiveStatus(StatusEffectType.Bleed, 10, 2));

            StatusEffects.Tick(holder);
            Assert.IsTrue(holder.Statuses.Any(s => s.Type == StatusEffectType.Bleed), "one tick left");

            var last = StatusEffects.Tick(holder);
            Assert.IsFalse(holder.Statuses.Any(s => s.Type == StatusEffectType.Bleed), "two ticks, then gone");
            CollectionAssert.Contains(last.Expired, StatusEffectType.Bleed);

            StatusEffects.Tick(holder);
            Assert.AreEqual(980, holder.CurrentHealth, "exactly two ticks of 10");
        }

        [Test]
        public void TwoBleedsStackAsTwoInstancesAndTickTogether()
        {
            // The DoT rule (StackPolicyOf: Stack, owner 2026-09-20), which
            // the plan names for Bleed (1.3: "stacks like the other DoTs").
            var holder = Member("Shawn");
            holder.PhysicalDefense = 0;
            StatusEffects.Apply(holder.Statuses, StatusEffectType.Bleed, 10, 3);
            StatusEffects.Apply(holder.Statuses, StatusEffectType.Bleed, 6, 2);

            Assert.AreEqual(2, StatusEffects.InstancesOf(holder, StatusEffectType.Bleed).Count());

            var report = StatusEffects.Tick(holder);

            Assert.AreEqual(16, report.Rows.Single(r => r.Status == StatusEffectType.Bleed).ToHealth,
                "one row for the type, the two instances summed");
            Assert.AreEqual(1, report.Rows.Count);
        }

        [Test]
        public void BleedAnswersEveryStatusTable()
        {
            Assert.AreEqual(DamageType.Physical, StatusEffects.ElementOf(StatusEffectType.Bleed));
            Assert.AreEqual(StatusMitigation.Armoured, StatusEffects.MitigationOf(StatusEffectType.Bleed));
            Assert.AreEqual(StatusClock.AtTick, StatusEffects.DurationClock(StatusEffectType.Bleed));
            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Bleed));
            Assert.IsTrue(StatusEffects.CarriesMagnitude(StatusEffectType.Bleed));
            Assert.AreEqual("BLD", StatusHud.CodeFor(StatusEffectType.Bleed));
            Assert.AreEqual("bleed", StatusHud.SlugFor(StatusEffectType.Bleed));
        }

        [Test]
        public void ABleedFromASkillIsSnapshottedOnceAtApplication()
        {
            // No caster, no potency: the authored 12 is what is stored.
            var shawn = Member("Shawn");
            shawn.PhysicalDefense = 0;
            var session = Fight(new[] { shawn }, Bellwether());

            session.ApplyStatusToForTest(shawn, StatusEffectType.Bleed, 12, 3, null);
            session.TickStatusesForTest(shawn);

            Assert.AreEqual(988, shawn.CurrentHealth);
        }

        // ---- a skill's own damageType ------------------------------------------

        [Test]
        public void AVoidSkillFromAPhysicalCasterMeetsMagicalDefence()
        {
            // flat 100 raw; Void meets magical defence 100: 100 * 100 / 200 = 50.
            var shawn = Member("Shawn");
            shawn.PhysicalDefense = 0;
            shawn.MagicalDefense = 100;
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            session.DrainBeats();

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, flatAmount: 100,
                damageType: DamageType.Void), shawn);

            Assert.AreEqual(950, shawn.CurrentHealth);
            Assert.AreEqual(DamageType.Void, session.DrainBeats().Last().DamageType,
                "the beat declares the skill's type, not its caster's");
        }

        [Test]
        public void WithoutDamageTypeTheSameSkillIsItsCastersPhysical()
        {
            // The control: same numbers, no override -> physical defence 0.
            var shawn = Member("Shawn");
            shawn.PhysicalDefense = 0;
            shawn.MagicalDefense = 100;
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, flatAmount: 100), shawn);

            Assert.AreEqual(900, shawn.CurrentHealth);
        }

        [Test]
        public void CastTypeOfPrefersTheSkillsOwnType()
        {
            var bell = Bellwether();
            var session = Fight(new[] { Member("Shawn") }, bell);

            Assert.AreEqual(DamageType.Void,
                session.CastTypeOf(bell, MonsterSkill(SkillEffect.DamageSingle, damageType: DamageType.Void)));
            Assert.AreEqual(DamageType.Physical,
                session.CastTypeOf(bell, MonsterSkill(SkillEffect.DamageSingle)));
        }

        // ---- damage by seat (percent of the target's max health) ---------------

        private static readonly int[] KnellFixture = { 110, 35, 0 };

        [Test]
        public void AtTheFrontTheSeatHitIs110PercentAndKillsAFullHealthShawn()
        {
            var shawn = Member("Shawn");
            shawn.MagicalDefense = 0;
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);

            Assert.AreEqual(1100, session.SeatSizedDamageBase(MonsterSkill(SkillEffect.DamageSingle,
                damageType: DamageType.Void, bySeat: KnellFixture), shawn));

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle,
                damageType: DamageType.Void, bySeat: KnellFixture), shawn);

            Assert.IsFalse(shawn.IsAlive);
        }

        [Test]
        public void InTheMiddleTheSeatHitIs35PercentAndItsRidersLand()
        {
            var shawn = Member("Shawn");
            shawn.MagicalDefense = 0;
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(shawn, 1, out _));

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, damageType: DamageType.Void,
                bySeat: KnellFixture, appliesStatus: StatusEffectType.Vulnerable), shawn);

            Assert.AreEqual(650, shawn.CurrentHealth);
            Assert.IsTrue(shawn.Statuses.Any(s => s.Type == StatusEffectType.Vulnerable), "a landed hit's rider");
        }

        [Test]
        public void AtTheRearAZeroEntryIsNoHitAtAll()
        {
            var shawn = Member("Shawn");
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(shawn, 2, out _));
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, damageType: DamageType.Void,
                bySeat: KnellFixture, appliesStatus: StatusEffectType.Vulnerable), shawn);

            Assert.AreEqual(1000, shawn.CurrentHealth, "no number");
            Assert.IsFalse(shawn.Statuses.Any(s => s.Type == StatusEffectType.Vulnerable), "no riders");
            Assert.IsTrue(Messages(session).Any(m => m == "Death Knell passes over Shawn."));
        }

        [Test]
        public void TheSeatHitMeetsDefenceUnlessItIgnoresIt()
        {
            // Middle: 350 raw; magical defence 100 -> 175. ignoresDefense -> 350.
            var shawn = Member("Shawn");
            shawn.MagicalDefense = 100;
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            session.Encounter.PlaceAt(shawn, 1, out _);

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, damageType: DamageType.Void,
                bySeat: KnellFixture), shawn);
            Assert.AreEqual(825, shawn.CurrentHealth);

            shawn.CurrentHealth = 1000;
            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, damageType: DamageType.Void,
                bySeat: KnellFixture, ignoresDefense: true), shawn);
            Assert.AreEqual(650, shawn.CurrentHealth);
        }

        [Test]
        public void TheSeatHitIgnoresTheCastersAttack()
        {
            // A rally or a huge attack changes nothing: the base is the
            // target's bar. 35% of 1000 = 350.
            var shawn = Member("Shawn");
            shawn.MagicalDefense = 0;
            var bell = Bellwether();
            bell.Attack = 5000;
            bell.BonusAttackPercent = 64;
            var session = Fight(new[] { shawn }, bell);
            session.Encounter.PlaceAt(shawn, 1, out _);

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, damageType: DamageType.Void,
                bySeat: KnellFixture), shawn);

            Assert.AreEqual(650, shawn.CurrentHealth);
        }

        [Test]
        public void ADeadOrAbsentTargetSizesToNothing()
        {
            var shawn = Member("Shawn");
            var odette = Member("Odette");
            var session = Fight(new[] { shawn, odette }, Bellwether());
            var knell = MonsterSkill(SkillEffect.DamageSingle, bySeat: KnellFixture);

            odette.CurrentHealth = 0;

            Assert.AreEqual(0, session.SeatSizedDamageBase(knell, odette), "a corpse holds no seat");
            Assert.AreEqual(0, session.SeatSizedDamageBase(knell, null), "nobody");
            Assert.AreEqual(0, session.SeatSizedDamageBase(MonsterSkill(SkillEffect.DamageSingle), shawn),
                "not a seat-sized skill");
        }

        [Test]
        public void AnEnemysSeatIsItsLivingRank()
        {
            var front = new CombatantState("Front", false, 2000, 0, 0, 1);
            var back = new CombatantState("Back", false, 2000, 0, 0, 1);
            var session = new FightSession(
                new CombatEncounter(new[] { Member("Shawn") }, new[] { front, back }), null, null, new SeededRandom(1));
            session.Begin();
            var knell = MonsterSkill(SkillEffect.DamageSingle, bySeat: KnellFixture);

            Assert.AreEqual(2200, session.SeatSizedDamageBase(knell, front));
            Assert.AreEqual(700, session.SeatSizedDamageBase(knell, back));
        }

        // ---- Reposition ------------------------------------------------------

        [Test]
        public void ChainsPullASoloShawnFromTheRearToTheFront()
        {
            var shawn = Member("Shawn");
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            session.Encounter.PlaceAt(shawn, 2, out _);
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 1), shawn);

            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(1000, shawn.CurrentHealth, "a Reposition deals no damage");
            Assert.IsTrue(Messages(session).Any(m => m == "Shawn is dragged to the front."));
        }

        [Test]
        public void ChainsTradeShawnWithTheFrontOccupant()
        {
            var odette = Member("Odette");
            var bjorn = Member("Bjorn");
            var shawn = Member("Shawn");
            var bell = Bellwether();
            var session = Fight(new[] { odette, bjorn, shawn }, bell);
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 1), shawn);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 },
                new[] { shawn, bjorn, odette }.Select(m => session.Encounter.SeatOf(m)).ToArray());
            Assert.IsTrue(Messages(session).Any(m => m == "Shawn is dragged to the front, trading places with Odette."));
        }

        [Test]
        public void RepositionPlacesInTheMiddleAndTheRear()
        {
            var shawn = Member("Shawn");
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 2), shawn);
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 3), shawn);
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
        }

        [Test]
        public void ARootedTargetIsHeldAndTheChainsSaySo()
        {
            var shawn = Member("Shawn");
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            session.Encounter.PlaceAt(shawn, 2, out _);
            StatusEffects.Apply(shawn.Statuses, StatusEffectType.Rooted, 0, 2);
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 1), shawn);

            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.IsTrue(Messages(session).Any(m => m == "Shawn is rooted and does not move."));
        }

        [Test]
        public void ARootedOccupantHoldsTheSeatAndNothingMoves()
        {
            var odette = Member("Odette");
            var shawn = Member("Shawn");
            var bell = Bellwether();
            var session = Fight(new[] { odette, shawn }, bell);
            StatusEffects.Apply(odette.Statuses, StatusEffectType.Rooted, 0, 2);
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 1), shawn);

            Assert.AreEqual(0, session.Encounter.SeatOf(odette));
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));
            Assert.IsTrue(Messages(session).Any(m => m == "Odette is rooted, and Shawn does not move."));
        }

        [Test]
        public void AMonstersPullPaysNoBuckler()
        {
            var shawn = Member("Shawn");
            var buckler = new List<ResolvedRelic>
            {
                new ResolvedRelic("buckler", "Sparring Buckler", "", RelicEffect.SparringBuckler, 0),
            };
            var kits = new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, buckler, DamageType.Physical) };
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell, kits);
            session.Encounter.PlaceAt(shawn, 2, out _);

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.Reposition, toSeat: 1), shawn);

            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            Assert.IsFalse(StatusEffects.IsWarded(shawn), "an enemy moving him is not his footwork");
        }

        [Test]
        public void AMonsterHitWithToSeatDragsTheTargetAfterItLands()
        {
            var shawn = Member("Shawn");
            shawn.PhysicalDefense = 0;
            var bell = Bellwether();
            var session = Fight(new[] { shawn }, bell);
            session.Encounter.PlaceAt(shawn, 2, out _);

            session.ResolveSkillForTest(bell, MonsterSkill(SkillEffect.DamageSingle, flatAmount: 100, toSeat: 1), shawn);

            Assert.AreEqual(900, shawn.CurrentHealth);
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
        }

        // ---- the Move row's caption ------------------------------------------

        [Test]
        public void TheMoveRowSaysRootedNotNoRoomWhenARootHolds()
        {
            var shawn = Member("Shawn");
            var session = Fight(new[] { shawn }, Bellwether());
            var rowsFree = FightHudModel.MoveRows(session, shawn);
            Assert.AreEqual("NO ROOM", rowsFree[0].Cost, "front seat, forward: there is no seat");
            Assert.AreEqual("ENDS TURN", rowsFree[1].Cost);

            StatusEffects.Apply(shawn.Statuses, StatusEffectType.Rooted, 0, 2);
            var rowsRooted = FightHudModel.MoveRows(session, shawn);

            Assert.AreEqual("ROOTED", rowsRooted[0].Cost, "a rooted actor is held in every direction");
            Assert.AreEqual("ROOTED", rowsRooted[1].Cost, "the empty middle is there; the root is what holds");
            Assert.AreEqual(PlaceOutcome.MemberRooted, session.MoveOutcome(shawn, MoveDirection.Back));
        }

        [Test]
        public void TheMoveRowSaysRootedWhenTheOccupantIsRooted()
        {
            var front = Member("Front", speed: 1);
            var shawn = Member("Shawn", speed: 100);
            var session = Fight(new[] { front, shawn }, Bellwether());
            StatusEffects.Apply(front.Statuses, StatusEffectType.Rooted, 0, 2);

            Assert.AreEqual(PlaceOutcome.OccupantRooted, session.MoveOutcome(shawn, MoveDirection.Forward));
            Assert.AreEqual("ROOTED", FightHudModel.MoveRows(session, shawn)[0].Cost);
            Assert.AreEqual("ENDS TURN", FightHudModel.MoveRows(session, shawn)[1].Cost,
                "back into the empty rear is legal");
        }
    }
}
