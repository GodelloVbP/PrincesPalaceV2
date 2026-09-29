namespace PrincesPalace.Domain.Combat
{
    // WHAT "CROWD CONTROL" MEANS, in one place (docs/PLAN_BJORN_CONSTELLATIONS.md
    // Phase 4 row 4e). Before this there was no single definition: Stun and
    // Feared shared StatusEffects.HasStun, Rooted had CombatActions.IsLegalFor
    // and Chilled only slowed. Unstoppable and Unyielding both need to ask one
    // question, so it is asked here and nowhere else.
    //
    // Stun, Feared, Rooted and Chilled -- the four statuses that take away a
    // turn, a verb or tempo. Marked, Vulnerable and Provoked are debuffs, not
    // control: they change numbers, never what the holder may do.
    // CrowdControlTests walks every StatusEffectType against this list, so a
    // new member has to be classified by a test rather than by accident.
    public static class CrowdControl
    {
        public static bool IsCrowdControl(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Stun:
                case StatusEffectType.Feared:
                case StatusEffectType.Rooted:
                case StatusEffectType.Chilled:
                    return true;
                default:
                    return false;
            }
        }
    }

    public enum CrowdControlVerdict
    {
        // Not crowd control, or nothing on the holder stops it: apply as ever.
        Lands,
        // Unstoppable was open. Unyielding neither fires nor starts its
        // cooldown (owner decision, plan section 4) -- it stays loaded.
        BlockedByUnstoppable,
        // Unyielding negated the attempt: the surge opened and the cooldown
        // started. The session grants the speed and any Fury.
        NegatedByUnyielding,
    }

    // UNYIELDING'S NUMBERS, per tier (plan section 4). The session reads
    // these; nothing here grants anything.
    public sealed class UnyieldingRule
    {
        public int CooldownTurns;
        public int SurgeTurns;
        public int SpeedPercent;
        public int DamagePercent;
        public int FuryGain;

        public UnyieldingRule(int cooldownTurns, int surgeTurns = 2, int speedPercent = 20,
            int damagePercent = 25, int furyGain = 0)
        {
            CooldownTurns = cooldownTurns;
            SurgeTurns = surgeTurns;
            SpeedPercent = speedPercent;
            DamagePercent = damagePercent;
            FuryGain = furyGain;
        }
    }

    // A COMBATANT'S DEFENCES AGAINST CONTROL, asked once per attempt at the
    // status-application seam (FightSession.RecordStatus). Everyone carries
    // one, closed and empty, and for them Attempt always answers Lands -- the
    // existing Stun/Fear/Root/Chill behaviour is unchanged for anyone the
    // Juggernaut wiring never touches.
    public sealed class CrowdControlGuard
    {
        // Unbroken's second half: "cannot be affected by crowd control".
        public readonly TurnWindow Unstoppable = new TurnWindow();

        // Null = no Unyielding. Set once from talents at fight start.
        public UnyieldingRule Unyielding;
        public readonly TurnWindow UnyieldingCooldown = new TurnWindow();

        // The +speed / +damage window a negation opens.
        public readonly TurnWindow UnyieldingSurge = new TurnWindow();

        // What the damage half of the surge is worth right now -- read by
        // FightSession.AttackBonusFor beside every other attack bonus.
        public int SurgeDamagePercent =>
            UnyieldingSurge.IsOpen && Unyielding != null ? Unyielding.DamagePercent : 0;

        // THE ONE DECISION. Mutates only this guard's own windows (the surge
        // and the cooldown); the session does the rest.
        public CrowdControlVerdict Attempt(StatusEffectType type, bool onHoldersTurn)
        {
            if (!CrowdControl.IsCrowdControl(type)) return CrowdControlVerdict.Lands;

            // FIRST, and it short-circuits Unyielding entirely: under
            // Unstoppable the attempt never reaches the passive.
            if (Unstoppable.IsOpen) return CrowdControlVerdict.BlockedByUnstoppable;

            if (Unyielding != null && !UnyieldingCooldown.IsOpen)
            {
                UnyieldingCooldown.Open(Unyielding.CooldownTurns, onHoldersTurn);
                UnyieldingSurge.Open(Unyielding.SurgeTurns, onHoldersTurn);
                return CrowdControlVerdict.NegatedByUnyielding;
            }

            return CrowdControlVerdict.Lands;
        }
    }
}
