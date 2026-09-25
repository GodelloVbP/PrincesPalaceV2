using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // EventCastPresence's forward dataflow (docs/PLAN_DIALOGUE_STAGE.md
    // contract 4), on hand-built resolved graphs so each case names exactly
    // the edges it is about. Expected sets are literals.
    public class EventCastPresenceTests
    {
        private static EventRequirement Sheep => EventRequirement.InParty("sheep", "Shawn");
        private static EventRequirement Owl => EventRequirement.InParty("owl", "Odette");

        private static ResolvedEventOutcome To(string pageId, params EventRequirement[] requires) =>
            new ResolvedEventOutcome(requires, null, "", pageId, false);

        private static ResolvedEventOutcome Leave() => new ResolvedEventOutcome(null, null, "", "", true);

        private static ResolvedEventChoice Choice(EventRequirement[] requires, params ResolvedEventOutcome[] outcomes) =>
            new ResolvedEventChoice("c", requires, false, null, outcomes);

        private static ResolvedEventChoice Choice(params ResolvedEventOutcome[] outcomes) =>
            Choice(null, outcomes);

        private static ResolvedEventPage Page(string id, params ResolvedEventChoice[] choices) =>
            new ResolvedEventPage(id, "", "", "", choices);

        private static ResolvedEventDefinition Event(EventRequirement[] requires, params ResolvedEventPage[] pages) =>
            new ResolvedEventDefinition("e", 0, null, requires, pages);

        private static string[] At(Dictionary<string, HashSet<string>> sets, string pageId) =>
            sets[pageId].OrderBy(id => id).ToArray();

        [Test]
        public void TheStartPage_GetsTheEventLevelRequires()
        {
            var sets = EventCastPresence.GuaranteedByPage(Event(new[] { Sheep }, Page("start", Choice(Leave()))));

            CollectionAssert.AreEqual(new[] { "sheep" }, At(sets, "start"));
        }

        [Test]
        public void AnEdge_AddsItsChoiceAndOutcomeRequiresToTheTarget()
        {
            var sets = EventCastPresence.GuaranteedByPage(Event(null,
                Page("start", Choice(new[] { Sheep }, To("next", Owl))),
                Page("next", Choice(Leave()))));

            CollectionAssert.AreEqual(new string[0], At(sets, "start"));
            CollectionAssert.AreEqual(new[] { "owl", "sheep" }, At(sets, "next"));
        }

        [Test]
        public void TwoIncomingEdges_OnlyOneGuaranteeing_GuaranteeNobody()
        {
            var sets = EventCastPresence.GuaranteedByPage(Event(null,
                Page("start", Choice(To("variant", Sheep), To("variant"))),
                Page("variant", Choice(Leave()))));

            CollectionAssert.AreEqual(new string[0], At(sets, "variant"));
        }

        [Test]
        public void ALoop_ReachesAFixpointAndKeepsWhatEveryRouteGuarantees()
        {
            // start -(owl)-> a -(sheep)-> b -> a. a is entered with {owl} and
            // re-entered from b with {owl, sheep}: a = {owl}, b = {owl, sheep}.
            var sets = EventCastPresence.GuaranteedByPage(Event(null,
                Page("start", Choice(To("a", Owl))),
                Page("a", Choice(To("b", Sheep)), Choice(Leave())),
                Page("b", Choice(To("a")))));

            CollectionAssert.AreEqual(new[] { "owl" }, At(sets, "a"));
            CollectionAssert.AreEqual(new[] { "owl", "sheep" }, At(sets, "b"));
        }

        [Test]
        public void ALoopBackToTheStart_IntersectsWithTheEventLevelEntry()
        {
            // Event guarantees sheep; the loop back from `side` adds owl but
            // the entry edge never had it, so the start stays {sheep}.
            var sets = EventCastPresence.GuaranteedByPage(Event(new[] { Sheep },
                Page("start", Choice(To("side", Owl)), Choice(Leave())),
                Page("side", Choice(To("start")))));

            CollectionAssert.AreEqual(new[] { "sheep" }, At(sets, "start"));
            CollectionAssert.AreEqual(new[] { "owl", "sheep" }, At(sets, "side"));
        }

        [Test]
        public void ANamedMemberLevelOrAbility_Guarantees_AnUnnamedOneDoesNot()
        {
            var requires = new[]
            {
                EventRequirement.MemberLevel(5, "bear", "Bjorn"),
                EventRequirement.AbilityAtLeast(AbilityScore.Charisma, 10, "owl", "Odette"),
                EventRequirement.MemberLevel(5),
                EventRequirement.AbilityAtLeast(AbilityScore.Charisma, 10),
                EventRequirement.Gold(5),
                EventRequirement.Counter("c", 1, null),
            };

            CollectionAssert.AreEqual(new[] { "bear", "owl" },
                EventCastPresence.Guaranteed(requires).OrderBy(id => id).ToArray());
        }

        [Test]
        public void AnUnreachablePage_HasNoEntry()
        {
            var sets = EventCastPresence.GuaranteedByPage(Event(null,
                Page("start", Choice(Leave())),
                Page("orphan", Choice(Leave()))));

            Assert.IsFalse(sets.ContainsKey("orphan"));
        }

        // ---- through the resolver: the refusal and the skip ------------------

        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static RawEventChoice RawChoice(params RawEventOutcome[] outcomes) =>
            new RawEventChoice { text = "Go", outcomes = outcomes };

        private static RawEventOutcome RawTo(string goTo, params RawEventRequirement[] requires) =>
            new RawEventOutcome { goTo = goTo, requires = requires };

        private static RawEventRequirement RawInParty(string character) =>
            new RawEventRequirement { kind = "inParty", character = character };

        private static RawEventPage RawPage(string id, RawEventLine[] lines, params RawEventChoice[] choices) =>
            new RawEventPage { id = id, lines = lines, choices = choices };

        private static bool Resolve(RawEventEntry entry, out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, new string[0], out _, out errors);

        private static readonly RawEventLine[] SheepSpeaks = { new RawEventLine { speaker = "sheep", text = "Baa." } };

        [Test]
        public void TheWorkedEventShape_ResolvesWithAGuardedVariantAndANarrationFallback()
        {
            // Plan "Worked event": the conditional variant is gated on
            // inParty sheep, the unconditional last outcome is narration only.
            var entry = new RawEventEntry
            {
                id = "zoo",
                pages = new[]
                {
                    RawPage("pen", new[] { new RawEventLine { speaker = "narration", text = "A pen." } },
                        RawChoice(RawTo("shawn", RawInParty("sheep")), RawTo("pet_narration")),
                        RawChoice(RawTo("Leave"))),
                    RawPage("shawn", SheepSpeaks, RawChoice(RawTo("Leave"))),
                    RawPage("pet_narration", new[] { new RawEventLine { speaker = "narration", text = "Soft." } },
                        RawChoice(RawTo("Leave"))),
                },
            };

            Assert.IsTrue(Resolve(entry, out var errors), string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void ASpeakerOnAPageOnlyOneRouteGuarantees_IsRefused()
        {
            var entry = new RawEventEntry
            {
                id = "zoo",
                pages = new[]
                {
                    RawPage("pen", null, RawChoice(RawTo("shawn", RawInParty("sheep")), RawTo("shawn"))),
                    RawPage("shawn", SheepSpeaks, RawChoice(RawTo("Leave"))),
                },
            };

            Assert.IsFalse(Resolve(entry, out var errors));
            StringAssert.Contains("event 'zoo' page 'shawn' line #1: speaker 'sheep' is not guaranteed", string.Join(" | ", errors));
        }

        [Test]
        public void ASpeakerOnAnUnreachablePage_IsNotChecked()
        {
            var entry = new RawEventEntry
            {
                id = "zoo",
                pages = new[]
                {
                    RawPage("pen", null, RawChoice(RawTo("Leave"))),
                    RawPage("orphan", SheepSpeaks, RawChoice(RawTo("Leave"))),
                },
            };

            Assert.IsTrue(Resolve(entry, out var errors), string.Join("; ", errors ?? new List<string>()));
        }
    }
}
