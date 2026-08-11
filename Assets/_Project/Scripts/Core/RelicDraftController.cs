using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The start-of-run relic draft, driven.
    //
    // Opened by the hub the moment a NEW run begins and closed by its own
    // Descend button, which is the only way to the map. That ordering is the
    // whole design: a draft you can navigate around is not a draft.
    //
    // Rolled from the RUN'S OWN SEED rather than from UnityEngine.Random, so
    // the same descent offers the same three relics if it is reloaded before
    // the choice is made. A draft that reshuffled on reload would let a player
    // re-roll it by quitting to the menu, which is the same class of problem
    // as a map that regenerates.
    public class RelicDraftController : MonoBehaviour
    {
        [SerializeField] internal Button[] cards;
        [SerializeField] internal Image[] cardSelections;
        [SerializeField] internal Image[] cardIcons;
        [SerializeField] internal TMP_Text[] cardNames;
        [SerializeField] internal TMP_Text[] cardRarities;
        [SerializeField] internal TMP_Text[] cardBodies;

        [SerializeField] internal GameObject emptyHint;
        [SerializeField] internal Button descendButton;

        // Art, as two parallel arrays -- a scene serialises arrays and does not
        // serialise dictionaries. Same shape the character overlay uses.
        [SerializeField] internal string[] iconIds;
        [SerializeField] internal Sprite[] iconSprites;

        private static readonly Color RingLit = new Color(0.95f, 0.86f, 0.62f, 1f);
        private static readonly Color RingDark = new Color(0.95f, 0.86f, 0.62f, 0f);

        private readonly List<RelicOption> _offer = new List<RelicOption>();
        private int _selected = -1;
        private bool _wired;

        // Raised when the player leaves the draft. An event rather than a
        // Navigation call: the draft has no business knowing that a descent
        // starts on the map screen.
        public System.Action Finished;

        private void Start()
        {
            Wire();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].onClick.AddListener(() => Select(index));
            }

            descendButton.onClick.AddListener(Commit);
        }

        // Rolls a fresh offer and shows it. Called by the hub when a run starts.
        public void Open(ulong seed)
        {
            gameObject.SetActive(true);
            Wire();

            _selected = -1;
            _offer.Clear();
            _offer.AddRange(Roll(seed));

            Paint();
        }

        private IEnumerable<RelicOption> Roll(ulong seed)
        {
            var save = SaveSlotManager.CurrentSave;
            var earned = Achievements.EarnedIds(save);

            var all = ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r => new RelicOption(r.id, r.rarity, r.unlockedBy))
                .ToList();

            var available = RelicPool.Available(all, earned);

            // Weighted, so a Godlike relic stays a story. The seed is the run's
            // own, so reloading before choosing offers the same three.
            var rng = new Domain.Rng.SeededRandom(seed);
            return RelicPool.DraftWeighted(available, bound => rng.NextInt(0, bound));
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _offer.Count) return;

            // Re-pressing the chosen card DESELECTS. A draft where the first
            // click is final would be a trap on a screen whose whole job is
            // letting the player compare three things.
            _selected = _selected == index ? -1 : index;
            Paint();
        }

        private void Paint()
        {
            bool anything = _offer.Count > 0;
            if (emptyHint != null) emptyHint.SetActive(!anything);

            for (int i = 0; i < cards.Length; i++)
            {
                bool present = i < _offer.Count;
                cards[i].gameObject.SetActive(present);
                if (!present) continue;

                var option = _offer[i];
                var definition = ContentDatabase.Relics.FirstOrDefault(r => r != null && r.id == option.Id);

                cardNames[i].SetContent(definition?.displayName ?? option.Id);
                cardRarities[i].Set(UiStrings.DraftRarity, RelicRarityNames.Of(option.Rarity));
                cardBodies[i].SetContent(definition?.description ?? "");

                ItemIcons.Apply(cardIcons[i], iconIds, iconSprites, option.Id);

                cardSelections[i].color = _selected == i ? RingLit : RingDark;
            }
        }

        private void Commit()
        {
            var run = RunManager.Run;
            if (run != null)
            {
                run.relicIds ??= new List<string>();

                // Nothing selected is a legal answer. Descending with no relic
                // is worse than descending with one, which is the player's
                // decision to make and not this screen's to refuse.
                if (_selected >= 0 && _selected < _offer.Count)
                {
                    run.relicIds.Add(_offer[_selected].Id);
                }

                // Marked drafted either way. Not derivable from the list being
                // empty: a player who declines must not be asked again every
                // time they walk back into the hub.
                run.relicDrafted = true;
                SaveSlotManager.SaveCurrent();
            }

            gameObject.SetActive(false);
            Finished?.Invoke();
        }
    }
}
