using System;

namespace PrincesPalace.Domain.Content
{
    // THE EIGHT WORDS A LAYER IS AUTHORED IN, each as a closed enum with a
    // Parse/IsKnown pair beside it.
    //
    // The shape is SpellAnchorNames' exactly (SpellAnchor.cs), and it is copied
    // deliberately rather than generalised: a Parse that falls back safely
    // cannot also report a typo, so the two callers -- the view, which wants to
    // keep going, and the validator, which wants to complain -- get one function
    // each. Every table below is `Lookup(name, fallback)` plus two readers of
    // it, so a word added to the switch is answered by both without either
    // being edited.
    //
    // A BLANK WORD IS KNOWN, and that is the documented default rather than an
    // oversight: an author who omits `sort` means "effects", and refusing an
    // omission would make every optional word mandatory. SpellLayerRules is
    // where blank `render` and blank `place` are refused instead, because
    // those two have no safe default -- a misplaced effect is still an effect,
    // and a layer that does not say what it draws is nothing at all.
    //
    // REAL ENUMS RATHER THAN BARE STRINGS because ContentSchema.EnumBackedFields
    // documents a field by calling Enum.GetNames on the type behind it
    // (ContentSchema.cs), so a word with no enum member has no representation in
    // docs/CONTENT_SCHEMA.md. `place`'s open `layer:<id>` form is the one thing
    // that cannot be an enum member and lives in the field's [ContentDoc]
    // instead.


    // ---- reading an authored word without allocating -------------------------

    // WHETHER AN AUTHORED WORD IS THIS ONE, character by character rather than
    // by normalising a copy of it.
    //
    // The tables below used to switch on
    // `name.Trim().ToLowerInvariant().Replace("_", "-")`, which is correct and
    // costs a STRING PER CALL -- ToLowerInvariant allocates whether or not the
    // word was already lower case. That is invisible at content-build time and
    // is not invisible in the tick path: SpellLayer.Render, .Sort, .Until and
    // .Facing are computed properties, the renderer reads several per layer per
    // frame, and a three-plume cinderfault was handing the collector a dozen
    // strings every frame it drew. Measured, not guessed --
    // SpellAllocationTests failed on the module's tick until this, and the
    // failure bisected to exactly here.
    //
    // The comparison is the same one: leading and trailing whitespace ignored,
    // case ignored, and an underscore reading as a hyphen so `caster_centre`
    // and `caster-centre` are one word. Every `word` passed in is already
    // lower case with hyphens, which is what lets the loop compare against it
    // directly.
    internal static class SpellWord
    {
        internal static bool Is(string authored, string word)
        {
            if (authored == null || word == null) return false;

            int start = 0;
            int end = authored.Length;
            while (start < end && char.IsWhiteSpace(authored[start])) start++;
            while (end > start && char.IsWhiteSpace(authored[end - 1])) end--;

            if (end - start != word.Length) return false;

            for (int i = 0; i < word.Length; i++)
            {
                char c = authored[start + i];
                if (c == '_') c = '-';
                if (char.ToLowerInvariant(c) != word[i]) return false;
            }

            return true;
        }
    }

    // ---- what draws ----------------------------------------------------------

    public enum SpellRender
    {
        // An animated folder, played at `fps` or fitted into `seconds`. What
        // every spell in the game is today.
        Sprite,

        // ONE frame of a folder -- the folder's `startFrame` -- held rather
        // than played. A wake, a ribbon, a glow that does not animate.
        //
        // A FOLDER AND NOT A SPRITE FILE, which is mechanical rather than
        // tidy: SpellVfxRecipeDriftTests matches a played path's LAST SEGMENT
        // against recipe filenames and requires it to be a directory on disk,
        // so `..._wake/f0` would present `f0` as its provenance id, find no
        // recipe of that name, and fail both halves. One loader
        // (FrameSequenceLoader) and one path convention for both kinds.
        Still,

        // Ballistic particles. The one kind whose end is derived rather than
        // authored -- see SpellEmitter -- because the brief requires emission
        // to stop while already-emitted particles finish.
        Emitter,
    }

