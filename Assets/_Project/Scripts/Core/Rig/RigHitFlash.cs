using System.Collections;
using UnityEngine;

namespace PrincesPalace.Core.Rig
{
    // The rig twin of StageHitFlash. SWAPS each part's MATERIAL to
    // RigHitFlash.shader for the flash's duration and restores it
    // afterward, rather than adding overlay renderers the way the uGUI
    // path does: SpriteSkin deforms the MESH and is completely independent
    // of the material, so a swap needs no second SpriteSkin binding per
    // part -- the same renderer just briefly wears a different material.
    //
    // Shares StageHitFlash.AlphaAt for the fade curve rather than
    // duplicating it -- the one extraction the plan called for, done by
    // reuse instead of a copy that could drift.
    public class RigHitFlash : MonoBehaviour
    {
        private const float HoldSeconds = 0.05f;
        private const float FadeSeconds = 0.16f;

        private const float HealHoldSeconds = 0.10f;
        private const float HealFadeSeconds = 0.42f;
        private const float HealPeakAlpha = 0.75f;
        private static readonly Color HealTint = new Color(0.45f, 0.95f, 0.55f, 1f);

        private static Material _flashMaterial;

        private SpriteRenderer[] _parts;
        private Material[] _originalMaterials;
        private Coroutine _running;

        private void Awake()
        {
            _parts = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            _originalMaterials = new Material[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _originalMaterials[i] = _parts[i] != null ? _parts[i].sharedMaterial : null;
            }
        }

        private static Material FlashMaterial()
        {
            if (_flashMaterial == null) _flashMaterial = Resources.Load<Material>("Materials/RigHitFlash");
            return _flashMaterial;
        }

        public void Flash() => Begin(Color.white, HoldSeconds, FadeSeconds, 1f);

        public void FlashHeal() => Begin(HealTint, HealHoldSeconds, HealFadeSeconds, HealPeakAlpha);

        // Stops mid-flash and puts every part's original material back --
        // an abandoned fight must not leave a rig frozen mid-flash over an
        // empty stage, the same reason StageHitFlash has one.
        public void Clear()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            RestoreMaterials();
        }

        private void Begin(Color tint, float hold, float fade, float peakAlpha)
        {
            if (_parts == null || _parts.Length == 0 || FlashMaterial() == null) return;

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(tint, hold, fade, peakAlpha));
        }

        private IEnumerator Run(Color tint, float hold, float fade, float peakAlpha)
        {
            var flashMaterial = FlashMaterial();
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null) _parts[i].sharedMaterial = flashMaterial;
            }

            SetTint(new Color(tint.r, tint.g, tint.b, peakAlpha));
            yield return new WaitForSeconds(FightBeatPlayer.Scaled(hold));

            float elapsed = 0f;
            float scaledFade = FightBeatPlayer.Scaled(fade);
            while (elapsed < scaledFade)
            {
                elapsed += Time.deltaTime;
                SetTint(new Color(tint.r, tint.g, tint.b, StageHitFlash.AlphaAt(elapsed / scaledFade, peakAlpha)));
                yield return null;
            }

            _running = null;
            RestoreMaterials();
        }

        private void SetTint(Color color)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null) _parts[i].color = color;
            }
        }

        private void RestoreMaterials()
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                _parts[i].sharedMaterial = _originalMaterials[i];
                _parts[i].color = Color.white;
            }
        }
    }
}
