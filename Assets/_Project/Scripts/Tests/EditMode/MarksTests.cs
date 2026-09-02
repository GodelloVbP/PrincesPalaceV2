using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (a): a general MARK, reusable by any relic or skill effect
    // keyed on marked targets -- not owned by Drowned Lantern's own private
    // per-session mark, which predates this and is untested here.
    public class MarksTests
    {
        private static CombatantState Dummy() => new CombatantState("Dummy", false, 100, 0, 10, 10);

        [Test]
        public void AnUnmarkedTargetIsNotMarked()
        {
            var target = Dummy();
            Assert.IsFalse(Marks.IsMarked(target));
        }

        [Test]
        public void ApplyMarksTheTarget()
        {
            var source = Dummy();
            var target = Dummy();

            Marks.Apply(target, source);

            Assert.IsTrue(Marks.IsMarked(target));
        }

        [Test]
        public void ConsumeMarkRemovesItAndReportsTrueExactlyOnce()
        {
            var target = Dummy();
            Marks.Apply(target, null);

            Assert.IsTrue(Marks.ConsumeMark(target), "a real mark was there to consume");
            Assert.IsFalse(Marks.IsMarked(target), "the mark should be gone");
            Assert.IsFalse(Marks.ConsumeMark(target), "nothing left to consume a second time");
        }

        [Test]
        public void ConsumingAnUnmarkedTargetReportsFalse()
        {
            var target = Dummy();
            Assert.IsFalse(Marks.ConsumeMark(target));
        }

        [Test]
        public void ReapplyingAMarkDoesNotStackASecondEntry()
        {
            var target = Dummy();
            Marks.Apply(target, null);
            Marks.Apply(target, null);

            Assert.AreEqual(1, target.Statuses.Count, "StatusEffects.Apply refreshes, never stacks");
        }
    }
}
