using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // WHERE A FINISHED FIGHT GOES (docs/PLAN_BUGS_QA.md C1, T0): the
    // routing is total over FightEndReason, and its precedence is Defeat, then
    // the Reckoning, then Continue, read from the settlement and from whether
    // the fight belonged to a run -- never from live run state.
    public class FightEndRoutingTests
    {
        // T0: an ending added to the enum without a row fails here, not in a
        // player's hands.
        [Test]
        public void EveryFightEndReason_HasExactlyOneRow()
        {
            var reasons = (FightEndReason[])Enum.GetValues(typeof(FightEndReason));
            Assert.AreEqual(4, reasons.Length, "a new ending: give it a row in FightEndRouting.Rows");

            foreach (var reason in reasons)
            {
                Assert.AreEqual(1, FightEndRouting.Rows.Count(r => r.Reason == reason), $"{reason}: rows");
                Assert.AreEqual(reason, FightEndRouting.RowFor(reason).Reason);
            }
        }

        [Test]
        public void AReasonWithNoRow_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FightEndRouting.RowFor((FightEndReason)99));
        }

        [Test]
        public void AFightThatIsNotOver_HasNoScreen()
        {
            Assert.Throws<InvalidOperationException>(() => FightEndRouting.Route(FightEndReason.None, false, true));
        }

        // The whole table, as literals: reason x run ended x belonged to a run.
        [TestCase(FightEndReason.Defeated, false, true, FightEndScreen.Reckoning)]
        [TestCase(FightEndReason.Defeated, false, false, FightEndScreen.Continue)]
        [TestCase(FightEndReason.Survived, false, true, FightEndScreen.Reckoning)]
        [TestCase(FightEndReason.Survived, false, false, FightEndScreen.Continue)]
        [TestCase(FightEndReason.Fell, true, true, FightEndScreen.Defeat)]
        [TestCase(FightEndReason.Fell, false, true, FightEndScreen.Reckoning)]
        [TestCase(FightEndReason.Fell, false, false, FightEndScreen.Continue)]
        public void Routes(FightEndReason reason, bool runEnded, bool belongedToRun, FightEndScreen expected)
        {
            Assert.AreEqual(expected, FightEndRouting.Route(reason, runEnded, belongedToRun));
        }

        // T2's core: the settlement ended the run, which also means RunManager
        // reports no run by the time routing reads anything. The fight still
        // belonged to one, captured when it started; the Defeat screen wins.
        [Test]
        public void ARunEndingLoss_IsTheDefeatScreen_WhateverElseIsTrue()
        {
            Assert.AreEqual(FightEndScreen.Defeat, FightEndRouting.Route(FightEndReason.Fell, true, true));
            Assert.AreEqual(FightEndScreen.Defeat, FightEndRouting.Route(FightEndReason.Fell, true, false));
        }

        [TestCase(FightEndReason.Defeated, FightEnding.Victory)]
        [TestCase(FightEndReason.Survived, FightEnding.Endured)]
        [TestCase(FightEndReason.Fell, FightEnding.Fell)]
        public void TheOutcomeFormNamesTheEnding(FightEndReason reason, FightEnding ending)
        {
            Assert.AreEqual(ending, FightEndRouting.RowFor(reason).Ending);
        }
    }
}
