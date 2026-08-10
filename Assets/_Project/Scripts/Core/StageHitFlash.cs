using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // A white silhouette of the actor, flashed on the frame a blow lands.
    //
    // Needs a SHADER rather than just tinting the Image white, because
    // Image.color MULTIPLIES the texture -- white is the identity there, so a
    // white tint changes nothing at all. UIHitFlash keeps the sprite's alpha (so
    // the flash is the figure's exact shape) and throws away its RGB.
    //
    // Degrades gracefully to nothing if the material is missing: a fight with no
    // flash still plays, which is better than a fight that throws.
    public class StageHitFlash : MonoBehaviour
    {
        // Short and hard. A flash long enough to read as a colour rather than as
        // an impact stops selling the hit.
        private const float HoldSeconds = 0.05f;
        private const float FadeSeconds = 0.16f;

        // A heal is the same mechanism at a different tempo: softer, greener,
        // and slower, because it is good news rather than an impact.
        private const float HealHoldSeconds = 0.10f;
        private const float HealFadeSeconds = 0.42f;
        private const float HealPeakAlpha = 0.75f;
        private static readonly Color HealTint = new Color(0.45f, 0.95f, 0.55f, 1f);

        [SerializeField] internal Image image;

        // Assignable from a test harness that builds its own canvas. The scene
        // binds the field directly; this is the seam a PlayMode pixel test uses
        // to point one at a sprite it made up.
        public void Attach(Image target)
        {
            image = target;
        }

        private Coroutine _running;

        // What the flash is currently shaped like. Exposed so a test can assert
        // the silhouette follows the actor's own art rather than lagging a
        // stance behind it.
        public Sprite LastFlashedSprite { get; private set; }

        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
            Clear();
        }

        // Kept in step with whatever stance the actor is wearing. Called by the
        // stage visuals whenever the sprite under it changes, because a flash
        // shaped like the PREVIOUS pose is worse than no flash.
        public void SetSprite(Sprite sprite)
        {
            LastFlashedSprite = sprite;
            if (image != null) image.sprite = sprite;
        }

        public void Flash() => Flash(Color.white, HoldSeconds, FadeSeconds, 1f);

        public void FlashHeal() => Flash(HealTint, HealHoldSeconds, HealFadeSeconds, HealPeakAlpha);

        public void Clear()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (image != null)
            {
                var colour = image.color;
                colour.a = 0f;
                image.color = colour;
                image.enabled = false;
            }
        }

        private void Flash(Color tint, float hold, float fade, float peakAlpha)
        {
            if (image == null || image.sprite == null) return;

            // The overlay is BUILT INACTIVE -- it has nothing to show until a
            // blow lands -- and StartCoroutine on an inactive GameObject is a
            // hard error, not a silent no-op. Woken here rather than left active
            // in the tree, because an always-on transparent overlay would still
            // be a Graphic in every rebuild and every raycast.
            //
            // It stays active afterwards: Clear disables the IMAGE, which stops
            // it drawing without re-arming this trap on the next flash.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(tint, hold, fade, peakAlpha));
        }

        private IEnumerator Run(Color tint, float hold, float fade, float peakAlpha)
        {
            image.enabled = true;
            image.color = new Color(tint.r, tint.g, tint.b, peakAlpha);

            yield return new WaitForSeconds(FightBeatPlayer.Scaled(hold));

            float elapsed = 0f;
            float scaledFade = FightBeatPlayer.Scaled(fade);
            while (elapsed < scaledFade)
            {
                elapsed += Time.deltaTime;
                image.color = new Color(tint.r, tint.g, tint.b, AlphaAt(elapsed / scaledFade, peakAlpha));
                yield return null;
            }

            _running = null;
            Clear();
        }

        // The pure static seam every animator in this project has: the curve is
        // pinned by an EditMode test with no scene, no coroutine and no frame.
        public static float AlphaAt(float fadeProgress, float peakAlpha) =>
            Mathf.Clamp01(peakAlpha * (1f - Mathf.Clamp01(fadeProgress)));
    }
}
