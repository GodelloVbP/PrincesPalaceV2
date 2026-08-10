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
                default: return null;
            }
        }
    }
}
