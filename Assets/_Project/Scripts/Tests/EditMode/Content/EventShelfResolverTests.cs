using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // Merchant shelves at content build (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
    // 3.4, 3.5, Stage B): the `shelves[]` block and the `shelf` / `takeShelf`
    // effects. One test per refusal, each named for it, M1's shape
    // (EventFightResolverTests). Every expected value is a literal.
    public class EventShelfResolverTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static readonly string[] KnownItems = { "health_potion" };
        private static readonly Dictionary<string, string> KnownRelics = new Dictionary<string, string>();
        private static readonly Dictionary<string, int> KnownEnemies = new Dictionary<string, int> { ["rat"] = 1 };

        private static RawEventEffect Shelf(string id = "wares", bool reveal = false) =>
            new RawEventEffect { kind = "shelf", shelf = id, reveal = reveal };

        private static RawEventEffect Take(string id = "wares", int amount = 1) =>
            new RawEventEffect { kind = "takeShelf", shelf = id, amount = amount };

        private static RawEventEffect Finish() => new RawEventEffect { kind = "finish" };

        private static RawEventOutcome GoTo(string page, params RawEventEffect[] effects) =>
            new RawEventOutcome { goTo = page, effects = effects };

        private static RawEventOutcome Leave(params RawEventEffect[] effects) =>
            new RawEventOutcome { goTo = "Leave", effects = effects };

        private static RawEventChoice Choice(string text, RawEventOutcome outcome, params RawEventEffect[] own) =>
            new RawEventChoice { text = text, effects = own, outcomes = new[] { outcome } };

        private static RawEventChoice WalkOn() => Choice("Walk on", Leave());

        private static RawEventShelf Wares() => new RawEventShelf
        {
            id = "wares", priceFactorPercent = 70, fakeShare = 3, sections = new[] { "gear" }, consumableCount = 2,
        };

        // The caravan's shape, trimmed: Browse, Browse with Odette, Rob, Walk
        // on; after_browse; Rob's win hands the shelf over and finishes.
        private static RawEventEntry Caravan(RawEventShelf shelf = null, RawEventChoice browse = null,
            RawEventOutcome robbed = null, bool mayReturn = true)
        {
            return new RawEventEntry
            {
                id = "caravan",
                mayReturn = mayReturn,
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "caravan", title = "T", body = "B",
                        choices = new[]
                        {
                            browse ?? Choice("Browse", GoTo("after_browse", Shelf())),
                            Choice("Browse with Odette", GoTo("after_browse", Shelf(reveal: true))),
                            Choice("Rob him", new RawEventOutcome
                            {
                                effects = new[] { new RawEventEffect { kind = "fight", fight = "rat_pack" } },
                            }),
                            WalkOn(),
                        },
                    },
                    new RawEventPage
                    {
                        id = "after_browse", title = "T", body = "B",
                        choices = new[] { WalkOn(), Choice("Look again", GoTo("after_browse", Shelf())) },
                    },
                },
                fights = new[]
                {
                    new RawEventFight
                    {
                        id = "rat_pack", enemies = new[] { "rat" }, elite = true,
                        onDefeated = robbed ?? Leave(Take(), Finish()),
                    },
                },
                shelves = shelf == null ? new[] { Wares() } : new[] { shelf },
            };
        }

        private static bool Resolve(RawEventEntry entry, out List<ResolvedEventDefinition> resolved, out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, KnownItems, KnownRelics,
                KnownEnemies, out resolved, out errors);

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

        // ---- what resolves ---------------------------------------------------------

        [Test]
        public void TheCaravanShape_ResolvesItsShelfAndEffects()
        {
            var evt = Resolved(Caravan());
            var shelf = evt.ShelfById("wares");

            Assert.AreEqual("wares", shelf.Id);
            Assert.AreEqual(70, shelf.PriceFactorPercent);
            Assert.AreEqual(3, shelf.FakeShare);
            CollectionAssert.AreEqual(new[] { ShopStock.GearSection }, shelf.Sections);
            Assert.AreEqual(2, shelf.ConsumableCount);

            var browse = evt.Pages[0].Choices[0].Outcomes[0].Effects.Single();
            Assert.AreEqual(EventEffectKind.Shelf, browse.Kind);
            Assert.AreEqual("wares", browse.ShelfId);
            Assert.IsFalse(browse.Reveal);
            Assert.IsTrue(evt.Pages[0].Choices[1].Outcomes[0].Effects.Single().Reveal);

            var robbed = evt.Fights[0].OnDefeated.Effects;
            Assert.AreEqual(EventEffectKind.TakeShelf, robbed[0].Kind);
            Assert.AreEqual("wares", robbed[0].ShelfId);
            Assert.AreEqual(1, robbed[0].Amount);
            Assert.AreEqual(EventEffectKind.Finish, robbed[1].Kind);
        }

        [Test]
        public void ASectionNameIsMatchedIgnoringCase()
        {
            var shelf = Wares();
            shelf.sections = new[] { "Gear" };
            CollectionAssert.AreEqual(new[] { ShopStock.GearSection }, Resolved(Caravan(shelf)).Shelves[0].Sections);
        }

        [Test]
        public void AConsumablesOnlyShelf_Resolves()
        {
            var shelf = Wares();
            shelf.sections = new string[0];
            Assert.AreEqual(0, Resolved(Caravan(shelf)).Shelves[0].Sections.Length);
        }

        // ---- the shelf block ---------------------------------------------------------

        [Test]
        public void AShelfWithNoId_IsRefused()
        {
            var shelf = Wares();
            shelf.id = "";
            StringAssert.Contains("a shelf has no id", Refusal(Caravan(shelf)));
        }

        [Test]
        public void ADuplicateShelfId_IsRefused()
        {
            var entry = Caravan();
            entry.shelves = new[] { Wares(), Wares() };
            StringAssert.Contains("duplicate shelf id", Refusal(entry));
        }

        [TestCase(0)]
        [TestCase(101)]
        public void APriceFactorOutsideOneToAHundred_IsRefused(int factor)
        {
            var shelf = Wares();
            shelf.priceFactorPercent = factor;
            StringAssert.Contains("priceFactorPercent must be 1-100", Refusal(Caravan(shelf)));
        }

        [Test]
        public void ANegativeFakeShare_IsRefused()
        {
            var shelf = Wares();
            shelf.fakeShare = -1;
            StringAssert.Contains("fakeShare is -1", Refusal(Caravan(shelf)));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void AConsumableCountOutsideZeroToThree_IsRefused(int count)
        {
            var shelf = Wares();
            shelf.consumableCount = count;
            StringAssert.Contains("consumableCount must be 0-3", Refusal(Caravan(shelf)));
        }

        [TestCase("books")]
        [TestCase("relics")]
        public void ABooksOrRelicsSection_IsRefused_ForHavingNoItemInstance(string section)
        {
            var shelf = Wares();
            shelf.sections = new[] { "gear", section };
            StringAssert.Contains("carry no item instance", Refusal(Caravan(shelf)));
        }

        [Test]
        public void AnUnknownSection_IsRefused()
        {
            var shelf = Wares();
            shelf.sections = new[] { "hats" };
            StringAssert.Contains("section 'hats' is not a shelf section", Refusal(Caravan(shelf)));
        }

        [Test]
        public void TheGearSectionNamedTwice_IsRefused()
        {
            var shelf = Wares();
            shelf.sections = new[] { "gear", "gear" };
            StringAssert.Contains("section 'gear' is named twice", Refusal(Caravan(shelf)));
        }

        [Test]
        public void AShelfThatStocksNothing_IsRefused()
        {
            var shelf = Wares();
            shelf.sections = new string[0];
            shelf.consumableCount = 0;
            StringAssert.Contains("stocks nothing", Refusal(Caravan(shelf)));
        }

        [Test]
        public void AShelfNoEffectNames_IsRefused()
        {
            var entry = Caravan();
            var spare = Wares();
            spare.id = "spare";
            entry.shelves = new[] { Wares(), spare };
            StringAssert.Contains("shelf 'spare': no effect opens or takes it", Refusal(entry));
        }

        // ---- the effects --------------------------------------------------------------

        [Test]
        public void AShelfEffectWithNoShelf_IsRefused()
        {
            StringAssert.Contains("shelf effect needs a shelf id",
                Refusal(Caravan(browse: Choice("Browse", GoTo("after_browse", Shelf(""))))));
        }

        [Test]
        public void AShelfEffectNamingAnUnknownShelf_IsRefused()
        {
            StringAssert.Contains("names shelf 'stall', which is not in this event's shelves",
                Refusal(Caravan(browse: Choice("Browse", GoTo("after_browse", Shelf("stall"))))));
        }

        [Test]
        public void ATakeShelfNamingAnUnknownShelf_IsRefused()
        {
            StringAssert.Contains("takeShelf effect names shelf 'stall'",
                Refusal(Caravan(robbed: Leave(Take("stall"), Finish()))));
        }

        [Test]
        public void TheShelfFieldOnAnotherKind_IsRefused()
        {
            var gold = new RawEventEffect { kind = "gold", amount = 5, shelf = "wares" };
            StringAssert.Contains("shelf is only read by a shelf or takeShelf effect",
                Refusal(Caravan(browse: Choice("Browse", GoTo("after_browse", Shelf(), gold)))));
        }

        [Test]
        public void RevealOnAnythingButShelf_IsRefused()
        {
            var take = Take();
            take.reveal = true;
            StringAssert.Contains("reveal is only read by a shelf effect", Refusal(Caravan(robbed: Leave(take, Finish()))));
        }

        [Test]
        public void AnAmountOnAShelfEffect_IsRefused()
        {
            var shelf = Shelf();
            shelf.amount = 2;
            StringAssert.Contains("shelf effect takes no amount",
                Refusal(Caravan(browse: Choice("Browse", GoTo("after_browse", shelf)))));
        }

        [Test]
        public void ANegativeTakeShelfAmount_IsRefused()
        {
            StringAssert.Contains("takeShelf amount is the cards lost first, 0 or more",
                Refusal(Caravan(robbed: Leave(Take(amount: -1), Finish()))));
        }

        [Test]
        public void APickOpeningTwoShelves_IsRefused()
        {
            // One from the choice's own effects, one from its outcome.
            StringAssert.Contains("a pick opens 2 shelves",
                Refusal(Caravan(browse: Choice("Browse", GoTo("after_browse", Shelf()), Shelf()))));
        }

        [Test]
        public void APickOpeningAShelfAndStartingAFight_IsRefused()
        {
            var both = Choice("Browse", new RawEventOutcome
            {
                effects = new[] { new RawEventEffect { kind = "fight", fight = "rat_pack" } },
            }, Shelf());
            StringAssert.Contains("both opens a shelf and starts a fight", Refusal(Caravan(browse: both)));
        }

        [Test]
        public void APickOpeningAShelfAndLeavingTheEvent_IsRefused()
        {
            StringAssert.Contains("opens a shelf and leaves the event",
                Refusal(Caravan(browse: Choice("Browse", Leave(Shelf())))));
        }

        [Test]
        public void APickOpeningAShelfAndFinishing_IsRefused()
        {
            StringAssert.Contains("opens a shelf and finishes the event",
                Refusal(Caravan(browse: Choice("Browse", GoTo("after_browse", Shelf(), Finish())))));
        }

        [Test]
        public void ATakeShelfAfterFinish_IsRefused()
        {
            StringAssert.Contains("TakeShelf comes after finish", Refusal(Caravan(robbed: Leave(Finish(), Take()))));
        }

        [Test]
        public void AShelfOpenedFromAFightResult_IsRefused()
        {
            StringAssert.Contains("opens a shelf from a fight's result",
                Refusal(Caravan(robbed: GoTo("after_browse", Shelf()))));
        }
    }
}
