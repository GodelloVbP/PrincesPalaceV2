using System;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    public class TurnOrderTests
    {
        [Test]
        public void Start_WithNoCombatants_Throws()
        {
            var order = new TurnOrder<string>();
            Assert.Throws<InvalidOperationException>(() => order.Start());
        }

        [Test]
        public void Current_BeforeStart_Throws()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Alice", 10);
            Assert.Throws<InvalidOperationException>(() => { var _ = order.Current; });
        }

        [Test]
        public void Advance_BeforeStart_Throws()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Alice", 10);
            Assert.Throws<InvalidOperationException>(() => order.Advance());
        }

        [Test]
        public void Start_OrdersByInitiativeDescending()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Slow", 5);
            order.AddCombatant("Fast", 20);
            order.AddCombatant("Medium", 10);

            order.Start();

            Assert.AreEqual("Fast", order.Current);
            Assert.AreEqual("Medium", order.Advance());
            Assert.AreEqual("Slow", order.Advance());
        }

        [Test]
        public void EqualInitiative_PreservesInsertionOrder()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("First", 10);
            order.AddCombatant("Second", 10);
            order.AddCombatant("Third", 10);

            order.Start();

            Assert.AreEqual("First", order.Current);
            Assert.AreEqual("Second", order.Advance());
            Assert.AreEqual("Third", order.Advance());
        }

        [Test]
        public void Advance_WrapsToNextRound()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("A", 10);
            order.AddCombatant("B", 5);
            order.Start();

            Assert.AreEqual(1, order.Round);
            order.Advance(); // B
            Assert.AreEqual("A", order.Advance()); // wraps
            Assert.AreEqual(2, order.Round);
        }

        [Test]
        public void AddCombatant_NullActor_Throws()
        {
            var order = new TurnOrder<string>();
            Assert.Throws<ArgumentNullException>(() => order.AddCombatant(null, 10));
        }

        [Test]
        public void AddCombatant_DuplicateActor_Throws()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Alice", 10);
            Assert.Throws<InvalidOperationException>(() => order.AddCombatant("Alice", 5));
        }

        [Test]
        public void RemoveCombatant_NotCurrent_DoesNotChangeCurrent()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Fast", 20);
            order.AddCombatant("Slow", 5);
            order.Start();

            order.RemoveCombatant("Slow");

            Assert.AreEqual("Fast", order.Current);
            Assert.AreEqual(1, order.Count);
        }

        [Test]
        public void RemoveCombatant_CurrentActor_HandsOffToNextWithoutResettingRound()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("A", 30);
            order.AddCombatant("B", 20);
            order.AddCombatant("C", 10);
            order.Start();

            Assert.AreEqual("A", order.Current);
            order.RemoveCombatant("A");

            Assert.AreEqual("B", order.Current);
            Assert.AreEqual(1, order.Round);
        }

        [Test]
        public void RemoveCombatant_CurrentActorWasLast_WrapsToNextRound()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("A", 30);
            order.AddCombatant("B", 20);
            order.Start();
            order.Advance(); // now on B (last)

            order.RemoveCombatant("B");

            Assert.AreEqual("A", order.Current);
            Assert.AreEqual(2, order.Round);
        }

        [Test]
        public void RemoveCombatant_CurrentActorMidList_HandsOffBySchedule_NotByListPosition()
        {
            // The entry list is sorted by INITIATIVE, which under charge
            // scheduling says nothing about who is next. B dies on its own
            // turn with A (fast, rate 1) and C (slow, speed 1 -> MinRate
            // 0.35) waiting; C sits at B's old list index, but A crosses the
            // threshold first. The hand-off must be what B's turn ending
            // normally (Advance) would have produced, not list[index].
            var order = new TurnOrder<string>();
            order.AddCombatant("A", 30);
            order.AddCombatant("B", 20);
            order.AddCombatant("C", 10);
            order.SetSpeed("C", 1);
            order.Start();                       // A acts: A -70, B 20, C 10

            Assert.AreEqual("B", order.Advance()); // 80 ticks: A 10, B 0, C 38
            Assert.AreEqual(10f, order.ChargeOf("A"), 0.01f);
            Assert.AreEqual(38f, order.ChargeOf("C"), 0.01f);

            order.RemoveCombatant("B");

            // A needs 90 ticks to reach 100, C needs (100-38)/0.35 = 177.
            // After A spends its turn: A 0, C 38 + 90*0.35 = 69.5.
            Assert.AreEqual("A", order.Current);
            Assert.AreEqual(0f, order.ChargeOf("A"), 0.01f);
            Assert.AreEqual(69.5f, order.ChargeOf("C"), 0.01f);
            // 80 + 90 = 170 baseline ticks: one round boundary crossed.
            Assert.AreEqual(2, order.Round);
        }

        [Test]
        public void RemoveCombatant_LastRemainingCombatant_CurrentThenThrows()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Solo", 10);
            order.Start();

            order.RemoveCombatant("Solo");

            Assert.AreEqual(0, order.Count);
            Assert.Throws<InvalidOperationException>(() => { var _ = order.Current; });
        }

        [Test]
        public void RemoveCombatant_UnknownActor_IsNoOp()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Alice", 10);
            order.Start();

            Assert.DoesNotThrow(() => order.RemoveCombatant("Nobody"));
            Assert.AreEqual("Alice", order.Current);
        }

        [Test]
        public void DelayCurrent_MovesActorToEndAndHandsToNext()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("A", 30);
            order.AddCombatant("B", 20);
            order.AddCombatant("C", 10);
            order.Start();

            Assert.AreEqual("B", order.DelayCurrent(), "DelayCurrent hands off to whoever is now next, same convention as Advance()");
            // Order is now B, C, A for the remainder of this round.
            Assert.AreEqual("C", order.Advance());
            Assert.AreEqual("A", order.Advance());
        }

        [Test]
        public void DelayCurrent_OnlyCombatant_ReturnsSameActor()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Solo", 10);
            order.Start();

            Assert.AreEqual("Solo", order.DelayCurrent());
            Assert.AreEqual("Solo", order.Current);
        }

        [Test]
        public void AddCombatant_MidEncounter_ReinforcementJoinsAtCorrectInitiativeSlot()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("A", 30);
            order.AddCombatant("C", 10);
            order.Start();

            order.AddCombatant("B", 20);

            Assert.AreEqual("A", order.Current);
            Assert.AreEqual("B", order.Advance());
            Assert.AreEqual("C", order.Advance());
        }
    
        // Extra turns are queue business, not driver business. Anything
        // layered above the queue (a flag the caller checks, a re-entrant
        // call) would desynchronise the initiative tracker, which reads this
        // same queue to say who is up next.
        [Test]
        public void GrantExtraTurn_MakesTheSameActorGoAgainBeforeTheQueueMovesOn()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("fast", 10);
            order.AddCombatant("slow", 1);
            order.Start();

            Assert.AreEqual("fast", order.Current);
            Assert.IsTrue(order.GrantExtraTurn("fast"));

            Assert.AreEqual("fast", order.Advance(), "The granted turn should come immediately after the one that earned it.");
            Assert.AreEqual("slow", order.Advance(), "The grant is spent, so the queue resumes.");
        }

        [Test]
        public void GrantExtraTurn_Stacks()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("a", 10);
            order.AddCombatant("b", 1);
            order.Start();

            order.GrantExtraTurn("a");
            order.GrantExtraTurn("a");
            Assert.AreEqual(2, order.PendingExtraTurns("a"));

            Assert.AreEqual("a", order.Advance());
            Assert.AreEqual("a", order.Advance());
            Assert.AreEqual("b", order.Advance());
        }

        [Test]
        public void GrantExtraTurn_ForSomeoneNotInTheOrder_IsRefusedRatherThanSilentlyBanked()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("a", 5);
            order.Start();

            Assert.IsFalse(order.GrantExtraTurn("ghost"));
            Assert.AreEqual(0, order.PendingExtraTurns("ghost"));
        }

        [Test]
        public void RemovingACombatant_DropsItsBankedExtraTurns()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("a", 10);
            order.AddCombatant("b", 1);
            order.Start();

            order.GrantExtraTurn("a");
            order.RemoveCombatant("a");

            Assert.AreEqual(0, order.PendingExtraTurns("a"),
                "A combatant that left the fight must not keep turns waiting for it.");
        }

        // ---- milestone C: the forecast's own definition (plan 1.9) ---------

        [Test]
        public void ProjectPutsTheCurrentActorFirst()
        {
            // THE FACT EVERY USE OF "FORECAST POSITION" TURNS ON, and the one
            // the spell-expansion baseline never wrote down (plan section 7's
            // own list of what the baseline is silent about, item 4). Index 0
            // is the action HAPPENING, not the next one to happen, which is
            // why an advance clips at 1 and why the current actor is never a
            // legal displacement target.
            //
            // AND IT IS NOT MERELY "THE MOST CHARGED ONE FIRST": the current
            // actor has just had a full threshold taken off it, so it is the
            // LEAST charged entry on the board and still leads the list.
            // SimulateForward adds it before the contest starts.
            var order = new TurnOrder<string>();
            order.AddCombatant("Slow", 5);
            order.AddCombatant("Fast", 90);
            order.Start();

            Assert.AreEqual("Fast", order.Current, "fixture: the higher initiative opens");
            Assert.Less(order.ChargeOf("Fast"), order.ChargeOf("Slow"),
                "fixture: the actor holding the turn has spent its charge and is now the lowest");
            Assert.AreEqual("Fast", order.Project(3)[0],
                "index 0 is the current actor, whatever its charge");
            Assert.AreEqual(0, order.ForecastPositionOf("Fast", 3));
        }

        [Test]
        public void ProjectPulled_LeavesEveryRealChargeUnchanged()
        {
            // PREVIEW PURITY FOR THE SCHEDULE (plan 1.13). ProjectPulled runs
            // the REAL displacement rule, which is the whole point of it --
            // the tracker must show where a cast would actually land -- so
            // the only thing standing between a hover and a committed
            // initiative change is that the rule runs on Snapshot()'s copy.
            // Pinned by charge rather than by order, because a mutation that
            // moved an entry without reordering anybody would be invisible to
            // an order assertion and is exactly the kind of bug a preview
            // introduces.
            var order = new TurnOrder<string>();
            order.AddCombatant("Now", 99);
            order.Start();
            order.AddCombatant("High", 80);
            order.AddCombatant("Low", 20);

            float nowBefore = order.ChargeOf("Now");
            float highBefore = order.ChargeOf("High");
            float lowBefore = order.ChargeOf("Low");

            var preview = order.ProjectPulled("Low", 2, 4);

            Assert.IsNotEmpty(preview, "fixture: the preview produced nothing to look at");
            Assert.AreEqual(nowBefore, order.ChargeOf("Now"), "a preview moved the current actor");
            Assert.AreEqual(highBefore, order.ChargeOf("High"), "a preview moved an untargeted entry");
            Assert.AreEqual(lowBefore, order.ChargeOf("Low"), "a preview committed the advance it was previewing");

            // AND IT SHOWED SOMETHING DIFFERENT FROM THE PLAIN FORECAST, or
            // the three assertions above would hold for a preview that had
            // simply forgotten to apply the rule at all.
            CollectionAssert.AreNotEqual(order.Project(4), preview,
                "the preview and the plain forecast agree, so the advance was never simulated");
        }

        [Test]
        public void ProjectPulled_OfTheCurrentActor_IsThePlainForecast()
        {
            // The preview refuses exactly what the operation refuses (1.9
            // rule 2). A tracker that showed the caster's own turn moving --
            // for a cast the session would then turn down -- is worse than no
            // preview at all.
            var order = new TurnOrder<string>();
            order.AddCombatant("Now", 99);
            order.Start();
            order.AddCombatant("Other", 40);

            CollectionAssert.AreEqual(order.Project(4), order.ProjectPulled("Now", 2, 4));
        }

}
}
