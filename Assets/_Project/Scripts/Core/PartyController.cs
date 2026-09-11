using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Fills and drives the Party pane: the model lives in
    // Domain/Party/PartyFormation.cs, engine-free; this is the translation
    // layer that turns a save into that model and the model's outcomes back
    // into a save, the same split RunStatsController and
    // CharacterDossierController already draw.
    //
    // NAMES MIRROR PartyScreen's OWN FIELD NAMES so UiAutoBind can bind them
    // (see ScreenRegistry.WireParty for the residual it cannot see: the menu
    // reference, the lock flag, each seat badge's own text label, the drag
    // sources and the toast's own fader, none of which UiAutoBind's five
    // typed lookups can reach).
    public class PartyController : MonoBehaviour
    {
        // ---- header -----------------------------------------------------------
        [SerializeField] internal TMP_Text banner;
        [SerializeField] internal Button cancelLink;
        [SerializeField] internal Button benchLink;
        [SerializeField] internal TMP_Text statusText;
        [SerializeField] internal TMP_Text filledCount;
        [SerializeField] internal GameObject toast;
        [SerializeField] internal TMP_Text toastText;

        // The toast's own fader (P4) -- a CanvasGroup lives on the same node
        // ([RequireComponent] on PartyToast adds it, so nothing else here
        // has to). Replaces P3's hard show/hide; see PartyToast's own header
        // for why it is a small local component rather than an existing
        // reuse-first primitive.
        [SerializeField] internal PartyToast toastFader;

        // ---- seats, front-first (index 0 = front), length PartySeat.Count -----
        [SerializeField] internal Button[] seatButtons;
        [SerializeField] internal GameObject[] seatBadges;
        [SerializeField] internal Image[] seatArts;
        [SerializeField] internal GameObject[] seatMonogramPlates;
        [SerializeField] internal TMP_Text[] seatMonogramLetters;
        [SerializeField] internal Image[] seatGlows;
        [SerializeField] internal TMP_Text[] seatNames;
        [SerializeField] internal TMP_Text[] seatRoles;
        [SerializeField] internal GameObject[] seatRings;
        [SerializeField] internal GameObject[] seatScrims;
        [SerializeField] internal TMP_Text[] seatScrimCaptions;

        // The label INSIDE each badge. PartyScreen.BuildSeat only kept a
        // NodeRef for the badge's own OutlineBox (screen.SeatBadges) -- the
        // text child is built and parented but never captured -- so this is
        // the one seat field UiAutoBind cannot reach and ScreenRegistry.
        // WireParty fills it by reading the badge's own child, the same way
        // ExitsController.exitLabels reaches a button's child label.
        [SerializeField] internal TMP_Text[] seatBadgeTexts;

        // One drag surface per seat, attached at build time (ScreenRegistry.
        // WireParty) but configured here at runtime -- see PartyDragSource's
        // own header for why the delegates cannot be baked into the scene.
        [SerializeField] internal PartyDragSource[] seatDragSources;

        // ---- roster cards, in save.roster order, length RosterCardCount -------
        [SerializeField] internal Button[] cardButtons;
        [SerializeField] internal Image[] cardArts;
        [SerializeField] internal GameObject[] cardMonogramPlates;
        [SerializeField] internal TMP_Text[] cardMonogramLetters;
        [SerializeField] internal TMP_Text[] cardNames;
        [SerializeField] internal TMP_Text[] cardRoles;
        [SerializeField] internal TMP_Text[] cardTags;
        [SerializeField] internal GameObject[] cardRings;
        [SerializeField] internal GameObject[] cardSelectedTags;
        [SerializeField] internal GameObject[] cardWashes;
        [SerializeField] internal PartyDragSource[] cardDragSources;

        // ---- P4: the one reusable drag ghost, and the roster's own drop zone ----
        [SerializeField] internal GameObject dragGhost;
        [SerializeField] internal Image dragGhostArt;
        [SerializeField] internal GameObject dragGhostMonogramPlate;
        [SerializeField] internal TMP_Text dragGhostMonogramLetter;
        [SerializeField] internal GameObject rosterDropZone;

        // The menu this pane lives in -- same shape as ExitsController.menu
        // and CharacterDossierController.menu, and for the same reason: mode
        // reads menu.InDescent rather than a second copy of the same fact.
        // NOT RunManager.HasRun -- see SystemMenuController.inDescent's own
        // comment for why that answers a different question (a run exists
        // the moment the relic draft rolls, which happens IN THE HUB, so
        // HasRun is true in the hub with no descent under way).
        [SerializeField] internal SystemMenuController menu;

        // The fight's copy is readable and inert, the same rule as the
        // dossier's own lockedForFight -- decided at BUILD time
        // (ScreenRegistry.WireParty), never sniffed at runtime.
        [SerializeField] internal bool lockedForFight;

        // ---- the glow tokens ----------------------------------------------------
        //
        // PartyScreen's own GlowNeutral is private (the build-time default
        // every seat starts on); these three are the runtime states a glow
        // can move to, read off the handoff's own token table
        // (docs/handoffs/party_screen/README.md, "Ground-glow accents") the
        // same way PartyScreen's constants were.
        private const string GlowNeutral = "#B4AA9629";     // rgba(180,170,150,.16)
        private const string GlowOccupied = "#5FE07A8C";    // rgba(95,224,122,.55)
        private const string GlowHighlighted = "#E8C07AB3"; // rgba(232,192,122,.70)

        private static readonly string[] SeatWords = { "front", "middle", "rear" };

        private bool _wired;
        private List<string> _rosterIds = new List<string>();

        // Each art slot's own canvas-bottom baseline, captured once by
        // AlignArtSlots at wire time -- the ground-line shift (GroundArt)
        // moves a slot's anchoredPosition away from this every repaint, so
        // the baseline has to live somewhere that shift cannot drift.
        private float[] _seatArtFloorY;
        private float[] _cardArtFloorY;

        // ONE SCALE PER SLOT KIND, recomputed each Paint from the roster's
        // own idle canvases (PartyArtScale.ScaleFor) rather than each Image
        // fitting its own sprite into the box independently -- see
        // PartyArtScale's own header for the defect this replaces (a bigger
        // canvas got shrunk MORE to fill the same fixed box, so an actor
        // delivered on a larger sheet read smaller than one on a smaller
        // sheet, backwards from the fight stage where every actor draws at
        // native pixel size). Defaults to 1f (native size) before the first
        // Paint, same answer PartyArtScale.NoRosterScale gives an empty
        // roster.
        private float _seatArtScale = 1f;
        private float _cardArtScale = 1f;

        // ---- P4: drag session state ---------------------------------------------

        // Whether THIS controller's own BeginDrag accepted the gesture that
        // is currently in flight -- distinct from whatever uGUI itself
        // thinks is dragging, because a refused begin (a locked seat, a
        // benched card in a Run) must still let OnDrag/OnEndDrag fire
        // harmlessly with no ghost to move and nothing to commit.
        private bool _dragging;
        private RectTransform _dragGhostRect;
        private RectTransform _dragGhostParentRect;

        // THE CLICK-SUPPRESSION SEAM. uGUI is documented to skip a Button's
        // own onClick on the pointer-up that ends a drag when the drag
        // handler lives on that Button's own object (see PartyDragSource's
        // header) -- but that guarantee lives entirely in the input module,
        // not in anything this class controls, and a synthetic PlayMode test
        // has no real pointer to drive it with. So EndDrag stamps the frame
        // a drag actually resolved on, and every click listener below skips
        // itself for that one frame -- deterministic, testable without a
        // real mouse, and correct however uGUI's own suppression behaves.
        private int _dragResolvedFrame = -1;

        private const int DragTargetNone = -2;
        private const int DragTargetRoster = -1;

        // The live model, read-only -- so a PlayMode test can assert against
        // it directly instead of reverse-engineering state from which nodes
        // are active.
        public PartyFormation Formation { get; private set; }

        private void OnEnable()
        {
            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (seatButtons != null)
            {
                for (int i = 0; i < seatButtons.Length; i++)
                {
                    if (seatButtons[i] == null) continue;
                    int seat = i;
                    seatButtons[i].onClick.AddListener(() =>
                    {
                        if (Time.frameCount == _dragResolvedFrame) return;
                        ClickSeat(seat);
                    });
                }
            }

            if (cardButtons != null)
            {
                for (int i = 0; i < cardButtons.Length; i++)
                {
                    if (cardButtons[i] == null) continue;
                    int index = i;
                    cardButtons[i].onClick.AddListener(() =>
                    {
                        if (Time.frameCount == _dragResolvedFrame) return;
                        ClickCardAt(index);
                    });
                }
            }

            if (cancelLink != null) cancelLink.onClick.AddListener(Cancel);
            if (benchLink != null) benchLink.onClick.AddListener(SendToBench);

            WireDragSources();

            if (dragGhost != null)
            {
                _dragGhostRect = dragGhost.transform as RectTransform;
                _dragGhostParentRect = _dragGhostRect != null ? _dragGhostRect.parent as RectTransform : null;
            }

            // BOTTOM-ALIGNED ONCE, not on every repaint. Each slot's
            // RectTransform is authored centre-pivoted at a fixed size;
            // pinning the bottom edge means re-anchoring the pivot and
            // compensating anchoredPosition by half the slot's own height,
            // which must happen exactly once or a second Paint would shift
            // it again by the same amount. What changes on every repaint is
            // only which sprite (if any) fills the slot, and (P4) how far
            // GroundArt shifts it off this baseline for that sprite's own
            // manifest ground line -- see Paint*/GroundArt.
            _seatArtFloorY = AlignArtSlots(seatArts);
            _cardArtFloorY = AlignArtSlots(cardArts);
        }

        // One drag surface per seat/card button, configured with a per-index
        // closure -- PartyDragSource itself carries no index or id, only the
        // three delegates a drag needs (see its own header).
        private void WireDragSources()
        {
            if (seatDragSources != null)
            {
                for (int i = 0; i < seatDragSources.Length; i++)
                {
                    if (seatDragSources[i] == null) continue;
                    int seat = i;
                    seatDragSources[i].Begin = e => BeginSeatDrag(seat, e);
                    seatDragSources[i].Dragging = OnGhostDrag;
                    seatDragSources[i].End = EndDrag;
                }
            }

            if (cardDragSources != null)
            {
                for (int i = 0; i < cardDragSources.Length; i++)
                {
                    if (cardDragSources[i] == null) continue;
                    int index = i;
                    cardDragSources[i].Begin = e => BeginCardDragAt(index, e);
                    cardDragSources[i].Dragging = OnGhostDrag;
                    cardDragSources[i].End = EndDrag;
                }
            }
        }

        private static float[] AlignArtSlots(Image[] slots)
        {
            if (slots == null) return null;

            var floors = new float[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var image = slots[i];
                if (image == null) continue;

                image.preserveAspect = true;

                var rect = image.rectTransform;
                float bottom = rect.anchoredPosition.y - rect.rect.height * 0.5f;
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom);
                floors[i] = bottom;
            }

            return floors;
        }

        // ---- building the model off the save -----------------------------------

        public void Refresh()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return;

            _rosterIds = save.roster
                .Where(c => c != null)
                .Select(c => c.definitionId)
                .ToList();

            var roster = _rosterIds
                .Select(id => new PartyRosterEntry(id, DisplayNameOf(id), HasArt(id)))
                .ToList();

            var rosterSet = new HashSet<string>(_rosterIds);
            var seatIds = save.selectedCharacterIds ?? new List<string>();
            var seats = new List<string>();
            var seen = new HashSet<string>();

            for (int i = 0; i < PartySeat.Count; i++)
            {
                string id = i < seatIds.Count ? seatIds[i] : null;

                // SaveData.EmptySeat back into the model's own null. The save
                // spells an empty seat "" because that is what JsonUtility
                // round-trips (SaveData.EmptySeat says why); PartyFormation
                // spells it null. One line, at the one seam between them.
                if (SaveData.IsEmptySeat(id)) id = null;

                // DEFENSIVE, not a second copy of SaveData's own
                // reconciliation (SaveData.ReconcileSelection and siblings
                // already keep selectedCharacterIds inside the roster and
                // duplicate-free before anything reaches this screen). This
                // is the one guard at the seam CODE_STANDARDS SS5 asks for --
                // a save that somehow reaches this screen unreconciled drops
                // the offending seat to empty rather than throwing the
                // PartyFormation constructor's ArgumentException at the
                // player.
                if (id != null && (!rosterSet.Contains(id) || !seen.Add(id))) id = null;

                seats.Add(id);
            }

            var mode = lockedForFight
                ? PartyMode.ViewOnly
                : (menu != null && menu.InDescent ? PartyMode.Run : PartyMode.Camp);

            // NO RUN-MODIFIER SYSTEM EXISTS to ask which seat, if any, is
            // locked for this run -- see PartyFormation's own header and
            // docs/handoffs/party_screen/DECISIONS.md ("Locked seats are a
            // rendered state with no system behind them yet"). `_ => false`
            // is production's honest answer until one is built; wire this to
            // whichever system ends up authoring run modifiers.
            Formation = new PartyFormation(roster, seats, mode, save.EffectiveMaxSquadSize(), _ => false);

            Paint();
        }

        // ---- commands -----------------------------------------------------------

        public void ClickSeat(int seat)
        {
            if (Formation == null) return;
            Apply(Formation.ClickSeat(seat));
        }

        public void ClickCard(string id)
        {
            if (Formation == null || string.IsNullOrEmpty(id)) return;
            Apply(Formation.ClickCard(id));
        }

        public void Cancel()
        {
            if (Formation == null) return;
            Apply(Formation.Cancel());
        }

        public void SendToBench()
        {
            if (Formation == null) return;
            Apply(Formation.SendToBench());
        }

        private void ClickCardAt(int index)
        {
            if (index < 0 || index >= _rosterIds.Count) return;
            ClickCard(_rosterIds[index]);
        }

        // THE ONE APPLY STEP -- click and drag share it. A click reaches it
        // through ClickSeat/ClickCard/Cancel/SendToBench above; a drag
        // reaches it through EndDrag below, which computes the very same
        // PartyOutcome (Drop/DropOnRoster/Cancel) and hands it here rather
        // than duplicating the persist-then-paint-then-toast sequence.
        private void Apply(PartyOutcome outcome)
        {
            if (outcome.Changed) Persist();
            Paint();
            ShowToast(outcome);
        }

        // Writes the seats back IN SEAT ORDER, HOLES AND ALL. WRITTEN ON EVERY
        // COMMIT rather than on close: this is a menu the player can leave
        // through Escape, a tab switch or a scene change, none of which run a
        // save-on-exit hook today, and a formation change lost to any of those
        // would read as a much worse bug than the extra disk writes cost.
        private void Persist()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || Formation == null) return;

            save.selectedCharacterIds = SeatList(Formation.SeatIds);
            SaveSlotManager.SaveCurrent();
        }

        // THE SEAT LIST THE SAVE CARRIES (AUDIT #93, owner's call 2026-09-11).
        //
        // This used to be `SeatIds.Where(id => id != null)`, and the filter was
        // the bug: PartyFormation leaves a hole where a seat is vacated because
        // "POSITIONS ARE MECHANICAL, not cosmetic -- seat 0 is the front rank
        // enemy melee concentrates on", and dropping the hole moved everybody
        // behind it one rank forward. Bench the front-ranker, reload, and Mid
        // was the melee magnet the player never put there.
        //
        // An empty seat is SaveData.EmptySeat in place. The TRAILING ones go:
        // a hole says something only when somebody sits behind it, and keeping
        // the tail would write three entries for a solo save's one member.
        //
        // Static and internal so PartySeatGapRoundTripTests can transcribe it
        // exactly -- that suite is engine-free Domain and cannot stand up a
        // MonoBehaviour, so the transcription is what keeps the two honest.
        internal static List<string> SeatList(IReadOnlyList<string> seatIds)
        {
            var seats = seatIds.Select(id => id ?? SaveData.EmptySeat).ToList();
            while (seats.Count > 0 && SaveData.IsEmptySeat(seats[seats.Count - 1]))
            {
                seats.RemoveAt(seats.Count - 1);
            }

            return seats;
        }

        // ---- P4: drag-and-drop ---------------------------------------------------
        //
        // BEGIN calls straight through ClickSeat/ClickCard -- "the controller,
        // which asks the model" -- rather than re-deriving selectability by
        // hand: a locked/closed seat or a ViewOnly/benched-in-Run card is
        // refused (and toasted) exactly as a click would refuse it, and
        // Cancel() first means the model holds no stale selection to leak
        // into a refused drag. The ghost only appears once the resulting
        // selection actually matches the id being dragged -- a refusal
        // leaves Formation.SelectedId null, so no ghost, no further state.

        private void BeginSeatDrag(int seat, PointerEventData eventData)
        {
            if (Formation == null) return;

            Cancel();
            ClickSeat(seat);

            bool accepted = Formation.SelectedId != null
                && Formation.SelectedFrom.IsSeat && Formation.SelectedFrom.SeatIndex == seat;

            _dragging = accepted;
            if (accepted) ShowGhost(Formation.SelectedId, eventData);
        }

        private void BeginCardDragAt(int index, PointerEventData eventData)
        {
            if (Formation == null || index < 0 || index >= _rosterIds.Count) return;

            string id = _rosterIds[index];
            Cancel();
            ClickCard(id);

            bool accepted = Formation.SelectedId == id;

            _dragging = accepted;
            if (accepted) ShowGhost(id, eventData);
        }

        private void OnGhostDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            MoveGhostTo(eventData);
        }

        // END hit-tests the pointer and turns what it lands on into the same
        // three outcomes the model already knows (Drop onto a seat,
        // DropOnRoster, or Cancel), then applies through the shared Apply
        // step above -- a drag never talks to Persist/Paint/ShowToast
        // directly.
        private void EndDrag(PointerEventData eventData)
        {
            HideGhost();

            bool wasDragging = _dragging;
            _dragging = false;
            if (!wasDragging) return;

            // Stamped even when nothing was selected to drop (shouldn't
            // happen once wasDragging is true, but costs nothing to be
            // certain) -- ANY resolved drag suppresses this frame's click,
            // regardless of outcome.
            _dragResolvedFrame = Time.frameCount;

            if (Formation == null || Formation.SelectedId == null) return;

            int target = HitTestSeatOrRoster(eventData);
            if (target >= 0) Apply(Formation.Drop(Formation.SelectedFrom, target));
            else if (target == DragTargetRoster) Apply(Formation.DropOnRoster());
            else Apply(Formation.Cancel());
        }

        // >= 0 is a seat index, DragTargetRoster is the roster (a card or the
        // gap between cards), DragTargetNone is neither -- a Cancel().
        //
        // A CARD counts as a roster drop too, not just the drop zone behind
        // it: the drop zone only has to cover the GAPS between cards
        // (PartyScreen.BuildRosterDropZone's own comment), because landing
        // squarely on a card is caught here first.
        private int HitTestSeatOrRoster(PointerEventData eventData)
        {
            var go = eventData.pointerCurrentRaycast.gameObject;
            if (go == null) return DragTargetNone;

            int seat = IndexOfButton(seatButtons, go);
            if (seat >= 0) return seat;

            if (IndexOfButton(cardButtons, go) >= 0) return DragTargetRoster;
            if (rosterDropZone != null && go == rosterDropZone) return DragTargetRoster;

            return DragTargetNone;
        }

        private static int IndexOfButton(Button[] buttons, GameObject go)
        {
            if (buttons == null) return -1;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject == go) return i;
            }

            return -1;
        }

        // ---- the ghost: one reusable node, re-skinned and repositioned ----------

        private void ShowGhost(string id, PointerEventData eventData)
        {
            if (dragGhost == null) return;

            var art = ArtFor(id);
            bool hasArt = art != null;

            if (dragGhostArt != null)
            {
                dragGhostArt.sprite = art;
                dragGhostArt.SetShown(hasArt);
            }

            dragGhostMonogramPlate?.SetShown(!hasArt);
            if (dragGhostMonogramLetter != null)
            {
                dragGhostMonogramLetter.gameObject.SetShown(!hasArt);
                if (!hasArt) dragGhostMonogramLetter.SetContent(MonogramFor(id));
            }

            dragGhost.SetShown(true);
            MoveGhostTo(eventData);
        }

        private void HideGhost() => dragGhost?.SetShown(false);

        private void MoveGhostTo(PointerEventData eventData)
        {
            if (_dragGhostRect == null || _dragGhostParentRect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragGhostParentRect, eventData.position, eventData.pressEventCamera, out var local))
            {
                _dragGhostRect.anchoredPosition = local;
            }
        }

        // ---- painting -------------------------------------------------------------

        private void Paint()
        {
            if (Formation == null) return;

            PaintHeader();

            // ONE PASS OVER THE ROSTER'S OWN CANVASES, before either row
            // paints -- both slot kinds measure against the SAME tallest
            // actor (PartyArtScale's own header), so a seat and a card
            // showing the same occupant always agree on how big he is drawn.
            var canvasHeights = _rosterIds
                .Select(id => ArtFor(id))
                .Where(sprite => sprite != null)
                .Select(sprite => sprite.rect.height);
            _seatArtScale = PartyArtScale.ScaleFor(PartyLayout.ArtHeight, canvasHeights);
            _cardArtScale = PartyArtScale.ScaleFor(PartyLayout.CardArtHeight, canvasHeights);

            for (int i = 0; i < PartySeat.Count; i++) PaintSeat(i);

            int cardCount = cardButtons?.Length ?? 0;
            for (int i = 0; i < cardCount; i++)
            {
                if (i < _rosterIds.Count) PaintCard(i);
                else HideCard(i);
            }
        }

        private void PaintHeader()
        {
            bool hasSelection = Formation.SelectedId != null;

            if (banner != null)
            {
                if (hasSelection) banner.Set(UiStrings.PartyBannerSelected, DisplayNameOf(Formation.SelectedId));
                else banner.Set(UiStrings.PartyBannerDefault);
            }

            cancelLink.SetShown(hasSelection);
            benchLink.SetShown(Formation.CanSendToBench);

            if (statusText != null) statusText.Set(StatusStringFor(Formation.Mode));
            if (filledCount != null) filledCount.Set(UiStrings.PartyFilledCount, Formation.FilledCount);
        }

        private static UiString StatusStringFor(PartyMode mode)
        {
            switch (mode)
            {
                case PartyMode.Camp: return UiStrings.PartyStatusCamp;
                case PartyMode.Run: return UiStrings.PartyStatusRun;
                default: return UiStrings.PartyStatusFight;
            }
        }

        private void PaintSeat(int seat)
        {
            var seatIds = Formation.SeatIds;
            string occupantId = seat < seatIds.Count ? seatIds[seat] : null;

            bool closed = Formation.IsSeatClosed(seat);
            bool locked = !closed && Formation.IsSeatLocked(seat);
            var badge = Formation.SeatBadge(seat);
            bool selectedHere = Formation.SelectedId != null
                && Formation.SelectedFrom.IsSeat && Formation.SelectedFrom.SeatIndex == seat;
            bool isDestination = Formation.IsValidDestination(seat);

            bool showBadge = badge != PartySeatBadge.None;
            SetShown(seatBadges, seat, showBadge);
            if (showBadge) SetSeatBadgeText(seat, badge, occupantId);

            var art = occupantId != null ? ArtFor(occupantId) : null;
            bool hasArt = art != null;
            SetArt(seatArts, seat, art);
            ScaleArt(At(seatArts, seat), art, _seatArtScale);
            GroundArt(At(seatArts, seat), FloorYAt(_seatArtFloorY, seat), art,
                occupantId != null ? BattleSpritePathFor(occupantId) : null);
            SetShown(seatMonogramPlates, seat, occupantId != null && !hasArt);
            SetMonogram(seatMonogramLetters, seat, occupantId != null && !hasArt, occupantId);

            var glow = At(seatGlows, seat);
            if (glow != null) glow.color = GlowColorFor(occupantId != null, selectedHere || isDestination);

            SetContent(seatNames, seat, occupantId != null ? DisplayNameOf(occupantId) : "");
            SetContent(seatRoles, seat, occupantId != null ? RoleNameOf(occupantId) : "");

            SetShown(seatRings, seat, selectedHere || isDestination);

            bool scrimmed = closed || locked;
            SetShown(seatScrims, seat, scrimmed);
            if (scrimmed)
            {
                var caption = At(seatScrimCaptions, seat);
                caption?.Set(closed ? UiStrings.PartyScrimClosed : UiStrings.PartyScrimLocked);
            }
        }

        private void SetSeatBadgeText(int seat, PartySeatBadge badge, string occupantId)
        {
            var text = At(seatBadgeTexts, seat);
            if (text == null) return;

            switch (badge)
            {
                case PartySeatBadge.PlaceHere:
                    text.Set(UiStrings.PartyBadgePlace, DisplayNameOf(Formation.SelectedId));
                    break;
                case PartySeatBadge.Replace:
                    text.Set(UiStrings.PartyBadgeReplace, DisplayNameOf(occupantId));
                    break;
                case PartySeatBadge.SwapWith:
                    text.Set(UiStrings.PartyBadgeSwap, DisplayNameOf(occupantId));
                    break;
            }
        }

        private void PaintCard(int index)
        {
            string id = _rosterIds[index];
            var state = Formation.CardState(id);

            var art = ArtFor(id);
            bool hasArt = art != null;
            SetArt(cardArts, index, art);
            ScaleArt(At(cardArts, index), art, _cardArtScale);
            GroundArt(At(cardArts, index), FloorYAt(_cardArtFloorY, index), art, BattleSpritePathFor(id));
            SetShown(cardMonogramPlates, index, !hasArt);
            SetMonogram(cardMonogramLetters, index, !hasArt, id);

            SetContent(cardNames, index, DisplayNameOf(id));
            SetContent(cardRoles, index, RoleNameOf(id));

            var tag = At(cardTags, index);
            if (tag != null)
            {
                if (state.IsActive)
                {
                    tag.Set(UiStrings.PartyCardTagInParty, SeatLabelFor(state.Seat));
                    tag.gameObject.SetShown(true);
                }
                else if (!state.HasArt)
                {
                    tag.Set(UiStrings.PartyCardTagArtPending);
                    tag.gameObject.SetShown(true);
                }
                else if (state.IsBenched)
                {
                    tag.Set(UiStrings.PartyCardTagBenched);
                    tag.gameObject.SetShown(true);
                }
                else
                {
                    tag.SetContent("");
                    tag.gameObject.SetShown(false);
                }
            }

            SetShown(cardRings, index, state.IsSelected);
            SetShown(cardSelectedTags, index, state.IsSelected);

            // DIMMED, NEVER DISABLED. A benched card in a Run, or a locked
            // seat's card, must still be clickable -- ClickCard is what
            // decides the refusal toast (BenchedDuringRun, SeatLocked), and a
            // Button whose interactable is false would eat the click before
            // the model ever got to explain why nothing happened.
            SetShown(cardWashes, index, !state.IsSelectable);
        }

        private void HideCard(int index)
        {
            SetArt(cardArts, index, null);
            GroundArt(At(cardArts, index), FloorYAt(_cardArtFloorY, index), null, null);
            SetShown(cardMonogramPlates, index, false);
            SetMonogram(cardMonogramLetters, index, false, null);
            SetContent(cardNames, index, "");
            SetContent(cardRoles, index, "");

            var tag = At(cardTags, index);
            if (tag != null)
            {
                tag.SetContent("");
                tag.gameObject.SetShown(false);
            }

            SetShown(cardRings, index, false);
            SetShown(cardSelectedTags, index, false);
            SetShown(cardWashes, index, false);
        }

        // ---- common scale (P5) ------------------------------------------------------
        //
        // Sizes the slot's own RectTransform to the sprite's NATIVE size times
        // the roster-wide scale Paint just computed (PartyArtScale.ScaleFor),
        // rather than leaving the box at its authored fixed size for
        // preserveAspect to fit into. Width and height both move by the same
        // factor, so the box's own aspect ratio always ends up matching the
        // sprite's -- preserveAspect (still set by AlignArtSlots) is then a
        // no-op, kept only as a safety net against a rounding gap between the
        // two.
        //
        // Only pivot.x = 0.5 is in play here (AlignArtSlots already moved
        // pivot.y to 0, bottom-anchored) -- sizeDelta grows/shrinks the box
        // symmetrically about its own centre X and upward from its pinned
        // floor Y, so the art stays centred in its column/card without this
        // method touching anchoredPosition at all; GroundArt (below) is what
        // moves the box off that floor for the ground-line shift.
        //
        // A null sprite is a no-op: HideCard/an empty seat leave the Image
        // inactive, so its box's leftover size from a previous occupant is
        // never seen.
        private static void ScaleArt(Image image, Sprite sprite, float scale)
        {
            if (image == null || sprite == null) return;

            image.rectTransform.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
        }

        // ---- ground line (P4) ------------------------------------------------------
        //
        // The fight stage grounds a figure against its stance manifest's own
        // ground line (FightController.StageVisuals.GroundTheFigure) because
        // delivered art does not put its feet on its own canvas edge --
        // Shawn's idle plants a staff 43px below his own feet
        // (Resources/StanceManifest.json's "Characters/sheep" entry). P3's
        // AlignArtSlots pinned every slot's canvas-bottom to the slot floor
        // instead, which DECISIONS.md flagged as a known gap. This closes it
        // the same way the stage does, adapted for a slot that draws at a
        // roster-common scale (ScaleArt, above) rather than at native size:
        //
        //   1. AlignArtSlots already pinned the UNSHIFTED canvas-bottom to
        //      the slot floor -- _seatArtFloorY/_cardArtFloorY remember that
        //      anchoredPosition.y so repeated repaints (a different occupant
        //      each time) always shift from the same baseline rather than
        //      compounding.
        //   2. ScaleArt already sized this image's own box to sprite-native
        //      size times the common scale, so re-deriving that scale off the
        //      box (rect-width/sprite-width, equivalently rect-height/sprite-
        //      height -- ScaleArt makes the two agree exactly) needs no
        //      second number threaded in from Paint.
        //   3. The shift is the manifest's ground line, scaled by exactly
        //      that factor (equivalently "drawn height / source height",
        //      since drawnHeight = sourceHeight * scale by construction) --
        //      the manifest speaks in the sprite's own source pixels, and
        //      this slot no longer shows the sprite at that size.
        //
        // FALLS BACK TO CANVAS-BOTTOM (no shift) for any actor with no
        // manifest entry -- StanceManifest.GroundLineFor already returns
        // DefaultGroundLine (0) on a miss, so a missing entry needs no
        // separate branch here, only this comment saying so.
        //
        // NO HOVER. Odette flies on the fight stage (HoverSpec, "on this
        // screen she stands on her ground line -- no hover" per the P4
        // brief) -- only GroundLineFor is ever read here, never HoverFor.
        private static void GroundArt(Image image, float floorY, Sprite sprite, string battleSpritePath)
        {
            if (image == null) return;

            var rect = image.rectTransform;
            float x = rect.anchoredPosition.x;

            if (sprite == null || string.IsNullOrEmpty(battleSpritePath) || sprite.rect.height <= 0f)
            {
                rect.anchoredPosition = new Vector2(x, floorY);
                return;
            }

            float scale = Mathf.Min(rect.rect.width / sprite.rect.width, rect.rect.height / sprite.rect.height);
            float groundLine = StanceManifestLoader.Manifest.GroundLineFor(battleSpritePath);

            rect.anchoredPosition = new Vector2(x, floorY - groundLine * scale);
        }

        private static float FloorYAt(float[] array, int index) =>
            array != null && index >= 0 && index < array.Length ? array[index] : 0f;

        // ---- toast ------------------------------------------------------------------

        private void ShowToast(PartyOutcome outcome)
        {
            if (toastText == null) return;
            if (outcome.Toast == PartyToastKind.None) return;

            switch (outcome.Toast)
            {
                case PartyToastKind.Placed:
                    toastText.Set(UiStrings.PartyToastPlaced, outcome.Actor, SeatWord(outcome.Seat));
                    break;
                case PartyToastKind.Moved:
                    toastText.Set(UiStrings.PartyToastMoved, outcome.Actor, SeatWord(outcome.Seat));
                    break;
                case PartyToastKind.Swapped:
                    toastText.Set(UiStrings.PartyToastSwapped, outcome.Actor, outcome.Other);
                    break;
                case PartyToastKind.Replaced:
                    toastText.Set(UiStrings.PartyToastReplaced, outcome.Actor, outcome.Other);
                    break;
                case PartyToastKind.Benched:
                    toastText.Set(UiStrings.PartyToastBenched, outcome.Actor);
                    break;
                case PartyToastKind.FormationFixedInFight:
                    toastText.Set(UiStrings.PartyToastFormationFixedInFight);
                    break;
                case PartyToastKind.SeatNotOpen:
                    toastText.Set(UiStrings.PartyToastSeatNotOpen);
                    break;
                case PartyToastKind.SeatLocked:
                    toastText.Set(UiStrings.PartyToastSeatLocked, outcome.Actor);
                    break;
                case PartyToastKind.BenchedDuringRun:
                    toastText.Set(UiStrings.PartyToastBenchedDuringRun);
                    break;
                case PartyToastKind.RepositionOnlyDuringRun:
                    toastText.Set(UiStrings.PartyToastRepositionOnlyDuringRun);
                    break;
                case PartyToastKind.PartyNeverEmpty:
                    toastText.Set(UiStrings.PartyToastPartyNeverEmpty);
                    break;
                default:
                    return;
            }

            if (toastFader != null) toastFader.Show();
            else toast?.SetShown(true);
        }

        // ---- content lookups --------------------------------------------------------

        private static string DisplayNameOf(string id)
        {
            var definition = ContentDatabase.GetCharacter(id);
            return definition == null || string.IsNullOrWhiteSpace(definition.Data.DisplayName)
                ? id
                : definition.Data.DisplayName;
        }

        private static string RoleNameOf(string id)
        {
            var definition = ContentDatabase.GetCharacter(id);
            return definition == null ? "" : RoleDisplayName(definition.Data.Role);
        }

        // No existing display-name helper for CharacterRole -- the fight
        // HUD's own class row was cut (see PartyScreen's RoleText comment)
        // and nothing replaced it. A small local switch rather than
        // `role.ToString()`, so CrowdControl reads as two words here.
        private static string RoleDisplayName(CharacterRole role)
        {
            switch (role)
            {
                case CharacterRole.Utility: return "Utility";
                case CharacterRole.Assassin: return "Assassin";
                case CharacterRole.CrowdControl: return "Crowd Control";
                case CharacterRole.Tank: return "Tank";
                case CharacterRole.Support: return "Support";
                default: return role.ToString();
            }
        }

        // ART LOADS AT RUNTIME, off the same folder the fight stage uses --
        // see FightController.StanceSpriteFor and docs/handoffs/party_screen/
        // DECISIONS.md ("Art loads from Resources/Characters/<id>, never from
        // the handoff's sprites"). "idle" is the only stance a menu screen
        // ever has reason to ask for.
        private static Sprite ArtFor(string id)
        {
            var definition = ContentDatabase.GetCharacter(id);
            return definition == null
                ? null
                : StanceAnimationLibrary.Resolve(definition.Data.BattleSpritePath, FightSession.Stances.Idle);
        }

        private static bool HasArt(string id) => ArtFor(id) != null;

        // The manifest's own key for an actor -- "Characters/sheep", the
        // same string ArtFor hands StanceAnimationLibrary.Resolve.
        private static string BattleSpritePathFor(string id)
        {
            var definition = ContentDatabase.GetCharacter(id);
            return definition == null ? null : definition.Data.BattleSpritePath;
        }

        private static string MonogramFor(string id)
        {
            string name = DisplayNameOf(id);
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        private static string SeatLabelFor(int seat)
        {
            switch (seat)
            {
                case PartySeat.Front: return UiStrings.PartySeatFront.Format();
                case PartySeat.Middle: return UiStrings.PartySeatMiddle.Format();
                default: return UiStrings.PartySeatRear.Format();
            }
        }

        // The toast's own copy uses the lower-case word ("takes the front
        // position"), where the seat COLUMN's label is upper-case tactical
        // data (UiStrings.PartySeatFront et al, baked at build time) -- two
        // different registers for the same fact, so this stays local to the
        // toast rather than becoming a fourth casing of PartySeatFront.
        private static string SeatWord(int seat) =>
            seat >= 0 && seat < SeatWords.Length ? SeatWords[seat] : "";

        private static Color GlowColorFor(bool occupied, bool highlighted)
        {
            string hex = highlighted ? GlowHighlighted : occupied ? GlowOccupied : GlowNeutral;
            return ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.white;
        }

        // ---- small array helpers ------------------------------------------------

        private static T At<T>(T[] array, int index) where T : class =>
            array != null && index >= 0 && index < array.Length ? array[index] : null;

        private static void SetShown(GameObject[] array, int index, bool shown) =>
            At(array, index)?.SetShown(shown);

        private static void SetContent(TMP_Text[] array, int index, string value) =>
            At(array, index)?.SetContent(value);

        private static void SetMonogram(TMP_Text[] array, int index, bool shown, string id)
        {
            var label = At(array, index);
            if (label == null) return;

            label.gameObject.SetShown(shown);
            if (shown) label.SetContent(MonogramFor(id));
        }

        private static void SetArt(Image[] array, int index, Sprite sprite)
        {
            var image = At(array, index);
            if (image == null) return;

            image.sprite = sprite;
            image.SetShown(sprite != null);
        }
    }
}
