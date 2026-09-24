using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    public class EventEntryResolverTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static readonly string[] KnownItems = { "health_potion" };

        // A minimal, valid event: one page, one unconditional Leave choice.
        // Every test below starts from this and breaks exactly one thing, so
        // a failure can only be attributed to the change the test made.
        private static RawEventEntry MinimalEvent(string id = "e1")
        {
            return new RawEventEntry
            {
                id = id,
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "p1",
                        title = "T",
                        body = "B",
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Leave",
                                outcomes = new[] { new RawEventOutcome { result = "", goTo = "Leave" } },
                            },
                        },
                    },
                },
            };
        }

        private static bool Resolve(RawEventEntry entry, out List<ResolvedEventDefinition> resolved, out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, KnownItems, out resolved, out errors);

        [Test]
        public void AMinimalEvent_Resolves()
        {
            bool ok = Resolve(MinimalEvent(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("e1", resolved[0].Id);
            Assert.IsTrue(resolved[0].Pages[0].Choices[0].Outcomes[0].IsLeave);
        }

        [Test]
        public void GoTo_IsCaseInsensitiveForTheLeaveKeyword()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].outcomes[0].goTo = "lEaVe";

            bool ok = Resolve(entry, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsTrue(resolved[0].Pages[0].Choices[0].Outcomes[0].IsLeave);
        }

        // ---- art path ---------------------------------------------------------

        // artPath is baked into the Map scene by AssetDatabase, which cannot
        // see a Resources-relative path: written that way the page would show
        // its empty frame forever with nothing logged. ArtPathConventionTests
        // pins the rule; this pins that the event resolver actually asks it.
        [Test]
        public void AResourcesRelativeArtPath_IsRefusedNamingTheField()
        {
            var entry = MinimalEvent();
            entry.pages[0].artPath = "Events/demo_well";

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("artPath", string.Join(" ", errors));
        }

        [Test]
        public void AnAssetsRelativeArtPath_ResolvesOntoThePagesArtKey()
        {
            var entry = MinimalEvent();
            entry.pages[0].artPath = "Assets/_Project/Art/Events/e1/p1.png";

            bool ok = Resolve(entry, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Assets/_Project/Art/Events/e1/p1.png", resolved[0].Pages[0].ArtKey);
        }

        // One folder per event, named by its id (owner's call 2026-09-24).
        // Literal paths throughout: each case is a filing mistake an author
        // could make, and the refusal must name the folder it wanted.
        [TestCase("Assets/_Project/Art/Events/e1_p1.png")]
        [TestCase("Assets/_Project/Art/Events/other_event/p1.png")]
        [TestCase("Assets/_Project/Art/Events/e1/sub/p1.png")]
        [TestCase("Assets/_Project/Art/Events/E1/p1.png")]
        [TestCase("Assets/_Project/Art/Events/e1/")]
        public void EventArtOutsideItsOwnEventFolder_IsRefused(string artPath)
        {
            var entry = MinimalEvent();
            entry.pages[0].artPath = artPath;

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Assets/_Project/Art/Events/e1/<file>", string.Join(" ", errors));
        }

        [Test]
        public void TheShippedDemoShape_Resolves()
        {
            var entry = MinimalEvent("demo_wishing_well");
            entry.pages[0].artPath = "Assets/_Project/Art/Events/demo_wishing_well/well.png";

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        // The folder rule is about Art/Events/ only; a page reusing a shared
        // background elsewhere under Art/ is not misfiled event art.
        [Test]
        public void ArtOutsideTheEventsRoot_IsNotTheFolderRulesBusiness()
        {
            var entry = MinimalEvent();
            entry.pages[0].artPath = "Assets/_Project/Art/Backgrounds/cavern.png";

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        // ---- unknown kinds --------------------------------------------------

        [Test]
        public void UnknownRequirementKind_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].requires = new[] { new RawEventRequirement { kind = "notAKind" } };
            // Give the choice an unconditional sibling so this refusal isn't
            // masked by the "no unconditional choice" one.
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
            StringAssert.Contains("not a known EventRequirementKind", string.Join(" ", errors));
        }

        [Test]
        public void UnknownEffectKind_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "notAKind" } };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
            StringAssert.Contains("not a known EventEffectKind", string.Join(" ", errors));
        }

        // ---- unknown ids ------------------------------------------------------

        [Test]
        public void UnknownCharacterId_OnAnInPartyRequirement_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].requires = new[] { new RawEventRequirement { kind = "inParty", character = "dragon" } };
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
            StringAssert.Contains("dragon", string.Join(" ", errors));
        }

        [Test]
        public void UnknownAbility_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].requires = new[] { new RawEventRequirement { kind = "ability", ability = "luck", min = 5 } };
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
        }

        [Test]
        public void UnknownItemId_OnAnItemEffect_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "item", item = "unobtainium" } };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
            StringAssert.Contains("unobtainium", string.Join(" ", errors));
        }

        // ---- graph shape --------------------------------------------------------

        [Test]
        public void DanglingGoTo_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].outcomes[0].goTo = "nowhere";

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
            StringAssert.Contains("nowhere", string.Join(" ", errors));
        }

        [Test]
        public void MoreThanFourChoicesOnAPage_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices = Enumerable.Range(0, 5)
                .Select(i => new RawEventChoice { text = $"c{i}", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } })
                .ToArray();

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("e1", string.Join(" ", errors));
        }

        [Test]
        public void APageWithNoUnconditionalChoice_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].requires = new[] { new RawEventRequirement { kind = "gold", min = 5 } };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no unconditional choice", string.Join(" ", errors));
        }

        [Test]
        public void APageWhereTheOnlyUnconditionalChoiceIsHiddenUntilMet_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].hiddenUntilMet = true;

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no unconditional choice", string.Join(" ", errors));
        }

        [Test]
        public void ALastOutcomeWithRequirements_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].outcomes = new[]
            {
                new RawEventOutcome { requires = new[] { new RawEventRequirement { kind = "gold", min = 1 } }, goTo = "Leave" },
            };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("last outcome carries requirements", string.Join(" ", errors));
        }

        [Test]
        public void BodyOverTheCap_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].body = new string('x', EventEntryResolver.MaxBodyLength + 1);

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains($"{EventEntryResolver.MaxBodyLength}-character cap", string.Join(" ", errors));
        }

        [Test]
        public void BodyExactlyAtTheCap_Resolves()
        {
            var entry = MinimalEvent();
            entry.pages[0].body = new string('x', EventEntryResolver.MaxBodyLength);

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        // ---- the string caps -------------------------------------------------

        // Pinned as literals: each is the length of the sample EventScreen's
        // box is audited against, so a change here is a change to what the
        // screen must fit (and needs a scene build to re-measure).
        [Test]
        public void TheCapsAreTheFittedSampleLengths()
        {
            Assert.AreEqual(600, EventEntryResolver.MaxBodyLength);
            Assert.AreEqual(28, EventEntryResolver.MaxTitleLength);
            Assert.AreEqual(50, EventEntryResolver.MaxChoiceTextLength);
            Assert.AreEqual(46, EventEntryResolver.MaxLockReasonLength);
        }

        // An event with a second, gated choice, so a requirement on it is
        // legal (the Leave choice stays the unconditional one).
        private static RawEventEntry WithGatedChoice(params RawEventRequirement[] requires)
        {
            var entry = MinimalEvent();
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice
                {
                    text = "Gated",
                    requires = requires,
                    outcomes = new[] { new RawEventOutcome { goTo = "Leave" } },
                },
            };
            return entry;
        }

        [Test]
        public void ATitleOverTheCap_IsRefusedNamingTheEventAndField()
        {
            var entry = MinimalEvent();
            entry.pages[0].title = new string('x', 29);

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual("event 'e1' page 'p1': title is 29 characters, over the 28-character cap.", errors.Single());
        }

        [Test]
        public void ATitleExactlyAtTheCap_Resolves()
        {
            var entry = MinimalEvent();
            entry.pages[0].title = new string('x', 28);

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void AChoiceTextOverTheCap_IsRefusedNamingTheEventAndField()
        {
            var entry = MinimalEvent();
            string text = new string('x', 51);
            entry.pages[0].choices[0].text = text;

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual($"event 'e1' page 'p1' choice '{text}': text is 51 characters, over the 50-character cap.",
                errors.Single());
        }

        [Test]
        public void AnOutcomeResultOverTheBodyCap_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].outcomes[0].result = new string('x', 601);

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual("event 'e1' page 'p1' choice 'Leave': outcome result is 601 characters, over the 600-character cap.",
                errors.Single());
        }

        [Test]
        public void AnAuthoredReasonOverTheCap_IsRefusedNamingTheEventAndField()
        {
            var entry = WithGatedChoice(new RawEventRequirement { kind = "gold", min = 5, reason = new string('x', 47) });

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual("event 'e1' page 'p1' choice 'Gated': lock reason is 47 characters, over the 46-character cap.",
                errors.Single());
        }

        // The cap covers generated captions too: a long display name makes a
        // long "Requires <name> with <n> <score>", and the build is the only
        // place that can see it before a player does.
        [Test]
        public void AGeneratedReasonOverTheCap_IsRefused()
        {
            var characters = new Dictionary<string, string> { ["owl"] = "Odette the Exceedingly Long-Named Owl" };
            var entry = WithGatedChoice(new RawEventRequirement
                { kind = "ability", ability = "charisma", min = 20, character = "owl" });

            bool ok = EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, characters, KnownItems,
                out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual("event 'e1' page 'p1' choice 'Gated': lock reason is 58 characters, over the 46-character cap.",
                errors.Single());
        }

        [Test]
        public void AnAuthoredReason_TravelsOntoTheResolvedRequirement()
        {
            var entry = WithGatedChoice(new RawEventRequirement
                { kind = "inParty", character = "sheep", reason = "Only Shawn would try this" });

            bool ok = Resolve(entry, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Only Shawn would try this", resolved[0].Pages[0].Choices[1].Requires[0].AuthoredReason);
        }

        // Outcome rows never show a caption, so their generated reason is
        // not measured: a long name there is not an error.
        [Test]
        public void ALongGeneratedReasonOnAnOutcomeRow_IsNotMeasured()
        {
            var characters = new Dictionary<string, string> { ["owl"] = "Odette the Exceedingly Long-Named Owl" };
            var entry = MinimalEvent();
            entry.pages[0].choices[0].outcomes = new[]
            {
                new RawEventOutcome
                {
                    requires = new[] { new RawEventRequirement { kind = "inParty", character = "owl" } },
                    goTo = "Leave",
                },
                new RawEventOutcome { goTo = "Leave" },
            };

            bool ok = EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, characters, KnownItems,
                out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        // ---- the counter typo guard (contract 9) -------------------------------

        [Test]
        public void ACounterRequirementNamingACounterNoEffectIncrements_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].requires = new[]
            {
                new RawEventRequirement { kind = "counter", counter = "never_incremented", min = 1 },
            };
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("never_incremented", string.Join(" ", errors));
            StringAssert.Contains("typo", string.Join(" ", errors));
        }

        [Test]
        public void ACounterRequirementWhoseCounterIsIncrementedByAnotherEvent_Resolves()
        {
            var requiring = MinimalEvent("e1");
            requiring.pages[0].choices[0].requires = new[]
            {
                new RawEventRequirement { kind = "counter", counter = "shared_counter", min = 1 },
            };
            requiring.pages[0].choices = new[]
            {
                requiring.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            var incrementing = MinimalEvent("e2");
            incrementing.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "counter", counter = "shared_counter", amount = 1 } };

            bool ok = EventEntryResolver.TryResolveAll(
                new List<RawEventEntry> { requiring, incrementing }, KnownCharacters, KnownItems, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        // ---- implied gold gate (contract 6), exercised through the resolver -----

        [Test]
        public void AChoiceThatSpendsGold_GetsAnImpliedGoldRequirement_WithoutBeingAuthoredByHand()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "gold", amount = -5 } };
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            bool ok = Resolve(entry, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var spendingChoice = resolved[0].Pages[0].Choices[0];
            Assert.AreEqual(1, spendingChoice.Requires.Length);
            Assert.AreEqual(PrincesPalace.Domain.Events.EventRequirementKind.Gold, spendingChoice.Requires[0].Kind);
            Assert.AreEqual(5, spendingChoice.Requires[0].Min);
        }

        // ---- "unconditional" is the runtime gate's own answer --------------------
        // A choice that spends gold is locked for a player short of it, so it
        // cannot be the page's way out. The build reads EventChoiceGate, the
        // same rule the panel, ChooseEventOption and the bot read.

        [Test]
        public void APageWhoseOnlyRequirementFreeChoiceSpendsGold_IsRefused_NamingEventAndPage()
        {
            var entry = MinimalEvent("toll_gate");
            entry.pages[0].id = "bridge";
            entry.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "gold", amount = -50 } };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            string all = string.Join(" ", errors);
            StringAssert.Contains("no unconditional choice", all);
            StringAssert.Contains("toll_gate", all);
            StringAssert.Contains("'bridge'", all);
        }

        [Test]
        public void TheSameGoldSpendingPage_PlusAFreeLeave_Builds()
        {
            var entry = MinimalEvent("toll_gate");
            entry.pages[0].id = "bridge";
            entry.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "gold", amount = -50 } };
            entry.pages[0].choices = new[]
            {
                entry.pages[0].choices[0],
                new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
            };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void AChoiceThatGainsGold_StillCountsAsUnconditional()
        {
            var entry = MinimalEvent();
            entry.pages[0].choices[0].effects = new[] { new RawEventEffect { kind = "gold", amount = 25 } };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        // ---- duplicate ids --------------------------------------------------------

        [Test]
        public void DuplicateEventId_IsRefused()
        {
            bool ok = EventEntryResolver.TryResolveAll(
                new List<RawEventEntry> { MinimalEvent("dup"), MinimalEvent("dup") },
                KnownCharacters, KnownItems, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Duplicate event id", string.Join(" ", errors));
        }

        [Test]
        public void DuplicatePageIdWithinAnEvent_IsRefused()
        {
            var entry = MinimalEvent();
            entry.pages = new[] { entry.pages[0], entry.pages[0] };

            bool ok = Resolve(entry, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("duplicate page id", string.Join(" ", errors));
        }
    }
}
