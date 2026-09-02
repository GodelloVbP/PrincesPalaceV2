using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The save-slot list.
    //
    // v1's Store bug was a strip sized off one collection and filled from
    // another. Two things stop that here, and it is worth being precise about
    // which does what: the screen builds every one of these arrays straight off
    // the same list it declared the nodes from, so the counts agree by
    // construction; and UiCountAudit (E4) fails the BUILD if a future edit
    // binds an array some other way and the two diverge. Only slotButtons is
    // bound to E4 -- the other five are built in the SAME loop iteration as
    // it, on the same screen field (MainMenuScreen.ChooseCards), so there is
    // nothing for a second binding to catch that the first one would not.
    public class SaveSlotController : MonoBehaviour
    {
        [SerializeField] internal Button[] slotButtons;

        // One card's content, per slot -- see MainMenuScreen.AddCardContent
        // for what each of these is and where it sits. No FilledWash/
        // EmptyWash any more (balance-bot, 2026-09-02): Slot{i}Button wears a
        // Gold ThemedPlate now, and Refresh drives its filled/empty state
        // through ThemedButtonState.SetMenuState instead of toggling two
        // background Solids.
        [SerializeField] internal TMP_Text[] slotNumbers;
        [SerializeField] internal TMP_Text[] slotTops;
        [SerializeField] internal TMP_Text[] slotDetails;
        [SerializeField] internal TMP_Text[] slotGolds;

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
        // through Manage Saves while the game is running would otherwise keep
        // advertising a character and a gold figure that no longer exist.
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
                // method writes it; SetContent is still the only thing that
                // ever touches .text, same as TalentController's identical
                // "authored template into a content-shaped setter" call.
                slotTops[i].SetContent(facts.Filled ? facts.CharacterName : UiStrings.NewDescent.Format());

                slotDetails[i].SetContent(facts.Filled
                    ? UiStrings.SlotDetail.Format(facts.Floor, PlaytimeFormat.Describe(facts.PlaySeconds))
                    : "");

                slotGolds[i].Set(UiStrings.SlotGold, facts.Gold);

                // PRIMARY for a filled slot (the gold "recommended action"
                // ring -- there is a run to continue), Idle for an empty one.
                // Hover's own glow is unconditional (ThemedButtonState.
                // UpdateGlow takes the MAX of menu-state alpha and focus
                // alpha), so "Open on hover" needs no extra call here.
                var themed = slotButtons[i].GetComponent<ThemedButtonState>();
                if (themed != null)
                {
                    themed.SetMenuState(facts.Filled ? ThemedMenuState.Primary : ThemedMenuState.Idle);
                }
            }
        }

        private void Choose(int slot)
        {
            // Everything "pick this slot and play it" shares with the main
            // menu's Continue button now lives on SaveSlotManager -- see
            // EnterSlot for why it stops short of navigating.
            SaveSlotManager.EnterSlot(slot);

            // Refreshed AFTER the write, same ordering this always used: a
            // freshly-created slot must not still read empty for whatever
            // frame this panel is visible before the scene actually changes.
            Refresh();

            // Into the hub. The slot is written BEFORE the scene changes, so a
            // player who picks a slot and immediately quits still finds it
            // there.
            Navigation.Go(Navigation.Hub);
        }
    }
}
