using System;

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
        public string path = "";

        // How long the whole animation takes, in seconds.
        public float seconds = DefaultSeconds;

        // Which frame the spell actually LANDS on, counting from 1. A bolt is
        // drawn arriving, not sitting still: the damage, the flash and the
        // number belong to the moment it connects, which is partway through the
        // sequence rather than at the end of it.
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
        public string anchor = "";

        // THE BOX THE ART IS FITTED INTO, square, in reference-frame units.
        //
        // 380 was a constant in FightController for every spell there has ever
        // been. It is fine for a bolt and wrong in both directions for the two
        // obvious next things: a boss's slam that should fill the stage, and a
        // status tick that should be a glint. Authored per spell now, and left
        // out it is still 380.
        public float size = DefaultSize;

        // WHICH FRAME THE EFFECT LEAVES ON, for a fromCaster sheet. 1-based
        // like impactFrame, and 0 means "from the very first frame".
        //
        // Without it a travelling effect starts drifting the instant it appears,
        // so mud_blast's conjuring glyph spun its eight turns while already
        // halfway across the stage -- it tumbled through the air instead of
        // charging where it was cast. The sequence has two phases and the flight
        // belongs to the second: hold at the caster for the charge, then throw.
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
        public float impactX = Unauthored;
        public float impactY = Unauthored;

        // Where the sound is, if there is one.
        public string sfxPath = "";

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

        // A copy, for every boundary a presentation crosses. Cheap, and it is
        // what keeps a combat beat from holding the same object a content asset
        // does -- a fight could otherwise edit the catalogue it was dealt from.
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
        };
    }
}
