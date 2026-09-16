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
    // PHASE D1. The exhaustive per-path proof the plan's own risk callout
    // demands: every REAL damage path in this codebase that ever reaches
    // DamagePipeline.AfterDefences must honour a target's DodgeRating,
    // and a dodge must silence every on-hit rider that would otherwise fire
    // off that same landed hit -- not just zero the damage number.
    //
    // The real, non-preview call sites of AfterDefences, enumerated by
    // reading every call site in Domain/Combat/Session/ (see this phase's own
    // report for the full list) -- one test per path below, named to match:
    //   1. FightSession.cs ResolveAttackSwing            (a plain swing)
    //   2. FightSession.Skills.cs ResolveDamageSingle     (a character skill, single target)
    //   3. FightSession.Skills.cs ResolveDamageAll        (a character skill, AOE)
    //   4. FightSession.Skills.cs ResolveDamageInstances  (a fixed-packet spell)
    //   5. FightSession.Enemies.cs ResolveEnemyAction      (an enemy's real swing)
    //
    // ExecuteSkillInner (the free basic Skill action every character used to
    // have regardless of what they had learned) was removed with BasicSpell
    // (docs/PLAN_SHOP.md Gate 4) -- this file's old path 2 proof went with it.
    //
    // Everything that does NOT reach AfterDefences at all -- splash/kill-
    // splash (SplashOntoNeighbours), Shatter, and a Poison DoT tick -- gets
    // its own "stays undodgeable" proof in the second half of this file,
    // confirming the documented design decision holds rather than merely
    // being asserted in a comment.
    public class DodgeCoversEveryDamagePathTests
    {
        private static CombatantState Hero(string name = "Hero", int health = 500, int mana = 100, int attack = 20, int speed = 10) =>
            new CombatantState(name, true, health, mana, attack, speed);

        private static CombatantState Foe(string name = "Foe", int health = 1000, int attack = 10, int speed = 1) =>
            new CombatantState(name, false, health, 10, attack, speed);

        private static void Give(CombatantState combatant, params ModifierEffect[] effects) =>
            combatant.ModifierEffects = new ModifierEffectSet(effects);

        // No finite DodgeRating is a TRUE guarantee any more -- see
        // CombatMath.DodgePercentFrom's own header for why (100 * R /
        // (R + 100) is strictly less than 100 for every finite R). 100_000
        // curves to 99%, the highest this curve can produce under integer
        // floor rounding, which every path test below combines with the
        // SAME fixed SeededRandom(3) this file's own Session() helper
        // always builds, on a fresh, otherwise-empty fixture -- confirmed
        // (by actually running this suite, not by hand-deriving the PRNG
        // stream) to dodge on every path exercised here. Named "Always" for
        // what it does across this file's own fixtures, not as a claim
        // about the curve.
        private static ModifierEffect AlwaysDodge => new ModifierEffect(ModifierEffectType.DodgeRating, 100_000);

        private static ResolvedSkill Skill(
            SkillEffect effect,
            string displayName = "Test Skill",
            int power = 100,
            DamageInstance[] damageInstances = null,
            StatusEffectType? appliesStatus = null,
            int statusMagnitude = 0,
            int statusDuration = 0,
            SkillTargeting targeting = SkillTargeting.SingleEnemy) =>
            new ResolvedSkill("test", displayName, "", "hero", 1, effect, targeting,
                0, 0, false, power, 0, false,
                damageInstances, SpellPresentation.None, 0,
                appliesStatus: appliesStatus, statusMagnitude: statusMagnitude, statusDuration: statusDuration);

        private static PlayerKit Kit(IReadOnlyList<ResolvedSkill> skills) =>
            new PlayerKit("hero", CharacterRole.Tank, skills, null, null);

        private static FightSession Session(CombatEncounter encounter, PlayerKit kit = null, List<EnemyKit> enemyKits = null) =>
            new FightSession(encounter, kit == null ? null : new List<PlayerKit> { kit }, enemyKits, new SeededRandom(3))
            { DamageVarianceRange = 0f };

        private static IReadOnlyList<CombatBeat> Beats(FightSession session) => session.DrainBeats();

        // ---- 1. the plain swing (FightSession.cs ResolveAttackSwing) ---------

        [Test]
        public void PlainSwing_AgainstAVeryHighDodgeChance_DealsNoDamage_AndSkipsEveryOnHitRider()
        {
            var hero = Hero(attack: 20);
            hero.CurrentHealth = 400; // room to prove lifesteal did not heal it back up
            var foe = Foe();
            Give(hero,
                new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 50, against: DamageType.Fire),
                new ModifierEffect(ModifierEffectType.LifestealPercent, 50));
            Give(foe, AlwaysDodge);
            foe.BreakShield = new BreakShield(10);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            Assert.AreEqual(1000, foe.CurrentHealth, "the plain swing itself must deal zero damage");
            Assert.AreEqual(400, hero.CurrentHealth, "no lifesteal rider may fire off a dodged swing");
            Assert.AreEqual(10, foe.BreakShield.Current, "BreakShield must not deplete on a dodge");
            Assert.AreEqual(0, session.Ledger.For("Hero").OtherDealt, "the elemental on-hit rider must not fire either");

            var beat = Beats(session).First(b => b.Actor == hero);
            Assert.IsTrue(beat.Missed, "the beat must carry the miss explicitly");
            Assert.IsTrue(beat.Messages.Any(m => m.Contains("dodges")), "the log must read as a dodge, not a 0-damage hit");
        }

        [Test]
        public void PlainSwing_Against0PercentDodge_BehavesExactlyAsBeforeThisPhase()
        {
            // The regression guard: a fixture with no dodge-granting item
            // (ModifierEffects.Empty, the class default) must land the swing
            // exactly as every pre-existing damage-path test already expects.
            var hero = Hero(attack: 20);
            var foe = Foe();

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            Assert.Less(foe.CurrentHealth, 1000, "with 0% dodge the swing must land exactly as it always has");
            Assert.IsFalse(Beats(session).First(b => b.Actor == hero).Missed);
        }

        // ---- 2. a character skill, single target (ResolveDamageSingle) -------

        [Test]
        public void CharacterSkill_SingleTarget_AgainstAVeryHighDodgeChance_DealsNoDamage_AndAppliesNoStatus()
        {
            var hero = Hero(attack: 20);
            var foe = Foe(health: 2000);
            Give(foe, AlwaysDodge);
            foe.BreakShield = new BreakShield(10);
            var skill = Skill(SkillEffect.DamageSingle, "Firebolt",
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 10, statusDuration: 3);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }), Kit(new[] { skill }));
            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(2000, foe.CurrentHealth, "a dodged skill must deal zero damage");
            Assert.AreEqual(10, foe.BreakShield.Current, "no BreakShield depletion on a dodge");
            Assert.IsFalse(foe.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "no status may be applied off a dodged skill cast");
        }

        // ---- 3. a character skill, AOE (ResolveDamageAll) --------------------

        [Test]
        public void CharacterSkill_Aoe_EachEnemyRollsItsOwnDodgeIndependently()
        {
            var hero = Hero(attack: 20);
            var dodger = Foe("Dodger", health: 1000);
            var sitter = Foe("Sitter", health: 1000);
            Give(dodger, AlwaysDodge);
            var skill = Skill(SkillEffect.DamageAll, "Firestorm", targeting: SkillTargeting.AllEnemies);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { dodger, sitter }), Kit(new[] { skill }));
            session.CastSkill(0, null);

            Assert.AreEqual(1000, dodger.CurrentHealth, "the enemy with a very high dodge chance must take zero damage from the sweep");
            Assert.Less(sitter.CurrentHealth, 1000, "a sibling with no dodge chance must still be hit by the SAME cast");
        }

        // ---- 4. a fixed-packet spell (ResolveDamageInstances) -----------------

        [Test]
        public void FixedPacketSpell_AgainstAVeryHighDodgeChance_NoPacketLands()
        {
            var hero = Hero(attack: 20);
            var foe = Foe(health: 2000);
            Give(foe, AlwaysDodge);
            var skill = Skill(SkillEffect.DamageSingle, "Prismatic Bolt", damageInstances: new[]
            {
                new DamageInstance(DamageType.Fire, 30),
                new DamageInstance(DamageType.Ice, 20),
            });

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }), Kit(new[] { skill }));
            session.CastSkill(0, foe);

            Assert.AreEqual(2000, foe.CurrentHealth, "a dodged multi-packet cast must land NEITHER packet");

            var beat = Beats(session).First(b => b.Actor == hero);
            Assert.IsTrue(beat.Missed);
            Assert.IsTrue(beat.Messages.Any(m => m.Contains("dodges")));
            Assert.IsFalse(beat.Messages.Any(m => m.Contains("Fire") || m.Contains("Ice")),
                "a dodged packet spell must not report a per-element split for damage that never landed");
        }

        [Test]
        public void FixedPacketSpell_RollsDodgeOnceForTheWholeCast_NotOncePerPacket()
        {
            // A target whose dodge chance would fail on the SECOND roll of a
            // stream but succeed on the first proves the two-packet spell
            // above consulted the roll exactly once, not twice -- if it
            // rolled per packet, a 50%-ish chance could plausibly hit one
            // packet and dodge the other, which never has an observable
            // "half landed" outcome in this combat model. This is a stronger,
            // deterministic version of that claim: replaying the SAME seed
            // through a direct RollDodge call must predict this cast's
            // result exactly once, not twice.
            var hero = Hero(attack: 20);
            var foe = Foe(health: 2000);
            Give(foe, new ModifierEffect(ModifierEffectType.DodgeRating, 50));
            var skill = Skill(SkillEffect.DamageSingle, "Prismatic Bolt", damageInstances: new[]
            {
                new DamageInstance(DamageType.Fire, 30),
                new DamageInstance(DamageType.Ice, 20),
            });

            var probeRng = new SeededRandom(3);
            bool predictedDodge = DamagePipeline.RollDodge(foe, hero, probeRng);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }), Kit(new[] { skill }));
            session.CastSkill(0, foe);

            bool actualDodge = foe.CurrentHealth == 2000;
            Assert.AreEqual(predictedDodge, actualDodge,
                "a single upfront RollDodge call (same seed) must exactly predict the whole cast's outcome");
        }

        // ---- 5. an enemy's real swing (FightSession.Enemies.cs) --------------

        [Test]
        public void EnemySwing_AgainstAPlayerWithAVeryHighDodgeChance_DealsNoDamage_AndAppliesNoStatus()
        {
            // Speed 9 against the hero's 10 -- close enough that exactly one
            // hero action is always followed by exactly one monster reply
            // (the same gap EnemyAiTests' own OneOnOne fixture relies on); a
            // wide gap would let the hero act again before the monster's
            // turn ever comes up, and this test would see no enemy beat at
            // all rather than a missed one.
            var hero = Hero("Hero", health: 500, speed: 10);
            Give(hero, AlwaysDodge);
            var monster = new CombatantState("Golem", false, 1000, 10, 30, 9);

            var source = new ResolvedEnemy("golem", "Golem", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0,
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 10, statusDuration: 3);

            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter, enemyKits: new List<EnemyKit> { new EnemyKit(source, false) });
            session.Begin();

            int before = hero.CurrentHealth;
            session.ExecuteAttack(monster); // hero swings first; the monster's own reply is what this test targets

            Assert.AreEqual(before, hero.CurrentHealth, "the enemy's swing must deal zero damage against a dodge");
            Assert.IsFalse(hero.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "an enemy's on-hit status rider must not apply off a dodged swing");

            var enemyBeat = Beats(session).First(b => b.Actor == monster);
            Assert.IsTrue(enemyBeat.Missed);
            Assert.IsTrue(enemyBeat.Messages.Any(m => m.Contains("misses")));
        }

        // ---- symmetry: enemies are never special-cased out of the check ------

        [Test]
        public void EnemyDodgeChance_IsNaturallyZero_BecauseEnemiesNeverGetModifierEffectsAssigned()
        {
            // Confirms the report's claim rather than merely asserting it: no
            // enemy CombatantState is ever handed a non-Empty ModifierEffects
            // (only FightEncounterAdapter's Character-based path assigns one),
            // so RollDodge is naturally symmetric-but-inert for the enemy side
            // without any special case in DamagePipeline itself.
            var monster = new CombatantState("Golem", false, 1000, 10, 30, 1);
            Assert.IsTrue(monster.ModifierEffects.IsEmpty, "an enemy's ModifierEffects must default to Empty");
            Assert.IsFalse(DamagePipeline.RollDodge(monster, null, new SeededRandom(1)),
                "with Empty ModifierEffects, an enemy can never roll a dodge -- the check is symmetric, the DATA is not");
        }

        // ---- RNG determinism -----------------------------------------------

        [Test]
        public void SameSeed_SameDodgeChance_ProducesTheIdenticalMissHitSequence()
        {
            var heroA = Hero(attack: 20, speed: 50);
            var foeA = Foe(speed: 1);
            Give(foeA, new ModifierEffect(ModifierEffectType.DodgeRating, 40));
            var sessionA = Session(new CombatEncounter(new[] { heroA }, new[] { foeA }));

            var heroB = Hero(attack: 20, speed: 50);
            var foeB = Foe(speed: 1);
            Give(foeB, new ModifierEffect(ModifierEffectType.DodgeRating, 40));
            var sessionB = Session(new CombatEncounter(new[] { heroB }, new[] { foeB }));

            var missSequenceA = new List<bool>();
            var missSequenceB = new List<bool>();

            for (int i = 0; i < 15; i++)
            {
                int beforeA = foeA.CurrentHealth;
                sessionA.ExecuteAttack(foeA);
                missSequenceA.Add(foeA.CurrentHealth == beforeA);

                int beforeB = foeB.CurrentHealth;
                sessionB.ExecuteAttack(foeB);
                missSequenceB.Add(foeB.CurrentHealth == beforeB);

                if (!foeA.IsAlive || !foeB.IsAlive) break;
            }

            CollectionAssert.AreEqual(missSequenceA, missSequenceB,
                "two independent sessions built the same way, same seed, same dodge% must miss/hit identically");
            Assert.IsTrue(missSequenceA.Contains(true), "fixture check: 40% over 15 swings should miss at least once");
            Assert.IsTrue(missSequenceA.Contains(false), "fixture check: 40% over 15 swings should also land at least once");
        }

        // ---- design decision 2: splash/AoE/DoT stay UNDODGEABLE ---------------
        //
        // None of the three below call DamagePipeline.AfterDefences at all --
        // see DamagePipeline's own header and ModifierEffectType.
        // DodgeRating's comment for the reasoning. These tests prove
        // the CONSEQUENCE of that design (a very-high-dodge target still
        // takes the hit) rather than merely re-asserting the comment.

        [Test]
        public void OnKillSplash_Explosive_IgnoresTheNeighboursDodgeChance()
        {
            var hero = Hero(attack: 20, speed: 10);
            var victim = Foe("Victim", health: 5, speed: 1);
            var neighbour = Foe("Neighbour", health: 100, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.OnKillSplashPercent, 30));
            Give(neighbour, AlwaysDodge);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { victim, neighbour }));
            session.ExecuteAttack(victim);

            Assert.IsFalse(victim.IsAlive, "fixture check: the swing must kill the target");
            Assert.Less(neighbour.CurrentHealth, 100,
                "kill-splash bypasses DamagePipeline entirely and so must land even against a very high dodge chance");
        }

        [Test]
        public void Shatter_IgnoresTheTargetsDodgeChance()
        {
            var lamb = Hero("Lamb", attack: 40);
            lamb.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100),
            });
            var foe = Foe(health: 1000);
            Give(foe, AlwaysDodge);

            // flatAmount 40 -- a ward authors its own pool now.
            var wardSkill = new ResolvedSkill("ward", "Fleece Ward", "", "hero", 1, SkillEffect.Ward,
                SkillTargeting.Self, 0, 0, false, 100, 40, false, null, SpellPresentation.None, 0);
            var shatterSkill = new ResolvedSkill("shatter", "Shatter", "", "hero", 1, SkillEffect.Shatter,
                SkillTargeting.Self, 0, 0, false, 100, 0, false, null, SpellPresentation.None, 0);

            var session = Session(new CombatEncounter(new[] { lamb }, new[] { foe }),
                Kit(new[] { wardSkill, shatterSkill }));

            session.CastSkill(0, null);
            session.DrainBeats();
            Assert.IsTrue(StatusEffects.IsWarded(lamb), "fixture check: there is a ward to shatter");

            session.CastSkill(1, null);

            Assert.Less(foe.CurrentHealth, 1000,
                "Shatter applies raw damage directly and must land even against a very high dodge chance");
        }

        [Test]
        public void PoisonDotTick_IgnoresTheTargetsDodgeChance()
        {
            var target = Foe(health: 1000);
            Give(target, AlwaysDodge);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 40, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(40, report.PoisonDamage,
                "a DoT tick never calls AfterDefences at all and so must apply in full regardless of dodge chance");
            Assert.AreEqual(960, target.CurrentHealth);
        }
    }
}
