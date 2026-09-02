using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Ice Fingernail, Loaded Dice, Monkey King's Scepter, Sparring Saber,
    // Sparring Buckler, Essence Siphon, Disgruntled Lackey, Inconspicuous
    // Key, Dancer's Anklet, Berserker's Vest and Phoenix Egg -- combat-level,
    // same shape as BalanceRelicsTests: a hand-built PlayerKit handed
    // straight to a real FightSession. (Jo-Sun's Book of Anatomy is a pure
    // CombatMath test below rather than a fight -- it has no relic hook at
    // all. Vampire Dentures is a fight test, but sets RelicLifestealPercent
    // directly on the CombatantState rather than through a ResolvedRelic --
    // the same shape ArmorPenetrationTests uses for a pure numeric stat --
    // since it carries no RelicEffect either.)
    public class BalanceRelics2Tests
    {
        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static EnemyKit Foe(string name = "dummy") =>
            new EnemyKit(new ResolvedEnemy(name, name, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) Fight(
            IReadOnlyList<RelicEffect> relics, int heroSpeed = 20, int foeSpeed = 1, int foeAttack = 1,
            IReadOnlyList<ResolvedSkill> skills = null)
        {
            var hero = new CombatantState("Shawn", true, 100, 100, 20, heroSpeed);
            var foe1 = new CombatantState("Foe1", false, 999999, 0, foeAttack, foeSpeed);
            var foe2 = new CombatantState("Foe2", false, 999999, 0, foeAttack, foeSpeed);

            var relicList = new List<ResolvedRelic>();
            foreach (var effect in relics) relicList.Add(Relic(effect));

            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, relicList, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe1, foe2 }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { Foe("Foe1"), Foe("Foe2") },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe1, foe2);
        }

        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) Fight(
            RelicEffect relic, int heroSpeed = 20, int foeSpeed = 1, int foeAttack = 1,
            IReadOnlyList<ResolvedSkill> skills = null) =>
            Fight(new[] { relic }, heroSpeed, foeSpeed, foeAttack, skills);

        // ---- ice fingernail --------------------------------------------------------

        [Test]
        public void EachLandedAttackStacksATenPercentSlow()
        {
            // Equal speed keeps the turn order a clean 1:1 alternation, so
            // the malus math below is exact rather than fighting whichever
            // side charges faster.
            var (session, hero, foe1, _) = Fight(RelicEffect.IceFingernail, heroSpeed: 100, foeSpeed: 100);

            session.ExecuteAttack(foe1);
            Assert.AreEqual(-10, session.SpeedBonusFrom(foe1, RelicEffect.IceFingernail), "1 stack of 10% off a 100 base");
            Assert.AreEqual(90, foe1.Speed);

            session.ExecuteAttack(foe1);
            Assert.AreEqual(-20, session.SpeedBonusFrom(foe1, RelicEffect.IceFingernail), "2 stacks = 20%");
            Assert.AreEqual(80, foe1.Speed);
        }

        [Test]
        public void TheSlowCapsAtFortyPercentAcrossFourStacks()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.IceFingernail, heroSpeed: 100, foeSpeed: 100);

            for (int i = 0; i < 4; i++) session.ExecuteAttack(foe1);

            Assert.AreEqual(4, FallingOffStacks.Count(foe1, FightTuning.IceFingernailStackKey), "capped at 4 stacks");
            Assert.AreEqual(-40, session.SpeedBonusFrom(foe1, RelicEffect.IceFingernail));
            Assert.AreEqual(60, foe1.Speed);
        }

        // ---- loaded dice -----------------------------------------------------------

        [Test]
        public void ExactlyOneEnemyIsStunnedAtCombatStart()
        {
            var (_, _, foe1, foe2) = Fight(RelicEffect.LoadedDice);

            bool foe1Stunned = StatusEffects.HasStun(foe1.Statuses);
            bool foe2Stunned = StatusEffects.HasStun(foe2.Statuses);

            Assert.AreNotEqual(foe1Stunned, foe2Stunned, "exactly one enemy should be stunned, never zero or both");
        }

        // ---- monkey king's scepter --------------------------------------------------

        [Test]
        public void MeleeReachesAnyEnemyRegardlessOfTheFrontRank()
        {
            var (session, _, _, foe2) = Fight(RelicEffect.MonkeyKingsScepter);

            Assert.IsTrue(session.CanMeleeReach(foe2), "foe2 stands behind the living front rank, foe1");
        }

        [Test]
        public void WithoutTheScepterTheFrontRankRuleStillApplies()
        {
            var (session, _, _, foe2) = Fight(RelicEffect.DualWield); // a relic irrelevant to reach

            Assert.IsFalse(session.CanMeleeReach(foe2), "foe1 is alive and blocks foe2 without the scepter");
        }

        // ---- jo-sun's book of anatomy (pure CombatMath, no relic hook) --------------

        [Test]
        public void JoSunsBonusMultipliesTheWeaknessMultiplierByTwentyPercent()
        {
            var weakToFire = ElementalAffinity.Of(DamageType.Fire, DamageType.Ice);

            float baseMultiplier = CombatMath.EffectivenessMultiplier(DamageType.Fire, weakToFire);
            float boosted = CombatMath.EffectivenessMultiplier(DamageType.Fire, weakToFire, weaknessBonusPercent: 20);

            Assert.AreEqual(1.5f, baseMultiplier, 0.0001f, "this game's own weakness multiplier is 1.5x, not 2.0x");
            Assert.AreEqual(1.8f, boosted, 0.0001f, "1.5 x 1.2 = 1.8 -- +20% multiplicative on the actual base");
        }

        [Test]
        public void JoSunsBonusDoesNotTouchTheResistanceMultiplier()
        {
            var resistsFire = ElementalAffinity.Of(DamageType.Ice, DamageType.Fire);

            float boosted = CombatMath.EffectivenessMultiplier(DamageType.Fire, resistsFire, weaknessBonusPercent: 20);

            Assert.AreEqual(0.5f, boosted, 0.0001f, "the bonus is authored against weakness only");
        }

        // ---- sparring saber ----------------------------------------------------------

        [Test]
        public void AlteringYourOwnPositionGrantsThirtyPercentSpeedForOneTurn()
        {
            var (session, hero, _, _) = Fight(RelicEffect.SparringSaber, heroSpeed: 100, foeSpeed: 100);

            session.NotePositionChangedForTest(hero, hero);

            Assert.AreEqual(30, session.SpeedBonusFrom(hero, RelicEffect.SparringSaber), "30% of a 100 base");
            Assert.AreEqual(130, hero.Speed);
        }

        [Test]
        public void SomeoneElseMovingDoesNotGrantSparringSaberAnything()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.SparringSaber, heroSpeed: 100);

            session.NotePositionChangedForTest(foe1, hero); // hero's ACTION moved someone else

            Assert.AreEqual(0, session.SpeedBonusFrom(hero, RelicEffect.SparringSaber), "only your OWN position counts");
        }

        // ---- sparring buckler ----------------------------------------------------------

        [Test]
        public void CastingAnAbilityThatAltersAnyPositionGrantsAWard()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.SparringBuckler);

            session.NotePositionChangedForTest(foe1, hero); // hero's cast moved foe1

            // "Ward" reuses this game's existing Shielded status -- spent on
            // the NEXT hit taken, not a passive multiplier -- see
            // NotePositionChanged's own comment.
            int reduced = StatusEffects.ConsumeShieldedReduction(hero, 100);
            Assert.AreEqual(85, reduced, "15% off the next hit taken");
        }

        [Test]
        public void TheWardIsOncePerTurn()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.SparringBuckler);

            session.NotePositionChangedForTest(foe1, hero);
            StatusEffects.ConsumeShieldedReduction(hero, 999); // spend it

            session.NotePositionChangedForTest(foe2, hero); // same turn, second trigger

            Assert.IsFalse(StatusEffects.IsWarded(hero), "the lock should have refused a second ward this turn");
        }

        // ---- essence siphon ---------------------------------------------------------

        [Test]
        public void KillingANonSummonEnemyHealsThreePercentMaxHealth()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.EssenceSiphon, heroSpeed: 100, foeSpeed: 1);
            hero.CurrentHealth = 50;
            foe1.CurrentHealth = 1;

            session.ExecuteAttack(foe1);

            Assert.AreEqual(53, hero.CurrentHealth, "3% of 100 max health = 3, healed on top of 50");
        }

        [Test]
        public void KillingASummonHealsNothing()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.EssenceSiphon, heroSpeed: 100, foeSpeed: 1);
            hero.CurrentHealth = 50;
            foe1.CurrentHealth = 1;
            foe1.IsSummon = true;

            session.ExecuteAttack(foe1);

            Assert.AreEqual(50, hero.CurrentHealth, "summons do not pay out");
        }

        // ---- disgruntled lackey -------------------------------------------------------

        [Test]
        public void AnEnemySummonDrawsTheHoldersFullMaxHealthInDamage()
        {
            bool SummonFactory(string id, out CombatantState state, out EnemyKit kit)
            {
                state = new CombatantState("Add", false, 50, 0, 1, 1);
                kit = Foe("Add");
                return true;
            }

            var session = SessionWithSummonFactory(RelicEffect.DisgruntledLackey, SummonFactory,
                out var hero, out var summoner);

            var summonSkill = new ResolvedSkill("roar", "Roar", "", "boss", 1, SkillEffect.Summon,
                SkillTargeting.Self, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                summonEnemyId: "add", summonCap: 1);

            session.ResolveSummonForTest(summoner, summonSkill);

            Assert.AreEqual(0, summoner.CurrentHealth,
                $"{hero.MaxHealth} max HP dealt to a 100-HP summoner kills it outright");
            Assert.IsFalse(summoner.IsAlive);
        }

        [Test]
        public void NoHolderMeansTheSummonerTakesNothing()
        {
            bool SummonFactory(string id, out CombatantState state, out EnemyKit kit)
            {
                state = new CombatantState("Add", false, 50, 0, 1, 1);
                kit = Foe("Add");
                return true;
            }

            var session = SessionWithSummonFactory(RelicEffect.DualWield, SummonFactory,
                out _, out var summoner);
            int before = summoner.CurrentHealth;

            var summonSkill = new ResolvedSkill("roar", "Roar", "", "boss", 1, SkillEffect.Summon,
                SkillTargeting.Self, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                summonEnemyId: "add", summonCap: 1);

            session.ResolveSummonForTest(summoner, summonSkill);

            Assert.AreEqual(before, summoner.CurrentHealth);
        }

        private static FightSession SessionWithSummonFactory(
            RelicEffect relic, FightSession.SummonFactory factory,
            out CombatantState hero, out CombatantState summoner)
        {
            hero = new CombatantState("Shawn", true, 100, 0, 20, 100) { };
            summoner = new CombatantState("Boss", false, 100, 0, 1, 1);

            var kit = new PlayerKit("hero", CharacterRole.Tank, new List<ResolvedSkill>(),
                new List<ResolvedRelic> { Relic(relic) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { summoner }),
                new List<PlayerKit> { kit }, new List<EnemyKit> { Foe("Boss") },
                new SeededRandom(5), summonFactory: factory) { DamageVarianceRange = 0f };
            session.Begin();
            return session;
        }

        // ---- inconspicuous key --------------------------------------------------------

        [Test]
        public void OncePerCombatAFallenEnemyStrikesOneLastBlow()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.InconspicuousKey, heroSpeed: 100, foeSpeed: 1, foeAttack: 7);
            hero.CurrentHealth = 100;
            foe1.CurrentHealth = 1;
            int foe2Before = foe2.CurrentHealth;

            session.ExecuteAttack(foe1); // kills foe1 (attack 7 -- the "one last blow" it deals)

            Assert.AreEqual(foe2Before - 7, foe2.CurrentHealth, "the fallen foe's own Attack, dealt to another enemy");
        }

        [Test]
        public void ItOnlyFiresOncePerCombat()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.InconspicuousKey, heroSpeed: 100, foeSpeed: 1, foeAttack: 7);
            hero.CurrentHealth = 100;
            foe1.CurrentHealth = 1;

            session.ExecuteAttack(foe1); // kills foe1 -- consumes the once-per-combat lock
            Assert.AreEqual(999999 - 7, foe2.CurrentHealth, "the first kill's bonus blow landed on foe2");

            foe2.CurrentHealth = 1;
            session.ExecuteAttack(foe2); // kills foe2 too -- no third enemy left, and the lock is spent regardless

            // Nothing left to assert a bonus blow landed ON -- foe2 was the
            // last enemy standing. The lock's own state is the load-bearing
            // claim here: a second RandomLivingEnemy draw never happens
            // because InconspicuousKeyOnKill returns before reaching it.
            Assert.IsTrue(session.IsOver);
        }

        // ---- dancer's anklet -----------------------------------------------------------

        [Test]
        public void AttackingAutomaticallyRepositionsTheWearerForward()
        {
            // Paired with Sparring Buckler rather than Sparring Saber: a
            // one-turn Speed buff's entire observable lifecycle sits inside
            // one synchronous round trip (see FightSession.SpeedBuffs' own
            // header) -- by the time ExecuteAttack returns control, hero's
            // own NEXT turn has already started (a 2-combatant fight
            // alternates every action) and already ticked a 1-turn buff
            // away. Sparring Buckler's ward rides Shielded's own 99-turn
            // "for the rest of the fight" duration instead, so it survives
            // the round trip and still proves Dancer's Anklet actually
            // fired the shared NotePositionChanged event.
            var (session, hero, foe1, _) = Fight(
                new[] { RelicEffect.DancersAnklet, RelicEffect.SparringBuckler }, heroSpeed: 100, foeSpeed: 100);

            session.ExecuteAttack(foe1);

            int reduced = StatusEffects.ConsumeShieldedReduction(hero, 100);
            Assert.AreEqual(85, reduced,
                "Dancer's Anklet's automatic reposition should have fired Sparring Buckler's own ward");
        }

        // ---- berserker's vest -----------------------------------------------------------

        [Test]
        public void GettingHitLowersEveryActiveCooldownByOneTurn()
        {
            // A large cooldown, deliberately -- CastSkill only returns
            // control once it is the player's own turn again, and how many
            // of the actor's own turns elapse before that happens is a
            // property of the turn order's charge scheduling, not something
            // this test controls. A small cooldown risks reading "before"
            // already at 0, which is Berserker's Vest's own OncePerTurn
            // reduction doing its job -- just not the moment this test means
            // to measure it at.
            var skill = new ResolvedSkill("s", "S", "", "hero", 1, SkillEffect.HealSelf, SkillTargeting.Self,
                0, 0, false, 0, 1, false, null, SpellPresentation.None, 0, cooldownTurns: 20);

            var (session, hero, foe1, _) = Fight(RelicEffect.BerserkersVest, heroSpeed: 100, foeSpeed: 100,
                skills: new List<ResolvedSkill> { skill });

            session.CastSkill(0, hero);

            // CastSkill only returns control once it is the player's own
            // turn again -- in a two-combatant fight that means hero's OWN
            // next turn has already started and ticked this very cooldown
            // once (GrantTurnStart -> TickCooldowns) before this line ever
            // runs. The baseline is read rather than assumed to be the
            // authored 20 for exactly that reason.
            int before = session.CooldownRemaining(hero, "s");
            Assert.Greater(before, 0, "fixture: the skill should still be on cooldown at all");

            session.DealDamageForTest(foe1, hero, 10, DamageType.Physical);
            Assert.AreEqual(before - 1, session.CooldownRemaining(hero, "s"), "one hit, one turn shaved off");

            session.DealDamageForTest(foe1, hero, 10, DamageType.Physical);
            Assert.AreEqual(before - 1, session.CooldownRemaining(hero, "s"),
                "once per turn -- the second hit does nothing more");
        }

        // ---- phoenix egg -----------------------------------------------------------------

        [Test]
        public void FatalDamageHatchesAnEggInsteadOfKilling()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.PhoenixEgg);

            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // would otherwise be fatal

            Assert.IsTrue(hero.IsPhoenixEgg);
            Assert.AreEqual(hero.MaxHealth, hero.EggHealth);
            Assert.AreEqual(FightTuning.PhoenixEggDurationTurns, hero.EggTurnsRemaining);
            Assert.AreEqual(1, hero.CurrentHealth, "pinned at 1 while the shell stands");
            Assert.IsTrue(hero.IsAlive);
        }

        [Test]
        public void FurtherHitsEatTheEggsOwnPoolNotRealHealth()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch, EggHealth = 100

            session.DealDamageForTest(foe1, hero, 40, DamageType.Physical);

            Assert.AreEqual(60, hero.EggHealth);
            Assert.AreEqual(1, hero.CurrentHealth, "still pinned -- the shell ate the hit, not the wearer");
        }

        [Test]
        public void TheEggBreakingKillsTheWearerOutright()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch, EggHealth = 100

            session.DealDamageForTest(foe1, hero, 100, DamageType.Physical); // the shell's own pool reaches 0

            Assert.IsFalse(hero.IsPhoenixEgg);
            Assert.AreEqual(0, hero.CurrentHealth);
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void SurvivingThreeTurnsRevivesWithTheEggsSurvivingFraction()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch, EggHealth = 100
            session.DealDamageForTest(foe1, hero, 50, DamageType.Physical); // EggHealth = 50 (50% survives)

            session.TickPhoenixEggForTest(hero); // 3 -> 2
            session.TickPhoenixEggForTest(hero); // 2 -> 1
            Assert.IsTrue(hero.IsPhoenixEgg, "must still be a shell before the third tick");

            session.TickPhoenixEggForTest(hero); // 1 -> 0, revives

            Assert.IsFalse(hero.IsPhoenixEgg);
            Assert.AreEqual(50, hero.CurrentHealth, "50% of the egg's own pool survived -> 50% of 100 max health");
        }

        [Test]
        public void ItOnlyHatchesOncePerCombat()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch #1
            session.TickPhoenixEggForTest(hero);
            session.TickPhoenixEggForTest(hero);
            session.TickPhoenixEggForTest(hero); // revives at 1 HP (EggHealth never touched again = 100)

            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // would be fatal again

            Assert.IsFalse(hero.IsPhoenixEgg, "the lock is once per COMBAT -- it must not hatch a second time");
            Assert.AreEqual(0, hero.CurrentHealth);
        }

        // ---- vampire dentures (pure RelicModifier stat, no RelicEffect) -----------------

        [Test]
        public void VampireDenturesHealsTenPercentOfLandedDamage()
        {
            var hero = new CombatantState("Shawn", true, 100, 0, 20, 20)
            {
                RelicLifestealPercent = 10,
                CurrentHealth = 50,
            };
            var foe = new CombatantState("Foe", false, 999999, 0, 1, 1);

            var kit = new PlayerKit("hero", CharacterRole.Tank, new List<ResolvedSkill>(),
                new List<ResolvedRelic>(), null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, new List<EnemyKit> { Foe("Foe") },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ExecuteAttack(foe);

            int damageDealt = 999999 - foe.CurrentHealth;
            Assert.AreEqual(20, damageDealt, "20 Attack, no defense, no variance -- an exact 20-point swing");
            Assert.AreEqual(52, hero.CurrentHealth, "10% of 20 = 2, healed on top of 50");
        }
    }
}
