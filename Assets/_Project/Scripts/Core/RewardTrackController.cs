using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // Paints the reward track, scrolls it to where the player is, and hands
    // over what the track owes.
    //
    // NOTHING HERE PLACES A NODE. Every dot, caption and level number was
    // emitted at its real coordinate, because level 40 sits at the same x
    // forever -- so the only things that move are the content rect, the caret
    // over the next reward, the halo under the player's own node and the
    // ribbon's window box. Four transforms against a hundred.
    //
    // FOUR STATES OUT OF TWO INTEGERS. `level` is what has been EARNED and
    // `claimedTrackLevel` is what has been PAID, and the whole screen is
    // RewardTrack.StateOf over that pair. They used to be kept in lockstep by
    // RewardApplier claiming after every fight; collection is a thing the
    // player does now, so the gap between them is visible, countable and
    // clickable rather than a 14px tick in a corner.
    public partial class RewardTrackController : MonoBehaviour
    {
        [SerializeField] internal RectTransform viewport;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal RectTransform railFill;
        [SerializeField] internal Image shimmer;
        [SerializeField] internal RectTransform shimmerRect;
        [SerializeField] internal Image hereHalo;
        [SerializeField] internal RectTransform hereHaloRect;
        [SerializeField] internal RectTransform nextCaret;
        [SerializeField] internal Image claimBurst;
        [SerializeField] internal RectTransform claimBurstRect;

        [SerializeField] internal TMP_Text summary;
        [SerializeField] internal Button collectButton;
        [SerializeField] internal TMP_Text collectLabel;
        [SerializeField] internal Button closeButton;

        [SerializeField] internal RectTransform cardRect;
        [SerializeField] internal Image cardMat;
        [SerializeField] internal Image cardArt;
        [SerializeField] internal TMP_Text cardKicker;
        [SerializeField] internal TMP_Text cardLevel;
        [SerializeField] internal TMP_Text cardCaption;
        [SerializeField] internal TMP_Text cardState;

        [SerializeField] internal RectTransform ribbon;
        [SerializeField] internal RectTransform ribbonFill;
        [SerializeField] internal RectTransform ribbonPlayhead;
        [SerializeField] internal RectTransform ribbonWindow;
        [SerializeField] internal Button ribbonGrab;

        [SerializeField] internal Button[] dots;
        [SerializeField] internal Image[] discs;
        [SerializeField] internal Image[] rings;
        [SerializeField] internal Image[] pulses;
        [SerializeField] internal Image[] mats;
        [SerializeField] internal Image[] icons;
        [SerializeField] internal GameObject[] seals;
        [SerializeField] internal TMP_Text[] captions;
        [SerializeField] internal TMP_Text[] levelNumbers;
        [SerializeField] internal GameObject[] ribbonTicks;
        [SerializeField] internal TMP_Text[] ribbonNumbers;

        private bool _wired;

        // Whether anything has been painted since this panel was last opened.
        // The guard that keeps the first Refresh from reading as a level-up.
        private bool _painted;

        // What the last Refresh painted. Held so the motion pass can ask what
        // state a node is in without re-deriving it from the save sixty times a
        // second -- and so a claim can diff against what was on screen to know
        // which bursts to fire.
        private int _level = RewardTrack.StartingLevel;
        private int _claimed;

        // Which node the card is resting on. -1 means "nothing hovered", which
        // is not the same as "no node": the card falls back to level+1, and
        // saying that with a sentinel rather than by writing level+1 in here
        // keeps the fallback in one place.
        private int _hovered = -1;

        private static readonly Color DiscToCome = Hex(RewardTrackScreen.DiscToCome);
        private static readonly Color DiscLit = Hex(RewardTrackScreen.DiscLit);
        private static readonly Color DiscHere = Hex(RewardTrackScreen.DiscHere);
        private static readonly Color RimToCome = Hex(RewardTrackScreen.RimToCome);
        private static readonly Color Gold = Hex(RewardTrackScreen.Gold);
        private static readonly Color MarkInk = Hex(RewardTrackScreen.MarkInk);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private static readonly Color TextToCome = Hex(RewardTrackScreen.TextToCome);
        private static readonly Color TextWaiting = Hex(RewardTrackScreen.TextWaiting);
        private static readonly Color TextCollected = Hex(RewardTrackScreen.TextCollected);
        private static readonly Color TextCollectedDim = Hex(RewardTrackScreen.TextCollectedDim);
        private static readonly Color TextHere = Hex(RewardTrackScreen.TextHere);
        private static readonly Color TextQuiet = Hex(RewardTrackScreen.TextQuiet);

        private void OnEnable()
        {
            Wire();
            Refresh();
            BeginFlyIn();
        }

        private void OnDisable()
        {
            // A panel closed mid-glide would reopen still animating toward a
            // level the player has since left. Every motion this screen runs is
            // cleared here rather than left to reassert itself.
            StopAllCoroutines();
            _hovered = -1;

            // And the next open is a FIRST paint again, not a level-up. A
            // character who gained eight levels while this was shut should be
            // flown to, not ignited eight times -- see the guard in Refresh.
            _painted = false;
        }

        private void Close() => gameObject.SetActive(false);

        public void Refresh()
        {
            int before = _level;
            bool first = !_painted;
            _painted = true;

            _level = SquadTrack.BestLevel();
            _claimed = ClaimedLevel();

            // The fade cache is filled BEFORE the nodes are painted, because
            // PaintNode multiplies by it. PaintDepthOfField is the scroll-time
            // path and deliberately skips whatever did not move; a state change
            // has to repaint everything, so it gets its own full pass.
            RefreshFade();

            PaintNodes();
            PaintRail();
            PaintSummary();
            PaintRibbon();
            PaintCard(CardLevel());
            MoveCaret();
            MoveHalo();

            // A LEVEL ARRIVING WHILE THE PANEL IS OPEN, which the first paint
            // is not: on open, _level goes from 1 to whatever the save holds,
            // and treating that as a gain would ignite the player's own node
            // every single time the screen is looked at.
            if (!first && _level > before) OnLevelGained(_level);
        }

        // How far the track has actually PAID the best-levelled character.
        //
        // Read off the same character BestLevel picked, not off the whole
        // squad: a watermark from one character against a level from another
        // would show rewards as collected that nobody has had.
        private static int ClaimedLevel()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return 0;

            int best = RewardTrack.StartingLevel;
            int claimed = 0;
            foreach (var character in save.ActiveSquad())
            {
                if (character == null) continue;
                if (character.level < best) continue;

                best = character.level;
                claimed = character.claimedTrackLevel;
            }

            return claimed;
        }

        // ---- the rail -------------------------------------------------------

        private void PaintNodes()
        {
            if (dots == null) return;

            for (int i = 0; i < dots.Length; i++)
            {
                PaintNode(i, RewardTrackLayout.FirstLevel + i);
            }
        }

        // ONE WRITE PATH for a node's colour, and it is deliberately also the
        // depth-of-field path.
        //
        // The falloff is a multiplier on the alpha of everything this writes,
        // so painting and fading cannot be two passes that disagree about what
        // colour a node currently is. The alternative -- caching six base
        // colours per node so a fade pass can multiply them -- is six arrays of
        // ninety-nine and a second place that has to be kept in step with the
        // state machine.
        private void PaintNode(int i, int nodeLevel)
        {
            var state = RewardTrack.StateOf(nodeLevel, _level, _claimed);
            bool waiting = RewardTrack.IsWaiting(nodeLevel, _level, _claimed);
            bool milestone = RewardTrackLayout.IsMilestone(nodeLevel);
            bool lit = state != TrackNodeState.ToCome;
            float fade = FadeAt(i);

            if (Has(discs, i))
            {
                discs[i].color = Faded(
                    state == TrackNodeState.Here ? DiscHere :
                    lit ? DiscLit : DiscToCome, fade);
            }

            // THE RING DOES TWO JOBS the state picks between -- the gold plate
            // that marks a milestone, always on; or the 1px rim an unreached
            // filler disc carries, gone the moment it lights.
            if (Has(rings, i))
            {
                rings[i].color = Faded(milestone ? Gold : (lit ? Clear : RimToCome), fade);
            }

            // The mark inside the disc, tinted so it reads AGAINST the disc
            // rather than with it: dark ink on a lit node, gold on a dark one.
            // A single colour would vanish on one half of the rail.
            if (Has(icons, i)) icons[i].color = Faded(lit ? MarkInk : Gold, fade);

            // The art slot's mat carries the reward-kind hue, and ONLY on an
            // unreached node -- a tint laid over gold metal reads as tarnish.
            if (Has(mats, i))
            {
                mats[i].color = Faded(
                    lit ? Clear : Hex(RewardTrackLayout.MatTintFor(nodeLevel)), fade);
            }

            // COLLECTED, which is not the same question as reached.
            if (seals != null && i < seals.Length && seals[i] != null)
            {
                seals[i].SetActive(state == TrackNodeState.Collected);
            }

            // The pulse plays on ANY waiting node, including the player's own
            // -- which draws pale because Here wins, and would otherwise be the
            // one node most likely to be holding an uncollected reward with
            // nothing saying so.
            if (Has(pulses, i))
            {
                var pulse = pulses[i].gameObject;
                if (pulse.activeSelf != waiting) pulse.SetActive(waiting);
            }

            if (captions != null && i < captions.Length && captions[i] != null)
            {
                captions[i].SetContent(RewardTrackNames.Of(RewardTrack.At(nodeLevel)));
                captions[i].color = Faded(CaptionColour(state), fade);
            }

            if (levelNumbers != null && i < levelNumbers.Length && levelNumbers[i] != null)
            {
                levelNumbers[i].SetContent(nodeLevel.ToString());
                levelNumbers[i].color = Faded(NumberColour(state), fade);
            }
        }

        private static Color CaptionColour(TrackNodeState state)
        {
            switch (state)
            {
                case TrackNodeState.Here: return TextHere;
                case TrackNodeState.Waiting: return TextWaiting;
                case TrackNodeState.Collected: return TextCollected;
                default: return TextToCome;
            }
        }

        // The number is dimmer than the caption on a collected node and BRIGHTER
        // on the two live ones. What the player is scanning for changes with the
        // state: on a node they have finished with, the reward's name is the
        // memory and the number is filing; on the one they are standing at, the
        // number is the thing.
        private static Color NumberColour(TrackNodeState state)
        {
            switch (state)
            {
                case TrackNodeState.Here: return TextHere;
                case TrackNodeState.Waiting: return TextCollected;
                case TrackNodeState.Collected: return TextCollectedDim;
                default: return TextToCome;
            }
        }

        // The lit half of the rail, stretched to the player's own node.
        //
        // Left-pivoted and driven by WIDTH, never by anchors -- anchors are
        // relative to the parent, and the parent here is a 19,000px content
        // rect. The dossier's XP bar records what that mistake looks like when
        // it ships.
        private void PaintRail()
        {
            if (railFill == null) return;

            float width = _level < RewardTrackLayout.FirstLevel
                ? 0f
                : RewardTrackLayout.NodeX(_level);

            railFill.sizeDelta = new Vector2(width, railFill.sizeDelta.y);
        }

        private void PaintSummary()
        {
            if (summary != null)
            {
                int next = RewardTrack.NextRewardLevel(_level);
                if (next <= 0) summary.Set(UiStrings.TrackSummaryComplete, _level);
                else summary.Set(UiStrings.TrackSummary, _level, next);
            }

            PaintCollectButton();
        }

        // COLLECT-ALL, shown only when there is something to collect.
        //
        // A button that is always there and usually does nothing teaches the
        // player it never does anything; this one appearing IS the notification
        // that the track owes them, which is the job the old tick was failing
        // at from a corner of a 26px disc.
        private void PaintCollectButton()
        {
            int owed = RewardTrack.UnclaimedCount(_level, _claimed);

            if (collectButton != null && collectButton.gameObject.activeSelf != owed > 0)
            {
                collectButton.gameObject.SetActive(owed > 0);
            }

            if (collectLabel == null || owed <= 0) return;

            if (owed == 1) collectLabel.Set(UiStrings.TrackCollectOne);
            else collectLabel.Set(UiStrings.TrackCollectMany, owed);
        }

        // ---- the focus card -------------------------------------------------

        // Which level the card is about: whatever the pointer is over, and
        // otherwise the next reward.
        //
        // level+1 RATHER THAN NextRewardLevel, and the difference matters
        // exactly once. NextRewardLevel skips levels that pay nothing, which
        // was the right answer for the summary line when the filler mix had
        // holes in it; it has none now (the five counts sum to 87, which is
        // every filler level), so the two agree -- and when they disagree, the
        // card is pointing at the node the caret is over, which has to be the
        // literal next one.
        private int CardLevel()
        {
            if (_hovered >= RewardTrackLayout.FirstLevel) return _hovered;

            int next = _level + 1;
            if (next > RewardTrack.MaxLevel) next = RewardTrack.MaxLevel;
            if (next < RewardTrackLayout.FirstLevel) next = RewardTrackLayout.FirstLevel;

            return next;
        }

        private void PaintCard(int level)
        {
            var state = RewardTrack.StateOf(level, _level, _claimed);
            bool waiting = RewardTrack.IsWaiting(level, _level, _claimed);
            var entry = RewardTrack.At(level);

            if (cardArt != null)
            {
                // THE CARD TAKES THE NODE'S OWN SPRITE, which is handoff section
                // 8's "reuses the milestone or filler art at the larger size"
                // read literally: the same Sprite object, not a second lookup
                // that could resolve to something else.
                //
                // The tree keys this slot to a placeholder at build time and
                // that is ALL it can do -- which the first capture showed as a
                // card that drew a ring for a stat point, for Favor, and for
                // everything else, because nothing ever changed the sprite. The
                // rail's own marks are already loaded and already bound; this
                // is the seam that was missing rather than a lookup that was
                // wrong.
                int i = level - RewardTrackLayout.FirstLevel;
                if (Has(icons, i)) cardArt.sprite = icons[i].sprite;

                cardArt.color = state == TrackNodeState.ToCome ? Gold : MarkInk;
            }

            if (cardMat != null)
            {
                cardMat.color = state == TrackNodeState.ToCome
                    ? Hex(RewardTrackScreen.DeepViolet)
                    : DiscLit;
            }

            if (cardKicker != null)
            {
                cardKicker.Set(KickerFor(level, state, waiting));
                cardKicker.color = waiting ? TextWaiting : TextQuiet;
            }

            if (cardLevel != null)
            {
                cardLevel.Set(UiStrings.TrackCardLevel, level);
                cardLevel.color = state == TrackNodeState.ToCome ? TextToCome : TextCollected;
            }

            if (cardCaption != null)
            {
                cardCaption.SetContent(RewardTrackNames.Of(entry));
                cardCaption.color = CaptionColour(state);
            }

            if (cardState == null) return;

            if (waiting)
            {
                cardState.Set(UiStrings.TrackStateReady);
                cardState.color = TextWaiting;
                return;
            }

            cardState.color = TextQuiet;

            if (state == TrackNodeState.Collected || state == TrackNodeState.Here)
            {
                cardState.Set(UiStrings.TrackStateCollected);
                return;
            }

            int away = level - _level;
            if (away <= 1) cardState.Set(UiStrings.TrackStateNextLevel);
            else cardState.Set(UiStrings.TrackStateLocked, away);
        }

        private UiString KickerFor(int level, TrackNodeState state, bool waiting)
        {
            if (waiting) return UiStrings.TrackCardWaiting;
            if (state == TrackNodeState.Here) return UiStrings.TrackCardHere;
            if (state == TrackNodeState.Collected) return UiStrings.TrackCardCollected;

            // "NEXT REWARD" only for the node the caret is actually over.
            // Everything else ahead is "STILL AHEAD", because a card that says
            // NEXT while pointing at level 90 is lying about the one thing it
            // exists to say.
            return level == _level + 1 ? UiStrings.TrackCardNext : UiStrings.TrackCardToCome;
        }

        // ---- the ascent ribbon ----------------------------------------------

        private void PaintRibbon()
        {
            if (ribbonFill != null)
            {
                float fraction = _level < RewardTrackLayout.FirstLevel
                    ? 0f
                    : RewardTrackLayout.NodeX(_level) / RewardTrackLayout.ContentWidth;

                // The same left-pivoted WIDTH the rail is driven by. Stated
                // twice because they are two rects, not because the rule is
                // two rules.
                ribbonFill.sizeDelta = new Vector2(
                    fraction * RewardTrackLayout.RibbonWidth, ribbonFill.sizeDelta.y);
            }

            if (ribbonPlayhead != null)
            {
                ribbonPlayhead.anchoredPosition = new Vector2(
                    RewardTrackLayout.RibbonOffsetX(_level), ribbonPlayhead.anchoredPosition.y);
            }

            if (ribbonTicks != null)
            {
                for (int i = 0; i < ribbonTicks.Length; i++)
                {
                    if (ribbonTicks[i] == null) continue;

                    bool waiting = RewardTrack.IsWaiting(
                        RewardTrackLayout.FirstLevel + i, _level, _claimed);

                    if (ribbonTicks[i].activeSelf != waiting) ribbonTicks[i].SetActive(waiting);
                }
            }

            PaintRibbonNumbers();
            MoveRibbonWindow();
        }

        // Filled once per refresh rather than once ever, because there is no
        // "once ever" hook on a panel that is built inactive -- and twelve
        // SetContent calls against unchanged text is not worth a flag to skip.
        private void PaintRibbonNumbers()
        {
            if (ribbonNumbers == null) return;

            int i = 0;
            foreach (int level in RewardTrackLayout.MilestoneLevels())
            {
                if (i >= ribbonNumbers.Length) break;
                if (ribbonNumbers[i] != null) ribbonNumbers[i].SetContent(level.ToString());
                i++;
            }
        }

        private void MoveRibbonWindow()
        {
            if (ribbonWindow == null || content == null || viewport == null) return;

            float width = viewport.rect.width;
            if (width <= 0f) return;

            ribbonWindow.anchoredPosition = new Vector2(
                RewardTrackLayout.RibbonWindowOffsetX(content.anchoredPosition.x, width),
                ribbonWindow.anchoredPosition.y);
        }

        // ---- the two nodes that move ----------------------------------------

        private void MoveCaret()
        {
            if (nextCaret == null) return;

            int next = _level + 1;
            bool visible = next >= RewardTrackLayout.FirstLevel && next <= RewardTrack.MaxLevel;

            if (nextCaret.gameObject.activeSelf != visible) nextCaret.gameObject.SetActive(visible);
            if (!visible) return;

            nextCaret.anchoredPosition = new Vector2(
                RewardTrackLayout.NodeOffsetX(next), RewardTrackLayout.CaretY);
        }

        private void MoveHalo()
        {
            if (hereHaloRect == null) return;

            bool visible = _level >= RewardTrackLayout.FirstLevel && _level <= RewardTrack.MaxLevel;

            if (hereHaloRect.gameObject.activeSelf != visible)
            {
                hereHaloRect.gameObject.SetActive(visible);
            }
            if (!visible) return;

            hereHaloRect.anchoredPosition = new Vector2(RewardTrackLayout.NodeOffsetX(_level), 0f);
        }

        // ---- helpers ---------------------------------------------------------

        private static bool Has(Image[] array, int i) =>
            array != null && i < array.Length && array[i] != null;

        private static Color Faded(Color colour, float fade)
        {
            colour.a *= fade;
            return colour;
        }

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
    }
}
