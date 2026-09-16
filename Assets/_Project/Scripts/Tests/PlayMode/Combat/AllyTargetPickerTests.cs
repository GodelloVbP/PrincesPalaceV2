using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE ALLY PICKER, through the real scene (AUDIT #147).
    //
    // WHY THIS CANNOT BE AN EditMode TEST. Everything about WHO may be picked
    // is Domain and is pinned there (FightTalentTests, BotAllyTargetSelection-
    // Tests). What only a scene can answer is whether the two racks actually
    // swap places on screen: that the party plates become buttons with markers
    // while an ally pick is open, that the enemy plates stop being buttons
    // while it is, and that both go back afterwards. Those are four
    // serialized arrays and a repaint, and none of them exists until the
    // scene is built.
    //
    // DRIVEN THROUGH MoveFocus/ConfirmFocus rather than through simulated
    // clicks, for the reason FightGamepadNavigationTests' own header gives:
    // legacy Input cannot be simulated headlessly, and these are the same
    // handlers a click reaches.
    public class AllyTargetPickerTests
    {
        private FightController _fight;
        private CombatantState _shawn;
        private CombatantState _ally;
        private CombatantState _second;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void RestoreBeatSpeed() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private Button ButtonNamed(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"the Fight scene has no node called '{name}'");
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, $"'{name}' is not a Button");
            return button;
        }

        // A ward is the cheapest single-ally skill to stand up: no mana, no
        // resource, no cooldown and no ability requirement, so the row is
        // affordable on turn one and the test is about the PICKER rather than
        // about affordability.
        // flatAmount 50, matching skills.json's own fleece_ward: a ward is a
        // POOL of shield points since the shield model (AUDIT #152), so a row
        // that authors nothing puts up nothing and this picker would have had
        // no ward to confirm.
        private static ResolvedSkill Ward() =>
            new ResolvedSkill("fleece_ward", "Ward", "", "sheep", 1, SkillEffect.Ward,
                SkillEntryResolver.DefaultTargetingFor(SkillEffect.Ward),
                0, 0, false, 0, 50, false, null, SpellPresentation.None, 0);

        private IEnumerator LoadFightWithAWard()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            // Fast enough to open, and the monster slow enough that it never
            // replies between the two halves of a test.
            _shawn = new CombatantState("Shawn", true, 300, 30, 40, 30);
            _shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
            });
            _ally = new CombatantState("Bjorn", true, 300, 30, 40, 8);
            _second = new CombatantState("Odette", true, 300, 30, 40, 7);

            var foe = new CombatantState("Front", false, 5000, 10, 8, 1);
            var encounter = new CombatEncounter(new[] { _shawn, _ally, _second }, new[] { foe });

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Ward() }, null, DamageType.Physical),
                new PlayerKit("bear", CharacterRole.Tank, null, null, DamageType.Physical),
                new PlayerKit("owl", CharacterRole.Utility, null, null, DamageType.Arcane),
            };

            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false),
            };

            var session = new FightSession(encounter, kits, enemyKits, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;

            Assert.AreSame(_shawn, session.Current, "fixture: the ward's owner has to be the one acting");
        }

        // Root focus starts on ATTACK (index 0) and the column is drawn
        // bottom-up, so one step "up" reaches SKILL. Confirm opens the list,
        // confirm again presses row 0 -- the ward.
        private IEnumerator OpenTheAllyPick()
        {
            _fight.MoveFocus(-1);
            _fight.ConfirmFocus();
            yield return null;

            _fight.ConfirmFocus();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AWardOpensThePartyRackAndClosesTheEnemyOne()
        {
            yield return LoadFightWithAWard();

            Assert.IsFalse(Named("PcPlate0Reticle").activeInHierarchy,
                "fixture: nothing is being picked yet");

            yield return OpenTheAllyPick();

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(ButtonNamed($"PcPlate{i}").interactable,
                    $"PcPlate{i} has to take the click that confirms the ward");
                Assert.IsTrue(Named($"PcPlate{i}Reticle").activeInHierarchy,
                    $"PcPlate{i} carries no marker to say it can be picked");
                Assert.IsTrue(Named($"PartyHitArea{i}").activeInHierarchy,
                    "clicking the figure is what a player tries first");
            }

            // AND THE OTHER RACK IS OFF. A live enemy plate during an ally
            // pick is a button offering a cast that would be refused.
            Assert.IsFalse(ButtonNamed("EnemyPlate0").interactable,
                "the enemy rack must go quiet while the party rack is live");
            Assert.IsFalse(Named("EnemyPlate0Reticle").activeInHierarchy);
            Assert.IsFalse(Named("EnemyHitArea0").activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator BackingOutOfAnAllyPickPutsBothRacksBackAsTheyWere()
        {
            yield return LoadFightWithAWard();
            yield return OpenTheAllyPick();

            // The same press the CANCEL control and Escape both reach.
            ButtonNamed("TargetCancelButton").onClick.Invoke();
            yield return null;

            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(ButtonNamed($"PcPlate{i}").interactable);
                Assert.IsFalse(Named($"PcPlate{i}Reticle").activeInHierarchy);
                Assert.IsFalse(Named($"PartyHitArea{i}").activeInHierarchy);
            }

            Assert.IsFalse(Named("EnemyPlate0Reticle").activeInHierarchy,
                "backing out of a pick is not the same as starting the other one");
        }

        [UnityTest]
        public IEnumerator MovingFocusAtTargetDepthCyclesTheHoveredLivingAlly()
        {
            // The party-side sibling of FightGamepadNavigationTests'
            // MovingFocusAtTargetDepthCyclesTheHoveredLivingEnemy, and the
            // same narrow claim: one step per call, through the cursor the
            // confirm actually reads.
            //
            // LITERAL INDICES, not just "changed" -- WrapFromNoHover's fix
            // (FightController.Input.cs) made a first "next" press land on
            // pickable[0] rather than pickable[1]. All three plates are
            // pickable for this fixture's Ward (AWardOpensThePartyRackAnd-
            // ClosesTheEnemyOne), so pickable == [0, 1, 2] in plate order.
            yield return LoadFightWithAWard();
            yield return OpenTheAllyPick();

            _fight.MoveFocus(1);
            int first = _fight.HoveredAllyIndexForTest;

            _fight.MoveFocus(1);
            int second = _fight.HoveredAllyIndexForTest;

            Assert.AreEqual(0, first, "a first \"next\" press from no hover lands on the first pickable plate");
            Assert.AreEqual(1, second, "a second \"next\" press steps to the next pickable plate");
        }

        // THE OTHER HALF OF THE SAME FIX: a first "previous" press from no
        // hover lands on the LAST pickable plate rather than the first --
        // WrapFromNoHover(-1, -1, count) == count - 1. Pinned alongside
        // MovingFocusAtTargetDepthCyclesTheHoveredLivingAlly's "next" case so
        // the two directions cannot silently drift apart.
        [UnityTest]
        public IEnumerator AFirstPreviousPressAtTargetDepthLandsOnTheLastPickableAlly()
        {
            yield return LoadFightWithAWard();
            yield return OpenTheAllyPick();

            _fight.MoveFocus(-1);
            Assert.AreEqual(2, _fight.HoveredAllyIndexForTest,
                "a first \"previous\" press from no hover lands on the last pickable plate, index 2 of 3");
        }

        [UnityTest]
        public IEnumerator ConfirmingOnAPartyPlateWardsThatSquadmate()
        {
            yield return LoadFightWithAWard();
            yield return OpenTheAllyPick();

            // PcPlate1 is the middle rank, which this fixture's party order
            // puts Bjorn in -- the ally, not the caster. Pressed through the
            // Button itself, which is what a mouse actually reaches.
            ButtonNamed("PcPlate1").onClick.Invoke();
            yield return null;

            Assert.IsTrue(StatusEffects.IsWarded(_ally), "the ward landed on the plate that was pressed");
            Assert.AreSame(_shawn, StatusEffects.WardedBy(_ally), "sourced to the caster, not to the wearer");

            // NOBODY ELSE. This caster holds WardReductionPercent and nothing
            // else, so The Flock is not in play and the share goes nowhere --
            // which is what makes "the pick, and only the pick" checkable here
            // rather than only in the talent tests.
            Assert.IsFalse(StatusEffects.IsWarded(_shawn));
            Assert.IsFalse(StatusEffects.IsWarded(_second));
        }

        // ---- the picture ------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheOpenAllyPick()
        {
            yield return LoadFightWithAWard();

            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 " +
                              "-Filter PrincesPalace.PlayModeTests.AllyTargetPickerTests");
                yield break;
            }

            yield return OpenTheAllyPick();
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");

            string dir = CaptureOutput.LabelDir("ally_picker");
            Directory.CreateDirectory(dir);

            CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "ally_pick_open.png"), 1920, 1080);

            Assert.IsTrue(File.Exists(Path.Combine(dir, "ally_pick_open.png")),
                "the capture wrote nothing");
        }
    }
}
