using System;
using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // PhaseCount used to be a hand-counted `private const int = 18`, agreeing
    // with BotPhase only until somebody added a member to the enum without
    // also touching this line -- Add() indexes Elapsed/Calls by (int)phase, so
    // a phase at or past a stale count throws on the first measurement, and
    // Report silently omits it below even that. Two things worth pinning
    // separately: that the DERIVATION is actually wired in (compare against
    // Enum.GetValues itself), and that the CURRENT count is what anyone
    // reading this test expects it to be (the literal) -- a member added to
    // BotPhase without thought moves the derived value silently; the literal
    // is what makes that visible here instead of only in a batch report.
    public class BotPhaseTimersTests
    {
        [Test]
        public void PhaseCountIsDerivedFromTheEnumItself()
        {
            Assert.AreEqual(Enum.GetValues(typeof(BotPhase)).Length, BotPhaseTimers.PhaseCountForTest,
                "PhaseCount has drifted from Enum.GetValues(typeof(BotPhase)).Length -- it is meant to BE that value, not merely agree with it today.");
        }

        [Test]
        public void PhaseCountIsEighteenToday()
        {
            Assert.AreEqual(18, BotPhaseTimers.PhaseCountForTest,
                "BotPhase gained or lost a member -- update this pin, and check every array sized off PhaseCount still makes sense.");
        }
    }
}
