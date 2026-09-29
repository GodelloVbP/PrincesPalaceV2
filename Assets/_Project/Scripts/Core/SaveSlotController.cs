using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The Load list: one card per slot, each filled slot with its own Delete
    // behind a hold-to-confirm dialog.
    //
    // ONE CONTROLLER, not a list controller plus a delete controller. The
    // confirm dialog is only ever open over this list, both read the same
    // slots, and a second controller needed a serialized back-reference and a
    // GetComponentInParent hop just to say "the list changed".
    //
    // v1's Store bug was a strip sized off one collection and filled from
    // another. Two things stop that here: the screen builds every one of these
    // arrays straight off the same list it declared the nodes from, so the
    // counts agree by construction; and UiCountAudit (E4) fails the BUILD if a
    // future edit binds an array some other way and the two diverge. The
    // card-content arrays are built in the SAME loop iteration as slotButtons
    // (MainMenuScreen.ChooseCards), so there is nothing for a second binding
    // to catch.
    public class SaveSlotController : MonoBehaviour
    {
        [SerializeField] internal Button[] slotButtons;

        // Each slot's Delete, a sibling of its card. Shown only while the slot
        // is filled: deleting an empty slot would be a modal about nothing.
        [SerializeField] internal Button[] deleteButtons;

        // One card's content, per slot -- see MainMenuScreen.AddCardContent.
        // The card wears a Gold ThemedPlate, and Refresh drives its filled/
        // empty state through ThemedButtonState.SetMenuState.
        [SerializeField] internal TMP_Text[] slotNumbers;
        [SerializeField] internal TMP_Text[] slotTops;
        [SerializeField] internal TMP_Text[] slotDetails;
        [SerializeField] internal TMP_Text[] slotGolds;

        [SerializeField] internal GameObject confirmPanel;
        [SerializeField] internal TMP_Text confirmLabel;
        [SerializeField] internal Button confirmYesButton;
        [SerializeField] internal RectTransform confirmFill;
        [SerializeField] internal Button confirmNoButton;

        // How long the delete has to be held. Matches ExitsScreen's own
        // abandon-hold -- the only other irreversible action in the game --
        // so a player who has learned that gesture does not learn a second
        // timing for it here.
        public const float HoldSeconds = 1.2f;

        // Which slot the open confirmation is about. -1 when nothing is
        // pending, so a stray Yes cannot delete slot 0.
        private int _pendingSlot = -1;

        private HoldToConfirm _hold;

        // Read by MainMenuController to tell which surface is showing:
        // confirmPanel is a SIBLING drawn over the list, not a child of it,
        // so "the list is active" and "its confirm is open" are two questions.
        internal bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

        private void Start()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => Choose(slot));
                deleteButtons[i].onClick.AddListener(() => AskToDelete(slot));
            }

            // NO onClick LISTENER on confirmYesButton, deliberately -- the same
            // reason ExitsScreen's own abandon-hold has none. A Button plays
            // its click on RELEASE whatever the press was for, so wiring this
            // to onClick would delete on a tap, which is the one thing a hold
            // exists to refuse. HoldToConfirm decides instead, from the
            // pointer or from Submit held on the selected button.
            _hold = confirmYesButton.gameObject.AddComponent<HoldToConfirm>();
            _hold.Seconds = HoldSeconds;
            _hold.Progress = SetFill;
            _hold.Completed = ConfirmDelete;

            confirmNoButton.onClick.AddListener(Dismiss);

            Refresh();
        }

        // Refreshed on every OPEN, not only once at Start: the list must not
        // keep advertising a save that was deleted since it last showed.
        private void OnEnable()
        {
            if (slotButtons != null) Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var facts = SaveSlotDetail.For(i);

                slotNumbers[i].Set(UiStrings.SlotNumber, i + 1);

                // Top line: the lead character's name for a filled slot --
                // CONTENT, so SetContent carries it raw -- or the authored
                // invitation for an empty one. Same TMP_Text either way, so
                // the branch picks which SOURCE feeds it rather than which
                // method writes it.
                slotTops[i].SetContent(facts.Filled ? facts.CharacterName : UiStrings.NewDescent.Format());

                slotDetails[i].SetContent(facts.Filled
                    ? UiStrings.SlotDetail.Format(facts.Floor, PlaytimeFormat.Describe(facts.PlaySeconds))
                    : "");

                slotGolds[i].Set(UiStrings.SlotGold, facts.Gold);

                deleteButtons[i].gameObject.SetActive(facts.Filled);

                // PRIMARY for a filled slot (the gold "recommended action"
                // ring -- there is a run to continue), Idle for an empty one.
                // Hover's own glow is unconditional (ThemedButtonState.
                // UpdateGlow takes the MAX of menu-state alpha and focus
                // alpha), so "Open on hover" needs no extra call here.
                ThemedButtonState.ApplySelection(slotButtons[i], facts.Filled, ThemedMenuState.Primary);
            }
        }

        // Public: the main menu's New Run enters the lowest empty slot
        // through this same path rather than a second copy of it.
        public void Choose(int slot)
        {
            // Everything "pick this slot and play it" shares with the main
            // menu's Continue button lives on SaveSlotManager -- see
            // EnterSlot for why it stops short of navigating.
            SaveSlotManager.EnterSlot(slot);

            // Refreshed AFTER the write: a freshly-created slot must not
            // still read empty for whatever frame this panel is visible before
            // the scene actually changes.
            Refresh();

            // Into the hub. The slot is written BEFORE the scene changes, so a
            // player who picks a slot and immediately quits still finds it
            // there.
            Navigation.Go(Navigation.Hub);
        }

        private void AskToDelete(int slot)
        {
            if (!SaveSystem.SlotExists(slot)) return;

            _pendingSlot = slot;
            confirmLabel.Set(UiStrings.ConfirmDelete, slot + 1);
            confirmPanel.SetActive(true);

            // STATED RATHER THAN INHERITED: HoldToConfirm cancels itself when
            // its GameObject is disabled, which covers every path OUT of the
            // dialog, but "the hold is not half-pressed the moment you open
            // it" is a claim THIS dialog makes every time it opens.
            _hold?.Cancel();
            SetFill(0f);

            GetComponentInParent<MainMenuController>()?.RefreshNavigation();
        }

        private void ConfirmDelete()
        {
            if (_pendingSlot < 0) return;

            int slot = _pendingSlot;
            SaveSystem.DeleteSlot(slot);

            // DROP THE CACHE, or the file comes straight back.
            //
            // SaveSlotManager holds one SaveData for the current slot and
            // SaveCurrent writes it wherever CurrentSlot points. Deleting the
            // file left that copy untouched, so the next Persist -- starting a
            // run, buying an upgrade, anything -- recreated the slot with every
            // item and every worn piece of gear still on it. The delete looked
            // like it worked, because the list refreshes off the disk.
            //
            // Unconditional rather than gated on slot == CurrentSlot: the
            // cache is rebuilt from disk on the next read, so dropping it when
            // it was some other slot costs one load and cannot be wrong.
            SaveSlotManager.Forget();

            // A runtime lookup, not a wired reference: a delegate assigned at
            // build time would not survive the scene being saved. Null-safe
            // because a test can build this controller without the main menu
            // above it.
            var menu = GetComponentInParent<MainMenuController>();
            menu?.RefreshContinue();

            _pendingSlot = -1;
            confirmPanel.SetActive(false);
            SetFill(0f);

            Refresh();
            menu?.RefreshNavigation();

            // The Delete that opened the dialog is gone with its save; the
            // card in its place is what the player was looking at.
            Select(slotButtons[slot]);
        }

        // Internal: MainMenuController.HandleCancel calls this for a Cancel
        // press while the dialog is open -- the same path ConfirmNoButton's
        // click takes, so a pad Cancel and a click on No leave identical state.
        internal void Dismiss()
        {
            int slot = _pendingSlot;
            _pendingSlot = -1;
            confirmPanel.SetActive(false);
            _hold?.Cancel();
            SetFill(0f);
            GetComponentInParent<MainMenuController>()?.RefreshNavigation();

            // Back onto the Delete that asked, not the list's first card.
            if (slot >= 0) Select(deleteButtons[slot]);
        }

        private static void Select(Selectable target)
        {
            if (EventSystem.current != null && target != null && target.gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }

        // See HoldFillMath's own header for why sizeDelta rather than anchors.
        private void SetFill(float progress) =>
            HoldFillMath.SetFill(confirmFill, progress, MainMenuScreen.ResetHoldWidth, MainMenuScreen.ResetHoldHeight);
    }
}
