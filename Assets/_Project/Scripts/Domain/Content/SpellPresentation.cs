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

        // Does this effect TRAVEL, or does it happen where it lands?
        //
        // Almost every sheet is a thing that occurs on the target -- a flare, a
        // bolt striking down, rocks erupting -- and is fitted into a square box
        // centred on them. mud_blast is drawn the other way: a conjuring glyph
        // at the left of the cell, a lance crossing it, an impact at the right.
        // Centred on the target that glyph appears in mid-air a few hundred
        // pixels short of the caster, which is what "it does not come from the
        // character" describes.
        //
        // A flag rather than something inferred from the art. It could be
        // guessed -- the energy's centroid walks left to right across a
        // directional sheet and stays put on a centred one -- and a guess that
        // is right four times out of five puts an effect in the wrong place on
        // the fifth with nothing in the content saying why.
        public bool fromCaster;

        // WHICH FRAME THE EFFECT LEAVES ON, for a fromCaster sheet. 1-based
        // like impactFrame, and 0 means "from the very first frame".
        //
        // Without it a travelling effect starts drifting the instant it appears,
        // so mud_blast's conjuring glyph spun its eight turns while already
        // halfway across the stage -- it tumbled through the air instead of
        // charging where it was cast. The sequence has two phases and the flight
        // belongs to the second: hold at the caster for the charge, then throw.
        public int departFrame;

        // Where the sound is, if there is one.
        public string sfxPath = "";

        // Left unset, a spell runs for this long and lands on this frame. Both
        // were the resolver's private constants and belong beside the fields
        // they default, so a new caller cannot invent a different silence.
        public const float DefaultSeconds = 0.6f;
        public const int DefaultImpactFrame = 3;

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
            string rawSfxPath, bool rawFromCaster = false, int rawDepartFrame = 0)
        {
            return new SpellPresentation
            {
                path = (rawPath ?? "").Trim(),
                seconds = rawSeconds >= 0f ? rawSeconds : DefaultSeconds,
                impactFrame = rawImpactFrame >= 1 ? rawImpactFrame : DefaultImpactFrame,
                fromCaster = rawFromCaster,
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
            fromCaster = fromCaster,
            departFrame = departFrame,
            sfxPath = sfxPath,
        };
    }
}
