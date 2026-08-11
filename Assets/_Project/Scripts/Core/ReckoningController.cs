using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The Reckoning, driven.
    //
    // Everything it SHOWS was decided before it opened: RewardApplier already
    // applied the experience and wrote the save, RunManager already banked the
    // gold. That ordering is deliberate and worth stating, because it is what
    // makes the screen safe to quit out of -- nothing here is load-bearing
    // except the item choice, which is the one decision left.
    //
    // The expand and the bar fills are presentation only. If the coroutine
    // never finished, every number would still be correct; it would just
    // arrive instantly.
    public class ReckoningController : MonoBehaviour
    {
        [SerializeField] internal RectTransform frame;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Button continueButton;
        [SerializeField] internal GameObject lootHeading;

        [SerializeField] internal GameObject[] rowGroups;
        [SerializeField] internal TMP_Text[] rowNames;
        [SerializeField] internal TMP_Text[] rowLevels;
        [SerializeField] internal Image[] rowBarFills;
        [SerializeField] internal Image[] rowBarBefores;
        [SerializeField] internal TMP_Text[] rowGains;

        [SerializeField] internal Button[] offerButtons;
        [SerializeField] internal TMP_Text[] offerNames;
        [SerializeField] internal TMP_Text[] offerMetas;

        private const float ExpandSeconds = 0.28f;
        private const float BarSeconds = 0.55f;
        private const float BarStagger = 0.12f;

        // Where the expand starts. Not zero: a panel growing from nothing reads
        // as a popup, while one growing from most of its size reads as the
        // screen settling into place.
        private const float ExpandFrom = 0.82f;

        private CombatReward _reward;
        private List<ItemOffer> _offers = new List<ItemOffer>();
        private bool _taken;
        private bool _wired;
        private Coroutine _animation;

        // Raised when the player dismisses it. An event rather than a call into
        // Navigation, for the same reason FightController.FightEnded is one:
        // the reward screen has no business knowing what comes after a fight.
        public System.Action Dismissed;

        private void Start()
        {
            Wire();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            continueButton.onClick.AddListener(() => Dismissed?.Invoke());

            for (int i = 0; i < offerButtons.Length; i++)
            {
                int index = i;
                offerButtons[i].onClick.AddListener(() => Take(index));
            }
        }

        // Opened with what the fight paid and what it offers.
        //
        // Both are handed in rather than fetched. The controller reading
        // FightBootstrap.LastReward itself would tie this screen to the one
        // bootstrap that happens to own it today, and the Reckoning is the sort
        // of thing a test wants to drive with a fixture.
        public void Show(CombatReward reward, List<ItemOffer> offers)
        {
            _reward = reward;
            _offers = offers ?? new List<ItemOffer>();
            _taken = false;

            gameObject.SetActive(true);
            Wire();
            Paint();

            if (_animation != null) StopCoroutine(_animation);

            // Only animate when the object is actually live in the hierarchy.
            // StartCoroutine on an inactive object throws, and a headless test
            // that never activates the canvas should still get correct numbers.
            if (isActiveAndEnabled) _animation = StartCoroutine(PlayIn());
            else SettleImmediately();
        }

        // ---- painting -------------------------------------------------------------

        private void Paint()
        {
            goldLabel.Set(UiStrings.ReckoningGold, _reward?.GoldGained ?? 0);

            var characters = _reward?.Characters ?? new List<CharacterReward>();

            for (int i = 0; i < rowGroups.Length; i++)
            {
                bool present = i < characters.Count;

                // Hidden rather than blank, the same rule the bag and the debug
                // list follow: empty furniture reads as a load failure.
                rowGroups[i].SetActive(present);
                if (!present) continue;

                var row = characters[i];
                rowNames[i].SetContent(row.DisplayName);

                if (row.IsDowned)
                {
                    // A squad member left out of the encounter still gets a
                    // row. Dropping them made the party silently change size
                    // between the fight and the results -- three went in, two
                    // came back, and nothing said why.
                    rowLevels[i].Set(UiStrings.ReckoningDowned);
                    rowGains[i].SetContent("");
                }
                else
                {
                    rowLevels[i].Set(
                        row.LevelledUp ? UiStrings.ReckoningLevelUp : UiStrings.ReckoningLevel,
                        row.LevelAfter);
                    rowGains[i].Set(UiStrings.ReckoningExpGain, row.ExpGained);
                }

                // The dim segment runs 0 -> where they already stood. The
                // bright one starts WHERE THAT ENDS and grows to the total.
                //
                // Both anchored from the left would put the bright bar on top
                // of the dim one -- it is declared last, so it paints over --
                // and the "before" segment would never be visible at all. The
                // whole point of CharacterReward carrying a before is that the
                // player can see what THIS fight was worth rather than only
                // where they ended up.
                float before = row.IsDowned ? row.BarFill01() : row.BarFillBefore01();

                SetSpan(rowBarBefores[i], 0f, before);
                SetSpan(rowBarFills[i], before, before);
            }

            PaintOffers();
        }

        private void PaintOffers()
        {
            bool any = _offers.Count > 0;
            if (lootHeading != null) lootHeading.SetActive(any);

            for (int i = 0; i < offerButtons.Length; i++)
            {
                bool present = i < _offers.Count;
                offerButtons[i].gameObject.SetActive(present);
                if (!present) continue;

                var offer = _offers[i];
                var item = ContentDatabase.GetItem(offer.ItemId);

                // Graceful degradation: an offer naming content that no longer
                // exists shows its id rather than taking the screen down.
                offerNames[i].SetContent(item == null
                    ? offer.ItemId
                    : RarityColors.NameOf(item, offer.Plus));
                offerMetas[i].Set(UiStrings.ReckoningOfferMeta,
                    item == null ? "?" : item.Rarity.ToString(), offer.Tier);

                // Every offer stays visible after one is taken, and all of them
                // stop responding. Hiding the two not chosen would erase the
                // decision the player just made from the screen that asked for
                // it.
                offerButtons[i].interactable = !_taken;
            }
        }

        // ---- the one decision -------------------------------------------------------

        private void Take(int index)
        {
            if (_taken || index < 0 || index >= _offers.Count) return;

            var save = SaveSlotManager.CurrentSave;
            if (save == null) return;

            var offer = _offers[index];
            _taken = true;

            // Straight to the meta stash, which is the single live bag -- the
            // same one the character overlay and the fight satchel read.
            InventoryOps.Add(save.stockpiledItems, offer.ItemId, 1, offer.Plus);
            SaveSlotManager.SaveCurrent();

            if (_reward != null)
            {
                var item = ContentDatabase.GetItem(offer.ItemId);
                _reward.ChosenItemId = offer.ItemId;
                _reward.ChosenItemName = item == null ? offer.ItemId : RarityColors.NameOf(item, offer.Plus);
            }

            offerNames[index].Set(UiStrings.ReckoningTaken);
            PaintOffers();
        }

        public bool HasTakenAnItem => _taken;

        // ---- animation ---------------------------------------------------------------

        private IEnumerator PlayIn()
        {
            frame.localScale = Vector3.one * ExpandFrom;

            for (float t = 0f; t < ExpandSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / ExpandSeconds);
                frame.localScale = Vector3.one * Mathf.Lerp(ExpandFrom, 1f, k * k * (3f - 2f * k));
                yield return null;
            }

            frame.localScale = Vector3.one;

            // THEN the bars, staggered down the party. Sequenced after the
            // expand rather than during it, because a bar filling inside a
            // panel that is still growing reads as one confused motion.
            var characters = _reward?.Characters ?? new List<CharacterReward>();

            for (int i = 0; i < characters.Count && i < rowBarFills.Length; i++)
            {
                if (characters[i].IsDowned) continue;
                StartCoroutine(FillBar(i, characters[i], i * BarStagger));
            }

            _animation = null;
        }

        private IEnumerator FillBar(int index, CharacterReward row, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            float from = row.BarFillBefore01();
            float to = row.BarFill01();

            for (float t = 0f; t < BarSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / BarSeconds);
                SetSpan(rowBarFills[index], from, Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
                yield return null;
            }

            SetSpan(rowBarFills[index], from, to);
        }

        // Everything the animation would have arrived at, at once. Used when
        // there is no live hierarchy to animate in.
        private void SettleImmediately()
        {
            if (frame != null) frame.localScale = Vector3.one;

            var characters = _reward?.Characters ?? new List<CharacterReward>();
            for (int i = 0; i < characters.Count && i < rowBarFills.Length; i++)
            {
                SetSpan(rowBarFills[i], characters[i].BarFillBefore01(), characters[i].BarFill01());
            }
        }

        // A SEGMENT of the track, from one fraction to another.
        //
        // Moves the ANCHORS, not the scale. localScale scales about the centre,
        // so a bar sized that way grows from the middle in both directions --
        // the exact bug already fixed once on the fight HUD's health bars.
        //
        // Takes a `from` as well as a `to` because this track carries two
        // segments that must sit BESIDE each other rather than on top of each
        // other; a fill-from-zero helper cannot express the second one.
        private static void SetSpan(Image fill, float from, float to)
        {
            if (fill == null) return;

            float min = Mathf.Clamp01(from);
            float max = Mathf.Clamp01(to);
            if (max < min) max = min;

            var rect = (RectTransform)fill.transform;
            rect.anchorMin = new Vector2(min, 0f);
            rect.anchorMax = new Vector2(max, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
