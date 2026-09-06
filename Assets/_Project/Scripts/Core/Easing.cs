using UnityEngine;

namespace PrincesPalace
{
    // Two easing curves, written out four times across Core before this --
    // the hub's zoom, the reward track's pulses and its fly-ins, and the
    // reckoning's sweep -- each its own private static, and not quite the
    // same contract twice: one of the four clamped its input, the other
    // three did not.
    //
    // BOTH CLAMP TO [0,1] HERE, because an unclamped smoothstep is not just
    // imprecise past its domain, it is wrong in a way a caller has to know to
    // avoid: SmoothStep(1.5f) returns -1.5, not 1 -- t*t*(3-2t) keeps falling
    // once t passes 1, it does not plateau. The four copies got away with
    // that only because every caller happened to pre-normalise (a
    // Mathf.Clamp01 at the call site, or a value already proven to sit in
    // [0,1] upstream); a fifth caller that forgot to would have gotten a
    // curve that reverses instead of a curve that finishes. Clamping here
    // makes that a property of the function, not an obligation on every
    // caller who reaches for it next.
    //
    // TWO COPIES ELSEWHERE ARE LEFT ALONE, on purpose:
    // - Domain/UiKit/ConstellationLayout.cs has its own smoothstep because
    //   Domain is engine-free and cannot reach Mathf; its own comment says
    //   so, and folding it in here would put a Mathf.Clamp01 call in a file
    //   that exists specifically to have none.
    // - Core/TalentController.Motion.cs has a quadratic ease-out,
    //   1-(1-t)^2, which is a genuinely different curve from OutCubic below,
    //   not a copy of it.
    public static class Easing
    {
        // Smoothstep: t*t*(3-2t). Eases in and out, symmetric about t=0.5.
        public static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Cubic out: 1-(1-t)^3. Leaves at full speed and settles.
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
    }
}
