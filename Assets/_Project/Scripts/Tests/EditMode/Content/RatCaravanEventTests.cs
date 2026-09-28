using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // THE REAL rat_caravan, read off events.json and enemies.json through the
    // real resolvers (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5, milestone M7b).
    // Every page is walked by its choice text; the shelf, the merchant and the
    // rob fight are pinned field by field. Expected values are literals.
    //
    // The copy is a first draft (docs/EVENTS.md, "The Rat Caravan"); only what
    // the owner would not rewrite is pinned -- page ids, choice texts, who
    // speaks with which face, the effects -- never the prose.
    //
    // What runs through RunOrchestrator (the shelf opened, the fight built and
    // settled, the room cleared, the bot's walk) is PlayMode: RatCaravanRunTests.
    public class RatCaravanEventTests
    {
        private const string Art = "Assets/_Project/Art/Events/rat_caravan/";

        private static ResolvedEventDefinition Caravan() =>
            RealEventCatalogue.Events().Single(e => e.Id == "rat_caravan");

        private static ResolvedEventPage Page(string id)
        {
            var page = Caravan().PageById(id);
            Assert.IsNotNull(page, $"fixture: rat_caravan has no page '{id}'");
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

        private static EventChoiceResolution Pick(string pageId, string text, FakeEventContext context = null)
        {
            var page = Page(pageId);
            return EventFlow.Resolve(page, Index(page, text), context ?? Party("sheep", "bear", "owl"));
        }

        private static void AssertOpensTheShelf(EventChoiceResolution picked, bool reveal, string thenPage)
        {
            var shelf = picked.Effects.Single();
            Assert.AreEqual(EventEffectKind.Shelf, shelf.Kind);
            Assert.AreEqual("caravan", shelf.ShelfId);
            Assert.AreEqual(reveal, shelf.Reveal);
            Assert.AreEqual(thenPage, picked.NextPageId);
            Assert.IsFalse(picked.IsLeave);
        }

        private static void AssertRobs(EventChoiceResolution picked)
        {
            var fight = picked.Effects.Single();
            Assert.AreEqual(EventEffectKind.Fight, fight.Kind);
            Assert.AreEqual("rat_pack", fight.FightId);
            Assert.AreEqual("", picked.NextPageId);
            Assert.IsFalse(picked.IsLeave);
        }

        private static void AssertWalksOn(EventChoiceResolution picked)
        {
            Assert.IsTrue(picked.IsLeave);
            CollectionAssert.IsEmpty(picked.Effects, "Walk on must not finish the caravan, or it never returns");
            Assert.AreNotEqual("", picked.Result, "the merchant calls after you");
        }

        // ---- the event ----------------------------------------------------------------

        [Test]
        public void TheCaravanResolves_Returns_OnAnyFloor_ForAnyone()
        {
            var caravan = Caravan();

            CollectionAssert.AreEqual(new[] { "caravan", "after_browse", "after_odette", "robbed" },
                caravan.Pages.Select(p => p.Id).ToArray());
            Assert.AreEqual("caravan", caravan.StartPage.Id);
            Assert.IsTrue(caravan.MayReturn);
            CollectionAssert.IsEmpty(caravan.Floors);
            CollectionAssert.IsEmpty(caravan.Requires);
            Assert.AreEqual(Art + "road.png", caravan.BackdropKey);
        }

        [Test]
        public void EveryPageIsStaged_OnItsSetPiece()
        {
            foreach (string id in new[] { "caravan", "after_browse", "after_odette" })
            {
                Assert.AreEqual(Art + "caravan.png", Page(id).ArtKey, id);
            }

            Assert.AreEqual(Art + "robbed.png", Page("robbed").ArtKey);

            foreach (var page in Caravan().Pages)
            {
                Assert.IsTrue(page.HasLines, $"{page.Id}: every caravan page plays on the dialogue stage");
                Assert.AreEqual("", page.Body, page.Id);
            }
        }

        // ---- the merchant -------------------------------------------------------------

        [Test]
        public void TheMerchant_IsAnEventSpeaker_WithThreeFaces_AndABustFolder()
        {
            var merchant = Caravan().Speakers.Single();

            Assert.AreEqual("merchant", merchant.Id);
            Assert.AreEqual("Mister Pockets", merchant.Name);
            Assert.AreEqual("Dealer in Nearly Everything", merchant.Epithet);
            Assert.AreEqual("Portraits/Dialogue/rat_merchant", merchant.BustPath);
            CollectionAssert.AreEqual(new[] { "neutral", "grinning", "hostile" }, merchant.Expressions);
        }

        // Greeting and pitch grinning, the talk after buying neutral, the
        // robbery hostile (the brief's three faces, one per mood).
        [TestCase("after_browse", new[] { "neutral" })]
        [TestCase("robbed", new[] { "hostile" })]
        [TestCase("caravan", new[] { "grinning", "neutral" })]
        [TestCase("after_odette", new[] { "grinning", "neutral" })]
        public void TheMerchantsFace_FitsThePage(string pageId, string[] faces)
        {
            var merchant = Page(pageId).Lines.Where(l => l.SpeakerId == "merchant").ToList();
            Assert.IsNotEmpty(merchant, $"{pageId}: the merchant does not speak");
            Assert.IsTrue(merchant.All(l => l.IsEventSpeaker), pageId);
            CollectionAssert.AreEquivalent(faces, merchant.Select(l => l.ExpressionName).Distinct().ToArray(), pageId);
            Assert.IsTrue(merchant.All(l => l.Side == DialogueSide.Right), $"{pageId}: the merchant stands on the right");
        }

        [Test]
        public void OnlyOdettesPage_HasOdette_AndNoOtherPartyMemberSpeaks()
        {
            var odette = Page("after_odette").Lines.Where(l => !l.IsNarration && !l.IsEventSpeaker).ToList();
            CollectionAssert.AreEqual(new[] { "owl" }, odette.Select(l => l.SpeakerId).Distinct().ToArray());
            Assert.IsTrue(odette.All(l => l.Side == DialogueSide.Left));

            foreach (string id in new[] { "caravan", "after_browse", "robbed" })
            {
                CollectionAssert.IsEmpty(Page(id).Lines.Where(l => !l.IsNarration && !l.IsEventSpeaker).ToList(), id);
            }
        }

        // ---- the shelf ----------------------------------------------------------------

        [Test]
        public void TheShelf_IsSeventyPercent_AThirdFake_GearAndTwoConsumables_KeptByTheMerchant()
        {
            var shelf = Caravan().ShelfById("caravan");

            Assert.AreEqual(70, shelf.PriceFactorPercent);
            Assert.AreEqual(3, shelf.FakeShare);
            CollectionAssert.AreEqual(new[] { ShopStock.GearSection }, shelf.Sections);
            Assert.AreEqual(2, shelf.ConsumableCount);
            Assert.AreEqual("The Rat Caravan", shelf.Title);
            Assert.AreEqual("merchant", shelf.KeeperId);
        }

        // ---- the caravan page ---------------------------------------------------------

        [Test]
        public void TheCaravanOffers_Browse_Odette_Rob_WalkOn_InThatOrder()
        {
            var page = Page("caravan");
            CollectionAssert.AreEqual(new[] { "Browse", "Browse with Odette", "Rob him", "Walk on" },
                page.Choices.Select(c => c.Text).ToArray());

            var odette = page.Choices[1];
            Assert.IsTrue(odette.HiddenUntilMet);
            var require = odette.Requires.Single();
            Assert.AreEqual(EventRequirementKind.InParty, require.Kind);
            Assert.AreEqual("owl", require.CharacterId);
            Assert.IsTrue(require.RequireStanding);

            foreach (int i in new[] { 0, 2, 3 }) CollectionAssert.IsEmpty(page.Choices[i].Requires, page.Choices[i].Text);
        }

        [Test]
        public void BrowseWithOdette_IsHiddenWithoutHerStanding()
        {
            var odette = Page("caravan").Choices[1];

            Assert.IsTrue(EventChoiceGate.Evaluate(odette, Party("sheep", "owl")).Visible);
            Assert.IsFalse(EventChoiceGate.Evaluate(odette, Party("sheep", "bear")).Visible);

            var downed = Party("sheep", "owl");
            downed.Downed.Add("owl");
            Assert.IsFalse(EventChoiceGate.Evaluate(odette, downed).Visible);
        }

        [Test]
        public void Browse_OpensThePlainShelf_ThenAfterBrowse() =>
            AssertOpensTheShelf(Pick("caravan", "Browse"), reveal: false, thenPage: "after_browse");

        [Test]
        public void BrowseWithOdette_RevealsTheFakes_ThenHerPage() =>
            AssertOpensTheShelf(Pick("caravan", "Browse with Odette"), reveal: true, thenPage: "after_odette");

        [Test]
        public void RobHim_StartsTheRatPack() => AssertRobs(Pick("caravan", "Rob him"));

        [Test]
        public void WalkOn_LeavesWithNoFinish() => AssertWalksOn(Pick("caravan", "Walk on"));

        // ---- after browsing -----------------------------------------------------------

        // Walk on FIRST, so a first-available bot leaves instead of looping
        // Look again (the Petting Zoo rule).
        [TestCase("after_browse")]
        [TestCase("after_odette")]
        public void AfterBrowsing_WalkOnComesFirst_ThenLookAgain_ThenRob(string pageId)
        {
            CollectionAssert.AreEqual(new[] { "Walk on", "Look again", "Rob him" },
                Page(pageId).Choices.Select(c => c.Text).ToArray());
            Assert.IsTrue(Page(pageId).Choices.All(c => c.Requires.Length == 0 && !c.HiddenUntilMet), pageId);

            AssertWalksOn(Pick(pageId, "Walk on"));
            AssertOpensTheShelf(Pick(pageId, "Look again"), reveal: false, thenPage: "after_browse");
            AssertRobs(Pick(pageId, "Rob him"));
        }

        // ---- the rob ------------------------------------------------------------------

        [Test]
        public void TheRatPack_IsThreeRats_Elite_TheNormalSquad_EndRun_AndPays()
        {
            var fight = Caravan().FightById("rat_pack");

            CollectionAssert.AreEqual(new[] { "rat", "rat", "rat" }, fight.EnemyIds);
            Assert.IsTrue(fight.Elite);
            CollectionAssert.IsEmpty(fight.PartyIds, "the normal fieldable squad");
            Assert.AreEqual(0, fight.SurviveRounds);
            Assert.AreEqual(EventFightLoss.EndRun, fight.Loss);
            Assert.IsTrue(fight.Pays);
            Assert.AreEqual(Art + "road.png", fight.BackdropKey);
        }

        [Test]
        public void ThreeRats_FitTheStagesThreeSlots()
        {
            var rat = RealEventCatalogue.Enemies().Single(e => e.Id == "rat");
            Assert.AreEqual(1, rat.SlotSpan);
            Assert.IsTrue(rat.Active);
        }

        [Test]
        public void WinningTheRob_HandsOverTheShelfLessOne_Finishes_AndGoesToRobbed()
        {
            var robbed = Caravan().FightById("rat_pack").OutcomeFor(EventFightResult.Defeated);

            Assert.AreEqual("robbed", robbed.GoTo);
            Assert.AreNotEqual("", robbed.Result, "the result plays before the robbed page, with the loss on its effects line");
            CollectionAssert.AreEqual(new[] { EventEffectKind.TakeShelf, EventEffectKind.Finish },
                robbed.Effects.Select(e => e.Kind).ToArray());
            Assert.AreEqual("caravan", robbed.Effects[0].ShelfId);
            Assert.AreEqual(1, robbed.Effects[0].Amount);
        }

        [Test]
        public void LeavingRobbed_ClosesAtOnce()
        {
            var left = Pick("robbed", "Leave");

            Assert.IsTrue(left.IsLeave);
            CollectionAssert.IsEmpty(left.Effects);
            Assert.AreEqual("", left.Result, "a silent Leave closes at once");
        }
    }
}
