using System.Collections;
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

        // OutputDir/Named/RootCanvas/Shoot moved to Tests/PlayMode/Shared/
        // HudCaptureRig.cs (S12's review) -- byte-for-byte duplicated from
        // FightMenuCaptureTests before this. "status_rows_" is this
        // fixture's own filename prefix, per this package's own instructions
        // -- the one detail that ties a written file back to this fixture
        // without opening it.
        private HudCaptureRig _rig;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

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
            _rig = new HudCaptureRig(_fight, "status_rows_", "StatusRowCapture");

            for (int i = 0; i < 240; i++) yield return null;

            // ALL THREE fielded characters with battle art -- the same squad
            // PartyFormationCaptureTests photographs (sheep, placeholder_
            // brawler, owl) -- so the roster row has two off-turn plates to
            // paint statuses onto, not only the acting one.
            var party = ContentDatabase.Characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath))
                .Select(c => c.id).ToList();
            // BEETLE FIRST, DELIBERATELY -- not just any two enemies with art.
            // "Ironback Beetle" is one of C3's own measured overflow cases
            // (~79px against the 72px name box at 11pt), so this fixture
            // doubles as the render check for that fix: Shoot("rest") below
            // must show it on one line, not wrapped onto a second the box
            // has no height for. "beetle"/"treant" both carry battle art.
            var enemies = new[] { "beetle", "treant" }
                .Where(id => ContentDatabase.Enemies.Any(e => e.id == id && !string.IsNullOrWhiteSpace(e.Data.SpritePath)))
                .ToList();
            Assert.GreaterOrEqual(party.Count, 1, "no character has battle art to capture");
            Assert.AreEqual(2, enemies.Count, "beetle/treant are missing or lack battle art -- needed for C3's long-name check");

            var built = FightEncounterAdapter.Build(party, enemies, new Domain.Rng.SeededRandom(11));
            var session = built.Session;

            // ---- one enemy carries five statuses -- the enemy row's real
            // capacity is 4 (FightScreen.BuildStatusBadge's own comment: "4
            // statuses plus the +N overflow chip is 5 nodes per row"), so a
            // 5th tips it into the chip.
            var enemy0 = session.Encounter.Enemies[0];

            // A NORMAL encounter never builds a BreakShield (see
            // FightSession.Enemies.cs's own comment) so BRK never has
            // anything to show here on its own. It is the enemy plate's tag
            // node's ONLY remaining content (FightHudModel.EnemyStatusLine),
            // so forcing one broken cheaply here is what lets this capture
            // prove the tag actually renders instead of always shooting "".
            enemy0.BreakShield = new BreakShield(1) { Current = 0, IsBroken = true };

            StatusEffects.Apply(enemy0.Statuses, StatusEffectType.Poison, 5, 3);

            // 1 TURN LEFT ON PURPOSE -- Phase 3's last-tick emphasis (section
            // 2/9) only lights up at Counter == 1, and nothing else in this
            // fixture ever counts down that far on its own.
            StatusEffects.Apply(enemy0.Statuses, StatusEffectType.Vulnerable, 25, 1);
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

            // ---- one OFF-TURN party member carries three statuses, so the
            // roster mini-plate's own row (RosterStatusBadges, 20px, no
            // counter -- section 1's table) is actually exercised by this
            // capture too, rather than only the enemy row and the acting
            // party plate. Mixed polarity (one benefit, two detriments) for
            // the same reason enemy1 above is all-benefit: prove both frame
            // colours render, not just whichever one the acting member's own
            // six statuses happen to lean toward.
            var offTurn = session.Encounter.PlayerParty.First(c => c != actor);
            StatusEffects.Apply(offTurn.Statuses, StatusEffectType.Regen, 5, 3);
            StatusEffects.Apply(offTurn.Statuses, StatusEffectType.Vulnerable, 15, 2);
            session.ApplyChilledForTest(offTurn, 20, 2);

            _fight.Bind(session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party,
                built.Party.Select(p => ContentDatabase.Characters
                        .FirstOrDefault(c => c.Data.DisplayName == p.Name)?.Data.BattleSpritePath)
                    .ToList());

            yield return null;
            yield return null;
            _fight.RefreshUi();

            yield return _rig.Shoot("rest");

            // ---- the FAR slot's enemy badge, hovered -- not the near one.
            // With two enemies on stage, AnchorStageSlots spreads them to
            // the two ENDS of the depth range (FightController.StageVisuals.
            // cs's own header), so slot 1 (enemy1, the second enemy above)
            // lands at the far end -- the rightmost badge row this screen
            // ever draws. Proves the shared tooltip's TooltipPlacement.
            // Beside positioning actually clamps inside the canvas rather
            // than merely landing "beside" a badge with room to spare on
            // every side, which the near slot's own badge would not have
            // caught (see PlaceStatusTooltip's own comment on the first
            // capture's defect). Driven through the component rather than a
            // synthetic pointer, same reasoning FightMenuCaptureTests' own
            // intent-hover shot gives (no real cursor exists in a capture,
            // and a fake EventSystem raycast would test Unity, not this
            // feature).
            var badge = _rig.Named("EnemyStatusBadge1_0");
            Assert.IsNotNull(badge, "enemy slot 1's first status badge was not found");
            var hover = badge.GetComponent<HoverIndex>();
            Assert.IsNotNull(hover, "no HoverIndex was attached to EnemyStatusBadge1_0");
            hover.OnPointerEnter(null);
            yield return _rig.Shoot("hover");
        }
    }
}
