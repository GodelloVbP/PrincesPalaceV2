using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // The event rows docs/PLAN_PETTING_ZOO.md P1 adds, on the fast host:
    // healPercent with a character, inParty with alive, and the relic,
    // princesFavor and fillSpecialPool effects. Fixture events only; the
    // Zoo itself is authored in P4. Every expected value is a literal.
    public class EventZooRowsTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static readonly string[] KnownItems = { "health_potion" };

        private static readonly Dictionary<string, string> KnownRelics =
            new Dictionary<string, string> { ["kinship"] = "Kinship" };

        // One page: a row under test on the first choice, and a free Leave.
        private static RawEventEntry EventWith(RawEventEffect effect = null, RawEventRequirement requirement = null)
        {
            return new RawEventEntry
            {
                id = "zoo_fixture",
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "zoo",
                        title = "T",
                        body = "B",
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Pet the sheep",
                                requires = requirement == null ? new RawEventRequirement[0] : new[] { requirement },
                                effects = effect == null ? new RawEventEffect[0] : new[] { effect },
                                outcomes = new[] { new RawEventOutcome { goTo = "Leave" } },
                            },
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

        private static bool Resolve(RawEventEntry entry, out List<ResolvedEventDefinition> resolved,
            out List<string> errors) =>
            EventEntryResolver.TryResolveAll(new List<RawEventEntry> { entry }, KnownCharacters, KnownItems,
                KnownRelics, out resolved, out errors);

        private static EventEffect FirstEffect(List<ResolvedEventDefinition> resolved) =>
            resolved[0].Pages[0].Choices[0].Effects[0];

        private static EventRequirement FirstRequirement(List<ResolvedEventDefinition> resolved) =>
            resolved[0].Pages[0].Choices[0].Requires[0];

        // ---- healPercent with a character ----------------------------------------------

        [Test]
        public void AOneMemberHeal_ResolvesWithTheMembersNameBakedIn()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "healPercent", amount = 100, character = "sheep" }),
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var effect = FirstEffect(resolved);
            Assert.AreEqual(EventEffectKind.HealPercent, effect.Kind);
            Assert.AreEqual("sheep", effect.CharacterId);
            Assert.AreEqual("Shawn", effect.CharacterDisplayName);
            Assert.AreEqual(100, effect.Amount);
        }

        [Test]
        public void AOneMemberHeal_NamingAnUnknownCharacter_IsRefused()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "healPercent", amount = 50, character = "turtle" }),
                out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("turtle", errors[0]);
        }

        [Test]
        public void ACharacterOnAnyOtherEffectKind_IsRefused()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "damagePercent", amount = 10, character = "sheep" }),
                out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("character is only read by a healPercent or exp effect", errors[0]);
        }

        [TestCase(40, 100, 30, 70)]
        [TestCase(90, 100, 30, 100)]
        [TestCase(1, 120, 100, 120)]
        public void AOneMemberHeal_HealsAStandingMember(int current, int max, int percent, int expected)
        {
            Assert.AreEqual(expected, EventHealth.HealedWithoutRevive(current, max, percent));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void AOneMemberHeal_NeverRevives(int current)
        {
            Assert.AreEqual(0, EventHealth.HealedWithoutRevive(current, 100, 100));
        }

        // The party-wide heal is unchanged and DOES stand a downed member up;
        // pinned so the difference between the two heals is on record.
        [Test]
        public void ThePartyHeal_StillRevives()
        {
            Assert.AreEqual(30, EventHealth.Healed(0, 100, 30));
        }

        // ---- inParty alive -------------------------------------------------------------

        [Test]
        public void Alive_ResolvesOntoTheRequirement()
        {
            bool ok = Resolve(EventWith(requirement: new RawEventRequirement { kind = "inParty", character = "sheep", alive = true }),
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsTrue(FirstRequirement(resolved).RequireStanding);
        }

        [Test]
        public void AliveOnAnyOtherRequirementKind_IsRefused()
        {
            bool ok = Resolve(EventWith(requirement: new RawEventRequirement { kind = "gold", min = 5, alive = true }),
                out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("alive is only read by an inParty requirement", errors[0]);
        }

        [Test]
        public void Alive_PassesForAStandingSquadMember()
        {
            var context = new FakeEventContext { Squad = { "sheep", "owl" } };

            var result = EventRequirement.InParty("sheep", "Shawn", alive: true).Evaluate(context);

            Assert.IsTrue(result.Passed);
            Assert.AreEqual("Requires Shawn standing", result.Reason);
        }

        [Test]
        public void Alive_FailsForADownedSquadMember()
        {
            var context = new FakeEventContext { Squad = { "sheep", "owl" }, Downed = { "sheep" } };

            var result = EventRequirement.InParty("sheep", "Shawn", alive: true).Evaluate(context);

            Assert.IsFalse(result.Passed);
            Assert.AreEqual("Requires Shawn standing", result.Reason);
        }

        [Test]
        public void Alive_FailsForABenchedMember_EvenStanding()
        {
            var context = new FakeEventContext { Squad = { "owl", "bear" } };

            Assert.IsFalse(EventRequirement.InParty("sheep", "Shawn", alive: true).Evaluate(context).Passed);
        }

        // Without alive a downed member still counts as in the party -- the
        // dialogue stage's presence rule relies on exactly that.
        [Test]
        public void WithoutAlive_ADownedMemberStillPasses()
        {
            var context = new FakeEventContext { Squad = { "sheep" }, Downed = { "sheep" } };

            var result = EventRequirement.InParty("sheep", "Shawn").Evaluate(context);

            Assert.IsTrue(result.Passed);
            Assert.AreEqual("Requires Shawn", result.Reason);
        }

        // ---- relic ---------------------------------------------------------------------

        [Test]
        public void ARelicEffect_ResolvesWithTheRelicsName()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "relic", relic = "kinship" }),
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var effect = FirstEffect(resolved);
            Assert.AreEqual(EventEffectKind.Relic, effect.Kind);
            Assert.AreEqual("kinship", effect.RelicId);
            Assert.AreEqual("Kinship", effect.RelicDisplayName);
        }

        [Test]
        public void ARelicEffect_NamingAnUnknownRelic_IsRefused()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "relic", relic = "kinshp" }), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("'kinshp', which is not a relic", errors[0]);
        }

        [Test]
        public void ARelicEffect_WithNoId_IsRefused()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "relic" }), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("relic effect needs a relic id", errors[0]);
        }

        // The five-argument overload knows no relics, so it refuses them all.
        [Test]
        public void TheOverloadWithNoRelicCatalogue_RefusesARelicEffect()
        {
            bool ok = EventEntryResolver.TryResolveAll(
                new List<RawEventEntry> { EventWith(new RawEventEffect { kind = "relic", relic = "kinship" }) },
                KnownCharacters, KnownItems, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a relic", errors[0]);
        }

        // ---- princesFavor / fillSpecialPool ----------------------------------------------

        [Test]
        public void PrincesFavor_Resolves()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "princesFavor", amount = 10 }),
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(EventEffectKind.PrincesFavor, FirstEffect(resolved).Kind);
            Assert.AreEqual(10, FirstEffect(resolved).Amount);
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void PrincesFavor_WithNoPositiveAmount_IsRefused(int amount)
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "princesFavor", amount = amount }), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("princesFavor effect amount must be greater than 0", errors[0]);
        }

        [Test]
        public void FillSpecialPool_WithAmountOne_Resolves()
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "fillSpecialPool", amount = 1 }),
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(EventEffectKind.FillSpecialPool, FirstEffect(resolved).Kind);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void FillSpecialPool_WithAnyOtherAmount_IsRefused(int amount)
        {
            bool ok = Resolve(EventWith(new RawEventEffect { kind = "fillSpecialPool", amount = amount }), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("fillSpecialPool effect amount must be 1", errors[0]);
        }

        // ---- the buffs -----------------------------------------------------------------

        private static List<EventBuffEntry> Buffs(params EventBuffEntry[] entries) => entries.ToList();

        [Test]
        public void ARunBuff_IsActiveOnEveryLeg()
        {
            var buffs = Buffs(new EventBuffEntry { kind = EventBuffs.PrincesFavor, amount = 10, legStartStep = -1 });

            Assert.AreEqual(10, EventBuffs.ActiveAmount(buffs, "princesFavor", 0));
            Assert.AreEqual(10, EventBuffs.ActiveAmount(buffs, "princesFavor", 7));
        }

        [Test]
        public void ALegBuff_IsActiveOnlyOnTheLegItWasGrantedOn()
        {
            var buffs = Buffs(new EventBuffEntry { kind = EventBuffs.FillSpecialPool, amount = 1, legStartStep = 7 });

            Assert.IsTrue(EventBuffs.AnyActive(buffs, "fillSpecialPool", 7));
            Assert.IsFalse(EventBuffs.AnyActive(buffs, "fillSpecialPool", 14));
            Assert.IsFalse(EventBuffs.AnyActive(buffs, "fillSpecialPool", 0));
        }

        [Test]
        public void ActiveAmount_SumsOnlyTheAskedKind()
        {
            var buffs = Buffs(
                new EventBuffEntry { kind = EventBuffs.PrincesFavor, amount = 10, legStartStep = -1 },
                new EventBuffEntry { kind = EventBuffs.PrincesFavor, amount = 5, legStartStep = -1 },
                new EventBuffEntry { kind = EventBuffs.FillSpecialPool, amount = 1, legStartStep = 0 },
                null);

            Assert.AreEqual(15, EventBuffs.ActiveAmount(buffs, "princesFavor", 0));
            Assert.AreEqual(1, EventBuffs.ActiveAmount(buffs, "fillSpecialPool", 0));
            Assert.AreEqual(0, EventBuffs.ActiveAmount(null, "princesFavor", 0));
        }

        [Test]
        public void Prune_DropsNullRowsAndUnknownKinds()
        {
            var buffs = Buffs(
                null,
                new EventBuffEntry { kind = "princesFavor", amount = 10, legStartStep = -1 },
                new EventBuffEntry { kind = "doubleGold", amount = 2, legStartStep = -1 },
                new EventBuffEntry { kind = "", amount = 1, legStartStep = -1 },
                new EventBuffEntry { kind = "fillSpecialPool", amount = 1, legStartStep = 7 });

            EventBuffs.Prune(buffs);

            CollectionAssert.AreEqual(new[] { "princesFavor", "fillSpecialPool" }, buffs.Select(b => b.kind).ToArray());
        }

        // ---- the effects line ----------------------------------------------------------

        [Test]
        public void TheEffectsLine_NamesTheNewKinds()
        {
            var line = EventEffectSummary.Describe(new[]
            {
                EventEffect.HealMemberPercent("sheep", "Shawn", 100),
                EventEffect.HealMemberPercent("owl", "Odette", 30),
                EventEffect.PrincesFavor(10),
                EventEffect.RelicGrant("kinship", "Kinship"),
                EventEffect.FillSpecialPool(),
            }, _ => null);

            Assert.AreEqual(
                "Shawn fully healed  ·  Odette healed 30%  ·  +10 Prince's favor  ·  Relic: Kinship  ·  " +
                "Special pools full each turn this leg",
                line);
        }

        // exp with a character reached that member alone, so the line names
        // them; squad-wide exp keeps "+N XP".
        [Test]
        public void TheEffectsLine_NamesTheMemberAnExpGrantReached()
        {
            var line = EventEffectSummary.Describe(new[]
            {
                EventEffect.ExpTo("sheep", "Shawn", 80),
                EventEffect.Exp(25),
            }, _ => null);

            Assert.AreEqual("Shawn +80 XP  ·  +25 XP", line);
        }
    }
}