    public static class SpellRenderNames
    {
        public static SpellRender Parse(string name) => Lookup(name, SpellRender.Sprite);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellRender)(-1)) != (SpellRender)(-1);

        public static string[] All => new[] { "sprite", "still", "emitter" };

        private static SpellRender Lookup(string name, SpellRender fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellRender.Sprite;

            if (SpellWord.Is(name, "sprite")) return SpellRender.Sprite;
            if (SpellWord.Is(name, "still")) return SpellRender.Still;
            if (SpellWord.Is(name, "emitter")) return SpellRender.Emitter;
            return fallback;
        }
    }

    // ---- where it belongs ----------------------------------------------------

    public enum SpellPlace
    {
        // Standing on the caster's ground line. ONE INSTANCE per cast, however
        // many targets were struck -- unless the layer travels, which is the
        // single exception in this table and is a property of travelSeconds
        // rather than of any word here.
        Caster,

        // Centred on the caster's middle. Cast-level, same as Caster.
        CasterCentre,

        // Standing on a struck target's ground line. ONE INSTANCE PER STRUCK
        // TARGET, which is what makes a per-target fan-out a placement fact
        // rather than a spell id somebody branched on.
        Target,

        // Centred on a struck target's middle. Target-level.
        TargetCentre,

        // THE MEASURED SPAN of every struck slot, and its average ground line.
        // Cast-level: one fault however many enemies stand in it.
        //
        // IGNORES `size` AND `dx`, which is why SpellLayerRules refuses both on
        // it rather than quietly overriding them. A fault authored wide enough
        // for three reaches half the stage past a lone rat, so the width is
        // MEASURED off the slots; authoring a second copy of it would be a
        // number with two homes.
        Formation,

        // Riding another layer, named as `layer:<id>`. INHERITS THE SCOPE of
        // the layer it names -- an emitter on a per-target projectile is
        // per-target, and would be cast-level if the projectile were -- so
        // "does this repeat per enemy" is answered by the thing being followed
        // rather than by a rule somebody has to remember.
        Layer,

        // IN THE AIR BETWEEN THE CASTER AND ONE STRUCK TARGET: horizontally
        // halfway from the caster's cast point to the target's visible body,
        // raised above the taller of the two (Domain.Stage.SpellFlight.SkyPoint). The
        // place a called strike amasses before it is fired, which no other
        // word could say -- every one of them is ON a body, so Winter's Rebuke
        // formed at the rat's feet (owner, 2026-09-23).
        //
        // PER TARGET, because the midpoint is a function of which target: a
        // volley at three enemies amasses three times, each over its own line
        // of fire. With travelSeconds it is the projectile's ORIGIN and the
        // layer flies to the target's middle -- the same "a travelling layer
        // leaves its own place and arrives on the target" rule the caster
        // words follow.
        Sky,
    }

    public static class SpellPlaceNames
    {
        // The prefix that turns a placement into a reference. One string,
        // read by Parse and by LayerReference, so the two cannot disagree
        // about where the id starts.
        public const string LayerPrefix = "layer:";

        public static SpellPlace Parse(string name) => Lookup(name, SpellPlace.Target);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) ||
            !string.IsNullOrWhiteSpace(LayerReference(name)) ||
            Lookup(name, (SpellPlace)(-1)) != (SpellPlace)(-1);

        // The id after `layer:`, or empty for any other placement. Empty for
        // a bare `layer:` too, which is a reference to nothing and is refused
        // by the declared-reference rule rather than parsed into a blank id.
        public static string LayerReference(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            string trimmed = name.Trim();
            if (!trimmed.StartsWith(LayerPrefix, StringComparison.OrdinalIgnoreCase)) return "";

            return trimmed.Substring(LayerPrefix.Length).Trim();
        }

        // Whether the word puts the layer on the CASTER's side of the stage,
        // which is the only place a projectile may leave from. Asked rather
        // than compared, so the two caster values cannot drift apart at the
        // several sites that branch on this.
        public static bool OnCaster(SpellPlace place) =>
            place == SpellPlace.Caster || place == SpellPlace.CasterCentre;

        // Whether the word draws one instance per struck target. `Layer` is
        // absent on purpose: it inherits its source's scope and answering here
        // would be a second, disagreeing home for that rule.
        public static bool PerTarget(SpellPlace place) =>
            place == SpellPlace.Target || place == SpellPlace.TargetCentre || place == SpellPlace.Sky;

        // Whether a travelling layer on this word has somewhere to LEAVE from:
        // the caster's own body, or the air it amassed in. Every other word is
        // already on the target, and a flight from the target to the target is
        // no flight.
        public static bool CanLaunch(SpellPlace place) => OnCaster(place) || place == SpellPlace.Sky;

        // `sky` IS CENTRED: a strike called down out of the air arrives in the
        // target's middle, not at its feet, and a non-travelling sky layer is
        // drawn centred on its own point because there is no floor up there
        // for a standing rule to measure against.
        public static bool Centred(SpellPlace place) =>
            place == SpellPlace.CasterCentre || place == SpellPlace.TargetCentre || place == SpellPlace.Sky;

        public static string[] All =>
            new[] { "caster", "caster-centre", "target", "target-centre", "formation", "sky", "layer:<id>" };

        private static SpellPlace Lookup(string name, SpellPlace fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellPlace.Target;
            if (!string.IsNullOrWhiteSpace(LayerReference(name))) return SpellPlace.Layer;

            if (SpellWord.Is(name, "caster")) return SpellPlace.Caster;

            if (SpellWord.Is(name, "caster-centre") ||
                SpellWord.Is(name, "caster-center")) return SpellPlace.CasterCentre;

            if (SpellWord.Is(name, "target")) return SpellPlace.Target;

            if (SpellWord.Is(name, "target-centre") ||
                SpellWord.Is(name, "target-center")) return SpellPlace.TargetCentre;

            if (SpellWord.Is(name, "formation")) return SpellPlace.Formation;

            if (SpellWord.Is(name, "sky")) return SpellPlace.Sky;

            return fallback;
        }
    }

    // ---- when it runs --------------------------------------------------------

    public enum SpellCue
    {
        // The beat's PlayVfx instant. The default, and what every adapted
        // pre-layer block schedules on.
        Release,

        // The instant the cast's projectile lands. ONE TIME FOR THE WHOLE
        // CAST at N places -- three projectiles leave one caster and land on
        // three slots at the same instant, because travelSeconds is an
        // authored number and not a distance divided by a speed.
        Arrival,

        // The one authoritative impact cue, authored in seconds on the
        // presentation. Independent of arrival on purpose: an author who wants
        // compression before the blow sets the cue later than the arrival.
        Hit,
    }

    public static class SpellCueNames
    {
        public static SpellCue Parse(string name) => Lookup(name, SpellCue.Release);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellCue)(-1)) != (SpellCue)(-1);

        public static string[] All => new[] { "release", "arrival", "hit" };

        private static SpellCue Lookup(string name, SpellCue fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellCue.Release;

            if (SpellWord.Is(name, "release")) return SpellCue.Release;
            if (SpellWord.Is(name, "arrival")) return SpellCue.Arrival;
            if (SpellWord.Is(name, "hit")) return SpellCue.Hit;
            return fallback;
        }
    }

    // ---- how it ends ---------------------------------------------------------

    public enum SpellEnd
    {
        // Play the folder through and stop. The default.
        Once,

        // Repeat the folder for the layer's whole life. Describes what the
        // sheet does WHILE it is alive, never how long it lives -- a loop with
        // no stated end is the unbounded case SpellLayerRules refuses.
        Loop,

        // Hold the last frame for the layer's whole life. Same bound.
        Hold,
    }

    public static class SpellEndNames
    {
        public static SpellEnd Parse(string name) => Lookup(name, SpellEnd.Once);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellEnd)(-1)) != (SpellEnd)(-1);

        public static string[] All => new[] { "once", "loop", "hold" };

        private static SpellEnd Lookup(string name, SpellEnd fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellEnd.Once;

            if (SpellWord.Is(name, "once")) return SpellEnd.Once;
            if (SpellWord.Is(name, "loop")) return SpellEnd.Loop;
            if (SpellWord.Is(name, "hold")) return SpellEnd.Hold;
            return fallback;
        }
    }

    // ---- which way it points -------------------------------------------------

    public enum SpellFacing
    {
        // Take the cast's own facing. The default, and right for anything with
        // a direction: a projectile, a wake, a sheet drawn firing left to
        // right.
        Auto,

        // Never mirror. What every pre-layer non-travelling effect becomes,
        // because those are unconditionally SetFacing(1f) today and a sheet
        // drawn symmetrical about the thing it lands on has no direction to
        // mirror.
        None,

        // Mirror against the cast's facing. No spell wants this yet; it is
        // here because "auto" and "none" leave the third case unsayable, and a
        // word costs one switch arm where a later field would cost a format
        // version.
        Reverse,
    }

    public static class SpellFacingNames
    {
        public static SpellFacing Parse(string name) => Lookup(name, SpellFacing.Auto);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellFacing)(-1)) != (SpellFacing)(-1);

        public static string[] All => new[] { "auto", "none", "reverse" };

        private static SpellFacing Lookup(string name, SpellFacing fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellFacing.Auto;

            if (SpellWord.Is(name, "auto")) return SpellFacing.Auto;
            if (SpellWord.Is(name, "none")) return SpellFacing.None;
            if (SpellWord.Is(name, "reverse")) return SpellFacing.Reverse;
            return fallback;
        }
    }

    // ---- which way it lies on the rank it spans ------------------------------

    // ONLY `place: formation` HAS A LINE TO LIE ALONG, which is why this word
    // is refused anywhere else (SpellLayerRules.CheckPlacement) rather than
    // quietly ignored. A target-anchored layer has one point, not two, and a
    // word that does nothing on five of the six placements is a word an author
    // will reasonably expect to do something.
    public enum SpellAlign
    {
        // Axis-aligned: the box is as wide as the struck rank's horizontal
        // extent and sits on its AVERAGE ground line. The default, and what
        // every formation layer did before this word existed.
        //
        // Right for something that describes a FOOTPRINT rather than a line --
        // a pool, a shadow, a wash of light across the floor -- where the
        // rank's tilt is not part of the drawing.
        Level,

        // Along the rank: the box runs from the leftmost struck body to the
        // rightmost, in BOTH axes, and is rotated to the line between them.
        //
        // Right for anything that IS a line: a crack, a sweep, a shockwave
        // running the length of the floor. The stage's ranks recede diagonally
        // (FightStageAnchors: the enemy's runs 300,-218 -> 660,-125), so a
        // line drawn level across them describes floor nobody stands on.
        Span,
    }

    public static class SpellAlignNames
    {
        public static SpellAlign Parse(string name) => Lookup(name, SpellAlign.Level);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellAlign)(-1)) != (SpellAlign)(-1);

        public static string[] All => new[] { "level", "span" };

        private static SpellAlign Lookup(string name, SpellAlign fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellAlign.Level;

            if (SpellWord.Is(name, "level")) return SpellAlign.Level;
            if (SpellWord.Is(name, "span")) return SpellAlign.Span;
            return fallback;
        }
    }

    // ---- what a layer's box scales against ------------------------------------

    // WHETHER `size` IS AN ABSOLUTE NUMBER OR A MULTIPLIER ON THE STRUCK
    // TARGET'S OWN FOOTPRINT. Every layer before this word existed authored a
    // constant box, which is right for a bolt or a status glint and wrong for
    // an effect that has to read as "covering the body" on both a rat and an
    // Elder Treant: Thorn Tithe's ritual at a fixed 350 reads as a small patch
    // on a target authoring stageScale 1.45, because nothing before this
    // multiplied the box by how big that target actually draws.
    //
    // ONLY WHERE A SINGLE STRUCK BODY IS RESOLVED. `target`, `target-centre`
    // and `sky` are the placements PlaceOne resolves against one struck
    // combatant, and a travelling layer arrives on one; `formation` measures its own span from every struck body at
    // once (there is no single "the target" to read a footprint off), and
    // `caster`/`caster-centre` without travel never touch a target's rect
    // either -- SpellLayerRules.CheckPlacement refuses `fit: target` on those
    // for the same reason it refuses `size` on a formation layer: a word
    // that would silently do nothing is refused rather than ignored.
    public enum SpellFit
    {
        // The authored `size` is the box, verbatim. The default, and every
        // layer that shipped before this word existed.
        None,

        // `size` (or SpellPresentation.DefaultSize) is multiplied by the
        // struck target's VISIBLE BODY: the larger of its opaque width and
        // height as drawn on stage, over Domain.Stage.TargetBody.Reference
        // Extent -- a front-rank Giant Rat -- so an authored size keeps
        // meaning "the box on a rat" and every other body is proportionate to
        // it. Measured off the idle drawing's alpha, not the sprite canvas:
        // a canvas carries headroom for the tallest pose (123px over the
        // golem's head), so a canvas-sized fit was a padding-sized fit.
        // Through the body's on-stage size it already carries the rank's
        // depth curve and the authored stageScale, read off the RESTING pose
        // so a squash mid-hit cannot smuggle itself into an effect's size.
        // An emitter fitted this way scales its particles, their speeds and
        // their source offset by the same factor. See FightController.BodyOf.
        Target,
    }

    public static class SpellFitNames
    {
        public static SpellFit Parse(string name) => Lookup(name, SpellFit.None);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellFit)(-1)) != (SpellFit)(-1);

        public static string[] All => new[] { "none", "target" };

        private static SpellFit Lookup(string name, SpellFit fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellFit.None;

            if (SpellWord.Is(name, "none")) return SpellFit.None;
            if (SpellWord.Is(name, "target")) return SpellFit.Target;
            return fallback;
        }
    }

    // ---- which way a projectile points ---------------------------------------

    // A PROJECTILE'S DRAWING TURNED ONTO ITS OWN LINE OF FLIGHT. Before this a
    // sheet flew at whatever angle it was painted, which is right for an orb
    // and wrong for a spear: Winter's Rebuke's is painted pointing 26 degrees
    // UP and the line from the air to the rat runs about 38 degrees DOWN.
    //
    // ONLY ON A LAYER THAT TRAVELS -- a still box has no line to lie along,
    // and SpellLayerRules refuses the word elsewhere rather than letting it do
    // nothing. The painted direction is the layer's own `artDegrees`.
    public enum SpellOrient
    {
        // Drawn as painted. The default and every layer before this word.
        None,

        // Turned so the painted direction (`artDegrees`) lies along the flight,
        // and the sheet's impact point rides the line from launch to aim
        // rather than the box's centre. Domain.Stage.SpellFlight.Turn.
        Path,
    }

    public static class SpellOrientNames
    {
        public static SpellOrient Parse(string name) => Lookup(name, SpellOrient.None);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellOrient)(-1)) != (SpellOrient)(-1);

        public static string[] All => new[] { "none", "path" };

        private static SpellOrient Lookup(string name, SpellOrient fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellOrient.None;

            if (SpellWord.Is(name, "none")) return SpellOrient.None;
            if (SpellWord.Is(name, "path")) return SpellOrient.Path;
            return fallback;
        }
    }

    // ---- which band it draws in ----------------------------------------------

    public enum SpellSort
    {
        // Behind every figure. The band FightScreen declares before the racks,
        // which is the only reason a fault reads as opening UNDER an enemy
        // rather than in front of it.
        Ground,

        // Over the HUD and under the damage numbers. The default, and where
        // every spell that is not a shared ground layer belongs.
        Effects,
    }

    public static class SpellSortNames
    {
        public static SpellSort Parse(string name) => Lookup(name, SpellSort.Effects);

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || Lookup(name, (SpellSort)(-1)) != (SpellSort)(-1);

        public static string[] All => new[] { "ground", "effects" };

        private static SpellSort Lookup(string name, SpellSort fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return SpellSort.Effects;

            if (SpellWord.Is(name, "ground")) return SpellSort.Ground;
            if (SpellWord.Is(name, "effects")) return SpellSort.Effects;
            return fallback;
        }
    }
}
