using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;
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
    // the same descent offers the same cards if it is reloaded before the
    // choice is made. A draft that reshuffled on reload would let a player
    // re-roll it by quitting to the menu, which is the same class of problem
    // as a map that regenerates.
    //
    // ONE OFFER OF THREE, N TIMES -- not one wide offer of N+2.
    //
    // The reward track grants extra starting relics at levels 25, 45 and 60,
    // so a descent drafts between one and four. Rounds rather than a wider
    // offer, for three reasons: three separate choices of three is a better
    // decision than one choice of six; the card row is emitted at
    // scene-build time from RelicPool.OfferCount and a runtime-variable width
    // would mean emitting the maximum and hiding the surplus; and the round
    // number falls out of run.relicIds.Count, which is persisted, so a reload
    // mid-draft returns to the same round with the same cards.
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
        [SerializeField] internal IconEntry[] icons;

        private static readonly Color RingLit = new Color(0.95f, 0.86f, 0.62f, 1f);
        private static readonly Color RingDark = new Color(0.95f, 0.86f, 0.62f, 0f);

        private readonly List<RelicOption> _offer = new List<RelicOption>();
        private int _selected = -1;
        private bool _wired;

        // The run's seed, held so a later round can re-roll from it. The ROUND
        // is not held -- it is derived from run.relicIds.Count, so it survives
        // a reload; see Roll().
        private ulong _seed;

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

            _seed = seed;
            _selected = -1;
            _offer.Clear();
            _offer.AddRange(Roll(seed));

            Paint();
        }

        // How many relics this descent gets to draft.
        //
        // THE BEST LEVEL IN THE FIELDED SQUAD, not the sum and not the first
        // slot's. The same rule ItemOfferRoll.SquadFavor uses, and for the same
        // reason: a run-scoped reward on a per-character track has to resolve
        // to one number somehow, and "the best character you brought" makes
        // fielding them the decision. Summing would make it "bring more
        // bodies".
        //
        // This is the open question in docs/HANDOVER_PROGRESSION_TRACK.md 4c,
        // answered the cheap way while the squad is one character. The other
        // reading -- the benefit applies only while that character is fielded
        // -- is more interesting and needs a per-character notion of "whose
        // relic this is", which relicIds does not have.
        internal static int DraftCount()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return RewardTrack.BaseStartingRelics;

            int best = RewardTrack.BaseStartingRelics;
            foreach (var character in save.ActiveSquad())
            {
                int allowed = RewardTrack.StartingRelics(character?.level ?? 1);
                if (allowed > best) best = allowed;
            }

            return best;
        }

        // What the CURRENT round offers.
        //
        // The round is derived from how many relics the run already holds
        // rather than counted in a field, which is what makes a mid-draft
        // reload safe: relicIds is persisted, so coming back re-derives the
        // same round and -- because the seed is offset by that same count --
        // re-offers the same cards. A counter in the controller would reset to
        // round one and hand out a fresh offer, which is a re-roll by quitting.
        private IEnumerable<RelicOption> Roll(ulong seed)
        {
            var save = SaveSlotManager.CurrentSave;
            var earned = Achievements.EarnedIds(save);
            var alreadyHeld = RunManager.Run?.relicIds ?? new List<string>();

            var all = ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r => new RelicOption(r.id, r.rarity, r.unlockedBy))
                .ToList();

            // Already-drafted relics are out of the pool. Draft() draws without
            // replacement WITHIN one offer, which was the whole story when
            // there was only ever one offer; across rounds nothing stopped the
            // same relic coming back, and being offered what you are already
            // carrying reads as a bug.
            var available = RelicPool.Available(all, earned)
                .Where(r => !alreadyHeld.Contains(r.Id))
                .ToList();

            // Weighted, so a Godlike relic stays a story. The seed is the run's
            // own PLUS the round, so reloading before choosing offers the same
            // three and the second round is not a repeat of the first.
            var rng = new Domain.Rng.SeededRandom(seed + (ulong)alreadyHeld.Count);
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

                ItemIcons.Apply(cardIcons[i], icons, option.Id);

                cardSelections[i].color = _selected == i ? RingLit : RingDark;
            }
        }

        private void Commit()
        {
            var run = RunManager.Run;
            if (run == null)
            {
                Close();
                return;
            }

            run.relicIds ??= new List<string>();

            // Nothing selected is a legal answer. Descending with no relic is
            // worse than descending with one, which is the player's decision to
            // make and not this screen's to refuse.
            bool took = _selected >= 0 && _selected < _offer.Count;
            if (took)
            {
                run.relicIds.Add(_offer[_selected].Id);
            }

            // ANOTHER ROUND, if the track has earned one and the player took
            // this one.
            //
            // Declining ends the whole draft rather than advancing. A player
            // who does not want the relics on offer should not have to press
            // Descend three times to say so -- and the alternative, re-offering
            // until they accept something, is a draft they cannot leave, which
            // this screen's own Descend button exists to prevent.
            //
            // An empty offer also ends it: with a pool smaller than the number
            // of rounds, there is eventually nothing left to show, and looping
            // on an empty offer would strand the player on a blank screen.
            if (took && run.relicIds.Count < DraftCount())
            {
                _selected = -1;
                _offer.Clear();
                _offer.AddRange(Roll(_seed));

                if (_offer.Count > 0)
                {
                    // Persisted BEFORE the next round is painted, so the round
                    // a reload comes back to is the one on screen.
                    SaveSlotManager.SaveCurrent();
                    Paint();
                    return;
                }
            }

            // Marked drafted either way. Not derivable from the list being
            // empty: a player who declines must not be asked again every time
            // they walk back into the hub.
            run.relicDrafted = true;
            SaveSlotManager.SaveCurrent();

            Close();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Finished?.Invoke();
        }
    }
}
