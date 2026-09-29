namespace PrincesPalace
{
    // Every sound the game can play, by MEANING rather than by filename.
    //
    // Call sites say Sound.ButtonClick, never "Audio/Sfx/button click". That
    // matters more than it looks: the clips currently carry the names they
    // were delivered with — spaces, and in one case a to-do written into the
    // filename — and renaming or replacing a file should be a one-line change
    // here rather than a hunt through every caller.
    //
    // Adding a sound is: a value here, a case in SoundLibrary.PathOf, and a
    // file. A test walks the enum and asserts every value resolves to a clip
    // that actually exists, so a missing file fails the suite rather than
    // being silently inaudible.
    public enum Sound
    {
        None,

        // Fired by ButtonPressAnimator on every button in the game.
        ButtonClick,

        // The Play button specifically, instead of the generic click.
        PressPlay,

        // Over the "Palazzo Games" splash, once per launch.
        StartupIntro,

        // Back, Cancel and Close buttons (UiNode.Closes), instead of the
        // generic click.
        MenuClose,

        // A press on a control that is switched off (locked, unaffordable,
        // not yet available). Played by the same two press handlers that play
        // ButtonClick, so it needs no per-screen wiring.
        ClickFail,

        // Something opening that was earned: a reward-track claim, a relic
        // draft appearing.
        OpenPack,

        // The two takes of a tab or screen switch. Never asked for by name --
        // TabSwipe.Next alternates them so two switches in a row differ.
        Swipe1,
        Swipe2,

        // The fight-won sting, layered with the character's own victory line.
        VictorySting,
    }

    public static class TabSwipe
    {
        private static bool _second;

        public static Sound Next()
        {
            _second = !_second;
            return _second ? Sound.Swipe2 : Sound.Swipe1;
        }
    }

    public static class SoundLibrary
    {
        // Resources-relative, WITHOUT the file extension — that is what
        // Resources.Load expects. The folder layout is documented in
        // Assets/_Project/Resources/Audio/README.md.
        public static string PathOf(Sound sound)
        {
            switch (sound)
            {
                case Sound.ButtonClick: return "Audio/Sfx/button click";
                case Sound.PressPlay: return "Audio/Sfx/press play";
                case Sound.StartupIntro: return "Audio/Sfx/start-up intro";
                case Sound.MenuClose: return "Audio/Sfx/menu_close";
                case Sound.ClickFail: return "Audio/Sfx/click_fail";
                case Sound.OpenPack: return "Audio/Sfx/open_the_pack";
                case Sound.Swipe1: return "Audio/Sfx/swipe_1";
                case Sound.Swipe2: return "Audio/Sfx/swipe_2";
                case Sound.VictorySting: return "Audio/Sfx/victory_sting";
                default: return null;
            }
        }
    }
}
