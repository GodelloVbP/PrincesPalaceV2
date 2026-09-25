using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // Dialogue stage D4 (docs/PLAN_DIALOGUE_STAGE.md): an outcome result that
    // plays on the stage takes the 200-character line cap, and a concluded
    // event picks its presentation from whether the event has lines anywhere.
    // Every cap is a literal (CLAUDE.md gotcha 5).
    public class EventStagedResultTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static RawEventLine[] OneLine() =>
            new[] { new RawEventLine { speaker = "narration", text = "The pen creaks." } };

        private static RawEventChoice Choice(string text, string result, string goTo) =>
            new RawEventChoice
            {
                text = text,
                outcomes = new[] { new RawEventOutcome { result = result, goTo = goTo } },
            };

        // Two pages: p1 has a choice that goes to p2 and one that leaves; p2
        // leaves. Which pages carry lines is the variable under test.
        private static RawEventEntry TwoPages(bool p1Lines, bool p2Lines, string toP2Result, string leaveResult)
        {
            return new RawEventEntry
            {
                id = "e1",
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "p1",
                        title = "T",
                        body = "B",
                        lines = p1Lines ? OneLine() : new RawEventLine[0],
                        choices = new[]
                        {
                            Choice("Onward", toP2Result, "p2"),
                            Choice("Leave", leaveResult, "Leave"),
                        },
                    },
                    new RawEventPage
                    {
                        id = "p2",
                        title = "T2",
                        body = "B2",
                        lines = p2Lines ? OneLine() : new RawEventLine[0],
                        choices = new[] { Choice("Leave", "", "Leave") },
                    },
                },
            };
        }

        private static bool Resolve(RawEventEntry entry, out List<ResolvedEventDefinition> resolved, out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, new string[0],
                out resolved, out errors);

        private static void AssertResolves(RawEventEntry entry)
        {
            bool ok = Resolve(entry, out _, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        private static string Refusal(RawEventEntry entry)
        {
            bool ok = Resolve(entry, out _, out var errors);
            Assert.IsFalse(ok, "expected the build to refuse this event");
            return string.Join(" | ", errors);
        }

        private static string Text(int length) => new string('a', length);

        // ---- the result cap on staged pages ---------------------------------

        [Test]
        public void AResultOfTwoHundred_OnAPageWithLines_Resolves()
        {
            AssertResolves(TwoPages(p1Lines: true, p2Lines: false, toP2Result: Text(200), leaveResult: Text(200)));
        }

        [Test]
        public void AResultOfTwoHundredAndOne_OnAPageWithLines_IsRefused()
        {
            string refusal = Refusal(TwoPages(p1Lines: true, p2Lines: false, toP2Result: "", leaveResult: Text(201)));
            StringAssert.Contains("event 'e1' page 'p1' choice 'Leave' outcome #1: result is 201 characters, " +
                                  "over the 200-character cap for a result that plays on the dialogue stage " +
                                  "(page 'p1' has lines)", refusal);
        }

        [Test]
        public void AResultOfTwoHundredAndOne_GoingToAPageWithLines_IsRefused()
        {
            string refusal = Refusal(TwoPages(p1Lines: false, p2Lines: true, toP2Result: Text(201), leaveResult: ""));
            StringAssert.Contains("page 'p1' choice 'Onward' outcome #1: result is 201 characters, " +
                                  "over the 200-character cap", refusal);
            StringAssert.Contains("(its goTo page 'p2' has lines)", refusal);
        }

        [Test]
        public void AResultOfTwoHundred_GoingToAPageWithLines_Resolves()
        {
            AssertResolves(TwoPages(p1Lines: false, p2Lines: true, toP2Result: Text(200), leaveResult: ""));
        }

        // The Leave on a line-less page plays in the legacy layout even when
        // another page of the same event has lines, so it keeps 600.
        [Test]
        public void ALeaveResultOfSixHundred_FromALineLessPage_KeepsTheBodyCap_EvenWhenAnotherPageHasLines()
        {
            AssertResolves(TwoPages(p1Lines: false, p2Lines: true, toP2Result: "", leaveResult: Text(600)));
        }

        [Test]
        public void AResultOfSixHundred_BetweenLineLessPages_Resolves()
        {
            AssertResolves(TwoPages(p1Lines: false, p2Lines: false, toP2Result: Text(600), leaveResult: Text(600)));
        }

        [Test]
        public void AResultOfSixHundredAndOne_BetweenLineLessPages_IsRefusedByTheBodyCap()
        {
            StringAssert.Contains("choice 'Onward': outcome result is 601 characters, over the 600-character cap",
                Refusal(TwoPages(p1Lines: false, p2Lines: false, toP2Result: Text(601), leaveResult: "")));
        }

        // The 200 cap counts the whole string, as the line cap does.
        [Test]
        public void TheStagedCap_CountsTheRawString()
        {
            string tagged = "<i>" + Text(194) + "</i>";
            Assert.AreEqual(201, tagged.Length);
            StringAssert.Contains("result is 201 characters",
                Refusal(TwoPages(p1Lines: true, p2Lines: false, toP2Result: "", leaveResult: tagged)));
        }

        // ---- which events conclude on the stage -----------------------------

        [Test]
        public void HasAnyLines_IsFalseWithNoLinesAnywhere()
        {
            Assert.IsTrue(Resolve(TwoPages(false, false, "", ""), out var resolved, out _));
            Assert.IsFalse(resolved[0].HasAnyLines);
        }

        [Test]
        public void HasAnyLines_IsTrueWhenOnlyALaterPageHasLines()
        {
            Assert.IsTrue(Resolve(TwoPages(false, true, "", ""), out var resolved, out _));
            Assert.IsFalse(resolved[0].Pages[0].HasLines, "fixture: the start page has no lines");
            Assert.IsTrue(resolved[0].HasAnyLines);
        }

        [Test]
        public void HasAnyLines_IsFalseForAnEmptyDefinition()
        {
            Assert.IsFalse(new ResolvedEventDefinition().HasAnyLines);
        }

        private static EventView Concluded(bool eventHasLines) =>
            new EventView("e1", "", "", "", "", true, "It is done.", "", null, "", null, eventHasLines);

        [Test]
        public void AConcludedEvent_WithLinesAnywhere_IsStaged()
        {
            // No held page and no lines of its own: this is exactly what a
            // resume from a save hands the panel.
            var view = Concluded(eventHasLines: true);
            Assert.IsFalse(view.HasLines);
            Assert.IsTrue(view.Staged);
        }

        [Test]
        public void AConcludedEvent_WithNoLines_IsLegacy()
        {
            Assert.IsFalse(Concluded(eventHasLines: false).Staged);
        }

        [Test]
        public void ALineLessPage_OfAnEventWithLines_IsLegacyWhileItIsOpen()
        {
            var view = new EventView("e1", "p1", "", "T", "B", false, "", "", null, "", null, eventHasLines: true);
            Assert.IsFalse(view.Staged, "contract 14: a page without lines shows as before");
        }

        [Test]
        public void APageWithLines_IsStagedAndCountsAsAnEventWithLines()
        {
            var line = new EventLineView("", true, "", "", "", "neutral", DialogueSide.Left, "x");
            var view = new EventView("e1", "p1", "", "T", "B", false, "", "", null, "", new[] { line });
            Assert.IsTrue(view.Staged);
            Assert.IsTrue(view.EventHasLines);
        }
    }
}
