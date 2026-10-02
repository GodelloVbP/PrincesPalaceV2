using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.PlayModeTests
{
    // The status-application cue table: keyed by status type, every take a
    // clip that loads, harmful statuses silent.
    public class StatusCuesTests
    {
        [TestCase(StatusEffectType.Shielded, 3)]
        [TestCase(StatusEffectType.Protect, 3)]
        [TestCase(StatusEffectType.Regen, 3)]
        [TestCase(StatusEffectType.Poison, 0)]
        [TestCase(StatusEffectType.Stun, 0)]
        public void EachStatusHasItsTakes(StatusEffectType type, int takes)
        {
            Assert.AreEqual(takes, StatusCues.ClipsFor(type).Count);
        }

        [Test]
        public void EveryTakeLoads()
        {
            foreach (var type in new[] { StatusEffectType.Shielded, StatusEffectType.Regen })
            {
                foreach (var path in StatusCues.ClipsFor(type))
                {
                    Assert.IsNotNull(SoundController.ClipAt(path), path);
                }
            }
        }
    }
}
