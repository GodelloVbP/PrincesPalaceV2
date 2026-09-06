using System.Collections.Generic;
using PrincesPalace.Content;
using UnityEngine;

namespace PrincesPalace
{
    // A character's head-and-shoulders portrait, looked up by character id and
    // loaded off Resources.
    //
    // WHY THIS IS NOT ItemIcons. Item art is baked: SceneBuilder walks every
    // ItemDefinition with an iconPath, loads each through AssetDatabase and
    // serialises the result into the scene as an IconEntry[]. Portraits were
    // baked the same way, and the cost showed up the first time somebody
    // authored a character without rebuilding the scenes: correct name, correct
    // attributes, empty portrait plate, and nothing a content rebuild could do
    // about it (docs/measurements/2026-09-step2-spell-character.md). A baked
    // array is a picture of the roster taken at scene-build time; this is the
    // roster.
    //
    // CACHED, INCLUDING MISSES. Resources.Load is a dictionary hit after the
    // first call, but the dossier asks on every character switch and a missing
    // portrait would otherwise log its warning once per switch. A cached null
    // means "asked, not there" and is the same answer forever -- until
    // ContentDatabase.Reset, which is what a test swapping content calls and
    // which drops this too.
    //
    // NO Apply(Image, ...) HERE, deliberately, unlike ItemIcons. That method's
    // contract is "hide the Image when there is no art", which is right for an
    // item cell and wrong for a portrait: the dossier's tree already puts an
    // armour stand in the slot, so hiding would replace a deliberate
    // placeholder with a hole. The caller decides what a miss means.
    public static class CharacterPortraits
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // The portrait for `characterId`, or null when the character is
        // unknown, authored no portraitPath, or named one that is not there.
        // All three are the same answer to the caller and none of them is an
        // error -- art is optional throughout this project.
        public static Sprite For(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (Cache.TryGetValue(characterId, out var cached)) return cached;

            var definition = ContentDatabase.GetCharacter(characterId);
            string path = definition != null && definition.data != null
                ? definition.data.PortraitPath
                : null;

            Sprite sprite = string.IsNullOrWhiteSpace(path)
                ? null
                : Resources.Load<Sprite>(path.Trim());

            // A PATH THAT NAMES NOTHING IS WORTH SAYING OUT LOUD, because the
            // two ways it happens are both invisible otherwise: a PNG that
            // imported as a plain Texture2D (Resources.Load<Sprite> returns
            // null, not an error -- see PortraitImportPostprocessor) and a
            // typo'd path. Authoring no portrait at all is silent, as it
            // should be.
            if (sprite == null && !string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning(
                    $"[CharacterPortraits] '{characterId}' names portraitPath '{path}', which loaded nothing. " +
                    "Resources-relative and without an extension, e.g. 'Portraits/sheep'; the file must sit " +
                    "under Assets/_Project/Resources/.");
            }

            Cache[characterId] = sprite;
            return sprite;
        }

        // Dropped by ContentDatabase.Reset, since what an id resolves to is
        // exactly what that changes.
        public static void Reset() => Cache.Clear();
    }
}
