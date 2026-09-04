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
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The three things that make a blow look like a blow: the figure moves, it
    // flashes white, and its pose steps through its own frames.
    //
    // ALL THREE ARE FOUND BY GetComponent AT PLAYBACK TIME, which is a silent
    // failure mode by construction -- a missing component is a null and an early
    // return, not an error. The lunge was exactly that: StageActorAnimator
    // existed, FightBeatPlayer.Lunge called for it, and nothing ever attached
    // one, so for the entire life of the fight screen nothing moved and every
    // test passed.
    public class StageAnimationTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // The transform breath runs on its own clock, unscaled by
            // BeatSpeedMultiplier by design (see FightController.
            // BreathSpeedMultiplier's own comment) -- this collapses its
            // ~2.8s real-time cycle for the four tests below that wait it
            // out.
            FightController.BreathSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        // Every stage slot on either rack -- these are what carry the
        // animator, the hit flash and the death fade, and this whole file
        // exists to catch one of them silently going without.
        private List<Transform> AnimatableSlots() =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(t => t.name.EndsWith("Slot")
                            && (t.name.StartsWith("Enemy") || t.name.StartsWith("Party")))
                .ToList();

        private IEnumerator OpenAFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);
            yield return null;
        }

        // ---- the components a playback call site reaches for -------------------

        [UnityTest]
        public IEnumerator EveryStageSlotCanActuallyBeMoved()
        {
            // The regression this file exists for.
            yield return OpenAFight();

            var slots = AnimatableSlots();

            Assert.IsNotEmpty(slots, "the stage has no slots at all - the naming convention moved");

            var unmovable = slots
                .Where(s => s.GetComponent<StageActorAnimator>() == null)
                .Select(s => s.name)
                .ToList();

            CollectionAssert.IsEmpty(unmovable,
                "these slots have no animator, so Lunge silently does nothing for them: " +
                string.Join(", ", unmovable));
        }

        [UnityTest]
        public IEnumerator BothStageRacksCanBeShaken()
        {
            // A length pin rather than a name check: ShakeStage iterates
            // the array positionally and does not care what each entry is
            // called, only that a kick actually reaches every rack that has
            // a figure standing on it.
            yield return OpenAFight();

            Assert.AreEqual(2, _fight.StageShakesForTest.Length,
                "expected one StageShake per rack (enemy, party) -- a rack with no shaker stands " +
                "nailed down while ShakeStage kicks the other one around it");

            CollectionAssert.DoesNotContain(_fight.StageShakesForTest, null,
                "a null entry in stageShakes is a rack nothing was ever attached to");
        }

        [UnityTest]
        public IEnumerator EveryStageSlotCanActuallyFlash()
        {
            yield return OpenAFight();

            var slots = AnimatableSlots();

            var unflashable = slots
                .Where(s => s.GetComponentInChildren<StageHitFlash>(includeInactive: true) == null)
                .Select(s => s.name)
                .ToList();

            CollectionAssert.IsEmpty(unflashable,
                "these slots have no hit flash: " + string.Join(", ", unflashable));
        }

        // ---- and that they do something ----------------------------------------

        private IEnumerator ABoundFight()
        {
            yield return OpenAFight();

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // ---- the breath between blows ---------------------------------------

        // NOBODY BREATHED, and every test in this file passed.
        //
        // Three actors ship a six-frame idle and the manifest authors a pace
        // for each, and all three stood on frame 0 for the whole fight because
        // nothing stepped them. This file's own header names the failure mode
        // exactly -- an animation that is never driven is a silent no-op, not
        // an error -- and then went on to test the three animations that DO
        // belong to a beat while the one that belongs to no beat at all had no
        // driver and no test.
        //
        // Asserted on a beetle specifically: the roster is mostly flat art, and
        // a test that took whatever enemy happened to be handy would pass by
        // testing a single-frame idle that legitimately never moves.
        [UnityTest]
        public IEnumerator AnIdleFigureBreathesWhileNothingIsHappening()
        {
            yield return AFightAgainst("Enemies/beetle");

            var foe = _fight.SessionForTest.Encounter.Enemies[0];
            Assert.AreEqual(FightSession.Stances.Idle, _fight.StanceFor(foe),
                "the stage is not at rest, so this would be testing playback rather than the idle loop");

            var seen = new HashSet<int>();
            for (int i = 0; i < 90; i++)
            {
                seen.Add(_fight.FrameFor(foe));
                yield return null;
            }

            Assert.Greater(seen.Count, 1,
                "the idle pose never left frame 0 - a six-frame sheet is being shown as a still");
        }

        // The other half of the rule, and the reason the loop asks the sheet
        // rather than a flag: flat art has one drawing and must simply sit on
        // it. A loop that wrapped a single frame would repaint the whole stage
        // every frame for a picture that never changes.
        //
        // Asserted on the golem, not the rat -- the rat shipped a 12-frame
        // idle sheet (tools/slice_actor_sheet.py, 2026-08-29) and is no
        // longer a single-frame-idle creature. The golem is: see the comment
        // below on which of the roster still qualifies.
        [UnityTest]
        public IEnumerator FlatArtStaysPutRatherThanFlickering()
        {
            yield return AFightAgainst("Enemies/golem");

            var foe = _fight.SessionForTest.Encounter.Enemies[0];

            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(0, _fight.FrameFor(foe),
                    "a single-frame idle moved off its only drawing");
                yield return null;
            }
        }

        // ---- and the breath that needs no drawings at all ----------------------

        // THE FIGURES THAT NEVER MOVED. FlatArtStaysPutRatherThanFlickering
        // above is still right -- a single-frame idle must stay on its only
        // drawing -- and for as long as swapping drawings was the only thing
        // that could animate anything, "stays put" and "stands perfectly still"
        // were the same sentence.
        //
        // They are not any more, and that is the whole point of the transform
        // breath. The golem, the bog witch and Shawn ship one idle.png each
        // (the rat did too, until its 12-frame idle sheet shipped 2026-08-29
        // -- see StanceManifest.json and tools/slice_actor_sheet.py); on a
        // stage where the troll wobbles and everything around it is frozen,
        // the troll reads as broken rather than as rough.
        //
        // ASSERTED ON THE GOLEM SPECIFICALLY, the mirror of the beetle above:
        // an enemy picked for having no idle sheet, so this cannot pass by
        // accident on art that was already animating.
        [UnityTest]
        public IEnumerator FlatArtStillBreathesEvenThoughItsDrawingCannot()
        {
            yield return AFightAgainst("Enemies/golem");

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the enemy slot has no animator, so nothing can breathe");

            float baseHeight = animator.BaseScale.y;
            float tallest = baseHeight;

            // A full period is 2.8s and this is not worth waiting out: the
            // curve is past a third of its amplitude within half a second, and
            // BreathCurveTests owns the shape. What this needs to see is that
            // SOMETHING is driving it.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && tallest <= baseHeight * 1.004f)
            {
                if (slot.localScale.y > tallest) tallest = slot.localScale.y;
                yield return null;
            }

            Assert.Greater(tallest, baseHeight * 1.004f,
                $"a single-frame idle stood at exactly {baseHeight:F4} for three seconds - it has " +
                "no second drawing to step to, so the transform breath is the only thing that can " +
                "move it and nothing is pushing one");

            Assert.AreEqual(0, _fight.FrameFor(_fight.SessionForTest.Encounter.Enemies[0]),
                "the breath moved the drawing as well - it is supposed to be a transform");
        }

        // THE OTHER HALF OF THE RULE, and it is about what "authored size"
        // means rather than about taste. A slot's base scale is the creature's
        // stageScale times its depth in the formation -- two numbers somebody
        // chose -- and a breath centred on that would leave every actor
        // spending half its life smaller than the size it was given.
        [UnityTest]
        public IEnumerator ABreathOnlyEverMakesTheFigureTallerThanItsMark()
        {
            yield return AFightAgainst("Enemies/rat");

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            float baseHeight = animator.BaseScale.y;

            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.GreaterOrEqual(slot.localScale.y, baseHeight - 0.0001f,
                    $"the figure shrank to {slot.localScale.y:F4} below its mark of {baseHeight:F4}");
                yield return null;
            }
        }

        // THE COMPOUNDING BUG, pinned because it is invisible for about a
        // minute and then obvious.
        //
        // AnchorStageSlots re-homes a slot every time the live count changes,
        // which on a fight with summons or deaths is often. It used to assign
        // the rect and have the animator read it straight back -- correct only
        // while nothing else wrote localScale between the two. The breath
        // writes it every frame, so a re-home landing mid-breath would have
        // taken base x 1.02 as the base, and the next one base x 1.02 x 1.02,
        // and the figure would grow a couple of percent per re-home for the
        // rest of the encounter.
        [UnityTest]
        public IEnumerator ReHomingAFigureMidBreathDoesNotFoldTheBreathIntoItsSize()
        {
            yield return AFightAgainst("Enemies/rat");

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();

            var mark = animator.Home;
            var size = animator.BaseScale;

            // Wait until the figure is demonstrably mid-breath, so this is
            // re-homing over a live deformation rather than over nothing.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline
                   && slot.localScale.y <= size.y * 1.004f)
            {
                yield return null;
            }

            Assert.Greater(slot.localScale.y, size.y * 1.004f,
                "the figure never got far enough into a breath for this test to mean anything");

            for (int i = 0; i < 8; i++)
            {
                animator.Rehome();
                yield return null;
            }

            Assert.AreEqual(size.y, animator.BaseScale.y, 0.0005f,
                $"eight re-homes took the base scale from {size.y:F4} to {animator.BaseScale.y:F4} - " +
                "the breath is being folded into the size the figure returns to");
            Assert.AreEqual(mark.x, animator.Home.x, 0.5f, "the re-homes moved the mark itself");
        }

        // A swing and a breath both deform the figure, and they overlap
        // constantly -- something is always breathing when something else
        // lands. The failure mode is not subtle: two callers each assigning
        // localScale means whichever wrote last wins, so a punch would vanish
        // for a frame every time the breath ticked, and vice versa.
        [UnityTest]
        public IEnumerator APunchStillLandsOnAFigureThatIsBreathing()
        {
            yield return AFightAgainst("Enemies/rat");

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();

            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline
                   && slot.localScale.y <= animator.BaseScale.y * 1.004f)
            {
                yield return null;
            }

            float breathing = slot.localScale.x;

            // Punch narrows and heightens (negative stretch). Against a breath
            // that is doing a little of the same, the test is that the punch
            // still dominates the width.
            animator.Punch(1f);
            yield return null;

            Assert.Less(slot.localScale.x, breathing * 0.95f,
                $"the punch left the width at {slot.localScale.x:F4} against {breathing:F4} - a " +
                "hit landing on a breathing figure did nothing to it");
        }

        // ---- the afterimage on a fast move -------------------------------------

        // A FAST TRAVEL LEAVES A TRAIL, and a slow one does not. Driven on a
        // bare animator rather than a whole fight so it is deterministic: the
        // trail is spaced by distance, so a long committed rush drops ghosts and
        // a short lean drops none, and both are asserted against the same
        // animator.
        //
        // At REAL speed on purpose. The suite runs at 60x, where a 0.14s fade is
        // two milliseconds and nothing could be caught active; this one move is
        // worth the wait.
        [UnityTest]
        public IEnumerator AFastMoveLeavesAnAfterimageAndASlowOneDoesNot()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            var slot = new GameObject("Slot", typeof(RectTransform), typeof(StageActorAnimator));
            slot.transform.SetParent(stage, false);
            var animator = slot.GetComponent<StageActorAnimator>();

            var spriteGo = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            spriteGo.transform.SetParent(slot.transform, false);
            var image = spriteGo.GetComponent<Image>();
            image.sprite = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f));

            yield return null;   // let Awake resolve the rect
            animator.Rehome();
            animator.BindSprite(image);

            // A long committed rush: distance far past the ghost spacing.
            animator.Play(new Vector2(600f, 0f), 0f, 0.3f);

            bool sawGhost = false;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && !sawGhost)
            {
                sawGhost = stage.GetComponentsInChildren<Transform>(true)
                    .Any(t => t.name == "Afterimage" && t.gameObject.activeSelf);
                yield return null;
            }

            Assert.IsTrue(sawGhost,
                "a fast move left no afterimage - the trail's emit is silently off, the exact class " +
                "of nulled-visual bug this file exists to catch");

            // Let the trail settle, then a move far shorter than one ghost's
            // spacing must leave nothing behind.
            yield return new WaitForSeconds(0.3f);
            foreach (var t in stage.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Afterimage") t.gameObject.SetActive(false);
            }

            animator.Play(new Vector2(20f, 0f), 0f, 0.3f);
            float shortDeadline = Time.realtimeSinceStartup + 1f;
            bool sawGhostOnShortMove = false;
            while (Time.realtimeSinceStartup < shortDeadline && !sawGhostOnShortMove)
            {
                sawGhostOnShortMove = stage.GetComponentsInChildren<Transform>(true)
                    .Any(t => t.name == "Afterimage" && t.gameObject.activeSelf);
                yield return null;
            }

            Assert.IsFalse(sawGhostOnShortMove,
                "a 20px lean trailed - the blur is meant for the fast parts, not every twitch");

            Object.Destroy(stage.gameObject);
        }

        // ---- the anticipation leg in front of a lunge --------------------------

        // EVERY TRANSFORM BACK TO BASELINE when a lead-in lunge ends.
        //
        // The anticipation leg drifts the figure the WRONG WAY and squashes it
        // before the strike leaves, which means two new ways for a move to end
        // somewhere other than where it started: a lead that returns early
        // leaves the mark stale, and a squash left unwound is not
        // self-correcting -- nothing else writes localScale, so the figure
        // simply stays deformed for the rest of the fight (ResetToHome's own
        // comment records that trap).
        //
        // Position and scale only. Where the figure GETS to mid-lead is a
        // perception question and sampling it would be timing-sensitive; that
        // belongs to a captured pilot, not here.
        [UnityTest]
        public IEnumerator AnAnticipatedLungeStillEndsExactlyOnItsMark()
        {
            var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            var slot = new GameObject("Slot", typeof(RectTransform), typeof(StageActorAnimator));
            slot.transform.SetParent(stage, false);
            var animator = slot.GetComponent<StageActorAnimator>();

            yield return null;   // let Awake resolve the rect
            animator.Rehome();

            var rect = (RectTransform)slot.transform;
            var mark = animator.Home;
            var size = animator.BaseScale;

            // 0.07 is StageActorAnimator.AnticipationSeconds, written out
            // rather than read: the constant is internal to Core, and a test
            // that read it back would move with any change to it instead of
            // noticing one.
            animator.Play(new Vector2(180f, 0f), 0f, -1f, 0.07f);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (animator.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(animator.IsPlaying, "the lunge never finished, so the checks below mean nothing");

            Assert.AreEqual(mark.x, rect.anchoredPosition.x, 0.0001f,
                "the figure came to rest off its mark - the anticipation leg moved it and the return " +
                "did not account for the whole distance");
            Assert.AreEqual(mark.y, rect.anchoredPosition.y, 0.0001f,
                "the figure came to rest off its mark vertically");
            Assert.AreEqual(size.x, rect.localScale.x, 0.0001f,
                "the anticipation squash was never unwound, and nothing else writes localScale - " +
                "the figure stays deformed for the rest of the fight");
            Assert.AreEqual(size.y, rect.localScale.y, 0.0001f,
                "the anticipation squash was never unwound vertically");

            Object.Destroy(stage.gameObject);
        }

        private IEnumerator AFightAgainst(string spritePath)
        {
            yield return OpenAFight();

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: spritePath), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackingMovesTheAttacker()
        {
            yield return ABoundFight();

            var slot = (RectTransform)Named("Party0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            // Catch it mid-lunge rather than after: the animator returns the
            // figure to where it started, so waiting for playback to finish
            // would assert on the resting position either way.
            bool moved = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (!Mathf.Approximately(slot.anchoredPosition.x, rest)) moved = true;
                yield return null;
            }

            Assert.IsTrue(moved, "the attacker never left its slot - the lunge did nothing");
        }

        [UnityTest]
        public IEnumerator TheAttackerComesBackToItsSlot()
        {
            // A figure that lunges and stays there drifts across the stage over a
            // long fight.
            yield return ABoundFight();

            var slot = (RectTransform)Named("Party0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            // Waited on as a condition, for the same reason as above.
            var animator = slot.GetComponent<StageActorAnimator>();
            float settle = Time.realtimeSinceStartup + 5f;
            while (!Mathf.Approximately(slot.anchoredPosition.x, animator.Home.x)
                   && Time.realtimeSinceStartup < settle) yield return null;

            Assert.AreEqual(rest, slot.anchoredPosition.x, 0.5f,
                "the figure is parked off its mark - over a long fight it drifts across the stage");
        }

        [UnityTest]
        public IEnumerator TakingAHitFlashesTheTarget()
        {
            yield return ABoundFight();

            var flash = Named("Enemy0HitFlash").GetComponent<Image>();
            Assert.IsNotNull(flash);

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            bool flashed = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (flash.enabled && flash.color.a > 0.01f) flashed = true;
                yield return null;
            }

            Assert.IsTrue(flashed, "the target never flashed - the one signal that means 'you were hit'");
        }

        [UnityTest]
        public IEnumerator TheFlashWearsOff()
        {
            yield return ABoundFight();

            var flash = Named("Enemy0HitFlash").GetComponent<Image>();

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            float clear = Time.realtimeSinceStartup + 5f;
            while (flash.enabled && flash.color.a >= 0.02f && Time.realtimeSinceStartup < clear) yield return null;

            Assert.IsTrue(!flash.enabled || flash.color.a < 0.02f,
                "a flash that never clears leaves the figure a white silhouette");
        }
    
        // ---- recoil --------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryStageSlotCanActuallyFade()
        {
            yield return OpenAFight();

            var slots = AnimatableSlots();

            var unfadable = slots
                .Where(s => s.GetComponent<StageDeathFade>() == null)
                .Select(s => s.name)
                .ToList();

            CollectionAssert.IsEmpty(unfadable,
                "these slots cannot fade, so a corpse would sit on the stage: " + string.Join(", ", unfadable));
        }

        [UnityTest]
        public IEnumerator BeingHitMovesTheTargetToo()
        {
            // The other half of a blow. Without it only the attacker moves, and
            // the hit reads as the target ignoring it.
            yield return ABoundFight();

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            bool flinched = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (!Mathf.Approximately(slot.anchoredPosition.x, rest)) flinched = true;
                yield return null;
            }

            Assert.IsTrue(flinched, "the target never flinched");
        }

        [UnityTest]
        public IEnumerator AMonsterFlinchesIntoItsOwnHalf()
        {
            // Away is decided by which SIDE a figure is on, not by where the
            // blow came from -- deriving it from the attacker would send a
            // back-row monster stumbling toward the party when a status tick
            // hit it from behind.
            yield return ABoundFight();

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float furthest = rest;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (slot.anchoredPosition.x > furthest) furthest = slot.anchoredPosition.x;
                yield return null;
            }

            Assert.Greater(furthest, rest, "a monster stands on the right, so it recoils to the RIGHT");
        }

        // ---- the fade ------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshFightUnfadesWhoeverDiedInTheLastOne()
        {
            // The slot is reused rather than rebuilt, so a figure left at zero
            // alpha would begin the next fight invisible.
            yield return ABoundFight();

            var sprite = Named("Enemy0Sprite").GetComponent<Image>();
            Named("Enemy0Slot").GetComponent<StageDeathFade>().PlayIfNotAlready();

            // Waited on as a CONDITION, not a frame count. A fixed count passes
            // alone and fails in the full suite, because how much wall-clock a
            // frame represents depends on what else is running -- which is a
            // property of the test host, not of the thing being tested.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (sprite.color.a > 0.98f && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.Less(sprite.color.a, 0.99f, "fixture: it never actually faded");

            yield return ABoundFight();

            Assert.AreEqual(1f, Named("Enemy0Sprite").GetComponent<Image>().color.a, 0.01f,
                "the next fight started with an invisible monster");
        }
}
}
