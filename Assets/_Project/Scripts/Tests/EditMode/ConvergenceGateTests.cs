using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (g): the acquisition gate a relic can declare -- offered
    // only to a party that has a convergence/ultimate ability. Today that
    // means a Transform skill (Shawn's Black Ram Mode is the one
    // implemented example); ConvergenceGate.HasConvergenceAbility is a pure
    // predicate over resolved skills, so this pins that reading directly
    // rather than reaching for a save or a squad.
    public class ConvergenceGateTests
    {
        private static ResolvedSkill Skill(SkillEffect effect) =>
            new ResolvedSkill("s", "S", "", "hero", 1, effect, SkillTargeting.Self,
                0, 0, false, 0, 1, false, null, SpellPresentation.None, 0);

        [Test]
        public void APartyWithNoTransformSkillHasNoConvergenceAbility()
        {
            var skills = new List<ResolvedSkill> { Skill(SkillEffect.DamageSingle), Skill(SkillEffect.HealSelf) };
            Assert.IsFalse(ConvergenceGate.HasConvergenceAbility(skills));
        }

        [Test]
        public void ATransformSkillIsAConvergenceAbility()
        {
            var skills = new List<ResolvedSkill> { Skill(SkillEffect.DamageSingle), Skill(SkillEffect.Transform) };
            Assert.IsTrue(ConvergenceGate.HasConvergenceAbility(skills));
        }

        [Test]
        public void ThePartyOverloadChecksEveryKit()
        {
            var noConvergence = new PlayerKit("a", CharacterRole.Tank,
                new List<ResolvedSkill> { Skill(SkillEffect.DamageSingle) }, null, null);
            var hasConvergence = new PlayerKit("b", CharacterRole.Utility,
                new List<ResolvedSkill> { Skill(SkillEffect.Transform) }, null, null);

            Assert.IsFalse(ConvergenceGate.HasConvergenceAbility(new[] { noConvergence }));
            Assert.IsTrue(ConvergenceGate.HasConvergenceAbility(new[] { noConvergence, hasConvergence }));
        }

        [Test]
        public void AnEmptyOrNullPartyHasNoConvergenceAbility()
        {
            Assert.IsFalse(ConvergenceGate.HasConvergenceAbility((IEnumerable<PlayerKit>)null));
            Assert.IsFalse(ConvergenceGate.HasConvergenceAbility(new List<PlayerKit>()));
        }
    }
}
