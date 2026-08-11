using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Item art lookup, and the white-quad rule.
    //
    // "ItemIconArt" rather than "ItemIcons" so the class name matches the art
    // area's ItemArt pattern as well as content's Item -- a name matching no
    // area makes the whole suite refuse to run.
    public class ItemIconArtTests
    {
        private Image _image;
        private Sprite _sprite;

        [SetUp]
        public void MakeAnImage()
        {
            var go = new GameObject("IconHarness", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _image = go.GetComponent<Image>();

            var texture = new Texture2D(4, 4);
            _sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        [TearDown]
        public void Cleanup()
        {
            if (_image != null) Object.DestroyImmediate(_image.gameObject);
        }

        [Test]
        public void ArtIsFoundByIdAtItsOwnIndex()
        {
            var ids = new[] { "helm", "cuirass", "boots" };
            var sprites = new[] { null, _sprite, null };

            Assert.AreSame(_sprite, ItemIcons.Find(ids, sprites, "cuirass"));
        }

        [Test]
        public void AMissingIconDisablesTheImageRatherThanShowingAWhiteSquare()
        {
            // THE rule. An Image with no sprite renders as a solid white quad,
            // not as nothing -- a bug class this project has shipped more than
            // once, most recently across an entire stage of invisible actors.
            _image.enabled = true;

            bool shown = ItemIcons.Apply(_image, new[] { "helm" }, new Sprite[] { null }, "helm");

            Assert.IsFalse(shown);
            Assert.IsFalse(_image.enabled, "a spriteless Image must be switched off, not left blank");
        }

        [Test]
        public void AnUnknownIdAlsoDisablesTheImage()
        {
            // Not the same path as "known id, no art" -- a save can name an item
            // that content dropped entirely.
            _image.enabled = true;

            Assert.IsFalse(ItemIcons.Apply(_image, new[] { "helm" }, new[] { _sprite }, "ghost"));
            Assert.IsFalse(_image.enabled);
        }

        [Test]
        public void ArtThatExistsIsShown()
        {
            _image.enabled = false;

            Assert.IsTrue(ItemIcons.Apply(_image, new[] { "helm" }, new[] { _sprite }, "helm"));

            Assert.IsTrue(_image.enabled);
            Assert.AreSame(_sprite, _image.sprite);
        }

        [Test]
        public void ApplyingOverAPreviousIconReplacesIt()
        {
            // A bag cell is reused across pages and characters, so yesterday's
            // sprite must not survive into today's empty slot.
            ItemIcons.Apply(_image, new[] { "helm" }, new[] { _sprite }, "helm");
            ItemIcons.Apply(_image, new[] { "helm" }, new[] { _sprite }, "ghost");

            Assert.IsFalse(_image.enabled, "the stale icon was cleared, not left behind");
        }

        [Test]
        public void NullsAndBlanksAreSurvivable()
        {
            Assert.IsNull(ItemIcons.Find(null, null, "helm"));
            Assert.IsNull(ItemIcons.Find(new[] { "helm" }, new[] { _sprite }, ""));
            Assert.IsFalse(ItemIcons.Apply(null, new[] { "helm" }, new[] { _sprite }, "helm"));
        }

        [Test]
        public void RaggedArraysDoNotThrow()
        {
            // The arrays are bound separately at build time; a mismatch is a
            // wiring bug that must not take the screen down with it.
            Assert.IsNull(ItemIcons.Find(new[] { "a", "b", "c" }, new[] { _sprite }, "c"));
        }
    }
}
