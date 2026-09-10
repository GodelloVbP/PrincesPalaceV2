using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // "+20% Fire dmg on hit", not "deals 20% bonus Fire damage on hit" and
    // not "ElementalDamageOnHitPercent: 20". ONE shared formatter for every
    // screen that lists a rolled modifier's effects, mirroring how
    // ItemStatLines' DamageReductionPercent is the one place resistance ever
    // becomes a percentage rather than every caller rounding R/(R+100) by
    // hand -- the item-modifier plan's Phase E asks for exactly this shape.
    //
    // TAKES AN ALREADY-SCALED EFFECT. Every Magnitude here is read as the
    // final number a combat hook would see -- base x TierMultiplier x
    // RiftMultiplier already applied (see ModifierEffect's own header on
    // where that happens: ContentDatabase.ModifierEffects /
    // ModifierEffectsForItem, never here). This class only maps a TYPE to a
    // sentence FRAGMENT; it holds no scaling logic of its own to keep in sync
    // with ModifierMagnitude, and never will.
    //
    // FRAGMENTS, not full sentences: no leading capital, no trailing period,
    // so a caller can prefix "{DisplayName} -- " (ItemStatLines.ModifierLines
    // does exactly this) without carrying two conventions.
    //
    // TERSE, ITEM-TOOLTIP DENSITY, not prose -- rewritten 2026-08-27 per
    // designer complaint: the pre-rewrite fragments read as full sentence
    // fragments ("deals 24% bonus Fire damage on hit", "converts 25% of
    // unspent mana to a Ward at the start of your turn") rather than the
    // clipped "+N ABBR" register ItemStatLines.BonusParts/AppendDelta already
    // use one line above every AFFIXES section in the same card. Every
    // fragment here now matches that economy: no "deals", no repeated "on
    // hit" filler, no restating "while equipped" that the AFFIXES heading
    // already implies.
    //
    // KEYWORD COLOUR: every fragment's own rolled number (the one fact this
    // effect grants) is wrapped in KeywordHex via ItemStatLines.Coloured --
    // the ONLY rich-text convention this codebase has (see ItemStatLines'
    // own header: confirmed zero <b> usage anywhere, no confirmed bold glyph
    // in the TMPro SDF atlas). This nests safely inside the whole-line
    // GainHex/LossHex wrap ModifierComparisonLines already applies for a
    // gained/lost affix -- TMP's rich-text colour tags are a stack, so the
    // inner KeywordHex span reverts to the outer colour on its own close tag
    // rather than leaking past it.
    //
    // A few members here name a FIXED TUNING CONSTANT alongside the authored,
    // scaled Magnitude (Chilled's own speed/duration, Root's own duration,
    // Hardened's push distance, Runic's ward conversion rate) -- those
    // constants are not per-modifier authored numbers (see each
    // ModifierEffectType member's own comment), so there is nothing to scale
    // and nothing wrong with reading FightTuning directly for display, the
    // exact posture FightSession's own on-hit riders already take.
    public static class ModifierEffectText
    {
        // Dedicated to affix keyword emphasis rather than reusing GainHex --
        // GainHex/LossHex already carry a SPECIFIC meaning elsewhere in this
        // same tooltip (a stat that moved up/down in a VS. EQUIPPED diff, or
        // an affix gained/lost against what's currently worn), and every
        // affix line's own rolled number is not itself a gain/loss signal --
        // it is the fact the line exists to state, gained or not. A third,
        // fixed colour keeps "this is the number to notice" from colliding
        // with "this is better/worse than what you have equipped".
        public const string KeywordHex = "#E0B84D";

        // WHO IS READING THE CARD, as far as this class needs to know: the
        // primary pool the viewer holds. Optional and null-means-mana, so
        // every caller with no character in scope (the shop's plain listing,
        // a tooltip built before a viewer is chosen) prints exactly what it
        // always did.
        //
        // TWO FRAGMENTS DEPEND ON IT, and both for the same reason. Phase B
        // made every Max Mana and every mana-regen source WisdomDerived-only
        // (PoolPrecedence.Capacity/GainPerTurn): for a Fixed pool the
        // authored number is the whole number from every source, so a relic
        // promising "+10 Max Mana" moves that bar by nothing. Printing the
        // promise anyway is the precise lie the sheet already refuses to
        // tell by dropping the Wisdom link (SheetStats.FedBy); this is the
        // same refusal on the item card.
        //
        // WHAT IS NOT FIXED BY THIS: nothing hides the talent or the relic
        // from a Fixed pool's owner, so it is still a dead pick they can
        // make. Saying so is cheap; filtering the trees is a change to what
        // the game OFFERS and is the owner's call, noted rather than built.
        public static string Describe(ModifierEffect effect, ResolvedPool primaryPool = null)
        {
            bool poolIgnoresManaSources =
                primaryPool != null && primaryPool.CapacityRule != PoolCapacityRule.WisdomDerived;

            switch (effect.Type)
            {
                case ModifierEffectType.ElementalDamageOnHitPercent:
                    return $"{Keyword($"+{effect.Magnitude}%")} {ElementName(effect)} dmg on hit";

                case ModifierEffectType.TypedResistanceFlat:
                    return $"{Keyword($"-{ItemStatLines.DamageReductionPercent(effect.Magnitude)}%")} {ElementName(effect)} dmg taken";

                case ModifierEffectType.FlatSpeedBonus:
                    return $"{Keyword($"+{effect.Magnitude}")} Speed";

                case ModifierEffectType.LifestealPercent:
                    return $"{Keyword($"+{effect.Magnitude}%")} lifesteal";

                case ModifierEffectType.GuaranteedFirstAction:
                    return Keyword("always acts first");

                case ModifierEffectType.FlatPhysicalDamageReduction:
                    return $"{Keyword($"-{effect.Magnitude}")} Physical dmg taken";

                case ModifierEffectType.BreakShieldDepletionResistPercent:
                    return $"{Keyword($"-{effect.Magnitude}%")} Break Shield depletion taken";

                case ModifierEffectType.OnKillSplashPercent:
                    return $"{Keyword($"+{effect.Magnitude}%")} ATK splash on kill";

                case ModifierEffectType.PushBackOnHitChancePercent:
                    return $"{Keyword($"{effect.Magnitude}%")} on hit: push back {FightTuning.ModifierPushBackSlots} in turn order";

                case ModifierEffectType.FlatMaxManaBonus:
                    return poolIgnoresManaSources
                        ? $"no effect on {PoolName(primaryPool)}"
                        : $"{Keyword($"+{effect.Magnitude}")} Max Mana";

                case ModifierEffectType.FlatManaRegenBonus:
                    // "MP/turn", not "Mana Regen" -- matching the exact
                    // abbreviation ItemStatLines.BonusParts already prints
                    // one line above this in the same card for the flat stat
                    // bonus, not the longer label UiStrings.StatManaRegen
                    // uses on the character sheet (a different screen with
                    // room for the fuller word).
                    //
                    // SAME GATE AS THE CAPACITY LINE ABOVE, and not an
                    // extension of the plan's attack point 3 but the other
                    // half of it: PoolPrecedence.GainPerTurn takes exactly
                    // the same shape as Capacity, on purpose, because a pool
                    // that ignored outside capacity but not outside income
                    // would be two rules wearing one word. A card that
                    // refused the first and printed the second would put
                    // that inconsistency in front of the player.
                    return poolIgnoresManaSources
                        ? $"no effect on {PoolName(primaryPool)}"
                        : $"{Keyword($"+{effect.Magnitude}")} MP/turn";

                case ModifierEffectType.NextSkillManaDiscountPercent:
                    return $"{Keyword($"-{effect.Magnitude}%")} next skill cost after a hit";

                case ModifierEffectType.ManaToWardOnTurnStartPercent:
                    return $"{Keyword($"{RatePercent(FightTuning.RunicWardConversionRate)}%")} unspent mana -> Ward each turn";

                case ModifierEffectType.FortunateFavorBonusFlat:
                    return $"{Keyword($"+{effect.Magnitude}")} Favor";

                case ModifierEffectType.DodgeRating:
                    // Magnitude is a RATING, not a direct percent -- see
                    // ModifierEffectType.DodgeRating's own header. The
                    // player-facing line names the actual chance the curve
                    // produces (CombatMath.DodgePercentFrom), the same
                    // posture TypedResistanceFlat above already takes
                    // toward ItemStatLines.DamageReductionPercent for the
                    // identical reason: showing the raw authored number
                    // here would just be a different way of restating the
                    // exact misreading this rename exists to prevent.
                    return $"{Keyword($"+{CombatMath.DodgePercentFrom(effect.Magnitude)}%")} dodge chance";

                case ModifierEffectType.ChilledOnHitChancePercent:
                    return $"{Keyword($"{effect.Magnitude}%")} on hit: Chill " +
                           $"(-{FightTuning.ChilledOnHitSpeedPercent}% Speed, {FightTuning.ChilledOnHitTurns} turns)";

                case ModifierEffectType.RootChancePercent:
                    return $"{Keyword($"{effect.Magnitude}%")} on hit: Root ({FightTuning.RootOnHitTurns} turns)";

                // None, and any future member this formatter has not caught up
                // with yet: an empty fragment rather than a thrown exception,
                // the same graceful-degradation posture the rest of the
                // content layer takes on an unrecognised id -- ItemStatLines.
                // ModifierLines skips an empty fragment rather than printing
                // "DisplayName -- ".
                default:
                    return "";
            }
        }

        // ONE place a fragment's own rolled number gets wrapped in
        // KeywordHex, so every case above reads the same "highlight this"
        // call rather than six copies of the same ItemStatLines.Coloured
        // literal.
        private static string Keyword(string text) => ItemStatLines.Coloured(KeywordHex, text);

        // NOT WRAPPED IN KeywordHex, deliberately. That colour means "this is
        // the rolled number to notice"; a refusal has no number, and painting
        // it gold would make a dead affix the brightest thing on the card.
        private static string PoolName(ResolvedPool pool) =>
            string.IsNullOrWhiteSpace(pool?.DisplayName) ? "this resource" : pool.DisplayName;

        // 0.25 -> "25", not "0.25%". A rate constant is authored as a
        // fraction (FightTuning.RunicWardConversionRate's own doc comment
        // calls it a rate), and the player-facing line wants the percentage
        // that fraction represents.
        private static int RatePercent(float rate) => Rounding.AwayFromZero(rate * 100f);

        // TypedResistanceFlat's and ElementalDamageOnHitPercent's target,
        // read the same way both members carry it -- AgainstMagical's
        // "magical" shorthand only ever reaches TypedResistanceFlat (see
        // ModifierEntryResolver.AcceptsMagicalShorthand), but this reads both
        // fields defensively rather than assuming Against is always set.
        private static string ElementName(ModifierEffect effect)
        {
            if (effect.AgainstMagical)
            {
                return "magic";
            }

            return effect.Against.HasValue ? effect.Against.Value.ToString() : "";
        }
    }
}
