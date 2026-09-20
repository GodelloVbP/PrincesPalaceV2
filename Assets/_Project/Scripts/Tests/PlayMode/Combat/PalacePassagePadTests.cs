using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE TWO-PICK ALLY SELECTION, DRIVEN ON A PAD, THROUGH THE PRODUCTION
    // DISPATCHER (plan 1.12; docs/GAMEPAD_NAVIGATION_PLAN.md sections 3 and
    // 10).
    //
    // WHY THIS FILE EXISTS RATHER THAN A HAND CHECK. Milestone C's own gate
    // asks the owner to confirm the pad path by hand, and the owner was not
    // available for this pass. The gamepad plan's answer to exactly that
    // situation is its section 10 "dispatcher integration" shape: a scripted
    // BaseInput assigned through BaseInputModule.inputOverride, frames driven
    // with `yield return null`, and the REAL NavigationInputModule reading it
    // -- which is the same code a controller reaches, not a stand-in for it.
    // Section 2 of that plan verifies, API by API, that every value the module
    // reads goes through that seam.
    //
    // WHAT IT THEREFORE DOES AND DOES NOT PROVE. It proves that the presses a
    // pad produces reach the picker, that two of them complete a swap, that
    // Cancel steps back one level rather than out, and that the Submit which
    // OPENS the picker does not also confirm inside it. It does not prove
    // repeat cadence, which is Time.unscaledTime inside StandaloneInputModule
    // and has no BaseInput equivalent -- the gamepad plan records that as a
    // known hardware-acceptance-only gap (section 2), and nothing here
    // pretends otherwise.
    public class PalacePassagePadTests
    {
        private FightController _fight;
        private ScriptedBaseInput _input;
        private CombatantState _caster;
        private CombatantState _ally;
        private CombatantState _second;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, freeAction: true);

        private IEnumerator LoadFightWithThePassage()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the Fight scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            // MANA COST ZERO on the fixture's Passage, deliberately: this file
            // is about the PRESSES, and an affordability refusal would fail it
            // for a reason that has nothing to do with input. What the cast
            // costs is pinned in PalacePassageTests.
            _caster = new CombatantState("Shawn", true, 300, 30, 40, 30);
            _ally = new CombatantState("Bjorn", true, 300, 30, 40, 8);
            _second = new CombatantState("Odette", true, 300, 30, 40, 7);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 1);

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
                new PlayerKit("bear", CharacterRole.Tank, null, null, DamageType.Physical),
                new PlayerKit("owl", CharacterRole.Utility, null, null, DamageType.Arcane),
            };

            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false),
            };

            var session = new FightSession(
                new CombatEncounter(new[] { _caster, _ally, _second }, new[] { foe }),
                kits, enemyKits, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            Assert.AreSame(_caster, session.Current, "fixture: the Passage's owner has to be the one acting");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // ONE STICK FRAME, through the real dispatcher. Fight's branch reads
        // the axis itself and has its own armed edge, so it never touches
        // StandaloneInputModule's real-time repeat gate.
        private IEnumerator Move(float vertical)
        {
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        private IEnumerator PressSubmit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
        }

        private IEnumerator PressCancel()
        {
            _input.CancelDown = true;
            yield return DriveFrame();
        }

        // Root focus starts on ATTACK (index 0) and the column is drawn
        // bottom-up, so one step "up" reaches SKILL. Submit opens the list,
        // Submit again presses row 0 -- the Passage.
        private IEnumerator OpenThePicker()
        {
            yield return Move(1f);
            Assert.AreEqual(1, _fight.FocusedVerbForTest, "precondition: the stick reached SKILL");

            yield return PressSubmit();
            yield return PressSubmit();
        }

        [UnityTest]
        public IEnumerator TheSubmitThatOpensThePickerDoesNotAlsoTakeItsFirstPick()
        {
            // THE GAMEPAD PLAN'S OWN RULE, and the single most likely pad bug
            // in this whole milestone: "Submit that opens a modal cannot
            // confirm it". One event, consumed once. If the press that opened
            // the ally rack also counted as pick 1, the player would be one
            // press into a cast they never chose to start, and the only
            // visible symptom would be the prompt reading "ally 2 of 2".
            yield return LoadFightWithThePassage();
            yield return OpenThePicker();

            Assert.IsTrue(_fight.IsPickingAllyForTest, "the Passage did not open the ally rack");
            Assert.AreEqual(2, _fight.PicksRequiredForTest, "the picker did not ask for two");
            Assert.AreEqual(0, _fight.PicksHeldForTest,
                "the Submit that opened the picker was spent twice");
        }

        [UnityTest]
        public IEnumerator TwoPadSubmitsCompleteTheSwap()
        {
            yield return LoadFightWithThePassage();
            var party = _fight.SessionForTest.Encounter.PlayerParty;
            var before = party.ToList();

            yield return OpenThePicker();

            yield return PressSubmit();
            Assert.AreEqual(1, _fight.PicksHeldForTest, "the first pad Submit recorded nothing");
            Assert.IsTrue(_fight.IsPickingAllyForTest, "the first pick closed the picker");
            CollectionAssert.AreEqual(before, party.ToList(), "the first pick already moved somebody");

            yield return PressSubmit();
            yield return null;

            // THE SECOND PRESS LANDS ON A DIFFERENT ALLY, which is the half
            // of this that nearly did not work: Submit with nothing hovered
            // presses the first PICKABLE plate, and the ally taken by pick 1
            // has to be off that list or the pad could never finish the cast.
            CollectionAssert.AreNotEqual(before, party.ToList(),
                "two pad Submits did not complete the swap");
            CollectionAssert.AreEquivalent(before, party.ToList(),
                "the swap lost or duplicated a party member -- it reorders, it does not recruit");
        }

        [UnityTest]
        public IEnumerator CancelAtTheSecondPickStepsBackOnePickRatherThanOutOfTheMenu()
        {
            // THE OWNER'S RULE FROM 2026-09-18, applied to a depth that did
            // not exist then: Cancel steps back ONE level first. A held pick
            // is a level, so the first Cancel drops it and the rack stays up;
            // only the second leaves.
            yield return LoadFightWithThePassage();
            yield return OpenThePicker();
            yield return PressSubmit();

            Assert.AreEqual(1, _fight.PicksHeldForTest, "precondition: a pick is held");

            yield return PressCancel();

            Assert.AreEqual(0, _fight.PicksHeldForTest, "Cancel did not drop the held pick");
            Assert.IsTrue(_fight.IsPickingAllyForTest,
                "Cancel at picks 1/2 left the ally rack entirely instead of stepping back one level");

            yield return PressCancel();

            Assert.IsFalse(_fight.IsPickingAllyForTest,
                "a second Cancel with nothing held has to leave the rack");
        }

        [UnityTest]
        public IEnumerator ACancelledPadSelectionLeavesTheFieldExactlyAsItWas()
        {
            // Atomic cancellation, from the input end. The Domain half is
            // PalacePassageTests.CancellingAfterTheFirstPick_LeavesTheBoard-
            // Unchanged; this is the same claim made of the presses a
            // controller actually sends.
            yield return LoadFightWithThePassage();
            var party = _fight.SessionForTest.Encounter.PlayerParty;
            var before = party.ToList();

            yield return OpenThePicker();
            yield return PressSubmit();
            yield return PressCancel();
            yield return PressCancel();
            yield return null;

            CollectionAssert.AreEqual(before, party.ToList(), "a cancelled pad selection moved somebody");
            Assert.AreSame(_caster, _fight.SessionForTest.Current,
                "a cancelled pad selection spent the turn");
        }
    }
}
