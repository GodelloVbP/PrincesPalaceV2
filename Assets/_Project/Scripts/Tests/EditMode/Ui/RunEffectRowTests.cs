using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // What the RUN pane's effects column says (plan C23): one row definition
    // per EventBuffs kind, and a test that fails on a kind without one.
    public class RunEffectRowTests
    {
        private static EventBuffEntry Buff(string kind, int amount, int leg = EventBuffs.WholeRun) =>
            new EventBuffEntry { kind = kind, amount = amount, legStartStep = leg };

        // THE GUARD. Reflection over the public string consts of EventBuffs, so
        // a kind added there without a row (or without joining Kinds) fails
        // here rather than shipping as an effect the pane cannot show.
        [Test]
        public void EveryEventBuffKindHasARowDefinition()
        {
            var kinds = typeof(EventBuffs)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

            Assert.GreaterOrEqual(kinds.Count, 2, "vacuous: EventBuffs declares no kinds");

            foreach (var kind in kinds)
            {
                CollectionAssert.Contains(EventBuffs.Kinds, kind, $"'{kind}' is a kind but not in EventBuffs.Kinds");
                Assert.IsTrue(RunEffectRows.DefFor(kind).HasValue, $"EventBuffs kind '{kind}' has no RunEffectRows definition");
            }

            CollectionAssert.AreEquivalent(EventBuffs.Kinds, RunEffectRows.Buffs.Select(d => d.Kind).ToList(),
                "a definition names a kind EventBuffs does not have, or lists one twice");
        }

        [Test]
        public void NothingActiveIsNoRows()
        {
            var rows = RunEffectRows.Rows(new List<EventBuffEntry>(), legStartStep: 3, squadFavor: 0,
                bonusDamagePercent: 0, secondLivesLeft: 0);

            Assert.IsEmpty(rows);
        }

        // Shawn's authored 4 plus a +10 event gift is the 14 the offers roll
        // with; the gift is inside the figure, so it gets no row of its own.
        [Test]
        public void TheSquadFavorIsOneRowWithTheFigureTheOffersUse()
        {
            var rows = RunEffectRows.Rows(new List<EventBuffEntry> { Buff(EventBuffs.PrincesFavor, 10) }, 0,
                squadFavor: 14, bonusDamagePercent: 0, secondLivesLeft: 0);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Prince's favor", rows[0].Label);
            Assert.AreEqual("14", rows[0].Value);
            Assert.AreEqual(
                "The best Favor among your squad, plus any gifts this run: the higher it is, the better the items you are offered.",
                rows[0].Detail);
        }

        // Zero hides the row, as every other figure on the pane does; it
        // appears the moment the squad has any.
        [Test]
        public void FavorAtZeroHasNoRowAndAtOneHasOne()
        {
            Assert.IsEmpty(RunEffectRows.Rows(null, 0, squadFavor: 0, bonusDamagePercent: 0, secondLivesLeft: 0));

            var rows = RunEffectRows.Rows(null, 0, squadFavor: 1, bonusDamagePercent: 0, secondLivesLeft: 0);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("1", rows[0].Value);
        }

        [Test]
        public void ALegBuffSaysThisLegAndAWholeRunBuffSaysThisRun()
        {
            var leg = RunEffectRows.Rows(new List<EventBuffEntry> { Buff(EventBuffs.FillSpecialPool, 0, leg: 4) }, 4, 0, 0, 0);
            var run = RunEffectRows.Rows(new List<EventBuffEntry> { Buff(EventBuffs.FillSpecialPool, 0) }, 4, 0, 0, 0);

            Assert.AreEqual("Special pools full each turn — this leg", leg[0].Label);
            Assert.AreEqual("", leg[0].Value);
            Assert.AreEqual("Special pools full each turn — this run", run[0].Label);
        }

        // A leg buff is never cleared: it stops matching once the leg moves on.
        [Test]
        public void ALegBuffFromAnEarlierLegIsNotShown()
        {
            var rows = RunEffectRows.Rows(new List<EventBuffEntry> { Buff(EventBuffs.FillSpecialPool, 0, leg: 2) }, 4, 0, 0, 0);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void FavorThenBuffsThenBonusDamageThenSecondLives()
        {
            var rows = RunEffectRows.Rows(new List<EventBuffEntry> { Buff(EventBuffs.FillSpecialPool, 0) }, 0,
                squadFavor: 4, bonusDamagePercent: 6, secondLivesLeft: 1);

            CollectionAssert.AreEqual(
                new[] { "Prince's favor", "Special pools full each turn — this run", "Bonus damage", "Second lives" },
                rows.Select(r => r.Label).ToList());
            CollectionAssert.AreEqual(new[] { "4", "", "+6 %", "1" }, rows.Select(r => r.Value).ToList());
        }

        [Test]
        public void AFigureAtZeroHasNoRow()
        {
            var rows = RunEffectRows.Rows(null, 0, squadFavor: 0, bonusDamagePercent: 0, secondLivesLeft: 0);

            Assert.IsEmpty(rows);
        }
    }
}
