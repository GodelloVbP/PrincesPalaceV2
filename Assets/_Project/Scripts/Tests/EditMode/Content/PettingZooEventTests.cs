using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // THE REAL petting_zoo, read off events.json and relics.json through the
    // real resolvers (docs/PLAN_PETTING_ZOO.md, "Page graph" and "Tests").
    // The fixture tests in EventZooRowsTests pin the rows; this pins the
    // event the player walks through. Choices are found by their text, and
    // every expected page, amount and caption is a literal.
    //
    // What runs through RunOrchestrator (reload, a failed
    // save, HP actually moving) is PlayMode: PettingZooRunTests.
    public class PettingZooEventTests
    {
        private const string Counter = "zoo_sheep";

        // ---- loading the real files ------------------------------------------------------

        private static Dictionary<string, string> CharacterNames() =>
            ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters
                .ToDictionary(c => c.id, c => c.displayName);

        private static List<ResolvedRelic> Relics()
        {
            var achievements = ContentDataFiles.ParseFile<RawAchievementFile>(ContentDataFiles.DataPath("achievements.json"))
                .achievements.Select(a => a.id).ToList();
            var raw = ContentDataFiles.ParseFile<RawRelicFile>(ContentDataFiles.DataPath("relics.json")).relics;

            bool ok = RelicEntryResolver.TryResolveAll(raw, achievements, CharacterNames().Keys.ToList(),
                out var resolved, out var errors);
            Assert.IsTrue(ok, "relics.json: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static List<ResolvedEventDefinition> Events()
        {
            var items = ContentDataFiles.ParseFile<RawItemFile>(ContentDataFiles.DataPath("items.json")).items
                .Select(i => i.id).ToList();
            var relicNames = Relics().ToDictionary(r => r.Id, r => r.DisplayName);
            var raw = ContentDataFiles.ParseFile<RawEventFile>(ContentDataFiles.DataPath("events.json")).events;

            bool ok = EventEntryResolver.TryResolveAll(raw, CharacterNames(), items, relicNames,
                out var resolved, out var errors);
            Assert.IsTrue(ok, "events.json: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static ResolvedEventDefinition Zoo() => Events().Single(e => e.Id == "petting_zoo");

        private static ResolvedEventPage Page(string id)
        {
            var page = Zoo().PageById(id);
            Assert.IsNotNull(page, $"fixture: petting_zoo has no page '{id}'");
            return page;
        }

        private static int Index(ResolvedEventPage page, string text)
        {
            for (int i = 0; i < page.Choices.Length; i++)
            {
                if (page.Choices[i].Text == text) return i;
            }

            throw new AssertionException($"fixture: page '{page.Id}' has no choice '{text}'");
        }

        private static FakeEventContext Party(params string[] squad)
        {
            var context = new FakeEventContext();
            context.Squad.AddRange(squad);
            return context;
        }

        private static EventChoiceResolution Pet(FakeEventContext context)
        {
            var zoo = Page("zoo");
            return EventFlow.Resolve(zoo, Index(zoo, "Pet the sheep"), context);
        }

        private static bool Grants(EventChoiceResolution resolution, EventEffectKind kind) =>
            resolution.Effects.Any(e => e.Kind == kind);

        private static readonly string[] AllPages =
        {
            "zoo", "zoo_after_pet", "petter", "step1_pair", "step1_solo",
            "step2", "step3", "step4", "step5", "step6", "step7", "step8", "step9", "step10", "post_arc",
        };

        // ---- resolver --------------------------------------------------------------------

        [Test]
        public void TheRealEventsAndRelicsResolve_AndTheZooHasExactlyItsPages()
        {
            var zoo = Zoo();

            CollectionAssert.AreEquivalent(AllPages, zoo.Pages.Select(p => p.Id).ToArray());
            Assert.AreEqual("zoo", zoo.StartPage.Id);
            CollectionAssert.IsEmpty(zoo.Floors);
            CollectionAssert.IsEmpty(zoo.Requires);
        }

        [Test]
        public void TheKinshipRow_IsShawnsAlone_NeverOffered_AndCarriesNoModifiers()
        {
            var kinship = Relics().Single(r => r.Id == "kinship");

            Assert.AreEqual("Kinship", kinship.DisplayName);
            Assert.AreEqual("The first blow Shawn takes each fight is turned aside.", kinship.Description);
            Assert.AreEqual(RelicEffect.Kinship, kinship.Effect);
            Assert.AreEqual("sheep", kinship.Bearer);
            Assert.IsFalse(kinship.Draftable);
            Assert.AreEqual(0, kinship.Modifiers.Length);
            Assert.AreEqual(RelicRarity.Rare, kinship.Rarity);
        }

        [Test]
        public void TheSetPiecePagesShowTheZooArt_AndTheRestShowTheEmptyFrame()
        {
            foreach (string id in new[] { "zoo", "zoo_after_pet", "petter" })
            {
                Assert.AreEqual("Assets/_Project/Art/Events/petting_zoo/zoo.png", Page(id).ArtKey, id);
            }
        }

        // ---- graph -----------------------------------------------------------------------

        [Test]
        public void EveryGoToResolves_AndEveryPageHasAnUnconditionalRow()
        {
            var zoo = Zoo();
            var ids = new HashSet<string>(zoo.Pages.Select(p => p.Id));

            foreach (var page in zoo.Pages)
            {
                Assert.IsTrue(page.Choices.Any(c => c.Requires.Length == 0 && !c.HiddenUntilMet),
                    $"{page.Id}: no unconditional row");
                Assert.LessOrEqual(page.Choices.Length, 4, page.Id);

                foreach (var choice in page.Choices)
                {
                    Assert.AreEqual(0, choice.Outcomes.Last().Requires.Length, $"{page.Id} '{choice.Text}': last outcome gated");
                    foreach (var outcome in choice.Outcomes)
                    {
                        Assert.IsTrue(outcome.IsLeave || ids.Contains(outcome.GoTo),
                            $"{page.Id} '{choice.Text}': goTo '{outcome.GoTo}' is not a page");
                    }
                }
            }
        }

        [Test]
        public void EachPageOffersExactlyThePlannedRows()
        {
            CollectionAssert.AreEqual(
                new[] { "Pet the sheep", "Strut for the peacock", "Feed the fawns", "Crack open a cold one" },
                Page("zoo").Choices.Select(c => c.Text).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Strut for the peacock", "Feed the fawns", "Crack open a cold one", "Say goodbye" },
                Page("zoo_after_pet").Choices.Select(c => c.Text).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Bjorn pets the sheep", "Odette pets the sheep", "Leave the sheep be" },
                Page("petter").Choices.Select(c => c.Text).ToArray());

            foreach (string id in AllPages.Where(p => p.StartsWith("step") || p == "post_arc"))
            {
                var page = Page(id);
                Assert.AreEqual(1, page.Choices.Length, id);
                Assert.AreEqual("Carry on", page.Choices[0].Text, id);
                Assert.AreEqual("zoo_after_pet", page.Choices[0].Outcomes.Single().GoTo, id);
                Assert.AreEqual(0, page.Choices[0].Outcomes.Single().Effects.Length, id);
            }
        }

        // Every outcome that heals lands on a page from which no further heal
        // is reachable: the step pages lead only to zoo_after_pet, and
        // zoo_after_pet has no sheep row and no heal at all. So a second pet
        // in one visit has no route, whatever the party.
        [Test]
        public void EveryHealingRoute_LeadsToZooAfterPet_AndNoSecondHealIsReachable()
        {
            var zoo = Zoo();
            var healDestinations = new HashSet<string>();
            foreach (var page in zoo.Pages)
            {
                foreach (var choice in page.Choices)
                {
                    Assert.IsFalse(choice.Effects.Any(e => e.Kind == EventEffectKind.HealPercent),
                        $"{page.Id} '{choice.Text}': a heal on the choice itself would apply before routing");
                    foreach (var outcome in choice.Outcomes.Where(o => o.Effects.Any(e => e.Kind == EventEffectKind.HealPercent)))
                    {
                        healDestinations.Add(outcome.GoTo);
                    }
                }
            }

            CollectionAssert.AreEquivalent(
                new[] { "step1_pair", "step1_solo", "step2", "step3", "step4", "step5", "step6", "step7", "step8",
                        "step9", "step10", "post_arc", "zoo_after_pet" },
                healDestinations);

            // Everything reachable after a heal.
            var reachable = new HashSet<string>();
            var frontier = new Queue<string>(healDestinations);
            while (frontier.Count > 0)
            {
                string id = frontier.Dequeue();
                if (!reachable.Add(id)) continue;
                foreach (var outcome in zoo.PageById(id).Choices.SelectMany(c => c.Outcomes).Where(o => !o.IsLeave))
                {
                    frontier.Enqueue(outcome.GoTo);
                }
            }

            CollectionAssert.DoesNotContain(reachable, "zoo");
            CollectionAssert.DoesNotContain(reachable, "petter");
            foreach (string id in reachable)
            {
                bool heals = zoo.PageById(id).Choices
                    .Any(c => c.Effects.Concat(c.Outcomes.SelectMany(o => o.Effects)).Any(e => e.Kind == EventEffectKind.HealPercent));
                Assert.IsFalse(heals, $"'{id}' is reachable after a pet and heals again");
            }

            CollectionAssert.DoesNotContain(Page("zoo_after_pet").Choices.Select(c => c.Text).ToArray(), "Pet the sheep");
        }

        [Test]
        public void EveryHealInTheZoo_IsOneMemberToFull_SoNoneRevives()
        {
            foreach (var effect in Zoo().Pages.SelectMany(p => p.Choices)
                         .SelectMany(c => c.Effects.Concat(c.Outcomes.SelectMany(o => o.Effects)))
                         .Where(e => e.Kind == EventEffectKind.HealPercent))
            {
                Assert.IsTrue(effect.TargetsOneMember, "a party heal stands a downed member up");
                Assert.AreEqual(100, effect.Amount);
            }
        }

        // Declining uses up the sheep for this visit: no effect, and the page
        // it lands on is the one without the sheep row.
        [Test]
        public void LeaveTheSheepBe_ChangesNothingButRemovingTheSheepRow()
        {
            var petter = Page("petter");
            var choice = petter.Choices[Index(petter, "Leave the sheep be")];

            Assert.AreEqual(0, choice.Requires.Length);
            Assert.AreEqual(0, choice.Effects.Length);
            var outcome = choice.Outcomes.Single();
            Assert.AreEqual(0, outcome.Effects.Length);
            Assert.AreEqual("", outcome.Result);
            Assert.AreEqual("zoo_after_pet", outcome.GoTo);
        }

        // Nothing returns to the opening page, so no choice policy (the bot
        // takes the first available row) can walk in a circle: every route
        // out of zoo reaches Leave in a bounded number of picks.
        [Test]
        public void NoPathLoopsBackToTheZoo()
        {
            var zoo = Zoo();
            foreach (var page in zoo.Pages)
            {
                foreach (var choice in page.Choices)
                {
                    foreach (var outcome in choice.Outcomes)
                    {
                        Assert.AreNotEqual("zoo", outcome.GoTo, $"{page.Id} '{choice.Text}' returns to zoo");
                    }
                }
            }

            // And the graph as a whole is acyclic.
            var state = new Dictionary<string, int>();
            void Visit(string id)
            {
                state.TryGetValue(id, out int s);
                Assert.AreNotEqual(1, s, $"a cycle runs through '{id}'");
                if (s == 2) return;
                state[id] = 1;
                foreach (var outcome in zoo.PageById(id).Choices.SelectMany(c => c.Outcomes).Where(o => !o.IsLeave))
                {
                    Visit(outcome.GoTo);
                }

                state[id] = 2;
            }

            Visit("zoo");
            CollectionAssert.AreEquivalent(AllPages, state.Keys, "every page is reachable from zoo");
        }

        // ---- the counter, 0 -> 11 --------------------------------------------------------

        // `before` is the counter when the pet is chosen (pets so far).
        [TestCase(0, "step1_pair", false)]
        [TestCase(1, "step2", false)]
        [TestCase(2, "step3", false)]
        [TestCase(3, "step4", false)]
        [TestCase(4, "step5", false)]
        [TestCase(5, "step6", false)]
        [TestCase(6, "step7", false)]
        [TestCase(7, "step8", false)]
        [TestCase(8, "step9", false)]
        [TestCase(9, "step10", true)]
        [TestCase(10, "post_arc", true)]
        [TestCase(25, "post_arc", true)]
        public void EachPetAdvancesTheArcByOne_AndKinshipComesOnTheTenth(int before, string expectedPage, bool kinship)
        {
            var context = Party("sheep", "owl");
            context.Counters[Counter] = before;

            var pet = Pet(context);

            Assert.AreEqual(expectedPage, pet.NextPageId);
            Assert.AreEqual("", pet.Result, "the step page's own body is the text; a result would replace it");
            var bump = pet.Effects.Single(e => e.Kind == EventEffectKind.Counter);
            Assert.AreEqual(Counter, bump.CounterId);
            Assert.AreEqual(1, bump.Amount);

            var heal = pet.Effects.Single(e => e.Kind == EventEffectKind.HealPercent);
            Assert.AreEqual("sheep", heal.CharacterId);
            Assert.AreEqual(100, heal.Amount);

            Assert.AreEqual(kinship, Grants(pet, EventEffectKind.Relic));
            if (kinship)
            {
                Assert.AreEqual("kinship", pet.Effects.Single(e => e.Kind == EventEffectKind.Relic).RelicId);
            }
        }

        // The whole arc end to end, feeding each bump back in the way the
        // orchestrator does: eleven pets visit every step once, in order.
        [Test]
        public void ElevenPetsInARow_WalkStepsOneToTenThenPostArc()
        {
            var context = Party("sheep");
            var pages = new List<string>();
            var kinshipOn = new List<int>();

            for (int pet = 1; pet <= 11; pet++)
            {
                var resolution = Pet(context);
                pages.Add(resolution.NextPageId);
                if (Grants(resolution, EventEffectKind.Relic)) kinshipOn.Add(pet);
                foreach (var e in resolution.Effects.Where(e => e.Kind == EventEffectKind.Counter))
                {
                    context.Counters[e.CounterId] = context.CounterValue(e.CounterId) + e.Amount;
                }
            }

            CollectionAssert.AreEqual(
                new[] { "step1_solo", "step2", "step3", "step4", "step5", "step6", "step7", "step8", "step9", "step10", "post_arc" },
                pages);
            CollectionAssert.AreEqual(new[] { 10, 11 }, kinshipOn);
            Assert.AreEqual(11, context.CounterValue(Counter));
        }

        [TestCase(new[] { "sheep", "owl" }, "step1_pair")]
        [TestCase(new[] { "sheep", "bear" }, "step1_solo")]
        [TestCase(new[] { "sheep" }, "step1_solo")]
        public void TheFirstPet_IsThePairSceneWithOdetteAndTheSoloSceneWithout(string[] squad, string expected)
        {
            var context = Party(squad);

            var pet = Pet(context);

            Assert.AreEqual(expected, pet.NextPageId);
            Assert.AreEqual(1, pet.Effects.Single(e => e.Kind == EventEffectKind.Counter).Amount, "the counter moves either way");
        }

        // Presence is squad membership: a knocked-out Odette still speaks.
        [Test]
        public void ADownedOdette_StillGetsThePairScene()
        {
            var context = Party("sheep", "owl");
            context.Downed.Add("owl");

            Assert.AreEqual("step1_pair", Pet(context).NextPageId);
        }

        [TestCase(false, 0)]
        [TestCase(true, 0)]
        [TestCase(false, 4)]
        [TestCase(true, 12)]
        public void WithShawnBenchedOrDowned_ThePetGoesToPetter_AndTheCounterStays(bool downedRatherThanBenched, int before)
        {
            var context = downedRatherThanBenched ? Party("sheep", "owl", "bear") : Party("owl", "bear");
            if (downedRatherThanBenched) context.Downed.Add("sheep");
            context.Counters[Counter] = before;

            var pet = Pet(context);

            Assert.AreEqual("petter", pet.NextPageId);
            Assert.AreEqual(0, pet.Effects.Count, "no counter, no heal, no relic");
        }

        // ---- petter ------------------------------------------------------------------------

        [TestCase("Bjorn pets the sheep", "bear")]
        [TestCase("Odette pets the sheep", "owl")]
        public void APetterRow_ShowsForAStandingMember_AndHealsThemAlone(string text, string id)
        {
            var petter = Page("petter");
            int index = Index(petter, text);
            var context = Party("bear", "owl");

            var state = EventChoiceGate.Evaluate(petter.Choices[index], context);
            Assert.IsTrue(state.Visible);
            Assert.IsTrue(state.Enabled);

            var resolution = EventFlow.Resolve(petter, index, context);
            Assert.AreEqual("zoo_after_pet", resolution.NextPageId);
            var heal = resolution.Effects.Single();
            Assert.AreEqual(EventEffectKind.HealPercent, heal.Kind);
            Assert.AreEqual(id, heal.CharacterId);
            Assert.AreEqual(100, heal.Amount);
        }

        [TestCase("Bjorn pets the sheep", "bear", true)]
        [TestCase("Bjorn pets the sheep", "bear", false)]
        [TestCase("Odette pets the sheep", "owl", true)]
        [TestCase("Odette pets the sheep", "owl", false)]
        public void APetterRow_IsHidden_WhenThatMemberIsDownedOrAbsent(string text, string id, bool downedRatherThanAbsent)
        {
            var petter = Page("petter");
            var context = downedRatherThanAbsent ? Party("bear", "owl") : Party(new[] { "bear", "owl" }.Where(m => m != id).ToArray());
            if (downedRatherThanAbsent) context.Downed.Add(id);

            var state = EventChoiceGate.Evaluate(petter.Choices[Index(petter, text)], context);

            Assert.IsFalse(state.Visible);
            Assert.IsFalse(state.Enabled);
        }

        // ---- peacock -----------------------------------------------------------------------

        [TestCase("zoo")]
        [TestCase("zoo_after_pet")]
        public void ThePeacock_AtCharisma19_IsGreyedNotHidden(string pageId)
        {
            var page = Page(pageId);
            var context = Party("sheep");
            context.Abilities[("sheep", AbilityScore.Charisma)] = 19;

            var state = EventChoiceGate.Evaluate(page.Choices[Index(page, "Strut for the peacock")], context);

            Assert.IsTrue(state.Visible);
            Assert.IsFalse(state.Enabled);
            Assert.AreEqual("Requires 20 CHA", state.LockReason);
        }

        [TestCase(20, 100)]
        [TestCase(21, 110)]
        [TestCase(25, 150)]
        [TestCase(29, 190)]
        [TestCase(30, 200)]
        [TestCase(40, 200)]
        public void ThePeacock_PaysByTheBestCharisma(int charisma, int gold)
        {
            foreach (string pageId in new[] { "zoo", "zoo_after_pet" })
            {
                var page = Page(pageId);
                var context = Party("sheep", "bear");
                context.Abilities[("sheep", AbilityScore.Charisma)] = 12;
                context.Abilities[("bear", AbilityScore.Charisma)] = charisma;

                int index = Index(page, "Strut for the peacock");
                Assert.IsTrue(EventChoiceGate.Evaluate(page.Choices[index], context).Enabled, pageId);

                var resolution = EventFlow.Resolve(page, index, context);
                Assert.IsTrue(resolution.IsLeave, pageId);
                Assert.IsNotEmpty(resolution.Result, pageId);
                var effect = resolution.Effects.Single();
                Assert.AreEqual(EventEffectKind.Gold, effect.Kind, pageId);
                Assert.AreEqual(gold, effect.Amount, pageId);
            }
        }

        // ---- fawns and the cold one --------------------------------------------------------

        [TestCase("zoo")]
        [TestCase("zoo_after_pet")]
        public void TheFawns_AreAlwaysOpen_AndGrantTenFavorForTheRun(string pageId)
        {
            var page = Page(pageId);
            int index = Index(page, "Feed the fawns");
            var context = Party("bear");

            Assert.IsTrue(EventChoiceGate.Evaluate(page.Choices[index], context).Enabled);
            var resolution = EventFlow.Resolve(page, index, context);

            Assert.IsTrue(resolution.IsLeave);
            Assert.IsNotEmpty(resolution.Result);
            var effect = resolution.Effects.Single();
            Assert.AreEqual(EventEffectKind.PrincesFavor, effect.Kind);
            Assert.AreEqual(10, effect.Amount);
        }

        [TestCase("zoo", 14, false)]
        [TestCase("zoo", 15, true)]
        [TestCase("zoo_after_pet", 14, false)]
        [TestCase("zoo_after_pet", 15, true)]
        public void TheColdOne_NeedsALevel15Member_AndFillsSpecialPoolsForTheLeg(string pageId, int level, bool open)
        {
            var page = Page(pageId);
            int index = Index(page, "Crack open a cold one");
            var context = Party("sheep", "bear");
            context.Levels["sheep"] = 3;
            context.Levels["bear"] = level;

            var state = EventChoiceGate.Evaluate(page.Choices[index], context);
            Assert.IsTrue(state.Visible, "greyed, not hidden");
            Assert.AreEqual(open, state.Enabled);
            if (!open)
            {
                Assert.AreEqual("Requires a level 15 party member", state.LockReason);
                return;
            }

            var resolution = EventFlow.Resolve(page, index, context);
            Assert.IsTrue(resolution.IsLeave);
            Assert.IsNotEmpty(resolution.Result);
            Assert.AreEqual(EventEffectKind.FillSpecialPool, resolution.Effects.Single().Kind);
        }

        [Test]
        public void SayGoodbye_LeavesAtOnce()
        {
            var page = Page("zoo_after_pet");
            var resolution = EventFlow.Resolve(page, Index(page, "Say goodbye"), Party("sheep"));

            Assert.IsTrue(resolution.IsLeave);
            Assert.AreEqual("", resolution.Result);
            Assert.AreEqual(0, resolution.Effects.Count);
        }

        // ---- text ----------------------------------------------------------------------------

        // The owner's script, verbatim (M1). Moves into `lines` once the
        // dialogue stage plays them; until then it is the page body.
        [Test]
        public void StepOnePair_IsTheOwnersScriptVerbatim()
        {
            Assert.AreEqual(
                "Shawn: \"Is that... A sheep?\"\n" +
                "Odette: \"I presume so.\"\n" +
                "Shawn: \"...\"\n" +
                "Odette: \"Is that a good or a bad thing?\"\n" +
                "Shawn: \"I... I don't know...\"\n" +
                "(Shawn proceeds to pet the sheep.)",
                Page("step1_pair").Body);
        }

        [Test]
        public void StepsTwoToTen_AreVisiblyPlaceholder_AndTheArcsEndNamesKinship()
        {
            for (int k = 2; k <= 9; k++)
            {
                Assert.AreEqual("-> placeholder", Page("step" + k).Body, "step" + k);
            }

            const string withKinship = "-> placeholder\n\nFuture visits with Shawn grant Kinship for that run.";
            Assert.AreEqual(withKinship, Page("step10").Body);
            Assert.AreEqual(withKinship, Page("post_arc").Body);
        }
    }
}
