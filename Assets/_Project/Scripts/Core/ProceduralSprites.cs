using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // Runtime-generated sprites, built once and cached for the process's
    // lifetime. None of these is a project asset -- nothing under
    // Resources/Content is meant to be hand-authored (see ContentBuilder's
    // own header), and these aren't gameplay content at all, just shapes a
    // flat uGUI Image cannot draw on its own.
    public static class ProceduralSprites
    {
        private const int Size = 128;
        private static Sprite _radialGlow;
        private static Sprite _solidCircle;
        private static Sprite _ringOutline;
        private static Sprite _radialGlowEmber;
        private static Sprite _softEdgeStripe;
        private static readonly Dictionary<(Color, Color), Sprite> _radialGradients = new Dictionary<(Color, Color), Sprite>();

        // Rounded rectangles are cached PER SIZE rather than made once and
        // stretched, which is the whole reason this takes width and height.
        //
        // The obvious alternative is one sprite with a 9-slice border and
        // Image.Type.Sliced, which is exactly what uGUI provides for this.
        // It is not used ON PURPOSE: Sliced rendering in this environment
        // once produced a completely invisible button on every screen in the
        // game, compiling and running with zero errors, and no working fix
        // was ever found (see SceneBuilder.GetButtonSprite and
        // ScreenshotTool's header). Everything here stays Type.Simple, and
        // a per-size texture is what keeps corners circular under Simple.
        private static readonly Dictionary<(int, int, int), Sprite> _roundedRects
            = new Dictionary<(int, int, int), Sprite>();

        // A rounded rectangle at exactly the size it will be drawn, so the
        // corner radius is honoured rather than scaled into an ellipse.
        // Tint through Image.color as usual.
        public static Sprite RoundedRect(int width, int height, int radius)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);
            // A radius past half the short side would overlap itself and
            // produce a stadium, not a rounded rect.
            radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);

            var key = (width, height, radius);
            if (_roundedRects.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var sprite = BuildRoundedRect(width, height, radius);
            _roundedRects[key] = sprite;
            return sprite;
        }

        private static Sprite BuildRoundedRect(int width, int height, int radius)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color(1f, 1f, 1f, CornerAlpha(x, y, width, height, radius));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        // Distance from the nearest corner's centre of curvature, softened
        // over one pixel so the curve does not read as a staircase.
        private static float CornerAlpha(int x, int y, int width, int height, int radius)
        {
            if (radius <= 0)
            {
                return 1f;
            }

            float px = x + 0.5f;
            float py = y + 0.5f;

            // Only the four corner squares can be outside the shape; the
            // cross through the middle is always solid.
            float cx = px < radius ? radius : px > width - radius ? width - radius : px;
            float cy = py < radius ? radius : py > height - radius ? height - radius : py;
            if (cx == px && cy == py)
            {
                return 1f;
            }

            float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        // White at the centre, fading smoothly to fully transparent at the
        // edge -- tint via Image.color to get the run map's soft glows: the
        // background atmosphere washes, and the pulsing beacon behind an
        // open room.
        public static Sprite RadialGlow()
        {
            if (_radialGlow == null)
            {
                _radialGlow = BuildSprite(dist => new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, dist)));
            }
            return _radialGlow;
        }

        // Hot yellow core cooling to ember orange at the rim, same alpha
        // falloff as RadialGlow -- unlike every other glow here, the COLOUR
        // itself carries the gradient rather than a flat tint, so the caller
        // has to leave Image.color at white (or another neutral multiply)
        // for it to show rather than wash it back to one flat hue. Talent
        // orbs only, hence the specific name rather than a parameterised
        // "two colour glow": nothing else has asked for this shape yet, and
        // a one-off is simpler than a generalization with a single caller.
        private static readonly Color EmberCore = new Color(1.0f, 0.85f, 0.25f, 1f);
        private static readonly Color EmberRim = new Color(1.0f, 0.45f, 0.05f, 1f);

        public static Sprite RadialGlowEmber()
        {
            if (_radialGlowEmber == null)
            {
                _radialGlowEmber = BuildSprite(dist =>
                {
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, dist);
                    var rgb = Color.Lerp(EmberCore, EmberRim, Mathf.Clamp01(dist));
                    return new Color(rgb.r, rgb.g, rgb.b, alpha);
                });
            }
            return _radialGlowEmber;
        }

        // A flat, fully opaque disc with a thin antialiased edge -- the
        // Embers coin icon's shape. A plain Image with no sprite assigned
        // renders as a square, which reads wrong for a coin.
        public static Sprite SolidCircle()
        {
            if (_solidCircle == null)
            {
                _solidCircle = BuildSprite(dist => new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((dist - 0.92f) / 0.08f)));
            }
            return _solidCircle;
        }

        // A thin, antialiased ring — nothing at the centre, nothing past the
        // outer edge, one soft band of full white in between. Tint via
        // Image.color; stretched into a wide, short rect by whatever draws
        // it (a combatant's ground shadow, say) to read as an ellipse rather
        // than a perfect circle, the same "one shape, non-uniform sizeDelta"
        // trick RadialGlow's own callers already use.
        private const float RingInnerRadius = 0.66f;
        private const float RingOuterRadius = 0.90f;
        private const float RingEdgeSoftness = 0.08f;

        public static Sprite RingOutline()
        {
            if (_ringOutline == null)
            {
                _ringOutline = BuildSprite(dist =>
                {
                    float innerFade = Mathf.Clamp01((dist - RingInnerRadius) / RingEdgeSoftness);
                    float outerFade = 1f - Mathf.Clamp01((dist - RingOuterRadius) / RingEdgeSoftness);
                    return new Color(1f, 1f, 1f, Mathf.Min(innerFade, outerFade));
                });
            }
            return _ringOutline;
        }

        // A two-colour radial gradient focused low in the frame (UV 0.5,
        //0.18 -- near the bottom edge but not clipped off it) rather than
        // dead centre, fading outward to `far` by the time it reaches the
        // texture's farthest corner. Talent tree background only, so far --
        // matches the design handoff's own canvas gradient (radial at
        // "50% 100%", i.e. bottom-centre) stretched non-uniformly across a
        // 1920x1080 rect, which reads as the same wide ellipse the CSS
        // radial-gradient produces at that aspect ratio. Cached per colour
        // pair like RoundedRect is per size.
        public static Sprite RadialGradientBottom(Color near, Color far)
        {
            var key = (near, far);
            if (_radialGradients.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var sprite = BuildFocusedGradient(near, far, new Vector2(0.5f, 0.18f));
            _radialGradients[key] = sprite;
            return sprite;
        }

        private static Sprite BuildFocusedGradient(Color near, Color far, Vector2 focusUv)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            // Normalised against the focus point's OWN farthest corner, so
            // a corner-shifted focus still reaches `far` exactly at the
            // texture edge in every direction rather than falling short on
            // the near side and overshooting on the far side.
            float maxDist = 0f;
            Vector2[] corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            foreach (var corner in corners)
            {
                maxDist = Mathf.Max(maxDist, Vector2.Distance(corner, focusUv));
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float t = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), focusUv) / maxDist);
                    pixels[y * size + x] = Color.Lerp(near, far, Mathf.SmoothStep(0f, 1f, t));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Solid all the way across the LONG axis, soft-fading only at the
        // top/bottom edges (falloff depends on vertical distance from the
        // centre line alone, never horizontal) -- unlike RadialGlow, this
        // stretches cleanly along its length with no visible seam or
        // squashed-oval artefact, which a radial gradient would produce.
        // Talent tree edges only, so far: a soft wide glow layer plus a
        // narrower bright core, both cut from this one sprite at different
        // sizeDelta/colour, is what gives a straight rotated line a bloomed
        // look without a real blur pass.
        public static Sprite SoftEdgeStripe()
        {
            if (_softEdgeStripe == null)
            {
                _softEdgeStripe = BuildStripeSprite(absNy => new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, absNy)));
            }
            return _softEdgeStripe;
        }

        private static Sprite BuildStripeSprite(Func<float, Color> pixelAtAbsNy)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float ny = Mathf.Abs((y + 0.5f) / Size * 2f - 1f);
                var pixel = pixelAtAbsNy(ny);
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = pixel;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildSprite(Func<float, Color> pixelAt)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float nx = (x + 0.5f) / Size * 2f - 1f;
                    float ny = (y + 0.5f) / Size * 2f - 1f;
                    pixels[y * Size + x] = pixelAt(Mathf.Sqrt(nx * nx + ny * ny));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        }
    }
}
