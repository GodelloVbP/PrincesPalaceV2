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
    // PHASE D2, the item-modifier plan: the unified Chilled status. Covers
    // the read hook itself (FightSession.SpeedBuffs.ApplyChilled/
    // RefreshChilledSpeed), Lucky Deck's migration onto it, its composition
    // with the Toothed Necklace's speed ramp (a buff, deliberately left
    // untouched by this phase), and the new Frosty on-hit rider
    // (ModifierEffectType.ChilledOnHitChancePercent). The real-content,
    // scaled-through-a-real-fight half of Frosty lives in
    // FrostyModifierReachesCombatTests (PlayMode, needs Resources).
    //
    // Every expected number here is a hand-derived literal from
    // GrantSpeedMalusPercent/GrantSpeedPercent's own PUBLISHED arithmetic
    // (percent of the TRUE base, floored at -1 rather than 0) -- never
    // recomputed from the production code under test (CLAUDE.md gotcha 5).
    public class ChilledStatusTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100,
            int attack = 20, int speed = 20) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static FightSession Session(CombatEncounter encounter) =>
            new FightSession(encounter, null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };

        private static void Give(CombatantState combatant, params ModifierEffect[] effects) =>
            combatant.ModifierEffects = new ModifierEffectSet(effects);

        // ---- the read hook itself ----------------------------------------------

        [Test]
        public void ApplyingChilled_ReducesSpeedByThePercentOfTheTrueBase()
        {
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { Fighter("Hero", true) }, new[] { target }));

            session.ApplyChilledForTest(target, magnitude: 25, turns: 3);

            // -(40 * 25 / 100) = -10.
            Assert.AreEqual(30, target.Speed, "a fresh 25% Chilled off true base 40 must take exactly 10 points");
        }

        [Test]
        public void ApplyingChilled_AddsAChilledEntryToStatuses_VisibleLikeAnyOtherStatus()
        {
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { Fighter("Hero", true) }, new[] { target }));

            session.ApplyChilledForTest(target, magnitude: 25, turns: 3);

            var chilled = target.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Chilled);
            Assert.IsNotNull(chilled, "Chilled must be a real ActiveStatus, not just a private number in the speed dictionary");
            Assert.AreEqual(25, chilled.Magnitude);
            Assert.AreEqual(3, chilled.TurnsRemaining);
        }

        // Reuses the same relative-position pattern
        // ItemModifierCombatHookTests.PushBackOnHit_MovesTheTargetLaterInTurnOrder
        // already uses to prove a turn-order effect is LIVE rather than
        // merely a stored number: comparing the target's own presence in the
        // real, already-Start()-ed TurnOrder queue before and after, with no
        // turn advanced and no attack made in between. If this only changed
        // CombatantState.Speed and never reached TurnOrder.SetSpeed, the two
        // counts would be identical.
        [Test]
        public void ApplyingChilled_ImmediatelyChangesTheLiveTurnQueue_NotJustTheStoredSpeedInt()
        {
            var hero = Fighter("Hero", true, speed: 10);
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { hero }, new[] { target }));

            var before = session.Encounter.UpcomingTurns(60);
            int countBefore = before.Count(a => ReferenceEquals(a, target));

            session.ApplyChilledForTest(target, magnitude: 50, turns: 5);

            var after = session.Encounter.UpcomingTurns(60);
            int countAfter = after.Count(a => ReferenceEquals(a, target));

            Assert.Less(countAfter, countBefore,
                "a target slowed by Chilled must appear FEWER times over the same upcoming window -- if only " +
                "the stored int moved and TurnOrder was never told, the live queue would be unchanged");
        }

        [Test]
        public void Chilled_RevertsSpeedExactly_WhenItExpires()
        {
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { Fighter("Hero", true) }, new[] { target }));

            session.ApplyChilledForTest(target, magnitude: 25, turns: 1);
            Assert.AreEqual(30, target.Speed, "fixture check: the malus should have applied");

            session.TickStatusesForTest(target);

            Assert.IsFalse(target.Statuses.Any(s => s.Type == StatusEffectType.Chilled),
                "a one-turn Chilled must be gone from the status list after one tick");
            Assert.AreEqual(40, target.Speed,
                "an expired Chilled must hand back EXACTLY what it took, not merely apply the inverse percent " +
                "(which would not return to 40 -- see FightSession.SpeedBuffs' own header on why)");
        }

        [Test]
        public void Chilled_SurvivesATickWithTurnsRemaining_AndKeepsItsMalus()
        {
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { Fighter("Hero", true) }, new[] { target }));

            session.ApplyChilledForTest(target, magnitude: 25, turns: 2);
            session.TickStatusesForTest(target);

            var chilled = target.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Chilled);
            Assert.IsNotNull(chilled, "a two-turn Chilled must survive one tick");
            Assert.AreEqual(1, chilled.TurnsRemaining);
            Assert.AreEqual(30, target.Speed, "the malus must still be in effect while turns remain");
        }

        // ---- refresh, not stack ------------------------------------------------

        [Test]
        public void Chilled_ASecondStrongerApplication_RefreshesRatherThanStacking()
        {
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { Fighter("Hero", true) }, new[] { target }));

            session.ApplyChilledForTest(target, magnitude: 10, turns: 2);
            Assert.AreEqual(36, target.Speed, "fixture check: -(40*10/100) = -4");

            session.ApplyChilledForTest(target, magnitude: 25, turns: 5);

            Assert.AreEqual(1, target.Statuses.Count(s => s.Type == StatusEffectType.Chilled),
                "a second application must refresh the existing entry, not add a second one");
            Assert.AreEqual(30, target.Speed,
                "the refreshed malus must be -(40*25/100) = -10 off the TRUE base, not -4 stacked with a further -10 (26)");
        }

        [Test]
        public void Chilled_AWeakerReapplication_NeverWeakensTheExistingOne()
        {
            var target = Fighter("Foe", false, speed: 40);
            var session = Session(new CombatEncounter(new[] { Fighter("Hero", true) }, new[] { target }));

            session.ApplyChilledForTest(target, magnitude: 25, turns: 5);
            Assert.AreEqual(30, target.Speed);

            session.ApplyChilledForTest(target, magnitude: 5, turns: 1);

            Assert.AreEqual(30, target.Speed, "a weaker re-application must not lift the stronger chill already active");
        }

        // ---- lucky deck's migration ---------------------------------------------

        [Test]
        public void LuckyDeckSlow_AppliesTheChilledStatus_NotABareSpeedNumber()
        {
            var actor = Fighter("Hero", true);
            var target = Fighter("Foe", false, speed: 20);
            var session = Session(new CombatEncounter(new[] { actor }, new[] { target }));

            session.LuckyDeckSlowForTest(actor, target);

            var chilled = target.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Chilled);
            Assert.IsNotNull(chilled, "the migrated Lucky Deck slow must land as the Chilled status");
            Assert.AreEqual(FightTuning.LuckyDeckSlowPercent, chilled.Magnitude);
            Assert.AreEqual(FightTuning.LuckyDeckSlowTurns, chilled.TurnsRemaining);
            Assert.AreSame(actor, chilled.Source, "credit belongs to whoever's Lucky Deck fired");
        }

        // THE REGRESSION GUARD. Lucky Deck's own numbers (30% for 1 turn)
        // existed before this migration and must produce the exact same net
        // Speed change afterward -- only the plumbing (a status instead of a
        // bare GrantSpeedMalusPercent call) was meant to change.
        [Test]
        public void LuckyDeckSlow_NetSpeedEffectIsUnchangedFromBeforeTheMigration()
        {
            var actor = Fighter("Hero", true);
            var target = Fighter("Foe", false, speed: 20);
            var session = Session(new CombatEncounter(new[] { actor }, new[] { target }));

            session.LuckyDeckSlowForTest(actor, target);

            // -(20 * 30 / 100) = -6 -- the exact figure the pre-migration
            // GrantSpeedMalusPercent(target, RelicEffect.LuckyDeck, 30, 1)
            // call always produced.
            Assert.AreEqual(14, target.Speed, "Lucky Deck's own balance must not silently move just because the plumbing did");
        }

        [Test]
        public void LuckyDeckSlow_StillTicksOffAfterOneOfTheTargetsOwnTurns()
        {
            var actor = Fighter("Hero", true);
            var target = Fighter("Foe", false, speed: 20);
            var session = Session(new CombatEncounter(new[] { actor }, new[] { target }));

            session.LuckyDeckSlowForTest(actor, target);
            Assert.AreEqual(14, target.Speed, "fixture check");

            session.TickStatusesForTest(target);

            Assert.AreEqual(20, target.Speed, "the one-turn slow must revert exactly after the target's own next turn starts");
        }

        // ---- composing with the necklace ramp (a BUFF, untouched by this phase) ----

        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        // THE INTERACTION TEST. A combatant carrying BOTH the Necklace's
        // low-health speed ramp (a buff) AND Chilled (a debuff) at once must
        // have the two compose ADDITIVELY against the SAME true base --
        // exactly the guarantee TwoSpeedRelicsBothMeasureAgainstTheSameTrueBase
        // (SpeedAndBountyRelicTests) already proves for two buffs. This is
        // the reason Chilled's malus was widened into the SAME _speedBuffs
        // dictionary every relic buff already uses, rather than a second,
        // parallel dictionary of its own -- two independent "true base"
        // computations over one combatant's Speed is exactly the shape of
        // bug TrueBaseSpeed's own header documents already happening once.
        [Test]
        public void Chilled_ComposesAdditivelyWithTheNecklaceRamp_AgainstTheSameTrueBase()
        {
            var hero = new CombatantState("Shawn", true, 200, 999, 20, 20);
            var foe = new CombatantState("Dummy", false, 999999, 0, 1, 1);
            hero.CurrentHealth = 50; // exactly 25% -- the necklace's ramp ceiling

            var kit = new PlayerKit("hero", CharacterRole.Tank, null,
                new List<ResolvedRelic> { Relic(RelicEffect.ToothedNecklace) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit>
                {
                    new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(), 0, 0, false,
                        DamageType.Physical, DamageType.Physical, 0), false),
                },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();

            // One swing is enough for AutoResolveEnemyTurns to hand hero its
            // own next turn back before returning control (heroSpeed 20 vs
            // foeSpeed 1 -- the same large-gap fixture shape
            // SpeedAndBountyRelicTests' own Fight() helper relies on), which
            // is what actually runs RefreshNecklaceSpeed.
            session.ExecuteAttack(foe);
            Assert.AreEqual(24, hero.Speed, "fixture check: 20 base + 20% necklace ramp at a quarter health = 24");

            session.ApplyChilledForTest(hero, magnitude: 30, turns: 3);

            // TrueBaseSpeed subtracts EVERY grant, so it reads 24 - 4 (the
            // necklace's own Granted) = 20, the real unbuffed base -- not 24.
            // -(20 * 30 / 100) = -6, so 24 - 6 = 18.
            Assert.AreEqual(18, hero.Speed,
                "Chilled must measure its 30% against the TRUE base (20), not the necklace-inflated 24 " +
                "(which would wrongly take -7 and land on 17)");

            hero.CurrentHealth = 200; // full health -- the ramp has nothing left to give
            session.ExecuteAttack(foe);

            // RefreshNecklaceSpeed revokes its now-zero grant (18 - 4 = 14);
            // Chilled's own malus (still 2 turns left) is untouched by that.
            Assert.AreEqual(14, hero.Speed,
                "healing off the ramp must hand back exactly the necklace's own 4 points, leaving Chilled's " +
                "-6 in place -- neither source may silently absorb or erase the other's bookkeeping");

            var chilled = hero.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Chilled);
            Assert.IsNotNull(chilled, "Chilled must still be an active status after the necklace's ramp cleared");
        }

        // ---- frosty's on-hit rider (ModifierEffectType.ChilledOnHitChancePercent) ----

        [Test]
        public void ChilledOnHit_LandsTheStatus_WhenTheChanceRollSucceeds()
        {
            // A LARGE hero/foe speed gap, the same fixture shape
            // ItemModifierCombatHookTests.PushBackOnHit... relies on: it
            // keeps the hero in control for this one swing, so nothing ticks
            // the freshly-applied Chilled down before this test gets to look
            // at it. A smaller gap risks the target's own turn starting
            // (and Chilled ticking once) inside this same ExecuteAttack call.
            var hero = Fighter("Hero", true, attack: 20, speed: 200);
            var foe = Fighter("Foe", false, maxHealth: 1000, speed: 20);
            Give(hero, new ModifierEffect(ModifierEffectType.ChilledOnHitChancePercent, 100));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);
            Assert.IsTrue(session.IsPlayerTurn,
                "fixture check: the hero must still hold the turn after one swing, or foe's own turn-start tick " +
                "would disturb the TurnsRemaining this test is about to check");

            var chilled = foe.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Chilled);
            Assert.IsNotNull(chilled, "a 100% ChilledOnHitChancePercent must land Chilled on every landed hit");
            Assert.AreEqual(FightTuning.ChilledOnHitSpeedPercent, chilled.Magnitude);
            Assert.AreEqual(FightTuning.ChilledOnHitTurns, chilled.TurnsRemaining);

            // -(20 * 20 / 100) = -4.
            Assert.AreEqual(16, foe.Speed, "20 speed, -20% of true base = -4, landing on 16");
        }

        [Test]
        public void ChilledOnHit_NeverFiresWithoutTheModifier()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 1000, speed: 5);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            Assert.IsFalse(foe.Statuses.Any(s => s.Type == StatusEffectType.Chilled), "no modifier equipped, no chill");
            Assert.AreEqual(5, foe.Speed, "speed must be untouched with no modifier equipped");
        }

        [Test]
        public void ChilledOnHit_NeverFiresWhenTheRollFails()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 1000, speed: 5);
            Give(hero, new ModifierEffect(ModifierEffectType.ChilledOnHitChancePercent, 0));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            Assert.IsFalse(foe.Statuses.Any(s => s.Type == StatusEffectType.Chilled), "a 0% chance must never proc");
            Assert.AreEqual(5, foe.Speed);
        }
    }
}
