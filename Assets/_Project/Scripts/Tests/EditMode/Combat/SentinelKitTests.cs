using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The Sentinel constellation's mechanics as content will drive them
    // (docs/PLAN_BJORN_CONSTELLATIONS.md section 2): the fight-start arm for
    // the planted shield's switches, the Iron Retort, Fortified, Hold the Line,
    // Bellow's two upgrades, Plant the Shield, Shield Bash and the bot's
    // valuation of the utility skills.
    //
    // FIXTURE ARITHMETIC. Bjorn: 260 max health, Attack 20, Defense 40 and
    // Magical Defense 12 (base total 52), no flat Fury gains. The foes have no
    // defences and 1000 health, so a hit lands as its raw figure: Attack 20 +
    // the skill's flatAmount + any flat rider. A physical hit of raw 140 on
    // Bjorn lands as 140 x 100 / 140 = 100.
    public class SentinelKitTests
    {
        private const string BashId = "shield_bash";
        private const string HoldId = "hold_the_line";

        private static TalentEffectSet Talents(params TalentEffect[] effects) => new TalentEffectSet(effects);

        private static TalentEffect T(TalentEffectType type, int magnitude = 0, string skillId = null) =>
            new TalentEffect(type, magnitude, 0,
                skillId ?? (TalentEffect.IsSkillScoped(type) ? SkillFor(type) : ""));

        private static string SkillFor(TalentEffectType type) =>
            type == TalentEffectType.SkillSplashPercent ? BashId : HoldId;

        private static CombatantState Bjorn(TalentEffectSet talents, int fury = 0, int physicalDefense = 40)
        {
            var pool = new ResourcePool("fury", "Fury", 100, 0, 0, 0);
            pool.Gain(fury);
            return new CombatantState("Bjorn", true, 260, pool, 20, 20)
            {
                CritChancePercent = 0,
                PhysicalDefense = physicalDefense,
                MagicalDefense = 12,
                BaseDefenseTotal = 52,
                Talents = talents,
            };
        }

        private static CombatantState Ally(string name) =>
            new CombatantState(name, true, 200, 0, 10, 10) { CritChancePercent = 0 };

        private static CombatantState Foe() =>
            new CombatantState("Foe", false, 1000, 0, 1, 1) { CritChancePercent = 0 };

        private static ResolvedSkill Strike(string id, int flat, int bashPercent = 0,
            StatusEffectType? status = null, int statusTurns = 0) =>
            new ResolvedSkill(id, id, "test fixture", "bjorn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, flat, false,
                null, SpellPresentation.None, 0,
                appliesStatus: status, statusDuration: statusTurns,
                physicalMove: true, plantedShieldBashPercent: bashPercent);

        private static ResolvedSkill Plant(int cost = 0) =>
            new ResolvedSkill("plant", "Plant the Shield", "test fixture", "bjorn", 1, SkillEffect.PlantShield,
                SkillTargeting.Self, cost, 0, false, 0, 0, false, null, SpellPresentation.None, 0);

        private static ResolvedSkill HoldTheLine(int magnitude = 15, int turns = 2) =>
            new ResolvedSkill(HoldId, "Hold the Line", "test fixture", "bjorn", 1, SkillEffect.BuffParty,
                SkillTargeting.Party, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Fortified, statusMagnitude: magnitude, statusDuration: turns);

        private static ResolvedSkill Bellow() =>
            new ResolvedSkill("bellow", "Bellow", "test fixture", "bjorn", 1, SkillEffect.Provoke,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0);

        private static FightSession Fight(CombatantState bjorn, ResolvedSkill[] skills,
            IReadOnlyList<CombatantState> allies, params CombatantState[] foes)
        {
            var party = new List<CombatantState> { bjorn };
            var kits = new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, skills, null, null) };
            foreach (var ally in allies ?? new CombatantState[0])
            {
                party.Add(ally);
                kits.Add(new PlayerKit(ally.Name, CharacterRole.Tank, null, null, null));
            }

            var session = new FightSession(new CombatEncounter(party.ToArray(), foes), kits, null,
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            Assert.AreSame(bjorn, session.Encounter.Current, "fixture: Bjorn's turn");
            return session;
        }

        private static ActiveStatus Fortified(CombatantState who) =>
            who.Statuses.Single(s => s.Type == StatusEffectType.Fortified);

        // ---- the fight-start pass ------------------------------------------------------

        [Test]
        public void TheRoot_SetsTheSentinelEngine()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.FuryEngineSentinel)));

            Fight(bjorn, null, null, Foe());

            Assert.AreEqual(FuryEngineKind.Sentinel, bjorn.FuryEngine.Kind);
        }

        [Test]
        public void NoTalents_LeavesEverySentinelSeamOff()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);

            Fight(bjorn, null, null, Foe());

            var shield = bjorn.PlantedShield;
            Assert.AreEqual(FuryEngineKind.None, bjorn.FuryEngine.Kind);
            Assert.IsFalse(shield.BreakShards);
            Assert.IsFalse(shield.ShortWaitAfterBash);
            Assert.IsFalse(shield.CoversParty);
            Assert.AreEqual(0, shield.ThornsPercent);
            Assert.AreEqual(0, shield.ReflectMagicPercent);
            Assert.IsFalse(shield.SilenceCasterOnSpellHit);
            Assert.IsFalse(shield.ReflectGrantsFury);
            Assert.IsFalse(shield.SlowsAttacker);
            Assert.IsFalse(shield.DisarmsOnBreak);
        }

        [Test]
        public void EveryShieldTalent_SwitchesItsOwnSeamOn()
        {
            var bjorn = Bjorn(Talents(
                T(TalentEffectType.PlantedShieldBreakShards), T(TalentEffectType.ShieldBashShortWait),
                T(TalentEffectType.ShieldwallCoversParty), T(TalentEffectType.ThornsPercent, 15),
                T(TalentEffectType.ReflectMagicPercent, 15), T(TalentEffectType.SilenceCasterOnSpellHit),
                T(TalentEffectType.ReflectGrantsFury), T(TalentEffectType.SlowsAttacker),
                T(TalentEffectType.DisarmsOnBreak)));

            Fight(bjorn, null, null, Foe());

            var shield = bjorn.PlantedShield;
            Assert.IsTrue(shield.BreakShards);
            Assert.IsTrue(shield.ShortWaitAfterBash);
            Assert.IsTrue(shield.CoversParty);
            Assert.AreEqual(15, shield.ThornsPercent);
            Assert.AreEqual(15, shield.ReflectMagicPercent);
            Assert.IsTrue(shield.SilenceCasterOnSpellHit);
            Assert.IsTrue(shield.ReflectGrantsFury);
            Assert.IsTrue(shield.SlowsAttacker);
            Assert.IsTrue(shield.DisarmsOnBreak);
        }

        // ---- Iron Retort ---------------------------------------------------------------

        [TestCase(10, 2, 968)]
        [TestCase(20, 4, 966)]
        [TestCase(30, 6, 964)]
        public void IronRetort_AddsItsShareOfTheDefenceAboveBase_ToAPhysicalHit(int percent, int bonus, int foeHealth)
        {
            // 60 + 12 against a base of 52 is 20 above; the hit is Attack 20 + 10.
            var bjorn = Bjorn(Talents(T(TalentEffectType.IronRetortPercent, percent)), physicalDefense: 60);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("cleave", 10) }, null, foe);

            Assert.AreEqual(bonus, CombatMath.IronRetortBonus(bjorn));
            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(foeHealth, foe.CurrentHealth);
        }

        [Test]
        public void IronRetort_AddsNothing_ToAFreshCharacterOnHisBaseStats()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.IronRetortPercent, 30)));
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("cleave", 10) }, null, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(0, CombatMath.IronRetortBonus(bjorn));
            Assert.AreEqual(970, foe.CurrentHealth);
        }

        [Test]
        public void IronRetort_CountsAFortifiedStatus_AsDefenceAboveBase()
        {
            // Fortified 10 is +10 to both: 20 above base, so 10% is 2.
            var bjorn = Bjorn(Talents(T(TalentEffectType.IronRetortPercent, 10)));
            var session = Fight(bjorn, null, null, Foe());

            session.ApplyStatusToForTest(bjorn, StatusEffectType.Fortified, 10, 2, bjorn);

            Assert.AreEqual(2, CombatMath.IronRetortBonus(bjorn));
        }

        [Test]
        public void IronRetort_IsZeroWithoutTheTalent_EvenWithDefenceAboveBase()
        {
            Assert.AreEqual(0, CombatMath.IronRetortBonus(Bjorn(TalentEffectSet.Empty, physicalDefense: 90)));
        }

        // ---- Fortified -----------------------------------------------------------------

        [Test]
        public void Fortified_RaisesBothBroadDefences_AndARecastRefreshesRatherThanDoubles()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var session = Fight(bjorn, null, null, Foe());

            session.ApplyStatusToForTest(bjorn, StatusEffectType.Fortified, 15, 2, bjorn);
            session.ApplyStatusToForTest(bjorn, StatusEffectType.Fortified, 15, 2, bjorn);

            Assert.AreEqual(55, CombatMath.BroadDefense(bjorn, DamageType.Physical, null, false));
            Assert.AreEqual(27, CombatMath.BroadDefense(bjorn, DamageType.Fire, null, false));
            Assert.AreEqual(1, bjorn.Statuses.Count(s => s.Type == StatusEffectType.Fortified));
        }

        // ---- Hold the Line -------------------------------------------------------------

        [Test]
        public void HoldTheLine_FortifiesEveryAlly_ForItsAuthoredTurns()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, fury: 0);
            var shawn = Ally("Shawn");
            var odette = Ally("Odette");
            var session = Fight(bjorn, new[] { HoldTheLine() }, new[] { shawn, odette }, Foe());

            Assert.IsTrue(session.CastSkill(0, bjorn));

            foreach (var member in new[] { bjorn, shawn, odette })
            {
                Assert.AreEqual(15, Fortified(member).Magnitude, member.Name);
                Assert.AreEqual(2, Fortified(member).TurnsRemaining, member.Name);
                Assert.AreSame(bjorn, Fortified(member).Source, member.Name);
            }
        }

        [Test]
        public void HoldTheLineT2_HoldsItThreeTurns()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.SkillStatusTurns, 3)));
            var shawn = Ally("Shawn");
            var session = Fight(bjorn, new[] { HoldTheLine() }, new[] { shawn }, Foe());

            Assert.IsTrue(session.CastSkill(0, bjorn));

            Assert.AreEqual(3, Fortified(shawn).TurnsRemaining);
        }

        [Test]
        public void HoldTheLineT2_DoesNotLengthenAnotherSkill()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.SkillStatusTurns, 3)));
            var shawn = Ally("Shawn");
            var other = new ResolvedSkill("other", "Other", "test fixture", "bjorn", 1, SkillEffect.BuffParty,
                SkillTargeting.Party, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Fortified, statusMagnitude: 15, statusDuration: 2);
            var session = Fight(bjorn, new[] { other }, new[] { shawn }, Foe());

            Assert.IsTrue(session.CastSkill(0, bjorn));

            Assert.AreEqual(2, Fortified(shawn).TurnsRemaining);
        }

        [Test]
        public void HoldTheLineT3_CleansesOneDebuffFromEachAlly_OldestFirst_AndNeverABuff()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.SkillCleansesDebuffs, 1)));
            var shawn = Ally("Shawn");
            var session = Fight(bjorn, new[] { HoldTheLine() }, new[] { shawn }, Foe());
            session.ApplyStatusToForTest(shawn, StatusEffectType.Regen, 5, 2);
            session.ApplyStatusToForTest(shawn, StatusEffectType.Poison, 4, 2);
            session.ApplyStatusToForTest(shawn, StatusEffectType.Vulnerable, 25, 2);

            Assert.IsTrue(session.CastSkill(0, bjorn));

            var types = shawn.Statuses.Select(s => s.Type).ToList();
            CollectionAssert.DoesNotContain(types, StatusEffectType.Poison, "the oldest debuff went");
            CollectionAssert.Contains(types, StatusEffectType.Vulnerable, "one per ally");
            CollectionAssert.Contains(types, StatusEffectType.Regen, "a buff is never cleansed");
            CollectionAssert.Contains(types, StatusEffectType.Fortified);
        }

        [Test]
        public void WithoutT3_TheCastCleansesNothing()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var shawn = Ally("Shawn");
            var session = Fight(bjorn, new[] { HoldTheLine() }, new[] { shawn }, Foe());
            session.ApplyStatusToForTest(shawn, StatusEffectType.Poison, 4, 2);

            Assert.IsTrue(session.CastSkill(0, bjorn));

            CollectionAssert.Contains(shawn.Statuses.Select(s => s.Type).ToList(), StatusEffectType.Poison);
        }

        [Test]
        public void HoldTheLineT2_PaysFuryWhenABuffedAllyIsHit_NotWhenHeIsHimself()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.PrimaryGainWhenBuffedAllyHit, 5)));
            var shawn = Ally("Shawn");
            var foe = Foe();
            var session = Fight(bjorn, null, new[] { shawn }, foe);
            session.ApplyStatusToForTest(shawn, StatusEffectType.Fortified, 15, 2, bjorn);
            session.ApplyStatusToForTest(bjorn, StatusEffectType.Fortified, 15, 2, bjorn);

            session.StrikeForTest(foe, shawn, 30, DamageType.Physical);
            Assert.AreEqual(5, bjorn.PrimaryPool.Current, "an ally was hit");

            session.StrikeForTest(foe, shawn, 30, DamageType.Physical);
            Assert.AreEqual(10, bjorn.PrimaryPool.Current, "and again");

            session.StrikeForTest(foe, bjorn, 30, DamageType.Physical);
            Assert.AreEqual(10, bjorn.PrimaryPool.Current, "his own hits are the engine's business, not this rider's");
        }

        [Test]
        public void TheFuryRider_NeedsTheTalent_AndTheBuff()
        {
            var noTalent = Bjorn(TalentEffectSet.Empty);
            var talented = Bjorn(Talents(T(TalentEffectType.PrimaryGainWhenBuffedAllyHit, 5)));
            var shawn = Ally("Shawn");
            var foe = Foe();
            var sessionA = Fight(noTalent, null, new[] { shawn }, foe);
            sessionA.ApplyStatusToForTest(shawn, StatusEffectType.Fortified, 15, 2, noTalent);
            sessionA.StrikeForTest(foe, shawn, 30, DamageType.Physical);
            Assert.AreEqual(0, noTalent.PrimaryPool.Current);

            var shawn2 = Ally("Shawn");
            var foe2 = Foe();
            var sessionB = Fight(talented, null, new[] { shawn2 }, foe2);
            sessionB.StrikeForTest(foe2, shawn2, 30, DamageType.Physical);
            Assert.AreEqual(0, talented.PrimaryPool.Current, "an unbuffed ally pays nothing");
        }

        // ---- Bellow --------------------------------------------------------------------

        [Test]
        public void BellowT3_PaysTenFuryForTheEnemyItProvoked()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.PrimaryGainPerProvokedEnemy, 10)));
            var foeA = Foe();
            var foeB = Foe();
            var session = Fight(bjorn, new[] { Bellow() }, null, foeA, foeB);

            Assert.IsTrue(session.CastSkill(0, foeA));

            Assert.AreEqual(10, bjorn.PrimaryPool.Current, "one enemy provoked");
        }

        [Test]
        public void BellowT3_PaysPerEnemy_WhenTheBellowReachesEveryEnemy()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.PrimaryGainPerProvokedEnemy, 10),
                T(TalentEffectType.ProvokeHitsEveryEnemy)));
            var session = Fight(bjorn, new[] { Bellow() }, null, Foe(), Foe(), Foe());

            Assert.IsTrue(session.CastSkill(0, session.Encounter.Enemies[0]));

            Assert.AreEqual(30, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void BellowWithoutT3_PaysNoFury()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Bellow() }, null, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(0, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void BellowT2_MakesEnemiesPickTheHolderMoreOften_AndOnlyHim()
        {
            int plain = BjornShareOfPicks(TalentEffectSet.Empty);
            int taunting = BjornShareOfPicks(Talents(T(TalentEffectType.TargetPreferencePercent, 200)));

            // A third of the picks without it; three of five (60%) with it.
            Assert.That(plain, Is.InRange(280, 380), "an ordinary party is drawn uniformly, one in three");
            Assert.That(taunting, Is.InRange(540, 660), "counted three times among five draws, 60% of 1000");
        }

        private static int BjornShareOfPicks(TalentEffectSet talents)
        {
            var bjorn = Bjorn(talents);
            var foe = Foe();
            var session = Fight(bjorn, null, new[] { Ally("Shawn"), Ally("Odette") }, foe);

            // A ranged ability, so every party member is a candidate (the plain
            // swing is melee and reaches only the front rank).
            var arrow = EnemyAbility.Of(Strike("arrow", 1), 1f);
            int bjornPicks = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (ReferenceEquals(session.PickIntentTargetForTest(foe, arrow), bjorn)) bjornPicks++;
            }

            return bjornPicks;
        }

        // ---- Plant the Shield ----------------------------------------------------------

        [Test]
        public void PlantTheShield_PutsTheShieldDown_AndIsRefusedWhileItStands()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var plant = Plant();
            var session = Fight(bjorn, new[] { plant }, null, Foe());

            Assert.IsTrue(CombatActions.IsLegalFor(bjorn, plant, out _));
            Assert.IsTrue(session.CastSkill(0, bjorn));

            Assert.IsTrue(bjorn.PlantedShield.IsPlaced);
            Assert.AreEqual(130, bjorn.PlantedShield.PlacedPoints, "2 x (40 + 12) + 26");
            Assert.IsFalse(CombatActions.IsLegalFor(bjorn, plant, out string refusal));
            StringAssert.Contains("already planted", refusal);
        }

        // ---- Shield Bash ---------------------------------------------------------------

        [Test]
        public void ShieldBash_IsRefusedWithNoShieldDown()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var bash = Strike(BashId, 20, bashPercent: 30);
            var foe = Foe();
            var session = Fight(bjorn, new[] { bash }, null, foe);

            Assert.IsFalse(CombatActions.IsLegalFor(bjorn, bash, out string refusal));
            StringAssert.Contains("no planted shield", refusal);
            Assert.IsFalse(session.CastSkill(0, foe));
            Assert.AreEqual(1000, foe.CurrentHealth);
        }

        [Test]
        public void ShieldBash_AddsAShareOfWhatTheShieldSoaked_AndConsumesIt()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var bash = Strike(BashId, 20, bashPercent: 30, status: StatusEffectType.Stun, statusTurns: 1);
            var foe = Foe();
            var session = Fight(bjorn, new[] { bash }, null, foe);
            session.PlantShield(bjorn);
            session.StrikeForTest(foe, bjorn, 140, DamageType.Physical); // lands 100, all soaked

            Assert.AreEqual(100, bjorn.PlantedShield.AbsorbedThisPlacement);
            Assert.IsTrue(session.CastSkill(0, foe));

            // Attack 20 + flat 20 + 30% of 100.
            Assert.AreEqual(930, foe.CurrentHealth);
            Assert.IsFalse(bjorn.PlantedShield.IsPlaced, "the bash consumes the shield");
            Assert.IsTrue(bjorn.PlantedShield.ReplaceWait.IsOpen);
            CollectionAssert.Contains(foe.Statuses.Select(s => s.Type).ToList(), StatusEffectType.Stun);
        }

        [Test]
        public void TheCard_QuotesTheBashBonus_WithoutSpendingTheShield()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var bash = Strike(BashId, 20, bashPercent: 30);
            var foe = Foe();
            var session = Fight(bjorn, new[] { bash }, null, foe);
            session.PlantShield(bjorn);
            session.StrikeForTest(foe, bjorn, 140, DamageType.Physical);

            Assert.AreEqual(70, session.PreviewSkillPower(bjorn, bash, foe));
            Assert.IsTrue(bjorn.PlantedShield.IsPlaced, "a preview consumes nothing");
        }

        [Test]
        public void ShieldBashT2_ShortensTheWaitToOneTurn()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.ShieldBashShortWait)));
            var bash = Strike(BashId, 20, bashPercent: 30);
            var foe = Foe();
            var session = Fight(bjorn, new[] { bash }, null, foe);
            session.PlantShield(bjorn);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(1, bjorn.PlantedShield.ReplaceWait.TurnsRemaining);
        }

        [Test]
        public void ShieldBashT3_SpillsItsDamageOntoTheNeighbours_AndNoFurther()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.SkillSplashPercent, 100, BashId)));
            var bash = Strike(BashId, 20, bashPercent: 30);
            var front = Foe();
            var middle = Foe();
            var back = Foe();
            var session = Fight(bjorn, new[] { bash }, null, front, middle, back);
            session.PlantShield(bjorn);
            session.StrikeForTest(front, bjorn, 140, DamageType.Physical);

            Assert.IsTrue(session.CastSkill(0, front));

            Assert.AreEqual(930, front.CurrentHealth, "Attack 20 + 20 + 30% of 100");
            Assert.AreEqual(930, middle.CurrentHealth, "the neighbour takes the same blow");
            Assert.AreEqual(1000, back.CurrentHealth, "two ranks away is untouched");
        }

        [Test]
        public void TheSplash_IsScopedToItsOwnSkill()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.SkillSplashPercent, 100, BashId)));
            var other = Strike("other", 20);
            var front = Foe();
            var middle = Foe();
            var session = Fight(bjorn, new[] { other }, null, front, middle);

            Assert.IsTrue(session.CastSkill(0, front));

            Assert.AreEqual(1000, middle.CurrentHealth);
        }

        // ---- the bot's valuation -------------------------------------------------------

        private static FightAction? Choose(FightSession session, CombatantState bjorn)
        {
            var legal = FightAction.LegalActions(session, bjorn, null);
            return UtilitySkills.Choose(session, bjorn, legal);
        }

        [Test]
        public void TheBot_PlantsTheShield_WheneverItIsLegal()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var session = Fight(bjorn, new[] { Plant() }, null, Foe());

            var choice = Choose(session, bjorn);
            Assert.IsTrue(choice.HasValue);
            Assert.AreEqual(0, choice.Value.SkillIndex);

            session.PlantShield(bjorn);
            Assert.IsFalse(Choose(session, bjorn).HasValue, "already down, so it is not on the legal menu");
        }

        [Test]
        public void TheBot_CastsHoldTheLine_OnlyWhileNobodyIsFortified()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var shawn = Ally("Shawn");
            var session = Fight(bjorn, new[] { HoldTheLine() }, new[] { shawn }, Foe());

            Assert.IsTrue(Choose(session, bjorn).HasValue);

            session.ApplyStatusToForTest(shawn, StatusEffectType.Fortified, 15, 2, bjorn);
            Assert.IsFalse(Choose(session, bjorn).HasValue);
        }

        [Test]
        public void TheBot_Bellows_WhenAnAllyIsHurt_OrWhenItPaysFury()
        {
            var shawn = Ally("Shawn");
            var bjorn = Bjorn(TalentEffectSet.Empty);
            var session = Fight(bjorn, new[] { Bellow() }, new[] { shawn }, Foe());
            Assert.IsFalse(Choose(session, bjorn).HasValue, "nobody hurt, and it pays nothing");

            shawn.CurrentHealth = 100;
            Assert.IsTrue(Choose(session, bjorn).HasValue, "Shawn at 50%");

            var paid = Bjorn(Talents(T(TalentEffectType.PrimaryGainPerProvokedEnemy, 10)));
            var paidSession = Fight(paid, new[] { Bellow() }, new[] { Ally("Odette") }, Foe());
            Assert.IsTrue(Choose(paidSession, paid).HasValue, "T3 makes it a Fury source");
        }
    }
}
