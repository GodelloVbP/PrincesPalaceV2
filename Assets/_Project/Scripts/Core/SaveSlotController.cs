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

        // The door into Manage Saves, and back out of it. Reached from HERE
        // rather than from the main menu root, on request: deleting a save is
        // now something you do from the screen that shows you the saves, not
        // a separate destination the main menu offers alongside Play.
        [SerializeField] internal Button manageSavesButton;
        [SerializeField] internal GameObject managePanel;

        private void Start()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => Choose(slot));
            }

            manageSavesButton.onClick.AddListener(() =>
            {
                // THIS panel hides itself rather than staying open behind
                // Manage Saves. The two screens ask two different questions of
                // the same five rows -- "which do I play" against "which do I
                // delete" -- and showing both trees at once would mean asking
                // which one a click was answering.
                gameObject.SetActive(false);
                managePanel.SetActive(true);
            });

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
            // Everything "pick this slot and play it" shares with the main
            // menu's Continue button now lives on SaveSlotManager -- see
            // EnterSlot for why it stops short of navigating.
            SaveSlotManager.EnterSlot(slot);

            // Refreshed AFTER the write, same ordering this always used: a
            // freshly-created slot must not still read Empty for whatever
            // frame this panel is visible before the scene actually changes.
            Refresh();

            // Into the hub. The slot is written BEFORE the scene changes, so a
            // player who picks a slot and immediately quits still finds it
            // there.
            Navigation.Go(Navigation.Hub);
        }
    }
}
