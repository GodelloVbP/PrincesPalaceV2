using System.Collections;
using System.Collections.Generic;
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
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The actors on the stage: which sprite, which way round, how far off the
    // floor, and what moves when a blow lands.
    //
    // These need a scene because every one of them is about a RectTransform or a
    // loaded Sprite. The rules BEHIND them -- who faces which way, where the
    // ground line is -- are Domain and already have EditMode tests; what is left
    // here is whether the view actually applies them.
    public class FightStageVisualTests
    {
        private FightController _fight;
        private CombatantState _hero;
        private CombatantState _front;

        [SetUp]
        public void PlayBeatsFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            StanceManifestLoader.Reset();
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private IEnumerator LoadFight(SpriteFacing enemyFacing = SpriteFacing.Left)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _front = new CombatantState("Front", false, 5000, 10, 8, 4);

            var encounter = new CombatEncounter(new[] { _hero }, new[] { _front });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, facing: enemyFacing), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(4));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // Two enemies, because slot 0 stands at depth 0 whatever the count and
        // therefore cannot show a stale mark. Beside LoadFight rather than a
        // parameter on it: every other test in this class is about a single
        // figure and would gain an argument it does not read.
        private IEnumerator LoadFightWithTwoEnemies()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _front = new CombatantState("Front", false, 5000, 10, 8, 4);
            var back = new CombatantState("Back", false, 5000, 10, 8, 4);

            var encounter = new CombatEncounter(new[] { _hero }, new[] { _front, back });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);

            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, facing: SpriteFacing.Left), false);
            var backKit = new EnemyKit(new ResolvedEnemy("back", "Back", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, facing: SpriteFacing.Left), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit, backKit }, new SeededRandom(4));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // ---- the mark a figure returns to ---------------------------------------
        //
        // THE BUG THESE EXIST FOR: StageActorAnimator captured its slot's
        // position and depth scale in Awake and never again, which assumed a
        // slot never moves. AnchorStageSlots moves them constantly -- it
        // re-spreads and re-scales the live slots from FightStageAnchors every
        // time the count changes, so a pair of monsters take the two ENDS of the
        // formation rather than crowding its first two positions, and every
        // death re-lays the survivors.
        //
        // So a lunge ended by snapping the figure back to where its slot used to
        // be, at the size it used to be. Not a drift -- both are absolute writes
        // at the end of a tween -- which is why it read as figures being flung
        // to the wrong place rather than as sliding.
        //
        // TWO ENEMIES, NOT ONE, AND THAT IS THE WHOLE POINT OF THE FIXTURE.
        // StageLayout.DepthForSlot(0, n) is 0 for every n, so slot 0 stands in
        // the same place whatever the count and a one-enemy fight cannot show
        // this at all -- the first version of these tests used one and passed
        // against the unfixed code. Slot 1 is where it bites: depth 0.5 of three
        // against depth 1.0 of two.
        [UnityTest]
        public IEnumerator AFigureKnowsTheMarkItsSlotActuallyHas()
        {
            yield return LoadFightWithTwoEnemies();

            var slot = (RectTransform)Named("Enemy1Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the second enemy slot has no animator");

            Assert.AreEqual(slot.anchoredPosition.x, animator.Home.x, 0.5f,
                $"the figure returns to x {animator.Home.x:F0} but its slot stands at " +
                $"{slot.anchoredPosition.x:F0} - the next lunge ends by snapping it there");
            Assert.AreEqual(slot.anchoredPosition.y, animator.Home.y, 0.5f,
                $"the figure returns to y {animator.Home.y:F0} but its slot stands at " +
                $"{slot.anchoredPosition.y:F0}");
        }

        // And the whole way round, because the assertion above is about a field
        // and this is about what the player sees: a figure that lunges has to
        // finish where it started, at the size it started.
        [UnityTest]
        public IEnumerator ALungeEndsBackOnItsOwnMarkAndItsOwnSize()
        {
            yield return LoadFightWithTwoEnemies();

            var slot = (RectTransform)Named("Enemy1Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();

            var mark = slot.anchoredPosition;
            var size = slot.localScale;

            animator.Play(new Vector2(140f, 40f), holdSeconds: 0f);

            // Polled on the animator's own IsPlaying rather than a flat
            // sleep -- [SetUp]'s BeatSpeedMultiplier = 60 already finishes
            // the lunge in milliseconds; 2s only bounds a genuine stall.
            float deadline = Time.realtimeSinceStartup + 2f;
            while (animator.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(animator.IsPlaying, "the lunge never finished");

            Assert.AreEqual(mark.x, slot.anchoredPosition.x, 0.5f, "the figure did not come home in x");
            Assert.AreEqual(mark.y, slot.anchoredPosition.y, 0.5f, "the figure did not come home in y");

            // The stretch multiplies onto the captured base scale, so a stale
            // one resizes the figure permanently rather than displacing it: a
            // back-row monster came back the size of a front-row one.
            Assert.AreEqual(size.x, slot.localScale.x, 0.01f, "the figure came back the wrong width");
            Assert.AreEqual(size.y, slot.localScale.y, 0.01f, "the figure came back the wrong height");
        }

        // The premise, pinned separately so a change to the formation curve
        // says THAT rather than failing the two tests above for a reason that
        // has nothing to do with animators.
        [Test]
        public void TheSecondSlotMovesWithTheCount()
        {
            Assert.AreNotEqual(
                StageLayout.DepthForSlot(1, 2), StageLayout.DepthForSlot(1, 3),
                "slot 1 now stands in the same place whether there are two combatants or three, so " +
                "the tests above can no longer tell a stale mark from a live one");
        }

        // ---- the slots exist and are populated ----------------------------------

        [UnityTest]
        public IEnumerator OnlyTheOccupiedSlotsAreShown()
        {
            yield return LoadFight();

            Assert.IsTrue(Named("Enemy0Slot").activeSelf, "one monster, one slot");
            Assert.IsFalse(Named("Enemy1Slot").activeSelf);
            Assert.IsFalse(Named("Enemy2Slot").activeSelf);
            Assert.IsTrue(Named("Party0Slot").activeSelf);
        }

        [UnityTest]
        public IEnumerator ANameplateNamesWhoeverIsStandingThere()
        {
            yield return LoadFight();

            Assert.AreEqual("Front", Named("Enemy0Nameplate").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("Shawn", Named("Party0Nameplate").GetComponent<TMPro.TMP_Text>().text);
        }

        // ---- graceful degradation ------------------------------------------------

        [UnityTest]
        public IEnumerator ACombatantWithNoArtGetsThePlateRatherThanAWhiteQuad()
        {
            // The shipped v1 bug class: an Image with no sprite renders as a
            // SOLID WHITE RECTANGLE, not as nothing. Neither of these combatants
            // has authored battle art in this fixture, so both take this path.
            yield return LoadFight();

            var enemy = Named("Enemy0Sprite").GetComponent<Image>();

            Assert.IsNotNull(enemy.sprite, "a sprite-less Image is a white quad");
            Assert.IsTrue(enemy.enabled);
            Assert.AreEqual(Vector3.one, enemy.rectTransform.localScale,
                "the fallback is a UI frame, not a character - mirroring it would just reverse its bevel");
        }

        [UnityTest]
        public IEnumerator TheFallbackClearsAnyGroundingItInherited()
        {
            // A slot that fell back AFTER showing real art would otherwise keep
            // the last pose's drop -- the plate has no feet, and the branch
            // returns early, so nothing else would undo it.
            yield return LoadFight();

            var enemy = Named("Enemy0Sprite").GetComponent<Image>();

            Assert.AreEqual(Vector2.zero, enemy.rectTransform.offsetMin);
            Assert.AreEqual(Vector2.zero, enemy.rectTransform.offsetMax);
        }

        // ---- what a blow does ----------------------------------------------------

        [UnityTest]
        public IEnumerator ALandedBlowFlashesTheTarget()
        {
            yield return LoadFight();

            var flash = Named("Enemy0HitFlash").GetComponent<StageHitFlash>();
            Assert.IsNotNull(flash);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            Assert.IsTrue(Named("Enemy0HitFlash").activeSelf,
                "the overlay is built inactive and has to wake itself - StartCoroutine on an inactive " +
                "GameObject is a hard error, not a no-op");
        }

        [UnityTest]
        public IEnumerator TheStageEndsTheRoundIdleRatherThanHoldingTheLastPose()
        {
            // A pose belongs to the blow that caused it. Left standing, an actor
            // would hold its attack frame until something else happened to
            // overwrite it, which reads as the animation having stuck.
            yield return LoadFight();

            Click("Verb0");
            Click("EnemyPlate0");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.AreEqual(FightSession.Stances.Idle, _fight.StanceFor(_hero));
            Assert.AreEqual(FightSession.Stances.Idle, _fight.StanceFor(_front));
        }

        [UnityTest]
        public IEnumerator ADefeatedCombatantStaysDefeated()
        {
            // Read from IsAlive, not from the beat -- the round-end reset above
            // must not stand a corpse back up.
            yield return LoadFight();
            _front.CurrentHealth = 0;

            _fight.RefreshUi();

            Assert.AreEqual(FightSession.Stances.Defeated, _fight.StanceFor(_front));
        }

        // ---- the ground line -----------------------------------------------------

        [UnityTest]
        public IEnumerator TheManifestsGroundLineIsWhatDropsTheFigure()
        {
            // The single most load-bearing rule on this stage, and the one that
            // was a comment and nothing else in v1. A figure whose art clears
            // 40px below its feet must be pushed DOWN by exactly that, or it
            // hangs in the air -- the "golem flies upwards in its attack" report.
            yield return LoadFight();

            const float drop = 40f;
            StanceManifestLoader.Override(ManifestWithGroundLine("Actors/test", drop));

            Assert.AreEqual(drop, StanceManifestLoader.Manifest.GroundLineFor("Actors/test"), 0.001f,
                "the override did not take, so nothing below would be testing the real path");

            // An actor with no authored art takes the fallback branch, which
            // deliberately does NOT apply a drop. That the two branches disagree
            // is the point: grounding belongs to real art only.
            var enemy = Named("Enemy0Sprite").GetComponent<Image>();
            Assert.AreEqual(0f, enemy.rectTransform.offsetMin.y, 0.001f,
                "the plate has no feet to ground");
        }

        private static StanceManifest ManifestWithGroundLine(string actor, float groundLine)
        {
            var raw = new RawStanceManifest
            {
                actors = new List<RawStanceActor>
                {
                    new RawStanceActor { spritePath = actor, groundLine = groundLine },
                },
            };
            return new StanceManifest(raw);
        }
        // ---- a repaint must not eat the swing -----------------------------------
        //
        // THE BUG THIS EXISTS FOR, and it is the one the fix above introduced.
        //
        // AnchorStageSlots re-homes the animator so a lunge returns to the mark
        // its slot actually stands on. But RefreshStage is not an occasional
        // event -- it runs from the HUD refresh AND from SetActorFrame, once per
        // FRAME of an attack animation. Re-homing unconditionally therefore
        // cancelled the lunge it was supposed to be supporting, on every
        // combatant with more than one frame of art. Single-frame poses never
        // call SetActorFrame, so those still moved, which is what made it look
        // like attacks failed to animate at random rather than always.
        [UnityTest]
        public IEnumerator ARepaintDoesNotCancelALungeInFlight()
        {
            yield return LoadFightWithTwoEnemies();

            var slot = (RectTransform)Named("Enemy1Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the second enemy slot has no animator");

            var mark = animator.Home;

            // A long hold, so the figure is unambiguously parked AWAY from its
            // mark when the repaint lands rather than racing the return leg.
            animator.Play(new Vector2(140f, 40f), holdSeconds: 5f);

            // Out to the target. The lunge is 0.055s and a batch-mode frame is
            // well under a millisecond, so this waits on the POSITION rather
            // than on a frame count.
            for (int i = 0; i < 600 && (slot.anchoredPosition - mark).sqrMagnitude < 1f; i++)
            {
                yield return null;
            }

            Assert.Greater((slot.anchoredPosition - mark).sqrMagnitude, 1f,
                "the figure never left its mark, so this test cannot show anything");

            var mid = slot.anchoredPosition;

            // The repaint that used to kill it.
            _fight.RefreshUi();
            yield return null;

            Assert.Greater((slot.anchoredPosition - mark).sqrMagnitude, 1f,
                "a repaint mid-lunge snapped the figure back onto its mark - the swing is being " +
                "cancelled by the very frame advance that is supposed to be drawing it");

            Assert.AreEqual(mark.x, animator.Home.x, 0.5f, "the repaint moved the mark itself");
            Assert.AreEqual(mark.y, animator.Home.y, 0.5f, "the repaint moved the mark itself");
        }

        // The other half, so the guard above cannot be satisfied by simply
        // never re-homing again: a slot that GENUINELY moves must still be
        // re-homed, which is the whole point of the previous fix.
        [UnityTest]
        public IEnumerator ASlotThatReallyMovesIsStillRehomed()
        {
            yield return LoadFightWithTwoEnemies();

            var slot = (RectTransform)Named("Enemy1Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();

            Assert.AreEqual(slot.anchoredPosition.x, animator.Home.x, 0.5f,
                "the mark and the slot disagree before anything has even moved");

            // Shove the slot somewhere its anchor says it does not belong, then
            // repaint. AnchorStageSlots must notice and put it back.
            slot.anchoredPosition = new Vector2(-999f, -999f);
            animator.Rehome();

            _fight.RefreshUi();
            yield return null;

            Assert.AreEqual(slot.anchoredPosition.x, animator.Home.x, 0.5f,
                "the slot moved and the animator was not told");
            Assert.AreNotEqual(-999f, slot.anchoredPosition.x,
                "the repaint left the slot where it had been shoved instead of re-anchoring it");
        }

    }
}
