using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE ADAPTER, and nothing else -- it feeds live Selectables into
    // UiNavLinkBuilder (Domain, pure, the ONE implementation of the
    // group/link algorithm) and writes what comes back as
    // UnityEngine.UI.Navigation.Explicit. It computes no neighbour itself.
    //
    // WHY NAVIGATION IS WIRED AT RUNTIME AND NOT AT SCENE-BUILD TIME.
    // Phase 2 step A built the other half -- a UiNavDeclaration hung off
    // ScreenDef, resolved by UiAudit.CheckNavigable and written by a
    // build-time UiNavWiring pass -- and it never gained a single consumer.
    // The evidence, screen by screen, is that the navigable set is a RUNTIME
    // fact everywhere this phase touches:
    //   - SystemMenu's tab strip: the scene carries five tabs, a context
    //     shows three, and WHICH three is recomputed in ApplyContext
    //     (SystemMenuScreen's own header says the scene cannot know).
    //   - RewardTrack: the collect button is hidden whenever nothing is owed
    //     (PaintCollectButton), so the rail's Down target exists or does not
    //     depending on save state.
    //   - Party: which seats are occupied, and whether a carry is in
    //     progress, decide what a Move should even reach.
    //   - Map: MapController.Refresh() recomputes the reachable node set per
    //     floor -- the plan's own section 4 already calls this out.
    // A build-time write would therefore be correct for no frame the player
    // ever sees: every one of those surfaces rewires on its own repaint, so
    // the build-time links are overwritten before first use. Keeping both
    // paths is the parallel implementation docs/CODE_STANDARDS.md "Build the model"
    // forbids, and between the two only this one can express a runtime fact
    // -- so the build-time declaration path (ScreenDef.Nav, UiNavWiring,
    // UiAudit.CheckNavigable) is deleted rather than kept for a consumer that
    // does not exist. What survives build-time is UiNavControlsAudit, which
    // checks something a runtime pass genuinely cannot: the inventory of
    // custom actionable controls a screen emitted.
    //
    // AUTHORITATIVE, not incremental: Apply writes all four directions of
    // every node its declaration resolved. A surface therefore passes its
    // WHOLE shape in one call (SystemMenu's tab rail and the selected tab's
    // Down-into-the-pane link together, say) rather than dribbling one axis
    // in per call and depending on what a previous call happened to leave
    // behind.
    //
    // UnityEngine.UI.Selectable/Navigation are spelled out in full
    // throughout: PrincesPalace.Navigation (Core/Navigation.cs, the
    // map/graph state type) sits in this same namespace and would otherwise
    // shadow the UI one -- phase 1's own hazard list calls this out
    // explicitly.
    public static class RuntimeNavWiring
    {
        // A group of live Selectables, with the nulls dropped. A caller
        // holding a fixed-length array with some slots empty (a headless
        // fixture's unwired row, a screen built without art) gets the
        // neighbours it meant -- the survivors linking PAST the hole rather
        // than into it. Returns null, not a throwing empty group, when
        // nothing survives: Apply skips it.
        public static UiNavGroup<UnityEngine.UI.Selectable> Group(
            string id, UiNavGroupKind kind, IEnumerable<UnityEngine.UI.Selectable> members,
            int gridRowLength = 1, UiNavWrap? wrap = null)
        {
            var present = members?.Where(m => m != null).ToList();
            if (present == null || present.Count == 0) return null;
            return new UiNavGroup<UnityEngine.UI.Selectable>(id, kind, present, gridRowLength, wrap);
        }

        public static UiNavLink<UnityEngine.UI.Selectable>? Link(
            UnityEngine.UI.Selectable from, UiNavDirection direction, UnityEngine.UI.Selectable to)
        {
            if (from == null || to == null) return null;
            return new UiNavLink<UnityEngine.UI.Selectable>(from, direction, to);
        }

        // Both halves of a hand-emitted link in one call. Added after a
        // gamepad-nav audit (2026-09-19) found the class of bug this exists
        // to close: MapController wrote choice[i-1] -Down-> choice[i] and
        // never its reverse, so Up did nothing on a screen where Down
        // worked -- one line forgotten among several near-identical ones, in
        // a file whose several other calls to Link already hand-paired both
        // directions correctly. A caller that wants a two-way edge asks for
        // one and gets both; there is nothing left to forget.
        //
        // Not a replacement for one-way Link -- most of this file's callers
        // (a Rail's foot down into a footer, a tab's Down into its pane)
        // mean exactly one direction, and pairing those would wire a
        // click-when-there-was-never-a-corresponding-step. Use this only
        // where the reverse is the same edge walked backwards.
        public static IEnumerable<UiNavLink<UnityEngine.UI.Selectable>?> LinkBoth(
            UnityEngine.UI.Selectable a, UiNavDirection direction, UnityEngine.UI.Selectable b)
        {
            yield return Link(a, direction, b);
            yield return Link(b, Opposite(direction), a);
        }

        private static UiNavDirection Opposite(UiNavDirection direction)
        {
            switch (direction)
            {
                case UiNavDirection.Up: return UiNavDirection.Down;
                case UiNavDirection.Down: return UiNavDirection.Up;
                case UiNavDirection.Left: return UiNavDirection.Right;
                case UiNavDirection.Right: return UiNavDirection.Left;
                default: throw new System.ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static void Apply(
            IEnumerable<UiNavGroup<UnityEngine.UI.Selectable>> groups,
            IEnumerable<UiNavLink<UnityEngine.UI.Selectable>?> links = null)
        {
            var declaration = new UiNavDeclaration<UnityEngine.UI.Selectable>(
                groups,
                links?.Where(l => l.HasValue).Select(l => l.Value));

            foreach (var pair in UiNavLinkBuilder.Build(declaration))
            {
                var selectable = pair.Key;
                if (selectable == null) continue;

                var targets = pair.Value;
                var navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = targets.Up,
                    selectOnDown = targets.Down,
                    selectOnLeft = targets.Left,
                    selectOnRight = targets.Right,
                };
                selectable.navigation = navigation;
            }
        }

        // The one-group case, which is most call sites.
        public static void Apply(UiNavGroup<UnityEngine.UI.Selectable> group,
            IEnumerable<UiNavLink<UnityEngine.UI.Selectable>?> links = null) =>
            Apply(new[] { group }, links);
    }
}
