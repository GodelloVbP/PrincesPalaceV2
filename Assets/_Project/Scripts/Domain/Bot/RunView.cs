using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Bot
{
    // What an IRunPolicy needs to see to make an out-of-fight choice,
    // flattened off whatever the real run state (RunManager/SaveData, both
    // Core) looks like at the moment -- deliberately minimal, per the plan:
    // the Core driver fills this in fresh at each choice point rather than
    // handing the policy the run itself, so a policy cannot reach past the
    // one screen's worth of information a player actually has.
    public readonly struct RunView
    {
        public readonly float PartyHpFraction;
        public readonly int Step;
        public readonly int Floor;
        public readonly int Gold;
        public readonly IReadOnlyList<string> HeldRelicIds;

        // WHAT EACH OFFER ON THE TABLE IS WORTH, index-aligned with the
        // `offers` list handed to ChooseOffer, and empty everywhere else.
        //
        // Here rather than on ItemOffer because ItemOffer is the GAME's type
        // -- the Reckoning screen builds them too -- and a bot-only field on
        // it would be a number the real game carries and never fills. It is
        // on the view for the reason the view exists: this is information the
        // driver puts in front of the policy at one choice point, exactly as
        // the Reckoning screen puts a stat comparison in front of a player.
        //
        // Filled by Core's GearEvaluator, which is the only layer that can
        // resolve an item id into what wearing it would do (the plan's F1).
        // ChooseOffer used to rank on tier-then-plus alone, which is not what
        // a player does: a tier-3 helm is worse than a tier-2 sword to
        // somebody with an empty hand, and blind to the fact that the helm is
        // already worn in a better roll.
        public readonly IReadOnlyList<float> OfferScores;

        public RunView(float partyHpFraction, int step, int floor, int gold,
            IReadOnlyList<string> heldRelicIds, IReadOnlyList<float> offerScores = null)
        {
            PartyHpFraction = partyHpFraction;
            Step = step;
            Floor = floor;
            Gold = gold;
            HeldRelicIds = heldRelicIds ?? Array.Empty<string>();
            OfferScores = offerScores ?? Array.Empty<float>();
        }

        // The same view with offer scores attached. The driver builds the view
        // once per choice point and only the offer point has scores to add.
        public RunView WithOfferScores(IReadOnlyList<float> scores) =>
            new RunView(PartyHpFraction, Step, Floor, Gold, HeldRelicIds, scores);
    }
}
