using NUnit.Framework;
using UnityEngine;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The custom cursor, ported from v1 along with the controller.
    //
    // The readability test below is v1's and it is here because it caught a
    // real shipped bug: Cursor.SetCursor needs an uncompressed, CPU-readable
    // texture, and when it does not get one it logs "Invalid texture used for
    // cursor" and leaves the OS arrow in place. Nothing else notices -- the
    // game plays fine with the default pointer -- so it was found by a person
    // looking at their screen rather than by the suite.
    //
    // isReadable alone does not prove the texture is actually pixel-accessible,
    // which is why this calls GetPixel: throwing there is the same failure mode
    // SetCursor hits, so it asserts the thing rather than the flag.
    public class CursorControllerTests
    {
        private static readonly string[] Names =
        {
            "Cursors/Cursor_idle", "Cursors/Cursor_hover", "Cursors/Cursor_click",
        };

        [Test]
        public void CursorTextures_AreReadableAndCpuAccessible()
        {
            foreach (string name in Names)
            {
                var texture = Resources.Load<Texture2D>(name);

                Assert.IsNotNull(texture, $"'{name}' did not load from Resources");
                Assert.IsTrue(texture.isReadable,
                    $"'{name}' is not readable, so Cursor.SetCursor will refuse it and leave the OS arrow");
                Assert.DoesNotThrow(() => texture.GetPixel(0, 0),
                    $"'{name}' is not CPU-accessible -- the exact failure SetCursor hits");

                // SetCursor refuses a mip chain too, and this project now has a
                // postprocessor that adds one. It is scoped to Art/Items and
                // these live under Resources/Cursors, so this is the assertion
                // that says so rather than a scoping rule nobody re-checks.
                Assert.AreEqual(1, texture.mipmapCount,
                    $"'{name}' has a mip chain, which Cursor.SetCursor will not accept");
            }
        }

        [Test]
        public void CursorTextures_AllShareOneCanvas()
        {
            // The hotspot is a single constant in texture pixels, shared by all
            // three states. If they stop being the same size the pointer jumps
            // the moment it crosses a button -- and a resize is the way that
            // happens, since it is the one edit that touches all three files.
            var sizes = new System.Collections.Generic.List<Vector2Int>();
            foreach (string name in Names)
            {
                var texture = Resources.Load<Texture2D>(name);
                Assert.IsNotNull(texture, $"'{name}' did not load from Resources");
                sizes.Add(new Vector2Int(texture.width, texture.height));
            }

            Assert.AreEqual(Names.Length, sizes.Count, "the loop measured nothing");
            Assert.AreEqual(1, new System.Collections.Generic.HashSet<Vector2Int>(sizes).Count,
                $"the three cursor textures are not the same size ({string.Join(", ", sizes)}), so the " +
                "shared hotspot is wrong for at least two of them");
        }

        [Test]
        public void ClickBeatsHover_AndHoverBeatsIdle()
        {
            // Pure decision logic, which is why it is worth pinning: it is the
            // one part of the cursor that has no mouse and no EventSystem in it.
            Assert.AreEqual(CursorVisualState.Click,
                CursorController.DetermineState(isMouseDown: true, isPointerOverUi: false));
            Assert.AreEqual(CursorVisualState.Click,
                CursorController.DetermineState(isMouseDown: true, isPointerOverUi: true));
            Assert.AreEqual(CursorVisualState.Hover,
                CursorController.DetermineState(isMouseDown: false, isPointerOverUi: true));
            Assert.AreEqual(CursorVisualState.Idle,
                CursorController.DetermineState(isMouseDown: false, isPointerOverUi: false));
        }
    }
}
