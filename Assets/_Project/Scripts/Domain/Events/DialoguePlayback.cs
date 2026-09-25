using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Events
{
    // ONE THING THE STAGE SAYS, in order: the outcome's result (narration plus
    // the effects line) when there is one, then each of the page's lines
    // (docs/PLAN_DIALOGUE_STAGE.md contract 3).
    public sealed class DialogueBeat
    {
        // What the stage paints: the speaker, side, expression and text. A
        // result beat carries a narration line built from the result text.
        public readonly EventLineView Line;

        // The effects summary, shown under a result beat's text. Empty on
        // every page line.
        public readonly string Effects;

        public readonly bool IsResult;

        // How many characters the typewriter reveals before the line is
        // full: tags excluded, which is what TMP's maxVisibleCharacters
        // counts. A result beat's effects line counts after one line break.
        public readonly int VisibleLength;

        private DialogueBeat(EventLineView line, string effects, bool isResult)
        {
            Line = line;
            Effects = effects ?? "";
            IsResult = isResult;

            int length = DialogueText.VisibleLength(line.Text);
            if (Effects.Length > 0) length += 1 + DialogueText.VisibleLength(Effects);
            VisibleLength = length;
        }

        public bool IsNarration => Line.IsNarration;

        public static DialogueBeat ForLine(EventLineView line) =>
            line == null ? null : new DialogueBeat(line, "", false);

        // Null when there is nothing to say: no result and no effects.
        public static DialogueBeat ForResult(string resultText, string effectsLine)
        {
            if (string.IsNullOrEmpty(resultText) && string.IsNullOrEmpty(effectsLine)) return null;

            var line = new EventLineView("", true, "", "", "", DialogueBust.Neutral, DialogueSide.Left, resultText ?? "");
            return new DialogueBeat(line, effectsLine, true);
        }

        // Result first (contract 3), then every line. Resume from a save
        // reaches this the same way (contract 18): the persisted result, then
        // the page from line 1.
        public static IReadOnlyList<DialogueBeat> Sequence(string resultText, string effectsLine,
            IReadOnlyList<EventLineView> lines)
        {
            var beats = new List<DialogueBeat>();
            var result = ForResult(resultText, effectsLine);
            if (result != null) beats.Add(result);

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    var beat = ForLine(line);
                    if (beat != null) beats.Add(beat);
                }
            }

            return beats;
        }
    }

    public static class DialogueText
    {
        // Characters outside <...> tags. Contract 12 allows only <i> in
        // authored text, and TMP's maxVisibleCharacters already skips tags,
        // so the typewriter's count and TMP's agree. An unclosed '<' counts
        // as text, as TMP draws it.
        public static int VisibleLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i)
                    {
                        i = close;
                        continue;
                    }
                }

                count++;
            }

            return count;
        }
    }

    public enum DialogueState
    {
        // The first speaker's bust slides in. Presses are ignored.
        Entering,

        // The typewriter runs. A press completes the line.
        Revealing,

        // The line is full. A press moves on.
        Revealed,

        // One speaker out to its edge, the next one in. Presses are ignored.
        Transitioning,

        // The rows are up. The playback no longer takes presses; the rows do.
        Choices,

        // The system menu is open: nothing ticks, presses are ignored.
        Suspended,

        // The panel closed. Terminal; every slide is snapped to its end.
        Closed,
    }

    // How the bust got to the current beat, so the stage knows what to draw.
    public enum BustCue
    {
        // No bust on this beat: narration, or the result.
        Hidden,

        // Slid in from its own edge with nothing going out (Entering).
        SlideIn,

        // The previous speaker slid out while this one slid in.
        SlideOutIn,

        // Same speaker, same side: the sprite swapped where it stands.
        SwapInPlace,
    }

    // THE DIALOGUE STAGE'S STATE MACHINE (docs/PLAN_DIALOGUE_STAGE.md,
    // Transition table). Engine-free: EventController feeds it presses, frame
    // numbers and unscaled time, and paints what it answers. Nothing here
    // reads a clock or a device.
    //
    // PRESSES ARE COUNTED BY THE FRAME THEY STARTED ON, never by how long
    // they are held. The press that finishes the last line opens the rows on
    // its own frame, and a choice press is accepted only if it started on a
    // LATER frame -- so that press can never also pick a choice (contract
    // 17).
    public sealed class DialoguePlayback
    {
        // Fast: a 200-character line (contract 13's cap) reads out in about
        // 3.3 s, and a press completes it at any point.
        public const float CharsPerSecond = 60f;

        // The first bust's slide in, and a speaker change's out-and-in.
        public const float SlideSeconds = 0.2f;

        // Float time summed frame by frame lands a hair under a whole
        // character; this keeps 0.25 s at 60/s at 15 rather than 14.
        private const double RevealEpsilon = 1e-4;

        private readonly IReadOnlyList<DialogueBeat> _beats;

        private double _revealed;
        private float _slideElapsed;
        private DialogueState _resumeTo;

        // The frame the rows appeared on; a choice press must start after it.
        private int _choicesFrame = int.MinValue;

        public DialoguePlayback(IReadOnlyList<DialogueBeat> beats)
        {
            _beats = beats ?? Array.Empty<DialogueBeat>();

            if (_beats.Count == 0)
            {
                // Nothing to say: straight to the rows. They were never
                // hidden behind a press, so any press may pick one.
                BeatIndex = -1;
                State = DialogueState.Choices;
                return;
            }

            EnterBeat(0, null);
        }

        public DialogueState State { get; private set; }

        // The beat on screen; -1 only for an empty sequence.
        public int BeatIndex { get; private set; }

        public int BeatCount => _beats.Count;

        public DialogueBeat Current => BeatIndex >= 0 && BeatIndex < _beats.Count ? _beats[BeatIndex] : null;

        // The beat whose bust is sliding out during SlideOutIn, else null.
        public DialogueBeat Outgoing { get; private set; }

        public BustCue Cue { get; private set; }

        // 0 at the start of a slide, 1 at its end and whenever nothing is
        // sliding. Linear; the stage eases it.
        public float SlideProgress =>
            IsSliding ? Math.Min(1f, _slideElapsed / SlideSeconds) : 1f;

        private bool IsSliding =>
            State == DialogueState.Entering || State == DialogueState.Transitioning
            || (State == DialogueState.Suspended
                && (_resumeTo == DialogueState.Entering || _resumeTo == DialogueState.Transitioning));

        // How many characters of the current beat show. The full length once
        // Revealed and after; 0 while its bust is still arriving.
        public int VisibleCharacters
        {
            get
            {
                var beat = Current;
                if (beat == null) return 0;

                switch (Effective)
                {
                    case DialogueState.Entering:
                    case DialogueState.Transitioning:
                        return 0;
                    case DialogueState.Revealing:
                        return Math.Min(beat.VisibleLength, (int)Math.Floor(_revealed + RevealEpsilon));
                    default:
                        return beat.VisibleLength;
                }
            }
        }

        // The state a suspension will return to, or the state itself.
        private DialogueState Effective => State == DialogueState.Suspended ? _resumeTo : State;

        // Whether the rows are on screen. They stay up under the system menu
        // if they were up when it opened.
        public bool ChoicesShown => Effective == DialogueState.Choices && State != DialogueState.Closed;

        // A choice press counts only while the rows are live and only if it
        // STARTED after the frame they appeared on.
        public bool AcceptsChoicePress(int pressStartFrame) =>
            State == DialogueState.Choices && pressStartFrame > _choicesFrame;

        // One press, consumed once, started on `frame`. Answers whether the
        // press did anything.
        public bool Advance(int frame)
        {
            switch (State)
            {
                case DialogueState.Revealing:
                    _revealed = Current.VisibleLength;
                    State = DialogueState.Revealed;
                    return true;

                case DialogueState.Revealed:
                    if (BeatIndex + 1 >= _beats.Count)
                    {
                        Outgoing = null;
                        State = DialogueState.Choices;
                        _choicesFrame = frame;
                        return true;
                    }

                    EnterBeat(BeatIndex + 1, Current);
                    return true;

                // Entering / Transitioning: ignored, not queued. Choices: the
                // rows take presses, not the playback. Suspended / Closed: no.
                default:
                    return false;
            }
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            switch (State)
            {
                case DialogueState.Entering:
                case DialogueState.Transitioning:
                    _slideElapsed += dt;
                    if (_slideElapsed >= SlideSeconds) FinishSlide();
                    return;

                case DialogueState.Revealing:
                    _revealed += dt * (double)CharsPerSecond;
                    if (_revealed + RevealEpsilon >= Current.VisibleLength) State = DialogueState.Revealed;
                    return;
            }
        }

        public void Suspend()
        {
            if (State == DialogueState.Suspended || State == DialogueState.Closed) return;
            _resumeTo = State;
            State = DialogueState.Suspended;
        }

        public void Resume()
        {
            if (State != DialogueState.Suspended) return;
            State = _resumeTo;
        }

        // Mid-slide: snap to the end and drop the outgoing bust.
        public void Close()
        {
            if (IsSliding) _slideElapsed = SlideSeconds;
            Outgoing = null;
            State = DialogueState.Closed;
        }

        // ---- stepping ------------------------------------------------------

        private void EnterBeat(int index, DialogueBeat previous)
        {
            BeatIndex = index;
            _revealed = 0;
            _slideElapsed = 0f;
            Outgoing = null;

            var next = _beats[index];

            // Narration (and the result) hides the bust at once.
            if (next.IsNarration)
            {
                Cue = BustCue.Hidden;
                BeginReveal();
                return;
            }

            bool previousHadBust = previous != null && !previous.IsNarration;

            if (!previousHadBust)
            {
                Cue = BustCue.SlideIn;
                State = DialogueState.Entering;
                return;
            }

            if (previous.Line.SpeakerId == next.Line.SpeakerId && previous.Line.Side == next.Line.Side)
            {
                // The same speaker with a new (or the same) expression swaps
                // in place: no slide.
                Cue = BustCue.SwapInPlace;
                BeginReveal();
                return;
            }

            Cue = BustCue.SlideOutIn;
            Outgoing = previous;
            State = DialogueState.Transitioning;
        }

        private void FinishSlide()
        {
            _slideElapsed = SlideSeconds;
            Outgoing = null;
            BeginReveal();
        }

        private void BeginReveal()
        {
            _revealed = 0;
            State = Current.VisibleLength > 0 ? DialogueState.Revealing : DialogueState.Revealed;
        }
    }
}
