using System;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Content
{
    // One validated realms.json row, and the shape RealmDefinition stores. Also
    // what RealmProfiles.Resolve returns for a floor with no row, so the opener
    // has one type to save from.
    //
    // A [Serializable] class with public fields, for the reason ResolvedSkill
    // records.
    [Serializable]
    public sealed class ResolvedRealm
    {
        public int Floor;
        public string Name = "";
        public RealmProfile Profile = new RealmProfile();

        // For the serializer only.
        public ResolvedRealm()
        {
        }

        public ResolvedRealm(int floor, string name, RealmProfile profile)
        {
            Floor = floor;
            Name = name;
            Profile = profile;
        }
    }
}
