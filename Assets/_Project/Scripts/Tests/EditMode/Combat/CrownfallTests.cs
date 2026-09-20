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
    // CROWNFALL (plan 2.4): reads and spends the GENERAL Marked status
    // (Marks.cs), never the Drowned Lantern's own private _marked set --
    // the two are independent mechanics a target may carry either, neither
    // or both of (1.8). Driven through CastSkill throughout, because the
    // ordering under test (a read before the roll, a spend after it lands)
    // is the dispatcher's own.
    public class CrownfallTests
    {
        private static ResolvedSkill Crownfall() =>
            new ResolvedSkill("crownfall", "Crownfall", "test fixture", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Arcane, 7) },
                SpellPresentation.None, 0,
                consumesStatus: StatusEffectType.Marked,
                damageInstancesIfConsumed: new[] { new DamageInstance(DamageType.Arcane, 12) });

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            bool giveDrownedLantern = false, params ModifierEffect[] foeEffects)
        {
            var hero = new CombatantState("Hero", true, 500, 50, 40, 10);
            var foe = new CombatantState("Foe", false, 1000, 10, 5, 1);
            if (foeEffects.Length > 0) foe.ModifierEffects = new ModifierEffectSet(foeEffects);

            var relics = giveDrownedLantern
                ? new List<ResolvedRelic> { new ResolvedRelic("lantern", "Drowned Lantern", "", RelicEffect.DrownedLantern, 0) }
                : null;

            var skills = new List<ResolvedSkill> { TestSkills.Bolt(flat: 1), Crownfall() };
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, relics, null);

            var enemyKit = new EnemyKit(
                new ResolvedEnemy("foe", "Foe", new StatBlock(), 0, 0, false, DamageType.Physical, DamageType.Physical, 0),
                false);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, new List<EnemyKit> { enemyKit }, new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
            };
            return (session, hero, foe);
        }

        [Test]
        public void AnUnmarkedTarget_TakesTheBaseSevenPacket()
        {
            var (session, _, foe) = Fight();
            int before = foe.CurrentHealth;

            session.CastSkill(1, foe);

            Assert.AreEqual(7, before - foe.CurrentHealth);
        }

        [Test]
        public void AGenerallyMarkedTarget_TakesTheHeavierTwelvePacket_AndTheMarkIsGone()
        {
            var (session, hero, foe) = Fight();
            Marks.Apply(foe, hero);
            int before = foe.CurrentHealth;

            session.CastSkill(1, foe);

            Assert.AreEqual(12, before - foe.CurrentHealth);
            Assert.IsFalse(Marks.IsMarked(foe), "a landed hit must spend the mark it read");
        }

        // THE LANTERN'S OWN MARK IS A DIFFERENT QUESTION. Marking a target
        // through the relic (which needs no book, no Crownfall in hand) must
        // not raise Crownfall's damage -- only the general Marked status
        // does that.
        [Test]
        public void ALanternMarkAlone_DoesNotRaiseCrownfallsDamage()
        {
            var (session, hero, foe) = Fight(giveDrownedLantern: true);

            // A damaging cast with the Lantern equipped marks the target
            // through FightSession's own private _marked set.
            session.CastSkill(0, foe);
            Assert.IsTrue(session.IsMarked(foe), "the Lantern should have marked the target");
            Assert.IsFalse(Marks.IsMarked(foe), "the Lantern's mark must not be the general Marked status");

            int before = foe.CurrentHealth;
            session.CastSkill(1, foe);

            Assert.AreEqual(7, before - foe.CurrentHealth,
                "Crownfall must read the general Marked status, never the Lantern's own private mark");
        }

        [Test]
        public void ADodgedCrownfall_LeavesTheMarkOnTheTarget()
        {
            var (session, hero, foe) = Fight(false, new ModifierEffect(ModifierEffectType.DodgeRating, 100_000));
            Marks.Apply(foe, hero);
            int before = foe.CurrentHealth;

            session.CastSkill(1, foe);

            Assert.AreEqual(before, foe.CurrentHealth, "a dodged Crownfall deals nothing");
            Assert.IsTrue(Marks.IsMarked(foe), "consumption happens after the miss check -- a dodge must not spend the mark");
        }

        [Test]
        public void ALandedCrownfall_SpendsTheMark_AndASecondCastIsBackToSeven()
        {
            var (session, hero, foe) = Fight();
            Marks.Apply(foe, hero);

            session.CastSkill(1, foe);
            int before = foe.CurrentHealth;
            session.CastSkill(1, foe);

            Assert.AreEqual(7, before - foe.CurrentHealth, "the mark is gone -- the second cast is the ordinary packet");
        }
    }
}
