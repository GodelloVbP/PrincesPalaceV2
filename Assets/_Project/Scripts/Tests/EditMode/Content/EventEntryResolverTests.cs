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
            entry.pages[0].artPath = "Assets/_Project/Art/Events/demo_well.png";

            bool ok = Resolve(entry, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Assets/_Project/Art/Events/demo_well.png", resolved[0].Pages[0].ArtKey);
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
