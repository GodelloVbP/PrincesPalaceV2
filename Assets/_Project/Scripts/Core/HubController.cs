using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The Divine Principality hub.
    //
    // The currency line is the reason UiStrings exists. In v1 the builder baked
    // "Gold: 0    Relics: 0" and this controller wrote
    // $"Gold: {save.Gold}    Relics: {save.Relics}" - two hand-typed copies of a
    // four-space separator, in two files, with nothing tying them together. One
    // entry serves both now, and the lint refuses a bare `.text =` here.
    public class HubController : MonoBehaviour
    {
        [SerializeField] internal Button talentsButton;
        [SerializeField] internal Button principalityButton;
        [SerializeField] internal Button characterSheetButton;
        [SerializeField] internal Button relicsButton;
        [SerializeField] internal Button startRunButton;
        [SerializeField] internal Button mainMenuButton;
        [SerializeField] internal TMP_Text currencyLabel;

        private void Start()
        {
            RefreshCurrency(0, 0);

            talentsButton.onClick.AddListener(() => Debug.Log("Talents"));
            principalityButton.onClick.AddListener(() => Debug.Log("Principality"));
            characterSheetButton.onClick.AddListener(() => Debug.Log("Character sheet"));
            relicsButton.onClick.AddListener(() => Debug.Log("Relics"));
            // The two that go somewhere. The other four are screens that do not
            // exist yet, and a button that logs is more honest than one that
            // loads an empty scene.
            startRunButton.onClick.AddListener(StartOrResumeRun);
            mainMenuButton.onClick.AddListener(() => Navigation.Go(Navigation.MainMenu));
        }

        public void RefreshCurrency(int gold, int relics)
        {
            currencyLabel.Set(UiStrings.WalletSummary, gold, relics);
        }
    
        // The gate both STARTS and RESUMES.
        //
        // One button rather than two, because the player's intent is the same
        // ("go down") and a run they forgot they had is not a different
        // decision. A second button would also need a rule for what happens
        // when it is pressed with no run, which is a state the design does not
        // have.
        private void StartOrResumeRun()
        {
            if (!RunManager.HasRun)
            {
                // Seeded from the save so a slot's descent is ITS OWN and
                // reproduces on resume. Not Random: a run that reshuffled its
                // own map when reloaded would make the map screen a lie.
                RunManager.StartRun(RunManager.NewSeed());
            }

            // Into the MAP, not straight into a fight. Which room to enter is
            // the player's first decision of a descent, and skipping it was the
            // placeholder this replaces.
            Navigation.Go(Navigation.Map);
        }
}
}
