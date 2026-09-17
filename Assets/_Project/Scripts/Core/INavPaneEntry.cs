using UnityEngine.UI;

namespace PrincesPalace
{
    // WHERE A MOVE INTO A PANE LANDS, answered by the pane itself.
    //
    // SystemMenuController links the selected tab's Down to its pane's own
    // entry, and until now that entry was "the first active Selectable in
    // hierarchy order" -- found generically on purpose, so a pane nobody had
    // wired yet still got something. That answer is the tree's order, not the
    // screen's subject: the dossier's first Selectable is the roster pager
    // beside the character's name, while the pane is about the loadout, so a
    // Move down off the Character tab landed on a paging arrow.
    //
    // A pane that knows better says so here; a pane that does not implement
    // this keeps the generic answer, which is why this is an interface on the
    // pane rather than a field on the menu (a field would be a second place
    // that has to be kept agreeing with whatever the pane later rebuilds).
    // Same shape and same reason as INavCancelClaim: the menu asks the ACTIVE
    // pane at the moment it needs to know.
    //
    // Core rather than Domain/UiKit, unlike the rest of the navigation types:
    // the answer is a live UnityEngine.UI.Selectable, and Domain carries no
    // UnityEngine reference at all (docs/CODE_STANDARDS.md section 1).
    public interface INavPaneEntry
    {
        // Null is a legal answer -- "I have nothing to offer right now, use
        // the generic one" -- which is what a pane whose content has not been
        // built yet has to be able to say.
        Selectable NavEntry { get; }
    }
}
