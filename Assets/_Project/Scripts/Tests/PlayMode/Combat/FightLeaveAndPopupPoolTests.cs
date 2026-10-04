using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Two "never twice / never dropped" rules on the fight screen.
    public class FightLeaveAndPopupPoolTests
    {
        [TearDown]
        public void Restore()
        {
            Navigation.LoadOverride = null;
            TestGlobals.ResetAll();
        }

        [UnityTest]
        public IEnumerator LeavingTheFightTwiceLoadsOneScene()
        {
            yield return FightSceneFixture.LoadFight();
            var fight = Object.FindAnyObjectByType<FightController>();

            var loads = 0;
            Navigation.LoadOverride = _ => loads++;

            fight.LeaveFightForTest();
            fight.LeaveFightForTest();

            Assert.AreEqual(1, loads, "Continue then Cancel must not queue a second scene load");
        }

        [UnityTest]
        public IEnumerator AClaimedPoolGrowsInsteadOfDroppingTheNumber()
        {
            yield return FightSceneFixture.LoadFight();
            var fight = Object.FindAnyObjectByType<FightController>();
            var beats = fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);

            int before = beats.Popups.Count;
            Assert.AreEqual(12, before);

            // 12 claimed at once: an active popup is what IsFree refuses.
            foreach (var popup in beats.Popups) popup.gameObject.SetActive(true);

            var extra = beats.FreePopupForTest();

            Assert.IsNotNull(extra, "a full pool must hand out a popup, not null");
            Assert.IsTrue(extra.IsFree);
            Assert.AreEqual(13, beats.Popups.Count);
            Assert.AreEqual(1, beats.Popups.Count(p => p == extra));
            Assert.AreSame(beats.Popups[0].transform.parent, extra.transform.parent);
        }
    }
}
