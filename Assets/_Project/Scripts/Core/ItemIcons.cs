using System;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Item art, looked up by id.
    //
    // Two parallel arrays rather than a dictionary, because a scene serialises
    // arrays and does not serialise dictionaries -- the registry binds these at
    // build time from every ItemDefinition with an authored iconPath, and the
    // wiring sweep can see them.
    //
    // Apply's contract is the important half: an Image with no sprite renders
    // as a SOLID WHITE QUAD, not as nothing. So a missing icon disables the
    // Image rather than leaving it showing. That is one line here and a bug
    // class everywhere else.
    public static class ItemIcons
    {
        public static Sprite Find(string[] ids, Sprite[] sprites, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || ids == null || sprites == null)
            {
                return null;
            }

            int index = Array.IndexOf(ids, itemId);
            return index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        // Shows `itemId`'s art on `image`, or hides the Image when there is
        // none. Returns whether anything was actually shown.
        public static bool Apply(Image image, string[] ids, Sprite[] sprites, string itemId)
        {
            if (image == null)
            {
                return false;
            }

            var sprite = Find(ids, sprites, itemId);
            image.sprite = sprite;
            image.enabled = sprite != null;
            return sprite != null;
        }
    }
}
