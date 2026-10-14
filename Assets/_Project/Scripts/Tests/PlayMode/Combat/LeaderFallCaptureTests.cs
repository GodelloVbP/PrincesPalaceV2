using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // The boss falls with two anchors standing: a series of frames across the
    // moment the Reckoning waits for, so the followers can be seen fading out
    // rather than vanishing or standing. Pictures, not assertions
    // (FightSettlementTests pins the behaviour).
    //
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.LeaderFallCaptureTests
    public class LeaderFallCaptureTests
    {
        private FightController _fight;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightSceneFixture.QuietForReuse(_fight);
            SharedScene.AfterTest();
        }

        [UnityTest]
        public IEnumerator CaptureTheFollowersDyingWithTheBoss()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 " +
                              "-Filter PrincesPalace.PlayModeTests.LeaderFallCaptureTests");
            }

            yield return SharedScene.EnsureFight();
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            var anchorIds = PreviewFight.EnemiesWithArt(3);
            string bossId = Content.ContentDatabase.Enemies.First(e => e.Data.IsBoss).id;
            var anchors = anchorIds.Take(2)
                .Select((id, i) => new HeartAnchors.Anchor(100 + i, i, id, 10 - i, PrizeKind.None)).ToList();

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            var built = FightEncounterAdapter.Build(new List<string> { "sheep" }, new List<string> { bossId },
                new SeededRandom(7), isBoss: true, heartAnchors: anchors);
            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Boss);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return WaitReal(0.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .First(c => c.isRootCanvas);
            string dir = CaptureOutput.LabelDir("leader_fall");
            Directory.CreateDirectory(dir);
            int frame = 0;

            void Shoot(string label) =>
                CanvasCapture.RenderToFile(canvas, Path.Combine(dir, $"{frame++:00}_{label}.png"), 1920, 1080);

            Shoot("before");

            var session = built.Session;
            session.Encounter.Leader.CurrentHealth = 0;
            Assert.IsTrue(session.ExecuteAttack(session.Encounter.Enemies[0]), "the swing was refused");
            typeof(FightController).GetMethod("AfterResolution",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_fight, null);

            for (int i = 0; i < 14; i++)
            {
                yield return WaitReal(0.2f);
                Shoot($"t{(i + 1) * 200}ms");
            }

            Assert.AreEqual(0, session.Encounter.LivingEnemies.Count(), "a follower is still standing");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
        }
    }
}
