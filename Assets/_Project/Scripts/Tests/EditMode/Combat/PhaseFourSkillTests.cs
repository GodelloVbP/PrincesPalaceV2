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
    // LITERALS, NOT RECOMPUTED FORMULAS (CLAUDE.md gotcha 5). Every expected
    // value below is typed out. Where a number has a derivation worth
    // knowing -- Rampage being 70% of Slam, Second Wind being a full heal at
    // a full bar -- the derivation is in the comment and the assertion is
    // still the literal.
    //
    // AND THE WARD RULES. A ward IS A SHIELD -- a pool of points, drained
    // hit by hit, on a two-turn clock.
    //
    // Every ward number below is SHIELD POINTS: Tuck In 5 a Wool (20 at
    // four, 32 once level 26's +3 lands), Bulwark 30% of the caster's own max
    // health, Prism Ward 20 plus her spell attack, Fleece Ward 50, Brace 20%
    // of the caster's max health. Wards STACK, and none of the four authors a
    // `wardTurns`, so all five stand for FightTuning.DefaultWardTurns -- one
    // of the wearer's own turns.
    // Mend is untouched, and Prism Ward's scaling term is part of the model:
    // a spell attack that passes 80 buys a bigger shield instead of an
    // immunity.
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

        // 20 PLUS HER SPELL ATTACK, in shield points, exactly as
        // PLAN_PROGRESSION_V2 section 5 authored it. A percent-of-hit model
        // breaks down here: a spell attack of 90 would mean a ward worth
        // more than 100% of any hit, i.e. one ally immune for eight mana. A
        // pool of 110 points is just a big shield.
        [Test]
        public void PrismWardIsTwentyPlusSpellAttackForEightMana()
        {
            var prismWard = Authored("prism_ward");

            Assert.AreEqual(SkillEffect.Ward, prismWard.Effect);
            Assert.AreEqual(SkillTargeting.SingleAlly, prismWard.Targeting);
            Assert.AreEqual(8, prismWard.ManaCost);
            Assert.AreEqual(ScalingAxis.Spell, prismWard.ScalingAxis, "the scaling term came back with the pool");

            // Attack 4 is Odette's level-1 figure and the fixture's scores are
            // neutral, so her spell attack is 4 flat: 24. At 90 it is 110.
            Assert.AreEqual(24, WardPoints(prismWard, Hero(attack: 4)));
            Assert.AreEqual(110, WardPoints(prismWard, Hero(attack: 90)));
        }

        // ---- Fleece Ward and Brace, the two older wards --------------------

        // BOTH AUTHOR A NUMBER: the talent is a multiplier over the row's
        // own pool, and a multiplier on nothing is nothing -- which is why
        // SkillEntryResolver refuses a sizeless Ward.
        [Test]
        public void FleeceWardIsFiftyShieldPointsForTwoWool()
        {
            var fleeceWard = Authored("fleece_ward");

            Assert.AreEqual(SkillEffect.Ward, fleeceWard.Effect);
            Assert.AreEqual(2, fleeceWard.ResourceCost);
            Assert.AreEqual(50, fleeceWard.FlatAmount);
            Assert.AreEqual(50, WardPoints(fleeceWard, Hero()));
        }

        // 20% OF THE CASTER'S OWN MAX HEALTH, like Bulwark and for the same
        // reason: a flat 60 was worth three enemy hits at level 1 and a
        // rounding error at level 40. Bjorn's authored 260 gives 52.
        [Test]
        public void BraceIsAFifthOfTheCastersOwnHealth()
        {
            var brace = Authored("placeholder_brawler_ward");

            Assert.AreEqual(SkillEffect.Ward, brace.Effect);
            Assert.AreEqual(20, brace.PercentOfCasterMaxHealth);
            Assert.AreEqual(0, brace.FlatAmount, "Brace's size is the percentage, not a flat number");

            Assert.AreEqual(52, WardPoints(brace, Hero(health: 260)), "Bjorn's own level-1 bar");
            Assert.AreEqual(100, WardPoints(brace, Hero(health: 500)), "and this file's fixture hero");
        }

        // NOT TWO TURNS RUNNING. Brace costs nothing at all -- no mana, no
        // resource -- so the cooldown is the only thing standing between it
        // and a shield every single turn, which with wards stacking is a wall
        // that grows for free.
        //
        // TWO, NOT ONE, and the number is the one place this is easy to get
        // wrong: a cooldown counts from the turn it was CAST on, so 2 means
        // "turn N, then turn N+2" and 1 means every turn. SkillEntryResolver
        // refuses 1 outright for exactly that reason. Walked here as the
        // sequence a player experiences rather than read off the field, which
        // is what makes this a pin rather than a restatement.
        [Test]
        public void BraceCannotBeCastOnTheVeryNextTurn()
        {
            var brace = Authored("placeholder_brawler_ward");
            Assert.AreEqual(2, brace.CooldownTurns);

            var bjorn = Hero(health: 260, pool: 0);
            var (session, encounter) = Fight(new[] { bjorn }, new[] { Foe() }, Kit(brace));
            session.Begin();

            Assert.IsTrue(session.CastSkill(0, bjorn), "turn 1 should be castable");

            Assert.IsFalse(session.CastSkill(0, bjorn), "turn 2 is the wait");
            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsTrue(session.CastSkill(0, bjorn), "turn 3 should be back");
        }

        // THE TALENT SCALES THE POOL. sheep_lamb_ward_1 authors 40, so Fleece
        // Ward's 50 becomes 70, and Brace's 52 on Bjorn's own bar becomes 72.
        // Taking the LARGER of talent-or-authored instead would mean a 40
        // talent does nothing at all to a 50 ward.
        [Test]
        public void TheWardTalentAddsItsPercentToWhateverTheSkillAuthored()
        {
            var shawn = Hero();
            shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.WardReductionPercent, 40),
            });

            Assert.AreEqual(70, WardPoints(Authored("fleece_ward"), shawn));

            var bjorn = Hero(health: 260);
            bjorn.Talents = shawn.Talents;
            Assert.AreEqual(72, WardPoints(Authored("placeholder_brawler_ward"), bjorn));
        }

        // WHAT ONE WARD IS WORTH, in one place, so nine assertions above
        // cannot drift into nine slightly different calls.
        private static int WardPoints(ResolvedSkill skill, CombatantState caster, int resourceSpent = 0) =>
            SkillResolution.Amount(SkillEffect.Ward, caster, caster, skill.Power, skill.FlatAmount,
                resourceSpent, ignoresDefense: false, type: DamageType.Physical, axis: skill.ScalingAxis,
                percentOfMaxHealthPerPoint: 0, percentOfCasterMaxHealth: skill.PercentOfCasterMaxHealth);

        // ---- Tuck In -------------------------------------------------------

        // 5 SHIELD POINTS PER WOOL, up to 4 Wool -- so 5, 10, 20 and never 25
        // however deep the bank is. The cap stops a hoarded twelve from being
        // three times the ward. Level 26's SkillPowerDelta of +3 takes the
        // per-Wool figure to 8, so the ceiling goes 20 -> 32.
        [TestCase(1, 5)]
        [TestCase(2, 10)]
        [TestCase(4, 20)]
        [TestCase(12, 20)]
        public void TuckInWardsFiveShieldPointsPerWoolSpentUpToFour(int banked, int expectedWard)
        {
            var tuckIn = Authored("tuck_in");
            var shawn = Hero();
            shawn.SignaturePool = Wool(banked);

            int spent = SkillResolution.ResourceToSpend(shawn.SignaturePool, tuckIn.ResourceCost,
                tuckIn.SpendsAllResource, tuckIn.ResourceSpendCap);

            Assert.AreEqual(5, tuckIn.Power);
            Assert.AreEqual(4, tuckIn.ResourceSpendCap);
            Assert.AreEqual(expectedWard, WardPoints(tuckIn, shawn, spent));
        }

        // LEVEL 26's +3, pinned as the arithmetic rather than through the
        // reward track (RewardTrackContentPinTests owns the track's own row).
        [Test]
        public void TuckInAtLevelTwentySixIsEightAWoolAndThirtyTwoAtFour()
        {
            var tuckIn = Authored("tuck_in");
            var shawn = Hero();
            shawn.SignaturePool = Wool(12);

            int spent = SkillResolution.ResourceToSpend(shawn.SignaturePool, tuckIn.ResourceCost,
                tuckIn.SpendsAllResource, tuckIn.ResourceSpendCap);

            Assert.AreEqual(4, spent);
            Assert.AreEqual(32, SkillResolution.Amount(SkillEffect.Ward, shawn, shawn, tuckIn.Power + 3,
                tuckIn.FlatAmount, spent, ignoresDefense: false, type: DamageType.Physical,
                axis: tuckIn.ScalingAxis));
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

        // ---- the ward rules, as the PLAN and the code now both state them --
        //
        // PLAN_PROGRESSION_V2.md section 5 stated the rule as "one ward per
        // character, a new ward replaces a smaller one and not a larger one,
        // absorbs until spent or expired". The code does not match that
        // exactly: wards stack instead of one-per-character, and the shield
        // total is the sum. WardTests owns the model in full; these are the
        // rules as a CAST produces them.

        [Test]
        public void AWardAbsorbsUpToItsPoolAndCarriesTheRestThrough()
        {
            var wearer = Hero(health: 1000);
            StatusEffects.ApplyWard(wearer.Statuses, 30, 1);

            Assert.AreEqual(70, StatusEffects.ConsumeWard(wearer, 100).Damage,
                "30 points off a 100 hit, and 70 reaches health");
            Assert.IsFalse(StatusEffects.IsWarded(wearer), "a hit that empties the pool takes the ward with it");
        }

        [Test]
        public void ASmallHitLeavesThePoolStandingWithLessInIt()
        {
            var wearer = Hero(health: 1000);
            StatusEffects.ApplyWard(wearer.Statuses, 30, 1);

            var first = StatusEffects.ConsumeWard(wearer, 12);
            Assert.AreEqual(0, first.Damage, "nothing reached health");
            Assert.AreEqual(12, first.Absorbed);
            Assert.IsFalse(first.Broke);
            Assert.AreEqual(18, StatusEffects.WardPoints(wearer));

            var second = StatusEffects.ConsumeWard(wearer, 25);
            Assert.AreEqual(7, second.Damage, "18 of the 25 was eaten, 7 got through");
            Assert.IsTrue(second.Broke);
            Assert.AreEqual(0, StatusEffects.WardPoints(wearer));
        }

        // A CAST WARD ADDS TO ONE ALREADY STANDING. The relic wards are the
        // case that matters and the only one a single character can reach in
        // one turn: they author the whole fight where a skill's ward stands
        // one turn, so a Tuck In pressed over a Magical Shield is 20 points on
        // top of 20 rather than an argument about which of them wins.
        [Test]
        public void ASkillWardStacksOnTopOfOneAlreadyStanding()
        {
            var tuckIn = Authored("tuck_in");
            var shawn = Hero(pool: 0);
            shawn.SignaturePool = Wool(12);

            // The Magical Shield relic's own ward, on its own whole-fight
            // duration and sourced by nobody, exactly as RaiseMagicalShield
            // puts it up.
            StatusEffects.ApplyWard(shawn.Statuses, FightTuning.MagicalShieldPoints,
                FightTuning.MagicalShieldDurationTurns);

            var (session, _) = Fight(new[] { shawn }, new[] { Foe() }, Kit(tuckIn));
            session.Begin();

            Assert.IsTrue(session.CastSkill(0, shawn));

            Assert.AreEqual(2, shawn.Statuses.Count(s => s.Type == StatusEffectType.Shielded),
                "the cast ward replaced the relic's instead of stacking with it");
            Assert.AreEqual(40, StatusEffects.WardPoints(shawn),
                "the relic's 20 and four Wool of Tuck In at 5 points each");
        }

        // THE CLOCK, WALKED THROUGH A REAL FIGHT, which is the half no
        // StatusEffects test can reach: the end-of-turn tick, the exemption
        // for the turn it went up, and the turn order that decides when either
        // happens are all FightSession's.
        //
        // CAST ON TURN N -> PRESENT AT THE START AND THE END OF N+1 -> GONE AT
        // THE START OF N+2. This fixture's foe is speed 1 against the hero's
        // 10, so "the next turn" is the hero's again and every step below is
        // one of his.
        [Test]
        public void AWardCastOnOneTurnStandsThroughTheWholeOfTheNextOne()
        {
            // A toothless foe, for the reason
            // ATwoTurnWardIsGoneAtTheStartOfTheThirdTurn gives: it acts on the
            // turn after the cast, and a 30-point swing would empty a 20-point
            // pool before the clock could be what took it.
            var shawn = Hero(pool: 0);
            shawn.SignaturePool = Wool(4);
            var (session, encounter) = Fight(new[] { shawn }, new[] { Foe(attack: 0) }, Kit(Authored("tuck_in")));
            session.Begin();

            Assert.AreEqual(1, FightTuning.DefaultWardTurns);

            // Turn N. Tuck In is a free action, so the turn is still N here --
            // the plain swing below is what ends it.
            Assert.IsTrue(session.CastSkill(0, shawn));
            Assert.AreEqual(20, StatusEffects.WardPoints(shawn));

            session.ExecuteAttack(encounter.Enemies[0]);

            // Turn N+1, both ends of it.
            Assert.AreEqual(20, StatusEffects.WardPoints(shawn), "gone at the start of N+1");
            session.ExecuteAttack(encounter.Enemies[0]);

            // Turn N+2.
            Assert.IsFalse(StatusEffects.IsWarded(shawn), "still standing at the start of N+2");
        }

        // AND SHATTER CAN REACH IT, which is what the end-of-turn clock was
        // for: the ward goes up on one turn and is detonated on the next,
        // through two separate turns, with no free-action node in play.
        [Test]
        public void ShatterReachesAWardCastTheTurnBefore()
        {
            // Twelve Wool: Tuck In takes four (its own cap) and Shatter's
            // authored three has to still be payable on the turn after.
            var shawn = Hero(pool: 0, attack: 40);
            shawn.SignaturePool = Wool(12);
            shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100),
            });

            var foe = Foe(health: 100000);
            var (session, _) = Fight(new[] { shawn }, new[] { foe },
                Kit(Authored("tuck_in"), Authored("shatter")));
            session.Begin();

            Assert.IsTrue(session.CastSkill(0, shawn), "turn N: the ward");
            session.ExecuteAttack(foe);

            Assert.IsTrue(session.CastSkill(1, null), "turn N+1: the detonation was refused");
            Assert.Less(foe.CurrentHealth, foe.MaxHealth);
            Assert.IsFalse(StatusEffects.IsWarded(shawn), "the ward survived its own detonation");
        }

        // A TWO-TURN WARD IS GONE AT THE START OF N+3, by the same rule one
        // more time.
        [Test]
        public void ATwoTurnWardIsGoneAtTheStartOfTheThirdTurn()
        {
            // NO SHIPPED SKILL AUTHORS A TWO-TURN WARD -- all five take the
            // house default -- so this one is built here. Through a CAST
            // rather than by hand, because the turn it was raised on not
            // counting is FightSession's half of the rule and a ward dropped
            // straight into the list would miss it.
            var twoTurns = new ResolvedSkill("two_turn_ward", "Long Ward", "", "hero", 1,
                SkillEffect.Ward, SkillEntryResolver.DefaultTargetingFor(SkillEffect.Ward),
                0, 0, false, 0, 9999, false, null, SpellPresentation.None, 0,
                wardTurns: 2);

            // A NINE-THOUSAND-POINT POOL AND A TOOTHLESS FOE, so the only
            // thing that can take this ward away is the clock. The monster in
            // this fixture acts on the turn after the cast and a 30-point
            // swing empties an ordinary pool outright, which is a true fact
            // about wards and a useless one to be measuring here.
            var wearer = Hero();
            var (session, encounter) = Fight(new[] { wearer }, new[] { Foe(attack: 0) }, Kit(twoTurns));
            session.Begin();

            // Turn N. The cast ends it, and the ward is exempt from that end.
            Assert.IsTrue(session.CastSkill(0, wearer));
            Assert.IsTrue(StatusEffects.IsWarded(wearer), "gone at the start of N+1");

            session.ExecuteAttack(encounter.Enemies[0]);   // N+1 ends: 2 -> 1
            Assert.IsTrue(StatusEffects.IsWarded(wearer), "gone at the start of N+2");

            session.ExecuteAttack(encounter.Enemies[0]);   // N+2 ends: 1 -> 0
            Assert.IsFalse(StatusEffects.IsWarded(wearer), "still standing at the start of N+3");
        }

        // AND EVERY SHIPPED WARD SKILL TAKES THAT DEFAULT -- none of the four
        // authors a `wardTurns`, which is the fact the sentence above is only
        // useful in the presence of.
        [TestCase("fleece_ward")]
        [TestCase("tuck_in")]
        [TestCase("placeholder_brawler_ward")]
        [TestCase("prism_ward")]
        public void EveryWardSkillStandsForTheHouseDefault(string id)
        {
            Assert.AreEqual(FightTuning.DefaultWardTurns, Authored(id).WardTurns);
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
