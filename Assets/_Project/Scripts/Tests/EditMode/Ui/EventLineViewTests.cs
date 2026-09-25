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
