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
    public partial class ReckoningController : MonoBehaviour
    {
        // The MASK, not the frame. Opening this uncovers a painted panel that
        // never changes size; the frame itself is deliberately not held here,
        // because anything with a reference to it is one edit away from
        // scaling it again.
        [SerializeField] internal RectTransform frameWipe;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal TMP_Text noticeLabel;
        [SerializeField] internal Button continueButton;

        [SerializeField] internal GameObject[] rowGroups;
        [SerializeField] internal TMP_Text[] rowNames;
        [SerializeField] internal TMP_Text[] rowLevels;
        [SerializeField] internal Image[] rowBarFills;
        [SerializeField] internal Image[] rowBarBefores;
        [SerializeField] internal TMP_Text[] rowGains;

        [SerializeField] internal Button[] offerButtons;
        [SerializeField] internal TMP_Text[] offerNames;
        [SerializeField] internal TMP_Text[] offerDeltas;
        [SerializeField] internal TMP_Text[] offerChevrons;
        [SerializeField] internal TMP_Text[] offerFeatures;

        // The card face's other parts (docs/PLAN_REWARD_SCREENS.md C5), one per card.
        [SerializeField] internal Image[] offerPlinths;
        [SerializeField] internal Image[] offerGhosts;
        [SerializeField] internal Image[] offerChains;
        [SerializeField] internal Image[] offerWearerDiscs;
        [SerializeField] internal Image[] offerWearerPortraits;
        [SerializeField] internal Image[] offerBands;
        [SerializeField] internal TMP_Text[] offerBandTiers;
        [SerializeField] internal TMP_Text[] offerBandPluses;

        // The spark that flies into a card's slot band on a hone or a leave.
        [SerializeField] internal Image[] offerSparks;

        // The focus detail shared by every card.
        [SerializeField] internal GameObject cardTip;
        [SerializeField] internal TMP_Text cardTipTitle;
        [SerializeField] internal TMP_Text cardTipBody;

        // "Hone instead" under each gear card, and the control that leaves the
        // page (or carries on once nothing is left to choose).
        [SerializeField] internal Button[] honeButtons;
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
        private const float WipeSeconds = RevealSchedule.WipeSeconds;

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

        // THE SPEED RULE (docs/PLAN_REWARD_SCREENS.md C10): every authored
        // duration here becomes wall time through ReckoningTime, at the battle
        // speed read when the overlay opens, and every clock below runs at
        // Tick, so the test seam speeds all of them at once.
        private float _preset = Domain.Combat.Session.BattleSpeed.DefaultDisplay;

        private float W(float authored) => ReckoningTime.Wall(authored, _preset);

        private static float Tick => (DeltaOverride != null ? DeltaOverride() : Time.unscaledDeltaTime) * SpeedMultiplier;

        // A FILM'S CLOCK: a capture renders each frame for longer than a frame
        // lasts, so a recording hands every frame its fixed 1/fps here instead
        // of the wall clock (tools/gif.ps1 reveal). Null in play.
        public static System.Func<float> DeltaOverride;

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

        // Row position -> page index and back (OfferRowLayout.RowOrder); the
        // card whose detail is showing, or -1.
        private int[] _rowOrder;
        private int[] _rowOf;
        private int _tipIndex = -1;
        private bool _tipHone;

        // What the focused whetstone and door would do to the bands; the faces
        // the last paint built, so a focus move repaints bands alone.
        private int _honeFocus = -1;
        private bool _leaveFocus;
        private RewardCardFace[] _faces = new RewardCardFace[0];

        // Per card: the flights that own its animated parts, and where those
        // parts rest.
        private int[] _flying = new int[0];
        private readonly List<Coroutine> _flights = new List<Coroutine>();
        private readonly HashSet<RectTransform> _nudging = new HashSet<RectTransform>();
        private Vector2[] _iconHome = new Vector2[0];
        private Vector2[] _sparkHome = new Vector2[0];
        private float[] _ghostHomeY = new float[0];

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

            _flying = new int[offerButtons.Length];
            _iconHome = HomesOf(offerIcons);
            _sparkHome = HomesOf(offerSparks);
            _ghostHomeY = HomesOf(offerGhosts).Select(h => h.y).ToArray();

            // The whetstones and the door share the cards' focus plumbing: the
            // band previews follow the stick and the pointer through one router.
            for (int i = 0; honeButtons != null && i < honeButtons.Length; i++)
            {
                int index = i;
                honeButtons[i].onClick.AddListener(() => Act(Choice.Hone, index));
                _tooltips.Register(honeButtons[i], entered => ShowHoneTip(index, entered));
                WireHover(honeButtons[i].gameObject, index);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(() => Act(Choice.Leave, -1));
                _tooltips.Register(leaveButton, ShowLeaveFocus);
                WireHover(leaveButton.gameObject, 0);
            }

            for (int i = 0; i < offerButtons.Length; i++)
            {
                int index = i;
                offerButtons[i].onClick.AddListener(() => Act(Choice.Take, index));

                // The focus detail, by the stick (selection) and the pointer
                // through one router.
                var node = offerButtons[i].gameObject;
                _tooltips.Register(offerButtons[i], entered => ShowCardTip(index, entered));
                WireHover(node, index);
            }

            if (cache != null) cache.onClick.AddListener(SkipReveal);
            if (catcher != null) catcher.onClick.AddListener(SkipReveal);
        }

        private static Vector2[] HomesOf(Graphic[] parts) =>
            (parts ?? new Graphic[0]).Select(p => p == null ? Vector2.zero : p.rectTransform.anchoredPosition).ToArray();

        private void WireHover(GameObject node, int index)
        {
            var hover = node.GetComponent<HoverIndex>() ?? node.AddComponent<HoverIndex>();
            hover.Index = index;
            hover.Changed = (_, entered) => _tooltips.Pointer(node, entered);
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
                // In ROW order, so Left/Right walk the cards as they sit.
                for (int row = 0; _page != null && row < offerButtons.Length && row < _page.Cards.Count; row++)
                {
                    int i = PageAt(row);
                    if (i >= offerButtons.Length || i >= _page.Cards.Count) continue;
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

                    // Right from the last card is the way off the page; Left
                    // from it comes back.
                    if (cards.Count > 0)
                    {
                        links.Add(RuntimeNavWiring.Link(cards[cards.Count - 1], UiNavDirection.Right, leave));
                        links.Add(RuntimeNavWiring.Link(leave, UiNavDirection.Left, cards[cards.Count - 1]));
                    }
                }

                // THE GAMEOBJECT, never the Button. NavContext reads its
                // entry back through `as GameObject` (it holds an opaque
                // handle, being engine-free), so a Selectable put here
                // resolves to null and the dispatcher's own reselection rule
                // then clears the selection every single frame.
                entry = (First(cards) ?? leave)?.gameObject;

                // While the cache stands it is the entry (C10); the face-down
                // cards stay declared, so a player who moves to one keeps it.
                if (_cacheUp && cache != null)
                {
                    entry = cache.gameObject;
                    selectables[cache.name] = cache.gameObject;
                }

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
            CancelFlights();
            StopReveal();
            _nudging.Clear();

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
            CancelFlights();
            StopReveal();
            _preset = GameSettings.BattleSpeed;
            BeatRunning = RevealBlocks;
            SkipBeat = SkipReveal;
            _honeFocus = -1;
            _leaveFocus = false;
            HideCardTip();
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
            PaintEmbers();

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
            SelectEntry();

            if (isActiveAndEnabled)
            {
                _animation = StartCoroutine(PlayIn());
                if (choosing) BeginReveal();

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
            float iconWidth = OfferRowLayout.IconWidth(count);
            float bandLabelX = OfferRowLayout.BandLabelX(count);

            for (int i = 0; i < count; i++)
            {
                // The card sits at its ROW position, not its page index: cards
                // group by wearer (OfferRowLayout.RowOrder) while the buttons,
                // their names and the page stay page-indexed.
                float x = OfferRowLayout.CardX(RowOf(i), count);

                Seat(RectAt(offerRects, i), x, cardWidth);

                // "Hone instead" keeps its size, because a plate is a 3:1 art
                // and stretching it with the row would bend it; only its x
                // follows the card.
                Seat(honeButtons != null && i < honeButtons.Length && honeButtons[i] != null
                    ? honeButtons[i].transform as RectTransform
                    : null, x, null);

                SetWidth(RectAt(offerNames, i), labelWidth);
                SetWidth(RectAt(offerFeatures, i), labelWidth);

                Seat(RectAt(offerDeltas, i), OfferRowLayout.DeltaLabelX(count), OfferRowLayout.DeltaLabelWidth(count));
                Seat(RectAt(offerChevrons, i), OfferRowLayout.ChevronX(count), null);

                // THE ART TOO. The icon box is emitted at the widest row's
                // width because that is the row the audit solves; leaving it
                // there would mean a three-card row drawing its items smaller
                // than the card can hold, for no reason but the audit's
                // convenience.
                SetWidth(RectAt(offerIcons, i), iconWidth);
                SetWidth(RectAt(offerChains, i), iconWidth);
                Seat(RectAt(offerDeltaGlyphs, i), OfferRowLayout.DeltaGlyphX(count), null);
                SetSquare(RectAt(offerHalos, i), OfferRowLayout.HaloDiameter(count));
                SetSquare(RectAt(offerBursts, i), OfferRowLayout.BurstDiameter(count));

                Seat(RectAt(offerGhosts, i), OfferRowLayout.GhostX(count), OfferRowLayout.GhostWidth(count));
                Seat(RectAt(offerWearerDiscs, i), OfferRowLayout.MedallionX(count), null);
                Seat(RectAt(offerWearerPortraits, i), OfferRowLayout.MedallionX(count), null);

                Seat(RectAt(offerBands, i), 0f, OfferRowLayout.BandWidth(count));
                Seat(RectAt(offerBandTiers, i), -bandLabelX, null);
                Seat(RectAt(offerBandPluses, i), bandLabelX, null);
            }
        }

        // Where card `pageIndex` sits in the row.
        private int RowOf(int pageIndex) =>
            _rowOf != null && pageIndex >= 0 && pageIndex < _rowOf.Length ? _rowOf[pageIndex] : pageIndex;

        // Row position -> page index for the cards on screen; identity before
        // the first paint.
        private int PageAt(int row) =>
            _rowOrder != null && row >= 0 && row < _rowOrder.Length ? _rowOrder[row] : row;

        // Cards group by wearer in squad order (C5), recomputed on every paint
        // because a take or a leave never changes the page's wearers but a new
        // page does.
        private void UpdateRowOrder(int count)
        {
            var wearers = new List<string>(count);
            for (int i = 0; i < count; i++) wearers.Add(_page.Cards[i].RecipientId);

            var squad = new List<string>();
            var save = SaveSlotManager.CurrentSave;
            if (save != null)
            {
                foreach (var member in save.ActiveSquad())
                {
                    if (member != null) squad.Add(member.definitionId);
                }
            }

            _rowOrder = OfferRowLayout.RowOrder(wearers, squad);
            _rowOf = OfferRowLayout.RowOf(_rowOrder);
        }

        // Position x and, when given, width; y and height are the tree's.
        private static void Seat(RectTransform rect, float x, float? width)
        {
            if (rect == null) return;

            var position = rect.anchoredPosition;
            position.x = x;
            rect.anchoredPosition = position;

            if (width.HasValue)
            {
                var size = rect.sizeDelta;
                size.x = width.Value;
                rect.sizeDelta = size;
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

        private static RectTransform RectAt(RectTransform[] items, int index) =>
            items == null || index < 0 || index >= items.Length ? null : items[index];

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
            if (leaveButton != null) leaveButton.gameObject.SetShown(any);

            int count = any ? Mathf.Min(_page.Cards.Count, offerButtons.Length) : 0;

            // RE-CENTRE FIRST, before anything reads a position.
            //
            // The row is emitted at OfferRowLayout.MaxCards wide because a
            // screen is built once for every save. A page of k + 2 cards left
            // at those positions would sit off-axis with a gap where the
            // missing ones belong -- which is what a hidden card looks like if
            // nobody moves the rest.
            if (any) UpdateRowOrder(count);
            LayOutOfferRow(count);

            var save = SaveSlotManager.CurrentSave;
            bool narrow = OfferRowLayout.IsNarrow(count);
            _faces = new RewardCardFace[offerButtons.Length];
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
                _faces[i] = face;
                if (!Flying(i)) ResetFlightParts(i);

                // Graceful degradation: a card naming content that no longer
                // exists shows what it can rather than taking the screen down.
                SetText(offerNames, i, face.Name);
                SetText(offerFeatures, i, narrow ? "" : face.Feature);
                SeatFeature(i, relic: !card.IsGear);
                PaintNameWrap(i, narrow);

                // The frame colour is the QUALITY, and it is the plinth, burst
                // and halo that wear it, from the same table the name and the
                // dossier's cell edges read. Held well under full alpha: it is
                // a glow behind an object, and at full strength it competes
                // with the object it exists to frame.
                var glow = HexColour(face.Hex);
                if (offerNames[i] != null)
                {
                    var nameColour = glow;
                    nameColour.a = open ? 1f : 0.55f;
                    offerNames[i].color = nameColour;
                }

                if (!Flying(i)) PaintIcon(i, card, face, open);

                // A card that was taken, honed or sold has left its glow behind;
                // one that was never chosen keeps a faint one.
                bool spent = card.Outcome != CardOutcome.Open;
                glow.a = open ? 0.42f : spent ? 0f : 0.18f;
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
                    soft.a = open ? 0.30f : spent ? 0f : 0.12f;
                    offerHalos[i].color = soft;
                    offerHalos[i].rectTransform.localScale = Vector3.one;
                }

                PaintUnderGlow(i, glow, face.PlinthStep, open, spent);

                PaintPlinth(i, face, count, open);
                if (!Flying(i)) PaintGhost(i, face);
                PaintWearer(i, face);
                PaintBand(i, face, card);
                PaintDelta(i, face, card);
                PaintHone(i, card);

                // Every card stays visible once the page is spent, and none of
                // them respond. Hiding the ones not chosen would erase the
                // decision the player just made from the screen that asked for
                // it.
                offerButtons[i].interactable = open;
            }

            PaintLeave();
            PaintEmbers();

            // The page repainted under a held focus (a take changes the worn
            // item): the detail follows it.
            if (_tipIndex >= 0) ShowTip(_tipIndex, true, _tipHone);
        }

        // The item on its plinth. What became of it is the card's state: a taken
        // card's plinth is empty, a honed one is dust, a sold one leaves a coin
        // mark (PaintDelta), and one nobody chose is dimmed.
        private void PaintIcon(int i, PendingCard card, RewardCardFace face, bool open)
        {
            if (card.IsGear) ItemIcons.Apply(offerIcons[i], icons, ItemIcons.KeyOf(card.Copy));
            else ItemIcons.Apply(offerIcons[i], relicArt, card.RelicId);

            var icon = offerIcons[i];
            bool visible = card.Outcome == CardOutcome.Open || card.Outcome == CardOutcome.Honed;
            if (icon != null)
            {
                // An inert card is grey with a chain over it (the chain is dark
                // iron already, so it is not tinted).
                var tint = face.Inert ? InertTint : Color.white;
                if (card.Outcome == CardOutcome.Honed) tint.a = DustAlpha;
                else if (!open) tint.a = DimAlpha;
                icon.color = tint;
                icon.rectTransform.localScale = card.Outcome == CardOutcome.Honed ? Vector3.one * DustScale : Vector3.one;
                if (!visible) icon.enabled = false;
            }

            if (offerChains[i] != null) offerChains[i].color = face.Inert && visible ? Color.white : Color.clear;
        }

        private static readonly Color InertTint = new Color(0.5f, 0.5f, 0.55f, 1f);
        private static readonly Color Up = new Color(0.44f, 0.75f, 0.45f, 1f);
        private static readonly Color Down = new Color(0.9f, 0.35f, 0.32f, 1f);
        private static readonly Color Flat = new Color(0.6f, 0.58f, 0.66f, 1f);
        private static readonly Color BandLit = new Color(0.95f, 0.82f, 0.45f, 1f);
        private static readonly Color BandDim = new Color(0.54f, 0.48f, 0.65f, 1f);

        // From four cards up the name truncates to one line.
        private void PaintNameWrap(int i, bool narrow)
        {
            var name = i < offerNames.Length ? offerNames[i] : null;
            if (name == null) return;

            name.textWrappingMode = narrow ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            name.overflowMode = narrow ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        }

        // The plinth is the quality's primary channel: its width steps with
        // the quality (1..4), tinted by the quality colour.
        private void PaintPlinth(int i, RewardCardFace face, int count, bool open)
        {
            var plinth = i < offerPlinths.Length ? offerPlinths[i] : null;
            if (plinth == null) return;

            var rect = plinth.rectTransform;
            var size = rect.sizeDelta;
            size.x = OfferRowLayout.PlinthWidth(count, face.PlinthStep);
            rect.sizeDelta = size;

            var tint = HexColour(face.Hex);
            tint.a = open ? 0.55f : 0.25f;
            plinth.color = tint;
        }

        // What the wearer has now, behind and to the left. An affix the worn
        // copy has and this one lacks is named in the focus detail only.
        private void PaintGhost(int i, RewardCardFace face)
        {
            var ghost = i < offerGhosts.Length ? offerGhosts[i] : null;
            if (ghost != null)
            {
                if (face.GhostKey != null) ItemIcons.Apply(ghost, icons, face.GhostKey);
                else ghost.enabled = false;

                ghost.color = new Color(1f, 1f, 1f, OfferRowLayout.GhostAlpha);
            }
        }

        // Identity only: whose slot this is.
        private void PaintWearer(int i, RewardCardFace face)
        {
            bool gear = face.WearerId.Length > 0;
            if (i < offerWearerDiscs.Length && offerWearerDiscs[i] != null) offerWearerDiscs[i].gameObject.SetActive(gear);

            var portrait = i < offerWearerPortraits.Length ? offerWearerPortraits[i] : null;
            if (portrait == null) return;

            var sprite = gear ? CharacterPortraits.For(face.WearerId) : null;
            portrait.sprite = sprite;
            portrait.preserveAspect = true;
            portrait.enabled = sprite != null;
            portrait.gameObject.SetActive(gear);
        }

        // The slot band: the tier numeral and the SLOT's honing, which stays
        // lit across a swap because it belongs to the slot. A taken card keeps
        // its band warm; a pending hone or leave shows +N>+N+k on it.
        private void PaintBand(int i, RewardCardFace face, PendingCard card)
        {
            var band = i < offerBands.Length ? offerBands[i] : null;
            if (band != null)
            {
                band.gameObject.SetActive(face.HasBand);
                band.color = WaitingHones(card) > 0 ? BandSpark
                    : card.Outcome == CardOutcome.Taken ? BandTaken
                    : BandRest;
            }

            var tier = i < offerBandTiers.Length ? offerBandTiers[i] : null;
            if (tier != null)
            {
                tier.gameObject.SetActive(face.HasBand);
                if (face.HasBand) tier.Set(UiStrings.ReckoningBandTier, face.BandTier);
            }

            var plus = i < offerBandPluses.Length ? offerBandPluses[i] : null;
            if (plus != null)
            {
                plus.gameObject.SetActive(face.HasBand);
                if (!face.HasBand) return;

                int extra = WaitingHones(card);
                if (extra > 0) plus.Set(UiStrings.ReckoningBandHoneNext, face.BandPlus, face.BandPlus + extra);
                else plus.Set(UiStrings.ReckoningBandHone, face.BandPlus);
                plus.color = extra > 0 || face.BandPlus > 0 ? BandLit : BandDim;
            }
        }

        // The +1s a focused control would put on this card's slot: one for the
        // whetstone under any card on the slot, what leaving would pay for the
        // door. Zero for a relic, a spent page and a slot that cannot be honed.
        private int WaitingHones(PendingCard card)
        {
            if (_page == null || card == null || !card.IsGear) return 0;

            var save = SaveSlotManager.CurrentSave;
            if (_honeFocus >= 0 && _honeFocus < _page.Cards.Count
                && RewardPages.HoneInsteadFor(save, _page, _honeFocus) == RewardPages.HoneInsteadResult.Hone)
            {
                var focused = _page.Cards[_honeFocus];
                if (focused.RecipientId == card.RecipientId && focused.Slot == card.Slot) return 1;
            }

            if (_leaveFocus && _page.ChoicesLeft > 0)
            {
                return RewardPages.PreviewLeave(save, _page).HonesFor(card.RecipientId, card.Slot);
            }

            return 0;
        }

        // Bands only, from the faces the last paint built: a focus moving over
        // the whetstones and the door must not re-run every card's compare.
        private void RepaintBands()
        {
            if (_page == null || _faces == null) return;

            for (int i = 0; i < _faces.Length && i < _page.Cards.Count; i++)
            {
                if (_faces[i] != null) PaintBand(i, _faces[i], _page.Cards[i]);
            }
        }

        // The headline stat, signed, and the chevron whose shape carries the sign.
        // A sold card leaves its price here, as the coin mark.
        private void PaintDelta(int i, RewardCardFace face, PendingCard card)
        {
            SetText(offerDeltas, i, card.Outcome == CardOutcome.Paid
                ? UiStrings.ReckoningCount.Format(card.GoldPaid)
                : face.HasDelta ? face.DeltaText : "");
            PaintDeltaGlyph(i, face, card);

            var chevron = i < offerChevrons.Length ? offerChevrons[i] : null;
            if (chevron == null) return;

            chevron.gameObject.SetActive(face.HasDelta);
            if (!face.HasDelta) return;

            if (face.DeltaSign > 0) chevron.Set(UiStrings.ReckoningChevronUp);
            else if (face.DeltaSign < 0) chevron.Set(UiStrings.ReckoningChevronDown);
            else chevron.Set(UiStrings.ReckoningChevronFlat);

            chevron.color = face.DeltaSign > 0 ? Up : face.DeltaSign < 0 ? Down : Flat;
        }

        // ---- the focus detail ---------------------------------------------------------
        //
        // One shared box over the offer phase, shown for the focused (or
        // hovered) card, or for the whetstone under it, through the same router
        // the chips use, so the stick and the mouse share one arbitration.
        private void ShowCardTip(int index, bool entered) => ShowTip(index, entered, false);

        // The whetstone's focus: the slot band reads +N>+N+1 and the box shows
        // the worn item's stats now -> next. The coin case has nothing to preview.
        private void ShowHoneTip(int index, bool entered)
        {
            if (entered) _honeFocus = index;
            else if (_honeFocus == index) _honeFocus = -1;

            bool previews = entered && _page != null && index >= 0 && index < _page.Cards.Count
                && RewardPages.HoneInsteadFor(SaveSlotManager.CurrentSave, _page, index) == RewardPages.HoneInsteadResult.Hone;
            ShowTip(index, previews, true);
            RepaintBands();
        }

        private void ShowLeaveFocus(bool entered)
        {
            _leaveFocus = entered;
            RepaintBands();
        }

        private void ShowTip(int index, bool entered, bool hone)
        {
            if (cardTip == null) return;

            // No detail while a beat runs (a face-down card has nothing to
            // describe); the focus the player moves during a beat gets its
            // detail when the row lands.
            if (entered && !TipAllowed(index, hone)) return;

            if (!entered || _page == null || index < 0 || index >= _page.Cards.Count
                || index >= offerButtons.Length || !offerButtons[index].gameObject.activeInHierarchy)
            {
                if (!entered && index == _tipIndex && hone == _tipHone) HideCardTip();
                return;
            }

            _tipIndex = index;
            _tipHone = hone;
            var card = _page.Cards[index];
            var save = SaveSlotManager.CurrentSave;
            var detail = hone ? RewardCardFace.DetailOfHone(save, card)
                : card.IsGear ? RewardCardFace.DetailOfGear(save, card) : RewardCardFace.DetailOfRelic(save, card);

            if (cardTipTitle != null)
            {
                cardTipTitle.SetContent(detail.Title);
                if (offerNames[index] != null) cardTipTitle.color = offerNames[index].color;
            }

            if (cardTipBody != null) cardTipBody.SetContent(detail.Body);

            FitCardTip();
            PlaceCardTip(index, hone);
            cardTip.SetActive(true);
        }

        private void HideCardTip()
        {
            _tipIndex = -1;
            _tipHone = false;
            if (cardTip != null) cardTip.SetActive(false);
        }

        private void FitCardTip()
        {
            var box = cardTip.transform as RectTransform;
            if (box == null) return;

            float inner = ItemComparisonPanel.BodyWidthFor(ReckoningScreen.CardTipWidth);
            float body = ReckoningScreen.CardTipMinBody;
            if (cardTipBody != null)
            {
                body = Mathf.Max(body, cardTipBody.GetPreferredValues(cardTipBody.text, inner, 0f).y);
            }

            float height = Mathf.Min(ReckoningScreen.CardTipMaxHeight,
                ItemComparisonPanel.HeightFor(ReckoningScreen.CardTipTitleHeight, body));
            box.sizeDelta = new Vector2(ReckoningScreen.CardTipWidth, height);
        }

        // Under the row, never over another card: every other card on the page
        // is a keep-out (TooltipPlacement.Beside), and a whetstone's detail
        // also keeps off the whetstone it describes.
        private void PlaceCardTip(int index, bool hone)
        {
            var box = cardTip.transform as RectTransform;
            var card = RectAt(offerRects, index);
            if (box == null || card == null) return;

            var keepOut = new List<UiRect>();
            for (int i = 0; i < offerRects.Length && _page != null && i < _page.Cards.Count; i++)
            {
                var other = offerRects[i];
                if (i == index || other == null || !other.gameObject.activeInHierarchy) continue;
                keepOut.Add(RectOf(other));
            }

            if (hone && honeButtons != null && index < honeButtons.Length && honeButtons[index] != null)
            {
                keepOut.Add(RectOf((RectTransform)honeButtons[index].transform));
            }

            var at = TooltipPlacement.Beside(
                card.anchoredPosition.x, card.anchoredPosition.y, card.sizeDelta.x, card.sizeDelta.y,
                box.sizeDelta.x, box.sizeDelta.y,
                -ReckoningScreen.ContentHalfWidth, ReckoningScreen.ContentHalfWidth,
                ReckoningScreen.ContentBottom, ReckoningScreen.ContentTop,
                ReckoningScreen.CardTipGap, keepOut);
            box.anchoredPosition = new Vector2(at.X, at.Y);
        }

        private static UiRect RectOf(RectTransform rect) =>
            new UiRect(new UiVec(rect.anchoredPosition.x, rect.anchoredPosition.y),
                new UiVec(rect.sizeDelta.x, rect.sizeDelta.y));

        // The whetstone under a card, or the coin stack with the card's price
        // when every shown slot is spent and the choice pays gold instead.
        private void PaintHone(int i, PendingCard card)
        {
            if (!HoneShown(i)) return;

            var save = SaveSlotManager.CurrentSave;
            bool gold = RewardPages.HoneInsteadFor(save, _page, i) == RewardPages.HoneInsteadResult.Gold;
            PaintHoneFace(i, gold, gold ? RewardPages.SalePrice(card.Copy, _page.Step) : 0);
        }

        // Whether card `i` offers "hone instead": a gear page's open card whose
        // slot can still be honed, or the one case where every slot is spent
        // and the choice pays the card's price (RewardPages.HoneInsteadFor).
        private bool HoneShown(int i) =>
            _page != null && honeButtons != null && i < honeButtons.Length && honeButtons[i] != null
            && i < _page.Cards.Count && _page.Kind == RewardPageKind.Gear
            && RewardPages.HoneInsteadFor(SaveSlotManager.CurrentSave, _page, i) != RewardPages.HoneInsteadResult.Refused;

        // The door's face is what leaving pays now (RewardPages.PreviewLeave, the
        // same numbers Leave pays): the door, then a whetstone count and a coin
        // count; once nothing is left to choose it is the way forward.
        private void PaintLeave()
        {
            var label = leaveButton == null ? null : leaveButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (_page == null || label == null) return;

            if (_page.ChoicesLeft == 0)
            {
                label.Set(UiStrings.ReckoningForward);
                PaintDoorFace(0, 0, forward: true);
                return;
            }

            label.SetContent("");
            var preview = RewardPages.PreviewLeave(SaveSlotManager.CurrentSave, _page);
            PaintDoorFace(preview.Hones.Sum(h => h.Count), preview.Gold, forward: false);
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

        private enum Choice
        {
            Take,
            Hone,
            Leave,
        }

        // THE ONE GATE (docs/PLAN_REWARD_SCREENS.md C10, review B1): take, hone
        // instead and leave all arrive here, from the mouse and from Submit.
        // While a reveal beat runs (BeatRunning, set by the reveal), a press
        // completes the beat through SkipBeat, nudges the pressed control and
        // returns without touching RewardPages: nothing can pay or close a page
        // the player has not seen.
        public System.Func<bool> BeatRunning;
        public System.Action SkipBeat;

        private void Act(Choice choice, int index)
        {
            if (BeatRunning != null && BeatRunning())
            {
                SkipBeat?.Invoke();
                Nudge(choice, index);
                return;
            }

            EndLinger();

            switch (choice)
            {
                case Choice.Take: Take(index); break;
                case Choice.Hone: HoneInstead(index); break;
                default: LeaveOrAdvance(); break;
            }
        }

        // A gear card equips on its recipient (the copy it displaced goes to
        // the bag: EquipMove puts exactly one back per slot), a relic joins the
        // run. The page stays on screen until the player moves on, so the spent
        // cards are readable.
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

            SoundController.Play(Sound.Take);
            StartFlight(FlyTake(index, card.IsGear), index);
            AfterChoice();
        }

        // Hone the slot, or sell the card when every shown slot is spent. The
        // honing belongs to the slot, so the spark lands on the band of every
        // card that shares it.
        private void HoneInstead(int index)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || !RewardPages.CanTake(_page, index)) return;

            var card = _page.Cards[index];
            var before = RewardPages.HoneInsteadFor(save, _page, index);
            if (before == RewardPages.HoneInsteadResult.Refused) return;

            var claimed = before == RewardPages.HoneInsteadResult.Hone
                ? CardsOnSlot(card.RecipientId, card.Slot)
                : new List<int> { index };
            if (!claimed.Contains(index)) claimed.Add(index);

            if (RewardPages.HoneInstead(save, _page, index) == RewardPages.HoneInsteadResult.Refused) return;

            bool coins = before == RewardPages.HoneInsteadResult.Gold;
            SoundController.Play(coins ? Sound.CoinTick : Sound.HoneScrape);
            StartFlight(FlyHone(index, claimed, coins), claimed.ToArray());
            AfterChoice();
        }

        // Leaving pays every unused choice and closes the page, which then
        // shows what it paid; once nothing is left to choose the same control
        // moves on to the next page, or to the summary after the last.
        private void LeaveOrAdvance()
        {
            if (_page == null) return;

            if (_page.ChoicesLeft > 0)
            {
                var save = SaveSlotManager.CurrentSave;
                var preview = RewardPages.PreviewLeave(save, _page);
                var targets = new List<int>();
                for (int i = 0; i < _page.Cards.Count && i < offerButtons.Length; i++)
                {
                    var card = _page.Cards[i];
                    if (card.IsGear && preview.HonesFor(card.RecipientId, card.Slot) > 0) targets.Add(i);
                }

                RewardPages.Leave(save, _page);
                SoundController.Play(Sound.EmberSnuff);
                if (preview.Gold > 0) SoundController.Play(Sound.CoinTick);
                StartFlight(FlyLeave(targets, preview.Gold > 0), targets.ToArray());
                AfterChoice();
                return;
            }

            CancelFlights();
            HideCardTip();
            _honeFocus = -1;
            _leaveFocus = false;

            _page = _pages.FirstOrDefault(p => p != null && p.ChoicesLeft > 0);
            if (_page != null)
            {
                PaintOffers();
                RefreshNavigation();
                SelectEntry();
                if (isActiveAndEnabled) BeginReveal();
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

        // Every card on the page that wears this slot, in page order.
        private List<int> CardsOnSlot(string recipientId, EquipmentSlot slot)
        {
            var cards = new List<int>();
            for (int i = 0; i < _page.Cards.Count && i < offerButtons.Length; i++)
            {
                var card = _page.Cards[i];
                if (card.IsGear && card.RecipientId == recipientId && card.Slot == slot) cards.Add(i);
            }

            return cards;
        }

        // ---- the choices' motion ------------------------------------------------------
        //
        // Plain tweens on placeholders (no art or real sound yet). State changes
        // the moment RewardPages answers; a flight only owns a card's animated
        // parts (item, ghost, spark) until it lands, and the repaint that ends it
        // draws the resting state, so a cancelled flight leaves nothing behind.

        private const float FlightSeconds = 0.26f;
        private const float PunchSeconds = 0.16f;
        private const float NudgePixels = 4f;
        private const float DustAlpha = 0.25f;
        private const float DustScale = 0.6f;
        private const float DimAlpha = 0.45f;
        private static readonly Color SparkColour = new Color(1f, 0.91f, 0.66f, 1f);
        private static readonly Color32 BandRest = new Color32(29, 18, 38, 230);
        private static readonly Color32 BandSpark = new Color32(107, 79, 31, 230);
        private static readonly Color32 BandTaken = new Color32(64, 48, 22, 230);

        private bool Flying(int i) => _flying != null && i >= 0 && i < _flying.Length && _flying[i] > 0;

        private IEnumerator Tween(float seconds, System.Action<float> apply)
        {
            float wall = W(seconds);
            for (float t = 0f; t < wall; t += Tick)
            {
                apply(Easing.SmoothStep(t / wall));
                yield return null;
            }

            apply(1f);
        }

        private void StartFlight(IEnumerator body, params int[] cards)
        {
            if (!isActiveAndEnabled) return;

            foreach (int c in cards)
            {
                if (c >= 0 && c < _flying.Length) _flying[c]++;
            }

            _flights.Add(StartCoroutine(Flight(body, cards)));
        }

        private IEnumerator Flight(IEnumerator body, int[] cards)
        {
            while (body.MoveNext()) yield return body.Current;

            foreach (int c in cards)
            {
                if (c >= 0 && c < _flying.Length && _flying[c] > 0) _flying[c]--;
            }

            PaintOffers();
        }

        private void CancelFlights()
        {
            foreach (var flight in _flights)
            {
                if (flight != null) StopCoroutine(flight);
            }

            _flights.Clear();
            if (_flying != null) System.Array.Clear(_flying, 0, _flying.Length);
        }

        // The animated parts back at rest: item at home, ghost on its shelf, the
        // spark invisible, the honing numeral at full size.
        private void ResetFlightParts(int i)
        {
            if (i < offerIcons.Length && offerIcons[i] != null && i < _iconHome.Length)
            {
                offerIcons[i].rectTransform.anchoredPosition = _iconHome[i];
                offerIcons[i].rectTransform.localScale = Vector3.one;
            }

            if (i < offerGhosts.Length && offerGhosts[i] != null && i < _ghostHomeY.Length)
            {
                var rect = offerGhosts[i].rectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, _ghostHomeY[i]);
            }

            if (offerSparks != null && i < offerSparks.Length && offerSparks[i] != null && i < _sparkHome.Length)
            {
                offerSparks[i].rectTransform.anchoredPosition = _sparkHome[i];
                offerSparks[i].color = Color.clear;
            }

            if (i < offerBandPluses.Length && offerBandPluses[i] != null) offerBandPluses[i].rectTransform.localScale = Vector3.one;
        }

        // The item lifts off its plinth to the wearer's medallion (a relic just
        // rises), the replaced item drifts off, and the band flashes: the honing
        // came with the slot.
        private IEnumerator FlyTake(int i, bool gear)
        {
            var icon = offerIcons[i];
            var rect = icon.rectTransform;
            var start = rect.position;
            var scale = rect.localScale;
            var end = gear && offerWearerDiscs[i] != null
                ? offerWearerDiscs[i].transform.position
                : start + rect.TransformVector(Vector3.up * 60f);

            var ghost = offerGhosts[i];
            var ghostRect = ghost.rectTransform;
            var ghostStart = ghostRect.anchoredPosition;
            float ghostAlpha = ghost.color.a;

            yield return Tween(FlightSeconds, k =>
            {
                rect.position = Vector3.Lerp(start, end, k);
                rect.localScale = Vector3.Lerp(scale, scale * 0.3f, k);
                var tint = icon.color;
                tint.a = 1f - k * k;
                icon.color = tint;

                ghostRect.anchoredPosition = ghostStart + new Vector2(24f * k, -18f * k);
                var faded = ghost.color;
                faded.a = ghostAlpha * (1f - k);
                ghost.color = faded;
            });

            var band = offerBands[i];
            yield return Tween(PunchSeconds, k => band.color = Color32.Lerp(BandSpark, BandTaken, k));
        }

        // The spark runs from the whetstone to the band of every card on the
        // slot while the item crumbles to dust; for a sale the coin rises.
        private IEnumerator FlyHone(int i, List<int> claimed, bool coins)
        {
            var icon = offerIcons[i];
            var scale = icon.rectTransform.localScale;
            float alpha = icon.color.a;
            var origin = honeButtons[i].transform.position;
            var targets = new List<Vector3>();
            foreach (int j in claimed)
            {
                targets.Add(coins ? origin + honeButtons[i].transform.TransformVector(Vector3.up * 80f)
                                  : offerBands[j].transform.position);
            }

            yield return Tween(FlightSeconds, k =>
            {
                var tint = icon.color;
                tint.a = Mathf.Lerp(alpha, coins ? 0f : DustAlpha, k);
                icon.color = tint;
                icon.rectTransform.localScale = Vector3.Lerp(scale, scale * (coins ? 0.4f : DustScale), k);
                MoveSparks(claimed, origin, targets, k);
            });

            yield return PunchBands(coins ? new List<int>() : claimed);
            FadeSparks(claimed, 0f);
        }

        // Every target band gets a spark from the door, and the unused choices'
        // coins make the door itself jump.
        private IEnumerator FlyLeave(List<int> targets, bool coins)
        {
            var origin = leaveButton.transform.position;
            var ends = targets.Select(j => offerBands[j].transform.position).ToList();

            yield return Tween(FlightSeconds, k =>
            {
                MoveSparks(targets, origin, ends, k);
                if (coins) leaveButton.transform.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(k * Mathf.PI));
            });

            leaveButton.transform.localScale = Vector3.one;
            yield return PunchBands(targets);
            FadeSparks(targets, 0f);
        }

        private void MoveSparks(List<int> cards, Vector3 origin, List<Vector3> ends, float k)
        {
            for (int n = 0; n < cards.Count; n++)
            {
                var spark = offerSparks[cards[n]];
                if (spark == null) continue;

                spark.rectTransform.position = Vector3.Lerp(origin, ends[n], k);
                var tint = SparkColour;
                tint.a = 1f - 0.4f * k;
                spark.color = tint;
            }
        }

        private void FadeSparks(List<int> cards, float alpha)
        {
            foreach (int j in cards)
            {
                if (offerSparks[j] == null) continue;

                var tint = offerSparks[j].color;
                tint.a = alpha;
                offerSparks[j].color = tint;
            }
        }

        // The honing numeral ticks: a short punch as the spark lands.
        private IEnumerator PunchBands(List<int> cards)
        {
            yield return Tween(PunchSeconds, k =>
            {
                float size = 1f + 0.3f * Mathf.Sin(k * Mathf.PI);
                foreach (int j in cards)
                {
                    if (offerBandPluses[j] != null) offerBandPluses[j].rectTransform.localScale = Vector3.one * size;
                }
            });
        }

        // A refused press moves the control 4 px and back.
        private void Nudge(Choice choice, int index)
        {
            if (!isActiveAndEnabled) return;

            var control = choice == Choice.Take ? (index >= 0 && index < offerButtons.Length ? offerButtons[index] : null)
                : choice == Choice.Hone ? (honeButtons != null && index >= 0 && index < honeButtons.Length ? honeButtons[index] : null)
                : leaveButton;
            if (control == null || !(control.transform is RectTransform rect) || !_nudging.Add(rect)) return;

            StartCoroutine(NudgeRoutine(rect));
        }

        private IEnumerator NudgeRoutine(RectTransform rect)
        {
            var home = rect.anchoredPosition;
            yield return Tween(PunchSeconds, k => rect.anchoredPosition = home + Vector2.right * (NudgePixels * Mathf.Sin(k * Mathf.PI)));
            rect.anchoredPosition = home;
            _nudging.Remove(rect);
        }

        // The screen's own focus is quiet: it opens no detail until the
        // player moves or hovers (TooltipFocus.Quiet), so a page never opens
        // with a box over its cards.
        private void SelectEntry()
        {
            var entry = NavigationInputModule.SelectionFor(_navContext);
            _tooltips.Quiet(entry);
            _entrySelected = entry;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(entry);
        }

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
            float wipe = W(WipeSeconds);
            float gloom = W(GloomSeconds);
            float longest = Mathf.Max(wipe, gloom);

            while (elapsed < longest)
            {
                elapsed += Tick;

                float k = Easing.SmoothStep(Mathf.Clamp01(elapsed / wipe));
                SetWipeWidth(ReckoningScreen.PanelWidth * k);
                frameWipe.anchoredPosition =
                    new Vector2(frameWipe.anchoredPosition.x, Mathf.Lerp(LiftFrom, 0f, k));

                float g = Easing.SmoothStep(Mathf.Clamp01(elapsed / gloom));
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

            float sweep = W(SweepSeconds);
            for (float t = 0f; t < sweep; t += Tick)
            {
                float k = Easing.OutCubic(Mathf.Clamp01(t / sweep));

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
                float wait = W(delay);
                for (float t = 0f; t < wait; t += Tick) yield return null;
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
            float seconds = W(BarSeconds * (countTo - countFrom));

            for (float t = 0f; t < seconds; t += Tick)
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
            float flash = W(FlashSeconds);
            for (float t = 0f; t < flash; t += Tick) yield return null;

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
