using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // An event's own speakers and the presence dataflow's two new rules
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.3, 3.5): event speakers are
    // present on every page, and each fight result is an edge from the page
    // that launched the fight. Expected values are literals.
    public class EventSpeakerResolverTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static readonly Dictionary<string, int> KnownEnemies = new Dictionary<string, int> { ["rat"] = 1 };

        private static RawEventLine Line(string speaker, string text, string expression = "") =>
            new RawEventLine { speaker = speaker, expression = expression, text = text };

        private static RawEventSpeaker Merchant() => new RawEventSpeaker
        {
            id = "merchant",
            name = "Rat Merchant",
            epithet = "Purveyor of Fine Goods",
            bustPath = "Portraits/Dialogue/rat_merchant",
            expressions = new[] { "neutral", "grinning", "hostile" },
        };

        private static RawEventChoice Leave() =>
            new RawEventChoice { text = "Walk on", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } };

        private static RawEventEntry Caravan(params RawEventLine[] lines) => new RawEventEntry
        {
            id = "rat_caravan",
            speakers = new[] { Merchant() },
            pages = new[]
            {
                new RawEventPage { id = "caravan", title = "T", body = "", lines = lines, choices = new[] { Leave() } },
            },
        };

        private static bool Resolve(RawEventEntry entry, out List<ResolvedEventDefinition> resolved, out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, new string[0],
                new Dictionary<string, string>(), KnownEnemies, out resolved, out errors);

        private static ResolvedEventDefinition Resolved(RawEventEntry entry)
        {
            bool ok = Resolve(entry, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        private static string Refusal(RawEventEntry entry)
        {
            bool ok = Resolve(entry, out _, out var errors);
            Assert.IsFalse(ok, "expected the build to refuse this event");
            return string.Join(" | ", errors);
        }

        // ---- what resolves ---------------------------------------------------

        [Test]
        public void ASpeaker_ResolvesItsFields()
        {
            var speaker = Resolved(Caravan(Line("merchant", "Wares!"))).Speakers.Single();

            Assert.AreEqual("merchant", speaker.Id);
            Assert.AreEqual("Rat Merchant", speaker.Name);
            Assert.AreEqual("Purveyor of Fine Goods", speaker.Epithet);
            Assert.AreEqual("Portraits/Dialogue/rat_merchant", speaker.BustPath);
            CollectionAssert.AreEqual(new[] { "neutral", "grinning", "hostile" }, speaker.Expressions);
        }

        [Test]
        public void ASpeakersLine_TakesADeclaredExpressionIgnoringCase()
        {
            var line = Resolved(Caravan(Line("merchant", "Heh.", "Grinning"))).Pages[0].Lines.Single();

            Assert.IsTrue(line.IsEventSpeaker);
            Assert.AreEqual("merchant", line.SpeakerId);
            Assert.AreEqual("grinning", line.ExpressionName);
            Assert.AreEqual(DialogueExpression.Neutral, line.Expression);
        }

        [Test]
        public void ASpeakersLineWithNoExpression_TakesTheFirstDeclared()
        {
            var entry = Caravan(Line("merchant", "Heh."));
            entry.speakers[0].expressions = new[] { "grinning", "hostile" };

            Assert.AreEqual("grinning", Resolved(entry).Pages[0].Lines.Single().ExpressionName);
        }

        [Test]
        public void ASpeakerDeclaringNoExpressions_DeclaresNeutral()
        {
            var entry = Caravan(Line("merchant", "Heh."));
            entry.speakers[0].expressions = new string[0];

            var evt = Resolved(entry);
            CollectionAssert.AreEqual(new[] { "neutral" }, evt.Speakers.Single().Expressions);
            Assert.AreEqual("neutral", evt.Pages[0].Lines.Single().ExpressionName);
        }

        [Test]
        public void ACharactersLine_CarriesItsExpressionAsAFileName()
        {
            var entry = Caravan(Line("sheep", "...", "entranced"));
            entry.requires = new[] { new RawEventRequirement { kind = "inParty", character = "sheep" } };

            var line = Resolved(entry).Pages[0].Lines.Single();
            Assert.IsFalse(line.IsEventSpeaker);
            Assert.AreEqual(DialogueExpression.Entranced, line.Expression);
            Assert.AreEqual("entranced", line.ExpressionName);
        }

        [Test]
        public void ASpeaker_CanBePinnedByCast()
        {
            var entry = Caravan(Line("merchant", "Wares!"));
            entry.pages[0].cast = new[] { new RawEventCastMember { character = "merchant", side = "right" } };

            Assert.AreEqual(DialogueSide.Right, Resolved(entry).Pages[0].Lines.Single().Side);
        }

        // Presence: never named by any requires, yet present on a page two
        // edges from the start.
        [Test]
        public void ASpeaker_IsPresentOnEveryPageWithNoRequires()
        {
            var entry = Caravan(Line("merchant", "Wares!"));
            entry.pages = new[]
            {
                new RawEventPage
                {
                    id = "caravan", title = "T",
                    choices = new[]
                    {
                        new RawEventChoice
                        {
                            text = "Browse", outcomes = new[] { new RawEventOutcome { goTo = "after_browse" } },
                        },
                        Leave(),
                    },
                },
                new RawEventPage
                {
                    id = "after_browse", title = "T", lines = new[] { Line("merchant", "Anything else?", "hostile") },
                    choices = new[] { Leave() },
                },
            };

            var sets = EventCastPresence.GuaranteedByPage(Resolved(entry));
            CollectionAssert.AreEqual(new[] { "merchant" }, sets["caravan"].ToArray());
            CollectionAssert.AreEqual(new[] { "merchant" }, sets["after_browse"].ToArray());
        }

        // ---- the refusals 3.5 lists -----------------------------------------

        [Test]
        public void Refuses_ASpeakerIdThatIsACharacterId()
        {
            var entry = Caravan(Line("narration", "Wind."));
            entry.speakers[0].id = "sheep";

            StringAssert.Contains("speaker 'sheep': the id is a character id in characters.json", Refusal(entry));
        }

        [Test]
        public void Refuses_AnUndeclaredEventSpeakerExpression()
        {
            StringAssert.Contains(
                "line #1: expression 'happy' is not declared by speaker 'merchant' (neutral, grinning, hostile)",
                Refusal(Caravan(Line("merchant", "Heh.", "happy"))));
        }

        [Test]
        public void Refuses_AnUnknownSpeakerId()
        {
            StringAssert.Contains("speaker names character 'merchnat', which is not in characters.json. " +
                                  "(Not one of this event's speakers either.)",
                Refusal(Caravan(Line("merchnat", "Heh."))));
        }

        [Test]
        public void Refuses_AnUnknownCastEntryId()
        {
            var entry = Caravan(Line("merchant", "Wares!"));
            entry.pages[0].cast = new[] { new RawEventCastMember { character = "merchnat", side = "right" } };

            StringAssert.Contains("cast entry names character 'merchnat', which is not in characters.json", Refusal(entry));
        }

        // A character's line still parses against the party's enum; a name
        // an event speaker declared does not leak across.
        [Test]
        public void Refuses_AnEventSpeakersExpressionOnACharacter()
        {
            var entry = Caravan(Line("sheep", "Heh.", "grinning"));
            entry.requires = new[] { new RawEventRequirement { kind = "inParty", character = "sheep" } };

            StringAssert.Contains("expression 'grinning' is not a known DialogueExpression", Refusal(entry));
        }

        // ---- the refusals this milestone adds around them -------------------

        [Test]
        public void Refuses_ASpeakerIdOfNarration()
        {
            var entry = Caravan();
            entry.speakers[0].id = "Narration";

            StringAssert.Contains("'narration' is the unvoiced line's keyword, not a speaker id", Refusal(entry));
        }

        [Test]
        public void Refuses_ADuplicateSpeakerId()
        {
            var entry = Caravan();
            entry.speakers = new[] { Merchant(), Merchant() };

            StringAssert.Contains("speaker 'merchant': duplicate speaker id", Refusal(entry));
        }

        [Test]
        public void Refuses_ASpeakerWithNoName()
        {
            var entry = Caravan();
            entry.speakers[0].name = " ";

            StringAssert.Contains("speaker 'merchant': name is required", Refusal(entry));
        }

        [Test]
        public void Refuses_ASpeakerEpithetOverTheCharacterCap()
        {
            var entry = Caravan();
            entry.speakers[0].epithet = new string('e', 33);

            StringAssert.Contains("epithet is 33 characters, over the 32-character cap", Refusal(entry));
        }

        [Test]
        public void Refuses_AnExpressionThatIsNotAFileName()
        {
            var entry = Caravan();
            entry.speakers[0].expressions = new[] { "Grinning" };

            StringAssert.Contains("expression 'Grinning' must be lowercase letters, digits and underscores", Refusal(entry));
        }

        [Test]
        public void Refuses_AnExpressionDeclaredTwice()
        {
            var entry = Caravan();
            entry.speakers[0].expressions = new[] { "neutral", "neutral" };

            StringAssert.Contains("expression 'neutral' is declared twice", Refusal(entry));
        }

        [Test]
        public void Refuses_AnAssetsRelativeBustPath()
        {
            var entry = Caravan();
            entry.speakers[0].bustPath = "Assets/_Project/Resources/Portraits/Dialogue/rat_merchant";

            StringAssert.Contains("bustPath 'Assets/_Project/Resources/Portraits/Dialogue/rat_merchant' must be RESOURCES-relative",
                Refusal(entry));
        }
    }
}
