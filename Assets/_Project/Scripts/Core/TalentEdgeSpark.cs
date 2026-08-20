using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // A bright dot that runs end to end along its edge, looping -- the energy
    // FLOWING through the line, where TalentEdgeCrackle is the line crackling
    // in place. The two are separate on purpose: flicker without travel reads
    // as a faulty bulb, travel without flicker reads as a loading bar.
    //
    // PARENTED TO THE EDGE'S GLOW, which is already rotated and centred on the
    // edge's midpoint, so this only ever moves along its own LOCAL X and the
    // parent's rotation carries it down the real line for free. Doing it in
    // world space instead would mean re-deriving the edge's angle here, which
    // is the sort of second copy that drifts the moment a layout constant moves.
    //
    // It also means an unlit edge has no spark without this having to know
    // anything about investment: the glow is switched off, and its children go
    // with it.
    //
    // Migrated from v1, on unscaled time for the same reason the crackle is.
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class TalentEdgeSpark : MonoBehaviour
    {
        // ONE PERIOD FOR EVERY EDGE, whatever its length -- so the spark on a
        // long branch connector visibly moves faster than one on a short grid
        // link, rather than every edge crawling at a shared pixel rate. Current
        // through a longer wire is the same current.
        private const float PeriodSeconds = 1.1f;

        // SERIALIZED, and that is not optional.
        //
        // SetLength is called at BUILD time by the wiring step, and a plain
        // private field is not written into the scene -- so at runtime this
        // came back as 0, Lerp(-0, 0, t) is 0, and every spark sat perfectly
        // still in the middle of its edge. The component was present, enabled
        // and running; it was animating a distance of nothing.
        //
        // Nothing could see it either: the emitted tree has the node, the
        // wiring sweep has the reference, and only asking the running scene
        // whether the thing MOVES catches it. v1 has the same field and the
        // same omission.
        [SerializeField] private float _halfLength;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        // Told the edge's own length at wiring time rather than measuring the
        // parent: the glow's rect is the SIZE of the lit layer, and reading it
        // back would tie the travel to a cosmetic width.
        public void SetLength(float length)
        {
            _halfLength = length * 0.5f;
        }

        private void Update()
        {
            if (_rect == null) return;

            float t = Mathf.Repeat(Time.unscaledTime / PeriodSeconds, 1f);

            var position = _rect.anchoredPosition;
            position.x = Mathf.Lerp(-_halfLength, _halfLength, t);
            _rect.anchoredPosition = position;
        }
    }
}
