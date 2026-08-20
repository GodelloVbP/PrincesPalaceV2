using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Item art, looked up by id.
    //
    // One array of IconEntry rather than two parallel ones. The registry binds
    // it at build time from every ItemDefinition with an authored iconPath, and
    // the wiring sweep can still see it -- see IconEntry for why the earlier
    // "arrays serialise and dictionaries do not" reasoning stopped one option
    // short of this.
    //
    // Apply's contract is the important half: an Image with no sprite renders
    // as a SOLID WHITE QUAD, not as nothing. So a missing icon disables the
    // Image rather than leaving it showing. That is one line here and a bug
    // class everywhere else.
    public static class ItemIcons
    {
        public static Sprite Find(IconEntry[] icons, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || icons == null)
            {
                return null;
            }

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i].Id == itemId) return icons[i].Sprite;
            }

            return null;
        }

        // Shows `itemId`'s art on `image`, or hides the Image when there is
        // none. Returns whether anything was actually shown.
        public static bool Apply(Image image, IconEntry[] icons, string itemId)
        {
            if (image == null)
            {
                return false;
            }

            var sprite = Find(icons, itemId);
            image.sprite = sprite;
            image.enabled = sprite != null;

            // ASPECT IS PRESERVED, and it has to be done here rather than per
            // screen. The sliced item sheets are not square -- slice_item_sheet
            // composites every level of a sheet onto one shared canvas, and
            // those come out around 260x384 -- while the slots that show them
            // are whatever shape their screen wanted. An Image stretches its
            // sprite to fill by default, so a tall dagger dropped into a square
            // cell came out squat and wrong, and the wrongness read as bad art
            // rather than as a layout property.
            //
            // Every item icon in the game goes through here, so this is the one
            // place it cannot be forgotten for a new screen.
            image.preserveAspect = true;
            return sprite != null;
        }
    }
}
