using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Pins the item-modifier plan's Phase E formatter -- "Fiery -- deals 24%
    // bonus Fire damage on hit", never "ElementalDamageOnHitPercent: 24".
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
    public class ModifierEffectTextTests
    {
        [Test]
        public void ElementalDamageOnHit_NamesTheElementAndTheBonusPercent()
        {
            var effect = new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 24, against: DamageType.Fire);
            Assert.AreEqual("deals 24% bonus Fire damage on hit", ModifierEffectText.Describe(effect));
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
            Assert.AreEqual("reduces Ice damage taken by 20%", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void TypedResistance_AgainstMagicalShorthand_ReadsAsMagic()
        {
            // Never authored today (see ModifierEntryResolver.
            // AcceptsMagicalShorthand's own comment -- only TypedResistanceFlat
            // accepts "magical", and nothing in modifiers.json uses it yet),
            // but a defensive fallback rather than an unreachable branch.
            var effect = new ModifierEffect(ModifierEffectType.TypedResistanceFlat, 25, againstMagical: true);
            Assert.AreEqual("reduces magic damage taken by 20%", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatSpeedBonus_IsASignedFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatSpeedBonus, 4);
            Assert.AreEqual("+4 Speed", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void Lifesteal_NamesThePercentOfDamageDealt()
        {
            var effect = new ModifierEffect(ModifierEffectType.LifestealPercent, 15);
            Assert.AreEqual("heals for 15% of damage dealt", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void GuaranteedFirstAction_IsAFlagSentenceWithNoNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.GuaranteedFirstAction, 0);
            Assert.AreEqual("always acts first in turn order", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatPhysicalDamageReduction_IsANegativeFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatPhysicalDamageReduction, 5);
            Assert.AreEqual("-5 flat Physical damage taken", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void BreakShieldDepletionResist_NamesThePercentReduction()
        {
            var effect = new ModifierEffect(ModifierEffectType.BreakShieldDepletionResistPercent, 30);
            Assert.AreEqual("30% less Break Shield depletion taken", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void OnKillSplash_NamesThePercentOfAttack()
        {
            var effect = new ModifierEffect(ModifierEffectType.OnKillSplashPercent, 30);
            Assert.AreEqual("kills splash 30% Attack onto nearby enemies", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void PushBackOnHit_NamesTheChanceAndTheFixedPushDistance()
        {
            // FightTuning.ModifierPushBackSlots (1) is a fixed constant, not
            // an authored-per-modifier number -- see PushBackOnHitChancePercent's
            // own header -- so the "1" here is the tuning constant, not the
            // effect's own Magnitude.
            var effect = new ModifierEffect(ModifierEffectType.PushBackOnHitChancePercent, 15);
            Assert.AreEqual($"15% chance on hit to push the target back {FightTuning.ModifierPushBackSlots} in turn order",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatMaxManaBonus_IsASignedFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatMaxManaBonus, 10);
            Assert.AreEqual("+10 Max Mana", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FlatManaRegenBonus_IsASignedFlatNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FlatManaRegenBonus, 2);
            Assert.AreEqual("+2 Mana Regen", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void NextSkillManaDiscount_NamesThePercentOff()
        {
            var effect = new ModifierEffect(ModifierEffectType.NextSkillManaDiscountPercent, 25);
            Assert.AreEqual("next skill after a plain hit costs 25% less mana", ModifierEffectText.Describe(effect));
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
            Assert.AreEqual("converts 25% of unspent mana to a Ward at the start of your turn",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void FortunateFavorOnWin_IsASignedFlatFavorNumber()
        {
            var effect = new ModifierEffect(ModifierEffectType.FortunateFavorOnWin, 5);
            Assert.AreEqual("+5 Favor (this run only) on a fight won", ModifierEffectText.Describe(effect));
        }

        [Test]
        public void DodgeChance_NamesThePercentChance()
        {
            var effect = new ModifierEffect(ModifierEffectType.DodgeChancePercent, 12);
            Assert.AreEqual("12% chance to dodge an attack outright", ModifierEffectText.Describe(effect));
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
                $"20% chance on hit to Chill the target (-{FightTuning.ChilledOnHitSpeedPercent}% Speed for {FightTuning.ChilledOnHitTurns} turns)",
                ModifierEffectText.Describe(effect));
        }

        [Test]
        public void RootOnHit_NamesTheChanceAndTheFixedDuration()
        {
            var effect = new ModifierEffect(ModifierEffectType.RootChancePercent, 20);
            Assert.AreEqual($"20% chance on hit to Root the target for {FightTuning.RootOnHitTurns} turns",
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
