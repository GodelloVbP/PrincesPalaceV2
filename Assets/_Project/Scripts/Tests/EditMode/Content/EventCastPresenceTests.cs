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

        // ---- fights and event speakers (PLAN_EVENTS_BELL_AND_CARAVAN 3.5) -----

        private static ResolvedEventOutcome StartsFight(string fightId, params EventRequirement[] requires) =>
            new ResolvedEventOutcome(requires, new[] { EventEffect.StartFight(fightId) }, "", "", false);

        private static ResolvedEventFight Fight(string id, ResolvedEventOutcome onDefeated,
            ResolvedEventOutcome onFell = null) => new ResolvedEventFight
        {
            Id = id,
            OnDefeated = onDefeated,
            OnFell = onFell ?? new ResolvedEventOutcome(),
            HasOnFell = onFell != null,
        };

        [Test]
        public void AFightResult_IsAnEdgeFromTheLaunchingPageWithTheLaunchingGuarantees()
        {
            // start -choice(sheep)-> outcome(owl) starts `duel`; its onDefeated
            // goes to `won`, its onFell to `lost`. Both carry {owl, sheep}.
            var evt = new ResolvedEventDefinition("e", 0, null, null, new[]
                {
                    Page("start", Choice(new[] { Sheep }, StartsFight("duel", Owl)), Choice(Leave())),
                    Page("won", Choice(Leave())),
                    Page("lost", Choice(Leave())),
                },
                fights: new[] { Fight("duel", To("won"), To("lost")) });

            var sets = EventCastPresence.GuaranteedByPage(evt);

            CollectionAssert.AreEqual(new[] { "owl", "sheep" }, At(sets, "won"));
            CollectionAssert.AreEqual(new[] { "owl", "sheep" }, At(sets, "lost"));
        }

        [Test]
        public void AFightResultPage_ReachedOnlyThroughTheFight_HasAnEntry()
        {
            var evt = new ResolvedEventDefinition("e", 0, null, null, new[]
                {
                    Page("start", Choice(StartsFight("duel")), Choice(Leave())),
                    Page("won", Choice(Leave())),
                },
                fights: new[] { Fight("duel", To("won")) });

            Assert.IsTrue(EventCastPresence.GuaranteedByPage(evt).ContainsKey("won"));
        }

        [Test]
        public void TwoLaunchesOfOneFight_IntersectAtItsResultPage()
        {
            // Launched once with sheep guaranteed and once with nobody: the
            // result page guarantees nobody.
            var evt = new ResolvedEventDefinition("e", 0, null, null, new[]
                {
                    Page("start", Choice(StartsFight("duel", Sheep)), Choice(StartsFight("duel")), Choice(Leave())),
                    Page("won", Choice(Leave())),
                },
                fights: new[] { Fight("duel", To("won")) });

            CollectionAssert.AreEqual(new string[0], At(EventCastPresence.GuaranteedByPage(evt), "won"));
        }

        [Test]
        public void EventSpeakers_ArePresentOnEveryReachablePage()
        {
            var evt = new ResolvedEventDefinition("e", 0, null, new[] { Owl }, new[]
                {
                    Page("start", Choice(To("next"))),
                    Page("next", Choice(Leave())),
                },
                speakers: new[] { new ResolvedEventSpeaker("merchant", "Rat Merchant", "", "", new[] { "neutral" }) });

            var sets = EventCastPresence.GuaranteedByPage(evt);

            CollectionAssert.AreEqual(new[] { "merchant", "owl" }, At(sets, "start"));
            CollectionAssert.AreEqual(new[] { "merchant", "owl" }, At(sets, "next"));
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

        // Through the resolver: a fight's result page speaks for Shawn only
        // if the launch guaranteed him.
        [Test]
        public void ASpeakerOnAFightResultPage_IsRefusedUnlessTheLaunchGuaranteesThem()
        {
            RawEventEntry Entry(params RawEventRequirement[] launchRequires) => new RawEventEntry
            {
                id = "bell",
                pages = new[]
                {
                    RawPage("bell", null,
                        new RawEventChoice
                        {
                            text = "Touch the bell", requires = launchRequires,
                            outcomes = new[]
                            {
                                new RawEventOutcome { effects = new[] { new RawEventEffect { kind = "fight", fight = "duel" } } },
                            },
                        },
                        RawChoice(RawTo("Leave"))),
                    RawPage("endure", SheepSpeaks, RawChoice(RawTo("Leave"))),
                },
                fights = new[]
                {
                    new RawEventFight { id = "duel", enemies = new[] { "rat" }, onDefeated = RawTo("endure") },
                },
            };

            var enemies = new Dictionary<string, int> { ["rat"] = 1 };
            bool Resolves(RawEventEntry entry, out List<string> errors) =>
                EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, new string[0],
                    new Dictionary<string, string>(), enemies, out _, out errors);

            Assert.IsFalse(Resolves(Entry(), out var refused));
            StringAssert.Contains("event 'bell' page 'endure' line #1: speaker 'sheep' is not guaranteed",
                string.Join(" | ", refused));
            Assert.IsTrue(Resolves(Entry(RawInParty("sheep")), out var errors), string.Join("; ", errors ?? new List<string>()));
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
