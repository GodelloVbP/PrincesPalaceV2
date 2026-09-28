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
    // A status tick lands through the session's damage funnel
    // (FightSession.Ledger), so what lives in the funnel hears it: Kinship,
    // the Phoenix Egg, World Ender's Crown, the ledger rows and the death
    // settlement. What does NOT change: a tick still skips the mitigation
    // pipeline, the pools still hear one blow per status type per tick, the
    // ledger still books the health actually lost, nobody is credited for a
    // DoT kill, and a tick is still not "a hit" to Berserker's Vest.
    public class DotThroughTheFunnelTests
    {
        private const string KinshipLine = "Kinship turns the blow aside.";

        private static ResolvedRelic Relic(RelicEffect effect, string bearer = null) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0, bearer: bearer);

        private static (FightSession session, CombatantState foe) Fight(
            CombatantState hero, string kitId = "sheep", IReadOnlyList<ResolvedSkill> skills = null,
            params ResolvedRelic[] relics)
        {
            var foe = new CombatantState("Foe", false, 999999, 0, 1, 1);
            var kit = new PlayerKit(kitId, CharacterRole.Tank, skills, relics.ToList(), null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return (session, foe);
        }

        private static CombatantState Hero(int health = 100, int speed = 20) =>
            new CombatantState("Shawn", true, health, 100, 20, speed);

        private static ResourcePool Fury(int gainOnAttack, int gainOnDamageTaken) =>
            new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: gainOnAttack, gainOnDamageTaken: gainOnDamageTaken);

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        // ---- Kinship --------------------------------------------------------------

        [Test]
        public void ADotTickSpendsKinship_TheFirstTickIsCancelledAndTheSecondLands()
        {
            var hero = Hero();
            var (session, _) = Fight(hero, "sheep", null, Relic(RelicEffect.Kinship, bearer: "sheep"));
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 3);

            session.TickStatusesForTest(hero);
            Assert.AreEqual(100, hero.CurrentHealth, "the first tick is turned aside whole");
            var beats = session.DrainBeats();
            var line = beats.Where(b => b.Messages.Contains(KinshipLine)).ToList();
            Assert.AreEqual(1, line.Count, "one Kinship line");
            Assert.AreSame(hero, line[0].Target, "said on the tick's own beat");
            Assert.AreEqual(0, line[0].Amount, "a cancelled tick shows no number");
            Assert.IsFalse(line[0].Stances.ContainsKey(hero), "and no hurt pose");
            Assert.IsFalse(beats.SelectMany(b => b.Messages).Any(m => m.Contains("suffers")),
                "no damage line for a tick that landed nothing");
            Assert.AreEqual(0, session.Ledger.For("sheep").DamageTaken);

            session.TickStatusesForTest(hero);
            Assert.AreEqual(90, hero.CurrentHealth, "Kinship is spent; the second tick lands");
            Assert.AreEqual(10, session.Ledger.For("sheep").DamageTaken);
        }

        // Pools hear a cancelled blow -- the funnel's own rule for every packet
        // Kinship turns aside (FightSession.Ledger.ApplyAndCountDamage).
        [Test]
        public void ACancelledTickStillFeedsThePoolsOnce()
        {
            var hero = new CombatantState("Shawn", true, 100, Fury(0, 10), 20, 20);
            var (session, _) = Fight(hero, "sheep", null, Relic(RelicEffect.Kinship, bearer: "sheep"));
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(100, hero.CurrentHealth, "fixture: cancelled");
            Assert.AreEqual(10, hero.PrimaryPool.Current);
        }

        // ---- the Phoenix Egg ------------------------------------------------------

        [Test]
        public void ALethalDotTickHatchesThePhoenixEgg()
        {
            var hero = Hero();
            var (session, _) = Fight(hero, "sheep", null, Relic(RelicEffect.PhoenixEgg));
            hero.CurrentHealth = 5;
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 3);

            session.TickStatusesForTest(hero);

            Assert.IsTrue(hero.IsPhoenixEgg, "the tick would have killed; the egg hatches instead");
            Assert.AreEqual(1, hero.CurrentHealth);
            Assert.IsTrue(hero.IsAlive);
            Assert.AreEqual(0, session.Ledger.For("sheep").TimesDowned);
            var hatch = session.DrainBeats().Where(b => b.Messages.Any(m => m.Contains("curl into an egg"))).ToList();
            Assert.AreEqual(1, hatch.Count, "one hatch line");
            Assert.AreSame(hero, hatch[0].Target, "said on the tick's own beat");
        }

        [Test]
        public void ADotTickOnAHatchedShellEatsTheShell()
        {
            var hero = Hero();
            var (session, foe) = Fight(hero, "sheep", null, Relic(RelicEffect.PhoenixEgg));
            session.DealDamageForTest(foe, hero, 999, DamageType.Physical); // hatch, EggHealth = 100
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(90, hero.EggHealth);
            Assert.AreEqual(1, hero.CurrentHealth, "the shell took it, not the wearer");
        }

        // ---- World Ender's Crown (a tick crossing counts) -------

        [Test]
        public void ADotTickCrossingThirtyPercentFiresTheCrown()
        {
            var hero = Hero();
            var (session, foe) = Fight(hero, "sheep", null, Relic(RelicEffect.WorldEndersCrown));
            hero.CurrentHealth = 35;
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(25, hero.CurrentHealth, "fixture: 35 -> 25 crosses 30%");
            Assert.IsTrue(foe.Statuses.Any(s => s.Type == StatusEffectType.Feared),
                "the crossing fired the crown");
        }

        // ---- unchanged: Berserker's Vest ------------------------------------------

        // A tick is not "a hit" (the Fragile Lamb's rule, FightSession.Enemies
        // GrantSignatureForHitTaken's header): the vest shaves nothing for it.
        [Test]
        public void ADotTickDoesNotProcBerserkersVest()
        {
            var skill = new ResolvedSkill("s", "S", "", "hero", 1, SkillEffect.HealSelf, SkillTargeting.Self,
                0, 0, false, 0, 1, false, null, SpellPresentation.None, 0, cooldownTurns: 20);
            var hero = Hero(speed: 100);
            var (session, foe) = Fight(hero, "sheep", new List<ResolvedSkill> { skill },
                Relic(RelicEffect.BerserkersVest));

            session.CastSkill(0, hero);
            int before = session.CooldownRemaining(hero, "s");
            Assert.Greater(before, 1, "fixture: the skill is on cooldown");
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(90, hero.CurrentHealth, "fixture: the tick landed");
            Assert.AreEqual(before, session.CooldownRemaining(hero, "s"), "a tick shaves nothing");

            // Control: the vest's once-per-turn lock is still unspent, so a
            // real hit now shaves.
            session.DealDamageForTest(foe, hero, 10, DamageType.Physical);
            Assert.AreEqual(before - 1, session.CooldownRemaining(hero, "s"));
        }

        // ---- unchanged: totals and kill credit ------------------------------------

        // 15 health, 10 poison a tick: 10, then 5 (no overkill in damage
        // taken). Nobody is credited: no kill row, no damage dealt.
        [Test]
        public void ADotKill_BooksTheSameTotalsAndCreditsNobody()
        {
            var hero = Hero();
            var rat = new CombatantState("Rat", false, 15, 0, 1, 1);
            var tank = new CombatantState("Tank", false, 999999, 0, 1, 1);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { rat, tank }),
                new List<PlayerKit> { new PlayerKit("hero", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.ApplyStatusToForTest(rat, StatusEffectType.Poison, 10, 3, source: hero);

            session.TickStatusesForTest(rat);
            Assert.AreEqual(5, rat.CurrentHealth, "fixture: 10 a tick");
            session.TickStatusesForTest(rat);

            Assert.IsFalse(rat.IsAlive, "fixture: the second tick killed");
            Assert.AreEqual(15, session.Ledger.For("Rat").DamageTaken);
            Assert.AreEqual(1, session.Ledger.For("Rat").TimesDowned, "one body, one down");
            Assert.AreEqual(0, session.Ledger.For("hero").Kills, "a tick credits nobody");
            Assert.AreEqual(0, session.Ledger.For("hero").TotalDealt);
        }

        // Last Stand T2 caps a 20-point tick to 5 while under half health. The
        // ledger books the 5 that left health, not the 20 that was sent.
        [Test]
        public void ASpikeCappedTickBooksTheHealthActuallyLost()
        {
            var hero = Hero();
            hero.Talents = new TalentEffectSet(new[]
                { new TalentEffect(TalentEffectType.DamageCapPercentBelowHealth, 5, threshold: 50) });
            var (session, _) = Fight(hero, "sheep");
            hero.CurrentHealth = 40;
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 20, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(35, hero.CurrentHealth, "fixture: capped at 5% of 100");
            Assert.AreEqual(5, session.Ledger.For("sheep").DamageTaken);
        }

        // ---- unchanged: the pools --------------------------------------------------

        // Two poison instances are one row, and a row is one blow to the pools:
        // +10 fury, not +20, and no gainOnAttack (nobody attacked).
        [Test]
        public void FuryFromAPoisonTickIsOneBlowPerStatusType()
        {
            var hero = new CombatantState("Bjorn", true, 500, Fury(15, 10), 20, 20);
            var (session, _) = Fight(hero, "bear");
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 5, 3);
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 5, 3);
            hero.PrimaryPool.Current = 0;

            session.TickStatusesForTest(hero);

            Assert.AreEqual(490, hero.CurrentHealth, "fixture: both instances ticked");
            Assert.AreEqual(10, hero.PrimaryPool.Current);
        }
    }
}
