using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // A playable character's fight-HUD plate -- the wide leather strip with
    // their head embossed at the right end -- looked up by the Resources path
    // their content row authored.
    //
    // KEYED BY PATH, NOT BY CHARACTER ID, which is the one thing that differs
    // from CharacterPortraits beside it. The fight HUD already holds the
    // combatant's kit (PlayerKit.PlateArt, carried in from the character
    // definition at encounter time) and paints off that rather than re-reading
    // content per frame -- the same posture PlateTheme takes one field over.
    // Asking ContentDatabase again by id here would be a second answer to a
    // question the kit has already answered, and the two could disagree the
    // moment a fight is set up from a fixture rather than from the catalogue.
    //
    // CACHED, INCLUDING MISSES, for CharacterPortraits' reason: the HUD repaints
    // on every beat and a missing plate would otherwise log once per repaint.
    // A cached null means "asked, not there" until ContentDatabase.Reset drops
    // it, which is what a test swapping content calls.
    public static class PcPlateSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // The plate at `resourcePath`, or null when the kit carries no path
        // (a hand-built fixture) or names one that is not there.
        //
        // A MISSING PLATE IS NOT AN ERROR HERE even though content refuses one
        // -- ContentDatabase.ValidateContent already failed the build for a
        // real row with a bad path, so what reaches this method with nothing
        // is a fixture, and a fixture drawing no plate is correct. The warning
        // fires only for a path that was authored and did not load, which is
        // the case worth saying out loud because Resources.Load is silent
        // about it.
        public static Sprite For(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;

            string path = resourcePath.Trim();
            if (Cache.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[PcPlateSprites] plateArt '{path}' loaded nothing. Resources-relative and without an " +
                    "extension, e.g. 'Plates/pc_sheep'; the PNG must sit under Assets/_Project/Resources/ and " +
                    "import as a Sprite. `py tools/normalize_pc_plates.py` produces these.");
            }

            Cache[path] = sprite;
            return sprite;
        }

        // Dropped by ContentDatabase.Reset, beside CharacterPortraits.Reset --
        // what a path resolves to is exactly what swapping content changes.
        public static void Reset() => Cache.Clear();
    }
}
