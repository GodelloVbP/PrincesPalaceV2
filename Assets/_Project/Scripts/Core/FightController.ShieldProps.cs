using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace
{
    // THE PLANTED SHIELD AS A STAGE PROP.
    //
    // A prop standing in front of its figure, not a spell-effect layer: it
    // outlives any one cast (up to LifetimeTurns), it is state the player reads
    // between actions, and it belongs to the seat rather than to a beat. One
    // Image per party slot (partyShieldProps), a child of the slot so it takes
    // the seat's depth scale and follows a Move with the figure, drawn at the
    // slot's ground line -- the shield is drawn with a free point, so its tip
    // is the sprite's bottom edge and sits on that line.
    //
    // WHEN IT PAINTS. Like the enemy status row, only when the view is idle
    // (RefreshStage skips it while busy): CombatBeat carries no shield
    // snapshot, so painting from live state mid-round would show a break several
    // beats before the blow that caused it. The prop therefore updates once a
    // round has finished playing, and a break reads then.
    //
    // SHIELDWALL. The wall art is four shields edge to edge at a two-to-one
    // aspect; at chest height it spans about one seat, not the party. So while
    // any ally's Shieldwall is up, every standing party figure gets the wall in
    // front of him, which reads as one wall along the front line and needs no
    // stage-level geometry.
    public partial class FightController
    {
        // How long the broken prop stays before it is cleared, scaled by the
        // battle-speed preset like every other beat of presentation.
        private const float BrokenShieldSeconds = 1.2f;

        private readonly Dictionary<CombatantState, PlantedShieldWatch> _shieldWatches =
            new Dictionary<CombatantState, PlantedShieldWatch>();

        // Per slot: the look being held while its broken frame plays out.
        private readonly Dictionary<int, ShieldLook> _shieldHeld = new Dictionary<int, ShieldLook>();

        private readonly Dictionary<string, Sprite> _shieldArt = new Dictionary<string, Sprite>();

        private void ResetShieldProps()
        {
            _shieldWatches.Clear();
            _shieldHeld.Clear();
            HideAllShieldProps();
        }

        private void HideAllShieldProps()
        {
            if (partyShieldProps == null) return;

            for (int i = 0; i < partyShieldProps.Length; i++)
            {
                if (partyShieldProps[i] != null) partyShieldProps[i].gameObject.SetShown(false);
            }
        }

        private void RefreshShieldProps()
        {
            if (_session == null || partyShieldProps == null) return;

            var party = _session.Encounter.PlayerParty;
            bool wallUp = false;
            var looks = new ShieldLook[partyShieldProps.Length];

            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                if (member == null) continue;

                if (!_shieldWatches.TryGetValue(member, out var watch))
                {
                    watch = new PlantedShieldWatch();
                    _shieldWatches[member] = watch;
                }

                var look = watch.Observe(member.PlantedShield);
                if (look == ShieldLook.Wall && member.IsAlive) wallUp = true;

                int slot = SlotIndexOf(member);
                if (slot < 0 || slot >= looks.Length) continue;

                if (look == ShieldLook.Broken)
                {
                    _shieldHeld[slot] = ShieldLook.Broken;
                    StartCoroutine(ClearBrokenShield(slot));
                }

                looks[slot] = look;
            }

            for (int slot = 0; slot < partyShieldProps.Length; slot++)
            {
                var image = partyShieldProps[slot];
                if (image == null) continue;

                CombatantState member = null;
                for (int i = 0; i < party.Count; i++)
                {
                    if (SlotIndexOf(party[i]) == slot) { member = party[i]; break; }
                }

                var look = looks[slot];

                // A wall covers every standing ally, and only a wall does: a
                // lone placement is never drawn on somebody else's seat. A held
                // broken frame shows until its timer clears it.
                if (wallUp && member != null && member.IsAlive) look = ShieldLook.Wall;
                else if (look == ShieldLook.Wall) look = ShieldLook.None;
                else if (look == ShieldLook.None && _shieldHeld.TryGetValue(slot, out var held)) look = held;

                if (member == null || !IsOnStage(member)) look = ShieldLook.None;

                PaintShieldProp(image, member, look);
            }
        }

        private IEnumerator ClearBrokenShield(int slot)
        {
            yield return new WaitForSecondsRealtime(FightBeatPlayer.Scaled(BrokenShieldSeconds));

            _shieldHeld.Remove(slot);
            if (!_isBusy) RefreshShieldProps();
        }

        private void PaintShieldProp(Image image, CombatantState member, ShieldLook look)
        {
            var sprite = LoadShieldArt(PlantedShieldArt.ResourceFor(look));
            if (sprite == null || member == null)
            {
                image.gameObject.SetShown(false);
                return;
            }

            var slot = image.transform.parent as RectTransform;
            string folder = SpriteFolderFor(member);
            var drawn = OpaqueBoxForActor(folder);
            if (slot == null || !drawn.HasValue)
            {
                image.gameObject.SetShown(false);
                return;
            }

            var box = drawn.Value.Box;
            float groundLine = StanceManifestLoader.Manifest.GroundLineFor(folder);
            float figureHeight = box.yMax - groundLine;
            float mirror = StageFacing.MirrorScaleX(FacingOf(member), StageSide.Left);

            // The figure's leading edge in canvas pixels, whichever way the art
            // faces: the prop stands just ahead of the body, not behind it.
            float frontX = mirror >= 0f ? box.xMax : drawn.Value.CanvasWidth - box.xMin;

            float scale = PlantedShieldArt.ChestFraction * figureHeight / sprite.rect.height;
            var rect = image.rectTransform;
            rect.sizeDelta = sprite.rect.size * scale;
            rect.anchoredPosition = new Vector2(frontX, -PlantedShieldArt.GroundLinePixels * scale);

            image.sprite = sprite;
            image.preserveAspect = true;
            image.gameObject.SetShown(true);
            image.enabled = true;
        }

        private Sprite LoadShieldArt(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_shieldArt.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            _shieldArt[path] = sprite;
            return sprite;
        }

        // Test seams: what a party slot's prop currently shows, or null; and a
        // repaint that does not wait for the view to go idle.
        public Sprite ShieldPropSpriteForTest(int slot) =>
            partyShieldProps != null && slot >= 0 && slot < partyShieldProps.Length
            && partyShieldProps[slot] != null && partyShieldProps[slot].gameObject.activeSelf
                ? partyShieldProps[slot].sprite
                : null;

        public void RefreshShieldPropsForTest() => RefreshShieldProps();
    }
}
