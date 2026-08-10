using System;

namespace PrincesPalace.Domain.Combat
{
    // The AUTHORED half: what a transform grants, as typed into skills.json
    // and carried on the SkillDefinition that hands it out.
    //
    // A nested block rather than five more loose fields on SkillDefinition,
    // because these five only ever mean anything together — an authored
    // speedPercent on a skill that does not transform is a typo the resolver
    // can then name, instead of a number sitting quietly in the asset doing
    // nothing. Exactly one skill uses it today (Black Ram Mode); the Lamb's
    // and mage's convergences are the same shape when their strands land.
    [Serializable]
    public class TransformGrant
    {
        // What the combat log and the plate call it while it is running.
        public string displayName = "";

        // Base duration in the holder's own turns, before Wrath's extensions.
        public int turns;

        // Percent bonuses applied on entry and taken back exactly on exit —
        // see Transformation.Enter/Exit for why the deltas are stored rather
        // than recomputed.
        public int attackPercent;
        public int speedPercent;

        // Temporary health as a percent of max, expiring with the transform.
        // Temporary ON PURPOSE: permanent max health would fight the Ram's
        // own low-health generator.
        public int temporaryHealthPercent;

        // What fraction of a landed hit spills onto the enemies either side
        // of the target, while the transform is running. 0 for a transform
        // that does not splash.
        //
        // A property of the TRANSFORM rather than a TalentEffectType,
        // unlike every other rule the Black Ram grants, because it is not
        // something a node hands you — it is part of what the mode IS
        // (handoff §6.4), and it arrives and leaves with the mode. Putting
        // it in the talent vocabulary would have meant a rule that is only
        // ever true while an unrelated system is active, which is exactly
        // the kind of hidden coupling the closed enum is meant to avoid.
        public int splashPercent;

        public bool IsAuthored => turns > 0;
    }

    // A timed transform a combatant is currently under — Shawn's Black Ram
    // Mode today, and the shape the other two paths' convergences will take.
    //
    // Holds the DELTAS it applied rather than the percentages it was asked
    // for. Entering multiplies Attack by 1.5 and Speed by 1.3; leaving has to
    // undo exactly that, and recomputing the inverse from the percentages
    // does not round-trip through integer arithmetic (a 7 Attack becomes 11,
    // and 11/1.5 is 7.33). Storing what was actually added is the only way
    // out that is exact — and it stays exact even if something else modifies
    // Attack while the transform is running.
    //
    // Temporary health is granted the same way and matters more. It is
    // temporary ON PURPOSE (handoff §6.4): it saves him during the window and
    // then drops him straight back into the wounded band where his engine
    // runs hot. Permanent max health would fight his own generator, which is
    // why there are no max-HP nodes anywhere in the reworked tree.
    public sealed class Transformation
    {
        // What the player sees this called. Carried on the instance rather
        // than hardcoded so the Lamb's and the mage's convergences reuse this
        // type rather than each growing a parallel one.
        public readonly string DisplayName;

        // Turns of the HOLDER's own remaining. Counts down at the start of
        // each of their turns, the same unit ActiveStatus.TurnsRemaining uses
        // and for the same reason (see its own comment): anything else makes
        // a transform quietly last longer on a slow combatant.
        public int TurnsRemaining;

        // How much of each stat this transform added, for exact reversal.
        public readonly int AttackBonus;
        public readonly int SpeedBonus;
        public readonly int TemporaryHealth;

        // Percent of a landed hit that spills onto the target's neighbours
        // while this is running — see TransformGrant.splashPercent.
        public readonly int SplashPercent;

        // Turns already added by Wrath T2's on-kill extension, against its
        // own cap. Tracked here rather than on the combatant because it is
        // meaningless outside a running transform and has to reset with it.
        public int ExtensionsGranted;

        // Set once the capstone's health gate has been met during THIS
        // transform. A permanent transform stops ticking down at all rather
        // than being handed a huge duration — a number the UI would then have
        // to render as "47 turns left", which reads as a bug rather than as
        // "this is who he is now".
        public bool IsPermanent;

        public Transformation(string displayName, int turns, int attackBonus, int speedBonus,
            int temporaryHealth, int splashPercent = 0)
        {
            DisplayName = string.IsNullOrEmpty(displayName) ? "Transformed" : displayName;
            TurnsRemaining = Math.Max(1, turns);
            AttackBonus = Math.Max(0, attackBonus);
            SpeedBonus = Math.Max(0, speedBonus);
            TemporaryHealth = Math.Max(0, temporaryHealth);
            SplashPercent = Math.Max(0, splashPercent);
        }

        // Applies the transform's grants to a combatant and hands back the
        // instance to store on them. Both halves in one place so nothing can
        // apply the bonuses without recording how to take them away.
        //
        // Percentages are of the combatant's CURRENT values at the moment of
        // entry, which is the reading that keeps a transform worth taking
        // late in a run: a Ram who has found a better weapon transforms into
        // a proportionally bigger ram.
        public static Transformation Enter(CombatantState combatant, string displayName, int turns,
            int attackPercent, int speedPercent, int temporaryHealthPercentOfMax, int splashPercent = 0)
        {
            int attackBonus = combatant.Attack * Math.Max(0, attackPercent) / 100;
            int speedBonus = combatant.Speed * Math.Max(0, speedPercent) / 100;
            int temporary = combatant.MaxHealth * Math.Max(0, temporaryHealthPercentOfMax) / 100;

            var transformation = new Transformation(displayName, turns, attackBonus, speedBonus, temporary, splashPercent);

            combatant.Attack += transformation.AttackBonus;
            combatant.Speed += transformation.SpeedBonus;
            combatant.MaxHealth += transformation.TemporaryHealth;
            combatant.CurrentHealth += transformation.TemporaryHealth;

            combatant.Transformation = transformation;
            return transformation;
        }

        // Takes every grant back. Current health is clamped to the restored
        // maximum rather than reduced by the granted amount, which is what
        // makes the temporary pool behave like temporary health should: if it
        // was spent absorbing hits it is already gone, and if it was not, the
        // unspent surplus is what evaporates. Either way he lands back in the
        // wounded band, never below where the fight actually left him.
        public static void Exit(CombatantState combatant)
        {
            var transformation = combatant?.Transformation;
            if (transformation == null)
            {
                return;
            }

            combatant.Attack = Math.Max(0, combatant.Attack - transformation.AttackBonus);
            combatant.Speed = Math.Max(0, combatant.Speed - transformation.SpeedBonus);
            combatant.MaxHealth = Math.Max(1, combatant.MaxHealth - transformation.TemporaryHealth);
            combatant.CurrentHealth = Math.Min(combatant.CurrentHealth, combatant.MaxHealth);

            combatant.Transformation = null;
        }
    }
}
