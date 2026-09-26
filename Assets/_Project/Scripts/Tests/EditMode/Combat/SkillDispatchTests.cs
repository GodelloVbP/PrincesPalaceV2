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

        // SPELL-EXPANSION BASELINE (docs/SPELL_EXPANSION_BASELINE.md, area 5):
        // a Nature/Poison-typed hit detonates whatever Poison is ALREADY on
        // the target (ResolveDetonation, called from inside DamagePipeline.
        // AfterDefences) before ApplySkillStatus -- reached only afterward,
        // once ApplyFinalDamage has landed -- applies the skill's OWN status.
        // The old entry is gone (StatusCombos.SpendPoisonIfMatched removes it)
        // by the time the fresh one is authored, so the two never merge --
        // this is the "detonates old Poison before fresh Poison is applied"
        // rule the Viper's Bite brief asks to preserve, proven end to end
        // rather than at StatusCombos' own unit level (StatusCombosTests).
        [Test]
        public void ANatureCastThatAlsoAppliesPoison_DetonatesTheOldPoisonBeforeTheFreshOneLands()
        {
            var skill = Skill(SkillEffect.DamageSingle, "Root Strike",
                damageInstances: new[] { new DamageInstance(DamageType.Nature, 10) },
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 5, statusDuration: 3);

            // Baseline: the same cast on a fresh target with no Poison out, so
            // the packet's own damage is measured rather than assumed.
            var (baselineSession, _, baselineEncounter) = Fight(Kit(skills: new[] { skill }));
            var baselineFoe = baselineEncounter.Enemies[0];
            int baselineBefore = baselineFoe.CurrentHealth;
            baselineSession.CastSkill(0, baselineFoe);
            int packetDamage = baselineBefore - baselineFoe.CurrentHealth;

            // The real case: an OLD Poison already out, worth 2 x 4 = 8
            // remaining -- deliberately NOT equal to the fresh skill's own
            // 5 x 3, so a wrong order (fresh applied/merged, THEN detonated)
            // would read completely differently on both counts below.
            var (session, _, encounter) = Fight(Kit(skills: new[] { skill }));
            var foe = encounter.Enemies[0];
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 2, 4));
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before - packetDamage - 8, foe.CurrentHealth,
                "the packet's own damage plus exactly the OLD poison's remaining worth (2 x 4) -- " +
                "not the fresh skill's own numbers, which is the proof the old one detonated first");

            var poison = foe.Statuses.Single(s => s.Type == StatusEffectType.Poison);
            Assert.AreEqual(5, poison.Magnitude, "the fresh Poison this skill authors, standing alone");
            Assert.AreEqual(3, poison.TurnsRemaining,
                "not merged with the old entry's turns -- it was already gone (detonated) by the time this applied");
        }

        // ---- ignoresDefense on the FIXED-PACKET path, milestone B (plan 1.3) -

        // ResolveDamageInstances used to omit ignoresDefense entirely, so a
        // fixed-packet spell's flag was inert -- true on the row, false at
        // every resolution. Blackglass Spear is the first live content that
        // would have hit this silently.
        private static ResolvedSkill FixedPacketSkill(DamageType type, int amount, bool ignoresDefense) =>
            new ResolvedSkill("test", "Fixed Packet", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, ignoresDefense,
                new[] { new DamageInstance(type, amount) }, SpellPresentation.None, 0);

        [Test]
        public void AFixedPacketSpellWithIgnoresDefense_TakesNoMagicalDefenceTerm()
        {
            var withFlag = FixedPacketSkill(DamageType.Void, 13, ignoresDefense: true);
            var withoutFlag = FixedPacketSkill(DamageType.Void, 13, ignoresDefense: false);

            var armoured = Foe("Armoured");
            armoured.MagicalDefense = 20;
            var (defended, _, defendedEncounter) = Fight(Kit(skills: new[] { withoutFlag }), null, armoured);
            int beforeDefended = defendedEncounter.Enemies[0].CurrentHealth;
            defended.CastSkill(0, defendedEncounter.Enemies[0]);
            int dealtWithBroadDefense = beforeDefended - defendedEncounter.Enemies[0].CurrentHealth;

            var bypassTarget = Foe("Armoured");
            bypassTarget.MagicalDefense = 20;
            var (bypassed, _, bypassedEncounter) = Fight(Kit(skills: new[] { withFlag }), null, bypassTarget);
            int beforeBypassed = bypassedEncounter.Enemies[0].CurrentHealth;
            bypassed.CastSkill(0, bypassedEncounter.Enemies[0]);
            int dealtIgnoringDefense = beforeBypassed - bypassedEncounter.Enemies[0].CurrentHealth;

            Assert.AreEqual(13, dealtIgnoringDefense, "the raw packet, with MagicalDefense zeroed out entirely");
            Assert.Less(dealtWithBroadDefense, dealtIgnoringDefense,
                "the un-flagged packet must still be reduced by the same MagicalDefense -- proving the " +
                "difference is the flag, not the fixture");
        }

        [Test]
        public void AFixedPacketSpellWithIgnoresDefense_StillTakesTypedResistance()
        {
            var skill = FixedPacketSkill(DamageType.Void, 13, ignoresDefense: true);
            var resistant = Foe("Warded");
            resistant.MagicalDefense = 20; // ignored by the flag
            resistant.TypedResistance.Void = 100; // NEVER skipped -- a separate term
            var (session, _, encounter) = Fight(Kit(skills: new[] { skill }), null, resistant);
            int before = encounter.Enemies[0].CurrentHealth;

            session.CastSkill(0, encounter.Enemies[0]);

            int dealt = before - encounter.Enemies[0].CurrentHealth;
            Assert.Less(dealt, 13, "100 Void resistance must still soften the packet even with defense ignored");
            Assert.Greater(dealt, 0, "resistance softens on a curve; it does not zero the hit");
        }

        [Test]
        public void AFixedPacketSpellWithIgnoresDefense_StillTakesTheWardPool()
        {
            var skill = FixedPacketSkill(DamageType.Void, 13, ignoresDefense: true);
            var warded = Foe("Warded");
            warded.MagicalDefense = 20; // bypassed by the flag
            StatusEffects.ApplyWard(warded.Statuses, points: 5, turns: 2, source: warded);
            var (session, _, encounter) = Fight(Kit(skills: new[] { skill }), null, warded);
            int before = encounter.Enemies[0].CurrentHealth;

            session.CastSkill(0, encounter.Enemies[0]);

            int dealt = before - encounter.Enemies[0].CurrentHealth;
            Assert.AreEqual(13 - 5, dealt,
                "the 5-point ward pool is the LAST thing between the raw packet and health -- " +
                "ignoresDefense has no say over it");
            Assert.AreEqual(0, StatusEffects.WardPoints(warded), "the ward absorbed exactly its 5 points and is spent");
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

        // THE RETURN, NOT THE REQUEST -- the rule the potion path
        // (FightSession.Items.cs), Gift: Mana and RestorePartyMana all already
        // state in writing. HealAndCount clamps at max health, so a 40-point
        // Mend on a hero at full health moves nothing: the ledger recorded 0
        // while the log said 40 and a green +40 floated over a full bar.
        [Test]
        public void AHealOnAFullHealthHeroRecordsNothing()
        {
            var skill = Skill(SkillEffect.HealSelf, "Mend", flatAmount: 40);
            var hero = Hero();
            var (session, _, _) = Fight(Kit(skills: new[] { skill }), hero);

            Assert.AreEqual(hero.MaxHealth, hero.CurrentHealth, "fixture: the hero starts at full health");

            session.CastSkill(0, null);
            var beats = session.DrainBeats();
            var beat = beats[0];

            Assert.AreEqual(0, beat.Amount, "no green popup over a bar that did not move");
            Assert.IsTrue(beats.SelectMany(b => b.Messages).Any(m => m.EndsWith("recovers 0 HP.")),
                "the line announced the request rather than what landed: "
                + string.Join(" | ", beats.SelectMany(b => b.Messages)));
        }

        // The party arm has one honest number too -- it is the SUM of what
        // landed, not the per-ally request repeated.
        [Test]
        public void APartyHealAnnouncesWhatTheSquadActuallyGained()
        {
            var skill = Skill(SkillEffect.HealParty, "Rally", flatAmount: 60);
            var hero = Hero();
            var ally = Hero("Ally");
            hero.CurrentHealth = hero.MaxHealth - 10;

            var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(skills: new[] { skill }) }, null, new SeededRandom(3))
            { DamageVarianceRange = 0f };

            session.CastSkill(0, null);
            var lines = Messages(session).ToList();

            // The caster was missing 10 and the ally nothing, so 10 landed in
            // total however generous the cast was.
            Assert.IsTrue(lines.Any(m => m.Contains("mends the squad for 10.")),
                "the squad line has to sum the returns: " + string.Join(" | ", lines));
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
            var mine = session.DrainBeats().Where(b => b.IsAction && b.Actor.IsPlayerSide).ToList();

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
            var mine = session.DrainBeats().Where(b => b.IsAction && b.Actor.IsPlayerSide).ToList();

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

        // ---- the PRIMARY pool's gainOnDamageTaken ------------------------------
        //
        // The other half of the same pair, and the same defect the block above
        // fixed for gainOnAttack: the grant sat at the enemy's plain-swing VERB
        // (GrantSignatureForHitTaken), which is where WOOL's narrower rule
        // lives, so every other way of taking a hit paid nothing at all -- an
        // enemy SKILL (the Bog Witch's Mud Burst, the Golem's Boulder Slam, the
        // Warden's Overhead Slam and Grapple all return down the skill branch),
        // an AOE, a relic's free swing, an on-hit rider.
        //
        // The two tests below field the SAME victim and the SAME single hit and
        // differ only in how the monster throws it, so the literal has to be
        // the same number twice. gainOnAttack is 0 on this fixture for the
        // reason the Brawler fixture above authors 0 the other way round:
        // Bjorn swings first here, and a fixture that also gained on attacking
        // would report the two added together.
        private static CombatantState Punchbag(string name = "Bjorn")
        {
            // 5000 health so the blow cannot kill him and cap the reading at
            // whatever HP he had left, and 5000 on the monster so Bjorn's own
            // opening swing cannot fell it before it replies.
            var bjorn = new CombatantState(name, true, 5000, 30, 40, 10);
            bjorn.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 0, gainOnDamageTaken: 10);
            return bjorn;
        }

        // Weight 1 against nothing else, so the draw cannot pick anything but
        // the ability handed in -- this is about the wiring, not the dice.
        private static (FightSession session, CombatantState bjorn, CombatantState monster) HitBy(
            EnemyAbility only)
        {
            var bjorn = Punchbag();
            var monster = new CombatantState("Witch", false, 5000, 0, 12, 9);
            var source = new ResolvedEnemy("witch", "Witch", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0);
            var kit = new EnemyKit(source, false, new List<EnemyAbility> { only });

            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { monster }),
                new List<PlayerKit> { Kit() }, new List<EnemyKit> { kit }, new SeededRandom(7))
            {
                DamageVarianceRange = 0f,
            };

            // Begin() is what commits the intent. Without it the monster
            // arrives at ResolveEnemyAction with nothing telegraphed and falls
            // back to a plain swing, which would make both tests below the
            // same test.
            session.Begin();
            return (session, bjorn, monster);
        }

        [Test]
        public void APlainEnemySwingFeedsTheVictimsPrimaryPool()
        {
            // The control: this one has always worked, because the grant was
            // wired to exactly this verb and nothing else.
            var (session, bjorn, monster) = HitBy(EnemyAbility.LegacyAttack("Swing", 1f, 1f));

            session.ExecuteAttack(monster);

            Assert.AreEqual(10, bjorn.PrimaryPool.Current, "one hit taken, one grant");
        }

        [Test]
        public void ADamagingEnemySkillFeedsTheVictimsPrimaryPoolToo()
        {
            // THE DEFECT THIS PINS. The skill branch of ResolveEnemyAction
            // returns before the verb's grant is ever reached, so this read 0
            // -- a rage bar that four of the six shipped monsters could not
            // fill by hitting its owner.
            var (session, bjorn, monster) = HitBy(
                EnemyAbility.Of(Skill(SkillEffect.DamageSingle, displayName: "Mud Burst"), 1f));

            session.ExecuteAttack(monster);

            Assert.AreEqual(10, bjorn.PrimaryPool.Current,
                "a skill that deals damage is dealing damage, on both ends of the blow");
        }

        // ---- a status tick is damage taken -------------------------------------
        //
        // THE DECISION THESE TWO PIN, stated once here rather than twice below.
        // A poison tick IS damage the pool's owner took: it pays
        // gainOnDamageTaken, and it makes the turn it lands in not an idle one
        // for the decay. Poison is the only damage in the game that never
        // passed through the session's funnel (StatusEffects.Tick writes health
        // itself and FightSession only books a ledger row afterwards), so it was
        // the one blow the pools could not hear -- and the shape that produced
        // was the exact opposite of what a rage bar is for: a poisoned Bjorn
        // standing still was ground down by 20 HP a turn AND lost 10 Fury a turn
        // for the privilege.
        //
        // ORDER, AND IT IS DELIBERATE. GrantTurnStart ticks the pool before it
        // ticks the statuses (FightSession.Riders), and that order is load-
        // bearing elsewhere -- Runic's ward conversion reads the mana "including
        // the regen this very turn-start just granted". So the decay at turn N's
        // start judges the window that ENDED at turn N, and the poison tick a
        // few lines later belongs to turn N. The first tick after being poisoned
        // therefore still eats one decay; every tick after it does not, which is
        // what the second test below pins.
        private static (FightSession session, CombatantState bjorn) Poisoned(
            int fury, int gainOnDamageTaken, int decayPerIdleTurn)
        {
            var bjorn = new CombatantState("Bjorn", true, 500, 30, 40, 100);
            bjorn.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0,
                gainOnAttack: 0, gainOnDamageTaken: gainOnDamageTaken);
            bjorn.PrimaryPool.DecayPerIdleTurn = decayPerIdleTurn;
            bjorn.PrimaryPool.Gain(fury);

            // Speed 100 against 1: Speed is a CHARGE RATE here, not a rotation,
            // so Bjorn takes every turn for the length of these tests and the
            // monster never gets one. That is what keeps a swing of its own out
            // of the numbers below without having to make anybody unhittable.
            var foe = Foe("Slug");
            StatusEffects.Apply(bjorn.Statuses, StatusEffectType.Poison, 20, 5, foe);

            var encounter = new CombatEncounter(new[] { bjorn }, new[] { foe });
            var session = new FightSession(encounter, new List<PlayerKit> { Kit() }, null,
                new SeededRandom(3)) { DamageVarianceRange = 0f };

            // Begin() is the turn boundary: it runs GrantTurnStart for whoever
            // opens the fight, which is Bjorn.
            session.Begin();
            return (session, bjorn);
        }

        [Test]
        public void APoisonTickIsDamageTakenLikeAnyOther()
        {
            // No decay authored here, so the only thing that can move the bar
            // is the grant: 50 in, one 20-point tick, 10 out.
            var (_, bjorn) = Poisoned(fury: 50, gainOnDamageTaken: 10, decayPerIdleTurn: 0);

            Assert.AreEqual(480, bjorn.CurrentHealth, "the tick itself still deals its 20");
            Assert.AreEqual(60, bjorn.PrimaryPool.Current, "the tick paid the pool nothing at all");
        }

        [Test]
        public void APoisonTickMeansTheTurnItLandedInWasNotIdle()
        {
            // gainOnDamageTaken 0, so the decay is the only mover and the
            // number below is about the idle test alone.
            var (session, bjorn) = Poisoned(fury: 50, gainOnDamageTaken: 0, decayPerIdleTurn: 10);

            // Turn one's start decayed against an empty window (nothing had
            // happened yet), which is correct: 50 -> 40. Then the tick landed.
            Assert.AreEqual(40, bjorn.PrimaryPool.Current, "turn one's decay");

            // An item is the idle action -- it opens a beat and notes Action,
            // never Damage, so nothing but the poison can keep turn two alive.
            session.UseConsumable("Rag", 0, restoresMana: false);

            Assert.AreEqual(40, bjorn.PrimaryPool.Current,
                "turn two decayed a bar that had been poisoned for 20 the turn before");
        }

        // ---- RestorePartyMana says what it did ---------------------------------
        //
        // Same contract as the Gift's: CombatMath.RestoreMana "returns how much
        // actually landed, so a caller can say the true number (or say nothing)
        // rather than announcing an amount it hoped for", and this arm threw the
        // return away and announced a squad-wide restore unconditionally. The
        // bot already previews the same cast correctly (Lookahead2Policy sums
        // only members CanRestoreMana accepts), so the POLICY and the LOG
        // disagreed about one cast.
        //
        // No skill in skills.json authors RestorePartyMana today, so this is an
        // authoring trap rather than a live bug -- which is the reason to close
        // it now rather than the reason not to.
        [Test]
        public void RestorePartyManaSaysNothingWasRestoredWhenNobodysPoolTakesIt()
        {
            // A squad of one Fury holder: every recipient the cast can reach
            // refuses mana, so the true total is 0.
            var bjorn = Hero("Bjorn");
            bjorn.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0, 0, 0);
            bjorn.PrimaryPool.RestoredByManaEffects = false;

            var (session, _, _) = Fight(
                Kit(skills: new[] { Skill(SkillEffect.RestorePartyMana, "Chorus", flatAmount: 25) }), bjorn);

            session.CastSkill(0, null);

            var lines = Messages(session).ToList();
            Assert.IsFalse(lines.Any(m => m.Contains("restores the squad")),
                "nobody's bar moved, so nothing restored the squad's anything");
            Assert.IsTrue(lines.Any(m => m.Contains("finds nothing to restore")),
                "and it has to say so rather than going quiet");
        }

        [Test]
        public void RestorePartyManaStillAnnouncesARestoreThatLanded()
        {
            var hero = Hero();
            hero.PrimaryPool.Current = 0;

            var (session, _, _) = Fight(
                Kit(skills: new[] { Skill(SkillEffect.RestorePartyMana, "Chorus", flatAmount: 25) }), hero);

            session.CastSkill(0, null);

            Assert.Greater(hero.CurrentMana, 0, "fixture: the cast has to actually restore something");
            Assert.IsTrue(Messages(session).Any(m => m.Contains("restores the squad")));
        }

        // ---- the POWER row on the skill card ---------------------------------

        // The row is a promise about the cast that is ABOUT TO HAPPEN
        // (FightHudModel.cs's DetailForSkill header: pre-mitigation damage from
        // the caster's own stats/relics/buffs). The buff half of that rides
        // CombatantState.BonusAttackPercent, which only an ACTION writes -- so a
        // preview reading the raw field quotes the previous action's board in
        // both directions: it misses a ward bonus the cast will apply, and it
        // keeps quoting a Gift: Fury the player already spent.
        //
        // Literal, not recomputed: attack 100 with no scaling set is a scaled
        // attack of 100, plus flatAmount 100 = 200 bare, and 150 + 100 = 250 at
        // +50%.
        private static ResolvedSkill PowerRowHit() =>
            new ResolvedSkill("headbutt", "Headbutt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 100, false,
                null, SpellPresentation.None, 0);

        [Test]
        public void ThePowerRowMatchesTheCastItDescribes_WithAWardJustLanded()
        {
            // flatAmount 40 -- a ward authors its own pool now.
            var ward = new ResolvedSkill("fleece_ward", "Fleece Ward", "", "hero", 1, SkillEffect.Ward,
                SkillEntryResolver.DefaultTargetingFor(SkillEffect.Ward), 0, 0, false, 0, 40, false,
                null, SpellPresentation.None, 0);
            var hit = PowerRowHit();

            var hero = new CombatantState("Shawn", true, 1000, 30, 100, 500);
            hero.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.WardDamageBonusSelf, 50),
            });

            var (session, _, _) = Fight(Kit(skills: new[] { ward, hit }), hero, Foe("Foe", 1000000));

            session.CastSkill(0, hero);
            Assert.IsTrue(StatusEffects.IsWarded(hero), "fixture: the ward landed on its caster");

            Assert.AreEqual(250, session.PreviewSkillPower(hero, hit),
                "the card has to see the ward bonus the very next cast will apply");
        }

        [Test]
        public void ThePowerRowMatchesTheCastItDescribes_AfterTheGiftWasSpent()
        {
            var hit = PowerRowHit();
            var hero = new CombatantState("Shawn", true, 1000, 30, 100, 500);
            var (session, _, _) = Fight(Kit(skills: new[] { hit }), hero, Foe("Foe", 1000000));

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Empowered, 50, 99, hero);
            session.CastSkill(0, session.Encounter.Enemies[0]);

            Assert.AreEqual(200, session.PreviewSkillPower(hero, hit),
                "the gift was burned by that cast; the card must stop quoting it");
        }

        // The worse bug the fix must not introduce: a card that spends the gift
        // by being looked at.
        [Test]
        public void PreviewingAPowerRowNeverSpendsTheGift()
        {
            var hit = PowerRowHit();
            var hero = new CombatantState("Shawn", true, 1000, 30, 100, 500);
            var (session, _, _) = Fight(Kit(skills: new[] { hit }), hero, Foe("Foe", 1000000));

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Empowered, 50, 99, hero);

            Assert.AreEqual(250, session.PreviewSkillPower(hero, hit), "the gift is still in hand");
            Assert.AreEqual(250, session.PreviewSkillPower(hero, hit), "and hovering twice does not spend it");

            var foe = session.Encounter.Enemies[0];
            int before = foe.CurrentHealth;
            session.CastSkill(0, foe);

            Assert.AreEqual(250, before - foe.CurrentHealth,
                "the cast still got the empowerment the previews quoted");
        }

        // ---- milestone C: two arities, one dispatcher (plan 1.12) ---------

        [Test]
        public void EverySinglePickSkillStillCastsThroughTheOneTargetOverload()
        {
            // THE REGRESSION THIS MILESTONE COULD HAVE CAUSED, and the one
            // worth a test of its own: a second door into CastSkill is only
            // safe if the first one still reaches the identical path. Every
            // caller in the game, the bot and this suite uses the one-target
            // form; if it had quietly become a different code path, the
            // failure would show up as "one spell behaves oddly" months
            // later rather than here.
            //
            // ASSERTED AS AN IDENTITY, not as a spot check: the same board,
            // the same skill and the same seed cast through each door have to
            // leave the same numbers behind.
            var skill = Skill(SkillEffect.DamageSingle, "Jab", damageInstances:
                new[] { new DamageInstance(DamageType.Fire, 9) });

            var (throughOne, _, oneEncounter) = Fight(Kit(skills: new[] { skill }));
            var oneFoe = oneEncounter.Enemies[0];
            Assert.IsTrue(throughOne.CastSkill(0, oneFoe), "the one-target door refused an ordinary cast");

            var (throughList, _, listEncounter) = Fight(Kit(skills: new[] { skill }));
            var listFoe = listEncounter.Enemies[0];
            Assert.IsTrue(throughList.CastSkillOnPicks(0, new[] { listFoe }),
                "the pick-list door refused the same cast");

            Assert.AreEqual(oneFoe.CurrentHealth, listFoe.CurrentHealth,
                "the two doors into CastSkill produced different damage");
            Assert.AreEqual(oneFoe.MaxHealth - oneFoe.CurrentHealth > 0, true,
                "fixture: neither cast dealt anything, so the comparison proves nothing");
        }

        [Test]
        public void ACastHandedNoTargetAtAllIsRefused_ThroughEitherDoor()
        {
            // A NULL TARGET STAYS A REFUSAL (AUDIT #147), and an EMPTY pick
            // list has to read the same way rather than as "this cast takes
            // no target". The two are one line apart in CastSkill and the
            // distinction is invisible at a call site, so it is pinned.
            var ward = Skill(SkillEffect.Ward, "Ward", flatAmount: 20);
            var ally = Hero("Ally", speed: 1);
            var (session, hero, _) = Fight(Kit(skills: new[] { ward }), Hero(speed: 30));
            var encounter = new CombatEncounter(new[] { hero, ally }, new[] { Foe() });
            var twoUp = new FightSession(encounter, new List<PlayerKit> { Kit(skills: new[] { ward }) },
                null, new SeededRandom(3));

            Assert.IsFalse(twoUp.CastSkillOnPicks(0, new CombatantState[0]),
                "an empty pick list is a cast nobody aimed, not a cast with no target");
            Assert.AreSame(hero, twoUp.Current, "the refusal spent the turn");
            Assert.IsFalse(StatusEffects.IsWarded(hero), "the refusal warded somebody anyway");
        }

    }
}
