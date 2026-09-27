using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    // Returning events and event fights at content build
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.1, 1.2, 3.5, Stage A). One test
    // per refusal, each named for it. Every expected value is a literal
    // (CLAUDE.md gotcha 5).
    public class EventFightResolverTests
    {
        private static readonly Dictionary<string, string> KnownCharacters =
            new Dictionary<string, string> { ["sheep"] = "Shawn", ["bear"] = "Bjorn", ["owl"] = "Odette" };

        private static readonly string[] KnownItems = { "health_potion" };
        private static readonly Dictionary<string, string> KnownRelics =
            new Dictionary<string, string> { ["toll_of_the_flock"] = "Toll of the Flock" };

        // rat 1 slot, bellwether 1, treant 2.
        private static readonly Dictionary<string, int> KnownEnemies =
            new Dictionary<string, int> { ["rat"] = 1, ["bellwether"] = 1, ["treant"] = 2 };

        private static RawEventEffect FightEffect(string fightId) => new RawEventEffect { kind = "fight", fight = fightId };
        private static RawEventEffect Finish() => new RawEventEffect { kind = "finish" };
        private static RawEventOutcome Leave(params RawEventEffect[] effects) =>
            new RawEventOutcome { goTo = "Leave", effects = effects };

        private static RawEventChoice WalkAway() =>
            new RawEventChoice { text = "Walk away", outcomes = new[] { Leave() } };

        private static RawEventChoice Touch(RawEventOutcome outcome) =>
            new RawEventChoice { text = "Touch the bell", outcomes = new[] { outcome } };

        private static RawEventOutcome StartFight(string fightId = "bellwether") =>
            new RawEventOutcome { effects = new[] { FightEffect(fightId) } };

        // The Bell's fight, trimmed to what a test varies: wake, 10 rounds,
        // all three results authored and each finishing the event.
        private static RawEventFight BellFight() => new RawEventFight
        {
            id = "bellwether",
            enemies = new[] { "bellwether" },
            party = new[] { "sheep" },
            surviveRounds = 10,
            roundLabel = "Toll",
            onLoss = "wake",
            pays = false,
            onDefeated = Leave(Finish()),
            onSurvived = Leave(Finish()),
            onFell = Leave(Finish()),
        };

        private static RawEventEntry Event(RawEventFight fight, RawEventChoice firstChoice = null, bool mayReturn = true)
        {
            return new RawEventEntry
            {
                id = "bell_in_the_fog",
                mayReturn = mayReturn,
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "bell",
                        title = "T",
                        body = "B",
                        choices = new[] { firstChoice ?? Touch(StartFight()), WalkAway() },
                    },
                },
                fights = fight == null ? new RawEventFight[0] : new[] { fight },
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

        // ---- what resolves ---------------------------------------------------

        [Test]
        public void ABellShapedFight_ResolvesEveryField()
        {
            var fight = BellFight();
            fight.backdrop = "Assets/_Project/Art/Events/bell_in_the_fog/fog_clearing.png";
            fight.roundSfx = "Audio/Sfx/bell_toll";
            fight.ambience = "Audio/Sfx/fog_wind";
            fight.roundOverlay = new EventRoundOverlay
            {
                path = "Assets/_Project/Art/Events/bell_in_the_fog/flock.png", fromScale = 0.5f, toScale = 1.25f,
            };
            fight.onDefeated = Leave(new RawEventEffect { kind = "relic", relic = "toll_of_the_flock" },
                new RawEventEffect { kind = "exp", amount = 40, character = "sheep" }, Finish());

            var evt = Resolved(Event(fight));
            var resolved = evt.Fights.Single();

            Assert.IsTrue(evt.MayReturn);
            Assert.AreEqual("bellwether", resolved.Id);
            CollectionAssert.AreEqual(new[] { "bellwether" }, resolved.EnemyIds);
            CollectionAssert.AreEqual(new[] { "sheep" }, resolved.PartyIds);
            Assert.IsFalse(resolved.Elite);
            Assert.AreEqual(10, resolved.SurviveRounds);
            Assert.AreEqual("Toll", resolved.RoundLabel);
            Assert.AreEqual(EventFightLoss.Wake, resolved.Loss);
            Assert.IsFalse(resolved.Pays);
            Assert.AreEqual("Assets/_Project/Art/Events/bell_in_the_fog/fog_clearing.png", resolved.BackdropKey);
            Assert.AreEqual("Audio/Sfx/bell_toll", resolved.RoundSfxPath);
            Assert.AreEqual("Audio/Sfx/fog_wind", resolved.AmbiencePath);
            Assert.AreEqual("Assets/_Project/Art/Events/bell_in_the_fog/flock.png", resolved.RoundOverlayKey);
            Assert.AreEqual(0.5f, resolved.RoundOverlayFromScale);
            Assert.AreEqual(1.25f, resolved.RoundOverlayToScale);

            var defeated = resolved.OutcomeFor(EventFightResult.Defeated);
            Assert.IsTrue(defeated.IsLeave);
            CollectionAssert.AreEqual(
                new[] { EventEffectKind.Relic, EventEffectKind.Exp, EventEffectKind.Finish },
                defeated.Effects.Select(e => e.Kind).ToArray());
            Assert.AreEqual("sheep", defeated.Effects[1].CharacterId);
            Assert.AreEqual("Shawn", defeated.Effects[1].CharacterDisplayName);
            Assert.AreEqual(40, defeated.Effects[1].Amount);
            Assert.IsNotNull(resolved.OutcomeFor(EventFightResult.Survived));
            Assert.IsNotNull(resolved.OutcomeFor(EventFightResult.Fell));
        }

        [Test]
        public void TheLaunchingOutcome_HasNoGoToAndNamesItsFight()
        {
            var evt = Resolved(Event(BellFight()));
            var launch = evt.Pages[0].Choices[0].Outcomes[0];

            Assert.AreEqual("", launch.GoTo);
            Assert.IsFalse(launch.IsLeave);
            Assert.AreEqual("bellwether", launch.FightId);
            Assert.AreEqual(EventEffectKind.Fight, launch.Effects.Single().Kind);
        }

        [Test]
        public void AnEndRunFightWithNoLimit_HasOnlyOnDefeated()
        {
            var fight = new RawEventFight
            {
                id = "rat_pack", enemies = new[] { "rat", "rat", "rat" }, elite = true,
                onDefeated = Leave(Finish()),
            };

            var resolved = Resolved(Event(fight, Touch(StartFight("rat_pack")))).Fights.Single();

            Assert.AreEqual(EventFightLoss.EndRun, resolved.Loss, "empty onLoss is a room's own rule");
            Assert.IsTrue(resolved.Pays, "pays defaults to true");
            Assert.IsTrue(resolved.Elite);
            CollectionAssert.AreEqual(new string[0], resolved.PartyIds);
            Assert.IsNull(resolved.OutcomeFor(EventFightResult.Survived));
            Assert.IsNull(resolved.OutcomeFor(EventFightResult.Fell));
            Assert.AreEqual(1, resolved.Outcomes().Count());
        }

        [Test]
        public void AnEventWithoutMayReturn_ResolvesMayReturnFalse()
        {
            var entry = Event(null, WalkAway(), mayReturn: false);
            entry.pages[0].choices = new[] { WalkAway() };

            Assert.IsFalse(Resolved(entry).MayReturn);
        }

        [Test]
        public void TwoEnemiesOfOneAndOneOfTwoSlots_FitExactlyThree()
        {
            var fight = BellFight();
            fight.enemies = new[] { "rat", "treant" };

            CollectionAssert.AreEqual(new[] { "rat", "treant" }, Resolved(Event(fight)).Fights.Single().EnemyIds);
        }

        // ---- the refusals 3.5 lists -----------------------------------------

        [Test]
        public void Refuses_FinishOnANonReturningEvent()
        {
            var entry = Event(BellFight(), mayReturn: false);

            StringAssert.Contains("finish on an event without mayReturn", Refusal(entry));
        }

        [Test]
        public void Refuses_AFightEffectInAChoicesOwnEffects()
        {
            var choice = new RawEventChoice
            {
                text = "Touch the bell",
                effects = new[] { FightEffect("bellwether") },
                outcomes = new[] { Leave() },
            };

            StringAssert.Contains("a fight effect is in the choice's own effects", Refusal(Event(BellFight(), choice)));
        }

        [Test]
        public void Refuses_TwoFightEffectsInOneOutcome()
        {
            var outcome = new RawEventOutcome { effects = new[] { FightEffect("bellwether"), FightEffect("bellwether") } };

            StringAssert.Contains("an outcome starts 2 fights -- at most one per outcome",
                Refusal(Event(BellFight(), Touch(outcome))));
        }

        [Test]
        public void Refuses_AFightEffectBesideANonEmptyGoTo()
        {
            var outcome = StartFight();
            outcome.goTo = "bell";

            StringAssert.Contains("an outcome that starts a fight has goTo 'bell'", Refusal(Event(BellFight(), Touch(outcome))));
        }

        [Test]
        public void Refuses_AFightEffectBesideLeave()
        {
            var outcome = StartFight();
            outcome.goTo = "Leave";

            StringAssert.Contains("an outcome that starts a fight has goTo 'Leave'", Refusal(Event(BellFight(), Touch(outcome))));
        }

        [Test]
        public void Refuses_RequiresOnAFightResult()
        {
            var fight = BellFight();
            fight.onDefeated.requires = new[] { new RawEventRequirement { kind = "inParty", character = "sheep" } };

            StringAssert.Contains("fight 'bellwether' onDefeated: carries requires", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_OnFellOnAnEndRunFight()
        {
            var fight = BellFight();
            fight.onLoss = "endRun";

            StringAssert.Contains("fight 'bellwether' onFell: is authored, but onLoss is endRun", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AMissingOnSurvivedWithARoundLimit()
        {
            var fight = BellFight();
            fight.onSurvived = new RawEventOutcome();

            StringAssert.Contains("fight 'bellwether' onSurvived: is missing, and surviveRounds is above 0",
                Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_EnemiesOverTheStagesThreeSlotsBySlotSpan()
        {
            var fight = BellFight();
            fight.enemies = new[] { "rat", "rat", "treant" };

            StringAssert.Contains("enemies take 4 stage slots by slotSpan (rat, rat, treant), over the 3 the stage has",
                Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AnUnknownFightId()
        {
            StringAssert.Contains("fight effect names fight 'bellwhether', which is not in this event's fights",
                Refusal(Event(BellFight(), Touch(StartFight("bellwhether")))));
        }

        [Test]
        public void Refuses_AnUnknownEnemyId()
        {
            var fight = BellFight();
            fight.enemies = new[] { "belwether" };

            StringAssert.Contains("enemy 'belwether' is not an active enemy in enemies.json", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AnUnknownPartyCharacterId()
        {
            var fight = BellFight();
            fight.party = new[] { "goat" };

            StringAssert.Contains("party entry names character 'goat', which is not in characters.json", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AnUnknownExpCharacterId()
        {
            var fight = BellFight();
            fight.onSurvived = Leave(new RawEventEffect { kind = "exp", amount = 40, character = "goat" }, Finish());

            StringAssert.Contains("exp effect names character 'goat', which is not in characters.json", Refusal(Event(fight)));
        }

        // Without an enemy catalogue (the older overload), every fight names
        // an unknown enemy -- nothing slips through on a missing argument.
        [Test]
        public void Refuses_AFightWhenNoEnemyCatalogueIsGiven()
        {
            bool ok = EventEntryResolver.TryResolveAll(new List<RawEventEntry> { Event(BellFight()) }, KnownCharacters,
                KnownItems, KnownRelics, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("enemy 'bellwether' is not an active enemy in enemies.json", string.Join(" | ", errors));
        }

        // ---- the refusals this milestone adds around them -------------------

        [Test]
        public void Refuses_AMissingOnDefeated()
        {
            var fight = BellFight();
            fight.onDefeated = new RawEventOutcome();

            StringAssert.Contains("fight 'bellwether' onDefeated: is missing", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AMissingOnFellOnAWakeFight()
        {
            var fight = BellFight();
            fight.onFell = new RawEventOutcome();

            StringAssert.Contains("fight 'bellwether' onFell: is missing, and onLoss is wake", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_OnSurvivedWithNoRoundLimit()
        {
            var fight = BellFight();
            fight.surviveRounds = 0;
            fight.roundLabel = "";

            StringAssert.Contains("onSurvived: is authored, but the fight has no round limit", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_ARoundLabelWithNoRoundLimit()
        {
            var fight = BellFight();
            fight.surviveRounds = 0;
            fight.onSurvived = new RawEventOutcome();

            StringAssert.Contains("roundLabel is set on a fight with no round limit", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_ARoundLabelOverTheCap()
        {
            var fight = BellFight();
            fight.roundLabel = "Toll of the bell";

            StringAssert.Contains("roundLabel is 16 characters, over the 12-character cap", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_ANegativeSurviveRounds()
        {
            var fight = BellFight();
            fight.surviveRounds = -1;

            StringAssert.Contains("surviveRounds is -1", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AnUnknownOnLoss()
        {
            var fight = BellFight();
            fight.onLoss = "retreat";

            StringAssert.Contains("onLoss 'retreat' is not endRun or wake", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AFightWithNoEnemies()
        {
            var fight = BellFight();
            fight.enemies = new string[0];

            StringAssert.Contains("enemies is empty", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AFightNothingStarts()
        {
            StringAssert.Contains("fight 'bellwether': no outcome starts it", Refusal(Event(BellFight(), WalkAway())));
        }

        [Test]
        public void Refuses_ADuplicateFightId()
        {
            var entry = Event(BellFight());
            entry.fights = new[] { BellFight(), BellFight() };

            StringAssert.Contains("fight 'bellwether': duplicate fight id", Refusal(entry));
        }

        [Test]
        public void Refuses_AFightStartedFromAFightsOwnResult()
        {
            var fight = BellFight();
            fight.onDefeated = new RawEventOutcome { effects = new[] { FightEffect("bellwether") } };

            StringAssert.Contains("starts a fight from a fight's own result", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AnAmountOnAFightOrFinishEffect()
        {
            var fight = BellFight();
            fight.onFell = Leave(new RawEventEffect { kind = "finish", amount = 1 });

            StringAssert.Contains("finish effect takes no amount, was 1", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AFightFieldOnAnotherEffectKind()
        {
            var fight = BellFight();
            fight.onFell = Leave(new RawEventEffect { kind = "gold", amount = 5, fight = "bellwether" });

            StringAssert.Contains("fight is only read by a fight effect, not by gold", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_ARoundOverlayOutsideItsEventFolder()
        {
            var fight = BellFight();
            fight.roundOverlay = new EventRoundOverlay { path = "Assets/_Project/Art/Events/petting_zoo/flock.png" };

            StringAssert.Contains("roundOverlay.path 'Assets/_Project/Art/Events/petting_zoo/flock.png' must be filed as " +
                                  "Assets/_Project/Art/Events/bell_in_the_fog/<file>", Refusal(Event(fight)));
        }

        [Test]
        public void Refuses_AnAssetsRelativeRoundSfx()
        {
            var fight = BellFight();
            fight.roundSfx = "Assets/_Project/Resources/Audio/Sfx/bell_toll.wav";

            StringAssert.Contains("roundSfx 'Assets/_Project/Resources/Audio/Sfx/bell_toll.wav' must be RESOURCES-relative",
                Refusal(Event(fight)));
        }

        // A result that plays on the stage takes the line cap, fight results
        // included: the launching page has lines here.
        [Test]
        public void Refuses_AFightResultOverTheLineCapWhenTheLaunchingPageHasLines()
        {
            var entry = Event(BellFight());
            entry.pages[0].lines = new[] { new RawEventLine { speaker = "narration", text = "A bell." } };
            entry.fights[0].onSurvived.result = new string('a', 201);

            StringAssert.Contains("fight 'bellwether' onSurvived: result is 201 characters, over the 200-character cap",
                Refusal(entry));
        }
    }
}
