namespace PrincesPalace.Domain.Combat.Session
{
    // HOW HARD A LANDED BLOW SHOULD READ ON THE STAGE, as a pure function of
    // what the beat already knows -- no Unity, no animator, nothing that
    // needs a scene to exercise. FightBeatPlayer.RecoilOne used to slide
    // every struck figure exactly RecoilDistance (45px) regardless of
    // whether the hit was a graze or a killing blow; this is the table that
    // replaces the one-size answer, and the player only ever maps a Tier to
    // a distance and a dwell -- the classification itself lives here so an
    // EditMode test can pin it without spinning up a fight.
    //
    // A FRACTION OF THE TARGET'S OWN MAX HEALTH, not a flat number: a 40
    // point hit is a graze on a 1000-HP boss and half the bar on a 80-HP
    // rat, and the whole point of "distinguish impact strength" is that the
    // STAGE should agree with what the HP bar just showed.
    public static class HitStrength
    {
        public enum Tier
        {
            // A miss, or a hit a shield ate whole -- nothing to sell with a
            // slide. The barrier flash (where wired) or nothing at all is
            // the whole reaction; RecoilOne skips the animator call outright.
            None,

            Light,
            Medium,
            Heavy,
        }

        // ---- the fraction bands ------------------------------------------------
        //
        // PINNED LITERALS, per this pass's own brief ("0.05-0.20 -> medium")
        // and CLAUDE.md gotcha 5 -- these are the spec, not derived from
        // anything.
        public const float LightCeiling = 0.05f;
        public const float MediumCeiling = 0.20f;

        // ---- the table ----------------------------------------------------------
        //
        // HEAVY'S 45px IS THE PRE-EXISTING RecoilDistance, restated here as
        // the table's own literal rather than read off FightBeatPlayer, so
        // this file stays free of a Core reference and a change to one
        // cannot silently drift from the other without a test noticing (see
        // HitStrengthTests). Light and medium are new judgement calls, sized
        // to read as a nudge and a shove against that same 45px anchor.
        public const float LightDistance = 12f;
        public const float MediumDistance = 28f;
        public const float HeavyDistance = 45f;

        // Dwell, AS A FRACTION OF THE EXISTING HEAVY DWELL (FightBeatPlayer.
        // RecoilDwellBeats) rather than a second authored beat-count: heavy
        // keeps the reeling behaviour that already shipped (owner,
        // 2026-09-19) byte for byte, and a light tap reading the stage for
        // barely a third as long is what makes it read as lighter rather
        // than merely shorter.
        //
        // A SHORTER DWELL HERE ONCE RACED FightController.StageVisuals.cs's
        // stage reformation (StageFormationTests.TheSurvivorClosesUpOnlyOnce
        // TheCorpseHasFinishedFading) -- HoldsRank used to read a
        // StageDeathFade's own Faded flag live, which an ordinary beat's
        // repaint and the fade's own Finished callback could both observe
        // on the same frame with no ordering guarantee between them. Fixed
        // at THAT root (HoldsRank now reads an explicit, one-shot
        // _corpseGone flag written only by the fade's own notification --
        // see its own header), not by padding these fractions back up, so
        // the dwell here is free to be the full distinction the brief asked
        // for.
        public const float LightDwellFraction = 0.35f;
        public const float MediumDwellFraction = 0.65f;
        public const float HeavyDwellFraction = 1f;

        // ONE CLASSIFICATION, EVERY CALLER. FightBeatPlayer's single-target
        // path and its per-enemy sweep path both funnel through this rather
        // than each writing its own version of "how hard was that" -- the
        // sweep path has no per-target Absorbed today (BeatTargetResult
        // carries no such field -- a known gap, see this pass's own
        // report), so it always passes 0 there and still gets a sensible
        // answer: nothing absorbed reads as "landed for `amount`", which is
        // exactly what it already meant before this table existed.
        //
        // `missed`: no slide, ever -- a dodge has nothing to react to.
        // `absorbed` + zero health damage: fully absorbed, no slide -- the
        // barrier is the whole story, not a hit that happened to deal zero.
        // `killed`: always Heavy, on top of whatever the fraction says --
        // the existing defeated handling (FadeTheFallen and friends) is
        // downstream of the stage reacting like the blow that landed it.
        public static Tier Classify(int amount, int absorbed, int targetMaxHealth, bool missed, bool killed)
        {
            if (missed) return Tier.None;

            int healthDamage = amount - absorbed;
            if (healthDamage < 0) healthDamage = 0;

            if (absorbed > 0 && healthDamage == 0) return Tier.None;

            if (killed) return Tier.Heavy;

            // No denominator to read a fraction against -- Heavy rather than
            // None, so a target with an unset/zero max health (a fixture,
            // never a real fight) still reacts to a real hit rather than
            // silently going limp.
            if (targetMaxHealth <= 0) return Tier.Heavy;

            float fraction = healthDamage / (float)targetMaxHealth;
            if (fraction < LightCeiling) return Tier.Light;
            if (fraction < MediumCeiling) return Tier.Medium;
            return Tier.Heavy;
        }

        public static float DistanceFor(Tier tier)
        {
            switch (tier)
            {
                case Tier.Light: return LightDistance;
                case Tier.Medium: return MediumDistance;
                case Tier.Heavy: return HeavyDistance;
                default: return 0f;
            }
        }

        public static float DwellFractionFor(Tier tier)
        {
            switch (tier)
            {
                case Tier.Light: return LightDwellFraction;
                case Tier.Medium: return MediumDwellFraction;
                case Tier.Heavy: return HeavyDwellFraction;
                default: return 0f;
            }
        }
    }
}
