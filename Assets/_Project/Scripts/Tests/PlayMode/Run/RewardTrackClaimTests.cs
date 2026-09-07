using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The reward track paying out, through the screen that does the paying.
    //
    // THROUGH THE PANEL, not by calling ClaimTrackRewards directly, and that is
    // the whole design of this file. The claim has exactly ONE production call
    // site -- RewardTrackController.Claim -- and a test that reached past it
    // would pass just as happily if that site were deleted, leaving a game
    // where stat points can no longer be collected at all and a green suite
    // saying otherwise.
    //
    // That argument is inherited rather than invented: it is the one
    // RewardApplierTests carried while the applier was the call site
    // (architecture_audit.md F17, AUDIT #46). The site moved when collection
    // became manual, so the coverage moved with it.
    public class RewardTrackClaimTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            // BEFORE the scene loads, which is the ordering that matters: the
            // hub reads the save on the way up, and an override applied after
            // would test against the developer's own save file and then write
            // to it.
            _root = Path.Combine(Path.GetTempPath(), "pp-track-claim-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            // Every event cue on this screen -- fly-in, glide included -- runs
            // off Time.unscaledDeltaTime (see RewardTrackController.Motion's
            // own SpeedMultiplier comment), so this collapses the wait below
            // to well under a frame. 1 outside a test, so play is unchanged.
            RewardTrackController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            RewardTrackController.SpeedMultiplier = 1f;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Opens the hub, levels the squad, and gets the reward track on screen.
        //
        // The same path SystemMenuCaptureTests takes, because it is the only
        // one there is: the panel is an inactive child of the dossier, inside a
        // pane of the system menu, inside the hub.
        private static IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
                character.claimedTrackLevel = claimed;
                character.unspentStatPoints = 0;
            }

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            menu.Open();
            menu.Select(0);
            yield return null;

            var row = menu.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name == "DossierTrackRow");
            Assert.IsNotNull(row, "the dossier has no reward-track row");
            row.onClick.Invoke();

            // TWICE. Start() runs one frame after SetActive(true), not
            // synchronously -- docs/CODE_STANDARDS.md section 5, and clicking a
            // button before it has had that frame is the flake this avoids.
            yield return null;
            yield return null;
        }

        private static Button Find(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);

        // CLOSING THE PANEL WHILE A NODE IS HOVERED, which threw every time.
        //
        // HoverIndex reports an EXIT from its own OnDisable -- deliberately, so
        // a tooltip anchored to something that vanishes mid-hover does not stay
        // open forever. On this screen that exit reaches BeginCardSwap, which
        // starts a coroutine; and by the time a child's OnDisable runs, the
        // panel it belongs to is already inactive. Unity refuses, loudly:
        //
        //   Coroutine couldn't be started because the game object
        //   'RewardTrackPanel' is inactive!
        //
        // Reported from play. The controller's own OnDisable calls
        // StopAllCoroutines, which looks like it covers this and does not --
        // Unity gives no ordering guarantee between a child's OnDisable and its
        // parent's, so the exit can arrive after the panel is down and before
        // the controller has torn anything off.
        //
        // A PlayMode test fails on an unexpected LogError, so the reproduction
        // IS the assertion -- there is nothing to check afterwards because the
        // throw leaves no state behind.
        [UnityTest]
        public IEnumerator ClosingTheTrackWhileANodeIsHoveredDoesNotThrow()
        {
            yield return OpenTheTrack(level: 5, claimed: 5);

            var panel = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "RewardTrackPanel");
            Assert.IsNotNull(panel, "the hub has no RewardTrackPanel");
            Assert.IsTrue(panel.gameObject.activeInHierarchy, "fixture: the track should be open");

            // Hovered through the real component, because that is what the
            // panel's own OnDisable will later report an exit from.
            var hover = panel.GetComponentsInChildren<HoverIndex>(includeInactive: true).FirstOrDefault();
            Assert.IsNotNull(hover, "the track's nodes carry no HoverIndex, so this pin is vacuous");
            hover.OnPointerEnter(null);
            yield return null;

            panel.gameObject.SetActive(false);
            yield return null;
        }

        // The migration case, end to end: a character carrying twenty-nine
        // unclaimed levels presses one button and is settled.
        [UnityTest]
        public IEnumerator CollectAllSettlesEveryWaitingLevel()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            var collect = Find("TrackCollectButton");
            Assert.IsNotNull(collect, "the reward track has no collect button");
            Assert.IsTrue(collect.gameObject.activeInHierarchy,
                "the collect button is hidden while thirty levels are owed - " +
                "it appearing IS the notification that the track owes something");

            collect.onClick.Invoke();
            yield return null;

            var character = SquadFixture.FirstLiveMember();
            Assert.AreEqual(30, character.claimedTrackLevel,
                "pressing collect did not move the watermark to the level reached");
            Assert.Greater(character.unspentStatPoints, 0,
                "no stat points were handed over for twenty-nine levels of track");
        }

        // The bug the watermark exists to prevent, now reachable the way a
        // player would actually reach it: press collect twice.
        [UnityTest]
        public IEnumerator TheSameLevelsAreNeverPaidTwice()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            var collect = Find("TrackCollectButton");
            collect.onClick.Invoke();
            yield return null;

            int points = SquadFixture.FirstLiveMember().unspentStatPoints;

            // The button has hidden itself by now, which is most of the answer.
            // Pressing it anyway is the half that matters: hiding a control is
            // presentation, and a claim that paid again when invoked would be a
            // bug wearing a disabled button.
            collect.onClick.Invoke();
            yield return null;

            Assert.AreEqual(points, SquadFixture.FirstLiveMember().unspentStatPoints,
                "stat points were paid a second time for levels already collected");
        }

        // Nothing owed means nothing to press. The button is the count of what
        // is waiting, so a squad in lockstep must not see one.
        [UnityTest]
        public IEnumerator WithNothingOwedThereIsNoCollectButton()
        {
            yield return OpenTheTrack(level: 30, claimed: 30);

            var collect = Find("TrackCollectButton");
            Assert.IsNotNull(collect, "the reward track has no collect button node at all");
            Assert.IsFalse(collect.gameObject.activeInHierarchy,
                "the collect button is offering to collect nothing");
        }

        // CLICKING A WAITING NODE collects up to it, which is the other half of
        // section 4 and the one the collect button is a shortcut for.
        //
        // Claiming is SEQUENTIAL: pressing level 20 with a watermark at 0
        // settles everything from 2 to 20 and stops, so claimedTrackLevel stays
        // a single integer and there is never a collected hole below an
        // uncollected level.
        [UnityTest]
        public IEnumerator PressingAWaitingNodeCollectsEverythingUpToIt()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            var node = Find("TrackDot20");
            Assert.IsNotNull(node, "the rail has no node for level 20");

            node.onClick.Invoke();
            yield return null;

            // 20, NOT 30 -- the node that was pressed, which is what this
            // test's own name says and what handoff section 4 specifies.
            //
            // IT ASSERTED 30 UNTIL 2026-08-22, with a comment arguing that
            // stopping at the pressed node "needs a second number on the save".
            // It does not: a claim always begins at the watermark and always
            // moves it, so stopping at 20 leaves the watermark at 20 and 21
            // upward still owed. One number, no hole.
            //
            // What the test was really pinning was the implementation it was
            // written beside -- ClaimTrackRewards took no argument, so every
            // node on the rail was a collect-everything button wearing a
            // different number. A test that agrees with the code rather than
            // with the design cannot fail when the code is the thing that is
            // wrong, which is the whole reason this one survived.
            Assert.AreEqual(20, SquadFixture.FirstLiveMember().claimedTrackLevel,
                "pressing a waiting node paid past the node that was pressed");

            // And the rest is still owed rather than lost.
            Assert.AreEqual(10, RewardTrack.UnclaimedCount(30, SquadFixture.FirstLiveMember().claimedTrackLevel),
                "the levels above the pressed node stopped being owed");
        }

        // A node that cannot be collected must still do something. A hundred
        // dots that do nothing when pressed teach the player that none of them
        // do, and the ones that DO matter become invisible.
        [UnityTest]
        public IEnumerator PressingAnUnreachedNodeGlidesRatherThanClaiming()
        {
            yield return OpenTheTrack(level: 30, claimed: 30);

            var content = Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(r => r.name == "TrackContent");
            Assert.IsNotNull(content, "the reward track has no content rect");

            float before = content.anchoredPosition.x;

            var node = Find("TrackDot90");
            Assert.IsNotNull(node, "the rail has no node for level 90");
            node.onClick.Invoke();

            // A FIXED WAIT STILL, not a poll -- no exposed "still animating"
            // flag to watch on RewardTrackController, and the glide it starts
            // cancels whatever fly-in was still running rather than one clean
            // state to sample. [SetUp]'s SpeedMultiplier = 40 runs the whole
            // glide (460ms) and the fly-in it cancels in low single-digit
            // milliseconds of real time; 0.15s real still catches a genuine
            // stall while being ~5x faster than the flat 0.8s this replaced.
            float deadline = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(30, SquadFixture.FirstLiveMember().claimedTrackLevel,
                "pressing an unreached node paid something out");
            Assert.AreNotEqual(before, content.anchoredPosition.x,
                "pressing an unreached node did nothing at all");
        }

        // ---- what a collected node actually pays ---------------------------------
        //
        // Everything below reads the PRODUCTION seam the reward is spent
        // through -- ContentDatabase.BuildSignatureResource, .ModifierEffects,
        // .AvailableSkillsFor, SquadTrack.SecondLivesLeft -- rather than
        // asking the definition what it holds. Asking the definition would
        // pass just as happily if no read site had ever been wired, which is
        // the exact failure AUDIT #53 records: a reward that is granted,
        // stored and read by nothing.
        //
        // EVERY LITERAL HERE IS READ OFF docs/PLAN_REWARD_TRACKS.md section 5's
        // tables, never recomputed from the definition under test (CLAUDE.md's
        // fifth gotcha). A retune in reward_tracks.json moves these numbers
        // and these tests together, which is the honest trade for pinning
        // figures a person can check by eye.

        private static Character Roster(string definitionId) =>
            SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == definitionId);

        // 10 base capacity (characters.json), plus the three filler
        // "+1 WOOL CAPACITY" nodes at levels 6, 14 and 21, plus the level-25
        // milestone's +5. The uncollected reading is the same minus that
        // milestone.
        [Test]
        public void AWoolCapacityNodeCollectedRaisesTheFightsCapacityAndAnUncollectedOneDoesNot()
        {
            var shawn = Roster("sheep");
            shawn.level = 26;

            shawn.claimedTrackLevel = 25;
            Assert.AreEqual(18, ContentDatabase.BuildSignatureResource(shawn).Max,
                "the level-25 wool capacity milestone never reaches the fight's resource");

            shawn.claimedTrackLevel = 24;
            Assert.AreEqual(13, ContentDatabase.BuildSignatureResource(shawn).Max,
                "an uncollected capacity node was paid anyway");
        }

        // LIGHTNING, and the choice of element is the whole test. It is the
        // one element on Odette's track that is milestone-only (section 5: Ice
        // and Lightning arrive with the spells that deal them, so validation
        // rule 4 refuses them as filler), which is what makes "none, then
        // exactly one" a true statement about it. Fire has filler nodes from
        // level 6 and is already well above zero by 69.
        //
        // ElementalDamagePercent, the packet hook P4b added, NOT the on-hit
        // rider: Lightning is not her attackType.
        [Test]
        public void AnElementalNodeReachesTheCombatantAsAModifierEffect()
        {
            var odette = Roster("owl");
            odette.level = 71;

            odette.claimedTrackLevel = 70;
            var lightning = ContentDatabase.ModifierEffects(odette).All
                .Where(e => e.Type == ModifierEffectType.ElementalDamagePercent
                            && e.Against == DamageType.Lightning)
                .ToList();

            Assert.AreEqual(1, lightning.Count,
                "the level-70 Lightning milestone did not reach the effect set as exactly one effect");
            Assert.AreEqual(20, lightning[0].Magnitude);

            odette.claimedTrackLevel = 69;
            Assert.IsFalse(ContentDatabase.ModifierEffects(odette).All
                    .Any(e => e.Against == DamageType.Lightning),
                "an uncollected elemental node was paid anyway");
        }

        // THE OTHER HALF OF THE ROUTING RULE (section 2), and the pin that
        // stops an implementer collapsing the two hooks back into one.
        //
        // A character's OWN attackType rides ElementalDamageOnHitPercent --
        // the existing rider, which multiplies every landed hit, swing and
        // cast alike -- because a plain swing carries no damage instances for
        // the packet hook to see at all. Everything else rides the packet
        // hook. Never both, for any element.
        [Test]
        public void TheCharactersOwnElementRidesTheOnHitRiderInstead()
        {
            // Shawn's attackType is Nature: 9 filler "+1% NATURE DAMAGE" nodes
            // through level 45, plus the level-45 milestone's 10.
            var shawn = Roster("sheep");
            shawn.level = 46;
            shawn.claimedTrackLevel = 45;

            var shawnEffects = ContentDatabase.ModifierEffects(shawn).All;
            var nature = shawnEffects
                .Where(e => e.Type == ModifierEffectType.ElementalDamageOnHitPercent)
                .ToList();

            Assert.AreEqual(1, nature.Count, "Shawn's Nature line is not one on-hit effect");
            Assert.AreEqual(DamageType.Nature, nature[0].Against);
            Assert.AreEqual(19, nature[0].Magnitude);
            Assert.IsFalse(shawnEffects.Any(e => e.Type == ModifierEffectType.ElementalDamagePercent),
                "Shawn's own element was ALSO routed through the packet hook, which would pay it twice " +
                "-- and pay it through the one hook 68 of his 100 levels cannot reach");

            // Odette's attackType is Arcane: 4 filler "+2% ARCANE DAMAGE"
            // nodes through level 49, plus the level-50 milestone's 10. Her
            // Fire line is the same track, the other branch.
            var odette = Roster("owl");
            odette.level = 51;
            odette.claimedTrackLevel = 50;

            var odetteEffects = ContentDatabase.ModifierEffects(odette).All;
            var arcane = odetteEffects
                .Where(e => e.Type == ModifierEffectType.ElementalDamageOnHitPercent)
                .ToList();

            Assert.AreEqual(1, arcane.Count, "Odette's Arcane line is not one on-hit effect");
            Assert.AreEqual(DamageType.Arcane, arcane[0].Against);
            Assert.AreEqual(18, arcane[0].Magnitude);

            Assert.IsTrue(odetteEffects.Any(e => e.Type == ModifierEffectType.ElementalDamagePercent
                                                 && e.Against == DamageType.Fire),
                "Odette's Fire line did not take the packet hook, which is the only seam that can " +
                "pay an element her attackType is not");
        }

        // Frost Flare is authored characterId "sheep" like every other
        // book-only spell in the game, and lands in Odette's kit anyway: the
        // track's skill route carries no ownership test, because the track IS
        // per-character and has already said whose skill it is (section 3f/3h).
        [Test]
        public void ASkillNodeCollectedPutsTheSpellInTheKit()
        {
            var odette = Roster("owl");
            odette.level = 11;

            odette.claimedTrackLevel = 10;
            Assert.IsTrue(ContentDatabase.AvailableSkillsFor(odette).Any(s => s.id == "frost_flare"),
                "the level-10 spell the track paid for is not in the kit the fight builds");

            odette.claimedTrackLevel = 9;
            Assert.IsFalse(ContentDatabase.AvailableSkillsFor(odette).Any(s => s.id == "frost_flare"),
                "a spell arrived before the player collected the node that grants it");
        }

        // Section 6: the SOURCE is per-character and the SPEND is squad-wide.
        // Two members who have collected level 90 bring two charges; the
        // third, who has collected nothing, brings none -- and the pair is
        // spent out of one pot, because TrySecondLife only fires when the
        // party would otherwise be wiped and raises everyone who is down.
        [Test]
        public void ASecondLifeIsCountedPerCollectingCharacter()
        {
            RunManager.StartRun(4457728181926001UL);

            var squad = SaveSlotManager.CurrentSave.ActiveSquad();
            Assert.AreEqual(3, squad.Count, "fixture: this test needs the squad of three");

            for (int i = 0; i < squad.Count; i++)
            {
                squad[i].level = 90;
                squad[i].claimedTrackLevel = i < 2 ? 90 : 0;
            }

            Assert.AreEqual(2, SquadTrack.SecondLivesLeft(RunManager.Run),
                "the second life is not sourced from the characters who collected it");
        }

        // ---- the v5 migration ----------------------------------------------------

        // DIRECT FIELD ASSIGNMENT ON A CreateNew SAVE, the idiom
        // EmberOwnershipTests established for a migration that reinterprets
        // values without changing the save's SHAPE. The JSON-token-stripping
        // technique in ItemModifierSaveCompatTests is for a field REMOVED from
        // the shape; claimedTrackLevel and unspentStatPoints are neither
        // removed nor renamed by this step, only read against a different
        // table afterwards, so there is nothing a round-trip would prove.
        [Test]
        public void AVersionFourSaveComesBackWithEverythingWaiting()
        {
            var save = SaveData.CreateNew();
            save.version = 4;

            var character = save.roster[0];
            character.level = 41;
            character.claimedTrackLevel = 40;
            character.unspentStatPoints = 7;

            Assert.IsTrue(save.Migrate());

            character = save.roster[0];
            Assert.AreEqual(0, character.claimedTrackLevel,
                "a v4 watermark was carried across and reinterpreted against the new table");
            Assert.AreEqual(0, character.unspentStatPoints,
                "points paid by the old table survived the track that paid them");
            Assert.AreEqual(41, character.level, "the migration took a level away");
            Assert.AreEqual(SaveData.CurrentVersion, save.version);
        }

        // The generated default, reached through the same lookup every read
        // site uses. RewardTrackDefinitionTests pins the two totals against
        // RewardTrackDefinition.Default directly; what is left to prove is
        // that a character with no authored track actually RESOLVES to it
        // rather than to an empty one or to somebody else's.
        [Test]
        public void AnUnauthoredCharacterResolvesToTheGeneratedDefault()
        {
            var track = RewardTracks.For("placeholder_brawler");

            Assert.AreEqual(50, track.GrantedBetween(TrackReward.StatPoint, 1, RewardTrack.MaxLevel),
                "40 filler singles plus level 80's ten");
            Assert.AreEqual(229, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel),
                "9 milestone nodes at 15 plus 47 filler nodes at 2");
        }
    }
}
