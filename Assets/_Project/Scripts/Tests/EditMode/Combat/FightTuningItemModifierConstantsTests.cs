using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // PHASE F, the item-modifier plan: pins the four Rift-affix tuning
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
    // number moved and what it used to be.
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

        [Test]
        public void RunicWardPointsCap_Pin()
        {
            // The hard ceiling on Runic's mana-to-shield conversion, however
            // deep the mana pool. ItemModifierRunicHookTests already proves
            // the cap is ENFORCED; this is the missing proof that the cap
            // ITSELF still reads 20. It read 25 as a PERCENT before wards
            // became shield pools.
            Assert.AreEqual(20, FightTuning.RunicWardPointsCap);
        }

        [Test]
        public void ChilledOnHitTurns_Pin()
        {
            // Frosty's on-hit Chilled duration. ChilledStatusTests already
            // pins ChilledOnHitSpeedPercent's magnitude via a hand-derived
            // speed literal; the duration side of that same proc was only
            // ever checked against FightTuning.ChilledOnHitTurns itself.
            Assert.AreEqual(2, FightTuning.ChilledOnHitTurns);
        }

        [Test]
        public void RootOnHitTurns_Pin()
        {
            // Sylvan's on-hit Rooted duration -- Rooted's only tunable
            // number, since the chance itself lives in modifiers.json and
            // Rooted carries no Magnitude of its own to author.
            Assert.AreEqual(2, FightTuning.RootOnHitTurns);
        }
    }
}
