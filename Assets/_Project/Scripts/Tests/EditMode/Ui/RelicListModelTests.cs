using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The relic list every surface shares (plan C24): held order, and paging
    // through the one Paging seam.
    public class RelicListModelTests
    {
        private static RelicRow Row(string id) =>
            new RelicRow(id, id.ToUpperInvariant(), "COMMON", RelicRarity.Common, "desc " + id, id, known: true);

        private static List<RelicRow> Rows(int count) =>
            Enumerable.Range(0, count).Select(i => Row("r" + i)).ToList();

        [Test]
        public void AnEmptyListIsOnePageWithNothingOnIt()
        {
            var model = new RelicListModel(Rows(0), 5);

            Assert.IsTrue(model.IsEmpty);
            Assert.AreEqual(1, model.PageCount);
            Assert.IsFalse(model.IsPaged);
            Assert.AreEqual(0, model.Visible.Count);
        }

        [Test]
        public void ExactlyOnePageOfRowsIsNotTwoPages()
        {
            var model = new RelicListModel(Rows(5), 5);

            Assert.AreEqual(1, model.PageCount);
            Assert.IsFalse(model.IsPaged);
            Assert.AreEqual(5, model.Visible.Count);
        }

        [Test]
        public void SevenRowsOfFiveMakeTwoPagesAndTheLastIsShortNotPadded()
        {
            var model = new RelicListModel(Rows(7), 5);

            Assert.AreEqual(2, model.PageCount);
            Assert.IsTrue(model.IsPaged);
            CollectionAssert.AreEqual(new[] { "r0", "r1", "r2", "r3", "r4" }, model.Visible.Select(r => r.Id).ToList());

            Assert.IsTrue(model.Step(1));
            Assert.AreEqual(1, model.Page);
            CollectionAssert.AreEqual(new[] { "r5", "r6" }, model.Visible.Select(r => r.Id).ToList());
        }

        [Test]
        public void HeldOrderIsTheOrderGiven()
        {
            var model = new RelicListModel(new[] { Row("zeta"), Row("alpha"), Row("mid") }, 5);

            CollectionAssert.AreEqual(new[] { "zeta", "alpha", "mid" }, model.Visible.Select(r => r.Id).ToList());
        }

        [Test]
        public void PagingClampsAtBothEndsAndSaysWhetherItMoved()
        {
            var model = new RelicListModel(Rows(7), 5);

            Assert.IsFalse(model.Step(-1), "stepped back off the first page");
            Assert.AreEqual(0, model.Page);

            Assert.IsTrue(model.Step(1));
            Assert.IsFalse(model.Step(1), "stepped on past the last page");
            Assert.AreEqual(1, model.Page);
            Assert.IsFalse(model.CanStep(1));
            Assert.IsTrue(model.CanStep(-1));
        }

        [Test]
        public void ShowRelicTurnsToItsPageAndReturnsItsSlot()
        {
            var model = new RelicListModel(Rows(12), 5);

            Assert.AreEqual(1, model.ShowRelic("r11"));
            Assert.AreEqual(2, model.Page);

            Assert.AreEqual(3, model.ShowRelic("r3"));
            Assert.AreEqual(0, model.Page);
        }

        [Test]
        public void ShowRelicOfAnIdTheListDoesNotHoldLeavesThePageAlone()
        {
            var model = new RelicListModel(Rows(12), 5);
            model.SetPage(1);

            Assert.AreEqual(-1, model.ShowRelic("nope"));
            Assert.AreEqual(1, model.Page);
        }

        // A list that shrank under the reader (a relic sold) must clamp rather
        // than show a blank page.
        [Test]
        public void ARebuiltListKeepsThePageWhereItStillExistsAndClampsWhereItDoesNot()
        {
            var old = new RelicListModel(Rows(12), 5);
            old.SetPage(2);

            var same = new RelicListModel(Rows(12), 5).WithPageOf(old);
            Assert.AreEqual(2, same.Page);

            var shorter = new RelicListModel(Rows(6), 5).WithPageOf(old);
            Assert.AreEqual(1, shorter.Page);
            Assert.AreEqual(1, shorter.Visible.Count);
        }

        [Test]
        public void ARelicRowCarriesWhatItWasGiven()
        {
            var row = new RelicRow("id", "Name", "RARE", RelicRarity.Rare, "Does a thing.", "icon", known: false);

            Assert.AreEqual("id", row.Id);
            Assert.AreEqual("Name", row.Name);
            Assert.AreEqual("RARE", row.RarityText);
            Assert.AreEqual(RelicRarity.Rare, row.Rarity);
            Assert.AreEqual("Does a thing.", row.Description);
            Assert.AreEqual("icon", row.IconId);
            Assert.IsFalse(row.Known);
        }
    }
}
