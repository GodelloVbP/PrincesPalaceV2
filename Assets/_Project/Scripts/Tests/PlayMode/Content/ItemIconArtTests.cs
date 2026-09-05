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
    //
    // RaggedArraysDoNotThrow used to live here, holding that a `string[] ids`
    // longer than its `Sprite[] sprites` returned null rather than throwing.
    // IconEntry retired it: ids and sprites are one array now, so there is no
    // ragged state to survive. Said here rather than deleted quietly, because a
    // test disappearing usually means coverage was lost and this time it means
    // the case was.
    public class ItemIconArtTests
    {
        private Image _image;
        private Sprite _sprite;

        private static IconEntry[] Icons(params (string Id, Sprite Sprite)[] entries)
        {
            var icons = new IconEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                icons[i] = new IconEntry(entries[i].Id, entries[i].Sprite);
            }
            return icons;
        }

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
            var icons = Icons(("helm", null), ("cuirass", _sprite), ("boots", null));

            Assert.AreSame(_sprite, ItemIcons.Find(icons, "cuirass"));
        }

        [Test]
        public void AMissingIconDisablesTheImageRatherThanShowingAWhiteSquare()
        {
            // THE rule. An Image with no sprite renders as a solid white quad,
            // not as nothing -- a bug class this project has shipped more than
            // once, most recently across an entire stage of invisible actors.
            _image.enabled = true;

            bool shown = ItemIcons.Apply(_image, Icons(("helm", null)), "helm");

            Assert.IsFalse(shown);
            Assert.IsFalse(_image.enabled, "a spriteless Image must be switched off, not left blank");
        }

        [Test]
        public void AnUnknownIdAlsoDisablesTheImage()
        {
            // Not the same path as "known id, no art" -- a save can name an item
            // that content dropped entirely.
            _image.enabled = true;

            Assert.IsFalse(ItemIcons.Apply(_image, Icons(("helm", _sprite)), "ghost"));
            Assert.IsFalse(_image.enabled);
        }

        [Test]
        public void ArtThatExistsIsShown()
        {
            _image.enabled = false;

            Assert.IsTrue(ItemIcons.Apply(_image, Icons(("helm", _sprite)), "helm"));

            Assert.IsTrue(_image.enabled);
            Assert.AreSame(_sprite, _image.sprite);
        }

        [Test]
        public void ApplyingOverAPreviousIconReplacesIt()
        {
            // A bag cell is reused across pages and characters, so yesterday's
            // sprite must not survive into today's empty slot.
            ItemIcons.Apply(_image, Icons(("helm", _sprite)), "helm");
            ItemIcons.Apply(_image, Icons(("helm", _sprite)), "ghost");

            Assert.IsFalse(_image.enabled, "the stale icon was cleared, not left behind");
        }

        [Test]
        public void NullsAndBlanksAreSurvivable()
        {
            Assert.IsNull(ItemIcons.Find(null, "helm"));
            Assert.IsNull(ItemIcons.Find(Icons(("helm", _sprite)), ""));
            Assert.IsFalse(ItemIcons.Apply(null, Icons(("helm", _sprite)), "helm"));
        }

        [Test]
        public void AnEntryWithNoSpriteIsFoundAndStillDisablesTheImage()
        {
            // What is left of RaggedArraysDoNotThrow, and the case that still
            // exists: LoadSpriteByKey returns null on a miss BY DESIGN, so an
            // entry can carry an id and no art. Find returns that null rather
            // than skipping the entry, and Apply turns the Image off.
            _image.enabled = true;

            Assert.IsNull(ItemIcons.Find(Icons(("a", null), ("b", _sprite)), "a"));
            Assert.IsFalse(ItemIcons.Apply(_image, Icons(("a", null)), "a"));
            Assert.IsFalse(_image.enabled);
        }
    }
}
