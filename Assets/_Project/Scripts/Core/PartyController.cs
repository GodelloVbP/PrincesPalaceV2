using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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
    // reference, the lock flag, and each seat badge's own text label, which
    // PartyScreen never captured a NodeRef for).
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

        private const float ToastSeconds = 2.4f;

        private static readonly string[] SeatWords = { "front", "middle", "rear" };

        private bool _wired;
        private List<string> _rosterIds = new List<string>();
        private Coroutine _toastFade;

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
                    seatButtons[i].onClick.AddListener(() => ClickSeat(seat));
                }
            }

            if (cardButtons != null)
            {
                for (int i = 0; i < cardButtons.Length; i++)
                {
                    if (cardButtons[i] == null) continue;
                    int index = i;
                    cardButtons[i].onClick.AddListener(() => ClickCardAt(index));
                }
            }

            if (cancelLink != null) cancelLink.onClick.AddListener(Cancel);
            if (benchLink != null) benchLink.onClick.AddListener(SendToBench);

            // BOTTOM-ALIGNED ONCE, not on every repaint. Each slot's
            // RectTransform is authored centre-pivoted at a fixed size;
            // pinning the bottom edge means re-anchoring the pivot and
            // compensating anchoredPosition by half the slot's own height,
            // which must happen exactly once or a second Paint would shift
            // it again by the same amount. What changes on every repaint is
            // only which sprite (if any) fills the slot -- see Paint*.
            //
            // THIS PINS THE SLOT'S FLOOR, NOT EACH ACTOR'S FEET. The fight
            // stage's own grounding (FightController.StageVisuals.cs,
            // GroundTheFigure) reads a per-actor manifest offset because
            // delivered art does not put its feet on its own canvas edge --
            // the golem's is 52px off, Shawn's 33px. Reproducing that here
            // would need the same manifest on a menu screen that only ever
              // shows an idle pose, which is not attempted; this aligns each
            // canvas's own bottom edge to the slot floor, which is close but
            // not exact for actors whose canvas has followed space under the
            // feet. Worth a second look once real art exists for more than
            // sheep/owl -- see the report on this package for the honest
            // version of this note.
            AlignArtSlots(seatArts);
            AlignArtSlots(cardArts);
        }

        private static void AlignArtSlots(Image[] slots)
        {
            if (slots == null) return;
            foreach (var image in slots)
            {
                if (image == null) continue;
                image.preserveAspect = true;

                var rect = image.rectTransform;
                float bottom = rect.anchoredPosition.y - rect.rect.height * 0.5f;
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom);
            }
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

        private void Apply(PartyOutcome outcome)
        {
            if (outcome.Changed) Persist();
            Paint();
            ShowToast(outcome);
        }

        // Writes the seats back in seat order, non-null entries only -- the
        // shape save.selectedCharacterIds has always carried (SaveData.
        // ActiveSquad reads it the same way). WRITTEN ON EVERY COMMIT rather
        // than on close: this is a menu the player can leave through Escape,
        // a tab switch or a scene change, none of which run a save-on-exit
        // hook today, and a formation change lost to any of those would read
        // as a much worse bug than the extra disk writes cost.
        private void Persist()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || Formation == null) return;

            save.selectedCharacterIds = Formation.SeatIds.Where(id => id != null).ToList();
            SaveSlotManager.SaveCurrent();
        }

        // ---- painting -------------------------------------------------------------

        private void Paint()
        {
            if (Formation == null) return;

            PaintHeader();

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

        // ---- toast ------------------------------------------------------------------

        private void ShowToast(PartyOutcome outcome)
        {
            if (toastText == null || toast == null) return;
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

            toast.SetShown(true);

            if (_toastFade != null) StopCoroutine(_toastFade);
            if (isActiveAndEnabled) _toastFade = StartCoroutine(FadeToastAfter(ToastSeconds));
        }

        // UNSCALED, like every other clock the system menu hosts (see
        // ExitsController.ArmSeconds's own comment) -- this pane only ever
        // runs while the menu has paused the game at timeScale 0.
        //
        // A HARD CUT, not an alpha fade. The toast node carries no
        // CanvasGroup and nothing in the reuse-first registry (CODE_
        // STANDARDS SS2) is a drop-in "fade this out after N seconds"
        // primitive -- BeaconPulse and StageDeathFade both drive a component
        // this node does not have. Show/hide is a smaller, honest scope for
        // this package; a later polish pass can add a real fade if the
        // design still wants one.
        private IEnumerator FadeToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            toast.SetShown(false);
            _toastFade = null;
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
