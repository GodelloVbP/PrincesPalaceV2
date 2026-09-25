using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // The dialogue stage's state machine (docs/PLAN_DIALOGUE_STAGE.md,
    // Transition table and contracts 3, 17, 18). Every count is a literal
    // worked out by hand at 60 characters a second and a 0.2 s slide --
    // never recomputed from the constants under test.
    public class DialoguePlaybackTests
    {
        private static EventLineView Say(string speaker, DialogueSide side, string text, string expression = "neutral") =>
            new EventLineView(speaker, false, speaker, "", "Portraits/Dialogue/" + speaker, expression, side, text);

        private static EventLineView Narrate(string text) =>
            new EventLineView("", true, "", "", "", "neutral", DialogueSide.Left, text);

        private static DialoguePlayback Play(params EventLineView[] lines) =>
            new DialoguePlayback(DialogueBeat.Sequence("", "", lines));

        // 30 characters.
        private const string Thirty = "abcdefghijklmnopqrstuvwxyz0123";

        [Test]
        public void TheConstantsArePinned()
        {
            Assert.AreEqual(60f, DialoguePlayback.CharsPerSecond);
            Assert.AreEqual(0.2f, DialoguePlayback.SlideSeconds);
        }

        [Test]
        public void TheFirstSpeakerSlidesIn_ThenTheTypewriterCountsAtSixtyASecond()
        {
            var playback = Play(Say("sheep", DialogueSide.Left, Thirty));

            Assert.AreEqual(DialogueState.Entering, playback.State);
            Assert.AreEqual(BustCue.SlideIn, playback.Cue);
            Assert.AreEqual(0, playback.VisibleCharacters);

            playback.Tick(0.1f);
            Assert.AreEqual(DialogueState.Entering, playback.State);
            Assert.AreEqual(0.5f, playback.SlideProgress, 1e-4f);

            playback.Tick(0.1f);
            Assert.AreEqual(DialogueState.Revealing, playback.State);
            Assert.AreEqual(0, playback.VisibleCharacters);

            playback.Tick(0.25f);
            Assert.AreEqual(15, playback.VisibleCharacters);

            playback.Tick(0.1f);
            Assert.AreEqual(21, playback.VisibleCharacters);

            playback.Tick(0.15f);
            Assert.AreEqual(DialogueState.Revealed, playback.State);
            Assert.AreEqual(30, playback.VisibleCharacters);
        }

        [Test]
        public void TagsDoNotCount()
        {
            // "a bc defghi j"
            Assert.AreEqual(13, DialogueText.VisibleLength("a <i>bc</i> defghi j"));
            Assert.AreEqual(4, DialogueText.VisibleLength("<i>wool</i>"));
            Assert.AreEqual(3, DialogueText.VisibleLength("a<b"));
            Assert.AreEqual(0, DialogueText.VisibleLength(""));
        }

        [Test]
        public void APressCompletesTheLine_TheNextPressAdvances()
        {
            var playback = Play(Narrate(Thirty), Narrate("second"));

            Assert.AreEqual(DialogueState.Revealing, playback.State, "narration has no bust to slide in");
            playback.Tick(0.1f);
            Assert.AreEqual(6, playback.VisibleCharacters);

            Assert.IsTrue(playback.Advance(10));
            Assert.AreEqual(DialogueState.Revealed, playback.State);
            Assert.AreEqual(30, playback.VisibleCharacters);
            Assert.AreEqual(0, playback.BeatIndex);

            Assert.IsTrue(playback.Advance(11));
            Assert.AreEqual(1, playback.BeatIndex);
            Assert.AreEqual(DialogueState.Revealing, playback.State);
            Assert.AreEqual(0, playback.VisibleCharacters);
        }

        [Test]
        public void PressesDuringEnteringAndTransitioningAreIgnored_NotQueued()
        {
            var playback = Play(Say("sheep", DialogueSide.Left, "hi"), Say("owl", DialogueSide.Right, Thirty));

            Assert.IsFalse(playback.Advance(1), "Entering");
            Assert.AreEqual(DialogueState.Entering, playback.State);

            playback.Tick(0.2f);
            playback.Tick(0.1f);
            Assert.AreEqual(DialogueState.Revealed, playback.State);

            Assert.IsTrue(playback.Advance(2));
            Assert.AreEqual(DialogueState.Transitioning, playback.State);
            Assert.AreEqual(BustCue.SlideOutIn, playback.Cue);
            Assert.AreEqual("sheep", playback.Outgoing.Line.SpeakerId);
            Assert.AreEqual("owl", playback.Current.Line.SpeakerId);

            Assert.IsFalse(playback.Advance(3));
            Assert.IsFalse(playback.Advance(4));
            Assert.AreEqual(0, playback.VisibleCharacters);

            playback.Tick(0.2f);
            Assert.AreEqual(DialogueState.Revealing, playback.State, "the ignored presses did not complete the line");
            Assert.IsNull(playback.Outgoing);
            Assert.AreEqual(0, playback.VisibleCharacters);
        }

        [Test]
        public void TheSameSpeakerOnTheSameSide_SwapsExpressionInPlace()
        {
            var playback = Play(Say("sheep", DialogueSide.Left, "a"), Say("sheep", DialogueSide.Left, "b", "happy"));
            playback.Tick(0.2f);
            playback.Tick(0.1f);

            Assert.IsTrue(playback.Advance(1));
            Assert.AreEqual(BustCue.SwapInPlace, playback.Cue);
            Assert.AreEqual(DialogueState.Revealing, playback.State, "no transition");
            Assert.IsNull(playback.Outgoing);
            Assert.AreEqual(1f, playback.SlideProgress);
            Assert.AreEqual("happy", playback.Current.Line.Expression);
        }

        [Test]
        public void TheSameSpeakerOnTheOtherSide_Transitions()
        {
            var playback = Play(Say("sheep", DialogueSide.Left, "a"), Say("sheep", DialogueSide.Right, "b"));
            playback.Tick(0.2f);
            playback.Tick(0.1f);

            playback.Advance(1);
            Assert.AreEqual(DialogueState.Transitioning, playback.State);
        }

        [Test]
        public void NarrationHidesTheBust_AndTheNextSpeakerSlidesInAgain()
        {
            var playback = Play(Say("sheep", DialogueSide.Left, "a"), Narrate("b"), Say("owl", DialogueSide.Right, "c"));
            playback.Tick(0.2f);
            playback.Tick(0.1f);

            playback.Advance(1);
            Assert.AreEqual(BustCue.Hidden, playback.Cue);
            Assert.AreEqual(DialogueState.Revealing, playback.State);
            Assert.IsNull(playback.Outgoing);

            playback.Tick(0.1f);
            playback.Advance(2);
            Assert.AreEqual(BustCue.SlideIn, playback.Cue);
            Assert.AreEqual(DialogueState.Entering, playback.State);
            Assert.IsNull(playback.Outgoing, "nothing was on screen to slide out");
        }

        [Test]
        public void ThePressThatFinishesTheLastLine_CannotPickAChoice()
        {
            var playback = Play(Narrate("only"));
            playback.Tick(0.1f);
            Assert.AreEqual(DialogueState.Revealed, playback.State);
            Assert.IsFalse(playback.ChoicesShown);
            Assert.IsFalse(playback.AcceptsChoicePress(40));

            Assert.IsTrue(playback.Advance(41));
            Assert.AreEqual(DialogueState.Choices, playback.State);
            Assert.IsTrue(playback.ChoicesShown);

            Assert.IsFalse(playback.AcceptsChoicePress(41), "started on the frame the rows appeared");
            Assert.IsFalse(playback.AcceptsChoicePress(39), "started before they appeared");
            Assert.IsTrue(playback.AcceptsChoicePress(42), "the first press after they appear acts");

            Assert.IsFalse(playback.Advance(43), "the rows take presses now, not the playback");
            Assert.AreEqual(DialogueState.Choices, playback.State);
        }

        [Test]
        public void SuspendPausesTheTypewriterAndIgnoresPresses_ResumeReturns()
        {
            var playback = Play(Narrate(Thirty));
            playback.Tick(0.1f);
            Assert.AreEqual(6, playback.VisibleCharacters);

            playback.Suspend();
            Assert.AreEqual(DialogueState.Suspended, playback.State);
            playback.Tick(1f);
            Assert.AreEqual(6, playback.VisibleCharacters);
            Assert.IsFalse(playback.Advance(5));
            Assert.AreEqual(6, playback.VisibleCharacters);

            playback.Resume();
            Assert.AreEqual(DialogueState.Revealing, playback.State);
            playback.Tick(0.1f);
            Assert.AreEqual(12, playback.VisibleCharacters);
        }

        [Test]
        public void SuspendedOverTheRows_KeepsThemShownButNotPickable()
        {
            var playback = Play(Narrate("x"));
            playback.Tick(0.1f);
            playback.Advance(1);

            playback.Suspend();
            Assert.IsTrue(playback.ChoicesShown);
            Assert.IsFalse(playback.AcceptsChoicePress(9));

            playback.Resume();
            Assert.IsTrue(playback.AcceptsChoicePress(9));
        }

        [Test]
        public void TheResultPlaysFirst_AsNarration_WithItsEffectsLine()
        {
            var beats = DialogueBeat.Sequence("The sheep eats it.", "+5 gold",
                new[] { Say("sheep", DialogueSide.Left, "Baa.") });

            Assert.AreEqual(2, beats.Count);
            Assert.IsTrue(beats[0].IsResult);
            Assert.IsTrue(beats[0].IsNarration);
            Assert.AreEqual("The sheep eats it.", beats[0].Line.Text);
            Assert.AreEqual("+5 gold", beats[0].Effects);

            // 18 + the line break + 7.
            Assert.AreEqual(26, beats[0].VisibleLength);
            Assert.AreEqual("sheep", beats[1].Line.SpeakerId);

            var playback = new DialoguePlayback(beats);
            Assert.AreEqual(DialogueState.Revealing, playback.State, "the result has no bust");
            Assert.AreEqual(BustCue.Hidden, playback.Cue);

            playback.Advance(1);
            playback.Advance(2);
            Assert.AreEqual(BustCue.SlideIn, playback.Cue, "the first speaker after the result slides in");
        }

        [Test]
        public void NoResult_StartsOnLineOne()
        {
            var beats = DialogueBeat.Sequence("", "", new[] { Narrate("one"), Narrate("two") });
            Assert.AreEqual(2, beats.Count);
            Assert.IsFalse(beats[0].IsResult);
            Assert.AreEqual("one", beats[0].Line.Text);
        }

        [Test]
        public void AConcludedEvent_IsTheResultAlone()
        {
            var beats = DialogueBeat.Sequence("You leave.", "", null);
            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(10, beats[0].VisibleLength);

            var playback = new DialoguePlayback(beats);
            playback.Advance(1);
            playback.Advance(2);
            Assert.AreEqual(DialogueState.Choices, playback.State);
        }

        [Test]
        public void CloseMidSlide_SnapsAndDropsTheOutgoingBust()
        {
            var playback = Play(Say("sheep", DialogueSide.Left, "a"), Say("owl", DialogueSide.Right, "b"));
            playback.Tick(0.2f);
            playback.Tick(0.1f);
            playback.Advance(1);
            playback.Tick(0.05f);
            Assert.AreEqual(0.25f, playback.SlideProgress, 1e-4f);

            playback.Close();
            Assert.AreEqual(DialogueState.Closed, playback.State);
            Assert.AreEqual(1f, playback.SlideProgress);
            Assert.IsNull(playback.Outgoing);
            Assert.IsFalse(playback.ChoicesShown);
            Assert.IsFalse(playback.Advance(2));
        }

        [Test]
        public void AnEmptySequence_GoesStraightToTheRows()
        {
            var playback = new DialoguePlayback(DialogueBeat.Sequence("", "", null));
            Assert.AreEqual(DialogueState.Choices, playback.State);
            Assert.IsTrue(playback.AcceptsChoicePress(0));
        }
    }
}
