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
    // PHASE 3: enough of one skill's own authored shape to validate a
    // SkillCostDelta/SkillFlatDelta entry against it -- see
    // RewardTrackCharacterContext.Skills.
    public sealed class RewardTrackSkillContext
    {
        public readonly string DisplayName;
        public readonly int ManaCost;
        public readonly int ResourceCost;
        public readonly bool HasFlatAmountPath;

        public RewardTrackSkillContext(string displayName, int manaCost, int resourceCost, bool hasFlatAmountPath)
        {
            DisplayName = displayName;
            ManaCost = manaCost;
            ResourceCost = resourceCost;
            HasFlatAmountPath = hasFlatAmountPath;
        }
    }

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

        // PHASE 3: the same whole-catalogue skill list as SkillDisplayNames
        // above, but carrying enough of each skill's own authored shape to
        // validate SkillCostDelta/SkillFlatDelta -- "the resource must be
        // one the skill actually costs", "the skill must have a flatAmount
        // path". A SEPARATE dictionary rather than widening
        // SkillDisplayNames' value type, so every existing fixture that
        // builds a RewardTrackCharacterContext by hand (this file's own
        // tests, RewardTrackContentPinTests) keeps compiling unchanged.
        public IReadOnlyDictionary<string, RewardTrackSkillContext> Skills =
            new Dictionary<string, RewardTrackSkillContext>();

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
            var everySkillContext = new Dictionary<string, RewardTrackSkillContext>();
            foreach (var skill in skills)
            {
                everySkillName[skill.Id] = skill.DisplayName;

                // HasFlatAmountPath: DamageSingle with no damageInstances --
                // SkillFlatDelta's own validation rule, "the skill must have
                // a flatAmount path". A fixed-packet spell's damage comes
                // from DamageInstances instead and never reads flatAmount at
                // all (SkillResolution.Damage), so a SkillFlatDelta on one
                // would author a number nothing ever looks at.
                bool hasFlatAmountPath = skill.Effect == PrincesPalace.Domain.Combat.SkillEffect.DamageSingle && !skill.HasFixedDamage;

                everySkillContext[skill.Id] = new RewardTrackSkillContext(skill.DisplayName, skill.ManaCost,
                    skill.ResourceCost, hasFlatAmountPath);
            }

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
                    Skills = everySkillContext,
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
                        pair.Value.skillId, pair.Value.resource, pair.Value.identityKind, pair.Value.value,
                        isFiller: false, context, out var core, out string entryError))
                {
                    resolvedMilestones.Add(new ResolvedTrackMilestone(pair.Key, core.Reward, core.Amount, core.Against,
                        core.SkillId, core.SkillDisplayName, core.ResourceDisplayName, core.Resource, core.IdentityKind,
                        core.IdentityValue));
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
                if (TryResolveEntry(label, "a filler row", f.reward, f.amount, f.against, null, null, null, null,
                        isFiller: true, context, out var core, out string entryError))
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

            // PHASE 3's node-kind rules, mirrored (not restated -- see
            // RewardTrackNodeValidation's own header) at ContentDatabase.
            // Validation for the loaded-catalogue path. Guarded internally
            // behind RewardTrack.MaxLevel, so this is a no-op against the
            // pre-P3, 100-level content still shipped today.
            var nodeErrors = RewardTrackNodeValidation.Validate(label, RewardTrackDefinition.From(track));
            if (nodeErrors.Count > 0)
            {
                errors.AddRange(nodeErrors);
                track = null;
                return false;
            }

            return true;
        }

        // The reward-kind-independent core of one line -- reward, amount,
        // against, skillId/skillDisplayName, resourceDisplayName, and P3's
        // resource/identityKind/identityValue -- shared by a milestone entry
        // (which also carries a level, added by the caller) and a filler
        // row (which also carries a count).
        private readonly struct ResolvedEntryCore
        {
            public readonly TrackReward Reward;
            public readonly int Amount;
            public readonly DamageType? Against;
            public readonly string SkillId;
            public readonly string SkillDisplayName;
            public readonly string ResourceDisplayName;
            public readonly TrackResourceTarget? Resource;
            public readonly TrackIdentityKind? IdentityKind;
            public readonly string IdentityValue;

            public ResolvedEntryCore(TrackReward reward, int amount, DamageType? against, string skillId,
                string skillDisplayName, string resourceDisplayName, TrackResourceTarget? resource,
                TrackIdentityKind? identityKind, string identityValue)
            {
                Reward = reward;
                Amount = amount;
                Against = against;
                SkillId = skillId;
                SkillDisplayName = skillDisplayName;
                ResourceDisplayName = resourceDisplayName;
                Resource = resource;
                IdentityKind = identityKind;
                IdentityValue = identityValue;
            }
        }

        private static bool TryResolveEntry(string trackLabel, string where, string rawReward, int amount,
            string rawAgainst, string skillId, string rawResource, string rawIdentityKind, string rawIdentityValue,
            bool isFiller, RewardTrackCharacterContext context,
            out ResolvedEntryCore core, out string error)
        {
            core = default;

            if (!Enum.TryParse<TrackReward>(rawReward, ignoreCase: true, out var reward) || reward == TrackReward.None)
            {
                error = $"{trackLabel}, {where}: reward '{rawReward}' is not a known TrackReward.";
                return false;
            }

            // P3 ALIAS: SignatureAbsorbs (the old boolean) is rewritten to
            // SignatureAbsorbPerPoint at amount 1 the moment it is parsed, so
            // every rule below (filler-eligibility, the signature-resource
            // check, node-kind classification) sees exactly one kind rather
            // than two that mean the same thing. See TrackReward.
            // SignatureAbsorbPerPoint's own header -- new content should
            // author SignatureAbsorbPerPoint directly; SignatureAbsorbs is
            // kept parseable only so old reward_tracks.json content still
            // resolves.
            if (reward == TrackReward.SignatureAbsorbs)
            {
                reward = TrackReward.SignatureAbsorbPerPoint;
                amount = 1;
            }

            // RULE 3, widened in P3 -- see RewardTrack.IsFillerIneligible's
            // own header for why this is a broader question than "is this a
            // one-shot capability".
            if (isFiller && RewardTrack.IsFillerIneligible(reward))
            {
                error = $"{trackLabel}, {where}: {reward} cannot appear as filler -- only grants may.";
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
            // for why there is no ownership test here (§3f/§3h). SkillCostDelta/
            // SkillFlatDelta share the same no-ownership skillId lookup, via
            // context.Skills below -- a track IS the character, so naming a
            // skill on it has already said whose it is (§3h again).
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

            // P3: SkillCostDelta -- one named skill, one resource, and that
            // resource must be one the skill actually costs (a nonzero
            // manaCost for Mana, a nonzero resourceCost for Signature).
            TrackResourceTarget? resource = null;
            if (reward == TrackReward.SkillCostDelta)
            {
                if (string.IsNullOrWhiteSpace(skillId) || !context.Skills.TryGetValue(skillId, out var skillCtx))
                {
                    error = $"{trackLabel}, {where}: SkillCostDelta names skillId '{skillId}', which is not a skill " +
                            "in the catalogue.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(rawResource) || !Enum.TryParse<TrackResourceTarget>(rawResource, ignoreCase: true, out var parsedResource))
                {
                    error = $"{trackLabel}, {where}: SkillCostDelta needs a resource ('Mana' or 'Signature'), got '{rawResource}'.";
                    return false;
                }

                bool costsIt = parsedResource == TrackResourceTarget.Mana ? skillCtx.ManaCost > 0 : skillCtx.ResourceCost > 0;
                if (!costsIt)
                {
                    error = $"{trackLabel}, {where}: SkillCostDelta discounts {parsedResource} on '{skillId}', but " +
                            $"'{skillId}' does not cost {parsedResource}.";
                    return false;
                }

                resource = parsedResource;
                resolvedSkillId = skillId;
                skillDisplayName = skillCtx.DisplayName;
            }

            // P3: SkillFlatDelta -- one named skill, and it must have a
            // flatAmount path (DamageSingle with no damageInstances) for the
            // delta to land on anything.
            if (reward == TrackReward.SkillFlatDelta)
            {
                if (string.IsNullOrWhiteSpace(skillId) || !context.Skills.TryGetValue(skillId, out var skillCtx))
                {
                    error = $"{trackLabel}, {where}: SkillFlatDelta names skillId '{skillId}', which is not a skill " +
                            "in the catalogue.";
                    return false;
                }

                if (!skillCtx.HasFlatAmountPath)
                {
                    error = $"{trackLabel}, {where}: SkillFlatDelta names '{skillId}', which has no flatAmount path " +
                            "-- it must be a DamageSingle skill with no damageInstances.";
                    return false;
                }

                resolvedSkillId = skillId;
                skillDisplayName = skillCtx.DisplayName;
            }

            // P3: Identity -- exactly one payload kind, with a value required
            // for Title (any string) and PlateRim/PlateEmboss (silver or
            // gold); PortraitFrame/VictoryPose/Mastery carry no value.
            TrackIdentityKind? identityKind = null;
            string identityValue = "";
            if (reward == TrackReward.Identity)
            {
                if (string.IsNullOrWhiteSpace(rawIdentityKind) || !Enum.TryParse<TrackIdentityKind>(rawIdentityKind, ignoreCase: true, out var parsedKind))
                {
                    error = $"{trackLabel}, {where}: Identity needs an identityKind. Valid options: " +
                            $"{string.Join(", ", Enum.GetNames(typeof(TrackIdentityKind)))}.";
                    return false;
                }

                if (parsedKind == TrackIdentityKind.Title)
                {
                    if (string.IsNullOrWhiteSpace(rawIdentityValue))
                    {
                        error = $"{trackLabel}, {where}: Identity Title needs a value -- the title text.";
                        return false;
                    }

                    identityValue = rawIdentityValue;
                }
                else if (parsedKind == TrackIdentityKind.PlateRim || parsedKind == TrackIdentityKind.PlateEmboss)
                {
                    string normalized = (rawIdentityValue ?? "").Trim().ToLowerInvariant();
                    if (normalized != "silver" && normalized != "gold")
                    {
                        error = $"{trackLabel}, {where}: Identity {parsedKind} needs a value of 'silver' or 'gold', got '{rawIdentityValue}'.";
                        return false;
                    }

                    identityValue = normalized;
                }

                identityKind = parsedKind;
            }

            string resourceDisplayName = RewardTrack.IsSignatureReward(reward)
                ? (string.IsNullOrWhiteSpace(context.SignatureDisplayName) ? "SIGNATURE" : context.SignatureDisplayName)
                : "";

            core = new ResolvedEntryCore(reward, amount, against, resolvedSkillId, skillDisplayName, resourceDisplayName,
                resource, identityKind, identityValue);
            error = null;
            return true;
        }
    }
}
