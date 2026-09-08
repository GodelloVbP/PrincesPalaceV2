using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // DRAWS ONE FRAME OF A SPELL, AND OWNS NOTHING ELSE.
    //
    // ONE Image, re-pointed frame by frame, rather than a sprite pool or an
    // Animator. There is no prefab to clone in a generated scene, and an
    // Animator would put the timing in an asset instead of next to the spell
    // that owns it -- a spell's seconds is authored in skills.json beside its
    // damage, which is where someone tuning the spell is looking.
    //
    // WHAT IT NO LONGER OWNS, and why that is the whole point: the clock, the
    // schedule and the flight. This used to run a coroutine per cast off its
    // own wall-clock stamp, which is a second orchestrator -- a scheduler
    // advancing a cast on one clock beside a renderer advancing its frames on
    // another. SpellPerformancePlayer owns both now and hands this the answer:
    // which sprite, which one is fading up over it, how far, and where.
    //
    // Frames are discovered and cached by FrameSequenceLoader, shared with
    // StanceAnimationLibrary.
    public class SpellVfxPlayer : MonoBehaviour
    {
        [SerializeField] internal Image image;

        // The dissolve layer: a child of `image`, holding the NEXT frame and
        // fading up over the current one. See FightScreen.BuildSpellVfx.
        [SerializeField] internal Image fade;

        // Whether anything is on screen. What the module sets and what a test
        // waiting out a flight reads -- the renderer has no schedule of its own
        // to be "playing" against any more, so this is state rather than a
        // coroutine handle.
        public bool IsPlaying { get; private set; }

        // Read-only view, for the tests that assert the effect starts silent
        // and never eats a click. Read-only rather than exposing the field:
        // nothing outside this class has any business swapping the Image.
        public Image Image => image;

        // WHICH WAY THE SHEET FIRES. 1 as drawn, -1 mirrored.
        //
        // Only a directional effect has a direction at all -- a flare or a
        // ground spike is symmetrical about the thing it lands on and mirroring
        // it changes nothing. A projectile is not: the same spell cast back
        // across the stage has to run the other way, and left as drawn its
        // glyph forms on the victim and its impact lands on the caster.
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

        // ONE INSTANT, DRAWN. Everything about WHICH instant was decided
        // upstream, which is what lets a held clock make "what is on screen at
        // the impact" a question with an answer rather than a race against
        // however long the next frame takes.
        public void Show(Sprite frame, Sprite next, float blend, float alpha, Vector2 at, Vector2 size)
        {
            if (image == null || frame == null) return;

            // The pool node is built inactive -- there is nothing to show until
            // something is cast -- and a disabled node draws nothing however
            // enabled its Image is.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;

            image.sprite = frame;
            image.enabled = true;

            var colour = image.color;
            image.color = new Color(colour.r, colour.g, colour.b, alpha);

            IsPlaying = true;

            if (fade == null) return;

            fade.enabled = next != null && blend > 0f;
            if (!fade.enabled) return;

            fade.sprite = next;
            var fading = fade.color;
            fade.color = new Color(fading.r, fading.g, fading.b, blend * alpha);
        }

        // EVERYTHING THIS MEMBER CARRIES, PUT BACK. A pool member handed on
        // with a mirror, a tint or a size left on it is how the next cast
        // inherits a bug from the last one -- which is why two defensive
        // SetFacing(1f) calls used to exist at the call sites, each with a
        // comment saying "a pool member keeps whatever facing the last thing to
        // use it left behind". One code path owns restoration now, so there is
        // nowhere else to get it wrong.
        public void StopImmediately()
        {
            IsPlaying = false;

            if (image != null)
            {
                image.enabled = false;
                image.sprite = null;

                var colour = image.color;
                image.color = new Color(colour.r, colour.g, colour.b, 1f);

                var rect = image.rectTransform;
                rect.localScale = new Vector3(1f, rect.localScale.y, rect.localScale.z);
                rect.localRotation = Quaternion.identity;
            }

            if (fade == null) return;

            fade.enabled = false;
            fade.sprite = null;
            var fading = fade.color;
            fade.color = new Color(fading.r, fading.g, fading.b, 1f);
        }

        private void OnDisable()
        {
            // An abandoned fight must not leave an effect frozen mid-frame, the
            // same rule FightBeatPlayer.EndFight follows for the popups.
            StopImmediately();
        }

        // Exposed so a test can assert a spell's frames actually load, rather
        // than discovering a bad path by seeing nothing happen. Discovery and
        // caching belong to FrameSequenceLoader -- v1 had this cache
        // per-INSTANCE while its two siblings were static, so every fight-scene
        // reload re-probed every spell folder from disk for no reason.
        public Sprite[] Frames(string vfxPath) => FrameSequenceLoader.Load(vfxPath);
    }
}
