namespace PrincesPalace
{
    // What a respec handed back.
    //
    // A pair rather than a single number because the two currencies are not
    // interchangeable and a caller that wants to say "12 embers and 4 points"
    // would otherwise have to diff the character across the call -- which is
    // the same trap CombatReward exists to avoid for experience, where "before"
    // stops existing the moment the mutation lands.
    public readonly struct RespecRefund
    {
        public readonly int Embers;
        public readonly int StatPoints;

        public RespecRefund(int embers, int statPoints)
        {
            Embers = embers;
            StatPoints = statPoints;
        }

        // Whether anything actually came back. A respec with nothing to refund
        // is legal and harmless -- it is what pressing the button twice does --
        // so callers need to tell "it worked" from "there was nothing to undo"
        // without comparing two ints themselves.
        public bool IsAnything => Embers > 0 || StatPoints > 0;
    }
}
