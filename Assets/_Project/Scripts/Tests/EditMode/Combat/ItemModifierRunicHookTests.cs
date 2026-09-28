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
    // Runic's two combat-time behaviours that need an authored skill cast to
    // exercise (the mana pool/regen halves are wired at FightEncounterAdapter,
    // covered by a PlayMode test with a real "runic" modifier equipped
    // instead -- Domain has no ContentDatabase to build a real Character
    // through). Mirrors SkillDispatchTests' own Hero/Skill/Kit/Fight fixture
    // shape so a real ResolvedSkill can be cast with no ContentDatabase
    // involved at all.
    public class ItemModifierRunicHookTests
    {
        private static CombatantState Hero(string name = "Hero", int health = 500, int mana = 100, int attack = 20, int speed = 10) =>
            new CombatantState(name, true, health, mana, attack, speed);

        private static CombatantState Foe(string name = "Foe", int health = 1000, int speed = 1) =>
            new CombatantState(name, false, health, 10, 5, speed);

        private static ResolvedSkill Skill(int manaCost) =>
            new ResolvedSkill("test", "Test Skill", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, manaCost, 0, false, 100, 0, false,
                null, SpellPresentation.None, 0);

        private static PlayerKit Kit() =>
            new PlayerKit("hero", CharacterRole.Tank, new[] { Skill(20) }, null, null);

        private static FightSession Session(CombatantState hero, CombatantState foe) =>
            new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { Kit() }, null, new SeededRandom(3))
            { DamageVarianceRange = 0f };

        private static void Give(CombatantState combatant, params ModifierEffect[] effects)
        {
            combatant.ModifierEffects = new ModifierEffectSet(effects);
        }

        // ---- NextSkillManaDiscountPercent ---------------------------------------

        [Test]
        public void APlainSwing_ArmsTheDiscount_ForTheWearerOnly()
        {
            var hero = Hero();
            var foe = Foe();
            Give(hero, new ModifierEffect(ModifierEffectType.NextSkillManaDiscountPercent, 25));

            Assert.AreEqual(0, hero.PendingManaDiscountPercent, "fixture check: nothing armed before any swing");

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            Assert.AreEqual(25, hero.PendingManaDiscountPercent,
                "a landed plain swing must arm the wearer's own discount at the modifier's magnitude");
        }

        [Test]
        public void ADiscountedCast_ChargesLessMana_ThenClearsItself()
        {
            var hero = Hero(mana: 100);
            var foe = Foe();
            Give(hero, new ModifierEffect(ModifierEffectType.NextSkillManaDiscountPercent, 25));

            var session = Session(hero, foe);
            session.ExecuteAttack(foe); // arms the discount

            var skill = Skill(20);
            Assert.IsTrue(session.CastSkill(skill, foe), "fixture check: the cast must actually go off");

            // 20 * (100 - 25) / 100 = 15, away-from-zero rounding.
            Assert.AreEqual(100 - 15, hero.CurrentMana, "the discounted cast must charge 15, not the full 20");
            Assert.AreEqual(0, hero.PendingManaDiscountPercent, "the one-shot discount must clear itself on use");

            int manaBeforeSecondCast = hero.CurrentMana;
            Assert.IsTrue(session.CastSkill(skill, foe));
            Assert.AreEqual(manaBeforeSecondCast - 20, hero.CurrentMana,
                "a SECOND cast, with nothing newly armed, must charge the FULL cost");
        }

        [Test]
        public void ACastWithNoPriorSwing_ChargesTheFullCost()
        {
            var hero = Hero(mana: 100);
            var foe = Foe();
            Give(hero, new ModifierEffect(ModifierEffectType.NextSkillManaDiscountPercent, 25));

            var session = Session(hero, foe);
            var skill = Skill(20);

            Assert.IsTrue(session.CastSkill(skill, foe));

            Assert.AreEqual(80, hero.CurrentMana, "nothing armed the discount, so the cast pays full price");
        }

        // ---- ManaToWardOnTurnStartPercent ---------------------------------------
        //
        // Fires from GrantTurnStart for whoever holds the NEXT turn -- routed
        // through here via ExecuteAttack + a large hero/foe speed gap, the
        // same pattern ItemModifierCombatHookTests' push test relies on, so
        // the hero reliably remains Current (and so is the one GrantTurnStart
        // runs for) rather than an enemy's own turn resolving instead.
        [Test]
        public void UnspentMana_ConvertsToAWardAtTurnStart_AtTheWeakFixedRate()
        {
            var hero = Hero(mana: 100, speed: 50);
            hero.PrimaryPool.Current = 40;
            var foe = Foe(speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0));

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            Assert.IsTrue(session.IsPlayerTurn,
                "fixture check: the hero must remain Current so GrantTurnStart runs for the hero, not an enemy");

            // FightTuning.RunicWardConversionRate (0.25) x 40 unspent mana = 10
            // shield points.
            var ward = hero.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded);
            Assert.IsNotNull(ward, "unspent mana at turn start must convert to a Shielded ward");
            Assert.AreEqual(10, ward.Magnitude);
        }

        // THE TURN IT WENT UP ON DOES NOT COUNT, for a ward raised inside the
        // turn-open itself: OpenTurnFor's exemption set must be emptied
        // BEFORE this conversion puts its ward in it, or the Runic ward
        // ages at the end of the very turn it went up on. 99 is
        // FightTuning.MagicalShieldDurationTurns.
        [Test]
        public void TheConvertedWard_DoesNotAgeAtTheEndOfTheTurnItWentUpOn()
        {
            var hero = Hero(mana: 100, speed: 50);
            hero.PrimaryPool.Current = 40;
            var foe = Foe(speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0));

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);
            Assert.AreSame(hero, session.Current, "fixture: the hero holds the next turn too");

            var ward = hero.Statuses.First(s => s.Type == StatusEffectType.Shielded);
            Assert.AreEqual(99, ward.TurnsRemaining);

            session.ExecuteAttack(foe);
            Assert.AreSame(hero, session.Current, "fixture: and the one after");
            Assert.AreEqual(99, ward.TurnsRemaining, "the turn it went up on counted");

            session.ExecuteAttack(foe);
            Assert.AreEqual(98, ward.TurnsRemaining, "and the next one did not");
        }

        [Test]
        public void TheWardConversion_NeverFiresWithoutTheModifier()
        {
            var hero = Hero(mana: 100, speed: 50);
            hero.PrimaryPool.Current = 40;
            var foe = Foe(speed: 1);

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            Assert.IsNull(hero.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded),
                "no Runic modifier, no ward -- unspent mana just sits there, same as it always has");
        }

        // NO CEILING: wards stack, so a cap would be the only special case
        // in a model that deliberately has none.
        //
        // 0.25 x 1000 unspent mana = 250, a literal rather than a reference
        // to a constant, since none exists.
        [Test]
        public void TheWardConversion_ScalesWithTheWholePool_WithNoCeiling()
        {
            var hero = Hero(mana: 1000, speed: 50);
            hero.PrimaryPool.Current = 1000;
            var foe = Foe(speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0));

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            var ward = hero.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded);
            Assert.IsNotNull(ward);
            Assert.AreEqual(250, ward.Magnitude,
                "a deep mana pool converts all of it -- there is no cap left to clip it");
        }

        [Test]
        public void AWearerWhoseResourceIsNotManaConvertsNothing()
        {
            // THE READ SITE THAT STOPPED BEING TRUTHFUL. CombatantState's own
            // header says the CurrentMana/MaxMana getters "will stop being
            // truthful the moment a character's primary pool is not mana",
            // which is why they are getters -- but only the WRITE sites were
            // made to change, so this one kept reading "mana" off whatever the
            // holder carried. On Bjorn that is a rage bar, at its highest
            // exactly when he is winning, and the log line still said "his
            // runes catch the leftover mana as a ward".
            var hero = Hero(mana: 100, speed: 50);
            hero.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0,
                gainOnAttack: 0, gainOnDamageTaken: 0)
            {
                ShortTag = "FURY",
                RestoredByManaEffects = false,
                Current = 60,
            };
            var foe = Foe(speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0));

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            Assert.IsNull(hero.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded),
                "60 Fury is not 60 unspent mana, and must buy no ward at all");
        }
    }
}
