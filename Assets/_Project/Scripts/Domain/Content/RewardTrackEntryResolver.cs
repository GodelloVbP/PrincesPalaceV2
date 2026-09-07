using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // What RewardTrackEntryResolver needs to know about one character in
    // order to validate and caption their track -- assembled by
    // ContentBuilder.BuildRewardTracks from the characters and skills it has
    // already resolved earlier in the same build (BuildCharacters runs
    // before BuildSkills, both before BuildRewardTracks), so this resolver
    // never reads characters.json or skills.json itself. See
    // docs/PLAN_REWARD_TRACKS.md §4, "the two display names the captions
    // need" and validation rules 4-5.
    //
    // A character absent from this map (an unknown characterId, or a test
    // that passes none at all) resolves against a blank context -- no
    // signature resource, no level-1 elements beyond none, no skills -- so
    // rules 4/5 and the skillId check refuse anything that needs a lookup,
    // which is the correct answer for a character nothing is known about.
    public sealed class RewardTrackCharacterContext
    {
        public int SortOrder;
        public DamageType AttackType;
        public bool HasSignatureResource;
        public string SignatureDisplayName = "";

        // Every element this character can already deal at level 1: their
        // own AttackType, plus the type of every damageInstances entry on a
        // skill authored to them with unlockLevel <= 1. Rule 4.
        public IReadOnlyCollection<DamageType> Level1DamageTypes = Array.Empty<DamageType>();

        // Every skill id in the whole catalogue, keyed to its own display
        // name -- what UnlockSkill's skillId resolves against, and captions
        // with "LEARN {S}".
        //
        // THE WHOLE CATALOGUE, NOT THIS CHARACTER'S OWN KIT, and that is the
        // rule rather than a convenience (docs/PLAN_REWARD_TRACKS.md §3f/§3h).
        // Every book-only spell in the game is authored characterId "sheep"
        // because Shawn is who they were drafted for, so an ownership test
        // here would refuse Odette's own level-10 Frost Flare. The check this
        // arm makes is the FIRST HALF of the talent system's
        // GrantsSkillId arm and deliberately not its second: the id resolves
        // to a real skill, and nothing about whose it is. The track
        // definition is per-character, so it has already said whose skill
        // this is.
        public IReadOnlyDictionary<string, string> SkillDisplayNames = new Dictionary<string, string>();
    }

    // Validates reward_tracks.json. Same collected-not-first-only error
    // reporting as SkillEntryResolver/RelicEntryResolver, and the same
    // shape RelicEntryResolver uses for a second, cross-catalogue argument
    // -- here a per-character lookup rather than a flat id set, because
    // each track's rules 4/5 depend on which character it belongs to. See
    // docs/PLAN_REWARD_TRACKS.md §4, "the five validation rules".
    public static class RewardTrackEntryResolver
    {
        // The twelve fixed milestone levels every track shares, derived
        // from RewardTrack.IsMilestone rather than restated as a literal
        // list. RewardTrack.MilestoneLevels is not public today --
        // docs/PLAN_REWARD_TRACKS.md's P3, rewriting that file concurrently
        // in a sibling worktree, is what would add such an accessor, and P2
        // must not touch Domain/Progression/RewardTrack.cs (§10 P2/P3's
        // worktree boundary). Deriving over the whole 2..100 range costs
        // nothing at content-build time.
        private static IReadOnlyList<int> MilestoneLevels()
        {
            var levels = new List<int>();
            for (int level = RewardTrack.StartingLevel + 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.IsMilestone(level)) levels.Add(level);
            }

            return levels;
        }

        public static bool TryResolveAll(IReadOnlyList<RawRewardTrackEntry> entries,
            IReadOnlyDictionary<string, RewardTrackCharacterContext> characters,
            out List<ResolvedRewardTrack> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRewardTrack>();
            errors = new List<string>();
            characters ??= new Dictionary<string, RewardTrackCharacterContext>();

            var milestoneLevels = MilestoneLevels();

            // RULE 2's target: the number of levels the track pays that are
            // NOT one of the twelve milestones. Restated from
            // RewardTrack.MaxLevel - MilestoneLevels().Count - StartingLevel,
            // never typed as a literal 87.
            int expectedFillerCount = (RewardTrack.MaxLevel - RewardTrack.StartingLevel) - milestoneLevels.Count;

            for (int i = 0; i < entries.Count; i++)
            {
                var raw = entries[i];
                string label = string.IsNullOrWhiteSpace(raw?.characterId)
                    ? $"reward_tracks.json entry #{i + 1}"
                    : $"reward track '{raw.characterId}'";

                if (TryResolveOne(raw, label, milestoneLevels, expectedFillerCount, characters, out var single, out var trackErrors))
                {
                    resolved.Add(single);
                }
                else
                {
                    errors.AddRange(trackErrors);
                }
            }

            foreach (string duplicateId in resolved.GroupBy(t => t.CharacterId).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate reward track for character '{duplicateId}' -- every character may have at most one track.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawRewardTrackEntry raw, string label, IReadOnlyList<int> milestoneLevels,
            int expectedFillerCount, IReadOnlyDictionary<string, RewardTrackCharacterContext> characters,
            out ResolvedRewardTrack track, out List<string> errors)
        {
            track = null;
            errors = new List<string>();

            if (raw == null || string.IsNullOrWhiteSpace(raw.characterId))
            {
                errors.Add($"{label}: characterId is required.");
                return false;
            }

            if (!characters.TryGetValue(raw.characterId, out var context) || context == null)
            {
                context = new RewardTrackCharacterContext();
            }

            // ---- rule 1: every one of the twelve milestone levels carries exactly one entry, and no entry names a non-milestone level ----
            var byLevel = new Dictionary<int, RawTrackMilestone>();
            foreach (var m in raw.milestones ?? Array.Empty<RawTrackMilestone>())
            {
                if (m == null) continue;

                if (!milestoneLevels.Contains(m.level))
                {
                    errors.Add($"{label}: milestone entry names level {m.level}, which is not a milestone level " +
                               $"-- the track's twelve are {string.Join(", ", milestoneLevels)}.");
                    continue;
                }

                if (byLevel.ContainsKey(m.level))
                {
                    errors.Add($"{label}: level {m.level} has more than one milestone entry.");
                    continue;
                }

                byLevel[m.level] = m;
            }

            foreach (int level in milestoneLevels)
            {
                if (!byLevel.ContainsKey(level))
                {
                    errors.Add($"{label}: milestone level {level} has no entry -- every one of the twelve must carry exactly one.");
                }
            }

            var resolvedMilestones = new List<ResolvedTrackMilestone>();
            foreach (var pair in byLevel.OrderBy(p => p.Key))
            {
                if (TryResolveEntry(label, $"level {pair.Key}", pair.Value.reward, pair.Value.amount, pair.Value.against,
                        pair.Value.skillId, isFiller: false, context, out var core, out string entryError))
                {
                    resolvedMilestones.Add(new ResolvedTrackMilestone(pair.Key, core.Reward, core.Amount, core.Against,
                        core.SkillId, core.SkillDisplayName, core.ResourceDisplayName));
                }
                else
                {
                    errors.Add(entryError);
                }
            }

            // ---- rule 2: the filler counts sum to exactly the number of filler levels ----
            var rawFiller = raw.filler ?? Array.Empty<RawTrackFiller>();
            int fillerSum = rawFiller.Where(f => f != null).Sum(f => f.count);
            if (fillerSum != expectedFillerCount)
            {
                errors.Add($"{label}: filler counts sum to {fillerSum}, but the track has {expectedFillerCount} " +
                           $"filler levels -- they must sum to exactly {expectedFillerCount}.");
            }

            var resolvedFiller = new List<ResolvedTrackFiller>();
            foreach (var f in rawFiller)
            {
                if (f == null) continue;

                // rule 3 (a one-shot reward as filler) is checked inside
                // TryResolveEntry, along with rules 4/5 -- filler and
                // milestone entries share one validation path so a change
                // to a rule cannot apply to one and not the other by
                // accident.
                if (TryResolveEntry(label, "a filler row", f.reward, f.amount, f.against, null, isFiller: true,
                        context, out var core, out string entryError))
                {
                    resolvedFiller.Add(new ResolvedTrackFiller(core.Reward, core.Amount, core.Against, f.count,
                        core.ResourceDisplayName));
                }
                else
                {
                    errors.Add(entryError);
                }
            }

            if (errors.Count > 0) return false;

            track = new ResolvedRewardTrack(raw.characterId, resolvedMilestones.ToArray(), resolvedFiller.ToArray(), context.SortOrder);
            return true;
        }

        // The reward-kind-independent core of one line -- reward, amount,
        // against, skillId/skillDisplayName, resourceDisplayName -- shared
        // by a milestone entry (which also carries a level, added by the
        // caller) and a filler row (which also carries a count).
        private readonly struct ResolvedEntryCore
        {
            public readonly TrackReward Reward;
            public readonly int Amount;
            public readonly DamageType? Against;
            public readonly string SkillId;
            public readonly string SkillDisplayName;
            public readonly string ResourceDisplayName;

            public ResolvedEntryCore(TrackReward reward, int amount, DamageType? against, string skillId,
                string skillDisplayName, string resourceDisplayName)
            {
                Reward = reward;
                Amount = amount;
                Against = against;
                SkillId = skillId;
                SkillDisplayName = skillDisplayName;
                ResourceDisplayName = resourceDisplayName;
            }
        }

        private static bool TryResolveEntry(string trackLabel, string where, string rawReward, int amount,
            string rawAgainst, string skillId, bool isFiller, RewardTrackCharacterContext context,
            out ResolvedEntryCore core, out string error)
        {
            core = default;

            if (!Enum.TryParse<TrackReward>(rawReward, ignoreCase: true, out var reward) || reward == TrackReward.None)
            {
                error = $"{trackLabel}, {where}: reward '{rawReward}' is not a known TrackReward.";
                return false;
            }

            // RULE 3. The four kinds RewardTrack.IsOneShotCapability names --
            // Respec, SecondLife, SignatureAbsorbs, UnlockSkill.
            //
            // THIS ASKED IsUnlock UNTIL P4, which was the same four while the
            // grants were StatPoint/MaxHealth/Favor/ExpPermille and became
            // "everything but StatPoint" the moment the one-grant model landed
            // -- so it refused the MaxHealth filler row both shipped tracks
            // author. The rule was always about a capability whose LEVEL would
            // otherwise be computed, not about the grant/unlock split, and it
            // now says so in one place.
            if (isFiller && RewardTrack.IsOneShotCapability(reward))
            {
                error = $"{trackLabel}, {where}: {reward} is a one-shot capability and cannot appear as filler -- only grants may.";
                return false;
            }

            DamageType? against = null;
            if (!string.IsNullOrWhiteSpace(rawAgainst))
            {
                if (!Enum.TryParse<DamageType>(rawAgainst, ignoreCase: true, out var parsedAgainst))
                {
                    error = $"{trackLabel}, {where}: against '{rawAgainst}' is not a known DamageType.";
                    return false;
                }

                against = parsedAgainst;
            }

            // RULE 4. Keyed on the enum member NAME rather than
            // TrackReward.ElementalDamagePercent -- that member does not
            // exist yet (P3 adds it), so referencing it here would not
            // compile. Because of that, Enum.TryParse above already refuses
            // any entry naming it, and this branch is unreachable until P3
            // lands: left in place, string-keyed, so the rule is correct the
            // day the member exists rather than a second thing to remember
            // to add then. See RewardTrackEntryResolverTests' two [Ignore]d
            // tests for the behaviour this is meant to produce.
            if (isFiller && string.Equals(rawReward, "ElementalDamagePercent", StringComparison.OrdinalIgnoreCase)
                         && against.HasValue && !context.Level1DamageTypes.Contains(against.Value))
            {
                error = $"{trackLabel}, {where}: filler {against} damage is not an element this character can deal " +
                        "at level 1 (their own attackType, or a damageInstances entry on a skill they own with " +
                        "unlockLevel <= 1) -- only a MILESTONE may place an element the character has not unlocked yet.";
                return false;
            }

            // RULE 5. Same status as rule 4: the four signature-resource
            // kinds do not exist on TrackReward yet, so this is unreachable
            // until P3 adds them.
            bool isSignatureReward = string.Equals(rawReward, "SignatureCapacity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawReward, "SignatureGainPerTurn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawReward, "SignatureGainOnDamageTaken", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawReward, "SignatureAbsorbs", StringComparison.OrdinalIgnoreCase);

            if (isSignatureReward && !context.HasSignatureResource)
            {
                error = $"{trackLabel}, {where}: {rawReward} is authored on a character with no signature resource.";
                return false;
            }

            // UnlockSkill's skillId, resolved against the WHOLE skill
            // catalogue -- see RewardTrackCharacterContext.SkillDisplayNames
            // for why there is no ownership test here (§3f/§3h).
            string resolvedSkillId = "";
            string skillDisplayName = "";
            if (string.Equals(rawReward, "UnlockSkill", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(skillId) || !context.SkillDisplayNames.TryGetValue(skillId, out skillDisplayName))
                {
                    error = $"{trackLabel}, {where}: UnlockSkill names skillId '{skillId}', which is not a skill " +
                            "in the catalogue.";
                    return false;
                }

                resolvedSkillId = skillId;
            }

            string resourceDisplayName = isSignatureReward
                ? (string.IsNullOrWhiteSpace(context.SignatureDisplayName) ? "SIGNATURE" : context.SignatureDisplayName)
                : "";

            core = new ResolvedEntryCore(reward, amount, against, resolvedSkillId, skillDisplayName, resourceDisplayName);
            error = null;
            return true;
        }
    }
}
