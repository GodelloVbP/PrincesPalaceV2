using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The extra turns an action can earn, and the bookkeeping the next actor
    // opens with.
    //
    // FIRST COVERAGE. In v1 this lived in FightController.Turns.cs, reachable
    // only by loading the Gameplay scene, arming a verb and clicking a target
    // — so the ordering rule these tests exist for (Trample is tried BEFORE
    // Bloodlust and short-circuits it) was never pinned by anything but a
    // comment. It is exactly the kind of rule that survives a refactor by
    // accident and then quietly stops holding.
    public class TurnRiderTests
    {
        // Hero opens on initiative and then genuinely loses the next turn --
        // which is what makes "the enemies never got to act" mean a rider
        // fired, rather than the hero simply being fast enough to lap them.
        // TheScheduleHandsOffWhenNoRiderFires below is the control that pins it.
        private const int HeroSpeed = 10;
        private const int FoeSpeed = 9;

        private static CombatantState Hero(int maxMana = 10) =>
            new CombatantState("Hero", true, 200, maxMana, 20, HeroSpeed);

        private static CombatantState Foe(string name, int health) =>
            new CombatantState(name, false, health, 10, 5, FoeSpeed);

        private static readonly ResolvedRelic BloodlustRelic =
            new ResolvedRelic("bloodlust", "Bloodlust", "One more turn.", RelicEffect.Bloodlust, 0);

        private static PlayerKit KitWith(params ResolvedRelic[] relics) =>
            new PlayerKit("hero", CharacterRole.Tank, null, relics.ToList(), null);

        private static void GiveTrample(CombatantState actor, int cap)
        {
            actor.Talents = new TalentEffectSet(
                new[] { new TalentEffect(TalentEffectType.ExtraAttackOnKill, cap) });
        }

        // A tank at the back keeps the fight alive: every rider bails out
        // early on IsOver, so a scenario that kills the last enemy tests the
        // fight-is-decided path instead of the rider.
        private static (FightSession session, CombatantState hero, CombatEncounter encounter)
            Fight(CombatantState hero, PlayerKit kit, params CombatantState[] foes)
        {
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var session = new FightSession(
                encounter,
                kit == null ? null : new List<PlayerKit> { kit },
                null,
                new SeededRandom(1)) { DamageVarianceRange = 0f };
            return (session, hero, encounter);
        }

        // An action and everything it set off, drained in one go.
        //
        // Riders are read through ENEMY TURNS rather than through who is
        // Current, because the session resolves the monsters' replies inside
        // the same call: by the time it returns, the turn is back with the
        // player either way. An extra turn is what the enemies did NOT get to
        // do with it, and that is also the thing the rule actually promises.
        private static IReadOnlyList<CombatBeat> Round(FightSession session, System.Action act)
        {
            act();
            return session.DrainBeats();
        }

        private static int EnemyTurnsIn(IReadOnlyList<CombatBeat> round) =>
            round.Count(b => b.Actor != null && !b.Actor.IsPlayerSide);

        private static IEnumerable<string> MessagesIn(IReadOnlyList<CombatBeat> round) =>
            round.SelectMany(b => b.Messages);

        private static IEnumerable<string> AllMessages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages);

        // ---- the control -----------------------------------------------------

        [Test]
        public void TheScheduleHandsOffWhenNoRiderFires()
        {
            // Everything below reads "hero is still Current" as evidence of an
            // extra turn. That only means anything if the schedule would
            // otherwise have moved on, which is what this pins.
            var hero = Hero();
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));

            Assert.AreSame(hero, encounter.Current, "hero opens on initiative");

            var round = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));

            Assert.GreaterOrEqual(EnemyTurnsIn(round), 1, "with no rider the turn passes on");
            Assert.AreEqual(0, encounter.PendingExtraTurns(hero));
        }

        // ---- Brave: DELETED WITH THE BANK IT SPENT ----------------------------
        //
        // Four tests stood here (holding back banks but cannot spend on
        // itself; the cap; a banked point buys an extra turn; no chain cap
        // needed because every grant was paid for). Hold Back is gone --
        // replaced by Move, which spends the turn on position instead of on
        // tempo -- and with it BankedActions, MaxBankedActions and
        // TryGrantBrave. There is no reduced version of these tests to keep:
        // the mechanic they pinned does not exist. The extra-turn PRIMITIVE
        // they rode on (CombatEncounter.GrantExtraTurn) is still covered by
        // Trample and Bloodlust below, and is what the three turn-start tests
        // further down now use directly to hand the turn back.

        // ---- Trample: a kill that does not consume the action -----------------

        [Test]
        public void TrampleGrantsAnExtraTurnOnAKill()
        {
            var hero = Hero();
            GiveTrample(hero, 1);
            var (session, _, encounter) = Fight(hero, null, Foe("Weak", 1), Foe("Tank", 1000));

            var round = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));

            Assert.AreEqual(0, EnemyTurnsIn(round));
            Assert.IsTrue(MessagesIn(round).Any(m => m.Contains("tramples")));
        }

        [Test]
        public void TrampleWithoutTheTalentDoesNothing()
        {
            var hero = Hero();
            var (session, _, encounter) = Fight(hero, null, Foe("Weak", 1), Foe("Tank", 1000));

            var round = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));

            Assert.GreaterOrEqual(EnemyTurnsIn(round), 1);
        }

        [Test]
        public void TrampleIsCappedPerChain()
        {
            // Cap 1: the first kill trades up, the second does not.
            var hero = Hero();
            GiveTrample(hero, 1);
            var (session, _, encounter) = Fight(hero, null, Foe("Weak", 1), Foe("AlsoWeak", 1), Foe("Tank", 1000));

            var first = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));
            Assert.AreEqual(0, EnemyTurnsIn(first), "first kill of the chain");

            var second = Round(session, () => session.ExecuteAttack(encounter.Enemies[1]));
            Assert.IsFalse(encounter.Enemies[1].IsAlive);
            Assert.GreaterOrEqual(EnemyTurnsIn(second), 1, "the cap holds on the second kill");
        }

        [Test]
        public void AnActionThatKillsNothingEndsTheTrampleChain()
        {
            // The reset lives in AdvanceAfterAction rather than inside
            // TryGrantTrample, because that method is only reached on a kill —
            // a counter that only resets on the path that increments it never
            // resets at all. This is that bug, pinned.
            //
            // Two granted extra turns carry the hero through the killless
            // turn, so the second kill happens on the same actor's
            // uninterrupted run. GrantExtraTurn is the primitive the deleted
            // Brave rider used to reach; this test never cared about the bank,
            // only about keeping the turn.
            var hero = Hero();
            GiveTrample(hero, 1);
            var (session, _, encounter) = Fight(hero, null, Foe("Weak", 1), Foe("AlsoWeak", 1), Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);
            encounter.GrantExtraTurn(hero);


            session.ExecuteAttack(encounter.Enemies[0]);   // kill: chain = 1
            session.UseConsumable("Potion", 1, false);     // no kill: chain reset
            session.DrainBeats();

            var round = Round(session, () => session.ExecuteAttack(encounter.Enemies[1]));   // kill again

            Assert.IsTrue(MessagesIn(round).Any(m => m.Contains("tramples")),
                "a fresh chain, not a capped one");
        }

        // ---- the short-circuit, which is what this file is for ----------------

        [Test]
        public void TrampleShortCircuitsBloodlust_SoOneKillBuysOneAction()
        {
            // A Ram wearing the Bloodlust relic would otherwise bank TWO extra
            // turns for one kill, which neither the talent nor the relic
            // promises, and which stacks their two independent caps into a
            // four-attack chain.
            //
            // Counting pending grants rather than reading messages is the
            // point: grants stack silently, so "hero is Current again" looks
            // identical whether one fired or two.
            var hero = Hero();
            GiveTrample(hero, 1);
            var (session, _, encounter) = Fight(hero, KitWith(BloodlustRelic),
                Foe("Weak", 1), Foe("Tank", 1000));

            var round = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));

            Assert.AreEqual(0, EnemyTurnsIn(round), "one extra turn, taken immediately");
            Assert.AreEqual(0, encounter.PendingExtraTurns(hero),
                "and only one: a second grant would still be queued behind it");

            var messages = MessagesIn(round).ToList();
            Assert.IsTrue(messages.Any(m => m.Contains("tramples")));
            Assert.IsFalse(messages.Any(m => m.Contains("Bloodlust")),
                "Bloodlust is short-circuited, not merely silent");
        }

        [Test]
        public void BloodlustFiresWhenTrampleIsNotAvailable()
        {
            // The control for the assertion above: without it, "no Bloodlust
            // message" would pass just as happily if Bloodlust were broken.
            var hero = Hero();
            var (session, _, encounter) = Fight(hero, KitWith(BloodlustRelic),
                Foe("Weak", 1), Foe("Tank", 1000));

            var round = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));

            Assert.AreEqual(0, EnemyTurnsIn(round));
            Assert.AreEqual(0, encounter.PendingExtraTurns(hero));
            Assert.IsTrue(MessagesIn(round).Any(m => m.Contains("Bloodlust")));
        }

        [Test]
        public void BloodlustIsCappedPerChain()
        {
            // Kills are free, unlike banked actions, so without a cap a lucky
            // room becomes an unbounded chain.
            var foes = new List<CombatantState>();
            for (int i = 0; i < FightTuning.MaxBloodlustChain + 1; i++)
            {
                foes.Add(Foe("Weak" + i, 1));
            }
            foes.Add(Foe("Tank", 1000));

            var hero = Hero();
            var (session, _, encounter) = Fight(hero, KitWith(BloodlustRelic), foes.ToArray());

            for (int i = 0; i < FightTuning.MaxBloodlustChain; i++)
            {
                int slot = i;
                var granted = Round(session, () => session.ExecuteAttack(encounter.Enemies[slot]));
                Assert.AreEqual(0, EnemyTurnsIn(granted), "grant " + (slot + 1) + " of the chain");
            }

            int lastSlot = FightTuning.MaxBloodlustChain;
            var capped = Round(session, () => session.ExecuteAttack(encounter.Enemies[lastSlot]));
            Assert.GreaterOrEqual(EnemyTurnsIn(capped), 1, "the chain is capped");
        }

        [Test]
        public void ARiderNeverFiresOnceTheFightIsDecided()
        {
            // Read-then-reset happens before the IsOver bail-out, so a flag
            // cannot survive into a fight that ended right here.
            var hero = Hero();
            GiveTrample(hero, 3);
            var (session, _, encounter) = Fight(hero, KitWith(BloodlustRelic), Foe("Last", 1));

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsTrue(encounter.IsOver);
            Assert.AreEqual(0, encounter.PendingExtraTurns(hero));
            Assert.IsFalse(AllMessages(session).Any(m => m.Contains("tramples") || m.Contains("Bloodlust")));
        }

        // ---- whose kill it was -----------------------------------------------

        [Test]
        public void AMonstersKillDoesNotEarnThePlayerAnExtraTurn()
        {
            // _killedThisAction says "the action being resolved killed
            // something", and SettleDeath raises it for EVERY credited death --
            // the party's own included, since a monster's swing goes through
            // the same DealDamage funnel. AdvanceAfterAction only clears it at
            // the top of the NEXT player action, so a monster felling an ally
            // during the enemy phase left the flag standing, and the player's
            // next swing read a corpse it did not make as its own kill.
            //
            // Ally at rank 0 so the melee monster has exactly one target it can
            // reach; the schedule then skips the corpse and hands the turn
            // straight back to the hero, which is what makes the second swing
            // the same actor's.
            var ally = new CombatantState("Ally", true, 1, 0, 1, 1);
            var hero = Hero();
            GiveTrample(hero, 1);
            var tank = Foe("Tank", 1000);

            var encounter = new CombatEncounter(new[] { ally, hero }, new[] { tank });
            var session = new FightSession(
                encounter, new List<PlayerKit> { KitWith() }, null,
                new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ExecuteAttack(tank);      // kills nothing; the monster replies
            session.DrainBeats();

            Assert.IsFalse(ally.IsAlive, "the monster was supposed to fell the front rank");
            Assert.AreSame(hero, encounter.Current, "the schedule skips the corpse");

            var round = Round(session, () => session.ExecuteAttack(tank));

            Assert.IsFalse(MessagesIn(round).Any(m => m.Contains("tramples")),
                "a swing that killed nothing collected on a kill the monster made");
            Assert.GreaterOrEqual(EnemyTurnsIn(round), 1, "and the turn has to pass on");
        }

        // ---- turn-start bookkeeping ------------------------------------------

        [Test]
        public void TheTurnOpensWithManaRegenerated()
        {
            var hero = Hero(maxMana: 20);
            hero.PrimaryPool.Current = 0;
            hero.PrimaryPool.GainPerTurn = 3;
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);   // hands the turn back so the opening is hero's

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(3, hero.CurrentMana);
        }

        [Test]
        public void TheTurnOpensWithStatusesTicked_AndSaysWhatHappened()
        {
            // StatusEffects.Tick applies the numbers and reports what it did;
            // the wording is the fight's job, not the status system's.
            var hero = Hero();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 7, 3);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);
            int before = hero.CurrentHealth;

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.Less(hero.CurrentHealth, before);
            Assert.IsTrue(AllMessages(session).Any(m => m.Contains("poison damage")));
        }

        [Test]
        public void TurnStartMessagesLandOnTheBeatTheyFollow()
        {
            // AUDIT #13 again, from the other side: turn-start lines are
            // decided after the beat committed. Written straight to the
            // immediate list they land OLDER than the blow they follow.
            var hero = Hero();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 7, 3);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(1, session.DrainBeats().Count, "no beat is invented for a rider");
            CollectionAssert.IsEmpty(session.DrainImmediateMessages());
        }

        // ---- WHAT AN EXTRA TURN RE-PAYS: an owner decision, parked here ------
        //
        // GrantTurnStart treats "a fresh actor's turn" and "the same actor's
        // extra turn" identically. AdvanceAfterAction calls GrantExtraTurn
        // BEFORE AdvanceTurn, so a Trample or a Bloodlust puts the same actor
        // back on Current and the whole thirteen-step block runs for them a
        // second time: the poison tick, every non-IsSpentByTheTurn duration
        // countdown, the cooldown countdown, and both duration clocks
        // (TickTransform, TickPhoenixEgg).
        //
        // The clearance ledger's row K8 cleared exactly two of those thirteen
        // steps -- _locks.ResetTurn and TickPrimaryPool -- and argued they were
        // intended. The other eleven were never examined.
        //
        // WHY THESE ARE [Ignore]d RATHER THAN FIXED. Every option here is a
        // balance number and belongs to the owner: not paying poison twice is a
        // straight buff to a Trample build, not refunding cooldowns is a
        // straight nerf. The three cases below are the reproduction, kept in
        // the tree rather than in a scratch project so the decision has
        // something to be made against. They FAIL today, by design.
        //
        // The one part that is not a balance question is the third: talents.json
        // ships sheep_ram_trample_3 ("A kill does not cost you the turn") as a
        // PREREQUISITE of sheep_ram_converge (Black Ram Mode, "7 wool for three
        // turns"), so nobody can own the form without owning the rider that
        // eats it -- and the form's splash makes the kill that eats it more
        // likely. A three-turn form that reliably lasts two is a content row
        // lying about itself.

        private const string ExtraTurnDecision = "AUDIT #113 pending: what an extra turn re-pays";

        [Test]
        [Ignore(ExtraTurnDecision)]
        public void APoisonedTramplerIsPoisonedOncePerRoundNotOncePerKill()
        {
            var hero = new CombatantState("Hero", true, 2000, 10, 20, HeroSpeed);
            GiveTrample(hero, 3);
            var (session, _, encounter) = Fight(hero, null,
                Foe("A", 1), Foe("B", 1), Foe("C", 1), Foe("Tank", 100000));

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 10, 99);

            session.ExecuteAttack(encounter.Enemies[0]);
            session.ExecuteAttack(encounter.Enemies[1]);
            session.ExecuteAttack(encounter.Enemies[2]);

            int poisonLines = AllMessages(session).Count(m => m.Contains("poison damage"));

            Assert.AreEqual(1, poisonLines,
                "the trampler was poisoned once per extra action, not once per turn");
        }

        [Test]
        [Ignore(ExtraTurnDecision)]
        public void ATramplersStatusDurationsTickOncePerRound()
        {
            var hero = new CombatantState("Hero", true, 2000, 10, 20, HeroSpeed);
            GiveTrample(hero, 3);
            var (session, _, encounter) = Fight(hero, null,
                Foe("A", 1), Foe("B", 1), Foe("C", 1), Foe("Tank", 100000));

            // Three turns of Shielded, applied before the hero's first action.
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Shielded, 50, 3);

            session.ExecuteAttack(encounter.Enemies[0]);
            session.ExecuteAttack(encounter.Enemies[1]);
            session.ExecuteAttack(encounter.Enemies[2]);

            Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Shielded),
                "a 3-turn Shielded expired inside ONE of the hero's rounds");
        }

        [Test]
        [Ignore(ExtraTurnDecision)]
        public void ThreeTurnsOfBlackRamModeSurviveARoundInWhichHeTramples()
        {
            var hero = new CombatantState("Hero", true, 2000, 10, 20, HeroSpeed);
            GiveTrample(hero, 1);
            var (session, _, encounter) = Fight(hero, null, Foe("A", 1), Foe("Tank", 100000));

            var form = Transformation.Enter(hero, "Black Ram Mode", 3, 0, 30, 20, 40);
            Assert.AreEqual(3, form.TurnsRemaining, "fixture: the form opens on three turns");

            // ONE round: kill A, trample, hit the tank.
            session.ExecuteAttack(encounter.Enemies[0]);
            session.ExecuteAttack(encounter.Enemies[1]);

            Assert.AreEqual(2, form.TurnsRemaining,
                "one round of Black Ram Mode cost two of its three turns because the trample re-opened the turn");
        }
    }
}
