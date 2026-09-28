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
            round.Count(b => b.IsAction && !b.Actor.IsPlayerSide);

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

        // EnemyTurnsIn above -- and every other "count the enemy's
        // turns" reader in the suite -- must not ask `Actor != null`, which a
        // status tick on an enemy can satisfy: a healing tick names its holder
        // as Actor (so the view does not make them flinch). Nothing shipped
        // gives a monster Regen yet, so this is the content change that would
        // otherwise break those tests for no reason their author could see.
        [Test]
        public void AStatusTickOnAnEnemyIsNotAnEnemyTurn()
        {
            var hero = Hero();
            var foe = Foe("Tank", 1000);
            var (session, _, _) = Fight(hero, null, foe);
            foe.CurrentHealth = 500;
            session.ApplyStatusToForTest(foe, StatusEffectType.Regen, 10, 3);
            session.ApplyStatusToForTest(foe, StatusEffectType.Poison, 10, 3);
            session.CrossTurnBoundaryForTest();
            session.DrainBeats();

            session.TickStatusesForTest(foe);
            var ticks = session.DrainBeats();

            Assert.IsTrue(ticks.Any(b => ReferenceEquals(b.Actor, foe) && b.StatusTick == StatusEffectType.Regen),
                "the regen tick is a beat whose Actor is the enemy -- the case the old idiom miscounted");
            Assert.IsTrue(ticks.Any(b => b.StatusTick == StatusEffectType.Poison),
                "and the poison tick is a beat too");
            Assert.AreEqual(0, EnemyTurnsIn(ticks), "neither tick is a turn the enemy took");
        }

        // ---- a form running out is not an action -----------------------------
        //
        // A transform expiring must not be an action. The expiry beat names
        // its holder as Actor (the stage flashes over him); opening it
        // through BeginBeat -- the action seam -- would count it as a turn
        // taken AND tell a decaying pool the turn was not idle.
        //
        // THE FIXTURE. Speed 100 against 1, so the hero takes every turn and
        // no enemy swing ever lands in the numbers. The pool decays 10 on any
        // turn with no ACTION in it (AnyAction; the Damage reading never
        // hears the expiry). The form has one turn left, so Begin()'s opening
        // turn start runs it out: the pool ticks first (50 -> 40, an empty
        // window), then the form expires.
        private static (FightSession session, CombatantState hero) AFormThatRunsOutAtTheFirstTurnStart()
        {
            var hero = new CombatantState("Hero", true, 500, 10, 20, 100);
            hero.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0, 0, 0)
            {
                DecayPerIdleTurn = 10,
                DecayUnless = PoolDecayTrigger.AnyAction,
            };
            hero.PrimaryPool.Gain(50);

            Transformation.Enter(hero, "Black Ram Mode", 1, 0, 0, 0);

            var foe = new CombatantState("Tank", false, 100000, 10, 5, 1);
            var (session, _, _) = Fight(hero, null, foe);
            session.Begin();
            return (session, hero);
        }

        private static CombatBeat ExpiryIn(IReadOnlyList<CombatBeat> beats) =>
            beats.SingleOrDefault(b => b.Cause == BeatCause.TransformExpiry);

        [Test]
        public void AFormRunningOutIsNotAnAction()
        {
            var (session, hero) = AFormThatRunsOutAtTheFirstTurnStart();

            Assert.IsNull(hero.Transformation, "fixture: the form ran out at the opening turn start");

            var expiry = ExpiryIn(session.DrainBeats());

            Assert.IsNotNull(expiry, "the form ran out without recording its beat");
            Assert.AreSame(hero, expiry.Actor, "the stage still flashes over the holder");
            Assert.IsFalse(expiry.IsAction, "the clock running out is not a turn anybody took");
            Assert.AreEqual(FightSession.Stances.Idle, expiry.Stances[hero],
                "and it wears no cast pose, only his own idle");
        }

        [Test]
        public void AFormRunningOutDoesNotKeepAnIdlePoolFromDecaying()
        {
            var (_, hero) = AFormThatRunsOutAtTheFirstTurnStart();
            Assert.AreEqual(40, hero.PrimaryPool.Current, "fixture: the opening turn's decay");

            // THE NEXT TURN START'S OWN RULE, asked directly. Every way to
            // reach that turn start through the session passes through a beat
            // the hero opens -- an action, or a forfeited turn, which also
            // counts as one -- and either would keep the pool on its own, so
            // the only window in which "the expiry kept it" can be seen is
            // this one: the form has run out and nothing else has happened.
            hero.PrimaryPool.TickTurnStart();

            Assert.AreEqual(30, hero.PrimaryPool.Current,
                "the form running out told the pool the turn was not idle");
        }

        [Test]
        public void ARealActionOnTheTurnTheFormRanOutStillKeepsThePool()
        {
            // The control for the one above: it asserts a decay, which deleting
            // NotePoolActivity outright would satisfy too.
            var (session, hero) = AFormThatRunsOutAtTheFirstTurnStart();
            session.DrainBeats();

            // An item: an action, and never damage, so under AnyAction it is
            // the one thing keeping the window alive. The hero's next turn
            // opens inside this call (the foe is too slow to intervene) and
            // its pool tick judges the turn the form ran out on.
            Assert.IsTrue(session.UseConsumable("Rag", 0, restoresMana: false));

            Assert.IsTrue(session.DrainBeats().Any(b => b.IsAction && ReferenceEquals(b.Actor, hero)),
                "the item is an action");
            Assert.AreEqual(40, hero.PrimaryPool.Current,
                "an action on the expiry turn stopped counting as activity");
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
            // uninterrupted run. GrantExtraTurn is the primitive that grants
            // a bonus turn; this test never cares about the bank, only about
            // keeping the turn.
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
            // promises -- and would burn Bloodlust's once-a-fight charge on a
            // kill Trample had already paid for.
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

        // ---- Bloodlust: once per fight -------------------------------------------
        //
        // The relic buys ONE extra action a fight, on the holder's first kill
        // Trample did not already reward, and is then spent until a new
        // FightSession. Counts are literal on purpose.

        [Test]
        public void BloodlustGrantsOnTheFirstKill_ThenNeverAgainThisFight()
        {
            var hero = Hero();
            var (session, _, encounter) = Fight(hero, KitWith(BloodlustRelic),
                Foe("W0", 1), Foe("W1", 1), Foe("W2", 1), Foe("Tank", 1000));

            var first = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));
            Assert.AreEqual(0, EnemyTurnsIn(first), "kill 1: Bloodlust's one extra action");
            Assert.AreEqual(1, MessagesIn(first).Count(m => m.Contains("Bloodlust")));

            var second = Round(session, () => session.ExecuteAttack(encounter.Enemies[1]));
            Assert.IsFalse(encounter.Enemies[1].IsAlive);
            Assert.GreaterOrEqual(EnemyTurnsIn(second), 1, "kill 2, same turn: spent");
            Assert.IsFalse(MessagesIn(second).Any(m => m.Contains("Bloodlust")));

            // A NEW TURN, same fight: still spent. This is what separates
            // "once per fight" from a per-turn chain cap.
            Assert.AreSame(hero, encounter.Current, "fixture: the turn came back to the hero");
            var third = Round(session, () => session.ExecuteAttack(encounter.Enemies[2]));
            Assert.IsFalse(encounter.Enemies[2].IsAlive);
            Assert.GreaterOrEqual(EnemyTurnsIn(third), 1, "kill 3, next turn: still spent");
            Assert.IsFalse(MessagesIn(third).Any(m => m.Contains("Bloodlust")));
        }

        [Test]
        public void BloodlustIsBackInTheNextFight()
        {
            // The SAME combatant and kit in both fights, so nothing but the
            // new FightSession can be what re-arms it.
            var hero = Hero();
            var kit = KitWith(BloodlustRelic);

            var (firstFight, _, firstEncounter) = Fight(hero, kit, Foe("W0", 1), Foe("Tank", 1000));
            var spent = Round(firstFight, () => firstFight.ExecuteAttack(firstEncounter.Enemies[0]));
            Assert.AreEqual(1, MessagesIn(spent).Count(m => m.Contains("Bloodlust")),
                "fixture: fight 1 spent it");

            var (secondFight, _, secondEncounter) = Fight(hero, kit, Foe("W0", 1), Foe("Tank", 1000));
            var round = Round(secondFight, () => secondFight.ExecuteAttack(secondEncounter.Enemies[0]));

            Assert.AreEqual(0, EnemyTurnsIn(round), "fight 2: one extra action again");
            Assert.AreEqual(1, MessagesIn(round).Count(m => m.Contains("Bloodlust")));
        }

        // ---- the two riders together ------------------------------------------

        [Test]
        public void TrampleOnePlusBloodlust_IsTwoExtrasThisTurn_AndBloodlustStaysSpent()
        {
            // Kill 1 -> Trample (cap 1). Kill 2 -> Trample capped, Bloodlust
            // grants and is spent. Kill 3 -> nothing: the turn passes. Then on
            // the NEXT turn Trample is back (per turn) but Bloodlust is not
            // (per fight): kill 4 tramples, kill 5 ends the turn.
            var hero = Hero();
            GiveTrample(hero, 1);
            var (session, _, encounter) = Fight(hero, KitWith(BloodlustRelic),
                Foe("W0", 1), Foe("W1", 1), Foe("W2", 1), Foe("W3", 1), Foe("W4", 1),
                Foe("Tank", 1000));

            var first = Round(session, () => session.ExecuteAttack(encounter.Enemies[0]));
            Assert.AreEqual(0, EnemyTurnsIn(first), "kill 1: Trample");
            Assert.IsTrue(MessagesIn(first).Any(m => m.Contains("tramples")));
            Assert.IsFalse(MessagesIn(first).Any(m => m.Contains("Bloodlust")));

            var second = Round(session, () => session.ExecuteAttack(encounter.Enemies[1]));
            Assert.AreEqual(0, EnemyTurnsIn(second), "kill 2: Trample capped, Bloodlust grants");
            Assert.IsTrue(MessagesIn(second).Any(m => m.Contains("Bloodlust")));
            Assert.IsFalse(MessagesIn(second).Any(m => m.Contains("tramples")));

            var third = Round(session, () => session.ExecuteAttack(encounter.Enemies[2]));
            Assert.IsFalse(encounter.Enemies[2].IsAlive);
            Assert.GreaterOrEqual(EnemyTurnsIn(third), 1, "kill 3: two extras taken, the turn passes");
            Assert.IsFalse(MessagesIn(third).Any(m => m.Contains("Bloodlust") || m.Contains("tramples")));

            Assert.AreSame(hero, encounter.Current, "fixture: the turn came back to the hero");
            var fourth = Round(session, () => session.ExecuteAttack(encounter.Enemies[3]));
            Assert.AreEqual(0, EnemyTurnsIn(fourth), "kill 4, new turn: Trample's cap is per turn");
            Assert.IsTrue(MessagesIn(fourth).Any(m => m.Contains("tramples")));

            var fifth = Round(session, () => session.ExecuteAttack(encounter.Enemies[4]));
            Assert.IsFalse(encounter.Enemies[4].IsAlive);
            Assert.GreaterOrEqual(EnemyTurnsIn(fifth), 1, "kill 5: Bloodlust is spent for the fight");
            Assert.IsFalse(MessagesIn(fifth).Any(m => m.Contains("Bloodlust") || m.Contains("tramples")));
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
            // From the other side: turn-start lines are decided after the
            // beat committed. Written straight to the immediate list they
            // would land OLDER than the blow they follow.
            //
            // A poison tick opens its own beat, precisely so the stage can
            // play the hurt pose, the tinted flash and the number that the
            // log line has always described on its own. What this test
            // exists for: nothing reaches the immediate list, so no
            // turn-start line can land older than the blow it follows.
            var hero = Hero();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 7, 3);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            var beats = session.DrainBeats();
            Assert.AreEqual(2, beats.Count, "the swing, then the poison tick that opened the next turn");
            Assert.IsTrue(beats[1].Messages.Any(m => m.Contains("poison damage")),
                "the tick's own line belongs to the tick's own beat");
            CollectionAssert.IsEmpty(session.DrainImmediateMessages());
        }

        // ---- a tick the stage can see ------------------------------------------
        //
        // A damage-over-time tick was log-only until now: it went through
        // StatusEffects.Tick and RecordUnattributedDamage without ever
        // opening a beat, so the victim played no hurt pose, no flash and no
        // damage number. These pin the shape of the beat it opens instead --
        // the shape is what carries the whole reaction, because playback has
        // exactly one path and the tick now uses it.

        [Test]
        public void APoisonTickRecordsABeatAimedAtItsVictim()
        {
            var hero = Hero();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 7, 3);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            var tick = session.DrainBeats().Last();

            Assert.AreSame(hero, tick.Target, "the poisoned combatant is what the tick lands on");
            Assert.IsNull(tick.Actor, "nobody is credited for a tick -- see SettleDeath's KillCredit.Nobody");
            Assert.AreEqual(DamageType.Poison, tick.DamageType);
            Assert.AreEqual(7, tick.Amount, "the magnitude, which is what the log line already prints");
            Assert.IsFalse(tick.IsHealing);
            Assert.AreEqual(StageApproach.Hold, tick.Approach, "nothing crosses the stage for a tick");
            Assert.AreEqual(FightSession.Stances.Hurt, tick.Stances[hero]);
        }

        [Test]
        public void APoisonTicksBeatShowsTheHealthGoingDownRatherThanAlreadyGone()
        {
            // The PreSnapshot has to predate the damage the beat describes,
            // or the bar drops the instant the beat opens instead of on the
            // frame the tick lands. StatusEffects.Tick spends the health
            // before anything can decide to record a beat, which is why
            // TickStatuses snapshots ahead of it.
            var hero = Hero();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 7, 3);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);

            int beforeTheSwing = hero.CurrentHealth;
            session.ExecuteAttack(encounter.Enemies[0]);

            var tick = session.DrainBeats().Last();

            Assert.AreEqual(beforeTheSwing, tick.PreSnapshot[hero].Health);
            Assert.AreEqual(beforeTheSwing - 7, tick.Snapshot[hero].Health);
        }

        [Test]
        public void ARegenTickIsItsOwnHealingBeatAndDoesNotFlinchTheHolder()
        {
            // The heal flash falls out of the same recorder: FlashOne branches
            // on IsHealing, and RecoilOne/Punch both skip a target that is its
            // own actor -- which is why a regen tick names the holder as the
            // actor where a poison tick names nobody.
            var hero = Hero();
            hero.CurrentHealth = 100;
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Regen, 5, 3);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            var tick = session.DrainBeats().Last();

            Assert.IsTrue(tick.IsHealing);
            Assert.AreEqual(5, tick.Amount);
            Assert.AreSame(hero, tick.Target);
            Assert.AreSame(hero, tick.Actor, "a body does not flinch away from its own mending");
            CollectionAssert.IsEmpty(tick.Stances, "there is no being-mended drawing to wear");
        }

        [Test]
        public void ATickWithNothingToReportOpensNoBeatAtAll()
        {
            // The control. Every other fight in the game must be beat-for-beat
            // what it was before the tick learned to record one, or this pass
            // changed the pacing of combat rather than the clarity of poison.
            var hero = Hero();
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 1000));
            encounter.GrantExtraTurn(hero);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.AreEqual(1, session.DrainBeats().Count, "the swing, and nothing invented after it");
        }

        // ---- WHAT AN EXTRA TURN RE-PAYS ----------------------------------------
        //
        // A bonus action is the SAME turn and re-pays nothing. GrantTurnStart
        // must not treat "a fresh actor's turn" and "the same actor's extra
        // turn" identically: AdvanceAfterAction calls GrantExtraTurn BEFORE
        // AdvanceTurn, so a Trample or a Bloodlust puts the same actor back
        // on Current, and running the whole thirteen-step block for them a
        // second time would double the poison tick, every
        // non-IsSpentByTheTurn duration countdown, the cooldown countdown, and
        // both duration clocks (TickTransform, TickPhoenixEgg).
        //
        // Exactly two of those thirteen -- _locks.ResetTurn and
        // TickPrimaryPool -- are what an ACTION pays for, and those two (plus
        // the two recomputes that read them) are the whole of ReopenTurnFor.
        // Every clock stays with OpenTurnFor.
        //
        // The three cases below are the reproduction. The one that was
        // never a balance question is the third: talents.json ships
        // sheep_ram_trample_3 ("A kill does not cost you the turn") as a
        // PREREQUISITE of sheep_ram_converge (Black Ram Mode, "7 wool for
        // three turns"), so nobody can own the form without owning the rider
        // that ate it -- and the form's splash makes the kill that ate it more
        // likely. A three-turn form that reliably lasted two is a content row
        // lying about itself.
        //
        // AFreshActorsTurnStillOpensWithTheFullTickBlock, at the bottom, is
        // the control: ReopenTurnFor is narrower than OpenTurnFor, and that
        // test is what says OpenTurnFor was not narrowed alongside it.

        [Test]
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

            // THE FOURTH ACTION IS THE ROUND. Fight() builds the session but
            // does not start it, so the hero's opening turn is never opened by
            // anything and a cap of 3 against three 1-HP foes never reaches a
            // second boundary either -- three kills, three extra actions, and
            // no turn start at all. Swinging at the tank kills nothing, so the
            // turn genuinely passes, the tank acts, and the hero's NEXT turn
            // opens: one boundary, and the one poison tick this counts.
            session.ExecuteAttack(encounter.Enemies[3]);

            int poisonLines = AllMessages(session).Count(m => m.Contains("poison damage"));

            Assert.AreEqual(1, poisonLines,
                "the trampler was poisoned once per extra action, not once per turn");
        }

        [Test]
        public void ATramplersStatusDurationsTickOncePerRound()
        {
            var hero = new CombatantState("Hero", true, 2000, 10, 20, HeroSpeed);
            GiveTrample(hero, 3);
            var (session, _, encounter) = Fight(hero, null,
                Foe("A", 1), Foe("B", 1), Foe("C", 1), Foe("Tank", 100000));

            // Three turns of Shielded, applied before the hero's first action.
            // Through ApplyWard, which is the only door into this status.
            StatusEffects.ApplyWard(hero.Statuses, 50, 3);

            session.ExecuteAttack(encounter.Enemies[0]);
            session.ExecuteAttack(encounter.Enemies[1]);
            session.ExecuteAttack(encounter.Enemies[2]);

            Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Shielded),
                "a 3-turn Shielded expired inside ONE of the hero's rounds");
        }

        [Test]
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

        [Test]
        public void AFreshActorsTurnStillOpensWithTheFullTickBlock()
        {
            // The control for the three above. They all assert an ABSENCE --
            // a tick that did not happen -- and an absence is equally
            // satisfied by deleting the tick outright, so this pins the other
            // side: no rider fires here, every action is a genuine turn
            // boundary, and OpenTurnFor still pays the whole bill.
            var hero = new CombatantState("Hero", true, 2000, 10, 20, HeroSpeed);
            var (session, _, encounter) = Fight(hero, null, Foe("Tank", 100000));

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 10, 99);

            // Nothing dies, so the turn passes to the tank and comes back --
            // one fresh opening of the hero's turn, inside this one call.
            session.ExecuteAttack(encounter.Enemies[0]);

            int poisonLines = AllMessages(session).Count(m => m.Contains("poison damage"));

            Assert.AreEqual(1, poisonLines, "a fresh turn still ticks statuses");
            Assert.AreEqual(98,
                hero.Statuses.First(s => s.Type == StatusEffectType.Poison).TurnsRemaining,
                "a fresh turn still ages durations");
        }
    }
}
