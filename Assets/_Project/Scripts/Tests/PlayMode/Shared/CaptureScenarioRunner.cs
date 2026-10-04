using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Preview;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // ONE SCENARIO, in the shape tools/gif.ps1 normalizes a request into
    // (docs/PLAN_GIF_CAPTURE.md 2.2). Public fields because JsonUtility reads
    // the request file straight into it. `crop` is carried for the encoder and
    // never read here: frames are always the whole 1920x1080 canvas.
    [Serializable]
    internal sealed class CaptureScenario
    {
        public string key = "";
        public string kind = "";
        public string id = "";
        public string vs = "rat";
        public string formation = "lone";
        public float speed = 1f;
        public string preStatus = "";
        public string skill = "";
        public string crop = "stage";
        public float seconds = 2f;
        // Frame size; 0 x 0 is CanvasCapture's default (the 1920x1080 reference).
        public int width;
        public int height;
        // Casts of the spell in one fight (spell kind only).
        public int repeat = 1;
        // Round-darkness opacity the fight is dressed with; 0 is none.
        public float darkness;
        // Comma list of character ids standing in the party, caster first; empty
        // keeps the plan's own squad. `vs` is likewise a comma list of enemy ids
        // in slot order.
        public string party = "";
        // A spell's pick: a party member's id (ally spells) or an enemy slot
        // number, 1 the front (single-enemy spells). Empty keeps the first.
        public string target = "";
    }

    [Serializable]
    internal sealed class CaptureRequest
    {
        public string requestId = "";
        public string outDir = "";
        public CaptureScenario[] scenarios = Array.Empty<CaptureScenario>();
    }

    // WHAT A SCENARIO WROTE. The batch reports it per scenario; the spell
    // capture test only reads the frame count back for its log line.
    internal sealed class CaptureOutcome
    {
        public int Frames;
        public int LitFrames;
        public int FirstDrawnWaitFrames;
        public int HitFrame = -1;
        public int FreedFrame = -1;
        public float ClearedSeconds;
        public bool PresentationOnly;
        public float TargetCursor;
        // The first frame index a following recording continues at.
        public int NextFrame;
    }

    // The inputs of CaptureScenarioRunner.StandTheFightUp for a spell.
    internal sealed class CaptureStandRequest
    {
        public string SpellId;
        // PP_RUNTIME_VERSUS: the one body to cast at. Empty keeps the plan's pick.
        public string Versus;
        public float Speed = 1f;
        // StatusEffectType name every fielded enemy carries when the cast starts.
        public string PreStatus;
        // A number overrides the plan's own enemy count (the overlap tests).
        public int? Enemies;
        // True: Versus fills the formation Enemies sizes. False keeps the
        // old rule, where a count given by the caller fields the art
        // roster and Versus applies only to the plan's own count.
        public bool VersusFillsFormation;
        // Character ids of the party, caster first; null or empty keeps the plan's squad.
        public IReadOnlyList<string> Party;
        // A party member's id or a 1-based enemy slot; empty keeps the first.
        public string Target;
    }

    // Where each frame goes, and the one file beside them. The spell test
    // and the batch name theirs differently.
    internal sealed class CaptureSink
    {
        public Func<int, string> FramePath;
        public string TimingPath;
    }

    // THE FIGHT-SETUP AND STOP RULE THAT SpellRuntimeCaptureTests USED TO OWN,
    // lifted out so the cold batch (GifBatchCaptureTests) and that test stand a
    // fight up and decide when to stop recording with ONE copy of both. A second
    // copy is how a GIF and a preview come to photograph two different fights
    // (PreviewFight's header makes the same argument for the setup).
    //
    // Everything here keeps the yield order the test had, frame for frame: the
    // preservation gate in docs/PLAN_GIF_CAPTURE.md section 3 compares frame
    // counts and pixels of the old path against this one.
    internal sealed class CaptureScenarioRunner
    {
        // 60, matching RuntimeScreenshotTests. With captureFramerate set, time
        // advances by exactly 1/fps per frame however long the frame really
        // took, so a frame index is an instant.
        public const int Fps = 60;

        // The recording polls the cast's own clock (SpellPerformancePlayer.
        // HasReached) rather than predicting a frame count: a beat's hit-stop
        // holds that clock while Update, and a capture frame with it, keeps
        // running, so capture frames and authored seconds are not one ratio.
        // The cast keeps FrameEndMargin more seconds on film past its cleared
        // time so the target's own reaction is in the series.
        public const float FrameEndMargin = 0.25f;

        // A spell that authors 'loop' or 'hold' with nothing to end it cannot
        // keep the recording open forever. Every shipped spell clears in under
        // 1.1 s of authored playback; 4 s is headroom, not a bound anything
        // approaches.
        public const float MaxCaptureSeconds = 4f;

        // The same guard in CAPTURE frames, the unit a hit-stop does not hold
        // still. A cast whose Cursor is genuinely stuck fails instead of hanging.
        public const int LoopSafetyFrames = 10 * Fps;

        // A round (the player's swing, then the enemy's forced action) is two
        // beats, so the attack kind gets more than one spell's cap.
        public const float MaxAttackSeconds = 8f;

        // Every scenario fights from this seed, whatever its kind.
        public const ulong Seed = 20260908UL;

        // FightBootstrap.FullFormationCount: three bodies, the whole rack.
        public const int FullFormation = 3;

        public FightController Fight { get; private set; }
        public FightBeatPlayer Beats { get; private set; }
        public Canvas Canvas { get; private set; }

        // Frame size every capture renders at; 0 x 0 keeps CanvasCapture's default.
        public int Width;
        public int Height;

        // Opacity of the round darkness the fight binds with (Bell in the Fog's
        // dressing, nothing else of it): 0 binds a room fight's None. Set before
        // StandTheFightUp.
        public float Darkness;

        // The body the cast is aimed at when the scenario names one (ally spell
        // on a squadmate, single-enemy spell on a back-rank seat); null keeps
        // the first. Set by StandTheFightUp from the request's Target.
        public CombatantState AimedTarget { get; private set; }

        // The presentation a darkened capture binds: a round counter (the
        // darkness is a round-limit layer, HasDarkness) held at one opacity.
        internal static FightRoundPresentation DarknessPresentation(float darkness) =>
            darkness <= 0f
                ? null
                : new FightRoundPresentation(roundLimit: 10, darknessStart: darkness, darknessFloor: darkness);

        private void Render(Canvas canvas, string path) =>
            CanvasCapture.RenderToFile(canvas, path, Width, Height, fitReference: true);

        // TRUE WHEN PreviewStage REFUSED THE EFFECT (Ward, Reclaim, Hasten,
        // Reposition: each needs a talent, a status or a turn schedule the stage
        // will not fake). The cast is then played through the module directly,
        // so only the presentation is recorded: no damage number, no status, no
        // cue from the session.
        public bool PresentationOnly { get; private set; }

        // ---- fight set-up ------------------------------------------------------

        // THE SPELL'S OWN FIGHT, stood up the way tools/preview.ps1 stands one
        // up: PreviewFight chooses the caster and the formation, so a recording
        // and a preview are photographs of the same encounter.
        public IEnumerator StandTheFightUp(CaptureStandRequest request)
        {
            string spellId = request.SpellId;
            _preStatus = request.PreStatus;
            var plan = ResolvePlan(spellId);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Debug.Log("[SpellRuntime] " + PreviewFight.Describe(plan));

            yield return LoadFight(request.Speed);

            // THE COUNT IS THE CALLER'S, not the plan's. PreviewFight.
            // EnemyCountFor answers for the SPELL being previewed, and the
            // pilot is single-target, so it says one however many are asked
            // for -- which is right for a recording of the pilot alone and
            // wrong for the overlap tests, where a cinderfault needs a
            // formation to open under.
            //
            // A SKILL WHOSE REACH EXCLUDES THE FRONT SEAT (reachSlots [2,3])
            // needs a formation to stand behind: one enemy stands in slot 1
            // and the forced press has nobody it may hit. Three, and the press
            // takes the first one the reach accepts (FirstReachableEnemyIndex).
            string versus = request.Versus;
            bool hasVersus = !string.IsNullOrWhiteSpace(versus);
            // A list is the formation itself, slot 1 first: mixed bodies, any count.
            var rack = hasVersus
                ? versus.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList()
                : new List<string>();
            bool needsDepth = plan.Skill != null && !plan.Skill.Reach.Allows(0);
            int depth = needsDepth ? 3 : 1;

            List<string> fielded;
            if (rack.Count > 1)
            {
                fielded = rack;
            }
            else if (hasVersus && request.VersusFillsFormation)
            {
                int count = request.Enemies ?? Mathf.Max(depth, PreviewFight.EnemyCountFor(plan.Formation, 3));
                fielded = Enumerable.Repeat(rack[0], count).ToList();
            }
            else if (hasVersus && request.Enemies == null)
            {
                fielded = Enumerable.Repeat(rack[0], depth).ToList();
            }
            else
            {
                fielded = PreviewFight.EnemiesWithArt(Mathf.Max(
                    request.Enemies ?? PreviewFight.EnemyCountFor(plan.Formation, 3),
                    request.Enemies == null ? depth : 1));
            }

            Assert.IsNotEmpty(fielded, "no enemies in content to cast at");

            // THE PARTY IS THE REAL ONE: the adapter seats it in list order (front
            // rank first) the way a run does. The caster must lead, because the
            // adapter hands the preview skill to the first member and Prepare
            // fills that member's pools.
            var party = request.Party != null && request.Party.Count > 0
                ? new List<string>(request.Party)
                : new List<string>(plan.Party);
            Assert.AreEqual(plan.CasterId, party[0],
                "the party leads with the caster '" + plan.CasterId + "', got '" + party[0] + "'");

            var built = FightEncounterAdapter.Build(
                party,
                fielded,
                new Domain.Rng.SeededRandom(Seed),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { spellId });

            Assert.IsNotNull(built?.Session, "'" + spellId + "' could not be built into an encounter");

            PreviewFight.Prepare(built, plan);
            ApplyPreStatus(built, request.PreStatus);
            AimedTarget = ResolveTarget(built, request.Target);

            yield return BindAndSettle(built);
        }

        // The combatant a scenario's Target names: a party member by character id
        // (PartyIds runs parallel to Party) or an enemy by 1-based slot.
        internal static CombatantState ResolveTarget(FightEncounterAdapter.BuiltFight built, string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return null;
            target = target.Trim();

            if (int.TryParse(target, out int slot))
            {
                var foes = built.Session.Encounter.Enemies;
                Assert.IsTrue(slot >= 1 && slot <= foes.Count, "enemy slot " + slot + " of " + foes.Count);
                return foes[slot - 1];
            }

            int at = -1;
            for (int i = 0; i < built.PartyIds.Count; i++) if (built.PartyIds[i] == target) at = i;
            Assert.GreaterOrEqual(at, 0, "'" + target + "' is not in the party");
            return built.Party[at];
        }

        // THE HERO A NON-SPELL SCENARIO FIGHTS AS: Shawn when content has him,
        // else the first character with battle art (what the showcase capture
        // in PreviewCaptureTests picks).
        public static string HeroId()
        {
            var shawn = ContentDatabase.Characters.FirstOrDefault(c => c != null && c.id == "sheep");
            if (shawn != null) return shawn.id;

            var withArt = ContentDatabase.Characters.FirstOrDefault(
                c => c != null && c.Data != null && !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath));
            return (withArt ?? ContentDatabase.Characters.FirstOrDefault())?.id;
        }

        // A fight with no preview plan: Shawn against `copies` of one enemy,
        // optionally carrying relics, a forced enemy ability and a status.
        public IEnumerator StandTheFightUp(string enemyId, int copies, float speed, IReadOnlyList<string> relicIds,
                                           EnemyShowcase showcase, string preStatus)
        {
            string hero = HeroId();
            Assert.IsNotNull(hero, "no characters in content");

            yield return LoadFight(speed);

            var built = FightEncounterAdapter.Build(
                new List<string> { hero },
                Enumerable.Repeat(enemyId, Mathf.Max(1, copies)).ToList(),
                new Domain.Rng.SeededRandom(Seed),
                relicIds: relicIds,
                depthStep: 0);

            Assert.IsNotNull(built?.Session, "'" + enemyId + "' could not be built into an encounter");

            if (showcase != null) built.Session.Showcase = showcase;
            ApplyPreStatus(built, preStatus);

            yield return BindAndSettle(built);
        }

        // Loads the Fight scene fresh and leaves the controller and beat player
        // found, with the placeholder fight's beat flushed.
        private IEnumerator LoadFight(float speed)
        {
            FightBeatPlayer.BeatSpeedMultiplier = speed;
            Time.captureFramerate = Fps;

            // The ambient animators (LanternFlicker, SlowDrift, StarTwinkle,
            // EmberFlare, StageShake) draw their phases from UnityEngine.Random
            // in Awake. Unseeded, two recordings of one scenario differ by
            // about 1.4% of pixels on the idle stage alone; seeded before the
            // scene loads they draw the same phases (docs/PLAN_GIF_CAPTURE.md 2.3).
            UnityEngine.Random.InitState((int)Seed);

            // A KNOWN TEARDOWN LOG, TOLERATED ACROSS THE SCENE SWAP AND
            // NOWHERE ELSE. Loading Fight over a Fight disables the outgoing
            // one, and FightBeatPlayer.OnDisable -> EndFight -> Flush ->
            // OnPlaybackFinished -> RefreshUi reaches
            // FightController.BeginAppearancePop (FightController.Hud.cs:1218),
            // which calls StartCoroutine on a panel that is already inactive
            // and logs an error. It is a real defect, recorded as AUDIT.md
            // #106; it belongs to fight-HUD teardown rather than to anything
            // recorded here. The tolerance is lifted before the cast.
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
            Debug.Log("[SpellRuntime] pace " + FightBeatPlayer.Pace + " (speed " + speed + ")");

            Fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(Fight, "the fight scene has no controller");

            Beats = UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(Beats, "the fight scene has no beat player");

            // FightBootstrap's own placeholder fight is mid-beat and holds the
            // controller busy, so a forced press would be swallowed.
            Beats.Flush();
            yield return null;
            yield return null;
        }

        private IEnumerator BindAndSettle(FightEncounterAdapter.BuiltFight built)
        {
            built.Session.Begin();
            Fight.Bind(built.Session, EncounterClass.Normal, null, DarknessPresentation(Darkness));
            Fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            Canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(Canvas, "the fight scene has no root canvas");

            // Seeded again here: whatever drew from UnityEngine.Random while the
            // scene loaded and settled did so a variable number of times, and the
            // shake offsets of the cast about to be recorded draw from the same
            // stream.
            UnityEngine.Random.InitState((int)Seed);

            // THE CAST'S CLOCK COUNTS FROM HERE, not from engine start. A layer
            // is drawn when Now() passes start + offset, and authored offsets
            // are whole capture frames (0.6 s is exactly frame 36), so the
            // comparison sits on a float boundary. Time.time is the absolute
            // engine clock and how many frames the scene load took varies, which
            // moved the rounding and showed a layer one frame early or late in
            // one of two recordings of the same scenario. From here to the cast
            // the frame count is fixed, so the same instants round the same way.
            // Time.timeAsDouble keeps timeScale and hit-stop stopping the clock
            // exactly as Time.time does.
            double clockBase = Time.timeAsDouble;
            SpellPerformancePlayer.ClockOverride = () => (float)(Time.timeAsDouble - clockBase);
        }

        private static void ApplyPreStatus(FightEncounterAdapter.BuiltFight built, string preStatus)
        {
            if (string.IsNullOrWhiteSpace(preStatus)) return;

            Assert.IsTrue(Enum.TryParse(preStatus.Trim(), true, out StatusEffectType pre),
                "pre-status '" + preStatus + "' is not a StatusEffectType");
            foreach (var foe in built.Session.Encounter.Enemies.Where(e => e != null))
            {
                foe.Statuses.Add(new ActiveStatus(pre, 3, 3));
            }
        }

        // The pre-status of the scenario being stood up, read by ResolvePlan.
        private string _preStatus;

        // A spell the preview refuses only for want of a status on the target
        // (Ashen Reckoning needs Poison) is a real cast when the scenario puts
        // that status there, so it is recorded with its numbers and badges.
        private bool PreStatusSatisfies(ResolvedSkill skill) =>
            skill != null && skill.HasRequiresStatus && !string.IsNullOrWhiteSpace(_preStatus)
            && Enum.TryParse(_preStatus.Trim(), true, out StatusEffectType pre) && pre == skill.RequiresStatusType;

        private PreviewFight.Plan ResolvePlan(string spellId)
        {
            PresentationOnly = false;
            var plan = PreviewFight.ForSpell(spellId);
            if (plan.Ok || plan.Skill == null || PreviewStage.IsSupported(plan.Skill.Effect)) return plan;

            if (plan.Skill.Effect == SkillEffect.Reclaim && PreStatusSatisfies(plan.Skill))
            {
                var roster0 = ContentDatabase.Characters
                    .Where(c => c != null && c.Data != null).Select(c => c.Data).ToList();
                var real = new PreviewFight.Plan { Skill = plan.Skill, Formation = PreviewFight.FormationLone };
                real.CasterId = PreviewFight.ChooseCaster(plan.Skill, roster0, real);
                if (real.CasterId == null) return real;
                real.Party = new List<string> { real.CasterId };
                real.Notes.Add("'" + spellId + "' resolves as Reclaim; the scenario's pre-status satisfies it, so it is a real cast");
                return real;
            }

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
            stand.Notes.Add("presentation only: '" + spellId + "' resolves as " + plan.Skill.Effect +
                            ", which the preview stage refuses, so the vfx is played through the module with " +
                            "no damage number, status or session cue");
            PresentationOnly = true;
            return stand;
        }

        // ---- spell and relic recording -----------------------------------------

        // Who the presentation is aimed at. A skill owned by an enemy that is
        // not in the player roster (the Bellwether's chains) is cast by the
        // first enemy at the player's caster, the direction it flies in a
        // real fight; everything else is cast by the player's caster, at an
        // enemy for enemy-facing skills and at the caster for the rest.
        public CombatBeat PresentationBeat(string spellId)
        {
            var enc = Fight.SessionForTest.Encounter;
            var caster = enc.PlayerParty.First(c => c != null);
            var foes = enc.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var skill = PreviewFight.ForSpell(spellId).Skill;

            bool enemyOwned = !string.IsNullOrWhiteSpace(skill.CharacterId) &&
                              !ContentDatabase.Characters.Any(c => c != null && c.id == skill.CharacterId);
            bool aimsAtEnemies = skill.Targeting == SkillTargeting.SingleEnemy ||
                                 skill.Targeting == SkillTargeting.AllEnemies;

            var beat = new CombatBeat { Vfx = ShippedVfx(spellId) };
            bool aimedAtFoe = AimedTarget != null && foes.Contains(AimedTarget);
            if (enemyOwned && aimsAtEnemies)
            {
                beat.Actor = foes[0];
                beat.Target = caster;
            }
            else
            {
                beat.Actor = caster;
                beat.Target = AimedTarget ?? (aimsAtEnemies ? foes[0] : caster);

                // The other end of a swap: the second party member.
                if (skill.Effect == SkillEffect.SwapAllies)
                {
                    beat.Counterpart = enc.PlayerParty.FirstOrDefault(c => c != null && !ReferenceEquals(c, caster));
                }
                if (skill.Targeting == SkillTargeting.AllEnemies) beat.SplashTargets = foes.Skip(1).ToList();
            }

            return beat;
        }

        public static SpellPresentation ShippedVfx(string skillId)
        {
            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            Assert.IsNotNull(definition, "'" + skillId + "' is not in the content database");

            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.IsTrue(vfx != null && vfx.HasArt, "'" + skillId + "' resolves to a presentation with no art");
            return vfx;
        }

        public static SpellPresentation RelicVfx(string relicId)
        {
            var definition = ContentDatabase.Relics.FirstOrDefault(r => r != null && r.id == relicId);
            Assert.IsNotNull(definition, "'" + relicId + "' is not in the content database");

            var vfx = definition.Data.Vfx;
            Assert.IsTrue(vfx != null && vfx.HasArt, "'" + relicId + "' has no vfx to record");
            return vfx;
        }

        // How long a cast plays before nothing of it is drawn, from
        // SpellPerformance -- the same expression FightController.ResolveCast
        // uses to release a handle, read here from Resources rather than a
        // live SpellVfxPlayer so it works before any player has loaded frames.
        private static float ClearedSecondsOf(SpellPresentation vfx, int targetCount) =>
            SpellPerformance.Resolve(vfx, targetCount, path => FrameSequenceLoader.Load(path)?.Length ?? 0)
                .ClearedSeconds;

        // A CAST, EVERY FRAME OF IT, until its own clock says it is done.
        // `spellId` is the skills.json id, or the relic id when `relic` is set
        // (a relic is always a presentation-only beat: nothing in the session
        // plays its vfx on demand).
        public IEnumerator RecordCast(string spellId, bool relic, float speed, CaptureSink sink, CaptureOutcome outcome,
                                      int frameBase = 0, bool recordLeadIn = false)
        {
            var fight = Fight;
            var canvas = Canvas;

            var handle = CastHandle.None;
            SpellPresentation shipped;
            int targetCount;

            if (relic)
            {
                shipped = RelicVfx(spellId);
                var enc = fight.SessionForTest.Encounter;
                var beat = new CombatBeat
                {
                    Actor = enc.PlayerParty.First(c => c != null),
                    Target = enc.Enemies.First(e => e != null && e.IsAlive),
                    Vfx = shipped,
                };
                PresentationOnly = true;
                handle = fight.PlaySpellVfxForTest(beat);
                Assert.IsTrue(handle.IsLive, "'" + spellId + "' obtained no cast when played through the module");
                targetCount = 1;
            }
            else
            {
                if (PresentationOnly)
                {
                    handle = fight.PlaySpellVfxForTest(PresentationBeat(spellId));
                    Assert.IsTrue(handle.IsLive, "'" + spellId + "' obtained no cast when played through the module");
                }
                else
                {
                    fight.ForceFirstAction(spellId);
                    if (AimedTarget != null) fight.ForceFirstActionTarget(AimedTarget);

                    float armed = Time.realtimeSinceStartup;
                    while (!fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
                    Assert.IsTrue(fight.IsBusy, "'" + spellId + "' was never cast -- the log says what was refused");
                }

                shipped = null;
                targetCount = 0;
            }

            // FRAME ZERO IS THE FRAME THE CAST FIRST DRAWS, so the series opens
            // on the ball leaving rather than on however many frames of wind-up
            // the beat happened to take. NOT the same instant as release on the
            // cast's own clock, though -- a beat's hit-stop can hold that clock
            // (Time.time) still while Update, and a drawn frame with it, keeps
            // running, so "first drawn" and "release" can be capture frames
            // apart. That is why the recording below polls the cast's own
            // Cursor rather than assuming a frame count derived from it.
            int waited = 0;
            // A later cast of a repeated series films its lead-in too (the press
            // to the first drawn frame), so the series has no jump between casts.
            while (!AnythingDrawn(fight) && waited < 240)
            {
                if (recordLeadIn) Render(canvas, sink.FramePath(frameBase + waited));
                waited++;
                yield return null;
            }

            int offset = frameBase + (recordLeadIn ? waited : 0);

            Assert.Less(waited, 240, "the cast never drew anything, so there is no recording to make");

            // THE HANDLE THIS RECORDING BELONGS TO. ForceFirstAction drives
            // the cast through the normal beat flow rather than through
            // PlaySpellVfxForTest, so LastBegunForTest is the only way to
            // name it -- and it must already be live, because AnythingDrawn
            // above only turns true once Paint has run for a live cast.
            var module = fight.PerformancePlayerForTest;
            if (!PresentationOnly) handle = module.LastBegunForTest;
            Assert.IsTrue(module.IsLive(handle),
                "'" + spellId + "' drew something on stage but LastBegunForTest names no live cast, so " +
                "there is no clock to record this series against.");

            // THE STRUCK COUNT, NOT THE LIVING COUNT. The cast's damage resolves
            // before its picture opens, so a spell that kills its target has no
            // living enemy left to count here; per-target layers then fan out to
            // zero instances and the cleared time collapses to the hit cue, which
            // ended the recording at the cue plus the margin and cut every
            // aftermath that outlives a kill (death_knell's wave).
            if (!relic)
            {
                var plannedSkill = ResolvePlan(spellId).Skill;
                targetCount = plannedSkill != null && plannedSkill.Targeting == SkillTargeting.AllEnemies
                    ? fight.SessionForTest.Encounter.Enemies.Count(e => e != null)
                    : 1;
                shipped = ShippedVfx(spellId);
            }

            float clearedSeconds = ClearedSecondsOf(shipped, targetCount);
            float hitCueSeconds = SpellPerformance.Resolve(shipped, targetCount,
                path => FrameSequenceLoader.Load(path)?.Length ?? 0).HitCueSeconds;
            float targetCursor = Mathf.Min(clearedSeconds + FrameEndMargin, MaxCaptureSeconds);

            // POLLED AGAINST THE CAST'S OWN CLOCK. A do/while rather than a
            // while so the frame the clock actually crosses targetCursor on is
            // still recorded.
            int lit = 0;
            int frame = 0;
            bool reached;

            // A short cast frees its handle before the target cursor, and
            // HasReached reads a freed handle as reached. The recording keeps
            // FrameEndMargin more seconds after that so the aftermath (the
            // target's own hurt reaction) is on film.
            int freedFrame = -1;
            int tailFrames = Mathf.CeilToInt(FrameEndMargin * Fps);

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

                // AT THE END OF THE FRAME, after every coroutine has run: a
                // picture taken from this coroutine showed the beat's own
                // coroutine work (badges, log, numbers) one frame after the
                // cast's Update-driven layers, a lag the player never sees.
                yield return new WaitForEndOfFrame();

                string path = sink.FramePath(offset + frame);
                Render(canvas, path);
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

            // Pacing and contact for tools/spell_feel.py and tools/gif.py,
            // beside the frames.
            File.WriteAllText(
                sink.TimingPath,
                string.Format(CultureInfo.InvariantCulture,
                    "{{\"spell\":\"{0}\",\"fps\":{1},\"frames\":{2},\"firstDrawnWaitFrames\":{3}," +
                    "\"clearedSeconds\":{4:F3},\"hitFrame\":{5},\"speed\":{6:F2}," +
                    "\"freedFrame\":{7},\"presentationOnly\":{8}}}",
                    spellId, Fps, frames, waited, clearedSeconds, hitFrame, speed,
                    freedFrame, PresentationOnly ? "true" : "false"));

            outcome.NextFrame = offset + frames;
            outcome.Frames = frames;
            outcome.LitFrames = lit;
            outcome.FirstDrawnWaitFrames = waited;
            outcome.HitFrame = hitFrame;
            outcome.FreedFrame = freedFrame;
            outcome.ClearedSeconds = clearedSeconds;
            outcome.PresentationOnly = PresentationOnly;
            outcome.TargetCursor = targetCursor;

            Assert.IsTrue(reached || freedFrame >= 0,
                "'" + spellId + "' had not reached " + targetCursor.ToString("F2") + "s on its own " +
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
                "only " + lit + " of " + frames + " recorded frames had anything drawn on them -- '" + spellId +
                "' cast at " + Fps + "fps, so a series this empty is a recording of the stage " +
                "rather than of the cast.");

            Debug.Log("[SpellRuntime] wrote " + frames + " frames (" + lit +
                      " of them with the cast drawing; frame 0 is the first drawn, " + waited +
                      " frames after the press; recorded until the cast's own clock reached " +
                      targetCursor.ToString("F2") + "s of " + clearedSeconds.ToString("F2") + "s cleared).");
        }

        // The most a pacing wait between two casts of a series may take.
        private const int MaxPacingFrames = 10 * Fps;

        // THE SAME SPELL `count` TIMES IN ONE FIGHT, one continuous frame series.
        // Between casts the fight plays on at its own pace (the enemies' turns,
        // the beats) and every frame of it is filmed; the next cast is pressed
        // when the player's turn is back. Mana is refilled and the cooldown run
        // down first, the way PreviewFight.Prepare waives them for the first
        // cast, so a series of five measures the spell's repeats and not the
        // caster's purse.
        public IEnumerator RecordRepeatedCast(string spellId, int count, float speed, CaptureSink sink,
                                              CaptureOutcome outcome)
        {
            int next = 0;
            int lit = 0;
            int hitFrame = -1;
            float cleared = 0f;

            for (int cast = 0; cast < count; cast++)
            {
                if (cast > 0)
                {
                    var session = Fight.SessionForTest;
                    var caster = session.Encounter.LivingPlayerParty.First();
                    caster.PrimaryPool.Gain(caster.PrimaryPool.Max);
                    int guard = 0;
                    while (session.CooldownRemaining(caster, spellId) > 0 &&
                           session.CooldownRemaining(caster, spellId) != FightSession.SpentForFight && guard++ < 50)
                    {
                        session.TickCooldownsForTest(caster);
                    }

                    int paced = 0;
                    while ((Fight.IsBusy || Beats.IsPlaying || !session.IsPlayerTurn) && paced < MaxPacingFrames)
                    {
                        Render(Canvas, sink.FramePath(next++));
                        paced++;
                        yield return null;
                    }

                    Assert.Less(paced, MaxPacingFrames, "the player's turn never came back before cast " + (cast + 1));
                }

                var one = new CaptureOutcome();
                var legSink = new CaptureSink { FramePath = sink.FramePath, TimingPath = sink.TimingPath };
                yield return RecordCast(spellId, relic: false, speed, legSink, one, next, recordLeadIn: cast > 0);
                next = one.NextFrame;
                lit += one.LitFrames;
                if (cast == 0)
                {
                    hitFrame = one.HitFrame;
                    cleared = one.ClearedSeconds;
                    outcome.FirstDrawnWaitFrames = one.FirstDrawnWaitFrames;
                }
            }

            File.WriteAllText(
                sink.TimingPath,
                string.Format(CultureInfo.InvariantCulture,
                    "{{\"spell\":\"{0}\",\"fps\":{1},\"frames\":{2},\"firstDrawnWaitFrames\":{3}," +
                    "\"clearedSeconds\":{4:F3},\"hitFrame\":{5},\"speed\":{6:F2}," +
                    "\"freedFrame\":-1,\"presentationOnly\":{7},\"repeat\":{8}}}",
                    spellId, Fps, next, outcome.FirstDrawnWaitFrames, cleared, hitFrame, speed,
                    PresentationOnly ? "true" : "false", count));

            outcome.Frames = next;
            outcome.NextFrame = next;
            outcome.LitFrames = lit;
            outcome.HitFrame = hitFrame;
            outcome.ClearedSeconds = cleared;
            outcome.PresentationOnly = PresentationOnly;
        }

        // ---- attack and status recording ---------------------------------------

        // THE ENEMY'S FORCED ACTION, through the buttons a hand would press:
        // Verb0 (attack) and the front enemy's plate. The round it starts is the
        // player's swing and then the enemy's reply, which the session's
        // EnemyShowcase has already decided. Recording runs from the press until
        // the beat player and the controller are both idle, plus FrameEndMargin.
        public IEnumerator RecordAttackRound(CaptureSink sink, float speed, string enemyId, string forcedSkill,
                                             CaptureOutcome outcome)
        {
            var fight = Fight;

            // The opening hold and any enemy-first turn play out before the
            // press; the controller refuses one while it is busy.
            int settle = 0;
            while ((fight.IsBusy || Beats.IsPlaying || !fight.SessionForTest.IsPlayerTurn) && settle < 600)
            {
                settle++;
                yield return null;
            }

            Assert.Less(settle, 600, "the fight never reached the player's turn");

            var verb = fight.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "Verb0");
            Assert.IsNotNull(verb, "the fight has no attack verb button");
            verb.onClick.Invoke();
            yield return null;

            var plate = fight.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "EnemyPlate0" && b.gameObject.activeInHierarchy);
            Assert.IsNotNull(plate, "the attack verb opened no enemy plate to pick");
            plate.onClick.Invoke();

            int frame = 0;
            int idleFrames = 0;
            int tailFrames = Mathf.CeilToInt(FrameEndMargin * Fps);
            int cap = Mathf.CeilToInt(MaxAttackSeconds * Fps);
            bool started = false;

            while (frame < cap)
            {
                started |= fight.IsBusy || Beats.IsPlaying;

                Render(Canvas, sink.FramePath(frame));
                yield return null;
                frame++;

                bool idle = started && !fight.IsBusy && !Beats.IsPlaying;
                idleFrames = idle ? idleFrames + 1 : 0;
                if (idleFrames >= tailFrames) break;
            }

            Assert.IsTrue(started, "'" + enemyId + "' never played a round after the press");
            Assert.Less(frame, cap, "'" + enemyId + "' was still playing after " + MaxAttackSeconds + "s");

            File.WriteAllText(sink.TimingPath, TimingJson("attack", enemyId, forcedSkill, Fps, frame, speed));

            outcome.Frames = frame;
        }

        // timing.json for the attack and status recorders. The speed is formatted apart from the
        // format string: "{4:F2}}}" parses the closing braces as part of the F2 specifier and
        // writes the text "F2" instead of the number.
        internal static string TimingJson(string kind, string id, string skill, int fps, int frames, float speed)
        {
            string skillField = skill == null ? "" : ",\"skill\":\"" + skill + "\"";
            return "{\"kind\":\"" + kind + "\",\"id\":\"" + id + "\"" + skillField +
                   ",\"fps\":" + fps.ToString(CultureInfo.InvariantCulture) +
                   ",\"frames\":" + frames.ToString(CultureInfo.InvariantCulture) +
                   ",\"speed\":" + speed.ToString("F2", CultureInfo.InvariantCulture) + "}";
        }

        // A FIXED SPAN OF AN IDLE STAGE, for a status the enemies already carry.
        // The recording opens on the first frame after the fight settles.
        public IEnumerator RecordSeconds(CaptureSink sink, float speed, string kind, string id, float seconds,
                                         CaptureOutcome outcome)
        {
            int frames = Mathf.Max(1, Mathf.CeilToInt(seconds * Fps));
            for (int frame = 0; frame < frames; frame++)
            {
                Render(Canvas, sink.FramePath(frame));
                yield return null;
            }

            File.WriteAllText(sink.TimingPath, TimingJson(kind, id, null, Fps, frames, speed));

            outcome.Frames = frames;
        }

        // ---- drawing probes ----------------------------------------------------

        public static bool AnythingDrawn(FightController fight) =>
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

        // What a scenario leaves behind that the next one must not inherit.
        public static void Restore()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
            TestGlobals.ResetAll();
        }
    }
}
