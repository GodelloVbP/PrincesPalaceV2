using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The destructive half of Options: one Delete per slot, behind a
    // confirmation.
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

        // Which slot the open confirmation is about. -1 when nothing is pending,
        // so a stray Yes cannot delete slot 0.
        private int _pendingSlot = -1;

        // No cross-panel event. The Play panel's own OnEnable re-reads the slots,
        // which covers the only path that matters (delete in Options, reopen
        // Play) -- and a delegate wired from the Editor's build step would not
        // survive serialisation anyway, silently, in a way the wiring sweep
        // cannot see because it only checks serialized object references.
        private void Start()
        {
            for (int i = 0; i < deleteButtons.Length; i++)
            {
                int slot = i;
                deleteButtons[i].onClick.AddListener(() => AskToDelete(slot));
            }

            confirmYesButton.onClick.AddListener(ConfirmDelete);
            confirmNoButton.onClick.AddListener(Dismiss);

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
