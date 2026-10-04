using System;

namespace PrincesPalace.Domain.Content
{
    // A SPELL'S CLAIM ON THE STAGE'S DARKNESS. Not a layer: a layer is a
    // drawing placed somewhere and gloom is placed nowhere, it is one more
    // input to the single roundDarkness image an event already drives. So it
    // is a block on the presentation beside layers, read by the cast clock the
    // way hitCueSeconds is.
    //
    // All times are AUTHORED seconds on the cast's clock, so the battle-speed
    // preset divides them with everything else a cast draws.
    //
    // Strength 0 is "no gloom" (the default), which keeps every shipped spell
    // inert.
    [Serializable]
    public class SpellGloom
    {
        [ContentDoc("How dark the spell makes the stage at most, 0-1 (the opacity of the stage's darkness image). 0 means no gloom. Shown as the larger of this and the event's own darkness, never added to it.")]
        public float strength;

        [ContentDoc("Seconds from the cast opening to full strength.")]
        public float fadeIn;

        [ContentDoc("When the gloom starts to lift: hit (the payoff cue) or cleared (when the cast's last layer is gone). Blank means hit.")]
        public string until = "";

        [ContentDoc("Seconds the gloom takes to lift once released.")]
        public float fadeOut;

        public bool HasGloom => strength > 0f;

        public SpellGloomEnd Until => SpellGloomEndNames.Parse(until);

        public SpellGloom Copy() => new SpellGloom
        {
            strength = strength,
            fadeIn = fadeIn,
            until = until,
            fadeOut = fadeOut,
        };

        // The gloom's displayed opacity at `seconds` into the cast. A payoff
        // that arrives before full strength lifts from wherever the rise had
        // got to, and the rise stops there instead of carrying on upward.
        public float AlphaAt(float seconds, float hitCueSeconds, float clearedSeconds) =>
            AlphaAtRelease(seconds, ReleaseSeconds(hitCueSeconds, clearedSeconds));

        // The same, for a caller that resolved the release instant once.
        public float AlphaAtRelease(float seconds, float release)
        {
            if (!HasGloom || seconds < 0f) return 0f;

            float level = Rise(Math.Min(seconds, release));
            if (seconds < release) return strength * level;

            float lift = fadeOut > 0f ? Math.Max(0f, 1f - (seconds - release) / fadeOut) : 0f;
            return strength * level * lift;
        }

        private float Rise(float seconds) => fadeIn > 0f ? Math.Min(1f, seconds / fadeIn) : 1f;

        public float ReleaseSeconds(float hitCueSeconds, float clearedSeconds) =>
            Until == SpellGloomEnd.Cleared ? clearedSeconds : hitCueSeconds;

        // When the cast has nothing left of this on the stage.
        public float EndSeconds(float hitCueSeconds, float clearedSeconds) =>
            EndSecondsAt(ReleaseSeconds(hitCueSeconds, clearedSeconds));

        public float EndSecondsAt(float release) => HasGloom ? release + Math.Max(0f, fadeOut) : 0f;
    }

    public enum SpellGloomEnd
    {
        // The payoff: lifted on the blow.
        Hit,

        // Held through the residue, lifted when the cast's last layer is gone.
        Cleared,
    }

    public static class SpellGloomEndNames
    {
        public static SpellGloomEnd Parse(string name) =>
            SpellWord.Is(name ?? "", "cleared") ? SpellGloomEnd.Cleared : SpellGloomEnd.Hit;

        public static bool IsKnown(string name) =>
            string.IsNullOrWhiteSpace(name) || SpellWord.Is(name, "hit") || SpellWord.Is(name, "cleared");

        public static string[] All => new[] { "hit", "cleared" };
    }

    // The stage's darkness has one owner, the roundDarkness image; an event
    // and a spell are two inputs to it and the larger wins. Never a sum, so a
    // spell cannot darken a stage the event already darkened more.
    public static class StageDarkness
    {
        public static float Compose(float eventAlpha, float spellGloom) =>
            Math.Max(Math.Max(0f, eventAlpha), Math.Max(0f, spellGloom));
    }
}
