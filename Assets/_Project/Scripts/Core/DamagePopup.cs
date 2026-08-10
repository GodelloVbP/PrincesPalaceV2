using System.Collections;
using TMPro;
using UnityEngine;

namespace PrincesPalace
{
    // A floating "-4" that rises and fades.
    //
    // Pooled at a fixed capacity rather than instantiated: the scene is
    // generated, so there is no prefab to clone, and building a hierarchy inside
    // a combat frame would put the text styling in a second place.
    public class DamagePopup : MonoBehaviour
    {
        private const float RiseDistance = 90f;
        private const float LifeSeconds = 0.85f;

        // Fully opaque for the first third, then fades. A number that starts
        // fading immediately is hard to read at exactly the moment it matters.
        private const float OpaqueFraction = 0.35f;

        private static readonly Color DamageColor = new Color(0.93f, 0.26f, 0.24f, 1f);
        private static readonly Color HealColor = new Color(0.42f, 0.86f, 0.45f, 1f);

        [SerializeField] internal TMP_Text label;

        private RectTransform _rect;
        private Coroutine _running;

        public bool IsFree => _running == null && !gameObject.activeSelf;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        public void Play(Vector2 anchoredStart, int amount, bool isHealing)
        {
            if (_rect == null) _rect = (RectTransform)transform;

            // Restarting on a popup that is already running is legitimate --
            // Reclaim below can hand one back mid-flight -- so the old coroutine
            // is stopped rather than left to fight the new one for the same
            // rect.
            if (_running != null) StopCoroutine(_running);

            gameObject.SetActive(true);
            _rect.anchoredPosition = anchoredStart;

            if (label != null)
            {
                label.SetContent((isHealing ? "+" : "-") + Mathf.Abs(amount));
                label.color = isHealing ? HealColor : DamageColor;
            }

            _running = StartCoroutine(Rise(anchoredStart));
        }

        // Hands the popup back to the pool immediately, wherever it was.
        //
        // THIS IS THE FIX for a leak v1 shipped: its equivalent had zero call
        // sites, so a fight abandoned mid-playback (a scene change, a defeat, a
        // player quitting to the hub) left every in-flight popup permanently
        // un-free. The pool is six deep and never refilled, so a few abandoned
        // fights in one session and the next fight showed no numbers at all --
        // with nothing in the log to say why.
        public void Reclaim()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator Rise(Vector2 start)
        {
            float elapsed = 0f;

            while (elapsed < LifeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / LifeSeconds);

                _rect.anchoredPosition = start + new Vector2(0f, RiseDistance * t);

                if (label != null)
                {
                    var colour = label.color;
                    colour.a = AlphaAt(t);
                    label.color = colour;
                }

                yield return null;
            }

            _running = null;
            gameObject.SetActive(false);
        }

        // A pure static seam, like every other animator in this project: the
        // curve can be pinned by a test without a scene, a coroutine or a frame.
        public static float AlphaAt(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (t <= OpaqueFraction) return 1f;
            return 1f - (t - OpaqueFraction) / (1f - OpaqueFraction);
        }
    }
}
