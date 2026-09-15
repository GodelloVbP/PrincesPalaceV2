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

    // PHASE 3's read model for a character's collected Identity nodes
    // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §7 phase 3
    // package 1: "expose a small read model"). No rendering yet -- phase 5
    // is where a dossier/roster/victory screen actually draws any of this.
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
        // follows (docs/PLAN_REWARD_TRACKS.md §2).
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
        public static CharacterIdentityItem? SelectedTitleFor(Character character)
        {
            if (character == null) return null;

            var titles = CollectedFor(character).Where(item => item.Kind == TrackIdentityKind.Title).ToList();
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
        // a line of words, a rim metal, an emboss metal, and whether the
        // portrait is framed. ONE read model for every surface -- the fight
        // plate, the hub roster card and whatever draws them next -- because
        // the alternative is each screen walking CollectedFor with its own
        // idea of what "gold" means and the three quietly disagreeing.
        //
        // MASTERY IS A MODIFIER, NOT A SEVENTH KIND. §4's level 40 reads
        // "Mastery: gold plate, 'Master'", which is three of the four fields
        // at once: it forces both metals gold and adds a word to the line.
        // Resolved here rather than at each draw site, so a surface that draws
        // only the rim cannot forget the half of mastery that is a rim.
        public readonly struct IdentityLook
        {
            // The words under the name: the selected title, then VICTOR if the
            // victory pose is collected, then MASTER. Empty for a character
            // below level 31, which is most of a career.
            //
            // NOT WHAT THE FIGHT PLATE DRAWS -- see PlateWord below. This
            // stays the full sentence so the victory screen (its own
            // placeholder for the pose art, §5) still has VICTOR to say when
            // it is built; the plate found three words unreadable at its
            // band's width and phase 5's review cut it down to one there
            // without touching what this field means everywhere else.
            public readonly string Line;

            // THE FIGHT PLATE'S OWN WORD: MASTER when mastery is collected
            // (it outranks a title the same way it already forces both
            // metals gold), else the selected title, else empty. Never
            // VICTOR -- the plate has no room for three words at a legible
            // size, and the pose is a placeholder word anyway, not a title.
            public readonly string PlateWord;

            // "silver", "gold" or null. Gold wins wherever both are collected
            // -- §4 authors silver at 32 and gold at 38, and a rim cannot be
            // two metals.
            public readonly string RimMetal;
            public readonly string EmbossMetal;
            public readonly bool HasPortraitFrame;

            public IdentityLook(string line, string plateWord, string rimMetal, string embossMetal, bool hasPortraitFrame)
            {
                Line = line ?? "";
                PlateWord = plateWord ?? "";
                RimMetal = rimMetal;
                EmbossMetal = embossMetal;
                HasPortraitFrame = hasPortraitFrame;
            }

            public bool IsAnything =>
                Line.Length > 0 || RimMetal != null || EmbossMetal != null || HasPortraitFrame;
        }

        public const string Silver = "silver";
        public const string Gold = "gold";

        // THE VICTORY POSE HAS NO ART AND IS DRAWN AS A WORD, deliberately and
        // temporarily. §5: the pose is "(art)" -- a still nobody has drawn --
        // and phase 5 renders it as a line on the plate so the node pays
        // something the moment it is collected rather than being the one
        // identity level that visibly does nothing. It is on the art backlog;
        // when the stills land this word comes out and the pose goes in.
        public const string VictorWord = "VICTOR";
        public const string MasterWord = "MASTER";

        public static IdentityLook LookFor(Character character)
        {
            if (character == null) return new IdentityLook("", "", null, null, false);

            var collected = CollectedFor(character);

            string rim = null;
            string emboss = null;
            bool frame = false;
            bool pose = false;
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
                    case TrackIdentityKind.VictoryPose: pose = true; break;
                    case TrackIdentityKind.Mastery: mastery = true; break;
                }
            }

            if (mastery)
            {
                rim = Gold;
                emboss = Gold;
            }

            var words = new List<string>();
            var title = SelectedTitleFor(character);
            if (title != null && !string.IsNullOrWhiteSpace(title.Value.Value))
            {
                words.Add(title.Value.Value.ToUpperInvariant());
            }

            if (pose) words.Add(VictorWord);
            if (mastery) words.Add(MasterWord);

            // THE PLATE'S ONE WORD. Mastery outranks a title the same way it
            // already outranks the metals above -- a character who reached
            // 40 is not still introduced by whatever they picked at 32. The
            // pose never appears here; VICTOR stays in Line only.
            string plateWord = mastery
                ? MasterWord
                : (title != null && !string.IsNullOrWhiteSpace(title.Value.Value) ? title.Value.Value.ToUpperInvariant() : "");

            // A MIDDLE DOT WOULD BE NON-ASCII and this string reaches a TMP
            // label through UiString.Runtime; the kit's own separator
            // everywhere else on these screens is a full stop with air round
            // it (UiStrings.TrackRibbonHint, PartyCardTagInParty), so this
            // uses the same one.
            return new IdentityLook(string.Join("   .   ", words), plateWord, rim, emboss, frame);
        }
    }
}
