using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Balance redesign Phase 5D -- balance validation. This IS the balance
    // authority from here on (§T's own words): §P's five canonical player
    // profiles ("Canonical profiles and derived targets"), hand-built as
    // SYNTHETIC CombatantState fixtures carrying §P's exact stat totals
    // (never assembled by equipping real items -- §P is explicit that the
    // fixtures are synthetic precisely so this suite does not depend on
    // which set pieces produced them), run through the REAL production
    // combat math (CombatMath.ComputeAttackDamage, DamagePipeline.
    // AfterDefences, DifficultyCurve, StatBlock.ScaledForElite via
    // FightEncounterAdapter) against REAL resolved boss/enemy content.
    //
    // PlayMode rather than EditMode: the "read from resolved enemy content"
    // half of the brief needs ContentDatabase.Enemies and
    // FightEncounterAdapter.Build, which read Resources -- see
    // DepthReachesTheEnemiesTests for the same pattern this file follows.
    //
    // *** ROOT-CAUSE FIX LANDED (2026-08-26) ***
    //
    // §P's own derived-combat-chain table was hand-derived WITHOUT
    // CombatMath.DamageScale. Proof: feed §P's own "WP x M" figure straight
    // into the canonical mitigation equation (damage x 100 / (100 + D),
    // integer division) with NO further multiplier, and all seven "after
    // DEF" figures in §P's table reproduce EXACTLY -- e.g. Noob@F1 raw 10
    // vs hollow_choir's MDEF 25: 10 x 100 / 125 = 8, §P's own pinned 8.
    // D1 says this explicitly: "DamageScale x5 ... are deleted."
    //
    // CombatMath.ComputeAttackDamage and ComputeSkillDamage -- the two
    // entry points any real basic attack or spell-tier cast calls
    // (FightSession.cs:275, FightSession.Skills.cs:72/88,
    // FightSession.Enemies.cs:185/472) -- had still been routing through
    // Scale() and its x5 DamageScale for two prior implementation passes
    // (one called removing it out of scope, the other found it already
    // live and built around it). A THIRD real entry point was found during
    // this fix and carried the identical bug: SkillResolution.Damage, which
    // every wool/authored-skill cast without fixed damageInstances resolves
    // through (FightSession.Skills.cs's ResolveDamageSingle/ResolveDamageAll)
    // -- also fed DamagePipeline.AfterDefences an x5-inflated figure. All
    // three now return their raw WP x M (or, for SkillResolution, scaledAttack
    // + flatAmount + power x resourceSpent) figure floored at 1, never
    // multiplied. CombatMath.Scale()/DamageScale itself is NOT deleted --
    // it still serves callers that were never part of the mitigated-combat
    // path: FightSession.Talents.cs's two unmitigated splash sites (Shatter,
    // Trample's kill splash), which are deliberately left on the old x5
    // scale (see CombatMath.Scale's own header for why that is worth a
    // second look before the next playtest). FightHudModel's POWER and
    // PreviewBasicSpellPower readouts now call ComputeAttackDamage/
    // ComputeSkillDamage directly instead of duplicating Scale(ScaledAttack
    // (...)) by hand, so they can never drift from what a real swing deals
    // again.
    //
    // Below, every one of the seven canonical rows is pinned against what
    // CombatMath returns POST-FIX (per CLAUDE.md gotcha 5 -- pin the real
    // output, never a recomputed formula). The after-DEF column now
    // reproduces §P's own figures exactly in every row (see the proof
    // above); TTK/bossDmg/hitsToDie are close to but not identical to §P's
    // own hand-derived figures, because AssertFight's Eff/action always
    // applies the x1.5 rotation multiplier uniformly, while §P's own table
    // explicitly gives the Noob row "no rotation" (Eff/action = the raw
    // after-DEF figure, not x1.5) to represent misplaying the rotation --
    // that nuance was never mechanized here and still is not; see each
    // test's own comment for the resulting gap.
    public class BalanceSheetTests
    {
        private const float NoVariance = 0f;

        // Shawn base speed 10, DEX7 => -1 SPD -- the plan's own worked
        // Shawn example (D2's "at a glance" table). None of §P's five gear
        // columns (HP/PDEF/MDEF only) adds Speed, so every profile shares
        // this one figure.
        private const int PlayerSpeed = 9;

        // ---- §P's five canonical player fixtures --------------------------
        //
        // Hand-built, not resolved through gear -- §P's own words, "synthetic
        // StatBlocks with these exact aggregates, so tests don't depend on
        // which set pieces produced them". Every WP is WeaponPower.Compute
        // (the real production formula) against weapons.json's real staff
        // endpoints (T0=9 T1=12 T2=16 T3=22 T4=29 T5=40), and
        // CanonicalWeaponPowers_MatchSectionPsOwnColumnExactly below cross-
        // checks every one against §P's own stated WP column before anything
        // downstream uses it.
        private static CombatantState Player(string name, int weaponPower, int wisdomScore, ScalingGrade grade,
            int maxHealth, int physicalDefense, int magicalDefense)
        {
            return new CombatantState(name, isPlayerSide: true, maxHealth, maxMana: 100,
                attack: weaponPower, speed: PlayerSpeed)
            {
                // Verdant staff: primary Wisdom. Shawn's attack type is
                // Nature (§P), so every fight below sends DamageType.Nature
                // at the boss and reads the boss's MagicalDefense.
                WeaponScaling = ScalingProfile.None.With(AbilityScore.Wisdom, grade),
                AbilityScores = new AbilityScoreBlock(10, 10, 10, wisdomScore, 10, 10),
                PhysicalDefense = physicalDefense,
                MagicalDefense = magicalDefense,
            };
        }

        // Noob@F1: staff T0 +0 -> WP 9, WIS 14 (d4, grade C: 1.12), gear
        // 30/22/0 on starting kit -> total 350/34/8.
        private static CombatantState NoobF1() =>
            Player("Noob@F1", WeaponPower.Compute(9, 0), wisdomScore: 14, ScalingGrade.C,
                maxHealth: 350, physicalDefense: 34, magicalDefense: 8);

        // Competent@F1: staff T1 +0 -> WP 12, WIS 15 (d5, grade C: 1.15),
        // total 350/37/13.
        private static CombatantState CompetentF1() =>
            Player("Competent@F1", WeaponPower.Compute(12, 0), wisdomScore: 15, ScalingGrade.C,
                maxHealth: 350, physicalDefense: 37, magicalDefense: 13);

        // Low-talent@F2: staff T1 +0 -> WP 12, WIS 14 (d4, grade C: 1.12),
        // total 370/42/13.
        private static CombatantState LowTalentF2() =>
            Player("LowTalent@F2", WeaponPower.Compute(12, 0), wisdomScore: 14, ScalingGrade.C,
                maxHealth: 370, physicalDefense: 42, magicalDefense: 13);

        // Competent@F3: staff T3 +2 -> WP 29, WIS 17 (d7, grade B: 1.35),
        // total 500/59/34.
        private static CombatantState CompetentF3() =>
            Player("Competent@F3", WeaponPower.Compute(22, 2), wisdomScore: 17, ScalingGrade.B,
                maxHealth: 500, physicalDefense: 59, magicalDefense: 34);

        // Competent@F5: staff T5 +3 -> WP 58, WIS 20 (d10, grade A: 1.70),
        // total 620/85/50.
        private static CombatantState CompetentF5() =>
            Player("Competent@F5", WeaponPower.Compute(40, 3), wisdomScore: 20, ScalingGrade.A,
                maxHealth: 620, physicalDefense: 85, magicalDefense: 50);

        [Test]
        public void CanonicalWeaponPowers_MatchSectionPsOwnColumnExactly()
        {
            // If any of these four drift, weapons.json's staff endpoints
            // moved and every literal below needs re-deriving from scratch --
            // not just this test.
            Assert.AreEqual(9, WeaponPower.Compute(9, 0), "Noob@F1: staff T0 +0");
            Assert.AreEqual(12, WeaponPower.Compute(12, 0), "Competent@F1 / LowTalent@F2: staff T1 +0");
            Assert.AreEqual(29, WeaponPower.Compute(22, 2), "Competent@F3: staff T3 +2");
            Assert.AreEqual(58, WeaponPower.Compute(40, 3), "Competent@F5: staff T5 +3");
        }

        // ---- real resolved enemy content -----------------------------------

        private static (CombatantState state, ElementalAffinity affinity, DamageType attackType) RealEnemy(
            string enemyId, int step, bool isElite = false)
        {
            var definition = ContentDatabase.Enemies.FirstOrDefault(e => e.id == enemyId);
            Assert.IsNotNull(definition, $"'{enemyId}' is not in the resolved enemy content -- " +
                "run tools/run_tests_parallel.ps1 -BuildContent so enemies.json's current numbers reach Resources/Content/Enemies/");

            var partyId = ContentDatabase.Characters.First().id;
            var built = FightEncounterAdapter.Build(
                new List<string> { partyId }, new List<string> { enemyId },
                new SeededRandom(11), isElite: isElite, depthStep: step);

            Assert.IsNotNull(built, "no fight could be built from the current content");
            var state = built.Session.Encounter.Enemies.FirstOrDefault();
            Assert.IsNotNull(state, "the built fight has no enemies");
            return (state, definition.Affinity, definition.attackType);
        }

        // hollow_choir is fully authored in enemies.json (HP100/SPD14/ATK24/
        // PDEF5/MDEF25, isBoss, minFloor 1, attackType Arcane -- exactly the
        // D5 numbers this file's own brief hands in) but is BENCHED
        // (active: false, no spritePath yet -- the same convention every
        // other art-less enemy in the file follows), so ContentBuilder.
        // BuildEnemies writes it no Resources asset at all
        // ("if (!enemy.Active) continue;") and ContentDatabase.Enemies
        // cannot see it. Resolved here straight from its own authored line
        // via the real EnemyEntryResolver instead, then depth-scaled by
        // hand with the SAME two DifficultyCurve calls
        // FightEncounterAdapter.ToCombatant itself makes on every other
        // enemy (health/attack scaled, both defenses left at their
        // authored, unscaled value per D6) -- a transcription of that
        // private method's current body, not a guess at it.
        private static (CombatantState state, ElementalAffinity affinity, DamageType attackType) HollowChoirAt(int step)
        {
            var raw = new RawEnemyEntry
            {
                id = "hollow_choir", displayName = "The Hollow Choir",
                maxHealth = 100, speed = 14, attack = 24, physicalDefense = 5, magicalDefense = 25,
                attackType = "Arcane", isBoss = true, minFloor = 1,
                weakness = "Physical", resistance = "Arcane", active = false,
            };

            bool ok = EnemyEntryResolver.TryResolveAll(new[] { raw }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors));
            var definition = resolved[0];

            var state = new CombatantState(definition.DisplayName, isPlayerSide: false,
                DifficultyCurve.ScaleHealth(definition.BaseStats.maxHealth, step),
                maxMana: 100,
                DifficultyCurve.ScaleAttack(definition.BaseStats.attack, step),
                definition.BaseStats.speed)
            {
                PhysicalDefense = definition.BaseStats.physicalDefense,
                MagicalDefense = definition.BaseStats.magicalDefense,
            };
            return (state, definition.Affinity, definition.AttackType);
        }

        // ---- the derived combat chain: §P's seven rows ---------------------
        //
        // Every figure is a PINNED LITERAL against what the real production
        // functions return TODAY (CLAUDE.md gotcha 5) -- never a recomputed
        // formula. "Eff/action" is the one non-production quantity: §P's own
        // authored rotation average (basic, then a x2-power skill, every
        // other action -> mean multiplier 1.5x on the mitigated basic
        // figure), applied to the real after-DEF number rather than
        // reimplemented as a second call through ComputeSkillDamage.
        private static void AssertFight(
            string label, CombatantState player,
            Func<(CombatantState state, ElementalAffinity affinity, DamageType attackType)> enemyFactory,
            DamageType playerAttackType,
            int expectedBossHp, int expectedRaw, int expectedAfterDef, int expectedTtk,
            int expectedBossDmg, int expectedHitsToDie)
        {
            var (boss, affinity, bossAttackType) = enemyFactory();
            Assert.AreEqual(expectedBossHp, boss.MaxHealth,
                $"{label}: boss HP (DifficultyCurve.ScaleHealth of the real authored content)");

            int raw = CombatMath.ComputeAttackDamage(player, boss);
            Assert.AreEqual(expectedRaw, raw,
                $"{label}: player raw basic (WP x M, no DamageScale -- see class header)");

            var afterDefOutcome = DamagePipeline.AfterDefences(
                raw, playerAttackType, boss, affinity, NoVariance, null, null, player);
            Assert.AreEqual(expectedAfterDef, afterDefOutcome.Damage,
                $"{label}: after boss {(playerAttackType == DamageType.Physical ? "PhysicalDefense" : "MagicalDefense")}");

            double effAction = afterDefOutcome.Damage * 1.5;
            int ttk = (int)Math.Ceiling(boss.MaxHealth / effAction);
            Assert.AreEqual(expectedTtk, ttk, $"{label}: TTK in actions (real, post-fix -- see class header)");

            int bossRaw = CombatMath.ComputeAttackDamage(boss, player);
            var bossOutcome = DamagePipeline.AfterDefences(
                bossRaw, bossAttackType, player, ElementalAffinity.Neutral, NoVariance, null, null, boss);
            Assert.AreEqual(expectedBossDmg, bossOutcome.Damage, $"{label}: boss damage per hit");

            int hitsToDie = (int)Math.Ceiling(player.MaxHealth / (double)bossOutcome.Damage);
            Assert.AreEqual(expectedHitsToDie, hitsToDie, $"{label}: player hits-to-die");
        }

        [Test]
        public void NoobF1_vs_HollowChoir_Step8()
        {
            // §P intended: raw 10, afterDef 8, TTK ~22, bossDmg 30, hitsToDie
            // ~12 -- "dies ~halfway". raw/afterDef reproduce §P exactly.
            // TTK reads higher than §P's own ~22 because AssertFight applies
            // the x1.5 rotation multiplier uniformly, while §P's own table
            // gives the Noob row "no rotation" (Eff/action = the raw 8, not
            // 8 x 1.5 = 12) to represent a noob misplaying it -- with that
            // nuance applied by hand, Ceiling(178/8) = 23, right next to
            // §P's own ~22.
            AssertFight("Noob@F1 vs hollow_choir@8", NoobF1(), () => HollowChoirAt(8), DamageType.Nature,
                expectedBossHp: 178, expectedRaw: 10, expectedAfterDef: 8, expectedTtk: 15,
                expectedBossDmg: 29, expectedHitsToDie: 13);
        }

        [Test]
        public void CompetentF1_vs_HollowChoir_Step8()
        {
            // §P intended: raw 14, afterDef 11, TTK ~10.5, bossDmg 28,
            // hitsToDie ~13 -- "survives on heals, ~50 HP margin". Every
            // figure lands within one point of §P's own -- this profile
            // gets the rotation §P assumed, unlike the Noob row above.
            AssertFight("Competent@F1 vs hollow_choir@8", CompetentF1(), () => HollowChoirAt(8), DamageType.Nature,
                expectedBossHp: 178, expectedRaw: 14, expectedAfterDef: 11, expectedTtk: 11,
                expectedBossDmg: 28, expectedHitsToDie: 13);
        }

        [Test]
        public void LowTalentF2_vs_Warden_Step16()
        {
            // §P intended: raw 13, afterDef 10, TTK ~26, bossDmg 39,
            // hitsToDie ~10 -- "dies". Boss HP here (333) is one point below
            // §P's own stated 334 -- §P's hand-derivation multiplied against
            // its own PUBLISHED, already-rounded 3.181x figure (105 x 3.181 =
            // 334.005); the real DifficultyCurve.ScaleHealth keeps full
            // double precision throughout and floors 105 x 3.180793... =
            // 333.98 to 333. A rounding-of-a-rounding artifact, not a design
            // disagreement. raw/afterDef/hitsToDie reproduce §P almost
            // exactly; TTK (23) sits comfortably in "dies", just short of
            // §P's ~26.
            AssertFight("LowTalent@F2 vs warden@16", LowTalentF2(), () => RealEnemy("warden", 16), DamageType.Nature,
                expectedBossHp: 333, expectedRaw: 13, expectedAfterDef: 10, expectedTtk: 23,
                expectedBossDmg: 38, expectedHitsToDie: 10);
        }

        [Test]
        public void CompetentF3_vs_Warden_Step24()
        {
            // §P intended: raw 39, afterDef 32, TTK ~12.5, bossDmg 46,
            // hitsToDie ~11 -- "survives with heals, thin - okay-ish run".
            // Every figure lands within a point or two of §P's own.
            AssertFight("Competent@F3 vs warden@24", CompetentF3(), () => RealEnemy("warden", 24), DamageType.Nature,
                expectedBossHp: 595, expectedRaw: 39, expectedAfterDef: 32, expectedTtk: 13,
                expectedBossDmg: 45, expectedHitsToDie: 12);
        }

        [Test]
        public void CompetentF3_vs_ThroneColossus_Step24()
        {
            // §P intended: raw 39, afterDef 33, TTK ~16, bossDmg 64,
            // hitsToDie ~8 -- "at death's door - the hard draw". raw/afterDef/
            // TTK/hitsToDie land within a point of §P; bossDmg (69) runs 5
            // over §P's 64, still reads as "the hard draw".
            AssertFight("Competent@F3 vs throne_colossus@24", CompetentF3(), () => RealEnemy("throne_colossus", 24), DamageType.Nature,
                expectedBossHp: 794, expectedRaw: 39, expectedAfterDef: 33, expectedTtk: 17,
                expectedBossDmg: 69, expectedHitsToDie: 8);
        }

        [Test]
        public void CompetentF5_vs_Warden_Step40()
        {
            // §P intended: raw 99, afterDef 82, TTK ~15.5, bossDmg 72,
            // hitsToDie ~9 -- "dies without talents/relics/potions - the meat".
            // Every figure lands within a point of §P's own.
            AssertFight("Competent@F5 vs warden@40", CompetentF5(), () => RealEnemy("warden", 40), DamageType.Nature,
                expectedBossHp: 1894, expectedRaw: 99, expectedAfterDef: 82, expectedTtk: 16,
                expectedBossDmg: 71, expectedHitsToDie: 9);
        }

        [Test]
        public void CompetentF5_vs_ThroneColossus_Step40()
        {
            // §P intended: raw 99, afterDef 86, TTK ~20, bossDmg 94,
            // hitsToDie ~7 -- "the wall boss". raw/afterDef/TTK match §P
            // exactly or within a point; bossDmg (108) runs 14 over §P's 94
            // -- worth a second look (flagged in the session report), though
            // hitsToDie still reads as "the wall boss".
            AssertFight("Competent@F5 vs throne_colossus@40", CompetentF5(), () => RealEnemy("throne_colossus", 40), DamageType.Nature,
                expectedBossHp: 2526, expectedRaw: 99, expectedAfterDef: 86, expectedTtk: 20,
                expectedBossDmg: 108, expectedHitsToDie: 6);
        }

        // ---- non-boss TTK bands ---------------------------------------------
        //
        // §P: trash 2-4 actions (drifting up by design, rat 2.6@F1 ->
        // 3.6@F5), tanky trash 5-7, elite pair ~9-11 total. Representative
        // floors: a step 4 rooms into floor 1's leg, step 36 into floor 5's.
        [Test]
        public void Trash_RatAtFloorOne_CompetentProfile()
        {
            // §P intended band: ~2-4 actions. Real: 3, inside the band.
            var (rat, affinity, _) = RealEnemy("rat", step: 4);
            Assert.AreEqual(53, rat.MaxHealth, "rat HP at step 4");

            var player = CompetentF1();
            int raw = CombatMath.ComputeAttackDamage(player, rat);
            int afterDef = DamagePipeline.AfterDefences(
                raw, DamageType.Nature, rat, affinity, NoVariance, null, null, player).Damage;
            double effAction = afterDef * 1.5;
            int ttk = (int)Math.Ceiling(rat.MaxHealth / effAction);

            Assert.AreEqual(14, raw);
            Assert.AreEqual(14, afterDef, "rat has 0 MagicalDefense -- unmitigated");
            Assert.AreEqual(3, ttk, "inside §P's 2-4 action trash band");
        }

        [Test]
        public void Trash_RatAtFloorFive_CompetentProfile()
        {
            // §P's own drift story (2.6 -> 3.6 actions across floors) now
            // shows up in the real numbers: 3 actions at F1 (above), 4 here
            // -- the same upward drift §P designed, previously invisible
            // because DamageScale's x5 flattened both ends to one hit.
            var (rat, affinity, _) = RealEnemy("rat", step: 36);
            Assert.AreEqual(540, rat.MaxHealth, "rat HP at step 36");

            var player = CompetentF5();
            int raw = CombatMath.ComputeAttackDamage(player, rat);
            int afterDef = DamagePipeline.AfterDefences(
                raw, DamageType.Nature, rat, affinity, NoVariance, null, null, player).Damage;
            int ttk = (int)Math.Ceiling(rat.MaxHealth / (afterDef * 1.5));

            Assert.AreEqual(99, raw);
            Assert.AreEqual(99, afterDef);
            Assert.AreEqual(4, ttk, "the drift §P designed: 3 at F1 -> 4 at F5");
        }

        [Test]
        public void TankyTrash_GolemAtFloorFive_CompetentProfile()
        {
            // §P intended band: ~5-7 actions for the trash-tank. Real: 6,
            // inside the band -- golem is authored weak to Nature
            // (enemies.json's own weakness list), which EffectivenessMultiplier
            // applies (x1.5, rounded away from zero) BEFORE the
            // MagicalDefense mitigation: 99 -> 148.5 -> 149 -> 149x100/110
            // = 135.45 -> 135.
            var (golem, affinity, _) = RealEnemy("golem", step: 36);
            Assert.AreEqual(1080, golem.MaxHealth, "golem HP at step 36");

            var player = CompetentF5();
            int raw = CombatMath.ComputeAttackDamage(player, golem);
            var outcome = DamagePipeline.AfterDefences(
                raw, DamageType.Nature, golem, affinity, NoVariance, null, null, player);
            int ttk = (int)Math.Ceiling(golem.MaxHealth / (outcome.Damage * 1.5));

            Assert.AreEqual(99, raw);
            Assert.AreEqual(1.5f, outcome.Effectiveness, 0.0001f, "golem is weak to Nature");
            Assert.AreEqual(135, outcome.Damage, "the weakness bonus outweighs golem's MagicalDefense (10)");
            Assert.AreEqual(6, ttk, "inside §P's 5-7 action tanky-trash band");
        }

        [Test]
        public void ElitePair_TreantsAtFloorTwo_LowTalentProfile()
        {
            // §P intended band: ~9-11 actions total, "real incoming pressure"
            // (x1.40 HP / x1.15 both DEF each, via the real elite wiring --
            // FightEncounterAdapter.Build(isElite: true), StatBlock.
            // ScaledForElite).
            //
            // REAL: 36 -- more than THREE TIMES §P's own ceiling, the
            // largest single discrepancy this file found post-fix. Under the
            // live x5 this used to read 7 (deceptively close to §P's band,
            // for the wrong reason -- the x5 was masking it). LowTalent@F2's
            // own raw basic is small (13) against a modestly-defended,
            // now-correctly-scaled elite pair (MagicalDefense 6, HP 316
            // each): afterDef floors to 12, so effAction is only 18 against
            // 632 combined HP. Flagged in the session report as a genuine
            // balance gap worth the designer's attention -- either the elite
            // DEF multiplier or LowTalent's own assumed weapon tier is the
            // likely lever (both live in §T), not a formula bug.
            var (eliteTreant, affinity, _) = RealEnemy("treant", step: 12, isElite: true);

            var player = LowTalentF2();
            int raw = CombatMath.ComputeAttackDamage(player, eliteTreant);
            int afterDef = DamagePipeline.AfterDefences(
                raw, DamageType.Nature, eliteTreant, affinity, NoVariance, null, null, player).Damage;
            double effAction = afterDef * 1.5;

            int ttkPair = (int)Math.Ceiling(2 * eliteTreant.MaxHealth / effAction);

            Assert.AreEqual(316, eliteTreant.MaxHealth, "elite treant HP at step 12 (x1.40, then depth-scaled)");
            Assert.AreEqual(13, raw);
            Assert.AreEqual(12, afterDef, "elite treant's MagicalDefense (5 x1.15 = 6, rounded)");
            Assert.AreEqual(36, ttkPair,
                "total actions to clear both treants -- more than 3x §P's ~9-11 band, see this test's own comment");
        }

        // ---- the weapon-vs-armour invariant ---------------------------------
        //
        // §P: "+1 weapon tier => +30-37% damage; a full armour-tier upgrade =>
        // <= +12% damage (via scores/grades)." This invariant is a RATIO
        // between two damage figures, so the DamageScale fix above does not
        // touch it at all -- a constant multiplier on both sides of a ratio
        // cancels out. The underlying dmgAtT3/dmgAtT4/dmgBefore/dmgAfter
        // literals below still had to move (each one is exactly the old
        // figure divided by 5, since every raw value here happens to divide
        // evenly), but every ratio is unchanged from before the fix.
        private static CombatantState Wielder(int attack, int wisdomScore, ScalingGrade grade) =>
            new CombatantState("Wielder", true, 999, 100, attack, PlayerSpeed)
            {
                WeaponScaling = ScalingProfile.None.With(AbilityScore.Wisdom, grade),
                AbilityScores = new AbilityScoreBlock(10, 10, 10, wisdomScore, 10, 10),
            };

        [Test]
        public void WeaponTierInvariant_OneStaffTierIsThirtyPercentMoreDamage()
        {
            // Isolates the weapon-power axis alone: same wielder (WIS d7,
            // grade B -- §P's own B band spans T3-T5, so this tier step does
            // not also cross a grade breakpoint), only the staff's tier moves
            // (T3's endpoint 22 -> T4's 29, both +0 hone).
            int dmgAtT3 = CombatMath.ComputeAttackDamage(
                Wielder(WeaponPower.Compute(22, 0), wisdomScore: 17, ScalingGrade.B), null);
            int dmgAtT4 = CombatMath.ComputeAttackDamage(
                Wielder(WeaponPower.Compute(29, 0), wisdomScore: 17, ScalingGrade.B), null);

            Assert.AreEqual(30, dmgAtT3);
            Assert.AreEqual(39, dmgAtT4);

            double ratio = dmgAtT4 / (double)dmgAtT3;
            Assert.AreEqual(1.30, ratio, 0.0001, "+1 staff tier, T3 -> T4");
            Assert.GreaterOrEqual(ratio, 1.30, "§P: +1 weapon tier should be worth at least +30% damage");
            Assert.LessOrEqual(ratio, 1.37, "§P: +1 weapon tier should be worth at most +37% damage");
        }

        // One full 5-piece set's real per-tier Wisdom grant (styleWeights
        // "wisdom <weight>, constitution 30", matching wool's authored
        // itemsets.json line), via the REAL ItemSetEntryResolver -- not a
        // reimplementation of GearScaling's floored-geometric interpolation.
        private static int FullSetWisdomAtTier(int wisdomWeight, int tier)
        {
            var set = new RawItemSetEntry
            {
                id = "wistest", displayName = "wistest", maxTier = 10, cost = 50, costPerTier = 30,
                statProfile = new[] { "maxHealth 100" },
                styleWeights = new[] { $"wisdom {wisdomWeight}", "constitution 30" },
                pieces = new[]
                {
                    new RawSetPiece { id = "head", displayName = "Head", slot = "Head" },
                    new RawSetPiece { id = "torso", displayName = "Torso", slot = "Torso" },
                    new RawSetPiece { id = "legs", displayName = "Legs", slot = "Legs" },
                    new RawSetPiece { id = "gloves", displayName = "Gloves", slot = "Gloves" },
                    new RawSetPiece { id = "shoes", displayName = "Shoes", slot = "Shoes" },
                }
            };

            bool ok = ItemSetEntryResolver.TryResolveAll(new[] { set }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors));
            return resolved.Where(p => p.Tier == tier).Sum(p => p.AbilityScoreBonus.wisdom);
        }

        [Test]
        public void ArmourTierInvariant_ATypicalTierStepStaysUnderTwelvePercent()
        {
            // wool's own styleWeights (wisdom 80, constitution 30), a typical
            // (non-boundary) tier step: T7 -> T8, on a Competent@F3-like base
            // (WIS d7) so the "before" score is a real, gear-inclusive figure
            // rather than a bare baseline. A fixed WP=100 test weapon
            // isolates the score-channel drip from any weapon-tier noise.
            int wisAtT7 = FullSetWisdomAtTier(wisdomWeight: 80, tier: 7);
            int wisAtT8 = FullSetWisdomAtTier(wisdomWeight: 80, tier: 8);
            Assert.AreEqual(5, wisAtT7);
            Assert.AreEqual(6, wisAtT8);

            const int wp = 100;
            const ScalingGrade grade = ScalingGrade.B;
            int dmgBefore = CombatMath.ComputeAttackDamage(Wielder(wp, wisdomScore: 10 + 7 + wisAtT7, grade), null);
            int dmgAfter = CombatMath.ComputeAttackDamage(Wielder(wp, wisdomScore: 10 + 7 + wisAtT8, grade), null);

            Assert.AreEqual(160, dmgBefore);
            Assert.AreEqual(165, dmgAfter);

            double ratio = dmgAfter / (double)dmgBefore;
            Assert.AreEqual(1.03125, ratio, 0.001, "T7 -> T8's real +1 Wisdom, applied through grade B");
            Assert.LessOrEqual(ratio, 1.12, "§P: a full armour-tier upgrade should be worth at most +12% damage");
        }

        // SECONDARY FINDING, mechanized as a pinned test rather than left as
        // prose: D4's floored-geometric score interpolation is lumpy (each
        // piece floors independently, then the whole set floors again across
        // tiers), so most tier-to-tier Wisdom deltas are 0 but a few specific
        // boundaries (T5->T6, T8->T9 for a WIS-80 style) carry the whole
        // jump at once. At T5->T6, paired with a high scaling grade (A,
        // 0.07/point -- §P's own Competent@F5 band), that single jump alone
        // exceeds the +12% ceiling the invariant wants. Not a §T dial by
        // itself; GearScaling's Base/TierGrowth constants ARE in §T, so a
        // designer wanting a smoother curve has a lever, but smoothing it is
        // a deliberate retune, not a one-line fix this phase should make
        // unilaterally.
        [Test]
        public void ArmourTierInvariant_TheFiveToSixBoundaryIsAKnownExceedingSpike()
        {
            int wisAtT5 = FullSetWisdomAtTier(wisdomWeight: 80, tier: 5);
            int wisAtT6 = FullSetWisdomAtTier(wisdomWeight: 80, tier: 6);
            Assert.AreEqual(0, wisAtT5);
            Assert.AreEqual(5, wisAtT6);

            const int wp = 100;
            const ScalingGrade grade = ScalingGrade.A;
            int dmgBefore = CombatMath.ComputeAttackDamage(Wielder(wp, wisdomScore: 10 + 10 + wisAtT5, grade), null);
            int dmgAfter = CombatMath.ComputeAttackDamage(Wielder(wp, wisdomScore: 10 + 10 + wisAtT6, grade), null);

            Assert.AreEqual(170, dmgBefore);
            Assert.AreEqual(205, dmgAfter);

            double ratio = dmgAfter / (double)dmgBefore;
            Assert.AreEqual(1.2059, ratio, 0.001, "T5 -> T6's real +5 Wisdom jump, applied through grade A");
            Assert.Greater(ratio, 1.12,
                "known exception: the T5->T6 boundary alone exceeds §P's +12% armour ceiling -- see this test's own comment");
        }
    }
}
