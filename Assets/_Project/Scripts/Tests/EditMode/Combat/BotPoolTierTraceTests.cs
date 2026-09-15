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
    // TurnTrace.PoolTierFired / .PrimaryPoolAfter, through FightRunner.Play --
    // not through FightSession.CastSkill directly, which is what
    // PoolTierSlamCastTests already covers and does not touch the trace at
    // all. This is the one place those two fields get written, so it is the
    // one place a test can catch them silently going back to 0/stale the way
    // Phase 1's own copy of this field did before it was reverted (see
    // RunTrace.TurnTrace.PoolTierFired's own header).
    //
    // Same Slam skill and the same Fury pool PoolTierSlamCastTests builds
    // (0.5 spend -> x2, 1.0 spend -> x4), zero UnityEngine and zero Core --
    // FightSession/FightRunner/CombatEncounter are all Domain, so this class
    // runs under `dotnet test` same as PoolTierResolutionTests beside it.
    public class BotPoolTierTraceTests
    {
        private const int BaseDamage = 27; // Attack 10, flatAmount 17, DamageVarianceRange 0 -- see PoolTierSlamCastTests.

        private sealed class AlwaysCastTheSkill : IFightPolicy
        {
            public FightAction Choose(FightSession session, CombatantState actor,
                IReadOnlyList<FightAction> legal, SeededRandom rng)
            {
                return legal.FirstOrDefault(a => a.Kind == FightActionKind.Skill).Kind == FightActionKind.Skill
                    ? legal.First(a => a.Kind == FightActionKind.Skill)
                    : legal[0];
            }
        }

        private static ResolvedSkill Slam() =>
            new ResolvedSkill("slam", "Slam", "", "bear", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 17, false,
                null, SpellPresentation.None, 0,
                poolTiers: new[]
                {
                    new ResolvedPoolTier(0.5f, 2f, shake: 0.6f),
                    new ResolvedPoolTier(1.0f, 4f, shake: 1f, hitStopSeconds: 0.18f),
                });

        // `foeHealth` is set BELOW the tiered hit it is meant to catch, so
        // the fight ends after exactly one command -- FightRunner.Play stops
        // the instant session.IsOver flips, which is what keeps this a
        // one-TurnTrace test without a second command to filter out.
        private static (FightSession session, CombatantState bjorn) Fight(int startingFury, int foeHealth)
        {
            var fury = new ResourcePool("fury", "Fury", max: 100, gainPerTurn: 0, gainOnAttack: 15, gainOnDamageTaken: 10);
            fury.Gain(startingFury);

            var bjorn = new CombatantState("Bjorn", true, 999999, fury, attack: 10, speed: 10);
            var foe = new CombatantState("Dummy", false, foeHealth, 0, 1, 1);

            var kit = new PlayerKit("bear", CharacterRole.Tank, new List<ResolvedSkill> { Slam() }, null, DamageType.Physical);

            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();

            return (session, bjorn);
        }

        [Test]
        public void Fury0_TiedToBaseDamage_TracesNoFiredTier()
        {
            var (session, bjorn) = Fight(startingFury: 0, foeHealth: BaseDamage);
            var fightTrace = new FightTrace();

            FightRunner.Play(session, new AlwaysCastTheSkill(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), fightTrace);

            Assert.AreEqual(1, fightTrace.TurnTraces.Count, "the hit should have ended the fight in one command");
            var turn = fightTrace.TurnTraces[0];
            Assert.AreEqual(0f, turn.PoolTierFired, "no tier fired at 0 fury");
            // gainOnAttack fires on any damaging action, tiered or not: 0
            // spent, then +15 for landing the hit (PoolTierSlamCastTests
            // pins the same arithmetic against the live pool).
            Assert.AreEqual(15, turn.PrimaryPoolAfter);
        }

        [Test]
        public void Fury50_FiresTheFirstTier_TracesItAndTheSpentPool()
        {
            var (session, bjorn) = Fight(startingFury: 50, foeHealth: BaseDamage * 2);
            var fightTrace = new FightTrace();

            FightRunner.Play(session, new AlwaysCastTheSkill(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), fightTrace);

            Assert.AreEqual(1, fightTrace.TurnTraces.Count);
            var turn = fightTrace.TurnTraces[0];
            Assert.AreEqual(2f, turn.PoolTierFired, "the x2 tier's own damage multiplier");
            Assert.AreEqual(15, turn.PrimaryPoolAfter, "50 spent to 0, then +15 for landing the hit");
        }

        [Test]
        public void Fury100_FiresTheHighestTier_TracesX4()
        {
            var (session, bjorn) = Fight(startingFury: 100, foeHealth: BaseDamage * 4);
            var fightTrace = new FightTrace();

            FightRunner.Play(session, new AlwaysCastTheSkill(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), fightTrace);

            Assert.AreEqual(1, fightTrace.TurnTraces.Count);
            var turn = fightTrace.TurnTraces[0];
            Assert.AreEqual(4f, turn.PoolTierFired, "the x4 tier's own damage multiplier");
            Assert.AreEqual(15, turn.PrimaryPoolAfter, "100 spent to 0, then +15 for landing the hit");
        }

        [Test]
        public void ASecondCommandInTheSameFight_TracesNoLeftoverTierFromTheFirstsBeat()
        {
            // FightRunner drains every command's own beats immediately (see
            // its own comment at the call site), so the SECOND command of
            // one fight must not read the FIRST command's fired tier back
            // off a beat list neither command emptied. foeHealth is set to
            // die on exactly the second command (108 from the x4 tier, then
            // 27 more of plain base damage = 135), so this is a two-
            // TurnTrace fight in one FightRunner.Play call rather than two
            // separate ones -- the shape the bot's own fights actually take.
            var (session, bjorn) = Fight(startingFury: 100, foeHealth: BaseDamage * 4 + BaseDamage);
            var fightTrace = new FightTrace();

            FightRunner.Play(session, new AlwaysCastTheSkill(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), fightTrace);

            Assert.AreEqual(2, fightTrace.TurnTraces.Count);

            Assert.AreEqual(4f, fightTrace.TurnTraces[0].PoolTierFired);
            Assert.AreEqual(15, fightTrace.TurnTraces[0].PrimaryPoolAfter, "100 spent to 0, then +15 for landing the hit");

            // Fury is 15 after the first cast, well under the first
            // threshold, so the second command must trace no fired tier --
            // unless a leftover beat from the first command's drain were
            // still sitting on the session, in which case this would wrongly
            // read 4 again.
            Assert.AreEqual(0f, fightTrace.TurnTraces[1].PoolTierFired,
                "fury at 15 after the first cast is below the first threshold");
            Assert.AreEqual(30, fightTrace.TurnTraces[1].PrimaryPoolAfter, "15 untouched, then +15 for landing the hit");
        }
    }
}
