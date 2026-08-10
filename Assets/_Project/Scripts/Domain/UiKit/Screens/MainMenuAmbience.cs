using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The main menu's ambient light layer, declared.
    //
    // Every position below was MEASURED FROM THE PAINTING, not invented: the
    // warm lights by scanning Main_menu.png for bright, warm-hued local maxima,
    // the stars by rejecting every sample that was not open dark sky. They are
    // in the art's own 1672x941 pixel space because they are measurements of a
    // specific image -- if it is ever repainted they must be re-measured, not
    // nudged. FromArtPixel is the one place the two spaces meet.
    //
    // This lives in Domain so the whole ambient layer is auditable from an
    // EditMode test. The COMPONENTS that animate it are Core types Domain
    // cannot name, so the screen exposes typed handles and the Editor module
    // attaches them -- the bounded "dressing" escape hatch, used exactly once.
    public static class MainMenuAmbience
    {
        private const float ArtWidth = 1672f;
        private const float ArtHeight = 941f;

        // Art pixel (0,0) is top-left; the canvas is centre-origin with +y up.
        public static UiVec FromArtPixel(float x, float y) => new UiVec(
            (x / ArtWidth - 0.5f) * 1920f,
            (0.5f - y / ArtHeight) * 1080f);

        public readonly struct Light
        {
            public readonly float X, Y, Size;
            public Light(float x, float y, float size) { X = x; Y = y; Size = size; }
        }

        // 1.3x the measured radius, not the 2.6x this started at. RadialGlow's
        // falloff means the visible core is roughly a third of the sprite, so a
        // glow sized to look right in isolation is enormous against painted art
        // -- at 2.6 the whole field read as falling snow. That is the sort of
        // thing only a composite against the real image will tell you.
        public const float StarSizeScale = 1.3f;

        public static readonly Light[] Stars =
        {
            new Light(374, 209, 5), new Light(436, 120, 8), new Light(919, 113, 9),
            new Light(508, 176, 5), new Light(549, 276, 7), new Light(729, 229, 7),
            new Light(332, 123, 7), new Light(859, 139, 10), new Light(1274, 125, 5),
            new Light(257, 57, 5), new Light(638, 50, 10), new Light(571, 99, 7),
            new Light(636, 108, 5), new Light(317, 224, 7), new Light(1025, 130, 8),
            new Light(217, 127, 7), new Light(581, 181, 8), new Light(241, 269, 7),
            new Light(273, 103, 7), new Light(479, 31, 10), new Light(740, 53, 7),
            new Light(445, 211, 7), new Light(1198, 69, 8), new Light(1110, 39, 10),
            new Light(443, 74, 10), new Light(713, 175, 10), new Light(785, 258, 10),
            new Light(997, 86, 5), new Light(476, 278, 7), new Light(1289, 42, 5),
            new Light(850, 81, 9), new Light(360, 33, 5), new Light(961, 219, 5),
            new Light(1318, 111, 8), new Light(330, 293, 10), new Light(683, 68, 7),
            new Light(833, 245, 6), new Light(798, 23, 7), new Light(762, 130, 5),
            new Light(1041, 67, 8), new Light(955, 164, 9), new Light(652, 157, 5),
            new Light(575, 234, 7), new Light(993, 20, 6), new Light(536, 43, 5),
        };

        // Lit windows across the palace face and its lower terraces.
        public static readonly Light[] PalaceWindows =
        {
            new Light(790, 398, 96), new Light(858, 442, 96), new Light(864, 340, 96),
            new Light(872, 388, 96), new Light(932, 376, 96), new Light(974, 416, 96),
            new Light(988, 240, 96), new Light(1004, 326, 96), new Light(1004, 508, 96),
            new Light(1038, 418, 96), new Light(1072, 224, 96), new Light(1076, 358, 96),
            new Light(1090, 160, 96), new Light(1118, 108, 96), new Light(1188, 240, 96),
            new Light(1200, 104, 96),
        };

        // Hanging lanterns down the right-hand colonnade: the nearest and
        // sharpest practical lights, so the ones the eye actually lands on.
        public static readonly Light[] ColonnadeLanterns =
        {
            new Light(1428, 122, 120), new Light(1390, 404, 120), new Light(1466, 402, 120),
            new Light(1416, 232, 120), new Light(1432, 322, 120), new Light(1502, 516, 120),
            new Light(1600, 512, 120), new Light(1394, 634, 120),
        };

        // The two post lanterns on the terrace the camera stands on -- closest
        // to the viewer, so bigger and warmer than the colonnade's.
        public static readonly Light[] TerraceLanterns =
        {
            new Light(1284, 762, 210), new Light(276, 708, 210),
        };

        // The gazebo is lit lavender in the painting, not gold, and it breathes
        // rather than gutters -- it is an interior seen through arches from a
        // long way off, not an open flame.
        public static readonly Light[] GazeboLights =
        {
            new Light(304, 476, 150), new Light(300, 572, 150),
        };

        public readonly struct Band
        {
            public readonly float X, Y, W, H, Period;
            public Band(float x, float y, float w, float h, float period) { X = x; Y = y; W = w; H = h; Period = period; }
        }

        // Confined to the band that is ALREADY fog in the painting. An overlay
        // on a flat background has nothing to go behind, so anything drifting
        // across the palace would read as a smudge on the lens.
        public static readonly Band[] MistBanks =
        {
            new Band(520, 660, 940, 260, 46f),
            new Band(1080, 620, 1000, 280, 61f),
            new Band(830, 730, 1240, 220, 53f),
        };

        public static readonly Band[] NebulaHaze =
        {
            new Band(430, 150, 700, 380, 88f),
            new Band(700, 90, 560, 300, 71f),
        };

        // Embers rising off the water, placed with a minimum separation and kept
        // clear of the button column so the field never clumps into something
        // that reads as an object.
        public static readonly Light[] MoteOrigins =
        {
            new Light(1005, 773, 0), new Light(1111, 589, 0), new Light(645, 831, 0),
            new Light(900, 722, 0), new Light(1296, 619, 0), new Light(434, 522, 0),
            new Light(592, 519, 0), new Light(954, 848, 0), new Light(1537, 658, 0),
            new Light(678, 708, 0), new Light(1391, 708, 0), new Light(471, 640, 0),
            new Light(250, 758, 0), new Light(145, 690, 0), new Light(448, 732, 0),
            new Light(1252, 801, 0), new Light(766, 711, 0), new Light(1236, 714, 0),
            new Light(1155, 812, 0), new Light(1395, 607, 0), new Light(1015, 675, 0),
            new Light(216, 529, 0), new Light(329, 583, 0), new Light(418, 833, 0),
            new Light(146, 807, 0), new Light(563, 718, 0),
        };

        // Graded by DISTANCE, not by taste.
        //
        // Bloom is a screen-space effect: it keys off pixel brightness and has
        // no idea how far away anything is, so a tower on the horizon blooms
        // exactly as hard as a lantern at the player's feet. That reads wrong,
        // because real distance does two things to a light -- atmosphere scatters
        // it dimmer, and it subtends a smaller angle.
        //
        // The fix is upstream of the effect: give distant lights less to bloom
        // WITH. Alphas below descend with depth, so the post stack produces
        // aerial perspective for free instead of flattening it.
        //
        //   terrace lanterns   nearest, on the balustrade the camera stands on
        //   colonnade lanterns near-mid, the right-hand pillars
        //   gazebo             mid, across the water
        //   palace windows     far, on the floating island
        //   stars              effectively at infinity
        public const string TerraceColor = "#FFD79B99";   // nearest - brightest
        public const string LanternColor = "#FFCE8C66";
        public const string GazeboColor = "#B9A6FF33";
        public const string WindowColor = "#FFC27A1F";    // far - barely blooms
        public const string StarColor = "#EDE9FF8C";      // infinity - never blooms

        public const string MoteColor = "#FFDCA680";
        public const string MistColor = "#B9AEE812";
        public const string NebulaColor = "#8E7BD60F";

        // An irrational stride: N items spread evenly over a cycle whatever N
        // is, and the same N always lands the same way. Random would look fine
        // but would make every rebuild produce a different scene, which turns
        // every real diff into noise.
        public const float GoldenStride = 0.6180339887f;

        // Travel derived from how low a mote starts, so one beginning on the
        // water rises a long way while one level with the islands stops before
        // it climbs into open sky, where a rising ember has no business being.
        public static float MoteTravel(float artY)
        {
            float travel = (artY - 380f) * 0.9f;
            return travel < 160f ? 160f : travel > 430f ? 430f : travel;
        }

        public static float Repeat01(float value)
        {
            float r = value - (int)value;
            return r < 0f ? r + 1f : r;
        }

        // Builds the whole layer. Returns the nodes AND, in the same order, the
        // handles the Editor module attaches components to -- one declaration,
        // so the two can never disagree about how many there are.
        public sealed class Layer
        {
            public UiNode Root;
            public readonly List<NodeRef> Stars = new List<NodeRef>();
            public readonly List<NodeRef> Windows = new List<NodeRef>();
            public readonly List<NodeRef> Lanterns = new List<NodeRef>();
            public readonly List<NodeRef> GazeboGlows = new List<NodeRef>();
            public readonly List<NodeRef> Motes = new List<NodeRef>();
            public readonly List<NodeRef> Drifts = new List<NodeRef>();

            // Every glow, for the one component they all share.
            public readonly List<NodeRef> AllGlows = new List<NodeRef>();
        }

        public static Layer Build()
        {
            var layer = new Layer();
            var children = new List<UiNode>();

            // A committed PNG, not a runtime-generated sprite. One asset, one
            // GUID, tinted per use through Image.color - see
            // ProceduralSpriteBaker for why the runtime route serialised a
            // texture into the scene.
            const string GlowSprite = "proc:radial_glow";

            UiNode Glow(string name, float artX, float artY, float w, float h, string colour)
            {
                var pos = FromArtPixel(artX, artY);
                var node = Ui.Sprite(name, GlowSprite, new UiVec(w, h), Place.At(pos.X, pos.Y)).Coloured(colour);
                children.Add(node);
                layer.AllGlows.Add(node);
                return node;
            }

            // The haze and mist bands are soft-edged and SlowDrift moves them at
            // runtime, so running past the frame edge is the intended look
            // rather than a layout mistake - stated here rather than by
            // shrinking them to fit and losing the effect.
            const string BleedReason = "soft haze is meant to run off the frame edge, and it drifts at runtime";

            for (int i = 0; i < NebulaHaze.Length; i++)
            {
                var b = NebulaHaze[i];
                layer.Drifts.Add(Glow($"NebulaHaze{i}", b.X, b.Y, b.W, b.H, NebulaColor).AllowOverflow(BleedReason));
            }

            for (int i = 0; i < MistBanks.Length; i++)
            {
                var b = MistBanks[i];
                layer.Drifts.Add(Glow($"MistBank{i}", b.X, b.Y, b.W, b.H, MistColor).AllowOverflow(BleedReason));
            }

            for (int i = 0; i < Stars.Length; i++)
            {
                var s = Stars[i];
                float size = s.Size * StarSizeScale;
                layer.Stars.Add(Glow($"Star{i}", s.X, s.Y, size, size, StarColor));
            }

            for (int i = 0; i < PalaceWindows.Length; i++)
            {
                var w = PalaceWindows[i];
                layer.Windows.Add(Glow($"WindowGlow{i}", w.X, w.Y, w.Size, w.Size, WindowColor));
            }

            for (int i = 0; i < ColonnadeLanterns.Length; i++)
            {
                var l = ColonnadeLanterns[i];
                layer.Lanterns.Add(Glow($"Lantern{i}", l.X, l.Y, l.Size, l.Size, LanternColor));
            }

            for (int i = 0; i < TerraceLanterns.Length; i++)
            {
                var l = TerraceLanterns[i];
                layer.Lanterns.Add(Glow($"TerraceLantern{i}", l.X, l.Y, l.Size, l.Size, TerraceColor));
            }

            for (int i = 0; i < GazeboLights.Length; i++)
            {
                var g = GazeboLights[i];
                layer.GazeboGlows.Add(Glow($"GazeboGlow{i}", g.X, g.Y, g.Size, g.Size, GazeboColor));
            }

            for (int i = 0; i < MoteOrigins.Length; i++)
            {
                var m = MoteOrigins[i];
                // Small for the same reason the stars are: a mote is sold by its
                // MOVEMENT. Anything big enough to read as a dot while standing
                // still reads as dirt on the lens.
                float size = 5f + i % 4 * 2f;
                layer.Motes.Add(Glow($"Mote{i}", m.X, m.Y, size, size, MoteColor));
            }

            // AsDecor is load-bearing, not cosmetic: it clears raycastTarget
            // across the whole subtree (110 sprites covering the button column)
            // and exempts it from the overlap audit, which would otherwise
            // report thousands of collisions that all mean "yes, that is the
            // effect".
            layer.Root = Ui.Panel("Ambience", Place.Stretch(), UiSize.Fill, children).AsDecor();
            return layer;
        }
    }
}
