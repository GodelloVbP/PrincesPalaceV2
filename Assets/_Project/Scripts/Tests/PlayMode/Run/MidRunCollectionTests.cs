using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE THIRD MOMENT, END TO END: earned, collected, active.
    //
    // docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §6 names three and
    // the gap between the last two is the one nothing could see: "active (from
    // the next fight; the kit is built once per fight)". Everything about that
    // sentence is provable only by playing two fights, and until now nothing
    // did -- RewardTrackClaimTests collects through the panel but against a
    // hand-set save with no encounter either side of it.
    //
    // WHAT THIS ACTUALLY DRIVES. A real run, a real room, the real verbs until
    // the enemy is down, the real settlement (FightBootstrap.OnFightEnded is
    // what pays the experience at all), the real system menu opened inside the
    // Fight scene, the real node pressed, and then a second real room. Every
    // link in that chain has been wrong at some point and none of them is
    // reachable by asserting against a session built by hand.
    //
    // THE ONE DIAL IS EXPERIENCE, NOT LEVEL. FightSettlementTests steers its
    // outcomes by levelling a squad to 90, and that is exactly what this test
    // cannot do: the level IS the subject. So Shawn walks in at level 1 with
    // 159 experience banked -- one short of the 160 that levels 2 and 3 cost
    // together -- and his power comes from ability scores granted directly,
    // which is a debug grant the game itself has (the dossier spends points
    // the same way) and which moves no watermark. Any positive payout then
    // crosses 160 exactly once: the cheapest room-0 fight pays about 20 and
    // level 4 needs another 150, so there is no draw that skips past 3.
    //
    // SOLO, for FightSettlementTests' own stated reason -- three attackers
    // make "will this party win" a different question -- and because the
    // subject is one character's track.
    public class MidRunCollectionTests
    {
        // FightAfterTheEliteTests' seed: a run known to generate a leg with
        // reachable plain fight rooms.
        private const ulong Seed = 639228196442867409UL;

        // Shawn's level-3 node on the shipped track (reward_tracks.json,
        // characterId "sheep"). Named rather than looked up, because the
        // WHOLE POINT is that this particular skill is what level 3 pays --
        // a test that read the id off the track would pass if the node were
        // reauthored to grant something else, which is the change most worth
        // noticing.
        private const string Woolgathering = "woolgathering";
        private const int AbilityLevel = 3;

        // Levels 2 and 3 together, from §3's cost table (75 + 85).
        private const int CostOfLevelThree = 160;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-midrun-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator ASkillCollectedMidFightArrivesInTheNextFightAndNotThisOne()
        {
            RunManager.StartRun(Seed);

            var shawn = SaveSlotManager.CurrentSave.ActiveSquad().First();
            Assert.AreEqual("sheep", shawn.definitionId,
                "fixture: the solo squad is no longer Shawn, so the level-3 node is not Woolgathering");

            OneShortOfLevelThree(shawn);
            GrantFightingPower(shawn);

            Assert.IsFalse(RewardTracks.For(shawn).HasUnlocked(TrackReward.UnlockSkill, shawn.claimedTrackLevel),
                "fixture: the track has already paid an ability, so nothing below proves anything");

            // ---- fight one: the level is EARNED ----------------------------

            FightController fight = null;
            yield return EnterAFightRoom(f => fight = f);

            var shawnInFight = fight.Session.Encounter.PlayerParty.First();
            var kitBefore = fight.Session.KitFor(shawnInFight);
            Assert.IsNotNull(kitBefore, "the fielded Shawn has no kit");
            CollectionAssert.DoesNotContain(kitBefore.Skills.Select(s => s.Id), Woolgathering,
                "fixture: Woolgathering was already in the opening kit");

            yield return PlayToTheEnd(fight);
            Assert.IsTrue(fight.Session.PlayerWon,
                "Shawn lost the room, so no experience was paid and this test is pinning nothing");

            Assert.GreaterOrEqual(shawn.level, AbilityLevel,
                $"the victory did not carry Shawn across the {CostOfLevelThree} experience levels 2 and 3 cost");
            Assert.AreEqual(1, shawn.claimedTrackLevel,
                "the track paid itself out -- collection is meant to be the player's own act");

            // AND NOT IN THE FIGHT THAT EARNED IT. The kit was built when the
            // encounter was (FightEncounterAdapter.KitFor), so the same object
            // that had no Woolgathering before the victory still has none
            // after it -- which is the half of §6 a player has to be told
            // about, and the reason the card says "from your next fight".
            CollectionAssert.DoesNotContain(
                fight.Session.KitFor(shawnInFight).Skills.Select(s => s.Id), Woolgathering,
                "the fight that paid for the level also handed over the skill");

            // ---- collected, through the fight's own system menu -------------

            yield return CollectLevelThreeThroughTheMenu();

            Assert.GreaterOrEqual(shawn.claimedTrackLevel, AbilityLevel,
                "pressing the level-3 node did not move the watermark");
            Assert.IsTrue(
                RewardTracks.For(shawn).HasUnlocked(TrackReward.UnlockSkill, shawn.claimedTrackLevel),
                "the track still reports no ability unlocked after collecting one");

            // STILL NOT IN THIS FIGHT'S KIT, now for the interesting reason:
            // it has been collected and the kit is simply not rebuilt.
            CollectionAssert.DoesNotContain(
                fight.Session.KitFor(shawnInFight).Skills.Select(s => s.Id), Woolgathering,
                "collecting rebuilt the kit of a fight already in progress");

            // ---- fight two: the skill is ACTIVE ----------------------------

            FightController next = null;
            yield return EnterAFightRoom(f => next = f);

            var shawnAgain = next.Session.Encounter.PlayerParty.First();
            CollectionAssert.Contains(
                next.Session.KitFor(shawnAgain).Skills.Select(s => s.Id), Woolgathering,
                "the collected ability did not reach the next fight's kit");
        }

        // ---- the fixture's two dials ----------------------------------------

        // ONE EXPERIENCE SHORT of levels 2 and 3 together, with the watermark
        // left where a fresh save has it.
        private static void OneShortOfLevelThree(Character shawn)
        {
            shawn.level = RewardTrack.StartingLevel;
            shawn.exp = CostOfLevelThree - 1;
            shawn.claimedTrackLevel = 1;

            Assert.AreEqual(CostOfLevelThree, Character.ExpToNextLevel(1) + Character.ExpToNextLevel(2),
                "the authored cost table no longer prices levels 2 and 3 at 160 together -- repin this fixture");
        }

        // POWER WITHOUT A LEVEL. Points are granted and spent the way the
        // dossier spends them, which touches nothing the track reads: a level
        // grant would defeat the test and a health grant would not make him
        // hit hard enough to finish a room inside the timeout.
        private static void GrantFightingPower(Character shawn)
        {
            shawn.unspentStatPoints = 60;

            int scoreIndex = 0;
            while (shawn.unspentStatPoints > 0)
            {
                var score = AbilityScores.All[scoreIndex % AbilityScores.All.Length];
                if (!shawn.Invest(score)) break;
                scoreIndex++;
            }

            Assert.AreEqual(0, shawn.unspentStatPoints, "the granted points did not all land");
        }

        // ---- the run, the room and the verbs --------------------------------

        private IEnumerator EnterAFightRoom(System.Action<FightController> found)
        {
            var room = APlainFightRoom();
            Assert.IsTrue(RunManager.MoveTo(room.Id), "could not move into the fight room");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession, "the room built no session");
            found(fight);
        }

        private static DescentNode APlainFightRoom()
        {
            var choices = RunManager.Choices();
            CollectionAssert.IsNotEmpty(choices, "the leg offered nowhere to go");

            var fightRoom = choices.FirstOrDefault(n => n.Type == RoomType.Fight);
            Assert.IsNotNull(fightRoom, "the leg offered no plain fight room to play");
            return fightRoom;
        }

        // Pressing Verb0 and a target until the enemy is down, then waiting for
        // FightBootstrap's own settlement -- the only thing that pays
        // experience. Lifted from FightSettlementTests, whose header explains
        // why forcing the session to IsOver settles nothing.
        private IEnumerator PlayToTheEnd(FightController fight)
        {
            bool settled = false;
            fight.FightEnded += _ => settled = true;

            float deadline = Time.realtimeSinceStartup + 60f;

            while (!fight.Session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (fight.IsBusy || !fight.Session.IsPlayerTurn) { yield return null; continue; }

                var attack = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name == "Verb0" && b.interactable);
                if (attack == null) { yield return null; continue; }

                attack.onClick.Invoke();
                yield return null;

                var target = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name.StartsWith("EnemyPlate") && b.interactable);
                if (target != null) target.onClick.Invoke();

                yield return null;
            }

            Assert.IsTrue(fight.Session.IsOver,
                $"the fight never ended: IsBusy={fight.IsBusy}, IsPlayerTurn={fight.Session.IsPlayerTurn}");

            while (!settled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(settled, "the fight is over and FightEnded never fired");
        }

        // ---- the menu, opened where the plan says it can be -------------------
        //
        // §6: "collected (system menu, available in hub, map and fight)". The
        // dossier's track row is wired ABOVE its own lockedForFight guard for
        // exactly this -- see CharacterDossierController's own note -- so the
        // path below is the one a player has inside a fight and not a back
        // door around it.
        private IEnumerator CollectLevelThreeThroughTheMenu()
        {
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the Fight scene has no SystemMenuController");

            menu.Open();
            menu.Select(0);
            yield return null;

            var trackRow = AllButtons(menu).FirstOrDefault(b => b.name == "DossierTrackRow");
            Assert.IsNotNull(trackRow, "the dossier has no reward-track row");
            trackRow.onClick.Invoke();
            yield return null;

            var node = AllButtons(menu).FirstOrDefault(b => b.name == "TrackDot" + AbilityLevel);
            Assert.IsNotNull(node, $"the rail has no node for level {AbilityLevel}");
            node.onClick.Invoke();
            yield return null;
        }

        private static IEnumerable<Button> AllButtons(SystemMenuController menu) =>
            menu.GetComponentsInChildren<Button>(includeInactive: true);
    }
}
