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
            new CombatantState(name, true, health, mana, 40, 0, speed);

        private static CombatantState Foe(string name = "Foe", int health = 1000) =>
            new CombatantState(name, false, health, 10, 5, 0, 1);

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
            new ResolvedSkill("test", displayName, "", "hero", 1, effect, SkillTargeting.SingleEnemy,
                manaCost, resourceCost, false, power, flatAmount, false,
                damageInstances, "", 0.6f, 3, "", 0,
                appliesStatus: appliesStatus, statusMagnitude: statusMagnitude,
                statusDuration: statusDuration, queuePushSlots: queuePushSlots, transform: transform);

        private static PlayerKit Kit(CharacterRole role = CharacterRole.Tank,
            IReadOnlyList<ResolvedSkill> skills = null,
            IReadOnlyList<ResolvedRelic> relics = null) =>
            new PlayerKit("hero", role, skills, relics, null);

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
            armoured.Defense = 20;
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
            hero.CurrentMana = 0;
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

        // ---- role riders ----------------------------------------------------------

        [Test]
        public void TheTankDrinksFromTheBlow()
        {
            var hero = Hero();
            hero.CurrentHealth = 100;
            var (session, _, encounter) = Fight(Kit(CharacterRole.Tank), hero);

            session.ExecuteSkill(encounter.Enemies[0]);

            Assert.Greater(hero.CurrentHealth, 100);
            Assert.IsTrue(Messages(session).Any(m => m.Contains("recovers")));
        }

        [Test]
        public void CrowdControlShredsTheTargetsGuard()
        {
            var foe = Foe();
            foe.Defense = 10;
            var (session, _, _) = Fight(Kit(CharacterRole.CrowdControl), null, foe);

            session.ExecuteSkill(foe);

            Assert.AreEqual(10 - FightTuning.CrowdControlDefenseShred, foe.Defense);
        }

        [Test]
        public void CrowdControlShredNeverGoesNegative()
        {
            // Below zero it would start ADDING damage through the subtraction in
            // the damage formula, which is a different mechanic entirely.
            var foe = Foe();
            foe.Defense = 1;
            var (session, _, _) = Fight(Kit(CharacterRole.CrowdControl), null, foe);

            session.ExecuteSkill(foe);

            Assert.AreEqual(0, foe.Defense);
        }

        [Test]
        public void SupportMendsTheWholeSquad()
        {
            var hero = Hero();
            var ally = Hero("Ally");
            hero.CurrentHealth = 100;
            ally.CurrentHealth = 100;
            var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(CharacterRole.Support) }, null, new SeededRandom(3))
            { DamageVarianceRange = 0f };

            session.ExecuteSkill(encounter.Enemies[0]);

            Assert.Greater(ally.CurrentHealth, 100, "the ally is mended too, not only the caster");
        }

        [Test]
        public void UtilityFeedsItsOwnEngineInsteadOfThePartysHealthBar()
        {
            var hero = Hero();
            hero.Signature = new SignatureResource("wool", "Wool", 16, 0, 0, 0);
            var (session, _, encounter) = Fight(Kit(CharacterRole.Utility), hero);

            session.ExecuteSkill(encounter.Enemies[0]);

            Assert.AreEqual(FightTuning.UtilitySkillSignatureGain, hero.Signature.Current);
        }

        [Test]
        public void UtilityWithNoSignatureResourceIsSimplyANoOp()
        {
            // Graceful by construction rather than by a special case.
            var (session, _, encounter) = Fight(Kit(CharacterRole.Utility));

            Assert.DoesNotThrow(() => session.ExecuteSkill(encounter.Enemies[0]));
        }

        [Test]
        public void TheAssassinExecutesAWeakenedTarget()
        {
            var strong = Foe("Strong");
            var (baseline, _, _) = Fight(Kit(CharacterRole.Assassin), null, strong);
            baseline.ExecuteSkill(strong);
            int ordinary = baseline.DrainBeats()[0].Amount;

            var weakened = Foe("Weakened");
            var (session, _, _) = Fight(Kit(CharacterRole.Assassin), null, weakened);
            weakened.CurrentHealth = (int)(weakened.MaxHealth * FightTuning.AssassinExecuteHealthFraction);

            session.ExecuteSkill(weakened);
            var beats = session.DrainBeats();

            Assert.Greater(beats[0].Amount, ordinary);
            Assert.IsTrue(beats.SelectMany(b => b.Messages).Any(m => m.Contains("finds an opening")));
        }

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
            var (cast, _, castFoes) = Fight(Kit(relics: new[] { relic }), caster);
            cast.ExecuteSkill(castFoes.Enemies[0]);

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
            swinger.Signature = new SignatureResource("wool", "Wool", 16, 0, gainOnAttack: 3, gainOnDamageTaken: 0);
            var (swing, _, swingFoes) = Fight(Kit(), swinger);
            swing.ExecuteAttack(swingFoes.Enemies[0]);
            Assert.AreEqual(3, swinger.Signature.Current);

            var caster = Hero();
            caster.Signature = new SignatureResource("wool", "Wool", 16, 0, gainOnAttack: 3, gainOnDamageTaken: 0);
            var (cast, _, castFoes) = Fight(Kit(), caster);
            cast.ExecuteSkill(castFoes.Enemies[0]);

            Assert.AreEqual(0, caster.Signature.Current, "a cast is not a swing");
        }

        [Test]
        public void DualWieldsSecondSwingDoesNotGrantTheResourceAgain()
        {
            var relic = new ResolvedRelic("dual", "Dual Wield", "", RelicEffect.DualWield, 0);
            var hero = Hero();
            hero.Signature = new SignatureResource("wool", "Wool", 16, 0, gainOnAttack: 3, gainOnDamageTaken: 0);
            var (session, _, encounter) = Fight(Kit(relics: new[] { relic }), hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(3, hero.Signature.Current, "one attack, one grant, however many swings it produced");
        }
    }
}
