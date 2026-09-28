namespace PrincesPalace.Domain.Progression
{
    // What a track reward is called, for showing a player.
    //
    // In Domain beside the table rather than in UiStrings, the same place and
    // for the same reason as RelicRarityNames: the text is a function of an
    // enum the Domain owns, and splitting "what rewards exist" from "what they
    // are called" across two assemblies means a new reward kind compiles
    // cleanly while displaying as nothing.
    //
    // Upper case like the rest of the game's chrome, and the AMOUNT is folded
    // in rather than left to the caller to append -- "+15 MAX HEALTH" and
    // "A STAT POINT" put the number in different places, so a caller building
    // the string would have to know which reward it was holding.
    //
    // Twelve captions. Two of them need a
    // lookup this class cannot do itself -- a skill's display name and a
    // signature resource's -- so the content resolver bakes both onto the
    // entry (TrackEntry.SkillDisplayName / ResourceDisplayName) before this
    // ever sees it; see TrackEntry's own header.
    public static class RewardTrackNames
    {
        public static string Of(TrackEntry entry)
        {
            int amount = entry.Amount;

            switch (entry.Reward)
            {
                case TrackReward.StatPoint:
                    return amount == 1 ? "A STAT POINT" : $"{amount} STAT POINTS";

                case TrackReward.MaxHealth:
                    return $"+{amount} MAX HEALTH";

                case TrackReward.Respec:
                    return "FREE RESPEC";

                case TrackReward.SecondLife:
                    return "A SECOND LIFE, ONCE PER RUN";

                case TrackReward.SignatureCapacity:
                    return $"+{amount} {ResourceNameOf(entry)} CAPACITY";

                case TrackReward.SignatureGainPerTurn:
                    return $"+{amount} {ResourceNameOf(entry)} PER TURN";

                case TrackReward.SignatureGainOnDamageTaken:
                    return $"+{amount} {ResourceNameOf(entry)} WHEN HURT";

                case TrackReward.ElementalDamagePercent:
                    return $"+{amount}% {ElementNameOf(entry)} DAMAGE";

                case TrackReward.MaxMana:
                    return $"+{amount} MAX MANA";

                case TrackReward.ManaRegen:
                    return $"+{amount} MANA A TURN";

                case TrackReward.UnlockSkill:
                    return $"LEARN {SkillNameOf(entry)}";

                // ---- PHASE 3 ----

                case TrackReward.FuryGainOnAttack:
                    return $"+{amount} FURY PER ATTACK";

                case TrackReward.FuryStartOfFight:
                    return $"OPENS AT {amount} FURY";

                case TrackReward.SpellCostDelta:
                    return $"SPELLS COST {amount} LESS";

                case TrackReward.SkillCostDelta:
                    return $"{SkillNameOf(entry)} COSTS {amount} LESS";

                case TrackReward.SkillFlatDelta:
                    return $"{SkillNameOf(entry)} +{amount}";

                // ---- PHASE 4 ----

                // "+1 PER POINT" rather than a bare "+1", because the whole
                // difference between this and SkillFlatDelta above is that
                // the number is paid once per point of resource spent -- a
                // caption that read the same as the flat one would make the
                // two nodes indistinguishable on the rail.
                case TrackReward.SkillPowerDelta:
                    return $"{SkillNameOf(entry)} +{amount} PER POINT";

                case TrackReward.SpellDamagePercent:
                    return $"+{amount}% SPELL DAMAGE";

                case TrackReward.SignatureAbsorbPerPoint:
                    return $"{ResourceNameOf(entry)} ABSORBS {amount} PER POINT";

                case TrackReward.Identity:
                    return IdentityCaption(entry);

                default:
                    return "";
            }
        }

        // Kept for callers that only have a reward and an amount in hand
        // (walking the enum in a test, for instance) -- delegates to the
        // entry-shaped overload above, so a kind that needs a baked caption
        // (the resource name, the element, the skill name) falls back to its
        // blank/"SIGNATURE" defaults rather than duplicating the switch.
        public static string Of(TrackReward reward, int amount) => Of(new TrackEntry(reward, amount));

        // A character with a signature resource but a blank
        // signatureDisplayName falls back to the literal "SIGNATURE" rather
        // than emitting "+5  CAPACITY" with a hole in it -- graceful
        // degradation, and it cannot happen once a track is authored, because
        // characters.json authors the name beside the id.
        private static string ResourceNameOf(TrackEntry entry) =>
            string.IsNullOrEmpty(entry.ResourceDisplayName)
                ? "SIGNATURE"
                : entry.ResourceDisplayName.ToUpperInvariant();

        private static string ElementNameOf(TrackEntry entry) =>
            entry.Against.HasValue ? entry.Against.Value.ToString().ToUpperInvariant() : "";

        private static string SkillNameOf(TrackEntry entry) =>
            string.IsNullOrEmpty(entry.SkillDisplayName) ? "" : entry.SkillDisplayName.ToUpperInvariant();

        // PHASE 3: Identity's caption depends on WHICH of the six payloads
        // the entry carries -- unlike every other kind, one reward maps to
        // six different templates. Falls back to a generic line for an
        // entry with no IdentityKind at all (a bare fixture, or content that
        // somehow lost its selector) rather than an empty string, the same
        // "never a blank line" posture every other case here already keeps.
        private static string IdentityCaption(TrackEntry entry)
        {
            switch (entry.IdentityKind)
            {
                case TrackIdentityKind.Title:
                    return string.IsNullOrEmpty(entry.IdentityValue)
                        ? "A NEW TITLE"
                        : $"TITLE: {entry.IdentityValue.ToUpperInvariant()}";

                case TrackIdentityKind.PlateRim:
                    return $"PLATE RIM, {(entry.IdentityValue ?? "").ToUpperInvariant()}";

                case TrackIdentityKind.PortraitFrame:
                    return "PORTRAIT FRAME";

                case TrackIdentityKind.PlateEmboss:
                    return $"PLATE EMBOSS, {(entry.IdentityValue ?? "").ToUpperInvariant()}";

                case TrackIdentityKind.VictoryPose:
                    return "VICTORY POSE";

                case TrackIdentityKind.Mastery:
                    return "MASTERY";

                default:
                    return "AN IDENTITY REWARD";
            }
        }
    }
}
