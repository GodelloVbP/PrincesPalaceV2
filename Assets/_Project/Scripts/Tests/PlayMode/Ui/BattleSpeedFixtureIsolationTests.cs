using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // T8, docs/PLAN_BATTLE_SPEED.md, revision 3 point 1 demoted to exactly
    // this: the shared PlayMode fight-loading helper and TestGlobals.ResetAll
    // both leave FightBeatPlayer.PlayerSpeedSource and .PlayerSpeedMultiplier
    // at the shipped default, independent of whatever a PREVIOUS fixture in
    // this process last did to them. Poisons both first -- the way a fixture
    // that forgot its own teardown would leave them -- so a pass here means
    // the pin actually overwrote something, not that it started clean.
    public class BattleSpeedFixtureIsolationTests
    {
        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        [UnityTest]
        public IEnumerator TheSharedHelperPinsThePace()
        {
            FightBeatPlayer.PlayerSpeedSource = () => 4f;
            FightBeatPlayer.AdoptPlayerSpeed();
            Assert.AreEqual(4f, FightBeatPlayer.PlayerSpeedMultiplier, 0f, "fixture: poisoning did not take");

            yield return FightSceneFixture.LoadFight();

            Assert.AreEqual(1f, FightBeatPlayer.PlayerSpeedSource(), 0f,
                "the shared helper did not re-pin PlayerSpeedSource to its shipped default");
            Assert.AreEqual(1f, FightBeatPlayer.PlayerSpeedMultiplier, 0f,
                "the shared helper re-pinned the source but never adopted it");
        }

        [UnityTest]
        public IEnumerator ResetAllPinsThePaceToo()
        {
            yield return FightSceneFixture.LoadFight();

            FightBeatPlayer.PlayerSpeedSource = () => 4f;
            FightBeatPlayer.AdoptPlayerSpeed();
            Assert.AreEqual(4f, FightBeatPlayer.PlayerSpeedMultiplier, 0f, "fixture: poisoning did not take");

            TestGlobals.ResetAll();

            Assert.AreEqual(1f, FightBeatPlayer.PlayerSpeedSource(), 0f,
                "ResetAll did not restore PlayerSpeedSource to its shipped default");
            Assert.AreEqual(1f, FightBeatPlayer.PlayerSpeedMultiplier, 0f,
                "ResetAll restored the source but never re-adopted it");
        }
    }
}
