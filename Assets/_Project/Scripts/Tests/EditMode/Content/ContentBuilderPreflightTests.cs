using System.IO;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // ContentBuilder is Editor code these tests cannot call, so this pins the
    // ONE ordering that keeps a refused build harmless: the muted preflight
    // pass runs and is checked before RecreateFolder deletes Resources/Content
    // (a build that failed after the delete regenerated 63 .asset.meta GUIDs).
    // The behavioural proof is a content build with an invalid entry leaving
    // `git status` clean under Resources/Content.
    public class ContentBuilderPreflightTests
    {
        private static string Source()
        {
            string dir = Directory.GetCurrentDirectory();
            while (dir != null && !File.Exists(Path.Combine(dir, "Assets/_Project/Scripts/Editor/ContentBuilder.cs")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.IsNotNull(dir, "repo root not found from " + Directory.GetCurrentDirectory());
            return File.ReadAllText(Path.Combine(dir, "Assets/_Project/Scripts/Editor/ContentBuilder.cs"));
        }

        [Test]
        public void ThePreflightPassRunsAndIsCheckedBeforeTheContentTreeIsDeleted()
        {
            string source = Source();
            int body = source.IndexOf("public static void BuildDefaultContent()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(body, 0);

            int preflightOn = source.IndexOf("preflight = true;", body, System.StringComparison.Ordinal);
            int refusal = source.IndexOf("refused to build", body, System.StringComparison.Ordinal);
            int delete = source.IndexOf("RecreateFolder(ContentRoot);", body, System.StringComparison.Ordinal);

            Assert.Greater(preflightOn, body, "no preflight pass in BuildDefaultContent");
            Assert.Greater(refusal, preflightOn, "the preflight's failure is not acted on");
            Assert.Greater(delete, refusal, "Resources/Content is deleted before the preflight verdict");
        }

        [Test]
        public void EveryAssetWriteGoesThroughTheMutedChokePoint()
        {
            string source = Source();
            int first = source.IndexOf("AssetDatabase.CreateAsset(", System.StringComparison.Ordinal);
            int last = source.LastIndexOf("AssetDatabase.CreateAsset(", System.StringComparison.Ordinal);
            Assert.AreEqual(first, last, "a second CreateAsset call bypasses the preflight mute");
            Assert.Greater(first, source.IndexOf("if (preflight)", System.StringComparison.Ordinal));
        }

        [Test]
        public void ACollisionIsDetectedAgainstCollectedIdsNotFilesOnDisk()
        {
            // The preflight runs before the old tree is deleted, so a File.Exists
            // probe reads every committed set piece and weapon as a collision.
            foreach (string builder in new[] { "BuildWeapons", "BuildItemSets" })
            {
                string body = Body(Source(), "IReadOnlyCollection<string> " + builder + "(");
                StringAssert.DoesNotContain("File.Exists(assetPath)", body, builder + " probes the old tree for collisions");
                StringAssert.Contains("taken.Add(", body, builder + " does not check the ids collected this run");
            }
        }

        [Test]
        public void ARealIdCollisionFailsTheBuild()
        {
            foreach (string builder in new[] { "BuildWeapons", "BuildItemSets" })
            {
                string body = Body(Source(), "IReadOnlyCollection<string> " + builder + "(");
                int collides = body.IndexOf("collides with an item", System.StringComparison.Ordinal);
                int record = body.IndexOf("RecordFailure(\"" + builder + "\");", collides, System.StringComparison.Ordinal);
                int next = body.IndexOf("CreateInstance<ItemDefinition>", collides, System.StringComparison.Ordinal);
                Assert.Greater(collides, 0, builder);
                Assert.That(record > collides && record < next, builder + " logs the collision without RecordFailure");
            }
        }

        private static string Body(string source, string signature)
        {
            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);
            int end = source.IndexOf("\n    }", start, System.StringComparison.Ordinal);
            return source.Substring(start, end - start);
        }
    }
}
