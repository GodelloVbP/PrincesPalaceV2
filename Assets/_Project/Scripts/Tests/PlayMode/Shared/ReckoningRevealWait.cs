using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // A Reckoning test that presses a choice waits for the reveal first: a
    // press during a beat or its guard only skips (docs/PLAN_REWARD_SCREENS.md
    // C10), which is the contract, not something a choice test should trip on.
    // It waits out the linger too, so a halo reads at rest.
    internal static class ReckoningRevealWait
    {
        public static IEnumerator UntilChoosable(ReckoningController reckoning, float realSeconds = 20f)
        {
            float until = Time.realtimeSinceStartup + realSeconds;
            while (reckoning != null && !(reckoning.AcceptsChoices && reckoning.RevealSettled) &&
                   Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.IsTrue(reckoning == null || reckoning.AcceptsChoices, "the reveal never reached a choosable row");
        }
    }
}
