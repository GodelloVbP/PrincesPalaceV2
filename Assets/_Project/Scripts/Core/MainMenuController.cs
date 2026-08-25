using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Contract B in practice: UI references are `internal` with [SerializeField]
    // kept.
    //
    // internal, because the Editor assembly assigns them directly (see
    // Core/AssemblyInfo.cs) instead of reflecting over a field NAME the way v1
    // did at 330 sites. [SerializeField] kept, because internal fields do not
    // serialize on their own -- and a field that does not serialize is null in
    // the built player no matter how correctly the builder assigned it.
    public class MainMenuController : MonoBehaviour
    {
        // Options went with Reset Progress. It was the only thing the main
        // menu's own Options panel ever held -- the real settings screen
        // (audio, display) lives in the in-run system menu, not here -- so
        // once Reset Progress moved to Manage Saves (see SaveSlotController),
        // there was nothing left for a main-menu Options entry to open.
        [SerializeField] internal Button playButton;
        [SerializeField] internal Button exitButton;
        [SerializeField] internal Button closeSaveSlotButton;
        [SerializeField] internal GameObject saveSlotPanel;

        // Shown only when a save exists to continue. Built unconditionally by
        // the screen -- Domain has no SaveSystem to ask at build time -- and
        // toggled here, which is what lets it sit OUTSIDE the Play/Exit
        // column rather than inside it: a flow container's siblings do not
        // reflow when one of them is hidden, so a conditional element has to
        // be positioned on its own to avoid leaving a gap-shaped hole.
        [SerializeField] internal Button continueButton;

        // -1 means nothing to continue into. Set by RefreshContinue, read by
        // the button's own click handler so the two can never disagree about
        // which slot "Continue" means.
        private int _continueSlot = -1;

        private void Start()
        {
            playButton.onClick.AddListener(() => Toggle(saveSlotPanel));
            // Settled BEFORE the process goes away. Quitting is not
            // continuing the run, so it ends it, and ending it is what pays out
            // what the run earned -- quitting straight to the desktop would
            // bank nothing.
            exitButton.onClick.AddListener(() =>
            {
                RunManager.EndRun();
                // Navigation.Quit, not Application.Quit: the system menu grew a
                // second quit and one of the two had to be the door. See Navigation.
                Navigation.Quit();
            });
            closeSaveSlotButton.onClick.AddListener(() => saveSlotPanel.SetActive(false));

            continueButton.onClick.AddListener(() =>
            {
                if (_continueSlot < 0) return;
                SaveSlotManager.EnterSlot(_continueSlot);
                Navigation.Go(Navigation.Hub);
            });

            RefreshContinue();
        }

        // Recomputes whether Continue has anything to continue, and what it
        // says. Called once at Start, and again from ResetProgressController
        // whenever a delete could have removed the slot Continue was pointing
        // at -- see that class's ConfirmDelete for why a runtime lookup reaches
        // back here instead of a wired event.
        public void RefreshContinue()
        {
            _continueSlot = SaveSystem.MostRecentSlot();
            bool hasSave = _continueSlot >= 0;

            continueButton.gameObject.SetActive(hasSave);
            if (!hasSave) return;

            var label = continueButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
            label.Set(UiStrings.ContinueSlot, _continueSlot + 1);
        }

        private static void Toggle(GameObject panel) => panel.SetActive(!panel.activeSelf);
    }
}
