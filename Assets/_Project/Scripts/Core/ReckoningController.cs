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
        // The MASK, not the frame. Opening this uncovers a painted panel that
        // never changes size; the frame itself is deliberately not held here,
        // because anything with a reference to it is one edit away from
        // scaling it again.
        [SerializeField] internal RectTransform frameWipe;
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

        [SerializeField] internal GameObject offerTooltip;
        [SerializeField] internal TMP_Text offerTooltipText;
        [SerializeField] internal Image[] offerHalos;
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
        [SerializeField] internal Image frameGlow;

        [SerializeField] internal RectTransform offerPhase;
        [SerializeField] internal RectTransform summaryPhase;

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

        // Long enough to register as a beat, short enough not to stall the
        // sequence behind it.
        private const float FlashSeconds = 0.09f;

        // A HORIZONTAL WIPE, not a scale-pop. Full height from the first
        // frame, zero width, opening outward -- so it reads as the panel being
        // drawn across the fight rather than as a dialog appearing on top of
        // it. The scale-from-0.82 version was a popup and looked like one.
        //
        // And a MASK, not a scale, which is a second and separate correction.
        // Driving frame.localScale.x got the timing right and the substance
        // wrong: localScale scales CHILDREN, so the border's corner ornaments,
        // the three item cards and every label compressed to nothing and sprang
        // back out. That is a squash, and it is what made the screen read
        // cheap. Opening a RectMask2D's WIDTH reveals fixed-size content
        // instead of deforming it.
        private const float WipeSeconds = 0.34f;

        // The phase change sweeps RIGHTWARD: the choice leaves to the right
        // and the summary follows it in from the left, both travelling the
        // same way, so it reads as moving forward rather than as two
        // unrelated slides.
        private const float SweepSeconds = 0.3f;

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
        private bool _choosing;

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

                // The comparison hover. Attached here rather than baked,
                // because HoverIndex carries a delegate and an index and a
                // scene serialises neither usefully -- same reason the fight
                // attaches it to its intent badges.
                var hover = offerButtons[i].gameObject.AddComponent<HoverIndex>();
                hover.Index = index;
                hover.Changed = OnOfferHover;
            }
        }

        // Opened with what the fight paid and what it offers.
        //
        // Both are handed in rather than fetched. The controller reading
        // FightBootstrap.LastReward itself would tie this screen to the one
        // bootstrap that happens to own it today, and the Reckoning is the sort
        // of thing a test wants to drive with a fixture.
        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        // What this offer would do to every member of the squad.
        //
        // Rebuilt on each hover rather than cached: it runs the real
        // clone-and-resolve Compare, and the squad's gear can change between
        // one Reckoning and the next.
        private void OnOfferHover(int index, bool entered)
        {
            if (offerTooltip == null) return;

            if (!entered || _taken || index < 0 || index >= _offers.Count)
            {
                SetActive(offerTooltip, false);
                return;
            }

            var item = ContentDatabase.GetItem(_offers[index].ItemId);
            var save = SaveSlotManager.CurrentSave;
            if (item == null || save == null)
            {
                SetActive(offerTooltip, false);
                return;
            }

            string body = ItemDescription.SquadComparisonBody(save.ActiveSquad(), item, _offers[index].Plus);
            if (string.IsNullOrEmpty(body))
            {
                SetActive(offerTooltip, false);
                return;
            }

            if (offerTooltipText != null) offerTooltipText.SetContent(body);
            SetActive(offerTooltip, true);
        }

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
            // OPENS ON THE CHOICE. You always take something, so the item
            // phase is where the screen begins and the only way past it is to
            // pick -- there is no skip and no Continue on it.
            //
            // A fight that offers nothing goes straight to the summary rather
            // than showing an empty choice with no way out.
            bool choosing = _offers.Count > 0;

            offerPhase.gameObject.SetActive(choosing);
            offerPhase.anchoredPosition = Vector2.zero;
            summaryPhase.gameObject.SetActive(!choosing);
            summaryPhase.anchoredPosition = Vector2.zero;
            _choosing = choosing;

            _tab = 0;
            PaintTabs();
            PaintRelics();
            PaintTally();

            if (isActiveAndEnabled)
            {
                _animation = StartCoroutine(PlayIn());

                // Straight to the summary means the bars start with it; the
                // choosing path starts them when the sweep lands.
                if (!choosing) StartBars();
            }
            else
            {
                SettleImmediately();
            }
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

                // The halo carries the SAME colour at lower alpha. It is the
                // part that survives being stood on: the burst's rays clear the
                // icon, but its core is hidden behind the item, so on its own
                // the rarity read as a few spikes rather than as a colour.
                if (offerHalos != null && i < offerHalos.Length && offerHalos[i] != null)
                {
                    var soft = glow;
                    soft.a = _taken ? 0.12f : 0.30f;
                    offerHalos[i].color = soft;
                }

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

            // The choice IS the transition. Nothing else on this phase leads
            // anywhere, which is what "you always take something" means.
            if (_choosing)
            {
                _choosing = false;

                if (isActiveAndEnabled)
                {
                    if (_animation != null) StopCoroutine(_animation);
                    _animation = StartCoroutine(SweepToSummary());
                }
                else
                {
                    offerPhase.gameObject.SetActive(false);
                    summaryPhase.gameObject.SetActive(true);
                }
            }
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
            // Full height, no width. The lift and the glow ride the same
            // clock: the panel rises the last few pixels as it finishes
            // opening, and the drop behind it fades up with the gloom.
            //
            // The LIFT moves the mask rather than the frame inside it, so the
            // two travel together -- lifting the frame alone would slide it out
            // from under its own clip and crop the bottom 46px of the border.
            SetWipeWidth(0f);
            frameWipe.anchoredPosition = new Vector2(frameWipe.anchoredPosition.x, LiftFrom);

            float gloomTarget = dimmer != null ? dimmer.color.a : 0f;
            float glowTarget = frameGlow != null ? GlowAlpha : 0f;
            if (dimmer != null) SetAlpha(dimmer, 0f);
            if (frameGlow != null) SetAlpha(frameGlow, 0f);

            float elapsed = 0f;
            float longest = Mathf.Max(WipeSeconds, GloomSeconds);

            while (elapsed < longest)
            {
                elapsed += Time.unscaledDeltaTime;

                float k = Smooth(Mathf.Clamp01(elapsed / WipeSeconds));
                SetWipeWidth(ReckoningScreen.PanelWidth * k);
                frameWipe.anchoredPosition =
                    new Vector2(frameWipe.anchoredPosition.x, Mathf.Lerp(LiftFrom, 0f, k));

                float g = Smooth(Mathf.Clamp01(elapsed / GloomSeconds));
                if (dimmer != null) SetAlpha(dimmer, gloomTarget * g);
                if (frameGlow != null) SetAlpha(frameGlow, glowTarget * g);

                yield return null;
            }

            SetWipeWidth(ReckoningScreen.PanelWidth);
            frameWipe.anchoredPosition = new Vector2(frameWipe.anchoredPosition.x, 0f);
            if (dimmer != null) SetAlpha(dimmer, gloomTarget);
            if (frameGlow != null) SetAlpha(frameGlow, glowTarget);

            _animation = null;
        }

        // How much of the panel is uncovered.
        //
        // sizeDelta, and the mask's pivot is centred, so the width opens
        // symmetrically about a point that does not move -- the frame inside
        // is anchored to that same centre and therefore sits still while it is
        // revealed. Height is restated rather than left alone because a
        // Vector2 assignment is the only way to set one axis, and reading the
        // live value back would let a stray edit anywhere else persist.
        private void SetWipeWidth(float width)
        {
            if (frameWipe == null) return;
            frameWipe.sizeDelta = new Vector2(width, ReckoningScreen.PanelHeight);
        }

        // The choice leaves, the summary arrives, both moving right.
        private IEnumerator SweepToSummary()
        {
            // A phase travels exactly its OWN width, which is the clip's, not
            // the panel's. Both phases now live inside a RectMask2D inset to
            // the painted border, so a card is gone the moment it has moved one
            // clip-width -- and it disappears BEHIND the border rather than
            // sailing on over the battlefield, which is what it used to do with
            // nothing bounding it at all.
            float width = ReckoningScreen.ContentHalfWidth * 2f;

            summaryPhase.gameObject.SetActive(true);
            summaryPhase.anchoredPosition = new Vector2(-width, 0f);

            for (float t = 0f; t < SweepSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(Mathf.Clamp01(t / SweepSeconds));
                offerPhase.anchoredPosition = new Vector2(Mathf.Lerp(0f, width, k), 0f);
                summaryPhase.anchoredPosition = new Vector2(Mathf.Lerp(-width, 0f, k), 0f);
                yield return null;
            }

            offerPhase.anchoredPosition = new Vector2(width, 0f);
            summaryPhase.anchoredPosition = Vector2.zero;
            offerPhase.gameObject.SetActive(false);

            // The bars only start once the summary has arrived. Filling them
            // mid-sweep means watching a number climb on something still
            // sliding, and neither gets read.
            StartBars();
            _animation = null;
        }

        private void StartBars()
        {
            var characters = _reward?.Characters ?? new List<CharacterReward>();

            for (int i = 0; i < characters.Count && i < rowBarFills.Length; i++)
            {
                if (characters[i].IsDowned) continue;
                StartCoroutine(FillBar(i, characters[i], i * BarStagger));
            }
        }

        private IEnumerator FillBar(int index, CharacterReward row, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            // NO LEVEL-UP: one fill, start to finish.
            if (!row.LevelledUp)
            {
                yield return Sweep(index, row, row.BarFillBefore01(), row.BarFill01(), 0f, 1f);
                CountGain(index, row.ExpGained);
                yield break;
            }

            // A LEVEL-UP SWEEPS THE BAR TO FULL, flashes, and starts again.
            //
            // Without this the biggest experience event the game has produced
            // the emptiest bar it can draw: the level reset the track, so what
            // remains is a handful of points measured against the NEW level's
            // larger requirement, and 75 experience and a level rendered as a
            // sliver. The arithmetic was right and the reading was backwards.
            int sweeps = row.LevelsGained;

            // The counter runs across the WHOLE sequence rather than resetting
            // per level -- it is reporting one number, however many times the
            // bar happens to refill behind it.
            float progressPerSweep = 1f / (sweeps + 1);

            // First: from wherever they actually were, up to full.
            yield return Sweep(index, row, row.SweepStart01(), 1f, 0f, progressPerSweep);
            yield return Flash(index);

            // Any further levels crossed in one fight: empty to full each.
            for (int i = 1; i < sweeps; i++)
            {
                float from = i * progressPerSweep;
                yield return Sweep(index, row, 0f, 1f, from, from + progressPerSweep);
                yield return Flash(index);
            }

            // Then the remainder on the new level's scale, which is where the
            // bar actually stands now.
            yield return Sweep(index, row, 0f, row.BarFill01(), sweeps * progressPerSweep, 1f);
            CountGain(index, row.ExpGained);
        }

        // One pass of the bar, carrying its share of the counter with it.
        private IEnumerator Sweep(int index, CharacterReward row, float from, float to,
                                  float countFrom, float countTo)
        {
            // Each pass gets an equal share of the budget, so a two-level fight
            // is not three times as long to watch as a one-level fight.
            float seconds = BarSeconds * (countTo - countFrom);

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(Mathf.Clamp01(t / seconds));
                SetSpan(rowBarFills[index], from, Mathf.Lerp(from, to, k));
                CountGain(index, Mathf.RoundToInt(row.ExpGained * Mathf.Lerp(countFrom, countTo, k)));
                yield return null;
            }

            SetSpan(rowBarFills[index], from, to);
        }

        // The moment the level lands. A brief white-out of the fill, because
        // the bar going from full to empty with no punctuation reads as the
        // animation glitching rather than as something being earned.
        private IEnumerator Flash(int index)
        {
            var fill = rowBarFills[index];
            var resting = fill.color;

            fill.color = Color.white;
            yield return new WaitForSecondsRealtime(FlashSeconds);
            fill.color = resting;
        }

        // Smoothstep. One definition, because three animations easing
        // differently by accident is the sort of thing nobody can name but
        // everybody feels.
        private static float Smooth(float k) => k * k * (3f - 2f * k);

        // Held well under half: it is a drop behind a panel, and a glow you
        // can point at has stopped being one.
        private const float GlowAlpha = 0.38f;

        private static void SetAlpha(Image image, float alpha)
        {
            var colour = image.color;
            colour.a = alpha;
            image.color = colour;
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
            if (frameWipe != null)
            {
                SetWipeWidth(ReckoningScreen.PanelWidth);
                frameWipe.anchoredPosition = new Vector2(frameWipe.anchoredPosition.x, 0f);
            }

            if (dimmer != null) SetAlpha(dimmer, dimmer.color.a);
            if (frameGlow != null) SetAlpha(frameGlow, GlowAlpha);

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
