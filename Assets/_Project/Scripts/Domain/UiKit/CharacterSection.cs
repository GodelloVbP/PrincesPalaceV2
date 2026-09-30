namespace PrincesPalace.Domain.UiKit
{
    // The CHARACTER tab's sections (plan C15), in rail order. The rail lists
    // them top to bottom and the detail area shows the selected one; nothing
    // covers the rail, so a section is a place, not a panel opened over one.
    public enum CharacterSection
    {
        Loadout,
        Attributes,
        Skills,
        RewardTrack,
    }

    public static class CharacterSections
    {
        // The rail's rows, top to bottom. The screen builds one row per entry
        // and the controller indexes by position, so this is the one order.
        public static readonly CharacterSection[] All =
        {
            CharacterSection.Loadout,
            CharacterSection.Attributes,
            CharacterSection.Skills,
            CharacterSection.RewardTrack,
        };

        public static int IndexOf(CharacterSection section) => System.Array.IndexOf(All, section);

        public static UiString Label(CharacterSection section)
        {
            switch (section)
            {
                case CharacterSection.Loadout: return UiStrings.CharacterSectionLoadout;
                case CharacterSection.Attributes: return UiStrings.CharacterSectionAttributes;
                case CharacterSection.Skills: return UiStrings.CharacterSectionSkills;
                case CharacterSection.RewardTrack: return UiStrings.CharacterSectionRewardTrack;
                default: throw new System.ArgumentOutOfRangeException(nameof(section), section, null);
            }
        }
    }
}
