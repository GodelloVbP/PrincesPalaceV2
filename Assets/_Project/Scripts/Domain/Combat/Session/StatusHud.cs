using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Combat.Session
{
    // The one merged code/slug/tooltip table for every status badge, on
    // every surface (party plate, roster mini-plate, enemy row, and the
    // enemy plate's own text line) -- see PLAN_STATUS_EFFECT_UI.md section
    // 4. Two tables disagreed before this: FightHudModel.StatusBadge spelled
    // three letters for ten statuses and two for Feared/Marked, PillCode
    // spelled two letters for all twelve. This is the one table either side
    // reads now.
    //
    // A SEPARATE FILE from FightHudModel on purpose: that file already
    // carries the command menu, the detail panel, enemy naming and the
    // intent sentence, and a fifth topic (what a status badge SAYS) earns
    // its own file rather than pushing FightHudModel past a thousand lines.
    // FightHudModel.StatusRow and FightHudModel.StatusRowsFor stay ON
    // FightHudModel regardless -- the plan's package contract (section 11)
    // fixes those two names, so the other packages compile against
    // FightHudModel, never against this file.
    public static class StatusHud
    {
        // At or above this many of the holder's own turns remaining, a
        // status reads as "until it is used" rather than a countdown -- see
        // section 2. Every sentinel this game actually authors
        // (Marks.MarkDurationTurns 99, FightTuning.MagicalShieldDurationTurns
        // 99, the Fragile Lamb's ward at 999) clears this with room to
        // spare; every genuine authored duration (content's statusDuration
        // tops out at 3, IceFingernailStackTurns at 5) sits nowhere near it.
        // StatusHudCoverageTests pins both sides of that gap so a future
        // 120-turn authored duration cannot silently read as "until used".
        public const int SentinelTurns = 90;

        // -1 is "draw no number" -- the one convention every surface that
        // reads a StatusRow.Counter shares.
        public static int CounterFor(int turnsRemaining) =>
            turnsRemaining < SentinelTurns ? turnsRemaining : -1;

        private static string DurationPhrase(int turnsRemaining) =>
            turnsRemaining < SentinelTurns ? Plural(turnsRemaining, "turn") : "until it is used";

        // Speed's own duration reads differently: TurnsLeft < 0 means "for
        // the rest of the fight" (FightSession.SpeedBuffs' own convention),
        // not "so long it must be spent" the way a status sentinel does.
        // Reusing DurationPhrase would make -1 read as "until it is used"
        // for the wrong reason -- it clears SentinelTurns by coincidence,
        // not because a speed grant is ever spent on one hit.
        private static string SpeedDurationPhrase(int turnsLeft) =>
            turnsLeft < 0 ? "for the rest of the fight" : Plural(turnsLeft, "turn");

        private static string Plural(int count, string noun) =>
            $"{count} {noun}{(count == 1 ? "" : "s")}";

        private static string Wrap(bool positive, string text) =>
            ItemStatLines.Coloured(positive ? ItemStatLines.GainHex : ItemStatLines.LossHex, text);

        // ---- the two speed presentations -------------------------------------
        //
        // No enum member for these -- PLAN_STATUS_EFFECT_UI.md section 4 is
        // explicit that speed stays a presentation id, not a thirteenth
        // StatusEffectType, because the row model is keyed on what to show,
        // not on what mechanism produced it.
        public const string SpeedUpCode = "SP+";
        public const string SpeedDownCode = "SP-";
        public const string SpeedUpSlug = "speed";
        public const string SpeedDownSlug = "speed_down";

        // ---- codes, slugs and polarity, one switch each ----------------------

        public static string CodeFor(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return "PSN";
                case StatusEffectType.Regen: return "RGN";
                case StatusEffectType.Protect: return "PRT";
                case StatusEffectType.Vulnerable: return "VLN";
                case StatusEffectType.Stun: return "STN";
                case StatusEffectType.Shielded: return "SHD";
                case StatusEffectType.Provoked: return "PRV";
                case StatusEffectType.Empowered: return "EMP";
                case StatusEffectType.Chilled: return "CHL";

                // RTD, not ROT -- this game already has a Poison status and
                // ROT reads as that (PLAN_STATUS_EFFECT_UI.md section 4).
                case StatusEffectType.Rooted: return "RTD";
                case StatusEffectType.Marked: return "MRK";
                case StatusEffectType.Feared: return "FER";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(type), type,
                        "StatusHud has no three-letter code for this status -- a badge would otherwise print nothing.");
            }
        }

        // Every current StatusEffectType slugs to its own lower-cased name,
        // which is exactly the filename STATUS_ICON_PROMPTS.md commissions
        // for each of the twelve -- no per-type table entry needed here.
        public static string SlugFor(StatusEffectType type) => type.ToString().ToLowerInvariant();

        public static bool IsPositive(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Regen:
                case StatusEffectType.Protect:
                case StatusEffectType.Shielded:
                case StatusEffectType.Empowered:
                    return true;
                default:
                    return false;
            }
        }

        // ---- ordering (section 5) ---------------------------------------------
        //
        // Never a blanket harm-first sort, which would bury an enemy's own
        // Shielded behind its Poison. Three tiers: what stops the holder
        // acting as normal, what changes the very next exchange, then
        // everything else -- and "everything else" still needs an order of
        // its own, so it falls back to the harm/control/benefit bucket this
        // file used to call PillCategory, and finally a fixed per-entry
        // table position so two rows in the same bucket never swap places
        // between one repaint and the next.
        private enum Tier { ActionRestriction, ImmediateExchange, Rest }
        private enum Bucket { Harm, Control, Benefit }

        private static Tier TierOf(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Stun:
                case StatusEffectType.Feared:
                case StatusEffectType.Provoked:
                case StatusEffectType.Rooted:
                    return Tier.ActionRestriction;
                case StatusEffectType.Shielded:
                case StatusEffectType.Empowered:
                    return Tier.ImmediateExchange;
                default:
                    return Tier.Rest;
            }
        }

        private static Bucket BucketOf(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison:
                case StatusEffectType.Vulnerable:
                case StatusEffectType.Stun:
                case StatusEffectType.Feared:
                case StatusEffectType.Marked:
                    return Bucket.Harm;
                case StatusEffectType.Provoked:
                case StatusEffectType.Chilled:
                case StatusEffectType.Rooted:
                    return Bucket.Control;
                default:
                    return Bucket.Benefit;
            }
        }

        // Section 4's own row order, the tie-break of last resort.
        private static int TableIndex(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return 0;
                case StatusEffectType.Regen: return 1;
                case StatusEffectType.Protect: return 2;
                case StatusEffectType.Vulnerable: return 3;
                case StatusEffectType.Stun: return 4;
                case StatusEffectType.Shielded: return 5;
                case StatusEffectType.Provoked: return 6;
                case StatusEffectType.Empowered: return 7;
                case StatusEffectType.Chilled: return 8;
                case StatusEffectType.Rooted: return 9;
                case StatusEffectType.Marked: return 10;
                case StatusEffectType.Feared: return 11;
                default: return 98;
            }
        }

        private const int SpeedUpTableIndex = 12;
        private const int SpeedDownTableIndex = 13;

        public static int SortKeyFor(StatusEffectType type) =>
            ((int)TierOf(type) * 10 + (int)BucketOf(type)) * 100 + TableIndex(type);

        public static int SpeedSortKey(bool positive) =>
            ((int)Tier.Rest * 10 + (int)(positive ? Bucket.Benefit : Bucket.Harm)) * 100
                + (positive ? SpeedUpTableIndex : SpeedDownTableIndex);

        // ---- tooltip text, section 5's own table -------------------------------
        //
        // "NAME -- <keyword>, <duration>", extending the shape Chilled and
        // Rooted already used. The keyword is the phrase wrapped in
        // ItemStatLines.Coloured, GainHex for a benefit and LossHex for a
        // detriment, by HOLDER-relative polarity -- an enemy's own Poison is
        // still a detriment badge even though the player is pleased about
        // it, which is why this reads IsPositive(status.Type) rather than
        // anything about who currently holds the status.
        public static string TooltipFor(ActiveStatus status)
        {
            bool positive = IsPositive(status.Type);
            string duration = DurationPhrase(status.TurnsRemaining);

            switch (status.Type)
            {
                case StatusEffectType.Poison:
                    return $"Poison -- {Wrap(positive, $"{status.Magnitude} damage each turn start")}, {duration}";
                case StatusEffectType.Regen:
                    return $"Regen -- {Wrap(positive, $"{status.Magnitude} healing each turn start")}, {duration}";
                case StatusEffectType.Protect:
                    return $"Protect -- {Wrap(positive, $"{status.Magnitude}% less damage taken")}, {duration}";
                case StatusEffectType.Vulnerable:
                    return $"Vulnerable -- {Wrap(positive, $"{status.Magnitude}% more damage taken")}, {duration}";
                case StatusEffectType.Stun:
                    return $"Stunned -- {Wrap(positive, "turn skipped")}, {duration}";
                case StatusEffectType.Shielded:
                    return $"Shielded -- {Wrap(positive, $"next hit taken reduced {status.Magnitude}%")}, {duration}";
                case StatusEffectType.Provoked:
                    return $"Provoked -- {Wrap(positive, $"must attack its provoker, for {status.Magnitude}% less damage to them")}, {duration}";
                case StatusEffectType.Empowered:
                    return $"Empowered -- {Wrap(positive, $"next attack deals {status.Magnitude}% more")}, {duration}";
                case StatusEffectType.Chilled:
                    return $"Chilled -- {Wrap(positive, $"-{status.Magnitude}% Speed")}, {duration}";
                case StatusEffectType.Rooted:
                    return $"Rooted -- {Wrap(positive, "Skill Only")}, {duration}";
                case StatusEffectType.Marked:
                    return $"Marked -- {Wrap(positive, "open to focused attacks")}, {duration}";

                // Feared is BOTH halves -- StatusEffects.HasStun counts it as
                // a stun, and DamageTakenMultiplier adds its Magnitude
                // beside Vulnerable's (see StatusEffectType.Feared's own
                // header). Fear.Apply sets Magnitude to Fear.VulnerablePercent
                // (25) every time it is cast, so reading Magnitude here
                // rather than the constant directly still always prints 25
                // and stays correct if that ever authors differently.
                case StatusEffectType.Feared:
                    return $"Feared -- {Wrap(positive, $"turn skipped and {status.Magnitude}% more damage taken")}, {duration}";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(status), status.Type,
                        "StatusHud has no tooltip for this status -- a badge would otherwise show nothing on hover.");
            }
        }

        // The signed flat grant a relic speed buff/malus is -- one per
        // source, `sourceName` already resolved by the caller (a relic's
        // DisplayName, or ActiveSpeedBuffs' own Source.ToString() when it
        // isn't one -- see FightHudModel.StatusRowsFor).
        private static string SpeedTooltip(string sourceName, int granted, int turnsLeft)
        {
            bool positive = granted > 0;
            string keyword = $"{(positive ? "+" : "")}{granted} Speed";
            return $"{sourceName} -- {Wrap(positive, keyword)}, {SpeedDurationPhrase(turnsLeft)}";
        }

        // ---- assembling a row --------------------------------------------------

        public static FightHudModel.StatusRow RowFor(ActiveStatus status) =>
            new FightHudModel.StatusRow(
                CodeFor(status.Type),
                SlugFor(status.Type),
                TooltipFor(status),
                IsPositive(status.Type),
                CounterFor(status.TurnsRemaining),
                SortKeyFor(status.Type));

        public static FightHudModel.StatusRow SpeedRow(string sourceName, int granted, int turnsLeft)
        {
            bool positive = granted > 0;
            return new FightHudModel.StatusRow(
                positive ? SpeedUpCode : SpeedDownCode,
                positive ? SpeedUpSlug : SpeedDownSlug,
                SpeedTooltip(sourceName, granted, turnsLeft),
                positive,
                CounterFor(turnsLeft),
                SpeedSortKey(positive));
        }
    }
}
