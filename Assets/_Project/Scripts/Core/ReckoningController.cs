using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
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
        [SerializeField] internal TMP_Text noticeLabel;
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
        [SerializeField] internal TMP_Text[] offerRecipients;
        [SerializeField] internal TMP_Text[] offerDeltas;
        [SerializeField] internal TMP_Text[] offerFeatures;
        [SerializeField] internal TMP_Text[] offerLoses;
        [SerializeField] internal TMP_Text[] offerActions;

        // "Hone instead" under each gear card, the page's Favor line and
        // ledger, and the button that leaves the page (or carries on once
        // nothing is left to choose).
        [SerializeField] internal Button[] honeButtons;
        [SerializeField] internal TMP_Text favorLabel;
        [SerializeField] internal TMP_Text ledgerLabel;
        [SerializeField] internal Button leaveButton;

        [SerializeField] internal Image[] offerHalos;
        [SerializeField] internal Image[] offerBursts;
        [SerializeField] internal Image[] offerIcons;

        [SerializeField] internal RectTransform[] offerBurstRects;

        // The page CARDS' own rects, so a row with fewer cards than the widest
        // page can be re-centred; see OfferRowLayout.
        [SerializeField] internal RectTransform[] offerRects;

        // Item art, as two parallel arrays -- a scene serialises arrays and
        // does not serialise dictionaries. Same shape the overlay uses.
        [SerializeField] internal IconEntry[] icons;

        // Relic art, by relic id: the RELICS tab's rows (RelicListPresenter).
        [SerializeField] internal IconEntry[] relicArt;

        [SerializeField] internal Button[] tabButtons;
        [SerializeField] internal Image[] tabMarkers;
        [SerializeField] internal GameObject[] pages;

        [SerializeField] internal Image dimmer;
        [SerializeField] internal Image frameGlow;

        [SerializeField] internal RectTransform offerPhase;
        [SerializeField] internal RectTransform summaryPhase;

        [SerializeField] internal GameObject relicEmptyHint;
        [SerializeField] internal GameObject[] relicRows;
        [SerializeField] internal Image[] relicIcons;
        [SerializeField] internal TMP_Text[] relicNames;
        [SerializeField] internal TMP_Text[] relicMetas;
        [SerializeField] internal TMP_Text[] relicBodies;
        [SerializeField] internal TMP_Text relicPageLabel;
        [SerializeField] internal Button relicPrevPageButton;
        [SerializeField] internal Button relicNextPageButton;

        [SerializeField] internal GameObject[] tallyRows;
        [SerializeField] internal TMP_Text[] tallyNames;
        [SerializeField] internal TMP_Text[] tallyStats;
        [SerializeField] internal TMP_Text[] tallyKills;

        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal GameObject expHeading;

        // The "Gained" chips (RelicChipStrip) and their tooltip.
        [SerializeField] internal GameObject gainedStrip;
        [SerializeField] internal RectTransform gainedCaption;
        [SerializeField] internal Button[] gainedChips;
        [SerializeField] internal Image[] gainedChipIcons;
        [SerializeField] internal TMP_Text[] gainedChipNames;
        [SerializeField] internal GameObject gainedTooltip;
        [SerializeField] internal TMP_Text gainedTooltipName;
        [SerializeField] internal TMP_Text gainedTooltipMeta;
        [SerializeField] internal TMP_Text gainedTooltipBody;

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

        // THE PHASE CHANGE ADVANCES: the summary comes IN FROM THE RIGHT and
        // the choice leaves to the left, both travelling the same way.
        //
        // Rightward is the handedness every UI here uses for going BACK, and
        // taking a reward is going forward, so this direction reads as
        // advancing rather than as a cancel.
        //
        // FAST, and that is half of the fix. At 0.3s on a smoothstep this
        // eased in AND out, so it left slowly, arrived slowly, and spent its
        // middle at a speed too low to read as a move at all.
        //
        // 0.13s on a quartic was the first answer and it overshot: quartic
        // covers two thirds of the distance in the first quarter of the time,
        // so at 130ms the whole crossing was over before the eye caught it
        // moving and it read as a cut rather than a shove. 0.18 on a cubic is
        // still well under the original and still leaves at full speed; it just
        // gives the motion enough frames to be seen as motion.
        private const float SweepSeconds = 0.18f;

        // Motion blur, such as a uGUI canvas can have one.
        //
        // NOT localScale on the frame, which this file already records as the
        // mistake that made the reveal read cheap -- that was a 0.34s squash of
        // static content, and every child compressed and sprang back. This is a
        // slight horizontal stretch on content that is crossing the panel at
        // speed, which is the smear a fast pan leaves.
        //
        // BOTH OF THESE WERE TOO STRONG at first: 6% of stretch and a 45% drop
        // in alpha, over 130ms. A blur is meant to be the thing you do not
        // quite see; at that strength the whole panel visibly thinned and
        // swelled mid-crossing, which is a flash rather than a smear and was
        // the twitchy half of "fast and twitchy". Enough to soften an edge in
        // motion, not enough to notice as an effect in its own right.
        private const float SweepSmear = 0.035f;
        private const float SweepAlphaDip = 0.15f;

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
        private RelicListPresenter _relics;
        private bool _choosing;

        // Degrees per second. Slow: this is a glow behind an object, and a
        // burst spinning fast enough to notice as MOTION reads as a loading
        // spinner rather than as something precious sitting on a plinth.
        private const float BurstDegreesPerSecond = 9f;

        private CombatReward _reward;
        private ReckoningRequest _request = ReckoningRequest.Full(null, null);
        private RelicChipStrip _gained;

        // The run's pending pages, by reference: every choice resolves them in
        // place, so opening the screen again resumes where it was left.
        private IReadOnlyList<PendingPage> _pages = new List<PendingPage>();

        // The page on screen, or null once every page is spent.
        private PendingPage _page;
        private bool _wired;
        private Coroutine _animation;

        // THE TEST SEAM, same shape as FightBeatPlayer.BeatSpeedMultiplier and
        // FightController.BreathSpeedMultiplier -- this driver has neither of
        // those, being entirely its own thing off Time.unscaledDeltaTime (the
        // wipe/gloom in PlayIn, the sweep and smear in SweepToSummary, the bar
        // fill and stagger delay in Sweep/FillBar, the level-up flash in
        // Flash), so it needed its own. 1 outside a test, which changes
        // nothing about play. ReckoningTests, ReckoningPhaseTests and
        // ReckoningTabTests set/reset it in [SetUp]/[TearDown].
        public static float SpeedMultiplier = 1f;

        // Raised when the player dismisses it. An event rather than a call into
        // Navigation, for the same reason FightController.FightEnded is one:
        // the reward screen has no business knowing what comes after a fight.
        public System.Action Dismissed;

        // WHO OWNS THE GAINED-CHIP TOOLTIP -- the pointer or the selection
        // (Core/TooltipFocusRouter, docs/GAMEPAD_NAVIGATION_PLAN.md section 7).
        private readonly TooltipFocusRouter _tooltips = new TooltipFocusRouter();

        // Pushed in Show() once Wire()/Paint() have settled which cards are
        // on screen, reconfigured when the choice resolves into the summary,
        // and removed on OnDisable -- Defeat's own PushNavContext header has
        // the argument for OnDisable rather than a button handler, and it
        // applies here for the same reason: Continue ends in a scene change.
        private NavContext _navContext;

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

            continueButton.onClick.AddListener(Dismiss);

            Relics.WirePager();

            // Pressing a chip is "done reading": focus goes to Continue.
            Gained.Pressed = () =>
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
            Gained.Wire(_tooltips);

            for (int i = 0; honeButtons != null && i < honeButtons.Length; i++)
            {
                int index = i;
                honeButtons[i].onClick.AddListener(() => HoneInstead(index));
            }

            if (leaveButton != null) leaveButton.onClick.AddListener(LeaveOrAdvance);

            for (int i = 0; i < offerButtons.Length; i++)
            {
                int index = i;
                offerButtons[i].onClick.AddListener(() => Take(index));
            }
        }

        // ---- navigation (docs/GAMEPAD_NAVIGATION_PLAN.md) ------------------------
        //
        // TWO STATES, one context. The choice phase is a Rail of the open
        // cards, a Rail of their "hone instead" buttons beneath, and the page's
        // way off; the summary it sweeps to is the page tabs plus Continue.
        // Reconfigured rather than popped and re-pushed, which is
        // NavContext.Reconfigure's own documented case -- the phase change is
        // the same screen showing something else, not a modal over it.
        //
        // SUBMIT NEEDS NO CODE: a card is a real Button, so its OnSubmit fires
        // the onClick that already calls Take(index) -- one path for the mouse
        // and the pad, and RewardPages.CanTake is what makes each card
        // once-only whichever input arrives.
        private void RefreshNavigation()
        {
            var groups = new List<UiNavGroup<Selectable>>();
            var links = new List<UiNavLink<Selectable>?>();
            var selectables = new Dictionary<string, object>();
            object entry = null;

            if (_choosing)
            {
                // Only the cards still open: a spent card stays on screen as
                // the record of what became of it, but answers nothing.
                var cards = new List<Selectable>();
                var hones = new List<Selectable>();
                var openIndexes = new List<int>();
                for (int i = 0; _page != null && i < offerButtons.Length && i < _page.Cards.Count; i++)
                {
                    if (offerButtons[i] == null || !offerButtons[i].interactable) continue;

                    cards.Add(offerButtons[i]);
                    openIndexes.Add(i);
                    if (HoneShown(i)) hones.Add(honeButtons[i]);
                }

                var leave = leaveButton;
                if (cards.Count > 0) groups.Add(RuntimeNavWiring.Group("reckoningOffers", UiNavGroupKind.Rail, cards));
                if (hones.Count > 0) groups.Add(RuntimeNavWiring.Group("reckoningHones", UiNavGroupKind.Rail, hones));

                // Down from a card is its own hone button, else the way off the
                // page; Up from the way off is the first hone, else the first
                // card.
                foreach (int i in openIndexes)
                {
                    links.Add(RuntimeNavWiring.Link(offerButtons[i], UiNavDirection.Down,
                        HoneShown(i) ? (Selectable)honeButtons[i] : leave));
                    if (HoneShown(i))
                    {
                        links.Add(RuntimeNavWiring.Link(honeButtons[i], UiNavDirection.Up, offerButtons[i]));
                        links.Add(RuntimeNavWiring.Link(honeButtons[i], UiNavDirection.Down, leave));
                    }
                }

                if (leave != null)
                {
                    links.Add(RuntimeNavWiring.Link(leave, UiNavDirection.Up, First(hones) ?? First(cards)));
                }

                // THE GAMEOBJECT, never the Button. NavContext reads its
                // entry back through `as GameObject` (it holds an opaque
                // handle, being engine-free), so a Selectable put here
                // resolves to null and the dispatcher's own reselection rule
                // then clears the selection every single frame.
                entry = (First(cards) ?? leave)?.gameObject;
                Declare(selectables, cards);
                Declare(selectables, hones);
                if (leave != null) selectables[leave.name] = leave.gameObject;
            }
            else
            {
                var tabs = new List<Selectable>();
                for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
                {
                    if (tabButtons[i] != null && TabShown(i)) tabs.Add(tabButtons[i]);
                }

                groups.Add(RuntimeNavWiring.Group("reckoningTabs", UiNavGroupKind.Rail, tabs));

                // Continue is the summary's own primary action and its entry:
                // the tabs are there to be read, and a player who wants them
                // presses Up. (Not a List with Continue as its last member --
                // the tabs are a horizontal strip, so Left/Right has to mean
                // "another page".)
                //
                // THE RELIC PAGER SITS BETWEEN THEM when it is up: it is only
                // on screen on the relics tab with more than one page, and a
                // pad player has to be able to reach it or the later pages are
                // mouse-only.
                //
                // THE CHIP RAIL sits between the pager and Continue, matching
                // where it is drawn (docs/PLAN_BUGS_QA.md C2, the
                // Reckoning navigation graph): each element's Down is the next
                // one present, Continue's Up the nearest one present.
                var pager = RelicPagerShown()
                    ? new List<Selectable> { relicPrevPageButton, relicNextPageButton }
                    : new List<Selectable>();
                var chips = Gained.Selectables;
                var currentTab = tabButtons != null && _tab < tabButtons.Length && TabShown(_tab)
                    ? tabButtons[_tab]
                    : First(tabs);
                var belowPager = First(chips) ?? continueButton;
                var belowTabs = First(pager) ?? belowPager;

                foreach (var tab in tabs)
                {
                    links.Add(RuntimeNavWiring.Link(tab, UiNavDirection.Down, belowTabs));
                }

                if (pager.Count > 0)
                {
                    groups.Add(RuntimeNavWiring.Group("reckoningRelicPager", UiNavGroupKind.Rail, pager));
                    foreach (var arrow in pager)
                    {
                        links.Add(RuntimeNavWiring.Link(arrow, UiNavDirection.Up, currentTab));
                        links.Add(RuntimeNavWiring.Link(arrow, UiNavDirection.Down, belowPager));
                    }

                    Declare(selectables, pager);
                }

                if (chips.Count > 0)
                {
                    groups.Add(RuntimeNavWiring.Group("reckoningGained", UiNavGroupKind.Rail, chips,
                        wrap: UiNavWrap.Clamp));
                    var aboveChips = First(pager) ?? currentTab;
                    foreach (var chip in chips)
                    {
                        links.Add(RuntimeNavWiring.Link(chip, UiNavDirection.Up, aboveChips));
                        links.Add(RuntimeNavWiring.Link(chip, UiNavDirection.Down, continueButton));
                    }

                    Declare(selectables, chips);
                }

                links.Add(RuntimeNavWiring.Link(continueButton, UiNavDirection.Up,
                    First(chips) ?? First(pager) ?? currentTab));

                entry = continueButton == null ? null : continueButton.gameObject;
                Declare(selectables, tabs);
                if (continueButton != null) selectables[continueButton.name] = continueButton.gameObject;
            }

            RuntimeNavWiring.Apply(groups, links);

            if (_navContext != null)
            {
                _navContext.Reconfigure(entry, selectables);
                return;
            }

            _navContext = new NavContext(entry, selectables, cancel: HandleCancel, cancelLive: () => !_choosing);
            NavigationInputModule.Contexts?.Push(_navContext);
        }

        // CANCEL IS A NO-OP WHILE THERE ARE PAGES TO RESOLVE, and that is the
        // mouse path's own rule rather than a gamepad restriction: leaving a
        // page pays its unused choices, so it is a deliberate press on the
        // page's own button, never a back press. Once the pages are spent,
        // the summary DOES have a way out, and Cancel takes it -- the same
        // action Continue's click raises, so the pad is not offered less than
        // the mouse.
        private void HandleCancel()
        {
            if (_choosing) return;

            Dismiss();
        }

        // THE ONE WAY OUT, for Continue and Cancel alike. Leaving acknowledges
        // exactly the relic receipts the chips showed (C2), in their own write,
        // since nothing else moves on dismissal.
        private void Dismiss()
        {
            RunOrchestrator.AcknowledgeRelicReceipts(Gained.Displayed.ToList());
            Dismissed?.Invoke();
        }

        private static Selectable First(List<Selectable> members) => members.Count > 0 ? members[0] : null;

        private static void Declare(Dictionary<string, object> selectables, List<Selectable> members)
        {
            foreach (var member in members) selectables[member.name] = member.gameObject;
        }

        private void OnDisable()
        {
            _tooltips.Detach();

            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        // A paid fight with no relic receipts to show: the full form.
        public void Show(CombatReward reward, IReadOnlyList<PendingPage> pages) =>
            Show(ReckoningRequest.Full(reward, pages));

        public void Show(ReckoningRequest request)
        {
            _request = request ?? ReckoningRequest.Full(null, null);
            _reward = _request.Reward;
            _pages = _request.Pages;

            // The first page with choices left, so a screen opened again over
            // the same pages resumes rather than starting over.
            _page = _pages.FirstOrDefault(p => p != null && p.ChoicesLeft > 0);

            gameObject.SetActive(true);
            Wire();
            Paint();

            if (_animation != null) StopCoroutine(_animation);

            // Only animate when the object is actually live in the hierarchy.
            // StartCoroutine on an inactive object throws, and a headless test
            // that never activates the canvas should still get correct numbers.
            // OPENS ON THE FIRST PAGE WITH CHOICES LEFT. The only way past a
            // page is to spend its choices or leave it (which pays them), so
            // there is no Continue on this phase.
            //
            // A fight that paid no page goes straight to the summary rather
            // than showing an empty choice with no way out.
            bool choosing = _page != null;

            offerPhase.gameObject.SetActive(choosing);
            offerPhase.anchoredPosition = Vector2.zero;
            summaryPhase.gameObject.SetActive(!choosing);
            summaryPhase.anchoredPosition = Vector2.zero;
            _choosing = choosing;

            // THE OUTCOME FORM HAS NO SPOILS TAB: nothing was paid, so the
            // page would be empty. It is hidden, the shown tabs close up, and
            // the screen opens on the tally, which is what the fight left.
            LayOutTabs();
            _tab = _request.IsOutcomeForm ? TallyTab : SpoilsTab;
            Relics.Model.SetPage(0);
            PaintTabs();
            PaintRelics();
            PaintTally();
            Gained.Show(_request.Receipts);

            // AFTER Paint/PaintOffers, never on OnEnable: which cards exist
            // is decided by what was just handed in, and a context pushed
            // before that would declare Selectables that are not on screen
            // (plan section 2/4, the same rule SystemMenuController's own
            // PushNavContext states).
            _tooltips.Attach(NavigationInputModule.Contexts);
            RefreshNavigation();

            // Selected NOW rather than left to the dispatcher's next-frame
            // reselection rule, so the screen never draws a frame with the
            // choice up and nothing focused.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(
                NavigationInputModule.SelectionFor(_navContext));

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
            // THE OUTCOME FORM pays nothing, so nothing that reports a payout
            // shows, and the title names the ending instead (C3).
            bool outcome = _request.IsOutcomeForm;
            if (titleLabel != null) titleLabel.Set(TitleFor(_request));
            goldLabel.gameObject.SetShown(!outcome);
            if (expHeading != null) expHeading.SetShown(!outcome);
            goldLabel.Set(UiStrings.ReckoningGold, _reward?.GoldGained ?? 0);

            // A caravan fake that fell apart in this fight, and the merchant's
            // "No refunds." (the settlement's notices). Hidden on every fight
            // that has nothing to say, which is nearly all of them.
            if (noticeLabel != null)
            {
                var notices = _request.Notices;
                bool any = notices != null && notices.Count > 0;
                noticeLabel.gameObject.SetShown(any);
                if (any) noticeLabel.SetContent(string.Join("\n", notices));
            }

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

        // Positions and sizes the page's row for the number of cards actually
        // being shown.
        //
        // The tree is emitted at OfferRowLayout.MaxCards cards dividing the
        // painted interior, because a screen is built once for every save. A
        // page of k + 2 cards gets the same interior split that many ways and
        // so comes out wider, not narrower.
        //
        // Both widths come from OfferRowLayout, so the row the audit solved and
        // the row the game draws cannot disagree about the arithmetic -- only
        // about the count, which is the one thing that genuinely varies.
        private void LayOutOfferRow(int count)
        {
            if (count <= 0) return;

            float cardWidth = OfferRowLayout.CardWidth(count);
            float labelWidth = OfferRowLayout.LabelWidth(count);

            for (int i = 0; i < count; i++)
            {
                if (offerRects != null && i < offerRects.Length && offerRects[i] != null)
                {
                    var rect = offerRects[i];

                    var position = rect.anchoredPosition;
                    position.x = OfferRowLayout.CardX(i, count);
                    rect.anchoredPosition = position;

                    var size = rect.sizeDelta;
                    size.x = cardWidth;
                    rect.sizeDelta = size;
                }

                // "Hone instead" keeps its size, because a plate is a 3:1 art
                // and stretching it with the row would bend it; only its x
                // follows the card.
                var hone = honeButtons != null && i < honeButtons.Length && honeButtons[i] != null
                    ? honeButtons[i].transform as RectTransform
                    : null;
                if (hone != null)
                {
                    var position = hone.anchoredPosition;
                    position.x = OfferRowLayout.CardX(i, count);
                    hone.anchoredPosition = position;
                }

                SetWidth(RectAt(offerNames, i), labelWidth);
                SetWidth(RectAt(offerMetas, i), labelWidth);
                SetWidth(RectAt(offerRecipients, i), labelWidth);
                SetWidth(RectAt(offerDeltas, i), labelWidth);
                SetWidth(RectAt(offerFeatures, i), labelWidth);
                SetWidth(RectAt(offerLoses, i), labelWidth);
                SetWidth(RectAt(offerActions, i), labelWidth);

                // THE ART TOO, which the labels' own comment below explains the
                // general case of. The icon box is emitted at the widest row's
                // width because that is the row the audit solves; leaving it
                // there would mean a three-card row drawing its items smaller
                // than the card can hold, for no reason but the audit's
                // convenience.
                SetWidth(RectAt(offerIcons, i), OfferRowLayout.IconWidth(count));
                SetSquare(RectAt(offerHalos, i), OfferRowLayout.HaloDiameter(count));
                SetSquare(RectAt(offerBursts, i), OfferRowLayout.BurstDiameter(count));
            }
        }

        // ---- resizing one card's parts -----------------------------------------
        //
        // THREE METHODS THAT WERE ONE METHOD. Icon, glow and label each had
        // their own copy of the same bounds check and the same sizeDelta
        // write, differing only in which array they indexed -- so a fix to the
        // guard was three edits, and the fourth part somebody adds tomorrow
        // would be a fourth copy.
        //
        // Split by what actually differs instead: WHICH rect and WHAT is
        // written to it.
        //
        // ONE overload, not two. Image and TMP_Text both descend from Graphic,
        // which is where rectTransform is declared -- so an Image[] and a
        // TMP_Text[] both pass as Graphic[] through array covariance and the
        // second copy of this guard was never needed. (The first draft split it
        // in two on the belief that the types were unrelated. They are
        // siblings.)
        private static RectTransform RectAt(Graphic[] items, int index) =>
            items == null || index < 0 || index >= items.Length || items[index] == null
                ? null
                : items[index].rectTransform;

        // Width only. The icon's HEIGHT is the band ReckoningScreen fixed, and
        // ItemIcons.Apply fits the art inside whatever box it is given, so
        // widening the box is the whole of "make the item bigger". The labels
        // have to follow the card for a different reason -- a long item name in
        // a narrow row runs under the card next door, and they are decor, so
        // nothing else would catch it.
        private static void SetWidth(RectTransform rect, float width)
        {
            if (rect == null) return;

            var size = rect.sizeDelta;
            size.x = width;
            rect.sizeDelta = size;
        }

        // Both axes, because a glow is a disc and stretching one reads as a
        // smear rather than as light.
        private static void SetSquare(RectTransform rect, float diameter)
        {
            if (rect != null) rect.sizeDelta = new Vector2(diameter, diameter);
        }

        private void PaintOffers()
        {
            bool any = _page != null && _page.Cards.Count > 0;
            if (lootHeading != null) lootHeading.SetActive(any);
            if (favorLabel != null) favorLabel.gameObject.SetShown(any && _page.Kind == RewardPageKind.Gear);
            if (ledgerLabel != null) ledgerLabel.gameObject.SetShown(any);
            if (leaveButton != null) leaveButton.gameObject.SetShown(any);

            int count = any ? Mathf.Min(_page.Cards.Count, offerButtons.Length) : 0;

            // RE-CENTRE FIRST, before anything reads a position.
            //
            // The row is emitted at OfferRowLayout.MaxCards wide because a
            // screen is built once for every save. A page of k + 2 cards left
            // at those positions would sit off-axis with a gap where the
            // missing ones belong -- which is what a hidden card looks like if
            // nobody moves the rest.
            LayOutOfferRow(count);

            var save = SaveSlotManager.CurrentSave;
            for (int i = 0; i < offerButtons.Length; i++)
            {
                bool present = i < count;
                offerButtons[i].gameObject.SetActive(present);
                if (honeButtons != null && i < honeButtons.Length && honeButtons[i] != null)
                {
                    honeButtons[i].gameObject.SetActive(present && HoneShown(i));
                }

                if (!present) continue;

                var card = _page.Cards[i];
                bool open = card.Outcome == CardOutcome.Open && _page.ChoicesLeft > 0;
                var face = card.IsGear ? RewardCardFace.OfGear(save, card) : RewardCardFace.OfRelic(card);

                // Graceful degradation: a card naming content that no longer
                // exists shows what it can rather than taking the screen down.
                SetText(offerNames, i, face.Name);
                SetText(offerMetas, i, face.Meta);
                SetText(offerRecipients, i, face.Recipient);
                SetText(offerDeltas, i, face.Delta);
                SetText(offerFeatures, i, face.Feature);
                SetText(offerLoses, i, face.Loses);
                SeatFeature(i, relic: !card.IsGear);

                // The frame colour is the QUALITY, and it is the burst and the
                // halo that wear it, from the same table the name and the
                // dossier's cell edges read. Held well under full alpha: it is
                // a glow behind an object, and at full strength it competes
                // with the object it exists to frame.
                var glow = HexColour(face.Hex);
                if (offerNames[i] != null) offerNames[i].color = glow;

                if (card.IsGear)
                {
                    ItemIcons.Apply(offerIcons[i], icons, ItemIcons.KeyOf(card.Copy));
                }
                else
                {
                    ItemIcons.Apply(offerIcons[i], relicArt, card.RelicId);
                }

                glow.a = open ? 0.42f : 0.18f;
                offerBursts[i].color = glow;

                // The halo carries the SAME colour at lower alpha. It is the
                // part that survives being stood on: the burst's rays clear the
                // icon, but its core is hidden behind the item, so on its own
                // the quality read as a few spikes rather than as a colour.
                //
                // ONE MEANING: this halo says only "this is Rare" (or whatever
                // quality), never also "this is the card you are on" -- focus
                // is the arrow's job (Core/FocusMarker.cs).
                if (offerHalos != null && i < offerHalos.Length && offerHalos[i] != null)
                {
                    var soft = glow;
                    soft.a = open ? 0.30f : 0.12f;
                    offerHalos[i].color = soft;
                    offerHalos[i].rectTransform.localScale = Vector3.one;
                }

                PaintAction(i, card, open);
                PaintHone(i, card);

                // Every card stays visible once the page is spent, and none of
                // them respond. Hiding the ones not chosen would erase the
                // decision the player just made from the screen that asked for
                // it.
                offerButtons[i].interactable = open;
            }

            PaintPageChrome();
        }

        // The caption at the foot of a card: what it will do, then what became
        // of it.
        private void PaintAction(int i, PendingCard card, bool open)
        {
            if (i >= offerActions.Length || offerActions[i] == null) return;

            switch (card.Outcome)
            {
                case CardOutcome.Taken: offerActions[i].Set(UiStrings.ReckoningTaken); break;
                case CardOutcome.Honed: offerActions[i].Set(UiStrings.ReckoningHoned); break;
                case CardOutcome.Paid: offerActions[i].Set(UiStrings.ReckoningPaid, card.GoldPaid); break;

                // An unspent card on a spent page was never taken or honed: its
                // caption is blank rather than a lie.
                default:
                    if (open) offerActions[i].Set(UiStrings.ReckoningTake);
                    else offerActions[i].SetContent("");
                    break;
            }
        }

        private void PaintHone(int i, PendingCard card)
        {
            if (!HoneShown(i)) return;

            var label = honeButtons[i].GetComponentInChildren<TMP_Text>(includeInactive: true);
            var save = SaveSlotManager.CurrentSave;
            if (RewardPages.HoneInsteadFor(save, _page, i) == RewardPages.HoneInsteadResult.Gold)
            {
                label.Set(UiStrings.ReckoningSellInstead, RewardPages.SalePrice(card.Copy, _page.Step));
            }
            else
            {
                label.Set(UiStrings.ReckoningHoneInstead);
            }
        }

        // Whether card `i` offers "hone instead": a gear page's open card whose
        // slot can still be honed, or the one case where every slot is spent
        // and the choice pays the card's price (RewardPages.HoneInsteadFor).
        private bool HoneShown(int i) =>
            _page != null && honeButtons != null && i < honeButtons.Length && honeButtons[i] != null
            && i < _page.Cards.Count && _page.Kind == RewardPageKind.Gear
            && RewardPages.HoneInsteadFor(SaveSlotManager.CurrentSave, _page, i) != RewardPages.HoneInsteadResult.Refused;

        // The heading, the Favor line, the ledger and the page's button.
        private void PaintPageChrome()
        {
            if (_page == null) return;

            var heading = lootHeading == null ? null : lootHeading.GetComponent<TMP_Text>();
            var pageName = PageName(_page);
            if (heading != null)
            {
                string name = pageName.Format();
                if (_page.ChoicesLeft == 0) heading.Set(UiStrings.ReckoningPageDone, name);
                else if (_page.ChoicesLeft == 1) heading.Set(UiStrings.ReckoningChoiceLeft, name);
                else heading.Set(UiStrings.ReckoningChoicesLeft, name, _page.ChoicesLeft);
            }

            if (favorLabel != null && _page.Kind == RewardPageKind.Gear)
            {
                int favor = ItemOfferRoll.CurrentSquadFavor();
                GearPage.FavorEffect(ContentDatabase.GetRewardSource(_page.SourceId), favor,
                    out int tierPoints, out int qualityPoints);
                favorLabel.Set(UiStrings.ReckoningFavor, favor, tierPoints, qualityPoints);
            }

            if (ledgerLabel != null) ledgerLabel.SetContent(LedgerText(_page));

            var leaveLabel = leaveButton == null ? null : leaveButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (leaveLabel != null)
            {
                leaveLabel.Set(_page.ChoicesLeft > 0 ? UiStrings.ReckoningLeavePage : UiStrings.Continue);
            }
        }

        private static UiString PageName(PendingPage page)
        {
            bool gear = page.Kind == RewardPageKind.Gear;
            switch (page.SourceClass)
            {
                case EncounterClass.Boss: return gear ? UiStrings.ReckoningPageGearBoss : UiStrings.ReckoningPageRelicBoss;
                case EncounterClass.Elite: return gear ? UiStrings.ReckoningPageGearAnchor : UiStrings.ReckoningPageRelicAnchor;
                default: return gear ? UiStrings.ReckoningPageGear : UiStrings.ReckoningPageRelic;
            }
        }

        // What the page did, in one paragraph: the copies it displaced into the
        // bag, the slots it honed, and what leaving paid for the choices left
        // unused.
        private static string LedgerText(PendingPage page)
        {
            var parts = new List<string>();

            var displaced = page.Cards
                .Where(c => c.IsGear && c.Outcome == CardOutcome.Taken && c.Displaced != null)
                .Select(c => RarityColors.NameOf(c.Displaced))
                .ToList();
            if (displaced.Count > 0) parts.Add(UiStrings.ReckoningLedgerDisplaced.Format(string.Join(", ", displaced)));

            var honed = page.Cards
                .Where(c => c.IsGear && c.Outcome == CardOutcome.Honed)
                .Select(c => RewardApplier.DisplayNameFor(c.RecipientId) + " " + UiStrings.EquipmentSlotName(c.Slot).Format())
                .ToList();
            if (honed.Count > 0) parts.Add(UiStrings.ReckoningLedgerHoned.Format(string.Join(", ", honed)));

            if (page.Closed && (page.UnusedHones > 0 || page.UnusedGold > 0))
            {
                var left = new List<string>();
                if (page.UnusedHones > 0) left.Add(UiStrings.ReckoningLeftHones.Format(page.UnusedHones));
                if (page.UnusedGold > 0) left.Add(UiStrings.ReckoningLeftGold.Format(page.UnusedGold));
                parts.Add(UiStrings.ReckoningLedgerLeft.Format(string.Join(", ", left)));
            }

            return string.Join("  -  ", parts);
        }

        private static void SetText(TMP_Text[] labels, int index, string value)
        {
            if (labels != null && index < labels.Length && labels[index] != null) labels[index].SetContent(value);
        }

        private static Color HexColour(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.white;

        // A relic's description takes the recipient and stat bands as well; a
        // gear card's feature line sits in its own band.
        private void SeatFeature(int i, bool relic)
        {
            var rect = RectAt(offerFeatures, i);
            if (rect == null) return;

            var position = rect.anchoredPosition;
            position.y = relic ? ReckoningScreen.RelicFeatureCentre : ReckoningScreen.FeatureCentre;
            rect.anchoredPosition = position;

            var size = rect.sizeDelta;
            size.y = relic ? ReckoningScreen.RelicFeatureHeight : ReckoningScreen.FeatureHeight;
            rect.sizeDelta = size;
        }

        // ---- the decisions ------------------------------------------------------------

        // A gear card equips on its recipient, a relic joins the run. The page
        // stays on screen until the player moves on, so the displaced copy and
        // the spent cards are readable.
        private void Take(int index)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || !RewardPages.CanTake(_page, index)) return;

            var card = _page.Cards[index];
            if (!RewardPages.Take(save, _page, index)) return;

            if (_reward != null && card.IsGear && string.IsNullOrEmpty(_reward.ChosenItemId))
            {
                _reward.ChosenItemId = card.Copy.ItemId;
                _reward.ChosenItemName = RarityColors.NameOf(card.Copy);
            }

            AfterChoice();
        }

        private void HoneInstead(int index)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || !RewardPages.CanTake(_page, index)) return;
            if (RewardPages.HoneInstead(save, _page, index) == RewardPages.HoneInsteadResult.Refused) return;

            AfterChoice();
        }

        // Leaving pays every unused choice and closes the page, which then
        // shows what it paid; once nothing is left to choose the same button
        // moves on to the next page, or to the summary after the last.
        private void LeaveOrAdvance()
        {
            if (_page == null) return;

            if (_page.ChoicesLeft > 0)
            {
                RewardPages.Leave(SaveSlotManager.CurrentSave, _page);
                AfterChoice();
                return;
            }

            _page = _pages.FirstOrDefault(p => p != null && p.ChoicesLeft > 0);
            if (_page != null)
            {
                PaintOffers();
                RefreshNavigation();
                SelectEntry();
                return;
            }

            // The last page is spent: the sweep to the summary is the
            // transition, as it always was.
            _choosing = false;
            PaintOffers();

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

            RefreshNavigation();
        }

        // The save is written once per player action, here, since neither
        // RewardPages nor the screen's other paths persist.
        private void AfterChoice()
        {
            SaveSlotManager.SaveCurrent();
            PaintOffers();
            RefreshNavigation();

            // Reselect NOW: the card the stick stood on no longer answers, and
            // the dispatcher's next-frame rule would leave a frame unfocused.
            SelectEntry();
        }

        private void SelectEntry() =>
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(
                NavigationInputModule.SelectionFor(_navContext));

        public bool HasTakenAnItem =>
            _pages.Any(p => p != null && p.Cards.Any(c => c.IsGear && c.Outcome == CardOutcome.Taken));

        // The page on screen, for a test or a capture.
        public PendingPage CurrentPage => _page;

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

        // The tabs' order on the tree (ReckoningScreen.TabStrings).
        private const int SpoilsTab = 0;
        private const int TallyTab = 2;

        private bool TabShown(int index) => !(_request.IsOutcomeForm && index == SpoilsTab);

        // Where the tree put each tab, read once, so a full form after an
        // outcome form gets the strip back exactly as built.
        private float[] _tabHomeX;

        // The shown tabs, spread over the same centre and pitch the tree uses
        // for all of them, so hiding one never leaves a gap in the strip.
        private void LayOutTabs()
        {
            if (tabButtons == null) return;

            if (_tabHomeX == null)
            {
                _tabHomeX = new float[tabButtons.Length];
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    var rect = tabButtons[i] == null ? null : tabButtons[i].transform as RectTransform;
                    if (rect != null) _tabHomeX[i] = rect.anchoredPosition.x;
                }
            }

            var shown = new List<int>();
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                tabButtons[i].gameObject.SetActive(TabShown(i));
                if (TabShown(i)) shown.Add(i);
            }

            float pitch = tabButtons.Length > 1 ? _tabHomeX[1] - _tabHomeX[0] : 0f;
            for (int k = 0; k < shown.Count; k++)
            {
                var rect = (RectTransform)tabButtons[shown[k]].transform;
                float x = (k - (shown.Count - 1) * 0.5f) * pitch;
                if (shown.Count == tabButtons.Length) x = _tabHomeX[shown[k]];
                rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            }
        }

        private void SelectTab(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length || !TabShown(index)) return;

            if (index != _tab) SoundController.Play(TabSwipe.Next());
            _tab = index;
            PaintTabs();

            // The pager joins or leaves the pad's graph with the relics tab.
            if (!_choosing && _navContext != null) RefreshNavigation();
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

        // The one relic presenter, over this screen's own widgets.
        private RelicListPresenter Relics =>
            _relics ?? (_relics = new RelicListPresenter(new RelicListWidgets(
                relicRows, relicIcons, relicNames, relicMetas, relicBodies,
                relicPrevPageButton, relicNextPageButton, relicPageLabel, relicEmptyHint, relicArt)));

        private void PaintRelics()
        {
            // The RUN'S relics, read straight off the snapshot. The Reckoning
            // is the only screen a player sees between fights, so it is where
            // "what am I actually carrying" belongs. PAGED, never truncated:
            // the shop sells relics with no cap.
            var held = RunManager.HasRun
                ? RunManager.Run.relicIds ?? new List<string>()
                : new List<string>();

            Relics.Show(held);
        }

        private bool RelicPagerShown() =>
            relicPrevPageButton != null && relicNextPageButton != null
            && pages != null && _tab < pages.Length && pages[_tab] != null
            && relicPrevPageButton.transform.IsChildOf(pages[_tab].transform)
            && relicPrevPageButton.gameObject.activeSelf;

        // ---- what everyone did ----------------------------------------------------------

        private static UiString TitleFor(ReckoningRequest request)
        {
            if (!request.IsOutcomeForm) return UiStrings.ReckoningTitle;

            switch (request.Ending)
            {
                case FightEnding.Endured: return UiStrings.ReckoningEndingEndured;
                case FightEnding.Fell: return UiStrings.ReckoningEndingFell;
                default: return UiStrings.ReckoningEndingVictory;
            }
        }

        // The one chip presenter, over this screen's own widgets.
        private RelicChipStrip Gained =>
            _gained ?? (_gained = new RelicChipStrip(new RelicChipWidgets(
                gainedStrip, gainedCaption, centred: true, gainedChips, gainedChipIcons, gainedChipNames,
                gainedTooltip, gainedTooltipName, gainedTooltipMeta, gainedTooltipBody, relicArt)));

        // The ids the chips are showing: what leaving the screen acknowledges.
        public IReadOnlyList<string> DisplayedReceipts => Gained.Displayed;

        public bool IsOutcomeForm => _request.IsOutcomeForm;

        private void PaintTally()
        {
            var characters = _request.Tally;
            var ledger = _request.Ledger;

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
                elapsed += Time.unscaledDeltaTime * SpeedMultiplier;

                float k = Easing.SmoothStep(Mathf.Clamp01(elapsed / WipeSeconds));
                SetWipeWidth(ReckoningScreen.PanelWidth * k);
                frameWipe.anchoredPosition =
                    new Vector2(frameWipe.anchoredPosition.x, Mathf.Lerp(LiftFrom, 0f, k));

                float g = Easing.SmoothStep(Mathf.Clamp01(elapsed / GloomSeconds));
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

        // The choice leaves left, the summary arrives from the right, both
        // moving the same way, fast, with a smear on them while they travel.
        private IEnumerator SweepToSummary()
        {
            // A phase travels exactly its OWN width, which is the clip's, not
            // the panel's. Both phases live inside a RectMask2D inset to the
            // painted border, so a card is gone the moment it has moved one
            // clip-width, disappearing BEHIND the border rather than sailing
            // on over the battlefield.
            float width = ReckoningScreen.ContentHalfWidth * 2f;

            var offerBlur = BlurGroup(offerPhase);
            var summaryBlur = BlurGroup(summaryPhase);

            summaryPhase.gameObject.SetActive(true);
            summaryPhase.anchoredPosition = new Vector2(width, 0f);

            for (float t = 0f; t < SweepSeconds; t += Time.unscaledDeltaTime * SpeedMultiplier)
            {
                float k = Easing.OutCubic(Mathf.Clamp01(t / SweepSeconds));

                offerPhase.anchoredPosition = new Vector2(Mathf.Lerp(0f, -width, k), 0f);
                summaryPhase.anchoredPosition = new Vector2(Mathf.Lerp(width, 0f, k), 0f);

                // Peaks in the middle of the travel and is back to nothing at
                // both ends, so nothing is ever LEFT smeared or faded -- the
                // blur belongs to the motion and not to either screen.
                float speed = 4f * k * (1f - k);
                Smear(offerPhase, offerBlur, speed);
                Smear(summaryPhase, summaryBlur, speed);

                yield return null;
            }

            offerPhase.anchoredPosition = new Vector2(-width, 0f);
            summaryPhase.anchoredPosition = Vector2.zero;

            // Cleared explicitly rather than left to the last loop iteration,
            // which never lands exactly on k = 1.
            Smear(offerPhase, offerBlur, 0f);
            Smear(summaryPhase, summaryBlur, 0f);

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
            // Scaled the same way as everything else on this seam, rather
            // than by dividing WaitForSecondsRealtime's own clock -- there is
            // no unscaled equivalent of WaitForSeconds that also multiplies,
            // so this is a manual timer instead of the realtime wait it
            // replaces.
            if (delay > 0f)
            {
                for (float t = 0f; t < delay; t += Time.unscaledDeltaTime * SpeedMultiplier) yield return null;
            }

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

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime * SpeedMultiplier)
            {
                float k = Easing.SmoothStep(Mathf.Clamp01(t / seconds));
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

            // Manual timer, same reason as FillBar's delay above.
            for (float t = 0f; t < FlashSeconds; t += Time.unscaledDeltaTime * SpeedMultiplier) yield return null;

            fill.color = resting;
        }

        // Attached at RUNTIME, the same way HoverIndex and BarSlider are: the
        // tree emits plain rects, and a group that exists only for the duration
        // of one animation is not something a scene should carry.
        private static CanvasGroup BlurGroup(RectTransform phase)
        {
            if (phase == null) return null;
            return phase.GetComponent<CanvasGroup>() ?? phase.gameObject.AddComponent<CanvasGroup>();
        }

        private static void Smear(RectTransform phase, CanvasGroup group, float amount)
        {
            if (phase != null)
            {
                phase.localScale = new Vector3(1f + SweepSmear * amount, 1f, 1f);
            }

            if (group != null)
            {
                group.alpha = 1f - SweepAlphaDip * amount;
            }
        }

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
