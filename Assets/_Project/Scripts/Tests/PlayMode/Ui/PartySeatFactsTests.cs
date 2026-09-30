using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE FACTS THAT DECIDE WHERE A CHARACTER STANDS (plan C4, C22): each seat
    // shows how hurt its occupant is and how well they take a blow, from the
    // context's own source -- full at camp, carried between fights in a descent,
    // and the HUD's painted values in a fight, never the run's carried figure.
    // The carry preview shows both characters' facts side by side.
    //
    // WHICH SOURCE WINS is PartyMemberFactsTests' (EditMode, literals); this
    // proves each place is actually read. The character's own maximum health
    // and defences are read from ContentDatabase.EffectiveStats, the same source
    // the sheet shows, because they are content and move with balance; every
    // figure a test SETS (a carried 100, a fight's 300) is a literal.
    public class PartySeatFactsTests
    {
        private const string Shawn = "sheep";
        private const string Odette = "owl";
        private const string Bjorn = "bear";

        private string _root;
        private SystemMenuController _menu;
        private FightController _fight;
        private FightBeatPlayer _beats;
        private CombatantState _bjorn;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-party-facts-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 0f;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            LogAssert.ignoreFailingMessages = true;
            if (_beats != null) _beats.EndFight();
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            SharedScene.MarkDirty("PartySeatFactsTests loads its own scenes and binds its own fight");
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- stand-ups -------------------------------------------------------------------

        private static void Seat(params string[] ids)
        {
            SaveSlotManager.CurrentSave.selectedCharacterIds = ids.ToList();
        }

        private IEnumerator OpenParty(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;

            OpenThePane();
            yield return null;
        }

        private void OpenThePane()
        {
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the scene has no SystemMenuController");
            _menu.Open();
            _menu.Select(SystemMenuTab.Party);
        }

        private static PartyController Party() =>
            Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);

        private GameObject NodeOf(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault(t => t.name == name)?.gameObject;

        private string TextOf(string name)
        {
            var go = NodeOf(name);
            Assert.IsNotNull(go, $"the Party pane has no '{name}'");
            return go.GetComponent<TMP_Text>().text;
        }

        private float FillWidth(int seat) =>
            ((RectTransform)NodeOf($"PartySeat{seat}HpFill").transform).sizeDelta.x;

        private static StatBlock StatsOf(string id) =>
            ContentDatabase.EffectiveStats(SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == id));

        private static string HpLine(int hp, int max) => $"HP {hp} / {max}";

        private static string DefenceLine(StatBlock stats) =>
            $"Phys def {stats.physicalDefense} · Mag def {stats.magicalDefense}";

        private static string BadgeDefenceLine(StatBlock stats) =>
            $"Phys {stats.physicalDefense} · Mag {stats.magicalDefense}";

        private static void CarryHealth(string id, int hp)
        {
            RunManager.Run.currentHealth.RemoveAll(e => e.characterId == id);
            RunManager.Run.currentHealth.Add(new RunHealthEntry { characterId = id, hp = hp });
        }

        // ---- camp: full ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AtCampEverySeatReadsFullHealthAndItsOccupantsDefences()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            Seat(Shawn, Odette, Bjorn);
            OpenThePane();
            yield return null;

            string[] ids = { Shawn, Odette, Bjorn };
            for (int seat = 0; seat < ids.Length; seat++)
            {
                var stats = StatsOf(ids[seat]);
                Assert.Greater(stats.maxHealth, 0, $"fixture: {ids[seat]} has no health");

                Assert.AreEqual(HpLine(stats.maxHealth, stats.maxHealth), TextOf($"PartySeat{seat}Hp"), $"seat {seat}");
                Assert.AreEqual(DefenceLine(stats), TextOf($"PartySeat{seat}Defences"), $"seat {seat}");
                Assert.AreEqual(412f, FillWidth(seat), 0.01f, $"seat {seat}'s bar is not full");
                Assert.IsTrue(NodeOf($"PartySeat{seat}Vitals").activeSelf, $"seat {seat}'s facts are hidden");
            }
        }

        [UnityTest]
        public IEnumerator AnEmptySeatShowsNoFacts()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            Seat(Shawn, Odette);
            OpenThePane();
            yield return null;

            Assert.IsTrue(NodeOf("PartySeat1Vitals").activeSelf);
            Assert.IsFalse(NodeOf("PartySeat2Vitals").activeSelf, "an empty seat still shows a bar and numbers");
        }

        // ---- descent: carried ------------------------------------------------------------

        [UnityTest]
        public IEnumerator InADescentASeatReadsWhatTheRunCarriesAndAnUnlistedCharacterIsFull()
        {
            RunManager.StartRun(4242UL);
            Seat(Shawn, Odette, Bjorn);
            CarryHealth(Shawn, 100);
            CarryHealth(Odette, 0);

            yield return OpenParty("Map");

            int shawnMax = StatsOf(Shawn).maxHealth;
            int odetteMax = StatsOf(Odette).maxHealth;
            int bjornMax = StatsOf(Bjorn).maxHealth;
            Assert.Greater(shawnMax, 100, "fixture: 100 must be short of Shawn's maximum to read as hurt");

            Assert.AreEqual(HpLine(100, shawnMax), TextOf("PartySeat0Hp"));
            Assert.AreEqual(HpLine(0, odetteMax), TextOf("PartySeat1Hp"), "a downed character reads zero");
            Assert.AreEqual(HpLine(bjornMax, bjornMax), TextOf("PartySeat2Hp"), "no entry is full health");

            Assert.Greater(FillWidth(0), 0f);
            Assert.Less(FillWidth(0), 412f, "a hurt character's bar is full");
            Assert.AreEqual(0f, FillWidth(1), 0.01f, "a downed character's bar is not empty");
            Assert.AreEqual(412f, FillWidth(2), 0.01f);
        }

        // ---- fight: from the source ---------------------------------------------------------

        private GameObject FightNamed(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator LoadTheFight()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightBeatPlayer.AdoptPlayerSpeed();
            _beats.Flush();
            yield return null;
            yield return null;

            var shawn = new CombatantState("Shawn", true, 300, 30, 12, 10);
            var odette = new CombatantState("Odette", true, 250, 30, 10, 9);
            _bjorn = new CombatantState("Bjorn", true, 200, 30, 10, 8);
            var foes = new[]
            {
                new CombatantState("Front", false, 50000, 10, 6, 4),
                new CombatantState("Back", false, 50000, 10, 6, 3),
            };

            var kits = new List<PlayerKit> { Kit(Shawn), Kit(Odette), Kit(Bjorn) };
            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name, new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();

            var session = new FightSession(new CombatEncounter(new[] { shawn, odette, _bjorn }, foes),
                kits, enemyKits, new SeededRandom(20260930));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private static PlayerKit Kit(string id) =>
            new PlayerKit(id, CharacterRole.Tank, new List<ResolvedSkill> { PlayModeSparkFixture.Skill() }, null, null, level: 4);

        [UnityTest]
        public IEnumerator InAFightASeatReadsTheFightsHealthNotTheRunsCarriedFigure()
        {
            RunManager.StartRun(4242UL);
            Seat(Shawn, Odette, Bjorn);
            CarryHealth(Shawn, 77);

            yield return LoadTheFight();
            OpenThePane();
            yield return null;

            Assert.AreEqual(MenuContext.Fight, _menu.Context);
            Assert.AreEqual(HpLine(300, 300), TextOf("PartySeat0Hp"), "Shawn: the fight's 300, not the carried 77");
            Assert.AreEqual(HpLine(250, 250), TextOf("PartySeat1Hp"));
            Assert.AreEqual(HpLine(200, 200), TextOf("PartySeat2Hp"));
            Assert.AreEqual(412f, FillWidth(0), 0.01f, "a full fight bar");
        }

        // A blow the session has recorded and no beat has painted is not the
        // pane's to show: it reads what the card reads.
        [UnityTest]
        public IEnumerator InAFightASeatReadsWhatTheHudHasPaintedNotTheRoundsEnd()
        {
            RunManager.StartRun(4242UL);
            Seat(Shawn, Odette, Bjorn);

            yield return LoadTheFight();

            FightNamed("Verb0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            FightNamed("EnemyPlate0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsTrue(_fight.IsBusy, "fixture: the swing should still be playing, or this proves nothing");
            _bjorn.CurrentHealth = 123;
            yield return null;

            OpenThePane();
            yield return null;

            Assert.AreEqual(HpLine(200, 200), TextOf("PartySeat2Hp"),
                "Bjorn has not been seen to take the blow: the seat read the session's lead");

            _menu.Close();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_fight.IsBusy, "playback never finished after the menu closed");
        }

        // ---- the roster is shown only when it has something to say -----------------------------

        [UnityTest]
        public IEnumerator TheRosterIsHiddenWhenEveryoneIsSeatedAndShownWhenSomeoneSitsOut()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;

            Seat(Shawn, Odette, Bjorn);
            OpenThePane();
            yield return null;
            Assert.IsFalse(NodeOf("PartyRosterSection").activeSelf, "everyone is seated and the roster still shows");

            _menu.Close();
            yield return null;

            Seat(Shawn, Odette);
            OpenThePane();
            yield return null;
            Assert.IsTrue(NodeOf("PartyRosterSection").activeSelf, "Bjorn sits out and the roster is hidden");
        }

        [UnityTest]
        public IEnumerator PlacingTheLastUnseatedCharacterHidesTheRosterAndKeepsFocusOnAControl()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            Seat(Shawn, Odette);
            OpenThePane();
            yield return null;

            var party = Party();
            party.ClickCard(Bjorn);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            Assert.IsFalse(NodeOf("PartyRosterSection").activeSelf, "the last unseated character was placed");
            var focused = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.IsTrue(focused == null || focused.activeInHierarchy, "focus was left on a hidden control");
        }

        // ---- the carry preview: both characters' facts side by side --------------------------------

        [UnityTest]
        public IEnumerator CarryingASeatPreviewsTheSwapWithBothCharactersFacts()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            Seat(Shawn, Odette, Bjorn);
            OpenThePane();
            yield return null;

            Party().ClickSeat(PartySeat.Front);
            yield return null;

            var shawn = StatsOf(Shawn);
            var odette = StatsOf(Odette);

            Assert.IsFalse(NodeOf("PartySeat0Badge").activeSelf, "the seat being carried previews a swap with itself");
            Assert.IsTrue(NodeOf("PartySeat1Badge").activeSelf);
            Assert.AreEqual("Swap with Odette", TextOf("PartySeat1BadgeText"));

            Assert.IsTrue(NodeOf("PartySeat1BadgeCarried").activeSelf);
            Assert.AreEqual("Shawn", TextOf("PartySeat1BadgeCarriedName"));
            Assert.AreEqual(HpLine(shawn.maxHealth, shawn.maxHealth), TextOf("PartySeat1BadgeCarriedHp"));
            Assert.AreEqual(BadgeDefenceLine(shawn), TextOf("PartySeat1BadgeCarriedDefences"));

            Assert.IsTrue(NodeOf("PartySeat1BadgeSeated").activeSelf);
            Assert.AreEqual("Odette", TextOf("PartySeat1BadgeSeatedName"));
            Assert.AreEqual(HpLine(odette.maxHealth, odette.maxHealth), TextOf("PartySeat1BadgeSeatedHp"));
            Assert.AreEqual(BadgeDefenceLine(odette), TextOf("PartySeat1BadgeSeatedDefences"));
        }

        [UnityTest]
        public IEnumerator CarryingARosterCardPreviewsAPlacementOnAnEmptySeatAndAReplaceOnAFullOne()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            Seat(Shawn, Odette);
            OpenThePane();
            yield return null;

            Party().ClickCard(Bjorn);
            yield return null;

            var bjorn = StatsOf(Bjorn);

            Assert.AreEqual("Place Bjorn here", TextOf("PartySeat2BadgeText"));
            Assert.IsTrue(NodeOf("PartySeat2BadgeCarried").activeSelf);
            Assert.AreEqual("Bjorn", TextOf("PartySeat2BadgeCarriedName"));
            Assert.AreEqual(HpLine(bjorn.maxHealth, bjorn.maxHealth), TextOf("PartySeat2BadgeCarriedHp"));
            Assert.IsFalse(NodeOf("PartySeat2BadgeSeated").activeSelf, "an empty seat has no one to compare against");

            Assert.AreEqual("Replace Shawn", TextOf("PartySeat0BadgeText"));
            Assert.IsTrue(NodeOf("PartySeat0BadgeSeated").activeSelf);
            Assert.AreEqual("Shawn", TextOf("PartySeat0BadgeSeatedName"));
        }

        [UnityTest]
        public IEnumerator InADescentThePreviewCarriesEachCharactersCarriedHealth()
        {
            RunManager.StartRun(4242UL);
            Seat(Shawn, Odette, Bjorn);
            CarryHealth(Shawn, 100);
            CarryHealth(Odette, 50);

            yield return OpenParty("Map");

            Party().ClickSeat(PartySeat.Front);
            yield return null;

            Assert.AreEqual("Swap with Odette", TextOf("PartySeat1BadgeText"));
            Assert.AreEqual(HpLine(100, StatsOf(Shawn).maxHealth), TextOf("PartySeat1BadgeCarriedHp"),
                "the carried character's health is what the run carries");
            Assert.AreEqual(HpLine(50, StatsOf(Odette).maxHealth), TextOf("PartySeat1BadgeSeatedHp"));
        }
    }
}
