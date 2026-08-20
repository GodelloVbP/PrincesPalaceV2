using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // What moves in the Divine Principality.
    //
    // ZERO NEW CURVES. All six animators already exist and are now tested in
    // Domain; this file is only DATA -- where each light sits, how big, how fast.
    // That is the whole point of having moved the curves: a new place gets
    // atmosphere by declaring positions, not by writing more arithmetic.
    //
    // Positions are in the BACKGROUND'S OWN PIXEL SPACE, measured off the
    // painting, then converted once. The house convention: measured, never
    // nudged. When the forecourt painting lands these get re-measured against
    // it rather than eyeballed into place.
    public static class HubAmbience
    {
        // The current placeholder is the nebula, which is 1536x1024. When the
        // forecourt painting arrives at a different size this constant moves and
        // every position below follows it automatically -- which is the reason
        // the numbers are stored in art space rather than canvas space.
        private const float ArtWidth = 1536f;
        private const float ArtHeight = 1024f;

        public static UiVec FromArtPixel(float x, float y) => new UiVec(
            (x / ArtWidth - 0.5f) * UiFrames.Reference.X,
            (0.5f - y / ArtHeight) * UiFrames.Reference.Y);

        public readonly struct Light
        {
            public readonly float X, Y, Size;

            // Empty means "use the layer's default". The braziers and the
            // keystone still do; the buildings no longer can, because they are
            // not all lit the same colour.
            public readonly string Colour;

            public Light(float x, float y, float size, string colour = "")
            {
                X = x;
                Y = y;
                Size = size;
                Colour = colour ?? "";
            }
        }

        public readonly struct Band
        {
            public readonly float X, Y, W, H, Period;
            public Band(float x, float y, float w, float h, float period) { X = x; Y = y; W = w; H = h; Period = period; }
        }

        // Same 1.3x the measured radius the menu settled on. A glow sized to
        // look right in isolation is enormous against painted art -- at 2.6 the
        // menu's star field read as falling snow.
        public const float StarSizeScale = 1.3f;

        // The sky. Two of these are deliberate CLUSTERS rather than scattered
        // points -- the talents system is constellation-themed, and the sky is
        // where that fiction will live when the Talents screen lands.
        public static readonly Light[] Stars =
        {
            // scattered
            new Light(210, 150, 6), new Light(430, 96, 8), new Light(1290, 130, 7),
            new Light(1420, 260, 6), new Light(120, 330, 5), new Light(1500, 420, 6),
            new Light(690, 70, 5), new Light(980, 118, 7), new Light(350, 250, 5),
            new Light(1180, 60, 6), new Light(60, 200, 5), new Light(1350, 350, 5),

            // constellation A, upper left -- above the talents tree
            new Light(300, 420, 9), new Light(360, 372, 7), new Light(430, 356, 8),
            new Light(392, 300, 7), new Light(470, 288, 9),

            // constellation B, upper right
            new Light(1120, 300, 8), new Light(1190, 262, 7), new Light(1258, 296, 9),
            new Light(1210, 356, 7),
        };

        // The void below the balustrade. Confined to the lower band, where the
        // painting is already dark -- mist drifting across a lit building reads
        // as a smudge on the lens rather than as depth.
        public static readonly Band[] VoidMist =
        {
            new Band(420, 830, 900, 240, 52f),
            new Band(1080, 880, 980, 220, 67f),
            new Band(760, 940, 1200, 200, 44f),
        };

        // Embers rising past the gate. Clustered around the centre, because
        // that is where the fiction puts them: warmth coming up out of the
        // opening the player is about to step through.
        public static readonly Light[] EmberOrigins =
        {
            new Light(640, 900, 7), new Light(720, 960, 6), new Light(830, 910, 8),
            new Light(900, 970, 6), new Light(560, 950, 6), new Light(980, 930, 7),
            new Light(700, 1000, 5), new Light(870, 1010, 6), new Light(610, 870, 5),
            new Light(940, 880, 6),
        };

        // The warm light each building carries. Keyed by the building's node
        // name so the dressing step can find its target without a parallel
        // array that could fall out of order.
        public static readonly IReadOnlyDictionary<string, Light> BuildingGlows =
            new Dictionary<string, Light>
            {
                // BELOW CENTRE, and each in its own colour.
                //
                // These sat at +30..+60 -- above the middle of the building,
                // where the structure is -- while every one of these paintings
                // is lit from UNDERNEATH: the tree burns at its roots, the
                // spire and the lectern are underlit by their platforms. A glow
                // over the structure lights the one part that is already
                // opaque, and reads as haze rather than as the building
                // casting light into the void it hangs over.
                //
                // The single warm #FFCE8C was worse than the position. Four
                // buildings painted orange, violet, blue and amber all wearing
                // one gold wash fights every one of them; the wash reads as
                // fog because it agrees with nothing underneath it.
                //
                // Y and Size are in BASE units -- the caller multiplies both by
                // the plot's depth scale, so a far building gets a smaller,
                // closer-in light without a second set of numbers.
                ["PrincipalityBuilding"] = new Light(0f, -125f, 265f, "#FFC98C4D"),
                ["CharacterSheetBuilding"] = new Light(0f, -115f, 245f, "#7FB4FF4D"),
                ["TalentsBuilding"] = new Light(0f, -130f, 275f, "#FFA24B55"),
                ["RelicsBuilding"] = new Light(0f, -110f, 235f, "#9C7BFF4D"),
            };

        // The gate's own two flames, in the gate's local space rather than art
        // space -- they belong to the arch, and the arch is composited over the
        // painting rather than part of it.
        public static readonly Light[] GateBraziers =
        {
            new Light(-210f, 90f, 150f),
            new Light(210f, 90f, 150f),
        };

        // The keystone above the arch: one steady pulse rather than a flicker,
        // because it is enchantment and not fire.
        public static readonly Light GateKeystone = new Light(0f, 470f, 170f);

        public const float GoldenStride = 0.6180339887f;

        // How far an ember climbs, derived from how low it starts: one leaving
        // the bottom of the void rises a long way, one already level with the
        // terrace stops before it reaches open sky.
        public static float EmberTravel(float artY)
        {
            float travel = (artY - 700f) * 1.1f;
            return travel < 180f ? 180f : travel > 460f ? 460f : travel;
        }

        // Graded by distance, exactly as the menu's are: bloom is screen-space
        // and depth-blind, so the only way a far light reads as far is for its
        // SOURCE alpha to be lower.
        public const string BrazierColor = "#FFD79B99";     // nearest -- on the terrace
        public const string KeystoneColor = "#C9A6FF66";    // enchantment, not fire
        // The FALLBACK only. Every building names its own colour now -- see
        // BuildingGlows -- because one warm wash over four differently-lit
        // paintings agreed with none of them.
        public const string BuildingColor = "#FFCE8C55";    // out over the void
        public const string EmberColor = "#FFDCA680";
        public const string MistColor = "#8E7BD614";
        public const string StarColor = "#EDE9FF8C";        // infinity -- never blooms

        public sealed class Layer
        {
            public UiNode Root;
            public readonly List<NodeRef> Stars = new List<NodeRef>();
            public readonly List<NodeRef> Braziers = new List<NodeRef>();
            public readonly List<NodeRef> Pulses = new List<NodeRef>();
            public readonly List<NodeRef> Embers = new List<NodeRef>();
            public readonly List<NodeRef> Drifts = new List<NodeRef>();
        }

        public static Layer Build()
        {
            var layer = new Layer();
            var children = new List<UiNode>();

            const string GlowSprite = "proc:radial_glow";

            UiNode Glow(string name, float x, float y, float w, float h, string colour)
            {
                var node = Ui.Sprite(name, GlowSprite, new UiVec(w, h), Place.At(x, y)).Coloured(colour);
                children.Add(node);
                return node;
            }

            UiNode ArtGlow(string name, float artX, float artY, float w, float h, string colour)
            {
                var pos = FromArtPixel(artX, artY);
                return Glow(name, pos.X, pos.Y, w, h, colour);
            }

            const string BleedReason = "soft mist is meant to run off the frame edge, and it drifts at runtime";

            for (int i = 0; i < VoidMist.Length; i++)
            {
                var b = VoidMist[i];
                layer.Drifts.Add(ArtGlow($"VoidMist{i}", b.X, b.Y, b.W, b.H, MistColor).AllowOverflow(BleedReason));
            }

            for (int i = 0; i < Stars.Length; i++)
            {
                var s = Stars[i];
                float size = s.Size * StarSizeScale;
                layer.Stars.Add(ArtGlow($"HubStar{i}", s.X, s.Y, size, size, StarColor));
            }

            // The gate's two flames and its keystone are in GATE-LOCAL space --
            // they belong to the arch, which is composited over the painting
            // rather than part of it, so measuring them off the background would
            // pin them to the wrong thing the moment the gate moves.
            for (int i = 0; i < GateBraziers.Length; i++)
            {
                var b = GateBraziers[i];
                layer.Braziers.Add(Glow($"GateBrazier{i}",
                    HubAnchors.Gate.X + b.X, HubAnchors.Gate.Y + b.Y, b.Size, b.Size, BrazierColor));
            }

            layer.Pulses.Add(Glow("GateKeystoneGlow",
                HubAnchors.Gate.X + GateKeystone.X, HubAnchors.Gate.Y + GateKeystone.Y,
                GateKeystone.Size, GateKeystone.Size, KeystoneColor));

            // One glow per building, placed from the building's own staged
            // position so it cannot drift away from the thing it lights.
            foreach (var pair in BuildingPlots)
            {
                var plot = pair.Value;
                var at = HubAnchors.PositionFor(plot);
                var light = BuildingGlows[pair.Key];
                float size = light.Size * HubAnchors.ScaleFor(plot);

                layer.Pulses.Add(Glow($"{pair.Key}Glow",
                    at.X + light.X, at.Y + light.Y * HubAnchors.ScaleFor(plot), size, size,
                    string.IsNullOrEmpty(light.Colour) ? BuildingColor : light.Colour));
            }

            for (int i = 0; i < EmberOrigins.Length; i++)
            {
                var m = EmberOrigins[i];
                // Small, like the menu's: an ember is sold by its MOVEMENT.
                // Anything big enough to read while standing still reads as dirt
                // on the lens.
                float size = 5f + i % 4 * 2f;
                layer.Embers.Add(ArtGlow($"HubEmber{i}", m.X, m.Y, size, size, EmberColor));
            }

            // AsDecor is load-bearing: it clears raycastTarget across the whole
            // subtree -- which sits directly over four buildings and a gate --
            // and exempts it from the overlap audit, which would otherwise
            // report every glow against every thing it is lighting.
            layer.Root = Ui.Panel("HubAmbience", Place.Stretch(), UiSize.Fill, children).AsDecor();
            return layer;
        }

        // Which plot each glow belongs to. A dictionary rather than a parallel
        // array, so a glow cannot end up lighting the wrong building.
        private static readonly IReadOnlyDictionary<string, HubAnchors.Plot> BuildingPlots =
            new Dictionary<string, HubAnchors.Plot>
            {
                ["PrincipalityBuilding"] = HubAnchors.Principality,
                ["CharacterSheetBuilding"] = HubAnchors.CharacterSheet,
                ["TalentsBuilding"] = HubAnchors.Talents,
                ["RelicsBuilding"] = HubAnchors.Relics,
            };

        public static float Repeat01(float value)
        {
            float r = value - (int)value;
            return r < 0f ? r + 1f : r;
        }
    }
}
