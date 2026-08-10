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
}
}
