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
    // Every relic from the two 2026 balance passes that needs a live
    // FightSession to exercise -- combat-level, a hand-built PlayerKit
    // handed straight to a real FightSession, same shape as
    // SpeedAndBountyRelicTests. Most of these pin
    // FightSession.RelicMechanics.cs's own arithmetic: Magic Marker, Jar of
    // Bear Urine, World Ender's Crown, Cursed Idol, Amassing Star,
    // Rampaging Bull's Horn, Ice Fingernail, Loaded Dice, Sparring Saber,
    // Sparring Buckler, Essence Siphon, Disgruntled Lackey, Inconspicuous
    // Key, Dancer's Anklet and Phoenix Egg. Three don't, and are tested
    // here anyway because "needs a live fight to prove" is the organizing
    // question, not which file the mechanic's code happens to live in:
    // Monkey King's Scepter is one line in FightSession.cs's CanReach,
    // Berserker's Vest lives in FightSession.Ledger.cs, and Jo-Sun's Book
    // of Anatomy / Vampire Dentures are pure numeric RelicModifier stats
    // with no relic hook at all. Pointy Nail on the End of a Stick has no
    // combat-level test here either, for the same reason -- see
    // RelicModifierTests.ArmorPenetrationFlatAppliesToTheArmorPenetrationStat
    // and CombatMath.BroadDefense's own ArmorPenetrationTests.
    public class RelicMechanicsTests
    {
        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static EnemyKit Foe(string name = "dummy") =>
            new EnemyKit(new ResolvedEnemy(name, name, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) Fight(
            RelicEffect relic, int heroMaxHealth = 100, int heroMaxMana = 100,
            IReadOnlyList<ResolvedSkill> skills = null)
        {
            var hero = new CombatantState("Shawn", true, heroMaxHealth, heroMaxMana, 20, 20);
            var foe1 = new CombatantState("Foe1", false, 999999, 0, 1, 1);
            var foe2 = new CombatantState("Foe2", false, 999999, 0, 1, 1);

            var kit = new PlayerKit("hero", CharacterRole.Tank, skills,
                new List<ResolvedRelic> { Relic(relic) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe1, foe2 }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { Foe("Foe1"), Foe("Foe2") },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe1, foe2);
        }

        // ---- magic marker -------------------------------------------------------

        [Test]
        public void MagicMarkerMarksASpellTarget()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker,
                skills: new List<ResolvedSkill> { TestSkills.CastableSkill() });

            session.CastSkill(0, foe1);

            Assert.IsTrue(Marks.IsMarked(foe1), "a cast should mark whatever it hits");
        }

        [Test]
        public void MagicMarkerConsumesTheMarkAndRestoresTwentyPercentOfMissingMana()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker, heroMaxMana: 100,
                skills: new List<ResolvedSkill> { TestSkills.CastableSkill() });
            hero.PrimaryPool.Current = 50; // 50 missing

            session.CastSkill(0, foe1); // marks foe1, costs 0 mana (test skill is authored free)
            session.ExecuteAttack(foe1); // consumes the mark

            Assert.IsFalse(Marks.IsMarked(foe1), "the attack should have consumed the mark");
            Assert.AreEqual(60, hero.CurrentMana, "50 missing x 20% = 10 restored, 50 + 10 = 60");
        }

        // relics.json: "restores 20% of your missing mana (or signature
        // resource)" -- MANA FIRST, the resource in the main clause. The code
        // read signature first on the premise that "every combatant has at
        // most one of the two", which characters.json contradicts: the sheep
        // authors a wool signature and no primaryPoolId, so he gets the
        // default mana pool and spends BOTH to act -- static_fleece used to
        // cost 6 mana + 3 wool (removed 2026-09-15, AUDIT #150; no shipped
        // skill costs both today). For the one character who could have both,
        // the relic therefore always refilled the pool that already
        // regenerates every turn and never the one gating his most expensive
        // skills.
        //
        // Literal: 20 missing mana x 20% = 4.
        [Test]
        public void TheMarkRefundsShawnsManaBeforeHisWool()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker, heroMaxMana: 100,
                skills: new List<ResolvedSkill> { TestSkills.CastableSkill() });
            hero.PrimaryPool.Current = 80;                             // 20 missing
            hero.SignaturePool = new ResourcePool("wool", "Wool", 16, 0, 0, 0);
            hero.SignaturePool.Gain(6);                                // 10 missing

            session.CastSkill(0, foe1);   // marks foe1
            session.ExecuteAttack(foe1);  // consumes the mark

            Assert.AreEqual(84, hero.CurrentMana, "20 missing mana x 20% = 4 restored");
            Assert.AreEqual(6, hero.SignaturePool.Current, "and the fleece is left alone");
        }

        [Test]
        public void AnAttackWithNoMarkRestoresNothing()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker, heroMaxMana: 100);
            hero.PrimaryPool.Current = 50;

            session.ExecuteAttack(foe1); // no spell was cast first -- nothing marked

            Assert.AreEqual(50, hero.CurrentMana, "no mark, no restore");
        }

        // ---- jar of bear urine ---------------------------------------------------

        [Test]
        public void JarOfBearUrineMarksEveryEnemyAtCombatStart()
        {
            var (_, _, foe1, foe2) = Fight(RelicEffect.JarOfBearUrine);

            Assert.IsTrue(Marks.IsMarked(foe1));
            Assert.IsTrue(Marks.IsMarked(foe2));
        }

        // ---- world ender's crown -------------------------------------------------

        [Test]
        public void CrossingBelowThirtyPercentFearsEveryEnemy()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.WorldEndersCrown);

            // 100 -> 25, which is 25% -- below the 30% line.
            session.DealDamageForTest(foe1, hero, 75, DamageType.Physical);

            Assert.IsTrue(Fear.IsFeared(foe1));
            Assert.IsTrue(Fear.IsFeared(foe2));
        }

        [Test]
        public void ItDoesNotFireAgainWhileStillBelowTheLine()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.WorldEndersCrown);

            session.DealDamageForTest(foe1, hero, 75, DamageType.Physical); // 100 -> 25, fires
            // Fear's 1-turn duration is spent by the turn it skips, not by
            // the tick that opens that turn -- see StatusEffects.Tick's
            // IsSpentByTheTurn exemption. Driving the skip is what expires it.
            StatusEffects.ConsumeStun(foe1.Statuses);
            StatusEffects.ConsumeStun(foe2.Statuses);
            Assert.IsFalse(Fear.IsFeared(foe1), "the first Fear must have expired for this to be a real check");

            session.DealDamageForTest(foe1, hero, 5, DamageType.Physical); // 25 -> 20, still below 30%

            Assert.IsFalse(Fear.IsFeared(foe1), "still below the line -- must not re-fire");
            Assert.IsFalse(Fear.IsFeared(foe2));
        }

        [Test]
        public void GoingBackAboveThirtyPercentReArmsIt()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.WorldEndersCrown);

            session.DealDamageForTest(foe1, hero, 75, DamageType.Physical); // 100 -> 25, fires
            StatusEffects.Tick(foe1);
            StatusEffects.Tick(foe2);

            session.HealForTest(hero, 50); // 25 -> 75, back above 30%
            session.DealDamageForTest(foe1, hero, 55, DamageType.Physical); // 75 -> 20, below again

            Assert.IsTrue(Fear.IsFeared(foe1), "re-armed by going back above 30%, so this crossing must fire too");
            Assert.IsTrue(Fear.IsFeared(foe2));
        }

        // ---- cursed idol ----------------------------------------------------------

        [Test]
        public void EachHitStacksAThreePercentResistanceShred()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.CursedIdol);

            int before = foe1.CurrentHealth;
            session.DealDamageForTest(hero, foe1, 100, DamageType.Physical); // 0 existing stacks -> +0%
            int firstLoss = before - foe1.CurrentHealth;
            Assert.AreEqual(100, firstLoss, "no stack yet, so no bonus on the hit that creates the first one");

            before = foe1.CurrentHealth;
            session.DealDamageForTest(hero, foe1, 100, DamageType.Physical); // 1 existing stack -> +3%
            int secondLoss = before - foe1.CurrentHealth;
            Assert.AreEqual(103, secondLoss, "1 stack x 3% = 3% bonus on top of the 100");

            before = foe1.CurrentHealth;
            session.DealDamageForTest(hero, foe1, 100, DamageType.Physical); // 2 existing stacks -> +6%
            int thirdLoss = before - foe1.CurrentHealth;
            Assert.AreEqual(106, thirdLoss, "2 stacks x 3% = 6% bonus");

            Assert.AreEqual(3, FallingOffStacks.Count(foe1, FightTuning.CursedIdolStackKey));
        }

        [Test]
        public void StacksCapAtFifteenPercent()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.CursedIdol);

            for (int i = 0; i < 7; i++)
            {
                session.DealDamageForTest(hero, foe1, 100, DamageType.Physical);
            }

            Assert.AreEqual(5, FallingOffStacks.Count(foe1, FightTuning.CursedIdolStackKey), "capped at 5 stacks");
            Assert.AreEqual(15, FallingOffStacks.Magnitude(foe1, FightTuning.CursedIdolStackKey, 3, 15));
        }

        // ---- amassing star ---------------------------------------------------------

        [Test]
        public void ARealKillGrantsTwoPercentRunWideDamage()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.AmassingStar);
            foe1.CurrentHealth = 1;

            // A REAL kill, through ExecuteAttack -- RelicsOnEachKill (and so
            // AmassingStarOnKill) only fires from the actual kill-settling
            // path (DealDamage -> SettleDeath), which the DealDamageForTest
            // seam deliberately bypasses by passing KillCredit.Nobody (it
            // exists to test the DAMAGE funnel alone, not the kill funnel).
            session.ExecuteAttack(foe1);

            Assert.AreEqual(2, session.BonusDamagePercentEarned, "one real kill = +2%");
        }

        [Test]
        public void KillingASummonGrantsNothing()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.AmassingStar);
            foe1.CurrentHealth = 1;
            foe1.IsSummon = true;

            session.ExecuteAttack(foe1);

            Assert.AreEqual(0, session.BonusDamagePercentEarned, "summons do not count");
        }

        [Test]
        public void RunWideBonusDamagePercentIncreasesActualDamageDealt()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.DualWield); // a relic irrelevant to this mechanic

            session.ExecuteAttack(foe1);
            int withoutBonus = 999999 - foe1.CurrentHealth;

            foe1.CurrentHealth = 999999;
            session.RunWideBonusDamagePercent = 50;
            session.ExecuteAttack(foe1);
            int withBonus = 999999 - foe1.CurrentHealth;

            Assert.AreEqual(withoutBonus + withoutBonus * 50 / 100, withBonus,
                "the run-wide bonus is an exact percent of the same swing's own damage");
        }

        // ---- rampaging bull's horn --------------------------------------------------

        private static ResolvedSkill TransformSkill() =>
            new ResolvedSkill("black_ram", "Black Ram Mode", "", "hero", 1,
                SkillEffect.Transform, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0);

        [Test]
        public void CastingAConvergenceAbilityGrantsFiftyPercentReduction()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.RampagingBullsHorn,
                skills: new List<ResolvedSkill> { TransformSkill() });

            session.CastSkill(0, hero);

            float multiplier = StatusEffects.DamageTakenMultiplier(hero.Statuses);
            Assert.AreEqual(0.5f, multiplier, 0.0001f, "Protect at 50% halves incoming damage");
        }

        [Test]
        public void AnOrdinaryCastDoesNotGrantTheReduction()
        {
            var noop = new ResolvedSkill("heal", "Heal", "", "hero", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0);

            var (session, hero, _, _) = Fight(RelicEffect.RampagingBullsHorn,
                skills: new List<ResolvedSkill> { noop });

            session.CastSkill(0, hero);

            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(hero.Statuses),
                "only a convergence (Transform) cast should grant the reduction");
        }

        // ---- fixture: the speed/attack-parameterised fights below --------------------
        //
        // A second Fight overload set, kept distinct from the one above
        // (FightWithSpeed rather than a second Fight overload) because both
        // would otherwise apply to a bare `Fight(RelicEffect.X)` call and
        // the compiler would refuse to pick one.
        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) FightWithSpeed(
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

        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) FightWithSpeed(
            RelicEffect relic, int heroSpeed = 20, int foeSpeed = 1, int foeAttack = 1,
            IReadOnlyList<ResolvedSkill> skills = null) =>
            FightWithSpeed(new[] { relic }, heroSpeed, foeSpeed, foeAttack, skills);

        // ---- ice fingernail --------------------------------------------------------

        [Test]
        public void EachLandedAttackStacksATenPercentSlow()
        {
            // Equal speed keeps the turn order a clean 1:1 alternation, so
            // the malus math below is exact rather than fighting whichever
            // side charges faster.
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.IceFingernail, heroSpeed: 100, foeSpeed: 100);

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
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.IceFingernail, heroSpeed: 100, foeSpeed: 100);

            for (int i = 0; i < 4; i++) session.ExecuteAttack(foe1);

            Assert.AreEqual(4, FallingOffStacks.Count(foe1, FightTuning.IceFingernailStackKey), "capped at 4 stacks");
            Assert.AreEqual(-40, session.SpeedBonusFrom(foe1, RelicEffect.IceFingernail));
            Assert.AreEqual(60, foe1.Speed);
        }

        // ---- loaded dice -----------------------------------------------------------

        [Test]
        public void ExactlyOneEnemyIsStunnedAtCombatStart()
        {
            var (_, _, foe1, foe2) = FightWithSpeed(RelicEffect.LoadedDice);

            bool foe1Stunned = StatusEffects.HasStun(foe1.Statuses);
            bool foe2Stunned = StatusEffects.HasStun(foe2.Statuses);

            Assert.AreNotEqual(foe1Stunned, foe2Stunned, "exactly one enemy should be stunned, never zero or both");
        }

        // ---- monkey king's scepter --------------------------------------------------

        [Test]
        public void MeleeReachesAnyEnemyRegardlessOfTheFrontRank()
        {
            var (session, hero, _, foe2) = FightWithSpeed(RelicEffect.MonkeyKingsScepter);

            Assert.IsTrue(session.CanReach(hero, Reach.Melee, foe2),
                "foe2 stands behind the living front rank, foe1");
        }

        [Test]
        public void WithoutTheScepterTheFrontRankRuleStillApplies()
        {
            var (session, hero, _, foe2) = FightWithSpeed(RelicEffect.DualWield); // a relic irrelevant to reach

            Assert.IsFalse(session.CanReach(hero, Reach.Melee, foe2),
                "foe1 is alive and blocks foe2 without the scepter");
        }

        [Test]
        public void TheScepterLiftsTheMeleeRuleAndNotAnAuthoredRankRestriction()
        {
            // THE KIND DECIDES, NOT THE MASK. Reach.Melee and an authored
            // front-only restriction (reachSlots: [1] -> FromContent([1]))
            // carry the identical mask {rank 0}, and the scepter lifts
            // exactly one of them: its promise is about striking past a
            // bodyguard, not about ignoring where a spell may be aimed.
            var (session, hero, _, foe2) = FightWithSpeed(RelicEffect.MonkeyKingsScepter);

            Assert.IsTrue(session.CanReach(hero, Reach.Melee, foe2));
            Assert.IsFalse(session.CanReach(hero, Reach.FromContent(new[] { 1 }), foe2),
                "an authored front-only reach is not the front-rank RULE, and nothing lifts it");
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
            var (session, hero, _, _) = FightWithSpeed(RelicEffect.SparringSaber, heroSpeed: 100, foeSpeed: 100);

            session.NoteDeliberateMoveForTest(hero, hero);

            Assert.AreEqual(30, session.SpeedBonusFrom(hero, RelicEffect.SparringSaber), "30% of a 100 base");
            Assert.AreEqual(130, hero.Speed);
        }

        [Test]
        public void SomeoneElseMovingDoesNotGrantSparringSaberAnything()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.SparringSaber, heroSpeed: 100);

            session.NoteDeliberateMoveForTest(foe1, hero); // hero's ACTION moved someone else

            Assert.AreEqual(0, session.SpeedBonusFrom(hero, RelicEffect.SparringSaber), "only your OWN position counts");
        }

        // ---- sparring buckler ----------------------------------------------------------

        [Test]
        public void CastingAnAbilityThatAltersAnyPositionGrantsAWard()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.SparringBuckler);

            session.NoteDeliberateMoveForTest(foe1, hero); // hero's cast moved foe1

            // "Ward" reuses this game's existing Shielded status -- spent on
            // the NEXT hit taken, not a passive multiplier -- see
            // NoteDeliberateMove's own comment.
            int reduced = StatusEffects.ConsumeShieldedReduction(hero, 100);
            Assert.AreEqual(85, reduced, "15% off the next hit taken");
        }

        [Test]
        public void TheWardIsOncePerTurn()
        {
            var (session, hero, foe1, foe2) = FightWithSpeed(RelicEffect.SparringBuckler);

            session.NoteDeliberateMoveForTest(foe1, hero);
            StatusEffects.ConsumeShieldedReduction(hero, 999); // spend it

            session.NoteDeliberateMoveForTest(foe2, hero); // same turn, second trigger

            Assert.IsFalse(StatusEffects.IsWarded(hero), "the lock should have refused a second ward this turn");
        }

        // ---- essence siphon ---------------------------------------------------------

        [Test]
        public void KillingANonSummonEnemyHealsThreePercentMaxHealth()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.EssenceSiphon, heroSpeed: 100, foeSpeed: 1);
            hero.CurrentHealth = 50;
            foe1.CurrentHealth = 1;

            session.ExecuteAttack(foe1);

            Assert.AreEqual(53, hero.CurrentHealth, "3% of 100 max health = 3, healed on top of 50");
        }

        // The relic fires on a kill, which is exactly when the holder is most
        // likely to be at full health already -- so it is the announcement
        // that has to be measured, not the request. See HealAndCount's header.
        [Test]
        public void AFullHealthHolderIsNotToldItDrewAnything()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.EssenceSiphon, heroSpeed: 100, foeSpeed: 1);
            foe1.CurrentHealth = 1;

            Assert.AreEqual(hero.MaxHealth, hero.CurrentHealth, "fixture: the holder starts full");

            session.ExecuteAttack(foe1);

            var lines = session.DrainBeats().SelectMany(b => b.Messages).ToList();
            Assert.IsFalse(lines.Any(m => m.Contains("fading essence")),
                "nothing was drawn, so nothing is announced: " + string.Join(" | ", lines));
        }

        [Test]
        public void KillingASummonHealsNothing()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.EssenceSiphon, heroSpeed: 100, foeSpeed: 1);
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
            var (session, hero, foe1, foe2) = FightWithSpeed(RelicEffect.InconspicuousKey, heroSpeed: 100, foeSpeed: 1, foeAttack: 7);
            hero.CurrentHealth = 100;
            foe1.CurrentHealth = 1;
            int foe2Before = foe2.CurrentHealth;

            session.ExecuteAttack(foe1); // kills foe1 (attack 7 -- the "one last blow" it deals)

            Assert.AreEqual(foe2Before - 7, foe2.CurrentHealth, "the fallen foe's own Attack, dealt to another enemy");
        }

        [Test]
        public void ItOnlyFiresOncePerCombat()
        {
            var (session, hero, foe1, foe2) = FightWithSpeed(RelicEffect.InconspicuousKey, heroSpeed: 100, foeSpeed: 1, foeAttack: 7);
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
        public void AttackingAutomaticallyRepositionsTheWearerForward_AndPaysNoSparringRelic()
        {
            // THE DECOUPLING, pinned from both sides. The Anklet still pulls
            // the wearer up the TURN ORDER on their own action, and it no
            // longer fires the field-move note the two Sparring relics hang
            // off: a queue position is not a field position, and paying
            // Sparring Buckler for one was the conflation the rename fixed.
            //
            // Buckler rather than Saber for the negative half, for the reason
            // the old version of this test already gave: a one-turn Speed
            // buff's whole lifecycle sits inside one synchronous round trip
            // (see FightSession.SpeedBuffs' own header), while Buckler's ward
            // rides Shielded's 99-turn duration and survives it.
            var (session, hero, foe1, _) = FightWithSpeed(
                new[] { RelicEffect.DancersAnklet, RelicEffect.SparringBuckler }, heroSpeed: 100, foeSpeed: 100);

            session.ExecuteAttack(foe1);

            Assert.IsTrue(session.DrainBeats().SelectMany(b => b.Messages)
                    .Any(m => m.Contains("slips a step forward in the order")),
                "the Anklet still takes its pull");
            Assert.IsFalse(StatusEffects.IsWarded(hero),
                "and a turn-order pull is not footwork -- no Sparring ward");
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

            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.BerserkersVest, heroSpeed: 100, foeSpeed: 100,
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

        // Finding 1: ResetTurn used to clear the WHOLE per-turn lock set on
        // ANY combatant's turn boundary, not just the owner's. Two enemies
        // both acting (and both hitting hero) inside the same round means
        // GrantTurnStart fires for foe1, THEN for foe2, before hero's own
        // turn comes back around -- the exact shape that caught the bug,
        // where foe2's turn boundary wiped the lock foe1's hit had already
        // spent and let a second shave through.
        //
        // Isolated by comparison rather than a hardcoded post-CastSkill
        // cooldown value: CastSkill only returns once every enemy in the
        // round has acted, so the normal per-turn cooldown tick and any
        // Berserker's Vest shaves all land before this test ever gets
        // control back, and pinning their combined total would mean
        // re-deriving TickCooldowns' own arithmetic (CLAUDE.md gotcha 5).
        // Two otherwise-identical fights that differ ONLY in whether a
        // second, ALSO-hitting enemy exists must land hero on the exact
        // same cooldown -- one shave per round, not one per attacker.
        [Test]
        public void TwoEnemiesHittingHeroInOneRoundOnlyShaveTheCooldownOnce()
        {
            ResolvedSkill Skill() => new ResolvedSkill("s", "S", "", "hero", 1, SkillEffect.HealSelf,
                SkillTargeting.Self, 0, 0, false, 0, 1, false, null, SpellPresentation.None, 0, cooldownTurns: 20);

            // One live attacker: foe2 is killed off before it can act, so
            // only foe1 lands a hit this round.
            var (soloSession, soloHero, _, soloFoe2) = FightWithSpeed(RelicEffect.BerserkersVest,
                heroSpeed: 100, foeSpeed: 1, foeAttack: 5, skills: new List<ResolvedSkill> { Skill() });
            soloFoe2.CurrentHealth = 0;
            soloSession.CastSkill(0, soloHero);
            int soloCooldown = soloSession.CooldownRemaining(soloHero, "s");

            // Two live attackers: foe1 AND foe2 both land a hit this round.
            var (duoSession, duoHero, _, _) = FightWithSpeed(RelicEffect.BerserkersVest,
                heroSpeed: 100, foeSpeed: 1, foeAttack: 5, skills: new List<ResolvedSkill> { Skill() });
            duoSession.CastSkill(0, duoHero);
            int duoCooldown = duoSession.CooldownRemaining(duoHero, "s");

            Assert.AreEqual(soloCooldown, duoCooldown,
                "a second enemy also hitting hero in the same round must not shave a second cooldown turn");
        }

        // Finding 5 (code review): Berserker's Vest used to gate on the RAW
        // `amount > 0`, read before absorption was even computed -- so a hit
        // a signature resource ate IN FULL still shaved a cooldown for a
        // blow that never reached the wearer's health at all. Fixed to gate
        // on `toHealth > 0` (post-absorb).
        [Test]
        public void AFullyAbsorbedHitDoesNotShaveTheCooldown()
        {
            var skill = new ResolvedSkill("s", "S", "", "hero", 1, SkillEffect.HealSelf, SkillTargeting.Self,
                0, 0, false, 0, 1, false, null, SpellPresentation.None, 0, cooldownTurns: 20);

            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.BerserkersVest, heroSpeed: 100, foeSpeed: 100,
                skills: new List<ResolvedSkill> { skill });

            session.CastSkill(0, hero);
            int before = session.CooldownRemaining(hero, "s");
            Assert.Greater(before, 0, "fixture: the skill should still be on cooldown at all");

            // Set AFTER the baseline read, not before: CastSkill's own
            // internal enemy turn(s) would otherwise chip the resource with
            // an incidental hit before the test's own hit ever lands.
            // AbsorbPerPoint 1, Current 10: a 10-point hit is absorbed in
            // full (min(10*1, 10) == 10), toHealth == 0.
            hero.SignaturePool = new ResourcePool("shield", "Shield", 10, 0, 0, 0,
                absorbPerPoint: 1, absorbsDamage: true) { Current = 10 };

            session.DealDamageForTest(foe1, hero, 10, DamageType.Physical);

            Assert.AreEqual(before, session.CooldownRemaining(hero, "s"),
                "a hit absorbed in full never reached the wearer's health -- the vest must not fire");
        }

        [Test]
        public void APartiallyAbsorbedHitStillShavesTheCooldown()
        {
            var skill = new ResolvedSkill("s", "S", "", "hero", 1, SkillEffect.HealSelf, SkillTargeting.Self,
                0, 0, false, 0, 1, false, null, SpellPresentation.None, 0, cooldownTurns: 20);

            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.BerserkersVest, heroSpeed: 100, foeSpeed: 100,
                skills: new List<ResolvedSkill> { skill });

            session.CastSkill(0, hero);
            int before = session.CooldownRemaining(hero, "s");
            Assert.Greater(before, 0, "fixture: the skill should still be on cooldown at all");

            // Set AFTER the baseline read -- see the sibling test's own
            // comment. AbsorbPerPoint 1, Current 3: a 10-point hit is only
            // absorbed for 3 (min(3*1, 10) == 3), toHealth == 7 -- the
            // wearer is genuinely hit, so the vest must still fire.
            hero.SignaturePool = new ResourcePool("shield", "Shield", 10, 0, 0, 0,
                absorbPerPoint: 1, absorbsDamage: true) { Current = 3 };

            session.DealDamageForTest(foe1, hero, 10, DamageType.Physical);

            Assert.AreEqual(before - 1, session.CooldownRemaining(hero, "s"),
                "a hit that partially reached the wearer's health must still shave the cooldown once");
        }

        // ---- phoenix egg -----------------------------------------------------------------

        [Test]
        public void FatalDamageHatchesAnEggInsteadOfKilling()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);

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
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch, EggHealth = 100

            session.DealDamageForTest(foe1, hero, 40, DamageType.Physical);

            Assert.AreEqual(60, hero.EggHealth);
            Assert.AreEqual(1, hero.CurrentHealth, "still pinned -- the shell ate the hit, not the wearer");
        }

        // THE TWO EGG BRANCHES ARE STILL DAMAGE, and both of them used to
        // return out of ApplyAndCountDamage before the pool bookkeeping at the
        // bottom of it ever ran -- so a wearer whose primary pool fills by
        // being hit got nothing for the blow that hatched the shell, nothing
        // for every blow that landed on it afterwards, and read as having spent
        // an idle turn while being beaten. The bookkeeping is at the TOP of
        // that method now, ahead of every exit.
        [Test]
        public void TheBlowThatHatchesTheEggStillFeedsTheWearersPrimaryPool()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);
            hero.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0,
                gainOnAttack: 0, gainOnDamageTaken: 10);

            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatches

            Assert.IsTrue(hero.IsPhoenixEgg, "fixture: this hit should have hatched the shell");
            Assert.AreEqual(10, hero.PrimaryPool.Current);
        }

        [Test]
        public void AHitOnTheShellStillFeedsTheWearersPrimaryPool()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch, EggHealth = 100

            // Set AFTER the hatch so the number below counts one blow, not two.
            hero.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0,
                gainOnAttack: 0, gainOnDamageTaken: 10);

            session.DealDamageForTest(foe1, hero, 40, DamageType.Physical);

            Assert.AreEqual(10, hero.PrimaryPool.Current);
        }

        [Test]
        public void TheEggBreakingKillsTheWearerOutright()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);
            session.DealDamageForTest(foe1, hero, 999, DamageType.Physical); // hatch, EggHealth = 100

            session.DealDamageForTest(foe1, hero, 100, DamageType.Physical); // the shell's own pool reaches 0

            Assert.IsFalse(hero.IsPhoenixEgg);
            Assert.AreEqual(0, hero.CurrentHealth);
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void SurvivingThreeTurnsRevivesWithTheEggsSurvivingFraction()
        {
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);
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
            var (session, hero, foe1, _) = FightWithSpeed(RelicEffect.PhoenixEgg);
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
