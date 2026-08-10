using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Stage
{
    // The on-disk shape of Resources/StanceManifest.json, in the same
    // Raw* -> Resolver -> Resolved* arrangement the content entries use.
    //
    // Public mutable fields with no properties, because JsonUtility only
    // populates fields -- see RawEnemyEntry for the same constraint. Numbers
    // are deliberately NOT nullable: JsonUtility cannot express absence, so
    // "unset" has to be an in-band sentinel rather than null, and the
    // resolver below is what turns a sentinel into the documented default.
    [Serializable]
    public class RawStanceManifest
    {
        public List<RawStanceActor> actors = new List<RawStanceActor>();
    }

    [Serializable]
    public class RawStanceActor
    {
        public string spritePath = "";

        // Pixels from the canvas's bottom edge up to the ground the figure
        // stands on. Float rather than int because a measured value can land
        // between two pixels and rounding it would move every figure that
        // uses it -- which is the exact class of thing this file exists to
        // stop happening by accident.
        public float groundLine;

        public List<RawStanceTiming> stances = new List<RawStanceTiming>();
    }

    [Serializable]
    public class RawStanceTiming
    {
        public string stance = "";

        // Zero means "unset, take the default" for all three. A real
        // animation cannot run at 0 seconds per frame or impact on frame 0
        // (frames are 1-based), so the sentinel can never collide with a
        // value someone meant to author.
        public float secondsPerFrame;
        public int impactFrame;
        public int soundFrame;
    }
}
