using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // Plays sound effects. Self-bootstrapping and surviving scene loads, the
    // same pattern as CursorController, LoadingScreenController and
    // GameSettings — so any caller can just say SoundController.Play(...)
    // without a wired reference, and a sound started in the Main Menu is not
    // cut off the instant the Gameplay scene loads.
    //
    // One AudioSource with PlayOneShot rather than a pool. PlayOneShot mixes
    // overlapping clips on a single source, which is exactly what UI sound
    // needs: two buttons clicked in quick succession should overlap, not cut
    // each other off.
    //
    // Volume is read from GameSettings AT PLAY TIME rather than cached, so
    // dragging the Sound slider takes effect on the very next sound with no
    // change-notification plumbing at all.
    public class SoundController : MonoBehaviour
    {
        private static SoundController _instance;

        private AudioSource _source;

        // Clips are cached on first use rather than all loaded up front:
        // Resources.Load is not free, most sounds are not needed in the first
        // seconds, and a clip that never plays never costs anything.
        private readonly Dictionary<Sound, AudioClip> _clips = new Dictionary<Sound, AudioClip>();
        private readonly Dictionary<string, AudioClip> _clipsByPath = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("SoundController");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoundController>();
            _instance._source = go.AddComponent<AudioSource>();
            _instance._source.playOnAwake = false;
            // 2D. A UI sound has no position in the world, and a default 3D
            // source sitting at the origin would pan and attenuate based on
            // where the camera happens to be.
            _instance._source.spatialBlend = 0f;
        }

        // Plays a sound at the player's current effect volume. Silent and
        // harmless if the clip is missing — a game with no audio files should
        // still run, which is the same graceful-degradation posture the rest
        // of the project takes toward missing art.
        public static void Play(Sound sound)
        {
            if (sound == Sound.None)
            {
                return;
            }

            Bootstrap();

            var clip = _instance.Resolve(sound);
            if (clip == null)
            {
                return;
            }

            // Scaled by this clip's own measured correction, so the Sound
            // slider sets ONE perceived level rather than a different one per
            // effect -- see AudioLevels for what the source material looked
            // like before this.
            _instance._source.PlayOneShot(clip, GameSettings.SoundVolume * AudioLevels.GainFor(SoundLibrary.PathOf(sound)));
        }

        // The content-driven half. A skill names its own clip by path in
        // skills.json, so adding a spell with its own sound is authoring
        // rather than a code change and a new member of the Sound enum.
        //
        // The enum still exists and is still the right shape for the game's
        // OWN sounds — button clicks, the startup sting — where a typo should
        // be a compile error rather than silence.
        public static void PlayClip(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return;
            }

            Bootstrap();

            var clip = _instance.ResolveByPath(resourcePath);
            if (clip == null)
            {
                return;
            }

            // Same correction as Play(Sound) above. This is also the path
            // every one of Shawn's voice lines takes (CharacterVoice routes
            // through here), and those were the loudest thing in the game by
            // a wide margin.
            _instance._source.PlayOneShot(clip, GameSettings.SoundVolume * AudioLevels.GainFor(resourcePath));
        }

        // ---- the one looping channel --------------------------------------------
        //
        // AMBIENCE: one clip looped under everything else, for as long as a
        // screen wants it (an event fight's fog and wind,
        // docs/PLAN_EVENTS_BELL_AND_CARAVAN.md M6). ITS OWN SOURCE, not the
        // one-shot one: PlayOneShot cannot loop and cannot be stopped without
        // cutting every effect mixed on it too.
        //
        // ONE CHANNEL, not a pool. Nothing wants two beds at once, and a
        // second StartAmbience replacing the first is exactly what a fight
        // opening straight after another needs. The same path again keeps the
        // loop running rather than restarting it.
        //
        // At the SOUND volume, read at start and followed live in Update --
        // the slider moves a playing bed too. Music has no player yet
        // (GameSettings' own header); ambience is effect sound, not score.
        //
        // A missing clip is silence, never an error, the one-shot path's
        // posture: the channel stops and reports nothing playing.
        private AudioSource _ambience;
        private string _ambiencePath = "";

        // What the channel is looping, "" when it is silent. For tests and
        // for a caller deciding whether to restart.
        public static string AmbiencePath => _instance != null ? _instance._ambiencePath : "";

        public static bool AmbiencePlaying =>
            _instance != null && _instance._ambience != null && _instance._ambience.isPlaying;

        public static void StartAmbience(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                StopAmbience();
                return;
            }

            Bootstrap();

            if (_instance._ambiencePath == resourcePath && AmbiencePlaying) return;

            var clip = _instance.ResolveByPath(resourcePath);
            if (clip == null)
            {
                StopAmbience();
                return;
            }

            var source = _instance.AmbienceSource();
            source.clip = clip;
            source.volume = AmbienceVolumeFor(resourcePath);
            source.Play();
            _instance._ambiencePath = resourcePath;
        }

        public static void StopAmbience()
        {
            if (_instance == null) return;

            if (_instance._ambience != null)
            {
                _instance._ambience.Stop();
                _instance._ambience.clip = null;
            }

            _instance._ambiencePath = "";
        }

        private static float AmbienceVolumeFor(string resourcePath) =>
            GameSettings.SoundVolume * AudioLevels.GainFor(resourcePath);

        private AudioSource AmbienceSource()
        {
            if (_ambience != null) return _ambience;

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.playOnAwake = false;
            _ambience.loop = true;
            _ambience.spatialBlend = 0f;
            return _ambience;
        }

        private void Update()
        {
            if (_ambience == null || !_ambience.isPlaying) return;
            _ambience.volume = AmbienceVolumeFor(_ambiencePath);
        }

        // Exposed so a test can assert every Sound value resolves to a real
        // file, rather than discovering a typo'd path by not hearing anything.
        public static AudioClip ClipFor(Sound sound)
        {
            Bootstrap();
            return _instance.Resolve(sound);
        }

        // The path-keyed twin, for the same reason.
        public static AudioClip ClipAt(string resourcePath)
        {
            Bootstrap();
            return string.IsNullOrWhiteSpace(resourcePath) ? null : _instance.ResolveByPath(resourcePath);
        }

        private AudioClip ResolveByPath(string resourcePath)
        {
            if (_clipsByPath.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var clip = Resources.Load<AudioClip>(resourcePath);
            _clipsByPath[resourcePath] = clip;
            return clip;
        }

        private AudioClip Resolve(Sound sound)
        {
            if (_clips.TryGetValue(sound, out var cached))
            {
                return cached;
            }

            string path = SoundLibrary.PathOf(sound);
            var clip = string.IsNullOrEmpty(path) ? null : Resources.Load<AudioClip>(path);

            // Cached even when null, so a missing file costs one failed
            // Resources.Load rather than one per click for the rest of the
            // session.
            _clips[sound] = clip;
            return clip;
        }
    }
}
