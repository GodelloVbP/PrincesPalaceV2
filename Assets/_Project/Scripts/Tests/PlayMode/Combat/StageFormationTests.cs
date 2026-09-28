using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
    // WHERE A FIGURE STANDS, AND WHAT IT IS -- the two facts A3 separated.
    //
    // Until Move existed they were one fact: a party member's index in
    // Encounter.PlayerParty was its slot, its sprite, its nameplate, its hit
    // flash and its death fade all at once, and that held only because nothing
    // ever reordered the list. Move reorders it. This fixture is the pair of
    // claims that separation has to keep true:
    //
    //   1. IDENTITY IS THE SLOT'S. A move swaps two figures' marks and swaps
    //      nothing else -- each keeps its own nameplate, its own flash and its
    //      own fade -- and the enemy swing that follows in the same round lands
    //      on whoever is actually standing in front by then.
    //   2. POSITION IS THE RANK'S, per beat. The line closes up over a corpse,
    //      and it does so only once the corpse has finished fading.
    //
    // Driven through the real scene by clicking the real buttons, the same way
    // FightFlowTests is, and with its hand-built fixture rather than the
    // bootstrap's: the whole subject is a two-member formation and exactly
    // which member is at rank 0 at which instant, neither of which a real run's
    // roster would let this state.
    //
    // A FIXED-RATE RECORDING, borrowing StaticPilotStageCaptureTests' use of
    // Time.captureDeltaTime. Both claims are about ORDER in time -- did the
    // cross finish before the swing, did the slide start after the fade -- and
    // a loop sampling real frames watches the beat run away from it whenever a
    // frame is expensive to produce, which is precisely backwards.
    public class StageFormationTests
    {
        // Fast enough to keep two rounds under a couple of seconds, slow enough
        // that the 0.35s cross and the 0.95s fade are still tens of samples
        // apart. At 60x (FightFlowTests' own multiplier) both collapse into a
        // single frame and every ordering claim below becomes unfalsifiable.
        private const float BeatSpeed = 4f;
        private const float SampleSeconds = 1f / 120f;

        // Generous: the two rounds under test are ~60 frames at the rate above.
        private const int FrameBudget = 900;

        // The stage marks, stated as the literals FightStageAnchorsTests pins
        // rather than recomputed from FightStageAnchors here -- a test that
        // re-derives the formula it is checking asserts only that arithmetic is
        // deterministic (CLAUDE.md gotcha 5).
        //
        // Far.X 565 -> 660, 2026-09-09: the party-overlap fix in
        // FightStageAnchors (see Far's own comment) widened the far anchor.
        private static readonly Vector2 NearMark = new Vector2(300f, -218f);
        private static readonly Vector2 FarMark = new Vector2(660f, -125f);

        // THE PARTY'S OWN FORMATION, not the enemy's mirrored. It stopped
        // being a mirror in Y on 2026-09-09 (the roster plates stand on the
        // party's half of the floor and the middle slot of a shared,
        // shallower line landed inside them), and in X later the same day:
        // -360/-720 -> -320/-810, because three party bipeds 197-228px wide
        // at a 180px slot pitch overlapped by a third each and read as one
        // clump. PartyRetreat, the single scalar these two used to be
        // derived from, is gone -- see FightStageAnchors.PartyNearX.
        private static readonly Vector2 PartyNearMark = new Vector2(-320f, -218f);
        private static readonly Vector2 PartyFarMark = new Vector2(-810f, -64f);

        // The middle of the party's three seats, FightStageAnchorsTests'
        // ThePartyLineIsSteeperThanTheEnemyLine literals.
        private static readonly Vector2 PartyMiddleMark = new Vector2(-565f, -141f);

        // A mark is arrived at or it is not; the walk lands on the exact value
        // rather than easing asymptotically into it (StageActorAnimator.Gliding
        // assigns the goal outright on its last frame).
        private const float MarkTolerance = 0.5f;

        private FightController _fight;
        private FightBeatPlayer _player;

        [SetUp]
        public void RecordAtAFixedRate()
        {
            FightBeatPlayer.BeatSpeedMultiplier = BeatSpeed;
            FightController.BreathSpeedMultiplier = 1f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
            Time.captureDeltaTime = 0f;
        }

        // ---- the fixture ------------------------------------------------------

        // Searched from THIS fight's own root, for the reason FightFlowTests'
        // own copy gives: these tests load the same scene repeatedly and a
        // global lookup can hand back the last one's node.
        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the Fight scene");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        private StageActorAnimator AnimatorOf(string slot) =>
            Named(slot).GetComponent<StageActorAnimator>();

        private RectTransform RectOf(string name) =>
            (RectTransform)Named(name).transform;

        private float AlphaOf(string name) => Named(name).GetComponent<Image>().color.a;

        private static EnemyKit Kit(string name) =>
            new EnemyKit(new ResolvedEnemy(name.ToLowerInvariant(), name, new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        private IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_player, "the Fight scene has no FightBeatPlayer");

            // The scene's own FightBootstrap has already started a fight and is
            // playing its opening beats; left running, its playback holds the
            // controller busy and every click below is swallowed. Same opening
            // move StaticPilotStageCaptureTests makes, and for the same reason.
            _player.Flush();
            yield return null;
        }

        private void StandUp(IReadOnlyList<CombatantState> party, IReadOnlyList<CombatantState> enemies)
        {
            var session = new FightSession(
                new CombatEncounter(party, enemies),
                party.Select(_ => PlayModeSparkFixture.Kit()).ToList(),
                enemies.Select(e => Kit(e.Name)).ToList(),
                new SeededRandom(11))
            {
                // Every claim here is about which figure a blow reaches, never
                // about how big it was, and a rolled number is one more reason
                // a fixture can drift.
                DamageVarianceRange = 0f,
            };

            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
        }

        // ---- one sampled frame ------------------------------------------------

        private sealed class Trace
        {
            public readonly List<int> PartyCrossing = new List<int>();
            public int EnemyLungeStart = -1;
            public int FrontFlash = -1;
            public int BackFlash = -1;
            public Vector2 PopupAt = Vector2.zero;
            public int PopupFrame = -1;
        }

        // ---- 1: a move, and the swing that answers it -------------------------

        [UnityTest]
        public IEnumerator AMoveCrossesBeforeTheEnemySwingsAndTheSwingFindsTheNewFront()
        {
            yield return LoadScene();

            // Vanguard acts first and steps BACK; the monster is next on
            // initiative and Reserve is last, so the reply lands inside the
            // same round as the move -- which is the whole case. Reserve
            // slower than the monster is what makes that true: if Reserve went
            // first the round would end on a player turn and the swing would
            // be a click away instead of a beat away.
            var vanguard = new CombatantState("Vanguard", true, 300, 30, 20, 10);
            var reserve = new CombatantState("Reserve", true, 300, 30, 20, 1);
            var foe = new CombatantState("Ogre", false, 5000, 10, 12, 5);

            StandUp(new[] { vanguard, reserve }, new[] { foe });
            yield return null;

            // The opening formation, and the identity that has to survive it.
            Assert.AreEqual("Vanguard", TextOf("Party0Nameplate"));
            Assert.AreEqual("Reserve", TextOf("Party1Nameplate"));
            // A DUO STANDS IN SEATS OF THREE (PLAN_BELLWETHER_KIT R1): the
            // second member is drawn in the middle, not at the far end.
            AssertMark("Party0Slot", PartyNearMark, "Vanguard opens in the front seat");
            AssertMark("Party1Slot", PartyMiddleMark, "Reserve opens in the middle seat");

            var fade0 = Named("Party0Slot").GetComponent<StageDeathFade>();
            var fade1 = Named("Party1Slot").GetComponent<StageDeathFade>();
            Assert.IsNotNull(fade0);
            Assert.AreNotSame(fade0, fade1, "each party slot carries its own death fade");

            // THE SCENE'S OWN BOOTSTRAP FIGHT RAN FIRST, and its opening beats
            // flashed a party slot on the way past. FightBeatPlayer.Flush
            // reclaims the popups and stops the beats but has no business
            // reaching into the stage's overlays, so a flash left part-lit by
            // that fight would read here as this one's blow landing on the
            // wrong figure -- which is exactly the thing under test.
            foreach (var flash in _fight.GetComponentsInChildren<StageHitFlash>(includeInactive: true))
            {
                flash.Clear();
            }

            Assert.AreEqual(0f, AlphaOf("Party0HitFlash"), 0.001f, "the stage did not start dark");
            Assert.AreEqual(0f, AlphaOf("Party1HitFlash"), 0.001f, "the stage did not start dark");

            Time.captureDeltaTime = SampleSeconds;

            // MOVE is verb 3, and its column's second row is BACK --
            // FightHudModel.MoveRows' own fixed order, FORWARD then BACK.
            Click("Verb3");
            Assert.AreEqual("BACK", TextOf("CharacterSkill1Name"),
                "row 1 of the move column should be BACK");
            Click("CharacterSkill1");

            var trace = new Trace();
            yield return Watch(trace, "Ogre", vanguard, reserve);

            // ---- position moved -------------------------------------------
            CollectionAssert.AreEqual(new[] { reserve, vanguard },
                _fight.Session.Encounter.PlayerParty.ToList(), "the move landed in the model");

            AssertMark("Party0Slot", PartyMiddleMark, "Vanguard's own slot walked back to the middle seat");
            AssertMark("Party1Slot", PartyNearMark, "Reserve's own slot walked up to rank 0");

            // ---- and identity did not ---------------------------------------
            Assert.AreEqual("Vanguard", TextOf("Party0Nameplate"),
                "slot 0 still belongs to Vanguard -- a move changes where a figure stands, " +
                "not which figure it is");
            Assert.AreEqual("Reserve", TextOf("Party1Nameplate"));
            Assert.AreSame(fade0, Named("Party0Slot").GetComponent<StageDeathFade>(),
                "slot 0 kept its own death fade across the move");
            Assert.AreSame(fade1, Named("Party1Slot").GetComponent<StageDeathFade>());

            // ---- the swing waited, then found the new front -----------------
            Assert.Greater(trace.PartyCrossing.Count, 0,
                "the party never crossed at all -- a move that teleports is not the tween under test");
            Assert.GreaterOrEqual(trace.EnemyLungeStart, 0, "the ogre never swung");

            int crossEnded = trace.PartyCrossing[trace.PartyCrossing.Count - 1];
            Assert.Greater(trace.EnemyLungeStart, crossEnded,
                $"the ogre began its lunge on frame {trace.EnemyLungeStart}, while the party was " +
                $"still crossing (last crossing frame {crossEnded}) -- it would be aiming at the " +
                "gap between two figures in transit");

            // Reserve stands at rank 0 by the time the blow lands, so the blow
            // is Reserve's: front-rank melee, re-picked after the move.
            Assert.Less(reserve.CurrentHealth, reserve.MaxHealth, "the new front rank took the swing");
            Assert.AreEqual(vanguard.MaxHealth, vanguard.CurrentHealth,
                "Vanguard stepped out of reach and must not have been touched");

            Assert.GreaterOrEqual(trace.FrontFlash, 0,
                "nothing flashed on Reserve's own slot -- the hit flash is bound to the slot, " +
                "so a flash on the wrong one means identity moved with position");
            Assert.AreEqual(-1, trace.BackFlash,
                "Vanguard's slot flashed for a blow that did not land on Vanguard");

            Assert.GreaterOrEqual(trace.PopupFrame, 0, "no damage number appeared");
            Assert.AreEqual(PartyNearMark.x, trace.PopupAt.x, MarkTolerance,
                "the damage number popped over the FAR mark -- it is placed off the target's " +
                "slot, and the target's slot is the one now standing at rank 0");
        }

        // ---- 2: a kill, the fade, and the line closing up ----------------------

        [UnityTest]
        public IEnumerator TheSurvivorClosesUpOnlyOnceTheCorpseHasFinishedFading()
        {
            yield return LoadScene();

            // One blow kills the front monster outright, and the one behind it
            // is built to survive the rest of the fight -- the subject is the
            // slide, so a second death inside the window would confuse it.
            var hero = new CombatantState("Shawn", true, 5000, 30, 40, 10);
            var doomed = new CombatantState("Front", false, 1, 0, 4, 3);
            var survivor = new CombatantState("Back", false, 5000, 0, 4, 2);

            StandUp(new[] { hero }, new[] { doomed, survivor });
            yield return null;

            AssertMark("Enemy0Slot", NearMark, "the doomed monster opens at rank 0");
            AssertMark("Enemy1Slot", FarMark, "the survivor opens at rank 1");

            // Their resting opacities, read before anything fades: the ring is
            // baked at 0.85 rather than 1, so "they fade together" is a claim
            // about the FRACTION of each, not about equal alphas.
            float bodyBase = AlphaOf("Enemy0Sprite");
            float ringBase = AlphaOf("Enemy0FootShadow");
            float glowBase = AlphaOf("Enemy0FootGlow");
            Assert.Greater(bodyBase, 0f, "the doomed monster is already invisible");
            Assert.Greater(ringBase, 0f, "the doomed monster's contact ring is already invisible");
            Assert.Greater(glowBase, 0f, "the doomed monster's foot glow is already invisible");

            Time.captureDeltaTime = SampleSeconds;

            Click("Verb0");
            Click("EnemyPlate0");

            int fadedFrame = -1;
            int slideStarted = -1;
            var survivorAnimator = AnimatorOf("Enemy1Slot");

            for (int frame = 0; frame < FrameBudget; frame++)
            {
                yield return null;

                float body = AlphaOf("Enemy0Sprite") / bodyBase;
                float ring = AlphaOf("Enemy0FootShadow") / ringBase;
                float glow = AlphaOf("Enemy0FootGlow") / glowBase;

                // THE RING GOES WITH THE BODY. A corpse whose shadow stayed put
                // reads as the sprite failing to draw rather than as a death --
                // StageDeathFade drives both off one fraction and this is what
                // holds that true through a real kill.
                Assert.AreEqual(body, ring, 0.01f,
                    $"frame {frame}: the body is at {body:F2} of its opacity and the ring at {ring:F2}");

                // AND THE GLOW GOES WITH BOTH (C2) -- a corpse whose foot
                // glow stayed lit read as a smudge of light left behind
                // rather than as part of the same death.
                Assert.AreEqual(body, glow, 0.01f,
                    $"frame {frame}: the body is at {body:F2} of its opacity and the glow at {glow:F2}");

                if (fadedFrame < 0 && body <= 0.001f) fadedFrame = frame;

                // THE WALK HAS BEGUN when the animator has a new goal, not when
                // the figure has visibly moved. The walk used to run on the
                // UNSCALED clock, which captureDeltaTime does not pin: at an
                // idle machine's ~1ms real frames its first step was under
                // MarkTolerance and was seen a frame or more later, and under
                // load a ~20ms real frame moved it past tolerance on the very
                // frame it began -- so "has it moved" measured the machine,
                // not the order. It steps by engine time now (see
                // StageActorAnimator's header), but the goal is still the
                // earlier and exact signal: Mark is the goal the moment
                // GlideTo is called, on the fixed beat clock the fade also
                // runs on.
                if (slideStarted < 0 &&
                    (survivorAnimator.IsGliding ||
                     Vector2.Distance(survivorAnimator.Mark, FarMark) > MarkTolerance))
                {
                    slideStarted = frame;
                }

                if (fadedFrame >= 0 && slideStarted >= 0 && !survivorAnimator.IsGliding) break;
            }

            Assert.GreaterOrEqual(fadedFrame, 0, "the front monster never faded out");
            Assert.GreaterOrEqual(slideStarted, 0, "the survivor never closed up");

            // AT OR AFTER, not strictly after: the walk is handed its goal on
            // the same beat-clock frame the fade writes its last zero, so the
            // first frame drawn with the survivor walking already shows no
            // corpse. The old strict ">" only held because the old detector
            // (visible movement past MarkTolerance, on the unscaled clock)
            // lagged the real start by a frame or more on an idle machine; a
            // loaded machine closed that lag and failed it. What is pinned is
            // unchanged: no sampled frame has the body visible while the
            // survivor is on its way.
            Assert.GreaterOrEqual(slideStarted, fadedFrame,
                $"the survivor left rank 1 on frame {slideStarted}, before the body had finished " +
                $"fading on frame {fadedFrame} -- it walked through a corpse that was still on screen");

            AssertMark("Enemy1Slot", NearMark,
                "the survivor is the only one standing, so it holds rank 0 and the near mark");

            // AND RESET FOR THE NEXT FIGHT (C2) -- a fresh StandUp reuses
            // these same slots, and a fade left at zero would leave the
            // glow (like the body and the ring before it) invisible for
            // whichever combatant next stands in Enemy0Slot.
            var nextFoe = new CombatantState("Next", false, 5000, 0, 4, 3);
            StandUp(new[] { hero }, new[] { nextFoe });
            yield return null;

            Assert.AreEqual(glowBase, AlphaOf("Enemy0FootGlow"), 0.01f,
                "the foot glow did not reset to full opacity for the next fight's occupant of this slot");
        }

        // ---- watching one round -----------------------------------------------

        // Runs the round out, recording the four moments the first test is
        // about: when the party was mid-cross, when the enemy's lunge left its
        // mark, which party slot flashed, and where the number popped.
        // ---- 3: a lone Shawn walks the three seats and is drawn at each -------

        [UnityTest]
        public IEnumerator ALoneShawnIsDrawnInEachSeatHeStepsTo()
        {
            yield return LoadScene();

            // PLAN_BELLWETHER_KIT 1.1 / M1: seats are real in a solo fight.
            // Front -> middle -> rear by Move BACK, then FORWARD to the middle,
            // each drawn at that seat's mark (FightStageAnchorsTests' literals).
            var shawn = new CombatantState("Shawn", true, 5000, 30, 40, 10);
            var foe = new CombatantState("Ogre", false, 5000, 10, 1, 5);

            StandUp(new[] { shawn }, new[] { foe });
            yield return null;

            AssertMark("Party0Slot", PartyNearMark, "a lone Shawn opens in the front seat");

            // The same fixed-rate clock the other two cases record on, so the
            // frame budget is a budget of game time and not of machine speed.
            Time.captureDeltaTime = SampleSeconds;

            yield return StepAndSettle(row: "CharacterSkill1", "BACK");
            Assert.AreEqual(1, _fight.Session.Encounter.SeatOf(shawn));
            AssertMark("Party0Slot", PartyMiddleMark, "one step back is the middle seat");

            yield return StepAndSettle(row: "CharacterSkill1", "BACK");
            Assert.AreEqual(2, _fight.Session.Encounter.SeatOf(shawn));
            AssertMark("Party0Slot", PartyFarMark, "two steps back is the rear seat");

            yield return StepAndSettle(row: "CharacterSkill0", "FORWARD");
            Assert.AreEqual(1, _fight.Session.Encounter.SeatOf(shawn));
            AssertMark("Party0Slot", PartyMiddleMark, "and forward again to the middle");
        }

        // Opens the Move column (verb 3), presses one direction row and waits
        // out the round it resolves.
        private IEnumerator StepAndSettle(string row, string label)
        {
            Click("Verb3");
            Assert.AreEqual(label, TextOf(row + "Name"), $"{row} of the move column should be {label}");
            Click(row);

            for (int frame = 0; frame < FrameBudget && _fight.IsBusy; frame++) yield return null;
            Assert.IsFalse(_fight.IsBusy, $"the {label} round never finished inside {FrameBudget} frames");
            yield return null;
        }

        private IEnumerator Watch(Trace trace, string enemySlotOwner,
                                  CombatantState front, CombatantState back)
        {
            var enemyAnimator = AnimatorOf("Enemy0Slot");
            var enemySlot = RectOf("Enemy0Slot");
            var party0 = AnimatorOf("Party0Slot");
            var party1 = AnimatorOf("Party1Slot");

            for (int frame = 0; frame < FrameBudget && _fight.IsBusy; frame++)
            {
                yield return null;

                if (party0.IsGliding || party1.IsGliding) trace.PartyCrossing.Add(frame);

                // A lunge is a TRAVEL: the rect leaves the mark the animator
                // still calls home. That is the one reading that separates it
                // from a walk, where the mark itself moves.
                if (trace.EnemyLungeStart < 0 &&
                    Vector2.Distance(enemySlot.anchoredPosition, enemyAnimator.Home) > 1f)
                {
                    trace.EnemyLungeStart = frame;
                }

                if (trace.BackFlash < 0 && AlphaOf("Party0HitFlash") > 0.01f) trace.BackFlash = frame;
                if (trace.FrontFlash < 0 && AlphaOf("Party1HitFlash") > 0.01f) trace.FrontFlash = frame;

                if (trace.PopupFrame < 0)
                {
                    var popup = _player.Popups.FirstOrDefault(p => p != null && !p.IsFree);
                    if (popup != null)
                    {
                        trace.PopupFrame = frame;
                        trace.PopupAt = ((RectTransform)popup.transform).anchoredPosition;
                    }
                }
            }

            Assert.IsFalse(_fight.IsBusy,
                $"the round never finished inside {FrameBudget} frames ({enemySlotOwner})");
        }

        private void AssertMark(string slot, Vector2 expected, string why)
        {
            var animator = AnimatorOf(slot);
            Assert.IsNotNull(animator, $"{slot} has no StageActorAnimator");
            Assert.AreEqual(expected.x, animator.Home.x, MarkTolerance, why + " (x)");
            Assert.AreEqual(expected.y, animator.Home.y, MarkTolerance, why + " (y)");
        }
    }
}
