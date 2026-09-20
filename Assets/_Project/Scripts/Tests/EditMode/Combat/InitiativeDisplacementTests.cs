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
    // INITIATIVE DISPLACEMENT (docs/PLAN_SPELL_EXPANSION.md section 1.9).
    //
    // WRITTEN BEFORE THE OPERATION, which the owner's brief asks for by name
    // and which is the reason the four worked examples in 1.9 exist at all:
    // a scheduler rule argued out on paper and then implemented is a rule
    // with two independent statements of itself, and the test is the one
    // that fails when the code drifts.
    //
    // HOW A CHARGE IS SET HERE, since nothing on TurnOrder writes one
    // directly. AddCombatant seeds Charge from `initiative` ONCE THE ORDER
    // HAS STARTED (TurnOrder.AddCombatant: `_started ? SeedCharge(initiative)
    // : 0f`), so the fixture starts the order with the acting combatant alone
    // and then joins everybody else at the charge the example names. The
    // consequence to know: a joiner's INITIATIVE equals its charge here, so
    // the "higher initiative wins a tie" arm is exercised by equal-charge
    // joiners falling back to insertion order instead -- which is the same
    // relative order, and is what 1.9's "equally displaced targets keep their
    // relative order" actually turns on.
    //
    // The one figure the fixture cannot reproduce literally is the CURRENT
    // actor's own charge: Start() hands it the turn and takes a full
    // threshold off, so it sits at (seed - 100) rather than at the small
    // positive number two of the worked examples give it. Nothing in any of
    // the four turns on it -- index 0 is the current actor because
    // SimulateForward puts it there, not because of its charge -- and each
    // test below says so where it matters.
    public class InitiativeDisplacementTests
    {
        private const float CurrentActorsCharge = -1f; // SeedCharge(99) - TurnThreshold

        private static TurnOrder<string> Acting(string current, int speed = 10)
        {
            var order = new TurnOrder<string>();
            order.AddCombatant(current, 99);
            order.SetSpeed(current, speed);
            order.Start();
            Assert.AreEqual(current, order.Current, "fixture: the named actor has to be the one acting");
            return order;
        }

        private static void Join(TurnOrder<string> order, string actor, int charge, int speed)
        {
            order.AddCombatant(actor, charge);
            order.SetSpeed(actor, speed);
            Assert.AreEqual(charge, order.ChargeOf(actor),
                $"fixture: {actor} was meant to join at charge {charge}");
        }

        // ---- (a) equal-speed actors ------------------------------------------

        [Test]
        public void EqualSpeedActors_AdvancingByTwo_LandsSecond()
        {
            // 1.9's worked example (a). A slot moves past one charge LEVEL,
            // not past one entry, so B and C -- tied at 80 -- are jumped
            // together by a single slot and the second slot finds nothing
            // above to clip against.
            var order = Acting("A");
            Join(order, "B", charge: 80, speed: 10);
            Join(order, "C", charge: 80, speed: 10);
            Join(order, "D", charge: 55, speed: 10);

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, order.Project(4),
                "fixture: the forecast before the advance");

            Assert.IsTrue(order.PullForward("D", 2));

            Assert.AreEqual(81f, order.ChargeOf("D"),
                "slot 1 raises D to (least charge strictly above) + 1; slot 2 finds nothing above and clips");
            CollectionAssert.AreEqual(new[] { "A", "D", "B", "C" }, order.Project(4),
                "D moved two forecast positions on one slot, because a slot moves past a level");
        }

        // ---- (b) a fast actor appearing twice ----------------------------------

        [Test]
        public void AFastActorAppearingTwice_IsMeasuredByItsFirstAppearance()
        {
            // 1.9's worked example (b). A legitimately appears twice inside a
            // four-turn window; its POSITION is the first of the two, which
            // is already position 1, so there is nothing for an advance to
            // buy. The refusal that follows from that is the session's
            // (AnActorAlreadyNext_IsRefusedBeforePayment below); what the
            // scheduler owes here is the measurement and a no-op.
            var order = Acting("B");
            Join(order, "A", charge: 60, speed: 40);
            Join(order, "C", charge: 40, speed: 10);

            CollectionAssert.AreEqual(new[] { "B", "A", "C", "A" }, order.Project(4),
                "a rate-2.0 actor earns a second turn before a rate-1.0 one earns its first");
            Assert.AreEqual(1, order.ForecastPositionOf("A", 4),
                "an actor's position is its FIRST appearance, not its last");

            Assert.IsTrue(order.PullForward("A", 2));

            Assert.AreEqual(60f, order.ChargeOf("A"), "nothing is above A, so both slots clip");
            CollectionAssert.AreEqual(new[] { "B", "A", "C", "A" }, order.Project(4));
        }

        [Test]
        public void AnActorAlreadyNext_IsRefusedBeforePayment()
        {
            // 1.9 rule 5, and the SESSION's half of example (b)/(c): a
            // single-target advance with nowhere to go is a board-state
            // refusal of the shape "Shatter with no wards" already is, and it
            // spends no mana, no cooldown and no turn.
            var (session, caster, next, _) = BorrowedMomentFight();

            int manaBefore = caster.PrimaryPool.Current;
            bool cast = session.CastSkill(0, next);

            Assert.IsFalse(cast, "an ally already at forecast position 1 has nowhere to be advanced to");
            Assert.AreEqual(manaBefore, caster.PrimaryPool.Current, "a refused advance spends no mana");
            Assert.AreEqual(0, session.CooldownRemaining(caster, "borrowed_moment"),
                "a refused advance starts no cooldown");
            Assert.AreSame(caster, session.Current, "a refused advance spends no turn");
        }

        // ---- (c) an actor already next ------------------------------------------

        [Test]
        public void AnActorTwoPlacesBack_AdvancingByTwo_ReachesPositionOneAndClips()
        {
            // 1.9's worked example (c). Target index is max(1, p - slots);
            // the first slot already reaches it and the second clips rather
            // than crossing into the current action's index 0.
            var order = Acting("A");
            Join(order, "B", charge: 95, speed: 10);
            Join(order, "C", charge: 40, speed: 10);

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, order.Project(3));
            Assert.AreEqual(2, order.ForecastPositionOf("C", 3));

            Assert.IsTrue(order.PullForward("C", 2));

            Assert.AreEqual(96f, order.ChargeOf("C"));
            CollectionAssert.AreEqual(new[] { "A", "C", "B" }, order.Project(3));
            Assert.AreEqual(1, order.ForecastPositionOf("C", 3),
                "an advance clips at position 1 -- index 0 is the current action and is not a destination");
        }

        // ---- (d) multi-target delay with a death ---------------------------------

        [Test]
        public void AMultiTargetDelayAfterADeath_DisplacesEverySurvivorOnce()
        {
            // 1.9's worked example (d), at the scheduler level: Gale Scythe
            // lands on E1/E2/E3, E2 dies, and the batch runs over the
            // survivors only.
            //
            // THE DEAD ENTRY IS STILL IN THE ORDER. Nothing in this project
            // removes a defeated combatant from TurnOrder -- Project filters
            // it out of what it REPORTS and still simulates it, because it
            // still consumes its turn in the real schedule (Project's own
            // header). The plan's example says "E2 is removed from the
            // order", which is the one factual slip in it; the arithmetic is
            // unaffected either way, because E2's 88 is not STRICTLY below
            // E1's 88 and so was never a level either survivor could fall
            // past. That is why this test asserts E2's charge is untouched
            // rather than asserting it is gone.
            // THE PLAN'S CASTER "A" IS AN ORDINARY ENTRY HERE, at its stated
            // charge of 10, and the actor holding the turn is a separate
            // "Now". The fixture cannot give the CURRENT entry a positive
            // charge (see this class's header), and A's 10 is the floor both
            // of E3's arithmetic and the plan's own answer depend on -- so it
            // is modelled where it can be modelled. Nothing else about the
            // example changes: the charge landscape is {10, 88, 88, 30} and
            // the two survivors fall through it exactly as 1.9 (d) says.
            var order = Acting("Now");
            Join(order, "A", charge: 10, speed: 10);
            Join(order, "E1", charge: 88, speed: 14);
            Join(order, "E2", charge: 88, speed: 9);
            Join(order, "E3", charge: 30, speed: 5);

            int moved = order.PushBackAll(new[] { "E1", "E3" }, 1);

            Assert.AreEqual(2, moved, "both survivors were displaced, exactly once each");
            Assert.AreEqual(29f, order.ChargeOf("E1"),
                "the greatest charge STRICTLY BELOW E1's 88 is E3's 30, so E1 lands at 29");
            Assert.AreEqual(9f, order.ChargeOf("E3"),
                "E3's destination is fixed from the SAME pre-pass board: the greatest charge below its 30 is " +
                "A's 10, not the 29 E1 has just been given");
            Assert.AreEqual(88f, order.ChargeOf("E2"), "the dead entry was never referenced and never moved");
        }

        [Test]
        public void ABatchDelayFixesEveryDestinationBeforeTheFirstOneMoves()
        {
            // 1.9 rule 1, isolated from the death case. Two targets stacked
            // one level apart: applied SEQUENTIALLY, the second would measure
            // itself against the first's new charge and land somewhere else
            // entirely. One forecast means both destinations come off the
            // board as it stood before either moved.
            var order = Acting("A");
            Join(order, "X", charge: 90, speed: 10);
            Join(order, "Y", charge: 50, speed: 10);
            Join(order, "Z", charge: 20, speed: 10);

            order.PushBackAll(new[] { "X", "Y" }, 1);

            Assert.AreEqual(49f, order.ChargeOf("X"), "greatest strictly below 90 is Y's ORIGINAL 50");
            Assert.AreEqual(19f, order.ChargeOf("Y"), "greatest strictly below 50 is Z's 20 -- not X's new 49");
            Assert.AreEqual(20f, order.ChargeOf("Z"), "an untargeted entry does not move");
        }

        [Test]
        public void TwoTargetsTiedAtOneCharge_KeepTheirRelativeOrderAfterABatchDelay()
        {
            // Two entries on the same charge level are displaced to the same
            // destination, so what preserves their order is the scheduler's
            // own tie-break -- initiative, then insertion order -- and not a
            // second rule inside the batch. Pinned because the alternative
            // (nudging the second one an extra point "to keep them apart")
            // is the obvious wrong fix and would silently cost one of them a
            // position.
            var order = Acting("A");
            Join(order, "First", charge: 70, speed: 10);
            Join(order, "Second", charge: 70, speed: 10);
            Join(order, "Floor", charge: 10, speed: 10);

            CollectionAssert.AreEqual(new[] { "A", "First", "Second", "Floor" }, order.Project(4));

            order.PushBackAll(new[] { "First", "Second" }, 1);

            Assert.AreEqual(9f, order.ChargeOf("First"));
            Assert.AreEqual(9f, order.ChargeOf("Second"));
            CollectionAssert.AreEqual(new[] { "A", "Floor", "First", "Second" }, order.Project(4),
                "both fell behind Floor together, and First is still ahead of Second");
        }

        // ---- the named risks ------------------------------------------------------

        [Test]
        public void EveryDisplacement_LeavesTheEntryCountAndTheEntrySetUnchanged()
        {
            // 1.9 rule 7. Neither operation adds or removes an entry; both
            // only write Charge on an entry already in the list. Asserted
            // over BOTH operations and over a batch, because "no duplication
            // or loss" is the kind of invariant that holds for the path
            // somebody tested and not for the other one.
            var order = Acting("A");
            Join(order, "B", charge: 80, speed: 10);
            Join(order, "C", charge: 60, speed: 10);
            Join(order, "D", charge: 40, speed: 10);

            var before = order.Order.OrderBy(a => a).ToList();
            Assert.AreEqual(4, order.Count, "fixture");

            order.PullForward("D", 2);
            order.PushBack("B", 1);
            order.PushBackAll(new[] { "C", "D" }, 1);

            Assert.AreEqual(4, order.Count, "an entry was added or lost by a displacement");
            CollectionAssert.AreEqual(before, order.Order.OrderBy(a => a).ToList(),
                "the SET of entries changed, not merely their charges");
        }

        [Test]
        public void NeitherOperationAcceptsTheCurrentActor()
        {
            // 1.9 rule 2. Index 0 is the action happening right now; moving
            // its entry would either replay it or drop it, and neither is a
            // thing any spell in this plan asks for.
            var order = Acting("A");
            Join(order, "B", charge: 80, speed: 10);

            Assert.IsFalse(order.PullForward("A", 1), "the current actor is not a legal advance target");
            Assert.IsFalse(order.PushBack("A", 1), "the current actor is not a legal delay target");
            Assert.AreEqual(0, order.PushBackAll(new[] { "A" }, 1), "nor through the batch");
            Assert.AreEqual(CurrentActorsCharge, order.ChargeOf("A"), "the current actor's charge was written");
            Assert.AreEqual("A", order.Current);
        }

        [Test]
        public void AnAdvanceGrantsNoExtraTurn_AndLeavesRateAlone()
        {
            // 1.9 rule 3, and the reason PullToFront is left alone rather
            // than reused: crossing the threshold outright buys "a turn and
            // a half" (PullToFront's own header). An advance buys a position.
            var order = Acting("A");
            Join(order, "B", charge: 80, speed: 10);
            Join(order, "C", charge: 40, speed: 40);

            float rateCarrier = order.ChargeOf("C");
            var before = order.Project(8).Count(a => a == "C");

            order.PullForward("C", 3);

            Assert.AreEqual(0, order.PendingExtraTurns("C"), "an advance granted an extra turn");
            Assert.AreEqual(81f, order.ChargeOf("C"),
                "raised above the 80 level and then clipped -- never over the threshold");
            Assert.Less(order.ChargeOf("C"), 100f, "an advance may never cross the turn threshold");
            Assert.AreEqual(before, order.Project(8).Count(a => a == "C"),
                "C acts the same number of times in the window, it just acts sooner -- Rate is untouched");
            Assert.AreNotEqual(rateCarrier, order.ChargeOf("C"), "fixture: the advance did something");
        }

        [Test]
        public void ADelayOfAnActorNotInTheOrder_IsFalseRatherThanAnError()
        {
            // 1.9 rule 6's empty case. The batch runs over survivors, and a
            // survivor list that has gone stale between the forecast and the
            // pass must not throw -- PushBack's existing "returns false for
            // an actor not in the order" is what carries that, and the batch
            // inherits it.
            var order = Acting("A");
            Join(order, "B", charge: 80, speed: 10);

            Assert.IsFalse(order.PushBack("Ghost", 1));
            Assert.IsFalse(order.PullForward("Ghost", 1));
            Assert.AreEqual(1, order.PushBackAll(new[] { "B", "Ghost" }, 1),
                "the living half of the list is still displaced");
            Assert.AreEqual(0, order.PushBackAll(new string[0], 1), "an empty survivor list applies nothing");
        }

        // ---- the session's own refusal fixture -------------------------------------

        // Borrowed Moment (plan 2.7): SkillEffect.Hasten, one OTHER ally,
        // advanceSlots 2. flatAmount/power stay 0 -- it deals no damage and
        // buys a position, which is the whole of it.
        private static ResolvedSkill BorrowedMoment() =>
            new ResolvedSkill("borrowed_moment", "Borrowed Moment", "test fixture", "sheep", 1,
                SkillEffect.Hasten, SkillTargeting.SingleAlly, 8, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 2, advanceSlots: 2);

        // A caster acting now, one ally ALREADY at forecast position 1 and
        // one further back -- the two arms rule 5 has to tell apart.
        private static (FightSession session, CombatantState caster, CombatantState next, CombatantState back)
            BorrowedMomentFight()
        {
            // SPEEDS ARE THE FIXTURE. The caster has to hold the turn, so it
            // is the fastest; "Next" then has to be the one entry between the
            // caster's turn and everything else, which is what makes it
            // forecast position 1 and the refusal arm's subject; "Back" is
            // slow enough that it is unambiguously further along.
            var caster = new CombatantState("Caster", true, 300, 50, 40, 30);
            var next = new CombatantState("Next", true, 300, 30, 20, 20);
            var back = new CombatantState("Back", true, 300, 30, 20, 4);
            var foe = new CombatantState("Foe", false, 5000, 10, 5, 1);

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { BorrowedMoment() }, null, DamageType.Physical),
                new PlayerKit("bear", CharacterRole.Tank, null, null, DamageType.Physical),
                new PlayerKit("owl", CharacterRole.Utility, null, null, DamageType.Arcane),
            };

            var session = new FightSession(new CombatEncounter(new[] { caster, next, back }, new[] { foe }),
                kits, null, new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
            };

            Assert.AreSame(caster, session.Current, "fixture: the caster has to be the one acting");
            Assert.AreSame(next, session.Encounter.UpcomingTurns(2)[1],
                "fixture: 'Next' has to be at forecast position 1 for the refusal arm to mean anything");
            return (session, caster, next, back);
        }

        [Test]
        public void AnAdvanceOnAnAllyFurtherBack_IsPaidForAndMovesThem()
        {
            // The other arm of the same refusal, because a rule that refuses
            // everything passes a refusal test.
            var (session, caster, next, back) = BorrowedMomentFight();

            // READ THROUGH ForecastPositionOf, not off a short window. A
            // slow ally legitimately does not appear in the first four turns
            // at all, and a -1 from a window too small to hold them would
            // read as "position unknown" rather than as "far back" -- which
            // is exactly why CombatEncounter owns the window size rather than
            // leaving each caller to guess one (ForecastWindow's own header).
            int before = session.Encounter.ForecastPositionOf(back);
            Assert.Greater(before, 1, "fixture: 'Back' has to start further back than position 1");

            bool cast = session.CastSkill(0, back);

            Assert.IsTrue(cast, "an ally with somewhere to go is a legal advance target");
            Assert.Less(session.Encounter.ForecastPositionOf(back), before, "the advance moved nobody");
            Assert.AreEqual(0, session.Encounter.PendingExtraTurns(back), "an advance granted an extra turn");
            // STILL IN THE ORDER, AND AT INDEX 0: the advance is not a free
            // action, so the caster's turn ended with it and the ally that
            // was jumped is the one now acting. Asserted as "has a position
            // at all" rather than as a number, because WHICH number is a fact
            // about the turn having advanced and not about the displacement.
            Assert.GreaterOrEqual(session.Encounter.ForecastPositionOf(next), 0,
                "the ally that was jumped fell out of the order entirely");
        }

        [Test]
        public void AnAdvanceIsRefusedForTheCasterThemselves()
        {
            // AllyTargeting excludes the caster from Hasten for the reason it
            // excludes them from the three Gifts: index 0 is the action
            // already happening, so advancing yourself is a cast with no
            // destination at all (1.9 rule 2).
            var (session, caster, _, _) = BorrowedMomentFight();

            Assert.IsFalse(session.CastSkill(0, caster));
            Assert.AreSame(caster, session.Current, "the refusal spent no turn");
        }
    }
}
