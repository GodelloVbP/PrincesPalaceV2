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
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE PICTURES tools/preview.ps1 -Enemy <id> writes, and nothing else.
    //
    // [Explicit], so it never runs as part of a slice or the commit gate --
    // NUnit will not pick an explicit fixture up unless something names it,
    // and preview.ps1 names it through graphics_tests.ps1 -Filter. Two reasons
    // it has to be kept apart, and they pull the same way:
    //
    //   THIS IS THE ONLY CODE IN THE REPO THAT READS PP_PREVIEW_IDS. A
    //   verification fixture that read an environment variable would test a
    //   different thing depending on the shell it was started from, which is
    //   the one property a gate cannot have. PreviewEnvironmentLintTests
    //   asserts that exactly one test file mentions the name.
    //
    //   Screenshots are an addition, not the gate. What actually protects new
    //   monster art on a machine with no GPU is EnemyArtCompletenessTests,
    //   headless, over every enemy. This is here so a human can look.
    //
    // Two kinds of picture per id:
    //
    //   <id>_stances.png       -- a contact sheet of every pose the mob can
    //                             reach, composited from the sprites
    //                             themselves. Needs no graphics device: it is
    //                             pixels copied, not a scene rendered.
    //   <id>_turn<n>_<ability>.png
    //                          -- the mob on the real stage, one frame per turn
    //                             of its showcase, named for what it did. This
    //                             is the half that needs a device.
    public class PreviewCaptureTests
    {
        // NAMED ONCE EACH. The lint test finds this file by looking for the
        // PP_PREVIEW_ prefix, so every one of these lives here and in the
        // script that sets it, and nowhere else.
        private const string IdsVariable = "PP_PREVIEW_IDS";
        private const string SpellVariable = "PP_PREVIEW_SPELL";
        private const string CharacterVariable = "PP_PREVIEW_CHARACTER";

        // OPTIONAL, AND THEREFORE NOT THROUGH RequiredIds. Every other
        // PP_PREVIEW_* variable answers "is this run asking for my mode"; this
        // one narrows a mode that is already asked for, so an empty value is a
        // legal -Spell run rather than a reason to ignore the test.
        private const string ElementVariable = "PP_PREVIEW_ELEMENT";

        // OPTIONAL, THE SAME WAY: which enemies a -Spell capture is cast at,
        // by id, instead of the first ones with art in sort order. A spell that
        // sizes to its target has to be looked at on a small body and a tall
        // one, and the sort order only ever fielded the small one.
        private const string VersusVariable = "PP_PREVIEW_VERSUS";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "preview"));

        // AN UNASKED-FOR MODE IGNORES, IT DOES NOT FAIL.
        //
        // Run by CLASS (by hand, through graphics_tests.ps1), every capture in
        // it runs and at most one has its variable set. The others used to
        // fail with "PP_PREVIEW_* is empty" -- red tests beside correct
        // pictures. preview.ps1 itself names only its mode's tests (see its
        // Invoke-PreviewCapture): graphics_tests.ps1 counts a Skipped result
        // as a failure, so running the whole class exited 1 on every preview.
        //
        // Ignore rather than a silent early return: an ignored test is
        // reported by name, so a preview that photographed nothing because the
        // variable never reached Unity still says so.
        private static string[] RequiredIds(string variable, string mode)
        {
            var ids = Requested(variable);
            if (ids.Length == 0)
            {
                Assert.Ignore($"{variable} is not set, so this run is not asking for {mode}. " +
                              $"tools/preview.ps1 {mode} sets it.");
            }

            return ids;
        }

        private static string[] Requested(string variable)
        {
            string raw = System.Environment.GetEnvironmentVariable(variable) ?? "";
            return raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        // FAST BY DEFAULT, and the spell capture turns it back down itself. A
        // stance contact sheet does not care how long a beat took; a
        // photograph OF the timing does, and the two live in one fixture.
        [SetUp]
        public void PlayBeatsFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 8f;

            // docs/archive/PLAN_BATTLE_SPEED.md G3: several fixed WaitForSecondsRealtime
            // calls below (the stage settle, the map/dossier/fight captures,
            // the 1/30s-per-frame spell sampling) are calibrated against
            // Pace == BeatSpeedMultiplier exactly, at both 8x and the local
            // 1x override further down. Pinned here so FightBootstrap
            // installing the settings-backed source cannot stretch any of
            // them past their budget.
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            StanceManifestLoader.Reset();

            // Global engine state the spell capture sets; leaking it would
            // repace every test that runs after this one.
            Time.captureFramerate = 0;
        }

        // The contact sheet. Deliberately built from the sprite pixels rather
        // than by posing the mob on the stage six times: every stance appears
        // at its authored size, side by side, which is the arrangement that
        // makes a mis-registered drawing obvious at a glance -- and it is the
        // one shot that can be produced on a machine that cannot render.
        [Test, Explicit("Written by tools/preview.ps1 -Enemy <id>.")]
        public void CaptureStanceSheets()
        {
            var ids = RequiredIds(IdsVariable, "-Enemy <id>");

            Directory.CreateDirectory(OutputDir);

            foreach (string id in ids)
            {
                var enemy = ContentDatabase.Enemies.FirstOrDefault(e => e != null && e.id == id);
                Assert.IsNotNull(enemy, $"'{id}' is not in the content database -- rebuild content first.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(enemy.Data.SpritePath),
                    $"'{id}' has no spritePath, so there is nothing to photograph.");

                var stances = EnemyArtCompletenessTests.StancesReachableBy(enemy).OrderBy(s => s).ToList();
                var sprites = new List<(string Stance, Sprite Sprite)>();

                foreach (string stance in stances)
                {
                    var sprite = Resources.Load<Sprite>($"{enemy.Data.SpritePath}/{stance}");
                    if (sprite != null) sprites.Add((stance, sprite));
                }

                Assert.IsNotEmpty(sprites, $"'{id}' resolved none of its stances: {string.Join(", ", stances)}");

                string path = Path.Combine(OutputDir, $"{id}_stances.png");
                WriteContactSheet(sprites, path);
                Debug.Log($"[PreviewCapture] wrote {path} ({sprites.Count} stances: " +
                          string.Join(", ", sprites.Select(s => s.Stance)) + ")");
            }
        }

        // One frame per turn of the showcase, on the real stage, through the
        // same buttons a player presses -- so what is photographed is the
        // fight, not a fixture wearing the right sprite folder.
        [UnityTest, Explicit("Written by tools/preview.ps1 -Enemy <id>.")]
        public IEnumerator CaptureShowcaseTurns()
        {
            var ids = RequiredIds(IdsVariable, "-Enemy <id>");

            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. tools/preview.ps1 runs this through graphics_tests.ps1, " +
                              "which omits -nographics for exactly this reason.");
            }

            Directory.CreateDirectory(OutputDir);

            foreach (string id in ids)
            {
                yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var fight = Object.FindAnyObjectByType<FightController>();
                Assert.IsNotNull(fight, "the fight scene has no controller");

                var hero = ContentDatabase.Characters.FirstOrDefault(
                    c => c != null && !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath))
                    ?? ContentDatabase.Characters.FirstOrDefault();
                Assert.IsNotNull(hero, "no characters in content");

                // The SAME fight -Launch shows: FightBootstrap's placeholder,
                // its fixed seed, one copy of the mob. A different construction
                // here would make the photograph evidence about something the
                // author cannot reproduce by pressing play.
                var built = FightEncounterAdapter.Build(
                    new List<string> { hero.id },
                    new List<string> { id },
                    new Domain.Rng.SeededRandom(20260810),
                    relicIds: null,
                    depthStep: 0);

                Assert.IsNotNull(built?.Session, $"'{id}' could not be built into an encounter");

                var showcase = new EnemyShowcase(Debug.Log);
                built.Session.Showcase = showcase;
                built.Session.Begin();
                fight.Bind(built.Session, EncounterClass.Normal);

                yield return new WaitForSecondsRealtime(1.2f);

                var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(canvas);

                int turns = EnemyShowcase.ScriptLength(SourcePoolFor(built.Session));

                for (int turn = 1; turn <= turns; turn++)
                {
                    // One swing, driven through the button a player would press
                    // -- the same seam EnemyFightableTests uses.
                    var attack = fight.GetComponentsInChildren<Button>(includeInactive: true)
                        .FirstOrDefault(b => b.name == "Verb0");
                    if (attack == null) break;
                    attack.onClick.Invoke();
                    yield return null;

                    var target = fight.GetComponentsInChildren<Button>(includeInactive: true)
                        .FirstOrDefault(b => b.name == "EnemyPlate0" && b.gameObject.activeInHierarchy);
                    if (target != null) target.onClick.Invoke();

                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

                    // NAMED FOR WHAT THE MOB ACTUALLY DID, read back off the
                    // showcase rather than guessed from the authored order --
                    // an entry whose prerequisite was unmet was skipped, and a
                    // filename claiming otherwise would be the exact lie the
                    // skip-and-report rule exists to prevent.
                    string label = showcase.Played.Count >= turn ? showcase.Played[turn - 1] : "unknown";
                    string path = Path.Combine(OutputDir, $"{id}_turn{turn}_{Slug(label)}.png");
                    CanvasCapture.RenderToFile(canvas, path);
                    Debug.Log($"[PreviewCapture] wrote {path}");

                    if (built.Session.IsOver) break;
                }

                if (showcase.Skipped.Count > 0)
                {
                    Debug.LogWarning($"[PreviewCapture] '{id}' could not show: " +
                                     string.Join(", ", showcase.Skipped));
                }
            }
        }

        // The pool the showcase is walking, so the turn count is the kit's
        // length rather than a number picked here.
        private static IReadOnlyList<EnemyAbility> SourcePoolFor(FightSession session)
        {
            var enemy = session.Encounter.LivingEnemies.FirstOrDefault();
            return enemy == null ? null : session.SourceFor(enemy)?.Abilities;
        }

        private static string Slug(string label)
        {
            var kept = label.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
            return new string(kept).Trim('_');
        }

        // ---- tools/preview.ps1 -Spell <id> ------------------------------------

        // ONE CAST, PHOTOGRAPHED AROUND THE MOMENT IT LANDS.
        //
        // Three frames, not a strip: the one before the blow, the blow, and
        // the one after. Everything an author checks when they preview a spell
        // is at that instant -- is the effect ON the target or beside it, does
        // the impact frame arrive with the number, does the ground layer sit
        // under everyone or in front of them -- and a forty-frame series
        // buries those three among thirty-seven that all look the same.
        //
        // WHEN the impact is comes from FightController.ImpactDelayFor, the
        // same function FightBeatPlayer waits out before it moves the numbers.
        // Recomputing "seconds times the impact fraction" here would make this
        // a test of a formula this file wrote, which is the tautology
        // CLAUDE.md gotcha 5 names. What the capture does check independently
        // is that the scheduled instant matched the observed one: the damage
        // popup rising is the beat's own statement about when the blow landed,
        // and both sample numbers are logged side by side so a disagreement is
        // visible rather than assumed away.
        [UnityTest, Explicit("Written by tools/preview.ps1 -Spell <id>.")]
        public IEnumerator CaptureSpellCast()
        {
            var ids = RequiredIds(SpellVariable, "-Spell <id>");

            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. tools/preview.ps1 runs this through graphics_tests.ps1, " +
                              "which omits -nographics for exactly this reason.");
            }

            Directory.CreateDirectory(OutputDir);

            string element = Requested(ElementVariable).FirstOrDefault();

            foreach (string id in ids)
            {
                yield return CaptureOneSpell(id, element);
            }
        }

        // Sampled at REAL SPEED. The whole subject is when things happen
        // relative to each other, and a beat run at 8x samples three frames
        // out of a cast that is over in a tenth of a second.
        private const float SpellSampleSeconds = 1f / 30f;

        // How far either side of the impact the flanking frames sit. Two
        // samples, so "before" is genuinely before the blow rather than a
        // rounding of the same instant.
        private const int FlankSamples = 2;

        // AND A FOURTH, FAR ENOUGH OUT TO SEE WHAT IS STILL FALLING. Nine
        // samples is 0.30s, which is where the pilot's droplets live: its
        // spray bursts at the cue with lives of 0.18-0.34s, so "after" at two
        // samples (0.067s) catches the crown at full extent and nothing at all
        // of the shed. A layered spell's tail is most of what distinguishes it
        // from the single sheet it replaced, and three frames around the blow
        // photographed exactly none of it.
        private const int TailSamples = 9;

        // How long to wait after the press for the cast it begins. Four
        // seconds of sampled time, several times the longest wind-up and lunge
        // this game has; a press that begins no cast in that time fails.
        private const int ReleaseFrameBudget = 120;

        private IEnumerator CaptureOneSpell(string id, string element)
        {
            // THE PLAN IS THE SAME ONE -Launch USES, refusals included. A
            // capture that quietly built a fight the Editor route would have
            // refused is exactly the "evidence about something the author
            // cannot reproduce" this fixture's other test guards against. That
            // includes -Element: an element the skill does not offer is a
            // refusal here, not a picture of whatever it does offer.
            var plan = PreviewFight.ForSpell(id, element);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Debug.Log("[PreviewCapture] " + PreviewFight.Describe(plan));

            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            // A FIXED 1/30s PER FRAME, WHATEVER A FRAME ACTUALLY COSTS. The
            // loop below used to compare realtimeSinceStartup against
            // sample * SpellSampleSeconds -- and one CanvasCapture.RenderToFile
            // is far more than 33ms, so writing the first picture made the next
            // two samples already overdue and they were taken back to back,
            // hundreds of milliseconds late. Measured on the Water pilot: the
            // "before" frame caught the splash at full crown and "impact",
            // nominally 67ms later, caught an empty stage -- the whole cast had
            // finished between two consecutive samples of a 30fps series.
            //
            // captureFramerate makes Time.time (which the spell reads) and
            // WaitForSeconds (which the beat waits on) advance by exactly one
            // sample per frame, so a sample number IS an instant. Restored at
            // the end of this method and again in TearDown -- it is global
            // engine state and leaking it would repace every test after it.
            Time.captureFramerate = Mathf.RoundToInt(1f / SpellSampleSeconds);

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");

            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player, "the fight scene has no beat player");

            // FightBootstrap has already opened its own placeholder fight and
            // is playing its opening beats; left running they hold the
            // controller busy and the forced press is swallowed. Same move
            // CinderfaultSpellCaptureTests makes, for the same reason.
            player.Flush();
            yield return null;
            yield return null;

            var versus = Requested(VersusVariable);
            var enemies = versus.Length > 0
                ? versus.ToList()
                : PreviewFight.EnemiesWithArt(PreviewFight.EnemyCountFor(plan.Formation, 3));
            Assert.IsNotEmpty(enemies, "no enemies in content to cast at");

            var built = FightEncounterAdapter.Build(
                new List<string>(plan.Party),
                enemies,
                new Domain.Rng.SeededRandom(20260810),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { id });

            Assert.IsNotNull(built?.Session, "'" + id + "' could not be built into an encounter");

            PreviewFight.Prepare(built, plan);
            built.Session.Begin();
            fight.Bind(built.Session, EncounterClass.Normal);
            fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "the fight scene has no root canvas");

            // WHEN the blow lands, asked of the controller rather than
            // recomputed. A beat carrying this skill's own presentation is all
            // ImpactDelayFor reads.
            // THE PRESENTATION THE PREVIEW WILL ACTUALLY CAST, which for an
            // elemental spell is not the skill's. prismatic_orb authors none
            // of its own and its Water element authors five layers, so timing
            // the samples off plan.Skill.Vfx put every one of them before the
            // cue -- a picture of the right spell at the wrong instants.
            // PreviewFight owns which element is pressed, so this asks it
            // rather than deciding again.
            float impactSeconds = fight.ImpactDelayFor(new CombatBeat
            {
                Actor = built.Party.FirstOrDefault(),
                Target = built.Session.Encounter.LivingEnemies.FirstOrDefault(),
                Vfx = PreviewFight.PreviewPresentationOf(plan.Skill, plan.Element),
            });

            var module = fight.PerformancePlayerForTest;
            Assert.IsNotNull(module, "the fight scene has no performance player");

            // THE CAST THIS PRESS BEGINS, by its handle -- see the anchoring
            // comment below. Read before the press so the one begun by it is
            // the first handle that differs.
            var beforePress = module.LastBegunForTest;

            // THE CUE, AS THE CAST'S OWN SCHEDULE CROSSES IT. Recorded so the
            // log can say which frame the "_impact" file landed on against the
            // frame the blow was actually dispatched, instead of assuming.
            int cueCrossedAt = -1;
            int frame = 0;
            var cast = CastHandle.None;
            System.Action<CastHandle> onCue = h =>
            {
                if (cueCrossedAt < 0 && h.Equals(cast)) cueCrossedAt = frame;
            };
            module.HitCueCrossedForTest += onCue;

            fight.ForceFirstAction(id, plan.Element);

            // Wait for the press to be taken: the controller polls it in
            // Update and refuses while anything else is still playing.
            float armed = Time.realtimeSinceStartup;
            while (!fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
            Assert.IsTrue(fight.IsBusy, "'" + id + "' was never cast -- the log says what the caster refused");

            // THE ELEMENT IS IN THE FILENAME, when one was asked for. Four
            // elements over one prefix would mean each capture overwrote the
            // last, and the four sets exist precisely to be compared -- the
            // delivery that found AUDIT #107 was reordering skills.json between
            // runs partly because the pictures collided as well.
            string prefix = "spell_" + id +
                (string.IsNullOrEmpty(plan.Element) ? "" : "_" + plan.Element.ToLowerInvariant()) +
                (versus.Length > 0 ? "_vs_" + string.Join("-", versus) : "");

            var wanted = SpellSampleSchedule(impactSeconds)
                .Select(s => (Name: prefix + "_" + s.Name + ".png", s.CastSeconds))
                .ToList();

            // SAMPLE ZERO IS THE FRAME THIS CAST BEGINS, named by its handle.
            //
            // AUDIT #207: this used to wait for "any effect renderer showing a
            // frame", on the theory that nothing is drawn before the release.
            // Something always was -- every sheep spell logged "0 frames after
            // the press" -- so the samples were counted from the press, early
            // by the whole wind-up and lunge, and Crownfall's "_impact" file
            // showed its spike still in the air (QA read it as a miss). The
            // handle is the cast itself, whatever else happens to be on screen.
            int waited = 0;
            while (waited < ReleaseFrameBudget &&
                   (module.LastBegunForTest.Equals(beforePress) || !module.LastBegunForTest.IsLive))
            {
                waited++;
                yield return null;
            }

            Assert.Less(waited, ReleaseFrameBudget,
                "'" + id + "' was pressed but never began a cast of its own, so there is no clock to sample on");
            cast = module.LastBegunForTest;

            // EACH NAMED FRAME IS TAKEN ON THE CAST'S OWN CLOCK, not on a frame
            // count. HasReached reads the cast's Cursor -- the same authored
            // seconds its layers are painted from -- so "_impact" is the first
            // frame at which the cast has reached its hit cue, which is the
            // frame the layers authored `at: hit` open. A beat's hit-stop
            // freezes that clock while frames keep running (be9855a0), so a
            // frame count from the anchor would drift off the blow again the
            // moment a hold lands; this cannot.
            int observedImpact = -1;
            int groundLit = -1;
            int busyPopups = 0;
            var takenAt = new Dictionary<string, int>();

            while (takenAt.Count < wanted.Count && frame < SpellLoopSafetyFrames)
            {
                int popups = player.Popups.Count(pp => pp != null && !pp.IsFree);
                if (observedImpact < 0 && popups > busyPopups) observedImpact = frame;
                busyPopups = popups;

                // THE GROUND LAYER, WHEN AUTHORED. Only some spells have
                // one (Cinderfault's fault); a spell without one never
                // lights it and no file is written, which is the report
                // rather than an empty picture claiming to be one.
                var ground = fight.GroundVfxPlayerForTest;
                if (groundLit < 0 && ground != null && ground.Image != null && ground.Image.enabled)
                {
                    groundLit = frame;
                    CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, prefix + "_ground.png"));
                }

                foreach (var sample in wanted)
                {
                    if (takenAt.ContainsKey(sample.Name) || !module.HasReached(cast, sample.CastSeconds)) continue;
                    takenAt[sample.Name] = frame;
                    CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, sample.Name));
                }

                frame++;
                yield return null;
            }

            module.HitCueCrossedForTest -= onCue;
            Time.captureFramerate = 0;

            Assert.AreEqual(wanted.Count, takenAt.Count,
                "'" + id + "': the cast's clock never reached " +
                string.Join(", ", wanted.Where(w => !takenAt.ContainsKey(w.Name)).Select(w => w.Name)));

            string impactFile = wanted.First(w => w.Name.EndsWith("_impact.png")).Name;
            Debug.Log("[PreviewCapture] '" + id + "': hit cue at " + impactSeconds.ToString("F3") +
                      "s of the cast's clock; cast began " + waited + " frames after the press; _impact taken at frame " +
                      takenAt[impactFile] + ", cue crossed at frame " +
                      (cueCrossedAt < 0 ? "never" : cueCrossedAt.ToString()) + ", popup observed at frame " +
                      (observedImpact < 0 ? "never" : observedImpact.ToString()) +
                      (groundLit < 0 ? ", no ground layer authored" : ", ground layer lit at frame " + groundLit));

            foreach (var sample in wanted)
            {
                Debug.Log("[PreviewCapture] wrote " + Path.Combine(OutputDir, sample.Name) +
                          " (cast clock " + sample.CastSeconds.ToString("F3") + "s, frame " + takenAt[sample.Name] + ")");
            }
        }

        // AUDIT #207's schedule, pinned in literals: every named file is an
        // instant of the CAST's clock around its own hit cue, so "_impact" is
        // the blow itself and not the moment a press-anchored count reached.
        [Test]
        public void SpellSamplesSitOnTheCastsOwnClockAroundItsCue()
        {
            var schedule = SpellSampleSchedule(0.6f);
            CollectionAssert.AreEqual(new[] { "flight", "before", "impact", "after", "tail" },
                schedule.Select(s => s.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 0.3f, 0.53333f, 0.6f, 0.66667f, 0.9f },
                schedule.Select(s => s.CastSeconds).ToArray(), new FloatWithin(0.0001f));

            // A cue this early leaves no instant between the start and
            // "before", so there is no flight picture rather than a duplicate.
            CollectionAssert.AreEqual(new[] { "before", "impact", "after", "tail" },
                SpellSampleSchedule(0.1f).Select(s => s.Name).ToArray());
        }

        private sealed class FloatWithin : System.Collections.IComparer
        {
            private readonly float _tolerance;
            public FloatWithin(float tolerance) { _tolerance = tolerance; }
            public int Compare(object x, object y) =>
                Mathf.Abs((float)x - (float)y) <= _tolerance ? 0 : ((float)x).CompareTo((float)y);
        }

        // Frames after the cast begins before the loop gives up. Ten seconds
        // of sampled time; the tail sits well inside that for any cast.
        private const int SpellLoopSafetyFrames = 300;

        // WHICH INSTANT OF THE CAST EACH FILE IS, in the cast's own authored
        // seconds, given its hit cue. before/impact/after/tail sit where they
        // always did relative to the blow (FlankSamples, TailSamples); only
        // the clock they are measured on moved. "flight" is halfway from the
        // cast's start to the blow -- mid-travel for a projectile, mid-fall
        // for a sky spell -- and is dropped when it would not come before
        // "before", since one picture cannot be two instants and the flanking
        // frames are the ones that answer "did the blow land with the number".
        public static List<(string Name, float CastSeconds)> SpellSampleSchedule(float hitCueSeconds)
        {
            float flank = FlankSamples * SpellSampleSeconds;
            float beforeAt = Mathf.Max(0f, hitCueSeconds - flank);

            var schedule = new List<(string Name, float CastSeconds)>();
            float flightAt = hitCueSeconds * 0.5f;
            if (flightAt < beforeAt) schedule.Add(("flight", flightAt));

            schedule.Add(("before", beforeAt));
            schedule.Add(("impact", hitCueSeconds));
            schedule.Add(("after", hitCueSeconds + flank));
            schedule.Add(("tail", hitCueSeconds + TailSamples * SpellSampleSeconds));
            return schedule;
        }

        // ---- tools/preview.ps1 -Character <id> --------------------------------

        // A CHARACTER IS THREE DRAWINGS IN THREE PLACES, so this is three
        // pictures.
        //
        // The map figure is the `walk` stance, sized off its own aspect ratio
        // by MapController.ResolveWalkerArt; the dossier portrait is a
        // different image entirely (portraitPath, not battleSpritePath); the
        // fight stage is the battle art with its ground line and its hover.
        // Each has its own way of being wrong -- a portrait that was never
        // authored, a walk stance the slicer never produced, a ground line
        // half a body off -- and none of the three is visible from the others.
        //
        // The Step 0 baseline lost about two minutes here to picking a capture
        // class by its name and finding it fielded a solo fixture rather than
        // the real squad. This does not ask the author to know which fixture
        // fields what: it stands up a throwaway save whose squad IS this one
        // character, so the map, the dossier and the stage all agree about who
        // is being looked at.
        //
        // A THROWAWAY SAVE ROOT, and it is put back in the teardown. The map
        // walker and the dossier both read SaveSlotManager.CurrentSave rather
        // than any DevForced key -- they are not fight code and know nothing
        // about the preview -- so the only honest way to point them at one
        // character is to be that character for the length of the capture.
        // Same pattern PartyFormationCaptureTests uses to reach a real squad.
        [UnityTest, Explicit("Written by tools/preview.ps1 -Character <id>.")]
        public IEnumerator CaptureCharacter()
        {
            var ids = RequiredIds(CharacterVariable, "-Character <id>");

            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. tools/preview.ps1 runs this through graphics_tests.ps1, " +
                              "which omits -nographics for exactly this reason.");
            }

            Directory.CreateDirectory(OutputDir);

            foreach (string id in ids)
            {
                yield return CaptureOneCharacter(id);
            }
        }

        private string _saveRoot;

        private void StandUpASaveWhoseSquadIs(string id)
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "pp-preview-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);

            SaveSystem.RootOverride = _saveRoot;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RunManager.StartRun(20260906UL);

            var save = SaveSlotManager.CurrentSave;
            Assert.IsNotNull(save, "no save was created for the preview");

            // SQUAD OF ONE, deliberately. Three figures on the map would be
            // three drawings and the author asked about one; the dossier would
            // open on whoever sorts first rather than on them.
            save.selectedCharacterIds = new List<string> { id };
            SaveSlotManager.SaveCurrent();
        }

        private void PutTheSaveBack()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, recursive: true);
            }

            _saveRoot = null;
        }

        private IEnumerator CaptureOneCharacter(string id)
        {
            var plan = PreviewFight.ForCharacter(id);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Debug.Log("[PreviewCapture] " + PreviewFight.Describe(plan));

            string prefix = "character_" + id;
            StandUpASaveWhoseSquadIs(id);

            try
            {
                // 1. THE MAP FIGURE. MapController places the walker from the
                // squad LEADER's battleSpritePath in the walk stance -- with a
                // squad of one, that is unambiguously this character.
                yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return new WaitForSecondsRealtime(0.5f);

                var mapCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(mapCanvas, "the Map scene has no root canvas");
                CanvasCapture.RenderToFile(mapCanvas, Path.Combine(OutputDir, prefix + "_map.png"));

                // 2. THE DOSSIER PORTRAIT. The panel lives inside the Hub's
                // system menu and is emitted inactive, so it is switched on
                // and refreshed rather than navigated to -- the picture wanted
                // is of the panel, not of the three clicks that reach it.
                yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var dossier = Object.FindObjectsByType<CharacterDossierController>(FindObjectsInactive.Include)
                    .FirstOrDefault();
                Assert.IsNotNull(dossier, "the Hub scene has no CharacterDossierController");

                for (var t = dossier.transform; t != null; t = t.parent)
                {
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                }

                dossier.Refresh();
                yield return null;
                yield return new WaitForSecondsRealtime(0.3f);

                var hubCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(hubCanvas, "the Hub scene has no root canvas");
                CanvasCapture.RenderToFile(hubCanvas, Path.Combine(OutputDir, prefix + "_dossier.png"));

                // 3. THE FIGHT. Their own stance sheet on the real stage, and
                // turn one casts the first row on their kit through the same
                // seam the spell preview uses.
                yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var fight = Object.FindAnyObjectByType<FightController>();
                Assert.IsNotNull(fight, "the fight scene has no controller");

                var player = Object.FindAnyObjectByType<FightBeatPlayer>();
                Assert.IsNotNull(player, "the fight scene has no beat player");
                player.Flush();
                yield return null;
                yield return null;

                var enemies = PreviewFight.EnemiesWithArt(3);
                var built = FightEncounterAdapter.Build(
                    new List<string> { id },
                    enemies,
                    new Domain.Rng.SeededRandom(20260810),
                    relicIds: null,
                    depthStep: 0);

                Assert.IsNotNull(built?.Session, "'" + id + "' could not be built into an encounter");

                built.Session.Begin();
                fight.Bind(built.Session, EncounterClass.Normal);
                fight.BindPartyArt(built.Party, built.PartyArt);
                yield return null;
                yield return new WaitForSecondsRealtime(0.8f);

                var fightCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(fightCanvas, "the fight scene has no root canvas");
                CanvasCapture.RenderToFile(fightCanvas, Path.Combine(OutputDir, prefix + "_fight_idle.png"));

                if (plan.Skill != null)
                {
                    fight.ForceFirstAction(plan.Skill.Id);

                    float armed = Time.realtimeSinceStartup;
                    while (!fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;

                    if (!fight.IsBusy)
                    {
                        Debug.LogWarning("[PreviewCapture] '" + id + "' never cast '" + plan.Skill.Id +
                                         "' -- the fight log says what it refused");
                    }
                    else
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        CanvasCapture.RenderToFile(fightCanvas,
                            Path.Combine(OutputDir, prefix + "_fight_cast.png"));
                    }
                }

                Debug.Log("[PreviewCapture] wrote " + prefix + "_{map,dossier,fight_idle,fight_cast}.png to " +
                          OutputDir);
            }
            finally
            {
                PutTheSaveBack();
            }
        }

        // Sprites laid out left to right on one strip, each on the actor's own
        // canvas, on a flat dark ground so an alpha edge is visible.
        private static void WriteContactSheet(List<(string Stance, Sprite Sprite)> sprites, string path)
        {
            const int Gap = 8;
            int cellWidth = sprites.Max(s => (int)s.Sprite.rect.width);
            int cellHeight = sprites.Max(s => (int)s.Sprite.rect.height);
            int width = sprites.Count * cellWidth + (sprites.Count + 1) * Gap;
            int height = cellHeight + 2 * Gap;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var ground = new Color(0.06f, 0.06f, 0.08f, 1f);
            var background = Enumerable.Repeat(ground, width * height).ToArray();
            sheet.SetPixels(background);

            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i].Sprite;
                var rect = sprite.rect;

                Color[] pixels;
                try
                {
                    pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                }
                catch (UnityException)
                {
                    // isReadable is set by StanceSpriteImporter on import, so
                    // this is a texture that came in some other way. Leaving
                    // its cell as flat ground is more useful than failing the
                    // whole sheet -- the gap in the strip IS the report.
                    continue;
                }

                int x = Gap + i * (cellWidth + Gap);
                int y = Gap;
                for (int py = 0; py < rect.height; py++)
                {
                    for (int px = 0; px < rect.width; px++)
                    {
                        var source = pixels[py * (int)rect.width + px];
                        if (source.a <= 0f) continue;
                        sheet.SetPixel(x + px, y + py, Color.Lerp(ground, source, source.a));
                    }
                }
            }

            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
