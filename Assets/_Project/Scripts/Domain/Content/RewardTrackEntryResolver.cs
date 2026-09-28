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
    // never reads characters.json or skills.json itself -- the two display
    // names the captions need, and validation rules 4-5.
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

        // WHETHER THIS CHARACTER'S PRIMARY POOL STARTS EMPTY -- what
        // FuryStartOfFight's own legality rule turns on (see
        // TryResolveEntry's rule 6 below). Computed from pools.json rather
        // than characters.json alone, the same reason PoolOwnership exists:
        // a fact about the OWNER's pool, not about the skill or the track.
        public bool PrimaryPoolStartsZero;

        // WHETHER THIS CHARACTER'S PRIMARY POOL TAKES OUTSIDE MAX-MANA AND
        // REGEN BONUSES AT ALL -- rule 7 (AUDIT #145 B9). PoolPrecedence.
        // Capacity and .GainPerTurn drop every derived bonus on a pool whose
        // CapacityRule is not WisdomDerived, which is where a track's
        // MaxMana/ManaRegen totals land. True by default, so a context
        // built without pools.json (a fixture) keeps the old answer.
        public bool PrimaryPoolTakesManaBonuses = true;

        // Every element this character can already deal at level 1: their
        // own AttackType, plus the type of every damageInstances entry on a
        // skill authored to them with unlockLevel <= 1. Rule 4.
        public IReadOnlyCollection<DamageType> Level1DamageTypes = Array.Empty<DamageType>();

        // Every skill id in the whole catalogue, keyed to its own display
        // name -- what UnlockSkill's skillId resolves against, and captions
        // with "LEARN {S}".
        //
        // THE WHOLE CATALOGUE, NOT THIS CHARACTER'S OWN KIT, and that is the
        // rule rather than a convenience.
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
            IReadOnlyList<ResolvedPool> pools, IReadOnlyList<ResolvedCharacter> characters, IReadOnlyList<ResolvedSkill> skills)
        {
            var poolById = (pools ?? Array.Empty<ResolvedPool>())
                .Where(pool => pool != null)
                .ToDictionary(pool => pool.Id, pool => pool);

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

                bool primaryPoolStartsZero = poolById.TryGetValue(character.PrimaryPoolId ?? "", out var primaryPool)
                    && primaryPool.StartRule == PoolStartRule.Zero;
                bool primaryPoolTakesManaBonuses = primaryPool == null
                    || primaryPool.CapacityRule == PoolCapacityRule.WisdomDerived;

                contexts[character.Id] = new RewardTrackCharacterContext
                {
                    SortOrder = character.SortOrder,
                    HasSignatureResource = character.HasSignatureResource,
                    SignatureDisplayName = character.SignatureDisplayName,
                    PrimaryPoolStartsZero = primaryPoolStartsZero,
                    PrimaryPoolTakesManaBonuses = primaryPoolTakesManaBonuses,
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
    // -- here a per-character lookup rather than a flat id set, because the
    // signature-resource rule depends on which character the track belongs
    // to. See the validation rules below.
    //
    // FIVE RULES: four (down from five at Phase 4, then up one for the P3
    // fury addendum), plus rule 7 from AUDIT #145 B9 -- MaxMana/ManaRegen
    // refused on a primary pool that ignores outside mana bonuses. Every level from StartingLevel+1 to MaxLevel carries
    // exactly one entry and no entry names a level outside that span (rule
    // 1, which now subsumes rule 2's "the filler counts add up"); a
    // one-shot capability appears at most once (rule 1's second half); a
    // signature reward is refused on a character with no signature resource
    // (rule 5); FuryStartOfFight is refused on anyone but a positive-amount,
    // Zero-start pool owner (rule 6). Rules 3 and 4 -- "a one-shot kind may
    // not be filler", "a filler element must be one the character can
    // already deal at level 1" -- existed only because a filler row's LEVEL
    // was computed rather than authored, and both went with the mix that
    // computed it (see RawTrackLevel's own header).
    public static class RewardTrackEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawRewardTrackEntry> entries,
            IReadOnlyDictionary<string, RewardTrackCharacterContext> characters,
            out List<ResolvedRewardTrack> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRewardTrack>();
            errors = new List<string>();
            characters ??= new Dictionary<string, RewardTrackCharacterContext>();

            for (int i = 0; i < entries.Count; i++)
            {
                var raw = entries[i];
                string label = string.IsNullOrWhiteSpace(raw?.characterId)
                    ? $"reward_tracks.json entry #{i + 1}"
                    : $"reward track '{raw.characterId}'";

                if (TryResolveOne(raw, label, characters, out var single, out var trackErrors))
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

        private static bool TryResolveOne(RawRewardTrackEntry raw, string label,
            IReadOnlyDictionary<string, RewardTrackCharacterContext> characters,
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

            // ---- RULE 1: every level from StartingLevel+1 to MaxLevel
            // carries exactly one entry, and no entry names a level outside
            // that span. This is the whole of what the milestone/filler pair
            // of rules used to say between them: the twelve-milestone check
            // and the "filler counts sum to 87" check were two halves of
            // "every node on the rail pays something", which one walk over
            // the levels now answers directly. ----
            int firstLevel = RewardTrack.StartingLevel + 1;
            var byLevel = new Dictionary<int, RawTrackLevel>();

            foreach (var row in raw.levels ?? Array.Empty<RawTrackLevel>())
            {
                if (row == null) continue;

                if (row.level < firstLevel || row.level > RewardTrack.MaxLevel)
                {
                    errors.Add($"{label}: entry names level {row.level}, outside the track's span of " +
                               $"{firstLevel} to {RewardTrack.MaxLevel}.");
                    continue;
                }

                if (byLevel.ContainsKey(row.level))
                {
                    errors.Add($"{label}: level {row.level} has more than one entry.");
                    continue;
                }

                byLevel[row.level] = row;
            }

            for (int level = firstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                if (!byLevel.ContainsKey(level))
                {
                    errors.Add($"{label}: level {level} has no entry -- every level from {firstLevel} to " +
                               $"{RewardTrack.MaxLevel} must carry exactly one.");
                }
            }

            var resolvedLevels = new List<ResolvedTrackLevel>();
            foreach (var pair in byLevel.OrderBy(p => p.Key))
            {
                if (TryResolveEntry(label, $"level {pair.Key}", pair.Value.reward, pair.Value.amount, pair.Value.against,
                        pair.Value.skillId, pair.Value.resource, pair.Value.identityKind, pair.Value.value,
                        context, out var core, out string entryError))
                {
                    resolvedLevels.Add(new ResolvedTrackLevel(pair.Key, core.Reward, core.Amount, core.Against,
                        core.SkillId, core.SkillDisplayName, core.ResourceDisplayName, core.Resource, core.IdentityKind,
                        core.IdentityValue));
                }
                else
                {
                    errors.Add(entryError);
                }
            }

            // ---- RULE 1, continued: a one-shot capability may appear at
            // most once on a track. HasUnlocked only ever asks "has the
            // FIRST level this reward appears at been reached", so a second
            // Respec or SecondLife node would be content no player could
            // ever see paid out twice. UnlockSkill is keyed by skillId
            // rather than by kind: two levels naming two different skills
            // are two different unlocks, but the same skillId named twice is
            // the same dead-content bug. ----
            var firstLevelOfCapability = new Dictionary<string, int>();
            foreach (var entry in resolvedLevels)
            {
                if (!RewardTrack.IsOneShotCapability(entry.Reward)) continue;

                string key = entry.Reward == TrackReward.UnlockSkill
                    ? $"{entry.Reward}:{entry.SkillId}"
                    : entry.Reward.ToString();

                if (firstLevelOfCapability.TryGetValue(key, out int alreadyAt))
                {
                    errors.Add($"{label}: {entry.Reward} is a one-shot capability and appears at both level " +
                               $"{alreadyAt} and level {entry.Level} -- it may only be granted once.");
                }
                else
                {
                    firstLevelOfCapability[key] = entry.Level;
                }
            }

            if (errors.Count > 0) return false;

            track = new ResolvedRewardTrack(raw.characterId, resolvedLevels.ToArray(), context.SortOrder);

            // PHASE 3's node-kind rules, mirrored (not restated -- see
            // RewardTrackNodeValidation's own header) at ContentDatabase.
            // Validation for the loaded-catalogue path.
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
        // against, skillId/skillDisplayName, resourceDisplayName, and
        // resource/identityKind/identityValue. The caller adds the level.
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
            RewardTrackCharacterContext context,
            out ResolvedEntryCore core, out string error)
        {
            core = default;

            if (!Enum.TryParse<TrackReward>(rawReward, ignoreCase: true, out var reward) || reward == TrackReward.None)
            {
                error = $"{trackLabel}, {where}: reward '{rawReward}' is not a known TrackReward.";
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

            // ElementalDamagePercent is PER-ELEMENT (RewardTrackDefinition.
            // CollectedElementalTotals groups by Against, and
            // ContentDatabase.Effective's ModifierEffects reads it per
            // DamageType) -- an entry with no `against` resolves with
            // Against == null and no error, and the only reader silently
            // skips exactly that: `!entry.Against.HasValue` in
            // CollectedElementalTotals. The reward would authored-but-grant-
            // nothing, the same silent-ignore shape CheckFloor's amount
            // check does not catch either. Refuse it at the source instead.
            if (reward == TrackReward.ElementalDamagePercent && against == null)
            {
                error = $"{trackLabel}, {where}: {reward} names no 'against' DamageType -- CollectedElementalTotals " +
                        "skips any entry without one, so this would grant nothing.";
                return false;
            }

            // RULE 5, the one cross-character rule left. (Rule 4 -- "a
            // filler element must be one the character can already deal at
            // level 1" -- went with the filler mix: a MILESTONE was always
            // exempt from it precisely because its level was authored, and
            // every level is authored now, so the exemption swallowed the
            // rule. Level1DamageTypes is kept on the context for the
            // ContentDatabase.Validation mirror and for whatever asks next.)
            if (RewardTrack.IsSignatureReward(reward) && !context.HasSignatureResource)
            {
                error = $"{trackLabel}, {where}: {rawReward} is authored on a character with no signature resource.";
                return false;
            }

            // RULE 6 (P3 fury addendum): FuryStartOfFight only means
            // anything when the track owner's primary pool actually starts
            // empty. SkillEntryResolver.cs's own free-turn-one exemption
            // (zeroStartPoolOwnerIds, derived from PoolOwnership.
            // ZeroStartOwners) reasons from that same StartRule.Zero fact to
            // excuse a Zero-start owner's free action from the "costs
            // nothing, would be spammed" refusal -- an override that could
            // not raise a full-start pool above its own authored starting
            // value would either do nothing or contradict the reasoning that
            // exemption depends on. amount > 0 for the same reason a Bump
            // floor is never authored at 0: an override that sets the
            // opening value back to what it already was pays nothing.
            // Mirrored in ContentDatabase.Validation for a hand-authored
            // asset that never passed through this resolver.
            if (reward == TrackReward.FuryStartOfFight && (!context.PrimaryPoolStartsZero || amount <= 0))
            {
                error = $"{trackLabel}, {where}: FuryStartOfFight is only legal on a character whose primary pool " +
                        "starts at Zero, with an amount above 0 -- it may only raise a zero-start pool, because " +
                        "SkillEntryResolver's free-action exemption reasons from that same start rule.";
                return false;
            }

            // RULE 7 (AUDIT #145 B9, the #134 detour made a rule): MaxMana and
            // ManaRegen feed the derived max-mana and regen totals, and
            // PoolPrecedence ignores both on a primary pool whose CapacityRule
            // is not WisdomDerived (Bjorn's `fury` is Fixed). Such a node
            // would render, be claimed, and pay nothing -- the same
            // pays-nothing shape rule 5 refuses for a signature reward.
            if ((reward == TrackReward.MaxMana || reward == TrackReward.ManaRegen)
                && !context.PrimaryPoolTakesManaBonuses)
            {
                error = $"{trackLabel}, {where}: {rawReward} is authored on a character whose primary pool has a " +
                        "fixed capacity rule, which ignores every outside max-mana and regen bonus -- it would " +
                        "pay nothing.";
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

            // PHASE 4: SkillPowerDelta -- one named skill, and it must
            // actually spend a signature resource for a per-point bonus to
            // have anything to multiply. A skill with no resourceCost never
            // reaches SkillResolution's `power * resourceSpent` term at all,
            // so a delta on one would be a number nothing reads -- the same
            // dead-authoring failure HasFlatAmountPath refuses for
            // SkillFlatDelta.
            if (reward == TrackReward.SkillPowerDelta)
            {
                if (string.IsNullOrWhiteSpace(skillId) || !context.Skills.TryGetValue(skillId, out var powerCtx))
                {
                    error = $"{trackLabel}, {where}: SkillPowerDelta names skillId '{skillId}', which is not a skill " +
                            "in the catalogue.";
                    return false;
                }

                if (powerCtx.ResourceCost <= 0)
                {
                    error = $"{trackLabel}, {where}: SkillPowerDelta names '{skillId}', which spends no signature " +
                            "resource -- `power` is paid per point spent, so a delta on it would never be read.";
                    return false;
                }

                resolvedSkillId = skillId;
                skillDisplayName = powerCtx.DisplayName;
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
