using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // Where an art path gets resolved, which is what decides how it has to be
    // written.
    public enum ArtPathKind
    {
        // Baked into a scene at BUILD time, through AssetDatabase. Written
        // Assets-relative, and it keeps its file extension because the asset
        // database addresses actual files.
        EditorBaked,

        // Resources.Load'ed at RUNTIME. Written Resources-relative, and it must
        // NOT carry an extension -- Resources.Load takes a path without one and
        // returns null for anything else.
        RuntimeLoaded,
    }

    // The one statement of which art fields are written which way.
    //
    // TWO CONVENTIONS COEXIST IN CONTENT JSON AND THEY ARE DISTINGUISHED ONLY BY
    // FIELD NAME. Nothing about a path's text says which kind it is, and getting
    // it wrong fails SILENTLY in both directions: AssetDatabase returns null for
    // a Resources-relative path, Resources.Load returns null for an Assets/ one,
    // and the slot then renders whatever the missing-art fallback is. The art
    // simply does not appear, with nothing logged and nothing failing.
    //
    // Stating it once, here, is the point. Before this the rule lived in
    // CharacterEntryResolver as a hand-written pair of checks covering two of
    // the nine fields, so the other seven accepted either convention in any
    // field. A rule spelled per-resolver is a rule that only holds where
    // somebody remembered it.
    //
    // ADDING A FIELD: put it in the table below, and call Check from that
    // field's resolver. ArtPathConventionTests fails if a Raw*Entry gains a
    // path-shaped field that never got classified, so the table cannot silently
    // fall behind the content types.
    public static class ArtPathConvention
    {
        // Keyed by the JSON field name, because that is what a content author
        // actually types and what an error message has to name to be actionable.
        private static readonly Dictionary<string, ArtPathKind> Kinds =
            new Dictionary<string, ArtPathKind>(StringComparer.Ordinal)
            {
                // Drawn by SceneBuilder into a scene at build time.
                { "iconPath", ArtPathKind.EditorBaked },
                { "portraitPath", ArtPathKind.EditorBaked },

                // A FOLDER of per-level icons rather than one file, which is why
                // it is a "Sheet" and not a "Path" -- ItemSetEntryResolver.
                // IconPathFor appends '/level_N.png' to it. Still Assets-relative,
                // and deliberately in this table despite the name: a sweep that
                // keyed on the suffix "Path" would not see it.
                { "iconSheet", ArtPathKind.EditorBaked },

                // Loaded at runtime, off Resources.
                { "spritePath", ArtPathKind.RuntimeLoaded },
                { "battleSpritePath", ArtPathKind.RuntimeLoaded },
                // DOTTED, because the field moved inside a nested block and the
                // key is what an author types. A spell's presentation is one
                // "vfx" object now -- see SpellPresentation -- so the JSON reads
                // "vfx": { "path": ... } and an error naming "vfxPath" would
                // send someone looking for a field that is not in the file.
                { "vfx.path", ArtPathKind.RuntimeLoaded },
                { "vfx.sfxPath", ArtPathKind.RuntimeLoaded },

                // The shared ground layer and the cue that leads into it --
                // same block, same convention, and classified here on the day
                // they were added rather than on the day one of them silently
                // showed nothing. See SpellPresentation.groundPath.
                { "vfx.groundPath", ArtPathKind.RuntimeLoaded },
                { "vfx.castSfxPath", ArtPathKind.RuntimeLoaded },
            };

        // Extensions checked for explicitly rather than "contains a dot",
        // because a folder name is allowed to contain one and a false rejection
        // here would block legitimate content.
        private static readonly string[] AssetExtensions =
            { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".wav", ".mp3", ".ogg", ".asset", ".prefab" };

        public static IReadOnlyCollection<string> ClassifiedFields => Kinds.Keys;

        public static bool IsClassified(string fieldName) =>
            !string.IsNullOrEmpty(fieldName) && Kinds.ContainsKey(fieldName);

        public static bool TryKindOf(string fieldName, out ArtPathKind kind)
        {
            kind = ArtPathKind.EditorBaked;
            return !string.IsNullOrEmpty(fieldName) && Kinds.TryGetValue(fieldName, out kind);
        }

        // Checks one field's value against its convention.
        //
        // An EMPTY value always passes: art is optional throughout this project
        // and the missing-art fallbacks are deliberate, so "no icon" is a valid
        // authoring choice and only a WRONGLY-WRITTEN path is an error.
        public static bool Check(string label, string fieldName, string value, out string error)
        {
            error = null;

            if (!TryKindOf(fieldName, out var kind))
            {
                // Reachable only from a resolver naming a field the table does
                // not know -- a code mistake, not a content one. Failing closed
                // says so, rather than waving through a field nothing checks.
                error = $"{label}: '{fieldName}' is not a classified art path. Add it to " +
                        "ArtPathConvention.Kinds with the convention it follows, so the rule stays " +
                        "in one place.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value)) return true;

            string trimmed = value.Trim();
            bool looksEditorBaked = trimmed.StartsWith("Assets/", StringComparison.Ordinal);

            if (kind == ArtPathKind.EditorBaked && !looksEditorBaked)
            {
                error = $"{label}: {fieldName} '{trimmed}' must be ASSETS-relative — it is baked into the " +
                        "scene at build time by AssetDatabase, which cannot see a Resources-relative path " +
                        "and would return null, leaving the slot on its missing-art fallback. Write it as " +
                        "'Assets/_Project/Art/...'.";
                return false;
            }

            if (kind == ArtPathKind.RuntimeLoaded)
            {
                if (looksEditorBaked)
                {
                    error = $"{label}: {fieldName} '{trimmed}' must be RESOURCES-relative — it is " +
                            "Resources.Load'ed at runtime, which returns null for an Assets/ path and shows " +
                            "the fallback instead. Write it as 'Enemies/rat', not an Assets/ path.";
                    return false;
                }

                if (HasAssetExtension(trimmed))
                {
                    error = $"{label}: {fieldName} '{trimmed}' must not carry a file extension — " +
                            "Resources.Load takes the path without one and returns null with it. Drop the " +
                            "extension.";
                    return false;
                }
            }

            return true;
        }

        private static bool HasAssetExtension(string value)
        {
            for (int i = 0; i < AssetExtensions.Length; i++)
            {
                if (value.EndsWith(AssetExtensions[i], StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
