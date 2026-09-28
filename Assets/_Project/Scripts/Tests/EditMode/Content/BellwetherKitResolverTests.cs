using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The Bellwether kit's three skill fields (docs/PLAN_BELLWETHER_KIT.md
    // M2): damageType, damageBySeatMaxHpPercent, toSeat. One named refusal
    // per way an author can write a field that would be read by nothing.
    public class BellwetherKitResolverTests
    {
        private static RawSkillEntry Monster(string effect = "DamageSingle") =>
            new RawSkillEntry
            {
                id = "knell", displayName = "Death Knell", characterId = "bellwether",
                effect = effect, manaCost = 0, playerSelectable = false,
            };

        private static RawSkillEntry Player(string effect, string targeting) =>
            new RawSkillEntry
            {
                id = "call", displayName = "Call to the Front", characterId = "sheep",
                effect = effect, targeting = targeting, manaCost = 5,
            };

        private static ResolvedSkill Resolves(RawSkillEntry entry)
        {
            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        private static void Refuses(RawSkillEntry entry, string fragment)
        {
            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);
            Assert.IsFalse(ok, "the row resolved");
            StringAssert.Contains(fragment, errors[0]);
        }

        // ---- the shapes M5 authors ---------------------------------------------

        [Test]
        public void AVoidSeatSizedKnellResolves()
        {
            var entry = Monster();
            entry.damageType = "Void";
            entry.damageBySeatMaxHpPercent = new[] { 110, 35, 0 };

            var skill = Resolves(entry);

            Assert.AreEqual(DamageType.Void, skill.OwnDamageType);
            CollectionAssert.AreEqual(new[] { 110, 35, 0 }, skill.DamageBySeatMaxHpPercent);
            Assert.AreEqual(0, skill.ToSeat);
        }

        [Test]
        public void ChainsToTheFrontResolve()
        {
            var entry = Monster("Reposition");
            entry.toSeat = 1;

            var skill = Resolves(entry);

            Assert.AreEqual(SkillEffect.Reposition, skill.Effect);
            Assert.AreEqual(SkillTargeting.SingleEnemy, skill.Targeting, "a monster's SingleEnemy is a party member");
            Assert.AreEqual(1, skill.ToSeat);
            Assert.IsNull(skill.OwnDamageType);
        }

        [Test]
        public void APlayerRepositionAimedAtAnAllyResolves()
        {
            var entry = Player("Reposition", "SingleAlly");
            entry.toSeat = 3;

            Assert.AreEqual(3, Resolves(entry).ToSeat);
        }

        [Test]
        public void AMonsterHitThatDragsResolves()
        {
            var entry = Monster();
            entry.flatAmount = 10;
            entry.toSeat = 1;

            Assert.AreEqual(1, Resolves(entry).ToSeat);
        }

        // ---- damageType ------------------------------------------------------

        [Test]
        public void AnUnknownDamageTypeIsRefused()
        {
            var entry = Monster();
            entry.damageType = "Shadow";
            Refuses(entry, "damageType 'Shadow' isn't valid");
        }

        [Test]
        public void DamageTypeOnAnEffectThatDealsNoDamageIsRefused()
        {
            var entry = Monster("Reposition");
            entry.toSeat = 1;
            entry.damageType = "Void";
            Refuses(entry, "damageType types a skill's damage, and a Reposition deals none");
        }

        [Test]
        public void DamageTypeBesideDamageInstancesIsRefused()
        {
            var entry = Monster();
            entry.damageType = "Void";
            entry.damageInstances = new[] { new RawDamageInstance { type = "Fire", amount = 5 } };
            Refuses(entry, "damageType has no meaning beside damageInstances");
        }

        // ---- damageBySeatMaxHpPercent ------------------------------------------

        [Test]
        public void ASeatTableOfTheWrongLengthIsRefused()
        {
            var entry = Monster();
            entry.damageBySeatMaxHpPercent = new[] { 100, 30 };
            Refuses(entry, "has 2 entries; it needs exactly 3");
        }

        [Test]
        public void ASeatEntryAbove1000IsRefused()
        {
            var entry = Monster();
            entry.damageBySeatMaxHpPercent = new[] { 1001, 30, 0 };
            Refuses(entry, "entry #1 is 1001");
        }

        [Test]
        public void ANegativeSeatEntryIsRefused()
        {
            var entry = Monster();
            entry.damageBySeatMaxHpPercent = new[] { 100, -1, 0 };
            Refuses(entry, "entry #2 is -1");
        }

        [Test]
        public void ASeatTableOfAllZeroesIsRefused()
        {
            var entry = Monster();
            entry.damageBySeatMaxHpPercent = new[] { 0, 0, 0 };
            Refuses(entry, "0 at every seat");
        }

        [Test]
        public void ASeatTableOnASweepIsRefused()
        {
            var entry = Monster("DamageAll");
            entry.damageBySeatMaxHpPercent = new[] { 100, 30, 0 };
            Refuses(entry, "and a DamageAll is not one");
        }

        [Test]
        public void ASeatTableWithFlatAmountIsRefused()
        {
            var entry = Monster();
            entry.damageBySeatMaxHpPercent = new[] { 100, 30, 0 };
            entry.flatAmount = 10;
            Refuses(entry, "power, flatAmount, scalingAxis and poolTiers are never read by it");
        }

        [Test]
        public void ASeatTableWithDamageInstancesIsRefused()
        {
            var entry = Monster();
            entry.damageBySeatMaxHpPercent = new[] { 100, 30, 0 };
            entry.damageInstances = new[] { new RawDamageInstance { type = "Void", amount = 5 } };
            Refuses(entry, "both say what the hit deals");
        }

        // ---- toSeat ----------------------------------------------------------

        [Test]
        public void ARepositionWithNoSeatIsRefused()
        {
            Refuses(Monster("Reposition"), "a Reposition skill needs a toSeat");
        }

        [Test]
        public void ASeatPastTheRearIsRefused()
        {
            var entry = Monster("Reposition");
            entry.toSeat = 4;
            Refuses(entry, "toSeat is 4; seats are 1-3");
        }

        [Test]
        public void ToSeatOnAnEffectThatMovesNobodyIsRefused()
        {
            var entry = Monster("HealSelf");
            entry.flatAmount = 10;
            entry.toSeat = 1;
            Refuses(entry, "and a HealSelf moves nobody");
        }

        [Test]
        public void APlayerRepositionAimedAtAnEnemyIsRefused()
        {
            var entry = Player("Reposition", "SingleEnemy");
            entry.toSeat = 1;
            Refuses(entry, "toSeat moves a party member");
        }

        [Test]
        public void AMonsterRepositionAimedAtItsOwnSideIsRefused()
        {
            var entry = Monster("Reposition");
            entry.targeting = "SingleAlly";
            entry.toSeat = 1;
            Refuses(entry, "toSeat moves a party member");
        }

        [Test]
        public void APlayerDamageRowWithToSeatIsRefused()
        {
            var entry = Player("DamageSingle", "SingleEnemy");
            entry.toSeat = 1;
            Refuses(entry, "toSeat moves a party member");
        }

        [Test]
        public void ARepositionThatAuthorsDamageIsRefused()
        {
            var entry = Monster("Reposition");
            entry.toSeat = 1;
            entry.flatAmount = 10;
            Refuses(entry, "a Reposition moves its target and deals no damage");
        }

        [Test]
        public void APlaceholderMayNotAuthorTheKitFields()
        {
            var entry = new RawSkillEntry
            {
                id = "undesigned", displayName = "Undecided", characterId = "sheep",
                placeholder = true, placeholderNote = "not written yet", toSeat = 1,
            };
            Refuses(entry, "placeholder skills may not author any effect field");
        }
    }
}
