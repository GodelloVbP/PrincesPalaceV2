using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // The line under a choice: derived from the effects a pick would apply,
    // never authored (EventChoicePreview). Every expected string is a literal.
    public class EventChoicePreviewTests
    {
        private static string Name(string id) => id == "health_potion" ? "Health Potion" : null;

        private static string Preview(params EventEffect[] effects) =>
            EventEffectSummary.DescribePreview(effects, Name);

        // ---- one literal per effect kind ----------------------------------------------

        [Test]
        public void EachKindIsSpelledForARow()
        {
            Assert.AreEqual("+25 gold", Preview(EventEffect.Gold(25)));
            Assert.AreEqual("-5 gold", Preview(EventEffect.Gold(-5)));
            Assert.AreEqual("Party healed 30%", Preview(EventEffect.HealPercent(30)));
            Assert.AreEqual("Odette healed 40%", Preview(EventEffect.HealMemberPercent("owl", "Odette", 40)));
            Assert.AreEqual("Odette fully healed", Preview(EventEffect.HealMemberPercent("owl", "Odette", 100)));
            Assert.AreEqual("Party hurt 10%", Preview(EventEffect.DamagePercent(10)));
            Assert.AreEqual("+50 XP", Preview(EventEffect.Exp(50)));
            Assert.AreEqual("Shawn +50 XP", Preview(EventEffect.ExpTo("sheep", "Shawn", 50)));
            Assert.AreEqual("+2 Health Potion", Preview(EventEffect.ItemGrant("health_potion", 2)));
            Assert.AreEqual("+10 Prince's favor", Preview(EventEffect.PrincesFavor(10)));
            Assert.AreEqual("Special pools full each turn this leg", Preview(EventEffect.FillSpecialPool()));
            Assert.AreEqual("Relic: Kinship", Preview(EventEffect.RelicGrant("kinship", "Kinship")));
            Assert.AreEqual("Starts a fight", Preview(EventEffect.StartFight("bellwether")));
            Assert.AreEqual("Opens the wares", Preview(EventEffect.OpenShelf("caravan", false)));
            Assert.AreEqual("Takes the wares", Preview(EventEffect.TakeShelf("caravan", 1)));
        }

        [Test]
        public void BookkeepingNamesNothing()
        {
            Assert.AreEqual("", Preview(EventEffect.Counter("tosses", 1)));
            Assert.AreEqual("", Preview(EventEffect.Finish()));
            Assert.AreEqual("", Preview());
        }

        [Test]
        public void SeveralEffectsJoinInOrder()
        {
            Assert.AreEqual("-5 gold  ·  +50 XP",
                Preview(EventEffect.Gold(-5), EventEffect.Counter("t", 1), EventEffect.Exp(50)));
        }

        [Test]
        public void ARowLineOverItsBudgetFoldsItsGrantsIntoACount()
        {
            var line = Preview(EventEffect.Gold(100), EventEffect.ItemGrant("health_potion", 1),
                EventEffect.ItemGrant("health_potion", 1), EventEffect.ItemGrant("health_potion", 1));

            Assert.AreEqual("+100 gold  ·  +3 items", line);
        }

        // ---- the pick's own resolution ------------------------------------------------

        private static ResolvedEventPage Page(params ResolvedEventChoice[] choices) =>
            new ResolvedEventPage("p", "", "T", "B", choices);

        private static ResolvedEventOutcome Out(EventRequirement[] requires, params EventEffect[] effects) =>
            new ResolvedEventOutcome(requires, effects, "", "", true);

        [Test]
        public void ThePreviewIsTheOwnEffectsThenTheOutcomeThatWinsNow()
        {
            var counterReached = new[] { EventRequirement.Counter("tosses", 10, 10) };
            var choice = new ResolvedEventChoice("Toss", new EventRequirement[0], false,
                new[] { EventEffect.Gold(-5), EventEffect.Counter("tosses", 1) },
                new[] { Out(counterReached, EventEffect.Exp(50)), Out(new EventRequirement[0]) });
            var page = Page(choice);
            var context = new FakeEventContext { GoldValue = 100 };

            Assert.AreEqual("-5 gold", EventChoicePreview.For(page, 0, context, Name));

            context.Counters["tosses"] = 10;
            Assert.AreEqual("-5 gold  ·  +50 XP", EventChoicePreview.For(page, 0, context, Name));
        }

        [Test]
        public void ALockedChoiceHasNoPreviewAndLeavingHasNone()
        {
            var costly = new ResolvedEventChoice("Pay", new EventRequirement[0], false,
                new[] { EventEffect.Gold(-5) }, new[] { Out(new EventRequirement[0]) });
            var leave = new ResolvedEventChoice("Leave", new EventRequirement[0], false,
                new EventEffect[0], new[] { Out(new EventRequirement[0]) });
            var page = Page(costly, leave);

            Assert.AreEqual("", EventChoicePreview.For(page, 0, new FakeEventContext { GoldValue = 4 }, Name));
            Assert.AreEqual("", EventChoicePreview.For(page, 1, new FakeEventContext(), Name));
        }

        [Test]
        public void AHiddenOutcomeShowsNothingEvenWhenItCouldBeDerived()
        {
            var choice = new ResolvedEventChoice("Open the box", new EventRequirement[0], false,
                new EventEffect[0], new[] { Out(new EventRequirement[0], EventEffect.Gold(30)) }, hideOutcome: true);

            Assert.AreEqual("", EventChoicePreview.For(Page(choice), 0, new FakeEventContext(), Name));
        }

        // ---- the content check ----------------------------------------------------------

        private static readonly Dictionary<string, string> Characters = new Dictionary<string, string>();

        private static bool Resolves(RawEventChoice choice, out string error)
        {
            var entry = new RawEventEntry
            {
                id = "e1",
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "p1", title = "T", body = "B",
                        choices = new[]
                        {
                            new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
                            choice,
                        },
                    },
                },
            };
            bool ok = EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, Characters, new string[0],
                out _, out var errors);
            error = errors == null ? "" : string.Join("; ", errors);
            return ok;
        }

        private static RawEventChoice Pick(bool hide, params RawEventEffect[] outcomeEffects) => new RawEventChoice
        {
            text = "Pick",
            hideOutcome = hide,
            outcomes = new[] { new RawEventOutcome { goTo = "Leave", effects = outcomeEffects } },
        };

        [Test]
        public void AChoiceWithADerivableOutcomeResolves()
        {
            Assert.IsTrue(Resolves(Pick(false, new RawEventEffect { kind = "gold", amount = 5 }), out var error), error);
        }

        [Test]
        public void AChoiceWithNothingToShowResolvesWithoutTheFlag()
        {
            Assert.IsTrue(Resolves(Pick(false), out var error), error);
        }

        [Test]
        public void AFlaggedChoiceWithAnOutcomeResolves()
        {
            Assert.IsTrue(Resolves(Pick(true, new RawEventEffect { kind = "gold", amount = 5 }), out var error), error);
        }

        [Test]
        public void TheFlagOnAChoiceWithNothingToHideIsRefused()
        {
            Assert.IsFalse(Resolves(Pick(true), out var error));
            StringAssert.Contains("sets hideOutcome but has no outcome to hide", error);
        }

        // A kind added to the enum without a preview wording derives to
        // nothing; the check is what makes that a build error, not a silent
        // blank row.
        [Test]
        public void AnEffectThePreviewCannotSpellIsRefusedUnlessFlagged()
        {
            var unspelled = new EventEffect { Kind = (EventEffectKind)99, Amount = 1 };
            var outcomes = new[] { Out(new EventRequirement[0], unspelled) };

            var plain = new ResolvedEventChoice("Pick", new EventRequirement[0], false, new EventEffect[0], outcomes);
            StringAssert.Contains("cannot spell", EventChoicePreview.CheckAuthoring(plain));

            var flagged = new ResolvedEventChoice("Pick", new EventRequirement[0], false, new EventEffect[0], outcomes, true);
            StringAssert.Contains("no outcome to hide", EventChoicePreview.CheckAuthoring(flagged));
        }
    }
}
