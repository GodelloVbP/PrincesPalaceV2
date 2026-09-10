using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Ambience;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The constellations, driven.
    //
    // Every rule about what can be kindled lives in TalentPage, in Domain, with
    // its own tests. This decides nothing -- it reads the save, asks Domain, and
    // paints. The one thing it genuinely owns is the SLIDE, because that is time
    // and time needs a frame.
    public partial class TalentController : MonoBehaviour
    {
        [SerializeField] internal RectTransform sky;
        [SerializeField] internal Button[] orbs;

        [SerializeField] internal GameObject[] edgeGlows;
        [SerializeField] internal int[] edgeChildSlots;
        [SerializeField] internal Image[] orbGlows;

        // A stone's own furniture, one entry per orb in the same order as
        // `orbs`. Parallel arrays rather than a lookup: the screen built them
        // in one pass and UiCountAudit can hold their lengths to each other,
        // which a dictionary assembled at runtime could not be checked against.
        [SerializeField] internal Image[] orbAuras;
        [SerializeField] internal Image[] orbCores;
        [SerializeField] internal Image[] orbRings;
        [SerializeField] internal TMP_Text[] orbLabels;
        [SerializeField] internal TMP_Text[] orbPrices;

        // The gated stones only -- SHORTER than the arrays above, and indexed
        // by their own position. collarSlots maps that position back to an orb
        // index, which is how this knows which stone a collar belongs to
        // without re-deriving the skeleton on this side of the boundary.
        [SerializeField] internal int[] collarSlots;
        [SerializeField] internal Image[] collarTracks;
        [SerializeField] internal Image[] collarFills;
        [SerializeField] internal TMP_Text[] collarCounts;

        // THE STATE IS THE SPRITE, not a tint. Six baked variants, wired at
        // build time because they live under Art/ rather than Resources/ --
        // the same arrangement MapScreen's node icons use. Nothing here
        // multiplies a colour into a stone: "ash" is a different material, and
        // reaching it by turning an orange stone down produced a brown one.
        [SerializeField] internal Sprite orbUnlitSprite;
        [SerializeField] internal Sprite orbLitSprite;
        [SerializeField] internal Sprite orbAshSprite;
        [SerializeField] internal Sprite orbAshUnauthoredSprite;
        [SerializeField] internal Sprite orbReachableSprite;
        [SerializeField] internal Sprite orbCostlySprite;

        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text pathName;
        [SerializeField] internal TMP_Text emberCount;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailBody;
        [SerializeField] internal TMP_Text panelKicker;
        [SerializeField] internal TMP_Text panelPrice;
        [SerializeField] internal TMP_Text panelRefusal;
        [SerializeField] internal RectTransform panelMeterFill;
        [SerializeField] internal TMP_Text investLabel;
        [SerializeField] internal Button investButton;

        // The backdrop's own layers, animated in Update rather than by a
        // component each: they are pure functions of time with no state worth
        // owning, and fifteen MonoBehaviours to say so would cost more than the
        // arithmetic does.
        [SerializeField] internal RectTransform[] starFields;
        [SerializeField] internal RectTransform[] cloudWashes;
        [SerializeField] internal Image[] cloudImages;
        [SerializeField] internal RectTransform[] dustMotes;
        [SerializeField] internal Image[] dustImages;
        [SerializeField] internal RectTransform[] shootingStars;
        [SerializeField] internal Image[] shootingStarImages;

        // Level 20 of the reward track. See Respec().
        [SerializeField] internal Button respecButton;
        [SerializeField] internal GameObject respecDialog;
        [SerializeField] internal TMP_Text respecDialogBody;
        [SerializeField] internal Button respecConfirmButton;
        [SerializeField] internal Button respecCancelButton;
        [SerializeField] internal Button prevPathButton;
        [SerializeField] internal Button nextPathButton;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;
        [SerializeField] internal Button backButton;

        // ---- the palette -----------------------------------------------------
        //
        // Every value here is the handoff's, and the one that looks arbitrary
        // is the one that matters: the ring at 40% while a stone is merely
        // affordable-later is what stops twenty rings pulsing at once.
        private static readonly Color GlowTaken = new Color(1f, 0.80f, 0.45f, 0.55f);
        private static readonly Color GlowOff = new Color(1f, 0.80f, 0.45f, 0f);

        private static readonly Color RingReady = new Color(0.906f, 0.698f, 0.361f, 1f);
        private static readonly Color RingCostly = new Color(0.906f, 0.698f, 0.361f, 0.4f);
        private static readonly Color RingOff = new Color(0.906f, 0.698f, 0.361f, 0f);

        private static readonly Color CoreLit = new Color(1f, 0.86f, 0.62f, 1f);
        private static readonly Color CoreOff = new Color(1f, 0.86f, 0.62f, 0f);
        private static readonly Color AuraLit = new Color(0.949f, 0.686f, 0.298f, 0.55f);
        private static readonly Color AuraOff = new Color(0.949f, 0.686f, 0.298f, 0f);

        // AN UNAUTHORED PATH IS DRAWN IN ITS OWN GREY. uGUI has no grayscale
        // filter, so the stones for it are a separately baked ash variant and
        // this only carries the 55% the design asks for.
        private static readonly Color UnauthoredTint = new Color(1f, 1f, 1f, 0.55f);

        // THE LIT SPHERE IS TURNED DOWN TO 70%, and the number is fitted rather
        // than judged.
        //
        // The design's ember is mostly DARK: across its disc the median pixel
        // sits at luma 21 and only the veins and a small heart are bright. The
        // delivered PNG drawn at full white has a median of 27 and far more of
        // the disc above 120, so it reads as a ball of fire rather than as a
        // stone with fire inside it.
        //
        // 0.70 is the multiple that minimises quantile-RMS error against the
        // design ember's own luma distribution, sampled off its recording --
        // 18.3, against 31.8 at full white. The white HEART the darkening costs
        // is what the core overlay puts back, which is why that overlay's alpha
        // and this were settled together.
        //
        // The residual 18.3 is NOT fixable here: the delivered sprite's veins
        // are denser and thicker than the prototype's, and no uniform multiply
        // moves vein coverage. That is an art difference, not a compositing one.
        private static readonly Color LitTint = new Color(0.70f, 0.70f, 0.70f, 1f);

        private static readonly Color LabelLit = new Color(0.929f, 0.902f, 1f, 1f);
        private static readonly Color LabelReady = new Color(0.949f, 0.859f, 0.620f, 1f);
        private static readonly Color LabelQuiet = new Color(0.557f, 0.498f, 0.690f, 1f);
        private static readonly Color PriceReady = new Color(0.949f, 0.859f, 0.620f, 1f);
        private static readonly Color PriceShort = new Color(0.761f, 0.376f, 0.227f, 1f);

        private static readonly Color CollarFill = new Color(0.906f, 0.698f, 0.361f, 0.85f);
        private static readonly Color CollarPassed = new Color(0.949f, 0.859f, 0.620f, 0.28f);
        private static readonly Color CollarTrack = new Color(0.443f, 0.376f, 0.588f, 0.55f);
        private static readonly Color CollarOff = new Color(0.443f, 0.376f, 0.588f, 0f);

        private int _path;
        private int _character;
        private int _selectedSlot = -1;
        private bool _wired;

        // The slide, in progress. Held rather than run as a coroutine so a
        // second press mid-slide retargets it instead of starting a second one
        // that fights the first for the same rect.
        private float _slideElapsed;
        private int _slideFrom;
        private bool _sliding;

        private void Start()
        {
            Wire();

            // Closed on arrival. The screen builds it Inactive, but a scene
            // re-entered mid-session arrives with whatever the last frame left,
            // and a confirmation dialog is the worst thing to inherit.
            CloseRespec();

            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < orbs.Length; i++)
            {
                int index = i;
                orbs[i].onClick.AddListener(() => OnOrbPressed(index));
            }

            prevPathButton.onClick.AddListener(() => StepPath(-1));
            nextPathButton.onClick.AddListener(() => StepPath(1));
            prevCharacterButton.onClick.AddListener(() => StepCharacter(-1));
            nextCharacterButton.onClick.AddListener(() => StepCharacter(1));
            investButton.onClick.AddListener(Kindle);
            if (respecButton != null) respecButton.onClick.AddListener(OpenRespec);
            if (respecConfirmButton != null) respecConfirmButton.onClick.AddListener(ConfirmRespec);
            if (respecCancelButton != null) respecCancelButton.onClick.AddListener(CloseRespec);
            backButton.onClick.AddListener(() => Navigation.Go(Navigation.Hub));
        }

        // ---- who and where ---------------------------------------------------

        private List<Character> Roster =>
            SaveSlotManager.CurrentSave?.roster ?? new List<Character>();

        private Character Current =>
            Roster.Count == 0 ? null : Roster[Mathf.Clamp(_character, 0, Roster.Count - 1)];

        private HashSet<string> Unlocked =>
            new HashSet<string>(Current?.unlockedTalentIds ?? new List<string>());

        // THE SELECTED CHARACTER'S embers, not the profile's. Reading a shared
        // pool here is what let a character you never fielded be kindled to the
        // top of their tree out of someone else's earnings.
        private int Embers => Current?.embers ?? 0;

        // AND WHAT THEY MAY STILL COMMIT, which is not the same number. The
        // wallet above accumulates across runs and is uncapped; this is what is
        // left of ContentDatabase.EmberSpendCap -- a per-character lifetime
        // budget of 30 that used to be enforced only inside the balance bot's
        // preset builder, so the screen would happily kindle past it.
        private int Budget => ContentDatabase.EmbersLeftFor(Current);

        // ---- input -----------------------------------------------------------

        private void StepPath(int direction)
        {
            int next = ConstellationLayout.Step(_path, direction, TalentPage.PathCount);
            if (next == _path) return;

            // Slid from wherever it currently IS, not from the last settled
            // page: pressing twice quickly should carry on from mid-flight
            // rather than snapping back and starting again.
            _slideFrom = _path;
            _path = next;
            _slideElapsed = 0f;
            _sliding = true;

            _selectedSlot = -1;
            AimPushIn();
            Refresh();
        }

        private void StepCharacter(int direction)
        {
            if (Roster.Count <= 1) return;

            _character = ConstellationLayout.Step(_character, direction, Roster.Count);

            // A different character's constellations are a different tree, so
            // the selection cannot survive the switch -- it would describe an
            // orb that is no longer under the cursor.
            _selectedSlot = -1;
            AimPushIn();
            Refresh();
        }

        private void OnOrbPressed(int index)
        {
            int path = index / TalentScreen.OrbCount;
            int slot = index % TalentScreen.OrbCount;

            // Only the page being LOOKED AT is clickable. The other two are
            // sitting off-screen with live buttons on them, and a stray click
            // landing there would kindle something the player cannot see.
            if (path != _path) return;

            _selectedSlot = slot;
            AimPushIn();
            Refresh();
        }

        // THE CHARACTER'S OWN TREE, resolved from content.
        //
        // Rebuilt when the character changes rather than per repaint: it walks
        // every talent the character has and costs each one, which is not work
        // to repeat on every hover. Cached against the id it was built for, so
        // switching character and switching back cannot leave a stale tree
        // describing somebody else's talents.
        private TalentTree _tree = TalentTree.None;
        private string _treeFor;

        private TalentTree Tree
        {
            get
            {
                var character = Current;
                if (character == null) return TalentTree.None;

                if (_treeFor == character.definitionId) return _tree;

                _tree = BuildTree(character);
                _treeFor = character.definitionId;
                return _tree;
            }
        }

        // MOVED to TalentOps.BuildTree so something that is not a
        // MonoBehaviour can resolve a character's tree -- the balance bot
        // needs it to buy talents for a profile preset. Unchanged on the way
        // down; this stays as the name this file already asks.
        private static TalentTree BuildTree(Character character) => TalentOps.BuildTree(character);

        private void Kindle()
        {
            var character = Current;
            if (character == null || _selectedSlot < 0) return;

            var save = SaveSlotManager.CurrentSave;
            if (save == null) return;

            // The refusal check, the id write and the ember spend are all
            // TalentOps.Kindle now -- see its header. This keeps only what is
            // actually the screen's: the beat, the save write and the repaint.
            if (!TalentOps.Kindle(character, Tree, _path, _selectedSlot)) return;

            // STARTED BEFORE THE REPAINT. Refresh below paints this stone lit,
            // and the beat is what carries it from ash to lit -- so the beat
            // has to already own the stone by the time the paint lands, or the
            // stone snaps and the beat plays over the top of an event that
            // already happened.
            BeginKindling(_path, _selectedSlot);

            // Written immediately. A talent tree that loses a kindled orb to a
            // crash is the single least forgivable thing this screen could do.
            SaveSlotManager.SaveCurrent();
            Refresh();

            // PRIMED HERE, not left for the next Update(). Refresh() above
            // just painted the settled GlowTaken state (PaintOrbs has no idea
            // a beat is running), and the earliest Update() can override that
            // with the beat's own t=0 frame is next frame -- a whole frame
            // where the stone reads as already lit before the beat has drawn
            // once. A zero-delta call folds the override into THIS frame,
            // same call, so the beat owns the paint from the instant it
            // exists rather than from one frame later.
            DriveKindling(0f);
        }

        // WHETHER THIS CHARACTER HAS COLLECTED THEIR RESPEC, asked in one
        // place because three separate controls on this screen ask it (the
        // button's visibility, opening the dialog, and confirming it) and
        // three copies of a rule is how one of them gets left behind.
        //
        // THEIR OWN TRACK, AND THE WATERMARK, not the shared table and not
        // `level`. Both halves changed with docs/PLAN_REWARD_TRACKS.md: the
        // track is per-character now, and every reward on it is COLLECTED
        // rather than merely reached -- so a character at level 40 who has
        // never opened the reward screen has not got a respec yet, and the
        // collect button is what hands it over.
        //
        // HasUnlocked rather than CollectedTotal(...) > 0, which is what the
        // plan's read-site table says: a respec is a capability with no
        // number, so its authored Amount is 0 and the sum can never be
        // positive however many are collected.
        private static bool HasEarnedARespec(Character character) =>
            RewardTracks.For(character).HasUnlocked(TrackReward.Respec, character.claimedTrackLevel);

        // Gives back everything this character has committed, so they can
        // spend it again. Level 20 of the reward track.
        //
        // IT ASKS FIRST NOW. One press used to clear all three constellations
        // outright, with nothing between the pointer and twenty-one dark
        // stones. The handoff settled both halves: the respec stays FREE --
        // charging for it taxes experimenting with a system whose whole point
        // is experimenting -- and it confirms.
        private void OpenRespec()
        {
            var character = Current;
            if (character == null || respecDialog == null) return;
            if (!HasEarnedARespec(character)) return;

            int refund = ContentDatabase.SpentBy(character);
            int stars = character.unlockedTalentIds?.Count ?? 0;

            if (respecDialogBody != null)
            {
                respecDialogBody.SetContent(UiStrings.TalentRespecPrompt.Format(stars, refund));
            }

            respecDialog.SetShown(true);
        }

        private void CloseRespec()
        {
            if (respecDialog != null) respecDialog.SetShown(false);
        }

        // BOTH CURRENCIES, because there are two: embers committed to orbs, and
        // stat points placed into ability scores. Character.Respec owns the
        // refund itself -- including the rule that event-taught skills are NOT
        // stripped -- and this supplies the one figure the model cannot compute
        // for itself: what the talents actually cost, which is a content
        // question (ContentDatabase.SpentBy walks OrbCost).
        //
        // The stat half currently refunds nothing in practice, and it is here
        // anyway: nothing in the game can SPEND a stat point yet
        // (AUDIT.md #53, Character.Invest has no production caller), so the
        // invested block is always zero. Written now rather than later because
        // the day a "+" appears on the dossier, a respec that quietly forgot
        // half its job is a bug nobody would look for.
        private void ConfirmRespec()
        {
            var character = Current;
            if (character == null) return;

            // Earned, not free-for-all. Checked here as well as on the button,
            // because a hidden button is a presentation fact and this is the
            // rule.
            if (!HasEarnedARespec(character)) return;

            // THROUGH TalentOps, not straight onto the model. A respec moves
            // max health twice over -- every talent's StatBonus and every
            // invested Constitution point -- and the run carries current health
            // as an absolute number against it. Character is Data and cannot
            // reach ScaleCarriedHealth; TalentOps.Respec is the Core half that
            // can, exactly as EquipmentOps is for a gear swap.
            TalentOps.Respec(character, ContentDatabase.SpentBy(character));

            // Every resting loop belonged to stones that are now ash, and the
            // selection describes one of them. Both go with the embers.
            _selectedSlot = -1;
            _kindlingSlot = -1;

            // Written immediately, for the same reason Kindle writes
            // immediately: a tree that loses its state to a crash is the least
            // forgivable thing this screen can do, and that cuts both ways.
            SaveSlotManager.SaveCurrent();
            CloseRespec();
            Refresh();
        }

        // ---- painting --------------------------------------------------------

        public void Refresh()
        {
            var character = Current;
            if (character == null) return;

            characterName.SetContent(character.definitionId);

            // HIDDEN until earned, not greyed out -- the same call the
            // Reckoning's reroll makes. A disabled button for a reward the
            // player has never heard of reads as something broken rather than
            // as something unearned.
            //
            // Interactable only when there is something to give back, so
            // pressing it is never a no-op the player has to interpret.
            if (respecButton != null)
            {
                bool earned = HasEarnedARespec(character);
                respecButton.SetShown(earned);
                if (earned)
                {
                    respecButton.interactable =
                        character.HasAnythingToRespec(ContentDatabase.SpentBy(character));
                    // Same seam as InvestButton above: RespecButton is themed
                    // (Violet) now, so the interactable flag needs the plate
                    // repainted explicitly.
                    respecButton.GetComponent<ThemedButtonState>()?.Refresh();
                }
            }

            var unlocked = Unlocked;
            pathName.Set(UiStrings.TalentPath, _path + 1, TalentPage.PathCount,
                TalentPage.SpentOn(Tree, _path, unlocked));

            PaintOrbs(unlocked);
            PaintDetail(unlocked);
            PaintMeter(character);

            // HIDDEN, not dimmed, when there is nobody to page to.
            //
            // Dimming was right while the roster was five and a squad might
            // field fewer: the arrows would come back, so keeping the screen
            // the same shape was worth more than removing two greyed controls.
            // The roster is ONE character now and the other four were removed
            // rather than postponed, so a permanently disabled arrow is not a
            // control between uses -- it is an advertisement for characters
            // that do not exist, which is exactly what this project keeps
            // having to unpick.
            //
            // The shape argument still holds for a roster that grows: this
            // hides only at one, so a second character brings both back
            // without anything else changing.
            bool canPage = Roster.Count > 1;

            prevCharacterButton.interactable = canPage;
            nextCharacterButton.interactable = canPage;

            if (prevCharacterButton.gameObject.activeSelf != canPage)
            {
                prevCharacterButton.gameObject.SetActive(canPage);
            }
            if (nextCharacterButton.gameObject.activeSelf != canPage)
            {
                nextCharacterButton.gameObject.SetActive(canPage);
            }

            // THE ARROWS FIND THE ENDS. Paging is a clamped line, not a ring,
            // so an arrow with nowhere to go says where the player is standing.
            prevPathButton.interactable = ConstellationLayout.CanStep(_path, -1, TalentPage.PathCount);
            nextPathButton.interactable = ConstellationLayout.CanStep(_path, 1, TalentPage.PathCount);
        }

        // ---- the states --------------------------------------------------
        //
        // The domain answers with seven refusals and the design names six
        // materials; the seventh, an allegiance sworn elsewhere, is drawn as
        // ash like the other two shut states, because the stone is shut and no
        // amount of paint would say WHY -- the kicker and the refusal line do
        // that. Nothing here tints a stone into another state: the variants are
        // baked, so Image.color only ever multiplies white, and the one
        // exception says why in its own comment.
        private Sprite SpriteFor(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return orbLitSprite;
                case TalentPage.Refusal.None: return orbReachableSprite;
                case TalentPage.Refusal.NotEnoughEmbers: return orbCostlySprite;
                case TalentPage.Refusal.NotAuthored: return orbAshUnauthoredSprite;

                // GATED AND LOCKED ARE THE SAME MATERIAL. A stone whose path is
                // short of its gate and one whose parent is dark are both ash,
                // and neither carries a price -- in neither case is the price
                // what is in the way. What separates them is the collar, which
                // only the gated pair wears.
                default: return orbAshSprite;
            }
        }

        private static bool Has<T>(T[] array, int index) where T : Object =>
            array != null && index >= 0 && index < array.Length && array[index] != null;

        private void PaintOrbs(HashSet<string> unlocked)
        {
            var tree = Tree;

            // Read once for the whole pass rather than per stone: it walks
            // every talent this character owns and prices each one, and 63
            // stones would ask for the same answer 63 times.
            int budget = Budget;

            for (int path = 0; path < TalentPage.PathCount; path++)
            {
                int spent = TalentPage.SpentOn(tree, path, unlocked);

                for (int slot = 0; slot < TalentScreen.OrbCount; slot++)
                {
                    int index = TalentScreen.OrbIndex(path, slot);
                    if (index >= orbs.Length) continue;

                    var refusal = TalentPage.Evaluate(tree, path, slot, unlocked, Embers, budget);
                    bool taken = refusal == TalentPage.Refusal.AlreadyTaken;
                    bool reachable = refusal == TalentPage.Refusal.None;
                    bool costly = refusal == TalentPage.Refusal.NotEnoughEmbers;
                    bool unauthored = refusal == TalentPage.Refusal.NotAuthored;

                    // AN UNAUTHORED PATH KEEPS ITS SHAPE. It used to be hidden
                    // outright, which left the third constellation as an empty
                    // sky; the design draws the spire in full-grey stones at
                    // 55% and says so in the panel. The shape ships even though
                    // the content does not -- what a player learns from it is
                    // that there IS a third path.
                    orbs[index].SetShown(true);

                    var image = orbs[index].targetGraphic as Image;
                    if (image != null)
                    {
                        var sprite = SpriteFor(refusal);
                        if (sprite != null) image.sprite = sprite;

                        image.color = unauthored ? UnauthoredTint
                            : taken ? LitTint
                            : Color.white;
                    }

                    // The warm drop-shadow, and only under a kindled stone.
                    if (Has(orbGlows, index)) orbGlows[index].color = taken ? GlowTaken : GlowOff;

                    // THE RING IS AN INVITATION and it is the only pulse on the
                    // screen, so nothing else may wear one: full accent while a
                    // stone can be bought, 40% while it is affordable-later,
                    // nothing at all otherwise.
                    if (Has(orbRings, index))
                    {
                        orbRings[index].color = reachable ? RingReady : costly ? RingCostly : RingOff;
                    }

                    // The molten core, and the aura around it. Painted here
                    // only to switch them on and off -- the flicker itself is a
                    // resting loop, and a loop this reset would restart every
                    // time the player moved the selection.
                    if (Has(orbCores, index))
                    {
                        orbCores[index].color = taken ? CoreLit : CoreOff;

                        // The flicker scales this rect, and an unlit core is
                        // invisible rather than absent -- so a respec that
                        // catches one mid-flare would leave it parked at 1.34
                        // and the next kindling would start from the wrong size.
                        if (!taken) orbCores[index].rectTransform.localScale = Vector3.one;
                    }
                    if (Has(orbAuras, index)) orbAuras[index].color = taken ? AuraLit : AuraOff;

                    // Everything stays PRESSABLE, including what cannot be
                    // afforded: pressing a stone selects it and the panel is
                    // where the refusal is explained. An uninteractable stone
                    // can only say "no" by doing nothing.
                    orbs[index].interactable = path == _path;

                    PaintOrbLabels(index, tree, path, slot, refusal);
                }

                PaintCollars(tree, path, spent);
            }

            PaintEdges(unlocked);
        }

        // A NAME UNDER EVERY STONE, A PRICE UNDER ALMOST NONE.
        //
        // The design's rule, and the reason for it: labelling all 21 prices
        // turned the sky back into a spreadsheet. A number is drawn only where
        // it changes a decision -- a stone that can be bought, one that is
        // short of embers, or the one the player has selected.
        private void PaintOrbLabels(int index, TalentTree tree, int path, int slot,
                                    TalentPage.Refusal refusal)
        {
            var here = tree.At(path, slot);

            if (Has(orbLabels, index))
            {
                orbLabels[index].SetContent(here.Exists ? here.Name : string.Empty);
                orbLabels[index].color =
                    refusal == TalentPage.Refusal.AlreadyTaken ? LabelLit
                    : refusal == TalentPage.Refusal.None ? LabelReady
                    : LabelQuiet;
            }

            if (!Has(orbPrices, index)) return;

            bool selected = path == _path && slot == _selectedSlot;
            bool shows = refusal == TalentPage.Refusal.None
                         || refusal == TalentPage.Refusal.NotEnoughEmbers
                         || selected;

            // A FREE SLOT SAYS NOTHING rather than showing a nought. The
            // convergence and the capstone cost zero embers and are gated
            // instead; reaching them is the price, and "0" would read as a
            // bargain rather than as a different kind of cost.
            if (!shows || refusal == TalentPage.Refusal.AlreadyTaken || here.Cost <= 0)
            {
                orbPrices[index].SetContent(string.Empty);
                return;
            }

            orbPrices[index].SetContent(here.Cost.ToString());
            orbPrices[index].color =
                refusal == TalentPage.Refusal.NotEnoughEmbers ? PriceShort : PriceReady;
        }

        // THE COLLAR IS A MEASURED ARC, DRAWN IN EVERY STATE.
        //
        // uGUI's Image.type Filled on Radial360 IS the design's conic gradient:
        // an arc from twelve o'clock filled to spent/required. It needs no
        // shader to say, and it STEPS rather than tweens -- in the same frame
        // as the stone it belongs to, so the two read as one event rather than
        // as a consequence arriving late.
        //
        // Drawn even where the stone is unreachable, which is the whole point:
        // a player reads how far a path is from its gate long before the shape
        // of the tree lets them buy anything up there.
        private void PaintCollars(TalentTree tree, int path, int spent)
        {
            if (collarSlots == null) return;

            for (int i = 0; i < collarSlots.Length; i++)
            {
                int index = collarSlots[i];
                if (TalentScreen.PathOf(index) != path) continue;

                var here = tree.At(path, TalentScreen.SlotOf(index));
                int required = here.MinSpent;

                // NO GATE, NO COLLAR, AND THAT INCLUDES AN UNWRITTEN PATH.
                //
                // A slot with nothing authored behind it answers MinSpent 0
                // like any ungated stone -- which drew a full, bright, met
                // collar around both singles of the third constellation: a ring
                // saying "gate passed" on a path that has no gate and no
                // content, and the brightest thing on a screen that is supposed
                // to read as unwritten.
                bool gated = here.Exists && required > 0;
                bool met = gated && spent >= required;

                if (Has(collarFills, i))
                {
                    var fill = collarFills[i];
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Radial360;
                    fill.fillOrigin = (int)Image.Origin360.Top;
                    fill.fillClockwise = true;
                    fill.fillAmount = gated ? Mathf.Clamp01(spent / (float)required) : 0f;

                    // WHEN THE GATE IS MET THE RUN FINISHES PALE and the track
                    // behind it goes: there is no longer a distance to read,
                    // and a full bright ring is a ring still asking for
                    // something. Pale rather than gone, because the collar is
                    // also how a player remembers WHICH two stones were gated.
                    fill.color = !gated ? CollarOff : met ? CollarPassed : CollarFill;
                }

                if (Has(collarTracks, i)) collarTracks[i].color = gated && !met ? CollarTrack : CollarOff;

                if (Has(collarCounts, i))
                {
                    // Blanked once met. "9/9" is a sum with nothing left to say,
                    // and it was landing on top of the label of the stone below.
                    collarCounts[i].SetContent(gated && !met ? spent + "/" + required : string.Empty);
                    collarCounts[i].color = LabelQuiet;
                }
            }
        }

        // THE PATH BEHIND YOU LIGHTS UP.
        //
        // An edge is lit when its CHILD is invested -- the parent necessarily
        // already is, because that is what a prerequisite means, so the child
        // alone answers it and nothing here has to re-walk the skeleton.
        //
        // The edges were pure decoration before this: three paths' worth of
        // limbs that never changed whatever the player spent, so a tree with
        // twenty orbs invested looked exactly like an empty one apart from the
        // orbs themselves. The climb is the thing the screen is about.
        private void PaintEdges(HashSet<string> unlocked)
        {
            var tree = Tree;

            if (edgeGlows == null || edgeChildSlots == null) return;

            // FROM THE SKELETON, NOT FROM THE ARRAY'S LENGTH.
            //
            // This divided the array length by the path count to work out how
            // many edges a path has, which is only correct while the array is
            // exactly the right size -- and silently wrong in the worst way
            // when it is not. One short array and every edge shifts to a
            // different path, lighting connections on trees the player has
            // never touched, with nothing logged.
            //
            // TalentSkeleton.EdgesPerPath is the same number derived from the
            // graph itself, so a mismatch is now a disagreement between two
            // sources that can be SEEN rather than a quiet reinterpretation of
            // one. The same reason the layout derives its waist from the
            // skeleton instead of writing 4.
            int perPath = Domain.Talents.TalentSkeleton.EdgesPerPath;
            int count = Mathf.Min(edgeGlows.Length, edgeChildSlots.Length);

            if (perPath <= 0 || count < perPath * TalentPage.PathCount)
            {
                Debug.LogWarning(
                    $"[TalentController] The tree has {count} edges wired but the skeleton describes " +
                    $"{perPath * TalentPage.PathCount}. Leaving them dark rather than lighting the " +
                    "wrong ones.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (edgeGlows[i] == null) continue;

                // The edges are built path by path in the same order the orbs
                // are, so which path an edge belongs to is its index over the
                // per-path count.
                int path = i / perPath;
                int slot = edgeChildSlots[i];

                // An edge is lit when its CHILD is invested, and an edge into
                // an unauthored slot can never be.
                string id = tree.IdAt(path, slot);
                bool lit = !string.IsNullOrEmpty(id) && unlocked.Contains(id);
                edgeGlows[i].SetShown(lit);
            }
        }

        // ---- the panel -------------------------------------------------------
        //
        // A COLUMN THAT IS ALWAYS THERE. The plate it replaced appeared and
        // disappeared with the selection, which made the screen change shape as
        // the pointer moved; this one is furniture, and says what to do with it
        // when nothing is picked.
        private void PaintDetail(HashSet<string> unlocked)
        {
            var tree = Tree;

            if (_selectedSlot < 0)
            {
                detailName.SetContent(string.Empty);
                panelKicker.SetContent(UiStrings.TalentPickPrompt.Template);
                panelPrice.SetContent(string.Empty);
                detailBody.SetContent(UiStrings.TalentPickBody.Template);
                panelRefusal.SetContent(string.Empty);
                investButton.SetShown(false);
                return;
            }

            var here = tree.At(_path, _selectedSlot);
            var refusal = TalentPage.Evaluate(tree, _path, _selectedSlot, unlocked, Embers, Budget);

            // AN UNAUTHORED SLOT STILL ANSWERS, because its stone is drawn now
            // and a stone you can press has to say something back. What it says
            // is that the path exists and is not written yet -- which is true,
            // and better than a panel that goes blank for no stated reason.
            if (refusal == TalentPage.Refusal.NotAuthored)
            {
                detailName.SetContent(UiStrings.TalentUnwrittenName.Template);
                panelKicker.SetContent(KickerFor(refusal));
                panelPrice.SetContent(string.Empty);
                detailBody.SetContent(UiStrings.TalentUnwrittenBody.Template);
                panelRefusal.SetContent(string.Empty);
                investButton.SetShown(false);
                return;
            }

            investButton.SetShown(true);

            // THE TALENT'S OWN NAME AND WORDS. This printed
            // TalentSkeleton.Kind -- literally "NORMAL", "MERGE" or "CAP" --
            // because the screen had no way to reach the content at all.
            detailName.SetContent(here.Name);
            detailBody.SetContent(here.Description);

            // THE KICKER IS THE STATE IN WORDS. The stone's own material says
            // it too, but a material is a thing you learn and a word is a thing
            // you read -- and on the first visit nothing has been learned yet.
            panelKicker.SetContent(KickerFor(refusal));

            // The two free orbs price themselves in distance rather than in
            // embers, so they say the gate instead of a number.
            panelPrice.SetContent(
                here.Cost > 0 ? UiStrings.TalentPriceEmbers.Format(here.Cost)
                : here.MinSpent > 0 ? UiStrings.TalentPriceGate.Format(here.MinSpent)
                : string.Empty);

            // THE REFUSAL, on its own line and in its own colour. A stone that
            // cannot be bought says why here, rather than by doing nothing when
            // it is pressed. It sits BESIDE the description rather than over
            // it: being refused and being unable to read what you were refused
            // is two problems.
            panelRefusal.SetContent(Explain(refusal));

            investButton.interactable = refusal == TalentPage.Refusal.None;
            // InvestButton wears a theme's plate now (see TalentScreen.
            // BuildPanel) -- Button.interactable alone does not repaint it;
            // ThemedButtonState.Refresh() reads the flag back into the
            // plate/glow tint the way OnEnable already does on activation.
            investButton.GetComponent<ThemedButtonState>()?.Refresh();
            investLabel.Set(LabelFor(refusal));
        }

        // The committed meter, drawn as a length because a proportion is a
        // length and a pair of numbers is not.
        //
        // AGAINST WHAT HAS BEEN EARNED, NOT AGAINST THE CAP -- and the reason
        // is narrower than this comment used to claim. It said "there is no
        // such ceiling in this game", which was wrong even when it was written:
        // ContentDatabase.EmberSpendCap is the design's 30 and is now enforced
        // on this very screen (TalentPage.Refusal.BudgetSpent). What is
        // uncapped is EARNING -- the wallet accumulates across runs with no
        // limit -- and a bar drawn against 30 would fill at the cap and then
        // keep being full while the player kept gathering. Committed over
        // committed-plus-held is the same shape and is a real quantity: empty
        // when nothing is spent, full when everything is. Changing the
        // denominator to the cap is a design decision, not a bug fix, so this
        // stays as it is and says so.
        private void PaintMeter(Character character)
        {
            emberCount.Set(UiStrings.TalentEmbers, Embers);

            if (panelMeterFill == null) return;

            int spent = ContentDatabase.SpentBy(character);
            int total = spent + Embers;
            float fraction = total <= 0 ? 0f : Mathf.Clamp01(spent / (float)total);

            panelMeterFill.sizeDelta = new Vector2(
                ConstellationLayout.PanelInnerWidth * fraction,
                panelMeterFill.sizeDelta.y);
        }

        // The refusal the player is told about is the FIRST one that applies,
        // in the order Domain ranks them -- being told "not enough Embers" for
        // an orb three tiers out of reach sends someone to farm a currency they
        // did not need.
        private static string Explain(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.PrerequisiteMissing:
                    return UiStrings.TalentWhyLocked.Template;
                case TalentPage.Refusal.Gated:
                    return UiStrings.TalentWhyGated.Template;
                case TalentPage.Refusal.NotEnoughEmbers:
                    return UiStrings.TalentWhyPoor.Template;

                // FOUR REASONS NOW, and this one is the only one that names a
                // decision the player already made rather than something they
                // have yet to do.
                case TalentPage.Refusal.AllegianceSworn:
                    return UiStrings.TalentWhySworn.Template;

                // THE NUMBER COMES FROM THE CONSTANT rather than from the
                // sentence, so the copy cannot drift from the cap the way the
                // meter's own comment below did.
                case TalentPage.Refusal.BudgetSpent:
                    return UiStrings.TalentWhySpent.Format(ContentDatabase.EmberSpendCap);
                default:
                    return string.Empty;
            }
        }

        private static string KickerFor(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return UiStrings.TalentKickerLit.Template;
                case TalentPage.Refusal.None: return UiStrings.TalentKickerReady.Template;
                case TalentPage.Refusal.NotEnoughEmbers: return UiStrings.TalentKickerCostly.Template;
                case TalentPage.Refusal.Gated: return UiStrings.TalentKickerGated.Template;
                case TalentPage.Refusal.NotAuthored: return UiStrings.TalentKickerUnwritten.Template;
                case TalentPage.Refusal.AllegianceSworn: return UiStrings.TalentKickerSworn.Template;
                case TalentPage.Refusal.BudgetSpent: return UiStrings.TalentKickerSpent.Template;
                default: return UiStrings.TalentKickerLocked.Template;
            }
        }

        private static UiString LabelFor(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return UiStrings.TalentTaken;
                case TalentPage.Refusal.PrerequisiteMissing: return UiStrings.TalentLocked;
                case TalentPage.Refusal.Gated: return UiStrings.TalentLocked;
                case TalentPage.Refusal.NotEnoughEmbers: return UiStrings.TalentNoEmbers;

                // LOCKED rather than a word of its own. The button is the one
                // place on this screen with no room to explain itself, and the
                // kicker and the refusal line beside it both say which lock
                // this is. A default of KINDLE on a stone that cannot be
                // kindled is the failure this arm exists to stop.
                case TalentPage.Refusal.AllegianceSworn: return UiStrings.TalentLocked;
                case TalentPage.Refusal.BudgetSpent: return UiStrings.TalentLocked;
                default: return UiStrings.TalentInvest;
            }
        }
    }
}
