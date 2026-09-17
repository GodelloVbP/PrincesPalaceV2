using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The destructive half of the save-slot flow: one Delete per slot, behind
    // a confirmation. Reached from Manage Saves, which is opened from the slot
    // list rather than from the main menu directly -- see SaveSlotController.
    //
    // Six parallel arrays that MUST stay the same length as deleteButtons. Only
    // deleteButtons is bound to UiCountAudit (E4) -- the other five are built in
    // the SAME loop iteration as it, off the same screen field
    // (MainMenuScreen.ManageCards), so there is nothing E4 would catch a
    // second binding that the first does not already cover. No gold array
    // here: a Manage Saves card shows Delete where the Choose list shows gold
    // (see MainMenuScreen.AddCardContent's showGold), so there is no gold
    // NodeRef to wire.
    public class ResetProgressController : MonoBehaviour
    {
        [SerializeField] internal Button[] deleteButtons;
        [SerializeField] internal TMP_Text[] slotNumbers;
        [SerializeField] internal TMP_Text[] slotTops;
        [SerializeField] internal TMP_Text[] slotDetails;
        [SerializeField] internal GameObject[] slotFilledWashes;
        [SerializeField] internal GameObject[] slotEmptyWashes;

        [SerializeField] internal GameObject confirmPanel;
        [SerializeField] internal TMP_Text confirmLabel;
        [SerializeField] internal Button confirmYesButton;
        [SerializeField] internal RectTransform confirmFill;
        [SerializeField] internal Button confirmNoButton;

        // The way back to the slot list this panel was opened from.
        [SerializeField] internal Button backButton;
        [SerializeField] internal GameObject saveSlotPanel;

        // Which slot the open confirmation is about. -1 when nothing is pending,
        // so a stray Yes cannot delete slot 0.
        private int _pendingSlot = -1;

        private HoldToConfirm _hold;

        // AUDIT.md #159: read by MainMenuController.RefreshNavigation to
        // tell which of the four shared-NavContext surfaces is showing --
        // confirmPanel is a SIBLING drawn over this whole screen, not a
        // child of it, so "am I active" and "is my confirm open" are two
        // separate questions the caller cannot answer from gameObject.activeSelf
        // alone.
        internal bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

        // How long the delete has to be held. Matches ExitsScreen's own
        // abandon-hold -- the only other irreversible action in the game --
        // so a player who has already learned that gesture once does not have
        // to learn a second timing for it here.
        public const float HoldSeconds = 1.2f;

        // No cross-panel event for the Play list. Its own OnEnable re-reads the
        // slots, which covers the only path that matters (delete here, reopen
        // it via Back) -- and a delegate wired from the Editor's build step
        // would not survive serialisation anyway, silently, in a way the
        // wiring sweep cannot see because it only checks serialized object
        // references.
        //
        // Continue IS reached this way, deliberately, and for the same reason:
        // GetComponentInParent is a runtime lookup, not a serialized field, so
        // it survives the build. See ConfirmDelete.
        private void Start()
        {
            for (int i = 0; i < deleteButtons.Length; i++)
            {
                int slot = i;
                deleteButtons[i].onClick.AddListener(() => AskToDelete(slot));
            }

            // NO onClick LISTENER on confirmYesButton, deliberately -- the same
            // reason ExitsScreen's own abandon-hold has none. A Button plays
            // its click on RELEASE whatever the press was for, so wiring this
            // to onClick would delete on a tap, which is the one thing a hold
            // exists to refuse. The hold decides instead.
            _hold = confirmYesButton.gameObject.AddComponent<HoldToConfirm>();
            _hold.Seconds = HoldSeconds;
            _hold.Progress = SetFill;
            _hold.Completed = ConfirmDelete;

            confirmNoButton.onClick.AddListener(Dismiss);

            backButton.onClick.AddListener(GoBack);

            Refresh();
        }

        private void OnEnable()
        {
            if (deleteButtons != null) Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < deleteButtons.Length; i++)
            {
                var facts = SaveSlotDetail.For(i);

                slotNumbers[i].Set(UiStrings.SlotNumber, i + 1);

                // "Empty" here, not "New Descent" -- see UiStrings.SlotEmpty's
                // own comment on why Manage Saves states the fact rather than
                // inviting a run nobody is about to start from a delete screen.
                slotTops[i].SetContent(facts.Filled ? facts.CharacterName : UiStrings.SlotEmpty.Format());

                slotDetails[i].SetContent(facts.Filled
                    ? UiStrings.SlotDetail.Format(facts.Floor, PlaytimeFormat.Describe(facts.PlaySeconds))
                    : "");

                slotFilledWashes[i].SetActive(facts.Filled);
                slotEmptyWashes[i].SetActive(!facts.Filled);
            }
        }

        private void AskToDelete(int slot)
        {
            // Deleting an already-empty slot is a no-op, so asking about it is a
            // pointless modal that trains people to click through confirmations.
            if (!SaveSystem.SlotExists(slot)) return;

            _pendingSlot = slot;
            confirmLabel.Set(UiStrings.ConfirmDelete, slot + 1);
            confirmPanel.SetActive(true);

            // STATED RATHER THAN INHERITED, the same call ExitsController's own
            // comment makes about its abandon hold: HoldToConfirm cancels
            // itself when its GameObject is disabled, which covers every path
            // OUT of this panel -- but "the hold is not half-pressed the
            // moment you open it" is a claim THIS panel makes, opening fresh
            // every time, so it says so rather than trusting a side effect on
            // a component two hops away.
            _hold?.Cancel();
            SetFill(0f);

            // AUDIT.md #159: the confirm dialog joins MainMenuController's
            // shared NavContext, so opening it (same as GoBack/Dismiss below)
            // has to say so.
            GetComponentInParent<MainMenuController>()?.RefreshNavigation();
        }

        private void ConfirmDelete()
        {
            if (_pendingSlot < 0) return;

            SaveSystem.DeleteSlot(_pendingSlot);

            // DROP THE CACHE, or the file comes straight back.
            //
            // SaveSlotManager holds one SaveData for the current slot and
            // SaveCurrent writes it wherever CurrentSlot points. Deleting the
            // file left that copy untouched, so the next Persist -- starting a
            // run, buying an upgrade, anything -- recreated the slot with every
            // item and every worn piece of gear still on it. The delete looked
            // like it worked, because the list refreshes off the disk.
            //
            // Unconditional rather than gated on _pendingSlot == CurrentSlot:
            // the cache is rebuilt from disk on the next read, so dropping it
            // when it was some other slot costs one load and cannot be wrong.
            SaveSlotManager.Forget();

            // AND WHOEVER IS OFFERING TO CONTINUE INTO THIS SLOT FINDS OUT.
            //
            // A runtime lookup rather than a wired reference -- see this
            // class's own header on why a delegate assigned at build time
            // would not survive the scene being saved. Null-safe because a
            // test can build this controller without the main menu's root
            // above it, and because a future screen might reach Manage Saves
            // by some other door.
            GetComponentInParent<MainMenuController>()?.RefreshContinue();

            _pendingSlot = -1;
            confirmPanel.SetActive(false);
            SetFill(0f);

            Refresh();
            GetComponentInParent<MainMenuController>()?.RefreshNavigation();
        }

        // Internal, not private: MainMenuController.HandleCancel calls this
        // directly for a Cancel press while the confirm dialog is open
        // (AUDIT.md #159) -- the same button ConfirmNoButton's own onClick
        // already calls, so a gamepad Cancel and a mouse click on No leave
        // identical state.
        internal void Dismiss()
        {
            _pendingSlot = -1;
            confirmPanel.SetActive(false);
            _hold?.Cancel();
            SetFill(0f);
            GetComponentInParent<MainMenuController>()?.RefreshNavigation();
        }

        // Internal for the same reason as Dismiss above: MainMenuController.
        // HandleCancel calls this directly for a Cancel press while the
        // manage-saves list itself (not its confirm dialog) is topmost.
        internal void GoBack()
        {
            // Looked up BEFORE deactivating, not after: GetComponentInParent
            // starting from an already-inactive GameObject is exactly the
            // hazard this class's own header warns about elsewhere (a
            // runtime lookup instead of a wired delegate), and ConfirmDelete
            // already establishes the safe order -- reach the parent first,
            // change activation second.
            var menu = GetComponentInParent<MainMenuController>();
            gameObject.SetActive(false);
            saveSlotPanel.SetActive(true);
            menu?.RefreshNavigation();
        }

        // See HoldFillMath's own header for why sizeDelta rather than anchors.
        private void SetFill(float progress) =>
            HoldFillMath.SetFill(confirmFill, progress, MainMenuScreen.ResetHoldWidth, MainMenuScreen.ResetHoldHeight);
    }
}
