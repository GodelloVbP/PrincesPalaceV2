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
        //
        // RETURNS WHETHER THE ITEM WAS ACTUALLY SPENT, and it was void. Core
        // removed the stack from the stockpile and saved regardless of what
        // happened in here, which was harmless for as long as every press did
        // something -- and stopped being harmless the moment a pool could
        // refuse a mana potion outright. A caller that deletes an item has to
        // be told whether the item was used.
        //
        // FALSE IS A REFUSAL, NOT A FAILURE: no beat, no message, no turn. The
        // row the press came from is greyed for the same reason by the same
        // predicate (FightHudModel.ItemRefusedBy), so reaching here at all
        // means something upstream is not asking -- and a refusal that still
        // cost the turn would be the worse of the two answers.
        //
        // It is deliberately NOT the same thing as "restored 0". Drinking at
        // full health restores 0, spends the potion and costs the turn, and
        // says "nothing changes" -- the player chose to do that and the potion
        // genuinely could have worked. A pool that refuses mana outright is a
        // press that could never have worked.
        //
        // A FAKE (a caravan fake, `isFake`) IS USED UP AND DOES NOTHING: the
        // same beat and stance, the turn spent, no restore and no number, and
        // the log says why. It is refused by exactly the rule a genuine copy
        // is, first, so a greyed row or a refusal never tells the two apart.
        public const string FakeConsumableLine = "This is of such poor quality... it's a fake.";

        public bool UseConsumable(string displayName, int amount, bool restoresMana, bool isFake = false)
        {
            var actor = Current;
            if (actor == null) return false;

            if (FightHudModel.ItemRefusedBy(restoresMana, actor.PrimaryPool)) return false;

            BeginBeat(actor, actor);
            SetStance(actor, Stances.Cast);

            if (isFake)
            {
                string used = string.IsNullOrEmpty(displayName) ? "something" : displayName;
                AppendMessage($"{actor.Name} uses {used}.");
                AppendMessage(FakeConsumableLine);
                CommitBeat();
                AdvanceAfterAction();
                return true;
            }

            // Clamped by CombatMath, so a 200-point potion on a hero missing 30
            // health restores 30 and says 30 -- the number the player sees is
            // the number that happened.
            int before = restoresMana ? actor.CurrentMana : actor.CurrentHealth;

            // HealAndCount rather than CombatMath.Heal so a potion lands in the
            // healing column. Mana deliberately does not: restoring mana is not
            // healing, and folding the two would make a support character's
            // headline number depend on which resource they topped up.
            //
            // RestoreMana REFUSES a primary pool that does not take mana
            // effects, restoring 0 -- so a mana potion drunk by a character
            // whose resource is earned rather than poured falls through to
            // the "nothing changes" line below on its own, with no second
            // check here. See CombatMath.RestoreMana.
            if (restoresMana) CombatMath.RestoreMana(actor, amount);
            else HealAndCount(actor, amount);

            int restored = (restoresMana ? actor.CurrentMana : actor.CurrentHealth) - before;

            // The pool NAMES ITSELF rather than the line saying "mana": for
            // the shipped roster that prints the same word it always did, and
            // for anyone else it prints theirs.
            string unit = restoresMana
                ? (actor.PrimaryPool?.DisplayName ?? "").ToLowerInvariant()
                : "health";

            string what = string.IsNullOrEmpty(displayName) ? "something" : displayName;
            AppendMessage(restored > 0
                ? $"{actor.Name} uses {what} and recovers {restored} {unit}."
                : $"{actor.Name} uses {what}, and nothing changes.");

            // Recorded as a heal so the floating number comes up green rather
            // than red -- the one visual difference between being helped and
            // being hit.
            RecordBeatAmount(restored, isHealing: true);

            CommitBeat();
            AdvanceAfterAction();
            return true;
        }
    }
}
