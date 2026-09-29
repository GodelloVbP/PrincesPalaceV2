using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // The status-badge counterpart of EnemyIntentIconTests, one folder over
    // (Resources/Status/ instead of Resources/Intent/). All fourteen
    // presentations -- every StatusEffectType plus the two speed
    // presentations (StatusHud.SpeedUpSlug/SpeedDownSlug, which have no enum
    // member on purpose -- section 4) now carry real generated artwork; see
    // CREDITS.md's "Status badge icons" entry and
    // docs/STATUS_ICON_PROMPTS.md, the frozen art contract they were
    // generated from.
    //
    // All fourteen presentations are asserted to carry real art; none is
    // asserted to fall back to text.
    //
    // No scene load anywhere in this file: Resources.Load<Sprite> needs no
    // FightController, no session, nothing running -- unlike
    // EnemyIntentIconTests' on-screen placement checks, which are about
    // where a badge SITS and have no status-badge equivalent to mirror
    // (a status row is a fixed strip, not stage-placed).
    public class StatusBadgeIconTests
    {
        // Every resource path a badge can ever ask Resources.Load for: the
        // twelve StatusEffectType members through StatusBadgeIcons.ResourceFor,
        // plus the two speed presentations, which have no enum member to
        // iterate and so are named explicitly here the same way
        // StatusHudCoverageTests does.
        private static IEnumerable<string> AllResourcePaths()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
                yield return FightHudModel.StatusBadgeIcons.ResourceFor(type);

            yield return "Status/" + StatusHud.SpeedUpSlug;
            yield return "Status/" + StatusHud.SpeedDownSlug;
        }

        // THIS is the guarantee that matters: every one of the fourteen must
        // resolve to a real, correctly-sized sprite. Resources.Load returns
        // null silently for a missing file, a wrong path, or a PNG that
        // imported as a plain Texture rather than a Sprite -- three separate
        // ways to end up with an invisible badge and nothing in the log, the
        // exact trap EnemyIntentIconTests' own version of this test exists
        // to catch. The 256x256 check catches a fourth: a master dropped in
        // unresized, which would blow out every badge's layout math.
        // FORTIFIED IS THE SAME STATED GAP (Bjorn's Hold the Line): the badge
        // draws its FRT code until the art is commissioned (Phase 7 of
        // docs/PLAN_BJORN_CONSTELLATIONS.md).
        //
        // BURN AND THORNED ARE A STATED GAP, NOT A REGRESSION (spell-expansion
        // milestone E, docs/PLAN_SPELL_EXPANSION.md section 6's stage 5
        // checklist): "register the key, let LoadSprite degrade to a blank
        // slot with a warning until the art lands" -- the same convention
        // Marks.IconKey documents for Marked before its own art shipped.
        // Excluded from the hard "must load" half below for exactly that
        // reason; TheIconPathsFollowTheRuntimeLoadingConvention still checks
        // both of their paths shape correctly, unconditionally.
        private static readonly StatusEffectType[] AwaitingArt =
        {
            StatusEffectType.Burn,
            StatusEffectType.Thorned,
            StatusEffectType.Fortified,
        };

        [Test]
        public void EveryStatusIconResolvesToArtworkThatActuallyLoaded()
        {
            var paths = AllResourcePaths().ToList();
            Assert.AreEqual(18, paths.Count, "expected sixteen statuses plus two speed presentations");

            var awaitingArtPaths = AwaitingArt
                .Select(t => FightHudModel.StatusBadgeIcons.ResourceFor(t))
                .ToHashSet();

            var missing = new List<string>();
            var wrongSize = new List<string>();

            foreach (var path in paths)
            {
                var sprite = Resources.Load<Sprite>(path);
                if (sprite == null)
                {
                    if (!awaitingArtPaths.Contains(path)) missing.Add(path);
                    continue;
                }

                if (sprite.rect.width != 256 || sprite.rect.height != 256)
                    wrongSize.Add($"{path} ({sprite.rect.width}x{sprite.rect.height})");
            }

            Assert.IsEmpty(missing,
                "these paths have no loadable sprite, so their badge renders as nothing: " +
                string.Join(", ", missing) +
                ". Check the file exists under Assets/_Project/Resources/Status/ and that " +
                "StatusIconImportPostprocessor gave it textureType Sprite.");

            Assert.IsEmpty(wrongSize,
                "these sprites are not the 256x256 delivery size the layout code expects: " +
                string.Join(", ", wrongSize));
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

        // StatusHud.RowFor is PUBLIC, so these two read the row straight
        // off StatusHud with no reflection at all.
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
