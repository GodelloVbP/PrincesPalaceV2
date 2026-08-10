using UnityEngine;
using PrincesPalace.Domain.Ambience;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Loops a hub building's frame sequence.
    //
    // The talents tree ships three frames -- its orbs light and dim -- and only
    // f0 has ever been drawn. Two authored frames of animation have been dead
    // weight in the project since the art landed.
    //
    // f0 stays the BAKED resting frame: SceneBuilder writes it into the scene,
    // so an Edit-Mode screenshot and a scene that never ticks both show a
    // sensible building rather than nothing. This only takes over at runtime.
    //
    // Silently does nothing for a single-frame building, which is four of the
    // five -- so it can be attached to all of them without a per-building list
    // that would need maintaining every time art gains a frame.
    [RequireComponent(typeof(Image))]
    public class HubBuildingLooper : MonoBehaviour
    {
        // Where the frames live, Resources-relative. Set by the dressing step
        // from the same key the tree declared, so the animation cannot end up
        // pointing at a different building than the sprite does.
        public string FramesFolder;

        public float SecondsPerFrame = 0.55f;

        // Spread per building so two animated buildings never step together.
        public float PhaseSeconds;

        private Image _image;
        private Sprite[] _frames;

        // Forwarder. The maths is AmbienceCurves.LoopFrameAt, in Domain, where
        // the EditMode suite can actually reach it.
        public static int FrameAt(float elapsed, int frameCount, float secondsPerFrame) =>
            AmbienceCurves.LoopFrameAt(elapsed, frameCount, secondsPerFrame);

        private void Awake()
        {
            _image = GetComponent<Image>();
            _frames = FrameSequenceLoader.Load(FramesFolder);
        }

        private void Update()
        {
            // One frame is a still building, not a broken animation.
            if (_frames == null || _frames.Length <= 1) return;

            _image.sprite = _frames[FrameAt(Time.time + PhaseSeconds, _frames.Length, SecondsPerFrame)];
        }
    }
}
