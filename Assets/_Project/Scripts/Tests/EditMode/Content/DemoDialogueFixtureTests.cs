using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // THE DIALOGUE STAGE'S FIXTURE EVENT (docs/PLAN_DIALOGUE_STAGE.md, D4),
    // read from its staging copy docs/fixtures/demo_dialogue.json through the
    // real resolver against the real characters.json. It must resolve with no
    // errors, and each thing it exists to exercise is pinned here by literal,
    // so an edit that quietly drops one (the 200-character line, the locked
    // row, the Bjorn variant) fails here rather than in a capture.
    //
    // The event is also merged into events.json as its last entry; the last
    // test here holds the two copies to the same resolved shape, so an edit
    // to one that is not made to the other fails. When the staging copy is
    // deleted, retarget Resolve() at events.json and drop that test.
    public class DemoDialogueFixtureTests
    {
        private static string FixturePath() =>
            Path.Combine(ContentDataFiles.RepoRoot(), "docs", "fixtures", "demo_dialogue.json");

        private static Dictionary<string, string> RealCharacters() =>
            ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters
                .ToDictionary(c => c.id, c => c.displayName);

        private static ResolvedEventDefinition Resolve()
        {
            var entries = ContentDataFiles.ParseFile<RawEventFile>(FixturePath()).events;
            bool ok = EventEntryResolver.TryResolveAll(entries, RealCharacters(), new string[0],
                new Dictionary<string, string>(), out var resolved, out var errors);
            Assert.IsTrue(ok, "demo_dialogue does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(1, resolved.Count);
            return resolved[0];
        }

        // Every string the stage shows and every edge, in order: two copies
        // that agree on this play identically.
        private static string Fingerprint(ResolvedEventDefinition evt)
        {
            var parts = new List<string> { evt.Id, string.Join(",", evt.Floors), evt.BackdropKey, evt.Requires.Length.ToString() };
            foreach (var page in evt.Pages)
            {
                parts.Add($"page {page.Id}|{page.Title}|{page.Body}|{page.ArtKey}|{page.BackdropKey}");
                parts.AddRange(page.Lines.Select(l => $"line {l.SpeakerId}|{l.IsNarration}|{l.Expression}|{l.Side}|{l.Text}"));
                foreach (var choice in page.Choices)
                {
                    parts.Add($"choice {choice.Text}|{choice.HiddenUntilMet}|{choice.Requires.Length}|{choice.Effects.Length}");
                    parts.AddRange(choice.Outcomes.Select(o =>
                        $"outcome {o.GoTo}|{o.IsLeave}|{o.Result}|{o.Requires.Length}|{o.Effects.Length}"));
                }
            }

            return string.Join("\n", parts);
        }

        [Test]
        public void TheMergedCopyInEventsJson_MatchesTheStagingCopy()
        {
            var resolved = RealEventCatalogue.Events();

            Assert.AreEqual("demo_dialogue", resolved.Last().Id, "demo_dialogue is the last entry of events.json");
            Assert.AreEqual(Fingerprint(Resolve()), Fingerprint(resolved.Last()));
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
            var raw = ContentDataFiles.ParseFile<RawEventFile>(FixturePath()).events[0].pages[0];
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
