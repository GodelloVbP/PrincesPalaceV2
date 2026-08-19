using UnityEngine;

namespace PrincesPalace
{
    // Global player settings — display and audio — kept deliberately apart
    // from SaveData. These belong to the machine, not to a save slot: a
    // player who picks 1920x1080 windowed expects that on every slot, and
    // deleting a save shouldn't reset their resolution. PlayerPrefs is the
    // idiomatic Unity home for exactly this, and it's keyed per
    // company/product so the isolated TestRunner copy (which has its own
    // productName) can't scribble on the real game's settings.
    //
    // SoundVolume is live — SoundController reads it at play time, so moving
    // the slider changes the very next sound with no change-notification
    // plumbing. MusicVolume is still stored and surfaced only: there is no
    // music yet for it to drive, and the Options label says so rather than
    // pretending.
    public static class GameSettings
    {
        private const string MusicVolumeKey = "settings.musicVolume";
        private const string SoundVolumeKey = "settings.soundVolume";
        private const string ResolutionIndexKey = "settings.resolutionIndex";
        private const string WindowModeIndexKey = "settings.windowModeIndex";
        private const string FpsLimitIndexKey = "settings.fpsLimitIndex";

        public readonly struct ResolutionOption
        {
            public readonly int Width;
            public readonly int Height;

            public ResolutionOption(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public string Label => $"{Width} x {Height}";
        }

        // Common 16:9 desktop resolutions, ascending. Ascending order is
        // load-bearing for DefaultResolutionIndex below, which walks it to
        // find the closest match to the player's actual display.
        public static readonly ResolutionOption[] Resolutions =
        {
            new ResolutionOption(1280, 720),
            new ResolutionOption(1366, 768),
            new ResolutionOption(1600, 900),
            new ResolutionOption(1920, 1080),
            new ResolutionOption(2560, 1440),
            new ResolutionOption(3840, 2160),
        };

        public static readonly string[] WindowModeLabels =
        {
            "Windowed",
            "Borderless Windowed",
            "Fullscreen",
        };

        // Parallel to WindowModeLabels. FullScreenWindow is Unity's
        // borderless-windowed mode; ExclusiveFullScreen is true fullscreen.
        private static readonly FullScreenMode[] WindowModes =
        {
            FullScreenMode.Windowed,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.ExclusiveFullScreen,
        };

        public static readonly int[] FpsLimits = { 30, 60, 120, 144, 240 };

        // NAMED ONCE. These were literals in three places -- the property
        // initialisers, Load's PlayerPrefs fallbacks, and nowhere else that
        // needed them until Options grew a Restore Defaults button. A fourth
        // copy is how a "restore" quietly restores something else.
        public const float DefaultVolume = 0.8f;
        public const int DefaultWindowModeIndex = 1;   // Borderless Windowed
        public const int DefaultFpsLimitIndex = 1;     // 60

        public static float MusicVolume { get; private set; } = DefaultVolume;
        public static float SoundVolume { get; private set; } = DefaultVolume;
        public static int ResolutionIndex { get; private set; }
        public static int WindowModeIndex { get; private set; } = DefaultWindowModeIndex;
        public static int FpsLimitIndex { get; private set; } = DefaultFpsLimitIndex;

        // Applies stored settings before the first scene loads, same
        // self-bootstrapping pattern as CursorController/LoadingScreen-
        // Controller — so a returning player's choices are live from the
        // moment the game opens rather than only after they visit Options.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Load();
            Apply();
        }

        public static void Load()
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
            SoundVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundVolumeKey, DefaultVolume));
            ResolutionIndex = ClampIndex(
                PlayerPrefs.GetInt(ResolutionIndexKey, DefaultResolutionIndexForDisplay()), Resolutions.Length);
            WindowModeIndex = ClampIndex(PlayerPrefs.GetInt(WindowModeIndexKey, DefaultWindowModeIndex), WindowModes.Length);
            FpsLimitIndex = ClampIndex(PlayerPrefs.GetInt(FpsLimitIndexKey, DefaultFpsLimitIndex), FpsLimits.Length);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.SetFloat(SoundVolumeKey, SoundVolume);
            PlayerPrefs.SetInt(ResolutionIndexKey, ResolutionIndex);
            PlayerPrefs.SetInt(WindowModeIndexKey, WindowModeIndex);
            PlayerPrefs.SetInt(FpsLimitIndexKey, FpsLimitIndex);
            PlayerPrefs.Save();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            Save();
        }

        public static void SetSoundVolume(float value)
        {
            SoundVolume = Mathf.Clamp01(value);
            Save();
        }

        public static void SetResolutionIndex(int index)
        {
            ResolutionIndex = ClampIndex(index, Resolutions.Length);
            Save();
            Apply();
        }

        public static void SetWindowModeIndex(int index)
        {
            WindowModeIndex = ClampIndex(index, WindowModes.Length);
            Save();
            Apply();
        }

        public static void SetFpsLimitIndex(int index)
        {
            FpsLimitIndex = ClampIndex(index, FpsLimits.Length);
            Save();
            Apply();
        }

        // Pushes the stored settings at the actual device.
        //
        // Skipped entirely in batch mode, and NOT just for tidiness — the
        // first version applied targetFrameRate unconditionally, which
        // capped the headless test runner at 60fps and took the PlayMode
        // suite from ~9s to ~30s, because every test that yields frames
        // suddenly waited 16ms per frame. A global engine setting changed
        // from a test leaks into every test after it, so the honest fix is
        // to keep device application out of headless runs completely.
        // Screen.SetResolution is skipped for the same class of reason: the
        // runner has no real window, and resizing one mid-suite is at best
        // a no-op and at worst destabilises rendering for everything after.
        //
        // What this costs in coverage, stated plainly: tests can prove the
        // DECISION (which resolution/mode/limit is stored and surfaced) but
        // not the OS-level call. That last hop needs a real windowed build.
        public static void Apply()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            Application.targetFrameRate = FpsLimits[FpsLimitIndex];

            var resolution = Resolutions[ResolutionIndex];
            Screen.SetResolution(resolution.Width, resolution.Height, WindowModes[WindowModeIndex]);
        }

        // Picks the closest listed resolution at or below the player's
        // actual display, so a 1366x768 laptop doesn't open on a 4K default
        // it can't show. Falls back to the largest entry when the display is
        // bigger than everything listed.
        public static int DefaultResolutionIndexForDisplay()
        {
            int displayWidth = Screen.currentResolution.width;
            int best = 0;

            for (int i = 0; i < Resolutions.Length; i++)
            {
                if (Resolutions[i].Width <= displayWidth)
                {
                    best = i;
                }
            }

            return best;
        }

        private static int ClampIndex(int index, int length)
        {
            return length == 0 ? 0 : Mathf.Clamp(index, 0, length - 1);
        }
    }
}
