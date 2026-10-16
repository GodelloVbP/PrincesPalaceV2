using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // THE REVEAL, played (docs/PLAN_REWARD_SCREENS.md C2-C4, C10, C11). The
    // timing is Domain's RevealSchedule; this file only draws the schedule at
    // the reveal's clock and fires each beat's sound once. Nothing here
    // derives a time.
    //
    // Every visual is a function of the clock (ApplyReveal), so a skip is one
    // jump of the schedule (SkippedAt) and one redraw, and a frame dropped or
    // doubled cannot leave a card half-dealt.
    public partial class ReckoningController
    {
        [SerializeField] internal Button catcher;
        [SerializeField] internal Image chipGear;
        [SerializeField] internal Image chipRelic;
        [SerializeField] internal Button cache;
        [SerializeField] internal Image cacheSeam;
        [SerializeField] internal Image[] offerBacks;
        [SerializeField] internal Image[] embers;

        private const float ChipRise = 140f;
        private const float EmberLit = 0.85f;
        private const float EmberSpent = 0.15f;
        private const float LingerHaloAlpha = 0.6f;
        private const float FlareGlowAlpha = 0.75f;

        private RevealSchedule _reveal;
        private float _revealClock;
        private Coroutine _revealRoutine;
        private int _fired;
        private bool _choosableFired;
        private bool _lingerEnded;
        private float _flareFrom = -1f;
        private float _flareFor;
        private Color _flareColour;
        private Color _glowRest;

        // Row position -> page index for the page being revealed, each card's
        // resting x, and the rows whose landing was a top-tier burst.
        private int[] _revealRows = new int[0];
        private float[] _homeX = new float[0];
        private readonly List<int> _burstRows = new List<int>();

        // Pages already revealed this session open face-up, with no sound
        // (C4 precedence 3): the pages live in memory only, so identity is
        // enough.
        private readonly HashSet<PendingPage> _revealed = new HashSet<PendingPage>();

        // What SelectEntry last put the focus on: the cache takes over only a
        // focus the player has not moved.
        private GameObject _entrySelected;

        // A detail asked for during a beat, shown when the row lands.
        private int _wantedTip = -1;
        private bool _wantedHone;
        private bool _cacheUp;

        // Whether the choice controls act now: false during a beat and the
        // guard after it. For a test or a capture that must wait for the row.
        public bool AcceptsChoices => !RevealBlocks();

        // Whether the reveal has nothing left to draw: no beat, guard, linger
        // or flare. For a film that stops on the settled row.
        public bool RevealSettled => _revealRoutine == null;

        private bool RevealBlocks() =>
            _reveal != null && (_reveal.IsRunningAt(_revealClock) || _reveal.GuardsAt(_revealClock));

        private bool RevealRunning => _reveal != null && _reveal.IsRunningAt(_revealClock);

        // ---- start and stop -------------------------------------------------------------

        private void BeginReveal()
        {
            StopReveal();
            if (_page == null || _page.Cards.Count == 0 || _revealed.Contains(_page)) return;
            _revealed.Add(_page);

            int n = Mathf.Min(_page.Cards.Count, offerButtons.Length);
            var save = SaveSlotManager.CurrentSave;
            var cards = new List<RevealCard>(n);
            _revealRows = new int[n];
            _homeX = new float[offerButtons.Length];
            for (int r = 0; r < n; r++)
            {
                int i = PageAt(r);
                _revealRows[r] = i;
                var card = _page.Cards[i];
                var face = i < _faces.Length ? _faces[i] : null;
                int tier = face?.PlinthStep ?? 1;
                bool live = LiveUpgrade.Of(save, card);
                cards.Add(card.IsGear
                    ? new RevealCard(tier, live, LiveUpgrade.WornTier(save, card), false,
                        face != null && face.HasDelta && face.DeltaSign > 0)
                    : RevealCard.Relic(tier, live));
            }

            for (int i = 0; i < offerButtons.Length; i++)
            {
                var rect = RectAt(offerRects, i);
                _homeX[i] = rect == null ? 0f : rect.anchoredPosition.x;
            }

            _reveal = RevealSchedule.For(cards, _page.Choices, _preset, _page.SourceClass);
            PaintCache();
            _revealClock = 0f;
            _fired = 0;
            _choosableFired = false;
            _lingerEnded = false;
            _flareFrom = -1f;
            _burstRows.Clear();
            _glowRest = frameGlow != null ? frameGlow.color : Color.clear;
            _glowRest.a = GlowAlpha;
            _wantedTip = -1;
            HideCardTip();

            if (catcher != null) catcher.gameObject.SetActive(true);
            ApplyReveal();
            _revealRoutine = StartCoroutine(RunReveal());
        }

        // Everything at rest, whatever the reveal was doing: the cards home and
        // face-up, the chip and cache gone, the embers painted from the page.
        private void StopReveal()
        {
            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = null;

            bool was = _reveal != null;
            for (int i = 0; i < offerButtons.Length; i++) RestCard(i, was);
            _reveal = null;
            _cacheUp = false;
            if (catcher != null) catcher.gameObject.SetActive(false);
            if (chipGear != null) chipGear.gameObject.SetActive(false);
            if (chipRelic != null) chipRelic.gameObject.SetActive(false);
            if (cache != null) cache.gameObject.SetActive(false);
            if (was && frameGlow != null) frameGlow.color = _glowRest;
            PaintEmbers();
        }

        private IEnumerator RunReveal()
        {
            bool settled = false;
            while (true)
            {
                FireDue();

                // Once the row has landed and been drawn there, only the linger
                // and the flare move: the cards are left to the nudge and the
                // choices.
                if (!settled)
                {
                    ApplyReveal();
                    settled = _choosableFired;
                }
                else
                {
                    DrawLinger(_revealClock);
                    DrawFlare();
                }

                if (_revealClock >= RevealEnd()) break;

                yield return null;
                _revealClock += Tick;
            }

            _revealRoutine = null;
            if (!_lingerEnded) EndLinger();
            _reveal = null;
            PaintEmbers();
        }

        // The clock runs past the row's landing for the guard, the linger and
        // a flare; none of them blocks input but the guard.
        private float RevealEnd()
        {
            float end = _reveal.GuardUntil;
            if (!_lingerEnded)
            {
                foreach (var beat in _reveal.Beats)
                    if (beat.Kind == RevealBeatKind.Linger) end = Mathf.Max(end, beat.End);
            }

            if (_flareFrom >= 0f) end = Mathf.Max(end, _flareFrom + _flareFor);
            return end;
        }

        // ---- the press -------------------------------------------------------------------

        // Any press during a beat: the haul lands, the cache opens, every card
        // turns face-up at once; a top tier keeps only its flare. Nothing else
        // sounds. The row is choosable from now, behind the guard.
        private void SkipReveal()
        {
            if (!RevealRunning) return;

            _reveal = _reveal.SkippedAt(_revealClock);
            _fired = _reveal.Beats.Count;

            if (_reveal.SkipFlareSeconds > 0f)
            {
                int top = FirstBurstRow();
                if (top >= 0) StartFlare(_revealRows[top], _reveal.SkipFlareSeconds);
            }

            _lingerEnded = true;
            ApplyReveal();
            OnChoosable();
        }

        private int FirstBurstRow()
        {
            foreach (var beat in _reveal.Beats)
                if (beat.Kind == RevealBeatKind.Land && beat.Style == LandStyle.Burst) return beat.Card;
            return -1;
        }

        // A take, a hone, a leave or a Submit ends the linger; moving focus
        // does not (C4.5).
        private void EndLinger()
        {
            if (_lingerEnded) return;
            _lingerEnded = true;
            foreach (int r in _burstRows)
            {
                if (r < _revealRows.Length) RestHalo(_revealRows[r]);
            }
        }

        // ---- beats -----------------------------------------------------------------------

        private void FireDue()
        {
            var beats = _reveal.Beats;
            while (_fired < beats.Count && beats[_fired].Start <= _revealClock)
            {
                Fire(beats[_fired]);
                _fired++;
            }
        }

        private void Fire(RevealBeat beat)
        {
            switch (beat.Kind)
            {
                case RevealBeatKind.HaulTick:
                    SoundController.Play(Sound.CoinTick);
                    break;
                case RevealBeatKind.Seal:
                    SoundController.Play(Sound.CacheOpen);
                    break;
                case RevealBeatKind.Flip:
                    SoundController.Play(Sound.CardFlip, RevealSchedule.LadderPitch(beat.PitchStep));
                    break;
                case RevealBeatKind.Land:
                    Land(beat);
                    break;
                case RevealBeatKind.Ember:
                    SoundController.Play(Sound.EmberLight);
                    break;
                case RevealBeatKind.Chevron:
                    SoundController.Play(Sound.ChevronNote, RevealSchedule.LadderPitch(beat.PitchStep));
                    break;
                case RevealBeatKind.Choosable:
                    OnChoosable();
                    break;
            }
        }

        // A dealt card rustles; a cache card lands by its quality, and a
        // top-tier live upgrade bursts, flares and rings the bell (the boss
        // page's peak under it).
        private void Land(RevealBeat beat)
        {
            if (!_reveal.HasCache) SoundController.Play(Sound.CardDeal);
            if (beat.Style != LandStyle.Burst) return;

            _burstRows.Add(beat.Card);
            SoundController.Play(Sound.LegendaryBell);
            if (_page != null && _page.SourceClass == EncounterClass.Boss) SoundController.Play(Sound.BossPeak);
            StartFlare(_revealRows[beat.Card], RevealSchedule.FlareSeconds * ReckoningTime.Scale(_preset));
        }

        private void StartFlare(int pageIndex, float seconds)
        {
            var face = pageIndex < _faces.Length ? _faces[pageIndex] : null;
            _flareColour = HexColour(face?.Hex ?? "#FFFFFF");
            _flareFrom = _revealClock;
            _flareFor = seconds;
        }

        // The row lands: the catcher goes, the cache's focus hands over to the
        // cards, and a detail the player asked for during the beat appears.
        private void OnChoosable()
        {
            if (_choosableFired) return;
            _choosableFired = true;

            if (catcher != null) catcher.gameObject.SetActive(false);
            SetCacheUp(false);

            if (_wantedTip >= 0 && _tooltips.Shown != null)
            {
                int index = _wantedTip;
                _wantedTip = -1;
                ShowTip(index, true, _wantedHone);
            }
        }

        // ---- drawing ---------------------------------------------------------------------

        // The whole reveal at the clock. A skipped schedule draws its end.
        private void ApplyReveal()
        {
            if (_reveal == null) return;

            float t = _reveal.Skipped ? float.MaxValue : _revealClock;
            RevealBeat? wipe = null, deal = null, wait = null, seal = null, slide = null;
            var flips = new Dictionary<int, RevealBeat>();
            int embersLit = 0;
            foreach (var beat in _reveal.Beats)
            {
                switch (beat.Kind)
                {
                    case RevealBeatKind.Wipe: wipe = beat; break;
                    case RevealBeatKind.Deal: deal = beat; break;
                    case RevealBeatKind.OpenWait: wait = beat; break;
                    case RevealBeatKind.Seal: seal = beat; break;
                    case RevealBeatKind.Slide: slide = beat; break;
                    case RevealBeatKind.Flip: flips[beat.Card] = beat; break;
                    case RevealBeatKind.Ember: if (beat.Start <= t) embersLit++; break;
                }
            }

            float wipeEnd = wipe?.End ?? 0f;

            // The map's chip flies down to the row's centre during the wipe,
            // then becomes the cache, or the dealt cards.
            var chip = _page != null && _page.Kind == RewardPageKind.Relic ? chipRelic : chipGear;
            var other = chip == chipRelic ? chipGear : chipRelic;
            if (other != null) other.gameObject.SetActive(false);
            if (chip != null)
            {
                bool up = t < wipeEnd;
                chip.gameObject.SetActive(up);
                if (up)
                {
                    float k = Easing.SmoothStep(Mathf.Clamp01(t / Mathf.Max(0.0001f, wipeEnd)));
                    var rect = chip.rectTransform;
                    rect.anchoredPosition = new Vector2(0f, CardCentre + ChipRise * (1f - k));
                    rect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, k);
                    SetAlpha(chip, k);
                }
            }

            // The cache: closed through the open wait (its seam humming), then
            // the seal snaps open and it is gone.
            bool cacheUp = _reveal.HasCache && seal.HasValue && t >= wipeEnd && t < seal.Value.End;
            SetCacheUp(cacheUp);
            if (cacheUp && cache != null)
            {
                float snap = Mathf.Clamp01((t - seal.Value.Start) / Mathf.Max(0.0001f, seal.Value.Duration));
                cache.transform.localScale = Vector3.one * (1f + 0.3f * snap);
                if (cacheSeam != null)
                {
                    float hum = wait.HasValue && t < wait.Value.End ? 0.45f + 0.25f * Mathf.Sin(t * 30f) : 0.5f;
                    SetAlpha(cacheSeam, t >= seal.Value.Start ? 1f - snap * 0.5f : hum);
                }
            }

            int n = _revealRows.Length;
            for (int r = 0; r < n; r++)
            {
                int i = _revealRows[r];
                float from, to;
                if (deal.HasValue)
                {
                    from = deal.Value.Start + deal.Value.Duration * r / n;
                    to = deal.Value.Start + deal.Value.Duration * (r + 1) / n;
                }
                else if (slide.HasValue)
                {
                    from = slide.Value.Start;
                    to = slide.Value.End;
                }
                else
                {
                    from = to = 0f;
                }

                float arrive = to > from ? Mathf.Clamp01((t - from) / (to - from)) : (t >= to ? 1f : 0f);
                float x = Mathf.Lerp(0f, _homeX[i], Easing.OutCubic(arrive));

                bool faceUp = true;
                float scaleX = 1f;
                if (flips.TryGetValue(r, out var flip))
                {
                    float k = Mathf.Clamp01((t - flip.Start) / Mathf.Max(0.0001f, flip.Duration));
                    faceUp = k >= 0.5f;
                    scaleX = t < flip.Start ? 1f : Mathf.Abs(Mathf.Cos(Mathf.PI * k));
                }

                DrawCard(i, x, t >= from ? arrive : 0f, scaleX, faceUp);
            }

            DrawEmbers(embersLit);
            DrawLinger(t);
            DrawFlare();
        }

        private void DrawCard(int i, float x, float alpha, float scaleX, bool faceUp)
        {
            var rect = RectAt(offerRects, i);
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                rect.localScale = new Vector3(Mathf.Max(0.02f, scaleX), 1f, 1f);
            }

            var group = CardGroup(i);
            if (group != null) group.alpha = alpha;
            ShowBack(i, !faceUp);
        }

        private void RestCard(int i, bool home)
        {
            if (i >= offerButtons.Length) return;
            var rect = RectAt(offerRects, i);
            if (rect != null)
            {
                if (home && i < _homeX.Length) rect.anchoredPosition = new Vector2(_homeX[i], rect.anchoredPosition.y);
                rect.localScale = Vector3.one;
            }

            var group = offerButtons[i] == null ? null : offerButtons[i].GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
            ShowBack(i, false);
        }

        // Attached at runtime, as BlurGroup is: a fade that belongs to one
        // animation is not something the scene should carry.
        private CanvasGroup CardGroup(int i)
        {
            if (i >= offerButtons.Length || offerButtons[i] == null) return null;
            // Not `??`: a missing component is a Unity fake null in the editor.
            var go = offerButtons[i].gameObject;
            var group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }

        private void DrawLinger(float t)
        {
            if (_lingerEnded) return;

            bool lingering = false;
            foreach (var beat in _reveal.Beats)
                if (beat.Kind == RevealBeatKind.Linger && t >= beat.Start && t < beat.End) lingering = true;
            if (!lingering) return;

            foreach (int r in _burstRows)
            {
                int i = _revealRows[r];
                if (offerHalos == null || i >= offerHalos.Length || offerHalos[i] == null) continue;
                SetAlpha(offerHalos[i], LingerHaloAlpha);
            }
        }

        private void RestHalo(int i)
        {
            if (offerHalos == null || i >= offerHalos.Length || offerHalos[i] == null || _page == null || i >= _page.Cards.Count) return;
            bool open = _page.Cards[i].Outcome == CardOutcome.Open && _page.ChoicesLeft > 0;
            SetAlpha(offerHalos[i], open ? 0.30f : 0f);
        }

        // The screen-edge flare: the frame's glow in the card's colour.
        private void DrawFlare()
        {
            if (frameGlow == null || _flareFrom < 0f) return;

            float k = (_revealClock - _flareFrom) / Mathf.Max(0.0001f, _flareFor);
            if (k >= 1f)
            {
                frameGlow.color = _glowRest;
                _flareFrom = -1f;
                return;
            }

            var lit = _flareColour;
            lit.a = FlareGlowAlpha;
            frameGlow.color = Color.Lerp(lit, _glowRest, Mathf.Clamp01(k));
        }

        // ---- embers ----------------------------------------------------------------------

        // One ember per choice above the row; a spent choice is snuffed.
        private void PaintEmbers()
        {
            if (_reveal != null && RevealRunning) return;
            int lit = _page == null || !_choosing ? 0 : _page.ChoicesLeft;
            DrawEmbers(lit);
        }

        private void DrawEmbers(int lit)
        {
            if (embers == null) return;

            int count = _page == null || !_choosing ? 0 : Mathf.Min(_page.Choices, embers.Length);
            for (int e = 0; e < embers.Length; e++)
            {
                var ember = embers[e];
                if (ember == null) continue;

                bool shown = e < count;
                ember.gameObject.SetActive(shown);
                if (!shown) continue;

                var rect = ember.rectTransform;
                rect.anchoredPosition = new Vector2(ReckoningScreen.EmberX(e, count), rect.anchoredPosition.y);
                SetAlpha(ember, e < lit ? EmberLit : EmberSpent);
            }
        }

        // ---- the cache as the focus ------------------------------------------------------

        // While the cache stands it is the page's focus entry (C10); when it
        // opens, a focus still on it moves to the first card.
        private void SetCacheUp(bool up)
        {
            if (cache != null && cache.gameObject.activeSelf != up) cache.gameObject.SetActive(up);
            if (up == _cacheUp) return;
            _cacheUp = up;
            if (!up && cache != null) cache.transform.localScale = Vector3.one;

            var current = UnityEngine.EventSystems.EventSystem.current;
            var selected = current == null ? null : current.currentSelectedGameObject;
            RefreshNavigation();
            if (cache != null && (up ? selected == _entrySelected : selected == cache.gameObject)) SelectEntry();
        }

        // ---- the detail ------------------------------------------------------------------

        private bool TipAllowed(int index, bool hone)
        {
            if (!RevealRunning) return true;

            _wantedTip = index;
            _wantedHone = hone;
            return false;
        }

        private static float CardCentre => ReckoningScreen.CardCentreY;
    }
}
