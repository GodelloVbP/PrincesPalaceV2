using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // A look at PLAN_STATUS_EFFECT_UI.md's three surfaces TOGETHER, not one
    // status at a time. StatusHudCoverageTests and StatusBadgeIconTests each
    // pin one fact about one status and render nothing; this is the one
    // fixture that stacks an over-full, realistic-worst-case set of statuses
    // across the enemy row, the roster row and the party plate at once and
    // photographs it.
    //
    // Shape borrowed from FightMenuCaptureTests (build a session by hand,
    // bind it, screenshot the whole canvas) rather than PartyFormationCapture-
    // Tests' cropped stage rig -- the roster and party plate this needs to
    // show sit outside that rig's stage box entirely.
    //
    // Runs only with a graphics device, i.e. through
    // tools/screenshot.ps1 -Runtime -RuntimeFilter StatusRowCaptureTests.
    public class StatusRowCaptureTests
    {
        private FightController _fight;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private static Canvas RootCanvas() =>
            Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).FirstOrDefault(c => c.isRootCanvas);

        // NAMED status_rows_*, per this package's own instructions -- the one
        // detail that ties the file back to this fixture without opening it.
        private IEnumerator Shoot(string label)
        {
            yield return null;
            yield return null;

            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, $"status_rows_{label}.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path), $"capture '{label}' was not written");
            Debug.Log($"[StatusRowCapture] wrote {path}");
        }

        [UnityTest]
        public IEnumerator EveryStatusRowSurfaceShowsAtOnce()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 " +
                              "-Runtime -RuntimeFilter StatusRowCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            for (int i = 0; i < 240; i++) yield return null;

            // ALL THREE fielded characters with battle art -- the same squad
            // PartyFormationCaptureTests photographs (sheep, placeholder_
            // brawler, owl) -- so the roster row has two off-turn plates to
            // paint statuses onto, not only the acting one.
            var party = ContentDatabase.Characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath))
                .Select(c => c.id).ToList();
            var enemies = ContentDatabase.Enemies
                .Where(e => !string.IsNullOrWhiteSpace(e.Data.SpritePath))
                .Take(2).Select(e => e.id).ToList();
            Assert.GreaterOrEqual(party.Count, 1, "no character has battle art to capture");
            Assert.AreEqual(2, enemies.Count, "fewer than two enemies have art to capture");

            var built = FightEncounterAdapter.Build(party, enemies, new Domain.Rng.SeededRandom(11));
            var session = built.Session;

            // ---- one enemy carries five statuses -- the enemy row's real
            // capacity is 4 (FightScreen.BuildStatusBadge's own comment: "4
            // statuses plus the +N overflow chip is 5 nodes per row"), so a
            // 5th tips it into the chip.
            var enemy0 = session.Encounter.Enemies[0];
            StatusEffects.Apply(enemy0.Statuses, StatusEffectType.Poison, 5, 3);
            StatusEffects.Apply(enemy0.Statuses, StatusEffectType.Vulnerable, 25, 2);
            session.ApplyChilledForTest(enemy0, 20, 2);
            StatusEffects.Apply(enemy0.Statuses, StatusEffectType.Rooted, 0, 2);
            StatusEffects.Apply(enemy0.Statuses, StatusEffectType.Marked, 0, Marks.MarkDurationTurns);

            // ---- a second enemy carries two BENEFIT-typed statuses, so the
            // enemy row's polarity frame is proven on both colours, not just
            // the detriment one the five-status enemy already covers.
            var enemy1 = session.Encounter.Enemies[1];
            StatusEffects.Apply(enemy1.Statuses, StatusEffectType.Regen, 5, 3);
            StatusEffects.Apply(enemy1.Statuses, StatusEffectType.Protect, 20, 2);

            // ---- the acting party member carries six -- section 6's
            // realistic worst case for a party member, which the plate's six
            // slots cover with no chip needed at exactly six. Read the same
            // way FightController.ActingCharacter() does, since applying
            // these to the wrong combatant would leave the party plate blank.
            var actor = session.IsPlayerTurn && session.Current != null
                ? session.Current
                : session.Encounter.LivingPlayerParty.First();
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Poison, 3, 2);
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Shielded, 30, FightTuning.MagicalShieldDurationTurns);
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Regen, 5, 3);
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Protect, 20, 2);
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Vulnerable, 15, 2);
            session.GrantSpeedPercentForTest(actor, RelicEffect.SparringSaber, 30, 1);

            _fight.Bind(session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party,
                built.Party.Select(p => ContentDatabase.Characters
                        .FirstOrDefault(c => c.Data.DisplayName == p.Name)?.Data.BattleSpritePath)
                    .ToList());

            yield return null;
            yield return null;
            _fight.RefreshUi();

            yield return Shoot("rest");

            // ---- one enemy badge, hovered -- proves the shared tooltip and
            // its TooltipPlacement.Beside positioning actually work, not
            // merely that a badge is drawn. Driven through the component
            // rather than a synthetic pointer, same reasoning FightMenuCapture-
            // Tests' own intent-hover shot gives (no real cursor exists in a
            // capture, and a fake EventSystem raycast would test Unity, not
            // this feature).
            var badge = Named("EnemyStatusBadge0_0");
            Assert.IsNotNull(badge, "enemy slot 0's first status badge was not found");
            var hover = badge.GetComponent<HoverIndex>();
            Assert.IsNotNull(hover, "no HoverIndex was attached to EnemyStatusBadge0_0");
            hover.OnPointerEnter(null);
            yield return Shoot("hover");
        }
    }
}
