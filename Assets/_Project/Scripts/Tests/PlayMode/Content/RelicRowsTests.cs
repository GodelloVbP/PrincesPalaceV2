using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // Relic ids into the rows every relic list paints (plan C24): one lookup,
    // held order kept, an unknown id named by itself.
    public class RelicRowsTests
    {
        private static List<string> Ids(int count) =>
            ContentDatabase.Relics.Where(r => r != null).Take(count).Select(r => r.id).ToList();

        [Test]
        public void EveryKnownIdBecomesARowCarryingTheDefinitionsOwnFields()
        {
            var ids = Ids(3);
            Assert.AreEqual(3, ids.Count, "fixture: the content has too few relics");

            var rows = RelicRows.From(ids);

            Assert.AreEqual(3, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var data = ContentDatabase.GetRelic(ids[i]).Data;
                Assert.AreEqual(ids[i], rows[i].Id);
                Assert.AreEqual(data.DisplayName, rows[i].Name);
                Assert.AreEqual(RelicRarityNames.Of(data.Rarity), rows[i].RarityText);
                Assert.AreEqual(data.Rarity, rows[i].Rarity);
                Assert.AreEqual(data.Description, rows[i].Description);
                Assert.AreEqual(ids[i], rows[i].IconId);
                Assert.IsTrue(rows[i].Known);
            }
        }

        [Test]
        public void HeldOrderIsKeptWhateverTheContentOrderIs()
        {
            var ids = Ids(3);
            ids.Reverse();

            CollectionAssert.AreEqual(ids, RelicRows.From(ids).Select(r => r.Id).ToList());
        }

        [Test]
        public void AnUnknownIdGetsARowNamedByItself()
        {
            var rows = RelicRows.From(new List<string> { "relic_that_was_deleted" });

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("relic_that_was_deleted", rows[0].Id);
            Assert.AreEqual("relic_that_was_deleted", rows[0].Name);
            Assert.AreEqual("Unknown relic", rows[0].Description);
            Assert.AreEqual("", rows[0].RarityText);
            Assert.IsFalse(rows[0].Known);
        }

        [Test]
        public void NothingHeldIsNoRows()
        {
            Assert.IsEmpty(RelicRows.From(null));
            Assert.IsEmpty(RelicRows.From(new List<string>()));
        }
    }
}
