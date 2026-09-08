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

            if (!ArtPathConvention.Check(label, "vfx.path", vfx.path, out error)
                || !ArtPathConvention.Check(label, "vfx.sfxPath", vfx.sfxPath, out error)
                || !ArtPathConvention.Check(label, "vfx.groundPath", vfx.groundPath, out error)
                || !ArtPathConvention.Check(label, "vfx.castSfxPath", vfx.castSfxPath, out error))
            {
                return false;
            }

            // THE SAME TWO CONVENTIONS, ONE LEVEL FURTHER IN. A layer's own
            // path and its emitter's are Resources-relative like every other
            // runtime path, and they belong on this list for the reason the
            // header gives: the presentation is what knows which of its fields
            // are paths, so a fifth (and sixth) added anywhere else would be
            // forgotten per caller.
            //
            // The first bad path stops the walk, keeping the short-circuit
            // shape the four checks above already had -- one error per entry is
            // the resolver's contract, not this type's decision to change.
            var layers = vfx.layers;
            if (layers == null) return true;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null) continue;

                if (!ArtPathConvention.Check(label, "vfx.layers[].path", layer.path, out error)) return false;

                string emitterPath = layer.emitter == null ? "" : layer.emitter.path;
                if (!ArtPathConvention.Check(label, "vfx.layers[].emitter.path", emitterPath, out error))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
