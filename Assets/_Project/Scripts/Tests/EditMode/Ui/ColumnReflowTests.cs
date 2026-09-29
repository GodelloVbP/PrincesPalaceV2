using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    public class ColumnReflowTests
    {
        // Tops of an 80-tall item, then three 52-tall items, 20 apart:
        // 190, 90, 18, -54.
        private static readonly float[] Tops = { 190f, 90f, 18f, -54f };

        [Test]
        public void EverythingVisibleMovesNothing()
        {
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 0f },
                ColumnReflow.Shifts(Tops, new[] { true, true, true, true }));
        }

        [Test]
        public void AHiddenFirstItemMovesEveryoneAfterItUpByItsHeightAndSpacing()
        {
            CollectionAssert.AreEqual(new[] { 0f, 100f, 100f, 100f },
                ColumnReflow.Shifts(Tops, new[] { false, true, true, true }));
        }

        [Test]
        public void TwoHiddenItemsAddUp()
        {
            CollectionAssert.AreEqual(new[] { 0f, 100f, 172f, 172f },
                ColumnReflow.Shifts(Tops, new[] { false, false, true, true }));
        }

        [Test]
        public void AHiddenLastItemHasNothingToMove()
        {
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 0f },
                ColumnReflow.Shifts(Tops, new[] { true, true, true, false }));
        }
    }
}
