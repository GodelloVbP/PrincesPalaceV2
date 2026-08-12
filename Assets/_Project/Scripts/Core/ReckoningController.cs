using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        [SerializeField] internal Image[] offerBursts;
        [SerializeField] internal Image[] offerIcons;
        [SerializeField] internal RectTransform[] offerBurstRects;

        // Item art, as two parallel arrays -- a scene serialises arrays and
        // does not serialise dictionaries. Same shape the overlay uses.
        [SerializeField] internal string[] iconIds;
        [SerializeField] internal Sprite[] iconSprites;

        [SerializeField] internal Button[] tabButtons;
        [SerializeField] internal Image[] tabMarkers;
        [SerializeField] internal GameObject[] pages;

        [SerializeField] internal Image dimmer;

        [SerializeField] internal GameObject relicEmptyHint;
        [SerializeField] internal GameObject[] relicRows;
        [SerializeField] internal TMP_Text[] relicNames;
        [SerializeField] internal TMP_Text[] relicMetas;
        [SerializeField] internal TMP_Text[] relicBodies;

        [SerializeField] internal GameObject[] tallyRows;
        [SerializeField] internal TMP_Text[] tallyNames;
        [SerializeField] internal TMP_Text[] tallyStats;
        [SerializeField] internal TMP_Text[] tallyKills;

        private const float ExpandSeconds = 0.28f;
        private const float BarSeconds = 0.55f;
        private const float BarStagger = 0.12f;

        // Where the expand starts. Not zero: a panel growing from nothing reads
        // as a popup, while one growing from most of its size reads as the
        // screen settling into place.
        private const float ExpandFrom = 0.82f;

        // The LIFT. The frame rises this far as it expands, so the screen
        // arrives from the fight rather than appearing at rest on top of it.
        // Small on purpose -- past about 60px it reads as a slide-in and the
        // expand stops being the thing you notice.
        private const float LiftFrom = -46f;

        // The gloom fades UP over the same beat instead of snapping on. A dim
        // that arrives in one frame is a scene cut, which is the one thing an
        // overlay is not supposed to be.
        private const float GloomSeconds = 0.34f;

        private static readonly Color MarkerLit = new Color(0.95f, 0.86f, 0.62f, 1f);
        private static readonly Color MarkerDark = new Color(0.95f, 0.86f, 0.62f, 0f);

        private int _tab;

        // Degrees per second. Slow: this is a glow behind an object, and a
        // burst spinning fast enough to notice as MOTION reads as a loading
        // spinner rather than as something precious sitting on a plinth.
        private const float BurstDegreesPerSecond = 9f;

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

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }

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
            // ALWAYS back to the payout. Reopening on whichever tab was last
            // read would bury the gold and the loot behind a click.
            _tab = 0;
            PaintTabs();
            PaintRelics();
            PaintTally();

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

                ItemIcons.Apply(offerIcons[i], iconIds, iconSprites, item?.id);

                // The burst wears the item's RARITY COLOUR, from the same table
                // the name above it and the character overlay's cell edges read.
                // Held well under full alpha: it is a glow behind an object, and
                // at full strength it competes with the object it exists to
                // frame.
                var glow = item == null ? Color.white : RarityColors.For(item);
                glow.a = _taken ? 0.18f : 0.42f;
                offerBursts[i].color = glow;

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

        private void Update()
        {
            if (offerBurstRects == null) return;

            float step = BurstDegreesPerSecond * Time.unscaledDeltaTime;

            for (int i = 0; i < offerBurstRects.Length; i++)
            {
                var rect = offerBurstRects[i];
                if (rect == null || !rect.gameObject.activeInHierarchy) continue;

                rect.localRotation = Quaternion.Euler(0f, 0f, rect.localEulerAngles.z + step);
            }
        }

        // ---- tabs ---------------------------------------------------------------------

        private void SelectTab(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;

            _tab = index;
            PaintTabs();
        }

        private void PaintTabs()
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null) pages[i].SetActive(i == _tab);
                if (i < tabMarkers.Length && tabMarkers[i] != null)
                {
                    tabMarkers[i].color = i == _tab ? MarkerLit : MarkerDark;
                }
            }
        }

        // ---- what the descent is carrying ----------------------------------------------

        private void PaintRelics()
        {
            // The RUN'S relics, read straight off the snapshot. The Reckoning
            // is the only screen a player sees between fights, so it is where
            // "what am I actually carrying" belongs.
            var held = RunManager.HasRun
                ? RunManager.Run.relicIds ?? new List<string>()
                : new List<string>();

            if (relicEmptyHint != null) relicEmptyHint.SetActive(held.Count == 0);

            for (int i = 0; i < relicRows.Length; i++)
            {
                bool present = i < held.Count;
                relicRows[i].SetActive(present);
                if (!present) continue;

                var definition = ContentDatabase.Relics.FirstOrDefault(r => r != null && r.id == held[i]);

                // A relic id naming content that is gone still gets a row: the
                // player is carrying SOMETHING and a silently shorter list
                // would be worse than an honest unknown.
                relicNames[i].SetContent(definition?.displayName ?? held[i]);
                relicMetas[i].SetContent(definition == null ? "" : RarityWord(definition.rarity));
                relicBodies[i].SetContent(definition?.description ?? "");
            }
        }

        private static string RarityWord(Domain.Content.RelicRarity rarity)
        {
            switch (rarity)
            {
                case Domain.Content.RelicRarity.Common: return "COMMON";
                case Domain.Content.RelicRarity.Uncommon: return "UNCOMMON";
                case Domain.Content.RelicRarity.Rare: return "RARE";
                case Domain.Content.RelicRarity.UltraRare: return "ULTRA-RARE";
                case Domain.Content.RelicRarity.Mythic: return "MYTHIC";
                default: return "GODLIKE";
            }
        }

        // ---- what everyone did ----------------------------------------------------------

        private void PaintTally()
        {
            var characters = _reward?.Characters ?? new List<CharacterReward>();
            var ledger = _reward?.Ledger;

            for (int i = 0; i < tallyRows.Length; i++)
            {
                bool present = i < characters.Count;
                tallyRows[i].SetActive(present);
                if (!present) continue;

                var character = characters[i];
                var line = ledger?.For(character.CharacterId);

                tallyNames[i].SetContent(character.DisplayName);
                tallyKills[i].Set(UiStrings.ReckoningKills, line?.Kills ?? 0);

                // A character who was benched reads as zeroes rather than
                // vanishing -- "you did nothing" is a true and useful thing to
                // be told, and a missing row is a bug the player has to guess
                // at. Same rule the defeat screen holds.
                tallyStats[i].Set(UiStrings.ReckoningTallyLine,
                    line?.TotalDealt ?? 0,
                    line?.PhysicalDealt ?? 0,
                    line?.OtherDealt ?? 0,
                    line?.DamageTaken ?? 0,
                    line?.Healed ?? 0);
            }
        }

        // ---- animation ---------------------------------------------------------------

        private IEnumerator PlayIn()
        {
            frame.localScale = Vector3.one * ExpandFrom;
            frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, LiftFrom);

            // The gloom runs on its OWN clock, slightly longer than the expand,
            // so the fight is still readable for the first frames the panel is
            // growing over it. Both start together; the dim finishes last.
            float gloomTarget = dimmer != null ? dimmer.color.a : 0f;
            if (dimmer != null) SetDimmerAlpha(0f);

            float elapsed = 0f;
            float longest = Mathf.Max(ExpandSeconds, GloomSeconds);

            while (elapsed < longest)
            {
                elapsed += Time.unscaledDeltaTime;

                float k = Smooth(Mathf.Clamp01(elapsed / ExpandSeconds));
                frame.localScale = Vector3.one * Mathf.Lerp(ExpandFrom, 1f, k);
                frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, Mathf.Lerp(LiftFrom, 0f, k));

                if (dimmer != null)
                {
                    SetDimmerAlpha(gloomTarget * Smooth(Mathf.Clamp01(elapsed / GloomSeconds)));
                }

                yield return null;
            }

            frame.localScale = Vector3.one;
            frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, 0f);
            if (dimmer != null) SetDimmerAlpha(gloomTarget);

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
                float k = Smooth(Mathf.Clamp01(t / BarSeconds));
                SetSpan(rowBarFills[index], from, Mathf.Lerp(from, to, k));

                // The NUMBER climbs with the bar. Two readouts of one fact
                // moving together, rather than a bar that fills beside a total
                // that was already sitting there -- which is the version that
                // makes the bar look decorative.
                CountGain(index, Mathf.RoundToInt(row.ExpGained * k));
                yield return null;
            }

            SetSpan(rowBarFills[index], from, to);
            CountGain(index, row.ExpGained);
        }

        // Smoothstep. One definition, because three animations easing
        // differently by accident is the sort of thing nobody can name but
        // everybody feels.
        private static float Smooth(float k) => k * k * (3f - 2f * k);

        private void SetDimmerAlpha(float alpha)
        {
            var colour = dimmer.color;
            colour.a = alpha;
            dimmer.color = colour;
        }

        private void CountGain(int index, int amount)
        {
            if (index < 0 || index >= rowGains.Length || rowGains[index] == null) return;
            rowGains[index].Set(UiStrings.ReckoningExpGain, amount);
        }

        // Everything the animation would have arrived at, at once. Used when
        // there is no live hierarchy to animate in.
        private void SettleImmediately()
        {
            if (frame != null)
            {
                frame.localScale = Vector3.one;
                frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, 0f);
            }

            var characters = _reward?.Characters ?? new List<CharacterReward>();
            for (int i = 0; i < characters.Count && i < rowBarFills.Length; i++)
            {
                SetSpan(rowBarFills[i], characters[i].BarFillBefore01(), characters[i].BarFill01());
                CountGain(i, characters[i].ExpGained);
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
