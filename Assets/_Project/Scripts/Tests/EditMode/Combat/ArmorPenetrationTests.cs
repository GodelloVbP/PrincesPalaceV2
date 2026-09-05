using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (e): a flat amount subtracted from the target's PHYSICAL
    // armor before mitigation, floored at 0, carried on the attacker.
    public class ArmorPenetrationTests
    {
        private static CombatantState Attacker(int armorPenetration)
        {
            var state = new CombatantState("Attacker", true, 100, 0, 20, 10) { ArmorPenetration = armorPenetration };
            return state;
        }

        private static CombatantState Target(int physicalDefense, int magicalDefense = 0)
        {
            return new CombatantState("Target", false, 100, 0, 10, 10)
            {
                PhysicalDefense = physicalDefense,
                MagicalDefense = magicalDefense,
            };
        }

        [Test]
        public void FlatPenetrationSubtractsFromPhysicalDefense()
        {
            var attacker = Attacker(35);
            var target = Target(100);

            int broad = CombatMath.BroadDefense(target, DamageType.Physical, attacker, ignoresDefense: false);

            Assert.AreEqual(65, broad, "100 physical defense - 35 flat penetration = 65");
        }

        [Test]
        public void PenetrationIsFlooredAtZero()
        {
            var attacker = Attacker(150);
            var target = Target(100);

            int broad = CombatMath.BroadDefense(target, DamageType.Physical, attacker, ignoresDefense: false);

            Assert.AreEqual(0, broad, "150 penetration against 100 defense must floor at 0, not go negative");
        }

        [Test]
        public void PenetrationDoesNotTouchNonPhysicalDefense()
        {
            var attacker = Attacker(35);
            var target = Target(physicalDefense: 100, magicalDefense: 100);

            int broad = CombatMath.BroadDefense(target, DamageType.Fire, attacker, ignoresDefense: false);

            Assert.AreEqual(100, broad, "armor penetration is authored as melee-only -- it must not touch a non-physical defense");
        }

        [Test]
        public void NoPenetrationLeavesDefenseUntouched()
        {
            var attacker = Attacker(0);
            var target = Target(100);

            Assert.AreEqual(100, CombatMath.BroadDefense(target, DamageType.Physical, attacker, ignoresDefense: false));
        }
    }
}
