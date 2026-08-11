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
        [SerializeField] internal GameObject characterOverlayPanel;
        [SerializeField] internal GameObject debugMenuPanel;
        [SerializeField] internal RelicDraftController relicDraft;
        [SerializeField] internal GameObject glossaryPanel;
        [SerializeField] internal Button principalityButton;
        [SerializeField] internal Button characterSheetButton;
        [SerializeField] internal Button relicsButton;
        [SerializeField] internal Button startRunButton;
        [SerializeField] internal Button mainMenuButton;
        [SerializeField] internal TMP_Text currencyLabel;
        [SerializeField] internal TMP_Text startRunCaption;
        [SerializeField] internal Button[] unbuiltButtons;

        // OnEnable, not Start: the hub is returned to repeatedly -- from a
        // finished fight, from an abandoned run -- and Start fires once. Gold
        // banked during a descent has to be on the plate when the player gets
        // back, not one scene load later.
        private void OnEnable()
        {
            Refresh();
        }

        // What the screen says about the save. Everything here is read, never
        // stored: a second copy of the wallet on this controller is a second
        // thing that can be wrong.
        public void Refresh()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save != null)
            {
                // Embers are per CHARACTER now, so the hub shows the roster's total --
                // an "unspent somewhere" figure. The per-character breakdown is
                // the talent screen's job, which is where they are spent.
                currencyLabel.Set(UiStrings.HubWallet, save.wallet.gold, EmberTotal(save));
            }

            RefreshGateCaption();
            DimTheUnbuilt();
        }

        // The gate knows whether you are starting or going back down. No new
        // save field: RunManager already knows, and the floor is already stored.
        private void RefreshGateCaption()
        {
            if (startRunCaption == null) return;

            if (RunManager.HasRun) startRunCaption.Set(UiStrings.HubResumeFloor, RunManager.Run.floor);
            else startRunCaption.Set(UiStrings.HubBeginDescent);
        }

        // Buildings whose screens do not exist yet hang dark and unpressable.
        //
        // Dimmed rather than hidden, and rather than a click that logs a
        // refusal: v2 has no bark or sound machinery to make a refusal feel
        // deliberate, so a press that produces nothing reads as a broken button.
        // A dark, still building over the void reads as a place that is asleep,
        // which is the mystery the design is after. The list shrinks as screens
        // land.
        private void DimTheUnbuilt()
        {
            if (unbuiltButtons == null) return;

            foreach (var button in unbuiltButtons)
            {
                if (button == null) continue;

                button.interactable = false;
                if (button.targetGraphic is UnityEngine.UI.Image image)
                {
                    image.color = UnbuiltTint;
                }
            }
        }

        private static int EmberTotal(SaveData save)
        {
            int total = 0;
            foreach (var character in save.roster ?? new System.Collections.Generic.List<Character>())
            {
                if (character != null) total += character.embers;
            }

            return total;
        }

        private static readonly Color UnbuiltTint = new Color(0.55f, 0.55f, 0.62f, 1f);

        private void Start()
        {

            // The first of the four annexes to actually lead somewhere.
            talentsButton.onClick.AddListener(() => Navigation.Go(Navigation.Talents));
            principalityButton.onClick.AddListener(() => Debug.Log("Principality"));
            // An overlay, not a scene load: it opens over the hub and the hub
            // is still standing behind it when it closes.
            characterSheetButton.onClick.AddListener(() => SetCharacterOverlay(true));
            // The Relics building opens the RECORD, not a relic screen.
            // Relics stopped being permanent progression, so a screen about
            // owning them had nothing to show; a record of every relic,
            // monster, spell, item, talent and deed does.
            relicsButton.onClick.AddListener(() => SetGlossary(true));
            // The two that go somewhere. The other four are screens that do not
            // exist yet, and a button that logs is more honest than one that
            // loads an empty scene.
            startRunButton.onClick.AddListener(StartOrResumeRun);
            mainMenuButton.onClick.AddListener(() => Navigation.Go(Navigation.MainMenu));
        }

        // ---- keyboard ------------------------------------------------------------

        // C and I open the character overlay; Escape closes it.
        //
        // Both keys land on the SAME overlay. v1 had two separate screens and a
        // key each -- an inventory and a paperdoll that could only agree because
        // both called the same service. v2 merged them, so honouring both keys
        // costs nothing and spares anyone their muscle memory.
        //
        // This lives on the hub rather than on CharacterOverlayController
        // because that component sits on the modal node, which is INACTIVE
        // while the overlay is closed. An Update() on a disabled object cannot
        // be the thing that opens it.
        private void Update()
        {
            // F1 for the debug menu, and ONLY in the editor or a development
            // build. Debug.isDebugBuild is true for both and false in a release
            // player, so a shipped build has no key that grants 10,000 gold.
            //
            // The gate is on the KEY, not on ToggleDebugMenu -- the methods stay
            // callable so PlayMode can drive them, and PlayMode runs in the
            // editor where this is true anyway.
            if (Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F1))
            {
                ToggleDebugMenu();
                return;
            }

            if (characterOverlayPanel == null) return;

            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.I))
            {
                ToggleCharacterOverlay();
            }
            // Only swallowed while something is up, so Escape stays free to
            // mean something else on the hub itself later. The debug menu is
            // checked first because it draws over the overlay -- closing the
            // thing underneath the thing you can see would be a nasty little
            // surprise.
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (debugMenuPanel != null && debugMenuPanel.activeSelf) SetDebugMenu(false);
                else if (glossaryPanel != null && glossaryPanel.activeSelf) SetGlossary(false);
                else if (characterOverlayPanel.activeSelf) SetCharacterOverlay(false);
            }
        }

        // The key read above is deliberately separated from the action here:
        // legacy Input cannot be simulated headlessly, so a test that had to
        // press C could not exist. Tests drive these two directly -- and so
        // does the Character Sheet building, which means the button and the
        // key can never drift apart.
        // Read-only, so callers outside Core can ask without being able to
        // reach past the two methods that are allowed to answer. Null-tolerant
        // for the same reason those are: a scene may mount this controller
        // without an overlay.
        public bool CharacterOverlayIsOpen =>
            characterOverlayPanel != null && characterOverlayPanel.activeSelf;

        public void ToggleCharacterOverlay()
        {
            if (characterOverlayPanel == null) return;
            SetCharacterOverlay(!characterOverlayPanel.activeSelf);
        }

        public void SetCharacterOverlay(bool open)
        {
            if (characterOverlayPanel == null) return;
            characterOverlayPanel.SetActive(open);
        }

        public bool GlossaryIsOpen => glossaryPanel != null && glossaryPanel.activeSelf;

        public void SetGlossary(bool open)
        {
            if (glossaryPanel == null) return;
            glossaryPanel.SetActive(open);
        }

        public bool DebugMenuIsOpen => debugMenuPanel != null && debugMenuPanel.activeSelf;

        public void ToggleDebugMenu()
        {
            if (debugMenuPanel == null) return;
            SetDebugMenu(!debugMenuPanel.activeSelf);
        }

        public void SetDebugMenu(bool open)
        {
            if (debugMenuPanel == null) return;
            debugMenuPanel.SetActive(open);
        }

        // Kept for the tests and callers that set a wallet explicitly. Goes
        // through the same three-currency line the save-backed path uses, so
        // the two cannot disagree about the format.
        public void RefreshCurrency(int gold, int embers = 0)
        {
            currencyLabel.Set(UiStrings.HubWallet, gold, embers);
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

            // The relic draft stands between the gate and the map, and only on
            // a run that has not drafted yet. RESUMING walks straight past it:
            // the relic was chosen when this descent began, and offering again
            // would let a player re-roll it by walking back to the hub.
            if (relicDraft != null && RunManager.HasRun && !RunManager.Run.relicDrafted)
            {
                relicDraft.Finished = () => Navigation.Go(Navigation.Map);
                relicDraft.Open(RunManager.Run.runSeed);
                return;
            }

            // Into the MAP, not straight into a fight. Which room to enter is
            // the player's first decision of a descent, and skipping it was the
            // placeholder this replaces.
            Navigation.Go(Navigation.Map);
        }
}
}
