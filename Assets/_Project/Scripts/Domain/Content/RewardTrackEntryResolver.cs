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

        // Assembles one context per character from the characters and skills
        // an earlier content-build phase already resolved -- see this
        // class's own header for why SkillDisplayNames is the whole
        // catalogue while Level1DamageTypes (rule 4) stays owner-filtered.
        // ONE SEAM for three call sites that would otherwise each build this
        // map by hand -- ContentBuilder.BuildRewardTracks, and the
        // equivalent map ContentStampIdsTests and RewardTrackContentPinTests
        // each need beside it: a change to what a context needs has one
        // place to update instead of three that could quietly drift apart.
        public static Dictionary<string, RewardTrackCharacterContext> BuildAll(
            IReadOnlyList<ResolvedCharacter> characters, IReadOnlyList<ResolvedSkill> skills)
        {
            var everySkillName = new Dictionary<string, string>();
            foreach (var skill in skills) everySkillName[skill.Id] = skill.DisplayName;

            var contexts = new Dictionary<string, RewardTrackCharacterContext>();

            foreach (var character in characters)
            {
                var ownSkills = skills.Where(s => s.CharacterId == character.Id).ToList();
                var level1Skills = ownSkills.Where(s => s.UnlockLevel <= 1);

                // SkillDamageTypes.AtLevel1 is the one walk -- it also folds
                // in every element a choice skill offers, not just its
                // authored packet, which is what keeps a filler row from
                // being refused for damage a character demonstrably does
                // (Odette's orb deals Fire on demand; its packet alone says
                // only Earth). ContentDatabase.Validation's rule 4 asks the
                // identical question over its own build-time skill list.
                var level1Types = SkillDamageTypes.AtLevel1(character.AttackType, level1Skills);

                contexts[character.Id] = new RewardTrackCharacterContext
                {
                    SortOrder = character.SortOrder,
                    HasSignatureResource = character.HasSignatureResource,
                    SignatureDisplayName = character.SignatureDisplayName,
                    Level1DamageTypes = level1Types,
                    SkillDisplayNames = everySkillName,
                };
            }

            return contexts;
        }
    }

    // Validates reward_tracks.json. Same collected-not-first-only error
    // reporting as SkillEntryResolver/RelicEntryResolver, and the same
    // shape RelicEntryResolver uses for a second, cross-catalogue argument
    // -- here a per-character lookup rather than a flat id set, because
    // each track's rules 4/5 depend on which character it belongs to. See
    // docs/PLAN_REWARD_TRACKS.md §4, "the five validation rules".
    public static class RewardTrackEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawRewardTrackEntry> entries,
            IReadOnlyDictionary<string, RewardTrackCharacterContext> characters,
            out List<ResolvedRewardTrack> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRewardTrack>();
            errors = new List<string>();
            characters ??= new Dictionary<string, RewardTrackCharacterContext>();

            var milestoneLevels = RewardTrack.MilestoneLevels;

            // RULE 2's target: the number of levels the track pays that are
            // NOT one of the twelve milestones. Restated from
            // RewardTrack.MaxLevel - RewardTrack.MilestoneLevels.Length -
            // StartingLevel, never typed as a literal 87.
            int expectedFillerCount = (RewardTrack.MaxLevel - RewardTrack.StartingLevel) - milestoneLevels.Length;

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

            // ---- rule 1, continued: a one-shot capability may appear at most
            // once across the track's milestones. Rule 3 already keeps every
            // one-shot kind out of filler, so a milestone is the only place
            // one can be authored at all -- and HasUnlocked only ever asks
            // "has the FIRST level this reward appears at been reached", so a
            // second Respec or SecondLife milestone would be content no
            // player could ever see paid out twice. UnlockSkill is keyed by
            // skillId rather than by kind: two milestones naming two
            // different skills are two different unlocks, but the same
            // skillId named twice is the same dead-content bug.
            var firstLevelOfCapability = new Dictionary<string, int>();
            foreach (var milestone in resolvedMilestones)
            {
                if (!RewardTrack.IsOneShotCapability(milestone.Reward)) continue;

                string key = milestone.Reward == TrackReward.UnlockSkill
                    ? $"{milestone.Reward}:{milestone.SkillId}"
                    : milestone.Reward.ToString();

                if (firstLevelOfCapability.TryGetValue(key, out int firstLevel))
                {
                    errors.Add($"{label}: {milestone.Reward} is a one-shot capability and appears at both level " +
                               $"{firstLevel} and level {milestone.Level} -- it may only be granted once.");
                }
                else
                {
                    firstLevelOfCapability[key] = milestone.Level;
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

            // RULE 4. P3 has landed TrackReward.ElementalDamagePercent, so
            // this reads the parsed enum value directly rather than the
            // reward's raw string -- see RewardTrackEntryResolverTests'
            // FillerElementalDamageOfAnUnknownElement_IsRejected.
            if (isFiller && reward == TrackReward.ElementalDamagePercent
                         && against.HasValue && !context.Level1DamageTypes.Contains(against.Value))
            {
                error = $"{trackLabel}, {where}: filler {against} damage is not an element this character can deal " +
                        "at level 1 (their own attackType, a damageInstances entry on a skill they own with " +
                        "unlockLevel <= 1, or an element a level-1 skill's own Elements list lets them choose) -- " +
                        "only a MILESTONE may place an element the character has not unlocked yet.";
                return false;
            }

            // RULE 5. Same as rule 4: P3 has landed the four signature-
            // resource kinds, so this reads the parsed enum value directly.
            if (RewardTrack.IsSignatureReward(reward) && !context.HasSignatureResource)
            {
                error = $"{trackLabel}, {where}: {rawReward} is authored on a character with no signature resource.";
                return false;
            }

            // UnlockSkill's skillId, resolved against the WHOLE skill
            // catalogue -- see RewardTrackCharacterContext.SkillDisplayNames
            // for why there is no ownership test here (§3f/§3h).
            string resolvedSkillId = "";
            string skillDisplayName = "";
            if (reward == TrackReward.UnlockSkill)
            {
                if (string.IsNullOrWhiteSpace(skillId) || !context.SkillDisplayNames.TryGetValue(skillId, out skillDisplayName))
                {
                    error = $"{trackLabel}, {where}: UnlockSkill names skillId '{skillId}', which is not a skill " +
                            "in the catalogue.";
                    return false;
                }

                resolvedSkillId = skillId;
            }

            string resourceDisplayName = RewardTrack.IsSignatureReward(reward)
                ? (string.IsNullOrWhiteSpace(context.SignatureDisplayName) ? "SIGNATURE" : context.SignatureDisplayName)
                : "";

            core = new ResolvedEntryCore(reward, amount, against, resolvedSkillId, skillDisplayName, resourceDisplayName);
            error = null;
            return true;
        }
    }
}
