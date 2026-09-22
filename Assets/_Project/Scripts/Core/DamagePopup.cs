using System.Collections;
using TMPro;
using UnityEngine;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

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

        // The AUTHORED constant, in engine seconds at 1x -- FightBeatPlayer
        // is the one caller that scales it (contract 9, docs/
        // PLAN_BATTLE_SPEED.md), so this stays internal rather than private:
        // same assembly, one reader outside this file, no reason to widen
        // further.
        internal const float LifeSeconds = 0.85f;

        // Fully opaque for the first third, then fades. A number that starts
        // fading immediately is hard to read at exactly the moment it matters.
        private const float OpaqueFraction = 0.35f;

        // ---- the pop -----------------------------------------------------------
        //
        // A number that simply appears at full size and drifts up reads as a
        // label. The same number punched in slightly oversized and settling
        // reads as an impact, which is what a hit is.
        //
        // Overshoot then settle, both inside the first third of the life so the
        // motion is over well before the fade starts -- two things moving at
        // once would be a wobble rather than a hit.
        private const float StartScale = 0.55f;
        private const float PeakScale = 1.22f;
        private const float PunchFraction = 0.15f;
        private const float SettleFraction = 0.32f;

        // ---- the glow ----------------------------------------------------------
        //
        // PipelineBuilder puts Bloom at threshold 1.05 and SceneBuilder puts the
        // canvas in ScreenSpaceCamera, so UI genuinely renders through post
        // -processing and a bright enough pixel blooms.
        //
        // It has to travel through the MATERIAL, not through label.color: TMP
        // packs vertex colour as Color32, so anything above 1 is clamped to 255
        // before it ever reaches the shader and the number would look merely
        // saturated. _FaceColor multiplies the vertex colour, so a white
        // multiplier above 1 scales brightness while the vertex colour keeps
        // the hue and carries the fade.
        private const float PeakGlow = 2.7f;
        private const float GlowFraction = 0.30f;

        private static readonly Color HealColor = new Color(0.42f, 0.86f, 0.45f, 1f);

        // The physical fallback if FightHudPalette's own hex ever failed to
        // parse -- kept as the literal DamagePopup always showed before
        // FightHudPalette.ForDamageType existed, so a bad hex string degrades
        // to the old flat red rather than to ColorUtility's own magenta.
        private static readonly Color PhysicalFallback = new Color(0.93f, 0.26f, 0.24f, 1f);

        // PHASE D1: a dodge's own colour -- neither the alarm-red of damage
        // nor the relief-green of a heal, because a miss is neither. Pale
        // and cool on purpose so "Miss" reads as a DIFFERENT kind of news
        // from a number, not a quiet or a loud version of the same one --
        // see FightBeatPlayer.ShowAmount's own comment on why this must
        // never look like "0 damage from armour" landed instead.
        private static readonly Color MissColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        // WHAT A SHIELD ATE, read straight off the same token the health
        // bar's own ward segment and StageHitFlash's barrier pulse both use
        // (FightHudPalette.WardText/WardBright) -- one colour language for
        // one mechanic across the bar, the flash and the number. Falls back
        // to MissColor rather than PhysicalFallback's red: an absorbed hit
        // is closer kin to "nothing landed" than to "something landed hard"
        // if the token ever fails to parse.
        private static readonly Color AbsorbedColor =
            ColorUtility.TryParseHtmlString(FightHudPalette.WardText, out var parsedWardText)
                ? parsedWardText
                : MissColor;

        [SerializeField] internal TMP_Text label;

        private RectTransform _rect;
        private Coroutine _running;

        public bool IsFree => _running == null && !gameObject.activeSelf;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        // TYPED, since a hit's DamageType decides its colour -- see
        // FightHudPalette.ForDamageType. Healing keeps its own fixed green
        // regardless of the type carried on the beat: DamageType describes an
        // ATTACK's element and a heal is never elemental, so reading it here
        // would tint a heal by whatever the healer's basic attack happens to
        // be.
        // No default on damageType any more: lifeSeconds has none either
        // (there is no honest default for a scaled duration), and a
        // parameter after one without a default cannot itself carry one.
        // FightBeatPlayer.PopNumber, the only call site, always passes both.
        public void Play(Vector2 anchoredStart, int amount, bool isHealing, DamageType damageType, float lifeSeconds) =>
            PlayContent(anchoredStart, (isHealing ? "+" : "-") + Mathf.Abs(amount),
                isHealing ? HealColor : ColorForDamageType(damageType), lifeSeconds);

        private static Color ColorForDamageType(DamageType type) =>
            ColorUtility.TryParseHtmlString(FightHudPalette.ForDamageType(type), out var parsed)
                ? parsed
                : PhysicalFallback;

        // PHASE D1: the dodge/miss reading -- "Miss" rather than a number, so
        // a dodged attack cannot be mistaken for "0 damage from armour" (see
        // DamagePipeline.Outcome.IsMiss's own header on why those two must
        // never look like the same information to the player). Shares the
        // exact same rise/punch/fade motion as a real number, deliberately:
        // the DIFFERENCE the player needs to read is the colour and the
        // word, not a second animation language to learn.
        public void PlayMiss(Vector2 anchoredStart, float lifeSeconds) => PlayContent(anchoredStart, "Miss", MissColor, lifeSeconds);

        // A SHIELD'S OWN NUMBER -- what it ate, not what reached health
        // (that is a separate Play call, at a separate anchor, when there is
        // any). Worded rather than signed ("ABSORBED 12", not "-12"): a
        // health-damage popup for the SAME hit can be on screen doing its
        // own "-n" at almost the same spot (a partial absorb shows both --
        // see FightBeatPlayer.ShowSingleAmount), and two "-" numbers one
        // above the other read as one hit that misreported its size rather
        // than as two different things that happened to it.
        public void PlayAbsorbed(Vector2 anchoredStart, int absorbed, float lifeSeconds) =>
            PlayContent(anchoredStart, "ABSORBED " + Mathf.Abs(absorbed), AbsorbedColor, lifeSeconds);

        private void PlayContent(Vector2 anchoredStart, string text, Color color, float lifeSeconds)
        {
            if (_rect == null) _rect = (RectTransform)transform;

            // Restarting on a popup that is already running is legitimate --
            // Reclaim below can hand one back mid-flight -- so the old coroutine
            // is stopped rather than left to fight the new one for the same
            // rect.
            if (_running != null) StopCoroutine(_running);

            gameObject.SetActive(true);
            _rect.anchoredPosition = anchoredStart;

            // Reset the punch explicitly. A popup handed back mid-flight by
            // Reclaim keeps whatever scale and glow it was stopped at, and a
            // pool six deep recycles fast enough for that to be visible -- the
            // next number would appear already grown and already dimmed.
            _rect.localScale = Vector3.one * ScaleAt(0f);

            if (label != null)
            {
                label.SetContent(text);
                label.color = color;
                ApplyGlow(GlowAt(0f));
            }

            _running = StartCoroutine(Rise(anchoredStart, lifeSeconds));
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

        // lifeSeconds is the SCALED life (contract 9) -- measured in plain
        // Time.deltaTime the same way it always was, so "measured in game
        // time" falls out of using the engine's own delta rather than
        // needing a second clock: a pause (Time.timeScale = 0, the system
        // menu) stops deltaTime along with it, which is exactly "a pause
        // does not count against the window" (T7).
        private IEnumerator Rise(Vector2 start, float lifeSeconds)
        {
            float elapsed = 0f;

            while (elapsed < lifeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifeSeconds);

                _rect.anchoredPosition = start + new Vector2(0f, RiseDistance * t);
                _rect.localScale = Vector3.one * ScaleAt(t);

                if (label != null)
                {
                    var colour = label.color;
                    colour.a = AlphaAt(t);
                    label.color = colour;
                    ApplyGlow(GlowAt(t));
                }

                yield return null;
            }

            _running = null;
            gameObject.SetActive(false);
        }

        // The brightness multiplier, on an INSTANCED material.
        //
        // fontMaterial hands back a per-label instance the first time it is
        // touched, which is what stops six pooled popups sharing one material
        // and strobing each other's glow. Six instances is the whole cost, paid
        // once, on a pool that is created with the scene.
        private void ApplyGlow(float multiplier)
        {
            var material = label.fontMaterial;
            if (material == null) return;

            material.SetColor(ShaderUtilities.ID_FaceColor,
                new Color(multiplier, multiplier, multiplier, 1f));
        }

        // A pure static seam, like every other animator in this project: the
        // curve can be pinned by a test without a scene, a coroutine or a frame.
        public static float AlphaAt(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (t <= OpaqueFraction) return 1f;
            return 1f - (t - OpaqueFraction) / (1f - OpaqueFraction);
        }

        // The punch. Up past full size, then back down to it, then still.
        //
        // Eased on the way out and linear on the way back: the overshoot should
        // arrive fast and hard, and the settle should not draw attention to
        // itself a second time.
        public static float ScaleAt(float progress)
        {
            float t = Mathf.Clamp01(progress);

            if (t <= PunchFraction)
            {
                float k = t / PunchFraction;
                // Ease out: most of the growth happens in the first frames, so
                // the number is legible almost immediately.
                return Mathf.Lerp(StartScale, PeakScale, 1f - (1f - k) * (1f - k));
            }

            if (t <= SettleFraction)
            {
                float k = (t - PunchFraction) / (SettleFraction - PunchFraction);
                return Mathf.Lerp(PeakScale, 1f, k);
            }

            return 1f;
        }

        // The flash. Bright enough to cross the bloom threshold on arrival,
        // decaying to 1 -- an unmultiplied face colour -- well before the fade.
        // A number that glowed for its whole life would read as a light source
        // rather than as a hit landing.
        public static float GlowAt(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (t >= GlowFraction) return 1f;

            return Mathf.Lerp(PeakGlow, 1f, t / GlowFraction);
        }
    }
}
