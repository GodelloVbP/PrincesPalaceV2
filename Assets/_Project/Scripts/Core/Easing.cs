using UnityEngine;

namespace PrincesPalace
{
    // Two easing curves, the one home for them in Core.
    //
    // BOTH CLAMP TO [0,1], because an unclamped smoothstep is not just
    // imprecise past its domain, it is wrong in a way a caller has to know to
    // avoid: SmoothStep(1.5f) returns -1.5, not 1 -- t*t*(3-2t) keeps falling
    // once t passes 1, it does not plateau. Clamping here makes a curve that
    // finishes a property of the function, not an obligation on every caller.
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
