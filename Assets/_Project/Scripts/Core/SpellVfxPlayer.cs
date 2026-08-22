using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Plays a spell's frame sequence over the fight stage.
    //
    // ONE Image, re-pointed frame by frame, rather than a sprite pool or an
    // Animator. There is no prefab to clone in a generated scene, and an
    // Animator would put the timing in an asset instead of next to the spell
    // that owns it -- a spell's vfxSeconds is authored in skills.json beside its
    // damage, which is where someone tuning the spell is looking.
    //
    // Frames are discovered and cached by FrameSequenceLoader, shared with
    // StanceAnimationLibrary.
    public class SpellVfxPlayer : MonoBehaviour
    {
        [SerializeField] internal Image image;

        // The dissolve layer: a child of `image`, holding the NEXT frame and
        // fading up over the current one. See FightScreen.BuildSpellVfx.
        [SerializeField] internal Image fade;

        // HOW MUCH OF EACH FRAME'S TIME IS SPENT DISSOLVING.
        //
        // Not all of it, and the number matters. A frame that cross-fades from
        // the moment it appears is never itself: at 0.5 through, two drawings
        // are on screen at half strength each and neither is legible, which for
        // a lightning bolt means two bolts. Holding for the first 55% and
        // dissolving over the last 45% keeps every frame readable and removes
        // the step between them, which is the whole complaint.
        private const float DissolveFraction = 0.45f;

        private Coroutine _playing;

        public bool IsPlaying => _playing != null;

        // Read-only view, for the tests that assert the effect starts silent and
        // never eats a click. Read-only rather than exposing the field: nothing
        // outside this class has any business swapping the Image.
        public Image Image => image;

        // WHICH WAY THE SHEET FIRES. 1 as drawn, -1 mirrored.
        //
        // Only a travelling effect has a direction at all -- a flare or a
        // ground spike is symmetrical about the thing it lands on and mirroring
        // it changes nothing. A beam is not: the same spell cast back across
        // the stage has to run the other way, and left as drawn its glyph forms
        // on the victim and its impact lands on the caster.
        //
        // Set on the RECT rather than on the sprite, so it costs nothing and
        // works for any sheet.
        public void SetFacing(float sign)
        {
            if (image == null) return;

            float x = sign < 0f ? -1f : 1f;
            var scale = image.rectTransform.localScale;
            image.rectTransform.localScale = new Vector3(x, scale.y, scale.z);
        }

        // Set from the controller so the effect lands ON the thing it hit
        // rather than in the middle of the screen.
        public void PlayAt(string vfxPath, float seconds, Vector2 anchoredPosition, Vector2 size) =>
            PlayFrom(vfxPath, seconds, anchoredPosition, anchoredPosition, size, 0, 0);

        // AN EFFECT THAT TRAVELS, arriving by `arriveAtFrame` and staying put
        // for the rest of the sequence.
        //
        // The alternative that was tried first and abandoned: stretch one box
        // from the caster to the target and let the sheet fill it. It cannot
        // work with this art, and the measurement says why. mud_blast's glyph
        // sits at 29% of the cell's width and its impact at 62%, so the piece
        // of the sheet that has to reach from caster to target is a third of
        // the sheet. Spanning a 615px gap therefore needs an 1863px box, which
        // stretches a round glyph into an ellipse three and a half times too
        // wide; and any box narrower than that lands the impact short of the
        // thing it is supposed to be hitting.
        //
        // Moving a correctly-sized box is the same idea without the distortion,
        // and it is what the sequence was already drawn for: the glyph turns
        // where it was cast, the lance leaves, and the burst is at the target
        // by the frame the damage lands.
        public void PlayFrom(string vfxPath, float seconds, Vector2 from, Vector2 to, Vector2 size,
            int departAtFrame, int arriveAtFrame)
        {
            if (image == null) return;

            var frames = Frames(vfxPath);
            if (frames == null || frames.Length == 0) return;

            // A second cast before the first finished RE-STARTS it rather than
            // overlapping: two copies of the same effect on one target reads as
            // a rendering bug, not as two casts.
            if (_playing != null) StopCoroutine(_playing);

            // The pool node is built inactive -- there is nothing to show until
            // something is cast -- and StartCoroutine on an inactive GameObject
            // is a hard error rather than a no-op. Same wake-up the hit flash
            // needs, for the same reason.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchoredPosition = from;
            rect.sizeDelta = size;
            _playing = StartCoroutine(PlayRoutine(frames, seconds, from, to, departAtFrame, arriveAtFrame));
        }

        public void StopImmediately()
        {
            if (_playing != null)
            {
                StopCoroutine(_playing);
                _playing = null;
            }

            if (image != null) image.enabled = false;
            if (fade != null) fade.enabled = false;
        }

        private void OnDisable()
        {
            // An abandoned fight must not leave an effect frozen mid-frame, the
            // same rule FightBeatPlayer.Flush follows for the popups.
            StopImmediately();
        }

        // Exposed so a test can assert a spell's frames actually load, rather
        // than discovering a bad path by seeing nothing happen. Discovery and
        // caching belong to FrameSequenceLoader -- v1 had this cache
        // per-INSTANCE while its two siblings were static, so every fight-scene
        // reload re-probed every spell folder from disk for no reason.
        public Sprite[] Frames(string vfxPath) => FrameSequenceLoader.Load(vfxPath);

        // TICKED EVERY FRAME, NOT STEPPED EVERY 50ms.
        //
        // This waited out each frame's whole duration and then swapped the
        // sprite, which is a slideshow: the drawing sits still, jumps, sits
        // still. Two things come out of running it per frame instead -- the
        // dissolve above, and a flight path that is continuous rather than
        // fourteen discrete hops.
        //
        // REAL TIME STILL, through FightBeatPlayer.Scaled, so a spell cannot
        // outlast the turn that cast it when a test runs the fight at 60x.
        private IEnumerator PlayRoutine(Sprite[] frames, float seconds,
            Vector2 from, Vector2 to, int departAtFrame, int arriveAtFrame)
        {
            float total = Mathf.Max(0.001f, FightBeatPlayer.Scaled(seconds));
            float perFrame = total / frames.Length;

            // Clamped to the sequence it is travelling across: an arrival frame
            // past the end would leave the effect still in flight when it stops
            // being drawn.
            int arrival = Mathf.Clamp(arriveAtFrame, 0, frames.Length - 1);

            // AND IT DOES NOT LEAVE UNTIL THE CHARGE IS DONE. Departure was
            // frame zero, so a sheet with a wind-up spent that wind-up already
            // moving -- mud_blast's glyph turned its eight charging revolutions
            // while halfway across the stage, which reads as tumbling rather
            // than as charging. Held below the arrival so the two can never
            // cross and produce a negative flight.
            int departure = Mathf.Clamp(departAtFrame, 0, arrival);

            var rect = image.rectTransform;
            image.enabled = true;

            // MEASURED FROM A START STAMP, not accumulated.
            //
            // `elapsed += Time.unscaledDeltaTime` at the end of the body charges
            // the effect's FIRST advance a delta measured before it existed --
            // the frame it was started in, whose length has nothing to do with
            // it. After a scene load or a long beat that is a tenth of a second
            // the sequence never had, spent before its first drawing is seen.
            // It also drifts: twenty-six additions of a float are not the sum
            // anyone intended.
            float started = Time.realtimeSinceStartup;

            float elapsed = 0f;
            while (elapsed < total)
            {
                // Fractional frame: 3.4 is "three tenths of the way from frame
                // 3 into frame 4", which is what both the dissolve and the
                // flight read.
                float at = Mathf.Min(elapsed / perFrame, frames.Length - 0.0001f);
                int index = (int)at;
                float within = at - index;

                image.sprite = frames[index];

                bool hasNext = index + 1 < frames.Length;
                float blend = within <= 1f - DissolveFraction
                    ? 0f
                    : (within - (1f - DissolveFraction)) / DissolveFraction;

                if (fade != null)
                {
                    fade.enabled = hasNext && blend > 0f;
                    if (fade.enabled)
                    {
                        fade.sprite = frames[index + 1];
                        var c = fade.color;
                        fade.color = new Color(c.r, c.g, c.b, blend);
                    }
                }

                if (arrival > departure)
                {
                    // EASED IN over the flight window ALONE, so it leaves fast
                    // and hard rather than being dragged the whole way. Before
                    // the departure frame it has not left: the lerp reads zero
                    // and the effect sits exactly where it was cast.
                    float t = Mathf.Clamp01((at - departure) / (arrival - departure));
                    rect.anchoredPosition = Vector2.Lerp(from, to, t * t);
                }

                yield return null;
                elapsed = Time.realtimeSinceStartup - started;
            }

            if (arrival > 0) rect.anchoredPosition = to;

            image.enabled = false;
            if (fade != null) fade.enabled = false;
            _playing = null;
        }
    }
}
