using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // A light silhouette of the actor, flashed on the frame a blow lands.
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
        //
        // Public so the numbers can be pinned without a scene, and so a pixel
        // test can work out when the white pulse is over and the typed one has
        // begun rather than guessing at a frame count.
        public const float HoldSeconds = 0.05f;
        public const float FadeSeconds = 0.16f;

        // THE HIT'S FLASH IS ONE PULSE IN THE DAMAGE TYPE'S OWN LIGHT. A
        // full-white silhouette was the loudest frame of every spell and
        // identical for all of them, which erased the element at the moment
        // it should read; an afterglow behind it was a second large flash
        // per cast (SPELL_FEEL.md, Communication and Guards). So the peak is
        // the type's palette colour lightened toward white, held for a few
        // frames, then faded fast. Every tunable of it is here.
        //
        // The peak is a LIGHT version of the palette colour, not the colour
        // itself: it has to flash on a dark sprite and on a bright one, and
        // a saturated fill over the whole silhouette is a palette swap.
        public const float FrameSeconds = 1f / 60f;
        public const int HitPeakFrames = 3;
        public const float HitHoldSeconds = HitPeakFrames * FrameSeconds;
        public const float HitFadeSeconds = 0.10f;
        public const float HitPeakAlpha = 1f;

        // How far white is pulled toward the type colour: 0 is white, 1 the
        // palette colour.
        public const float HitTintAmount = 0.5f;

        // A heal is the same mechanism at a different tempo: softer, greener,
        // and slower, because it is good news rather than an impact.
        private const float HealHoldSeconds = 0.10f;
        private const float HealFadeSeconds = 0.42f;
        private const float HealPeakAlpha = 0.75f;
        private static readonly Color HealTint = new Color(0.45f, 0.95f, 0.55f, 1f);

        // A BARRIER TAKING THE BLOW -- a third kind of news, so a third
        // tint: not the impact's white/typed pulse (nothing reached health)
        // and not the heal's green (nothing was mended). FightHudPalette.
        // WardBright, the same cyan the health bar's own ward segment
        // paints, so the flash and the segment it is reporting on cannot
        // read as two different colours for one shield.
        //
        // HOLD/FADE SIT BETWEEN THE IMPACT'S AND THE HEAL'S: a shield
        // taking a hit is not as sudden as a blow connecting (nothing
        // actually struck flesh) and not as soft as being mended, so it
        // borrows neither pair outright.
        private const float BarrierHoldSeconds = 0.08f;
        private const float BarrierFadeSeconds = 0.30f;
        private const float BarrierPeakAlpha = 0.85f;
        private static readonly Color BarrierTint = ParseOrWhite(FightHudPalette.WardBright);

        private static Color ParseOrWhite(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.white;

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

        // ONE PULSE OF THE FLASH: a colour held at an alpha, then faded out of
        // it. A flash is one of these or two, and nothing else about it varies.
        //
        // A struct rather than four parallel arrays or four parameters
        // repeated twice, because the only thing that changes between the
        // white impact and the element behind it is which set of these four
        // numbers is being run -- so the run loop reads one of these and has
        // no idea how many there are.
        public readonly struct Pulse
        {
            public readonly Color Tint;
            public readonly float Hold;
            public readonly float Fade;
            public readonly float Peak;

            public Pulse(Color tint, float hold, float fade, float peak)
            {
                Tint = tint;
                Hold = hold;
                Fade = fade;
                Peak = peak;
            }
        }

        // The two that never vary, built once. The typed pair is built per
        // call because its second pulse carries the element's own colour.
        private static readonly Pulse[] WhiteOnly =
            { new Pulse(Color.white, HoldSeconds, FadeSeconds, 1f) };

        private static readonly Pulse[] HealOnly =
            { new Pulse(HealTint, HealHoldSeconds, HealFadeSeconds, HealPeakAlpha) };

        private static readonly Pulse BarrierPulse =
            new Pulse(BarrierTint, BarrierHoldSeconds, BarrierFadeSeconds, BarrierPeakAlpha);

        private static readonly Pulse[] BarrierOnly = { BarrierPulse };

        public void Flash() => Flash(WhiteOnly);

        // THE FLASH, IN THE LIGHT OF THE ELEMENT THAT CAUSED IT -- see
        // HitTintAmount.
        public void Flash(DamageType type) => Flash(PulsesFor(type));

        public void FlashHeal() => Flash(HealOnly);

        // A SHIELD ATE THE WHOLE BLOW: the barrier is the only news, so it
        // is the only pulse -- there is no hit underneath it to open with.
        public void FlashBarrier() => Flash(BarrierOnly);

        // A SHIELD ATE PART OF THE BLOW: the ordinary (or typed) flash
        // still opens it, because the hit landed and mattered, and the
        // barrier rides in AFTER as a second pulse.
        public void FlashPartiallyWarded(DamageType type)
        {
            var hit = PulsesFor(type);
            var withBarrier = new Pulse[hit.Length + 1];
            hit.CopyTo(withBarrier, 0);
            withBarrier[hit.Length] = BarrierPulse;
            Flash(withBarrier);
        }

        // WHAT A TYPE FLASHES, AS DATA, and the pure seam the numbers are
        // pinned through: no scene, no coroutine, no frame.
        //
        // ONE PULSE FOR EVERY TYPE. A beat carries a single DamageType (the
        // declared one, else the actor's attack type -- CombatBeat.DamageType),
        // so a multi-element hit has already been resolved to its first
        // element by the time it reaches here; nothing is alternated.
        public static Pulse[] PulsesFor(DamageType type) =>
            new[] { new Pulse(TintFor(type), HitHoldSeconds, HitFadeSeconds, HitPeakAlpha) };

        // THE PEAK COLOUR: the palette the damage NUMBER already uses
        // (DamagePopup.ColorForDamageType reads the same hex), lightened
        // toward white by HitTintAmount, so the flash and the figure floating
        // off it cannot come out two different colours. Physical uses its
        // token like every other type.
        //
        // Degrades to white rather than to ColorUtility's magenta if a token
        // ever stops parsing: a hit that flashes white is the old behaviour,
        // a hit that flashes magenta is a bug report.
        public static Color TintFor(DamageType type)
        {
            if (!ColorUtility.TryParseHtmlString(FightHudPalette.ForDamageType(type), out var parsed))
                return Color.white;

            var light = Color.Lerp(Color.white, parsed, HitTintAmount);
            light.a = 1f;
            return light;
        }

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

        private void Flash(Pulse[] pulses)
        {
            if (image == null || image.sprite == null) return;
            if (pulses == null || pulses.Length == 0) return;

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
            _running = StartCoroutine(Run(pulses));
        }

        // ONE COROUTINE FOR ONE PULSE OR TWO, and one loop rather than a
        // second flash object or a nested `yield return Stage(...)` per pulse.
        //
        // THE FIRST PULSE'S PEAK IS STILL ASSIGNED ON THE FRAME StartCoroutine
        // WAS CALLED, which is the property worth protecting here and the
        // reason this is written flat: the impact frame is the one the damage
        // number, the recoil and the shake all land on, and a seam in front of
        // the first pulse is a seam in front of the only thing in this file
        // that has to be simultaneous with them. FightBeatPlayer's own header
        // records what an innocent-looking extra yield cost when it went in
        // front of every beat in the game.
        //
        // Everything after that first assignment is free to cost a frame --
        // the gap before a barrier pulse is a gap in a residue, not in an
        // impact.
        private IEnumerator Run(Pulse[] pulses)
        {
            image.enabled = true;

            foreach (var pulse in pulses)
            {
                image.color = new Color(pulse.Tint.r, pulse.Tint.g, pulse.Tint.b, pulse.Peak);

                yield return new WaitForSeconds(FightBeatPlayer.Scaled(pulse.Hold));

                float elapsed = 0f;
                float scaledFade = FightBeatPlayer.Scaled(pulse.Fade);
                while (elapsed < scaledFade)
                {
                    elapsed += Time.deltaTime;
                    image.color = new Color(pulse.Tint.r, pulse.Tint.g, pulse.Tint.b,
                                            AlphaAt(elapsed / scaledFade, pulse.Peak));
                    yield return null;
                }
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
