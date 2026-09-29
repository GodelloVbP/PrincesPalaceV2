namespace PrincesPalace.Domain.Combat.Session
{
    // CRITICAL HITS, the session's half (PLAN_BJORN_CONSTELLATIONS Phase 1).
    // CritRules owns the rule and DamagePipeline applies it; this owns the two
    // things only the session knows: which action is AUTHORED to crit, and
    // how a landed crit is shown (the beat flag and the log line).
    //
    // AN AUTHORED CRIT belongs to the ACTION, not to one pipeline call: an
    // enemy's real skill resolves through the same ResolveCharacterSkill tree
    // a party cast does (single, sweep, packet arms), and threading one flag
    // through every arm is how one of them would forget it. So the enemy turn
    // names the actor whose current action crits (_authoredCritActor, set and
    // cleared around the resolution in ResolveEnemyAction), and every damage
    // call site asks CritCallFor(actor) -- true for that actor, null ("roll
    // it") for everyone else. A party member is never named here; nothing
    // authors a party crit today.
    public sealed partial class FightSession
    {
        public const string CritLine = "A critical hit!";

        private CombatantState _authoredCritActor;

        // What a damage call site hands DamagePipeline as `crit`.
        private bool? CritCallFor(CombatantState actor) =>
            actor != null && ReferenceEquals(actor, _authoredCritActor) ? true : (bool?)null;

        // ONE decision for a whole multi-packet cast, for the call sites that
        // resolve the swing themselves (dodgeAlreadyResolved) -- the authored
        // call when there is one, otherwise one roll on the fight's own rng.
        private bool ResolveCrit(CombatantState actor) =>
            CritCallFor(actor) ?? DamagePipeline.RollCrit(actor, _rng);

        // A single-target blow landed as a crit: flag the beat for the view
        // and say so in the log, ahead of the damage line.
        private void NoteCrit()
        {
            if (_recordingBeat != null) _recordingBeat.Crit = true;
            AppendMessage(CritLine);
        }

        // A sweep reports each target in one summary line, so its crits are
        // said per target, inline ("Goblin takes 30 (critical)").
        private static string CritSuffix(bool crit) => crit ? " (critical)" : "";
    }
}
