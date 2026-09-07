using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
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
        // graceful-degradation posture and is what `placeholder_brawler`
        // actually ships on (§5).
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
                        return From(asset.Data);
                    }
                }
            }

            return RewardTrackDefinition.Default(characterId);
        }

        // The resolved content record turned into the Domain definition that
        // knows how to read itself.
        //
        // HERE RATHER THAN ON RewardTrackDefinition, and the reason is the
        // package boundary this landed across: ResolvedRewardTrack is
        // Domain/Content and RewardTrackDefinition is Domain/Progression, and
        // P3 built the latter without being allowed to see the former. If a
        // From(ResolvedRewardTrack) later appears in Domain, this is the one
        // call site to delete.
        private static RewardTrackDefinition From(ResolvedRewardTrack track)
        {
            var milestones = new (int Level, TrackEntry Entry)[track.Milestones.Length];
            for (int i = 0; i < track.Milestones.Length; i++)
            {
                var m = track.Milestones[i];
                milestones[i] = (m.Level, new TrackEntry(m.Reward, m.Amount, m.Against,
                    m.SkillId, m.SkillDisplayName, m.ResourceDisplayName));
            }

            var filler = new (TrackEntry Entry, int Count)[track.Filler.Length];
            for (int i = 0; i < track.Filler.Length; i++)
            {
                var f = track.Filler[i];

                // NO SKILL OR RESOURCE CAPTION ON FILLER, because there can be
                // none: UnlockSkill is refused as filler by rule 3, and the
                // signature kinds caption off the character rather than off the
                // row (RewardTrackEntryResolver bakes ResourceDisplayName onto
                // milestones only, which is where the resolver puts it).
                filler[i] = (new TrackEntry(f.Reward, f.Amount, f.Against), f.Count);
            }

            return RewardTrackDefinition.Build(track.CharacterId, milestones, filler);
        }
    }
}
