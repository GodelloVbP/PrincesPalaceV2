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

        // ---- second life, level 90 of the reward track --------------------------
        //
        // How many times this fight may refuse to end. Set by the caller before
        // the fight opens, because Domain is engine-free and cannot ask a save
        // what the squad has earned.
        public int SecondLifeCharges { get; set; }

        // How many were actually spent, so the caller can write the run back.
        // Reported rather than pushed: the session has no business knowing that
        // a run exists.
        public int SecondLivesSpent { get; private set; }

        // What a revived character comes back on: half of max, rounded down but
        // never to zero. Named because "half" is the design and the arithmetic
        // is the detail -- a character with 1 max health coming back on 0 would
        // revive into death and spend the charge for nothing.
        public const int SecondLifeNumerator = 1;
        public const int SecondLifeDenominator = 2;

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

            // BEFORE CONCEDING, not after. A second life is spent at the moment
            // the fight would otherwise be lost, which makes this the one place
            // it can live: health is reduced by at least four different paths
            // (DealDamage, poison ticks, status combos, the enemies partial's
            // own call), and hooking each of them would be four chances to miss
            // one. Every path ends up here, because every path ends up asking
            // whether the fight is over.
            //
            // The fight then simply does not end -- there is no re-entry, no
            // branch in the defeat path, and nothing in the teardown changes.
            // That is the whole reason an in-fight revive is safe where a
            // run-level "continue" would not have been.
            if (!_encounter.PlayerWon && TrySecondLife())
            {
                return;
            }

            _payoutResolved = true;

            if (_encounter.PlayerWon)
            {
                // PLUS THE BOUNTY, which nothing collected until now.
                //
                // The Bounty Hunter Contract paid into BountyEarned on every
                // kill, printed "collects on the Giant Rat - 3 gold" in the
                // log, and stopped there: no production code read the property.
                // The relic was implemented, covered by EditMode tests that
                // assert on BountyEarned directly, and worth exactly zero gold
                // in a real run. Same shape as AUDIT #42 and as the
                // DifficultyCurve that was written, tested and never called.
                //
                // Added to the PAYOUT rather than banked separately, so it
                // reaches the wallet by the one route every other coin takes
                // (RunManager.BankPayout) and shows up in the Reckoning's gold
                // line, which is where a player would look for it.
                //
                // NOT put through DifficultyCurve.ScaleReward: the bounty is
                // already measured off the victim's own expReward, and that
                // figure is authored per enemy -- scaling it again here would
                // pay the depth multiplier twice for the same body. Worth
                // knowing that this makes the relic's absolute value flat with
                // depth while the base payout climbs; it is a tuning question
                // for whoever balances the descent, not a wiring one.
                var earned = VictoryRewards.For(_enemyKits.Values, IsEliteFight, DepthStep);
                Payout = new VictoryRewards.Payout(earned.Experience, earned.Gold + BountyEarned);

                // NO NUMBERS HERE. The Reckoning expands seconds later saying
                // exactly this experience and exactly this gold, in larger
                // type, with a bar -- and the bark sits above the panel where
                // it is the first thing the eye lands on. Two readouts of one
                // fact, and the smaller one arrives first.
                //
                // "Victory!" stays: that is the fight announcing its outcome,
                // which the Reckoning never says.
                return;
            }

            // No payout at all on a loss, rather than a reduced one. A run that
            // ends pays nothing, and saying so plainly beats a consolation
            // number the player has to work out is meaningless.
            Payout = new VictoryRewards.Payout(0, 0);
            AppendMessage("The party falls.");
        }

        // Brings every fallen party member back on half health, once per
        // charge. Returns whether the fight should carry on.
        //
        // EVERYONE WHO IS DOWN, for one charge, rather than one character per
        // charge. This only fires when the party would otherwise be wiped, so
        // the alternative -- reviving one and losing anyway because the second
        // is still at zero -- would spend the charge and change nothing, which
        // is the worst outcome available.
        //
        // Refuses when there is nobody to raise, so a charge cannot be spent on
        // a fight lost some other way.
        private bool TrySecondLife()
        {
            if (SecondLivesSpent >= SecondLifeCharges) return false;

            bool raised = false;
            foreach (var member in _encounter.PlayerParty)
            {
                if (member == null || member.IsAlive) continue;

                int half = member.MaxHealth * SecondLifeNumerator / SecondLifeDenominator;
                member.CurrentHealth = half < 1 ? 1 : half;
                raised = true;
            }

            if (!raised) return false;

            SecondLivesSpent++;
            AppendMessage("Not yet.");
            return true;
        }
    }
}
