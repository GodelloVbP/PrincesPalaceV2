using System;
using UnityEngine;

namespace PrincesPalace
{
    // Runtime-generated sprites, built once and cached for the process's
    // lifetime. None of these is a project asset -- nothing under
    // Resources/Content is meant to be hand-authored (see ContentBuilder's
    // own header), and these aren't gameplay content at all, just shapes a
    // flat uGUI Image cannot draw on its own.
    //
    // This class used to hold seven of these; six were the runtime half of a
    // migration to Editor/ProceduralSpriteBaker.cs (build-time PNGs, see that
    // file's own header for why) and never got deleted once every caller had
    // moved to the baked asset. RadialGlow is the one shape nothing baked:
    // RadialGlowImage assigns it at runtime because it is tinted per-instance
    // rather than committed as one fixed-colour PNG.
    public static class ProceduralSprites
    {
        private const int Size = 128;
        private static Sprite _radialGlow;

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
