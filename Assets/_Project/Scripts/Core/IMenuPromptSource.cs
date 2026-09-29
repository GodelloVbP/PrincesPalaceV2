using System.Collections.Generic;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // A pane that knows what its buttons do (plan C12). The footer asks every
    // active source under the visible pane which prompts to draw for the
    // focused control, and draws the first answer.
    //
    // A source answers ONLY for controls it owns and returns null for anything
    // else, so a pane hosting another (the dossier hosts the reward track)
    // needs no knowledge of it. Null from every source means the frame's
    // defaults (MenuPrompts.Defaults). A returned list is the whole row: it is
    // what the pane's state offers, in order, and the frame adds nothing to it.
    //
    // Pending state belongs in the answer, not in a flag: a Party carry says
    // "Place / Put back" because it is carrying when asked, the same way
    // INavCancelClaim answers at the moment of the press.
    //
    // A prompt whose press changes game state names its MenuAction; the frame
    // suffixes the label where MenuPermissions refuses it, so the pane never
    // decides "locked" itself.
    public interface IMenuPromptSource
    {
        IReadOnlyList<MenuPrompt> PromptsFor(Selectable focused);
    }
}
