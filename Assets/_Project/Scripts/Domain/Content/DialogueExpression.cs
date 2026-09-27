namespace PrincesPalace.Domain.Content
{
    // The faces a party character's dialogue line can ask for (plan
    // contract 11). Authored as a lowercase string on RawEventLine.expression
    // -- empty means Neutral -- and parsed by EventEntryResolver, which
    // refuses anything else. The member names ARE the bust file names,
    // lowercased (DialogueBust.FileNameOf), so tools/normalize_dialogue_busts.py's
    // EXPRESSIONS list and this enum have to name the same seven. Appended,
    // never inserted: ResolvedEventLine stores it as an int on the asset.
    // An event's own speakers declare their faces as strings instead
    // (RawEventSpeaker.expressions), so this enum stays the party's.
    public enum DialogueExpression
    {
        Neutral,
        Happy,
        Annoyed,
        Nervous,
        Sad,
        Surprised,

        // Shawn at the Bell in the Fog (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.3).
        Entranced,
    }

    // Which screen edge a speaker's bust stands against (plan contract 5).
    // Busts are painted facing the viewer's right, so Left shows as painted
    // and Right is mirrored -- a speaker always faces inward (contract 9).
    public enum DialogueSide
    {
        Left,
        Right,
    }
}
