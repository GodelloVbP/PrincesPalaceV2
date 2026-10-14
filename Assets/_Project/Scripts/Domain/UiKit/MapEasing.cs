using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The realm eases where the party has been (docs/PLAN_RUN_MAP.md section 1,
    // P6). A second painting lies over the found wood and a mask lets it show
    // around every room walked and around the heart as anchors fall. Never
    // labelled, never a meter.
    //
    // The mask is a list of soft discs in CONTENT-LOCAL coordinates (MapLayout's
    // own: x from the centre, y negative going down), because that is the one
    // space that does not move when the camera scrolls. The shader
    // (Resources/Shaders/MapEasing.shader) evaluates the same Falloff per pixel;
    // MapEasingTests pin its literals and the property names, so the two cannot
    // drift.
    //
    // Rebuilt from the run on every repaint, never stored: clearedNodeIds and
    // slainAnchorIds are already saved, so a loaded run shows the same ease.
    public readonly struct EasingDisc
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Radius;

        public EasingDisc(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }

    public static class MapEasing
    {
        // The shader's fixed array. Every node of the largest default realm plus
        // the heart, rounded up.
        public const int ShaderCapacity = 40;

        // Walked rooms sit a row (224) apart, so discs this wide overlap into
        // one eased road with the lateral rooms eased around it.
        public const float NodeRadius = 300f;

        // The heart eases as its anchors fall: one step per anchor, up to the
        // ceiling. Zero anchors fallen leaves the heart as it was found.
        public const float HeartRadiusPerAnchor = 160f;
        public const float HeartMaxRadius = 640f;

        // Fully eased inside this share of the radius, found again at the edge.
        public const float SolidShare = 0.5f;

        public static float Falloff(float distance, float radius)
        {
            if (radius <= 0f) return 0f;

            float inner = radius * SolidShare;
            if (distance <= inner) return 1f;
            if (distance >= radius) return 0f;

            float t = (distance - inner) / (radius - inner);
            return 1f - t * t * (3f - 2f * t);
        }

        // How eased the wood is at one point: the strongest disc over it.
        public static float Coverage(IReadOnlyList<EasingDisc> discs, float x, float y)
        {
            float best = 0f;
            for (int i = 0; i < discs.Count; i++)
            {
                float dx = x - discs[i].X;
                float dy = y - discs[i].Y;
                float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                float f = Falloff(d, discs[i].Radius);
                if (f > best) best = f;
            }

            return best;
        }

        public static float HeartRadius(int slainAnchors)
        {
            if (slainAnchors <= 0) return 0f;
            float radius = slainAnchors * HeartRadiusPerAnchor;
            return radius > HeartMaxRadius ? HeartMaxRadius : radius;
        }

        // One disc per walked room, then the heart's. A realm past the shader's
        // capacity drops the surplus rather than overrun it.
        public static List<EasingDisc> Discs(IEnumerable<UiVec> walkedRooms, UiVec heart, int slainAnchors)
        {
            var discs = new List<EasingDisc>();
            if (walkedRooms != null)
            {
                foreach (var at in walkedRooms)
                {
                    if (discs.Count >= ShaderCapacity - 1) break;
                    discs.Add(new EasingDisc(at.X, at.Y, NodeRadius));
                }
            }

            float heartRadius = HeartRadius(slainAnchors);
            if (heartRadius > 0f) discs.Add(new EasingDisc(heart.X, heart.Y, heartRadius));
            return discs;
        }
    }
}
