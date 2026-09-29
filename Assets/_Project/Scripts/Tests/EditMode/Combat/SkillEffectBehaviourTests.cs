using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // A SkillEffect member is only real once casting it in a live FightSession
    // moves something a player could point at, AND the four places that
    // enumerate the effect know about it.
    //
    // WHY A TABLE RATHER THAN ONE TEST PER EFFECT. SkillDispatchTests already
    // covers most members individually and is the better place to pin a
    // member's own rules -- what a Ward's spread costs, what a refusal says.
    // What it cannot do is fail when the FIFTEENTH member arrives, because a
    // hand-written list of tests has no way to know the enum grew. Every case
    // here is generated from Enum.GetValues, so a new member produces a new
    // failing test case named after itself the moment it is declared, and the
    // table row is what turns that name into a behavioural assertion.
    //
    // The four enumerating consumers are covered from the same source:
    // FightSession.ResolveCharacterSkillInner (the cast itself),
    // SkillEntryResolver's targeting default (what the author gets for free),
    // FightHudModel.VerbFor (the EFFECT row on the skill card) and
    // EnemyIntentIcons.KindFor/ScopeFor (the telegraph badge).
    //
    // WHICH OF THE FOUR ACTUALLY REFUSE A NEW MEMBER, because they are not
    // equal and pretending otherwise would overstate what this file buys.
    // Verified by adding a throwaway member and reading which cases went red:
    //
    //   * the table above, and FightSession's resolution switch -- BOTH, and
    //     both by name. A cast with no branch had already charged its mana,
    //     spent its cooldown and committed the turn by the time it fell out
    //     of the switch, so silence there is the expensive kind.
    //   * FightHudModel.VerbFor -- YES, by name. Its old ToString() fallback
    //     printed "GIFTMANA" at the player, so there was never a case where
    //     falling through was right.
    //   * the targeting default and the intent badge -- NO, deliberately.
    //     SingleEnemy, EnemyIntentKind.Skill and EnemyIntentScope.One are
    //     honest answers for an effect nothing special has been said about
    //     (each enum's own header says so), so these two assert the value is
    //     a DEFINED member -- they catch a broken case, not a missing one.
    //     Refusing the build on them would refuse a member whose telegraph
    //     legitimately has nothing to add.
    //
    // NOT a generic effect framework. The enums stay closed and every branch
    // stays hand-written; what is mechanised is only the question "did anyone
    // forget one".
    public class SkillEffectBehaviourTests
    {
        private static readonly SkillEffect[] AllEffects = (SkillEffect[])Enum.GetValues(typeof(SkillEffect));

        // ---- fixture ------------------------------------------------------

        private static CombatantState Hero(string name = "Hero", int health = 500, int mana = 50, int speed = 10) =>
            new CombatantState(name, true, health, mana, 40, speed);

        // Deliberately sluggish, for the same reason FightTalentTests' Foe is:
        // a cast is a full turn, and a foe fast enough to reply would act
        // between the setup and the assertion.
        private static CombatantState Foe(string name = "Foe", int health = 1000) =>
            new CombatantState(name, false, health, 10, 5, 1);

        private static PlayerKit Kit(params ResolvedSkill[] skills) =>
            new PlayerKit("hero", CharacterRole.Tank, skills, null, null);

        private static void Talents(CombatantState actor, params TalentEffect[] effects) =>
            actor.Talents = new TalentEffectSet(effects);

        private static ResolvedSkill Skill(
            SkillEffect effect,
            string displayName = "Coverage",
            int power = 0,
            int flatAmount = 0,
            StatusEffectType? appliesStatus = null,
            int statusMagnitude = 0,
            int statusDuration = 0,
            TransformGrant transform = null,
            string summonEnemyId = "",
            int summonCap = 0) =>
            new ResolvedSkill("coverage", displayName, "", "hero", 1, effect,
                SkillEntryResolver.DefaultTargetingFor(effect), 0, 0, false, power, flatAmount, false,
                null, SpellPresentation.None, 0,
                appliesStatus: appliesStatus, statusMagnitude: statusMagnitude,
                statusDuration: statusDuration, transform: transform,
                summonEnemyId: summonEnemyId, summonCap: summonCap);

        private static FightSession Session(
            CombatEncounter encounter, PlayerKit kit, FightSession.SummonFactory summonFactory = null) =>
            new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(3),
                summonFactory: summonFactory)
            {
                DamageVarianceRange = 0f,
            };

        // ---- the table ----------------------------------------------------

        // One row per SkillEffect: the synthetic skill to build, and the state
        // change to look for after ONE cast in a real session. The Observable
        // string is what a missing/failing row reports, so it is written as
        // the sentence a reader wants rather than a field name.
        private sealed class Row
        {
            public readonly SkillEffect Effect;
            public readonly string Observable;
            private readonly Action _cast;

            public Row(SkillEffect effect, string observable, Action cast)
            {
                Effect = effect;
                Observable = observable;
                _cast = cast;
            }

            public void Run() => _cast();
        }

        private static readonly Row[] Table =
        {
            new Row(SkillEffect.DamageSingle, "the target's health drops", () =>
            {
                var hero = Hero();
                var foe = Foe();
                var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
                var session = Session(encounter, Kit(Skill(SkillEffect.DamageSingle, power: 0, flatAmount: 30)));

                Assert.IsTrue(session.CastSkill(0, foe), "the cast was refused");
                Assert.Less(foe.CurrentHealth, foe.MaxHealth);
            }),

            new Row(SkillEffect.DamageAll, "every living enemy's health drops", () =>
            {
                var a = Foe("A");
                var b = Foe("B");
                var encounter = new CombatEncounter(new[] { Hero() }, new[] { a, b });
                var session = Session(encounter, Kit(Skill(SkillEffect.DamageAll, flatAmount: 30)));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.Less(a.CurrentHealth, a.MaxHealth);
                Assert.Less(b.CurrentHealth, b.MaxHealth);
            }),

            new Row(SkillEffect.HealSelf, "the caster's health rises from a lowered start", () =>
            {
                var hero = Hero();
                hero.CurrentHealth = 100;
                var encounter = new CombatEncounter(new[] { hero }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.HealSelf, flatAmount: 60)));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.Greater(hero.CurrentHealth, 100);
            }),

            // ONE ALLY, AND NOT THE CASTER -- the row asserts both halves,
            // because "the caster's own bar moved" is exactly what a
            // HealSingle that ignored its target would look like.
            new Row(SkillEffect.HealSingle, "the chosen ally's health rises and the caster's does not", () =>
            {
                var hero = Hero();
                var ally = Hero("Ally");
                var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.HealSingle, flatAmount: 60)));

                hero.CurrentHealth = 40;
                ally.CurrentHealth = 40;
                session.CastSkill(0, ally);

                Assert.Greater(ally.CurrentHealth, 40, "the chosen ally was not healed");
                Assert.AreEqual(40, hero.CurrentHealth, "the caster healed herself instead of the ally");
            }),

            new Row(SkillEffect.HealParty, "every living ally's health rises from a lowered start", () =>
            {
                var hero = Hero();
                var ally = Hero("Ally");
                hero.CurrentHealth = 100;
                ally.CurrentHealth = 100;
                var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.HealParty, flatAmount: 60)));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.Greater(hero.CurrentHealth, 100);
                Assert.Greater(ally.CurrentHealth, 100);
            }),

            new Row(SkillEffect.RestorePartyMana, "every ally's mana rises from a drained start", () =>
            {
                var hero = Hero();
                var ally = Hero("Ally");
                hero.PrimaryPool.Current = 0;
                ally.PrimaryPool.Current = 0;
                var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.RestorePartyMana, flatAmount: 20)));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.Greater(hero.CurrentMana, 0);
                Assert.Greater(ally.CurrentMana, 0);
            }),

            new Row(SkillEffect.Provoke, "the target carries Provoked, sourced to the caster", () =>
            {
                var hero = Hero();
                var foe = Foe();
                var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
                var session = Session(encounter, Kit(Skill(SkillEffect.Provoke, "Bellow")));

                Assert.IsTrue(session.CastSkill(0, foe), "the cast was refused");
                Assert.AreSame(hero, StatusEffects.ProvokedBy(foe));
            }),

            new Row(SkillEffect.Transform, "the caster is under the skill's transform", () =>
            {
                var hero = Hero();
                var grant = new TransformGrant
                {
                    displayName = "Black Ram Mode",
                    turns = 3,
                    attackPercent = 50,
                };
                var encounter = new CombatEncounter(new[] { hero }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.Transform, "Ram Up", transform: grant)));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.IsNotNull(hero.Transformation, "nothing was entered");
                Assert.AreEqual("Black Ram Mode", hero.Transformation.DisplayName);
            }),

            new Row(SkillEffect.Ward, "the caster carries a ward he cast on himself", () =>
            {
                var lamb = Hero("Lamb");
                Talents(lamb, new TalentEffect(TalentEffectType.WardReductionPercent, 50));
                var encounter = new CombatEncounter(new[] { lamb }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.Ward, "Fleece Ward", flatAmount: 40)));

                // AIMED, since AUDIT #147: a ward is SingleAlly and his own
                // plate is one of the squadmates it may be aimed at.
                Assert.IsTrue(session.CastSkill(0, lamb), "the cast was refused");
                Assert.IsTrue(StatusEffects.IsWarded(lamb));
                Assert.AreSame(lamb, StatusEffects.WardedBy(lamb));
            }),

            new Row(SkillEffect.Shatter, "the ward is gone and an enemy took the blast", () =>
            {
                // WardIsFreeAction so the ward and the detonation are the same
                // player turn -- otherwise the foe replies in between and
                // CastSkill would be resolving against the wrong Current.
                var lamb = Hero("Lamb");
                Talents(lamb,
                    new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                    new TalentEffect(TalentEffectType.WardIsFreeAction, 1),
                    new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100));

                var foe = Foe();
                var encounter = new CombatEncounter(new[] { lamb }, new[] { foe });
                var session = Session(encounter, Kit(
                    Skill(SkillEffect.Ward, "Fleece Ward", flatAmount: 40),
                    Skill(SkillEffect.Shatter, "Shatter")));

                Assert.IsTrue(session.CastSkill(0, lamb), "fixture: the ward did not go up");
                Assert.IsTrue(StatusEffects.IsWarded(lamb), "fixture: the ward did not go up");

                Assert.IsTrue(session.CastSkill(1, null), "the cast was refused");
                Assert.IsFalse(StatusEffects.IsWarded(lamb), "the ward survived its own detonation");
                Assert.Less(foe.CurrentHealth, foe.MaxHealth);
            }),

            new Row(SkillEffect.BuffParty, "the authored status is on every living ally", () =>
            {
                var hero = Hero();
                var ally = Hero("Ally");
                var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.BuffParty, "Wail",
                    appliesStatus: StatusEffectType.Protect, statusMagnitude: 20, statusDuration: 3)));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Protect));
                Assert.IsTrue(ally.Statuses.Any(s => s.Type == StatusEffectType.Protect));
            }),

            new Row(SkillEffect.GiftMana, "the ally's mana rises from a drained start", () =>
            {
                var lamb = Hero("Lamb");
                Talents(lamb, new TalentEffect(TalentEffectType.GiftManaPercent, 50));
                var ally = Hero("Ally");
                ally.PrimaryPool.Current = 0;
                var encounter = new CombatEncounter(new[] { lamb, ally }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.GiftMana, "Gift: Mana")));

                Assert.IsTrue(session.CastSkill(0, ally), "the cast was refused");
                Assert.Greater(ally.CurrentMana, 0);
            }),

            new Row(SkillEffect.GiftFury, "the ally carries Empowered", () =>
            {
                var lamb = Hero("Lamb");
                Talents(lamb, new TalentEffect(TalentEffectType.GiftAttackBonusPercent, 30));
                var ally = Hero("Ally");
                var encounter = new CombatEncounter(new[] { lamb, ally }, new[] { Foe() });
                var session = Session(encounter, Kit(Skill(SkillEffect.GiftFury, "Gift: Fury")));

                Assert.IsTrue(session.CastSkill(0, ally), "the cast was refused");
                Assert.IsTrue(ally.Statuses.Any(s => s.Type == StatusEffectType.Empowered));
            }),

            new Row(SkillEffect.GiftHaste, "the ally is pulled to the front of the queue", () =>
            {
                // THESE SPEEDS ARE THE FIXTURE, not decoration. The shove
                // lands the ally one charge point above the most-charged
                // rival rather than over the threshold outright (see
                // GiftHasteTests.BeingHurried_DoesNotAlsoBuyTheTurnAfterIt for
                // why), so it only decides the next turn when the race is
                // close. 30/8/12 is a board where the caster would come round
                // again first and the gift is what changes that; a slow ally
                // against a slow field is a board where a one-point lead is
                // eaten in a tick and the row would pass for both answers.
                var lamb = Hero("Lamb", speed: 30);
                Talents(lamb, new TalentEffect(TalentEffectType.GiftAppliesImmediateTurn, 1));
                var ally = Hero("Ally", speed: 8);
                var foe = new CombatantState("Foe", false, 1000, 10, 5, 12);
                var encounter = new CombatEncounter(new[] { lamb, ally }, new[] { foe });
                var session = Session(encounter, Kit(Skill(SkillEffect.GiftHaste, "Gift: Haste")));

                Assert.IsTrue(session.CastSkill(0, ally), "the cast was refused");
                Assert.AreSame(ally, encounter.Current,
                    "the ally was not moved to the front -- the turn went to " + encounter.Current.Name);
            }),

            new Row(SkillEffect.Summon, "the field holds one more combatant than it did", () =>
            {
                var hero = Hero();
                var encounter = new CombatEncounter(new[] { hero }, new[] { Foe() });
                int before = encounter.Enemies.Count;

                FightSession.SummonFactory factory = (string id, out CombatantState state, out EnemyKit kit) =>
                {
                    state = new CombatantState("Called " + id, false, 40, 0, 5, 4);
                    kit = new EnemyKit(null, false);
                    return true;
                };

                var session = Session(encounter, Kit(Skill(SkillEffect.Summon, "Roar",
                    summonEnemyId: "rat", summonCap: 3)), factory);

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.AreEqual(before + 1, encounter.Enemies.Count);
            }),

            // Built by hand rather than through this file's Skill() helper --
            // Reclaim has no power/flatAmount of its own, and its authoring
            // surface (requiresStatus/detonationPercent/detonationSplit) is
            // the same one SkillEntryResolver refuses without.
            new Row(SkillEffect.Reclaim, "the poisoned target's health drops and its Poison is gone", () =>
            {
                var hero = Hero();
                var foe = Foe();
                foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 10, 2));
                var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
                var skill = new ResolvedSkill("coverage", "Coverage", "", "hero", 1, SkillEffect.Reclaim,
                    SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0,
                    requiresStatus: StatusEffectType.Poison, detonationPercent: 150,
                    detonationSplit: new[] { DamageType.Poison, DamageType.Fire });
                var session = Session(encounter, Kit(skill));

                Assert.IsTrue(session.CastSkill(0, foe), "the cast was refused");
                Assert.Less(foe.CurrentHealth, foe.MaxHealth);
                Assert.IsFalse(foe.Statuses.Any(s => s.Type == StatusEffectType.Poison));
            }),

            new Row(SkillEffect.Hasten, "the chosen ally's forecast position moves earlier", () =>
            {
                // THE SPEEDS ARE THE FIXTURE, the same way Gift: Haste's are.
                // An advance buys places, so it can only be SEEN on a board
                // where the target has places to lose: 30/20/4 puts the
                // caster first, a mid ally next and the target well behind
                // both, which is a board where moving is visible and where
                // the refusal arm (already at position 1) is not the one
                // being exercised.
                var caster = Hero("Caster", speed: 30);
                var mid = Hero("Mid", speed: 20);
                var back = Hero("Back", speed: 4);
                var encounter = new CombatEncounter(new[] { caster, mid, back }, new[] { Foe() });
                var skill = new ResolvedSkill("coverage", "Borrowed Moment", "", "hero", 1,
                    SkillEffect.Hasten, SkillTargeting.SingleAlly, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0, advanceSlots: 2);
                var session = Session(encounter, Kit(skill));

                int before = encounter.ForecastPositionOf(back);
                Assert.Greater(before, 1, "fixture: the target has to start with places to gain");

                Assert.IsTrue(session.CastSkill(0, back), "the cast was refused");
                Assert.Less(encounter.ForecastPositionOf(back), before,
                    "the ally did not move earlier in the order");
            }),

            new Row(SkillEffect.SwapAllies, "the two allies hold each other's field slots", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var ally = Hero("Ally", speed: 8);
                var encounter = new CombatEncounter(new[] { caster, ally }, new[] { Foe() });
                var skill = new ResolvedSkill("coverage", "Palace Passage", "", "hero", 1,
                    SkillEffect.SwapAllies, SkillTargeting.SingleAlly, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0);
                var session = Session(encounter, Kit(skill));

                Assert.AreSame(caster, encounter.PlayerParty[0], "fixture: the caster opens at the front");

                // THE TWO-PICK DOOR (plan 1.12). A swap has two ends, so the
                // one-target CastSkill cannot express it -- this row is also
                // the pin that the second door exists and reaches the same
                // dispatcher.
                Assert.IsTrue(session.CastSkillOnPicks(0, new[] { caster, ally }), "the cast was refused");

                Assert.AreSame(ally, encounter.PlayerParty[0], "the two did not trade slots");
                Assert.AreSame(caster, encounter.PlayerParty[1]);
            }),

            new Row(SkillEffect.Afflict, "the target carries the status the row named", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var foe = Foe();
                var encounter = new CombatEncounter(new[] { caster }, new[] { foe });
                var skill = new ResolvedSkill("coverage", "Velvet Shackles", "", "hero", 1,
                    SkillEffect.Afflict, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0,
                    appliesStatus: StatusEffectType.Rooted, statusMagnitude: 0, statusDuration: 2);
                var session = Session(encounter, Kit(skill));

                int before = foe.CurrentHealth;
                Assert.IsTrue(session.CastSkill(0, foe), "the cast was refused");

                Assert.IsTrue(foe.Statuses.Any(s => s.Type == StatusEffectType.Rooted),
                    "the authored status did not land");
                Assert.AreEqual(before, foe.CurrentHealth,
                    "an Afflict is the status and nothing else -- it must deal no damage");
            }),

            new Row(SkillEffect.Enthrall, "the ordinary enemy carries Fear and the caster is exposed", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var foe = Foe();
                var encounter = new CombatEncounter(new[] { caster }, new[] { foe });
                var skill = new ResolvedSkill("coverage", "Court of Whispers", "", "hero", 1,
                    SkillEffect.Enthrall, SkillTargeting.AllEnemies, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0,
                    appliesStatus: StatusEffectType.Vulnerable, statusMagnitude: 20, statusDuration: 1,
                    queuePushSlots: 2);
                var session = Session(encounter, Kit(skill));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");
                Assert.IsTrue(foe.Statuses.Any(s => s.Type == StatusEffectType.Feared));
                Assert.IsTrue(caster.Statuses.Any(s => s.Type == StatusEffectType.Vulnerable));
            }),

            new Row(SkillEffect.Reposition, "the chosen ally stands in the authored seat", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var ally = Hero("Ally", speed: 8);
                var encounter = new CombatEncounter(new[] { caster, ally }, new[] { Foe() });
                var skill = new ResolvedSkill("coverage", "Call to the Front", "", "hero", 1,
                    SkillEffect.Reposition, SkillTargeting.SingleAlly, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0, toSeat: 1);
                var session = Session(encounter, Kit(skill));

                Assert.AreEqual(1, encounter.SeatOf(ally), "fixture: the ally opens in the middle");
                Assert.IsTrue(session.CastSkill(0, ally), "the cast was refused");

                Assert.AreEqual(0, encounter.SeatOf(ally), "the ally was not put in the front seat");
                Assert.AreEqual(1, encounter.SeatOf(caster), "and the occupant took the ally's seat");
            }),

            new Row(SkillEffect.Unbroken, "the caster regenerates and no crowd control can land", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var encounter = new CombatEncounter(new[] { caster }, new[] { Foe() });
                var skill = new ResolvedSkill("coverage", "Unbroken", "", "hero", 1,
                    SkillEffect.Unbroken, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0, windowTurns: 2, regenPercentOfMaxHealth: 15);
                var session = Session(encounter, Kit(skill));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");

                Assert.IsTrue(caster.Statuses.Any(s => s.Type == StatusEffectType.Regen),
                    "the regen did not open");
                Assert.IsTrue(caster.CrowdControl.Unstoppable.IsOpen, "Unstoppable did not open");
            }),

            new Row(SkillEffect.CursedBlood, "the caster's heals are converted for the window", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var encounter = new CombatEncounter(new[] { caster }, new[] { Foe() });
                var skill = new ResolvedSkill("coverage", "Cursed Blood", "", "hero", 1,
                    SkillEffect.CursedBlood, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0, windowTurns: 2, oncePerFight: true);
                var session = Session(encounter, Kit(skill));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");

                Assert.IsTrue(caster.HealConversion.IsActive, "the conversion window did not open");
            }),

            new Row(SkillEffect.PlantShield, "the caster's planted shield goes down", () =>
            {
                var caster = Hero("Caster", speed: 30);
                var encounter = new CombatEncounter(new[] { caster }, new[] { Foe() });
                var skill = new ResolvedSkill("coverage", "Plant the Shield", "", "hero", 1,
                    SkillEffect.PlantShield, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                    null, SpellPresentation.None, 0);
                var session = Session(encounter, Kit(skill));

                Assert.IsTrue(session.CastSkill(0, null), "the cast was refused");

                Assert.IsTrue(caster.PlantedShield.IsPlaced, "the shield did not go down");
            }),
        };

        // ---- the four consumers, one case per member -----------------------

        // THE COMPLETENESS GUARANTEE. Generated from Enum.GetValues, so a
        // member added with no row fails here by its own name.
        [TestCaseSource(nameof(AllEffects))]
        public void EveryEffectMovesTheStateItsRowNames(SkillEffect effect)
        {
            var row = Table.FirstOrDefault(r => r.Effect == effect);
            Assert.IsNotNull(row,
                $"SkillEffect.{effect} has no row in SkillEffectBehaviourTests' table. Add one naming " +
                "the synthetic skill to cast and the state change a player could point at afterwards -- " +
                "a member nothing observably does is a member nobody can tell is broken.");

            row.Run();
        }

        // Through the real resolver rather than the private DefaultTargetingFor
        // it calls: what matters is that an author who states only the effect
        // gets a usable targeting, and that is the path they actually take.
        [TestCaseSource(nameof(AllEffects))]
        public void EveryEffectResolvesToADefinedTargetingWithNoneAuthored(SkillEffect effect)
        {
            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { RawFor(effect) }, out var resolved, out var errors);

            Assert.IsTrue(ok, $"SkillEffect.{effect} could not be authored at all: " +
                              string.Join("; ", errors ?? new List<string>()));
            Assert.IsTrue(Enum.IsDefined(typeof(SkillTargeting), resolved[0].Targeting),
                $"SkillEffect.{effect} defaulted to targeting {(int)resolved[0].Targeting}, which is not a " +
                "SkillTargeting member -- SkillEntryResolver.DefaultTargetingFor has no case for it.");
        }

        [TestCaseSource(nameof(AllEffects))]
        public void EveryEffectHasAHudVerb(SkillEffect effect)
        {
            string verb = FightHudModel.VerbFor(effect);

            Assert.IsFalse(string.IsNullOrWhiteSpace(verb),
                $"SkillEffect.{effect} has no verb for the skill card's EFFECT row.");
        }

        [TestCaseSource(nameof(AllEffects))]
        public void EveryEffectTelegraphsADefinedKindAndScope(SkillEffect effect)
        {
            var kind = EnemyIntentIcons.KindFor(effect, null, false);
            var scope = EnemyIntentIcons.ScopeFor(effect);

            Assert.IsTrue(Enum.IsDefined(typeof(EnemyIntentKind), kind),
                $"SkillEffect.{effect} telegraphs kind {(int)kind}, which is not an EnemyIntentKind member.");
            Assert.IsTrue(Enum.IsDefined(typeof(EnemyIntentScope), scope),
                $"SkillEffect.{effect} telegraphs scope {(int)scope}, which is not an EnemyIntentScope member.");
        }

        // ---- the loud branches --------------------------------------------

        // An undefined member stands in for the NEXT member added without a
        // case: both land on the same `default:` arm. Before this, a cast with
        // no branch paid its mana, spent its cooldown, committed a beat and
        // did nothing -- the exact silent shape the switch was missing a
        // default for.
        [Test]
        public void AnEffectWithNoResolutionBranchThrowsRatherThanCastingSilently()
        {
            var hero = Hero();
            var foe = Foe();
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var session = Session(encounter, Kit(Skill(Unmapped, "Nothing At All")));

            var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => session.CastSkill(0, foe));
            StringAssert.Contains("effect", thrown.Message);
        }

        [Test]
        public void AnEffectWithNoHudVerbThrowsRatherThanPrintingItsOwnName()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FightHudModel.VerbFor(Unmapped));
        }

        // Not a member of SkillEffect and never will be: 9999 is past anything
        // the enum can reach, so this cannot collide with a real member added
        // later.
        private const SkillEffect Unmapped = (SkillEffect)9999;

        // ---- authoring fixture --------------------------------------------

        // The minimum skills.json row each effect will accept, with `targeting`
        // deliberately left blank so the default is what gets exercised.
        // Everything set here is set because SkillEntryResolver refuses the
        // entry without it -- see its cross-field rules.
        private static RawSkillEntry RawFor(SkillEffect effect)
        {
            var raw = new RawSkillEntry
            {
                id = "coverage_" + effect.ToString().ToLowerInvariant(),
                displayName = "Coverage " + effect,
                characterId = "hero",
                effect = effect.ToString(),
                manaCost = 5,
            };

            if (effect == SkillEffect.HealSelf || effect == SkillEffect.HealParty
                || effect == SkillEffect.HealSingle
                || effect == SkillEffect.RestorePartyMana
                // A Ward is refused with no size for the same reason a heal is
                // refused with no amount: since the shield model the talent is
                // a multiplier over the row's own pool, and a multiplier on
                // nothing is nothing.
                || effect == SkillEffect.Ward)
            {
                raw.flatAmount = 10;
            }

            if (effect == SkillEffect.Unbroken || effect == SkillEffect.CursedBlood)
            {
                raw.windowTurns = 2;
                if (effect == SkillEffect.Unbroken) raw.regenPercentOfMaxHealth = 15;
            }

            if (effect == SkillEffect.BuffParty)
            {
                raw.appliesStatus = "Protect";
                raw.statusMagnitude = 20;
                raw.statusDuration = 3;
            }

            if (effect == SkillEffect.Summon)
            {
                raw.summonEnemyId = "rat";
                raw.summonCap = 2;
            }

            if (effect == SkillEffect.Transform)
            {
                raw.transform = new TransformGrant
                {
                    displayName = "Mode",
                    turns = 3,
                    attackPercent = 50,
                };
            }

            // A Reclaim skill's damage IS the consumed total marked up --
            // requiresStatus/detonationPercent/detonationSplit are the whole
            // authoring surface, the same way BuffParty's status trio above
            // is the whole of what makes THAT effect nonempty.
            if (effect == SkillEffect.Reclaim)
            {
                raw.requiresStatus = "Poison";
                raw.detonationPercent = 150;
                raw.detonationSplit = new[] { "Poison", "Fire" };
            }

            // A Hasten skill's whole payload is how many places it buys, and
            // zero places is a cast that does nothing -- so the resolver
            // refuses a row without it, the same way it refuses a heal with
            // no amount. SwapAllies needs nothing: the two ends come from the
            // picker, never from the row (plan 2.9).
            if (effect == SkillEffect.Hasten)
            {
                raw.advanceSlots = 2;
            }

            // A Reposition IS its seat, so the row must name one. And its
            // default SingleEnemy is only a party member when a MONSTER casts
            // it (the resolver refuses a player row aimed at an enemy), which
            // is Dark Chains' own shape.
            if (effect == SkillEffect.Reposition)
            {
                raw.toSeat = 1;
                raw.playerSelectable = false;
            }

            // An Afflict IS its status, exactly as BuffParty above is, so the
            // resolver refuses a row without one. NO statusMagnitude: Rooted
            // is a gate rather than a quantity and the resolver refuses a
            // magnitude on one (StatusEffects.CarriesMagnitude), which is the
            // same rule from the other side.
            if (effect == SkillEffect.Afflict)
            {
                raw.appliesStatus = "Rooted";
                raw.statusDuration = 2;
            }

            if (effect == SkillEffect.Enthrall)
            {
                raw.appliesStatus = "Vulnerable";
                raw.statusMagnitude = 20;
                raw.statusDuration = 1;
                raw.queuePushSlots = 2;
            }

            return raw;
        }

        // ---- diagnostic ----------------------------------------------------
        //
        // DIAGNOSTIC ONLY, and weaker than everything above on purpose. A
        // RelicEffect or TalentEffectType member has no single observable a
        // table could assert -- a relic's effect shows up in a damage number,
        // a status, a shop roll or a turn order depending on which one it is,
        // and several are conditional on board state a fixture would have to
        // fake. What this DOES catch is the cheapest version of the same
        // mistake: a member declared and then never referenced by anything,
        // which is the state a half-finished relic is left in.
        //
        // A NAME MATCH IS NOT A BEHAVIOUR CHECK. This passes for a member
        // mentioned once in a comment. Read a failure as "nothing anywhere
        // knows this exists", never a pass as "this works".

        private const int MinimumFilesSwept = 200;

        [Test]
        public void DiagnosticEveryRelicEffectIsNamedOutsideItsOwnEnum()
        {
            AssertEveryMemberIsNamed(typeof(RelicEffect), "RelicEffect.cs",
                exempt: RelicEffect.None.ToString());
        }

        [Test]
        public void DiagnosticEveryTalentEffectTypeIsNamedOutsideItsOwnEnum()
        {
            AssertEveryMemberIsNamed(typeof(TalentEffectType), "TalentEffect.cs", exempt: null);
        }

        private static void AssertEveryMemberIsNamed(Type enumType, string declaringFileName, string exempt)
        {
            string scriptsRoot = Path.Combine(RepoTree.Root(), "Assets", "_Project", "Scripts");
            var files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(p => !string.Equals(Path.GetFileName(p), declaringFileName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Vacuity guard: a path change that swept nothing would report
            // every member as covered.
            Assert.Greater(files.Count, MinimumFilesSwept,
                $"only {files.Count} files were swept -- the scripts root moved and this check is a no-op");

            string corpus = string.Join("\n", files.Select(File.ReadAllText));

            var unnamed = Enum.GetNames(enumType)
                .Where(name => name != exempt)
                .Where(name => !corpus.Contains(enumType.Name + "." + name))
                .ToList();

            Assert.IsEmpty(unnamed,
                $"{enumType.Name} members nothing outside {declaringFileName} names: {string.Join(", ", unnamed)}. " +
                "A member no consumer mentions is a rule the game does not have.");
        }
    }
}
