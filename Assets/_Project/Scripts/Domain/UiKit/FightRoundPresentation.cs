using System;

namespace PrincesPalace.Domain.UiKit
{
    // WHAT THE FIGHT SCREEN DRESSES A FIGHT IN, beyond its class
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4, 1.6, M6): the round counter,
    // the toll's sound, the overlay that closes in round by round, the looping
    // ambience and the backdrop. A room fight carries None -- no counter, no
    // overlay, no ambience, the class's own backdrop -- which is what "hidden
    // in room fights" means here: not a flag the screen checks, but a
    // presentation with nothing in it.
    //
    // Engine-free so the three rules the screen follows are pinned on the fast
    // host: whether the counter shows, what it says, and how big the overlay
    // is at a given round. The producer is Core's EncounterRequest.Presentation,
    // off the event fight's own fields; the reader is FightController.
    //
    // Paths and keys are exactly as authored and resolved (ResolvedEventFight):
    // BackdropKey and OverlayKey are Assets-relative keys baked into the Fight
    // scene at build; RoundSfxPath and AmbiencePath are Resources paths. A key
    // or path with no file behind it hides its layer or stays silent -- the
    // screen's business, never an error.
    public sealed class FightRoundPresentation
    {
        public static readonly FightRoundPresentation None = new FightRoundPresentation();

        public string BackdropKey { get; }
        public int RoundLimit { get; }
        public string Label { get; }
        public string RoundSfxPath { get; }
        public string OverlayKey { get; }
        public float OverlayFromScale { get; }
        public float OverlayToScale { get; }
        // The point of the overlay IMAGE (fractions, from its bottom-left)
        // that is held at the ANCHOR, a point of the fight FRAME (fractions,
        // from its bottom-left), and that the overlay scales about. 0.5 each
        // is the centre-scaled full frame.
        public float OverlayPivotX { get; }
        public float OverlayPivotY { get; }
        public float OverlayAnchorX { get; }
        public float OverlayAnchorY { get; }
        // A colour token the image is multiplied by; empty is as painted.
        public string OverlayTint { get; }
        public string AmbiencePath { get; }

        public FightRoundPresentation(string backdropKey = "", int roundLimit = 0, string label = "",
            string roundSfxPath = "", string overlayKey = "", float overlayFromScale = 1f, float overlayToScale = 1f,
            string ambiencePath = "", float overlayPivotX = 0.5f, float overlayPivotY = 0.5f,
            float overlayAnchorX = 0.5f, float overlayAnchorY = 0.5f, string overlayTint = "")
        {
            BackdropKey = backdropKey ?? "";
            RoundLimit = Math.Max(0, roundLimit);
            Label = (label ?? "").Trim();
            RoundSfxPath = roundSfxPath ?? "";
            OverlayKey = overlayKey ?? "";
            OverlayFromScale = overlayFromScale;
            OverlayToScale = overlayToScale;
            OverlayPivotX = overlayPivotX;
            OverlayPivotY = overlayPivotY;
            OverlayAnchorX = overlayAnchorX;
            OverlayAnchorY = overlayAnchorY;
            OverlayTint = (overlayTint ?? "").Trim();
            AmbiencePath = ambiencePath ?? "";
        }

        // The counter exists only for a fight that can run out of rounds.
        public bool ShowsCounter => RoundLimit > 0;

        // The overlay, like the counter, is a round-limit layer: it steps
        // across the limit, so without one it has nothing to step across
        // (the content build refuses the path without a limit anyway).
        public bool HasOverlay => ShowsCounter && OverlayKey.Length > 0;

        // The authored word, or the manifest's default when none was authored.
        public string CounterLabel => Label.Length > 0 ? Label : UiStrings.FightRoundDefaultLabel.Template;

        // The number the counter shows for a round: never past the limit
        // (round limit + 1 is the round that ends the fight Survived, and it
        // never starts) and never below 1.
        public int CounterRound(int round) =>
            RoundLimit <= 0 ? 0 : Math.Min(RoundLimit, Math.Max(1, round));

        // "Toll 3". Empty for a fight without a counter.
        public string CounterText(int round) =>
            ShowsCounter ? UiStrings.FightRoundCounter.Format(CounterLabel, CounterRound(round)) : "";

        // THE OVERLAY'S SCALE AT A ROUND: fromScale at round 1, toScale at
        // the last round of the limit, in equal steps between -- one step per
        // toll, so with a limit of 10 the flock closes in nine times after the
        // first toll shows it. Clamped to the ends outside 1..limit. A limit
        // of 1 has no steps and shows toScale, the only round there is.
        public float OverlayScaleFor(int round)
        {
            if (RoundLimit <= 1) return OverlayToScale;

            int clamped = CounterRound(round);
            float t = (clamped - 1) / (float)(RoundLimit - 1);
            return OverlayFromScale + (OverlayToScale - OverlayFromScale) * t;
        }

        // WHERE THE OVERLAY STANDS in a frame of frameWidth x frameHeight
        // (the overlay's own stretched rect) when its image, of aspect
        // imageAspect (width / height), is fitted inside it keeping that
        // aspect and centred -- uGUI's PreserveAspect.
        //
        // Returns the rect's pivot (fractions of the FRAME, which is what
        // RectTransform.pivot takes) at the image's own pivot point, and the
        // offset in frame units that carries that point onto the anchor.
        // Scaling about the rect's pivot then leaves the point on the anchor
        // at every round. The defaults (0.5 everywhere) give pivot (0.5, 0.5)
        // and offset (0, 0): the centre-scaled full frame, unchanged.
        public OverlayPlacement PlaceOverlay(float frameWidth, float frameHeight, float imageAspect)
        {
            if (frameWidth <= 0f || frameHeight <= 0f || imageAspect <= 0f)
            {
                return new OverlayPlacement(0.5f, 0.5f, 0f, 0f);
            }

            float drawnWidth = Math.Min(frameWidth, frameHeight * imageAspect);
            float drawnHeight = drawnWidth / imageAspect;
            float pointX = (frameWidth - drawnWidth) * 0.5f + OverlayPivotX * drawnWidth;
            float pointY = (frameHeight - drawnHeight) * 0.5f + OverlayPivotY * drawnHeight;

            return new OverlayPlacement(
                pointX / frameWidth, pointY / frameHeight,
                OverlayAnchorX * frameWidth - pointX, OverlayAnchorY * frameHeight - pointY);
        }
    }

    // A RectTransform pivot (frame fractions) and anchored offset (frame
    // units), engine-free so PlaceOverlay is pinned on the fast host.
    public readonly struct OverlayPlacement
    {
        public readonly float PivotX;
        public readonly float PivotY;
        public readonly float OffsetX;
        public readonly float OffsetY;

        public OverlayPlacement(float pivotX, float pivotY, float offsetX, float offsetY)
        {
            PivotX = pivotX;
            PivotY = pivotY;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }
    }
}
