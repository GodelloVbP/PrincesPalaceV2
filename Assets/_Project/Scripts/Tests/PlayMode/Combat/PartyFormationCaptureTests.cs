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
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE REAL THREE-MEMBER PARTY, STANDING IN FORMATION, AT NORMAL SPEED.
    //
    // The question this fixture answers: does Odette (the navy owl, the
    // party's flying caster, seated in the FAR/back party slot per
    // characters.json's order -- sheep, bear, owl) actually
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

            // C3: the ring's own RENDERED bounds, in the same stage-pixel
            // space SpriteRectStagePixels already reports -- so "where is
            // the ring's centre" and "where is the figure's centre" can be
            // compared directly, with the slot's depth scale and the
            // mirror already baked in by the transform walk rather than
            // reconstructed from AnchoredPosition + a scale read separately.
            public Rect ShadowRectStagePixels;
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
                float halfW = FightStageAnchors.StageSize.X / 2f;
                float halfH = FightStageAnchors.StageSize.Y / 2f;

                Rect StagePixels(RectTransform rt)
                {
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rt);
                    return new Rect(bounds.min.x + halfW, bounds.min.y + halfH, bounds.size.x, bounds.size.y);
                }

                return new SlotReading
                {
                    Name = Name,
                    AnchoredPosition = Slot.anchoredPosition,
                    AnimatorHome = Animator.Home,
                    SpriteRectStagePixels = StagePixels(Sprite.rectTransform),
                    ShadowAnchoredPosition = Shadow != null ? Shadow.anchoredPosition : Vector2.zero,
                    ShadowRectStagePixels = Shadow != null ? StagePixels(Shadow) : default,
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
        private PartySlot[] OccupiedPartySlots() => OccupiedSlots("Party");

        // C3: the SAME per-slot reading, over the ENEMY prefix instead --
        // nothing in PartySlot's fields is actually party-specific, and the
        // ring-vs-foot-band claim below is a claim about every stage slot,
        // not a party-only one.
        private PartySlot[] OccupiedEnemySlots() => OccupiedSlots("Enemy");

        private PartySlot[] OccupiedSlots(string prefix)
        {
            var found = new System.Collections.Generic.List<PartySlot>();

            for (int i = 0; i < 8; i++)
            {
                var slotGo = Named($"{prefix}{i}Slot");
                if (slotGo == null || !slotGo.activeSelf) continue;

                var slot = slotGo.GetComponent<RectTransform>();
                var animator = slotGo.GetComponent<StageActorAnimator>();
                var sprite = Named($"{prefix}{i}Sprite")?.GetComponent<Image>();
                var shadow = Named($"{prefix}{i}FootShadow")?.GetComponent<RectTransform>();

                found.Add(new PartySlot
                {
                    Name = $"{prefix}{i}",
                    CombatantName = Named($"{prefix}{i}Nameplate")?.GetComponent<TMPro.TMP_Text>()?.text,
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

            // FULL-FRAME, NOT JUST THE STAGE CROP BELOW. StageCaptureRig (and
            // every reading in this fixture) exists to answer "is the ring
            // under the feet" against a crop of FightStageAnchors.StageSize --
            // a coordinate frame, not a clip region (see StageSize's own
            // header) -- so nothing here had ever shown whether a widened Far
            // anchor pushes a far-slot figure off the REAL screen or under a
            // HUD panel the crop doesn't include. CanvasCapture.RenderToFile
            // is the same renderer ScreenshotTool uses, called at the two ends
            // of UiAudit's own audited-aspect range (UiFrames.All) -- widest
            // (21:9 UltraWide, more half-width than Reference) and narrowest
            // by aspect ratio (4:3 FourThree, same half-width as Reference but
            // the most vertical headroom) -- so a problem at either extreme
            // shows up in a picture rather than only in arithmetic. Taken
            // BEFORE StageCaptureRig exists: RenderToFile owns its own
            // camera/scaler swap and restores the canvas exactly as it found
            // it, same as the rig below does for its own capture.
            CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "full_widest_ultrawide_2580x1080.png"),
                (int)PrincesPalace.Domain.UiKit.UiFrames.UltraWide.X, (int)PrincesPalace.Domain.UiKit.UiFrames.UltraWide.Y);
            CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "full_narrowest_fourthree_1920x1440.png"),
                (int)PrincesPalace.Domain.UiKit.UiFrames.FourThree.X, (int)PrincesPalace.Domain.UiKit.UiFrames.FourThree.Y);

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

            // C3: the ring sits under the FEET, on every occupied slot of
            // both sides -- not just the three party members this fixture
            // otherwise photographs. Checked against the opening formation
            // (frame 0's readings), before any hover has moved a flyer off
            // its own ground line.
            AssertRingUnderFeet(OccupiedPartySlots(), first, _fight.Session.Encounter.PlayerParty);
            AssertRingUnderFeet(OccupiedEnemySlots(), OccupiedEnemySlots().Select(s => s.Read()).ToArray(),
                _fight.Session.Encounter.Enemies);

            // A LITERAL PIN, not just the check above -- CLAUDE.md gotcha 5.
            // AssertRingUnderFeet compares the ring against
            // FightController.ExpectedRingCentreXForTest, which calls the
            // very same production function (ContentCentreFractionForActor)
            // PlaceShadow itself does; a regression that makes BOTH sides of
            // that comparison wrong THE SAME WAY (exactly what shipped here:
            // the trimmed-sprite bug made the "expected" and the "actual"
            // agree at ~0px, nowhere near the feet) would still pass it.
            // Shawn's own foot-band offset is independently known (an
            // offline PIL scan of the same Characters/sheep/idle.png,
            // recorded in this method's own C3 note) to be a REAL ~65-72px,
            // not a near-zero one -- so pin that the ring for whichever slot
            // is showing "Shawn" sits meaningfully away from that sprite's
            // own rendered centre, not merely "wherever the formula points".
            var shawnSlot = OccupiedPartySlots().FirstOrDefault(s => s.CombatantName == "Shawn");
            if (shawnSlot != null)
            {
                var shawnReading = first.FirstOrDefault(r => r.Name == shawnSlot.Name);
                float shawnRingX = shawnReading.ShadowRectStagePixels.x + shawnReading.ShadowRectStagePixels.width * 0.5f;
                float shawnSpriteCentreX = shawnReading.SpriteRectStagePixels.x + shawnReading.SpriteRectStagePixels.width * 0.5f;

                Assert.Greater(Mathf.Abs(shawnRingX - shawnSpriteCentreX), 20f,
                    "Shawn's ring sits within 20px of his sprite's own rendered centre -- his idle art is " +
                    "known (offline PIL scan of Characters/sheep/idle.png) to be off-centre by far more than " +
                    "that, so a ring this close to centre means the foot-band measurement collapsed to ~0 " +
                    "again (the exact shape of the C3 bug), not that Shawn's art happens to be centred.");
            }
        }

        // THE THREE CARDS WEAR THREE DIFFERENT PEOPLE'S COLOURS.
        //
        // The only check that can catch a card-major/edge-major mix-up in
        // FightController.rosterCardRims. Rim edges are AsDecor, so UiAudit
        // never looks at their colour; FightScreenTests pins the twelve node
        // NAMES, which proves the tree is built right but not that the
        // controller indexes it right. This reads the colour actually on the
        // Image after a real RefreshUi, for the character actually on that
        // card.
        //
        // NOT GRAPHICS-GATED, deliberately: the colours are component state,
        // not pixels, so this runs headless with the rest of the suite. The
        // full-frame PNGs the fixture above writes are the visual evidence
        // that the result is legible; this is the evidence that it is right.
        [UnityTest]
        public IEnumerator EachHudCardWearsItsOwnOccupantsIdentityColour()
        {
            yield return OpenTheScene();

            // Two frames, per docs/CODE_STANDARDS.md Sec8: Start() runs one
            // frame after SetActive, and RefreshUi is what writes these.
            yield return null;
            yield return null;

            var session = _fight.Session;
            Assert.IsNotNull(session, "the fight never started, so no card has an occupant");

            // WHOSE CARD IS THIS is read off the card's own name label, not
            // recomputed from the turn order. FightController decides which
            // member each card shows (ActingCharacter, then RefreshRoster's
            // "everyone else" ordering) and a test that re-derived that would
            // pass on a controller that painted the right colour on the wrong
            // card, which is precisely the bug this exists to find.
            var themes = new System.Collections.Generic.List<ButtonTheme>();
            foreach (var card in new[]
                     {
                         ("PartyPlate", "PartyName"),
                         ("Roster0", "Roster0Name"),
                         ("Roster1", "Roster1Name"),
                     })
            {
                string who = Named(card.Item2)?.GetComponent<TMPro.TMP_Text>()?.text;
                Assert.IsFalse(string.IsNullOrWhiteSpace(who),
                    $"{card.Item2} is blank -- the card names nobody, so nothing can be checked against it");

                var occupant = session.Encounter.PlayerParty.FirstOrDefault(c => c.Name == who);
                Assert.IsNotNull(occupant, $"{card.Item2} says '{who}', who is not in the party");

                var kit = session.KitFor(occupant);
                Assert.IsNotNull(kit, $"{who} has no PlayerKit, so no theme could reach their card");

                AssertCardWears(card.Item1, card.Item2, kit.PlateTheme, who);
                themes.Add(kit.PlateTheme);
            }

            // MUTUALLY DISTINCT -- the whole point of a per-PC colour. If two
            // cards agreed, every assert above would still pass while the
            // column told the player nothing.
            CollectionAssert.AllItemsAreUnique(themes,
                "two of the three HUD cards wear the same identity colour: " +
                string.Join(", ", themes.Select(t => t.ToString())));
        }

        // ---- the second meter draws the pool its holder actually carries ----
        //
        // A FIXTURE POOL, HANDED TO A REAL COMBATANT IN A REAL SCENE. Nothing
        // authors a non-mana row yet (that is phase E), and the whole point of
        // plan P7 is that the meter stopped being a mana bar -- so the only
        // way to test it before that content exists is to build the row the
        // test needs and give it to whoever is acting. The hexes are stated
        // here as literals so this cannot start passing because some shipped
        // row happens to be orange.
        //
        // WHAT THE CONTROL PROVES. The roster cards keep mana throughout, so
        // their fill alpha must not move by a single frame's worth. "The
        // pulse is gated on the row" is the claim, and a tick that wrote to
        // every meter would satisfy every other assert in here.
        private static ResolvedPool FuryFixture() =>
            new ResolvedPool(
                "fury_fixture", "Fury", "FURY",
                PoolCapacityRule.Fixed, 100,
                0, 15, 10,
                10, PoolDecayTrigger.Damage,
                PoolStartRule.Zero, 0,
                FixtureBrightHex, FixtureDeepHex, FixtureTextHex,
                pulse: true, allowsSpellBooks: false, restoredByManaEffects: false, absorbsDamage: false,
                sortOrder: 99);

        private const string FixtureBrightHex = "#FF8A3A";
        private const string FixtureDeepHex = "#8E3A12";
        private const string FixtureTextHex = "#FFD2B0";

        // Ui.Meter's own derivation, restated as this test's expectation
        // rather than read off the production constant: deepHex at 0.70 for
        // the rim and 0.44 for the band under the fill, which is what
        // FightHudPalette.MpRim/MpShade are relative to MpDeep and what
        // RawPoolEntry.deepHex promises an author.
        private const float ExpectedRimAlpha = 179f / 255f;
        private const float ExpectedShadeAlpha = 112f / 255f;

        [UnityTest]
        public IEnumerator TheSecondMeterWearsItsPoolsColoursAndOnlyPulsesWhenTheRowSaysSo()
        {
            yield return OpenTheScene();
            yield return null;
            yield return null;

            var session = _fight.Session;
            Assert.IsNotNull(session, "the fight never started, so nobody holds a pool");

            string who = Named("PartyName")?.GetComponent<TMPro.TMP_Text>()?.text;
            var actor = session.Encounter.PlayerParty.FirstOrDefault(c => c.Name == who);
            Assert.IsNotNull(actor, $"the party card names '{who}', who is not in the party");

            // BEFORE: everyone holds mana, so the card must read exactly what
            // the scene baked. This is the "invisible until a row authors
            // different colours" half of the claim, and it is checked first
            // because it is the half a regression would break silently.
            var partyFill = Named("PartyMpFill")?.GetComponent<Image>();
            Assert.IsNotNull(partyFill, "PartyMpFill is missing or carries no Image");
            AssertColour(Hex(FightHudPalette.MpBright), partyFill.color, "PartyMpFill before (mana)");
            Assert.AreEqual("MP", Named("PartyMpTag")?.GetComponent<TMPro.TMP_Text>()?.text);

            var pool = new ResourcePool(FuryFixture(), capacity: 100, gainPerTurn: 0);
            pool.Gain(60);
            actor.PrimaryPool = pool;
            _fight.RefreshUi();
            yield return null;

            Assert.AreEqual("FURY", Named("PartyMpTag")?.GetComponent<TMPro.TMP_Text>()?.text,
                "the tag is still a literal rather than the pool's own shortTag");

            AssertColour(Hex(FixtureBrightHex), Opaque(partyFill.color), "PartyMpFill");

            var shade = Named("PartyMpFillShade")?.GetComponent<Image>();
            Assert.IsNotNull(shade, "PartyMpFillShade is missing or carries no Image");
            AssertColour(WithAlpha(Hex(FixtureDeepHex), ExpectedShadeAlpha), shade.color, "PartyMpFillShade");

            foreach (string edge in new[] { "Top", "Bottom", "Left", "Right" })
            {
                var rim = Named("PartyMpBarRim" + edge)?.GetComponent<Image>();
                Assert.IsNotNull(rim, $"PartyMpBarRim{edge} is missing or carries no Image");
                AssertColour(WithAlpha(Hex(FixtureDeepHex), ExpectedRimAlpha), rim.color, "PartyMpBarRim" + edge);
            }

            var value = Named("PartyMpValue")?.GetComponent<TMPro.TMP_Text>();
            Assert.IsNotNull(value, "PartyMpValue is missing or carries no TMP_Text");
            AssertColour(Hex(FixtureTextHex), value.color, "PartyMpValue");
            Assert.AreEqual("60/100", value.text);

            // ---- the heartbeat, sampled across a whole loop ----
            //
            // NOT TWO SAMPLES 0.3s APART. The envelope is lub-dub-REST: two
            // thumps in the first quarter of the loop and the remainder at
            // the floor, which is what a heartbeat looks like and which means
            // two arbitrary moments can legitimately read the same alpha.
            // This walks more than one full 1.1s loop and asserts the SPREAD,
            // which no static bar can satisfy.
            var pulsed = new System.Collections.Generic.List<float>();
            var mana = new System.Collections.Generic.List<float>();
            var rosterFill = Named("Roster0MpFill")?.GetComponent<Image>();

            for (int i = 0; i < 16; i++)
            {
                yield return new WaitForSecondsRealtime(0.08f);
                pulsed.Add(partyFill.color.a);
                if (rosterFill != null && rosterFill.gameObject.activeInHierarchy) mana.Add(rosterFill.color.a);
            }

            Assert.AreEqual("FURY", Named("PartyMpTag")?.GetComponent<TMPro.TMP_Text>()?.text,
                "the party card changed hands mid-sample, so these alphas describe two different pools");

            Assert.Greater(pulsed.Max() - pulsed.Min(), 0.15f,
                "the fixture row says pulse:true and the bar never moved -- sampled alphas: " +
                string.Join(", ", pulsed.Select(a => a.ToString("0.000", CultureInfo.InvariantCulture))));
            Assert.LessOrEqual(pulsed.Max(), 1.001f, "the pulse overshot fully opaque");

            // THE CONTROL. Mana's row says pulse:false, so the tick must
            // never have touched this Image at all.
            if (mana.Count > 0)
            {
                Assert.AreEqual(0f, mana.Max() - mana.Min(), 0.0001f,
                    "a mana meter's alpha moved -- the tick is writing to meters whose row never asked");
                Assert.AreEqual(1f, mana[0], 0.004f, "mana's bar is no longer fully opaque");
            }

            if (!CanvasCapture.IsSupported) yield break;

            var canvas = RootCanvas();
            if (canvas == null) yield break;

            string dir = CaptureOutput.LabelDir("pool_meter");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "fury_meter_fourthree_1920x1440.png"),
                (int)UiFrames.FourThree.X, (int)UiFrames.FourThree.Y);
            Debug.Log($"[PoolMeterCapture] wrote the fixture-pool frame to {dir}");
        }

        private static Color Opaque(Color colour) => new Color(colour.r, colour.g, colour.b, 1f);

        private static Color WithAlpha(Color colour, float alpha) =>
            new Color(colour.r, colour.g, colour.b, alpha);

        private void AssertCardWears(string cardStem, string nameNode, ButtonTheme theme, string who)
        {
            var expected = PcTheme.For(theme);
            var expectedRim = Hex(expected.Rim);

            foreach (string edge in new[] { "Top", "Bottom", "Left", "Right" })
            {
                var rim = Named(cardStem + "Rim" + edge)?.GetComponent<Image>();
                Assert.IsNotNull(rim, $"{cardStem}Rim{edge} is missing or carries no Image");
                AssertColour(expectedRim, rim.color, $"{cardStem}Rim{edge} ({who}, {theme})");
            }

            var label = Named(nameNode)?.GetComponent<TMPro.TMP_Text>();
            Assert.IsNotNull(label, $"{nameNode} is missing or carries no TMP_Text");
            AssertColour(Hex(expected.Name), label.color, $"{nameNode} ({who}, {theme})");
        }

        // 1/255 either way: the hex goes through ColorUtility and back out as
        // a float per channel, so an exact equality would be pinning float
        // round-tripping rather than the colour.
        private static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.004f, what + " red");
            Assert.AreEqual(expected.g, actual.g, 0.004f, what + " green");
            Assert.AreEqual(expected.b, actual.b, 0.004f, what + " blue");
            Assert.AreEqual(expected.a, actual.a, 0.004f, what + " alpha");
        }

        private static Color Hex(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var parsed), $"'{hex}' is not a colour");
            return parsed;
        }

        // C3: |ring centre x - foot-band midpoint x| < 6px, for whichever
        // combatant this slot's nameplate says is standing there. Skips a
        // slot with no sprite/shadow/matching combatant rather than
        // failing outright -- an empty or fallback-plate slot has no foot
        // band to check, and that is FightPlayableTests' claim to make, not
        // this fixture's.
        private void AssertRingUnderFeet(PartySlot[] slots, SlotReading[] readings,
            System.Collections.Generic.IReadOnlyList<CombatantState> combatants)
        {
            const float ToleranceX = 6f;

            for (int i = 0; i < slots.Length && i < readings.Length; i++)
            {
                var slot = slots[i];
                if (slot.Sprite == null || slot.Sprite.sprite == null || slot.Shadow == null) continue;

                var combatant = combatants.FirstOrDefault(c => c.Name == slot.CombatantName);
                if (combatant == null) continue;

                // The same value RefreshCombatantSprite wrote onto this exact
                // Image's rectTransform -- reading it back here means the
                // test needs no second copy of StageFacing's mirroring rule.
                float mirrorSign = Mathf.Sign(slot.Sprite.rectTransform.localScale.x);
                var reading = readings[i];

                float expectedRingX = _fight.ExpectedRingCentreXForTest(
                    combatant, reading.SpriteRectStagePixels, mirrorSign);
                float actualRingX = reading.ShadowRectStagePixels.x + reading.ShadowRectStagePixels.width * 0.5f;

                Assert.Less(Mathf.Abs(actualRingX - expectedRingX), ToleranceX,
                    $"{slot.Name} ({slot.CombatantName}): ring centre x={actualRingX:F1}, " +
                    $"expected foot-band midpoint x={expectedRingX:F1}");
            }
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
                $"      \"footShadowAnchoredPosition\": {Vec(s.ShadowAnchoredPosition)},\n" +
                $"      \"footShadowRectStagePixels\": {Rct(s.ShadowRectStagePixels)}\n" +
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
