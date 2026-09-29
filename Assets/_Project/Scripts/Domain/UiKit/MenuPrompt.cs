using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // One entry of the footer's prompt row: a button and what it does now.
    //
    // `Action` is set when pressing it changes game state. The label is then
    // computed against MenuPermissions (MenuPrompts.Resolve), so a prompt can
    // never offer what the same table refuses on the control it belongs to.
    public readonly struct MenuPrompt
    {
        public readonly PromptButton Button;
        public readonly UiString Label;
        public readonly MenuAction? Action;

        public MenuPrompt(PromptButton button, UiString label, MenuAction? action = null)
        {
            Button = button;
            Label = label;
            Action = action;
        }
    }

    // A prompt as it is drawn: glyph letters, their tint, and the final label.
    public readonly struct MenuPromptView
    {
        public readonly string GlyphText;
        public readonly string GlyphTintHex;
        public readonly string Label;

        public MenuPromptView(string glyphText, string glyphTintHex, string label)
        {
            GlyphText = glyphText;
            GlyphTintHex = glyphTintHex;
            Label = label;
        }
    }

    // What the footer says, as pure functions: the frame's default row, and
    // the turn of a prompt list into what a device draws in a context.
    public static class MenuPrompts
    {
        // The row when no pane has anything more specific to say: the frame's
        // own buttons. The pager prompt is there only where the tab pages
        // something (SystemMenuTabDef.Pages, plan C8), so a tab with nothing to
        // page never advertises LB/RB.
        public static IReadOnlyList<MenuPrompt> Defaults(SystemMenuTabDef tab)
        {
            var row = new List<MenuPrompt>
            {
                new MenuPrompt(PromptButton.Confirm, UiStrings.PromptSelect),
                new MenuPrompt(PromptButton.Cancel, UiStrings.PromptClose),
                new MenuPrompt(PromptButton.Tabs, UiStrings.PromptTab),
            };

            if (tab.Pages.IsValid) row.Add(new MenuPrompt(PromptButton.Pages, tab.Pages));
            return row;
        }

        // Prompts in the order given, minus the ones this device has no button
        // for. A refused action gets the lock suffix in whatever context
        // refuses it: the Fight refuses every mutation, and a book at camp or a
        // refund in a descent is locked the same way, from the same table.
        public static IReadOnlyList<MenuPromptView> Resolve(
            IReadOnlyList<MenuPrompt> prompts, MenuContext context, PromptDevice device)
        {
            var views = new List<MenuPromptView>();
            if (prompts == null) return views;

            foreach (var prompt in prompts)
            {
                var glyph = PromptGlyphs.For(device, prompt.Button);
                if (!glyph.IsSet) continue;

                string label = prompt.Label.Format();
                if (prompt.Action.HasValue && !MenuPermissions.Allows(prompt.Action.Value, context))
                {
                    label = UiStrings.PromptLocked.Format(label);
                }

                views.Add(new MenuPromptView(glyph.Text, glyph.TintHex, label));
            }

            return views;
        }
    }
}
