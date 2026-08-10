namespace PrincesPalace.Domain.Combat.Session
{
    // What the fight PAID, and what a loss says.
    //
    // The celebrating half already lives in the riders partial, called from both
    // paths a fight can end on. This adds the two things it did not do: settle a
    // payout, and say something when the party loses.
    public sealed partial class FightSession
    {
        private bool _payoutResolved;

        // What the fight paid out. Null until it is over -- a caller reading
        // this mid-fight is asking a question that has no answer yet, and a
        // zeroed struct would answer it wrongly rather than not at all.
        public VictoryRewards.Payout? Payout { get; private set; }

        // How deep the room was.
        //
        // Set on the session rather than fetched from the run when the payout is
        // computed, because the reward belongs to the room that was ENTERED --
        // by the time a fight resolves, a run could already have moved on.
        public int DepthStep { get; set; }

        // Called from both places a fight can end. Idempotent for the same
        // reason ResolveVictory is: a round resolves in one pass and more than
        // one path is entitled to notice.
        private void ResolveOutcome()
        {
            if (_payoutResolved || !_encounter.IsOver) return;
            _payoutResolved = true;

            if (_encounter.PlayerWon)
            {
                Payout = VictoryRewards.For(_enemyKits.Values, IsEliteFight, DepthStep);

                // Retro-attached, like every other line a turn decides after its
                // beat closed -- so the numbers arrive with the killing blow
                // rather than a beat later.
                AppendMessage($"{Payout.Value.Experience} experience, {Payout.Value.Gold} gold.");
                return;
            }

            // No payout at all on a loss, rather than a reduced one. A run that
            // ends pays nothing, and saying so plainly beats a consolation
            // number the player has to work out is meaningless.
            Payout = new VictoryRewards.Payout(0, 0);
            AppendMessage("The party falls.");
        }
    }
}
