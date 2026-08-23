using System;

namespace PrincesPalace.Domain.Content
{
    // WHERE A SPELL'S ART HAPPENS, as opposed to who it hits.
    //
    // These are not the same question, and treating them as one is what a bool
    // called fromCaster was doing: it could say "on the target" or "flies from
    // the caster" and there was no third thing it could say.
    //
    // TWO AXES, FLATTENED INTO ONE ENUM ON PURPOSE.
    //
    // Whose body (target or caster) and where on it (feet or middle) are
    // independent, and the first attempt at this had them as one axis -- which
    // produced a Centre value that could only ever mean "centred on the
    // TARGET", with a branch inside it asking whether the anchor was Caster
    // that no anchor could ever satisfy. Dead code, and a placement nobody
    // could author: an aura around the caster, which is the single most likely
    // thing a self-buff wants.
    //
    // Flattened rather than split into two fields because five names an author
    // picks from is plainer than two fields they have to combine correctly, and
    // because the pair is queried through OnCaster/Centred below rather than
    // switched on -- so the controller reads the two axes even though the
    // content writes one word.
    public enum SpellAnchor
    {
        // Standing on the TARGET's ground line. What almost every sheet is
        // drawn for: a flare, a bolt striking down, an impact, an eruption.
        // The default, and the one to reach for when unsure.
        Target,

        // Standing on the CASTER's ground line, whoever the skill is aimed at.
        // A gathering wind-up, a stomp, a transformation.
        Caster,

        // Centred on the TARGET's middle rather than its feet. A ring, a bind,
        // a status glint -- anything drawn around a body, which standing on the
        // floor would wrap around the ankles.
        TargetCentre,

        // Centred on the CASTER's middle. A self-buff aura, a shield bubble.
        CasterCentre,

        // Starts at the caster and flies to the target, mirrored if the caster
        // is on the right. See SpellPresentation.departFrame for the half of
        // this that says WHEN it leaves.
        Travel,
    }

    public static class SpellAnchorNames
    {
        // Case-insensitive, and an unknown word falls back to Target rather
        // than throwing. Graceful degradation is the house style, and it is
        // right here specifically: a typo in one spell's anchor should misplace
        // one effect, not stop a fight from resolving.
        //
        // The typo is still findable -- SpellVfxTests refuses any anchor in
        // content that does not parse, so it fails the suite rather than
        // reaching a player as a quietly misplaced explosion.
        public static SpellAnchor Parse(string name) => Lookup(name, SpellAnchor.Target);

        // Whether a word is one this understands. Separate from Parse because
        // Parse cannot both fall back safely AND report a typo, and the two
        // callers want opposite things: the view wants to keep going, the test
        // wants to complain.
        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellAnchor)(-1)) != (SpellAnchor)(-1);

        // ---- the two axes the flattened enum still carries -----------------------

        public static bool OnCaster(SpellAnchor anchor) =>
            anchor == SpellAnchor.Caster || anchor == SpellAnchor.CasterCentre;

        public static bool Centred(SpellAnchor anchor) =>
            anchor == SpellAnchor.TargetCentre || anchor == SpellAnchor.CasterCentre;

        // Advertised spellings, for the error message that lists them. Both
        // spellings of centre parse and only one is advertised -- refusing
        // "center" would be a rule about English rather than about spells.
        public static string[] All =>
            new[] { "target", "caster", "target-centre", "caster-centre", "travel" };

        private static SpellAnchor Lookup(string name, SpellAnchor fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellAnchor.Target;

            switch (name.Trim().ToLowerInvariant().Replace("_", "-"))
            {
                case "target": return SpellAnchor.Target;
                case "caster": return SpellAnchor.Caster;
                case "travel": return SpellAnchor.Travel;

                case "target-centre":
                case "target-center": return SpellAnchor.TargetCentre;

                case "caster-centre":
                case "caster-center": return SpellAnchor.CasterCentre;

                default: return fallback;
            }
        }
    }
}
