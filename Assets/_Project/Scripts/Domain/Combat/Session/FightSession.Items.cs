namespace PrincesPalace.Domain.Combat.Session
{
    // Using something out of the satchel.
    //
    // The satchel's UI has existed since the fight screen was rebuilt and has
    // never had anything in it: FightController.Bind's third argument was never
    // supplied and its ItemUsed event had no subscriber. This is the command
    // the other end of that wire was missing.
    public sealed partial class FightSession
    {
        // A potion, drunk.
        //
        // `restoresMana` rather than an ItemEffect: that enum is Core content
        // and this is Domain. Core translates at the one place it already
        // builds a SatchelStack, which is the same boundary and the same shape.
        //
        // COSTS THE TURN, like every other action. A free heal would make the
        // item column strictly better than defending, and every fight would
        // open with the whole satchel.
        public void UseConsumable(string displayName, int amount, bool restoresMana)
        {
            var actor = Current;
            if (actor == null) return;

            BeginBeat(actor, actor);
            SetStance(actor, Stances.Cast);

            // Clamped by CombatMath, so a 200-point potion on a hero missing 30
            // health restores 30 and says 30 -- the number the player sees is
            // the number that happened.
            int before = restoresMana ? actor.CurrentMana : actor.CurrentHealth;

            // HealAndCount rather than CombatMath.Heal so a potion lands in the
            // healing column. Mana deliberately does not: restoring mana is not
            // healing, and folding the two would make a support character's
            // headline number depend on which resource they topped up.
            if (restoresMana) CombatMath.RestoreMana(actor, amount);
            else HealAndCount(actor, amount);

            int restored = (restoresMana ? actor.CurrentMana : actor.CurrentHealth) - before;

            string what = string.IsNullOrEmpty(displayName) ? "something" : displayName;
            AppendMessage(restored > 0
                ? $"{actor.Name} uses {what} and recovers {restored} {(restoresMana ? "mana" : "health")}."
                : $"{actor.Name} uses {what}, and nothing changes.");

            // Recorded as a heal so the floating number comes up green rather
            // than red -- the one visual difference between being helped and
            // being hit.
            RecordBeatAmount(restored, isHealing: true);

            CommitBeat();
            AdvanceAfterAction();
        }
    }
}
