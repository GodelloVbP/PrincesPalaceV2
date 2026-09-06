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

        // The halo and burst behind the icon, same pair the Reckoning's item
        // offers wear -- see RelicDraftScreen.BuildCard.
        [SerializeField] internal Image[] cardHalos;
        [SerializeField] internal Image[] cardBursts;

        [SerializeField] internal GameObject emptyHint;
        [SerializeField] internal Button descendButton;

        // Level 70's paging. See Roll() and StepPage().
        [SerializeField] internal Button prevPageButton;
        [SerializeField] internal Button nextPageButton;
        [SerializeField] internal TMP_Text pageLabel;

        // Art, as two parallel arrays -- a scene serialises arrays and does not
        // serialise dictionaries. Same shape the character overlay uses.
        [SerializeField] internal IconEntry[] icons;

        // The selection glow's RGB is baked into the sprite's own colour at
        // build time (Ui.ThemeGlowHex(Violet), see RelicDraftScreen.BuildCard)
        // -- only the ALPHA toggles here, the same fade ThemedButtonState's
        // own focus glow uses (FocusGlowAlpha), rather than a second colour
        // pair that could disagree with the theme.

        private readonly List<RelicOption> _offer = new List<RelicOption>();
        private int _selected = -1;
        private bool _wired;

        // The run's seed, held so a later round can re-roll from it. The ROUND
        // is not held -- it is derived from run.relicIds.Count, so it survives
        // a reload; see Roll().
        private ulong _seed;

        // Which page of the offer is on screen. Only ever non-zero once
        // level 70 turns the offer into the whole pool.
        private int _page;

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

            if (prevPageButton != null) prevPageButton.onClick.AddListener(() => StepPage(-1));
            if (nextPageButton != null) nextPageButton.onClick.AddListener(() => StepPage(1));
        }

        // Rolls a fresh offer and shows it. Called by the hub when a run starts.
        public void Open(ulong seed)
        {
            gameObject.SetActive(true);
            Wire();

            _seed = seed;
            _selected = -1;
            _page = 0;
            _offer.Clear();
            _offer.AddRange(Roll(seed));

            Paint();
        }

        // What the CURRENT round offers -- RunOrchestrator.RelicDraftOffer,
        // where the roll moved so the bot drafts from the same pool with the
        // same weighting (docs/PLAN_BALANCE_BOT.md F2). Everything about WHY
        // the seed is offset by the round travelled with it.
        private IEnumerable<RelicOption> Roll(ulong seed) =>
            RunOrchestrator.RelicDraftOffer(seed);

        // How many pages the current offer needs. At least one, so an empty
        // pool still reads as a page rather than dividing by zero.
        private int PageCount =>
            _offer.Count == 0 ? 1 : (_offer.Count + cards.Length - 1) / cards.Length;

        // The offer index that card `cardIndex` is currently showing.
        private int AbsoluteIndex(int cardIndex) => _page * cards.Length + cardIndex;

        private void StepPage(int direction)
        {
            int next = _page + direction;
            if (next < 0 || next >= PageCount) return;

            // THE SELECTION SURVIVES THE PAGE TURN, because it is an index
            // into the whole offer rather than into the three cards. Clearing
            // it here would mean a player who chose something on page 1 and
            // then looked at page 2 came back to nothing selected -- the same
            // trap the deselect-on-second-press rule exists to avoid, arriving
            // from a different direction.
            _page = next;
            Paint();
        }

        private void Select(int cardIndex)
        {
            int index = AbsoluteIndex(cardIndex);
            if (index < 0 || index >= _offer.Count) return;

            // Re-pressing the chosen card DESELECTS. A draft where the first
            // click is final would be a trap on a screen whose whole job is
            // letting the player compare things.
            _selected = _selected == index ? -1 : index;
            Paint();
        }

        private void Paint()
        {
            bool anything = _offer.Count > 0;
            if (emptyHint != null) emptyHint.SetActive(!anything);

            // Paging exists only when there is more than one page, which is
            // only ever once level 70 opens the whole pool. Hidden rather than
            // disabled, like every other unearned reward on the track.
            bool paged = PageCount > 1;
            if (prevPageButton != null)
            {
                prevPageButton.gameObject.SetActive(paged);
                prevPageButton.interactable = _page > 0;
            }

            if (nextPageButton != null)
            {
                nextPageButton.gameObject.SetActive(paged);
                nextPageButton.interactable = _page < PageCount - 1;
            }

            if (pageLabel != null)
            {
                pageLabel.gameObject.SetActive(paged);
                if (paged) pageLabel.SetContent((_page + 1) + " / " + PageCount);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                int absolute = AbsoluteIndex(i);
                bool present = absolute < _offer.Count;
                cards[i].gameObject.SetActive(present);
                if (!present) continue;

                var option = _offer[absolute];
                var definition = ContentDatabase.Relics.FirstOrDefault(r => r != null && r.id == option.Id);

                cardNames[i].SetContent(definition?.Data.DisplayName ?? option.Id);
                cardRarities[i].Set(UiStrings.DraftRarity, RelicRarityNames.Of(option.Rarity));
                cardBodies[i].SetContent(definition?.Data.Description ?? "");

                ItemIcons.Apply(cardIcons[i], icons, option.Id);

                var glowColor = cardSelections[i].color;
                glowColor.a = _selected == absolute ? ThemedButtonState.FocusGlowAlpha : 0f;
                cardSelections[i].color = glowColor;

                // Same two-layer rarity read as the Reckoning's item offers:
                // a burst carrying the colour behind the icon and a softer
                // halo under that, since the burst's core is hidden by the
                // icon it sits behind. See ReckoningController.PaintOffers.
                var glow = RarityColors.For(option.Rarity);
                if (cardBursts != null && i < cardBursts.Length && cardBursts[i] != null)
                {
                    var strong = glow;
                    strong.a = 0.42f;
                    cardBursts[i].color = strong;
                }

                if (cardHalos != null && i < cardHalos.Length && cardHalos[i] != null)
                {
                    var soft = glow;
                    soft.a = 0.30f;
                    cardHalos[i].color = soft;
                }
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

            // Nothing selected is a legal answer. Descending with no relic is
            // worse than descending with one, which is the player's decision to
            // make and not this screen's to refuse.
            bool took = _selected >= 0 && _selected < _offer.Count;
            RunOrchestrator.TakeRelic(took ? _offer[_selected].Id : null);

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
            if (took && RunOrchestrator.DraftHasAnotherRound())
            {
                _selected = -1;
                _page = 0;
                _offer.Clear();
                _offer.AddRange(Roll(_seed));

                if (_offer.Count > 0)
                {
                    // Persisted BEFORE the next round is painted, so the round
                    // a reload comes back to is the one on screen.
                    RunOrchestrator.PersistDraft();
                    Paint();
                    return;
                }
            }

            RunOrchestrator.FinishDraft();
            Close();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Finished?.Invoke();
        }
    }
}
