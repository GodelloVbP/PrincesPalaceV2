namespace PrincesPalace.Domain.Content
{
    // The four art paths a SpellPresentation carries, checked as one thing.
    //
    // WHY THIS EXISTS. Every resolver holding a presentation had the same four
    // lines in it -- path, sfxPath, groundPath, castSfxPath, each with its own
    // ArtPathConvention.Check and its own early return -- and a fifth path on
    // SpellPresentation would have to be remembered in each of them
    // independently, with a miss costing exactly nothing at build time and the
    // art silently not appearing at run time. That is AUDIT #60's shape one
    // level down: a field list restated per caller.
    //
    // The presentation is the thing that knows which of its fields are paths,
    // so it is the thing that gets asked. ArtPathConvention still owns WHICH
    // convention each one follows; this only owns the list.
    public static class SpellPresentationPaths
    {
        // `label` names the entry being resolved, and is what an author reads
        // to find the row -- the field names below are the JSON keys
        // ArtPathConvention classifies, so they stay exactly what an author
        // typed even when the presentation is nested inside a list.
        public static bool Check(string label, SpellPresentation vfx, out string error)
        {
            error = null;
            if (vfx == null) return true;

            return ArtPathConvention.Check(label, "vfx.path", vfx.path, out error)
                   && ArtPathConvention.Check(label, "vfx.sfxPath", vfx.sfxPath, out error)
                   && ArtPathConvention.Check(label, "vfx.groundPath", vfx.groundPath, out error)
                   && ArtPathConvention.Check(label, "vfx.castSfxPath", vfx.castSfxPath, out error);
        }
    }
}
