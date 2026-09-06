using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // The status-badge counterpart of EnemyIntentIconTests, one folder over
    // (Resources/Status/ instead of Resources/Intent/). Chilled and Rooted
    // are the only two statuses with real artwork today -- see
    // tools/art/make_status_icons.py and FightHudModel.StatusBadgeIcons.
    //
    // NARROWED DELIBERATELY to the two that already have art -- Package C of
    // PLAN_STATUS_EFFECT_UI.md. Section 10 wants this widened to all
    // fourteen entries (twelve statuses plus the two speed presentations)
    // and left red until the art lands; CLAUDE.md's "commit only
    // test-passing checkpoints" rule means that widening ships in the same
    // commit as the art itself (phase 2), not here. The EditMode half of the
    // completeness test (StatusHudCoverageTests) already covers the other
    // twelve's code/slug/tooltip, so nothing about them goes unchecked in
    // the meantime -- only "does a PNG actually exist for it" is deferred.
    //
    // The negative half this file used to run
    // (EveryOtherStatusStillFallsBackToTextWithNoSpriteFound, asserting the
    // other statuses must NOT load a sprite) is deleted outright rather than
    // ignored: PLAN_STATUS_EFFECT_UI.md commissions exactly that art, so a
    // test forbidding it would have to go the moment phase 2 lands it
    // anyway, and there is no value in keeping it green for a commit or two
    // in between.
    //
    // No scene load anywhere in this file: Resources.Load<Sprite> needs no
    // FightController, no session, nothing running -- unlike
    // EnemyIntentIconTests' on-screen placement checks, which are about
    // where a badge SITS and have no status-badge equivalent to mirror
    // (a status row is a fixed strip, not stage-placed).
    public class StatusBadgeIconTests
    {
        private static readonly StatusEffectType[] IconisedStatuses =
        {
            StatusEffectType.Chilled,
            StatusEffectType.Rooted,
        };

        // THIS is the guarantee that matters: Chilled and Rooted must each
        // resolve to a real sprite. Resources.Load returns null silently for
        // a missing file, a wrong path, or a PNG that imported as a plain
        // Texture rather than a Sprite -- three separate ways to end up with
        // an invisible badge and nothing in the log, the exact trap
        // EnemyIntentIconTests' own version of this test exists to catch.
        [Test]
        public void EveryStatusIconResolvesToArtworkThatActuallyLoaded()
        {
            var missing = IconisedStatuses
                .Where(k => Resources.Load<Sprite>(FightHudModel.StatusBadgeIcons.ResourceFor(k)) == null)
                .Select(k => $"{k} -> {FightHudModel.StatusBadgeIcons.ResourceFor(k)}")
                .ToList();

            Assert.IsEmpty(missing,
                "these status kinds have no loadable sprite, so their badge renders as nothing: " +
                string.Join(", ", missing) +
                ". Check the file exists under Assets/_Project/Resources/Status/ and that " +
                "StatusIconImportPostprocessor gave it textureType Sprite.");
        }

        // The path convention, asserted rather than assumed -- same reason
        // EnemyIntentIconTests pins it for Intent/: Resources.Load returns
        // null for an Assets/-rooted path or one carrying a file extension,
        // and says nothing about why.
        [Test]
        public void TheIconPathsFollowTheRuntimeLoadingConvention()
        {
            foreach (StatusEffectType kind in System.Enum.GetValues(typeof(StatusEffectType)))
            {
                string path = FightHudModel.StatusBadgeIcons.ResourceFor(kind);
                Assert.IsFalse(path.StartsWith("Assets/"), $"{kind}: '{path}' must be Resources-relative");
                Assert.IsFalse(path.EndsWith(".png"), $"{kind}: '{path}' must not carry a file extension");
                StringAssert.StartsWith("Status/", path, $"{kind}: '{path}' must live under the Status/ folder");
            }
        }

        // StatusHud.RowFor is PUBLIC -- unlike the FightHudModel.StatusBadge
        // switch this file used to reach through reflection (deleted;
        // PLAN_STATUS_EFFECT_UI.md section 4/11 merged StatusBadge and
        // PillCode into the one StatusHud table), so these two read the row
        // straight off StatusHud with no reflection at all.
        [Test]
        public void ChilledTooltip_ReadsAsAKeywordPhrase_NotASentence()
        {
            var status = new ActiveStatus(StatusEffectType.Chilled, 20, 2);
            var row = StatusHud.RowFor(status);

            Assert.AreEqual("CHL", row.Code);
            Assert.AreEqual("chilled", row.Slug);
            Assert.IsFalse(row.IsPositive, "Chilled is a malus and must tint red like every other negative badge");
            Assert.AreEqual("Chilled -- <color=#E05A5A>-20% Speed</color>, 2 turns", row.Tooltip);
        }

        [Test]
        public void RootedTooltip_ReadsAsAKeywordPhrase_NotASentence()
        {
            var status = new ActiveStatus(StatusEffectType.Rooted, 0, 1);
            var row = StatusHud.RowFor(status);

            // RTD, not ROT -- section 4's merged table renamed Rooted's code
            // because this game already has a Poison status and ROT reads
            // as that.
            Assert.AreEqual("RTD", row.Code);
            Assert.AreEqual("rooted", row.Slug);
            Assert.IsFalse(row.IsPositive, "Rooted is a malus and must tint red like every other negative badge");
            Assert.AreEqual("Rooted -- <color=#E05A5A>Skill Only</color>, 1 turn", row.Tooltip);
        }
    }
}
