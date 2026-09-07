using System.Collections.Generic;

namespace PrincesPalace.Domain.Bot
{
    // ONE PLACE that maps an archetype's name to a policy instance,
    // Domain-side. Core/Bot/BotRunDriver.cs (`PolicyFor`, `Archetypes`) still
    // owns the registry the batch runner and smoke tests actually call as of
    // this phase -- it is out of scope here (this session touches only
    // Domain/Bot, Tests/EditMode/Bot*Tests.cs and the tools/bot_report.py
    // trio) and a concurrent session may be mid-edit on it. This factory
    // exists so Core's registry CAN become a one-line call into Domain
    // instead of a second switch that has to be kept in sync by hand every
    // time an archetype is added -- see this phase's own report for the
    // handoff note asking for that switch.
    public static class Archetypes
    {
        // Declaration order is also report order (BalanceBotRunner's cells,
        // the archetype-gap graph) wherever a caller iterates this instead
        // of hand-typing the four names.
        // APPENDED, never reordered: BalanceBotRunner filters -botArchetypes
        // against this list, so a name has to be in it to be selectable at
        // all (`tools/bot.ps1 -Archetypes ProtectTheFront`), and the four
        // that were here keep both their behaviour and their report column.
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "RandomLegal", "GreedyAggressive", "GreedyDefensive", "Lookahead2", "ProtectTheFront",
        };

        // One object per call, implementing both IFightPolicy and
        // IRunPolicy -- every archetype in this file is exactly that shape,
        // which is also what BotRunDriver.PolicyFor's own header says the
        // registry expects. Null for a name this file does not know, rather
        // than throwing: BotRunDriver already turns "no policy named X" into
        // its own UnknownArchetype invariant hit, and duplicating that
        // reporting here would be a second place that decides what an
        // unknown archetype means.
        public static object Create(string name)
        {
            switch (name)
            {
                case "RandomLegal": return new RandomLegalPolicy();
                case "GreedyAggressive": return new GreedyAggressivePolicy();
                case "GreedyDefensive": return new GreedyDefensivePolicy();
                case "Lookahead2": return new Lookahead2Policy();
                case "ProtectTheFront": return new ProtectTheFrontPolicy();
                default: return null;
            }
        }
    }
}
