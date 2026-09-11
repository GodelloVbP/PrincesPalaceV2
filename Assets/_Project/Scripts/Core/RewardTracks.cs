using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // WHICH REWARD TRACK A CHARACTER IS ON -- the one lookup every read site
    // goes through (docs/PLAN_REWARD_TRACKS.md §2's read-site table).
    //
    // Core rather than Domain because it needs ContentDatabase to find the
    // authored track, and Domain cannot see a content type at all
    // (CODE_STANDARDS.md §1). Domain owns the ARITHMETIC over a track
    // (RewardTrackDefinition); this owns only "whose track, and where does it
    // come from".
    //
    // A CACHE, NOT STATE (CODE_STANDARDS.md §7): every definition here is
    // rebuildable from the content it was built out of, so it is keyed by the
    // character id it was built for and dropped wholesale by Reset(), which
    // ContentDatabase.Reset calls beside CharacterPortraits.Reset for exactly
    // the same reason -- a test that installs its own content would otherwise
    // keep answering off the last catalogue's tracks.
    //
    // MISSES ARE CACHED TOO. A character nobody has authored a track for
    // resolves to RewardTrackDefinition.Default(id) and that answer is stored,
    // so the every-frame read sites (ModifierEffects asks once per DamageType,
    // per fight build) do not re-run Build's hundred-entry interleave on every
    // call.
    public static class RewardTracks
    {
        private static readonly Dictionary<string, RewardTrackDefinition> Cache =
            new Dictionary<string, RewardTrackDefinition>();

        // The track a character is on. Never null: a character with no
        // authored track gets the generated default, which is the project's
        // graceful-degradation posture (§5).
        //
        // No SHIPPED character rides that default any more. `bear` did until
        // f432a366 authored him a track; reward_tracks.json now carries all
        // three of sheep, bear and owl. The default's live job is a character
        // added to characters.json before anyone writes his track, and the
        // placeholder rosters the tests stand up.
        public static RewardTrackDefinition For(string characterId)
        {
            string key = characterId ?? "";

            if (Cache.TryGetValue(key, out var cached)) return cached;

            var built = Build(key);
            Cache[key] = built;
            return built;
        }

        public static RewardTrackDefinition For(Character character) =>
            For(character?.definitionId);

        // Drops the cache so the next lookup rebuilds from Resources.
        public static void Reset() => Cache.Clear();

        private static RewardTrackDefinition Build(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId))
            {
                foreach (var asset in ContentDatabase.RewardTrackAssets)
                {
                    if (asset?.Data != null && asset.Data.CharacterId == characterId)
                    {
                        return RewardTrackDefinition.From(asset.Data);
                    }
                }
            }

            return RewardTrackDefinition.Default(characterId);
        }
    }
}
