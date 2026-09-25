using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // DialogueBust is pure path arithmetic -- no Resources, no Editor -- so
    // every case here is pinned against a literal rather than recomputed
    // from the same formula (CLAUDE.md gotcha 5).
    public class DialogueBustTests
    {
        // ---- ResourcePath ----------------------------------------------------

        [Test]
        public void ResourcePath_JoinsFolderAndExpression()
        {
            Assert.AreEqual("Portraits/Dialogue/sheep/happy",
                DialogueBust.ResourcePath("Portraits/Dialogue/sheep", "happy"));
        }

        [Test]
        public void ResourcePath_TrimsATrailingSlashOnTheFolder()
        {
            Assert.AreEqual("Portraits/Dialogue/sheep/happy",
                DialogueBust.ResourcePath("Portraits/Dialogue/sheep/", "happy"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void ResourcePath_EmptyFolder_ReturnsEmpty(string folder)
        {
            Assert.AreEqual("", DialogueBust.ResourcePath(folder, "happy"));
        }

        // ---- Fallbacks ---------------------------------------------------------

        [Test]
        public void Fallbacks_NonNeutralExpression_TriesItselfThenNeutral()
        {
            CollectionAssert.AreEqual(new[] { "happy", "neutral" }, DialogueBust.Fallbacks("happy").ToList());
        }

        // NO DUPLICATE: requesting neutral itself must not yield ["neutral",
        // "neutral"] -- a caller building a retry chain off this list would
        // otherwise attempt the same load twice for no reason.
        [Test]
        public void Fallbacks_NeutralExpression_YieldsNeutralOnce()
        {
            CollectionAssert.AreEqual(new[] { "neutral" }, DialogueBust.Fallbacks("neutral").ToList());
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void Fallbacks_EmptyExpression_YieldsNeutralOnly(string expression)
        {
            CollectionAssert.AreEqual(new[] { "neutral" }, DialogueBust.Fallbacks(expression).ToList());
        }

        [Test]
        public void Fallbacks_EndsOnNeutral_ForEveryKnownExpression()
        {
            foreach (string expression in new[] { "happy", "annoyed", "nervous", "sad", "surprised" })
            {
                var chain = DialogueBust.Fallbacks(expression).ToList();
                Assert.AreEqual("neutral", chain.Last(),
                    $"the fallback chain for '{expression}' must end on neutral");
            }
        }
    }
}
