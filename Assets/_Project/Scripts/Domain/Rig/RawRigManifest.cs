using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Rig
{
    // The on-disk shape of Resources/Rigs/<folder>/animations.json, in the
    // same Raw* -> Resolver -> Resolved* arrangement RawStanceManifest uses.
    //
    // Public mutable fields with no properties, because JsonUtility only
    // populates fields. Numbers are not nullable for the same reason
    // RawStanceTiming's are not: JsonUtility cannot express absence, so
    // durationSeconds/impactAt/soundAt are read as authored rather than
    // defaulted -- a rig clip with no duration is simply empty (see
    // RigStanceClip.IsEmpty), which is the graceful-degradation posture
    // every other content loader in this project takes on a gap.
    [Serializable]
    public class RawRigManifest
    {
        public List<RawRigClip> clips = new List<RawRigClip>();
    }

    [Serializable]
    public class RawRigClip
    {
        public string stance = "";
        public float durationSeconds;
        public float impactAt;
        public float soundAt;
        public bool loop;
        public List<RawRigTrack> tracks = new List<RawRigTrack>();
    }

    [Serializable]
    public class RawRigTrack
    {
        public string bone = "";
        public List<RawRigKeyframe> keyframes = new List<RawRigKeyframe>();
    }

    [Serializable]
    public class RawRigKeyframe
    {
        public float t;
        public float deg;
    }
}
