namespace PrincesPalace.Domain.Combat.Session
{
    // The fight HUD's fixed capacities, in ONE place that both the screen tree
    // and the combat session read.
    //
    // This exists because of a specific v1 bug: the turn-order snapshot was
    // sized by `initiativeIcons.Length` -- a UI ARRAY LENGTH reaching into
    // combat logic to decide how far ahead to simulate. Domain cannot see a
    // Unity array, and it should not want to: the number is a design decision
    // about the HUD, so it is declared here and passed in.
    //
    // Nothing here is derived from content at build time on purpose. These are
    // capacities the layout reserves; the RUNTIME fill count is a separate
    // thing, guarded at the Domain seam by layout functions that take a count,
    // and by content-side pins asserting no character or signature can exceed
    // what is reserved.
    public static class FightHudSpec
    {
        // How far ahead the turn order is shown, and therefore how far the
        // encounter is asked to project. v1: InitiativeTrackerSlots.
        public const int InitiativeSlots = 6;

        // Pooled, genuinely runtime-positioned - one of only two elements on
        // this screen that earns the pool audit exemption.
        public const int DamagePopups = 6;

        // HOW MANY COMBATANTS A SIDE CAN FIELD, and it is a rule rather than a
        // drawing detail.
        //
        // The stage has this many slots and the encounter roll draws this many
        // enemies at most, because a combatant with no slot is not merely
        // undrawn -- it is INVISIBLE AND STILL SWINGING. RefreshStage walks the
        // slots, so a fourth monster in a three-slot stage never appears while
        // the session goes on giving it turns, and the player takes damage from
        // something that is not on screen.
        //
        // Which is why this lives here, beside the session, rather than in the
        // screen: it bounds what combat may CREATE, and the stage having that
        // many slots is the consequence, not the cause.
        public const int StageSlotsPerSide = 3;

        // One plate per enemy stage slot; v1 tied these together with
        // `EnemyPlateCount = EnemyStageSlots` and so does this.
        public const int EnemyPlates = StageSlotsPerSide;

        // The signature-resource pip row. Kept as a reserved capacity with a
        // content pin rather than derived from content: hiding pips beyond the
        // current maximum is proven behaviour, and deriving the count would
        // change it for no gain.
        public const int WoolPips = 16;

        // The detail column's fixed stat rows: cost, power, target, effect.
        public const int DetailStatRows = 4;

        // Attack / Skill / Item / Run / Hold Back.
        public const int Verbs = 5;
    }
}
