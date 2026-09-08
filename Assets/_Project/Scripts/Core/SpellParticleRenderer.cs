using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // ONE POOLED DROP.
    //
    // A BARE Image AND NOT A SpellVfxPlayer. Every member of the sprite pool
    // carries a dissolve child and a renderer component, all of which is dead
    // weight on a droplet: a particle draws one still, never cross-fades, and
    // sixty-four of them carrying a second Image each would double the canvas's
    // rebuild for nothing.
    //
    // SAME BAND, LATER SIBLINGS. The particle pool is declared after the sprite
    // pool and before the damage numbers, so drops draw over the crown they
    // came out of and under the number -- which is the brief's
    // "contact/foreground spray" as a node rather than as a third semantic
    // category.
    public class SpellParticleRenderer : MonoBehaviour
    {
        [SerializeField] internal Image[] particles;

        public int Capacity => particles == null ? 0 : particles.Length;

        // Whether this member is drawing. Not a bool per member kept here --
        // the Image's own state already is that, and a second copy could
        // disagree with it.
        //
        // isActiveAndEnabled AND NOT enabled, which is the difference between
        // "the component would draw" and "anything is on screen". Every pool
        // member is built Inactive (FightScreen.BuildSpellParticles), so an
        // `enabled` Image on a deactivated GameObject reports itself as drawing
        // and renders nothing -- which is exactly what the Water pilot's first
        // two captures showed: a perfect crown, no droplets, and four green
        // emitter tests asserting on `enabled`.
        public bool IsDrawing(int member) =>
            member >= 0 && member < Capacity && particles[member] != null &&
            particles[member].isActiveAndEnabled;

        public void Show(int member, Sprite sprite, Vector2 at, float size, float alpha, float rotation)
        {
            if (member < 0 || member >= Capacity) return;

            var image = particles[member];
            if (image == null || sprite == null) return;

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            image.sprite = sprite;

            // THE MEMBER'S OWN GameObject, not only this component's. Every
            // member is built Inactive, so `enabled = true` on its Image is
            // half the switch and the visible half is the other one. The sprite
            // pool never met this because each of ITS members carries its own
            // SpellVfxPlayer and activates itself; this is one component over
            // sixty-four objects, and `gameObject` here is the pool node.
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            image.enabled = true;

            var colour = image.color;
            image.color = new Color(colour.r, colour.g, colour.b, alpha);
        }

        // The same restore list the sprite renderer applies, for the same
        // reason: a member handed back with a rotation or a tint on it is how
        // the next cast inherits the last one's state.
        public void Release(int member)
        {
            if (member < 0 || member >= Capacity) return;

            var image = particles[member];
            if (image == null) return;

            image.enabled = false;
            image.gameObject.SetActive(false);
            image.sprite = null;

            var colour = image.color;
            image.color = new Color(colour.r, colour.g, colour.b, 1f);

            var rect = image.rectTransform;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < Capacity; i++) Release(i);
        }

        private void OnDisable() => ReleaseAll();
    }
}
