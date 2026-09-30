using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The words on a SKILLS row (plan C18): cost in the pool the caster holds,
    // the wait, the target and the reach, pinned as literals.
    public class SkillListingTests
    {
        private static ResolvedSkill Skill(int manaCost = 0, int resourceCost = 0, bool spendsAll = false,
            SkillTargeting targeting = SkillTargeting.SingleEnemy, int cooldown = 0, bool oncePerFight = false,
            Reach reach = default, int healthCostPercent = 0, ResolvedPoolTier[] tiers = null) =>
            new ResolvedSkill("s", "Skill", "Does a thing.", "hero", 1, SkillEffect.DamageSingle, targeting,
                manaCost, resourceCost, spendsAll, 10, 0, false, null, SpellPresentation.None, 0,
                cooldownTurns: cooldown, oncePerFight: oncePerFight, reach: reach,
                healthCostPercent: healthCostPercent, poolTiers: tiers);

        [Test]
        public void ManaIsPricedInTheCastersOwnPoolName()
        {
            Assert.AreEqual("8 Mana", SkillListing.CostText(Skill(manaCost: 8), "Mana", "Wool"));
            Assert.AreEqual("30 Fury", SkillListing.CostText(Skill(manaCost: 30), "Fury", ""));
        }

        [Test]
        public void ASignatureCostUsesTheSignatureName()
        {
            Assert.AreEqual("2 Wool", SkillListing.CostText(Skill(resourceCost: 2), "Mana", "Wool"));
            Assert.AreEqual("All Wool", SkillListing.CostText(Skill(spendsAll: true), "Mana", "Wool"));
        }

        [Test]
        public void TwoPaymentsAreJoinedAndAHealthPaymentSaysHp()
        {
            Assert.AreEqual("5 Mana + 3 Wool", SkillListing.CostText(Skill(manaCost: 5, resourceCost: 3), "Mana", "Wool"));
            Assert.AreEqual("20% HP", SkillListing.CostText(Skill(healthCostPercent: 20), "Mana", "Wool"));
        }

        [Test]
        public void ASkillThatSpendsAShareOfThePoolSaysWhichPool()
        {
            var tiers = new[] { new ResolvedPoolTier { Spend = 0.5f, DamageMultiplier = 2f } };

            Assert.AreEqual("Spends Fury", SkillListing.CostText(Skill(tiers: tiers), "Fury", ""));
        }

        [Test]
        public void ASkillThatCostsNothingIsFree()
        {
            Assert.AreEqual("Free", SkillListing.CostText(Skill(), "Mana", "Wool"));
        }

        [Test]
        public void TheCooldownIsAWaitOrOncePerFightOrNothing()
        {
            Assert.AreEqual("Cooldown 3", SkillListing.CooldownText(Skill(cooldown: 3)));
            Assert.AreEqual("Once per fight", SkillListing.CooldownText(Skill(cooldown: 3, oncePerFight: true)));
            Assert.AreEqual("", SkillListing.CooldownText(Skill()));
        }

        [TestCase(SkillTargeting.SingleEnemy, "One enemy")]
        [TestCase(SkillTargeting.AllEnemies, "All enemies")]
        [TestCase(SkillTargeting.Self, "Self")]
        [TestCase(SkillTargeting.Party, "Party")]
        [TestCase(SkillTargeting.SingleAlly, "One ally")]
        public void EveryTargetingHasWords(SkillTargeting targeting, string words)
        {
            Assert.AreEqual(words, SkillListing.TargetText(targeting));
        }

        [Test]
        public void MeleeReachesTheFrontRankOnly()
        {
            Assert.AreEqual("Front rank only", SkillListing.ReachText(Skill(reach: Reach.Melee)));
        }

        // The content counts ranks from 1, as a player does.
        [Test]
        public void ExplicitRanksAreListedAsThePlayerCountsThem()
        {
            Assert.AreEqual("Ranks 1, 2", SkillListing.ReachText(Skill(reach: Reach.FromContent(new[] { 1, 2 }))));
            Assert.AreEqual("Ranks 3", SkillListing.ReachText(Skill(reach: Reach.FromContent(new[] { 3 }))));
        }

        [Test]
        public void ReachIsSilentWhereRankIsNotAQuestion()
        {
            Assert.AreEqual("", SkillListing.ReachText(Skill()), "an unrestricted skill says nothing");
            Assert.AreEqual("", SkillListing.ReachText(Skill(targeting: SkillTargeting.Self, reach: Reach.Melee)));
            Assert.AreEqual("", SkillListing.ReachText(Skill(targeting: SkillTargeting.AllEnemies, reach: Reach.Melee)));
        }

        [Test]
        public void TheMetaLineJoinsWhatIsThereAndSkipsWhatIsNot()
        {
            var melee = Skill(manaCost: 8, cooldown: 2, reach: Reach.Melee);
            Assert.AreEqual("8 Mana  ·  Cooldown 2  ·  One enemy  ·  Front rank only",
                SkillListing.MetaText(melee, "Mana", "Wool"));

            Assert.AreEqual("Free  ·  Self",
                SkillListing.MetaText(Skill(targeting: SkillTargeting.Self), "Mana", "Wool"));
        }

        [TestCase(SkillSource.Innate, "INNATE")]
        [TestCase(SkillSource.Talent, "FROM TALENTS")]
        [TestCase(SkillSource.SpellBook, "FROM SPELL BOOKS")]
        [TestCase(SkillSource.RewardTrack, "FROM THE REWARD TRACK")]
        [TestCase(SkillSource.Taught, "TAUGHT")]
        public void EverySourceHasAHeader(SkillSource source, string header)
        {
            Assert.AreEqual(header, SkillListing.HeaderFor(source).Format());
        }
    }
}
