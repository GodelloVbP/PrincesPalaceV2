using System;

namespace PrincesPalace.Domain.Party
{
    // One roster member as the formation sees it -- not a Character
    // (Data/SaveData.cs, a Core concept Domain cannot reference), just the
    // three facts a seating decision needs. The controller (P3) is what
    // translates a real Character into this.
    //
    // HasArt is supplied by the caller rather than derived here: "does this
    // character have art yet" is a content/art-pipeline question, and this
    // assembly has no way to ask it -- Domain cannot see Resources or the
    // art folders any more than it can see SaveData.
    public readonly struct PartyRosterEntry : IEquatable<PartyRosterEntry>
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly bool HasArt;

        public PartyRosterEntry(string id, string displayName, bool hasArt)
        {
            Id = id;
            DisplayName = displayName;
            HasArt = hasArt;
        }

        // Identity is the id alone -- two entries naming the same character
        // are the same roster member even if a caller built them from two
        // different snapshots with (say) a stale DisplayName.
        public bool Equals(PartyRosterEntry other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PartyRosterEntry other && Equals(other);
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    }
}
