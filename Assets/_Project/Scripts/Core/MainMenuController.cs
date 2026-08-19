using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] internal Button playButton;
        [SerializeField] internal Button optionsButton;
        [SerializeField] internal Button exitButton;
        [SerializeField] internal Button closeSaveSlotButton;
        [SerializeField] internal Button closeOptionsButton;
        [SerializeField] internal GameObject saveSlotPanel;
        [SerializeField] internal GameObject optionsPanel;

        private void Start()
        {
            playButton.onClick.AddListener(() => Toggle(saveSlotPanel));
            optionsButton.onClick.AddListener(() => Toggle(optionsPanel));
            // Settled BEFORE the process goes away. Quitting is not
            // continuing the run, so it ends it, and ending it is what pays out
            // what the run earned -- quitting straight to the desktop would
            // bank nothing.
            exitButton.onClick.AddListener(() =>
            {
                RunManager.EndRun();
                Application.Quit();
            });
            closeSaveSlotButton.onClick.AddListener(() => saveSlotPanel.SetActive(false));
            closeOptionsButton.onClick.AddListener(() => optionsPanel.SetActive(false));
        }

        private static void Toggle(GameObject panel) => panel.SetActive(!panel.activeSelf);
    }
}
