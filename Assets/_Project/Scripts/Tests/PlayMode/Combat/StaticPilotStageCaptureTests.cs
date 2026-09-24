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
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // ONE BEAT OF ONE MONSTER'S PLAIN SWING, PHOTOGRAPHED AT NORMAL SPEED.
    //
    // The static-combat pilot (docs/archive/STATIC_COMBAT_ART_DEEP_DIVE.md, "Recommended
    // pilot") asks a question no existing fixture answers: does an ordinary
    // melee blow by a still-art creature READ as a blow at the speed a player
    // actually sees it? Every other capture in this suite is a single still
    // (EnemyStanceCaptureTests, FightMenuCaptureTests) or runs the fight at
    // 8x-60x to keep the suite quick, and both of those throw away the only
    // thing under examination here, which is timing.
    //
    // So this is a frame SERIES at BeatSpeedMultiplier 1, written to
    // tools/screenshots/runtime/static_pilot/<PP_CAPTURE_LABEL>/f{i}.png plus a
    // timing.json, and stitched into a contact strip and a real-time GIF by
    // tools/capture_strip.py. The label is an environment variable so the same
    // fixture records the "before" and the "after" of an art change and the two
    // are directly comparable frame for frame.
    //
    // THE SUBJECT IS FIXED, AND STAYS FIXED. Every other hand-kept list in
    // these capture fixtures became content-derived -- the stance kits, the
    // spell list -- because a list is the thing that stops covering what was
    // added after it. This one did not, and the reason is in the paragraph
    // above: the whole instrument is a BEFORE and an AFTER of the same art,
    // stitched frame for frame. A subject that moved to whichever monster
    // happens to sort first would make the two strips incomparable on the day
    // somebody adds a mob, which is the one thing this fixture must never do.
    //
    // The Bog Witch specifically, and the original reason for it has expired:
    // it was picked because every one of its stances was a single still, and
    // that is now true of every actor in the game (docs/ART_PIPELINE.md -- an
    // actor stance is one drawing, frame animation is not supported). What
    // keeps it here is continuity with the strips already taken against it.
    // Its art is asserted below rather than assumed, so a Bog Witch that lost
    // its stances fails by name instead of photographing a nameplate.
    public class StaticPilotStageCaptureTests
    {
        private const string EnemyId = "bog_witch";

        // 30fps, and it is the GAME clock rather than the wall clock -- see
        // Time.captureDeltaTime below.
        private const float SampleSeconds = 1f / 30f;

        // 1.4s: past the swinger's own settle by a wide margin, which is the
        // half of the pilot's four questions ("does the actor return cleanly
        // to formation?") that a shorter window cannot answer.
        //
        // AND IT STAYS 42 FRAMES, deliberately, even though since 2026-09-19
        // the strip no longer reaches the end of the STRUCK figure's reel: a
        // hit figure dwells 0.81s at full extent and takes another 0.64s to
        // spring home (FightBeatPlayer's RecoilDwellSeconds and
        // RecoilReturnSeconds, the owner's "4x as slow"), which lands 45
        // frames past the impact. Frame-for-frame comparability with every
        // strip taken before it is the stated reason this fixture names one
        // fixed subject at all, and a longer strip would forfeit that for
        // nine more frames of a figure easing home. TheBeat's settle tail
        // carries the return instead, and asserts it.
        //
        // The window is longer than the fight leaves free. TheBeat stops
        // playback the moment the SWINGER is done instead of relying on a
        // short window; see its Flush call.
        private const int FrameCount = 42;

        // A slot is back at its mark or it is not; the tween assigns the home
        // position outright rather than easing into it.
        private const float MarkTolerance = 0.01f;

        private static string LabelDir => CaptureOutput.LabelDir("static_pilot");

        private FightController _fight;
        private FightBeatPlayer _player;
        private CombatantState _hero;
        private CombatantState _witch;

        // NORMAL SPEED IS THE WHOLE POINT, so this fixture states it rather
        // than inheriting whatever the previous class left behind: every other
        // fight test in this suite sets a multiplier in its own [SetUp], and a
        // leaked 60x would silently turn this capture into four frames.
        //
        // docs/archive/PLAN_BATTLE_SPEED.md G3: BeatSpeedMultiplier == 1 no longer
        // guarantees Pace == 1 by itself -- FightBootstrap can install a
        // settings-backed PlayerSpeedMultiplier on top of it -- so both are
        // pinned now.
        [SetUp]
        public void RealTime()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureDeltaTime = 0f;
        }

        // ---- the fixture ------------------------------------------------------

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private static Canvas RootCanvas() =>
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);

        // Opens the scene, throws away the bootstrap's own fight, and stands the
        // witch opposite Shawn.
        //
        // THE PLAIN SWING IS FORCED BY THE POOL, not by a seed. EnemyKit's
        // constructor already takes the ability list (it only falls back to
        // building one from the definition when handed none), so a pool holding
        // exactly one entry -- the plain swing every pool carries anyway --
        // makes EnemyAbilityDraw.Pick's weighted draw return index 0 whatever
        // roll it is given. The witch's own bog_mud_burst (weight 2 against
        // attackWeight 3) is simply not in the pool to be drawn, so this cannot
        // drift when the RNG's consumption order changes upstream. Nothing in
        // production is touched to achieve it.
        //
        // Everything else about the monster is the REAL resolved content --
        // stats, sprite path, stage scale, and the Lunge approach its blank
        // attackApproach parses to -- so the capture shows what the game fields.
        private IEnumerator StandTheFixtureUp()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _player = UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_player, "the Fight scene has no FightBeatPlayer");

            // The scene's own FightBootstrap has already started a fight and is
            // playing its opening beats. Left running, its playback holds
            // FightController busy and the potion click below is swallowed.
            _player.Flush();
            yield return null;
            yield return null;

            var enemy = ContentDatabase.Enemies.FirstOrDefault(e => e.id == EnemyId);
            Assert.IsNotNull(enemy, $"'{EnemyId}' is not in the content database");

            var shawn = ContentDatabase.Characters
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath));
            Assert.IsNotNull(shawn, "no character has battle art, so the target would be a grey plate");

            // Speed 1 against the witch's authored 8, so the monster wins
            // initiative and FightSession.Begin resolves its swing into the beat
            // queue before the player ever gets a turn. That queued beat is then
            // the FIRST thing drained when the player acts, which is what lets
            // the capture start on the witch's own beat rather than a third of a
            // second into someone else's.
            //
            // 500 health so it survives the blow: a kill would route the target
            // to its defeated pose and this is a capture of a hit, not a death.
            var resolved = FightEncounterAdapter.Resolve(enemy);

            _hero = new CombatantState(shawn.Data.DisplayName, true, 500, 30, 20, 1);
            _witch = new CombatantState(resolved.DisplayName, false, 5000, 0,
                                        resolved.BaseStats.attack, resolved.BaseStats.speed);

            var plainSwingOnly = new List<EnemyAbility>
            {
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f),
            };

            var session = new FightSession(
                new CombatEncounter(new[] { _hero }, new[] { _witch }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(resolved, false, plainSwingOnly) },
                new Domain.Rng.SeededRandom(7));

            session.Begin();

            // ASSERTED RATHER THAN SKIPPED PAST (docs/CODE_STANDARDS.md §8): if
            // initiative ever stops going on Speed the witch's beat is not in
            // the queue and this fixture would quietly photograph an empty
            // stage.
            Assert.Less(_hero.CurrentHealth, _hero.MaxHealth,
                "the witch did not win initiative, so its swing is not queued and there is no beat to capture");

            // ONE MANA ELIXIR, and the hero is already at full mana -- see the
            // click in TheBeat for why this stands in for the Hold Back that
            // used to open the window.
            _fight.Bind(session, EncounterClass.Normal, new List<SatchelStack>
            {
                new SatchelStack("elixir", "Elixir", 1, true),
            });
            _fight.BindPartyArt(new[] { _hero }, new[] { shawn.Data.BattleSpritePath });

            yield return null;
            yield return null;
        }

        // ---- what one sampled frame knows ------------------------------------

        private struct Sample
        {
            public int PopupsBusy;
            public Vector2 EnemyAt;
            public Vector3 EnemyScale;
            public Vector2 PartyAt;
            public string EnemyStanceSprite;
        }

        private sealed class Recording
        {
            public readonly List<Sample> Samples = new List<Sample>();
            public RectTransform EnemySlot;
            public RectTransform PartySlot;
            public StageActorAnimator EnemyAnimator;
            public StageActorAnimator PartyAnimator;
            public string IdleSprite;

            // Every frame on which the busy-popup count ROSE. A popup is taken
            // out of the pool on the exact frame FightBeatPlayer lands the blow
            // (ShowAmount runs in the same guarded block as FlashTarget), and it
            // stays out for the whole of its rise -- so the rise of the count,
            // not its value, is the impact instant.
            public readonly List<int> ImpactFrames = new List<int>();

            // How far each figure actually travelled from its mark, in canvas
            // pixels. The pilot's whole question is whether a still-art blow
            // READS as a blow, and these two numbers are the measurable half of
            // it -- a before/after that changes nothing here changed nothing
            // about the staging either.
            public float EnemyTravelPeak;
            public float PartyTravelPeak;

            // The frame on which playback was stopped, once the beat had
            // settled. Everything after it is a deliberately still stage.
            public int StoppedAt = -1;
        }

        private Sample Read(Recording rec)
        {
            int busy = _player.Popups.Count(p => p != null && !p.IsFree);
            var sprite = rec.EnemySlot.GetComponentsInChildren<Image>(includeInactive: true)
                .FirstOrDefault(i => i.gameObject.name.EndsWith("Sprite", StringComparison.Ordinal));

            return new Sample
            {
                PopupsBusy = busy,
                EnemyAt = rec.EnemySlot.anchoredPosition,
                EnemyScale = rec.EnemySlot.localScale,
                PartyAt = rec.PartySlot.anchoredPosition,
                EnemyStanceSprite = sprite == null || sprite.sprite == null ? "" : sprite.sprite.name,
            };
        }

        // Finds the one slot per side that actually has somebody standing in it.
        // The stage is built for FightHudSpec.StageSlotsPerSide and the
        // unoccupied slots are switched off, so "which index" is a runtime fact
        // rather than something this fixture is entitled to assume.
        private RectTransform OccupiedSlot(string prefix)
        {
            for (int i = 0; i < 8; i++)
            {
                var go = Named($"{prefix}{i}Slot");
                if (go != null && go.activeSelf) return go.GetComponent<RectTransform>();
            }

            return null;
        }

        // Runs the witch's beat and records one Sample per 1/30s of GAME time.
        //
        // Time.captureDeltaTime is what makes that sentence true. Writing a
        // frame costs real milliseconds, and Unity's clock is real time by
        // default, so a capture loop that simply sampled every frame would watch
        // the beat run away from it -- the harder the frame is to produce, the
        // more of the animation is missed between samples, which is precisely
        // backwards. Pinning the delta makes every rendered frame worth exactly
        // one sample interval regardless of how long it took to produce, so the
        // series is a fixed-rate recording of the beat and two runs are
        // comparable frame for frame.
        private IEnumerator TheBeat(Recording rec, Action<int> onFrame)
        {
            rec.EnemySlot = OccupiedSlot("Enemy");
            rec.PartySlot = OccupiedSlot("Party");
            Assert.IsNotNull(rec.EnemySlot, "no enemy slot is occupied");
            Assert.IsNotNull(rec.PartySlot, "no party slot is occupied");

            rec.EnemyAnimator = rec.EnemySlot.GetComponent<StageActorAnimator>();
            rec.PartyAnimator = rec.PartySlot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(rec.EnemyAnimator, "the enemy slot has no StageActorAnimator, so nothing can lunge");
            Assert.IsNotNull(rec.PartyAnimator, "the party slot has no StageActorAnimator, so nothing can recoil");

            rec.IdleSprite = Read(rec).EnemyStanceSprite;

            Time.captureDeltaTime = SampleSeconds;

            // A MANA ELIXIR ON A HERO WHO IS ALREADY AT FULL MANA, and the
            // choice is load-bearing in exactly the way HOLD BACK's used to
            // be: it reaches AfterResolution, which drains the queued beats,
            // so the witch's swing plays first -- and the player's own beat
            // that follows it restores nothing, so FightBeatPlayer.ShowAmount
            // returns before popping a number (`!Missed && Amount <= 0`).
            // That makes the rest of the capture window provably free of a
            // second blow, which is what lets the impact assertion below be an
            // exact count rather than a guess.
            //
            // WHY NOT MOVE, the verb that replaced HOLD BACK on row 3: this
            // party is one character, so there is nobody to trade places with
            // and Move is refused without spending a turn at all. Fielding a
            // second party member to make it legal would put a second figure
            // in the strip and change what the pilot is a picture of.
            var item = Named("Verb2");
            Assert.IsNotNull(item, "the fight scene has no Verb2 (ITEM)");
            item.GetComponent<Button>().onClick.Invoke();

            var row = Named("CharacterSkill0");
            Assert.IsNotNull(row, "the item submenu did not open its first row");
            row.GetComponent<Button>().onClick.Invoke();

            // The baseline the first frame is compared against, taken BEFORE the
            // click. Without it a popup still in flight from the scene's own
            // bootstrap fight reads as an impact on frame 0.
            var previous = Read(rec);

            for (int i = 0; i < FrameCount; i++)
            {
                yield return null;

                var sample = Read(rec);
                if (sample.PopupsBusy > previous.PopupsBusy) rec.ImpactFrames.Add(i);
                previous = sample;

                rec.Samples.Add(sample);
                rec.EnemyTravelPeak = Mathf.Max(rec.EnemyTravelPeak,
                    Vector2.Distance(sample.EnemyAt, rec.EnemyAnimator.Home));
                rec.PartyTravelPeak = Mathf.Max(rec.PartyTravelPeak,
                    Vector2.Distance(sample.PartyAt, rec.PartyAnimator.Home));

                // ONE BEAT, and this is what holds the window to one.
                //
                // The fight does not pause between turns to be photographed:
                // the player's Hold Back is folded away (a beat with no target
                // and no amount records nothing to play) and the witch's NEXT
                // swing opens 0.8s after this one, well inside a window long
                // enough to show the settle. Stopping playback the moment both
                // figures are home leaves the rest of the capture a still stage
                // -- which is the correct picture of "she returned to her mark
                // and stayed there", and keeps the impact count below an exact
                // assertion rather than a range.
                //
                // Flush rather than a shorter window because the two are not
                // the same claim: a window that simply ended early could not
                // tell a figure that settled from one whose next beat happened
                // to start it moving again.
                if (rec.StoppedAt < 0 && rec.ImpactFrames.Count > 0 && i > rec.ImpactFrames[0]
                    && SwingOver(rec, sample))
                {
                    _player.Flush();
                    rec.StoppedAt = i;
                }

                onFrame?.Invoke(i);
            }

            Time.captureDeltaTime = 0f;

            // AND THEN PAST THE STRIP, until the figure the witch hit is back
            // on its mark.
            //
            // WHY A TAIL AND NOT A LONGER STRIP. The reel runs 1.505s from
            // the impact frame (FightBeatPlayer's RecoilDwellSeconds plus
            // RecoilReturnSeconds), which at this fixture's pinned 1/30
            // capture step is 45 frames AFTER the impact -- so a strip long
            // enough to contain it is about 51, and the 42 here is deliberate:
            // frame-for-frame comparability with every strip taken before is
            // the stated reason this fixture names one fixed subject at all,
            // and nine more frames of a figure easing home buys nothing to
            // look at.
            //
            // IT IS A COUNTABLE NUMBER OF FRAMES NOW, which it was not when
            // the reel's dwell was a WaitForSeconds: that ignored the pinned
            // captureDeltaTime the tweens either side of it obeyed, so the
            // struck figure sat 45px off its mark for all 72 frames of a 2.4s
            // strip and no frame count could have contained it. The dwell
            // steps by Time.deltaTime like everything else on the beat clock
            // as of 2026-09-19, so the tail below is bounded rather than a
            // race -- the deadline is a backstop, not the mechanism.
            //
            // APPENDED TO THE RECORDING BUT NOT TO THE STRIP: onFrame is not
            // called here, so the pictures on disk are exactly the 42 the
            // capture has always written, and only the assertions can see the
            // tail. Playback was flushed back at SwingOver, so nothing new can
            // land during it.
            float settleDeadline = Time.realtimeSinceStartup + 6f;
            while (true)
            {
                var tail = Read(rec);
                rec.Samples.Add(tail);
                rec.EnemyTravelPeak = Mathf.Max(rec.EnemyTravelPeak,
                    Vector2.Distance(tail.EnemyAt, rec.EnemyAnimator.Home));
                rec.PartyTravelPeak = Mathf.Max(rec.PartyTravelPeak,
                    Vector2.Distance(tail.PartyAt, rec.PartyAnimator.Home));

                if (AtRest(rec, tail) || Time.realtimeSinceStartup >= settleDeadline) break;
                yield return null;
            }
        }

        // The witch is posed idle again and both figures are standing on their
        // own marks -- the beat is over as far as the stage is concerned.
        private static bool AtRest(Recording rec, Sample s) =>
            SwingOver(rec, s)
            && Vector2.Distance(s.PartyAt, rec.PartyAnimator.Home) <= MarkTolerance;

        // THE SWINGER IS DONE, WHICH IS NOT THE SAME AS THE STAGE BEING AT
        // REST -- and since 2026-09-19 the two are far apart. A struck figure
        // reels for 1.505s (FightBeatPlayer's RecoilDwellSeconds plus
        // RecoilReturnSeconds, the owner's "4x as slow"), and the witch's
        // NEXT turn opens about 1.2s after the last one, so the party member
        // she hit is still shoved back when she winds up again: waiting for
        // the whole stage before calling Flush meant the window could never
        // hold exactly one blow, whatever its length. Measured on this
        // fixture, a 72-frame window stopped at rest photographed her
        // swinging twice, at frames 6 and 42.
        //
        // NOT THE SAME COMPLAINT THE GAME HAS, and the difference is worth
        // stating so this is not read as one. FightBeatPlayer holds a beat
        // whose own actor or target is still reeling (StillReeling), so no
        // blow in production is measured against a displaced body. What this
        // fixture needs is narrower and nothing production owes it: exactly
        // ONE blow in the window, whoever the next one would land on.
        //
        // So the two claims are separated. This one -- the witch idle, back
        // on her own mark -- is what stops the fight, because it is the last
        // frame of HER beat and nothing after it belongs to the pilot.
        // AtRest above still carries the claim the fixture exists to make,
        // that the struck figure comes home too, and SettledFrame still looks
        // for it; the reel just lands a good 30 frames later than it used to.
        private static bool SwingOver(Recording rec, Sample s) =>
            s.EnemyStanceSprite == rec.IdleSprite
            && Vector2.Distance(s.EnemyAt, rec.EnemyAnimator.Home) <= MarkTolerance;

        // The first frame after the blow on which that is true. -1 means it
        // never happened inside the window.
        private static int SettledFrame(Recording rec)
        {
            int after = rec.ImpactFrames.Count > 0 ? rec.ImpactFrames[0] : 0;

            for (int i = after + 1; i < rec.Samples.Count; i++)
            {
                if (AtRest(rec, rec.Samples[i])) return i;
            }

            return -1;
        }

        // A compact per-frame picture of the whole window, so a failure says
        // WHAT the stage was doing rather than only that a count was wrong.
        // "idle" is the drawing the witch wore before the beat opened; the
        // two numbers after it are how far SHE stood from her mark and how
        // far the figure she hit stood from ITS mark. The second one was
        // added 2026-09-19: with the reel four times longer than it was, the
        // struck figure is the half of the stage that decides whether this
        // window is long enough, and a timeline that only showed the swinger
        // could not say why a settle never arrived.
        private static string Timeline(Recording rec)
        {
            var line = new StringBuilder();
            for (int i = 0; i < rec.Samples.Count; i++)
            {
                var s = rec.Samples[i];
                line.Append(i).Append(':')
                    .Append(s.EnemyStanceSprite == rec.IdleSprite ? "idle" : s.EnemyStanceSprite)
                    .Append('/')
                    .Append(Mathf.RoundToInt(Vector2.Distance(s.EnemyAt, rec.EnemyAnimator.Home)))
                    .Append('/')
                    .Append(Mathf.RoundToInt(Vector2.Distance(s.PartyAt, rec.PartyAnimator.Home)))
                    .Append('/')
                    .Append(s.PopupsBusy)
                    .Append(' ');
            }

            return line.ToString();
        }

        private void AssertTheBeatBehaved(Recording rec)
        {
            Assert.AreEqual(1, rec.ImpactFrames.Count,
                "the window should hold exactly one blow -- the witch's. Impacts at frames: " +
                string.Join(", ", rec.ImpactFrames) + ". Timeline: " + Timeline(rec));

            Assert.IsTrue(_witch.IsAlive,
                "the witch died mid-capture, so the strip shows a corpse rather than a swing");

            Assert.IsTrue(rec.Samples.Any(s => s.EnemyStanceSprite != rec.IdleSprite),
                "the witch never left her idle drawing, so no attack pose reached the stage");

            Assert.Greater(rec.PartyTravelPeak, MarkTolerance,
                "the target never moved, so nothing on the stage said it had been hit");

            int settled = SettledFrame(rec);
            Assert.GreaterOrEqual(settled, 0,
                "the stage never returned to rest inside the window: the witch is still posed or a figure " +
                "is parked off its mark, which is the leak StageActorAnimator.ResetToHome exists to prevent. " +
                "Timeline: " + Timeline(rec));

            var last = rec.Samples[settled];
            Assert.AreEqual(rec.EnemyAnimator.BaseScale.x, last.EnemyScale.x,
                BreathCurve.FullAmplitude * 2f,
                "the witch is left deformed after her own swing -- the squash never unwound");
        }

        // ---- the assertions, which run in the commit gate ---------------------

        // Everything the capture depends on, checked WITHOUT pixels so the
        // headless suite still covers it. A capture test that self-skips under
        // -nographics and asserts nothing else would mean the pilot's fixture
        // could rot for weeks with the gate green.
        [UnityTest]
        public IEnumerator ThePlainSwingPlaysAndTheStageReturnsToRest()
        {
            var enemy = ContentDatabase.Enemies.FirstOrDefault(e => e.id == EnemyId);
            Assert.IsNotNull(enemy, $"'{EnemyId}' is not in the content database");

            foreach (string stance in new[] { "idle", "attack" })
            {
                Assert.IsNotNull(StanceAnimationLibrary.Resolve(enemy.Data.SpritePath, stance),
                    $"{EnemyId} has no '{stance}' art, so the pilot would photograph a nameplate");
            }

            yield return StandTheFixtureUp();

            var rec = new Recording();
            yield return TheBeat(rec, null);
            AssertTheBeatBehaved(rec);
        }

        // ---- the picture ------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureThePlainSwingAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 " +
                              "-Filter PrincesPalace.PlayModeTests.StaticPilotStageCaptureTests");
                yield break;
            }

            yield return StandTheFixtureUp();

            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");

            var rig = new StageCaptureRig(canvas, (int)FightStageAnchors.StageSize.X, (int)FightStageAnchors.StageSize.Y);
            var frames = new List<Texture2D>(FrameCount);
            var rec = new Recording();

            try
            {
                // Read back, do not encode. EncodeToPNG is the expensive half,
                // and doing it per frame stretches the wall clock the idle
                // breath (the one unscaled clock left on the stage) runs
                // against. The punch and the kick step by engine time now and
                // follow the pinned frame. The frames are held and written
                // once the beat is over.
                yield return TheBeat(rec, i => frames.Add(rig.Grab()));
            }
            finally
            {
                rig.Restore();
            }

            if (Directory.Exists(LabelDir)) Directory.Delete(LabelDir, recursive: true);
            Directory.CreateDirectory(LabelDir);

            for (int i = 0; i < frames.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(LabelDir, $"f{i}.png"), frames[i].EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frames[i]);
            }

            File.WriteAllText(Path.Combine(LabelDir, "timing.json"), TimingJson(rec, rig));
            Debug.Log($"[StaticPilotCapture] wrote {frames.Count} frames to {LabelDir}");

            AssertTheBeatBehaved(rec);
        }

        private string TimingJson(Recording rec, StageCaptureRig rig)
        {
            int impact = rec.ImpactFrames.Count > 0 ? rec.ImpactFrames[0] : -1;
            int settled = SettledFrame(rec);

            string Ms(int frame) => frame < 0
                ? "null"
                : (frame * SampleSeconds * 1000f).ToString("F1", CultureInfo.InvariantCulture);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"label\": \"{Environment.GetEnvironmentVariable("PP_CAPTURE_LABEL") ?? "unlabelled"}\",");
            json.AppendLine($"  \"enemyId\": \"{EnemyId}\",");
            json.AppendLine($"  \"beatSpeedMultiplier\": {FightBeatPlayer.BeatSpeedMultiplier.ToString("F2", CultureInfo.InvariantCulture)},");
            json.AppendLine($"  \"captureIntervalMs\": {(SampleSeconds * 1000f).ToString("F3", CultureInfo.InvariantCulture)},");
            json.AppendLine($"  \"frameCount\": {rec.Samples.Count},");
            json.AppendLine($"  \"impactFrame\": {impact},");
            json.AppendLine($"  \"impactMs\": {Ms(impact)},");
            json.AppendLine($"  \"impactFrames\": [{string.Join(", ", rec.ImpactFrames)}],");
            json.AppendLine($"  \"settledFrame\": {settled},");
            json.AppendLine($"  \"playbackStoppedFrame\": {rec.StoppedAt},");
            json.AppendLine($"  \"beatDurationMs\": {Ms(settled)},");
            json.AppendLine($"  \"cropX\": {rig.CropX}, \"cropY\": {rig.CropY},");
            json.AppendLine($"  \"cropWidth\": {rig.CropWidth}, \"cropHeight\": {rig.CropHeight},");
            json.AppendLine($"  \"attackerTravelPeakPx\": {rec.EnemyTravelPeak.ToString("F2", CultureInfo.InvariantCulture)},");
            json.AppendLine($"  \"targetTravelPeakPx\": {rec.PartyTravelPeak.ToString("F2", CultureInfo.InvariantCulture)}");
            json.Append("}");
            return json.ToString();
        }

    }
}
