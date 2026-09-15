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
    // THE SIX SKILLS PROGRESSION V2 PHASE 4 ADDS -- Rampage, Bulwark, Second
    // Wind, Mend, Prism Ward and Tuck In -- pinned twice over: once against
    // the numbers actually authored in skills.json, and once against what
    // the fight does with them.
    //
    // LITERALS, NOT RECOMPUTED FORMULAS (CODE_STANDARDS §8). Every expected
    // value below is typed out. Where a number has a derivation worth
    // knowing -- Rampage being 70% of Slam, Second Wind being a full heal at
    // a full bar -- the derivation is in the comment and the assertion is
    // still the literal.
    //
    // AND THE WARD RULES, pinned as the CODE behaves rather than as the plan
    // describes them, because the two differ and the difference is not a bug
    // to fix in this phase. See WardsAre* below.
    public class PhaseFourSkillTests
    {
        // ---- the authored numbers, read from skills.json -------------------

        private static Dictionary<string, ResolvedSkill> Authored()
        {
            var raw = ContentDataFiles.ParseFile<RawSkillFile>(ContentDataFiles.DataPath("skills.json")).skills;

            // The zero-start owner set is what lets Bjorn's free skills
            // resolve at all -- see SkillEntryResolver's own carve-out. Named
            // literally here rather than resolved out of pools.json, because
            // this file is about the six skills and a pool resolution in
            // front of them is one more thing that can fail for an unrelated
            // reason.
            bool ok = SkillEntryResolver.TryResolveAll(raw, null, new[] { "bear" }, out var resolved, out var errors);
            Assert.IsTrue(ok, "skills.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));

            return resolved.ToDictionary(s => s.Id, s => s);
        }

        private static ResolvedSkill Authored(string id)
        {
            var all = Authored();
            Assert.IsTrue(all.ContainsKey(id), $"skills.json no longer authors '{id}'");
            return all[id];
        }

        // ---- fixtures ------------------------------------------------------

        private static CombatantState Hero(string name = "Hero", int health = 500, int pool = 100,
            int attack = 10, int speed = 10) =>
            new CombatantState(name, true, health, pool, attack, speed);

        // Speed 1 against 10, the same reason FightTalentTests gives: a cast
        // is a full turn, and a foe fast enough to reply would consume the
        // ward or the taunt between the two halves of every setup here.
        private static CombatantState Foe(string name = "Foe", int health = 1000, int attack = 30, int speed = 1) =>
            new CombatantState(name, false, health, 10, attack, speed);

        private static PlayerKit Kit(params ResolvedSkill[] skills) =>
            new PlayerKit("hero", CharacterRole.Tank, skills, null, null);

        private static (FightSession session, CombatEncounter encounter) Fight(
            IReadOnlyList<CombatantState> party, IReadOnlyList<CombatantState> foes, params PlayerKit[] kits)
        {
            var encounter = new CombatEncounter(party, foes);
            var session = new FightSession(encounter, kits.ToList(), null, new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
            };
            return (session, encounter);
        }

        // BOTH BUFFERS. AppendMessage puts a line on the beat being
        // recorded, else on the LAST committed beat, else in the immediate
        // list -- so which one a refusal lands in depends on whether
        // anything has been cast yet this fight, which is not a fact a test
        // about Tuck In should have to know.
        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        private static ResourcePool Wool(int banked)
        {
            var pool = new ResourcePool("wool", "Wool", 20, 0, 0, 0);
            pool.Gain(banked);
            return pool;
        }

        // ---- Rampage -------------------------------------------------------

        // 19 PER TARGET for a level-1 Bjorn: Attack 10, no gear scaling, plus
        // the authored flatAmount of 9. Slam is the same arithmetic with 17,
        // so 27 -- and 19 is 70% of 27 to the nearest point (18.9), which is
        // the relation §5 states. Both literals, so a change to either
        // skill's flatAmount has to be typed here on purpose.
        [Test]
        public void RampageDealsNineteenPerTargetForALevelOneBjorn()
        {
            var rampage = Authored("rampage");
            var bjorn = Hero(attack: 10);
            var foe = Foe();

            int perTarget = SkillResolution.Amount(rampage.Effect, bjorn, foe, rampage.Power, rampage.FlatAmount,
                resourceSpent: 0, ignoresDefense: false, type: DamageType.Physical, axis: rampage.ScalingAxis);

            Assert.AreEqual(SkillEffect.DamageAll, rampage.Effect);
            Assert.AreEqual(19, perTarget);
        }

        [Test]
        public void SlamStillDealsTwentySevenForALevelOneBjorn()
        {
            var slam = Authored("placeholder_brawler_slam");
            var bjorn = Hero(attack: 10);

            int hit = SkillResolution.Amount(slam.Effect, bjorn, Foe(), slam.Power, slam.FlatAmount,
                resourceSpent: 0, ignoresDefense: false, type: DamageType.Physical, axis: slam.ScalingAxis);

            Assert.AreEqual(27, hit, "Rampage's 19 is authored as 70% of this, so the two move together");
        }

        // TIERS IDENTICAL TO SLAM'S, asserted field by field rather than by
        // comparing the two arrays wholesale: a wholesale comparison passes
        // if both are empty, which is exactly the failure (an authored
        // poolTiers block dropped) this is here to catch.
        [Test]
        public void RampageCarriesSlamsFuryTiers()
        {
            var rampage = Authored("rampage");
            var slam = Authored("placeholder_brawler_slam");

            Assert.AreEqual(2, slam.PoolTiers.Length, "Slam's own tiers changed -- repin both");
            Assert.AreEqual(2, rampage.PoolTiers.Length);

            Assert.AreEqual(0.5f, rampage.PoolTiers[0].Spend, 0.0001f);
            Assert.AreEqual(2f, rampage.PoolTiers[0].DamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, rampage.PoolTiers[1].Spend, 0.0001f);
            Assert.AreEqual(4f, rampage.PoolTiers[1].DamageMultiplier, 0.0001f);
        }

        // The approach and the three stance phases are Slam's, which is what
        // "reuse Slam's" means in art terms: no new drawing is commissioned
        // for Rampage, it wears the hammer poses.
        [Test]
        public void RampageWearsSlamsApproachAndStances()
        {
            var rampage = Authored("rampage");
            var slam = Authored("placeholder_brawler_slam");

            Assert.AreEqual(slam.Approach, rampage.Approach);
            Assert.AreEqual(slam.ApproachStance, rampage.ApproachStance);
            Assert.AreEqual(slam.WindupStance, rampage.WindupStance);
            Assert.AreEqual(slam.Stance, rampage.Stance);
            Assert.IsNotEmpty(rampage.Stance, "Slam's own stance went blank, so this asserts nothing");
        }

        // ---- Bulwark -------------------------------------------------------

        [Test]
        public void BulwarkCostsFiftyFromThePrimaryPoolAndWardsForThirty()
        {
            var bulwark = Authored("bulwark");

            Assert.AreEqual(SkillEffect.Ward, bulwark.Effect);
            Assert.AreEqual(SkillTargeting.SingleAlly, bulwark.Targeting, "it is aimed through the ally picker");
            Assert.AreEqual(50, bulwark.ManaCost);
            Assert.AreEqual(0, bulwark.ResourceCost, "Bjorn has no signature resource to spend");

            var bjorn = Hero(attack: 10, pool: 100);
            int ward = SkillResolution.Amount(SkillEffect.Ward, bjorn, bjorn, bulwark.Power, bulwark.FlatAmount,
                resourceSpent: 0, ignoresDefense: false, type: DamageType.Physical, axis: bulwark.ScalingAxis);

            Assert.AreEqual(30, ward);
        }

        [Test]
        public void BulwarkIsRefusedUnderFiftyFury()
        {
            var bulwark = Authored("bulwark");
            var bjorn = Hero(pool: 100);
            var (session, _) = Fight(new[] { bjorn }, new[] { Foe() }, Kit(bulwark));

            bjorn.PrimaryPool.TrySpend(51); // 49 left
            Assert.AreEqual(49, bjorn.PrimaryPool.Current);

            Assert.IsFalse(session.CastSkill(0, bjorn), "49 Fury paid for a 50-Fury ward");
            Assert.AreEqual(49, bjorn.PrimaryPool.Current, "a refused cast must spend nothing");

            bjorn.PrimaryPool.Gain(1); // 50
            Assert.IsTrue(session.CastSkill(0, bjorn), "50 Fury is exactly the price and must pay it");
            Assert.AreEqual(0, bjorn.PrimaryPool.Current);
            Assert.IsTrue(StatusEffects.IsWarded(bjorn));
        }

        // ---- Second Wind ---------------------------------------------------

        // 1% OF MAX HEALTH PER FURY, and the authored promise is "at 100, a
        // full heal". 500 max health, 100 Fury spent: 500 * 1 * 100 / 100 =
        // 500. At 40 Fury it is 200. Both literals.
        [TestCase(100, 500)]
        [TestCase(40, 200)]
        [TestCase(25, 125)]
        public void SecondWindHealsOnePercentOfMaxHealthPerFurySpent(int furySpent, int expected)
        {
            var secondWind = Authored("second_wind");
            var bjorn = Hero(health: 500, pool: 100);

            int healed = SkillResolution.Amount(secondWind.Effect, bjorn, bjorn, secondWind.Power,
                secondWind.FlatAmount, furySpent, ignoresDefense: false, type: DamageType.Physical,
                axis: secondWind.ScalingAxis, percentOfMaxHealthPerPoint: secondWind.PercentOfMaxHealthPerPoint);

            Assert.AreEqual(1, secondWind.PercentOfMaxHealthPerPoint);
            Assert.AreEqual(expected, healed);
        }

        [Test]
        public void SecondWindIsRefusedUnderTwentyFiveFuryAndOtherwiseSpendsItAll()
        {
            var secondWind = Authored("second_wind");
            var bjorn = Hero(health: 500, pool: 100);
            var (session, _) = Fight(new[] { bjorn }, new[] { Foe() }, Kit(secondWind));

            bjorn.CurrentHealth = 100;
            bjorn.PrimaryPool.TrySpend(76); // 24 left
            Assert.AreEqual(24, bjorn.PrimaryPool.Current);

            Assert.IsFalse(session.CastSkill(0, bjorn), "24 Fury fired a 25-Fury minimum");
            Assert.AreEqual(100, bjorn.CurrentHealth, "a refused cast must heal nothing");

            bjorn.PrimaryPool.Gain(36); // 60
            Assert.IsTrue(session.CastSkill(0, bjorn));

            // 60 Fury on a 500 bar is 300, onto 100, capped at 500.
            Assert.AreEqual(400, bjorn.CurrentHealth);
            Assert.AreEqual(0, bjorn.PrimaryPool.Current, "spendsAllPrimary left Fury behind");
        }

        // ---- Mend ----------------------------------------------------------

        // 20 PLUS HER SPELL ATTACK. Attack 4 and no gear scaling is Odette's
        // level-1 figure, so 24. The ability scores are neutral in this
        // fixture, which is what keeps CombatMath.WisdomHealBonus at 0 and
        // this literal readable.
        [Test]
        public void MendHealsTwentyPlusSpellAttack()
        {
            var mend = Authored("mend");
            var odette = Hero(attack: 4);

            Assert.AreEqual(SkillEffect.HealSingle, mend.Effect);
            Assert.AreEqual(SkillTargeting.SingleAlly, mend.Targeting);
            Assert.AreEqual(6, mend.ManaCost);
            Assert.AreEqual(ScalingAxis.Spell, mend.ScalingAxis);

            int healed = SkillResolution.Amount(mend.Effect, odette, odette, mend.Power, mend.FlatAmount,
                resourceSpent: 0, ignoresDefense: false, type: DamageType.Physical, axis: mend.ScalingAxis);

            Assert.AreEqual(24, healed);
        }

        [Test]
        public void MendLandsOnTheChosenAllyAndNotOnTheCaster()
        {
            var mend = Authored("mend");
            var odette = Hero("Odette", health: 200, pool: 30, attack: 4);
            var shawn = Hero("Shawn", health: 400, pool: 0, attack: 7, speed: 1);
            var (session, _) = Fight(new[] { odette, shawn }, new[] { Foe() }, Kit(mend), Kit());

            odette.CurrentHealth = 100;
            shawn.CurrentHealth = 100;

            Assert.IsTrue(session.CastSkill(0, shawn));

            Assert.AreEqual(124, shawn.CurrentHealth, "the chosen ally took the 24");
            Assert.AreEqual(100, odette.CurrentHealth, "the caster healed herself instead of the ally she aimed at");
            Assert.AreEqual(24, odette.PrimaryPool.Current, "6 mana charged from 30");
        }

        // The ally picker's own predicate has to accept a heal, or the menu
        // would light no plates and CastSkill would refuse every target.
        [Test]
        public void EveryLivingAllyIncludingTheCasterIsAMendTarget()
        {
            var mend = Authored("mend");
            var odette = Hero("Odette", pool: 30, attack: 4);
            var shawn = Hero("Shawn", speed: 1);
            var (session, _) = Fight(new[] { odette, shawn }, new[] { Foe() }, Kit(mend), Kit());

            var eligible = session.EligibleAllies(odette, mend);

            CollectionAssert.Contains(eligible, odette, "a heal a caster cannot aim at herself is not a heal");
            CollectionAssert.Contains(eligible, shawn);
        }

        // ---- Prism Ward ----------------------------------------------------

        [Test]
        public void PrismWardIsTwentyPlusSpellAttackForEightMana()
        {
            var prismWard = Authored("prism_ward");
            var odette = Hero(attack: 4);

            Assert.AreEqual(SkillEffect.Ward, prismWard.Effect);
            Assert.AreEqual(SkillTargeting.SingleAlly, prismWard.Targeting);
            Assert.AreEqual(8, prismWard.ManaCost);

            int ward = SkillResolution.Amount(SkillEffect.Ward, odette, odette, prismWard.Power,
                prismWard.FlatAmount, resourceSpent: 0, ignoresDefense: false, type: DamageType.Physical,
                axis: prismWard.ScalingAxis);

            Assert.AreEqual(24, ward);
        }

        // ---- Tuck In -------------------------------------------------------

        // 2 PER WOOL, up to 4 Wool -- so 2, 4, 6, 8 and never 10 however deep
        // the bank is. The cap is what stops a hoarded twelve from being
        // three times the ward.
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(4, 8)]
        [TestCase(12, 8)]
        public void TuckInWardsTwoPerWoolSpentUpToFour(int banked, int expectedWard)
        {
            var tuckIn = Authored("tuck_in");
            var shawn = Hero();
            shawn.SignaturePool = Wool(banked);

            int spent = SkillResolution.ResourceToSpend(shawn.SignaturePool, tuckIn.ResourceCost,
                tuckIn.SpendsAllResource, tuckIn.ResourceSpendCap);
            int ward = SkillResolution.Amount(SkillEffect.Ward, shawn, shawn, tuckIn.Power, tuckIn.FlatAmount,
                spent, ignoresDefense: false, type: DamageType.Physical, axis: tuckIn.ScalingAxis);

            Assert.AreEqual(2, tuckIn.Power);
            Assert.AreEqual(4, tuckIn.ResourceSpendCap);
            Assert.AreEqual(expectedWard, ward);
        }

        [Test]
        public void TuckInIsRefusedAtZeroWoolAndCostsNoMana()
        {
            var tuckIn = Authored("tuck_in");
            Assert.AreEqual(0, tuckIn.ManaCost);

            var shawn = Hero(pool: 0);
            shawn.SignaturePool = Wool(0);
            var (session, _) = Fight(new[] { shawn }, new[] { Foe() }, Kit(tuckIn));

            Assert.IsFalse(session.CastSkill(0, shawn), "an empty bank paid for a ward");
            Assert.IsFalse(StatusEffects.IsWarded(shawn));
        }

        // DOES NOT END THE TURN, and does it exactly once. The second press
        // is refused rather than silently ending the turn, and refusing it
        // spends no Wool -- the same "nothing is paid on the way to a
        // refusal" discipline every other gate in CastSkill keeps.
        [Test]
        public void TuckInIsAFreeActionOnceATurn()
        {
            var tuckIn = Authored("tuck_in");
            var shawn = Hero(pool: 0);
            shawn.SignaturePool = Wool(12);
            var (session, _) = Fight(new[] { shawn }, new[] { Foe() }, Kit(tuckIn));

            Assert.IsTrue(session.CastSkill(0, shawn));
            Assert.AreSame(shawn, session.Current, "a free action ended the turn");
            Assert.AreEqual(8, shawn.SignaturePool.Current, "4 Wool of 12 spent");

            Assert.IsFalse(session.CastSkill(0, shawn), "a second free action in one turn was allowed");
            Assert.AreEqual(8, shawn.SignaturePool.Current, "the refused second cast spent Wool anyway");
            StringAssert.Contains("already taken a free action", string.Join(" | ", Messages(session)));
        }

        // THE LOCK IS PER TURN, not per fight. Proved by taking an ordinary
        // action after the free one and coming back round.
        [Test]
        public void TheFreeActionLockClearsWhenTheTurnEnds()
        {
            var tuckIn = Authored("tuck_in");
            var shear = Authored("shear");
            var shawn = Hero(pool: 0, speed: 20);
            shawn.SignaturePool = Wool(20);
            var foe = Foe(health: 100000);
            var (session, _) = Fight(new[] { shawn }, new[] { foe }, Kit(tuckIn, shear));

            Assert.IsTrue(session.CastSkill(0, shawn), "the turn's free action");
            Assert.IsFalse(session.CastSkill(0, shawn), "and it is spent");

            // An ordinary cast ends the turn; speed 20 against 1 brings it
            // straight back round to him.
            Assert.IsTrue(session.CastSkill(1, foe));
            Assert.AreSame(shawn, session.Current, "the fixture's speeds no longer return the turn to Shawn");

            Assert.IsTrue(session.CastSkill(0, shawn), "the new turn did not get its own free action");
        }

        // ---- the ward rules, as the CODE behaves ---------------------------
        //
        // PLAN_PROGRESSION_V2.md §5 states the ward rule as "one ward per
        // character, a new ward replaces a smaller one and not a larger one,
        // absorbs until spent or expired". The first two halves are what
        // StatusEffects does. THE THIRD IS NOT, and this phase pins the code
        // rather than changing it:
        //
        //  - A ward is a PERCENTAGE REDUCTION of ONE incoming hit
        //    (StatusEffects.ConsumeWard: `damage - damage * Magnitude / 100`),
        //    not a pool of points that absorbs until exhausted. A 30 ward
        //    takes 30% off the next blow whatever its size, and is gone.
        //  - It has no meaningful DURATION either: every ward is applied with
        //    FightSession.WardDurationTurns, which is 999. Nothing expires a
        //    ward; the next hit spends it.
        //
        // So "two of the wearer's turns" (Bulwark, Prism Ward) and "for the
        // enemy phase" (Tuck In) describe a timer that does not exist, and
        // every authored ward number in skills.json is a PERCENT rather than
        // an absorb pool. Changing that would be replacing the ward model,
        // which is a phase of its own.

        [Test]
        public void AWardIsAPercentageOfOneHitAndIsSpentByIt()
        {
            var wearer = Hero(health: 1000);
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 30, 999);

            Assert.AreEqual(70, StatusEffects.ConsumeShieldedReduction(wearer, 100),
                "30% off a 100 hit");
            Assert.IsFalse(StatusEffects.IsWarded(wearer), "the ward survived the hit it was meant to be spent on");
        }

        [Test]
        public void AWardScalesWithTheHitRatherThanAbsorbingAFixedPool()
        {
            var wearer = Hero(health: 1000);
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 30, 999);

            // An absorb pool of 30 would leave 970 here. A percentage leaves
            // 700. The difference is the whole of the plan-versus-code gap.
            Assert.AreEqual(700, StatusEffects.ConsumeShieldedReduction(wearer, 1000));
        }

        [Test]
        public void OneWardPerCharacterAndTheLargerWins()
        {
            var wearer = Hero();
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 30, 999);
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 12, 999);

            Assert.AreEqual(1, wearer.Statuses.Count(s => s.Type == StatusEffectType.Shielded),
                "a second ward stacked rather than refreshing");
            Assert.AreEqual(30, wearer.Statuses.First(s => s.Type == StatusEffectType.Shielded).Magnitude,
                "a smaller ward overwrote a larger one");

            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 44, 999);
            Assert.AreEqual(44, wearer.Statuses.First(s => s.Type == StatusEffectType.Shielded).Magnitude,
                "a larger ward failed to replace a smaller one");
        }

        // Every ward the game applies carries the same 999-turn duration, so
        // no ward in the game has ever expired on a clock. Read by
        // reflection off the private constant rather than retyped, the same
        // way StatusHudCoverageTests already reaches it.
        [Test]
        public void NoWardExpiresOnATimer()
        {
            var field = typeof(FightSession).GetField("WardDurationTurns",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(field, "FightSession.WardDurationTurns has moved or been renamed");
            Assert.AreEqual(999, (int)field.GetRawConstantValue(),
                "a ward duration short enough to expire would be a change to the ward model, not to a skill");
        }

        // ---- the talent and the authored flag are one rule -----------------

        // The Fragile Lamb's Fleece Ward T3 made warding stop costing the
        // turn long before an authored freeAction existed. It still works,
        // through the same predicate, which is what "the talent keeps working
        // by setting the same flag" means -- one rule, two sources, not a
        // talent special case beside a field.
        [Test]
        public void TheFleeceWardTalentStillBuysAFreeAction()
        {
            var fleeceWard = Authored("fleece_ward");
            Assert.IsFalse(fleeceWard.FreeAction, "fleece_ward authors no freeAction -- the talent is its only source");

            var shawn = Hero(pool: 0);
            shawn.SignaturePool = Wool(10);
            shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.WardReductionPercent, 25),
                new TalentEffect(TalentEffectType.WardIsFreeAction, 1),
            });

            var (session, _) = Fight(new[] { shawn }, new[] { Foe() }, Kit(fleeceWard));

            Assert.IsTrue(session.CastSkill(0, shawn));
            Assert.AreSame(shawn, session.Current, "the talent stopped making the ward a free action");
            Assert.IsFalse(session.CastSkill(0, shawn), "the talent's free action is now once a turn too");
        }
    }
}
