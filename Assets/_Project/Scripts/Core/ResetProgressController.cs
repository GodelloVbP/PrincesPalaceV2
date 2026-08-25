using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The destructive half of the save-slot flow: one Delete per slot, behind
    // a confirmation. Reached from Manage Saves, which is opened from the slot
    // list rather than from the main menu directly -- see SaveSlotController.
    //
    // Two parallel arrays that MUST stay the same length. UiCountAudit (E4)
    // checks each of them against the node list the screen declared, at build
    // time, so the pair cannot drift the way v1's builder-count and
    // controller-count did.
    public class ResetProgressController : MonoBehaviour
    {
        [SerializeField] internal Button[] deleteButtons;
        [SerializeField] internal TMP_Text[] slotLabels;
        [SerializeField] internal GameObject confirmPanel;
        [SerializeField] internal TMP_Text confirmLabel;
        [SerializeField] internal Button confirmYesButton;
        [SerializeField] internal Button confirmNoButton;

        // The way back to the slot list this panel was opened from.
        [SerializeField] internal Button backButton;
        [SerializeField] internal GameObject saveSlotPanel;

        // Which slot the open confirmation is about. -1 when nothing is pending,
        // so a stray Yes cannot delete slot 0.
        private int _pendingSlot = -1;

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

            confirmYesButton.onClick.AddListener(ConfirmDelete);
            confirmNoButton.onClick.AddListener(Dismiss);

            backButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                saveSlotPanel.SetActive(true);
            });

            Refresh();
        }

        private void OnEnable()
        {
            if (slotLabels != null) Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < slotLabels.Length; i++)
            {
                slotLabels[i].SetContent(SaveSlotLabel.For(i));
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

            Refresh();
        }

        private void Dismiss()
        {
            _pendingSlot = -1;
            confirmPanel.SetActive(false);
        }
    }
}
