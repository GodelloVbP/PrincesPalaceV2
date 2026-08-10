using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Assigns the shared runtime-generated soft-glow sprite on Awake, so
    // SceneBuilder only has to add this component rather than reference an
    // asset that does not exist on disk (see ProceduralSprites).
    //
    // [ExecuteAlways] so this ALSO runs in Edit Mode -- specifically for
    // ScreenshotTool, which renders a panel straight from the scene
    // SceneBuilder authored without ever entering Play Mode (Play Mode
    // itself hangs in this environment; see that tool's own header comment).
    // Pure one-shot sprite assignment with no per-frame state, so running it
    // outside Play Mode carries none of the risk a real animation would.
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class RadialGlowImage : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Image>().sprite = ProceduralSprites.RadialGlow();
        }
    }
}
