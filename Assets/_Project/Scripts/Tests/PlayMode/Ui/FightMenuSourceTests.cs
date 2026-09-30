using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // IN A FIGHT THE MENU SHOWS THE FIGHT (plan C4): the health on the rail and
    // the stats in the derived column come from IFightMenuSource, which reports
    // what the HUD has PAINTED -- never RunSnapshot.currentHealth (written when
    // the fight ends) and never the session's lead (a round is recorded before
    // its beats play).
    //
    // A hand-built session over the real party ids (Shawn, Odette, Bjorn) in
    // the real Fight scene, driven with the real buttons: the FightHud-
    // SnapshotLifecycleTests arrangement, whose own note explains why the
    // enemies must be able to hurt the party and the pace must be the authored
    // one -- a single-frame round has no window between "recorded" and
    // "painted" to look through.
    public class FightMenuSourceTests
    {
        private const string Shawn = "sheep";
        private const string Odette = "owl";
        private const string Bjorn = "bear";

        private string _root;
        private FightController _fight;
        private FightBeatPlayer _beats;
        private CombatantState _shawn;
        private CombatantState _odette;
        private CombatantState _bjorn;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-fight-source-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };

            // AUTHORED PACE: the tests below sample playback frame by frame.
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

            SharedScene.MarkDirty("FightMenuSourceTests binds its own session");
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- the stand-up -------------------------------------------------------------

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the Fight scene");
            go.GetComponent<Button>().onClick.Invoke();
        }

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

            // Shawn first, fast enough to act before the foes do, and foes that
            // outlast the test: the blow the tests below land is written into
            // the session by hand (see BleedBjorn), so nothing here depends on
            // the foes' turn order.
            _shawn = new CombatantState("Shawn", true, 300, 30, 12, 10);
            _odette = new CombatantState("Odette", true, 250, 30, 10, 9);
            _bjorn = new CombatantState("Bjorn", true, 200, 30, 10, 8);
            var foes = new[]
            {
                new CombatantState("Front", false, 50000, 10, 6, 4),
                new CombatantState("Back", false, 50000, 10, 6, 3),
            };

            var kits = new List<PlayerKit>
            {
                Kit(Shawn), Kit(Odette), Kit(Bjorn),
            };
            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name, new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();

            var session = new FightSession(new CombatEncounter(new[] { _shawn, _odette, _bjorn }, foes),
                kits, enemyKits, new SeededRandom(20260930));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private static PlayerKit Kit(string id) =>
            new PlayerKit(id, CharacterRole.Tank, new List<ResolvedSkill> { PlayModeSparkFixture.Skill() }, null, null, level: 4);

        private IFightMenuSource Source => _fight;

        private SystemMenuController Menu() => DossierPane.Menu();

        private CombatantState LiveOf(string id) => id == Shawn ? _shawn : id == Odette ? _odette : _bjorn;

        // "180/240" on a plate's caption: the same format the HUD prints.
        private (int health, int max) PaintedOnPlate(int plate)
        {
            string label = Named($"PcPlate{plate}HpValue").GetComponent<TMP_Text>().text;
            var parts = label.Split('/');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        // A SWING, and a blow the session records that no beat has painted:
        // the round is resolved before it plays, so by the time Shawn's swing
        // is on screen the session can already hold what happens after it.
        // Written by hand, as FightHudSnapshotLifecycleTests writes a moved
        // maximum -- the site under test is the READER, which cannot tell the
        // two apart, and it keeps the test independent of the foes' turn order.
        private const int BledTo = 123;

        private void SwingAndBleedBjorn()
        {
            Click("Verb0");
            Click("EnemyPlate0");
            Assert.IsTrue(_fight.IsBusy, "fixture: the swing should still be playing, or this proves nothing");

            _bjorn.CurrentHealth = BledTo;
        }

        // The first party member whose live health differs from what is on
        // their card, or null.
        private FightMemberFacts SessionIsAhead()
        {
            foreach (var facts in Source.Members)
            {
                if (LiveOf(facts.CharacterId).CurrentHealth != facts.Health) return facts;
            }

            return null;
        }

        // ---- the registration -----------------------------------------------------------

        [UnityTest]
        public IEnumerator BindingAFightRegistersItAsTheMenusSource()
        {
            yield return LoadTheFight();

            Assert.AreSame(_fight, Menu().FightSource, "the scene's menu was never told where the fight's values live");
            Assert.AreEqual(MenuContext.Fight, Menu().Context);
        }

        // ---- health --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSourceReportsExactlyWhatEveryCardShowsThroughoutAPlayback()
        {
            yield return LoadTheFight();

            Assert.AreEqual(3, Source.Members.Count, "one entry per party plate");
            CollectionAssert.AreEqual(new[] { Shawn, Odette, Bjorn }, Source.Members.Select(m => m.CharacterId).ToArray(),
                "the members come in the column's order");

            SwingAndBleedBjorn();

            int samples = 0;
            bool everAhead = false;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                var members = Source.Members;
                for (int plate = 0; plate < members.Count; plate++)
                {
                    var painted = PaintedOnPlate(plate);
                    Assert.AreEqual(painted.health, members[plate].Health,
                        $"plate {plate}: the source said something other than the number on the card");
                    Assert.AreEqual(painted.max, members[plate].MaxHealth, $"plate {plate}: the source's maximum");
                    if (LiveOf(members[plate].CharacterId).CurrentHealth != members[plate].Health) everAhead = true;
                }

                samples++;
                yield return null;
            }

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Greater(samples, 1, "fixture: playback was never sampled mid-flight");
            Assert.IsTrue(everAhead,
                "the session was never ahead of the HUD, so this test could not tell a painted number from a live one");

            foreach (var facts in Source.Members)
            {
                Assert.AreEqual(LiveOf(facts.CharacterId).CurrentHealth, facts.Health,
                    $"{facts.CharacterId}: once the playback is over the card, and so the source, are live again");
            }
        }

        // THE MENU OPENED MID-ROUND reads the card, not the session: the round
        // is resolved and the party already hurt, and the player has not yet
        // watched the blow land.
        [UnityTest]
        public IEnumerator TheRailShowsTheHealthTheHudHasPaintedNotTheRoundsEnd()
        {
            yield return LoadTheFight();
            SwingAndBleedBjorn();
            yield return null;

            var ahead = SessionIsAhead();
            Assert.IsNotNull(ahead, "fixture: nothing is ahead of the HUD");
            Assert.AreEqual(Bjorn, ahead.CharacterId, "fixture: the blow was written to Bjorn");
            int live = LiveOf(ahead.CharacterId).CurrentHealth;
            Assert.AreEqual(BledTo, live);

            var menu = Menu();
            menu.Open(SystemMenuTab.Character);
            yield return null;

            var dossier = DossierPane.Dossier();
            Assert.AreEqual(MenuContext.Fight, dossier.Context);
            Assert.IsTrue(dossier.SelectCharacter(ahead.CharacterId), "fixture: the member is not on the strip");
            yield return null;

            string level = DossierPane.TextOf("DossierLevel");
            Assert.AreEqual($"Level {DossierPane.Shown().level} · 200/200 HP", level,
                "the rail did not say what the card says (Bjorn has not been seen to take the blow)");
            StringAssert.DoesNotContain($" {live}/", level, "the rail read the session's lead instead of the card");

            menu.Close();
            yield return null;

            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_fight.IsBusy, "playback never finished after the menu closed");
        }

        [UnityTest]
        public IEnumerator TheRailShowsTheRunsCarriedHealthNowhereInAFight()
        {
            yield return LoadTheFight();

            var menu = Menu();
            menu.Open(SystemMenuTab.Character);
            yield return null;
            var dossier = DossierPane.Dossier();
            Assert.IsTrue(dossier.SelectCharacter(Shawn), "fixture: Shawn is not on the strip");
            yield return null;

            Assert.AreEqual($"Level {DossierPane.Shown().level} · 300/300 HP", DossierPane.TextOf("DossierLevel"),
                "the rail's line is the level, then the fight's health over the fight's maximum");
        }

        // ---- temporary modifiers ------------------------------------------------------------

        [UnityTest]
        public IEnumerator ASpeedMalusReadsBaseThenNowThenItsSourceOnceTheHudHasPaintedIt()
        {
            yield return LoadTheFight();

            // 30% of Speed 10 is 3.
            _fight.Session.ApplyChilledForTest(_shawn, 30, 2);

            var before = Source.Members.First(m => m.CharacterId == Shawn);
            Assert.IsFalse(before.Modifies(SheetStat.Speed),
                "the menu named a change the HUD has not painted yet: the badge row repaints on RefreshUi");

            _fight.RefreshUi();

            var facts = Source.Members.First(m => m.CharacterId == Shawn);
            Assert.AreEqual(-3, facts.DeltaFor(SheetStat.Speed), "30% of Speed 10");
            Assert.AreEqual("Chilled", facts.SourcesFor(SheetStat.Speed));

            var menu = Menu();
            menu.Open(SystemMenuTab.Character);
            yield return null;
            var dossier = DossierPane.Dossier();
            Assert.IsTrue(dossier.SelectCharacter(Shawn), "fixture: Shawn is not on the strip");
            dossier.ShowSection(CharacterSection.Attributes, focusRow: false);
            yield return null;

            int sheet = ContentDatabase.EffectiveStats(DossierPane.Shown()).speed;
            Assert.GreaterOrEqual(sheet, 3, "fixture: the sheet's speed is too low to lose three");

            int speedRow = SheetStats.Derived.ToList().IndexOf(SheetStat.Speed);
            Assert.AreEqual($"{sheet} → {sheet - 3} (Chilled)", DossierPane.TextOf($"DossierStatValue{speedRow}"),
                "the derived column did not read sheet, then now, then who did it");

            // A stat the fight has not touched reads as the sheet does.
            int healthRow = SheetStats.Derived.ToList().IndexOf(SheetStat.MaxHealth);
            Assert.AreEqual(ContentDatabase.EffectiveStats(DossierPane.Shown()).maxHealth.ToString(),
                DossierPane.TextOf($"DossierStatValue{healthRow}"));
        }

        [UnityTest]
        public IEnumerator TheLoadoutCompareColumnCarriesTheSameEffectiveStats()
        {
            yield return LoadTheFight();
            _fight.Session.ApplyChilledForTest(_shawn, 30, 2);
            _fight.RefreshUi();

            var menu = Menu();
            menu.Open(SystemMenuTab.Character);
            yield return null;
            var dossier = DossierPane.Dossier();
            Assert.IsTrue(dossier.SelectCharacter(Shawn), "fixture: Shawn is not on the strip");
            dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            dossier.Refresh();
            yield return null;

            string speed = UiStrings.StatSpeed.Format();
            int row = Enumerable.Range(0, SheetStats.Derived.Length)
                .First(i => DossierPane.TextOf($"DossierCompareStat{i}Name") == speed);
            StringAssert.EndsWith("(Chilled)", DossierPane.TextOf($"DossierCompareStat{row}Value"));
        }
    }
}
