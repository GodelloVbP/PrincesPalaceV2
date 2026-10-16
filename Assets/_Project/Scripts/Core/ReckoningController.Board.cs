using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // THE ARRIVAL AND THE BOARD (docs/PLAN_REWARD_SCREENS.md C1, C2, C8).
    //
    // The arrival plays inside the wipe whichever phase opens: the crest's
    // medallion, the gold count from CombatReward.GoldBefore, a chip that paid
    // as gold dissolving at the stack, and on the outcome form the medallion's
    // state once the gloom lifts. The board plays when it lands: the rings
    // fill, a level-up closes its ring and rings the bell, the haul drops into
    // its strip.
    //
    // Both are functions of a clock (ApplyArrival, DrawBoard), so settling is
    // drawing the end, silently, and a dropped frame cannot leave a ring
    // half-filled.
    public partial class ReckoningController
    {
        [SerializeField] internal Image crestMedallion;
        [SerializeField] internal Image crestCrack;
        [SerializeField] internal GameObject goldStack;
        [SerializeField] internal Image goldGlyph;
        [SerializeField] internal TMP_Text goldCount;
        [SerializeField] internal Image goldDissolve;

        [SerializeField] internal Button[] portraitButtons;
        [SerializeField] internal Image[] portraitFlares;
        [SerializeField] internal Image[] portraitRingTracks;
        [SerializeField] internal Image[] portraitRingBefores;
        [SerializeField] internal Image[] portraitRingGains;
        [SerializeField] internal Image[] portraitFaces;
        [SerializeField] internal Image[] portraitMarks;
        [SerializeField] internal Image[] portraitPulses;
        [SerializeField] internal Image[] portraitLevelDiscs;
        [SerializeField] internal TMP_Text[] portraitLevels;
        [SerializeField] internal GameObject[] deedRows;
        [SerializeField] internal Image[] deedGlyphs;
        [SerializeField] internal TMP_Text[] deedCounts;

        [SerializeField] internal GameObject haulStrip;
        [SerializeField] internal Button[] haulChips;
        [SerializeField] internal Image[] haulPlinths;
        [SerializeField] internal Image[] haulIcons;

        [SerializeField] internal GameObject boardTip;
        [SerializeField] internal TMP_Text boardTipTitle;
        [SerializeField] internal TMP_Text boardTipBody;

        // The map's medallions and chips (RewardArt.MapTableIds), wired at build.
        [SerializeField] internal IconEntry[] mapArt;

        // C1.3: how long the outcome form holds the gloom low before the
        // medallion shows how the fight ended.
        private const float OutcomeHoldSeconds = 0.4f;

        // An Endured medallion: dimmed, but whole.
        private static readonly Color EnduredTint = new Color(0.5f, 0.5f, 0.55f, 1f);

        // A member who was not fielded.
        private static readonly Color NotFieldedTint = new Color(0.45f, 0.45f, 0.5f, 1f);

        private const float LevelFlareSeconds = 0.25f;
        private const float LevelFlareAlpha = 0.6f;
        private const float HaulStagger = 0.06f;
        private const float HaulDropSeconds = 0.18f;
        private const float HaulDrop = 14f;
        private const string DefaultPlinthHex = "#B8A8D9";

        // ---- the arrival's state ------------------------------------------------------

        private int _goldFrom;
        private int _goldTo;
        private int _goldTicks;
        private int _ticksHeard;
        private int _runGoldAtShow;
        private bool _outcomeShown;
        private float _gloomTarget = -1f;
        private readonly List<PrizeKind> _dissolving = new List<PrizeKind>();

        // The chips the gold stack dissolved this arrival, in pay order: for a
        // test, which cannot see a dissolve that lasts a fraction of the wipe.
        public IReadOnlyList<PrizeKind> DissolvedChips => _dissolving;

        public FightEnding Ending => _request.Ending;

        // ---- the board's state --------------------------------------------------------

        private struct Member
        {
            public string Id;
            public string Name;
            public bool Fielded;
            public bool HasRing;
            public CharacterReward Row;
        }

        private struct HaulEntry
        {
            public Sprite Icon;
            public string PlinthHex;
            public string Title;
            public Sound Land;
        }

        private readonly List<Member> _members = new List<Member>();
        private readonly List<HaulEntry> _haul = new List<HaulEntry>();
        private int[] _closesHeard = new int[0];
        private bool _humHeard;
        private int _haulHeard;
        private float _boardClock;
        private float _boardEnd;
        private Coroutine _boardRoutine;
        private bool _watchingMenus;

        // Whether the board has nothing left to draw: for a test or a capture.
        public bool BoardSettled => _boardRoutine == null && _animation == null;

        private void WireBoard()
        {
            for (int i = 0; portraitButtons != null && i < portraitButtons.Length; i++)
            {
                int index = i;
                if (portraitButtons[i] == null) continue;
                _tooltips.Register(portraitButtons[i], entered => ShowPortraitTip(index, entered));
                WireHover(portraitButtons[i].gameObject, index);
                RadialFill(At(portraitRingBefores, i));
                RadialFill(At(portraitRingGains, i));
            }

            for (int i = 0; haulChips != null && i < haulChips.Length; i++)
            {
                int index = i;
                if (haulChips[i] == null) continue;
                _tooltips.Register(haulChips[i], entered => ShowHaulTip(index, entered));
                WireHover(haulChips[i].gameObject, index);
            }
        }

        // uGUI's Filled / Radial360 is the ring's arc from twelve o'clock, as the
        // talent collar draws it.
        private static void RadialFill(Image image)
        {
            if (image == null) return;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
        }

        private static T At<T>(T[] items, int index) where T : class =>
            items != null && index >= 0 && index < items.Length ? items[index] : null;

        private List<Selectable> ShownPortraits()
        {
            var shown = new List<Selectable>();
            for (int i = 0; portraitButtons != null && i < portraitButtons.Length; i++)
            {
                if (portraitButtons[i] != null && portraitButtons[i].gameObject.activeSelf) shown.Add(portraitButtons[i]);
            }

            return shown;
        }

        private List<Selectable> ShownHaulChips()
        {
            var shown = new List<Selectable>();
            for (int i = 0; haulChips != null && i < haulChips.Length; i++)
            {
                if (haulChips[i] != null && haulChips[i].gameObject.activeSelf) shown.Add(haulChips[i]);
            }

            return shown;
        }

        // ---- the arrival (C1, C2) ------------------------------------------------------

        // What the wipe will play: the crest's medallion, where the gold count
        // starts and ends, and which chips paid as gold.
        private void PaintArrival()
        {
            bool outcome = _request.IsOutcomeForm;

            if (crestMedallion != null)
            {
                ItemIcons.Apply(crestMedallion, mapArt, RewardArt.MedallionId(_reward?.EncounterClass));
                crestMedallion.color = Color.white;
            }

            if (crestCrack != null) crestCrack.gameObject.SetActive(false);
            _outcomeShown = false;

            if (goldStack != null) goldStack.SetActive(!outcome);
            if (goldGlyph != null) ItemIcons.Apply(goldGlyph, rewardArt, RewardArt.Coins);

            // The fight's own gold is GoldGained; every other gold the
            // settlement paid is a chip's (Shown != None), an overflowed page or
            // an exhausted pool among them.
            _dissolving.Clear();
            int chipGold = 0;
            foreach (var prize in _reward?.Prizes ?? new List<PaidPrize>())
            {
                if (prize.Paid != PrizeKind.Gold || prize.Shown == PrizeKind.None) continue;
                chipGold += prize.Amount;
                if (prize.Shown != PrizeKind.Gold) _dissolving.Add(prize.Shown);
            }

            _goldFrom = _reward?.GoldBefore ?? 0;
            _goldTo = _goldFrom + (_reward?.GoldGained ?? 0) + chipGold;
            _goldTicks = Mathf.Min(RevealSchedule.MaxHaulTicks, Mathf.Max(0, _goldTo - _goldFrom));
            _ticksHeard = 0;
            _runGoldAtShow = RunManager.HasRun ? RunManager.Run.gold : 0;

            if (goldDissolve != null) goldDissolve.gameObject.SetActive(false);
            SetGold(_goldFrom);
        }

        // The arrival at `elapsed` seconds into the wipe.
        private void ApplyArrival(float elapsed, bool audible)
        {
            float wipe = W(WipeSeconds);

            // The count, in at most MaxHaulTicks soft ticks, the last on the
            // wipe's end.
            if (!_request.IsOutcomeForm && _goldTicks > 0)
            {
                int ticks = elapsed >= wipe ? _goldTicks : Mathf.FloorToInt(elapsed / (wipe / _goldTicks));
                ticks = Mathf.Clamp(ticks, 0, _goldTicks);
                while (_ticksHeard < ticks)
                {
                    _ticksHeard++;
                    if (audible) SoundController.Play(Sound.CoinTick);
                }

                SetGold(_goldFrom + Mathf.RoundToInt((_goldTo - _goldFrom) * (ticks / (float)_goldTicks)));
            }

            // Each chip that paid as gold shows as the map drew it, then
            // dissolves into the stack, one after another across the wipe.
            if (goldDissolve != null)
            {
                int n = _dissolving.Count;
                float slot = n == 0 ? 0f : wipe / n;
                int current = n == 0 || elapsed >= wipe ? n : Mathf.FloorToInt(elapsed / slot);
                bool up = current < n;
                goldDissolve.gameObject.SetActive(up);
                if (up)
                {
                    float k = Mathf.Clamp01((elapsed - current * slot) / slot);
                    ItemIcons.Apply(goldDissolve, mapArt, RewardArt.ChipId(_dissolving[current]));
                    SetAlpha(goldDissolve, 1f - k);
                    goldDissolve.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.6f, k);
                }
            }

            // The outcome form's medallion, once the stage has shown the ending.
            if (_request.IsOutcomeForm && !_outcomeShown && elapsed >= W(OutcomeHoldSeconds))
            {
                _outcomeShown = true;
                switch (_request.Ending)
                {
                    case FightEnding.Fell:
                        if (crestCrack != null) crestCrack.gameObject.SetActive(true);
                        if (audible) SoundController.Play(Sound.Fell);
                        break;
                    case FightEnding.Endured:
                        if (crestMedallion != null) crestMedallion.color = EnduredTint;
                        if (audible) SoundController.Play(Sound.Endured);
                        break;
                }
            }
        }

        private void SetGold(int value)
        {
            if (goldCount != null) goldCount.Set(UiStrings.ReckoningGoldCount, value);
        }

        // Gold a hone or a leave paid joins the stack once the count is in.
        private void RefreshGold()
        {
            if (_request.IsOutcomeForm || !RunManager.HasRun) return;
            if (_ticksHeard < _goldTicks) return;
            SetGold(_goldTo + RunManager.Run.gold - _runGoldAtShow);
        }

        // ---- the board (C8) -----------------------------------------------------------

        private List<Member> MembersOf()
        {
            var members = new List<Member>();
            if (_reward != null)
            {
                foreach (var row in _reward.Characters)
                {
                    members.Add(new Member
                    {
                        Id = row.CharacterId, Name = row.DisplayName, Fielded = !row.IsDowned, HasRing = true, Row = row,
                    });
                }

                return members;
            }

            foreach (var member in _request.Tally)
            {
                members.Add(new Member { Id = member.CharacterId, Name = member.DisplayName, Fielded = true });
            }

            return members;
        }

        // Who is on the board and what they did. The rings rest where they
        // stood before the fight until StartBoard plays them.
        private void PaintBoard()
        {
            _members.Clear();
            _members.AddRange(MembersOf());
            int count = Mathf.Min(_members.Count, portraitButtons?.Length ?? 0);
            _closesHeard = new int[count];

            for (int i = 0; portraitButtons != null && i < portraitButtons.Length; i++)
            {
                bool shown = i < count;
                if (portraitButtons[i] != null)
                {
                    portraitButtons[i].gameObject.SetActive(shown);
                    SetX(portraitButtons[i].transform as RectTransform, ReckoningScreen.PortraitX(i, count));
                }

                var deeds = At(deedRows, i);
                if (deeds != null)
                {
                    deeds.SetActive(shown);
                    SetX(deeds.transform as RectTransform, ReckoningScreen.PortraitX(i, count));
                }

                if (!shown) continue;
                PaintPortrait(i, _members[i]);
                PaintDeeds(i, _members[i]);
                DrawRing(i, float.NegativeInfinity, audible: false);
            }

            if (haulStrip != null) haulStrip.SetActive(!_request.IsOutcomeForm);
            for (int i = 0; haulChips != null && i < haulChips.Length; i++)
            {
                if (haulChips[i] != null) haulChips[i].gameObject.SetActive(false);
            }

            HideBoardTip();
        }

        private void PaintPortrait(int i, Member member)
        {
            var face = At(portraitFaces, i);
            if (face != null)
            {
                var sprite = CharacterPortraits.For(member.Id);
                face.sprite = sprite;
                face.preserveAspect = true;
                face.enabled = sprite != null;
                face.color = member.Fielded ? Color.white : NotFieldedTint;
            }

            // The cracked heart: fielded, and fell at least once.
            var line = _request.Ledger?.For(member.Id);
            var mark = At(portraitMarks, i);
            if (mark != null)
            {
                bool fell = member.Fielded && line != null && line.TimesDowned > 0;
                mark.gameObject.SetActive(fell);
                if (fell) ItemIcons.Apply(mark, rewardArt, RewardArt.HeartCracked);
            }

            // The outcome form paid nothing: no ring, no level.
            bool ring = member.HasRing;
            foreach (var part in new[] { At(portraitRingTracks, i), At(portraitRingBefores, i), At(portraitRingGains, i),
                         At(portraitLevelDiscs, i) })
            {
                if (part != null) part.gameObject.SetActive(ring);
            }

            var level = At(portraitLevels, i);
            if (level != null) level.gameObject.SetActive(ring);
            var pulse = At(portraitPulses, i);
            if (pulse != null) pulse.gameObject.SetActive(false);
            var flare = At(portraitFlares, i);
            if (flare != null) SetAlpha(flare, 0f);
        }

        // Sword = dealt, cracked shield = taken, leaf = healed (when any),
        // skull = felled. The split and shielded are the portrait's detail.
        private void PaintDeeds(int i, Member member)
        {
            var line = _request.Ledger?.For(member.Id);
            int dealt = line?.TotalDealt ?? 0;
            int taken = line?.DamageTaken ?? 0;
            int healed = line?.Healed ?? 0;
            int felled = line?.Kills ?? 0;

            var groups = new List<GlyphGroup>();
            var ids = new[] { RewardArt.Sword, RewardArt.ShieldCracked, healed > 0 ? RewardArt.Leaf : null, RewardArt.Skull };
            var values = new[] { dealt, taken, healed, felled };
            for (int k = 0; k < ReckoningScreen.DeedsPerRow; k++)
            {
                int at = i * ReckoningScreen.DeedsPerRow + k;
                var glyph = At(deedGlyphs, at);
                if (glyph == null) continue;
                groups.Add(new GlyphGroup(glyph, At(deedCounts, at), ids[k], UiStrings.ReckoningCount.Format(values[k])));
            }

            PaintGroups(groups);
        }

        // The haul strip's contents, in pay order: the potions and the book
        // the chips drew, then what each page left the party holding -- a
        // taken card, a displaced copy, a hone.
        private void CollectHaul()
        {
            _haul.Clear();
            if (_request.IsOutcomeForm) return;

            foreach (var prize in _reward?.Prizes ?? new List<PaidPrize>())
            {
                if (prize.Paid == PrizeKind.Consumable)
                {
                    foreach (var id in prize.Ids)
                    {
                        _haul.Add(new HaulEntry
                        {
                            Icon = ItemIcons.Find(rewardArt, RewardArt.Potion),
                            PlinthHex = DefaultPlinthHex,
                            Title = ContentDatabase.GetItem(id)?.displayName ?? id,
                            Land = Sound.PotionLand,
                        });
                    }
                }
                else if (prize.Paid == PrizeKind.Book)
                {
                    foreach (var id in prize.Ids)
                    {
                        _haul.Add(new HaulEntry
                        {
                            Icon = ItemIcons.Find(mapArt, RewardArt.ChipBook),
                            PlinthHex = DefaultPlinthHex,
                            Title = ContentDatabase.GetSkill(id)?.Data.DisplayName ?? id,
                            Land = Sound.BookLand,
                        });
                    }
                }
            }

            var save = SaveSlotManager.CurrentSave;
            foreach (var page in _pages)
            {
                if (page == null) continue;
                foreach (var card in page.Cards)
                {
                    if (card.Outcome == CardOutcome.Taken)
                    {
                        var face = card.IsGear ? RewardCardFace.OfGear(save, card) : RewardCardFace.OfRelic(card);
                        _haul.Add(new HaulEntry
                        {
                            Icon = card.IsGear
                                ? ItemIcons.Find(icons, ItemIcons.KeyOf(card.Copy))
                                : ItemIcons.Find(relicArt, card.RelicId),
                            PlinthHex = face?.Hex ?? DefaultPlinthHex,
                            Title = face?.Name ?? "",
                            Land = Sound.None,
                        });
                    }

                    if (card.Displaced != null)
                    {
                        _haul.Add(new HaulEntry
                        {
                            Icon = ItemIcons.Find(icons, ItemIcons.KeyOf(card.Displaced)),
                            PlinthHex = DefaultPlinthHex,
                            Title = ContentDatabase.NameOf(card.Displaced),
                            Land = Sound.None,
                        });
                    }

                    if (card.Outcome == CardOutcome.Honed)
                    {
                        _haul.Add(new HaulEntry
                        {
                            Icon = ItemIcons.Find(rewardArt, RewardArt.Whetstone),
                            PlinthHex = DefaultPlinthHex,
                            Title = UiStrings.ReckoningForSlot.Format(RewardApplier.DisplayNameFor(card.RecipientId),
                                card.Slot),
                            Land = Sound.None,
                        });
                    }
                }
            }
        }

        private void PaintHaul()
        {
            CollectHaul();
            int count = Mathf.Min(_haul.Count, haulChips?.Length ?? 0);
            for (int j = 0; haulChips != null && j < haulChips.Length; j++)
            {
                var chip = haulChips[j];
                if (chip == null) continue;
                bool shown = j < count;
                chip.gameObject.SetActive(shown);
                if (!shown) continue;

                SetX(chip.transform as RectTransform, ReckoningScreen.HaulChipX(j, count));
                var icon = At(haulIcons, j);
                if (icon != null)
                {
                    icon.sprite = _haul[j].Icon;
                    icon.preserveAspect = true;
                    icon.enabled = _haul[j].Icon != null;
                }

                var plinth = At(haulPlinths, j);
                if (plinth != null)
                {
                    var tint = HexColour(_haul[j].PlinthHex);
                    tint.a = 0.4f;
                    plinth.color = tint;
                }
            }
        }

        // The board lands: the rings fill, the haul drops in.
        private void StartBoard()
        {
            StopBoard();
            PaintBoard();
            PaintHaul();
            _humHeard = false;
            _haulHeard = 0;
            _boardClock = 0f;
            _boardEnd = BoardEnd();

            if (_navContext != null) RefreshNavigation();

            if (isActiveAndEnabled)
            {
                DrawBoard(0f, audible: true);
                _boardRoutine = StartCoroutine(RunBoard());
            }
            else
            {
                SettleBoard();
            }
        }

        private IEnumerator RunBoard()
        {
            while (_boardClock < _boardEnd)
            {
                yield return null;
                _boardClock += Tick;
                DrawBoard(_boardClock, audible: true);
            }

            _boardRoutine = null;
        }

        private void StopBoard()
        {
            if (_boardRoutine != null) StopCoroutine(_boardRoutine);
            _boardRoutine = null;
        }

        // The board's end state, silently.
        private void SettleBoard()
        {
            StopBoard();
            DrawBoard(float.MaxValue, audible: false);
        }

        private float BoardEnd()
        {
            float end = 0f;
            for (int i = 0; i < _closesHeard.Length; i++) end = Mathf.Max(end, MemberStart(i) + MemberLength(i));
            int chips = Mathf.Min(_haul.Count, haulChips?.Length ?? 0);
            if (chips > 0) end = Mathf.Max(end, W(HaulStagger) * (chips - 1) + W(HaulDropSeconds));
            return end;
        }

        private void DrawBoard(float t, bool audible)
        {
            if (!_humHeard && _members.Any(m => m.HasRing))
            {
                _humHeard = true;
                if (audible) SoundController.Play(Sound.XpHum);
            }

            for (int i = 0; i < _closesHeard.Length; i++) DrawRing(i, t, audible);
            DrawHaul(t, audible);
        }

        // ---- one ring -----------------------------------------------------------------

        private float MemberStart(int i) => i * W(BarStagger);

        private int Closes(int i) =>
            i < _members.Count && _members[i].HasRing ? _members[i].Row.LevelsGained : 0;

        // One pass per level crossed plus the remainder, sharing BarSeconds, with
        // a flare after each close.
        private float SweepLength(int i) => W(BarSeconds) / (Closes(i) + 1);

        private float MemberLength(int i) =>
            i < _members.Count && _members[i].HasRing
                ? W(BarSeconds) + Closes(i) * W(LevelFlareSeconds)
                : 0f;

        private void DrawRing(int i, float t, bool audible)
        {
            if (i >= _members.Count || !_members[i].HasRing) return;

            var row = _members[i].Row;
            int closes = Closes(i);
            float local = t - MemberStart(i);
            float sweep = SweepLength(i);
            float flareFor = W(LevelFlareSeconds);

            float before, gain, flare = 0f;
            int levelsIn;
            bool done = false;

            if (local <= 0f)
            {
                before = closes > 0 ? row.SweepStart01() : row.BarFillBefore01();
                gain = before;
                levelsIn = 0;
            }
            else
            {
                // Which pass `local` falls in: pass k, then its flare if k closed.
                float cursor = 0f;
                int k = 0;
                before = gain = 0f;
                levelsIn = 0;
                while (true)
                {
                    bool last = k == closes;
                    float from = k == 0 ? (closes > 0 ? row.SweepStart01() : row.BarFillBefore01()) : 0f;
                    float to = last ? row.BarFill01() : 1f;
                    float arc = k == 0 ? from : 0f;

                    if (local < cursor + sweep)
                    {
                        float f = Easing.SmoothStep(Mathf.Clamp01((local - cursor) / sweep));
                        before = arc;
                        gain = Mathf.Lerp(from, to, f);
                        levelsIn = k;
                        break;
                    }

                    cursor += sweep;
                    if (last)
                    {
                        before = closes > 0 ? 0f : row.BarFillBefore01();
                        gain = row.BarFill01();
                        levelsIn = closes;
                        done = true;
                        break;
                    }

                    if (local < cursor + flareFor)
                    {
                        // The ring is closed and the portrait flares.
                        before = 0f;
                        gain = 1f;
                        levelsIn = k + 1;
                        flare = LevelFlareAlpha * (1f - (local - cursor) / flareFor);
                        break;
                    }

                    cursor += flareFor;
                    k++;
                }
            }

            while (_closesHeard[i] < levelsIn)
            {
                _closesHeard[i]++;
                if (audible) SoundController.Play(Sound.LevelBell);
            }

            var beforeImage = At(portraitRingBefores, i);
            var gainImage = At(portraitRingGains, i);
            if (beforeImage != null) beforeImage.fillAmount = Mathf.Clamp01(before);
            if (gainImage != null) gainImage.fillAmount = Mathf.Clamp01(gain);

            var flareImage = At(portraitFlares, i);
            if (flareImage != null) SetAlpha(flareImage, Mathf.Clamp01(flare));

            var level = At(portraitLevels, i);
            if (level != null) level.Set(UiStrings.ReckoningLevelNumeral, row.LevelBefore + levelsIn);

            // A level-up leaves the badge waiting, as the reward track's node
            // waits: it shows something is owed and claims nothing.
            var pulse = At(portraitPulses, i);
            if (pulse != null) pulse.gameObject.SetActive(done && row.LevelledUp);
        }

        // ---- the haul strip -----------------------------------------------------------

        private void DrawHaul(float t, bool audible)
        {
            int count = Mathf.Min(_haul.Count, haulChips?.Length ?? 0);
            float stagger = W(HaulStagger);
            float drop = W(HaulDropSeconds);
            for (int j = 0; j < count; j++)
            {
                var chip = haulChips[j];
                if (chip == null) continue;

                float k = Mathf.Clamp01((t - j * stagger) / drop);
                var rect = (RectTransform)chip.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, HaulDrop * (1f - Easing.OutCubic(k)));
                var group = chip.GetComponent<CanvasGroup>();
                if (group == null) group = chip.gameObject.AddComponent<CanvasGroup>();
                group.alpha = k;

                if (k >= 1f && _haulHeard == j)
                {
                    _haulHeard++;
                    if (audible && _haul[j].Land != Sound.None) SoundController.Play(_haul[j].Land);
                }
            }
        }

        // ---- the detail ---------------------------------------------------------------

        private void ShowPortraitTip(int index, bool entered)
        {
            if (!entered || index >= _members.Count)
            {
                HideBoardTip();
                return;
            }

            var member = _members[index];
            var line = _request.Ledger?.For(member.Id);
            var lines = new List<string>();
            if (!member.Fielded) lines.Add(UiStrings.ReckoningDowned.Format());
            if (member.HasRing) lines.Add(UiStrings.ReckoningExpGain.Format(member.Row.ExpGained));
            lines.Add(UiStrings.ReckoningTipDealt.Format(line?.TotalDealt ?? 0, line?.PhysicalDealt ?? 0,
                line?.OtherDealt ?? 0));
            lines.Add(UiStrings.ReckoningTipTaken.Format(line?.DamageTaken ?? 0, line?.Shielded ?? 0));
            if ((line?.Healed ?? 0) > 0) lines.Add(UiStrings.ReckoningTipHealed.Format(line.Healed));
            lines.Add(UiStrings.ReckoningKills.Format(line?.Kills ?? 0));

            ShowBoardTip(portraitButtons[index].transform as RectTransform, member.Name, string.Join("\n", lines));
        }

        private void ShowHaulTip(int index, bool entered)
        {
            if (!entered || index >= _haul.Count)
            {
                HideBoardTip();
                return;
            }

            ShowBoardTip(haulChips[index].transform as RectTransform, _haul[index].Title, "");
        }

        private void ShowBoardTip(RectTransform over, string title, string body)
        {
            if (boardTip == null) return;
            boardTip.SetActive(true);
            if (boardTipTitle != null) boardTipTitle.SetContent(title);
            if (boardTipBody != null) boardTipBody.SetContent(body);

            var box = boardTip.transform as RectTransform;
            if (box == null || over == null) return;

            float limit = ReckoningScreen.ContentHalfWidth - ReckoningScreen.BoardTipWidth * 0.5f;
            float x = over.parent == box.parent ? over.anchoredPosition.x
                : over.anchoredPosition.x + ((RectTransform)over.parent).anchoredPosition.x;
            box.anchoredPosition = new Vector2(Mathf.Clamp(x, -limit, limit), box.anchoredPosition.y);
        }

        private void HideBoardTip()
        {
            if (boardTip != null) boardTip.SetActive(false);
        }

        // ---- a menu over a running beat (section 4.13) ----------------------------------

        private void WatchMenus(bool on)
        {
            var stack = NavigationInputModule.Contexts;
            if (stack == null || on == _watchingMenus) return;
            _watchingMenus = on;
            if (on) stack.Changed += OnContextsChanged;
            else stack.Changed -= OnContextsChanged;
        }

        // Something opened over the Reckoning: whatever was running lands now,
        // so nothing ticks underneath the menu.
        private void OnContextsChanged()
        {
            var stack = NavigationInputModule.Contexts;
            if (stack == null || _navContext == null) return;
            if (!stack.Contains(_navContext) || stack.IsTop(_navContext)) return;
            SettleBeat();
        }

        public void SettleBeat()
        {
            if (RevealRunning) SkipReveal();

            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;

                // A sweep cut short lands the board where it would have arrived.
                if (!_choosing && offerPhase != null && offerPhase.gameObject.activeSelf)
                {
                    offerPhase.gameObject.SetActive(false);
                    summaryPhase.gameObject.SetActive(true);
                    summaryPhase.anchoredPosition = Vector2.zero;
                    Smear(summaryPhase, BlurGroup(summaryPhase), 0f);
                    StartBoard();
                }
            }

            SettleImmediately();
        }

        private static void SetX(RectTransform rect, float x)
        {
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }
    }
}
