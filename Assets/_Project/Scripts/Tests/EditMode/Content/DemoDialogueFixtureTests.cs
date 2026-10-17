using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // THE DIALOGUE STAGE'S FIXTURE EVENT (docs/PLAN_DIALOGUE_STAGE.md, D4),
    // read from events.json (its last entry) through the real resolver
    // against the real characters.json. Each thing it exists to exercise is
    // pinned here by literal, so an edit that quietly drops one (the
    // 200-character line, the locked row, the Bjorn variant) fails here
    // rather than in a capture.
    public class DemoDialogueFixtureTests
    {
        private static Dictionary<string, string> RealCharacters() =>
            ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters
                .ToDictionary(c => c.id, c => c.displayName);

        private static RawEventEntry RawEntry() =>
            ContentDataFiles.ParseFile<RawEventFile>(ContentDataFiles.DataPath("events.json")).events
                .Single(e => e.id == "demo_dialogue");

        private static ResolvedEventDefinition Resolve()
        {
            bool ok = EventEntryResolver.TryResolveAll(new[] { RawEntry() }, RealCharacters(), new string[0],
                new Dictionary<string, string>(), out var resolved, out var errors);
            Assert.IsTrue(ok, "demo_dialogue does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(1, resolved.Count);
            return resolved[0];
        }

        [Test]
        public void ResolvesWithNoErrors_AgainstTheRealCharacters()
        {
            var evt = Resolve();
            Assert.AreEqual("demo_dialogue", evt.Id);
            CollectionAssert.AreEqual(new[] { "landing", "bearer", "quiet", "farewell" }, evt.Pages.Select(p => p.Id).ToArray());
            Assert.IsTrue(evt.HasAnyLines);
        }

        // Kept out of the random roll: no real run reaches floor 999, and the
        // debug menu's OpenEventForDebug opens it anywhere.
        [Test]
        public void IsScopedAwayFromRealRuns()
        {
            CollectionAssert.AreEqual(new[] { 999 }, Resolve().Floors);
        }

        [Test]
        public void EventLevelRequires_GuaranteeShawnAndOdette()
        {
            var requires = Resolve().Requires;
            Assert.AreEqual(2, requires.Length);
            Assert.IsTrue(requires.All(r => r.Kind == EventRequirementKind.InParty));
        }

        [Test]
        public void TheDefaultBackdropAndNoSetPiece()
        {
            var evt = Resolve();
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", evt.BackdropKey);
            foreach (var page in evt.Pages)
            {
                Assert.AreEqual("", page.ArtKey, page.Id);
                Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", page.BackdropKey, page.Id);
            }
        }

        [Test]
        public void TheFirstPage_NarrationShawnShawnOdette_WithUndeclaredSides()
        {
            var page = Resolve().Pages[0];
            Assert.AreEqual(4, page.Lines.Length);
            var raw = RawEntry().pages[0];
            Assert.AreEqual(0, raw.cast?.Length ?? 0, "fixture: no declared sides, so they come from first appearance");

            Assert.IsTrue(page.Lines[0].IsNarration);

            Assert.AreEqual("sheep", page.Lines[1].SpeakerId);
            Assert.AreEqual(DialogueExpression.Happy, page.Lines[1].Expression);
            Assert.AreEqual(DialogueSide.Left, page.Lines[1].Side);
            StringAssert.Contains("<i>excellent</i>", page.Lines[1].Text);

            // Straight after his first line, same side, new expression: the
            // in-place swap (DialoguePlayback swaps only between consecutive
            // lines of one speaker, so Odette cannot sit between them).
            Assert.AreEqual("sheep", page.Lines[2].SpeakerId);
            Assert.AreEqual(DialogueExpression.Annoyed, page.Lines[2].Expression);
            Assert.AreEqual(DialogueSide.Left, page.Lines[2].Side);

            Assert.AreEqual("owl", page.Lines[3].SpeakerId);
            Assert.AreEqual(DialogueExpression.Neutral, page.Lines[3].Expression);
            Assert.AreEqual(DialogueSide.Right, page.Lines[3].Side);
            Assert.AreEqual(200, page.Lines[3].Text.Length, "the longest legal line, for the overflow check and captures");
        }

        [Test]
        public void TheFirstPage_FourRows_LockedHiddenOpenAndLeave()
        {
            var choices = Resolve().Pages[0].Choices;
            CollectionAssert.AreEqual(
                new[] { "Buy the lantern outright", "Ask the lantern its name", "Follow the light", "Leave" },
                choices.Select(c => c.Text).ToArray());

            var locked = choices[0];
            Assert.IsFalse(locked.HiddenUntilMet);
            Assert.AreEqual(EventRequirementKind.Gold, locked.Requires[0].Kind);
            CollectionAssert.Contains(locked.Requires[0].PossibleReasons().ToList(), "Prince does not pay that well");

            var hidden = choices[1];
            Assert.IsTrue(hidden.HiddenUntilMet);
            Assert.AreEqual(EventRequirementKind.MemberLevel, hidden.Requires[0].Kind);

            var follow = choices[2];
            Assert.AreEqual(2, follow.Outcomes.Length);
            Assert.AreEqual("bearer", follow.Outcomes[0].GoTo);
            Assert.AreEqual(EventRequirementKind.InParty, follow.Outcomes[0].Requires.Single().Kind);
            Assert.AreEqual("quiet", follow.Outcomes[1].GoTo);
            Assert.AreEqual(0, follow.Outcomes[1].Requires.Length, "contract 1: the fallback is unconditional");
            foreach (var outcome in follow.Outcomes)
            {
                Assert.AreEqual(EventEffectKind.Gold, outcome.Effects.Single().Kind);
                Assert.That(outcome.Result.Length, Is.InRange(1, 200));
            }

            Assert.IsTrue(choices[3].Outcomes.Single().IsLeave);
        }

        // Bjorn's art has neutral only, so "happy" exercises the fallback.
        [Test]
        public void TheBjornVariant_AsksForAnExpressionHisArtLacks()
        {
            var bearer = Resolve().PageById("bearer");
            Assert.AreEqual("bear", bearer.Lines[0].SpeakerId);
            Assert.AreEqual(DialogueExpression.Happy, bearer.Lines[0].Expression);
        }

        [Test]
        public void TheFallbackVariant_IsNarrationOnly()
        {
            Assert.IsTrue(Resolve().PageById("quiet").Lines.All(l => l.IsNarration));
        }

        [Test]
        public void TheLastPage_IsNarrationOnly_AndLeavesWithAResult()
        {
            var farewell = Resolve().PageById("farewell");
            Assert.IsTrue(farewell.Lines.All(l => l.IsNarration));

            var leave = farewell.Choices.Single().Outcomes.Single();
            Assert.IsTrue(leave.IsLeave);
            Assert.That(leave.Result.Length, Is.InRange(1, 200), "a result makes the event conclude on the stage");
        }
    }
}
