using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // WHERE AN ATTACKER ACTUALLY STOPS, PHOTOGRAPHED AT THE IMPACT FRAME.
    //
    // The owner, 2026-09-09: "when attacking from the middle and backline,
    // when you go in for a hit, you stop earlier and not in front of the
    // enemy", and "Slam for Bjorn clips into the enemy when he goes in".
    // Both are claims about ONE NUMBER -- the daylight between the attacker's
    // leading opaque edge and the target's near opaque edge at the moment the
    // blow lands -- and nothing measured that number before this fixture.
    // StageStandOffTests pins the arithmetic in Domain; this proves the
    // arithmetic reaches the real stage, wearing the real art, at the real
    // marks.
    //
    // MEASURED OFF THE RENDERED TRANSFORMS, not off the production formula:
    // the opaque box is read as a FRACTION of each sprite's own untrimmed
    // canvas and applied to the Image's rendered bounds, so this agrees with
    // StageStandOff only if the stage really put the figure where the
    // arithmetic says. Re-deriving TravelTo here would assert that
    // multiplication is deterministic (CLAUDE.md gotcha 5).
    //
    // A BARE FightBeatPlayer DRIVING THE REAL SLOTS, the seam
    // FightBeatPlayerFixtureTests opened. The scene's own controller is left
    // alone: this needs one chosen actor swinging one chosen skill at one
    // chosen target, which a real turn order will not hand over on demand,
    // and everything the measurement reads -- marks, depth scales, art,
    // ground lines -- belongs to the real scene either way.
    public class MeleeStandOffCaptureTests
    {
        private const ulong Seed = 20260904UL;

        // The contract: contact, and no clipping. A Lunge or a Close leaves
        // the authored gap; a Charge ends against what it hit.
        private const float MinGap = 0f;
        private const float MaxGap = 30f;

        private FightController _fight;
        private string _root;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<string> _report = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRootWithARealSquad()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 2f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 0f;

            _root = Path.Combine(Path.GetTempPath(), "pp-standoff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            SaveData.TestSquadOfThreeEnabled = true;
            RunManager.StartRun(Seed);
        }

        [TearDown]
        public void Restore()
        {
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- the stage, as this fixture reads it ------------------------------

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private static Canvas RootCanvas() =>
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);

        private sealed class Stand
        {
            public string Name;
            public string Combatant;
            public RectTransform Slot;
            public StageActorAnimator Animator;
            public Image Sprite;
        }

        private Stand[] Occupied(string prefix)
        {
            var found = new List<Stand>();
            for (int i = 0; i < 8; i++)
            {
                var go = Named($"{prefix}{i}Slot");
                if (go == null || !go.activeSelf) continue;

                found.Add(new Stand
                {
                    Name = $"{prefix}{i}",
                    Combatant = Named($"{prefix}{i}Nameplate")?.GetComponent<TMPro.TMP_Text>()?.text,
                    Slot = go.GetComponent<RectTransform>(),
                    Animator = go.GetComponent<StageActorAnimator>(),
                    Sprite = Named($"{prefix}{i}Sprite")?.GetComponent<Image>(),
                });
            }

            return found.ToArray();
        }

        // The opaque x-range of whatever drawing is in this slot right now, in
        // canvas pixels.
        //
        // THE TRIMMED RECT AS A FRACTION OF THE UNTRIMMED ONE, applied to the
        // Image's own rendered bounds -- which folds in the depth scale, the
        // mirror and the live travel offset without this fixture restating any
        // of them. (A Tight sprite mesh crops the stored texture down to the
        // alpha box; textureRectOffset is where that box sits inside the
        // authored canvas, and forgetting it is the bug
        // FightController.StageVisuals' FootBandCentreFraction documents.)
        private static (float Left, float Right) OpaqueX(Stand stand)
        {
            var image = stand.Sprite;
            var sprite = image.sprite;
            var canvasRect = (RectTransform)image.canvas.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, image.rectTransform);

            float canvasWidth = sprite.rect.width;
            float fracLeft = sprite.textureRectOffset.x / canvasWidth;
            float fracRight = (sprite.textureRectOffset.x + sprite.textureRect.width) / canvasWidth;

            bool mirrored = image.rectTransform.localScale.x < 0f;
            float l = mirrored ? 1f - fracRight : fracLeft;
            float r = mirrored ? 1f - fracLeft : fracRight;

            return (bounds.min.x + l * bounds.size.x, bounds.min.x + r * bounds.size.x);
        }

        // ---- the fixture ------------------------------------------------------

        private IEnumerator OpenTheScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            // The bootstrap's own opening beats are still draining; left
            // running they would repaint the stage under this fixture's feet.
            var scenePlayer = UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>();
            if (scenePlayer != null) scenePlayer.Flush();
            yield return null;
            yield return null;
        }

        private FightBeatPlayer PlayerDriving(Stand actor, Stand target,
                                              CombatantState actorState, CombatantState targetState)
        {
            var go = new GameObject("StandOffPlayer");
            _spawned.Add(go);
            var player = go.AddComponent<FightBeatPlayer>();

            player.WireStageForTest(
                c => ReferenceEquals(c, actorState) ? actor.Slot
                    : ReferenceEquals(c, targetState) ? target.Slot : null,
                c => ReferenceEquals(c, actorState) ? actor.Animator
                    : ReferenceEquals(c, targetState) ? target.Animator : null);

            // THE POSE, APPLIED FOR REAL. Each figure stays in its own slot, so
            // "wear this stance" is one sprite swap: same canvas, same ground
            // line, same mirror, all of which the controller already set up.
            // Without this the capture would photograph an idle standing at a
            // stand-off measured for a slam, which is the one thing this
            // fixture must not do.
            player.WireStancesForTest(
                (c, stance) =>
                {
                    var stand = ReferenceEquals(c, actorState) ? actor
                        : ReferenceEquals(c, targetState) ? target : null;
                    if (stand?.Sprite == null) return;

                    var art = StanceAnimationLibrary.Resolve(FolderOf(stand), stance);
                    if (art != null) stand.Sprite.sprite = art;
                },
                (_, __) => { }, () => { });

            return player;
        }

        // The party's three folders, by the name on the nameplate. A fixture
        // constant rather than a controller lookup: SpriteFolderFor is private
        // to FightController, and the three characters this run fields are
        // fixed by SaveData.TestSquadOfThreeEnabled.
        private static string FolderOf(Stand stand)
        {
            switch (stand.Combatant)
            {
                case "Shawn": return "Characters/sheep";
                case "Bjorn": return "Characters/bear";
                case "Odette": return "Characters/owl";
                default: return null;
            }
        }

        private static CombatBeat Beat(CombatantState actor, CombatantState target,
                                       StageApproach approach, string approachStance,
                                       string windupStance, string strike)
        {
            var beat = new CombatBeat
            {
                Actor = actor,
                Target = target,
                Amount = 12,
                Approach = approach,
                ActorApproachStance = approachStance,
                ActorWindupStance = windupStance,
            };

            if (!string.IsNullOrWhiteSpace(strike)) beat.Stances[actor] = strike;
            return beat;
        }

        // One frame of the beat, as this fixture judges it.
        private struct Extent
        {
            public float Travel;
            public float Gap;
            public string Wearing;
            public Vector2 StoodAt;
            public string Diagnostic;
        }

        // Plays the beat and keeps the frame that matters: the actor at FULL
        // EXTENT, wearing the drawing it hits with.
        //
        // Both halves are load-bearing and neither alone is enough.
        //
        // "The frame it starts wearing its strike" is wrong for a plain
        // Lunge: it authors no phase poses, so CombatBeat's precedence puts
        // the strike on from the OPENING frame, before the figure has moved.
        //
        // "The frame of maximum travel" is wrong for a Close: it arrives and
        // then HOLDS, so the first frame at full extent is the one still
        // wearing the approach pose -- narrower than the drawing the
        // stand-off was measured against, which reads several pixels wider
        // than the gap that matters.
        //
        // So: the running maximum travel is tracked and never lowered (a
        // threshold that follows the figure back home walks the sample all
        // the way to its mark, which is the shape of the first two attempts
        // at this), and among the frames within a pixel of it the one wearing
        // the strike wins.
        private IEnumerator AtFullExtent(FightBeatPlayer player, CombatBeat beat, Stand actor, Stand target,
                                         string strike, string capturePath, Canvas canvas)
        {
            bool finished = false;
            var mark = actor.Animator.Mark;

            // THE TARGET AT REST, READ BEFORE THE BLOW. It is recoiling and
            // being squashed on the very frame the strike goes on
            // (RecoilOne/Punch fire in the same guarded block), so a live
            // reading answers "how far did the target get shoved" as much as
            // "where did the attacker stop", and which of the two a sample
            // catches is a coin flip on frame timing -- measured at 12px on
            // one run and 74 on the next with nothing changed between them.
            // The contract is about where the attacker stops relative to
            // where the target STANDS.
            var targetBox = OpaqueX(target);

            float peak = -1f;
            var atPeak = new Extent();
            var atStrike = new Extent { Travel = -1f };

            player.Play(new[] { beat }, () => finished = true);

            float deadline = Time.realtimeSinceStartup + 15f;
            while (!finished && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (actor.Sprite == null || actor.Sprite.sprite == null) continue;

                float travel = Vector2.Distance(actor.Slot.anchoredPosition, mark);
                if (travel < peak - 1f) continue;

                var actorBox = OpaqueX(actor);
                var here = new Extent
                {
                    Travel = travel,
                    Gap = targetBox.Left - actorBox.Right,
                    Wearing = actor.Sprite.sprite.name,
                    StoodAt = actor.Slot.anchoredPosition,
                    Diagnostic = $"forward edge {actorBox.Right:F1}, target near edge {targetBox.Left:F1}",
                };

                if (travel > peak)
                {
                    peak = travel;
                    atPeak = here;
                }

                if (here.Wearing == strike && travel > atStrike.Travel) atStrike = here;
            }

            Assert.IsTrue(finished, "the beat never finished playing");
            _peak = atStrike.Travel >= 0f ? atStrike : atPeak;

            // Re-staged for the photograph rather than written on every frame
            // of the approach: the figure is home by now, so it is put back
            // where the sample says it stood, in what it was wearing.
            if (capturePath != null && canvas != null)
            {
                var art = StanceAnimationLibrary.Resolve(FolderOf(actor), _peak.Wearing);
                if (art != null) actor.Sprite.sprite = art;
                actor.Slot.anchoredPosition = _peak.StoodAt;
                Canvas.ForceUpdateCanvases();
                CanvasCapture.RenderToFile(canvas, capturePath, 1920, 1080);
            }
        }

        private Extent _peak;

        [UnityTest]
        public IEnumerator EveryMeleeApproachArrivesInFrontOfItsTarget_FromEverySlot()
        {
            yield return OpenTheScene();

            var party = Occupied("Party");
            var enemies = Occupied("Enemy");
            Assert.AreEqual(3, party.Length,
                "expected the real three-member party: " + string.Join(", ", party.Select(p => $"{p.Name}={p.Combatant}")));
            Assert.GreaterOrEqual(enemies.Length, 1, "no enemy is on stage to stand off from");

            var partyStates = _fight.Session.Encounter.PlayerParty;
            var target = enemies[0];
            var targetState = _fight.Session.Encounter.Enemies[0];

            bool canCapture = CanvasCapture.IsSupported;
            string dir = CaptureOutput.LabelDir("melee_standoff");
            if (canCapture)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                Directory.CreateDirectory(dir);
            }

            var canvas = RootCanvas();
            var failures = new List<string>();

            // Bjorn's Slam wherever Bjorn is standing, and a plain swing from
            // every slot the party occupies -- so the front, middle and back
            // ranks are all measured, which is the whole of the owner's first
            // complaint.
            foreach (var stand in party)
            {
                var actorState = partyStates.FirstOrDefault(c => c.Name == stand.Combatant);
                if (actorState == null || stand.Sprite == null || stand.Sprite.sprite == null) continue;

                bool isBjorn = stand.Combatant == "Bjorn";
                string strike = isBjorn ? "slam" : FightSession.Stances.Attack;

                var beat = isBjorn
                    ? Beat(actorState, targetState, StageApproach.Close, "rush", "overhead", "slam")
                    : Beat(actorState, targetState, StageApproach.Lunge, null, null, FightSession.Stances.Attack);

                var player = PlayerDriving(stand, target, actorState, targetState);
                string shot = canCapture && canvas != null
                    ? Path.Combine(dir, $"impact_{stand.Name}_{stand.Combatant}.png")
                    : null;

                yield return AtFullExtent(player, beat, stand, target, strike, shot, canvas);

                string line = $"{stand.Name} {stand.Combatant} {(isBjorn ? "Slam (Close)" : "Attack (Lunge)")} " +
                              $"from mark {stand.Animator.Mark.x:F0},{stand.Animator.Mark.y:F0} " +
                              $"-> stood at {_peak.StoodAt.x:F1},{_peak.StoodAt.y:F1} " +
                              $"wearing '{_peak.Wearing}'; GAP {_peak.Gap:F1}px [{_peak.Diagnostic}]";
                _report.Add(line);

                if (_peak.Gap < MinGap || _peak.Gap > MaxGap)
                {
                    failures.Add(line);
                }

                // Home again, and back into the drawing the stage put there,
                // before the next actor is measured against a stage this one
                // left leaning.
                stand.Animator.Play(Vector2.zero, 0f);
                var back = StanceAnimationLibrary.Resolve(FolderOf(stand), FightSession.Stances.Idle);
                if (back != null) stand.Sprite.sprite = back;
                var playerGo = player.gameObject;
                _spawned.Remove(playerGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
                yield return null;
            }

            if (canCapture)
            {
                File.WriteAllText(Path.Combine(dir, "gaps.txt"), string.Join("\n", _report));
                Debug.Log("[MeleeStandOff] " + string.Join("\n[MeleeStandOff] ", _report) + "\nwrote " + dir);
            }
            else
            {
                Debug.Log("[MeleeStandOff] " + string.Join("\n[MeleeStandOff] ", _report));
            }

            CollectionAssert.IsEmpty(failures,
                $"an attacker did not arrive in front of its target (want {MinGap}..{MaxGap}px of daylight " +
                "between the two opaque edges):\n  " + string.Join("\n  ", failures));
        }
    }
}
