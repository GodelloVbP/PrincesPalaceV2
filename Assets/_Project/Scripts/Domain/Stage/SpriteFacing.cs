using System;

namespace PrincesPalace.Domain.Stage
{
    // Which way a piece of character art is drawn in its SOURCE file.
    //
    // There is no project-wide "all art faces left" convention to rely on:
    // the two enemy sheets authored so far (Bog Witch, Stone Golem) both
    // face RIGHT, while Shawn's dialogue portraits face LEFT. Assuming a
    // single global direction would mirror one of those groups backwards —
    // a monster turning its back on the party it is attacking. So facing is
    // authored per sprite set and the mirror decision is derived, never
    // assumed.
    public enum SpriteFacing
    {
        Left,
        Right,
    }

    // Which half of the stage a combatant stands on. Determines the
    // direction they should LOOK — always inward, toward the opposition.
    public enum StageSide
    {
        Left,
        Right,
    }

    public static class StageFacing
    {
        // A combatant faces inward: someone on the left looks right, someone
        // on the right looks left. This is the whole rule, in one place, so
        // no call site re-derives it and gets it inverted.
        public static SpriteFacing DesiredFacingFor(StageSide side)
        {
            return side == StageSide.Left ? SpriteFacing.Right : SpriteFacing.Left;
        }

        // True when the art has to be horizontally mirrored to face the way
        // its side requires. Callers apply this as a negative x scale, the
        // same technique AddArrowIcon uses to reuse one arrow both ways.
        public static bool ShouldMirror(SpriteFacing nativeFacing, StageSide side)
        {
            return nativeFacing != DesiredFacingFor(side);
        }

        // Convenience for the actual transform value, so no call site has to
        // remember that "mirrored" means -1 rather than, say, 180 degrees of
        // Y rotation (which would also flip it, but breaks the bottom-centre
        // pivot the depth scaling depends on).
        public static float MirrorScaleX(SpriteFacing nativeFacing, StageSide side)
        {
            return ShouldMirror(nativeFacing, side) ? -1f : 1f;
        }

        // Parses the authored string from enemies.json. Deliberately strict
        // rather than silently defaulting: a typo'd facing would otherwise
        // show up as a monster facing the wrong way, which reads as an art
        // bug and would send someone hunting through the sprite sheets
        // instead of the content file.
        public static bool TryParse(string value, out SpriteFacing facing)
        {
            facing = SpriteFacing.Right;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "left":
                    facing = SpriteFacing.Left;
                    return true;
                case "right":
                    facing = SpriteFacing.Right;
                    return true;
                default:
                    return false;
            }
        }
    }
}
