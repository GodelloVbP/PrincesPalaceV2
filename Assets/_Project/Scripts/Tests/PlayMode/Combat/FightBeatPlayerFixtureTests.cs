using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // THE SEAM StageAnimationTests' own header describes as the bug: every
    // one of Lunge/RecoilOne/Punch used to reach its StageActorAnimator with
    // a GetComponent off a RectTransform a real FightController's SlotFor
    // handed back, so the only way to exercise any of them was to load the
    // whole Fight scene and bind a session to it.
    //
    // FightBeatPlayer.AnimatorFor replaced that GetComponent with a plain
    // delegate, the same shape SlotFor already was -- so this drives a beat
    // through a FightBeatPlayer that was never attached to a FightController
    // at all, with NO SceneManager.LoadSceneAsync anywhere in it. If
    // AnimatorFor regressed back to a GetComponent (or the array it reads
    // stopped being handed to playback), this fails; every other PlayMode
    // test for this layer would still pass, because they all bind a real
    // scene, which still wires AnimatorFor the ordinary way.
    public class FightBeatPlayerFixtureTests
    {
        [UnityTest]
        public IEnumerator RecoilAndPunchReachTheTargetsAnimatorWithNoSceneAtAll()
        {
            var beatPlayerGo = new GameObject("BeatPlayer", typeof(FightBeatPlayer));
            var beatPlayer = beatPlayerGo.GetComponent<FightBeatPlayer>();

            // A single bare slot, standing in for a whole stage: this beat
            // never approaches (StageApproach.Hold), so only the TARGET's
            // animator is ever asked for.
            var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            var targetSlotGo = new GameObject("TargetSlot", typeof(RectTransform), typeof(StageActorAnimator));
            targetSlotGo.transform.SetParent(stage, false);
            var targetRect = (RectTransform)targetSlotGo.transform;
            var targetAnimator = targetSlotGo.GetComponent<StageActorAnimator>();

            yield return null;   // let Awake resolve the rect, same as every other bare fixture in this suite
            targetAnimator.Rehome();

            var attacker = new CombatantState("Attacker", true, 300, 30, 40, 10);
            var victim = new CombatantState("Victim", false, 100, 10, 8, 4);

            // THE SEAM ITSELF: a fixture's own SlotFor/AnimatorFor, handed
            // straight to the player through WireStageForTest rather than
            // through a FightController this test never builds.
            beatPlayer.WireStageForTest(
                combatant => ReferenceEquals(combatant, victim) ? targetRect : null,
                combatant => ReferenceEquals(combatant, victim) ? targetAnimator : null);

            var beat = new CombatBeat
            {
                Actor = attacker,
                Target = victim,
                Amount = 25,
                Approach = StageApproach.Hold,
            };

            bool finished = false;
            beatPlayer.Play(new[] { beat }, () => finished = true);

            // Caught mid-recoil, the same way FightStageVisualTests catches a
            // real scene's target flinching -- except there is no scene here
            // for AnimatorFor to have quietly fallen back to finding through.
            bool moved = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!finished && Time.realtimeSinceStartup < deadline)
            {
                if (!Mathf.Approximately(targetRect.anchoredPosition.x, 0f)) moved = true;
                yield return null;
            }

            Assert.IsTrue(finished, "the beat never finished playing");
            Assert.IsTrue(moved,
                "the target's slot never moved -- AnimatorFor never reached a real StageActorAnimator");
            Assert.AreEqual(0f, targetRect.anchoredPosition.x, 0.01f,
                "the recoil never carried the target back to its mark");

            Object.Destroy(stage.gameObject);
            Object.Destroy(beatPlayerGo);
        }
    }
}
