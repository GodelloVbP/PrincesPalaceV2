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
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // WHERE AN ATTACKER ACTUALLY STOPS, PHOTOGRAPHED AT THE IMPACT FRAME.
    //
    // The owner, 2026-09-09: "when attacking from the middle and backline,
    // when you go in for a hit, you stop earlier and not in front of the
    // enemy", and "Slam for Bjorn clips into the enemy when he goes in".
    // Both are claims about the daylight between the two figures at the moment
    // the blow lands, and nothing measured that before this fixture.
    // StageStandOffTests pins the arithmetic in Domain; this proves the
    // arithmetic reaches the real stage, wearing the real art, at the real
    // marks.
    //
    // TWO GAPS PER BEAT SINCE 2026-09-15, because the owner's third complaint
    // is that the first two were answered against the wrong edge: Bjorn's
    // hammer stopped 12px from the TIP of the beetle's horn, about a body's
    // width in front of the beetle. The stand-off now closes on BODIES, so
    // that is the gap the contract is written against, and the gap between the
    // outermost drawn pixels is bounded above only -- weapons are supposed to
    // be inside the target at impact. See MinGap/MaxGap below.
    //
    // MEASURED OFF THE RENDERED TRANSFORMS, not off the production formula:
    // every edge is read as a FRACTION of each sprite's own untrimmed canvas
    // and applied to the Image's rendered bounds, so this agrees with
    // StageStandOff only if the stage really put the figure where the
    // arithmetic says. Re-deriving TravelTo here would assert that
    // multiplication is deterministic (CLAUDE.md gotcha 5), and so would
    // asking StageActorAnimator where the body edges are -- this fixture
    // counts the columns itself and shares only the threshold.
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

        // THE CONTRACT, IN TWO NUMBERS PER BEAT.
        //
        // The BODIES touch and do not interpenetrate: a Lunge or a Close
        // leaves the authored gap, a Charge ends against what it hit, and
        // neither ever leaves daylight you could park a third figure in. That
        // is MinGap..MaxGap, and it is measured between the two MASS edges,
        // which is where the stand-off aims.
        //
        // And there is NEVER DAYLIGHT between the two drawings. The tight-edge
        // gap is normally negative here -- the attacker's weapon is inside the
        // target's silhouette at the instant of the blow, which is the whole
        // point of closing on bodies rather than on outermost pixels -- so it
        // is bounded above only. A positive tight gap larger than MaxGap means
        // the two figures are visibly apart at impact whatever the bodies say.
        private const float MinGap = 0f;
        private const float MaxGap = 30f;

        private FightController _fight;
        private string _root;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<string> _report = new List<string>();

        // BOTH TESTS IN THIS FIXTURE WRITE INTO THE SAME "melee_standoff"
        // label directory, and NUnit does not promise to run them in
        // declaration order (observed: EnemiesArriveInFrontOfTheParty...
        // before EveryMeleeApproach..., alphabetically). Whichever runs
        // SECOND must not wipe what the first one just wrote -- exactly the
        // failure mode a naive "delete then recreate" has when two tests
        // share one directory. static rather than per-instance because NUnit
        // builds a fresh instance per [UnityTest]; [OneTimeSetUp] resets it
        // once per fixture RUN, so a second `dotnet test`/graphics_tests.ps1
        // invocation still gets a clean directory.
        private static bool _outputDirReady;

        [OneTimeSetUp]
        public static void ResetOutputDirFlag()
        {
            _outputDirReady = false;
        }

        // Wipes the label directory the FIRST time either test calls this in
        // a run, and leaves it alone (just ensures it exists) for whichever
        // test calls it second -- see _outputDirReady's own comment.
        private static void PrepareOutputDir(string dir)
        {
            if (!_outputDirReady)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                _outputDirReady = true;
            }

            Directory.CreateDirectory(dir);
        }

        // APPENDS, never overwrites -- gaps.txt is shared between both tests
        // in this fixture (see _outputDirReady), so whichever ran first left
        // real lines in it that the second must not erase. A leading blank
        // line only when there is already something to separate from.
        private static void AppendGapsReport(string dir, IReadOnlyList<string> lines)
        {
            string path = Path.Combine(dir, "gaps.txt");
            string text = string.Join("\n", lines);
            if (File.Exists(path)) text = "\n" + text;
            File.AppendAllText(path, text);
        }

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

        // BOTH PAIRS OF EDGES of whatever drawing is in this slot right now, in
        // canvas pixels. Tight is the outermost opaque pixel; Mass is where
        // the body ends, ignoring a horn, a tail, a raised weapon or a wingtip
        // (StageStandOff / OpaqueSpan.MassFraction argue why the stand-off
        // closes on the second pair).
        private struct Edges
        {
            public float TightLeft, TightRight, MassLeft, MassRight;
        }

        // EVERY EDGE AS A FRACTION OF THE UNTRIMMED CANVAS, applied to the
        // Image's own rendered bounds -- which folds in the depth scale, the
        // mirror and the live travel offset without this fixture restating any
        // of them. (A Tight sprite mesh crops the stored texture down to the
        // alpha box; textureRectOffset is where that box sits inside the
        // authored canvas, and forgetting it is the bug
        // FightController.StageVisuals' FootBandCentreFraction documents.)
        private static Edges EdgesOf(Stand stand)
        {
            var image = stand.Sprite;
            var sprite = image.sprite;
            var canvasRect = (RectTransform)image.canvas.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, image.rectTransform);

            float canvasWidth = sprite.rect.width;
            float tightLeft = sprite.textureRectOffset.x / canvasWidth;
            float tightRight = (sprite.textureRectOffset.x + sprite.textureRect.width) / canvasWidth;
            var mass = MassFractionsOf(sprite);

            bool mirrored = image.rectTransform.localScale.x < 0f;
            float Map(float fraction) =>
                bounds.min.x + (mirrored ? 1f - fraction : fraction) * bounds.size.x;

            return new Edges
            {
                TightLeft = Map(mirrored ? tightRight : tightLeft),
                TightRight = Map(mirrored ? tightLeft : tightRight),
                MassLeft = Map(mirrored ? mass.Right : mass.Left),
                MassRight = Map(mirrored ? mass.Left : mass.Right),
            };
        }

        // THE FIXTURE'S OWN COLUMN COUNT, deliberately not
        // StageActorAnimator's. Only the THRESHOLD is shared
        // (OpaqueSpan.MassFraction), because a number with two homes rots;
        // finding the edge is re-derived here, or this would assert that a
        // method equals itself (CLAUDE.md gotcha 5).
        //
        // Cached per Sprite because this fixture plays seven beats over a
        // roster of six actors and GetPixels on an 830x413 canvas is not free.
        private static readonly Dictionary<Sprite, (float Left, float Right)> MassBox =
            new Dictionary<Sprite, (float Left, float Right)>();

        private static (float Left, float Right) MassFractionsOf(Sprite sprite)
        {
            if (MassBox.TryGetValue(sprite, out var cached)) return cached;

            // ASSERTED, NEVER SKIPPED PAST. StanceSpriteImporter forces every
            // stance texture readable; a stance that arrives here unreadable
            // is a broken import, and a fixture that quietly fell back to the
            // tight box would still go green while measuring the wrong thing.
            Assert.IsTrue(sprite.texture != null && sprite.texture.isReadable,
                $"stance texture '{sprite.name}' is not readable -- StanceSpriteImporter should have forced it");

            var crop = sprite.textureRect;
            int cropWidth = Mathf.RoundToInt(crop.width);
            int cropHeight = Mathf.RoundToInt(crop.height);
            Assert.Greater(cropWidth, 0, $"'{sprite.name}' has an empty texture rect");

            var pixels = sprite.texture.GetPixels(
                Mathf.RoundToInt(crop.x), Mathf.RoundToInt(crop.y), cropWidth, cropHeight);

            var rows = new int[cropWidth];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.02f) rows[i % cropWidth]++;
            }

            float threshold = rows.Max() * OpaqueSpan.MassFraction;
            int first = Array.FindIndex(rows, count => count >= threshold);
            int last = Array.FindLastIndex(rows, count => count >= threshold);
            Assert.GreaterOrEqual(first, 0, $"'{sprite.name}' has no opaque column at all");

            float canvasWidth = sprite.rect.width;
            float offsetX = sprite.textureRectOffset.x;
            var box = ((offsetX + first) / canvasWidth, (offsetX + last + 1) / canvasWidth);
            MassBox[sprite] = box;
            return box;
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

        // The party's three folders, by the name on the nameplate, PLUS the
        // three enemies EnemiesArriveInFrontOfTheParty_AndPartyReachesThroughToTheBackRank
        // fields below it -- same reasoning either side of the line:
        // SpriteFolderFor is private to FightController, and both rosters are
        // fixed (the party by SaveData.TestSquadOfThreeEnabled, the enemies by
        // that test's own FightEncounterAdapter.Build call), so a constant
        // keyed off the nameplate is cheaper than reaching into the controller
        // for something this fixture already knows.
        private static string FolderOf(Stand stand)
        {
            switch (stand.Combatant)
            {
                case "Shawn": return "Characters/sheep";
                case "Bjorn": return "Characters/bear";
                case "Odette": return "Characters/owl";
                case "Giant Rat": return "Enemies/rat";
                case "Ironback Beetle": return "Enemies/beetle";
                case "Forest Troll": return "Enemies/forest_warden";
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
            public float MassGap;
            public float TightGap;
            public string Wearing;
            public Vector2 StoodAt;
            public string Diagnostic;

            public bool Honours(float min, float max) =>
                MassGap >= min && MassGap <= max && TightGap <= max;
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

            // WHICH WAY THIS BEAT CROSSES THE STAGE, the same sign
            // StageStandOff.TravelTo itself keys off of (actor/target HOME
            // marks, not live position). Every beat this fixture played until
            // EnemiesArriveInFrontOfTheParty_AndPartyReachesThroughToTheBackRank
            // was party-on-enemy, where the actor always starts left of its
            // target -- so "forward" was always the actor's own RIGHT edge and
            // "near" was always the target's LEFT, and the two constants below
            // could be, and were, hardcoded that way. An enemy attacking the
            // party approaches from the OTHER side, and measuring the same two
            // fixed edges there reads off both figures' BACKS instead of their
            // facing sides -- not daylight, just two wrong numbers that happen
            // to still subtract into something. Bug found the hard way: the
            // first run of the enemy-actor beats below reported three-figure
            // negative gaps (-514, -460, -669px) that looked like a production
            // stand-off failure until the diagnostic showed both edges were on
            // the wrong side of each figure.
            bool actorApproachesFromTheLeft = target.Animator.Mark.x >= actor.Animator.Mark.x;

            // THE TARGET AT REST, READ BEFORE THE BLOW. It is recoiling and
            // being squashed on the very frame the strike goes on
            // (RecoilOne/Punch fire in the same guarded block), so a live
            // reading answers "how far did the target get shoved" as much as
            // "where did the attacker stop", and which of the two a sample
            // catches is a coin flip on frame timing -- measured at 12px on
            // one run and 74 on the next with nothing changed between them.
            // The contract is about where the attacker stops relative to
            // where the target STANDS.
            //
            // AT REST NOW HAS TO BE WAITED FOR, not assumed. Both tests below
            // play their beats back to back in a loop, and since 2026-09-19 a
            // struck figure's reel runs 1.505s (FightBeatPlayer's
            // RecoilDwellSeconds plus RecoilReturnSeconds) and deliberately
            // outlives the beat that caused it -- so the target of the
            // PREVIOUS beat is still shoved RecoilDistance back when the next
            // one opens, and reading its edges there measures the recoil
            // rather than the stand-off. Every beat after the first reported
            // exactly 45px of phantom daylight before this wait existed.
            //
            // THE SAME WAIT THE GAME MAKES, one step earlier. FightBeatPlayer
            // holds a beat whose actor or target is still reeling before it
            // measures either of their marks (see StillReeling), so
            // production never lands a blow in that daylight either. This
            // fixture cannot lean on that gate, because it reads the target's
            // edges BEFORE handing the beat over -- so it waits for the same
            // condition itself rather than photographing a body mid-flinch
            // and calling the difference a stand-off.
            float restDeadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < restDeadline
                   && (target.Animator.IsPlaying || actor.Animator.IsPlaying
                       || Vector2.Distance(target.Slot.anchoredPosition, target.Animator.Mark) > 0.01f
                       || Vector2.Distance(actor.Slot.anchoredPosition, actor.Animator.Mark) > 0.01f))
            {
                yield return null;
            }

            var targetBox = EdgesOf(target);
            float targetNearMass = actorApproachesFromTheLeft ? targetBox.MassLeft : targetBox.MassRight;
            float targetNearTight = actorApproachesFromTheLeft ? targetBox.TightLeft : targetBox.TightRight;

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

                var actorBox = EdgesOf(actor);
                float actorForwardMass = actorApproachesFromTheLeft ? actorBox.MassRight : actorBox.MassLeft;
                float actorForwardTight = actorApproachesFromTheLeft ? actorBox.TightRight : actorBox.TightLeft;
                float Daylight(float forward, float near) =>
                    actorApproachesFromTheLeft ? near - forward : forward - near;

                var here = new Extent
                {
                    Travel = travel,
                    MassGap = Daylight(actorForwardMass, targetNearMass),
                    TightGap = Daylight(actorForwardTight, targetNearTight),
                    Wearing = actor.Sprite.sprite.name,
                    StoodAt = actor.Slot.anchoredPosition,
                    Diagnostic = $"body edges {actorForwardMass:F1} -> {targetNearMass:F1}, " +
                                 $"drawn edges {actorForwardTight:F1} -> {targetNearTight:F1}",
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
            if (canCapture) PrepareOutputDir(dir);

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
                              $"wearing '{_peak.Wearing}'; BODY GAP {_peak.MassGap:F1}px, DRAWN GAP {_peak.TightGap:F1}px " +
                              $"[{_peak.Diagnostic}]";
                _report.Add(line);

                if (!_peak.Honours(MinGap, MaxGap))
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
                AppendGapsReport(dir, _report);
                Debug.Log("[MeleeStandOff] " + string.Join("\n[MeleeStandOff] ", _report) + "\nwrote " + dir);
            }
            else
            {
                Debug.Log("[MeleeStandOff] " + string.Join("\n[MeleeStandOff] ", _report));
            }

            CollectionAssert.IsEmpty(failures,
                $"an attacker did not arrive in front of its target (want {MinGap}..{MaxGap}px between " +
                $"the two BODIES, and never more than {MaxGap}px between the two DRAWINGS):\n  " +
                string.Join("\n  ", failures));
        }

        // WHERE AN ENEMY STOPS HITTING THE PARTY, and where a party attacker
        // stops reaching PAST the front two ranks to hit the third. The test
        // above only ever measures party-on-enemy[0]; the owner's complaint
        // ("you stop earlier ... when you go in for a hit") says nothing about
        // which side is swinging, and nothing here had photographed an enemy's
        // own approach, or an attacker passing a knot of several targets to
        // reach the one standing at the back.
        //
        // A FIXED THREE-ENEMY ENCOUNTER, not whatever Seed's own room rolls --
        // built through FightEncounterAdapter.Build, the SAME content pipeline
        // RunManager.StartFight uses for a real fight, so each monster keeps
        // its own authored kit rather than a fixture's guess at one. This is
        // also why the seed's natural room is irrelevant here: OpenTheScene
        // still opens it (the bootstrap needs a live run to drain), and this
        // rebinds _fight straight over it the moment the scene is stable,
        // exactly the way EnemyStanceCaptureTests already does with no
        // production seam added for this file.
        //
        // THREE DIFFERENT SPRITE SIZES, so the middle case ("attacking from
        // the middle and backline") and the largest art in the roster
        // (Forest Troll, stageScale 1.5) both get a frame. Picked from the
        // four enemies with stance art at all (rat, beetle, treant,
        // forest_warden): the rat and the beetle for the small end, and the
        // Troll over the Elder Treant for the large end because 1.5 > 1.45 and
        // its "attack" stance already exists (the Treant's own strike,
        // "trunk_slam", would have worked too, but the Troll is strictly the
        // bigger claim about clipping).
        [UnityTest]
        public IEnumerator EnemiesArriveInFrontOfTheParty_AndPartyReachesThroughToTheBackRank()
        {
            yield return OpenTheScene();

            var built = FightEncounterAdapter.Build(
                new[] { "sheep", "bear", "owl" },
                new[] { "rat", "beetle", "forest_warden" },
                new SeededRandom(Seed));
            Assert.IsNotNull(built,
                "content lookup failed building sheep/bear/owl vs rat/beetle/forest_warden -- check those ids still exist");

            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            var party = Occupied("Party");
            var enemies = Occupied("Enemy");
            Assert.AreEqual(3, party.Length,
                "expected the three-member party this fixture just bound: " + string.Join(", ", party.Select(p => p.Combatant)));
            Assert.AreEqual(3, enemies.Length,
                "expected the three enemies this fixture just bound (rat, beetle, forest troll): " +
                string.Join(", ", enemies.Select(e => e.Combatant)));

            var partyStates = built.Session.Encounter.PlayerParty;
            var enemyStates = built.Session.Encounter.Enemies;

            // sheep is first in the partyIds this test passed to Build, so
            // Shawn is the front-rank party member -- the same "list order is
            // rank order" rule CombatEncounter.FrontEnemy documents for the
            // enemy side applies to PlayerParty too.
            var frontParty = party[0];
            var frontPartyState = partyStates[0];
            Assert.AreEqual("Shawn", frontParty.Combatant, "sheep should have landed the party's front rank");

            bool canCapture = CanvasCapture.IsSupported;
            string dir = CaptureOutput.LabelDir("melee_standoff");
            if (canCapture) PrepareOutputDir(dir);

            var canvas = RootCanvas();
            var failures = new List<string>();
            var report = new List<string>();

            // Each enemy's OWN authored approach and strike, read off
            // enemies.json/skills.json exactly as Beat()'s own call sites do
            // for Bjorn's Slam above -- none of these three authors an
            // approachStance or windupStance of its own (only Bjorn's Slam
            // does), so every beat here passes null for both, same as the
            // party's own plain Attack (Lunge) does in the test above.
            //   rat: no authored ability at all -- the plain-swing path,
            //     which lunges and wears "attack" (FightSession.Stances.Attack)
            //     exactly like an unarmed party member's own basic Attack.
            //   beetle: Barrel Roll -- approach "charge", stance "turtle_up".
            //   forest_warden (Forest Troll): Overhead Slam -- approach
            //     "close", stance "attack".
            var attacks = new (string Label, StageApproach Approach, string Strike)[]
            {
                ("Attack (Lunge)", StageApproach.Lunge, FightSession.Stances.Attack),
                ("Barrel Roll (Charge)", StageApproach.Charge, "turtle_up"),
                ("Overhead Slam (Close)", StageApproach.Close, FightSession.Stances.Attack),
            };

            for (int i = 0; i < enemies.Length; i++)
            {
                var actor = enemies[i];
                var actorState = enemyStates[i];
                var (label, approach, strike) = attacks[i];

                var beat = Beat(actorState, frontPartyState, approach, null, null, strike);
                var player = PlayerDriving(actor, frontParty, actorState, frontPartyState);
                string shot = canCapture && canvas != null
                    ? Path.Combine(dir, $"impact_Enemy{i}_{actor.Combatant.Replace(" ", "")}.png")
                    : null;

                yield return AtFullExtent(player, beat, actor, frontParty, strike, shot, canvas);

                string line = $"{actor.Name} {actor.Combatant} {label} at Party0 Shawn " +
                              $"from mark {actor.Animator.Mark.x:F0},{actor.Animator.Mark.y:F0} " +
                              $"-> stood at {_peak.StoodAt.x:F1},{_peak.StoodAt.y:F1} " +
                              $"wearing '{_peak.Wearing}'; BODY GAP {_peak.MassGap:F1}px, DRAWN GAP {_peak.TightGap:F1}px " +
                              $"[{_peak.Diagnostic}]";
                report.Add(line);

                if (!_peak.Honours(MinGap, MaxGap))
                {
                    failures.Add(line);
                }

                // Home again, wearing its own idle, before the next enemy is
                // measured against a stage this one left leaning -- the same
                // reset the test above does for each party actor in turn.
                actor.Animator.Play(Vector2.zero, 0f);
                var back = StanceAnimationLibrary.Resolve(FolderOf(actor), FightSession.Stances.Idle);
                if (back != null) actor.Sprite.sprite = back;
                var playerGo = player.gameObject;
                _spawned.Remove(playerGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
                yield return null;
            }

            // Shawn reaching PAST the rat and the beetle to hit the Troll
            // standing third -- the one shape nothing above exercises, since
            // every beat in the loop lands on the front rank. Same plain
            // Attack (Lunge) the party's own basic swing always uses.
            var backEnemy = enemies[2];
            var backEnemyState = enemyStates[2];
            {
                var beat = Beat(frontPartyState, backEnemyState, StageApproach.Lunge, null, null, FightSession.Stances.Attack);
                var player = PlayerDriving(frontParty, backEnemy, frontPartyState, backEnemyState);
                string shot = canCapture && canvas != null
                    ? Path.Combine(dir, "impact_Party0_Shawn_backrank.png")
                    : null;

                yield return AtFullExtent(player, beat, frontParty, backEnemy, FightSession.Stances.Attack, shot, canvas);

                string line = $"Party0 Shawn Attack (Lunge) at back rank {backEnemy.Name} {backEnemy.Combatant} " +
                              $"from mark {frontParty.Animator.Mark.x:F0},{frontParty.Animator.Mark.y:F0} " +
                              $"-> stood at {_peak.StoodAt.x:F1},{_peak.StoodAt.y:F1} " +
                              $"wearing '{_peak.Wearing}'; BODY GAP {_peak.MassGap:F1}px, DRAWN GAP {_peak.TightGap:F1}px " +
                              $"[{_peak.Diagnostic}]";
                report.Add(line);

                if (!_peak.Honours(MinGap, MaxGap))
                {
                    failures.Add(line);
                }

                frontParty.Animator.Play(Vector2.zero, 0f);
                var back = StanceAnimationLibrary.Resolve(FolderOf(frontParty), FightSession.Stances.Idle);
                if (back != null) frontParty.Sprite.sprite = back;
                var playerGo = player.gameObject;
                _spawned.Remove(playerGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }

            // APPENDED, not overwritten -- see _outputDirReady/AppendGapsReport:
            // NUnit does not promise this runs after (or before) the test
            // above, and both write into the same "melee_standoff" label
            // directory.
            if (canCapture)
            {
                AppendGapsReport(dir, report);
                Debug.Log("[MeleeStandOff] " + string.Join("\n[MeleeStandOff] ", report) + "\nwrote " + dir);
            }
            else
            {
                Debug.Log("[MeleeStandOff] " + string.Join("\n[MeleeStandOff] ", report));
            }

            CollectionAssert.IsEmpty(failures,
                $"an attacker did not arrive in front of its target (want {MinGap}..{MaxGap}px between " +
                $"the two BODIES, and never more than {MaxGap}px between the two DRAWINGS):\n  " +
                string.Join("\n  ", failures));
        }
    }
}
