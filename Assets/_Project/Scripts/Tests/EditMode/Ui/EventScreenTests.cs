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

        // ---- the dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, D2) -------------

        [Test]
        public void TheStageCarriesEveryLayerBackToFront()
        {
            var stage = EventScreen.Build().DialogueStage.Node;

            CollectionAssert.AreEqual(
                new[]
                {
                    "StageGround", "StageBackdrop", "StageShadeTop", "StageShadeBottom", "StageVignette",
                    "StageSetPiece", "StageBustA", "StageBustB", "StageBox", "StageNamePlate", "StageTitle",
                },
                stage.Children.Select(c => c.Name).ToArray(),
                "uGUI draws later siblings on top, so this order IS the plan's layer table");

            var names = Walk(stage).Select(n => n.Name).ToList();
            CollectionAssert.Contains(names, "StageLineText");
            CollectionAssert.Contains(names, "StageName");
            CollectionAssert.Contains(names, "StageEpithet");
        }

        // Contract 14: nothing about the stage shows unless a page has lines,
        // and the controller is what turns it on.
        [Test]
        public void TheStageStartsHiddenAndMasksItsOverhang()
        {
            var screen = EventScreen.Build();

            Assert.IsTrue(screen.DialogueStage.Node.StartInactive);
            Assert.IsTrue(screen.DialogueStage.Node.Masks, "the cover-cropped backdrop would draw past the canvas");
            Assert.IsTrue(screen.StageBusts[1].Node.StartInactive, "the second bust is D3's slide partner");
            Assert.IsTrue(screen.StageEpithet.Node.StartInactive, "no character has an epithet yet");
            Assert.IsFalse(screen.ArtFrame.Node.StartInactive, "the legacy layout is the default");
            Assert.IsFalse(screen.TextColumn.Node.StartInactive);
        }

        // The rows are shared, not duplicated: one panel, drawn after the
        // stage so both presentations can show them.
        [Test]
        public void TheRowsLiveInOneSharedPanelDrawnOverTheStage()
        {
            var screen = EventScreen.Build();
            var children = screen.Root.Children.Select(c => c.Name).ToList();

            Assert.AreEqual(4, Walk(screen.Root).Count(n => n.Name == "EventChoice0" || n.Name == "EventChoice1"
                || n.Name == "EventChoice2" || n.Name == "EventChoice3"));
            Assert.Greater(children.IndexOf("EventChoices"), children.IndexOf("DialogueStage"));
            CollectionAssert.AreEqual(
                screen.ChoiceRows.Select(r => r.Button.Node).ToArray(),
                screen.ChoicesPanel.Node.Children.ToArray());
        }

        // The legacy rows kept their screen positions through the move to
        // their own panel -- pinned as literals from before the move.
        [Test]
        public void TheLegacyRowsSitWhereTheyAlwaysDid()
        {
            var solved = UiSolver.Solve(Tree(), UiFrames.Reference);
            var rowNames = new[] { "EventChoice0", "EventChoice1", "EventChoice2", "EventChoice3" };
            var rows = solved.Descendants().Where(n => rowNames.Contains(n.Name))
                .OrderBy(n => n.Name).ToList();

            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual(512f, rows[0].Rect.Centre.X, 0.01f);
            Assert.AreEqual(-164f, rows[0].Rect.Centre.Y, 0.01f);
            Assert.AreEqual(-440f, rows[3].Rect.Centre.Y, 0.01f);
        }

        // Owner rule 2026-09-23: container art is never stretched. The box
        // and plate are plain sprites at exactly half their painted size.
        [Test]
        public void TheBoxAndPlateAreDrawnAtTheirPaintedAspect()
        {
            var screen = EventScreen.Build();

            Assert.AreEqual(1280f, screen.StageBox.Node.Size.X);
            Assert.AreEqual(256f, screen.StageBox.Node.Size.Y);
            Assert.AreEqual(480f, screen.StageNamePlate.Node.Size.X);
            Assert.AreEqual(96f, screen.StageNamePlate.Node.Size.Y);
            Assert.AreEqual(EventScreen.BoxSpriteKey, screen.StageBox.Node.SpriteKey);
            Assert.AreEqual(EventScreen.PlateSpriteKey, screen.StageNamePlate.Node.SpriteKey);
        }

        // At the reference frame, a left speaker: the bust against the left
        // edge, the box 608 in with its far edge 32 from the right, the plate
        // straddling the box's top edge.
        [Test]
        public void TheBuildLayoutIsTheLeftSpeakerLayout()
        {
            var solved = UiSolver.Solve(Tree(), UiFrames.Reference);
            UiRect Rect(string name) => solved.Descendants().First(n => n.Name == name).Rect;

            var box = Rect("StageBox");
            Assert.AreEqual(608f - 960f, box.Left, 0.01f);
            Assert.AreEqual(960f - 32f, box.Right, 0.01f);
            Assert.AreEqual(32f - 540f, box.Bottom, 0.01f);

            var bust = Rect("StageBustA");
            Assert.AreEqual(-960f, bust.Left, 0.01f);
            Assert.AreEqual(-540f, bust.Bottom, 0.01f);
            Assert.AreEqual(840f, bust.Height, 0.01f);

            var plate = Rect("StageNamePlate");
            Assert.AreEqual(648f - 960f, plate.Left, 0.01f);
            Assert.Less(plate.Bottom, box.Top, "the plate should overlap the box's top edge");
            Assert.Greater(plate.Top, box.Top);
        }

        // The box text is measured against a line exactly at the content cap
        // (contract 13), the epithet against its own cap.
        [Test]
        public void TheStageTextBoxesAreMeasuredAgainstTheirCaps()
        {
            var screen = EventScreen.Build();

            Assert.AreEqual(UiStrings.EventLine.Key, screen.StageLineText.Node.Text.Key);
            Assert.AreEqual(EventEntryResolver.MaxLineLength, UiStrings.EventLine.AuditSample.Length);
            Assert.AreEqual(UiStrings.EventEpithet.Key, screen.StageEpithet.Node.Text.Key);
            Assert.AreEqual(CharacterEntryResolver.MaxEpithetLength, UiStrings.EventEpithet.AuditSample.Length);
            Assert.IsTrue(screen.StageName.Node.Truncates, "a runtime name has no sample to be measured against");
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
