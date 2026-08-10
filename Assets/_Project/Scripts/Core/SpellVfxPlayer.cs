using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Plays a spell's frame sequence over the fight stage.
    //
    // ONE Image, re-pointed frame by frame, rather than a sprite pool or an
    // Animator. There is no prefab to clone in a generated scene, and an
    // Animator would put the timing in an asset instead of next to the spell
    // that owns it -- a spell's vfxSeconds is authored in skills.json beside its
    // damage, which is where someone tuning the spell is looking.
    //
    // Frames are discovered and cached by FrameSequenceLoader, shared with
    // StanceAnimationLibrary.
    public class SpellVfxPlayer : MonoBehaviour
    {
        [SerializeField] internal Image image;

        private Coroutine _playing;

        public bool IsPlaying => _playing != null;

        // Read-only view, for the tests that assert the effect starts silent and
        // never eats a click. Read-only rather than exposing the field: nothing
        // outside this class has any business swapping the Image.
        public Image Image => image;

        // Set from the controller so the effect lands ON the thing it hit
        // rather than in the middle of the screen.
        public void PlayAt(string vfxPath, float seconds, Vector2 anchoredPosition, Vector2 size)
        {
            if (image == null) return;

            var frames = Frames(vfxPath);
            if (frames == null || frames.Length == 0) return;

            // A second cast before the first finished RE-STARTS it rather than
            // overlapping: two copies of the same effect on one target reads as
            // a rendering bug, not as two casts.
            if (_playing != null) StopCoroutine(_playing);

            // The pool node is built inactive -- there is nothing to show until
            // something is cast -- and StartCoroutine on an inactive GameObject
            // is a hard error rather than a no-op. Same wake-up the hit flash
            // needs, for the same reason.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            _playing = StartCoroutine(PlayRoutine(frames, seconds));
        }

        public void StopImmediately()
        {
            if (_playing != null)
            {
                StopCoroutine(_playing);
                _playing = null;
            }

            if (image != null) image.enabled = false;
        }

        private void OnDisable()
        {
            // An abandoned fight must not leave an effect frozen mid-frame, the
            // same rule FightBeatPlayer.Flush follows for the popups.
            StopImmediately();
        }

        // Exposed so a test can assert a spell's frames actually load, rather
        // than discovering a bad path by seeing nothing happen. Discovery and
        // caching belong to FrameSequenceLoader -- v1 had this cache
        // per-INSTANCE while its two siblings were static, so every fight-scene
        // reload re-probed every spell folder from disk for no reason.
        public Sprite[] Frames(string vfxPath) => FrameSequenceLoader.Load(vfxPath);

        private IEnumerator PlayRoutine(Sprite[] frames, float seconds)
        {
            // Respects the same battle-speed seam the rest of playback uses, so
            // a spell cannot outlast the turn that cast it when a test runs the
            // fight at 60x.
            float perFrame = Mathf.Max(0.001f, FightBeatPlayer.Scaled(seconds / frames.Length));

            image.enabled = true;
            for (int i = 0; i < frames.Length; i++)
            {
                image.sprite = frames[i];
                yield return new WaitForSecondsRealtime(perFrame);
            }

            image.enabled = false;
            _playing = null;
        }
    }
}
