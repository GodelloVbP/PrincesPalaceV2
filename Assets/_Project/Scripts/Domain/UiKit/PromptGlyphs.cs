namespace PrincesPalace.Domain.UiKit
{
    // Which family of controls the player is holding. The footer reads
    // NavigationInputModule.LastInputWasPad and maps it here, so the mapping is
    // the only place a device is named. A PlayStation table is one more member
    // and one more switch arm in PromptGlyphs.
    public enum PromptDevice
    {
        Keyboard,
        Xbox,
    }

    // A control by what it is, not by what it is printed as: the same prompt
    // ("confirm") is Enter on a keyboard and A on a pad.
    public enum PromptButton
    {
        Confirm,
        Cancel,
        West,
        North,
        PageBack,
        PageForward,
        TabBack,
        TabForward,

        // Both shoulders (or both keys) as one prompt: "LB/RB Character".
        Pages,

        // Both triggers as one prompt: "LT/RT Tab". A keyboard steps tabs with
        // the pointer, so its glyph is the click.
        Tabs,

        View,
        Start,

        // What opens the menu on CHARACTER: the C hotkey, or Start on a pad.
        Character,
    }

    // What a button is drawn as: the letters on its plate, and the tint of
    // those letters. The plate itself is generated (a rimmed solid), not art.
    public readonly struct PromptGlyph
    {
        public readonly string Text;
        public readonly string TintHex;

        public PromptGlyph(string text, string tintHex)
        {
            Text = text;
            TintHex = tintHex;
        }

        public bool IsSet => !string.IsNullOrEmpty(Text);
    }

    // THE ONE TABLE: device x button -> glyph text and tint (plan C12).
    //
    // A button a device does not have returns an unset glyph, and the footer
    // leaves that prompt out rather than drawing a blank plate: the keyboard
    // has no X, Y or View.
    public static class PromptGlyphs
    {
        // Xbox face buttons wear their own colours; everything else is plain.
        public const string Plain = FightHudPalette.TextPrimary;
        public const string Green = "#6FD08C";
        public const string Red = "#F0716A";
        public const string Blue = "#66AEF0";
        public const string Yellow = "#F0C955";

        public static PromptDevice DeviceFor(bool lastInputWasPad) =>
            lastInputWasPad ? PromptDevice.Xbox : PromptDevice.Keyboard;

        public static PromptGlyph For(PromptDevice device, PromptButton button)
        {
            switch (device)
            {
                case PromptDevice.Xbox: return Xbox(button);
                case PromptDevice.Keyboard: return Keyboard(button);
                default: throw new System.ArgumentOutOfRangeException(nameof(device), device, null);
            }
        }

        private static PromptGlyph Xbox(PromptButton button)
        {
            switch (button)
            {
                case PromptButton.Confirm: return new PromptGlyph("A", Green);
                case PromptButton.Cancel: return new PromptGlyph("B", Red);
                case PromptButton.West: return new PromptGlyph("X", Blue);
                case PromptButton.North: return new PromptGlyph("Y", Yellow);
                case PromptButton.PageBack: return new PromptGlyph("LB", Plain);
                case PromptButton.PageForward: return new PromptGlyph("RB", Plain);
                case PromptButton.TabBack: return new PromptGlyph("LT", Plain);
                case PromptButton.TabForward: return new PromptGlyph("RT", Plain);
                case PromptButton.Pages: return new PromptGlyph("LB/RB", Plain);
                case PromptButton.Tabs: return new PromptGlyph("LT/RT", Plain);
                case PromptButton.View: return new PromptGlyph("View", Plain);
                case PromptButton.Start: return new PromptGlyph("Start", Plain);
                case PromptButton.Character: return new PromptGlyph("Start", Plain);
                default: throw new System.ArgumentOutOfRangeException(nameof(button), button, null);
            }
        }

        private static PromptGlyph Keyboard(PromptButton button)
        {
            switch (button)
            {
                case PromptButton.Confirm: return new PromptGlyph("Enter", Plain);
                case PromptButton.Cancel: return new PromptGlyph("Esc", Plain);
                case PromptButton.PageBack: return new PromptGlyph("Q", Plain);
                case PromptButton.PageForward: return new PromptGlyph("E", Plain);
                case PromptButton.Pages: return new PromptGlyph("Q/E", Plain);
                case PromptButton.Tabs: return new PromptGlyph("Click", Plain);
                case PromptButton.Start: return new PromptGlyph("Esc", Plain);
                case PromptButton.Character: return new PromptGlyph("C", Plain);

                // No keys: the keyboard reaches these through the pointer and
                // the hotkeys, so a prompt naming them is dropped.
                case PromptButton.West:
                case PromptButton.North:
                case PromptButton.TabBack:
                case PromptButton.TabForward:
                case PromptButton.View:
                    return default;

                default: throw new System.ArgumentOutOfRangeException(nameof(button), button, null);
            }
        }
    }
}
