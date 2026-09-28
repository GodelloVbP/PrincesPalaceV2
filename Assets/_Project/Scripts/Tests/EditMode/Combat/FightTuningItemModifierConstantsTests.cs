using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // Pins the four Rift-affix tuning
    // constants in FightTuning that every OTHER test in the suite only ever
    // reads back through the FightTuning.X symbol itself (Assert.AreEqual(
    // FightTuning.X, actual) is a real regression guard against the WIRING
    // breaking, but proves nothing about the NUMBER -- change the constant
    // and every one of those call sites silently retunes with it, no test
    // fails, no test says why).
    //
    // These four are that missing tripwire: a bare literal on the left,
    // FightTuning's own symbol on the right (CLAUDE.md gotcha 5 -- never let
    // a test recompute a production value; a raw literal is not a
    // recomputation, it is the one number the test is FOR). If a designer
    // retunes one of these, this is the file that turns red and says which
    // number moved.
    //
    // The OTHER Rift constants (ModifierMagnitude's TierMultiplier/
    // RiftMultiplier curve, RunicWardConversionRate, ChilledOnHitSpeedPercent)
    // already carry a literal pin elsewhere -- ModifierMagnitudeTests,
    // ItemModifierRunicHookTests, ChilledStatusTests respectively -- so are
    // deliberately not duplicated here.
    public class FightTuningItemModifierConstantsTests
    {
        [Test]
        public void ModifierPushBackSlots_Pin()
        {
            // Hardened's on-hit push: how many turn-order slots later the
            // target lands. See FightTuning's own comment for why this
            // matches TurnOrder.PushBack's unit rather than a percent.
            Assert.AreEqual(1, FightTuning.ModifierPushBackSlots);
        }

        // RunicWardPointsCap_Pin IS GONE, and so is the cap: unlimited
        // stacking is the design, relic wards included, so there is no
        // ceiling left to pin. The
        // conversion RATE is still a constant and is still pinned above;
        // ItemModifierRunicHookTests now asserts the absence, which is the
        // half a deleted constant cannot cover on its own.

        [Test]
        public void ChilledOnHitTurns_Pin()
        {
            // Frosty's on-hit Chilled duration. ChilledStatusTests already
            // pins ChilledOnHitSpeedPercent's magnitude via a hand-derived
            // speed literal; the duration side of that same proc was only
            // ever checked against FightTuning.ChilledOnHitTurns itself.
            //
            // ONE, not the 2 it read until plan D1. Chilled moved to the
            // turn-end clock, where 1 affects exactly the turn the old
            // turn-start 2 actually affected -- the number changed so the
            // behaviour would not, and StatusDurationMigrationTests is where
            // that equivalence is pinned.
            Assert.AreEqual(1, FightTuning.ChilledOnHitTurns);
        }

        [Test]
        public void RootOnHitTurns_Pin()
        {
            // Sylvan's on-hit Rooted duration -- Rooted's only tunable
            // number, since the chance itself lives in modifiers.json and
            // Rooted carries no Magnitude of its own to author.
            //
            // ONE since plan D1 -- see ChilledOnHitTurns_Pin just above.
            Assert.AreEqual(1, FightTuning.RootOnHitTurns);
        }
    }
}
