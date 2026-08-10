using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // The save-slot list.
    //
    // v1's Store bug was a strip sized off one collection and filled from
    // another. Two things stop that here, and it is worth being precise about
    // which does what: the screen builds this array straight off the same list
    // it declared the nodes from, so the counts agree by construction; and
    // UiCountAudit (E4) fails the BUILD if a future edit binds the array some
    // other way and the two diverge.
    public class SaveSlotController : MonoBehaviour
    {
        [SerializeField] internal Button[] slotButtons;

        private void Start()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => Choose(slot));
            }

            Refresh();
        }

        // Refreshed on every OPEN, not only once at Start: a slot deleted
        // through Options while the game is running would otherwise keep
        // advertising gold that no longer exists.
        private void OnEnable()
        {
            if (slotButtons != null) Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var label = slotButtons[i].GetComponentInChildren<TMP_Text>(includeInactive: true);
                // SetContent, not Set: this text is DATA (a slot's gold and
                // relics), assembled by SaveSlotLabel out of the manifest.
                label.SetContent(SaveSlotLabel.For(i));
            }
        }

        private void Choose(int slot)
        {
            SaveSlotManager.CurrentSlot = slot;

            // A brand new slot is written immediately rather than on first save.
            // Picking a slot and finding it still "Empty" next launch is the
            // sort of thing players read as lost progress.
            if (!SaveSystem.SlotExists(slot))
            {
                SaveSystem.Save(SaveData.CreateNew(), slot);
            }

            Refresh();

            // Into the hub. The slot is written BEFORE the scene changes, so a
            // player who picks a slot and immediately quits still finds it there
            // -- the alternative reads as lost progress.
            Navigation.Go(Navigation.Hub);
        }
    }
}
