using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The dialogue stage's content checks (docs/PLAN_DIALOGUE_STAGE.md, D1):
    // backdrops, cast, lines, markup, caps and the presence refusal. Every
    // expected value is a literal (CLAUDE.md gotcha 5). Presence's own
    // dataflow is pinned separately in EventCastPresenceTests.
    public class EventDialogueResolverTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static readonly string[] KnownItems = { "health_potion" };

        private static RawEventLine Line(string speaker, string text, string expression = "") =>
            new RawEventLine { speaker = speaker, expression = expression, text = text };

        private static RawEventRequirement InParty(string character) =>
            new RawEventRequirement { kind = "inParty", character = character };

        // One page, one Leave choice, every character guaranteed by the
        // event's own requires -- so a test that breaks one thing can only
        // be refused for that thing, never for presence.
        private static RawEventEntry EventWithLines(params RawEventLine[] lines)
        {
            return new RawEventEntry
            {
                id = "e1",
                requires = new[] { InParty("sheep"), InParty("owl"), InParty("bear") },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "p1",
                        title = "T",
                        body = "B",
                        lines = lines,
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Leave",
                                outcomes = new[] { new RawEventOutcome { goTo = "Leave" } },
                            },
                        },
                    },
                },
            };
        }

        private static bool Resolve(RawEventEntry entry, out List<ResolvedEventDefinition> resolved, out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, KnownItems, out resolved, out errors);

        private static ResolvedEventPage ResolveFirstPage(RawEventEntry entry)
        {
            bool ok = Resolve(entry, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0].Pages[0];
        }

        private static string Refusal(RawEventEntry entry)
        {
            bool ok = Resolve(entry, out _, out var errors);
            Assert.IsFalse(ok, "expected the build to refuse this event");
            return string.Join(" | ", errors);
        }

        // ---- a page with lines ----------------------------------------------

        [Test]
        public void APageWithLines_ResolvesEveryLineInOrder()
        {
            var page = ResolveFirstPage(EventWithLines(
                Line("narration", "The pen <i>creaks</i>."),
                Line("sheep", "Baa?", "happy"),
                Line("owl", "Hoo.")));

            Assert.IsTrue(page.HasLines);
            Assert.AreEqual(3, page.Lines.Length);

            Assert.IsTrue(page.Lines[0].IsNarration);
            Assert.AreEqual("", page.Lines[0].SpeakerId);
            Assert.AreEqual("The pen <i>creaks</i>.", page.Lines[0].Text);

            Assert.IsFalse(page.Lines[1].IsNarration);
            Assert.AreEqual("sheep", page.Lines[1].SpeakerId);
            Assert.AreEqual(DialogueExpression.Happy, page.Lines[1].Expression);
            Assert.AreEqual("Baa?", page.Lines[1].Text);

            Assert.AreEqual("owl", page.Lines[2].SpeakerId);
            Assert.AreEqual(DialogueExpression.Neutral, page.Lines[2].Expression, "empty expression means neutral");

            CollectionAssert.AreEqual(new[] { "sheep", "owl" }, page.Cast.Select(c => c.CharacterId).ToArray(),
                "cast is every distinct speaker in order of first appearance, narration excluded");
        }

        [Test]
        public void ExpressionAndNarration_AreMatchedCaseInsensitively()
        {
            var page = ResolveFirstPage(EventWithLines(Line("Narration", "x"), Line("sheep", "y", "SURPRISED")));

            Assert.IsTrue(page.Lines[0].IsNarration);
            Assert.AreEqual(DialogueExpression.Surprised, page.Lines[1].Expression);
        }

        [Test]
        public void AtTheCaps_TwelveLinesOfTwoHundredCharacters_Resolve()
        {
            var lines = Enumerable.Range(0, 12).Select(_ => Line("narration", new string('a', 200))).ToArray();
            Assert.AreEqual(12, ResolveFirstPage(EventWithLines(lines)).Lines.Length);
        }

        // ---- a page without lines -------------------------------------------

        [Test]
        public void APageWithoutLines_HasNoLinesNoCastAndTheDefaultBackdrop()
        {
            var page = ResolveFirstPage(EventWithLines());

            Assert.IsFalse(page.HasLines);
            Assert.AreEqual(0, page.Lines.Length);
            Assert.AreEqual(0, page.Cast.Length);
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", page.BackdropKey);
        }

        // demo_wishing_well is the compatibility fixture (contract 14): read
        // off the real events.json and pinned field by field, so D1 provably
        // changed nothing it shows.
        [Test]
        public void DemoWishingWell_StillResolvesUnchanged()
        {
            var entries = ContentDataFiles.ParseFile<RawEventFile>(ContentDataFiles.DataPath("events.json")).events;
            // The real file also holds petting_zoo, whose `relic` effect names
            // kinship; without a relic catalogue the resolver refuses it.
            var knownRelics = new Dictionary<string, string> { ["kinship"] = "Kinship" };
            bool ok = EventEntryResolver.TryResolveAll(entries, KnownCharacters, KnownItems, knownRelics,
                out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));

            var well = resolved.Single(e => e.Id == "demo_wishing_well");
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", well.BackdropKey);
            CollectionAssert.AreEqual(new[] { "well", "wish_granted" }, well.Pages.Select(p => p.Id).ToArray());

            var first = well.Pages[0];
            Assert.AreEqual("A Wishing Well", first.Title);
            Assert.AreEqual("A mossy well sits at the crossing, its water black and still. Something glints at the bottom.", first.Body);
            Assert.AreEqual("", first.ArtKey);
            CollectionAssert.AreEqual(
                new[] { "Leave", "Toss a coin (5 gold)", "Call on Shawn's luck", "Recite a well-worn prayer" },
                first.Choices.Select(c => c.Text).ToArray());
            Assert.AreEqual(2, first.Choices[1].Outcomes.Length);
            Assert.AreEqual("wish_granted", first.Choices[1].Outcomes[0].GoTo);
            Assert.AreEqual("well", first.Choices[1].Outcomes[1].GoTo);

            var second = well.Pages[1];
            Assert.AreEqual("The Well Answers", second.Title);
            CollectionAssert.AreEqual(new[] { "Leave", "Whisper one more charm" }, second.Choices.Select(c => c.Text).ToArray());
            Assert.IsTrue(second.Choices[1].HiddenUntilMet);

            foreach (var page in well.Pages)
            {
                Assert.IsFalse(page.HasLines, page.Id);
                Assert.AreEqual(0, page.Cast.Length, page.Id);
                Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", page.BackdropKey, page.Id);
            }
        }

        // ---- backdrops ------------------------------------------------------

        [Test]
        public void AnEventBackdrop_IsInheritedAndAPageBackdropOverridesIt()
        {
            var entry = EventWithLines();
            entry.backdrop = "Assets/_Project/Art/Events/e1/pen.png";
            var second = new RawEventPage
            {
                id = "p2",
                backdrop = "Assets/_Project/Art/Backgrounds/Divine_principality.png",
                choices = entry.pages[0].choices,
            };
            entry.pages = new[] { entry.pages[0], second };

            bool ok = Resolve(entry, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));

            Assert.AreEqual("Assets/_Project/Art/Events/e1/pen.png", resolved[0].BackdropKey);
            Assert.AreEqual("Assets/_Project/Art/Events/e1/pen.png", resolved[0].Pages[0].BackdropKey);
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/Divine_principality.png", resolved[0].Pages[1].BackdropKey);
        }

        [Test]
        public void ABackdrop_ThatIsNotAssetsRelative_IsRefused()
        {
            var entry = EventWithLines();
            entry.backdrop = "Backgrounds/Dungeon";
            StringAssert.Contains("backdrop 'Backgrounds/Dungeon' must be ASSETS-relative", Refusal(entry));
        }

        [Test]
        public void ABackdrop_FiledOutsideBackgroundsAndItsEventFolder_IsRefused()
        {
            var entry = EventWithLines();
            entry.pages[0].backdrop = "Assets/_Project/Art/UI/pen.png";
            StringAssert.Contains("page 'p1': backdrop 'Assets/_Project/Art/UI/pen.png' must be filed under " +
                                  "Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/e1/ folder",
                Refusal(entry));
        }

        [Test]
        public void ABackdrop_InAnotherEventsFolder_IsRefused()
        {
            var entry = EventWithLines();
            entry.backdrop = "Assets/_Project/Art/Events/other/pen.png";
            StringAssert.Contains("backdrop 'Assets/_Project/Art/Events/other/pen.png' must be filed as " +
                                  "Assets/_Project/Art/Events/e1/<file>", Refusal(entry));
        }

        // ---- speakers and expressions ---------------------------------------

        [Test]
        public void AnUnknownSpeaker_IsRefused()
        {
            StringAssert.Contains("line #1: speaker names character 'turtle', which is not in characters.json",
                Refusal(EventWithLines(Line("turtle", "Hi."))));
        }

        [Test]
        public void AnEmptySpeaker_IsRefused()
        {
            StringAssert.Contains("line #1: speaker is required", Refusal(EventWithLines(Line("", "Hi."))));
        }

        [Test]
        public void AnUnknownExpression_IsRefused()
        {
            StringAssert.Contains("line #2: expression 'smug' is not a known DialogueExpression " +
                                  "(neutral, happy, annoyed, nervous, sad, surprised, entranced)",
                Refusal(EventWithLines(Line("sheep", "a"), Line("sheep", "b", "smug"))));
        }

        [Test]
        public void ANumericExpression_IsRefusedRatherThanReadAsAnOrdinal()
        {
            StringAssert.Contains("expression '3' is not a known DialogueExpression",
                Refusal(EventWithLines(Line("sheep", "a", "3"))));
        }

        [Test]
        public void AnExpressionOnNarration_IsRefused()
        {
            StringAssert.Contains("line #1: narration has expression 'happy'",
                Refusal(EventWithLines(Line("narration", "a", "happy"))));
        }

        [Test]
        public void ALineWithNoText_IsRefused()
        {
            StringAssert.Contains("line #1: has no text", Refusal(EventWithLines(Line("sheep", "  "))));
        }

        // ---- caps -----------------------------------------------------------

        [Test]
        public void ThirteenLines_AreRefused()
        {
            var lines = Enumerable.Range(0, 13).Select(_ => Line("narration", "a")).ToArray();
            StringAssert.Contains("page 'p1': has 13 lines, over the 12 allowed", Refusal(EventWithLines(lines)));
        }

        [Test]
        public void ALineOfTwoHundredAndOneCharacters_IsRefused()
        {
            StringAssert.Contains("line #1: text is 201 characters, over the 200-character cap",
                Refusal(EventWithLines(Line("narration", new string('a', 201)))));
        }

        // ---- rich text ------------------------------------------------------

        [TestCase("A <b>bold</b> claim.", "'<b>'")]
        [TestCase("<color=red>Red</color>", "'<color=red>'")]
        [TestCase("An <I>upper</I> tag.", "'<I>'")]
        [TestCase("Less < more", "'< more'")]
        public void AnyTagButItalic_IsRefused(string text, string quotedTag)
        {
            string refusal = Refusal(EventWithLines(Line("narration", text)));
            StringAssert.Contains("line #1: text contains " + quotedTag, refusal);
            StringAssert.Contains("<b> included, is refused", refusal);
        }

        [TestCase("An <i>open italic.", "opens <i> and never closes it")]
        [TestCase("A stray close</i>.", "closes </i> with no <i> open")]
        [TestCase("<i>Nested <i>twice</i></i>", "opens <i> inside an <i>")]
        public void UnbalancedItalic_IsRefused(string text, string expected)
        {
            StringAssert.Contains(expected, Refusal(EventWithLines(Line("narration", text))));
        }

        // ---- cast and sides -------------------------------------------------

        [Test]
        public void Sides_DefaultToLeftRightLeftByFirstAppearance()
        {
            var page = ResolveFirstPage(EventWithLines(
                Line("owl", "1"), Line("owl", "2"), Line("narration", "3"), Line("sheep", "4"), Line("bear", "5"), Line("owl", "6")));

            CollectionAssert.AreEqual(
                new[] { DialogueSide.Left, DialogueSide.Left, DialogueSide.Left, DialogueSide.Right, DialogueSide.Left, DialogueSide.Left },
                page.Lines.Select(l => l.Side).ToArray());
            CollectionAssert.AreEqual(new[] { "owl", "sheep", "bear" }, page.Cast.Select(c => c.CharacterId).ToArray());
            CollectionAssert.AreEqual(new[] { DialogueSide.Left, DialogueSide.Right, DialogueSide.Left },
                page.Cast.Select(c => c.Side).ToArray());
        }

        [Test]
        public void ADeclaredCastSide_WinsAndLeavesTheOthersWhereTheyWere()
        {
            var entry = EventWithLines(Line("sheep", "1"), Line("owl", "2"), Line("bear", "3"));
            entry.pages[0].cast = new[] { new RawEventCastMember { character = "sheep", side = "RIGHT" } };

            var page = ResolveFirstPage(entry);

            // sheep pinned right; owl keeps slot 2 (right), bear slot 3 (left).
            CollectionAssert.AreEqual(new[] { DialogueSide.Right, DialogueSide.Right, DialogueSide.Left },
                page.Lines.Select(l => l.Side).ToArray());
            CollectionAssert.AreEqual(new[] { DialogueSide.Right, DialogueSide.Right, DialogueSide.Left },
                page.Cast.Select(c => c.Side).ToArray());
        }

        [Test]
        public void ACastEntryThatNeverSpeaks_IsRefused()
        {
            var entry = EventWithLines(Line("sheep", "1"));
            entry.pages[0].cast = new[] { new RawEventCastMember { character = "owl", side = "left" } };
            StringAssert.Contains("page 'p1' cast 'owl': never speaks on this page", Refusal(entry));
        }

        [Test]
        public void ADuplicateCastCharacter_IsRefused()
        {
            var entry = EventWithLines(Line("sheep", "1"));
            entry.pages[0].cast = new[]
            {
                new RawEventCastMember { character = "sheep", side = "left" },
                new RawEventCastMember { character = "sheep", side = "right" },
            };
            StringAssert.Contains("page 'p1' cast 'sheep': listed twice", Refusal(entry));
        }

        [Test]
        public void AnUnknownCastCharacter_IsRefused()
        {
            var entry = EventWithLines(Line("sheep", "1"));
            entry.pages[0].cast = new[] { new RawEventCastMember { character = "dog", side = "left" } };
            StringAssert.Contains("page 'p1': cast entry names character 'dog', which is not in characters.json", Refusal(entry));
        }

        [TestCase("")]
        [TestCase("middle")]
        [TestCase("1")]
        public void AnUnknownCastSide_IsRefused(string side)
        {
            var entry = EventWithLines(Line("sheep", "1"));
            entry.pages[0].cast = new[] { new RawEventCastMember { character = "sheep", side = side } };
            StringAssert.Contains($"page 'p1' cast 'sheep': side '{side}' is not left or right", Refusal(entry));
        }

        // ---- presence refusal -----------------------------------------------

        [Test]
        public void ASpeakerNobodyGuarantees_IsRefusedNamingEventPageLineAndFix()
        {
            var entry = EventWithLines(Line("narration", "a"), Line("sheep", "b"));
            entry.requires = new RawEventRequirement[0];

            string refusal = Refusal(entry);
            StringAssert.Contains("event 'e1' page 'p1' line #2: speaker 'sheep' is not guaranteed to be in the party here", refusal);
            StringAssert.Contains("Add an inParty requirement naming 'sheep'", refusal);
        }

        [Test]
        public void NarrationAlone_NeedsNoPresence()
        {
            var entry = EventWithLines(Line("narration", "a"));
            entry.requires = new RawEventRequirement[0];
            Assert.IsTrue(ResolveFirstPage(entry).Lines[0].IsNarration);
        }
    }
}
