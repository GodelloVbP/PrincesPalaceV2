using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // EVERYTHING ABOUT HOW A SPELL LOOKS, as one value that travels together.
    //
    // This exists because of a measurement rather than a preference. Adding one
    // int -- vfxDepartFrame, which holds a travelling effect at the caster
    // through its own wind-up -- touched TEN files:
    //
    //   RawSkillEntry, SkillEntryResolver, ResolvedSkill, SkillDefinition,
    //   ContentBuilder, FightEncounterAdapter, CombatBeat,
    //   FightSession.Beats, FightSession.Skills, FightController.SpellVfx
    //
    // Nine of those ten are the same edit: declare a field, then copy it from
    // the layer below to the layer above. None of them decide anything. There
    // were already five such fields and each had made the identical walk, so
    // the cost of a sixth was five files of prose about a value nobody in the
    // middle looks at.
    //
    // Carried as ONE VALUE the chain collapses: the middle layers hold a
    // SpellPresentation and hand it on whole, and a new knob is a field here
    // plus the code that reads it. Two files instead of ten, and the two are
    // the two that have an opinion.
    //
    // A CLASS WITH PUBLIC FIELDS, not a readonly struct, and that is Unity's
    // requirement rather than a design choice: JsonUtility deserialises into
    // writable fields, and a SerializeField on a ScriptableObject needs the
    // same. So it is mutable by construction and treated as immutable by
    // convention -- which is what every other DTO in this folder already does.
    //
    // The resolver COPIES rather than aliases when it builds one from raw JSON;
    // see Of(). A shared reference between a content asset and a combat beat
    // would let a fight edit the catalogue.
    [Serializable]
    public class SpellPresentation
    {
        // Resources-relative folder of this spell's animation frames, played
        // over the stage when it resolves. Empty means no visual.
        [ContentDoc("Resources-relative folder of this spell's animation frames; empty means no visual.")]
        public string path = "";

        // How long the whole animation takes, in seconds.
        [ContentDoc("How long the whole per-target animation takes, in seconds.")]
        public float seconds = DefaultSeconds;

        // Which frame the spell actually LANDS on, counting from 1. A bolt is
        // drawn arriving, not sitting still: the damage, the flash and the
        // number belong to the moment it connects, which is partway through the
        // sequence rather than at the end of it.
        [ContentDoc("Which frame (1-based) the spell actually lands on.")]
        public int impactFrame = DefaultImpactFrame;

        // WHERE THE EFFECT HAPPENS. One of SpellAnchor, spelled as a word.
        //
        // This was a bool called fromCaster, which is an enum wearing a
        // disguise: it could say "on the target" or "flies from the caster" and
        // there was no third thing it could say. A sheet of rocks erupting
        // wants the ground rather than the target's midriff; a self-buff wants
        // the caster even when the skill's target is an ally; a screen flash
        // wants neither. Each of those was a code change to a bool.
        //
        // A STRING, parsed, rather than the enum itself: JsonUtility writes an
        // enum as its ordinal, so the file would read "anchor": 2 and a
        // reordering of the enum would silently repoint every spell. The word
        // is what an author types and what an error message can name.
        //
        // Unrecognised spellings fall back to Target and say so once -- the
        // house style, and the right one here: a typo should misplace one
        // effect, not stop a fight resolving.
        [ContentDoc("Where the effect happens: a SpellAnchor name (Target, Caster, ...), parsed case-insensitively; unrecognised falls back to Target.")]
        public string anchor = "";

        // THE BOX THE ART IS FITTED INTO, square, in reference-frame units.
        //
        // 380 was a constant in FightController for every spell there has ever
        // been. It is fine for a bolt and wrong in both directions for the two
        // obvious next things: a boss's slam that should fill the stage, and a
        // status tick that should be a glint. Authored per spell now, and left
        // out it is still 380.
        [ContentDoc("The square box the art is fitted into, in reference-frame units; 0 means the default size.")]
        public float size = DefaultSize;

        // WHICH FRAME THE EFFECT LEAVES ON, for a fromCaster sheet. 1-based
        // like impactFrame, and 0 means "from the very first frame".
        //
        // Without it a travelling effect starts drifting the instant it appears,
        // so mud_blast's conjuring glyph spun its eight turns while already
        // halfway across the stage -- it tumbled through the air instead of
        // charging where it was cast. The sequence has two phases and the flight
        // belongs to the second: hold at the caster for the charge, then throw.
        [ContentDoc("Which frame (1-based) a from-caster effect leaves on; 0 means from the first frame.")]
        public int departFrame;

        // WHERE THE BLOW LANDS INSIDE THE SHEET, as fractions of one frame:
        // impactX from the LEFT edge, impactY from the BOTTOM edge -- the same
        // way up StanceManifest's groundLine measures, so the two numbers a
        // reader has to hold at once agree about which way is up.
        //
        // WHY AUTHORED RATHER THAN MEASURED, which is the whole argument.
        // The view used to derive this: put the box's bottom edge on the
        // target's ground line, corrected by the lowest opaque pixel the sheet
        // reaches once it has landed. That is a good rule for an effect drawn
        // standing on a floor and it is not a rule at all for anything else,
        // and both failure modes shipped:
        //
        //   frost_flare's burst sits a sixth of the way up its frame with a
        //   spray of embers falling BELOW it, so the measured floor was the
        //   embers and the burst rendered on the Giant Rat's chest;
        //
        //   mud_burst is a horizontal lance -- its impact is halfway up the
        //   frame and two thirds of the way ACROSS it, because the left third
        //   is reserved for the incoming bolt. Bottom-anchored and
        //   centre-aligned, it detonated above the rat's head and to one side.
        //
        // This is the same conclusion StanceManifest reached about actors,
        // and for the same reason: a scan believes whatever it finds, and what
        // it finds is a spray of sparks. A sheet states where it hits.
        //
        // UNAUTHORED means "keep the measured behaviour", so nothing that does
        // not state a point moves. -1 rather than 0, because 0 is a legitimate
        // point (the bottom-left corner) and a sentinel that collides with a
        // real value is a bug waiting for its first author.
        [ContentDoc("Where the blow lands inside the sheet, as a fraction from the left edge; -1 means unauthored (use the measured fallback).")]
        public float impactX = Unauthored;
        [ContentDoc("Where the blow lands inside the sheet, as a fraction from the bottom edge; -1 means unauthored (use the measured fallback).")]
        public float impactY = Unauthored;

        // Where the sound is, if there is one. Played at the moment the blow
        // lands -- see FightBeatPlayer's impact instant.
        [ContentDoc("Resources-relative path to the sound played the moment the blow lands.")]
        public string sfxPath = "";

        // ---- the shared ground layer, and the cue that leads into it -----------
        //
        // OPTIONAL, AND EVERY FIELD BELOW IS INERT WHEN groundPath IS EMPTY.
        // Every spell shipped before Cinderfault authors none of them and is
        // byte-identical in behaviour for it: HasGroundLayer is the single gate
        // the view asks, and it is false for an empty path.
        //
        // WHY A SECOND LAYER RATHER THAN A SECOND SPELL. Cinderfault is one
        // connected fault opening under the whole enemy formation and one
        // eruption per enemy standing on it. The per-target half is exactly
        // what the existing pool already draws; the fault is not, because
        // there is only ever ONE of it however many enemies are hit, and its
        // size is a property of the FORMATION rather than of any one target.
        // Two paths on one presentation is what lets the view draw both from
        // one beat without the controller learning any spell's id.
        [ContentDoc("Resources-relative folder of the shared ground-layer frames drawn once behind every enemy struck; empty means no ground layer at all.")]
        public string groundPath = "";

        // How long the ground layer runs and which frame it ruptures on.
        //
        // BOTH DEFAULT TO THE PER-TARGET SEQUENCE'S OWN NUMBERS, which is the
        // only default that can be right: the two layers have to peak on the
        // same instant or the damage lands on one of them and not the other.
        // Authored separately only for a sheet pair that cannot be composed to
        // the same length -- Cinderfault's are (nine frames each, rupture on
        // five, see tools/slice_spell_sheet.py), so it authors neither.
        [ContentDoc("How long the ground layer runs, in seconds; 0 falls back to the per-target sequence's own seconds.")]
        public float groundSeconds;
        [ContentDoc("Which frame (1-based) the ground layer ruptures on; 0 falls back to the per-target sequence's own impactFrame.")]
        public int groundImpactFrame;

        // THE SHAPE OF THE BOX THE FAULT IS FITTED INTO, width over height.
        //
        // The per-target box is square (see FightController.BoxFor) because
        // that is what every sheet before this one was drawn as. A fault is a
        // horizontal thing sized to the rack it opens under, and forcing it
        // into a square would letterbox it into a strip a third of the width
        // it was asked to span.
        //
        // ZERO means "take the sheet's own frame aspect", which is what a
        // square sheet with a wide drawing on it wants: the box matches the
        // frame, preserveAspect adds no letterbox, and the empty top and
        // bottom of the frame cost nothing because they are transparent.
        [ContentDoc("Width-over-height of the ground layer's box; 0 means take the sheet's own frame aspect.")]
        public float groundAspect;

        // WHERE THE FAULT'S OWN GROUND LINE SITS INSIDE ITS FRAME, as a
        // fraction from the BOTTOM edge -- the same measurement impactY makes,
        // for the same reason it is authored rather than scanned. The per-frame
        // lowest opaque pixel of a fault is its near lip in one drawing and a
        // thrown fragment in the next.
        [ContentDoc("Where the ground layer's own ground line sits, as a fraction from the bottom edge; -1 means unauthored.")]
        public float groundImpactY = Unauthored;

        // THE CUE THAT RUNS THROUGH THE CAST, as against sfxPath's one
        // transient at contact. Rock under strain, ending before the rupture.
        //
        // A SECOND PATH RATHER THAN A SECOND CLIP ON sfxPath, because the two
        // fire at different instants: this one when the beat opens, sfxPath
        // when the blow lands. One field could only ever have meant one of them.
        [ContentDoc("Resources-relative path to the sound that runs through the cast, ending before the rupture.")]
        public string castSfxPath = "";

        // ---- the layered format ------------------------------------------------
        //
        // EXTENDING THIS TYPE RATHER THAN SUCCEEDING IT, and the argument is a
        // measurement rather than a preference. AUDIT #60 counted what a field
        // on this chain costs: vfx.groundPath was 8 file mentions where
        // cooldownTurns was 41, "lower because SpellPresentation had already
        // collapsed the middle". A parallel presentation type means a second
        // field on RawSkillEntry, on RawElementChoice, on ResolvedSkill, on
        // CombatBeat and a second Copy() -- the exact restatement #60 exists to
        // stop. And RawElementChoice.vfx is a whole SpellPresentation, so every
        // element of an elemental spell gets a full layer list for free with no
        // change anywhere in the chain.

        // AN ARRAY, NOT A LIST, AND THAT IS FORCED. ContentSchema.ElementType
        // unwraps arrays only, so a List<SpellLayer> would print as List`1 in
        // docs/CONTENT_SCHEMA.md and its element type would never be expanded
        // into a table.
        [ContentDoc("Ordered layers this spell draws; empty means the single-block fields above are used as-is.")]
        public SpellLayer[] layers = Array.Empty<SpellLayer>();

        // WHICH REVISION OF THE VOCABULARY THIS BLOCK WAS AUTHORED AGAINST.
        //
        // 0 is every block written before layers existed and every block that
        // authors none; a block with layers must say 1, and SpellLayerRules
        // refuses it otherwise. The policy the number exists to make arguable:
        // ADDING a word to a discriminator, or a field with an inert default,
        // keeps version 1 -- old content still means what it meant. CHANGING
        // what an existing word or field means increments it, and version 1
        // blocks are then refused with a migration message or converted, never
        // reinterpreted silently. No migration function is written here,
        // because there is no version 2 to migrate to and naming one now would
        // be a framework with no user.
        [ContentDoc("Which revision of the layer vocabulary this block was authored against; 0 means the pre-layer format.")]
        public int layerFormat;

        // THE ONE AUTHORITATIVE IMPACT CUE, in seconds after the beat opens.
        //
        // A PROPERTY OF THE CAST AND NOT OF ANY LAYER, precisely because the
        // brief demands one cue: a layer could own it, and then two layers
        // could disagree about when the blow landed. 0 is legal and common --
        // it is what every melee beat does today.
        //
        // A version-0 block leaves this at 0 and derives its cue from
        // impactFrame instead, exactly as today. That derivation is a function
        // of the number of PNGs on disk, which is the accident the layered
        // format removes: a version-1 spell whose art fails to load still
        // lands its blow when it said it would.
        [ContentDoc("Seconds after the cast opens that the blow lands; the one authoritative impact cue. Ignored for a pre-layer block, which derives the cue from impactFrame.")]
        public float hitCueSeconds;

        // Left unset, a spell runs for this long and lands on this frame. Both
        // were the resolver's private constants and belong beside the fields
        // they default, so a new caller cannot invent a different silence.
        public const float DefaultSeconds = 0.6f;
        public const int DefaultImpactFrame = 3;
        public const float DefaultSize = 380f;

        // "This sheet does not say." Negative on purpose -- see impactX.
        public const float Unauthored = -1f;

        // Whether both halves of the point are there. BOTH, never one: a sheet
        // that states its height and not its width would be corrected on one
        // axis and left on the other, which lands the effect somewhere neither
        // rule intended and looks like a third bug.
        public bool HasImpactPoint =>
            impactX >= 0f && impactX <= 1f && impactY >= 0f && impactY <= 1f;

        // The parsed anchor. Case-insensitive, because "Travel" and "travel"
        // are the same intent and refusing one of them teaches nothing.
        public SpellAnchor Anchor => SpellAnchorNames.Parse(anchor);

        // THE ONE THING A CONSUMER ASKS BEFORE DRAWING ANYTHING.
        public bool HasAnimation => !string.IsNullOrEmpty(path) && seconds > 0f;

        // The same question for the shared layer, and the ONE gate the view
        // asks: a spell that authored no ground path draws no ground layer, and
        // that is every spell but one.
        public bool HasGroundLayer => !string.IsNullOrEmpty(groundPath) && GroundSeconds > 0f;

        // The ground layer's timing, falling back to the per-target sequence's.
        // See groundSeconds' own header for why that fallback is the point
        // rather than a convenience.
        public float GroundSeconds => groundSeconds > 0f ? groundSeconds : seconds;
        public int GroundImpactFrame => groundImpactFrame >= 1 ? groundImpactFrame : impactFrame;

        // Whether the fault states its own ground line. One axis only, unlike
        // HasImpactPoint: a fault is placed horizontally by the FORMATION it
        // opens under, so there is no x for a sheet to state.
        public bool HasGroundImpactY => groundImpactY >= 0f && groundImpactY <= 1f;

        // A FRESH ONE EVERY TIME, not a shared static.
        //
        // It was `static readonly`, and CombatBeatTests found the hole in one
        // line: a beat defaulting to the shared instance and then setting a
        // field on it edits the value every other default holder is looking at.
        // The type is mutable because Unity's serialisers require it, so the
        // only safe "empty" is a new one -- and this is called once per cast,
        // which is not a place an allocation matters.
        public static SpellPresentation None => new SpellPresentation();

        // Built from raw JSON, with the sentinels resolved once. -1 means "not
        // authored" in the file and must never reach the view, which would read
        // it as a real duration and play nothing for six tenths of a negative
        // second.
        public static SpellPresentation Of(string rawPath, float rawSeconds, int rawImpactFrame,
            string rawSfxPath, string rawAnchor = "", int rawDepartFrame = 0)
        {
            return new SpellPresentation
            {
                path = (rawPath ?? "").Trim(),
                seconds = rawSeconds >= 0f ? rawSeconds : DefaultSeconds,
                impactFrame = rawImpactFrame >= 1 ? rawImpactFrame : DefaultImpactFrame,
                anchor = rawAnchor ?? "",
                departFrame = rawDepartFrame,
                sfxPath = (rawSfxPath ?? "").Trim(),
            };
        }

        // Whether this block was authored in the layered vocabulary. The one
        // question the runtime asks before deciding whether the adapter has to
        // run -- asked rather than compared, so "which version is this" has one
        // home.
        public bool HasLayers => layers != null && layers.Length > 0;

        // WHETHER THIS BLOCK DRAWS ANYTHING AT ALL, in either vocabulary.
        //
        // Two callers ask this question and both used to ask it of `path`
        // alone: "did this element author its own art" (ResolvedSkill
        // .AsElement) and "does this blow bring its own effect, so the house's
        // contact language should stay out of the way"
        // (FightBeatPlayer.WantsContactFx). A pre-layer block's only way of
        // saying yes IS a path, so that read correctly for five years and for
        // every spell that shipped.
        //
        // A layered block authors no `path` by construction -- SpellLayerRules
        // refuses a block that authors both -- so both callers read the first
        // layered spell in the game as authoring nothing. The element's whole
        // presentation was dropped and the melee burst it does not want was
        // drawn over the splash. One property, so a third caller cannot ask it
        // the old way.
        public bool HasArt =>
            HasLayers || !string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(groundPath);

        // ---- the legacy adapter --------------------------------------------------

        // A PRE-LAYER BLOCK, SAID IN LAYERS. Pure, engine-free, and its output
        // is NEVER STORED: authored content stays exactly what an author typed,
        // and the runtime sees layers and only layers. That is what makes one
        // orchestration path rather than two -- the alternative, running this
        // in the resolver and writing the result back, would put every shipped
        // spell in the state SpellLayerRules refuses (layers with layerFormat
        // 0, and layers beside a single-block path).
        //
        // TAKES THE FRAME COUNT, which the plan's signature did not. Two of the
        // three numbers a version-0 block needs -- the hold before the flight
        // and the flight's own length -- are `seconds` times a FRAME INDEX over
        // the frame count, because that is what today's player does: it departs
        // on frame departFrame-1 and arrives on frame impactFrame-1 at
        // seconds/frames.Length each. Domain cannot read a folder, so the count
        // is an argument rather than a lookup; the arithmetic stays here, where
        // an EditMode test can reach it, instead of being split across the
        // Domain/Core line where the departure half is the part that needs care.
        //
        // A count of 0 -- art that failed to load -- yields a non-travelling
        // layer, which is what the player does today: PlayRoutine returns
        // before it computes anything when the frame array is empty.
        public SpellLayer[] ToLayers(int frameCount)
        {
            var built = new List<SpellLayer>(2);

            if (!string.IsNullOrWhiteSpace(path)) built.Add(PerTargetLayer(frameCount));
            if (!string.IsNullOrWhiteSpace(groundPath)) built.Add(GroundLayer());

            return built.ToArray();
        }

        private SpellLayer PerTargetLayer(int frameCount)
        {
            var anchor = Anchor;
            bool travels = SpellAnchorNames.Travels(anchor);
            bool centred = SpellAnchorNames.Centred(anchor);

            var layer = new SpellLayer
            {
                id = "vfx",
                render = "sprite",

                // A TRAVELLING ANCHOR BECOMES A CASTER-SIDE PLACE PLUS A
                // TRAVEL TIME, because travel is a behaviour and not a kind of
                // placement. Which caster point it leaves from is the same
                // centred/standing axis the anchor already carried.
                place = travels
                    ? (centred ? "caster-centre" : "caster")
                    : PlaceWordFor(anchor),

                at = "release",
                path = path,
                seconds = seconds,

                // FIT THE FOLDER INTO `seconds`, which reproduces
                // perFrame = total / frames.Length exactly. The frame rate of
                // a pre-layer block IS its duration divided by its frame count
                // and was never authored, so anything else here would retime
                // every shipped spell.
                fps = 0f,
                until = "once",
                size = size,
                impactX = impactX,
                impactY = impactY,
                sort = "effects",

                // A NON-TRAVELLING EFFECT IS UNCONDITIONALLY SetFacing(1f)
                // today, which is 'none' and not 'auto'. Mapping it to auto
                // would mirror a monster's flare and break
                // AnOrdinaryEffectAfterAMirroredOneIsNotItselfMirrored.
                facing = travels ? "auto" : "none",
            };

            if (!travels || frameCount <= 0) return layer;

            // THE SAME CLAMPS THE PLAYER APPLIES, in the same order: the
            // arrival is held inside the sequence and the departure is held
            // below the arrival, so the two can never cross and produce a
            // negative flight.
            int arrivalIndex = Clamp(impactFrame - 1, 0, frameCount - 1);
            int departIndex = Clamp(departFrame - 1, 0, arrivalIndex);

            float perFrame = seconds / frameCount;
            layer.travelDelay = departIndex * perFrame;
            layer.travelSeconds = (arrivalIndex - departIndex) * perFrame;

            // A sheet whose departure and arrival land on one frame does not
            // move at all today (the lerp is guarded on arrival > departure),
            // so it must not become a projectile with a zero-length flight
            // that snaps. It keeps the caster placement and simply stays.
            return layer;
        }

        private SpellLayer GroundLayer() => new SpellLayer
        {
            id = "ground",
            render = "sprite",

            // ONE FAULT HOWEVER MANY ENEMIES STAND IN IT, and the width is the
            // measured span rather than an authored number -- which is why no
            // size, no dx and no impactX come across. The pre-layer block has
            // no groundImpactX field to have brought one from.
            place = "formation",
            at = "release",
            path = groundPath,
            seconds = GroundSeconds,
            fps = 0f,
            until = "once",
            aspect = groundAspect,
            impactY = groundImpactY,
            facing = "none",
            sort = "ground",
        };

        private static string PlaceWordFor(SpellAnchor anchor)
        {
            switch (anchor)
            {
                case SpellAnchor.Caster: return "caster";
                case SpellAnchor.CasterCentre: return "caster-centre";
                case SpellAnchor.TargetCentre: return "target-centre";
                default: return "target";
            }
        }

        private static int Clamp(int value, int low, int high) =>
            value < low ? low : value > high ? high : value;

        // A copy, for every boundary a presentation crosses. Cheap, and it is
        // what keeps a combat beat from holding the same object a content asset
        // does -- a fight could otherwise edit the catalogue it was dealt from.
        //
        // THE LAYER ARRAY IS DEEP-COPIED, and it matters more here than for the
        // scalar fields. Copy() is the ONLY boundary, three call sites go
        // through it, and the first is reached from AsElement's
        // MemberwiseClone() -- which shares every reference type it does not
        // overwrite. A shallow array copy would alias the catalogue's own layer
        // objects into every beat, which is the exact bug this method exists to
        // prevent, one level down.
        //
        // Still a hand-written field list, which is the shape AUDIT #60 records
        // dropping fields twice. What closes it is
        // SpellPresentationCopyTests' reflection pin: every public instance
        // field of this type must come across, so a field added and not copied
        // fails rather than going quiet.
        public SpellPresentation Copy() => new SpellPresentation
        {
            path = path,
            seconds = seconds,
            impactFrame = impactFrame,
            anchor = anchor,
            size = size,
            departFrame = departFrame,
            impactX = impactX,
            impactY = impactY,
            sfxPath = sfxPath,
            groundPath = groundPath,
            groundSeconds = groundSeconds,
            groundImpactFrame = groundImpactFrame,
            groundAspect = groundAspect,
            groundImpactY = groundImpactY,
            castSfxPath = castSfxPath,
            layerFormat = layerFormat,
            hitCueSeconds = hitCueSeconds,
            layers = CopyOf(layers),
        };

        private static SpellLayer[] CopyOf(SpellLayer[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<SpellLayer>();

            var copied = new SpellLayer[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                copied[i] = source[i] == null ? new SpellLayer() : source[i].Copy();
            }

            return copied;
        }
    }
}
