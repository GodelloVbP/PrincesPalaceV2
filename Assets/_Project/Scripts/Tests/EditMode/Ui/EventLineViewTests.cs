using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // What the dialogue stage is handed per line (docs/PLAN_DIALOGUE_STAGE.md
    // D2): the resolved line joined to its speaker, and HasLines deciding
    // stage versus legacy (contract 14).
    public class EventLineViewTests
    {
        private static ResolvedCharacter Sheep()
        {
            var character = new ResolvedCharacter();
            character.Id = "sheep";
            character.DisplayName = "Shawn";
            character.Epithet = "";
            character.DialogueBustPath = "Portraits/Dialogue/sheep";
            return character;
        }

        private static ResolvedEventPage Page(params ResolvedEventLine[] lines) =>
            new ResolvedEventPage("p", "", "Title", "Body", new ResolvedEventChoice[0], "Assets/_Project/Art/Backgrounds/Dungeon.png",
                null, lines);

        [Test]
        public void APageWithoutLinesGivesAViewWithoutLines()
        {
            var lines = EventLineView.ListFor(Page(), _ => Sheep());
            var view = new EventView("e", "p", "", "Title", "Body", false, "", "", null,
                "Assets/_Project/Art/Backgrounds/Dungeon.png", lines);

            Assert.AreEqual(0, lines.Count);
            Assert.IsFalse(view.HasLines, "a lines-less page must paint the legacy layout (contract 14)");
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", view.BackdropKey,
                "every page carries a backdrop, so the backdrop cannot be what picks the stage");
        }

        [Test]
        public void TheOldConstructorStillMeansNoLines()
        {
            var view = new EventView("e", "p", "", "", "", false, "", "", null);

            Assert.IsFalse(view.HasLines);
            Assert.AreEqual("", view.BackdropKey);
        }

        [Test]
        public void ASpokenLineCarriesItsSpeakersNameBustAndExpression()
        {
            var page = Page(new ResolvedEventLine("sheep", false, DialogueExpression.Happy, DialogueSide.Right, "Baa."));
            var lines = EventLineView.ListFor(page, id => id == "sheep" ? Sheep() : null);
            var view = new EventView("e", "p", "", "", "", false, "", "", null, "", lines);

            Assert.IsTrue(view.HasLines);
            var line = view.Lines[0];
            Assert.AreEqual("Shawn", line.SpeakerName);
            Assert.AreEqual("Portraits/Dialogue/sheep", line.BustFolder);
            Assert.AreEqual("happy", line.Expression);
            Assert.AreEqual(DialogueSide.Right, line.Side);
            Assert.AreEqual("Baa.", line.Text);
            Assert.IsFalse(line.IsNarration);
        }

        [Test]
        public void NarrationHasNoSpeakerNameOrBust()
        {
            var lookedUp = new List<string>();
            var page = Page(new ResolvedEventLine("", true, DialogueExpression.Neutral, DialogueSide.Left, "<i>Wind.</i>"));
            var line = EventLineView.ListFor(page, id => { lookedUp.Add(id); return Sheep(); })[0];

            Assert.IsTrue(line.IsNarration);
            Assert.AreEqual("", line.SpeakerName);
            Assert.AreEqual("", line.BustFolder);
            CollectionAssert.IsEmpty(lookedUp, "narration names nobody to look up");
        }

        // ---- event speakers (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.3) ---------

        private static ResolvedEventSpeaker Merchant(string bustPath = "Portraits/Dialogue/rat_merchant") =>
            new ResolvedEventSpeaker("merchant", "Grisk", "Purveyor of Fine Goods", bustPath,
                new[] { "neutral", "grinning", "hostile" });

        [Test]
        public void AnEventSpeakersLineCarriesItsOwnNameEpithetFolderAndDeclaredFace_OnEitherSide()
        {
            var page = Page(
                ResolvedEventLine.ForEventSpeaker("merchant", "grinning", DialogueSide.Right, "Everything must go."),
                ResolvedEventLine.ForEventSpeaker("merchant", "hostile", DialogueSide.Left, "Put it back."));
            var lookedUpCharacters = new List<string>();
            var lines = EventLineView.ListFor(page, id => { lookedUpCharacters.Add(id); return null; },
                id => id == "merchant" ? Merchant() : null);

            Assert.AreEqual("Grisk", lines[0].SpeakerName);
            Assert.AreEqual("Purveyor of Fine Goods", lines[0].Epithet);
            Assert.AreEqual("Portraits/Dialogue/rat_merchant", lines[0].BustFolder);
            Assert.AreEqual("grinning", lines[0].Expression);
            Assert.AreEqual(DialogueSide.Right, lines[0].Side);

            Assert.AreEqual("hostile", lines[1].Expression);
            Assert.AreEqual(DialogueSide.Left, lines[1].Side);
            CollectionAssert.IsEmpty(lookedUpCharacters, "an event speaker is never looked up as a character");
        }

        // The stage's fallback walk is the same for both kinds: the requested
        // face, then neutral, then no bust at all -- with the name plate
        // still carrying the name (contract 15's posture).
        [Test]
        public void AnEventSpeakerWithMissingArt_FallsBackToNeutral_ThenToNoBust()
        {
            var line = EventLineView.ListFor(
                Page(ResolvedEventLine.ForEventSpeaker("merchant", "grinning", DialogueSide.Right, "Bargains.")),
                _ => null, _ => Merchant())[0];

            string onlyNeutral = DialogueBust.FirstAvailable(line.BustFolder, line.Expression,
                path => path == "Portraits/Dialogue/rat_merchant/neutral");
            Assert.AreEqual("Portraits/Dialogue/rat_merchant/neutral", onlyNeutral);

            string none = DialogueBust.FirstAvailable(line.BustFolder, line.Expression, _ => false);
            Assert.AreEqual("", none, "no file on either rung shows no bust");
            Assert.AreEqual("Grisk", line.SpeakerName);

            var noFolder = EventLineView.ListFor(
                Page(ResolvedEventLine.ForEventSpeaker("merchant", "neutral", DialogueSide.Left, "...")),
                _ => null, _ => Merchant(bustPath: ""))[0];
            Assert.AreEqual("", noFolder.BustFolder);
            Assert.AreEqual("", DialogueBust.FirstAvailable(noFolder.BustFolder, noFolder.Expression, _ => true),
                "a speaker with no bustPath has no bust whatever exists");
        }

        [Test]
        public void AnEventSpeakerTheEventNoLongerDeclares_KeepsItsIdOnThePlate()
        {
            var line = EventLineView.ListFor(
                Page(ResolvedEventLine.ForEventSpeaker("merchant", "grinning", DialogueSide.Right, "...")),
                _ => Sheep(), _ => null)[0];

            Assert.AreEqual("merchant", line.SpeakerName);
            Assert.AreEqual("", line.BustFolder);
            Assert.AreEqual("", line.Epithet);
        }

        // Shawn's bell page face: a party character's expression enum member,
        // loaded down the same folder + file-name path as every other face.
        [Test]
        public void ShawnsEntrancedFaceIsTheEntrancedFileInHisOwnFolder()
        {
            var line = EventLineView.ListFor(
                Page(new ResolvedEventLine("sheep", false, DialogueExpression.Entranced, DialogueSide.Left, "The bell...")),
                id => id == "sheep" ? Sheep() : null)[0];

            Assert.AreEqual("entranced", line.Expression);
            Assert.AreEqual("Portraits/Dialogue/sheep", line.BustFolder);
            Assert.AreEqual("Portraits/Dialogue/sheep/entranced",
                DialogueBust.FirstAvailable(line.BustFolder, line.Expression, _ => true));
        }

        // Contract 15's posture: a speaker content no longer has still gets a
        // name on the plate and simply no bust.
        [Test]
        public void AnUnknownSpeakerFallsBackToItsIdAndNoBust()
        {
            var page = Page(new ResolvedEventLine("ghost", false, DialogueExpression.Sad, DialogueSide.Left, "..."));
            var line = EventLineView.ListFor(page, _ => null)[0];

            Assert.AreEqual("ghost", line.SpeakerName);
            Assert.AreEqual("", line.BustFolder);
            Assert.AreEqual("", line.Epithet);
        }
    }
}
