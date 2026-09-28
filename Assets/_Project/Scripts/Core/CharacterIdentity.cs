using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // ONE COLLECTED IDENTITY ITEM -- the level it was collected at (needed to
    // tell which Title is newest; Identity entries carry no Amount) plus the
    // kind and value TrackEntry already carries.
    public readonly struct CharacterIdentityItem
    {
        public readonly int Level;
        public readonly TrackIdentityKind Kind;
        public readonly string Value;

        public CharacterIdentityItem(int level, TrackIdentityKind kind, string value)
        {
            Level = level;
            Kind = kind;
            Value = value ?? "";
        }
    }

    // A read model for a character's collected Identity nodes
    // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §7: "expose a
    // small read model"). No rendering yet -- that is a dossier/roster/
    // victory screen's job to add.
    //
    // Core rather than Domain, same reason RewardTracks itself is Core: it
    // needs RewardTracks.For(character) to find the track at all
    // (CODE_STANDARDS §1, content-layer concepts cannot reach Domain).
    // Domain owns the ARITHMETIC (RewardTrackDefinition.CollectedIdentity);
    // this owns only "whose track, and which title is shown".
    public static class CharacterIdentity
    {
        // Every Identity item this character has collected, oldest first --
        // read live off claimedTrackLevel, the same "nothing but the one
        // grant is stored" posture every other reward track total already
        // follows.
        public static IReadOnlyList<CharacterIdentityItem> CollectedFor(Character character)
        {
            if (character == null) return System.Array.Empty<CharacterIdentityItem>();

            var track = RewardTracks.For(character);
            return track.CollectedIdentity(character.claimedTrackLevel)
                .Where(pair => pair.Entry.IdentityKind.HasValue)
                .Select(pair => new CharacterIdentityItem(pair.Level, pair.Entry.IdentityKind.Value, pair.Entry.IdentityValue))
                .ToList();
        }

        // The title shown right now -- the character's own selectedTitleTrackLevel
        // if it still names a collected Title, else the newest collected
        // Title, else null (nothing collected yet). Defaulting to newest
        // matches the plan's own words: "the newest is shown, earlier ones
        // selectable in the hub roster".
        public static CharacterIdentityItem? SelectedTitleFor(Character character) =>
            character == null ? null : SelectedTitleFor(character, CollectedFor(character));

        // SAME ANSWER, GIVEN THE COLLECTED LIST RATHER THAN RECOMPUTING IT --
        // LookFor below already has to call CollectedFor once for the rim/
        // emboss/pose/mastery walk, so it hands that same list in here
        // instead of paying CollectedFor's own RewardTracks.For + LINQ walk
        // a second time for the same character. The single-argument overload
        // above stays the public entry point for every other caller, who has
        // no collected list of their own lying around.
        public static CharacterIdentityItem? SelectedTitleFor(Character character, IReadOnlyList<CharacterIdentityItem> collected)
        {
            if (character == null || collected == null) return null;

            var titles = collected.Where(item => item.Kind == TrackIdentityKind.Title).ToList();
            if (titles.Count == 0) return null;

            if (character.selectedTitleTrackLevel > 0)
            {
                foreach (var title in titles)
                {
                    if (title.Level == character.selectedTitleTrackLevel) return title;
                }
            }

            // Newest = collected at the highest level. CollectedFor walks
            // levels ascending, so the last Title in the list is the newest.
            return titles[titles.Count - 1];
        }

        // ---- PHASE 5: what the identity stretch actually LOOKS like ----------
        //
        // Six Identity kinds resolved into the four things a surface can draw:
        // a plate word, a rim metal, an emboss metal, and whether the
        // portrait is framed. ONE read model for every surface -- the fight
        // plate, the hub roster card and whatever draws them next -- because
        // the alternative is each screen walking CollectedFor with its own
        // idea of what "gold" means and the three quietly disagreeing.
        //
        // MASTERY IS A MODIFIER, NOT A SEVENTH KIND. §4's level 40 reads
        // "Mastery: gold plate, 'Master'", which is three of the four fields
        // at once: it forces both metals gold and becomes the plate word.
        // Resolved here rather than at each draw site, so a surface that draws
        // only the rim cannot forget the half of mastery that is a rim.
        public readonly struct IdentityLook
        {
            // THE FIGHT PLATE'S OWN WORD: MASTER when mastery is collected
            // (it outranks a title the same way it already forces both
            // metals gold), else the selected title, else empty. Never
            // VICTOR -- the plate has no room for three words at a legible
            // size, and the pose is a placeholder word anyway, not a title.
            //
            // THE ONLY WORD THIS TYPE RENDERS. A victory pose's placeholder
            // word (formerly VICTOR, on a since-deleted `Line` field) had no
            // renderer of its own -- only PlateWord ever reached a TMP label
            // (FightController.Hud.cs) -- so it was dead content rather than
            // a second surface waiting to be built. See docs/ART_PIPELINE.md
            // §7's victory-pose backlog entry for where the pose word goes
            // once the stills exist.
            public readonly string PlateWord;

            // "silver", "gold" or null. Gold wins wherever both are collected
            // -- §4 authors silver at 32 and gold at 38, and a rim cannot be
            // two metals.
            public readonly string RimMetal;
            public readonly string EmbossMetal;
            public readonly bool HasPortraitFrame;

            public IdentityLook(string plateWord, string rimMetal, string embossMetal, bool hasPortraitFrame)
            {
                PlateWord = plateWord ?? "";
                RimMetal = rimMetal;
                EmbossMetal = embossMetal;
                HasPortraitFrame = hasPortraitFrame;
            }

            public bool IsAnything =>
                PlateWord.Length > 0 || RimMetal != null || EmbossMetal != null || HasPortraitFrame;
        }

        public const string Silver = "silver";
        public const string Gold = "gold";

        public const string MasterWord = "MASTER";

        public static IdentityLook LookFor(Character character)
        {
            if (character == null) return new IdentityLook("", null, null, false);

            var collected = CollectedFor(character);

            string rim = null;
            string emboss = null;
            bool frame = false;
            bool mastery = false;

            foreach (var item in collected)
            {
                switch (item.Kind)
                {
                    // ASCENDING ORDER MAKES THE LAST ONE WIN, which is the
                    // same "newest is shown" rule the title follows and lands
                    // on gold for a fully collected track without this file
                    // having to rank the metals itself. CollectedFor walks
                    // levels ascending; §4 authors silver below gold.
                    case TrackIdentityKind.PlateRim: rim = item.Value; break;
                    case TrackIdentityKind.PlateEmboss: emboss = item.Value; break;
                    case TrackIdentityKind.PortraitFrame: frame = true; break;

                    // VICTORYPOSE CONTRIBUTES NOTHING HERE. It is still read
                    // by CollectedIdentity's own list (the track screen and
                    // roster show the node as collected) but has no plate
                    // rendering -- it has no still to draw. See
                    // docs/ART_PIPELINE.md §7's victory-pose backlog entry.
                    case TrackIdentityKind.VictoryPose: break;

                    case TrackIdentityKind.Mastery: mastery = true; break;
                }
            }

            if (mastery)
            {
                rim = Gold;
                emboss = Gold;
            }

            var title = SelectedTitleFor(character, collected);

            // THE PLATE'S ONE WORD. Mastery outranks a title the same way it
            // already outranks the metals above -- a character who reached
            // 40 is not still introduced by whatever they picked at 32.
            string plateWord = mastery
                ? MasterWord
                : (title != null && !string.IsNullOrWhiteSpace(title.Value.Value) ? title.Value.Value.ToUpperInvariant() : "");

            return new IdentityLook(plateWord, rim, emboss, frame);
        }
    }
}
