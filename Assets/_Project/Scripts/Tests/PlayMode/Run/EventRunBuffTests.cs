using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // THE P1 EVENT ROWS THROUGH THE REAL RUN (docs/PLAN_PETTING_ZOO.md):
    // one-member heals, `inParty alive`, the relic grant, and the two run
    // buffs, applied by RunOrchestrator.ChooseEventOption against a real save.
    //
    // FIXTURE EVENTS, not the Zoo: the Zoo is authored in P4. Each fixture is
    // resolved by EventEntryResolver and appended to ContentDatabase.Events
    // for the test's lifetime (TearDown reloads the catalogue). Their floors
    // are 999, so the random roll never picks one; OpenEventForDebug opens
    // them where the party stands, which is the real path after arrival.
    public class EventRunBuffTests
    {
        private const string GatedEvent = "p1_fixture_gated";
        private const string UngatedEvent = "p1_fixture_ungated";

        // Choice indices on the gated fixture's one page.
        private const int Pet = 0;
        private const int Feed = 1;
        private const int ColdOne = 2;
        private const int TakeRelic = 3;

        private string _root;
        private string _relicId;

        [SetUp]
        public void UseAThrowawaySaveRootAndFixtureEvents()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-buffs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();

            _relicId = ContentDatabase.Relics.First(r => r != null && r.Data != null).Data.Id;
            AddFixtureEvents();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            ContentDatabase.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- fixtures -------------------------------------------------------------------

        private static RawEventChoice Loop(string text, RawEventEffect effect, params RawEventRequirement[] requires) =>
            new RawEventChoice
            {
                text = text,
                requires = requires,
                effects = new[] { effect },
                outcomes = new[] { new RawEventOutcome { result = "Done.", goTo = "zoo" } },
            };

        private static RawEventEntry Fixture(string id, params RawEventChoice[] choices) =>
            new RawEventEntry
            {
                id = id,
                floors = new[] { 999 },
                pages = new[] { new RawEventPage { id = "zoo", title = "Zoo", body = "B", choices = choices } },
            };

        private void AddFixtureEvents()
        {
            var raws = new List<RawEventEntry>
            {
                Fixture(GatedEvent,
                    Loop("Pet the sheep", new RawEventEffect { kind = "healPercent", amount = 100, character = "sheep" },
                        new RawEventRequirement { kind = "inParty", character = "sheep", alive = true }),
                    Loop("Feed the fawns", new RawEventEffect { kind = "princesFavor", amount = 10 }),
                    Loop("Crack open a cold one", new RawEventEffect { kind = "fillSpecialPool", amount = 1 }),
                    Loop("Take the relic", new RawEventEffect { kind = "relic", relic = _relicId })),
                Fixture(UngatedEvent,
                    Loop("Pet the sheep", new RawEventEffect { kind = "healPercent", amount = 100, character = "sheep" }),
                    Loop("Look around", new RawEventEffect { kind = "princesFavor", amount = 1 })),
            };

            var characters = new Dictionary<string, string> { ["sheep"] = "Shawn", ["owl"] = "Odette", ["bear"] = "Bjorn" };
            var relics = new Dictionary<string, string> { [_relicId] = "Fixture Relic" };

            bool ok = EventEntryResolver.TryResolveAll(raws, characters, new string[0], relics,
                out var resolved, out var errors);
            Assert.IsTrue(ok, "fixture: " + string.Join("; ", errors ?? new List<string>()));

            // Events is the loaded List itself; the fixture rides on it until
            // TearDown's Reset reloads from Resources.
            var events = (List<EventDefinition>)ContentDatabase.Events;
            var dataField = typeof(EventDefinition).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var definition in resolved)
            {
                var asset = ScriptableObject.CreateInstance<EventDefinition>();
                dataField.SetValue(asset, definition);
                events.Add(asset);
            }
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;
        private static RunSnapshot Run => RunManager.Run;

        private static void StartRunAndOpen(string eventId, ulong seed = 11UL)
        {
            RunManager.StartRun(seed);
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(eventId), $"fixture: {eventId} did not open");
        }

        private static void Reload()
        {
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        private static void SetHp(string characterId, int hp)
        {
            Run.currentHealth.RemoveAll(e => e != null && e.characterId == characterId);
            Run.currentHealth.Add(new RunHealthEntry { characterId = characterId, hp = hp });
        }

        private static int Hp(string characterId) => Run.currentHealth.First(e => e.characterId == characterId).hp;

        private static int MaxHp(string characterId) =>
            ContentDatabase.EffectiveStats(Save.roster.First(c => c.definitionId == characterId)).maxHealth;

        private static EventChoiceView Shown(int index) =>
            RunOrchestrator.CurrentEvent().Choices.First(c => c.Index == index);

        // ---- one-member heal ------------------------------------------------------------

        [Test]
        public void AOneMemberHealTouchesOnlyThatMember()
        {
            StartRunAndOpen(GatedEvent);
            CollectionAssert.AreEquivalent(new[] { "sheep", "owl", "bear" }, Save.ActiveSquadIds(),
                "fixture: a new save fields the three starters");
            SetHp("sheep", 10);
            SetHp("owl", 10);
            SetHp("bear", 0);

            var result = EventPicks.OnCurrentPage(Pet);

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual(MaxHp("sheep"), Hp("sheep"));
            Assert.AreEqual(10, Hp("owl"));
            Assert.AreEqual(0, Hp("bear"));
            Assert.AreEqual("Shawn fully healed", result.EffectsLine);
        }

        [Test]
        public void AOneMemberHealLeavesADownedMemberAtZero_AndSaysNothing()
        {
            StartRunAndOpen(UngatedEvent);
            SetHp("sheep", 0);

            var result = EventPicks.OnCurrentPage(0);

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual(0, Hp("sheep"));
            Assert.AreEqual("", result.EffectsLine, "no heal happened, so the line must not claim one");
        }

        // ---- inParty alive --------------------------------------------------------------

        [Test]
        public void AliveIsOpenForAStandingMember()
        {
            StartRunAndOpen(GatedEvent);
            SetHp("sheep", 1);

            Assert.IsTrue(Shown(Pet).Enabled);
        }

        [Test]
        public void AliveLocksForADownedMember()
        {
            StartRunAndOpen(GatedEvent);
            SetHp("sheep", 0);

            var shown = Shown(Pet);
            Assert.IsFalse(shown.Enabled);
            Assert.AreEqual("Requires Shawn standing", shown.LockReason);
            Assert.AreEqual(EventRefusal.Locked, EventPicks.OnCurrentPage(Pet).Reason);
            Assert.AreEqual(0, Hp("sheep"));
        }

        [Test]
        public void AliveLocksForABenchedMember()
        {
            StartRunAndOpen(GatedEvent);
            Save.selectedCharacterIds = new List<string> { "owl", "bear" };

            var shown = Shown(Pet);
            Assert.IsFalse(shown.Enabled);
            Assert.AreEqual("Requires Shawn standing", shown.LockReason);
        }

        // ---- relic ----------------------------------------------------------------------

        [Test]
        public void TheRelicGrantIsIdempotent()
        {
            StartRunAndOpen(GatedEvent);
            Run.relicIds.Clear();

            var first = EventPicks.OnCurrentPage(TakeRelic);
            var second = EventPicks.OnCurrentPage(TakeRelic);

            Assert.AreEqual(1, Run.relicIds.Count(id => id == _relicId));
            // A relic is shown as a chip, never as effects text, and only
            // what was applied becomes a receipt (C2, C3).
            Assert.AreEqual("", first.EffectsLine);
            Assert.AreEqual("", second.EffectsLine);
            CollectionAssert.AreEqual(new[] { _relicId }, Run.pendingRelicReceipts,
                "an already-held relic is not granted again, so it is not received again");
        }

        // ---- Prince's favor (run scope) -------------------------------------------------

        [Test]
        public void AFavorBuffAddsToTheSquadMax_SurvivesALegAReloadAndASwap_AndEndsWithTheRun()
        {
            StartRunAndOpen(GatedEvent);
            Assert.AreEqual(4, ItemOfferRoll.CurrentSquadFavor(), "fixture: Shawn's authored 4 is the squad max");

            var result = EventPicks.OnCurrentPage(Feed);
            Assert.AreEqual("+10 Prince's favor", result.EffectsLine);
            Assert.AreEqual(14, ItemOfferRoll.CurrentSquadFavor());

            RunManager.AdvanceLeg();
            Assert.AreEqual(14, ItemOfferRoll.CurrentSquadFavor(), "a run buff outlives its leg");

            Reload();
            Assert.AreEqual(14, ItemOfferRoll.CurrentSquadFavor(), "a run buff survives a reload");

            Save.selectedCharacterIds = new List<string> { "owl", "bear" };
            Assert.AreEqual(10, ItemOfferRoll.CurrentSquadFavor(), "the buff belongs to the run, not to Shawn");

            Save.selectedCharacterIds = new List<string> { "sheep", "owl", "bear" };
            RunManager.StartRun(12UL);
            Assert.AreEqual(4, ItemOfferRoll.CurrentSquadFavor(), "a new run starts without it");
            CollectionAssert.IsEmpty(Run.eventBuffs);
        }

        // A constant roll of 250 sits between the tier ladder's step threshold
        // at favor 4 (0.22 + 4 x 0.006 = 0.244, threshold 244) and at favor 24
        // (0.22 + 24 x 0.006 = 0.364, threshold 364): it fails the first rung
        // before the grant and clears all five after it. Two feeds take 4 to 24.
        [Test]
        public void AnActualTierRollReadsTheBoostedFavor()
        {
            StartRunAndOpen(GatedEvent);
            var source = Content.ContentDatabase.GetRewardSource("normal_gear");
            Assert.IsNotNull(source, "fixture: the normal gear row is built");

            int before = Domain.Rewards.RarityTable.RollTier(source, 0, ItemOfferRoll.CurrentSquadFavor(), _ => 250);
            EventPicks.OnCurrentPage(Feed);
            EventPicks.OnCurrentPage(Feed);
            int after = Domain.Rewards.RarityTable.RollTier(source, 0, ItemOfferRoll.CurrentSquadFavor(), _ => 250);

            Assert.AreEqual(24, ItemOfferRoll.CurrentSquadFavor());
            Assert.AreEqual(0, before);
            Assert.AreEqual(5, after);
        }

        private static string Shelf(IEnumerable<ShopStockEntry> stock) =>
            string.Join("|", stock.Where(e => e.section == ShopStock.GearSection)
                .Select(e => $"{e.contentId}@{e.tier}/{string.Join(",", e.modifiers)}"));

        [Test]
        public void TheShopShelfDoesNotReadFavorYet_AndAnExistingShelfIsNotRerolled()
        {
            StartRunAndOpen(GatedEvent);
            RunOrchestrator.EnsureShopStock();
            var unboosted = Run.shopStock.ToList();
            Assert.IsTrue(unboosted.Any(e => e.section == ShopStock.GearSection), "fixture: the shop rolled a gear shelf");

            // Control: rolled fresh at the same position with nothing changed,
            // the shelf is identical -- so any difference below is favor's.
            Run.shopStock = new List<ShopStockEntry>();
            RunOrchestrator.EnsureShopStock();
            Assert.AreEqual(Shelf(unboosted), Shelf(Run.shopStock), "fixture: a shelf is a pure function of its position");

            // A large grant: it moves the fight reward's tier ladder (above),
            // and the shop's shelf has no favor input until the page roll.
            for (int i = 0; i < 50; i++) EventPicks.OnCurrentPage(Feed);
            Assert.AreEqual(504, ItemOfferRoll.CurrentSquadFavor());

            // Existing stock stays exactly as it was rolled.
            RunOrchestrator.EnsureShopStock();
            Assert.AreEqual(Shelf(unboosted), Shelf(Run.shopStock));

            // The same node and reroll count, rolled fresh: favor is not an
            // input to the shelf, so it is the same shelf.
            Run.shopStock = new List<ShopStockEntry>();
            RunOrchestrator.EnsureShopStock();
            Assert.AreEqual(Shelf(unboosted), Shelf(Run.shopStock));
        }

        // ---- fillSpecialPool (leg scope) ------------------------------------------------

        [Test]
        public void ALegBuffIsActiveThisLeg_SurvivesAReload_AndStopsAtAdvanceLeg()
        {
            StartRunAndOpen(GatedEvent);

            var result = EventPicks.OnCurrentPage(ColdOne);
            Assert.AreEqual("Special pools full each turn this leg", result.EffectsLine);
            Assert.AreEqual(0, Run.eventBuffs.Single().legStartStep);
            Assert.IsTrue(EventBuffs.AnyActive(Run.eventBuffs, EventBuffs.FillSpecialPool, Run.legStartStep));

            Reload();
            Assert.IsTrue(EventBuffs.AnyActive(Run.eventBuffs, EventBuffs.FillSpecialPool, Run.legStartStep),
                "a leg buff survives a reload within its leg");

            RunManager.AdvanceLeg();
            Assert.IsFalse(EventBuffs.AnyActive(Run.eventBuffs, EventBuffs.FillSpecialPool, Run.legStartStep));
            Assert.AreEqual(4, ItemOfferRoll.CurrentSquadFavor(), "a leg buff adds no favor");
        }

        // ---- load-time prune ------------------------------------------------------------

        [Test]
        public void UnknownBuffKindsAndNullRowsArePrunedOnLoad()
        {
            RunManager.StartRun(11UL);
            Run.eventBuffs = new List<EventBuffEntry>
            {
                new EventBuffEntry { kind = "princesFavor", amount = 10, legStartStep = -1 },
                new EventBuffEntry { kind = "doubleGold", amount = 2, legStartStep = -1 },
                null,
                new EventBuffEntry { kind = "fillSpecialPool", amount = 1, legStartStep = 0 },
            };
            SaveSlotManager.SaveCurrent();

            Reload();

            CollectionAssert.AreEqual(new[] { "princesFavor", "fillSpecialPool" },
                Run.eventBuffs.Select(b => b?.kind).ToArray());
        }
    }
}
