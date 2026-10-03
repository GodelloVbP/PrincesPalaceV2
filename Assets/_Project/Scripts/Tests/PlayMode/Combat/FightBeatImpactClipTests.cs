using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // A split hit (two element packets in one cast) staggers its numbers and
    // flashes per packet but requests the beat's impact clip once.
    public class FightBeatImpactClipTests
    {
        private const string ImpactClip = "Audio/Sfx/fixture_impact";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SoundController.ClipObserver = null;
        }

        [UnityTest]
        public IEnumerator ASplitHitRequestsItsImpactClipOnce()
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);
            var player = go.AddComponent<FightBeatPlayer>();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;

            var actor = new CombatantState("Actor", true, 300, 30, 40, 10);
            var victim = new CombatantState("Victim", false, 100, 10, 8, 4);

            var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            _spawned.Add(stage.gameObject);
            var actorSlot = Slot(stage, "ActorSlot");
            var victimSlot = Slot(stage, "VictimSlot");
            player.WireStageForTest(
                c => ReferenceEquals(c, actor) ? actorSlot.rect : ReferenceEquals(c, victim) ? victimSlot.rect : null,
                c => ReferenceEquals(c, actor) ? actorSlot.animator
                    : ReferenceEquals(c, victim) ? victimSlot.animator : null);

            var requested = new List<string>();
            SoundController.ClipObserver = requested.Add;

            var beat = new CombatBeat
            {
                Actor = actor,
                Target = victim,
                Amount = 10,
                Approach = StageApproach.Hold,
                Packets = new[] { new BeatPacket(DamageType.Fire, 6), new BeatPacket(DamageType.Ice, 4) },
            };
            beat.Vfx.sfxPath = ImpactClip;
            Assert.IsNotNull(beat.StaggeredPacketsFor(victim), "the fixture beat is not a split hit");

            bool finished = false;
            player.Play(new[] { beat }, () => finished = true);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(finished, "the beat never finished playing");

            // Past the old replay's delay, so a late second request would be seen.
            float settle = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;

            Assert.AreEqual(1, requested.FindAll(c => c == ImpactClip).Count);
        }

        private static (RectTransform rect, StageActorAnimator animator) Slot(RectTransform stage, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(StageActorAnimator));
            go.transform.SetParent(stage, false);
            return ((RectTransform)go.transform, go.GetComponent<StageActorAnimator>());
        }
    }
}
