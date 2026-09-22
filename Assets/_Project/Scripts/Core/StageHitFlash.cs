using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

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
        //
        // Public so the numbers can be pinned without a scene, and so a pixel
        // test can work out when the white pulse is over and the typed one has
        // begun rather than guessing at a frame count.
        public const float HoldSeconds = 0.05f;
        public const float FadeSeconds = 0.16f;

        // THE AFTERGLOW, and the reason the typed flash is two pulses rather
        // than one.
        //
        // Owner 2026-09-19: "flash like the white but THEN for example green
        // poison, red for fire." The first attempt read the "but" as "instead
        // of" and tinted the single pulse, which at alpha 1 over the whole
        // silhouette is not a flash at all -- it is the monster repainted
        // green for a fifth of a second. Three enemies doing it at once looked
        // like a palette swap.
        //
        // So the impact stays exactly what it was -- white, alpha 1, 0.05 hold
        // and 0.16 fade, byte-for-byte -- and the element arrives AFTER it, at
        // a little over half strength and fading twice as slowly. Softer and
        // longer is what makes the eye read it as light left behind by the
        // blow rather than as the body's own colour: a hard, brief pulse is an
        // event, a soft, slow one is a residue, and stacking them in that
        // order is the only arrangement where the element decorates the hit
        // instead of replacing it.
        public const float TypedPeakAlpha = 0.6f;
        public const float TypedHoldSeconds = 0.06f;
        public const float TypedFadeSeconds = 0.30f;

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

        // THE FLASH, WEARING THE ELEMENT THAT CAUSED IT. Owner 2026-09-19:
        // "flash like the white but THEN for example green poison, red for
        // fire." The white first, the element second -- see TypedPeakAlpha's
        // own header for why that order and those numbers.
        public void Flash(DamageType type) => Flash(PulsesFor(type));

        public void FlashHeal() => Flash(HealOnly);

        // A SHIELD ATE THE WHOLE BLOW: the barrier is the only news, so it
        // is the only pulse -- there is no hit underneath it to open with.
        public void FlashBarrier() => Flash(BarrierOnly);

        // A SHIELD ATE PART OF THE BLOW: the ordinary (or typed) flash
        // still opens it, because the hit landed and mattered, and the
        // barrier rides in AFTER as a third pulse -- the identical
        // "residue trails the impact" arrangement Flash(DamageType) already
        // uses for an element (TypedPeakAlpha's own header), extended by
        // one more pulse rather than given a second mechanism.
        public void FlashPartiallyWarded(DamageType type)
        {
            var hit = PulsesFor(type);
            var withBarrier = new Pulse[hit.Length + 1];
            hit.CopyTo(withBarrier, 0);
            withBarrier[hit.Length] = BarrierPulse;
            Flash(withBarrier);
        }

        // WHAT A TYPE FLASHES, AS DATA, and the pure seam the numbers are
        // pinned through: no scene, no coroutine, no frame, the same posture
        // AlphaAt below already takes.
        //
        // PHYSICAL IS ONE PULSE, NOT TWO OF THE SAME COLOUR. Its tint is
        // white (TintFor, below), so a second white pulse would be a second
        // flash of the identical colour -- the ordinary swing, which is most
        // of the blows in the game, flashing twice. It gets exactly the
        // single pulse it has always had.
        public static Pulse[] PulsesFor(DamageType type)
        {
            if (type == DamageType.Physical) return WhiteOnly;

            return new[]
            {
                WhiteOnly[0],
                new Pulse(TintFor(type), TypedHoldSeconds, TypedFadeSeconds, TypedPeakAlpha),
            };
        }

        // ONE HOME FOR THE COLOUR, and it is the palette the damage NUMBER
        // already uses (DamagePopup.ColorForDamageType reads the very same
        // hex) -- so the flash and the figure floating off it cannot come out
        // two different greens.
        //
        // PHYSICAL IS PURE WHITE, DELIBERATELY NOT ITS TOKEN. FightHudPalette
        // .DamageTypePhysical is #ED423D, the popup's flat alarm red, which
        // is right for a number and wrong for a silhouette: every ordinary
        // swing in the game has flashed white since the effect existed, and
        // this pass was asked to colour the OTHER elements rather than to
        // restyle the one the player sees most. The white is therefore an
        // exception with a reason rather than a missing case.
        //
        // Degrades to white rather than to ColorUtility's magenta if a token
        // ever stops parsing -- a hit that flashes white is the old
        // behaviour, a hit that flashes magenta is a bug report.
        public static Color TintFor(DamageType type)
        {
            if (type == DamageType.Physical) return Color.white;

            return ColorUtility.TryParseHtmlString(FightHudPalette.ForDamageType(type), out var parsed)
                ? parsed
                : Color.white;
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
        // the white pulse is a seam in front of the only thing in this file
        // that has to be simultaneous with them. FightBeatPlayer's own header
        // records what an innocent-looking extra yield cost when it went in
        // front of every beat in the game.
        //
        // Everything after that first assignment is free to cost a frame --
        // the gap between the white fading out and the element coming up is a
        // gap in a residue, not in an impact.
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
