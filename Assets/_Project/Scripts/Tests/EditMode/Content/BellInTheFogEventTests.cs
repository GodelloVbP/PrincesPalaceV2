using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // THE REAL bell_in_the_fog, read off events.json, enemies.json and
    // relics.json through the real resolvers (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
    // 1.4, milestone M7a). Every page is walked by its choice text, every
    // squad shape routes "Ask the others", and the fight and its three results
    // are pinned field by field. Expected values are literals.
    //
    // The copy is a first draft (docs/EVENTS.md, "Bell in the Fog"); only
    // what the owner would not rewrite is pinned here -- page ids, choice
    // texts, who speaks, the effects -- never the prose itself, so a rewrite
    // does not fail this file.
    //
    // What runs through RunOrchestrator (the fight built and settled, the room
    // cleared) is PlayMode: BellInTheFogRunTests.
    public class BellInTheFogEventTests
    {
        private const string Art = "Assets/_Project/Art/Events/bell_in_the_fog/";

        private static ResolvedEventDefinition Bell() =>
            RealEventCatalogue.Events().Single(e => e.Id == "bell_in_the_fog");

        private static ResolvedEventPage Page(string id)
        {
            var page = Bell().PageById(id);
            Assert.IsNotNull(page, $"fixture: bell_in_the_fog has no page '{id}'");
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

        private static EventChoiceResolution Pick(string pageId, string text, FakeEventContext context)
        {
            var page = Page(pageId);
            return EventFlow.Resolve(page, Index(page, text), context);
        }

        private static readonly string[] AskPages = { "ask_both", "ask_bjorn", "ask_odette", "ask_none" };

        // ---- the event ----------------------------------------------------------------

        [Test]
        public void TheBellResolves_Returns_OpensOnBell_AndNeedsShawnStanding()
        {
            var bell = Bell();

            CollectionAssert.AreEqual(
                new[] { "bell", "ask_both", "ask_bjorn", "ask_odette", "ask_none", "endure", "break" },
                bell.Pages.Select(p => p.Id).ToArray());
            Assert.AreEqual("bell", bell.StartPage.Id);
            Assert.IsTrue(bell.MayReturn);
            CollectionAssert.IsEmpty(bell.Floors);

            var require = bell.Requires.Single();
            Assert.AreEqual(EventRequirementKind.InParty, require.Kind);
            Assert.AreEqual("sheep", require.CharacterId);
            Assert.IsTrue(require.RequireStanding);

            Assert.AreEqual(Art + "fog_clearing.png", bell.BackdropKey);
        }

        [Test]
        public void EveryPageNamesItsSetPiece_AndEveryPageIsStaged()
        {
            Assert.AreEqual(Art + "stump_bell.png", Page("bell").ArtKey);
            foreach (string id in AskPages) Assert.AreEqual(Art + "stump_bell.png", Page(id).ArtKey, id);
            Assert.AreEqual(Art + "endure.png", Page("endure").ArtKey);
            Assert.AreEqual(Art + "bell_broken.png", Page("break").ArtKey);

            foreach (var page in Bell().Pages)
            {
                Assert.IsTrue(page.HasLines, $"{page.Id}: every Bell page plays on the dialogue stage");
                Assert.AreEqual("", page.Body, page.Id);
                Assert.AreEqual(Art + "fog_clearing.png", page.BackdropKey, page.Id);
            }
        }

        [Test]
        public void ShawnIsEntrancedWheneverHeSpeaksBeforeTheFight()
        {
            foreach (string id in new[] { "bell" }.Concat(AskPages))
            {
                var shawn = Page(id).Lines.Where(l => l.SpeakerId == "sheep").ToList();
                Assert.IsNotEmpty(shawn, $"{id}: Shawn does not speak");
                Assert.IsTrue(shawn.All(l => l.Expression == DialogueExpression.Entranced), $"{id}: not entranced");
            }
        }

        [Test]
        public void TheOthersSpeakOnlyWhereTheirPageGuaranteesThem()
        {
            CollectionAssert.AreEquivalent(new[] { "sheep", "bear", "owl" },
                Page("ask_both").Lines.Where(l => !l.IsNarration).Select(l => l.SpeakerId).Distinct().ToArray());
            CollectionAssert.AreEquivalent(new[] { "sheep", "bear" },
                Page("ask_bjorn").Lines.Where(l => !l.IsNarration).Select(l => l.SpeakerId).Distinct().ToArray());
            CollectionAssert.AreEquivalent(new[] { "sheep", "owl" },
                Page("ask_odette").Lines.Where(l => !l.IsNarration).Select(l => l.SpeakerId).Distinct().ToArray());

            foreach (string id in new[] { "bell", "ask_none", "endure", "break" })
            {
                CollectionAssert.AreEquivalent(new[] { "sheep" },
                    Page(id).Lines.Where(l => !l.IsNarration).Select(l => l.SpeakerId).Distinct().ToArray(), id);
            }
        }

        // ---- the bell page ------------------------------------------------------------

        [Test]
        public void TheBellOffersTouch_Ask_AndWalkAway_InThatOrder()
        {
            var bell = Page("bell");
            CollectionAssert.AreEqual(new[] { "Touch the bell", "Ask the others", "Walk away" },
                bell.Choices.Select(c => c.Text).ToArray());

            var touch = bell.Choices[0].Requires.Single();
            Assert.AreEqual(EventRequirementKind.InParty, touch.Kind);
            Assert.AreEqual("sheep", touch.CharacterId);
            Assert.IsTrue(touch.RequireStanding);
            Assert.IsFalse(bell.Choices[0].HiddenUntilMet);

            Assert.AreEqual(0, bell.Choices[1].Requires.Length);
            Assert.AreEqual(0, bell.Choices[2].Requires.Length);
        }

        [Test]
        public void TouchingTheBell_StartsTheBellwetherFight_AndGoesNowhere()
        {
            var touched = Pick("bell", "Touch the bell", Party("sheep", "bear", "owl"));

            Assert.AreEqual(EventEffectKind.Fight, touched.Effects.Single().Kind);
            Assert.AreEqual("bellwether", touched.Effects.Single().FightId);
            Assert.AreEqual("", touched.NextPageId);
            Assert.IsFalse(touched.IsLeave);
        }

        [Test]
        public void WalkingAway_LeavesWithNoEffectAndNoFinish()
        {
            var walked = Pick("bell", "Walk away", Party("sheep", "bear", "owl"));

            Assert.IsTrue(walked.IsLeave);
            CollectionAssert.IsEmpty(walked.Effects, "Walk away must not finish the event, or it never returns");
            Assert.AreNotEqual("", walked.Result);
        }

        [TestCase(new[] { "sheep", "bear", "owl" }, new string[0], "ask_both")]
        [TestCase(new[] { "sheep", "bear", "owl" }, new[] { "owl" }, "ask_bjorn")]
        [TestCase(new[] { "sheep", "bear", "owl" }, new[] { "bear" }, "ask_odette")]
        [TestCase(new[] { "sheep", "bear", "owl" }, new[] { "bear", "owl" }, "ask_none")]
        [TestCase(new[] { "sheep", "bear" }, new string[0], "ask_bjorn")]
        [TestCase(new[] { "sheep", "owl" }, new string[0], "ask_odette")]
        [TestCase(new[] { "sheep" }, new string[0], "ask_none")]
        public void AskingTheOthers_RoutesByWhoIsStanding(string[] squad, string[] downed, string expected)
        {
            var context = Party(squad);
            foreach (string id in downed) context.Downed.Add(id);

            var asked = Pick("bell", "Ask the others", context);

            Assert.AreEqual(expected, asked.NextPageId);
            CollectionAssert.IsEmpty(asked.Effects, "they hear nothing; asking changes nothing");
            Assert.AreEqual("", asked.Result, "the ask page plays its own lines");
        }

        [Test]
        public void EveryAskPage_ListensAgain_BackToTheBell()
        {
            foreach (string id in AskPages)
            {
                var page = Page(id);
                CollectionAssert.AreEqual(new[] { "Listen again" }, page.Choices.Select(c => c.Text).ToArray(), id);

                var again = EventFlow.Resolve(page, 0, Party("sheep"));
                Assert.AreEqual("bell", again.NextPageId, id);
                CollectionAssert.IsEmpty(again.Effects, id);
            }
        }

        // ---- the fight ----------------------------------------------------------------

        [Test]
        public void TheBellwetherFight_IsShawnAloneForTenTolls_WakeAndNoPay()
        {
            var fight = Bell().FightById("bellwether");

            CollectionAssert.AreEqual(new[] { "bellwether" }, fight.EnemyIds);
            CollectionAssert.AreEqual(new[] { "sheep" }, fight.PartyIds);
            Assert.IsFalse(fight.Elite);
            Assert.AreEqual(10, fight.SurviveRounds);
            Assert.AreEqual("Toll", fight.RoundLabel);
            Assert.AreEqual(EventFightLoss.Wake, fight.Loss);
            Assert.IsFalse(fight.Pays);

            Assert.AreEqual(Art + "fog_clearing.png", fight.BackdropKey);
            Assert.AreEqual(Art + "flock.png", fight.RoundOverlayKey);
            Assert.AreEqual(0.6f, fight.RoundOverlayFromScale);
            Assert.AreEqual(1.3f, fight.RoundOverlayToScale);
            Assert.AreEqual("Audio/Sfx/Events/bell_in_the_fog/toll", fight.RoundSfxPath);
            Assert.AreEqual("Audio/Music/Events/bell_in_the_fog/wind", fight.AmbiencePath);
        }

        [Test]
        public void Endure_GrantsTheTollAndExp_Finishes_AndGoesToEndure()
        {
            var endured = Bell().FightById("bellwether").OutcomeFor(EventFightResult.Survived);

            Assert.AreEqual("endure", endured.GoTo);
            Assert.AreEqual("", endured.Result, "the endure page plays its own lines");
            CollectionAssert.AreEqual(
                new[] { EventEffectKind.Relic, EventEffectKind.Exp, EventEffectKind.Finish },
                endured.Effects.Select(e => e.Kind).ToArray());
            Assert.AreEqual("toll_of_the_flock", endured.Effects[0].RelicId);
            Assert.AreEqual("Toll of the Flock", endured.Effects[0].RelicDisplayName);
            Assert.AreEqual("sheep", endured.Effects[1].CharacterId);
            Assert.AreEqual(80, endured.Effects[1].Amount);
        }

        [Test]
        public void Break_GrantsBothRelicsAndTheSameExp_Finishes_AndGoesToBreak()
        {
            var broke = Bell().FightById("bellwether").OutcomeFor(EventFightResult.Defeated);

            Assert.AreEqual("break", broke.GoTo);
            Assert.AreEqual("", broke.Result);
            CollectionAssert.AreEqual(
                new[] { EventEffectKind.Relic, EventEffectKind.Relic, EventEffectKind.Exp, EventEffectKind.Finish },
                broke.Effects.Select(e => e.Kind).ToArray());
            Assert.AreEqual("toll_of_the_flock", broke.Effects[0].RelicId);
            Assert.AreEqual("bellwethers_bell", broke.Effects[1].RelicId);
            Assert.AreEqual("Bellwether's Bell", broke.Effects[1].RelicDisplayName);
            Assert.AreEqual("sheep", broke.Effects[2].CharacterId);
            Assert.AreEqual(80, broke.Effects[2].Amount);
        }

        [Test]
        public void Fall_OnlyFinishes_AndLeavesOnItsResult()
        {
            var fell = Bell().FightById("bellwether").OutcomeFor(EventFightResult.Fell);

            Assert.IsTrue(fell.IsLeave);
            Assert.AreNotEqual("", fell.Result);
            CollectionAssert.AreEqual(new[] { EventEffectKind.Finish }, fell.Effects.Select(e => e.Kind).ToArray());
        }

        // ---- the results' pages -------------------------------------------------------

        [TestCase(new[] { "sheep", "bear", "owl" }, new string[0], "Bjorn", "Odette")]
        [TestCase(new[] { "sheep", "bear", "owl" }, new[] { "owl" }, "Bjorn", null)]
        [TestCase(new[] { "sheep", "bear", "owl" }, new[] { "bear" }, "Odette", null)]
        [TestCase(new[] { "sheep" }, new string[0], null, null)]
        public void LeavingEndure_TheOthersSayOnlyWhoIsStanding(string[] squad, string[] downed,
            string names, string alsoNames)
        {
            var context = Party(squad);
            foreach (string id in downed) context.Downed.Add(id);

            var left = Pick("endure", "Leave", context);

            Assert.IsTrue(left.IsLeave);
            CollectionAssert.IsEmpty(left.Effects, "the endure page pays nothing twice");
            Assert.AreNotEqual("", left.Result);

            foreach (string name in new[] { "Bjorn", "Odette" })
            {
                bool expected = name == names || name == alsoNames;
                Assert.AreEqual(expected, left.Result.Contains(name), $"'{name}' in: {left.Result}");
            }
        }

        [Test]
        public void LeavingBreak_ClosesAtOnce()
        {
            var left = Pick("break", "Leave", Party("sheep"));

            Assert.IsTrue(left.IsLeave);
            CollectionAssert.IsEmpty(left.Effects);
            Assert.AreEqual("", left.Result, "a silent Leave closes at once");
        }

        // ---- the enemy and the relics -------------------------------------------------

        [Test]
        public void TheBellwether_NeverRolls_RalliesEightPercentToTen_AndHasNoArtYet()
        {
            var bellwether = RealEventCatalogue.Enemies().Single(e => e.Id == "bellwether");

            Assert.AreEqual("The Bellwether", bellwether.DisplayName);
            Assert.IsTrue(bellwether.Active);
            Assert.IsFalse(bellwether.Rollable);
            Assert.AreEqual(8, bellwether.RallyAttackPercentPerStack);
            Assert.AreEqual(10, bellwether.RallyMaxStacks);
            Assert.AreEqual(1, bellwether.SlotSpan);
            Assert.IsFalse(bellwether.IsBoss);

            // No sprite until M9a: an empty spritePath is the stage's
            // deliberate "not drawn yet" state (the name-plate fallback).
            Assert.AreEqual("", bellwether.SpritePath);
        }

        [Test]
        public void BothBellRelics_AreShawnsAlone_NeverOffered_AndSayWhatTheyDo()
        {
            var relics = RealEventCatalogue.Relics();
            var bell = relics.Single(r => r.Id == "bellwethers_bell");
            var toll = relics.Single(r => r.Id == "toll_of_the_flock");

            Assert.AreEqual("Bellwether's Bell", bell.DisplayName);
            Assert.AreEqual(RelicEffect.BellwethersBell, bell.Effect);
            Assert.AreEqual("sheep", bell.Bearer);
            Assert.IsFalse(bell.Draftable);
            Assert.AreEqual("Every transformation Shawn enters lasts one turn longer.", bell.Description);
            // Empty until M9a delivers the icon: a named file that is not there
            // yet is a null sprite, which the scene build's wiring sweep
            // refuses. Empty is the documented flat-circle fallback.
            Assert.AreEqual("", bell.IconPath);

            Assert.AreEqual("Toll of the Flock", toll.DisplayName);
            Assert.AreEqual(RelicEffect.TollOfTheFlock, toll.Effect);
            Assert.AreEqual("sheep", toll.Bearer);
            Assert.IsFalse(toll.Draftable);
            Assert.AreEqual(
                "Every third turn Shawn takes, a ghost flock charges every enemy. It hits harder while he is transformed.",
                toll.Description);
            Assert.AreEqual("", toll.IconPath);
            Assert.AreEqual("Audio/Sfx/Events/bell_in_the_fog/flock_charge", toll.Vfx.sfxPath);
        }
    }
}
