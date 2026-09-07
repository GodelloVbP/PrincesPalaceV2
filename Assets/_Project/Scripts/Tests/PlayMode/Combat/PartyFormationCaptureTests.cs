using System;
using System.Collections;
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
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // THE REAL THREE-MEMBER PARTY, STANDING IN FORMATION, AT NORMAL SPEED.
    //
    // The question this fixture answers: does Odette (the navy owl, the
    // party's flying caster, seated in the FAR/back party slot per
    // characters.json's order -- sheep, placeholder_brawler, owl) actually
    // read above the two figures standing in front of her, does her foot
    // shadow stay pinned to the floor while she hovers, and does she bob.
    // FightController.StageVisuals.HoverIdle pushes her altitude every idle
    // frame from Resources/StanceManifest.json's hover.height/bob, through
    // StageActorAnimator.SetHover -- none of that is exercised by a single
    // still, which is why this samples four moments a few hundred
    // milliseconds apart instead of one screenshot.
    //
    // Opens the Fight scene the way FightPlayableTests does -- a player's own
    // path, not a hand-built session -- so the party fielded is whatever
    // FightBootstrap actually seats a player with, not a fixture's idea of
    // one. See StaticPilotStageCaptureTests for the frame-series/timing
    // conventions this borrows (PP_CAPTURE_LABEL, Time.captureDeltaTime is
    // deliberately NOT used here -- this samples the WALL clock via
    // WaitForSecondsRealtime, because the question is what a player watching
    // in real time actually sees, not a fixed-rate recording of a beat).
    //
    // A RUN, NOT THE NO-RUN PLACEHOLDER. FightBootstrap.BuildOpeningFight
    // takes two different paths (RunManager.HasRun ? RunOrchestrator.
    // BuildFight() : BuildPlaceholderFight()), and only the run path fields
    // more than one party member -- BuildPlaceholderFight hard-codes
    // `.Take(1)` for the no-run tooling fight, which is a single-Shawn stage
    // and was the FIRST thing this fixture found when it opened the scene
    // with no run active. So this starts a real throwaway run first, the
    // same pattern FightSettlementTests uses to reach the real settlement
    // door -- a fresh SaveData.CreateNew() roster resolves to a three-member
    // squad on its own (SaveDataSquadOfThreeTests' own default case), and
    // TestSquadOfThreeEnabled is forced true anyway so this fixture does not
    // silently start fielding one again if that content-derived default
    // ever changes.
    public class PartyFormationCaptureTests
    {
        private const ulong Seed = 20260904UL;

        // t = 0.0, 0.6, 1.2, 1.8s of real time -- long enough to cross more
        // than one full hover period at StanceManifest's authored
        // periodSeconds, so a bob that never moves would be caught rather
        // than sampled on its one flat moment.
        private static readonly float[] SampleTimes = { 0f, 0.6f, 0.6f, 0.6f };

        private FightController _fight;
        private string _root;

        // REAL TIME IS THE WHOLE POINT, so BreathSpeedMultiplier is stated
        // rather than inherited from whatever the previous class in this
        // process left behind -- the same reasoning StaticPilotStageCapture-
        // Tests' own [SetUp] gives. BreathSpeedMultiplier, not BeatSpeed-
        // Multiplier: nothing here plays a beat, but HoverIdle's clock (and
        // the breath's) is scaled by it, and a leaked 60x would collapse
        // four seconds of hover into something no human eye could judge.
        [SetUp]
        public void UseAThrowawaySaveRootWithARealSquad()
        {
            FightController.BreathSpeedMultiplier = 1f;

            _root = Path.Combine(Path.GetTempPath(), "pp-party-formation-" + Guid.NewGuid().ToString("N"));
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
            FightController.BreathSpeedMultiplier = 1f;
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private static Canvas RootCanvas() =>
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);

        private IEnumerator OpenTheScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
        }

        // ---- one party slot's worth of state, read on demand ------------------

        private struct SlotReading
        {
            public string Name;
            public Vector2 AnchoredPosition;
            public Vector2 AnimatorHome;
            public Rect SpriteRectStagePixels;
            public Vector2 ShadowAnchoredPosition;
        }

        private sealed class PartySlot
        {
            public string Name;
            public string CombatantName;
            public RectTransform Slot;
            public StageActorAnimator Animator;
            public Image Sprite;
            public RectTransform Shadow;

            public SlotReading Read()
            {
                // In "stage pixels", i.e. relative to the crop's own
                // bottom-left, matching StageCaptureRig's Grab() -- computed
                // from the transform hierarchy alone (no camera needed, so
                // this half of the fixture stays headless-safe), and it works
                // because the stage box and the canvas share one centre: see
                // FightStageAnchors' own header on why every slot offset is
                // measured from stage centre, and StageCaptureRig's crop is
                // centred the same way against the render target.
                var canvasRect = (RectTransform)Sprite.canvas.transform;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, Sprite.rectTransform);
                float halfW = FightStageAnchors.StageSize.X / 2f;
                float halfH = FightStageAnchors.StageSize.Y / 2f;
                var rect = new Rect(
                    bounds.min.x + halfW, bounds.min.y + halfH,
                    bounds.size.x, bounds.size.y);

                return new SlotReading
                {
                    Name = Name,
                    AnchoredPosition = Slot.anchoredPosition,
                    AnimatorHome = Animator.Home,
                    SpriteRectStagePixels = rect,
                    ShadowAnchoredPosition = Shadow != null ? Shadow.anchoredPosition : Vector2.zero,
                };
            }
        }

        // Every party slot that is actually occupied, in slot order.
        //
        // WHO IS IN A SLOT IS READ OFF THE NAMEPLATE, not off PlayerParty[i].
        // Those were the same answer until A3: a slot belongs to the combatant
        // that started in it for the whole fight, while the party LIST is
        // reordered by every Move, so indexing the list by slot number names
        // the wrong figure the moment anybody has stepped. Nothing in this
        // fixture moves -- it photographs an opening formation -- but the
        // shortcut is exactly the assumption that shipped the identity bug,
        // and it does not belong in a fixture whose whole subject is which
        // figure stands where.
        private PartySlot[] OccupiedPartySlots()
        {
            var found = new System.Collections.Generic.List<PartySlot>();

            for (int i = 0; i < 8; i++)
            {
                var slotGo = Named($"Party{i}Slot");
                if (slotGo == null || !slotGo.activeSelf) continue;

                var slot = slotGo.GetComponent<RectTransform>();
                var animator = slotGo.GetComponent<StageActorAnimator>();
                var sprite = Named($"Party{i}Sprite")?.GetComponent<Image>();
                var shadow = Named($"Party{i}FootShadow")?.GetComponent<RectTransform>();

                found.Add(new PartySlot
                {
                    Name = $"Party{i}",
                    CombatantName = Named($"Party{i}Nameplate")?.GetComponent<TMPro.TMP_Text>()?.text,
                    Slot = slot,
                    Animator = animator,
                    Sprite = sprite,
                    Shadow = shadow,
                });
            }

            return found.ToArray();
        }

        // ---- the fixture --------------------------------------------------

        [UnityTest]
        public IEnumerator ThreePartyMembersStandInFormationAndOdetteIsAmongThem()
        {
            yield return OpenTheScene();

            var slots = OccupiedPartySlots();

            // ASSERTED, NOT SKIPPED (docs/CODE_STANDARDS.md Sec8): a bootstrap
            // that quietly seats fewer than three would make every other
            // claim this fixture photographs meaningless, and a skip here
            // would let that drift go unnoticed indefinitely.
            Assert.AreEqual(3, slots.Length,
                "expected the real three-member party (Shawn, the placeholder brawler, Odette) on stage, found: " +
                string.Join(", ", slots.Select(s => $"{s.Name}={s.CombatantName}")));

            // Mirrors FightPlayableTests.TheActorsWearTheirOwnArt_NotTheFallbackPlate:
            // a real sprite, not null and not the fallback plate, is what
            // "wearing its own art" means on this stage.
            var odette = slots.FirstOrDefault(s => s.CombatantName == "Odette");
            Assert.IsNotNull(odette, "no party slot holds a combatant named Odette");
            Assert.IsNotNull(odette.Sprite, "Odette's slot has no sprite Image");
            Assert.IsNotNull(odette.Sprite.sprite,
                "Odette has no sprite loaded -- Characters/owl art never reached the stage");
            Assert.AreNotSame(_fight.FallbackSprite, odette.Sprite.sprite,
                "Odette fell back to the plate -- Characters/owl never loaded from Resources");
            Assert.IsTrue(odette.Sprite.gameObject.activeInHierarchy,
                "Odette's art is loaded but not on screen");

            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 " +
                              "-Filter PrincesPalace.PlayModeTests.PartyFormationCaptureTests");
                yield break;
            }

            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");

            string dir = CaptureOutput.LabelDir("party_formation");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);

            var rig = new StageCaptureRig(canvas, (int)FightStageAnchors.StageSize.X, (int)FightStageAnchors.StageSize.Y);
            SlotReading[] first = null;
            SlotReading[] last = null;

            try
            {
                for (int i = 0; i < SampleTimes.Length; i++)
                {
                    if (SampleTimes[i] > 0f) yield return new WaitForSecondsRealtime(SampleTimes[i]);

                    // Re-fetched each frame rather than cached from
                    // OccupiedPartySlots' first call: the slot GameObjects
                    // themselves are stable across a fight with no deaths or
                    // arrivals, but re-reading costs nothing and does not
                    // depend on that staying true.
                    var current = OccupiedPartySlots();
                    var readings = current.Select(s => s.Read()).ToArray();
                    if (i == 0) first = readings;
                    last = readings;

                    var frame = rig.Grab();
                    File.WriteAllBytes(Path.Combine(dir, $"f{i}.png"), frame.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(frame);
                }
            }
            finally
            {
                rig.Restore();
            }

            File.WriteAllText(Path.Combine(dir, "slots.json"), SlotsJson(first, last));
            Debug.Log($"[PartyFormationCapture] wrote {SampleTimes.Length} frames + slots.json to {dir}");
        }

        private static string SlotsJson(SlotReading[] first, SlotReading[] last)
        {
            string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
            string Vec(Vector2 v) => $"{{ \"x\": {F(v.x)}, \"y\": {F(v.y)} }}";
            string Rct(Rect r) =>
                $"{{ \"x\": {F(r.x)}, \"y\": {F(r.y)}, \"width\": {F(r.width)}, \"height\": {F(r.height)} }}";

            string Slot(SlotReading s) =>
                "    {\n" +
                $"      \"name\": \"{s.Name}\",\n" +
                $"      \"anchoredPosition\": {Vec(s.AnchoredPosition)},\n" +
                $"      \"animatorHome\": {Vec(s.AnimatorHome)},\n" +
                $"      \"spriteRectStagePixels\": {Rct(s.SpriteRectStagePixels)},\n" +
                $"      \"footShadowAnchoredPosition\": {Vec(s.ShadowAnchoredPosition)}\n" +
                "    }";

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"label\": \"{Environment.GetEnvironmentVariable("PP_CAPTURE_LABEL") ?? "unlabelled"}\",");
            json.AppendLine("  \"f0\": [");
            json.AppendLine(string.Join(",\n", first.Select(Slot)));
            json.AppendLine("  ],");
            json.AppendLine("  \"fLast\": [");
            json.AppendLine(string.Join(",\n", last.Select(Slot)));
            json.AppendLine("  ]");
            json.Append("}");
            return json.ToString();
        }
    }
}
