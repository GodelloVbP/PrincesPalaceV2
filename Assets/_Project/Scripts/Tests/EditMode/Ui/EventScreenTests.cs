using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The event panel's tree, audited before any scene exists -- the shop's
    // posture (ShopScreenTests), since both nest in the map.
    //
    // WHAT THIS FILE CANNOT DO is measure glyphs: Domain has no font. The
    // 600-character body is measured twice elsewhere -- by UiTextFitAudit at
    // every scene build, against UiStrings.EventBody's sample, and by
    // EventPanelTests.ABodyAtTheContentCapFitsTheBodyBox on the real emitted
    // label. What is pinned here is the wiring between the two: that the
    // sample IS the content cap long, so neither measurement can go stale
    // against a cap that moved.
    public class EventScreenTests
    {
        private static UiNode Tree() => EventScreen.Build().Root;

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void ItAuditsCleanInsideTheMapItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(MapScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void TheBodyBoxIsMeasuredAgainstABodyExactlyAtTheContentCap()
        {
            var body = Walk(Tree()).First(n => n.Name == "EventBody");

            Assert.AreEqual(UiStrings.EventBody.Key, body.Text.Key,
                "the body box is no longer declared with the at-cap sample, so the build stopped measuring it");
            Assert.AreEqual(EventEntryResolver.MaxBodyLength, UiStrings.EventBody.AuditSample.Length);
        }

        // Title, choice text and lock caption are content-authored too; each
        // box must be declared with the sample that is exactly its resolver
        // cap long, so the scene build measures the longest string content
        // can put there.
        [Test]
        public void EveryAuthoredTextBoxIsMeasuredAgainstASampleExactlyAtItsCap()
        {
            var screen = EventScreen.Build();
            var nodes = Walk(screen.Root).ToList();

            Assert.AreEqual(UiStrings.EventTitle.Key, nodes.First(n => n.Name == "EventTitle").Text.Key);
            Assert.AreEqual(UiStrings.EventChoice.Key, nodes.First(n => n.Name == "EventChoice0Text").Text.Key);
            Assert.AreEqual(UiStrings.EventChoiceLocked.Key, nodes.First(n => n.Name == "EventChoice0Lock").Text.Key);

            Assert.AreEqual(EventEntryResolver.MaxTitleLength, UiStrings.EventTitle.AuditSample.Length);
            Assert.AreEqual(EventEntryResolver.MaxChoiceTextLength, UiStrings.EventChoice.AuditSample.Length);
            Assert.AreEqual(EventEntryResolver.MaxLockReasonLength, UiStrings.EventChoiceLocked.AuditSample.Length);
        }

        // Pinned as literals: this is the size art is commissioned at
        // (docs/EVENTS.md, docs/ART_PIPELINE.md), and a change here is a
        // change to every brief already sent out.
        [Test]
        public void TheArtFrameIsA960By720FourByThreeThatKeepsItsAspect()
        {
            var art = Walk(Tree()).First(n => n.Name == "EventArt");

            Assert.AreEqual(960f, art.Size.X);
            Assert.AreEqual(720f, art.Size.Y);
            Assert.IsTrue(art.PreserveAspect, "a slightly-off delivery would stretch");
            Assert.IsNull(art.SpriteKey, "the art is chosen per page at runtime, never baked into the node");
        }

        [Test]
        public void ThePanelStartsHidden()
        {
            Assert.IsTrue(Tree().StartInactive);
        }

        [Test]
        public void ThereAreFourRows_EachStartingHiddenWithAHiddenLockCaption()
        {
            var screen = EventScreen.Build();

            Assert.AreEqual(4, screen.ChoiceRows.Count, "a page holds at most four choices");
            foreach (var row in screen.ChoiceRows)
            {
                Assert.IsTrue(row.Button.Node.StartInactive, $"{row.Button.Node.Name} shows before it is painted");
                Assert.IsTrue(row.Text.IsValid);
                Assert.IsTrue(row.Lock.Node.StartInactive, $"{row.Lock.Node.Name} shows on an open choice");
            }
        }

        // UiEmitter names a button's own caption "<button>Label"; a declared
        // child by that name would vanish from every by-name lookup
        // (ReckoningScreenTests has the history).
        [Test]
        public void NoRowChildCollidesWithItsButtonsGeneratedCaption()
        {
            var offenders = EventScreen.Build().ChoiceRows
                .Select(r => r.Button.Node)
                .SelectMany(b => b.Children.Where(c => c.Name == b.Name + "Label").Select(c => c.Name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
    }
}
