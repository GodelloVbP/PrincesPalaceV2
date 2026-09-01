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

        public RunView(float partyHpFraction, int step, int floor, int gold, IReadOnlyList<string> heldRelicIds)
        {
            PartyHpFraction = partyHpFraction;
            Step = step;
            Floor = floor;
            Gold = gold;
            HeldRelicIds = heldRelicIds ?? Array.Empty<string>();
        }
    }
}
