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
    // every expected page, amount, line and caption is a literal.
    //
    // One choice per visit (owner, 2026-09-25): every pick plays its scene
    // and the event ends. The one way back to `zoo` is declining on `petter`,
    // where nothing was chosen.
    //
    // What runs through RunOrchestrator (reload, a failed
    // save, HP actually moving) is PlayMode: PettingZooRunTests.
    public class PettingZooEventTests
    {
        private const string Counter = "zoo_sheep";
        private const string ZooArt = "Assets/_Project/Art/Events/petting_zoo/zoo.png";
        private const string Goodbye = "Say goodbye";
        private const string LeaveBe = "Leave the sheep be";
        private const string KinshipLine = "Future visits with Shawn grant Kinship for that run.";

        // ---- loading the real files ------------------------------------------------------

        private static List<ResolvedRelic> Relics() => RealEventCatalogue.Relics();

        private static List<ResolvedEventDefinition> Events() => RealEventCatalogue.Events();

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

        private static readonly string[] StepPages =
        {
            "step1_pair", "step1_solo", "step2", "step3", "step4", "step5", "step6", "step7", "step8", "step9",
            "step10", "post_arc",
        };

        private static readonly string[] AllPages = new[] { "zoo", "petter" }.Concat(StepPages).ToArray();

        // A scene page plays its lines and then only ends the event: every
        // row is an unconditional, effect-free, silent Leave.
        private static bool IsScenePage(ResolvedEventPage page) =>
            page.Choices.Length > 0
            && page.Choices.All(c => c.Effects.Length == 0 && c.Outcomes.All(o => o.IsLeave && o.Effects.Length == 0 && o.Result == ""));

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

        // The set piece is the page's artPath (dialogue stage contract 6); the
        // file does not exist yet, which the stage draws as no layer 2. The
        // backdrop is the stage's default, since the zoo names none.
        [Test]
        public void TheZooAndPetterShowTheSetPiece_AndEveryPageTheDefaultBackdrop()
        {
            Assert.AreEqual(ZooArt, Page("zoo").ArtKey);
            Assert.AreEqual(ZooArt, Page("petter").ArtKey);
            foreach (string id in StepPages) Assert.AreEqual("", Page(id).ArtKey, id);

            foreach (string id in AllPages)
            {
                Assert.AreEqual("Assets/_Project/Art/Backgrounds/Dungeon.png", Page(id).BackdropKey, id);
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
                new[] { "Bjorn pets the sheep", "Odette pets the sheep", LeaveBe },
                Page("petter").Choices.Select(c => c.Text).ToArray());

            foreach (string id in StepPages)
            {
                var page = Page(id);
                Assert.AreEqual(1, page.Choices.Length, id);
                Assert.AreEqual(Goodbye, page.Choices[0].Text, id);
                Assert.AreEqual(0, page.Choices[0].Requires.Length, id);
                var outcome = page.Choices[0].Outcomes.Single();
                Assert.IsTrue(outcome.IsLeave, id);
                Assert.AreEqual(0, outcome.Effects.Length, id);
                Assert.AreEqual("", outcome.Result, id + ": a silent Leave closes at once");
            }
        }

        // ONE CHOICE PER EVENT. Every choice but declining on petter ends the
        // event after its scene: it leaves outright, or goes to a scene page
        // whose only row leaves. "Pet the sheep" without a standing Shawn goes
        // to petter having chosen nothing -- no effects -- and petter's rows
        // are held to the same rule. The one edge back to zoo is declining.
        [Test]
        public void EveryChoiceButLeaveTheSheepBe_EndsTheEventAfterItsScene()
        {
            var zoo = Zoo();
            var backToZoo = new List<string>();

            foreach (var page in zoo.Pages)
            {
                foreach (var choice in page.Choices)
                {
                    foreach (var outcome in choice.Outcomes)
                    {
                        string where = $"{page.Id} '{choice.Text}' -> {outcome.GoTo}";
                        if (outcome.GoTo == "zoo") backToZoo.Add($"{page.Id}/{choice.Text}");
                        if (choice.Text == LeaveBe || outcome.IsLeave) continue;

                        if (outcome.GoTo == "petter")
                        {
                            Assert.AreEqual("Pet the sheep", choice.Text, where);
                            Assert.AreEqual(0, choice.Effects.Length + outcome.Effects.Length, where + ": nothing is chosen yet");
                            continue;
                        }

                        Assert.IsTrue(IsScenePage(zoo.PageById(outcome.GoTo)), where + ": not a page that only ends the event");
                    }
                }
            }

            CollectionAssert.AreEqual(new[] { "petter/" + LeaveBe }, backToZoo);
        }

        // THE BOT-LOOP GUARD. A first-available bot could circle zoo <-> petter
        // only if Pet sends it to petter AND petter offers no pet row, which
        // needs a squad with nobody standing -- a run over, not a node reached.
        // Every squad drawn from the real roster, every standing/downed mix,
        // at counters inside, at the end of, and past the arc.
        [Test]
        public void WheneverAnyoneIsStanding_ShawnsBranchFiresOrPetterShowsAPetRow()
        {
            var roster = RealEventCatalogue.CharacterNames().Keys.ToList();
            var petter = Page("petter");
            int checkedSquads = 0;

            for (int members = 1; members < 1 << roster.Count; members++)
            {
                var squad = roster.Where((_, i) => (members & (1 << i)) != 0).ToList();
                for (int downedMask = 0; downedMask < 1 << squad.Count; downedMask++)
                {
                    if (downedMask == (1 << squad.Count) - 1) continue; // nobody standing
                    foreach (int counter in new[] { 0, 5, 9, 10, 40 })
                    {
                        var context = Party(squad.ToArray());
                        for (int i = 0; i < squad.Count; i++)
                        {
                            if ((downedMask & (1 << i)) != 0) context.Downed.Add(squad[i]);
                        }

                        context.Counters[Counter] = counter;
                        string label = $"[{string.Join(",", squad)}] downed [{string.Join(",", context.Downed)}] counter {counter}";

                        var pet = Pet(context);
                        checkedSquads++;
                        if (pet.NextPageId != "petter") continue;

                        bool aPetRowShows = petter.Choices
                            .Where(c => c.Text != LeaveBe)
                            .Any(c => EventChoiceGate.Evaluate(c, context).Enabled);
                        Assert.IsTrue(aPetRowShows, label + ": petter offers only '" + LeaveBe + "', so a bot loops");
                    }
                }
            }

            Assert.Greater(checkedSquads, 0, "fixture: the roster is empty");
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

        // Declining is not a choice: no effect, no text, back to the opening page.
        [Test]
        public void LeaveTheSheepBe_ChangesNothing_AndGoesBackToTheZoo()
        {
            var petter = Page("petter");
            var choice = petter.Choices[Index(petter, LeaveBe)];

            Assert.AreEqual(0, choice.Requires.Length);
            Assert.IsFalse(choice.HiddenUntilMet);
            Assert.AreEqual(0, choice.Effects.Length);
            var outcome = choice.Outcomes.Single();
            Assert.AreEqual(0, outcome.Effects.Length);
            Assert.AreEqual("", outcome.Result);
            Assert.AreEqual("zoo", outcome.GoTo);
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
            Assert.AreEqual("", pet.Result, "the step page's lines are the text; a result would play before them");
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

        [TestCase("Bjorn pets the sheep", "bear",
            "Bjorn scratches the sheep behind the ears with one enormous claw. It leans right into it.")]
        [TestCase("Odette pets the sheep", "owl",
            "Odette preens a tuft of the sheep's wool back into place. The sheep allows this.")]
        public void APetterRow_ShowsForAStandingMember_HealsThemAlone_AndEndsTheEvent(string text, string id, string result)
        {
            var petter = Page("petter");
            int index = Index(petter, text);
            var context = Party("bear", "owl");

            var state = EventChoiceGate.Evaluate(petter.Choices[index], context);
            Assert.IsTrue(state.Visible);
            Assert.IsTrue(state.Enabled);

            var resolution = EventFlow.Resolve(petter, index, context);
            Assert.IsTrue(resolution.IsLeave);
            Assert.AreEqual(result, resolution.Result);
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

        [Test]
        public void ThePeacock_AtCharisma19_IsGreyedNotHidden()
        {
            var page = Page("zoo");
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
            var page = Page("zoo");
            var context = Party("sheep", "bear");
            context.Abilities[("sheep", AbilityScore.Charisma)] = 12;
            context.Abilities[("bear", AbilityScore.Charisma)] = charisma;

            int index = Index(page, "Strut for the peacock");
            Assert.IsTrue(EventChoiceGate.Evaluate(page.Choices[index], context).Enabled);

            var resolution = EventFlow.Resolve(page, index, context);
            Assert.IsTrue(resolution.IsLeave);
            Assert.AreEqual(
                "The peacock looks your strut up and down, then fans its tail in grudging approval. " +
                "The crowd along the fence throws coins.",
                resolution.Result);
            var effect = resolution.Effects.Single();
            Assert.AreEqual(EventEffectKind.Gold, effect.Kind);
            Assert.AreEqual(gold, effect.Amount);
        }

        // ---- fawns and the cold one --------------------------------------------------------

        [Test]
        public void TheFawns_AreAlwaysOpen_AndGrantTenFavorForTheRun()
        {
            var page = Page("zoo");
            int index = Index(page, "Feed the fawns");
            var context = Party("bear");

            Assert.IsTrue(EventChoiceGate.Evaluate(page.Choices[index], context).Enabled);
            var resolution = EventFlow.Resolve(page, index, context);

            Assert.IsTrue(resolution.IsLeave);
            Assert.AreEqual(
                "The fawns eat out of your hand and trail you all the way to the gate. " +
                "Word reaches Prince, who purrs about it for days.",
                resolution.Result);
            var effect = resolution.Effects.Single();
            Assert.AreEqual(EventEffectKind.PrincesFavor, effect.Kind);
            Assert.AreEqual(10, effect.Amount);
        }

        [TestCase(14, false)]
        [TestCase(15, true)]
        public void TheColdOne_NeedsALevel15Member_AndFillsSpecialPoolsForTheLeg(int level, bool open)
        {
            var page = Page("zoo");
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
            Assert.AreEqual(
                "It's cold, it's crisp, and it goes straight to everyone's head. " +
                "Whatever waits down the road, the party meets it at full strength.",
                resolution.Result);
            Assert.AreEqual(EventEffectKind.FillSpecialPool, resolution.Effects.Single().Kind);
        }

        // ---- text: the dialogue skeleton ---------------------------------------------------

        private static string Speaker(ResolvedEventLine line) => line.IsNarration ? "narration" : line.SpeakerId;

        private static void AssertLines(string pageId, params (string speaker, DialogueExpression expression, string text)[] expected)
        {
            var lines = Page(pageId).Lines;
            CollectionAssert.AreEqual(expected.Select(e => e.speaker).ToArray(), lines.Select(Speaker).ToArray(), pageId + " speakers");
            CollectionAssert.AreEqual(expected.Select(e => e.text).ToArray(), lines.Select(l => l.Text).ToArray(), pageId + " text");
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i].speaker == "narration") continue;
                Assert.AreEqual(expected[i].expression, lines[i].Expression, $"{pageId} line {i + 1} expression");
            }
        }

        private const DialogueExpression N = DialogueExpression.Neutral;

        // All text is on the skeleton: no page body, and every page plays lines.
        [Test]
        public void EveryPagePlaysLines_AndNoneHasABody()
        {
            foreach (string id in AllPages)
            {
                Assert.IsTrue(Page(id).HasLines, id);
                Assert.AreEqual("", Page(id).Body, id);
            }
        }

        // The owner's script, verbatim (M1). Odette speaks only here, where
        // the outcome that leads in requires her in the squad.
        [Test]
        public void StepOnePair_IsTheOwnersScript_ShawnLeftOdetteRight()
        {
            AssertLines("step1_pair",
                ("sheep", DialogueExpression.Surprised, "Is that... A sheep?"),
                ("owl", DialogueExpression.Neutral, "I presume so."),
                ("sheep", DialogueExpression.Nervous, "..."),
                ("owl", DialogueExpression.Neutral, "Is that a good or a bad thing?"),
                ("sheep", DialogueExpression.Nervous, "I... I don't know..."),
                ("narration", N, "<i>Shawn proceeds to pet the sheep.</i>"));

            var cast = Page("step1_pair").Cast;
            CollectionAssert.AreEqual(new[] { "sheep", "owl" }, cast.Select(c => c.CharacterId).ToArray());
            CollectionAssert.AreEqual(new[] { DialogueSide.Left, DialogueSide.Right }, cast.Select(c => c.Side).ToArray());
        }

        [Test]
        public void StepOneSolo_IsShawnAlone()
        {
            AssertLines("step1_solo",
                ("sheep", DialogueExpression.Surprised, "Is that... A sheep?"),
                ("sheep", DialogueExpression.Nervous, "..."),
                ("sheep", DialogueExpression.Nervous, "I... I don't know how I feel about this."),
                ("narration", N, "<i>Shawn proceeds to pet the sheep.</i>"));

            var cast = Page("step1_solo").Cast.Single();
            Assert.AreEqual("sheep", cast.CharacterId);
            Assert.AreEqual(DialogueSide.Left, cast.Side);
        }

        [Test]
        public void StepsTwoToTen_AreVisiblyPlaceholder_AndTheArcsEndNamesKinship()
        {
            for (int k = 2; k <= 9; k++)
            {
                AssertLines("step" + k, ("narration", N, "-> placeholder"));
            }

            AssertLines("step10", ("narration", N, "-> placeholder"), ("narration", N, KinshipLine));
            AssertLines("post_arc", ("narration", N, "-> placeholder"), ("narration", N, KinshipLine));
        }

        [Test]
        public void TheZooAndPetter_AreNarrated()
        {
            AssertLines("zoo",
                ("narration", N, "Somewhere between two realities, someone has fenced off a paddock and hung a sign: PETTING ZOO."),
                ("narration", N, "A sheep chews at the rail. A peacock fans its tail at nobody in particular. " +
                                 "Two fawns nose at an empty trough, and a cooler of cold ones sweats by the gate."),
                ("narration", N, "Prince's contract says nothing about petting zoos. It doesn't forbid them either."));

            AssertLines("petter",
                ("narration", N, "The sheep looks your party over and seems unimpressed. It was expecting someone woollier."),
                ("narration", N, "Still, a scratch is a scratch, if anyone cares to give it one."));
        }
    }
}
