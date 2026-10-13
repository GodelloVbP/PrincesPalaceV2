using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHAT THE FIGHT HAS DONE TO A PARTY MEMBER'S STATS (plan C4), read off the
    // same session state the HUD's badges are drawn from. Only what ends with
    // the fight is listed: the sheet's own figure is the base.
    public class FightModifiersTests
    {
        private static (FightSession session, CombatantState hero) Fight(params ResolvedRelic[] relics)
        {
            var hero = new CombatantState("Shawn", true, 200, 10, 20, 20);
            var foe = new CombatantState("Dummy", false, 999999, 0, 1, 1);

            var kit = new PlayerKit("hero", CharacterRole.Tank, null, new List<ResolvedRelic>(relics), null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(),
                    0, 0, false, DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(5));
            TestRelics.Hold(session);
            session.Begin();
            return (session, hero);
        }

        [Test]
        public void ANewMemberHasNoModifiers()
        {
            var (session, hero) = Fight();

            CollectionAssert.IsEmpty(FightModifiers.For(session, hero));
        }

        [Test]
        public void ARelicSpeedBuffIsNamedForTheRelic()
        {
            var pipe = new ResolvedRelic("tin_foil_pipe", "Tin-Foil Pipe", "", new[] { TestRelics.Mechanic(EffectKind.TinFoilPipe) }, 0);
            var (session, hero) = Fight(pipe);

            session.GrantSpeedPercentForTest(hero, EffectKind.TinFoilPipe, 20, 3);

            var modifiers = FightModifiers.For(session, hero);
            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual(SheetStat.Speed, modifiers[0].Stat);
            Assert.AreEqual(4, modifiers[0].Delta, "20 base + 20% is 24");
            Assert.AreEqual("Tin-Foil Pipe", modifiers[0].Source);
        }

        [Test]
        public void ChilledIsASpeedMalusNamedForTheStatus()
        {
            var (session, hero) = Fight();

            session.ApplyChilledForTest(hero, 25, 2);

            var modifiers = FightModifiers.For(session, hero);
            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual(SheetStat.Speed, modifiers[0].Stat);
            Assert.AreEqual(-5, modifiers[0].Delta, "25% of 20");
            Assert.AreEqual("Chilled", modifiers[0].Source);
        }

        [Test]
        public void ShreddedArmourIsAPhysicalDefenceLoss()
        {
            var (session, hero) = Fight();
            hero.PermanentPhysicalDefenseShred = 12;

            var modifiers = FightModifiers.For(session, hero);

            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual(SheetStat.PhysicalDefense, modifiers[0].Stat);
            Assert.AreEqual(-12, modifiers[0].Delta);
            Assert.AreEqual("Armour shred", modifiers[0].Source);
        }

        // A form's defence is converted into attack: the attack it reports
        // already includes those points, so the two defences show the loss.
        [Test]
        public void AFormListsItsGrantsAndItsConversions()
        {
            var (session, hero) = Fight();
            hero.PhysicalDefense = 40;
            hero.MagicalDefense = 20;

            Transformation.Enter(hero, "Black Ram", 3, attackPercent: 50, speedPercent: 25,
                temporaryHealthPercentOfMax: 0, defenseToAttackPercent: 50);

            var facts = new FightMemberFacts("hero", 200, 200, "Mana", 0, 0, null, 0, 0, FightModifiers.For(session, hero));

            Assert.AreEqual(5, facts.DeltaFor(SheetStat.Speed), "25% of 20");
            Assert.AreEqual(-20, facts.DeltaFor(SheetStat.PhysicalDefense), "half of 40");
            Assert.AreEqual(-10, facts.DeltaFor(SheetStat.MagicalDefense), "half of 20");
            Assert.AreEqual(40, facts.DeltaFor(SheetStat.Attack), "50% of Attack 20 is 10, plus the 30 converted defence");
            Assert.AreEqual("Black Ram", facts.SourcesFor(SheetStat.Speed));
        }

        [Test]
        public void NoMemberIsNoModifiers()
        {
            CollectionAssert.IsEmpty(FightModifiers.For(null, null));
        }
    }
}
