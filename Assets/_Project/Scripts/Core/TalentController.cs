using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Ambience;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The constellations, driven.
    //
    // Every rule about what can be kindled lives in TalentPage, in Domain, with
    // its own tests. This decides nothing -- it reads the save, asks Domain, and
    // paints. The one thing it genuinely owns is the SLIDE, because that is time
    // and time needs a frame.
    public class TalentController : MonoBehaviour
    {
        [SerializeField] internal RectTransform sky;
        [SerializeField] internal Button[] orbs;
        [SerializeField] internal Image[] orbGlows;
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text pathName;
        [SerializeField] internal TMP_Text emberCount;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailBody;
        [SerializeField] internal TMP_Text investLabel;
        [SerializeField] internal Button investButton;
        [SerializeField] internal Button prevPathButton;
        [SerializeField] internal Button nextPathButton;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;
        [SerializeField] internal Button backButton;

        // Orb states, graded so the tree reads at a glance rather than needing
        // to be studied: yours is bright, reachable is warm, everything else is
        // barely there.
        private static readonly Color OrbTaken = new Color(1f, 0.85f, 0.55f, 1f);
        private static readonly Color OrbReachable = new Color(0.95f, 0.78f, 0.45f, 0.75f);
        private static readonly Color OrbDistant = new Color(0.45f, 0.40f, 0.58f, 0.5f);
        private static readonly Color GlowTaken = new Color(1f, 0.80f, 0.45f, 0.55f);
        private static readonly Color GlowReachable = new Color(1f, 0.80f, 0.45f, 0.16f);
        private static readonly Color GlowOff = new Color(1f, 0.80f, 0.45f, 0f);

        private int _path;
        private int _character;
        private int _selectedSlot = -1;
        private bool _wired;

        // The slide, in progress. Held rather than run as a coroutine so a
        // second press mid-slide retargets it instead of starting a second one
        // that fights the first for the same rect.
        private float _slideElapsed;
        private int _slideFrom;
        private bool _sliding;

        private void Start()
        {
            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < orbs.Length; i++)
            {
                int index = i;
                orbs[i].onClick.AddListener(() => OnOrbPressed(index));
            }

            prevPathButton.onClick.AddListener(() => StepPath(-1));
            nextPathButton.onClick.AddListener(() => StepPath(1));
            prevCharacterButton.onClick.AddListener(() => StepCharacter(-1));
            nextCharacterButton.onClick.AddListener(() => StepCharacter(1));
            investButton.onClick.AddListener(Kindle);
            backButton.onClick.AddListener(() => Navigation.Go(Navigation.Hub));
        }

        // ---- who and where ---------------------------------------------------

        private List<Character> Roster =>
            SaveSlotManager.CurrentSave?.roster ?? new List<Character>();

        private Character Current =>
            Roster.Count == 0 ? null : Roster[Mathf.Clamp(_character, 0, Roster.Count - 1)];

        private HashSet<string> Unlocked =>
            new HashSet<string>(Current?.unlockedTalentIds ?? new List<string>());

        private int Embers => SaveSlotManager.CurrentSave?.wallet.embers ?? 0;

        // ---- input -----------------------------------------------------------

        private void StepPath(int direction)
        {
            int next = ConstellationLayout.Step(_path, direction, TalentPage.PathCount);
            if (next == _path) return;

            // Slid from wherever it currently IS, not from the last settled
            // page: pressing twice quickly should carry on from mid-flight
            // rather than snapping back and starting again.
            _slideFrom = _path;
            _path = next;
            _slideElapsed = 0f;
            _sliding = true;

            _selectedSlot = -1;
            Refresh();
        }

        private void StepCharacter(int direction)
        {
            if (Roster.Count <= 1) return;

            _character = ConstellationLayout.Step(_character, direction, Roster.Count);

            // A different character's constellations are a different tree, so
            // the selection cannot survive the switch -- it would describe an
            // orb that is no longer under the cursor.
            _selectedSlot = -1;
            Refresh();
        }

        private void OnOrbPressed(int index)
        {
            int path = index / TalentScreen.OrbCount;
            int slot = index % TalentScreen.OrbCount;

            // Only the page being LOOKED AT is clickable. The other two are
            // sitting off-screen with live buttons on them, and a stray click
            // landing there would kindle something the player cannot see.
            if (path != _path) return;

            _selectedSlot = slot;
            Refresh();
        }

        private void Kindle()
        {
            var character = Current;
            if (character == null || _selectedSlot < 0) return;

            var save = SaveSlotManager.CurrentSave;
            if (save == null) return;

            if (!TalentPage.CanInvest(character.definitionId, _path, _selectedSlot, Unlocked, Embers)) return;

            character.unlockedTalentIds.Add(
                TalentPage.SlotId(character.definitionId, _path, _selectedSlot));
            save.wallet.embers -= TalentPage.EmberCost;

            // Written immediately. A talent tree that loses a kindled orb to a
            // crash is the single least forgivable thing this screen could do.
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // ---- painting --------------------------------------------------------

        public void Refresh()
        {
            var character = Current;
            if (character == null) return;

            characterName.SetContent(character.definitionId);
            emberCount.Set(UiStrings.TalentEmbers, Embers);

            var unlocked = Unlocked;
            pathName.Set(UiStrings.TalentPath, _path + 1, TalentPage.PathCount,
                TalentPage.SpentOn(character.definitionId, _path, unlocked));

            PaintOrbs(character.definitionId, unlocked);
            PaintDetail(character.definitionId, unlocked);

            // Arrows that cannot go anywhere are dimmed rather than hidden, so
            // the screen does not change shape as the player pages.
            prevCharacterButton.interactable = Roster.Count > 1;
            nextCharacterButton.interactable = Roster.Count > 1;
        }

        private void PaintOrbs(string characterId, HashSet<string> unlocked)
        {
            for (int path = 0; path < TalentPage.PathCount; path++)
            {
                for (int slot = 0; slot < TalentScreen.OrbCount; slot++)
                {
                    int index = TalentScreen.OrbIndex(path, slot);
                    if (index >= orbs.Length) continue;

                    var refusal = TalentPage.Evaluate(characterId, path, slot, unlocked, TalentPage.EmberCost);
                    bool taken = refusal == TalentPage.Refusal.AlreadyTaken;
                    bool reachable = refusal == TalentPage.Refusal.None;

                    var image = orbs[index].targetGraphic as Image;
                    if (image != null) image.color = taken ? OrbTaken : reachable ? OrbReachable : OrbDistant;

                    if (index < orbGlows.Length && orbGlows[index] != null)
                    {
                        orbGlows[index].color = taken ? GlowTaken : reachable ? GlowReachable : GlowOff;
                    }

                    // Everything stays PRESSABLE, including what cannot be
                    // afforded: pressing an orb selects it and the detail plate
                    // is where the refusal is explained. An uninteractable orb
                    // can only say "no" by doing nothing.
                    orbs[index].interactable = path == _path;
                }
            }
        }

        private void PaintDetail(string characterId, HashSet<string> unlocked)
        {
            if (_selectedSlot < 0)
            {
                detailName.SetContent("");
                detailBody.SetContent("");
                investButton.gameObject.SetActive(false);
                return;
            }

            investButton.gameObject.SetActive(true);

            var refusal = TalentPage.Evaluate(characterId, _path, _selectedSlot, unlocked, Embers);
            detailName.SetContent(TalentSkeleton.Kind[_selectedSlot].ToUpperInvariant());
            detailBody.SetContent(Explain(refusal));

            investButton.interactable = refusal == TalentPage.Refusal.None;
            investLabel.Set(LabelFor(refusal));
        }

        // The refusal the player is told about is the FIRST one that applies,
        // in the order Domain ranks them -- being told "not enough Embers" for
        // an orb three tiers out of reach sends someone to farm a currency they
        // did not need.
        private static string Explain(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return "Already kindled.";
                case TalentPage.Refusal.PrerequisiteMissing: return "Kindle the star before it first.";
                case TalentPage.Refusal.NotEnoughEmbers: return "Not enough Embers.";
                default: return "Ready to kindle.";
            }
        }

        private static UiString LabelFor(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return UiStrings.TalentTaken;
                case TalentPage.Refusal.PrerequisiteMissing: return UiStrings.TalentLocked;
                case TalentPage.Refusal.NotEnoughEmbers: return UiStrings.TalentNoEmbers;
                default: return UiStrings.TalentInvest;
            }
        }

        // ---- the slide -------------------------------------------------------

        private void Update()
        {
            if (sky == null) return;

            if (!_sliding)
            {
                sky.anchoredPosition = new Vector2(SettledX(), sky.anchoredPosition.y);
                return;
            }

            _slideElapsed += Time.deltaTime;
            float progress = ConstellationLayout.SlideProgress(_slideElapsed);

            sky.anchoredPosition = new Vector2(
                ConstellationLayout.SlideOffset(_slideFrom, _path, progress),
                sky.anchoredPosition.y);

            if (progress >= 1f) _sliding = false;
        }

        private float SettledX() => ConstellationLayout.SlideOffset(_path, _path, 1f);
    }
}
