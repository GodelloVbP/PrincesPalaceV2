using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.PlayModeTests
{
    // Captures the constellations WITH SOMETHING SPENT ON THEM.
    //
    // The Edit-Mode screenshot tool renders the scene at rest, and at rest this
    // screen is 21 unlit stones with no labels, no prices, no collars and an
    // empty panel -- correct, and nothing like what a player ever sees. Every
    // state the handoff describes is a RUNTIME fact painted from the save:
    // which stones are lit, which wear a ready ring, which are ash, what the
    // gate collar reads, what the panel says about the one that is selected.
    //
    // So the only useful picture of this screen is one taken with a save behind
    // it, and this is the fixture that arranges one.
    public class TalentCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        // The save root is a PROCESS-WIDE global, and this fixture points it at
        // a scratch directory so the arranged character cannot land in a real
        // slot. Left flipped it outlives this test and every one after it reads
        // the same scratch save -- which GlobalStateLintTests refuses at build
        // time rather than leaving to be discovered as a mystery failure
        // somewhere else. See TestGlobals.
        [TearDown]
        public void RestoreGlobals()
        {
            TestGlobals.ResetAll();
        }

        [UnityTest]
        public IEnumerator CaptureTheConstellations()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter TalentCaptureTests");
                yield break;
            }

            var root = Path.Combine(Path.GetTempPath(), "pp-talent-shot");
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var talents = Object.FindAnyObjectByType<TalentController>();
            Assert.IsNotNull(talents, "the talent scene has no controller");

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);

            // ENOUGH TO SHOW EVERY STATE AT ONCE, which is the whole reason for
            // arranging it rather than capturing a fresh save. Four tiers lit
            // puts kindled stones at the bottom, ready ones at the frontier,
            // locked ones above them and the gated pair still shut -- and the
            // ember count is deliberately mean, so at least one reachable stone
            // is priced out of reach and draws in the costly material.
            character.level = 20;
            character.embers = 4;
            character.unlockedTalentIds.Clear();

            var tree = ContentDatabase.TalentsFor(character)
                .Where(t => t != null && t.Data.Column == 0 && t.Data.Row <= 6)
                .ToList();

            foreach (var talent in tree) character.unlockedTalentIds.Add(talent.id);

            talents.Refresh();
            yield return null;

            Directory.CreateDirectory(OutputDir);
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "the talent scene has no root canvas");

            CanvasCapture.RenderToFile(
                canvas, Path.Combine(OutputDir, "TalentsSpent.png"), 1920, 1080);

            // AND WITH A STONE PICKED, because the panel is half the screen and
            // it says nothing at all until something is selected. The frontier
            // is where the interesting refusals live.
            int frontier = TalentPage.Frontier(TalentTreeOf(talents), 0,
                new System.Collections.Generic.HashSet<string>(character.unlockedTalentIds))
                .FirstOrDefault();

            var orb = talents.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == $"Orb0_{frontier}");

            if (orb != null)
            {
                orb.onClick.Invoke();
                yield return null;

                CanvasCapture.RenderToFile(
                    canvas, Path.Combine(OutputDir, "TalentsSelected.png"), 1920, 1080);
            }

            // The unwritten path, which the handoff made a STATE rather than an
            // absence: it used to be an empty sky, and it is now a full spire of
            // grey stones and a panel that says so. Worth a picture of its own,
            // because "the third path looks broken" was the report that led to
            // it being drawn at all.
            var next = talents.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "NextPathButton");

            if (next != null)
            {
                next.onClick.Invoke();
                next.onClick.Invoke();

                // WAITED ON, NOT TIMED. A wall-clock wait of SlideSeconds + a
                // margin looked right and was not: batchmode frames do not
                // advance the controller at the rate the coroutine measures,
                // and the first capture caught the sky 72% of the way through
                // its slide with the third constellation still half under the
                // panel. Polling the thing itself cannot be wrong about it.
                var skyRect = talents.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(r => r.name == "TalentSky");

                // Exact equality over several frames. The slide eases out, so
                // its last 2% moves by less than a Mathf.Approximately
                // tolerance while still moving -- only the clamp at progress 1
                // produces bit-identical frames.
                float last = float.NaN;
                int still = 0;

                for (int i = 0; i < 900 && still < 4; i++)
                {
                    still = skyRect.anchoredPosition.x == last && skyRect.localScale.x == 1f
                        ? still + 1
                        : 0;

                    last = skyRect.anchoredPosition.x;
                    yield return null;
                }

                CanvasCapture.RenderToFile(
                    canvas, Path.Combine(OutputDir, "TalentsUnwritten.png"), 1920, 1080);
            }

            FileAssert.Exists(Path.Combine(OutputDir, "TalentsSpent.png"));
        }

        // The controller builds its tree privately and caches it against the
        // character. Rebuilt here rather than reached into, because a test that
        // read the private field would be asserting against the cache rather
        // than against the content.
        private static TalentTree TalentTreeOf(TalentController talents)
        {
            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            var tree = new TalentTree();

            foreach (var talent in ContentDatabase.TalentsFor(character))
            {
                if (talent == null) continue;

                tree.Set(talent.Data.Column, talent.Data.Row, new TalentSlot(
                    talent.id, talent.Data.DisplayName, talent.Data.Description,
                    ContentDatabase.OrbCost(talent), talent.Data.MinSpent));
            }

            return tree;
        }
    }
}
