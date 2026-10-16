using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // A FILM OF ONE REVEAL (tools/gif.ps1 reveal <id>): the Reckoning opened
    // over the fight on one of the R0 fixtures (ReckoningPageFixtures) and
    // recorded from the wipe's first frame until the row is choosable and the
    // linger has run out, plus a short tail. The feel gate in
    // docs/PLAN_REWARD_SCREENS.md §6 (R4) is judged on these.
    //
    // The Reckoning's clock is unscaled time, which a capture frame does not
    // respect (each one takes as long as the render does), so the film hands
    // the controller a fixed 1/fps per frame through DeltaOverride: frame n is
    // exactly n/fps seconds into the reveal.
    internal static class ReckoningRevealFilm
    {
        public static readonly string[] Ids = { "nocache", "cachetop", "ghosts" };

        private const float TailSeconds = 0.4f;
        private const float CapSeconds = 5f;

        public static IEnumerator Record(string id, int width, int height, CaptureSink sink, CaptureOutcome outcome)
        {
            Assert.Contains(id, Ids, "reveal id '" + id + "' is not one of " + string.Join(", ", Ids));

            string root = Path.Combine(Path.GetTempPath(), "pp-reveal-film-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = true;

            try
            {
                yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var reckoning = UnityEngine.Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
                Assert.IsNotNull(reckoning, "the Reckoning was never wired into the fight scene");

                var pages = PagesFor(id);
                var reward = new CombatReward { GoldGained = 46 };
                reward.Characters.Add(new CharacterReward("shawn", "Shawn", 1, 0, 1, 79, 100, 79, expToNextBefore: 100));

                float step = 1f / CaptureScenarioRunner.Fps;
                ReckoningController.DeltaOverride = () => step;
                reckoning.Show(reward, pages);

                var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(canvas);

                int frame = 0;
                int tail = 0;
                int tailFrames = Mathf.CeilToInt(TailSeconds * CaptureScenarioRunner.Fps);
                int cap = Mathf.CeilToInt(CapSeconds * CaptureScenarioRunner.Fps);
                while (frame < cap && tail < tailFrames)
                {
                    CanvasCapture.RenderToFile(canvas, sink.FramePath(frame), width, height, fitReference: true);
                    yield return null;
                    frame++;
                    tail = reckoning.RevealSettled ? tail + 1 : 0;
                }

                Assert.Less(frame, cap, "the " + id + " reveal was still playing after " + CapSeconds + "s");
                File.WriteAllText(sink.TimingPath,
                    CaptureScenarioRunner.TimingJson("reveal", id, null, CaptureScenarioRunner.Fps, frame, 1f));
                outcome.Frames = frame;
            }
            finally
            {
                ReckoningController.DeltaOverride = null;
                SaveData.TestSquadOfThreeEnabled = null;
                Navigation.Reset();
                SaveSystem.RootOverride = null;
                SaveSlotManager.Forget();
                RunManager.ResetForTests();
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private static IReadOnlyList<PendingPage> PagesFor(string id)
        {
            switch (id)
            {
                case "cachetop": return ReckoningPageFixtures.CacheTopTierPages();
                case "ghosts": return ReckoningPageFixtures.FiveGhostsPages();
                default: return ReckoningPageFixtures.NoCachePage();
            }
        }
    }
}
