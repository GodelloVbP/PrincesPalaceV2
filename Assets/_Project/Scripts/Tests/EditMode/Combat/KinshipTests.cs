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
    // Kinship (docs/PLAN_PETTING_ZOO.md, "Kinship"): the bearer's first damage
    // packet with amount > 0 in a fight is turned aside whole, in
    // FightSession.ApplyAndCountDamage after the pools hear it and before the
    // Phoenix Egg. Fixture relics only -- relics.json has no Kinship row yet.
    public class KinshipTests
    {
        private const string Bearer = "sheep";
        private const string KinshipLine = "Kinship turns the blow aside.";

        private static ResolvedRelic Kinship() =>
            new ResolvedRelic("kinship", "Kinship", "", RelicEffect.Kinship, 0, bearer: Bearer, draftable: false);

        private static ResolvedRelic Plain(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static EnemyKit Dummy(string name) =>
            new EnemyKit(new ResolvedEnemy(name, name, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        // Hero on 100/100, two unkillable dummies that barely act. The kit id
        // is what a `bearer` is matched against.
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            string kitId = Bearer, IEnumerable<ResolvedRelic> extra = null,
            IReadOnlyList<ResolvedSkill> skills = null, int heroSpeed = 20, CombatantState hero = null)
        {
            hero ??= new CombatantState("Shawn", true, 100, 100, 20, heroSpeed);
            var foe1 = new CombatantState("Foe1", false, 999999, 0, 1, 1);
            var foe2 = new CombatantState("Foe2", false, 999999, 0, 1, 1);

            var relics = new List<ResolvedRelic> { Kinship() };
            if (extra != null) relics.AddRange(extra);

            var kit = new PlayerKit(kitId, CharacterRole.Tank, skills, relics, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe1, foe2 }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { Dummy("Foe1"), Dummy("Foe2") },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe1);
        }

        // One hero, one monster that answers every hero action with exactly
        // one swing (speed 10 against 9 -- DodgeCoversEveryDamagePathTests'
        // fixture). Attack 30, no variance.
        private static (FightSession session, CombatantState hero, CombatantState monster) Duel(
            DamageType monsterAttackType = DamageType.Physical, StatusEffectType? appliesStatus = null)
        {
            var hero = new CombatantState("Shawn", true, 500, 100, 20, 10);
            var monster = new CombatantState("Golem", false, 1000, 10, 30, 9);
            var source = new ResolvedEnemy("golem", "Golem", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0, attackType: monsterAttackType,
                appliesStatus: appliesStatus, statusMagnitude: appliesStatus.HasValue ? 10 : 0,
                statusDuration: appliesStatus.HasValue ? 3 : 0);

            var kit = new PlayerKit(Bearer, CharacterRole.Tank, null, new List<ResolvedRelic> { Kinship() }, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { monster }),
                new List<PlayerKit> { kit }, new List<EnemyKit> { new EnemyKit(source, false) },
                new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, monster);
        }

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        // ---- the one packet ------------------------------------------------------

        [Test]
        public void TheFirstPacketIsCancelledAndTheNextOneLands()
        {
            var (session, hero, foe) = Fight();

            session.DealDamageForTest(foe, hero, 10, DamageType.Physical);
            Assert.AreEqual(100, hero.CurrentHealth, "the first packet is turned aside whole");
            Assert.AreEqual(1, Messages(session).Count(m => m == KinshipLine), "one log line");

            session.DealDamageForTest(foe, hero, 10, DamageType.Physical);
            Assert.AreEqual(90, hero.CurrentHealth, "once per fight -- the second packet lands");
            Assert.AreEqual(0, Messages(session).Count(m => m == KinshipLine));
        }

        [Test]
        public void AnEnemySwingIsTurnedAsideAndTheNextSwingLands()
        {
            var (session, hero, monster) = Duel();

            session.ExecuteAttack(monster);
            Assert.AreEqual(500, hero.CurrentHealth, "the monster's first swing is cancelled");
            var first = session.DrainBeats().First(b => b.Actor == monster);
            Assert.Contains(KinshipLine, first.Messages.ToList());
            Assert.AreEqual(0, first.Amount, "a cancelled packet shows no number on the stage");
            Assert.AreEqual(0, first.Absorbed, "and no soak");
            CollectionAssert.AreEqual(new[] { KinshipLine }, first.Messages,
                "the Kinship line is the whole log -- no 'attacks Shawn for 30 damage!'");
            Assert.IsFalse(first.Stances.ContainsKey(hero), "no hurt pose on a blow that landed nothing");
            Assert.IsFalse(first.HasTargetVoice, "and no grunt");
            Assert.IsFalse(first.Missed, "not a dodge either -- nothing was missed");

            session.ExecuteAttack(monster);
            Assert.AreEqual(470, hero.CurrentHealth, "the second swing lands for its full 30");
        }

        // A multi-packet attack: a Poison swing into a poisoned bearer
        // detonates the poison (one packet) and then hits (another). Only the
        // first packet to reach the funnel is cancelled.
        [Test]
        public void ACancelledSwingAppliesNoOnHitStatus()
        {
            var (session, hero, monster) = Duel(appliesStatus: StatusEffectType.Poison);

            session.ExecuteAttack(monster);
            Assert.AreEqual(500, hero.CurrentHealth, "fixture: the swing was cancelled");
            Assert.IsFalse(hero.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "the claws' poison rides a landed hit, and this one landed nothing");

            session.ExecuteAttack(monster);
            // 500 - 30 (the swing) - 10 (the poison it applied, ticking at the
            // start of Shawn's own turn, which is where ExecuteAttack returns).
            Assert.AreEqual(460, hero.CurrentHealth, "fixture: the second swing lands");
            Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "the landed swing applies it as usual");
        }

        [Test]
        public void ARiderPacketFromTheSameAttackStillLands()
        {
            var (session, hero, monster) = Duel(DamageType.Poison);
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 5, 3); // detonates for 5 x 3 = 15

            session.ExecuteAttack(monster);

            Assert.IsFalse(hero.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "fixture: the swing really detonated the poison");
            Assert.AreEqual(470, hero.CurrentHealth,
                "the detonation (15) is cancelled and the swing (30) lands: 500 - 30");
        }

        [Test]
        public void ItResetsInTheNextFight()
        {
            var hero = new CombatantState("Shawn", true, 100, 100, 20, 20);

            var (first, _, foe1) = Fight(hero: hero);
            first.DealDamageForTest(foe1, hero, 10, DamageType.Physical);
            first.DealDamageForTest(foe1, hero, 10, DamageType.Physical);
            Assert.AreEqual(90, hero.CurrentHealth, "fixture: spent in the first fight");

            var (second, _, foe2) = Fight(hero: hero);
            second.DealDamageForTest(foe2, hero, 10, DamageType.Physical);
            Assert.AreEqual(90, hero.CurrentHealth, "a new fight turns aside a new first packet");
        }

        // ---- the Phoenix Egg -----------------------------------------------------

        [Test]
        public void ALethalFirstPacketIsCancelledAndTheEggStaysUnhatchedAndUnlocked()
        {
            var (session, hero, foe) = Fight(extra: new[] { Plain(RelicEffect.PhoenixEgg) });

            session.DealDamageForTest(foe, hero, 999, DamageType.Physical);
            Assert.AreEqual(100, hero.CurrentHealth);
            Assert.IsFalse(hero.IsPhoenixEgg, "a cancelled blow must not hatch the egg");

            // The egg's once-per-combat lock was never spent, so the next
            // lethal blow still hatches it.
            session.DealDamageForTest(foe, hero, 999, DamageType.Physical);
            Assert.IsTrue(hero.IsPhoenixEgg, "the egg's lock was left unused");
            Assert.AreEqual(1, hero.CurrentHealth);
        }

        // ---- what the cancel skips -----------------------------------------------

        [Test]
        public void ACancelledPacketAbsorbsNothingAndBooksNoLedgerRow()
        {
            var (session, hero, foe) = Fight();
            hero.SignaturePool = new ResourcePool("wool", "Wool", 10, 0, 0, 0,
                absorbPerPoint: 1, absorbsDamage: true) { Current = 10 };

            session.DealDamageForTest(foe, hero, 10, DamageType.Physical);

            Assert.AreEqual(100, hero.CurrentHealth);
            Assert.AreEqual(10, hero.SignaturePool.Current, "no wool absorb");
            Assert.AreEqual(0, session.Ledger.For(Bearer).DamageTaken, "no damage-taken row");
            Assert.AreEqual(0, session.Ledger.For(Bearer).Shielded, "no absorbed row");
            Assert.AreEqual(0, session.Ledger.For("Foe1").TotalDealt, "no damage-dealt row");
            Assert.IsFalse(Messages(session).Any(m => m.Contains("soaks")), "no false soak report");
        }

        [Test]
        public void ACancelledPacketDoesNotProcBerserkersVest()
        {
            var skill = new ResolvedSkill("s", "S", "", "hero", 1, SkillEffect.HealSelf, SkillTargeting.Self,
                0, 0, false, 0, 1, false, null, SpellPresentation.None, 0, cooldownTurns: 20);
            var (session, hero, foe) = Fight(extra: new[] { Plain(RelicEffect.BerserkersVest) },
                skills: new List<ResolvedSkill> { skill }, heroSpeed: 100);

            session.CastSkill(0, hero);
            Assert.IsFalse(Messages(session).Contains(KinshipLine), "fixture: nothing has spent Kinship yet");
            int before = session.CooldownRemaining(hero, "s");
            Assert.Greater(before, 1, "fixture: the skill is on cooldown");

            session.DealDamageForTest(foe, hero, 10, DamageType.Physical);
            Assert.AreEqual(before, session.CooldownRemaining(hero, "s"), "the cancelled packet shaves nothing");

            // And the vest's once-per-turn lock was not spent by it either:
            // the next packet, same turn, lands and shaves.
            session.DealDamageForTest(foe, hero, 10, DamageType.Physical);
            Assert.AreEqual(before - 1, session.CooldownRemaining(hero, "s"));
        }

        // ---- what never reaches it -----------------------------------------------

        [Test]
        public void ADodgedSwingDoesNotSpendIt()
        {
            var (session, hero, monster) = Duel();
            hero.ModifierEffects = new ModifierEffectSet(new[]
                { new ModifierEffect(ModifierEffectType.DodgeRating, 100_000) });

            session.ExecuteAttack(monster);
            Assert.IsTrue(session.DrainBeats().First(b => b.Actor == monster).Missed, "fixture: the swing was dodged");

            session.DealDamageForTest(monster, hero, 10, DamageType.Physical);
            Assert.AreEqual(500, hero.CurrentHealth, "Kinship was still there for the first packet that arrived");
        }

        [Test]
        public void AFullyWardedSwingDoesNotSpendIt()
        {
            var (session, hero, monster) = Duel();
            StatusEffects.ApplyWard(hero.Statuses, 1000, 5);

            session.ExecuteAttack(monster);
            Assert.AreEqual(500, hero.CurrentHealth);
            Assert.IsFalse(Messages(session).Contains(KinshipLine), "the ward ate the swing; Kinship never saw it");

            // DealDamageForTest goes straight to the funnel, past the ward.
            session.DealDamageForTest(monster, hero, 10, DamageType.Physical);
            Assert.AreEqual(500, hero.CurrentHealth, "Kinship was still there for the first packet that arrived");
        }

        // ---- the bearer ----------------------------------------------------------

        [Test]
        public void ANonBearerHoldingItGetsNothing()
        {
            var (session, bear, foe) = Fight(kitId: "bear");

            session.DealDamageForTest(foe, bear, 10, DamageType.Physical);

            Assert.AreEqual(90, bear.CurrentHealth);
            Assert.IsFalse(session.KitFor(bear).HasRelic(RelicEffect.Kinship));
            Assert.AreEqual(1, session.KitFor(bear).Relics.Count, "carried, just inert");
        }
    }
}
