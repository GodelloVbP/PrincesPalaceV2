namespace PrincesPalace.Domain.Combat.Session
{
    // Which moment in a fight a voice line belongs to.
    //
    // Moved out of Core's CharacterVoice because the DECISION -- "this blow
    // earned a Hurt line, and this one a LowHealth line" -- is made while
    // combat resolves, which is Domain. What stays in Core is the catalog: the
    // actual clip paths per character, and the fall back to Hurt when a
    // character has no take for a line. That split matters because the decision
    // has to happen at RECORD time: live health has already moved on by the
    // time a beat plays, so asking "are they low?" during playback would answer
    // for the end of the round rather than for this blow.
    public enum VoiceLine
    {
        Hurt,
        Down,
        LowHealth,
        Victory,

        // What the ATTACKER says landing a blow, not what the target says
        // taking one -- keyed off the actor of a beat rather than its target,
        // the only line here that is.
        AttackHit,
    }

    public static class VoiceThresholds
    {
        // At or below this fraction of max health, a hurt line becomes a
        // low-health line. Consumed when the beat is recorded, for the reason
        // in VoiceLine's own comment.
        public const float LowHealthFraction = 0.3f;

        public static bool IsLowHealth(int currentHealth, int maxHealth)
        {
            if (maxHealth <= 0) return false;
            return currentHealth > 0 && currentHealth <= maxHealth * LowHealthFraction;
        }
    }
}
