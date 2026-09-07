using System.IO;

namespace PrincesPalace.Domain.Tests
{
    // WHERE THE RAW JSON LIVES, AND HOW TO PARSE IT -- shared by every
    // fixture that reads Assets/_Project/ContentData/*.json directly through
    // the real resolvers instead of through a loaded ScriptableObject
    // catalogue (RewardTrackContentPinTests, ContentStampIdsTests).
    //
    // TWO PARSERS, ONE ASSERTION: the Raw*Entry types are plain
    // [Serializable] classes with public fields, which both Unity's
    // JsonUtility and System.Text.Json read the same way -- and neither host
    // has the other's serializer (Unity ships no System.Text.Json; the
    // dotnet host has no engine). ParseFile takes whichever parser its host
    // has rather than excluding these fixtures from the [D] fast loop.
    //
    // Lives in Shared/ for the same reason RepoTree does: a second copy of
    // this trio is the kind of duplication that drifts silently, and did --
    // this is that second copy, promoted after the fact.
    internal static class ContentDataFiles
    {
        internal static string RepoRoot() => RepoTree.Root();

        internal static string DataPath(string file) =>
            Path.Combine(RepoRoot(), "Assets", "_Project", "ContentData", file);

        // The one line that differs between the two hosts. UNITY_5_3_OR_NEWER
        // is defined by every Unity since 5.3 and by nothing else, so the
        // dotnet host takes the other branch without needing a define of its
        // own.
        internal static T ParseFile<T>(string path)
        {
            string json = File.ReadAllText(path);
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(json);
#else
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions
            {
                // Fields, because every Raw*Entry member is one.
                //
                // CASE-SENSITIVE, deliberately, and it is not a preference:
                // JsonUtility matches a JSON key to a field name exactly, so
                // case-insensitivity here would accept files Unity's parser
                // does not. It also breaks outright -- SpellPresentation has
                // both a `anchor` field and an `Anchor` property, which
                // System.Text.Json reports as a name collision the moment
                // names stop being case-sensitive.
                IncludeFields = true,
            });
#endif
        }
    }
}
