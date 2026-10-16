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
    // Unpitched sound is one AudioSource with PlayOneShot rather than a pool.
    // PlayOneShot mixes overlapping clips on a single source, which is exactly
    // what UI sound needs: two buttons clicked in quick succession should
    // overlap, not cut each other off. Pitched sound has its own small pool
    // (see "pitched one-shots").
    //
    // Volume is read from GameSettings AT PLAY TIME rather than cached, so
    // dragging the Sound slider takes effect on the very next sound with no
    // change-notification plumbing at all.
    public class SoundController : MonoBehaviour
    {
        private static SoundController _instance;

        private AudioSource _source;

        // THE SPACE THE WORLD'S SOUNDS HAPPEN IN. PlayClip (spells, melee,
        // voices, statuses, events) goes through a second source on a child
        // object carrying an AudioReverbFilter; Play(Sound) (UI clicks,
        // stings) stays dry, since a reverberant button is a muddy button.
        // The filter is inline on that source rather than a mixer send: the
        // project has no AudioMixer, and one inline filter is the whole of the
        // need. If a send or a per-group volume is ever wanted, an SFX mixer
        // group replaces this source.
        //
        // THE OWNER TUNES BY EAR, SO ReverbWetMillibels IS THE ONE KNOB: how
        // loud the reverb is against the dry sound, in millibels (-10000
        // silent .. 0 as loud as the dry signal). ReverbEnabled switches the
        // whole thing off.
        public const bool ReverbEnabled = true;
        public const int ReverbWetMillibels = -2200;
        private const float ReverbDecaySeconds = 0.9f;
        private const int ReverbRoomMillibels = -1000;

        private AudioSource _worldSource;

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

            var world = new GameObject("WorldSfx");
            world.transform.SetParent(go.transform, false);
            _instance._worldSource = world.AddComponent<AudioSource>();
            _instance._worldSource.playOnAwake = false;
            _instance._worldSource.spatialBlend = 0f;
            if (ReverbEnabled)
            {
                var reverb = world.AddComponent<AudioReverbFilter>();
                reverb.reverbPreset = AudioReverbPreset.User;
                reverb.dryLevel = 0f;
                reverb.room = ReverbRoomMillibels;
                reverb.decayTime = ReverbDecaySeconds;
                reverb.reverbLevel = ReverbWetMillibels;
            }
        }

        // Told of every enum sound asked for, before the clip is resolved, so a
        // test can count cues in a build with no audio files. Null in play;
        // TestGlobals.ResetAll puts it back.
        public static System.Action<Sound> Observer;

        // PlayClip's twin of Observer: every content-driven clip path requested.
        public static System.Action<string> ClipObserver;

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

            Observer?.Invoke(sound);

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
        public static void PlayClip(string resourcePath, float volumeScale = 1f)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return;
            }

            ClipObserver?.Invoke(resourcePath);

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
            _instance._worldSource.PlayOneShot(clip,
                GameSettings.SoundVolume * AudioLevels.GainFor(resourcePath) * volumeScale);
        }

        // ---- pitched one-shots --------------------------------------------------
        //
        // Pitch belongs to an AudioSource, not to a PlayOneShot, so a pitched
        // shot cannot share the main source: it would retune every clip still
        // ringing there. A small pool of dry sources instead, one clip each,
        // pitch set when the shot starts. A pool rather than pre-pitched clips
        // because a pitch ladder (flip, chevron, coin) is then data at the call
        // site, not N rendered files per sound. The unpitched Play(Sound) path
        // above is untouched.
        //
        // A shot takes an idle source. When all are busy the pool grows up to
        // PitchPoolMax; past that the shot that started longest ago is cut
        // (by start order, not a rotating index, so a shot that just started
        // is never the one lost). An idle
        // source is put back to pitch 1 so a leftover pitch never leaks into
        // the next user of it.
        public const int PitchPoolMax = 8;
        private readonly List<AudioSource> _pitchPool = new List<AudioSource>();

        // Per pooled source, the order its current shot started in.
        private readonly List<long> _pitchStarted = new List<long>();
        private long _pitchShots;

        // Plays a sound at the given pitch (1 = as recorded, 2 = an octave up).
        // Volume, correction and the Observer behave exactly as Play(Sound).
        public static void Play(Sound sound, float pitch)
        {
            if (sound == Sound.None)
            {
                return;
            }

            Observer?.Invoke(sound);

            Bootstrap();

            var clip = _instance.Resolve(sound);
            if (clip == null)
            {
                return;
            }

            var source = _instance.AcquirePitchSource();
            _instance._pitchStarted[_instance._pitchPool.IndexOf(source)] = ++_instance._pitchShots;
            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            source.clip = clip;
            source.volume = Mathf.Clamp01(GameSettings.SoundVolume * AudioLevels.GainFor(SoundLibrary.PathOf(sound)));
            source.Play();
        }

        // For tests: how many pooled sources exist, and how many are sounding.
        public static int PitchSourceCount => _instance != null ? _instance._pitchPool.Count : 0;

        public static int PitchSourcesPlaying
        {
            get
            {
                if (_instance == null) return 0;
                int n = 0;
                foreach (var s in _instance._pitchPool) if (s.isPlaying) n++;
                return n;
            }
        }

        // The pitch of every pooled source, for a test pinning per-shot pitch.
        public static float[] PitchSourcePitches()
        {
            if (_instance == null) return new float[0];
            var r = new float[_instance._pitchPool.Count];
            for (int i = 0; i < r.Length; i++) r[i] = _instance._pitchPool[i].pitch;
            return r;
        }

        // Test seams: end every pooled shot, and run the idle-pitch reset that
        // Update does each frame.
        public static void StopPitched()
        {
            if (_instance == null) return;
            foreach (var s in _instance._pitchPool) s.Stop();
        }

        public static void RestoreIdlePitches()
        {
            if (_instance != null) _instance.ResetIdlePitches();
        }

        private void ResetIdlePitches()
        {
            foreach (var s in _pitchPool)
            {
                if (!s.isPlaying && s.pitch != 1f) s.pitch = 1f;
            }
        }

        public static float MainSourcePitch => _instance != null ? _instance._source.pitch : 1f;

        private AudioSource AcquirePitchSource()
        {
            foreach (var s in _pitchPool)
            {
                if (!s.isPlaying) return s;
            }

            if (_pitchPool.Count < PitchPoolMax)
            {
                var go = new GameObject("PitchedSfx");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _pitchPool.Add(s);
                _pitchStarted.Add(0);
                return s;
            }

            int oldest = 0;
            for (int i = 1; i < _pitchPool.Count; i++)
            {
                if (_pitchStarted[i] < _pitchStarted[oldest]) oldest = i;
            }

            _pitchPool[oldest].Stop();
            return _pitchPool[oldest];
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
            ResetIdlePitches();

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
