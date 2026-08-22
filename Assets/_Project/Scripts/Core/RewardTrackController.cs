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
        [SerializeField] internal RectTransform railGlowFill;
        [SerializeField] internal Image shimmer;
        [SerializeField] internal RectTransform shimmerRect;
        [SerializeField] internal Image hereHalo;
        [SerializeField] internal RectTransform hereHaloRect;
        [SerializeField] internal RectTransform nextMark;
        // The claim burst's four rigs and their parts. Sparks are flattened:
        // rig k's spark s is at k * BurstSparkCount + s.
        [SerializeField] internal RectTransform[] burstRoots;
        [SerializeField] internal Image[] burstCores;
        [SerializeField] internal Image[] burstRings;
        [SerializeField] internal Image[] burstRays;
        [SerializeField] internal Image[] burstSparks;

        [SerializeField] internal TMP_Text summaryLevel;
        [SerializeField] internal TMP_Text summaryNextAt;
        [SerializeField] internal TMP_Text summaryReward;
        [SerializeField] internal Button collectButton;
        [SerializeField] internal TMP_Text collectLabel;
        [SerializeField] internal Image collectPip;
        [SerializeField] internal Button closeButton;

        [SerializeField] internal RectTransform cardRect;
        [SerializeField] internal Image cardMat;
        [SerializeField] internal Image cardArt;
        [SerializeField] internal TMP_Text cardKicker;
        [SerializeField] internal TMP_Text cardLevel;
        [SerializeField] internal TMP_Text cardCaption;
        [SerializeField] internal TMP_Text cardState;
        [SerializeField] internal Image cardStateDot;

        // The painted medallion the card shows, one entry per level in rail
        // order. Twelve distinct sprites across ninety-nine entries, because
        // the map is by reward KIND and the index is by level -- which keeps
        // the lookup a subscript rather than a switch the controller has to
        // carry a second copy of.
        [SerializeField] internal Sprite[] cardArtByLevel;

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

        // ONE TICK PER NON-MILESTONE LEVEL, in rail order and nothing else --
        // index i is NOT level i + FirstLevel, because the twelve landmarks are
        // drawn as dots instead and have no tick. TickLevels() is the only
        // thing that knows the mapping.
        // The twelve landmarks' ambient cues, in ascending level order.
        [SerializeField] internal Image[] milestoneAuras;
        [SerializeField] internal Image[] milestoneRings;

        [SerializeField] internal Image[] ribbonTicks;
        [SerializeField] internal Image[] ribbonDots;
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

        // The card's ghost glyph, at the 62% the design draws its placeholder
        // at: a stroke standing in for art is meant to read as a stand-in.
        private static readonly Color CardMark = Hex("#F2DB9E9E");
        private static readonly Color Pale = Hex(RewardTrackScreen.Pale);

        // The card's own text, none of it tinted by state -- see PaintCard.
        private static readonly Color CardName = Hex(RewardTrackScreen.CardName);
        private static readonly Color CardKickerQuiet = Hex(RewardTrackScreen.CardKickerQuiet);
        private static readonly Color RibbonTickToCome = Hex(RewardTrackScreen.RibbonTickToCome);
        private static readonly Color RibbonTickReached = Hex(RewardTrackScreen.RibbonTickReached);
        private static readonly Color RibbonDotToCome = Hex(RewardTrackScreen.RibbonDotToCome);

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

            // A BURST STOPPED MID-FLIGHT LEAVES ITS RIG LIT. StopAllCoroutines
            // kills the thing that would have switched it off, so closing the
            // panel during a collection and reopening it showed a frozen
            // explosion hanging over a node -- the first frame of the next
            // visit, before anything had a chance to repaint.
            if (burstRoots != null)
            {
                foreach (var rig in burstRoots)
                {
                    if (rig != null) rig.gameObject.SetActive(false);
                }
            }

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

                // AND ITS SCALE PUT BACK, because the stamp animation leaves it
                // wherever it stopped. A claim interrupted by the panel closing
                // -- or by a second claim landing on the same node's coroutine
                // -- would otherwise leave a pip frozen at 2.4x forever, and
                // the paint pass is the only thing that runs unconditionally.
                seals[i].transform.localScale = Vector3.one;
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
            float width = _level < RewardTrackLayout.FirstLevel
                ? 0f
                : RewardTrackLayout.NodeX(_level);

            if (railFill != null)
            {
                railFill.sizeDelta = new Vector2(width, railFill.sizeDelta.y);
            }

            // The bloom is the same width as the line it is under, always. Two
            // rects rather than one taller sprite, because the line has to stay
            // a hard 3px edge and the light under it must not.
            if (railGlowFill != null)
            {
                railGlowFill.sizeDelta = new Vector2(width, railGlowFill.sizeDelta.y);
            }
        }

        // THE ROW AS FOUR WRITES, not one. The figure, where the next reward
        // is, and what it is -- each in its own box at its own size, which is
        // the whole reason the row was split up.
        private void PaintSummary()
        {
            if (summaryLevel != null) summaryLevel.SetContent(_level.ToString());

            int next = RewardTrack.NextRewardLevel(_level);
            bool complete = next <= 0;

            // A FINISHED TRACK SAYS SO IN THE REWARD'S SLOT, not in the one
            // that names a level -- "REWARD TRACK COMPLETE" is twenty-one
            // characters and the "NEXT AT 100" box is 130px wide, so putting it
            // there would overflow a box nothing measures. The reward slot is
            // 360 and is already sized for the longest thing the track can
            // name.
            //
            // Blank rather than hidden for whichever of the two is not
            // speaking: an empty label holds its place in the row, and
            // switching a GameObject off to say "nothing more" is a state the
            // row's geometry never sees.
            if (summaryNextAt != null)
            {
                if (complete) summaryNextAt.SetContent(string.Empty);
                else summaryNextAt.Set(UiStrings.TrackNextAt, next);
            }

            if (summaryReward != null)
            {
                if (complete) summaryReward.Set(UiStrings.TrackNextAtComplete);
                else summaryReward.SetContent(RewardTrackNames.Of(RewardTrack.At(next)));
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
                var painted = i >= 0 && cardArtByLevel != null && i < cardArtByLevel.Length
                    ? cardArtByLevel[i]
                    : null;

                if (painted != null)
                {
                    // PAINTED ART IS NOT TINTED. It arrives with its own gold
                    // and its own light; a colour over it is a filter, and the
                    // one thing an art slot exists to stop is the UI deciding
                    // what the art looks like.
                    cardArt.sprite = painted;
                    cardArt.color = Color.white;
                    cardArt.rectTransform.sizeDelta =
                        new Vector2(RewardTrackLayout.CardArtSize, RewardTrackLayout.CardArtSize);
                }
                else
                {
                    // The stroke glyph, at a stroke's size rather than blown up
                    // to fill the slot. Reached for when a reward has no art
                    // mapped or its file is missing -- graceful degradation, as
                    // everywhere else.
                    //
                    // GOLD IN EVERY STATE, which the rail's marks are not. A
                    // mark on the rail is tinted against its disc -- ink on a
                    // lit node, gold on a dark one -- because the disc under it
                    // changes; this one always sits on the same near-black
                    // plate, and the ink it used to switch to on a reached
                    // reward simply vanished there.
                    if (Has(icons, i)) cardArt.sprite = icons[i].sprite;
                    cardArt.color = CardMark;
                    cardArt.rectTransform.sizeDelta =
                        new Vector2(RewardTrackLayout.CardMarkSize, RewardTrackLayout.CardMarkSize);
                }
            }

            // The mat carries the reward kind's hue over the plate, at the same
            // 2E the rail's own slots use. Not state-dependent: the plate is a
            // dark well whatever the state, so there is no gold underneath for
            // a tint to turn into tarnish.
            if (cardMat != null)
            {
                cardMat.color = Hex(RewardTrackLayout.MatTintFor(level));
            }

            if (cardKicker != null)
            {
                cardKicker.Set(KickerFor(level, state, waiting));
                cardKicker.color = waiting ? TextWaiting : CardKickerQuiet;
            }

            // GOLD IN EVERY STATE, like the caption below it.
            //
            // These two were tinted by state and should not be: the card is
            // whatever the rail is pointing at, and dimming its level and its
            // name for a reward the player has not reached yet makes the focus
            // element hardest to read exactly when it is doing its job -- which
            // is telling them about something they do not have. The state is
            // said three times already, by the dot, the kicker and the words
            // along the bottom.
            if (cardLevel != null)
            {
                cardLevel.SetContent(level.ToString());
                cardLevel.color = Gold;
            }

            // The state's dot, which says the same thing the words below it do
            // and says it in a colour: pale for where you are, gold for
            // something owed, dim gold for something had, and the unreached
            // violet for everything else.
            if (cardStateDot != null)
            {
                cardStateDot.color =
                    state == TrackNodeState.Here ? Pale :
                    waiting ? Gold :
                    state == TrackNodeState.Collected ? Hex("#F2DB9E80") :
                    Hex(RewardTrackScreen.DeepViolet);
            }

            if (cardCaption != null)
            {
                cardCaption.SetContent(RewardTrackNames.Of(entry));
                cardCaption.color = CardName;
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

            PaintRibbonTicks();
            PaintRibbonDots();
            PaintRibbonNumbers();
            MoveRibbonWindow();
        }

        // EVERY TICK IS DRAWN, in one of three weights: a pale hairline for a
        // level not yet reached, a gold one for a level had, and twice the size
        // in full gold for one that is waiting.
        //
        // The size is written as well as the colour, and it has to be: a
        // waiting tick is 2x14 against 1x7, which is what makes a run of them
        // read as a comb from across the panel rather than as a slightly
        // brighter stretch of the same line.
        private void PaintRibbonTicks()
        {
            if (ribbonTicks == null) return;

            int i = 0;
            for (int level = RewardTrackLayout.FirstLevel;
                 level <= RewardTrack.MaxLevel && i < ribbonTicks.Length;
                 level++)
            {
                if (RewardTrackLayout.IsMilestone(level)) continue;

                var tick = ribbonTicks[i++];
                if (tick == null) continue;

                bool waiting = RewardTrack.IsWaiting(level, _level, _claimed);

                tick.color = waiting ? Gold
                    : level <= _level ? RibbonTickReached
                    : RibbonTickToCome;

                tick.rectTransform.sizeDelta = waiting
                    ? new Vector2(RewardTrackLayout.RibbonWaitingTickWidth,
                                  RewardTrackLayout.RibbonWaitingTickHeight)
                    : new Vector2(RewardTrackLayout.RibbonTickWidth,
                                  RewardTrackLayout.RibbonTickHeight);
            }
        }

        // The twelve landmarks: filled once reached, hollow until then. The
        // hairline ring around each is emitted gold and never repainted, so a
        // milestone reads as a landmark at every state and as a REACHED one
        // only when the disc inside it lights.
        private void PaintRibbonDots()
        {
            if (ribbonDots == null) return;

            int i = 0;
            foreach (int level in RewardTrackLayout.MilestoneLevels())
            {
                if (i >= ribbonDots.Length) break;

                if (ribbonDots[i] != null)
                {
                    ribbonDots[i].color = level <= _level ? Gold : RibbonDotToCome;
                }
                i++;
            }
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
            if (nextMark == null) return;

            int next = _level + 1;
            bool visible = next >= RewardTrackLayout.FirstLevel && next <= RewardTrack.MaxLevel;

            if (nextMark.gameObject.activeSelf != visible) nextMark.gameObject.SetActive(visible);
            if (!visible) return;

            nextMark.anchoredPosition = new Vector2(
                RewardTrackLayout.NodeOffsetX(next), RewardTrackLayout.NextMarkCentreY);
        }

        // SIZED AS WELL AS MOVED, because a milestone disc is 44 and a filler
        // one 26 -- a halo built for the larger and left there is two thirds
        // wider than the node it is behind on eighty-seven levels out of
        // ninety-nine, which is how a glow becomes a smear.
        private void MoveHalo()
        {
            if (hereHaloRect == null) return;

            bool visible = _level >= RewardTrackLayout.FirstLevel && _level <= RewardTrack.MaxLevel;

            if (hereHaloRect.gameObject.activeSelf != visible)
            {
                hereHaloRect.gameObject.SetActive(visible);
            }
            if (!visible) return;

            float size = RewardTrackLayout.HaloSize(_level);
            hereHaloRect.sizeDelta = new Vector2(size, size);
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
