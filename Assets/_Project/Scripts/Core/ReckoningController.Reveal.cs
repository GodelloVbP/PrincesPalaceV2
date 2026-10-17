using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // THE REVEAL, played (docs/PLAN_REWARD_SCREENS.md C2-C4, C10, C11). The
    // timing is Domain's RevealSchedule and the look of each treatment is
    // RevealFeel; this file only draws them at the reveal's clock and fires
    // each beat's sound once. Nothing here derives a time.
    //
    // Every visual is a function of the clock (ApplyReveal, DrawEffects), so a
    // skip is one jump of the schedule (SkippedAt) and one redraw, and a frame
    // dropped or doubled cannot leave a card half-dealt. The two shakes are
    // the exception: StageShake owns its own short envelope.
    //
    // THE CARD'S FACE AND BACK TURN, NOT THE CARD: the button keeps its slide,
    // its fade and the nudge; the face and back take the flip, the lift, the
    // tremble and the punch; the under-glow, rays, flash and ring sit behind
    // them on the button and never flip.
    public partial class ReckoningController
    {
        [SerializeField] internal Button catcher;
        [SerializeField] internal Image chipGear;
        [SerializeField] internal Image chipRelic;
        [SerializeField] internal Button cache;
        [SerializeField] internal Image cacheSeam;
        [SerializeField] internal Image[] offerBacks;
        [SerializeField] internal Image[] embers;
        [SerializeField] internal Image[] offerUnderGlows;
        [SerializeField] internal Image[] offerRays;
        [SerializeField] internal Image[] offerFlashes;
        [SerializeField] internal Image[] offerRings;
        [SerializeField] internal Image[] revealMotes;
        [SerializeField] internal Image[] offerShadows;
        [SerializeField] internal Image[] offerRims;
        [SerializeField] internal Image[] offerFaceTints;
        [SerializeField] internal Image[] offerFaceRims;

        // The top-tier flare: light inside the cloth's inner rect, under the
        // cards, so it never washes the fight past the rope border.
        [SerializeField] internal Image flareGlow;

        // The fight's two stage racks (ScreenRegistry wires the same shakers
        // the fight's blows kick): a top-tier landing kicks them too.
        [SerializeField] internal StageShake[] stageShakes;

        private const float ChipRise = 140f;
        private const float EmberLit = 0.85f;
        private const float EmberSpent = 0.15f;
        private const float LingerHaloAlpha = 0.6f;
        private const float FlareGlowAlpha = 0.75f;

        // The face's quality wash over its dark ground, and its rim, at rest.
        private const float FaceWash = 0.34f;
        private const float FaceRimAlpha = 0.9f;
        private const float OpenHaloAlpha = 0.30f;

        // A particle is its card's colour pulled toward white, so it reads as
        // light rather than as paint.
        private const float MoteWhiten = 0.35f;

        private RevealSchedule _reveal;
        private float _revealClock;
        private Coroutine _revealRoutine;
        private int _fired;
        private bool _choosableFired;
        private bool _lingerEnded;
        private float _flareFrom = -1f;
        private float _flareFor;
        private Color _flareColour;

        // Row position -> page index for the page being revealed, each card's
        // resting x, and the rows whose landing was a top-tier burst.
        private int[] _revealRows = new int[0];
        private float[] _homeX = new float[0];
        private readonly List<int> _burstRows = new List<int>();

        // Per row, from the schedule: the build, the flip and the landing.
        private RowTimes[] _rowTimes = new RowTimes[0];
        private float _effectsEnd;
        private bool[] _pulseRested = new bool[0];

        // The landing's and the build's particles per row, sized to the card.
        private SpellEmitter[] _landEmitters = new SpellEmitter[0];
        private SpellEmitter[] _buildEmitters = new SpellEmitter[0];

        // Each card's under-glow at rest (PaintCardGround), by page index.
        private Color[] _underRest = new Color[0];

        private StageShake _panelShake;

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

        private struct RowTimes
        {
            public LandStyle Style;
            public float BuildFrom;
            public float BuildFor;
            public float FlipFrom;
            public float FlipFor;
            public float LandAt;
        }

        // Whether the choice controls act now: false during a beat and the
        // guard after it. For a test or a capture that must wait for the row.
        public bool AcceptsChoices => !RevealBlocks();

        // Whether the reveal has nothing left to draw: no beat, guard, linger,
        // flare or landing effect. For a film that stops on the settled row.
        public bool RevealSettled => _revealRoutine == null;

        private bool RevealBlocks() =>
            _reveal != null && (_reveal.IsRunningAt(_revealClock) || _reveal.GuardsAt(_revealClock));

        private bool RevealRunning => _reveal != null && _reveal.IsRunningAt(_revealClock);

        // Wall seconds per authored second of RevealFeel.
        private float FeelScale => ReckoningTime.Scale(_preset);

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
                cards.Add(LiveUpgrade.RevealCardOf(save, _page.Cards[i], i < _faces.Length ? _faces[i] : null));
            }

            for (int i = 0; i < offerButtons.Length; i++)
            {
                var rect = RectAt(offerRects, i);
                _homeX[i] = rect == null ? 0f : rect.anchoredPosition.x;
            }

            _reveal = RevealSchedule.For(cards, _page.Choices, _preset, _page.SourceClass);
            ReadRowTimes(n);
            PaintCache();
            _revealClock = 0f;
            _fired = 0;
            _choosableFired = false;
            _lingerEnded = false;
            _flareFrom = -1f;
            _burstRows.Clear();
            _wantedTip = -1;
            HideCardTip();
            EnsurePanelShake();

            if (catcher != null) catcher.gameObject.SetActive(true);
            HoldRims(true);
            ApplyReveal();
            _revealRoutine = StartCoroutine(RunReveal());
        }

        // The offer buttons' hover/focus rim stays off until the row accepts
        // choices: a box on card 1 through the reveal marks a choice the
        // player cannot make yet.
        private void HoldRims(bool held)
        {
            if (offerButtons == null) return;
            foreach (var button in offerButtons)
            {
                var box = button == null ? null : button.GetComponent<HoverBox>();
                if (box != null) box.Suppressed = held;
            }
        }

        // The schedule's per-card beats, read once, and the emitters sized to
        // the row's cards; the effects end when the last landing's tail does.
        private void ReadRowTimes(int n)
        {
            _rowTimes = new RowTimes[n];
            _pulseRested = new bool[n];
            _landEmitters = new SpellEmitter[n];
            _buildEmitters = new SpellEmitter[n];
            _effectsEnd = 0f;

            foreach (var beat in _reveal.Beats)
            {
                if (beat.Card < 0 || beat.Card >= n) continue;
                switch (beat.Kind)
                {
                    case RevealBeatKind.Anticipate:
                        _rowTimes[beat.Card].BuildFrom = beat.Start;
                        _rowTimes[beat.Card].BuildFor = beat.Duration;
                        break;
                    case RevealBeatKind.Flip:
                        _rowTimes[beat.Card].FlipFrom = beat.Start;
                        _rowTimes[beat.Card].FlipFor = beat.Duration;
                        break;
                    case RevealBeatKind.Land:
                        _rowTimes[beat.Card].Style = beat.Style;
                        _rowTimes[beat.Card].LandAt = beat.Start;
                        _effectsEnd = Mathf.Max(_effectsEnd,
                            beat.Start + RevealFeel.TailSeconds(beat.Style) * FeelScale);
                        break;
                }
            }

            for (int r = 0; r < n; r++)
            {
                if (_rowTimes[r].BuildFor <= 0f) _rowTimes[r].BuildFrom = _rowTimes[r].FlipFrom;

                var rect = RectAt(offerRects, _revealRows[r]);
                float w = rect == null ? 200f : rect.rect.width;
                float h = rect == null ? 300f : rect.rect.height;
                _landEmitters[r] = RevealFeel.LandEmitter(_rowTimes[r].Style, w, h);
                _buildEmitters[r] = RevealFeel.BuildEmitter(_rowTimes[r].Style, w, h);
            }
        }

        // The reward panel's shaker, attached at runtime as CardGroup's fade
        // is: it lives on the panel's content (the frame and its drop), which
        // nothing else moves, so the frame's lift and wipe stay its own.
        private void EnsurePanelShake()
        {
            if (_panelShake != null || frameGlow == null) return;
            var content = frameGlow.rectTransform.parent;
            if (content == null) return;
            var shake = content.GetComponent<StageShake>();
            _panelShake = shake != null ? shake : content.gameObject.AddComponent<StageShake>();
        }

        // Everything at rest, whatever the reveal was doing: the cards home and
        // face-up, the chip and cache gone, the embers painted from the page.
        private void StopReveal()
        {
            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = null;

            bool was = _reveal != null;
            HoldRims(false);
            for (int i = 0; i < offerButtons.Length; i++) RestCard(i, was);
            RestEffects();
            _reveal = null;
            _cacheUp = false;
            if (catcher != null) catcher.gameObject.SetActive(false);
            if (chipGear != null) chipGear.gameObject.SetActive(false);
            if (chipRelic != null) chipRelic.gameObject.SetActive(false);
            if (cache != null) cache.gameObject.SetActive(false);
            if (was) RestFlare();
            PaintEmbers();
        }

        private IEnumerator RunReveal()
        {
            bool settled = false;
            while (true)
            {
                FireDue();
                HoldRims(RevealBlocks());

                // Once the row has landed and been drawn there, the cards are
                // left to the nudge and the choices; only the landing's
                // effects, the linger and the flare still move.
                if (!settled)
                {
                    ApplyReveal();
                    settled = _choosableFired;
                }
                else
                {
                    DrawEffects(_revealClock);
                    DrawLinger(_revealClock);
                    DrawFlare();
                }

                if (_revealClock >= RevealEnd()) break;

                yield return null;
                _revealClock += Tick;
            }

            _revealRoutine = null;
            HoldRims(false);
            if (!_lingerEnded) EndLinger();
            RestEffects();
            _reveal = null;
            PaintEmbers();
        }

        // The clock runs past the row's landing for the guard, the landing's
        // effects, the linger and a flare; none of them blocks input but the
        // guard.
        private float RevealEnd()
        {
            float end = _reveal.GuardUntil;
            if (!_reveal.Skipped) end = Mathf.Max(end, _effectsEnd);
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
        // turns face-up at once with its glow at rest; a top tier keeps only
        // its flare. Nothing else sounds or shakes. The row is choosable from
        // now, behind the guard.
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
                case RevealBeatKind.Deal:
                    SoundController.Play(Sound.CardDeal);
                    break;
                case RevealBeatKind.Anticipate:
                    if (beat.Style == LandStyle.Burst) SoundController.Play(Sound.RevealRiser);
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

        // A Rare rings and kicks the panel; a top-tier live upgrade bursts,
        // flares, rings the bell over its impact and kicks the panel and the
        // stage. The boss page's peak is the page's, once, under its first
        // top-tier bell.
        private void Land(RevealBeat beat)
        {
            // Every landing kicks, scaled by RevealFeel; only a Rare rings.
            if (beat.Style != LandStyle.Burst)
            {
                if (beat.Style == LandStyle.Shimmer) SoundController.Play(Sound.RevealRing);
                Kick(beat.Style);
                return;
            }

            _burstRows.Add(beat.Card);
            SoundController.Play(Sound.RevealImpact);
            SoundController.Play(Sound.LegendaryBell);
            SoundController.Play(Sound.RevealRumble);
            if (_burstRows.Count == 1 && _page != null && _page.SourceClass == EncounterClass.Boss)
                SoundController.Play(Sound.BossPeak);
            StartFlare(_revealRows[beat.Card], RevealSchedule.FlareSeconds * FeelScale);
            Kick(beat.Style);
        }

        // The kicks run on real time (StageShake's envelope), so their length
        // follows the reveal's own speed: the preset and the test seam.
        private void Kick(LandStyle style)
        {
            float seconds = RevealFeel.ShakeSeconds(style) * FeelScale / Mathf.Max(0.001f, SpeedMultiplier);
            float panel = RevealFeel.PanelShakePixels(style) / StageShake.MaxPixels;
            if (_panelShake != null) _panelShake.Kick(panel, seconds);

            float stage = RevealFeel.StageShakePixels(style) / StageShake.MaxPixels;
            if (stage <= 0f || stageShakes == null) return;
            foreach (var shake in stageShakes)
                if (shake != null) shake.Kick(stage, seconds);
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
            RevealBeat? wipe = null, seal = null, slide = null;
            int embersLit = 0;
            foreach (var beat in _reveal.Beats)
            {
                switch (beat.Kind)
                {
                    case RevealBeatKind.Wipe: wipe = beat; break;
                    case RevealBeatKind.Seal: seal = beat; break;
                    case RevealBeatKind.Slide: slide = beat; break;
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
                    // It hums for as long as it stands closed.
                    float hum = 0.45f + 0.25f * Mathf.Sin(t * 30f);
                    SetAlpha(cacheSeam, t >= seal.Value.Start ? 1f - snap * 0.5f : hum);
                }
            }

            // Every card comes out face-down: out of the cache all together, or
            // dealt out of the chip left to right.
            int n = _revealRows.Length;
            for (int r = 0; r < n; r++)
            {
                int i = _revealRows[r];
                float from = 0f, to = 0f;
                if (slide.HasValue)
                {
                    from = slide.Value.Start;
                    to = slide.Value.End;
                    if (!_reveal.HasCache)
                    {
                        from = slide.Value.Start + slide.Value.Duration * r / n;
                        to = slide.Value.Start + slide.Value.Duration * (r + 1) / n;
                    }
                }

                float arrive = to > from ? Mathf.Clamp01((t - from) / (to - from)) : (t >= to ? 1f : 0f);
                float x = Mathf.Lerp(0f, _homeX[i], Easing.OutCubic(arrive));
                bool faceUp = r >= _rowTimes.Length || t >= _rowTimes[r].FlipFrom + _rowTimes[r].FlipFor * 0.5f;
                DrawCard(i, x, t >= from ? arrive : 0f, faceUp);
            }

            DrawEmbers(embersLit);
            DrawEffects(t);
            DrawLinger(t);
            DrawFlare();
        }

        private void DrawCard(int i, float x, float alpha, bool faceUp)
        {
            var rect = RectAt(offerRects, i);
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                rect.localScale = Vector3.one;
            }

            var group = CardGroup(i);
            if (group != null) group.alpha = alpha;
            ShowBack(i, !faceUp);
        }

        // Each card's build and landing at `t` (float.MaxValue: everything at
        // rest), and the particles of the whole row.
        private void DrawEffects(float t)
        {
            if (_reveal == null) return;
            if (_reveal.Skipped || t >= _effectsEnd + 0.0001f)
            {
                RestEffects();
                return;
            }

            float scale = Mathf.Max(0.0001f, FeelScale);
            int mote = 0;
            for (int r = 0; r < _rowTimes.Length; r++)
            {
                int i = _revealRows[r];
                var row = _rowTimes[r];
                var colour = CardColour(i);

                float build = row.BuildFor > 0f
                    ? Mathf.Clamp01((t - row.BuildFrom) / row.BuildFor)
                    : (t >= row.FlipFrom ? 1f : 0f);
                if (t < row.BuildFrom) build = 0f;
                float afterLand = t >= row.LandAt ? (t - row.LandAt) / scale : -1f;

                // The under-glow: builds, flashes on landing, settles to rest.
                float rest = i < _underRest.Length ? _underRest[i].a : 0f;
                RevealFeel.Glow(row.Style, build, afterLand, rest, out float glowAlpha, out float glowScale);
                var under = Part(offerUnderGlows, i);
                if (under != null)
                {
                    under.color = WithAlpha(colour, glowAlpha);
                    under.rectTransform.localScale = Vector3.one * glowScale;
                }

                // The card: lifted and trembling through its build, turning
                // through its flip, punching as it lands.
                float flipK = row.FlipFor > 0f ? Mathf.Clamp01((t - row.FlipFrom) / row.FlipFor) : 1f;
                float turn = t < row.FlipFrom ? 1f : Mathf.Abs(Mathf.Cos(Mathf.PI * flipK));
                float punch = RevealFeel.Punch(row.Style, afterLand);
                float lift = RevealFeel.Lift(row.Style, build, afterLand);
                RevealFeel.Tremble(row.Style, afterLand < 0f && t < row.FlipFrom ? build : 0f, t / scale,
                    out float dx, out float dy);
                PoseCard(i, new Vector2(dx, lift + dy), new Vector3(Mathf.Max(0.02f, turn) * punch, punch, 1f));

                // The landing's light.
                float reach = RevealFeel.RingReach(row.Style);
                float ringSeconds = RevealFeel.RingSeconds(row.Style);
                var ring = Part(offerRings, i);
                if (reach > 0f && afterLand >= 0f && afterLand < ringSeconds)
                    UiBurst.Ring(ring, afterLand / ringSeconds, colour, reach);
                else if (ring != null) SetAlpha(ring, 0f);

                bool rays = row.Style == LandStyle.Burst && afterLand >= 0f && afterLand < RevealFeel.RaysSeconds;
                if (rays)
                {
                    UiBurst.Rays(Part(offerRays, i), afterLand / RevealFeel.RaysSeconds, colour);
                    UiBurst.Core(Part(offerFlashes, i), afterLand / RevealFeel.RaysSeconds, colour);
                }
                else
                {
                    if (Part(offerRays, i) != null) SetAlpha(Part(offerRays, i), 0f);
                    if (Part(offerFlashes, i) != null) SetAlpha(Part(offerFlashes, i), 0f);
                }

                // A Rare's edge pulse: its halo flares and falls back to rest.
                if (row.Style == LandStyle.Shimmer && afterLand >= 0f && !_pulseRested[r])
                {
                    var halo = Part(offerHalos, i);
                    float u = afterLand / RevealFeel.EdgePulseSeconds;
                    if (u >= 1f)
                    {
                        _pulseRested[r] = true;
                        RestHalo(i);
                    }
                    else if (halo != null)
                    {
                        SetAlpha(halo, Mathf.Lerp(RevealFeel.EdgePulseAlpha, OpenHaloAlpha, u));
                    }
                }

                var centre = RectAt(offerRects, i) is RectTransform cardRect ? cardRect.anchoredPosition : Vector2.zero;
                var light = Color.Lerp(colour, Color.white, MoteWhiten);
                if (_buildEmitters[r] != null && row.BuildFor > 0f)
                    mote = DrawMotes(_buildEmitters[r], r, row.BuildFrom, t, scale, centre, light, mote);
                mote = DrawMotes(_landEmitters[r], r, row.LandAt, t, scale, centre, light, mote);
            }

            HideMotes(mote);
        }

        // One emitter's live particles into the shared pool from `next` on;
        // returns the next free mote. A pool that runs out drops the rest.
        private int DrawMotes(SpellEmitter spec, int row, float windowStart, float t, float scale, Vector2 centre,
                              Color tint, int next)
        {
            if (spec == null || revealMotes == null || t < windowStart) return next;

            int seed = spec.seed + row * 31;
            int count = SpellEmitterSim.CountOf(spec);
            var source = new UiVec(centre.x + spec.sourceDx, centre.y + spec.sourceDy);
            for (int p = 0; p < count && next < revealMotes.Length; p++)
            {
                float birth = windowStart + SpellEmitterSim.BirthOf(spec, p, 0f) * scale;
                if (t < birth) continue;

                var origin = source + SpellEmitterSim.LateralOf(spec, seed, p, UiVec.Zero, new UiVec(0f, 1f));
                var particle = SpellEmitterSim.At(spec, seed, p, origin, SpellEmitterSim.LaunchOf(spec, seed, p, 1f),
                    (t - birth) / scale, 1);
                if (!particle.Alive) continue;

                var image = revealMotes[next++];
                if (image == null) continue;
                image.rectTransform.anchoredPosition = new Vector2(particle.Position.X, particle.Position.Y);
                image.rectTransform.localScale = Vector3.one * particle.Scale;
                image.color = WithAlpha(tint, particle.Alpha);
            }

            return next;
        }

        private void HideMotes(int from)
        {
            if (revealMotes == null) return;
            for (int m = from; m < revealMotes.Length; m++)
                if (revealMotes[m] != null && revealMotes[m].color.a > 0f) SetAlpha(revealMotes[m], 0f);
        }

        // Every card's effects at rest: posed home, the light out, the glow at
        // its resting level, no particles.
        private void RestEffects()
        {
            for (int i = 0; i < offerButtons.Length; i++)
            {
                PoseCard(i, Vector2.zero, Vector3.one);
                if (Part(offerRays, i) != null) SetAlpha(Part(offerRays, i), 0f);
                if (Part(offerFlashes, i) != null) SetAlpha(Part(offerFlashes, i), 0f);
                if (Part(offerRings, i) != null) SetAlpha(Part(offerRings, i), 0f);
                RestUnderGlow(i);
            }

            for (int r = 0; r < _pulseRested.Length; r++)
            {
                if (_pulseRested[r] || r >= _rowTimes.Length || _rowTimes[r].Style != LandStyle.Shimmer) continue;
                _pulseRested[r] = true;
                RestHalo(_revealRows[r]);
            }

            HideMotes(0);
        }

        // The face and the back move together, with the card's shadow and
        // rim: offset from home, and scaled.
        private void PoseCard(int i, Vector2 offset, Vector3 scale)
        {
            var shadow = Part(offerShadows, i);
            if (shadow != null)
            {
                shadow.rectTransform.anchoredPosition = offset + new Vector2(0f, -ReckoningScreen.CardShadowDrop);
                shadow.rectTransform.localScale = scale;
            }

            var rim = Part(offerRims, i);
            if (rim != null)
            {
                rim.rectTransform.anchoredPosition = offset;
                rim.rectTransform.localScale = scale;
            }

            if (offerFaces != null && i < offerFaces.Length && offerFaces[i] != null)
            {
                var face = (RectTransform)offerFaces[i].transform;
                face.anchoredPosition = offset;
                face.localScale = scale;
            }

            var back = Part(offerBacks, i);
            if (back != null)
            {
                back.rectTransform.anchoredPosition = offset;
                back.rectTransform.localScale = scale;
            }
        }

        // What card `i` stands on, at rest: the face paint calls this with
        // the open/spent state the halo reads. The quality's colour glows
        // under it, so the row keeps reading by rarity after it lands (C4; a
        // running reveal draws the glow itself and settles onto this), and
        // its shadow and rim are seated on the card back's drawn shape.
        private void PaintCardGround(int i, Color colour, int tier, bool open, bool spent)
        {
            if (_underRest.Length != offerButtons.Length) _underRest = new Color[offerButtons.Length];
            if (i < 0 || i >= _underRest.Length) return;

            SeatCardParts(i);

            // The face's wash and rim carry the quality colour; the face is
            // hidden while the back shows, so neither can leak ahead of a flip.
            if (Part(offerFaceTints, i) != null)
                Part(offerFaceTints, i).color = WithAlpha(colour, open ? FaceWash : spent ? 0f : FaceWash * 0.5f);
            if (Part(offerFaceRims, i) != null)
                Part(offerFaceRims, i).color = WithAlpha(colour, open ? FaceRimAlpha : spent ? 0.25f : FaceRimAlpha * 0.5f);

            float rest = RevealFeel.RestGlow(tier);
            _underRest[i] = WithAlpha(colour, open ? rest : spent ? 0f : rest * 0.5f);
            if (_revealRoutine == null) RestUnderGlow(i);
        }

        // The tree is emitted at the five-card row; a shorter row's wider
        // card takes its light, shadow and rim at its own size. The back keeps
        // its aspect inside the card's box, so the shadow and the rim take the
        // back's fitted size, not the box's.
        private void SeatCardParts(int i)
        {
            var card = RectAt(offerRects, i);
            if (card == null) return;

            float boxW = card.rect.width;
            float w = Mathf.Min(boxW, card.rect.height * RewardArt.CardBackAspect);
            var size = new Vector2(w, w / RewardArt.CardBackAspect);
            Size(Part(offerRims, i), size);
            Size(Part(offerShadows, i), size * ReckoningScreen.CardShadowSpread);
            Size(Part(offerUnderGlows, i), new Vector2(boxW * ReckoningScreen.UnderGlowWidth,
                card.rect.height * ReckoningScreen.UnderGlowHeight));
            Size(Part(offerRays, i), Vector2.one * boxW * ReckoningScreen.RaysSpan);
            Size(Part(offerFlashes, i), Vector2.one * boxW * ReckoningScreen.FlashSpan);
            Size(Part(offerRings, i), Vector2.one * boxW * ReckoningScreen.RingSpan);
        }

        private static void Size(Image image, Vector2 size)
        {
            if (image != null) image.rectTransform.sizeDelta = size;
        }

        private void RestUnderGlow(int i)
        {
            var under = Part(offerUnderGlows, i);
            if (under == null) return;
            under.color = i < _underRest.Length ? _underRest[i] : Color.clear;
            under.rectTransform.localScale = Vector3.one;
        }

        private Color CardColour(int i)
        {
            var face = i < _faces.Length ? _faces[i] : null;
            return HexColour(face?.Hex ?? "#FFFFFF");
        }

        private static Image Part(Image[] items, int i) =>
            items != null && i >= 0 && i < items.Length ? items[i] : null;

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
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
            SetAlpha(offerHalos[i], open ? OpenHaloAlpha : 0f);
        }

        // The flare: the cloth's inner light in the card's colour, fading out.
        private void DrawFlare()
        {
            if (flareGlow == null || _flareFrom < 0f) return;

            float k = (_revealClock - _flareFrom) / Mathf.Max(0.0001f, _flareFor);
            if (k >= 1f)
            {
                RestFlare();
                return;
            }

            flareGlow.color = WithAlpha(_flareColour, FlareGlowAlpha * (1f - Mathf.Clamp01(k)));
        }

        private void RestFlare()
        {
            _flareFrom = -1f;
            if (flareGlow != null) SetAlpha(flareGlow, 0f);
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
