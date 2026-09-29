namespace PrincesPalace.Domain.UiKit
{
    // What a caller wants the menu to point at once its tab is showing. The
    // second half of SystemMenuController.Open(tab, target): the tab says where,
    // the target says which thing on it.
    public enum MenuTargetKind
    {
        None,

        // A character in the dossier, by definition id.
        CharacterId,

        // A named section of the tab, one of the MenuSections ids.
        Section,

        GlossaryEntry,
        Relic,
    }

    // The section ids a target can name. One home for the strings, so a pane
    // and its callers cannot disagree about spelling.
    public static class MenuSections
    {
        // The dossier's pack panel, which becomes the LOADOUT pack when the
        // Character tab is rebuilt.
        public const string Pack = "pack";
    }

    // Kind plus id. A pane acts on the kinds it has a place for and ignores
    // the rest, so a target may name something a later phase builds without
    // breaking the earlier ones.
    public readonly struct MenuTarget
    {
        public readonly MenuTargetKind Kind;
        public readonly string Id;

        public MenuTarget(MenuTargetKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public bool IsNone => Kind == MenuTargetKind.None;

        public bool Is(MenuTargetKind kind, string id) => Kind == kind && Id == id;

        public static MenuTarget None => default;

        public static MenuTarget Character(string definitionId) =>
            new MenuTarget(MenuTargetKind.CharacterId, definitionId);

        public static MenuTarget Section(string sectionId) =>
            new MenuTarget(MenuTargetKind.Section, sectionId);

        public static MenuTarget Glossary(string entryId) =>
            new MenuTarget(MenuTargetKind.GlossaryEntry, entryId);

        public static MenuTarget RelicEntry(string relicId) =>
            new MenuTarget(MenuTargetKind.Relic, relicId);

        // The pack, for the I key.
        public static MenuTarget Pack => Section(MenuSections.Pack);
    }
}
