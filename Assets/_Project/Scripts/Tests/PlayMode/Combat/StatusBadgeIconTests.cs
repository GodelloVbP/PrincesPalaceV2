using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // The status-badge counterpart of EnemyIntentIconTests, one folder over
    // (Resources/Status/ instead of Resources/Intent/). Chilled and Rooted
    // are the only two statuses with real artwork today -- see
    // tools/art/make_status_icons.py and FightHudModel.StatusBadgeIcons --
    // so this splits into "the two that must load a sprite" and "the eight
    // that must NOT", rather than one loop over every StatusEffectType that
    // would treat a still-unauthored icon as a failure.
    //
    // No scene load anywhere in this file: Resources.Load<Sprite> needs no
    // FightController, no session, nothing running -- unlike
    // EnemyIntentIconTests' on-screen placement checks, which are about
    // where a badge SITS and have no status-badge equivalent to mirror
    // (buff badges are a fixed row, not stage-placed).
    public class StatusBadgeIconTests
    {
        private static readonly StatusEffectType[] IconisedStatuses =
        {
            StatusEffectType.Chilled,
            StatusEffectType.Rooted,
        };

        // Every StatusEffectType value that is NOT one of the two above --
        // computed from the enum rather than hand-listed, so a future
        // eleventh status is automatically covered by the "must still fall
        // back to text" half of this file without anyone remembering to add
        // it here.
        private static readonly StatusEffectType[] TextOnlyStatuses =
            System.Enum.GetValues(typeof(StatusEffectType))
                .Cast<StatusEffectType>()
                .Where(k => !IconisedStatuses.Contains(k))
                .ToArray();

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

        // The other eight statuses must resolve to NOTHING -- not "nothing
        // yet by accident", but a path that actually fails to load, which is
        // what lets FightController.Hud.cs's icon-first/glyph-fallback
        // priority fall through to the text glyph. If one of these ever
        // resolved to a real sprite (someone drops a poison.png into
        // Status/ without wiring it through StatusBadgeIcons.Slug, say) the
        // fallback path this test exists to exercise would silently stop
        // being exercised for that status, and nobody would notice until a
        // badge looked wrong on screen.
        [Test]
        public void EveryOtherStatusStillFallsBackToTextWithNoSpriteFound()
        {
            var unexpectedlyLoaded = TextOnlyStatuses
                .Where(k => Resources.Load<Sprite>(FightHudModel.StatusBadgeIcons.ResourceFor(k)) != null)
                .Select(k => $"{k} -> {FightHudModel.StatusBadgeIcons.ResourceFor(k)}")
                .ToList();

            Assert.IsEmpty(unexpectedlyLoaded,
                "these statuses were expected to have no icon (and so fall back to their glyph), " +
                "but a sprite actually loaded for them, which means the icon-first/glyph-fallback " +
                "branch in RefreshPartyBuffs is no longer being exercised for: " +
                string.Join(", ", unexpectedlyLoaded));
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

        // FightHudModel.StatusBadge is private -- it has no public entry
        // point of its own, BuffBadgesFor is the only caller, and
        // BuffBadgesFor needs a full FightSession/CombatantState pair just
        // to reach it. Reflection is the same shortcut
        // EnemyIntentIconTests already takes to reach FightController's
        // private _session field, used here for the identical reason: the
        // fact under test (the exact phrase StatusBadge builds) does not
        // need a live session to exist, and building one would make the
        // test about encounter setup instead of about the string.
        private static FightHudModel.BuffBadge InvokeStatusBadge(ActiveStatus status)
        {
            var method = typeof(FightHudModel).GetMethod("StatusBadge",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FightHudModel.StatusBadge was not found by reflection -- has it been renamed?");
            return (FightHudModel.BuffBadge)method.Invoke(null, new object[] { status });
        }

        // The keyword-style tooltip the designer actually asked for: "a
        // symbol under a character... hovering over this symbol shows what
        // it does (again, in keywords)". Pinned to a LITERAL expected
        // string rather than rebuilding it from status.Magnitude/TurnsLeft
        // in the assertion, per CLAUDE.md gotcha 5 -- recomputing the
        // production formula here would make this test a tautology that
        // could never catch StatusBadge phrasing it differently than
        // intended.
        [Test]
        public void ChilledTooltip_ReadsAsAKeywordPhrase_NotASentence()
        {
            var status = new ActiveStatus(StatusEffectType.Chilled, 20, 2);
            var badge = InvokeStatusBadge(status);

            Assert.AreEqual("CHL", badge.Glyph);
            Assert.AreEqual(StatusEffectType.Chilled, badge.Kind);
            Assert.IsFalse(badge.IsPositive, "Chilled is a malus and must tint red like every other negative badge");
            Assert.AreEqual("Chilled -- <color=#E05A5A>-20% Speed</color>, 2 turns", badge.Tooltip);
        }

        [Test]
        public void RootedTooltip_ReadsAsAKeywordPhrase_NotASentence()
        {
            var status = new ActiveStatus(StatusEffectType.Rooted, 0, 1);
            var badge = InvokeStatusBadge(status);

            Assert.AreEqual("ROT", badge.Glyph);
            Assert.AreEqual(StatusEffectType.Rooted, badge.Kind);
            Assert.IsFalse(badge.IsPositive, "Rooted is a malus and must tint red like every other negative badge");
            Assert.AreEqual("Rooted -- <color=#E05A5A>Skill Only</color>, 1 turn", badge.Tooltip);
        }
    }
}
