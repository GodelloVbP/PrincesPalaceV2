using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Rewards
{
    // One party member's line on the Reckoning's tally tab.
    public readonly struct TallyMember
    {
        public readonly string CharacterId;
        public readonly string DisplayName;

        public TallyMember(string characterId, string displayName)
        {
            CharacterId = characterId;
            DisplayName = displayName;
        }
    }

    // Everything the Reckoning paints, handed in by whoever opens it.
    //
    // TWO FORMS (docs/PLAN_BUGS_QA.md C3). The full form shows what a
    // paid fight paid: gold, experience, the offers. The outcome form is a
    // fight inside a run that paid nothing (a `wake` loss, a `pays: false`
    // event fight): no gold, experience or offers, a title naming the ending,
    // and the rest of the screen as always -- the relic chips, the tally and
    // the way out. Reward is the discriminator: null is the outcome form.
    public sealed class ReckoningRequest
    {
        public readonly CombatReward Reward;
        public readonly IReadOnlyList<ItemOffer> Offers;
        public readonly FightEnding Ending;

        // Relic receipts to show as chips (C2), in grant order.
        public readonly IReadOnlyList<string> Receipts;

        public readonly IReadOnlyList<string> Notices;
        public readonly IReadOnlyList<TallyMember> Tally;
        public readonly CombatLedger Ledger;

        public bool IsOutcomeForm => Reward == null;

        private ReckoningRequest(CombatReward reward, IReadOnlyList<ItemOffer> offers, FightEnding ending,
            IReadOnlyList<string> receipts, IReadOnlyList<string> notices, IReadOnlyList<TallyMember> tally,
            CombatLedger ledger)
        {
            Reward = reward;
            Offers = offers ?? new List<ItemOffer>();
            Ending = ending;
            Receipts = receipts ?? new List<string>();
            Notices = notices ?? new List<string>();
            Tally = tally ?? new List<TallyMember>();
            Ledger = ledger ?? new CombatLedger();
        }

        // A paid fight. The tally, the ledger and the notices are the reward's
        // own, so the two forms cannot disagree about where those come from.
        public static ReckoningRequest Full(CombatReward reward, IReadOnlyList<ItemOffer> offers,
            IReadOnlyList<string> receipts = null)
        {
            var tally = reward?.Characters.Select(c => new TallyMember(c.CharacterId, c.DisplayName)).ToList();
            return new ReckoningRequest(reward, offers, FightEnding.Victory, receipts, reward?.Notices, tally,
                reward?.Ledger);
        }

        public static ReckoningRequest Outcome(FightEnding ending, IReadOnlyList<string> receipts,
            IReadOnlyList<string> notices, IReadOnlyList<TallyMember> tally, CombatLedger ledger) =>
            new ReckoningRequest(null, null, ending, receipts, notices, tally, ledger);
    }
}
