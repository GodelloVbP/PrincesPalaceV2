using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Preview;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // A CAST, EVERY FRAME OF IT, IN REAL TIME.
    //
    // The brief asks for real-time in-game playback as evidence and says
    // screenshots supplement it. Four sampled stills cannot answer the two
    // questions the pilot is actually judged on -- does the ball read as heavy,
    // and does the splash arrive WITH the blow -- because both are about how
    // one instant follows another. This writes the whole cast as a frame
    // series; `ffmpeg -framerate 60 -i spell_prismatic_orb_f%02d.png out.mp4`
    // makes it a video, and the series is the thing this can produce.
    //
    // WHAT MAKES IT MEAN ANYTHING IS M4'S CLOCK. The renderer used to read
    // realtimeSinceStartup while the beat waited on scaled time, so a capture
    // sampled the spell at wall speed while frames advanced at a fixed step and
    // the recording showed a spell running at a different rate from the fight
    // around it. Time.time and Time.captureFramerate move together, so 60
    // frames of this series ARE one second of the game.
    //
    // NOT A GATE, and it cannot be: run_tests_parallel passes -nographics,
    // where CanvasCapture.IsSupported is false and ReadPixels returns garbage,
    // so this Ignores itself there -- the same guard RuntimeScreenshotTests
    // carries, and for the same reason it is a guard rather than [Explicit]:
    // the ignore MESSAGE names the command that does run it, which an
    // Explicit's silence does not. Driven by tools/screenshot.ps1 -Runtime
    // -RuntimeFilter SpellRuntimeCaptureTests.
    //
    // M8 EXTENDS THIS RATHER THAN REPLACING IT: a second cast opened while the
    // first cast's droplets are still alive, with the fixture asserting on the
    // frame that second cast opens that the first still owns a particle. This
    // is the one-cast half, which is what M5 owes.
    public class SpellRuntimeCaptureTests
    {
        // 60, matching RuntimeScreenshotTests. With captureFramerate set, time
        // advances by exactly 1/fps per frame however long the frame really
        // took, so a frame index is an instant and the series plays back at the
        // rate it was authored at.
        private const int CaptureFps = 60;

        // DERIVED PER SPELL FROM THE CAST'S OWN CLOCK, not a fixed frame
        // count. A fixed window (66 frames, raised from 45 in commit
        // 8ea8c149) kept being wrong for whichever spell PP_RUNTIME_SPELL
        // named next -- winters_rebuke and blackglass_spear both travel and
        // run past it -- and a frame count derived from ClearedSeconds ALONE
        // is wrong the same way, for a reason real capture evidence caught: a
        // real cast's hit cue landed at capture frame 42 (0.70s of CAPTURE
        // time) while its own hitCueSeconds is 0.46. FightBeatPlayer holds a
        // beat's own clock (Time.time, which SpellPerformancePlayer's Cursor
        // ages on) still for a hit-stop, but Update -- and with it a capture
        // frame -- keeps running every tick regardless of timeScale. So
        // capture frames and the spell's own authored seconds are not one
        // ratio; a frame count computed from ClearedSeconds up front assumes
        // they are and is wrong by however long the beat held.
        //
        // THE FIX POLLS THE CAST'S OWN CLOCK INSTEAD OF PREDICTING IT.
        // SpellPerformancePlayer.HasReached reads the live cast's Cursor --
        // the exact clock Advance releases a cast against -- so the loop
        // below keeps recording real capture frames until THAT clock, not a
        // frame count derived from it, has delivered ClearedSeconds +
        // FrameEndMargin of authored playback.
        private const float FrameEndMargin = 0.25f;

        // THE TARGET CLOCK'S OWN CAP, so a spell that authors 'loop' or
        // 'hold' with nothing to end it cannot ask the loop below to wait
        // forever. Every shipped spell clears in under 1.1s of authored
        // playback; 4s is generous headroom rather than a bound anything is
        // expected to approach.
        private const float MaxCaptureSeconds = 4f;

        // THE LOOP'S OWN SAFETY VALVE, in CAPTURE frames rather than the
        // authored seconds MaxCaptureSeconds bounds -- the two are not the
        // same unit once a hit-stop can hold the authored clock still while
        // capture frames keep coming (see above). Ten seconds of capture
        // frames is well past the ~0.24s a single hit-stop hold cost the
        // real evidence this fixes was measured against, several times over,
        // while still being finite: a cast whose Cursor is genuinely stuck
        // (a bug, not a hold) fails the loop below rather than hanging it.
        private const int LoopSafetyFrames = 10 * CaptureFps;

        // How long a cast plays before nothing of it is drawn, from
        // SpellPerformance -- the same expression FightController.ResolveCast
        // uses to release a handle, read here from Resources rather than a
        // live SpellVfxPlayer so it works before any player has loaded frames.
        private static float ClearedSecondsOf(SpellPresentation vfx, int targetCount) =>
            SpellPerformance.Resolve(vfx, targetCount, path => FrameSequenceLoader.Load(path)?.Length ?? 0)
                .ClearedSeconds;

        // The spell whose composition this exists to show, from
        // PP_RUNTIME_SPELL when tools/screenshot.ps1 -Runtime -Spell <id> set
        // one, defaulting to the pilot so today's -RuntimeFilter
        // SpellRuntimeCaptureTests behaviour is unchanged when nothing is
        // passed. Still not open to just anything: ForSpell below owns the
        // caster resolution and refusals, the same way tools/preview.ps1
        // -Spell does, and an id it cannot stand a fight up for is refused
        // there by name rather than approximated here.
        private static readonly string SpellId = ResolveSpellId();

        // Read directly here rather than through PreviewCaptureTests' own
        // env-var family: PreviewEnvironmentLintTests requires that family's
        // prefix appear in exactly one file (PreviewCaptureTests.cs, which is
        // [Explicit] and never runs in the gate), because a second fixture
        // reading one would test a different subset depending on the shell it
        // was started from. This capture is driven by tools/screenshot.ps1,
        // not tools/preview.ps1, so it gets its own variable rather than
        // reaching into that reserved family.
        private const string SpellVariable = "PP_RUNTIME_SPELL";
        private const string VersusVariable = "PP_RUNTIME_VERSUS";

        // Battle speed the cast is recorded at (tools/spell_feel.ps1 -Speed),
        // as FightBeatPlayer.BeatSpeedMultiplier. 1 when unset or unparsable.
        private const string SpeedVariable = "PP_RUNTIME_SPEED";

        // A status every fielded enemy already carries when the cast starts
        // (StatusEffectType name), so a spell that spends one -- Viper's Bite
        // detonating Poison -- is recorded doing so. Empty means none.
        private const string PreStatusVariable = "PP_RUNTIME_PRESTATUS";

        private static float ResolveSpeed()
        {
            string raw = System.Environment.GetEnvironmentVariable(SpeedVariable);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float speed) && speed > 0f
                ? speed
                : 1f;
        }

        private static string ResolveSpellId()
        {
            string raw = System.Environment.GetEnvironmentVariable(SpellVariable);
            return string.IsNullOrWhiteSpace(raw) ? PilotSpellId : raw.Trim();
        }

        // THE WATER PILOT, which the two M8 overlap tests below are written
        // against and nothing else. Their frame numbers (SecondCastFrame,
        // WaterOverFaultFrame) and their premise (a tail of droplets still
        // alive half a second after release) are the pilot's own timings; with
        // PP_RUNTIME_SPELL naming winters_rebuke or blackglass_spear the
        // first cast's particles are gone before frame 30 and the overlap
        // assertion fails for a reason that says nothing about the spell being
        // captured. So they Ignore themselves for any other spell rather than
        // retime themselves per spell -- the frame series above is the part
        // that is general.
        private const string PilotSpellId = "prismatic_orb";

        private static void OnlyForThePilot(string what)
        {
            if (SpellId == PilotSpellId) return;

            Assert.Ignore(what + " is timed against the " + PilotSpellId + " pilot's own tail, and " +
                          SpellVariable + " names '" + SpellId + "'. Run it with no -Spell (or -Spell " +
                          PilotSpellId + ") to check the overlap; the frame series for '" + SpellId +
                          "' is TheWaterPilotRecordsAsAFrameSeries.");
        }

        // 1.25s at 60fps, which holds one whole pilot cast (0.63s) plus a
        // second one opened half a second in.
        private const int OverlapFrames = 75;

        // 0.50s from the first release: past the arrival and the cue, inside
        // the tail. Chosen against the beat's own hold (0.45s), so the overlap
        // recorded is the one two casts in a real round already produce.
        private const int SecondCastFrame = 30;

        // 0.20s into the fault, which runs 0.78s -- the water lands while every
        // plume is still erupting.
        private const int WaterOverFaultFrame = 12;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void Restore()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
        }

        private FightController _fight;
        private FightBeatPlayer _beats;
        private Canvas _canvas;

        // THE PILOT'S OWN FIGHT, stood up the way tools/preview.ps1 stands one
        // up: PreviewFight chooses the caster and the formation, so a recording
        // and a preview are photographs of the same encounter rather than of two
        // fixtures that drifted.
        // `enemies` null fields what the PLAN says (PreviewFight.EnemyCountFor
        // over its formation), which is what tools/preview.ps1 fields for the
        // same spell: one for a single-target cast, three for an all-target
        // one. A number overrides it -- see the overlap tests.
        private IEnumerator StandTheFightUp(int? enemies)
        {
            var plan = ResolvePlan();
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Debug.Log("[SpellRuntime] " + PreviewFight.Describe(plan));

            FightBeatPlayer.BeatSpeedMultiplier = ResolveSpeed();
            Time.captureFramerate = CaptureFps;

            // A KNOWN TEARDOWN LOG, TOLERATED ACROSS THE SCENE SWAP AND
            // NOWHERE ELSE. Loading Fight over a Fight disables the outgoing
            // one, and FightBeatPlayer.OnDisable -> EndFight -> Flush ->
            // OnPlaybackFinished -> RefreshUi reaches
            // FightController.BeginAppearancePop (FightController.Hud.cs:1218),
            // which calls StartCoroutine on a panel that is already inactive
            // and logs an error. It is a real defect and it is recorded as
            // AUDIT.md #106; it belongs to fight-HUD teardown rather than to
            // anything this fixture is recording, and a second test in this
            // class is simply the first thing in the suite to load a fight
            // scene over a live one. The tolerance is lifted before the cast.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            // The fight scene's bootstrap installs the settings-backed battle
            // speed; a recording is at the speed the caller asked for, not the
            // speed this machine's options menu says, so Pace is
            // BeatSpeedMultiplier alone.
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightBeatPlayer.AdoptPlayerSpeed();
            Assert.AreEqual(1f, FightBeatPlayer.PlayerSpeedMultiplier,
                "PlayerSpeedMultiplier is not 1 in the fixture");
            Debug.Log("[SpellRuntime] pace " + FightBeatPlayer.Pace + " (PP_RUNTIME_SPEED " +
                      ResolveSpeed() + ")");

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the fight scene has no controller");

            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the fight scene has no beat player");

            // FightBootstrap's own placeholder fight is mid-beat and holds the
            // controller busy, so the forced press would be swallowed. Same
            // move PreviewCaptureTests and CinderfaultSpellCaptureTests make.
            _beats.Flush();
            yield return null;
            yield return null;

            // THE COUNT IS THE CALLER'S, not the plan's. PreviewFight.
            // EnemyCountFor answers for the SPELL being previewed, and the
            // pilot is single-target, so it says one however many are asked
            // for -- which is right for a recording of the pilot alone and
            // wrong for the overlap below, where a cinderfault needs a
            // formation to open under.
            // PP_RUNTIME_VERSUS names the one body to cast at (enemies.json id),
            // so a spell that sizes to its target is recorded on a small body and
            // on a tall one; empty keeps the plan's own pick.
            //
            // A SKILL WHOSE REACH EXCLUDES THE FRONT SEAT (reachSlots [2,3])
            // needs a formation to stand behind: one enemy stands in slot 1
            // and the forced press has nobody it may hit. Three, and the press
            // takes the first one the reach accepts (FirstReachableEnemyIndex).
            string versus = System.Environment.GetEnvironmentVariable(VersusVariable);
            bool needsDepth = plan.Skill != null && !plan.Skill.Reach.Allows(0);
            int depth = needsDepth ? 3 : 1;
            var fielded = !string.IsNullOrWhiteSpace(versus) && enemies == null
                ? Enumerable.Repeat(versus.Trim(), depth).ToList()
                : PreviewFight.EnemiesWithArt(Mathf.Max(
                    enemies ?? PreviewFight.EnemyCountFor(plan.Formation, 3), enemies == null ? depth : 1));
            Assert.IsNotEmpty(fielded, "no enemies in content to cast at");

            var built = FightEncounterAdapter.Build(
                new List<string>(plan.Party),
                fielded,
                new Domain.Rng.SeededRandom(20260908),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { SpellId });

            Assert.IsNotNull(built?.Session, "'" + SpellId + "' could not be built into an encounter");

            PreviewFight.Prepare(built, plan);

            string preStatus = System.Environment.GetEnvironmentVariable(PreStatusVariable);
            if (!string.IsNullOrWhiteSpace(preStatus))
            {
                Assert.IsTrue(System.Enum.TryParse(preStatus.Trim(), true, out StatusEffectType pre),
                    PreStatusVariable + " '" + preStatus + "' is not a StatusEffectType");
                foreach (var foe in built.Session.Encounter.Enemies.Where(e => e != null))
                {
                    foe.Statuses.Add(new ActiveStatus(pre, 3, 3));
                }
            }

            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            _canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(_canvas, "the fight scene has no root canvas");
        }

        // TRUE WHEN PreviewStage REFUSED THE EFFECT (Ward, Reclaim, Hasten,
        // Reposition: each needs a talent, a status or a turn schedule the
        // stage will not fake). The cast is then played through the module
        // directly, the way the overlap tests do, so only the presentation is
        // recorded: no damage number, no status, no cue from the session.
        private bool _presentationOnly;

        private PreviewFight.Plan ResolvePlan()
        {
            _presentationOnly = false;
            var plan = PreviewFight.ForSpell(SpellId);
            if (plan.Ok || plan.Skill == null || PreviewStage.IsSupported(plan.Skill.Effect)) return plan;

            var roster = ContentDatabase.Characters
                .Where(c => c != null && c.Data != null).Select(c => c.Data).ToList();
            var stand = new PreviewFight.Plan
            {
                Skill = plan.Skill,
                Formation = PreviewFight.FormationFull,
            };
            stand.CasterId = PreviewFight.ChooseCaster(plan.Skill, roster, stand);
            if (stand.CasterId == null) return stand;

            stand.Party = new List<string> { stand.CasterId };

            // A swap has two ends, so the stand-in party has two bodies for
            // the counterpart layers to draw on.
            if (plan.Skill.Effect == SkillEffect.SwapAllies)
            {
                var other = roster.FirstOrDefault(c => c.Id != stand.CasterId);
                if (other != null) stand.Party.Add(other.Id);
            }
            stand.Notes.Add("presentation only: '" + SpellId + "' resolves as " + plan.Skill.Effect +
                            ", which the preview stage refuses, so the vfx is played through the module with " +
                            "no damage number, status or session cue");
            _presentationOnly = true;
            return stand;
        }

        // Who the presentation is aimed at. A skill owned by an enemy that is
        // not in the player roster (the Bellwether's chains) is cast by the
        // first enemy at the player's caster, the direction it flies in a
        // real fight; everything else is cast by the player's caster, at an
        // enemy for enemy-facing skills and at the caster for the rest.
        private CombatBeat PresentationBeat()
        {
            var enc = _fight.SessionForTest.Encounter;
            var caster = enc.PlayerParty.First(c => c != null);
            var foes = enc.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var skill = PreviewFight.ForSpell(SpellId).Skill;

            bool enemyOwned = !string.IsNullOrWhiteSpace(skill.CharacterId) &&
                              !ContentDatabase.Characters.Any(c => c != null && c.id == skill.CharacterId);
            bool aimsAtEnemies = skill.Targeting == SkillTargeting.SingleEnemy ||
                                 skill.Targeting == SkillTargeting.AllEnemies;

            var beat = new CombatBeat { Vfx = ShippedVfx(SpellId) };
            if (enemyOwned && aimsAtEnemies)
            {
                beat.Actor = foes[0];
                beat.Target = caster;
            }
            else
            {
                beat.Actor = caster;
                beat.Target = aimsAtEnemies ? foes[0] : caster;

                // The other end of a swap: the second party member.
                if (skill.Effect == SkillEffect.SwapAllies)
                {
                    beat.Counterpart = enc.PlayerParty.FirstOrDefault(c => c != null && !ReferenceEquals(c, caster));
                }
                if (skill.Targeting == SkillTargeting.AllEnemies) beat.SplashTargets = foes.Skip(1).ToList();
            }

            return beat;
        }

        [UnityTest]
        public IEnumerator TheWaterPilotRecordsAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime " +
                              "-RuntimeFilter SpellRuntimeCaptureTests");
            }

            // THE PLAN'S FORMATION, which is what tools/preview.ps1
            // photographs for the same spell: one enemy for a single-target
            // cast (the pilot, an Afflict), three for an all-target one.
            yield return StandTheFightUp(null);

            var fight = _fight;
            var canvas = _canvas;

            var handle = CastHandle.None;
            if (_presentationOnly)
            {
                handle = fight.PlaySpellVfxForTest(PresentationBeat());
                Assert.IsTrue(handle.IsLive, "'" + SpellId + "' obtained no cast when played through the module");
            }
            else
            {
                fight.ForceFirstAction(SpellId);

                float armed = Time.realtimeSinceStartup;
                while (!fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
                Assert.IsTrue(fight.IsBusy, "'" + SpellId + "' was never cast -- the log says what was refused");
            }

            // FRAME ZERO IS THE FRAME THE CAST FIRST DRAWS, so the series opens
            // on the ball leaving rather than on however many frames of wind-up
            // the beat happened to take. NOT the same instant as release on the
            // cast's own clock, though -- a beat's hit-stop can hold that clock
            // (Time.time) still while Update, and a drawn frame with it, keeps
            // running, so "first drawn" and "release" can be capture frames
            // apart. That is exactly why the recording below polls the cast's
            // own Cursor rather than assuming a frame count derived from it.
            int waited = 0;
            while (!AnythingDrawn(fight) && waited < 240)
            {
                waited++;
                yield return null;
            }

            Assert.Less(waited, 240, "the cast never drew anything, so there is no recording to make");

            // THE HANDLE THIS RECORDING BELONGS TO. ForceFirstAction drives
            // the cast through the normal beat flow rather than through
            // PlaySpellVfxForTest, so LastBegunForTest is the only way to
            // name it -- and it must already be live, because AnythingDrawn
            // above only turns true once Paint has run for a live cast.
            var module = fight.PerformancePlayerForTest;
            if (!_presentationOnly) handle = module.LastBegunForTest;
            Assert.IsTrue(module.IsLive(handle),
                "'" + SpellId + "' drew something on stage but LastBegunForTest names no live cast, so " +
                "there is no clock to record this series against.");

            int targetCount = fight.SessionForTest.Encounter.Enemies.Count(e => e != null && e.IsAlive);
            var shipped = ShippedVfx(SpellId);
            float clearedSeconds = ClearedSecondsOf(shipped, targetCount);
            float hitCueSeconds = SpellPerformance.Resolve(shipped, targetCount,
                path => FrameSequenceLoader.Load(path)?.Length ?? 0).HitCueSeconds;
            float targetCursor = Mathf.Min(clearedSeconds + FrameEndMargin, MaxCaptureSeconds);

            Directory.CreateDirectory(OutputDir);

            // POLLED AGAINST THE CAST'S OWN CLOCK, not a frame count derived
            // from it -- a fixed ratio between capture frames and Cursor
            // seconds does not hold across a hit-stop hold (see the header
            // above HasReached). A do/while rather than a while so the frame
            // the clock actually crosses targetCursor on is still recorded,
            // the same inclusive shape the fixed-count loop this replaces had.
            int lit = 0;
            int frame = 0;
            bool reached;

            // A short cast frees its handle before the target cursor, and
            // HasReached reads a freed handle as reached. The recording keeps
            // FrameEndMargin more seconds after that so the aftermath (the
            // target's own hurt reaction) is on film.
            int freedFrame = -1;
            int tailFrames = Mathf.CeilToInt(FrameEndMargin * CaptureFps);

            // THE FRAME THE AUTHORED HIT CUE FIRST SHOWS ON. The cue is
            // crossed during Update, after the frame just written, so the
            // first picture that carries it is the next one. tools/spell_feel.py
            // reads it from the timing file to mark contact instead of
            // guessing it off the pixels.
            //
            // AN `at: hit` SPELL FIRST DRAWS ON THE CUE FRAME ITSELF, so the
            // cursor is already past hitCueSeconds when this hook installs and
            // the hook never fires: that cue is on frame 0.
            int hitFrame = module.HasReached(handle, hitCueSeconds) ? 0 : -1;
            module.HitCueCrossedForTest = h =>
            {
                if (hitFrame < 0 && h.Equals(handle)) hitFrame = frame + 1;
            };

            do
            {
                if (AnythingDrawn(fight)) lit++;

                string path = Path.Combine(OutputDir,
                    string.Format("spell_{0}_f{1:00}.png", SpellId, frame));
                CanvasCapture.RenderToFile(canvas, path);
                FileAssert.Exists(path);

                yield return null;

                bool live = module.IsLive(handle);
                if (!live && freedFrame < 0) freedFrame = frame + 1;
                reached = live
                    ? module.HasReached(handle, targetCursor)
                    : frame + 1 - freedFrame >= tailFrames;
                frame++;
            } while (!reached && frame < LoopSafetyFrames);

            int frames = frame;
            int activeFrames = freedFrame >= 0 ? freedFrame : frames;
            module.HitCueCrossedForTest = null;

            // Pacing and contact for tools/spell_feel.py, beside the frames.
            File.WriteAllText(
                Path.Combine(OutputDir, string.Format("spell_{0}_timing.json", SpellId)),
                string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"spell\":\"{0}\",\"fps\":{1},\"frames\":{2},\"firstDrawnWaitFrames\":{3}," +
                    "\"clearedSeconds\":{4:F3},\"hitFrame\":{5},\"speed\":{6:F2}," +
                    "\"freedFrame\":{7},\"presentationOnly\":{8}}}",
                    SpellId, CaptureFps, frames, waited, clearedSeconds, hitFrame, ResolveSpeed(),
                    freedFrame, _presentationOnly ? "true" : "false"));

            Assert.IsTrue(reached || freedFrame >= 0,
                "'" + SpellId + "' had not reached " + targetCursor.ToString("F2") + "s on its own " +
                "performance clock after " + LoopSafetyFrames + " capture frames -- its cast clock looks " +
                "stuck rather than merely held by a hit-stop.");

            // THE SERIES IS OF SOMETHING. A recording of an empty stage is the
            // failure this cannot see by eye -- and the one the pilot actually
            // produced twice before the particle pool's members were being
            // activated at all.
            //
            // THE FLOOR SCALES WITH THE SERIES: a short cast (trunk_slam, 17
            // frames) is all lit and still fewer than 20, so the bar is half
            // the frames the cast was live for, capped at 20.
            Assert.Greater(lit, Mathf.Min(20, activeFrames / 2),
                "only " + lit + " of " + frames + " recorded frames had anything drawn on them -- '" + SpellId +
                "' cast at " + CaptureFps + "fps, so a series this empty is a recording of the stage " +
                "rather than of the cast.");

            Debug.Log("[SpellRuntime] wrote " + frames + " frames to " + OutputDir + " (" + lit +
                      " of them with the cast drawing; frame 0 is the first drawn, " + waited +
                      " frames after the press; recorded until the cast's own clock reached " +
                      targetCursor.ToString("F2") + "s of " + clearedSeconds.ToString("F2") +
                      "s cleared). ffmpeg -framerate " + CaptureFps + " -i spell_" + SpellId +
                      "_f%02d.png out.mp4 makes it a video.");
        }

        // ---- M8: two consecutive casts, the first's droplets still alive ---------

        // THE CASE THE POOLING EXISTS FOR, and the one a single-cast recording
        // cannot show: a second cast opening over a tail that has not finished.
        // The pilot's last droplet clears 0.63s after its release and a beat's
        // hold is 0.45s, so this is not a contrived overlap -- it is what two
        // party members casting in one round already produce.
        //
        // THE ASSERTION IS THE POINT AND THE PICTURES ARE THE EVIDENCE. On the
        // frame the second cast opens, the first cast's HANDLE still owns at
        // least one particle -- a fact about ownership rather than about what a
        // human thought they saw in a PNG. The frames are then written for the
        // human, and only when there is a graphics device; the assertion runs
        // in the commit gate either way.
        //
        // DRIVEN THROUGH THE MODULE RATHER THAN THROUGH TWO BEATS, and that is
        // a deviation worth naming: the beat player is sequential, so a second
        // BEAT arrives when its own hold, hit-stop, settle and gap say it does,
        // which is not a time this fixture can choose. Opening the second cast
        // on a chosen frame is the only way to record a NAMED overlap; what it
        // gives up is the damage popup, which is not what this records.
        [UnityTest]
        public IEnumerator TwoConsecutiveCastsOverlapWithTheFirstsDropletsStillOwned()
        {
            OnlyForThePilot("The two-cast overlap");

            yield return StandTheFightUp(1);

            var module = _fight.PerformancePlayerForTest;
            var caster = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(e => e != null && e.IsAlive);

            var first = _fight.PlaySpellVfxForTest(WaterAt(caster, foe));
            Assert.IsTrue(module.IsLive(first), "the first cast obtained nothing");

            bool haveGraphics = CanvasCapture.IsSupported;
            if (haveGraphics) Directory.CreateDirectory(OutputDir);

            int owned = -1;
            var second = CastHandle.None;

            for (int frame = 0; frame < OverlapFrames; frame++)
            {
                if (frame == SecondCastFrame)
                {
                    // READ BEFORE THE SECOND CAST OPENS, so the number is the
                    // FIRST cast's own and cannot be the second's.
                    owned = module.ParticlesOwnedBy(first);
                    second = _fight.PlaySpellVfxForTest(WaterAt(caster, foe));
                }

                if (haveGraphics)
                {
                    string path = Path.Combine(OutputDir,
                        string.Format("spell_{0}_two_f{1:00}.png", SpellId, frame));
                    CanvasCapture.RenderToFile(_canvas, path);
                }

                yield return null;
            }

            Assert.Greater(owned, 0,
                "on the frame the second cast opened, the first cast owned no particles at all -- so " +
                "this recording is of two casts in a row rather than of two casts overlapping. The " +
                "pilot's shed and spray clear at 0.63s and the second opens at " +
                (SecondCastFrame / (float)CaptureFps).ToString("F2") + "s.");

            Assert.IsTrue(second.IsLive, "the second cast never opened");

            Debug.Log("[SpellRuntime] second cast opened on frame " + SecondCastFrame + " with " + owned +
                      " droplets of the first still owned" +
                      (haveGraphics ? "; wrote " + OverlapFrames + " frames to " + OutputDir : ""));
        }

        // ---- M8: a cinderfault and a water tail on one target --------------------

        // TWO CASTS, TWO BANDS, ONE TARGET. The fault is a cast-level ground
        // layer under the whole rack; the water lands on one of the three
        // enemies standing in it while every plume is still erupting. The
        // assertion is that nothing is shared: two casts, disjoint members, and
        // the ground band held by the one that authored a ground layer.
        [UnityTest]
        public IEnumerator ACinderfaultAndAWaterTailOverlapOnOneTarget()
        {
            OnlyForThePilot("The cinderfault-and-water overlap");

            yield return StandTheFightUp(3);

            var module = _fight.PerformancePlayerForTest;
            var caster = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foes = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            Assert.GreaterOrEqual(foes.Count, 2, "the overlap wants a formation, not a lone enemy");

            var fault = _fight.PlaySpellVfxForTest(CinderfaultOn(caster, foes));
            Assert.IsTrue(module.IsLive(fault), "the cinderfault obtained nothing");

            bool haveGraphics = CanvasCapture.IsSupported;
            if (haveGraphics) Directory.CreateDirectory(OutputDir);

            var water = CastHandle.None;
            bool checkedOverlap = false;

            for (int frame = 0; frame < OverlapFrames; frame++)
            {
                if (frame == WaterOverFaultFrame)
                {
                    water = _fight.PlaySpellVfxForTest(WaterAt(caster, foes[0]));
                }

                if (frame == WaterOverFaultFrame + 1)
                {
                    Assert.IsTrue(module.IsLive(fault) && module.IsLive(water),
                        "the two casts are not both alive, so there is no overlap to record");

                    // THE FAULT IS IN THE GROUND BAND AND THE WATER IS NOT -- a
                    // member index only means something inside its own band.
                    Assert.IsTrue(module.DrawsInGroundBand(fault, 0),
                        "the cinderfault's first layer is not drawing behind the racks");

                    var faultMembers = MembersOf(module, fault);
                    var waterMembers = MembersOf(module, water);

                    Assert.IsNotEmpty(faultMembers, "the cinderfault's plumes drew nothing");
                    Assert.IsNotEmpty(waterMembers, "the water cast drew no sprite layer");
                    CollectionAssert.IsEmpty(faultMembers.Intersect(waterMembers).ToList(),
                        "the two casts are sharing an effects renderer, so one is drawing over the " +
                        "other's member rather than beside it");

                    checkedOverlap = true;
                }

                if (haveGraphics)
                {
                    string path = Path.Combine(OutputDir, string.Format("spell_overlap_f{0:00}.png", frame));
                    CanvasCapture.RenderToFile(_canvas, path);
                }

                yield return null;
            }

            Assert.IsTrue(checkedOverlap, "the overlap frame was never reached");

            Debug.Log("[SpellRuntime] cinderfault plus a water cast on one of its targets, water opening " +
                      "on frame " + WaterOverFaultFrame +
                      (haveGraphics ? "; wrote " + OverlapFrames + " frames to " + OutputDir : ""));
        }

        // Every EFFECTS-band member a live cast holds, which is the list two
        // overlapping casts have to keep disjoint. Sixteen is past any cast the
        // pool can serve, so the walk ends before the instances do only if a
        // spell ever authors more layers than the band has members.
        private static List<int> MembersOf(SpellPerformancePlayer module, CastHandle handle)
        {
            var held = new List<int>();
            for (int instance = 0; instance < 16; instance++)
            {
                int member = module.MemberFor(handle, instance);
                if (member < 0) continue;
                if (module.DrawsInGroundBand(handle, instance)) continue;
                held.Add(member);
            }

            return held;
        }

        private static SpellPresentation ShippedVfx(string skillId)
        {
            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            Assert.IsNotNull(definition, "'" + skillId + "' is not in the content database");

            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.IsTrue(vfx != null && vfx.HasArt, "'" + skillId + "' resolves to a presentation with no art");
            return vfx;
        }

        private static CombatBeat WaterAt(CombatantState caster, CombatantState target) => new CombatBeat
        {
            Actor = caster,
            Target = target,
            Vfx = ShippedVfx(SpellId),
        };

        private static CombatBeat CinderfaultOn(CombatantState caster, List<CombatantState> struck) =>
            new CombatBeat
            {
                Actor = caster,
                Target = struck[0],
                SplashTargets = struck.Skip(1).ToList(),
                Vfx = ShippedVfx("cinderfault"),
            };

        private static bool AnythingDrawn(FightController fight) =>
            fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Any(p => p != null && p.Image != null && p.Image.enabled) ||
            AnyParticleDrawn(fight);

        private static bool AnyParticleDrawn(FightController fight)
        {
            var pool = fight.GetComponentInChildren<SpellParticleRenderer>(includeInactive: true);
            if (pool == null) return false;

            for (int i = 0; i < pool.Capacity; i++)
            {
                if (pool.IsDrawing(i)) return true;
            }

            return false;
        }
    }
}
