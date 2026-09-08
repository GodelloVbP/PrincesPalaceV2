using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // ONE WALK, TWO CALLERS: ContentDatabase.Validation's rule 4 (build-time)
    // and RewardTrackEntryResolver.RewardTrackCharacterContext.BuildAll (what
    // actually gates a filler reward row) used to compute this set
    // independently. Pinned here once so a change to the rule shows up in
    // one test rather than needing to be re-derived at each call site.
    public class SkillDamageTypesTests
    {
        private static ResolvedSkill PacketSkill(DamageType type) =>
            new ResolvedSkill("packet", "Packet", "", "shawn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 4, 0, false, 100, 0, false,
                new[] { new DamageInstance(type, 10) }, SpellPresentation.None, 0);

        private static ResolvedSkill ElementSkill(params ElementChoice[] elements) =>
            new ResolvedSkill("orb", "Orb", "", "shawn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 6, 0, false, 100, 0, false,
                null, SpellPresentation.None, 0, elements: elements);

        [Test]
        public void TheAttackTypeAlwaysStartsTheSet()
        {
            var result = SkillDamageTypes.AtLevel1(DamageType.Physical, new List<ResolvedSkill>());

            CollectionAssert.AreEquivalent(new[] { DamageType.Physical }, result);
        }

        // ONE PACKET SKILL AND ONE ELEMENT SKILL, THE ELEMENT SKILL CARRYING
        // A NULL SLOT -- the fixture the finding names. The null slot must
        // not throw and must not appear as a phantom DamageType in the set;
        // the packet type and the one real element choice both must.
        [Test]
        public void APacketSkillAndAnElementSkillWithANullSlotBothContribute()
        {
            var level1Skills = new List<ResolvedSkill>
            {
                PacketSkill(DamageType.Fire),
                ElementSkill(new ElementChoice(DamageType.Ice), null),
            };

            var result = SkillDamageTypes.AtLevel1(DamageType.Physical, level1Skills);

            CollectionAssert.AreEquivalent(
                new[] { DamageType.Physical, DamageType.Fire, DamageType.Ice }, result);
        }

        [Test]
        public void ANullSkillListStillReturnsJustTheAttackType()
        {
            var result = SkillDamageTypes.AtLevel1(DamageType.Arcane, null);

            CollectionAssert.AreEquivalent(new[] { DamageType.Arcane }, result);
        }

        // THE VALIDATION-SIDE GAP THE FINDING NAMES: a hand-authored asset
        // whose Elements array reads back null (predates the field, or a
        // ScriptableObject that never ran ResolvedSkill's own constructor)
        // must not throw either.
        [Test]
        public void ASkillWithNullDamageInstancesAndNullElementsContributesNothingButDoesNotThrow()
        {
            var bareSkill = new ResolvedSkill { DamageInstances = null, Elements = null };

            HashSet<DamageType> result = null;
            Assert.DoesNotThrow(() =>
                result = SkillDamageTypes.AtLevel1(DamageType.Physical, new List<ResolvedSkill> { bareSkill }));

            CollectionAssert.AreEquivalent(new[] { DamageType.Physical }, result);
        }
    }
}
