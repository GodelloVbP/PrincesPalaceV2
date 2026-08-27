using System.Collections;
using UnityEngine;

namespace PrincesPalace.Core.Rig
{
    // The rig twin of StageDeathFade. Needs no shader at all, unlike the
    // hit flash: a fade is pure ALPHA, and the default sprite material
    // already blends that correctly -- it is TINT (RGB) that a plain
    // SpriteRenderer.color multiplies away, not alpha.
    public class RigDeathFade : MonoBehaviour
    {
        private const float HoldSeconds = 0.35f;
        private const float FadeSeconds = 0.6f;

        private SpriteRenderer[] _parts;
        private float[] _baseAlpha;
        private Coroutine _running;
        private bool _played;

        private void Awake() => CacheParts();

        private void CacheParts()
        {
            if (_parts != null) return;

            _parts = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            _baseAlpha = new float[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _baseAlpha[i] = _parts[i] != null ? _parts[i].color.a : 1f;
            }
        }

        // No-ops on every call after the first, so FadeTheFallen can call
        // this on EVERY repaint of a dead combatant without restarting the
        // fade each time -- the same contract StageDeathFade.PlayIfNotAlready
        // states.
        public void PlayIfNotAlready()
        {
            CacheParts();
            if (_played || !isActiveAndEnabled) return;

            _played = true;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(FadeRoutine());
        }

        // Snaps to fully faded, for a fight ending mid-fade -- the rig twin
        // of StageDeathFade.Clear().
        public void Clear()
        {
            CacheParts();
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _played = true;
            SetFraction(0f);
        }

        private IEnumerator FadeRoutine()
        {
            yield return new WaitForSeconds(FightBeatPlayer.Scaled(HoldSeconds));

            float fadeSeconds = FightBeatPlayer.Scaled(FadeSeconds);
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                SetFraction(1f - (t / fadeSeconds));
                yield return null;
            }

            SetFraction(0f);
            _running = null;
        }

        private void SetFraction(float fraction)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                var c = _parts[i].color;
                _parts[i].color = new Color(c.r, c.g, c.b, _baseAlpha[i] * fraction);
            }
        }
    }
}
