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
    // WHAT IT DOES NOT OWN, and why that matters: the clock, the schedule
    // and the flight. A coroutine per cast off its own wall-clock stamp
    // would be a second orchestrator -- a scheduler advancing a cast on one
    // clock beside a renderer advancing its frames on another.
    // SpellPerformancePlayer owns both and hands this the answer: which
    // sprite, which one is fading up over it, how far, and where.
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
        public void Show(Sprite frame, Sprite next, float blend, float alpha, Vector2 at, Vector2 size,
            float degrees = 0f, float glow = 0f)
        {
            if (image == null || frame == null) return;

            // The pool node is built inactive -- there is nothing to show until
            // something is cast -- and a disabled node draws nothing however
            // enabled its Image is.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;

            // TURNED ON THE RECT, AFTER THE MIRROR. localScale carries the
            // facing and localRotation carries this, and a RectTransform
            // composes them as T * R * S -- so the sheet is mirrored in its own
            // frame first and the whole mirrored thing is then tilted. That is
            // what makes a spanning layer's angle mean the same slope whichever
            // way the cast faces, instead of a mirrored cast tilting the other
            // way. StopImmediately puts it back, so a member handed on carries
            // no angle.
            rect.localRotation = degrees == 0f
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, degrees);

            ApplyGlow(glow);

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
        // inherits a bug from the last one. One code path owns restoration,
        // so there is nowhere else to get it wrong.
        public void StopImmediately()
        {
            IsPlaying = false;
            ApplyGlow(0f);

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

        // ---- the one thing on this stage that can be brighter than white ----------

        // A UI Image CANNOT EXCEED 1.0 ON ITS OWN. A sprite texture tops out at
        // white and a CanvasRenderer's vertex colour is a Color32, so
        // `image.color` clamps long before the fragment shader runs -- which
        // means that with the Volume's Bloom threshold at 1.05
        // (PipelineBuilder) nothing drawn on this canvas has ever crossed it.
        // Every precondition for bloom is in place (HDR on, post-processing on
        // the camera, canvas in ScreenSpaceCamera) and the effect is still dead
        // for want of a pixel above 1. UISpellGlow multiplies AFTER the sample
        // and is the only way past that; see its own header for the knee.
        //
        // ONE MATERIAL PER RENDERER, created lazily and never shared. A pool
        // member draws one layer at a time and layers author different boosts,
        // so a shared material would make the last writer win for every
        // concurrent cast -- and a MaterialPropertyBlock is not an option:
        // CanvasRenderer batches by material and ignores per-renderer blocks.
        //
        // DEGRADES TO NOTHING. A missing shader leaves the Image on its default
        // material and the layer draws flat, which is what it drew before this
        // existed -- the same posture LoadSprite and StageHitFlash take.
        private const string GlowShaderPath = "Shaders/UISpellGlow";
        private static readonly int BoostId = Shader.PropertyToID("_Boost");

        private Material _glowMaterial;
        private bool _glowUnavailable;

        private void ApplyGlow(float boost)
        {
            if (image == null) return;

            if (boost <= 0f)
            {
                // BACK TO THE DEFAULT MATERIAL, not merely to _Boost 0. A
                // member handed on with a custom material still costs the
                // canvas a separate batch for whatever draws next on it, and
                // "what this member is wearing" is exactly the kind of leftover
                // StopImmediately's own header exists to refuse.
                if (_glowMaterial != null && image.material == _glowMaterial) image.material = null;
                if (fade != null && _glowMaterial != null && fade.material == _glowMaterial)
                {
                    fade.material = null;
                }
                return;
            }

            if (_glowMaterial == null)
            {
                if (_glowUnavailable) return;

                var shader = Resources.Load<Shader>(GlowShaderPath);
                if (shader == null)
                {
                    _glowUnavailable = true;
                    Debug.LogWarning($"SpellVfxPlayer: no shader at Resources/{GlowShaderPath}; " +
                                     "glowing layers draw flat.");
                    return;
                }

                _glowMaterial = new Material(shader);
            }

            _glowMaterial.SetFloat(BoostId, boost);

            // ASSIGNED ONLY WHEN IT CHANGES. Graphic.material's SETTER calls
            // SetMaterialDirty() unconditionally, so re-assigning the same
            // material every frame queues a canvas rebuild every frame for a
            // change that did not happen -- and this runs once per drawn layer
            // per frame. SetFloat above is not a material swap and does not
            // dirty anything.
            if (image.material != _glowMaterial) image.material = _glowMaterial;

            // THE DISSOLVE LAYER TOO, or the frame fading up over a glowing one
            // arrives flat and the effect visibly dims once per frame boundary.
            if (fade != null && fade.material != _glowMaterial) fade.material = _glowMaterial;
        }

        private void OnDestroy()
        {
            // Created with `new Material`, so it is this component's to destroy
            // -- an undestroyed material leaks for the lifetime of the process,
            // and a PlayMode suite loads this scene dozens of times.
            if (_glowMaterial != null) Destroy(_glowMaterial);
            _glowMaterial = null;
        }
    }
}
