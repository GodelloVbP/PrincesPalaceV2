using UnityEngine;

namespace PrincesPalace.Core.Rig
{
    // Carries rig.json data a runtime consumer needs but the prefab's own
    // Transform/SpriteRenderer hierarchy doesn't otherwise expose. Set once
    // by RigPrefabBuilder at build time; read by whatever positions/scales
    // a rig instance at runtime (FightController.StageVisuals.cs) so the
    // two never have to agree on a duplicated literal.
    public class RigMeta : MonoBehaviour
    {
        // The bind pose's own tight content height, in SOURCE image pixels
        // (rig.json's own "referenceHeightPx", same value RigPrefabBuilder
        // used to place every bone). Combined with RigLibrary.ScaleFor,
        // this is what lets a rig instance be scaled to match whatever
        // on-screen height its folder is calibrated to, instead of
        // whatever size the bind-pose source art happened to be drawn at.
        public float ReferenceHeightPx;
    }
}
