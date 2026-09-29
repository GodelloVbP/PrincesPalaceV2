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
    // WHEN IT PAINTS. On the blow: every beat carries the shield state it left
    // behind (CombatBeat.Shields) and FightBeatPlayer hands it to PaintShields
    // at the impact instant, beside the damage popup, so the crack shows on the
    // blow that crossed half and the broken frame plays on the blow that broke
    // it. The idle repaint (RefreshStage, when not busy) reconciles from live
    // state after a Move, a plant or the fight opening; it never invents a
    // break, because a break is an event only a beat can carry.
    //
    // SHIELDWALL. One planted shield per standing ally, all reading the one
    // shared pool, so the wall cracks and breaks at every seat together
    // (PlantedShieldStage.Resolve). The four-shield wall art is not loaded.
    public partial class FightController
    {
        // How long the broken prop stays before it is cleared, scaled by the
        // battle-speed preset like every other beat of presentation.
        private const float BrokenShieldSeconds = 1.2f;

        // Per slot: the look on screen, and which break the clear timer belongs
        // to, so a timer from an earlier break cannot clear a newer one.
        private readonly Dictionary<int, ShieldLook> _shieldShown = new Dictionary<int, ShieldLook>();
        private readonly Dictionary<int, int> _shieldBreakToken = new Dictionary<int, int>();

        private readonly Dictionary<string, Sprite> _shieldArt = new Dictionary<string, Sprite>();

        private void ResetShieldProps()
        {
            _shieldShown.Clear();
            _shieldBreakToken.Clear();
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

        // The live reconciliation: what the shields are now, never a break.
        private void RefreshShieldProps()
        {
            if (_session == null || partyShieldProps == null) return;

            var party = _session.Encounter.PlayerParty;
            var shields = new Dictionary<CombatantState, ShieldSnapshot>();
            foreach (var member in party)
            {
                if (member != null) shields[member] = ShieldSnapshot.Of(member.PlantedShield);
            }

            ApplyShieldLooks(PlantedShieldStage.Resolve(party, shields, m => m.IsAlive));
        }

        // The beat's moment: the shields as the blow left them, with standing
        // judged by the beat's own vitals rather than the round's end.
        private void PaintShields(IReadOnlyDictionary<CombatantState, ShieldSnapshot> shields,
                                  IReadOnlyDictionary<CombatantState, Vitals> vitals)
        {
            if (_session == null || partyShieldProps == null || shields == null) return;

            var party = _session.Encounter.PlayerParty;
            ApplyShieldLooks(PlantedShieldStage.Resolve(party, shields, m =>
                vitals != null && vitals.TryGetValue(m, out var v) ? v.Health > 0 : m.IsAlive));
        }

        private void ApplyShieldLooks(IReadOnlyDictionary<CombatantState, ShieldLook> looks)
        {
            var party = _session.Encounter.PlayerParty;

            for (int slot = 0; slot < partyShieldProps.Length; slot++)
            {
                var image = partyShieldProps[slot];
                if (image == null) continue;

                CombatantState member = null;
                for (int i = 0; i < party.Count; i++)
                {
                    if (SlotIndexOf(party[i]) == slot) { member = party[i]; break; }
                }

                var look = ShieldLook.None;
                if (member != null && IsOnStage(member)) looks.TryGetValue(member, out look);

                _shieldShown.TryGetValue(slot, out var shown);

                if (look == ShieldLook.Broken)
                {
                    int token = _shieldBreakToken.TryGetValue(slot, out var last) ? last + 1 : 1;
                    _shieldBreakToken[slot] = token;
                    StartCoroutine(ClearBrokenShield(slot, token));
                }
                else if (look == ShieldLook.None && shown == ShieldLook.Broken)
                {
                    // The broken frame plays out its own timer; nothing else
                    // repaints over it.
                    look = ShieldLook.Broken;
                }

                _shieldShown[slot] = look;
                PaintShieldProp(image, member, look);
            }
        }

        private IEnumerator ClearBrokenShield(int slot, int token)
        {
            yield return new WaitForSecondsRealtime(FightBeatPlayer.Scaled(BrokenShieldSeconds));

            if (!_shieldBreakToken.TryGetValue(slot, out var current) || current != token) yield break;
            if (!_shieldShown.TryGetValue(slot, out var shown) || shown != ShieldLook.Broken) yield break;

            _shieldShown[slot] = ShieldLook.None;
            if (partyShieldProps != null && slot < partyShieldProps.Length && partyShieldProps[slot] != null)
            {
                partyShieldProps[slot].gameObject.SetShown(false);
            }
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

        // Test seams: what a party slot's prop currently shows, or null; a
        // repaint that does not wait for the view to go idle; and the beat's
        // own impact paint, for a test that plays a beat by hand.
        public Sprite ShieldPropSpriteForTest(int slot) =>
            partyShieldProps != null && slot >= 0 && slot < partyShieldProps.Length
            && partyShieldProps[slot] != null && partyShieldProps[slot].gameObject.activeSelf
                ? partyShieldProps[slot].sprite
                : null;

        public void RefreshShieldPropsForTest() => RefreshShieldProps();

        public void PaintShieldsForTest(CombatBeat beat) => PaintShields(beat.Shields, beat.Snapshot);

        // Plays hand-recorded beats the way AfterResolution does: busy for the
        // whole playback, so the idle repaint cannot stand in for the beats.
        public void PlayBeatsForTest(IReadOnlyList<CombatBeat> beats)
        {
            _isBusy = true;
            beatPlayer.Play(beats, OnPlaybackFinished);
        }
    }
}
