using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Pins the item-modifier plan's Phase E formatter -- "Fiery -- +20% Fire
    // dmg on hit", never "deals 20% bonus Fire damage on hit" and never
    // "ElementalDamageOnHitPercent: 20".
    //
    // Every ModifierEffect here carries a LITERAL magnitude that already
    // stands in for "already scaled" -- ModifierEffectText.Describe holds no
    // tier/rift arithmetic of its own to pin (that formula is
    // ModifierMagnitudeTests' job), so these tests exist purely to catch a
    // wrong word, a missing number, or a member this formatter has not
    // caught up with. One test per ModifierEffectType member the item-
    // modifier plan's Phase C/D actually authors content against, plus the
    // two members that are real, working infrastructure with no authored
    // content yet (FlatSpeedBonus, GuaranteedFirstAction) and the closed-
    // enum default (None).
    //
    // REWRITTEN 2026-08-27 for the terse, keyword-coloured format -- every
    // expected string here now wraps the fragment's own rolled number in
    // ModifierEffectText.KeywordHex via the literal "<color=...>" tag rather
    // than the old plain-prose sentence, matching Describe's own rewrite.
    public class ModifierEffectTextTests
    {
        private static string Keyword(string text) => $"<color={ModifierEffectText.KeywordHex}>{text}</color>";

        [Test]
        public void ElementalDamageOnHit_NamesTheElementAndTheBonusPercent()
        {
            var effect = new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 24, against: DamageType.Fire);
            Assert.AreEqual($"{Keyword("+24%")} Fire dmg on hit", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void TypedResistance_ReadsThroughTheSharedDamageReductionPercentFormatter()
        {
            // 25 -> 20% is one of ItemStatLines.DamageReductionPercent's own
            // pinned worked examples (its own header comment) -- reused here
            // rather than re-derived, because this test exists to prove the
            // DISPLAY layer calls that one shared formatter, not to re-pin
            // the R/(R+100) curve a second time.
            var effect = new ModifierEffect(ModifierEffectType.TypedResistanceFlat, 25, against: DamageType.Ice);
            Assert.AreEqual($"{Keyword("-20%")} Ice dmg taken", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void TypedResistance_AgainstMagicalShorthand_ReadsAsMagic()
        {
            // Never authored today (see ModifierEntryResolver.
            // AcceptsMagicalShorthand's own comment -- only TypedResistanceFlat
            // accepts "magical", and nothing in modifiers.json uses it yet),
            // but a defensive fallback rather than an unreachable branch.
            var effect = new ModifierEffect(ModifierEffectType.TypedResistanceFlat, 25, againstMagical: true);
            Assert.AreEqual($"{Keyword("-20%")} magic dmg taken", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatSpeedBonus_IsASignedFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatSpeedBonus, 4);
            Assert.AreEqual($"{Keyword("+4")} Speed", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void Lifesteal_NamesThePercentOfDamageDealt()
        {
            var effect = new ModifierEffect(ModifierEffectType.LifestealPercent, 15);
            Assert.AreEqual($"{Keyword("+15%")} lifesteal", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void GuaranteedFirstAction_IsAFlagSentenceWithNoNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.GuaranteedFirstAction, 0);
            Assert.AreEqual(Keyword("always acts first"), ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatPhysicalDamageReduction_IsANegativeFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatPhysicalDamageReduction, 5);
            Assert.AreEqual($"{Keyword("-5")} Physical dmg taken", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void BreakShieldDepletionResist_NamesThePercentReduction()
        {
            var effect = new ModifierEffect(ModifierEffectType.BreakShieldDepletionResistPercent, 30);
            Assert.AreEqual($"{Keyword("-30%")} Break Shield depletion taken", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void OnKillSplash_NamesThePercentOfAttack()
        {
            var effect = new ModifierEffect(ModifierEffectType.OnKillSplashPercent, 30);
            Assert.AreEqual($"{Keyword("+30%")} ATK splash on kill", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void PushBackOnHit_NamesTheChanceAndTheFixedPushDistance()
        {
            // FightTuning.ModifierPushBackSlots (1) is a fixed constant, not
            // an authored-per-modifier number -- see PushBackOnHitChancePercent's
            // own header -- so the "1" here is the tuning constant, not the
            // effect's own Magnitude.
            var effect = new ModifierEffect(ModifierEffectType.PushBackOnHitChancePercent, 15);
            Assert.AreEqual($"{Keyword("15%")} on hit: push back {FightTuning.ModifierPushBackSlots} in turn order",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatMaxManaBonus_IsASignedFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatMaxManaBonus, 10);
            Assert.AreEqual($"{Keyword("+10")} Max Mana", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatManaRegenBonus_IsASignedFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatManaRegenBonus, 2);
            Assert.AreEqual($"{Keyword("+2")} MP/turn", ModifierEffectText.Describe(effect));
        }

        // THE TWO MANA FRAGMENTS AGAINST A POOL THAT IGNORES THEM.
        //
        // A Fixed pool's authored capacity is the WHOLE number from every
        // source (PoolPrecedence.Capacity), and its income takes the same
        // shape, so both of these affixes move a Fury bar by exactly zero.
        // The card has to say so: the character sheet already drops the
        // Wisdom link on that row for the same reason, and an item promising
        // "+10 Max Mana" to someone it does nothing for is the same lie one
        // screen over.
        //
        // A FIXTURE POOL, not the shipped mana row: the whole promise of
        // PoolCapacityRule.Fixed is about a row nobody has authored yet, and
        // a rule reachable only through the catalogue can only be tested
        // against the row the catalogue ships (PoolPrecedence's own header
        // makes the same argument).
        private static ResolvedPool FuryFixture() =>
            new ResolvedPool(
                "fury", "Fury", "FURY",
                PoolCapacityRule.Fixed, 100,
                0, 15, 10,
                10, PoolDecayTrigger.Damage,
                PoolStartRule.Zero, 0,
                "#FF8A3A", "#8E3A12", "#FFD2B0",
                pulse: true, allowsSpellBooks: false, restoredByManaEffects: false, absorbsDamage: false,
                sortOrder: 1);

        [Test]
        public void FlatMaxManaBonus_SaysItDoesNothingForAFixedPool()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatMaxManaBonus, 10);
            Assert.AreEqual("no effect on Fury", ModifierEffectText.Describe(effect, FuryFixture()));
        }

        [Test]
        public void FlatManaRegenBonus_SaysItDoesNothingForAFixedPool()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatManaRegenBonus, 2);
            Assert.AreEqual("no effect on Fury", ModifierEffectText.Describe(effect, FuryFixture()));
        }

        // A WisdomDerived pool is what every character shipped today carries,
        // so passing one must change nothing at all -- this is the control
        // that proves the two tests above are gated on the RULE and not
        // merely on "a pool was passed".
        [Test]
        public void AWisdomDerivedPool_ReadsExactlyAsNoPoolAtAll()
        {
            var mana = new ResolvedPool(
                "mana", "Mana", "MP",
                PoolCapacityRule.WisdomDerived, 30,
                0, 0, 0,
                0, PoolDecayTrigger.Damage,
                PoolStartRule.Full, 0,
                "#7EA8E6", "#3A5A9A", "#C4D8F2",
                pulse: false, allowsSpellBooks: true, restoredByManaEffects: true, absorbsDamage: false,
                sortOrder: 0);

            var capacity = new ModifierEffect(ModifierEffectType.FlatMaxManaBonus, 10);
            var regen = new ModifierEffect(ModifierEffectType.FlatManaRegenBonus, 2);

            Assert.AreEqual($"{Keyword("+10")} Max Mana", ModifierEffectText.Describe(capacity, mana));
            Assert.AreEqual($"{Keyword("+2")} MP/turn", ModifierEffectText.Describe(regen, mana));
        }

        [Test]
        public void NextSkillManaDiscount_NamesThePercentOff()
        {
            var effect = new ModifierEffect(ModifierEffectType.NextSkillManaDiscountPercent, 25);
            Assert.AreEqual($"{Keyword("-25%")} next skill cost after a hit", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void ManaToWardOnTurnStart_IsAFlagThatNamesTheFixedConversionRate()
        {
            // A FLAG (Magnitude ignored, always authored 0 -- see
            // ModifierEntryResolver.IgnoresMagnitude) whose sentence still
            // names a real number: FightTuning.RunicWardConversionRate
            // (0.25), the fixed rate ManaToWardOnTurnStartPercent's own
            // header says is a code constant rather than an authored one.
            var effect = new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0);
            Assert.AreEqual($"{Keyword("25%")} of unspent mana as shield each turn",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FortunateFavorBonusFlat_IsASignedFlatFavorNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FortunateFavorBonusFlat, 5);
            Assert.AreEqual($"{Keyword("+5")} Favor", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void DodgeRating_NamesTheCurvedPercentChance_NotTheRawRating()
        {
            // 12 is a RATING, not a percent -- the line must show what
            // CombatMath.DodgePercentFrom's curve actually produces
            // (100 * 12 / 112 = 10.71..., floored to 10), never the raw 12.
            var effect = new ModifierEffect(ModifierEffectType.DodgeRating, 12);
            Assert.AreEqual($"{Keyword("+10%")} dodge chance", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void DodgeRating_AtTheSoftenerValue_NamesExactlyHalf()
        {
            var effect = new ModifierEffect(ModifierEffectType.DodgeRating, 100);
            Assert.AreEqual($"{Keyword("+50%")} dodge chance", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void ChilledOnHit_NamesTheChanceAndTheFixedChillShape()
        {
            // FightTuning.ChilledOnHitSpeedPercent (20) and ChilledOnHitTurns
            // (2) are fixed constants, not authored per modifier -- see
            // ChilledOnHitChancePercent's own header -- so both numbers in
            // the parenthetical come from FightTuning, never from Magnitude.
            var effect = new ModifierEffect(ModifierEffectType.ChilledOnHitChancePercent, 20);
            Assert.AreEqual(
                $"{Keyword("20%")} on hit: Chill (-{FightTuning.ChilledOnHitSpeedPercent}% Speed, {FightTuning.ChilledOnHitTurns} turns)",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void RootOnHit_NamesTheChanceAndTheFixedDuration()
        {
            var effect = new ModifierEffect(ModifierEffectType.RootChancePercent, 20);
            Assert.AreEqual($"{Keyword("20%")} on hit: Root ({FightTuning.RootOnHitTurns} turns)",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void None_ProducesAnEmptyFragmentRatherThanThrowing()
        {
            // Never authored (ModifierEntryResolver rejects it) but reachable
            // defensively, and any future member this formatter has not
            // caught up with should degrade the same way -- graceful
            // degradation on missing content is this codebase's house style.
            var effect = new ModifierEffect(ModifierEffectType.None, 0);
            Assert.AreEqual("", ModifierEffectText.Describe(effect));
        }
    }
}
