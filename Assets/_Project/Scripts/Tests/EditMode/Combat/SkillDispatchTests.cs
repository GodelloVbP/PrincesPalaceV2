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
    // The fourteen authored skill effects, resolved from data.
    //
    // In v1 reaching any of these meant loading the Gameplay scene, opening the
    // skill submenu and clicking the right button, so most of the switch was
    // covered by nothing at all. Everything here is a plain function of the
    // resolved skill and the board.
    public class SkillDispatchTests
    {
        private static CombatantState Hero(string name = "Hero", int health = 500, int mana = 50, int speed = 10) =>
            new CombatantState(name, true, health, mana, 40, speed);

        private static CombatantState Foe(string name = "Foe", int health = 1000) =>
            new CombatantState(name, false, health, 10, 5, 1);

        private static ResolvedSkill Skill(
            SkillEffect effect,
            string displayName = "Test Skill",
            int manaCost = 0,
            int resourceCost = 0,
            int power = 100,
            int flatAmount = 0,
            StatusEffectType? appliesStatus = null,
            int statusMagnitude = 0,
            int statusDuration = 0,
            int queuePushSlots = 0,
            DamageInstance[] damageInstances = null,
            TransformGrant transform = null) =>
            new ResolvedSkill("test", displayName, "", "hero", 1, effect,
                SkillEntryResolver.DefaultTargetingFor(effect),
                manaCost, resourceCost, false, power, flatAmount, false,
                damageInstances, SpellPresentation.None, 0,
                appliesStatus: appliesStatus, statusMagnitude: statusMagnitude,
                statusDuration: statusDuration, queuePushSlots: queuePushSlots, transform: transform);

        private static PlayerKit Kit(CharacterRole role = CharacterRole.Tank,
            IReadOnlyList<ResolvedSkill> skills = null,
            IReadOnlyList<ResolvedRelic> relics = null) =>
            new PlayerKit("hero", role, skills, relics, null);

        // A skill the actor cannot meet. STR 99 is past anything a fixture hero
        // rolls, so "unmet" here is a fact rather than a tuning coincidence.
        private static ResolvedSkill OutOfReachSkill(string displayName) =>
            new ResolvedSkill("outofreach", displayName, "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 100, 0, false,
                null, SpellPresentation.None, 0,
                requirements: new AbilityScoreBlock(99, 0, 0, 0, 0, 0));

        private static (FightSession session, CombatantState hero, CombatEncounter encounter) Fight(
            PlayerKit kit, CombatantState hero = null, params CombatantState[] foes)
        {
            hero = hero ?? Hero();
            if (foes.Length == 0) foes = new[] { Foe() };

            var encounter = new CombatEncounter(new[] { hero }, foes);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
            };
            return (session, hero, encounter);
        }

        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages);

        // ---- damage ------------------------------------------------------------

        // ---- what the menu offers -------------------------------------------

        [Test]
        public void ASkillWhoseRequirementsAreUnmet_IsNotOffered()
        {
            // It used to be listed with a LOCKED prefix and greyed out, so every
            // turn the player read past rows they could not pick.
            var reachable = Skill(SkillEffect.DamageSingle, displayName: "Reachable");
            var (session, hero, _) = Fight(Kit(skills: new[] { OutOfReachSkill("Out Of Reach"), reachable }));

            var options = session.SkillOptionsFor(hero);

            CollectionAssert.AreEqual(new[] { "Reachable" },
                options.Select(o => o.Skill.DisplayName).ToArray(),
                "an unusable skill is still being offered");
        }

        [Test]
        public void AFilteredOutSkill_DoesNotShiftTheOnesBehindIt()
        {
            // THE TRAP THIS FILTER SETS. The menu dispatches on the row that was
            // pressed, and while nothing was hidden the row and the skill's index
            // in the kit were the same number. Hide the first skill and they stop
            // agreeing -- so pressing the only visible row would have cast
            // whatever sits at kit index 0, which is the skill just hidden for
            // being unusable.
            //
            // The option carries the index it came from, and that is what the
            // dispatcher sends. This pins it.
            var reachable = Skill(SkillEffect.DamageSingle, displayName: "Reachable");
            var (session, hero, _) = Fight(Kit(skills: new[] { OutOfReachSkill("Out Of Reach"), reachable }));

            var options = session.SkillOptionsFor(hero);

            Assert.AreEqual(1, options.Count, "fixture check: exactly one skill should survive the filter");
            Assert.AreEqual(1, options[0].Index,
                "the surviving option forgot where it came from, so the menu would cast the hidden skill");
        }

        [Test]
        public void DamageSingleHitsTheTargetAndSaysWhatItWas()
        {
            var skill = Skill(SkillEffect.DamageSingle, "Firebolt");
            var (session, _, encounter) = Fight(Kit(skills: new[] { skill }));
            var foe = encounter.Enemies[0];
            int before = foe.CurrentHealth;

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.Less(foe.CurrentHealth, before);
            Assert.IsTrue(Messages(session).Any(m => m.Contains("Firebolt") && m.Contains("damage")));
        }

        [Test]
        public void AnAuthoredPacketSpellReportsItsSplit()
        {
            // A spell with authored packets deals exactly what it says, per
            // element, rather than scaling off the caster's attack.
            var skill = Skill(SkillEffect.DamageSingle, "Prismatic Bolt", damageInstances: new[]
            {
                new DamageInstance(DamageType.Fire, 30),
                new DamageInstance(DamageType.Ice, 20),
            });
            var (session, _, encounter) = Fight(Kit(skills: new[] { skill }));

            session.CastSkill(0, encounter.Enemies[0]);
            var line = Messages(session).First(m => m.Contains("Prismatic Bolt"));

            Assert.IsTrue(line.Contains("Fire"), line);
            Assert.IsTrue(line.Contains("Ice"), line);
        }

        [Test]
        public void DamageAllHitsEveryLivingEnemy()
        {
            var skill = Skill(SkillEffect.DamageAll, "Firestorm");
            var a = Foe("A");
            var b = Foe("B");
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), null, a, b);

            session.CastSkill(0, null);

            Assert.Less(a.CurrentHealth, a.MaxHealth);
            Assert.Less(b.CurrentHealth, b.MaxHealth);
        }

        [Test]
        public void AnAreaEffectReportsItsLargestSingleHitNotTheTotal()
        {
            // One beat shows one number. A total would match no one enemy's HP
            // drop, so the floating figure would be a lie about every target.
            var skill = Skill(SkillEffect.DamageAll, "Firestorm");
            var soft = Foe("Soft");
            var armoured = Foe("Armoured");
            armoured.PhysicalDefense = 20;
            armoured.MagicalDefense = 20;
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), null, soft, armoured);

            session.CastSkill(0, null);
            var beat = session.DrainBeats()[0];

            int softDrop = soft.MaxHealth - soft.CurrentHealth;
            int armouredDrop = armoured.MaxHealth - armoured.CurrentHealth;

            Assert.AreEqual(System.Math.Max(softDrop, armouredDrop), beat.Amount);
            Assert.AreNotEqual(softDrop + armouredDrop, beat.Amount, "not a total");
        }

        [Test]
        public void ADeadTargetIsNotAfflictedByTheSkillsStatus()
        {
            var skill = Skill(SkillEffect.DamageSingle, "Venom Bolt",
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 5, statusDuration: 3);
            var frail = Foe("Frail", health: 1);
            var tank = Foe("Tank");
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), null, frail, tank);

            session.CastSkill(0, frail);

            Assert.IsFalse(frail.IsAlive);
            Assert.IsEmpty(frail.Statuses);
        }

        [Test]
        public void ABeneficialStatusIsWordedAsAGainNotAnAffliction()
        {
            var skill = Skill(SkillEffect.HealSelf, "Mend",
                flatAmount: 10, appliesStatus: StatusEffectType.Regen,
                statusMagnitude: 5, statusDuration: 3);
            var hero = Hero();
            hero.CurrentHealth = 100;
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), hero);

            session.CastSkill(0, null);

            Assert.IsTrue(Messages(session).Any(m => m.Contains("gains Regen")));
        }

        // ---- support effects ----------------------------------------------------

        [Test]
        public void HealSelfRestoresTheCasterAndRecordsAHealingBeat()
        {
            var skill = Skill(SkillEffect.HealSelf, "Mend", flatAmount: 60);
            var hero = Hero();
            hero.CurrentHealth = 100;
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), hero);

            session.CastSkill(0, null);
            var beat = session.DrainBeats()[0];

            Assert.Greater(hero.CurrentHealth, 100);
            Assert.IsTrue(beat.IsHealing, "a heal must not render as a damage number");
            Assert.Greater(beat.Amount, 0);
        }

        [Test]
        public void HealPartyMendsEveryLivingAlly_AndSkipsTheFallen()
        {
            var skill = Skill(SkillEffect.HealParty, "Rally", flatAmount: 60);
            var hero = Hero();
            var ally = Hero("Ally");
            var fallen = Hero("Fallen");
            hero.CurrentHealth = 100;
            ally.CurrentHealth = 100;
            fallen.CurrentHealth = 0;

            var encounter = new CombatEncounter(new[] { hero, ally, fallen }, new[] { Foe() });
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(skills: new[] { skill }) }, null, new SeededRandom(3))
            { DamageVarianceRange = 0f };

            session.CastSkill(0, null);

            Assert.Greater(hero.CurrentHealth, 100);
            Assert.Greater(ally.CurrentHealth, 100);
            Assert.AreEqual(0, fallen.CurrentHealth, "a heal does not raise the dead");
        }

        [Test]
        public void RestorePartyManaRefillsTheSquad()
        {
            var skill = Skill(SkillEffect.RestorePartyMana, "Focus", flatAmount: 20);
            var hero = Hero();
            hero.PrimaryPool.Current = 0;
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), hero);

            session.CastSkill(0, null);

            Assert.Greater(hero.CurrentMana, 0);
        }

        [Test]
        public void BuffPartyLandsItsStatusOnEveryone()
        {
            var skill = Skill(SkillEffect.BuffParty, "War Cry",
                appliesStatus: StatusEffectType.Protect, statusMagnitude: 20, statusDuration: 3);
            var hero = Hero();
            var ally = Hero("Ally");
            var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(skills: new[] { skill }) }, null, new SeededRandom(3));

            session.CastSkill(0, null);

            Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Protect));
            Assert.IsTrue(ally.Statuses.Any(s => s.Type == StatusEffectType.Protect));
        }

        // ---- costs and refusals --------------------------------------------------

        [Test]
        public void ACastThatCannotBePaidForIsRefusedAndCostsNoTurn()
        {
            var skill = Skill(SkillEffect.DamageSingle, "Firebolt", manaCost: 999);
            var (session, hero, encounter) = Fight(Kit(skills: new[] { skill }));
            int mana = hero.CurrentMana;

            Assert.IsFalse(session.CastSkill(0, encounter.Enemies[0]));

            Assert.AreEqual(mana, hero.CurrentMana, "nothing paid");
            Assert.AreSame(hero, encounter.Current, "and the turn is still theirs");
        }

        [Test]
        public void ShatterWithNoWardsOutRefusesBeforeAnythingIsPaid()
        {
            // Conditional on board state the player can misread, so eating the
            // resource AND the turn for a cast that visibly did nothing is the
            // worst possible answer.
            var skill = Skill(SkillEffect.Shatter, "Shatter", manaCost: 5);
            var (session, hero, encounter) = Fight(Kit(skills: new[] { skill }));
            int mana = hero.CurrentMana;

            Assert.IsFalse(session.CastSkill(0, null));

            Assert.AreEqual(mana, hero.CurrentMana);
            Assert.AreSame(hero, encounter.Current);
            Assert.IsTrue(session.DrainImmediateMessages().Any(m => m.Contains("no wards out")));
        }

        [Test]
        public void AGiftWithNobodyToGiveItToRefuses()
        {
            var skill = Skill(SkillEffect.GiftMana, "Gift: Mana", manaCost: 5);
            var (session, hero, _) = Fight(Kit(skills: new[] { skill }));
            int mana = hero.CurrentMana;

            Assert.IsFalse(session.CastSkill(0, null));

            Assert.AreEqual(mana, hero.CurrentMana);
            Assert.IsTrue(session.DrainImmediateMessages().Any(m => m.Contains("nobody to give it to")));
        }

        [Test]
        public void ManaIsSpentOnACastThatGoesThrough()
        {
            var skill = Skill(SkillEffect.DamageSingle, "Firebolt", manaCost: 12);
            var (session, hero, encounter) = Fight(Kit(skills: new[] { skill }));

            session.CastSkill(0, encounter.Enemies[0]);

            Assert.AreEqual(hero.MaxMana - 12, hero.CurrentMana);
        }

        [Test]
        public void AnIndexOffTheEndOfTheStripDoesNothing()
        {
            // Graceful degradation rather than an exception: the strip is built
            // from content and a mismatch must not take the fight down.
            var (session, hero, encounter) = Fight(Kit(skills: new ResolvedSkill[0]));

            Assert.IsFalse(session.CastSkill(3, encounter.Enemies[0]));
            Assert.AreSame(hero, encounter.Current);
        }

        // ---- the queue push -------------------------------------------------------

        [Test]
        public void HeadbuttKnocksTheTargetDownTheOrder()
        {
            var skill = Skill(SkillEffect.DamageSingle, "Headbutt", queuePushSlots: 1);
            var (session, _, encounter) = Fight(Kit(skills: new[] { skill }));

            session.CastSkill(0, encounter.Enemies[0]);

            Assert.IsTrue(Messages(session).Any(m => m.Contains("knocked back down the order")));
        }

        [Test]
        public void ChargeT2TakesTheTelegraphedActionAway()
        {
            // The shove does not merely delay what was coming, it removes the
            // commitment -- so the monster genuinely loses it rather than
            // performing it one turn later.
            var skill = Skill(SkillEffect.DamageSingle, "Headbutt", queuePushSlots: 1);
            var hero = Hero();
            hero.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.HeadbuttCancelsIntent, 1),
            });

            var foe = Foe();
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(new ResolvedEnemy("golem", "Golem", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0,
                skillName: "Boulder Slam", skillPower: 2f, skillChance: 1f), false);
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(skills: new[] { skill }) },
                new List<EnemyKit> { enemyKit }, new SeededRandom(3)) { DamageVarianceRange = 0f };

            session.PrepareEnemyIntents();
            Assert.AreEqual("Boulder Slam", session.IntentFor(foe), "fixture: something was telegraphed");

            session.CastSkill(0, foe);

            Assert.IsTrue(Messages(session).Any(m => m.Contains("loses hold of Boulder Slam")));
        }

        [Test]
        public void CancellingClaimsNoCreditForInterruptingAPlainAttack()
        {
            // A plain attack is never telegraphed, so announcing that one was
            // cancelled would be claiming credit for interrupting nothing.
            var skill = Skill(SkillEffect.DamageSingle, "Headbutt", queuePushSlots: 1);
            var hero = Hero();
            hero.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.HeadbuttCancelsIntent, 1),
            });

            var foe = Foe();
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(skills: new[] { skill }) }, null, new SeededRandom(3))
            { DamageVarianceRange = 0f };

            session.PrepareEnemyIntents();
            session.CastSkill(0, foe);

            Assert.IsFalse(Messages(session).Any(m => m.Contains("loses hold of")));
        }

        // Role riders (Tank lifesteal, CrowdControl defense-shred, Support
        // party-heal, Utility signature-gain, Assassin execute messaging)
        // were removed with BasicSpell (docs/PLAN_SHOP.md Gate 4) -- their
        // only entry point was the deleted free "Skill" action, so the tests
        // that exercised them through ExecuteSkill went with it. See
        // FightSession.Skills.cs's "riders on a resolved skill" comment.

        // ---- relics ---------------------------------------------------------------

        [Test]
        public void MagicalShieldRisesOnACastAndNotOnASwing()
        {
            var relic = new ResolvedRelic("shield", "Magical Shield", "", RelicEffect.MagicalShield, 0);

            var swinger = Hero();
            var (swing, _, swingFoes) = Fight(Kit(relics: new[] { relic }), swinger);
            swing.ExecuteAttack(swingFoes.Enemies[0]);
            Assert.IsFalse(swinger.Statuses.Any(s => s.Type == StatusEffectType.Shielded),
                "a plain attack raises nothing");

            var caster = Hero();
            var (cast, _, castFoes) = Fight(Kit(relics: new[] { relic }, skills: new[] { Skill(SkillEffect.DamageSingle) }), caster);
            cast.CastSkill(0, castFoes.Enemies[0]);

            Assert.IsTrue(caster.Statuses.Any(s => s.Type == StatusEffectType.Shielded));
        }

        [Test]
        public void DualWieldSwingsTwiceAsTwoSeparateBeats()
        {
            // Two beats, not one: a single beat plays exactly one
            // lunge/hit/recoil cycle no matter what happens inside it, so two
            // swings sharing a beat would render as one action with the second
            // number silently overwriting the first.
            var relic = new ResolvedRelic("dual", "Dual Wield", "", RelicEffect.DualWield, 0);
            var (session, _, encounter) = Fight(Kit(relics: new[] { relic }));

            session.ExecuteAttack(encounter.Enemies[0]);
            var mine = session.DrainBeats().Where(b => b.Actor != null && b.Actor.IsPlayerSide).ToList();

            Assert.AreEqual(2, mine.Count);
            Assert.IsTrue(mine.SelectMany(b => b.Messages).Any(m => m.Contains("Dual Wield strikes")));
        }

        [Test]
        public void DualWieldDoesNotSwingAtACorpse()
        {
            var relic = new ResolvedRelic("dual", "Dual Wield", "", RelicEffect.DualWield, 0);
            var frail = Foe("Frail", health: 1);
            var tank = Foe("Tank");
            var (session, _, _) = Fight(Kit(relics: new[] { relic }), null, frail, tank);

            session.ExecuteAttack(frail);
            var mine = session.DrainBeats().Where(b => b.Actor != null && b.Actor.IsPlayerSide).ToList();

            Assert.AreEqual(1, mine.Count);
        }

        [Test]
        public void OnlyABasicAttackFeedsTheSignatureResource()
        {
            // Shawn's attack barely dents an enemy's defence; the resource is
            // what makes swinging anyway a deliberate choice rather than a
            // wasted turn. Skill deliberately does not grant it.
            var swinger = Hero();
            swinger.SignaturePool = new ResourcePool("wool", "Wool", 16, 0, gainOnAttack: 3, gainOnDamageTaken: 0);
            var (swing, _, swingFoes) = Fight(Kit(), swinger);
            swing.ExecuteAttack(swingFoes.Enemies[0]);
            Assert.AreEqual(3, swinger.SignaturePool.Current);

            var caster = Hero();
            caster.SignaturePool = new ResourcePool("wool", "Wool", 16, 0, gainOnAttack: 3, gainOnDamageTaken: 0);
            var (cast, _, castFoes) = Fight(Kit(skills: new[] { Skill(SkillEffect.DamageSingle) }), caster);
            cast.CastSkill(0, castFoes.Enemies[0]);

            Assert.AreEqual(0, caster.SignaturePool.Current, "a cast is not a swing");
        }

        [Test]
        public void DualWieldsSecondSwingDoesNotGrantTheResourceAgain()
        {
            var relic = new ResolvedRelic("dual", "Dual Wield", "", RelicEffect.DualWield, 0);
            var hero = Hero();
            hero.SignaturePool = new ResourcePool("wool", "Wool", 16, 0, gainOnAttack: 3, gainOnDamageTaken: 0);
            var (session, _, encounter) = Fight(Kit(relics: new[] { relic }), hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(3, hero.SignaturePool.Current, "one attack, one grant, however many swings it produced");
        }

        // ---- the PRIMARY pool's gainOnAttack -----------------------------------
        //
        // The other half of "what counts as an attack", and deliberately not
        // the same answer as the three tests above. Wool's rule is narrow on
        // purpose (only a basic swing); a rage bar's is the plain reading of
        // the authored sentence -- Fury "gains when he deals damage" -- so it
        // fires off any damaging ACTION, once, and off nothing that deals no
        // damage. Phase E shipped it wired to the Attack verb like Wool, and
        // Bjorn's Slam therefore built nothing at all.
        //
        // Every expected value below is a LITERAL (CLAUDE.md gotcha 5): the
        // fixture authors 15, so a grant is 15 and no grant is 0.
        //
        // gainOnDamageTaken is 0 on this fixture even though Fury authors 10,
        // and the zero is what makes the numbers below readable: every one of
        // these actions ends the turn, the enemies reply inside
        // AdvanceAfterAction, and a fixture that also gained on being hit
        // would report the attack gain and the enemy's reply added together.
        // The same reason the Wool fixtures three tests up author 0 there.
        private static CombatantState Brawler(string name = "Bjorn")
        {
            var bjorn = Hero(name);
            bjorn.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 15, gainOnDamageTaken: 0);
            return bjorn;
        }

        // 100_000 DodgeRating curves to 99% and, against this file's fixed
        // SeededRandom(3) on an otherwise-empty fixture, dodges -- the same
        // device DodgeCoversEveryDamagePathTests uses, for the same reason.
        private static void MakeUnhittable(CombatantState target) =>
            target.ModifierEffects = new ModifierEffectSet(
                new[] { new ModifierEffect(ModifierEffectType.DodgeRating, 100_000) });

        [Test]
        public void APlainAttackThatLandsFeedsThePrimaryPool()
        {
            var bjorn = Brawler();
            var (session, _, encounter) = Fight(Kit(), bjorn);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(15, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void ADamagingSkillFeedsThePrimaryPoolToo()
        {
            // THE DEFECT THIS PINS. A Slam is a Skill, and phase B's wiring
            // sat inside the Attack verb, so this was 0.
            var bjorn = Brawler();
            var (session, _, encounter) = Fight(Kit(skills: new[] { Skill(SkillEffect.DamageSingle) }), bjorn);

            session.CastSkill(0, encounter.Enemies[0]);

            Assert.AreEqual(15, bjorn.PrimaryPool.Current, "a cast that deals damage is dealing damage");
        }

        [Test]
        public void AThreeTargetSweepPaysThePrimaryPoolOnceAndNotThreeTimes()
        {
            // The damage funnel runs once per target. Without a per-action
            // boundary this reads 45, which would make a sweep the only move
            // a rage bar's owner ever wants.
            var bjorn = Brawler();
            var (session, _, _) = Fight(
                Kit(skills: new[] { Skill(SkillEffect.DamageAll) }), bjorn,
                Foe("A"), Foe("B"), Foe("C"));

            session.CastSkill(0, null);

            Assert.AreEqual(15, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void AMissFeedsThePrimaryPoolNothing()
        {
            // The verb site paid whether or not the blow landed, which is the
            // second half of the same bug: a rage bar filled by swinging at
            // air.
            var bjorn = Brawler();
            var slippery = Foe("Slippery");
            MakeUnhittable(slippery);
            var (session, _, _) = Fight(Kit(), bjorn, slippery);

            session.ExecuteAttack(slippery);

            Assert.AreEqual(0, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void AHealFeedsThePrimaryPoolNothing()
        {
            var bjorn = Brawler();
            bjorn.CurrentHealth = 100;
            var (session, _, _) = Fight(Kit(skills: new[] { Skill(SkillEffect.HealSelf) }), bjorn);

            session.CastSkill(0, bjorn);

            Assert.AreEqual(0, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void DualWieldsSecondSwingDoesNotFeedThePrimaryPoolAgainEither()
        {
            // One action, one grant -- the same answer the signature pool
            // gives two tests up, arrived at by a different route (a
            // per-action lock rather than a single call site).
            var relic = new ResolvedRelic("dual", "Dual Wield", "", RelicEffect.DualWield, 0);
            var bjorn = Brawler();
            var (session, _, encounter) = Fight(Kit(relics: new[] { relic }), bjorn);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(15, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void ManaAuthorsZeroOnAttackSoNothingAboveChangesIt()
        {
            // The control. Every other character in the game holds the mana
            // row, whose gainOnAttack is 0, so moving the grant to the damage
            // funnel must be invisible to them.
            var hero = Hero();
            hero.PrimaryPool.TrySpend(10);
            var (session, _, encounter) = Fight(Kit(), hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(40, hero.CurrentMana, "50 minus the 10 spent, and not a point of attack gain");
        }
    }
}
