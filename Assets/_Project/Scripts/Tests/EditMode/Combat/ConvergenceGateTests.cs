using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (g): the acquisition gate a relic can declare -- offered
    // only to a party that has a convergence/ultimate ability. Today that
    // means a Transform skill (Shawn's Black Ram Mode is the one
    // implemented example); ConvergenceGate.IsConvergenceEffect is the
    // single definition of that reading.
    public class ConvergenceGateTests
    {
        [Test]
        public void ATransformSkillIsAConvergenceAbility()
        {
            Assert.IsTrue(ConvergenceGate.IsConvergenceEffect(SkillEffect.Transform));
        }

        [TestCase(SkillEffect.DamageSingle)]
        [TestCase(SkillEffect.HealSelf)]
        public void NoOtherEffectIsAConvergenceAbility(SkillEffect effect)
        {
            Assert.IsFalse(ConvergenceGate.IsConvergenceEffect(effect));
        }
    }
}
