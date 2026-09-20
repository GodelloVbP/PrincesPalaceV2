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
    //
    // PUBLIC SURFACE IS SentinelTurns, CodeFor, SlugFor, SortKeyFor, RowFor,
    // SpeedRow and TransformRow (S6's review) -- every one of those has a real caller
    // outside this file (FightHudModel.StatusRowsFor, the icon resource
    // lookups, StatusHudCoverageTests' own CodeFor/SortKeyFor coverage).
    // CounterFor, IsPositive, SpeedSortKey and TooltipFor are composition
    // steps RowFor/SpeedRow assemble from -- nothing outside this file has
    // ever called one of those four directly, so they stay private.
    public static class StatusHud
    {
        // At or above this many of the holder's own turns remaining, a
        // status reads as "until it is used" rather than a countdown -- see
        // section 2. Every sentinel this game actually authors
        // (Marks.MarkDurationTurns 99, FightTuning.MagicalShieldDurationTurns
        // 99, StatusEffects.PermanentWardTurns 999 for The Golden Fleece)
        // clears this with room to
        // spare; every genuine authored duration (content's statusDuration
        // tops out at 3, IceFingernailStackTurns at 5) sits nowhere near it.
        // StatusHudCoverageTests pins both sides of that gap so a future
        // 120-turn authored duration cannot silently read as "until used".
        public const int SentinelTurns = 90;

        // -1 is "draw no number" -- the one convention every surface that
        // reads a StatusRow.Counter shares.
        private static int CounterFor(int turnsRemaining) =>
            turnsRemaining < SentinelTurns ? turnsRemaining : -1;

        private static string DurationPhrase(int turnsRemaining) =>
            turnsRemaining < SentinelTurns ? Plural(turnsRemaining, "turn") : "until it is used";

        // A WARD'S DURATION READS DIFFERENTLY, and it is the only status
        // whose does. "Until it is used" was exactly right while a ward was
        // one hit's worth of percentage spent whole by the next blow; a shield
        // pool is spent gradually, so one that is not on a clock stands until
        // something empties it rather than until it is "used".
        //
        // Two ways to not be on a clock, and they are not the same thing: The
        // Golden Fleece stops its caster's wards being counted down at all
        // (StatusEffects.NeverExpires), and the relic wards author a
        // whole-fight duration that clears SentinelTurns. Both read the same
        // to a player, so both say the same sentence.
        private static string WardDurationPhrase(StatusEffects.WardSummary summary) =>
            summary.AllPermanent || summary.SoonestTurns >= SentinelTurns
                ? "for the rest of the fight"
                : Plural(summary.SoonestTurns, "turn");

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

        // ---- the transformation, as a status ----------------------------------
        //
        // A presentation id like speed's, and for the same reason: a
        // Transformation is a live object on the combatant (Domain/Combat/
        // Transformation.cs), not a StatusEffectType, and adding a thirteenth
        // enum member to give it a badge would mean every switch in this file
        // pretending it has a magnitude and a duration in the ActiveStatus
        // sense.
        //
        // It replaces the TransformStrip -- a whole 36px panel fused above the
        // party plate that said one sentence and, being a panel, could only
        // ever say it about whoever was acting. As a row it appears on every
        // surface that reads StatusRowsFor, so a transformed ally sitting in
        // the roster is finally visible too, and the 36px goes back to the
        // roster cards. There is no Status/transformed.png yet, which is a
        // stated gap rather than a bug: PaintBadge's icon-first/code-fallback
        // path draws "FRM" until one ships.
        public const string TransformCode = "FRM";
        public const string TransformSlug = "transformed";

        // Second tier, benefit bucket -- ahead of poison and regen, behind
        // anything that stops the holder acting. A transform changes what
        // every subsequent action of theirs does, which is exactly what
        // Tier.ImmediateExchange already means for Shielded and Empowered.
        private const int TransformTableIndex = 14;

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
                case StatusEffectType.Burn: return "BRN";
                case StatusEffectType.Thorned: return "THN";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(type), type,
                        "StatusHud has no three-letter code for this status -- a badge would otherwise print nothing.");
            }
        }

        // Every current StatusEffectType slugs to its own lower-cased name,
        // which is exactly the filename STATUS_ICON_PROMPTS.md commissions
        // for each of the twelve -- no per-type table entry needed here.
        public static string SlugFor(StatusEffectType type) => type.ToString().ToLowerInvariant();

        private static bool IsPositive(StatusEffectType type)
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
                case StatusEffectType.Burn:
                case StatusEffectType.Thorned:
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
                // 12/13 are the speed presentations' own table indices
                // (SpeedUpTableIndex/SpeedDownTableIndex below), 14 is the
                // transformation's (TransformTableIndex above) -- Burn and
                // Thorned pick up after all three rather than between them,
                // so no existing sort key's Tier+Bucket+index triple can
                // collide with a new one.
                case StatusEffectType.Burn: return 15;
                case StatusEffectType.Thorned: return 16;
                default: return 98;
            }
        }

        private const int SpeedUpTableIndex = 12;
        private const int SpeedDownTableIndex = 13;

        public static int SortKeyFor(StatusEffectType type) =>
            ((int)TierOf(type) * 10 + (int)BucketOf(type)) * 100 + TableIndex(type);

        private static int SpeedSortKey(bool positive) =>
            ((int)Tier.Rest * 10 + (int)(positive ? Bucket.Benefit : Bucket.Harm)) * 100
                + (positive ? SpeedUpTableIndex : SpeedDownTableIndex);

        private static int TransformSortKey() =>
            ((int)Tier.ImmediateExchange * 10 + (int)Bucket.Benefit) * 100 + TransformTableIndex;

        // ---- tooltip text, section 5's own table -------------------------------
        //
        // "NAME -- <keyword>, <duration>", extending the shape Chilled and
        // Rooted already used. The keyword is the phrase wrapped in
        // ItemStatLines.Coloured, GainHex for a benefit and LossHex for a
        // detriment, by HOLDER-relative polarity -- an enemy's own Poison is
        // still a detriment badge even though the player is pleased about
        // it, which is why this reads IsPositive(status.Type) rather than
        // anything about who currently holds the status.
        //
        // TAKES THE SUMMARY, NOT ONE ENTRY, because statuses stack as of
        // 2026-09-20: a holder carrying three poisons is taking their sum every
        // turn, and a tooltip quoting the first entry would say a third of the
        // truth with total confidence. Magnitude is the sum, the duration is
        // the SOONEST instance -- the next moment the number changes on its own
        // -- and the "across N stacks" clause is the same one WardRow has used
        // since wards started stacking, for the same reason.
        private static string TooltipFor(StatusEffects.StatusSummary summary)
        {
            bool positive = IsPositive(summary.Type);
            string duration = DurationPhrase(summary.SoonestTurns);
            string stacks = summary.Instances > 1 ? $" across {summary.Instances} stacks" : "";
            int magnitude = summary.Magnitude;

            switch (summary.Type)
            {
                case StatusEffectType.Poison:
                    return $"Poison -- {Wrap(positive, $"{magnitude} damage each turn start")}{stacks}, {duration}";
                case StatusEffectType.Regen:
                    return $"Regen -- {Wrap(positive, $"{magnitude} healing each turn start")}{stacks}, {duration}";
                case StatusEffectType.Protect:
                    return $"Protect -- {Wrap(positive, $"{magnitude}% less damage taken")}{stacks}, {duration}";
                case StatusEffectType.Vulnerable:
                    return $"Vulnerable -- {Wrap(positive, $"{magnitude}% more damage taken")}{stacks}, {duration}";
                case StatusEffectType.Stun:
                    return $"Stunned -- {Wrap(positive, "turn skipped")}, {duration}";
                // NEVER REACHED FOR A WARD. Wards stack, so they do not get a
                // badge each -- RowFor sends Shielded to WardRow, which takes
                // the whole summary rather than one entry. Kept as a throw
                // rather than a duplicate of WardTooltip so the two can never
                // drift into saying different things about the same shield.
                case StatusEffectType.Shielded:
                    throw new System.InvalidOperationException(
                        "A ward's badge is built from StatusEffects.SummariseWards, not from one entry -- "
                        + "see StatusHud.WardRow.");
                case StatusEffectType.Provoked:
                    return $"Provoked -- {Wrap(positive, $"must attack its provoker, for {magnitude}% less damage to them")}, {duration}";
                case StatusEffectType.Empowered:
                    return $"Empowered -- {Wrap(positive, $"next attack deals {magnitude}% more")}, {duration}";
                case StatusEffectType.Chilled:
                    return $"Chilled -- {Wrap(positive, $"-{magnitude}% Speed")}{stacks}, {duration}";
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
                    return $"Feared -- {Wrap(positive, $"turn skipped and {magnitude}% more damage taken")}, {duration}";

                // THE SUMMED, SNAPSHOTTED TICK STRENGTH -- summary.Magnitude
                // is StatusEffects.MagnitudeOf's sum across live instances
                // (D3), and each instance's own Magnitude is 1.5's snapshot,
                // taken once at application and never re-read. A recast adds
                // a second instance rather than restating the first, so this
                // number is already the NEW sum the moment the badge next
                // redraws -- there is nothing here for a recast to leave
                // stale.
                case StatusEffectType.Burn:
                    return $"Burn -- {Wrap(positive, $"{magnitude} damage each turn start")}{stacks}, {duration}";
                case StatusEffectType.Thorned:
                    return $"Thorned -- {Wrap(positive, $"{magnitude} damage each turn start, and again after a physical move")}{stacks}, {duration}";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(summary), summary.Type,
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

        // ONE BADGE FOR EVERY WARD A COMBATANT CARRIES, and its number is the
        // TOTAL of the live pools rather than a turn count.
        //
        // Both halves are the shield model showing through. Wards stack, so a
        // badge each would be three SHD pills saying 20, 20 and 10 where the
        // player wants to know they are behind 50. And every other badge's
        // number answers "how much longer" where a shield's answers "how much
        // is left", which is the question somebody deciding whether to eat the
        // next hit is actually asking -- and the only one whose answer moves
        // between two of the wearer's own turns. The duration is still in the
        // tooltip, as the SOONEST of the entries, because that is the next
        // moment the number on the badge changes without anyone being hit.
        //
        // No sentinel branch on the counter: a pool is never 90-odd points by
        // accident, and a ward with points left always has a number worth
        // drawing.
        public static FightHudModel.StatusRow WardRow(StatusEffects.WardSummary summary)
        {
            string duration = WardDurationPhrase(summary);
            string across = summary.Entries > 1 ? $" across {summary.Entries} wards" : "";

            return new FightHudModel.StatusRow(
                CodeFor(StatusEffectType.Shielded),
                SlugFor(StatusEffectType.Shielded),
                $"Shielded -- {Wrap(true, $"{summary.Points} shield")}{across}, {duration}",
                true,
                summary.Points,
                SortKeyFor(StatusEffectType.Shielded));
        }

        public static FightHudModel.StatusRow RowFor(ActiveStatus status)
        {
            // A LONE WARD ENTRY IS A ONE-ENTRY SUMMARY, so the coverage sweep
            // that walks every StatusEffectType through this method still gets
            // an honest row and there is still only one place that words a
            // shield. StatusRowsFor never comes through here for a ward -- it
            // summarises the whole list once.
            if (status.Type == StatusEffectType.Shielded)
            {
                return WardRow(new StatusEffects.WardSummary(
                    status.Magnitude, 1, status.TurnsRemaining, StatusEffects.NeverExpires(status)));
            }

            // AND A LONE ANYTHING ELSE IS A ONE-INSTANCE SUMMARY, for the same
            // reason: one place words each status, whether the caller is
            // holding one entry or a pile.
            return RowFor(new StatusEffects.StatusSummary(
                status.Type, status.Magnitude, 1, status.TurnsRemaining));
        }

        // ONE BADGE FOR EVERY INSTANCE OF ONE TYPE, its number the SOONEST
        // expiry and its tooltip the summed magnitude.
        //
        // The counter stays a turn count rather than becoming an instance
        // count: every non-ward badge in the game answers "how much longer",
        // and a three that means "three stacks" sitting beside a three that
        // means "three turns" is a worse lie than no number. How many stacks
        // there are is in the tooltip, where WardRow already puts it.
        public static FightHudModel.StatusRow RowFor(StatusEffects.StatusSummary summary)
        {
            return new FightHudModel.StatusRow(
                CodeFor(summary.Type),
                SlugFor(summary.Type),
                TooltipFor(summary),
                IsPositive(summary.Type),
                CounterFor(summary.SoonestTurns),
                SortKeyFor(summary.Type));
        }

        // A running transformation as one badge. `displayName` is the form's
        // own name ("Black Ram Mode"), which is what the retired strip said
        // and what the tooltip still says -- the badge itself carries only
        // FRM and the turn count, exactly like every other row.
        //
        // A PERMANENT form draws NO number (the -1 sentinel every surface
        // already understands) rather than a count that has stopped moving:
        // see Transformation.IsPermanent's own comment for why "47 turns left"
        // reads as a bug rather than as "this is who he is now".
        public static FightHudModel.StatusRow TransformRow(string displayName, int turnsRemaining, bool permanent)
        {
            string name = string.IsNullOrEmpty(displayName) ? "Transformed" : displayName;
            string duration = permanent ? "for the rest of the run" : Plural(turnsRemaining, "turn");

            return new FightHudModel.StatusRow(
                TransformCode,
                TransformSlug,
                $"{name} -- {Wrap(true, "transformed")}, {duration}",
                true,
                permanent ? -1 : CounterFor(turnsRemaining),
                TransformSortKey());
        }

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
