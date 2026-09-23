using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Fills the character dossier, and drives the one mechanic it is built
    // around: hover an attribute, see what it is actually doing for you.
    //
    // The numbers come from the SAME ContentDatabase.Effective* calls the fight
    // uses, never from a second computation -- a sheet that recomputed its own
    // figures would eventually disagree with the battle, and the player would be
    // right either way.
    public class CharacterDossierController : MonoBehaviour, INavPaneEntry, INavCancelClaim, INavSectionStrip
    {
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text subLine;
        [SerializeField] internal RectTransform xpFill;
        [SerializeField] internal TMP_Text xpRemaining;

        // What the next reward-track level pays. See PaintTrackLine.
        [SerializeField] internal TMP_Text trackNext;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;

        [SerializeField] internal TMP_Text skillsCount;
        [SerializeField] internal Button packRow;

        // The reward track's row and the panel it opens. Same pair as the pack
        // below: a nav row and a panel that closes on its own button.
        [SerializeField] internal Button trackRow;
        [SerializeField] internal GameObject trackPanel;

        // THE PANEL'S OWN CONTROLLER, not just the GameObject it lives on --
        // so ShowTrack can tell it WHICH character to paint before it opens
        // (docs/archive/PLAN_REWARD_TRACKS.md §8). Assigned by ScreenRegistry
        // (ScreenRegistry.WireSystemMenu), which is the one place that has
        // both wiring calls' return values in hand; not auto-bound, because
        // it is a controller reference rather than one of UiAutoBind's five
        // node types.
        [SerializeField] internal RewardTrackController trackScreen;
        [SerializeField] internal TMP_Text packChevron;
        [SerializeField] internal GameObject packPanel;
        [SerializeField] internal Button packCloseButton;

        // The spell-books panel (docs/PLAN_SHOP.md §1g, gate 3). Same
        // row+panel+close shape as Pack/Track above.
        [SerializeField] internal Button spellsRow;
        [SerializeField] internal TMP_Text spellsChevron;
        [SerializeField] internal TMP_Text spellsCount;
        [SerializeField] internal GameObject spellsPanel;
        [SerializeField] internal Button spellsCloseButton;
        [SerializeField] internal Button[] spellSlots;
        [SerializeField] internal TMP_Text[] spellSlotNames;
        [SerializeField] internal Image[] spellSlotSelections;
        [SerializeField] internal Image[] spellSlotIcons;
        [SerializeField] internal TMP_Text spellsNoBooksLine;
        [SerializeField] internal GameObject unassignedEmptyHint;
        [SerializeField] internal Button[] unassignedRows;
        [SerializeField] internal TMP_Text[] unassignedNames;
        [SerializeField] internal Image[] unassignedSelections;

        // The skills panel (owner bug report, 2026-09-19: "Skills in the
        // char menu, when you click on it, nothing happens" -- there was no
        // skillsRow field here for UiAutoBind to fill, and no pane to open).
        // Same row+panel+close shape as Spells above, but read-only: NO
        // per-entry Button field, because nothing in this pane takes a
        // press -- see DeclareSkills.
        [SerializeField] internal Button skillsRow;
        [SerializeField] internal TMP_Text skillsChevron;
        [SerializeField] internal GameObject skillsPanel;
        [SerializeField] internal Button skillsCloseButton;
        [SerializeField] internal GameObject skillsEmptyHint;
        [SerializeField] internal TMP_Text[] skillNames;
        [SerializeField] internal TMP_Text[] skillDescriptions;

        // The fight's copy is readable and inert; every other copy is live.
        //
        // Decided at BUILD time rather than sniffed at runtime, which is the
        // arrangement CharacterOverlayController already used for exactly this
        // -- gear is locked for the duration of a battle, and the sheet the
        // fight shows must not be able to change the fight it is describing.
        // Putting C and I on this menu made that invariant this screen's
        // problem: before, those keys reached a panel that had already been
        // wired inert.
        [SerializeField] internal bool lockedForFight;

        // Public because the serialized field is internal and the PlayMode test
        // assembly cannot see internals. Same name the overlay exposed, so the
        // test that pins "the fight's copy is inert" reads unchanged.
        public bool EquipLocked => lockedForFight;

        // The menu this dossier lives in -- same shape as ExitsController.menu,
        // and for the same reason: a second serialized bool here would be one
        // more thing that can disagree with the menu's own state. Set by
        // ScreenRegistry.WireSystemMenu, not part of WireDossier's signature,
        // because it is not carried by WireDossier's own parameters.
        //
        // Gates the dossier's refund minus: only the hub copy of this screen
        // (InDescent false) shows it, so a build revised mid-run can only be
        // reconsidered between descents, never undone inside one.
        [SerializeField] internal SystemMenuController menu;

        public bool InDescent => menu != null && menu.InDescent;

        [SerializeField] internal Button[] slotCells;
        [SerializeField] internal Image[] slotIcons;
        [SerializeField] internal TMP_Text[] slotLabels;
        [SerializeField] internal GameObject[] slotBlockedCaptions;

        [SerializeField] internal Button[] attributeCells;

        // One "+" per attribute cell, and the label saying how many points are
        // waiting. See Spend().
        [SerializeField] internal Button[] attributePluses;

        // One "-" per attribute cell, mirrored off the plus. See Refund().
        [SerializeField] internal Button[] attributeMinuses;
        [SerializeField] internal TMP_Text unspentPoints;
        [SerializeField] internal TMP_Text[] attributeValues;
        [SerializeField] internal TMP_Text[] attributeKeys;

        // The attributes panel (owner item: "dedicated attributes screen
        // showing stat effects, with mouse/gamepad allocation and
        // reallocation") -- see CharacterDossierScreen.AttributesPanel's own
        // comment for the shape. attributesRow opens it, the same
        // row-that-opens-a-panel idiom Pack/Spells/Skills/Track's own rows
        // already use.
        [SerializeField] internal Button attributesRow;
        [SerializeField] internal GameObject attributesPanel;
        [SerializeField] internal Button attributesCloseButton;
        [SerializeField] internal TMP_Text attributesUnspentPoints;
        [SerializeField] internal GameObject attributesLockNote;

        // ONE PER ABILITY SCORE, in _cellOrder's order (RefreshAttributes),
        // same as attributeCells above. attributeRowSteps is the row
        // ITSELF -- an AttributeRow (this file, below), attached by
        // ScreenRegistry.WireDossier the same way OptionRow is attached to
        // an Options row, so a focused row answers Left/Right as
        // refund/spend and Submit as spend without a second Selectable on
        // the same GameObject.
        [SerializeField] internal AttributeRow[] attributeRowSteps;
        [SerializeField] internal TMP_Text[] attributeRowNames;
        [SerializeField] internal TMP_Text[] attributeRowEffects;
        [SerializeField] internal Button[] attributeRowPluses;
        [SerializeField] internal Button[] attributeRowMinuses;

        [SerializeField] internal Image[] slotRarityTicks;

        // ITEM-MODIFIER PLAN PHASE E: a rolled item's RiftTier, distinct from
        // its Rarity tick above -- an inset ring drawn ON the icon rather
        // than a corner tick, so the two axes never compete for the same
        // pixel. Hidden (SetShown false) at RiftTier.Ordinary, which is the
        // regression guard: an item that rolled nothing must look EXACTLY
        // as it did before this phase. See RiftTierColors.ShouldGlow.
        [SerializeField] internal Image[] slotRiftGlows;

        [SerializeField] internal Button[] packCells;
        [SerializeField] internal Image[] packIcons;
        [SerializeField] internal Image[] packRarityTicks;
        [SerializeField] internal Image[] packRiftGlows;
        [SerializeField] internal TMP_Text[] packCounts;
        [SerializeField] internal TMP_Text[] packNames;
        [SerializeField] internal Button[] packSortTabs;
        [SerializeField] internal GameObject[] packSortUnderlines;
        [SerializeField] internal Image packScrollTrack;
        [SerializeField] internal RectTransform packScrollThumb;
        [SerializeField] internal TMP_Text carriedValue;

        [SerializeField] internal GameObject tooltip;
        [SerializeField] internal TMP_Text tooltipTitle;
        [SerializeField] internal TMP_Text tooltipBody;

        [SerializeField] internal TMP_Text[] statValues;
        [SerializeField] internal TMP_Text[] statPreviews;
        [SerializeField] internal GameObject[] statHighlights;

        // THE ROW LABELS, and exactly one of them is not static: the pool
        // row reads "Max Fury" for a character who does not hold mana (plan
        // P7). Everything else in this column is authored copy that no
        // character can change, and stays baked -- the array exists so ONE
        // row can be rewritten, not so the list becomes runtime text.
        // FightScreen.StatNames has declared these nodes since the sheet was
        // built; this is the first field to bind them.
        [SerializeField] internal TMP_Text[] statNames;

        // The portrait slot. The FACES ARE NOT BOUND HERE any more: they were
        // an IconEntry[] baked by ScreenRegistry from every character with a
        // portraitPath, which meant the scene held a photograph of the roster
        // taken at scene-build time and a character authored afterwards showed
        // an empty plate no content rebuild could fill. CharacterPortraits
        // loads them off Resources by id instead.
        [SerializeField] internal Image portrait;

        // Item art, bound at build time from every ItemDefinition with an icon.
        [SerializeField] internal IconEntry[] icons;

        private static readonly Color Neutral = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Text);
        private static readonly Color Dim = Hex(Domain.UiKit.Screens.CharacterDossierScreen.TextDim);
        private static readonly Color Accent = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Accent);
        private static readonly Color Good = Hex("#8FB37A");
        private static readonly Color Bad = Hex("#D99A8C");
        private static readonly Color AccentHi = Hex(Domain.UiKit.Screens.CharacterDossierScreen.AccentHi);

        private bool _wired;
        private int _index;

        // Which attribute sits in which cell. Rebuilt per character because the
        // handover wants the grid ordered highest-first, so the shape of a build
        // reads off the top row -- which means cell 0 is not always Strength.
        private readonly List<AbilityScore> _cellOrder = new List<AbilityScore>();

        // Who _cellOrder was sorted for. See RefreshAttributes -- the order is
        // FROZEN while one character is on screen, and this is how it knows the
        // character changed.
        private string _cellOrderFor;

        // Held so the tooltip can measure the NEXT point from what the
        // character actually has, rather than from a neutral block.
        private AbilityScoreBlock _scores;
        private AbilityScoreBlock _previewScores;

        // The character's LIVE weapon/spell scaling, held for the same reason
        // _scores is: the attributes panel's Strength/Intelligence lines
        // (AbilityEffectDescriptions.Strength/Intelligence) read these rather
        // than recomputing anything -- ContentDatabase.EffectiveWeaponScaling/
        // EffectiveSkillScaling are the SAME calls FightEncounterAdapter.
        // ToCombatant makes, so a sword's grade cannot show one number here
        // and a different one in the fight it is about to be used in.
        private ScalingSet _weaponScaling;
        private ScalingSet _skillScaling;

        // WHICH CAPACITY RULE THE SHOWN CHARACTER'S PRIMARY POOL USES, held
        // for the same reason _scores is: the sheet's hover mechanic and the
        // equip preview both have to know whether Wisdom moves the pool row
        // at all, and for a Fixed pool it does not (see SheetStats.FedBy).
        // WisdomDerived until a character is resolved -- that is what mana is
        // and what every character shipped today carries.
        private PoolCapacityRule _primaryPoolRule = PoolCapacityRule.WisdomDerived;

        // AND WHAT IT IS CALLED, threaded the same way and for the same
        // reason: the pool row's LABEL is the pool's own displayName, so a
        // Fury holder's sheet says "Max Fury" rather than naming a resource
        // they do not have. "Mana" until a character is resolved, matching
        // the label the scene bakes.
        private string _primaryPoolName = ManaDisplayNameFallback;

        private const string ManaDisplayNameFallback = "Mana";

        // AND WHETHER THAT POOL READS SPELL BOOKS (plan P6, gate 3), held
        // beside the other two because it is the same fact about the same
        // resolved row and RefreshSpells is reached both from Refresh and
        // directly from a row press. True until a character is resolved --
        // mana reads books, and that is what every character shipped today
        // carries.
        private bool _canHoldSpellBooks = true;

        // WHO OWNS THE ONE TOOLTIP BOX -- the pointer or the selection
        // (Core/TooltipFocusRouter, docs/GAMEPAD_NAVIGATION_PLAN.md section
        // 7). The three hover handlers below stay the ONE show/hide
        // implementation per surface; this decides which of them a given
        // pointer or focus event is allowed to reach.
        private readonly TooltipFocusRouter _tooltips = new TooltipFocusRouter();

        private void OnEnable()
        {
            Wire();
            Refresh();

            // The stack is replaced on every scene load, so the subscription
            // is per-enable rather than per-Wire (which runs once).
            _tooltips.Attach(NavigationInputModule.Contexts);
        }

        private void OnDisable() => _tooltips.Detach();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (prevCharacterButton != null) prevCharacterButton.onClick.AddListener(() => Step(-1));
            if (nextCharacterButton != null) nextCharacterButton.onClick.AddListener(() => Step(1));
            if (packRow != null) packRow.onClick.AddListener(TogglePack);

            // Wired ABOVE the lockedForFight guard, so the track opens from the
            // fight's copy of the dossier too. It changes nothing -- it is a
            // list of what levelling gives -- and a player checking what is
            // coming mid-battle is exactly when they would want to.
            if (trackRow != null) trackRow.onClick.AddListener(ShowTrack);
            if (packCloseButton != null) packCloseButton.onClick.AddListener(() => ShowPack(false));

            if (spellsRow != null) spellsRow.onClick.AddListener(ToggleSpells);
            if (spellsCloseButton != null) spellsCloseButton.onClick.AddListener(() => ShowSpells(false));

            // Wired ABOVE the lockedForFight guard too, same reasoning as
            // trackRow/spellsRow just above: this pane names what the
            // character can already do, nothing it opens edits the fight
            // it would be describing.
            if (skillsRow != null) skillsRow.onClick.AddListener(ToggleSkills);
            if (skillsCloseButton != null) skillsCloseButton.onClick.AddListener(() => ShowSkills(false));

            if (spellSlots != null)
            {
                for (int i = 0; i < spellSlots.Length; i++)
                {
                    int slot = i;
                    if (spellSlots[i] != null) spellSlots[i].onClick.AddListener(() => PressSlot(slot));
                }
            }

            if (unassignedRows != null)
            {
                for (int i = 0; i < unassignedRows.Length; i++)
                {
                    int row = i;
                    if (unassignedRows[i] != null) unassignedRows[i].onClick.AddListener(() => SelectUnassigned(row));
                }
            }

            // The attribute link, driven from hover AND from the button itself,
            // so a controller player reaches it too -- the handover asks for
            // that explicitly and it is one line here. (It was a hand-rolled
            // copy of AttachHovers' own loop until the selection path needed
            // registering on all three surfaces; there is one loop now.)
            AttachHovers(attributeCells, OnAttributeHover);
            AttachHovers(slotCells, OnSlotHover);
            AttachHovers(packCells, OnPackHover);

            // Opening/closing the attributes panel is VIEWING, same as the
            // hovers just above -- wired above the lockedForFight guard
            // below so the fight's own locked copy can still show what a
            // score does, even though spending or refunding a point inside
            // it (wired below the guard, with the rest of Pack/Spells/
            // Skills' own row+panel pairs) stays barred.
            if (attributesRow != null) attributesRow.onClick.AddListener(() => ShowAttributes(true));
            if (attributesCloseButton != null) attributesCloseButton.onClick.AddListener(() => ShowAttributes(false));

            // AUDIT.md #160: wired above the lockedForFight guard, same
            // reasoning as AttachHovers just above it -- the fight's own
            // locked copy of this sheet still takes Move/Submit (equipping is
            // what is barred, not looking), so it should still show where the
            // stick is standing.
            // THE THREE WireSelectHalos CALLS THAT STOOD HERE ARE GONE
            // (hardware round 1: "a player can't see where they're going in
            // the character sheets screen: no obvious selectors" -- a soft
            // glow the size of a cell was never going to be one). The arrow
            // Core/FocusMarker.cs draws needs nothing from this screen.
            //
            // The SelectIndex components those calls used to create are
            // still created, by AttachHovers/TooltipFocusRouter.Register a
            // few lines above -- the tooltip rides the same component and is
            // untouched by this. That is also why the halo subscription was
            // a `+=` rather than a `=`, and why removing it is a removal
            // rather than a replacement.

            // Clicking equips; clicking a worn slot takes it off. A screen that
            // could only put gear ON would be a trap, so both gestures exist --
            // the same pair the old sheet had.
            //
            // Not attached at all in a fight, rather than attached and refused:
            // a button that visibly does nothing invites the player to press it
            // again, and there is nowhere on this screen to explain why.
            if (lockedForFight) return;

            // THE "+" PER SCORE, which is what Character.Invest's own comment
            // has always described and what nothing ever built (AUDIT #53).
            // Below the lockedForFight guard for the same reason the equip
            // handlers are: a stat point spent mid-battle would change the
            // fight the sheet is describing.
            //
            // Indexed by CELL, not by AbilityScore. The grid is re-ordered per
            // character so the build reads highest-first, so which score a cell
            // holds is a runtime fact -- _cellOrder is the only thing that
            // knows, and reading it at click time rather than at wire time is
            // what keeps the "+" pointing at the score under it after a
            // character step.
            if (attributePluses != null)
            {
                for (int i = 0; i < attributePluses.Length; i++)
                {
                    if (attributePluses[i] == null) continue;

                    int cell = i;
                    attributePluses[i].onClick.AddListener(() => Spend(cell));
                }
            }

            // THE "-" PER SCORE, beside the plus for the same reasons: below
            // the lockedForFight guard, indexed by cell and resolved through
            // _cellOrder at click time.
            if (attributeMinuses != null)
            {
                for (int i = 0; i < attributeMinuses.Length; i++)
                {
                    if (attributeMinuses[i] == null) continue;

                    int cell = i;
                    attributeMinuses[i].onClick.AddListener(() => Refund(cell));
                }
            }

            // THE ATTRIBUTES PANEL'S OWN ROWS -- same index-by-cell,
            // resolve-through-_cellOrder-at-click-time rule as the compact
            // grid's plus/minus just above, so Spend(int)/Refund(int) never
            // need a second overload for this panel (edit-surface rule:
            // "Do not duplicate Spend/Refund").
            //
            // Submit fires the row's own onClick (Button's stock behaviour
            // -- AttributeRow adds nothing for it), so wiring that alongside
            // Right below keeps the two ways to spend from this row
            // (Submit, and Right) pointed at literally the same delegate.
            if (attributeRowSteps != null)
            {
                for (int i = 0; i < attributeRowSteps.Length; i++)
                {
                    if (attributeRowSteps[i] == null) continue;

                    int cell = i;
                    attributeRowSteps[i].onClick.AddListener(() => Spend(cell));
                    attributeRowSteps[i].OnLeftRight = delta =>
                    {
                        if (delta < 0) Refund(cell);
                        else if (delta > 0) Spend(cell);
                    };
                }
            }

            if (attributeRowPluses != null)
            {
                for (int i = 0; i < attributeRowPluses.Length; i++)
                {
                    if (attributeRowPluses[i] == null) continue;

                    int cell = i;
                    attributeRowPluses[i].onClick.AddListener(() => Spend(cell));
                    SetNoNavigation(attributeRowPluses[i]);
                }
            }

            if (attributeRowMinuses != null)
            {
                for (int i = 0; i < attributeRowMinuses.Length; i++)
                {
                    if (attributeRowMinuses[i] == null) continue;

                    int cell = i;
                    attributeRowMinuses[i].onClick.AddListener(() => Refund(cell));
                    SetNoNavigation(attributeRowMinuses[i]);
                }
            }

            if (packSortTabs != null)
            {
                for (int i = 0; i < packSortTabs.Length; i++)
                {
                    if (packSortTabs[i] == null) continue;
                    int key = i;
                    packSortTabs[i].onClick.AddListener(() => SortBy(key));
                }
            }

            // Dragging the thumb, through the same component the Options
            // sliders use -- it reports a 0..1 position on a bar, which is
            // exactly what a scrollbar is.
            if (packScrollTrack != null)
            {
                var bar = packScrollTrack.gameObject.AddComponent<BarSlider>();
                bar.Changed = ScrollToFraction;
            }

            for (int i = 0; i < packCells.Length; i++)
            {
                if (packCells[i] == null) continue;
                int index = i;
                packCells[i].onClick.AddListener(() => EquipFromPack(index));
            }

            for (int i = 0; i < slotCells.Length; i++)
            {
                if (slotCells[i] == null) continue;
                int index = i;
                slotCells[i].onClick.AddListener(() => UnequipSlot(index));
            }
        }

        // THE POINTER IS ONE CALLER OF `changed`, NOT `changed` ITSELF -- the
        // whole of job 1's shape on this screen. A HoverIndex (the mouse) and
        // a SelectIndex (the module, added by Register) both report into
        // TooltipFocusRouter, which decides which of them may call the
        // handler: focus outranks hover, and a pointer leaving a node the
        // stick is standing on changes nothing. Three surfaces, one
        // arbitration, and each handler below is still the only place that
        // screen's tooltip is built.
        private void AttachHovers(Button[] buttons, System.Action<int, bool> changed)
        {
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                int index = i;
                var node = buttons[i].gameObject;
                _tooltips.Register(buttons[i], entered => changed(index, entered));

                var hover = node.GetComponent<HoverIndex>() ?? node.AddComponent<HoverIndex>();
                hover.Index = index;
                hover.Changed = (_, entered) => _tooltips.Pointer(node, entered);
            }
        }

        // ---- navigation (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3b, item 1) -------
        //
        // FOUR GROUPS AND THE LINKS BETWEEN THEM, rewired at runtime like
        // every other surface in this phase (RuntimeNavWiring's own header
        // has the argument): which pack cells hold anything is a function of
        // the bag and the scroll offset, and whether the pack is open decides
        // what column A even is.
        //
        // TWO STATES, not one declaration with holes -- the same shape Shop
        // reconfigures between its shelf and its pack modal, and for the same
        // reason: DossierPackPanel is an opaque Image over the whole of
        // column A, so while it is up the mouse cannot reach the nav rows
        // underneath it and neither should a Move. The rows keep whatever
        // links they last had, which is harmless because nothing links INTO
        // them in the open state.

        // THE PANE'S ENTRY (INavPaneEntry): the first equipment slot. The
        // generic answer SystemMenuController would otherwise use is the
        // first Selectable in tree order, which here is the roster pager
        // beside the name -- a paging arrow, not what the pane is about.
        public Selectable NavEntry => Cell(slotCells, 0);

        // THE TWO ALIGNED FILES THE PAPERDOLL IS DRAWN IN, top to bottom,
        // read off DossierLayout.SlotGeometry's own table rather than
        // restated as a second opinion about where a slot sits: Head is
        // centred above the figure and everything below it alternates left
        // file / right file down the body. This is the order a Move steps in,
        // so it has to be the drawn order and not EquipmentSlots.All's
        // declaration order (which interleaves the two files).
        private static readonly EquipmentSlot[] LeftFile =
        {
            EquipmentSlot.Head, EquipmentSlot.Necklace, EquipmentSlot.Torso,
            EquipmentSlot.Gloves, EquipmentSlot.Legs,
        };

        private static readonly EquipmentSlot[] RightFile =
        {
            EquipmentSlot.Weapon1, EquipmentSlot.Weapon2, EquipmentSlot.Shoes,
        };

        private void RefreshNavigation()
        {
            // THE ATTRIBUTES PANEL IS A MODAL, not a column-A overlay like
            // Pack/Spells/Skills/Track -- it covers columns A, B AND C at
            // once (AttributesPanel's own comment says why), so every other
            // group below (the paperdoll's two files, the compact score
            // grid) would be reaching for Selectables the player cannot see
            // or click right now. Declaring ONLY the panel's own group while
            // it is open, rather than leaving the rest declared-but-hidden
            // the way Pack/Spells/Skills leave the paperdoll reachable
            // (correctly -- column B stays visible behind THOSE three), is
            // what keeps a Move from ever landing on a covered button.
            if (IsAttributesShown)
            {
                var attributesGroups = new List<UiNavGroup<Selectable>>();
                DeclareAttributes(attributesGroups);
                RuntimeNavWiring.Apply(attributesGroups, new List<UiNavLink<Selectable>?>());
                return;
            }

            var leftFile = FileCells(LeftFile);
            var rightFile = FileCells(RightFile);
            var scores = Present(attributeCells);
            var opener = Present(new[] { attributesRow });

            var groups = new List<UiNavGroup<Selectable>>
            {
                RuntimeNavWiring.Group("dossierSlotsLeft", UiNavGroupKind.List, leftFile),
                RuntimeNavWiring.Group("dossierSlotsRight", UiNavGroupKind.List, rightFile),

                // CLAMPED, against the owner's wrap-a-Grid-row default (plan
                // section 12.3), and the same reason applies to the pack grid
                // below: this group hands off SIDEWAYS to its neighbour
                // (Left out of column 0 reaches the loadout), and a row that
                // also wraps would have to answer Left twice. A group that is
                // the last one on its axis wraps; one with somewhere to go
                // does not.
                RuntimeNavWiring.Group("dossierScores", UiNavGroupKind.Grid, scores,
                    gridRowLength: ScoreColumns, wrap: UiNavWrap.Clamp),
            };

            // THE ATTRIBUTES PANEL'S OPENER, never once in any navigable
            // group or link -- "there is no way to allocate stat points with
            // the gamepad" (owner's hardware playtest, 2026-09-23) traced to
            // exactly this: the modal itself (DeclareAttributes) is a fully
            // worked pad surface -- entry focused, Close reachable, every
            // row's own Left/Right spend/refund (AttributeRow.OnMove) -- and
            // nothing could ever open it, because attributesRow sat outside
            // DeclareColumnARows' rows list AND outside this method's own
            // groups/links, with RuntimeNavWiring never once touching its
            // `.navigation` -- left on whatever Unity's own Automatic default
            // does near a screen full of Explicit-wired neighbours, which is
            // not "reachable", it is "undefined".
            //
            // ITS OWN GROUP so it gets Explicit navigation without becoming a
            // GRID member (it sits ABOVE the score grid, not inside it --
            // CharacterDossierScreen.BuildColumnC's own header: "this one
            // lives in column C... because that is where the thing it opens
            // already is").
            if (opener.Count > 0) groups.Add(RuntimeNavWiring.Group("dossierAttributesOpener",
                UiNavGroupKind.List, opener));

            var links = new List<UiNavLink<Selectable>?>();

            // The loadout's two files, paired by body row: Torso beside
            // Weapon 1, Gloves beside Weapon 2, Legs beside Shoes -- and the
            // two slots above the right file's first entry (Head, Necklace)
            // clamp onto it rather than dead-ending.
            PairFilesByBodyRow(links);

            // And the right file out to column C -- the opener above the
            // grid AND the grid's own left column, so PairAcross's own
            // nearest-by-height match (NearestAcross) decides which one a
            // given loadout row actually reaches, exactly the way it already
            // decides between grid rows. No hand-authored Y coordinate: the
            // opener sits physically above row 0, so it simply wins for
            // whichever right-file row sits nearest it.
            var columnC = new List<Selectable>(opener);
            columnC.AddRange(ColumnOf(scores, 0));
            PairAcross(links, rightFile, columnC, UiNavDirection.Right, UiNavDirection.Left);

            // Down from the opener into the grid's own top row, and Up from
            // every cell in that row back to the opener -- the one path
            // PairAcross's height-only pairing above does not already cover,
            // since the opener and the grid are two separate groups rather
            // than one taller one.
            if (opener.Count > 0)
            {
                var topRow = Row(scores, 0);
                links.Add(RuntimeNavWiring.Link(opener[0], UiNavDirection.Down, First(topRow)));
                foreach (var cell in topRow) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Up, opener[0]));
            }

            // FOUR STATES, not three -- DossierSkillsPanel joins Pack and
            // Spells (owner bug report, 2026-09-19). Same opaque-cover-of-
            // column-A shape as the other two, checked before falling back
            // to the column-A rows so at most one of the three is ever
            // considered.
            if (IsPackShown) DeclarePack(groups, links, leftFile);
            else if (IsSpellsShown) DeclareSpells(groups, links, leftFile);
            else if (IsSkillsShown) DeclareSkills(groups, links, leftFile);
            else DeclareColumnARows(groups, links, leftFile);

            RuntimeNavWiring.Apply(groups, links);
        }

        // Column A as the player meets it with no panel open: the roster
        // pager (a Rail of two, hidden entirely for a one-character squad)
        // over the four live nav rows, top to bottom as drawn.
        //
        // THE SKILLS ROW USED TO BE MISSING HERE (owner bug report,
        // 2026-09-19: "Skills in the char menu, when you click on it,
        // nothing happens") -- it carried no onClick at all, so a Move that
        // landed on it was a Submit that did nothing. It opens
        // DossierSkillsPanel now, the same row+panel+close shape Spells and
        // Pack already use.
        private void DeclareColumnARows(List<UiNavGroup<Selectable>> groups,
            List<UiNavLink<Selectable>?> links, List<Selectable> leftFile)
        {
            var pager = Present(new[] { prevCharacterButton, nextCharacterButton });
            var rows = Present(new[] { spellsRow, trackRow, skillsRow, packRow });

            groups.Add(RuntimeNavWiring.Group("dossierRoster", UiNavGroupKind.Rail, pager));
            groups.Add(RuntimeNavWiring.Group("dossierRows", UiNavGroupKind.List, rows));

            // The pager sits at the top of the column and the rows at its
            // foot, with the portrait between them; Down/Up bridges the gap.
            foreach (var arrow in pager) links.Add(RuntimeNavWiring.Link(arrow, UiNavDirection.Down, First(rows)));
            links.Add(RuntimeNavWiring.Link(First(rows), UiNavDirection.Up, First(pager)));

            // Out to the loadout. The pager's own representative in the
            // handoff is its RIGHT arrow, which is the control physically
            // nearest column B.
            var spine = new List<Selectable>();
            if (pager.Count > 0) spine.Add(pager[pager.Count - 1]);
            spine.AddRange(rows);

            PairAcross(links, spine, leftFile, UiNavDirection.Right, UiNavDirection.Left);
        }

        // Column A while the pack covers it: the three sort tabs as a Rail
        // under the panel's own Close button, over a 2-wide grid of whatever
        // cells currently hold an item.
        //
        // CLOSE IS IN THE GRAPH. It used to be the ONLY way out on a stick,
        // because this pane claimed no Cancel at all; ClaimCancel below now
        // steps back out of the pack for free (hardware round 1 item 5), and
        // Close stays anyway -- it is the mouse's own way out, drawn on the
        // panel, and a control the eye can see has to be a control a Move can
        // reach.
        private void DeclarePack(List<UiNavGroup<Selectable>> groups,
            List<UiNavLink<Selectable>?> links, List<Selectable> leftFile)
        {
            var tabs = Present(packSortTabs);
            var cells = BoundPackCells();

            groups.Add(RuntimeNavWiring.Group("dossierPackTabs", UiNavGroupKind.Rail, tabs));
            groups.Add(RuntimeNavWiring.Group("dossierPack", UiNavGroupKind.Grid, cells,
                gridRowLength: PackColumns, wrap: UiNavWrap.Clamp));

            var close = (Selectable)packCloseButton;

            foreach (var tab in tabs) links.Add(RuntimeNavWiring.Link(tab, UiNavDirection.Up, close));
            links.Add(RuntimeNavWiring.Link(close, UiNavDirection.Down, First(tabs)));

            // The tabs onto the first row of cells, paired by column.
            PairAcross(links, tabs, Row(cells, 0), UiNavDirection.Down, UiNavDirection.Up);

            // And out to the loadout, off the rightmost cell of each row --
            // with Close standing at the top of the column the way the
            // pager's right arrow does when the pack is shut.
            var spine = new List<Selectable>();
            if (close != null) spine.Add(close);
            for (int row = 0; row * PackColumns < cells.Count; row++)
            {
                var members = Row(cells, row);
                if (members.Count > 0) spine.Add(members[members.Count - 1]);
            }

            PairAcross(links, spine, leftFile, UiNavDirection.Right, UiNavDirection.Left);
        }

        // Column A while the spells panel covers it (AUDIT.md #161). Same
        // shape as DeclarePack immediately above -- Close standing at the
        // top of the column, the covered controls declared as the reachable
        // groups, a spine handing off to the loadout -- but ONE column, not
        // a grid: BuildSpellsPanel draws the three spell slots and the
        // unassigned-book rows in a single vertical stack (no second axis),
        // so both are Lists rather than one of them being forced into a
        // Grid shape the layout does not have.
        //
        // CLOSE IS IN THE GRAPH for the identical reason DeclarePack's own
        // Close is, and with the identical correction: Cancel now steps back
        // out of this panel too, and Close remains because it is drawn on the
        // panel for the mouse and must therefore be reachable by a Move.
        private void DeclareSpells(List<UiNavGroup<Selectable>> groups,
            List<UiNavLink<Selectable>?> links, List<Selectable> leftFile)
        {
            var slots = Present(spellSlots);
            var rows = Present(unassignedRows);

            groups.Add(RuntimeNavWiring.Group("dossierSpellSlots", UiNavGroupKind.List, slots));
            groups.Add(RuntimeNavWiring.Group("dossierUnassigned", UiNavGroupKind.List, rows, wrap: UiNavWrap.Clamp));

            var close = (Selectable)spellsCloseButton;

            // TOP TO BOTTOM, as drawn: Close sits above the slots band,
            // which sits above the unassigned-rows band (BuildSpellsPanel's
            // own vertical stack, top to bottom). Either band can be empty
            // (no slots for a character whose pool refuses books; no
            // unassigned books at all), so the links skip a missing one
            // rather than pointing at null.
            var firstBelowClose = First(slots) ?? First(rows);
            if (firstBelowClose != null) links.Add(RuntimeNavWiring.Link(close, UiNavDirection.Down, firstBelowClose));

            if (slots.Count > 0)
            {
                links.Add(RuntimeNavWiring.Link(slots[0], UiNavDirection.Up, close));
                if (rows.Count > 0)
                {
                    links.Add(RuntimeNavWiring.Link(slots[slots.Count - 1], UiNavDirection.Down, rows[0]));
                    links.Add(RuntimeNavWiring.Link(rows[0], UiNavDirection.Up, slots[slots.Count - 1]));
                }
            }
            else if (rows.Count > 0)
            {
                links.Add(RuntimeNavWiring.Link(rows[0], UiNavDirection.Up, close));
            }

            // And out to the loadout, the same "one representative per row"
            // spine shape DeclarePack's own spine uses, off Close at the top
            // of the column -- there is only one column here, unlike the
            // pack's grid, so the whole panel hands off through whichever
            // single control sits at each spine position rather than one per
            // pack row.
            var spine = new List<Selectable>();
            if (close != null) spine.Add(close);
            spine.AddRange(slots);
            spine.AddRange(rows);

            PairAcross(links, spine, leftFile, UiNavDirection.Right, UiNavDirection.Left);
        }

        // The attributes panel's own graph -- a plain vertical List, Close
        // above the six rows in drawn order, clamped rather than wrapping.
        // NO cross-column PairAcross call, unlike DeclarePack/DeclareSpells/
        // DeclareSkills above: this panel covers every column at once, so
        // there is no neighbouring file or grid still visible to hand
        // Left/Right off to (RefreshNavigation's own header says why the
        // other three groups are not even declared while this one is
        // open). A row's own Left/Right are answered by AttributeRow.OnMove
        // (spend/refund), not by this graph at all.
        private void DeclareAttributes(List<UiNavGroup<Selectable>> groups)
        {
            var column = new List<Selectable>();
            if (attributesCloseButton != null) column.Add(attributesCloseButton);

            if (attributeRowSteps != null)
            {
                foreach (var row in attributeRowSteps)
                {
                    if (row != null) column.Add(row);
                }
            }

            groups.Add(RuntimeNavWiring.Group("dossierAttributesRows", UiNavGroupKind.List, column,
                wrap: UiNavWrap.Clamp));
        }

        // The panel's own entry: the first row that actually holds a score,
        // else Close -- same "land somewhere real" rule SpellsEntry states.
        private Selectable AttributesEntry()
        {
            if (attributeRowSteps != null)
            {
                foreach (var row in attributeRowSteps)
                {
                    if (row != null) return row;
                }
            }

            return attributesCloseButton;
        }

        // Column A while the skills panel covers it (owner bug report,
        // 2026-09-19) -- the character's talent-granted kit, read-only.
        // Same "cover it, own the graph while covered" spine handoff
        // DeclarePack/DeclareSpells use, but NO group of entries to
        // declare: a skill is something the character already has, not
        // something a press here does anything with, and giving each row a
        // Selectable with no onClick would be the exact bug this pane
        // exists to fix (DeclareColumnARows' own header), one level
        // further in. Close is this pane's only control.
        private void DeclareSkills(List<UiNavGroup<Selectable>> groups,
            List<UiNavLink<Selectable>?> links, List<Selectable> leftFile)
        {
            var close = (Selectable)skillsCloseButton;
            var spine = new List<Selectable>();
            if (close != null) spine.Add(close);

            PairAcross(links, spine, leftFile, UiNavDirection.Right, UiNavDirection.Left);
        }

        // The panel's own entry, same rule DeclareColumnARows/DeclarePack's
        // callers effectively get for free from Move alone -- but opening
        // via Submit needs it stated explicitly (ShowSpells's own header
        // says why): the first spell slot if the character can hold any,
        // else the first unassigned book row, else Close, so a character
        // with neither still lands somewhere real rather than nowhere.
        private Selectable SpellsEntry()
        {
            var slots = Present(spellSlots);
            if (slots.Count > 0) return slots[0];

            var rows = Present(unassignedRows);
            if (rows.Count > 0) return rows[0];

            return spellsCloseButton;
        }

        // The skills pane's own entry. Always Close -- unlike Spells/Pack
        // there is no interactive row to land on first, because nothing in
        // this pane takes a press (see DeclareSkills).
        private Selectable SkillsEntry() => skillsCloseButton;

        private const int PackColumns = 2;
        private const int ScoreColumns = 3;

        // NOT BY POSITION IN THE TWO LISTS -- that was the first version of
        // this and it was wrong in a way only the test caught: the left file
        // has two slots above the right file's first entry, so index 2
        // (Torso) paired with index 2 (Shoes), three body rows down. The
        // files are paired by the slot's own DRAWN ROW instead, off
        // DossierLayout.SlotTop -- the same table SlotAt and LeaderAt read,
        // so a slot that moves takes its neighbour with it rather than
        // leaving a stale pairing behind. Nearest row rather than equal:
        // Head and Necklace have no opposite number and clamp onto the
        // topmost one that does.
        private void PairFilesByBodyRow(List<UiNavLink<Selectable>?> links)
        {
            foreach (var slot in LeftFile)
            {
                links.Add(RuntimeNavWiring.Link(SlotCell(slot), UiNavDirection.Right,
                    SlotCell(NearestRow(RightFile, slot))));
            }

            foreach (var slot in RightFile)
            {
                links.Add(RuntimeNavWiring.Link(SlotCell(slot), UiNavDirection.Left,
                    SlotCell(NearestRow(LeftFile, slot))));
            }
        }

        private static EquipmentSlot NearestRow(EquipmentSlot[] file, EquipmentSlot to)
        {
            var best = file[0];
            float bestGap = Mathf.Abs(DossierLayout.SlotTop(best) - DossierLayout.SlotTop(to));

            for (int i = 1; i < file.Length; i++)
            {
                float gap = Mathf.Abs(DossierLayout.SlotTop(file[i]) - DossierLayout.SlotTop(to));
                if (gap >= bestGap) continue;

                best = file[i];
                bestGap = gap;
            }

            return best;
        }

        private Selectable SlotCell(EquipmentSlot slot) =>
            Cell(slotCells, System.Array.IndexOf(EquipmentSlots.All, slot));

        // PAIRED BY WHERE THE CONTROLS ACTUALLY ARE ON SCREEN, not by their
        // position in the two lists.
        //
        // HARDWARE PLAY-TEST ROUND 1, ITEM 6: "a player can't see where
        // they're going in the character sheets screen: no obvious selectors
        // and no intuitive navigating." The selectors are another pass's job.
        // This is the other half, and it was a real defect rather than a
        // matter of taste.
        //
        // What this replaced was PartyController.CrossLinks' own
        // Mathf.Min(column, cards - 1) rule, turned sideways -- pair index i
        // with index i, clamp where one list is shorter. That rule carries an
        // UNSTATED PRECONDITION: that the two lists are already aligned across
        // the axis being crossed. It is true for Party, whose seats and roster
        // cards are drawn in matching rows. It is false for every use of it on
        // this screen, because column A and the loadout have completely
        // different vertical extents, and the result is what the owner
        // reported. Measured off the solved layout rather than guessed at:
        //
        //     column A            the loadout
        //     pager   y  114      Head      y  260
        //     Spells  y  -67      Necklace  y  121
        //     Track   y -123      Torso     y   30
        //     Pack    y -235      Gloves    y  -65
        //                         Legs      y -160
        //
        // Index pairing sent Spells (y -67) Right to Necklace (y 121), 188
        // units up the screen, past Gloves sitting 2 units away from it. Track
        // and Pack were out by 153 and 170. Coming back was equally wrong in
        // the other direction, and the right file's handoff into the ability
        // scores had the same fault: Weapon 1 crossed to the TOP score cell
        // when the bottom one was 86 units nearer.
        //
        // The rule instead is the one a player is actually applying: a
        // sideways press keeps your height and a vertical press keeps your
        // column, so the target is the nearest member of the other group along
        // the axis PERPENDICULAR to the press. That subsumes index pairing --
        // where two lists genuinely are row-aligned it picks the same partners
        // -- so there is one rule here rather than a second one beside it.
        //
        // It is NOT fully reversible, and that is a property of the screen
        // rather than of this rule: two columns of different lengths cannot be
        // a bijection. Three left-file slots are nearest to the same score
        // cell; only one of them can be what that cell steps back to.
        //
        // The rect's CENTRE in world space, never RectTransform.position,
        // which is the PIVOT -- the distinction AUDIT.md #162 cost five
        // instrumented reproductions to name. Nothing on this screen is
        // pivoted off-centre today; reading the centre means nothing has to
        // stay that way.
        private static void PairAcross(List<UiNavLink<Selectable>?> links,
            IReadOnlyList<Selectable> from, IReadOnlyList<Selectable> to,
            UiNavDirection toward, UiNavDirection back)
        {
            if (from == null || to == null || from.Count == 0 || to.Count == 0) return;

            bool horizontal = toward == UiNavDirection.Left || toward == UiNavDirection.Right;

            for (int i = 0; i < from.Count; i++)
            {
                links.Add(RuntimeNavWiring.Link(from[i], toward, NearestAcross(to, from[i], horizontal)));
            }

            for (int j = 0; j < to.Count; j++)
            {
                links.Add(RuntimeNavWiring.Link(to[j], back, NearestAcross(from, to[j], horizontal)));
            }
        }

        // The member of `candidates` closest to `of` along the axis a press in
        // this direction does NOT travel along: a Left/Right press is answered
        // by the nearest control at the same height, an Up/Down press by the
        // nearest one in the same column. Strictly less-than, so a tie keeps
        // the earlier candidate and the declaration order still decides
        // something rather than being silently unstable.
        private static Selectable NearestAcross(IReadOnlyList<Selectable> candidates,
            Selectable of, bool horizontal)
        {
            if (of == null) return null;

            Vector2 anchor = CentreOf(of);
            Selectable best = null;
            float bestGap = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == null) continue;

                Vector2 centre = CentreOf(candidates[i]);
                float gap = Mathf.Abs(horizontal ? centre.y - anchor.y : centre.x - anchor.x);
                if (gap >= bestGap) continue;

                best = candidates[i];
                bestGap = gap;
            }

            return best;
        }

        // WORLD SPACE, and the rect's centre rather than its pivot. World
        // because these groups live under different parents (column A's frame,
        // the mannequin stage, column C) and a local position means nothing
        // across them; the centre because a pivot is free to sit on an edge
        // and this screen's arithmetic must not depend on nobody ever moving
        // one.
        private static Vector2 CentreOf(Selectable control)
        {
            var rect = control.transform as RectTransform;
            if (rect == null) return control.transform.position;

            return rect.TransformPoint(rect.rect.center);
        }

        // The cells the window has actually bound to an item, which is what a
        // Move may land on. An empty cell stays active and clickable (the
        // mouse has always been free to click one, and EquipFromPack refuses
        // it), but a stick walking into a blank square with nothing to show
        // reads as the navigation being broken rather than as the pack being
        // short.
        private List<Selectable> BoundPackCells()
        {
            var cells = new List<Selectable>();
            if (packCells == null) return cells;

            int bound = _bag.Count - _scroll;
            for (int i = 0; i < packCells.Length && i < bound; i++)
            {
                if (packCells[i] != null) cells.Add(packCells[i]);
            }

            return cells;
        }

        private static List<Selectable> Row(List<Selectable> cells, int row)
        {
            var members = new List<Selectable>();
            for (int i = row * PackColumns; i < cells.Count && i < (row + 1) * PackColumns; i++)
            {
                members.Add(cells[i]);
            }

            return members;
        }

        private static List<Selectable> ColumnOf(List<Selectable> grid, int column)
        {
            var members = new List<Selectable>();
            for (int i = column; i < grid.Count; i += ScoreColumns) members.Add(grid[i]);
            return members;
        }

        private List<Selectable> FileCells(EquipmentSlot[] file)
        {
            var cells = new List<Selectable>();
            foreach (var slot in file)
            {
                var cell = SlotCell(slot);
                if (cell != null) cells.Add(cell);
            }

            return cells;
        }

        // Everything present AND active in a fixed-length authored array --
        // the pager is deactivated for a one-character squad, and a fixture
        // scene can leave any of these unbound.
        private static List<Selectable> Present(Button[] buttons)
        {
            var present = new List<Selectable>();
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.activeSelf) present.Add(buttons[i]);
            }

            return present;
        }

        private static Selectable First(List<Selectable> members) => members.Count > 0 ? members[0] : null;

        private static Selectable Cell(Button[] cells, int index) =>
            cells != null && index >= 0 && index < cells.Length ? cells[index] : null;

        public void ShowPack(bool open)
        {
            packPanel.SetShown(open);
            if (packChevron != null) packChevron.SetContent(open ? "<" : ">");
            RefreshNavigation();

            // EXPLICIT ON BOTH EDGES, the gap ShowSpells' own comment named as
            // "left alone here... a separate change" and hardware round 1
            // item 5 has now asked for: the dispatcher's reselection rule
            // cannot do this for free, because
            // SystemMenuController.RefreshSelectables declares every
            // Selectable under the whole pane regardless of visibility, so
            // PackRow -- now hidden behind the panel covering it -- still
            // counts as declared and the rule stays silent.
            if (open) EventSystem.current?.SetSelectedGameObject(PackEntry());
            else if (packRow != null) EventSystem.current?.SetSelectedGameObject(packRow.gameObject);
        }

        // The panel's own entry: its Close button, which DeclarePack already
        // puts at the head of the column. Not the first bound cell -- a pack
        // with nothing in it has no cells at all, and Close is the one control
        // this panel always has.
        private GameObject PackEntry()
        {
            var cells = BoundPackCells();
            if (cells.Count > 0 && cells[0] != null) return cells[0].gameObject;
            return packCloseButton != null ? packCloseButton.gameObject : null;
        }

        // ---- Cancel, claimed one level at a time (INavCancelClaim) ----------
        //
        // HARDWARE PLAY-TEST ROUND 1, ITEM 5: "Back (B) should not close a
        // screen but take you back first (e.g. reward track to character
        // sheet screen)."
        //
        // Before this, Cancel anywhere inside this pane ran SystemMenu's own
        // Close and took the whole menu down, so a player who had opened the
        // reward track from a row, or the pack, or the books, lost three
        // levels to one press and had to walk all the way back in.
        //
        // The seam is the one PartyController already uses while carrying a
        // character, and generalising it is deliberately NOT a per-panel flag:
        // this method answers at the moment of the press, so nothing has to
        // remember to set or clear anything, and a pane with no level open
        // simply says false and lets SystemMenu close as it always did. The
        // panels are tested innermost first -- only one of the four can be
        // open at a time (the track row is only reachable with column A's
        // rows showing, which means the pack, the books and the skills pane
        // are not), but stating the order costs nothing and means the fourth
        // level this comment used to predict -- now landed, skills below --
        // cannot quietly invert it, nor can a fifth.
        bool INavCancelClaim.ClaimCancel()
        {
            // Checked FIRST, innermost of the five: it is reachable only
            // from the base column-A-rows state (there is no attributesRow
            // to press while Pack/Spells/Skills/Track already cover the
            // screen), so a Cancel while it is open only ever needs to
            // return to that same base state, never chain into a second
            // claim below.
            if (IsAttributesShown)
            {
                ShowAttributes(false);
                return true;
            }

            if (trackPanel != null && trackPanel.activeSelf)
            {
                // Deactivating is the whole of it: RewardTrackController's own
                // OnDisable fires the Closed callback ShowTrack assigned, and
                // that is what hands selection back to DossierTrackRow. Going
                // through the panel rather than reaching for the controller's
                // private Close keeps one implementation of "the track shuts".
                trackPanel.SetActive(false);
                return true;
            }

            if (IsPackShown)
            {
                ShowPack(false);
                return true;
            }

            if (IsSpellsShown)
            {
                ShowSpells(false);
                return true;
            }

            if (IsSkillsShown)
            {
                ShowSkills(false);
                return true;
            }

            return false;
        }

        // Read by SheetPanel so I/C can tell "already showing what was asked
        // for" from "showing the other state" -- the distinction that makes
        // the second press of the OTHER key switch instead of closing.
        public bool IsPackShown => packPanel != null && packPanel.activeSelf;

        private void TogglePack() => ShowPack(packPanel != null && !packPanel.activeSelf);

        // ---- spell books (docs/PLAN_SHOP.md §1g, gate 3) -------------------------

        // Which unassigned-book row is selected, as an index into the SAME
        // snapshot RefreshSpells just painted rather than into
        // run.unassignedSpellBooks live, so the index cannot outlive the list
        // it was taken against.
        //
        // IT DOES NOT BUY WHAT IT USED TO CLAIM. The comment here said the
        // snapshot exists because "a duplicate skillId can occupy more than one
        // row and 'row 2' has to mean row 2, not 'some row with this skillId'".
        // That distinction is defeated one hop later: both LearnSpell and
        // ReplaceSpell take the id off the pool with
        // run.unassignedSpellBooks.Remove(skillId), which removes the FIRST
        // match, not the row that was selected. Harmless -- two copies of one
        // book are interchangeable, so which one goes carries no information --
        // but the snapshot is a bounds-and-staleness guard, not row identity.
        private int _selectedUnassignedRow = -1;
        private List<string> _unassignedSnapshot = new List<string>();

        // Read by SheetPanel and now by RefreshNavigation's own four-way
        // branch, same shape and same reason IsPackShown already has.
        public bool IsSpellsShown => spellsPanel != null && spellsPanel.activeSelf;

        public void ShowSpells(bool open)
        {
            spellsPanel.SetShown(open);
            if (spellsChevron != null) spellsChevron.SetContent(open ? "<" : ">");
            if (!open)
            {
                _selectedUnassignedRow = -1;
                _alreadyKnownRefusal = false;
            }

            RefreshSpells();

            // EXPLICIT, on both edges -- the dispatcher's own next-frame
            // reselection rule (NavigationInputModule.
            // ReselectIfOutsideDeclaredSet) never fires here to do this for
            // free: it only acts once the CURRENT selection has fallen
            // OUTSIDE the top context's declared Selectables, and
            // SystemMenuController.RefreshSelectables declares EVERY
            // Selectable under the whole panel regardless of visibility, so
            // SpellsRow (now hidden behind the panel) still counts as
            // "declared" and that rule stays silent. A Submit that opened
            // this panel must not leave the player standing on a row they
            // can no longer see or reach; Close must hand it straight back.
            // (DossierPackPanel has the identical latent gap -- ShowPack
            // does not do this either -- left alone here: no test today
            // depends on it, and fixing an unrelated, unrequested control
            // is a separate change from wiring this one.)
            if (open) EventSystem.current?.SetSelectedGameObject(SpellsEntry()?.gameObject);
            else if (spellsRow != null) EventSystem.current?.SetSelectedGameObject(spellsRow.gameObject);
        }

        private void ToggleSpells() => ShowSpells(spellsPanel != null && !spellsPanel.activeSelf);

        private void SelectUnassigned(int row)
        {
            if (row < 0 || row >= _unassignedSnapshot.Count) return;

            // Re-pressing the selected row deselects, same rule the relic
            // draft's own card press follows -- a screen whose whole job is
            // comparing things should let a choice be reconsidered.
            _selectedUnassignedRow = _selectedUnassignedRow == row ? -1 : row;
            _alreadyKnownRefusal = false;
            RefreshSpells();
        }

        // WHY THE LAST SLOT PRESS DID NOTHING (AUDIT #116), for exactly as
        // long as the player is still looking at the press that caused it: a
        // different row, a different character, or a successful placement all
        // clear it.
        private bool _alreadyKnownRefusal;

        // Does the selected unassigned book already sit in one of THIS
        // character's slots? Asked once per refresh rather than per slot --
        // the answer is about the book and the character, and the slot the
        // press lands on cannot change it.
        private bool SelectedBookIsAlreadyKnown(Character character, List<LearnedSpellEntry> learned)
        {
            if (character == null) return false;
            if (_selectedUnassignedRow < 0 || _selectedUnassignedRow >= _unassignedSnapshot.Count) return false;

            string skillId = _unassignedSnapshot[_selectedUnassignedRow];
            return learned.Exists(e => e != null
                && e.characterId == character.definitionId
                && e.skillId == skillId);
        }

        // A press on a slot COMMITS the currently selected unassigned book
        // into THAT slot -- an empty slot learns it directly (LearnSpell's
        // explicit-slot overload, not the lowest-free-slot one: the player
        // clicked THIS box, and placing it in a different one because it
        // happened to sort lower would be a screen disagreeing with its own
        // click), a full slot replaces it (ReplaceSpell, which returns the
        // displaced book to the pool rather than destroying it -- §7.1
        // point 5). Nothing selected is a no-op; there is nothing to place.
        private void PressSlot(int slot)
        {
            if (_selectedUnassignedRow < 0 || _selectedUnassignedRow >= _unassignedSnapshot.Count) return;

            var squad = Squad();
            if (_index < 0 || _index >= squad.Count) return;
            var character = squad[_index];
            string skillId = _unassignedSnapshot[_selectedUnassignedRow];

            var run = RunManager.Run;
            bool slotOccupied = run?.learnedSpells != null
                && run.learnedSpells.Exists(e => e != null && e.characterId == character.definitionId && e.slot == slot);

            var result = slotOccupied
                ? RunOrchestrator.ReplaceSpell(character.definitionId, skillId, slot)
                : RunOrchestrator.LearnSpell(character.definitionId, skillId, slot);

            // THE RESULT IS READ NOW (AUDIT #116). It used to be consulted for
            // `.Applied` alone, so AlreadyKnown -- the refusal docs/PLAN_SHOP
            // 2d names as the moment the player finds out they bought a
            // duplicate -- happened and the screen did not move.
            if (result.Applied)
            {
                _selectedUnassignedRow = -1;
                _alreadyKnownRefusal = false;
            }
            else
            {
                _alreadyKnownRefusal = result.Reason == ShopRefusal.AlreadyKnown;
            }

            Refresh();
        }

        private void RefreshSpells()
        {
            var squad = Squad();
            var run = RunManager.Run;

            var character = (_index >= 0 && _index < squad.Count) ? squad[_index] : null;
            var learned = run?.learnedSpells ?? new List<LearnedSpellEntry>();
            var unassigned = run?.unassignedSpellBooks ?? new List<string>();

            // "SPELLS 0/3" ON A ROW THAT WILL NEVER READ 1/3 IS A LIE, and
            // the collapsed header is the only part of this panel a player
            // sees without opening it. The count says how many of three slots
            // are filled; a character whose pool refuses books has no slots at
            // all, so the fraction has no denominator to be out of and the
            // header shows the word alone. The sentence inside the panel is
            // where the reason lives -- a header has no room for one.
            if (spellsCount != null && character != null)
            {
                if (!_canHoldSpellBooks)
                {
                    spellsCount.SetContent("");
                }
                else
                {
                    int filled = learned.Count(e => e != null && e.characterId == character.definitionId);
                    spellsCount.Set(UiStrings.DossierSpellsCount, filled, SpellBooks.MaxSpellSlots);
                }
            }

            // THE WHOLE SLOT BLOCK, OR ONE SENTENCE (plan P6, gate 3). Never
            // three greyed chips: a disabled control says "not yet" and the
            // answer here is "never", so the three boxes go entirely and the
            // line that replaces them names the character and the reason.
            //
            // The pool question is asked once per resolved character
            // (_canHoldSpellBooks) rather than per slot -- this method is also
            // reached from a row press, which is why it is a field and not a
            // local.
            //
            // IT CARRIES THE DUPLICATE REFUSAL TOO (AUDIT #116). The two can
            // never be up at once -- a character who cannot hold a book cannot
            // have one already -- so this is one line with two things to say
            // rather than a second node and a second scene rebuild. The
            // refusal is transient; the pool sentence is permanent for that
            // character, which is why it wins the branch.
            bool alreadyKnown = _alreadyKnownRefusal && _canHoldSpellBooks && character != null;
            if (spellsNoBooksLine != null)
            {
                bool noBooks = !_canHoldSpellBooks && character != null;
                spellsNoBooksLine.gameObject.SetActive(noBooks || alreadyKnown);
                if (noBooks)
                {
                    spellsNoBooksLine.Set(UiStrings.DossierNoSpellBooks, DisplayNameOf(character));
                }
                else if (alreadyKnown)
                {
                    spellsNoBooksLine.Set(UiStrings.DossierSpellAlreadyKnown);
                }
            }

            // THE GREEN PREVIEW IS A PROMISE. It lit for ANY empty slot while
            // a row was selected, including every slot a book this character
            // already carries can never fill -- so the screen offered a
            // placement and then refused it. Asked once, here, because the
            // answer is about the book and the character.
            bool selectedBookAlreadyKnown = SelectedBookIsAlreadyKnown(character, learned);

            if (spellSlots != null)
            {
                for (int i = 0; i < spellSlots.Length; i++)
                {
                    if (spellSlots[i] != null && spellSlots[i].gameObject.activeSelf != _canHoldSpellBooks)
                    {
                        spellSlots[i].gameObject.SetActive(_canHoldSpellBooks);
                    }

                    string skillId = character == null || !_canHoldSpellBooks
                        ? null
                        : learned.FirstOrDefault(e => e != null && e.characterId == character.definitionId && e.slot == i)?.skillId;

                    if (spellSlotNames != null && i < spellSlotNames.Length)
                    {
                        if (string.IsNullOrEmpty(skillId))
                        {
                            spellSlotNames[i].Set(UiStrings.DossierSlotEmpty);
                        }
                        else
                        {
                            var definition = ContentDatabase.GetSkill(skillId);
                            spellSlotNames[i].Set(UiStrings.DossierSlotFilled, definition?.Data.DisplayName ?? skillId);
                        }
                    }

                    // ItemIcons.Apply already hides the Image for a null/empty
                    // id (Find's own IsNullOrEmpty guard) -- an empty slot's
                    // skillId is exactly that, so the empty case needs no
                    // separate branch here, the same graceful path a shop
                    // card's no-offer icon already takes.
                    if (spellSlotIcons != null && i < spellSlotIcons.Length)
                    {
                        ItemIcons.Apply(spellSlotIcons[i], icons, skillId);
                    }

                    // A green preview when the currently selected book would
                    // FILL this slot, so a full-vs-empty slot reads the same
                    // way the shop's own would-fill/would-replace preview
                    // does, before the second click commits either.
                    if (spellSlotSelections != null && i < spellSlotSelections.Length && spellSlotSelections[i] != null)
                    {
                        bool previewFill = _selectedUnassignedRow >= 0
                            && string.IsNullOrEmpty(skillId)
                            && !selectedBookAlreadyKnown;
                        spellSlotSelections[i].gameObject.SetActive(previewFill);
                    }
                }
            }

            _unassignedSnapshot = new List<string>(unassigned);

            if (unassignedEmptyHint != null) unassignedEmptyHint.SetActive(_unassignedSnapshot.Count == 0);

            if (unassignedRows != null)
            {
                for (int i = 0; i < unassignedRows.Length; i++)
                {
                    bool present = i < _unassignedSnapshot.Count;
                    unassignedRows[i].gameObject.SetActive(present);
                    if (!present) continue;

                    if (unassignedNames != null && i < unassignedNames.Length)
                    {
                        var definition = ContentDatabase.GetSkill(_unassignedSnapshot[i]);
                        unassignedNames[i].SetContent(definition?.Data.DisplayName ?? _unassignedSnapshot[i]);
                    }

                    if (unassignedSelections != null && i < unassignedSelections.Length && unassignedSelections[i] != null)
                    {
                        unassignedSelections[i].gameObject.SetActive(i == _selectedUnassignedRow);
                    }
                }
            }

            // WHICH SLOTS AND ROWS ARE ACTIVE changed above (a character
            // swap, a book placed, a row (de)selected does not resize either
            // list, but ShowSpells opening/closing the panel does), so the
            // links do too -- the same "one method every path goes through"
            // rule BindPackWindow's own call states for the pack. Runs
            // regardless of which state RefreshNavigation resolves to
            // (AUDIT.md #161's fix reads IsSpellsShown itself), so a
            // pack-open repaint that reaches this via Refresh() harmlessly
            // recomputes a declaration DeclarePack's own branch will win.
            RefreshNavigation();
        }

        // ---- skills (owner bug report, 2026-09-19) --------------------------------

        // Read by RefreshNavigation's own four-way branch, same shape and
        // same reason IsPackShown/IsSpellsShown already have.
        public bool IsSkillsShown => skillsPanel != null && skillsPanel.activeSelf;

        public void ShowSkills(bool open)
        {
            skillsPanel.SetShown(open);
            if (skillsChevron != null) skillsChevron.SetContent(open ? "<" : ">");
            RefreshSkills();

            // EXPLICIT on both edges, same reason ShowSpells/ShowPack state
            // on their own identical two lines: the dispatcher's reselection
            // rule never fires here on its own, because
            // SystemMenuController.RefreshSelectables declares every
            // Selectable under the whole pane regardless of visibility, so
            // SkillsRow -- hidden behind the panel covering it -- still
            // counts as declared.
            if (open) EventSystem.current?.SetSelectedGameObject(SkillsEntry()?.gameObject);
            else if (skillsRow != null) EventSystem.current?.SetSelectedGameObject(skillsRow.gameObject);
        }

        private void ToggleSkills() => ShowSkills(skillsPanel != null && !skillsPanel.activeSelf);

        public bool IsAttributesShown => attributesPanel != null && attributesPanel.activeSelf;

        public void ShowAttributes(bool open)
        {
            attributesPanel.SetShown(open);

            // Closing any open tooltip rather than leaving it floating over
            // a modal that just covered the button it was anchored to --
            // OnAttributeHover/OnSlotHover/OnPackHover never fire again to
            // clear it on their own once their own cell is hidden behind
            // this panel.
            if (open) HideTooltip();

            var squad = Squad();
            var character = (_index >= 0 && _index < squad.Count) ? squad[_index] : null;
            if (open) PaintAttributesPanel(character);

            RefreshNavigation();

            // EXPLICIT on both edges, same reason ShowPack/ShowSpells/
            // ShowSkills state on their own identical two lines.
            if (open) EventSystem.current?.SetSelectedGameObject(AttributesEntry()?.gameObject);
            else if (attributesRow != null) EventSystem.current?.SetSelectedGameObject(attributesRow.gameObject);
        }

        // What one row shows: the score's short name, its current value and
        // however many points are invested in it -- one line, so the effect
        // text below does not have to fight a second column for the row's
        // width (AttributeRowNames' own comment on the screen tree).
        private static string AttributeRowLabel(AbilityScore score, int value, int invested)
        {
            return invested > 0
                ? $"{AbilityScores.ShortName(score)}   {value}   ({invested} invested)"
                : $"{AbilityScores.ShortName(score)}   {value}";
        }

        // Paints the attributes panel's six rows plus its own unspent-points
        // and lock-note lines. Called on every Refresh() (so the panel never
        // goes stale while a player pages between characters with it open)
        // and once more from ShowAttributes when it opens, for the same
        // "paint before the first frame it is visible" reason ShowSkills
        // calls RefreshSkills.
        //
        // MUST run after RefreshAttributes -- it reads _cellOrder, the same
        // ordering dependency PaintStatSpending already has (see that
        // method's own call site in Refresh()).
        private void PaintAttributesPanel(Character character)
        {
            int points = character?.unspentStatPoints ?? 0;
            bool canSpend = points > 0 && !lockedForFight;

            if (attributesUnspentPoints != null)
            {
                attributesUnspentPoints.SetContent(UnspentPointsLabel(points, canSpend));
            }

            // THE SAME LOCK Refund ENFORCES (lockedForFight || InDescent),
            // stated in the panel rather than left for a quiet minus button
            // to imply -- see DossierAttributesLocked's own comment.
            bool locked = lockedForFight || InDescent;
            if (attributesLockNote != null) attributesLockNote.SetActive(locked);

            bool canRefundAny = !lockedForFight && !InDescent;

            for (int i = 0; i < SheetStats.Abilities.Length; i++)
            {
                bool hasScore = i < _cellOrder.Count;
                AbilityScore score = hasScore ? _cellOrder[i] : AbilityScore.Strength;
                int value = hasScore ? ValueOf(_scores, score) : 0;
                int invested = hasScore && character != null ? character.investedAbilityScores[score] : 0;

                if (attributeRowNames != null && i < attributeRowNames.Length && attributeRowNames[i] != null)
                {
                    attributeRowNames[i].SetContent(hasScore ? AttributeRowLabel(score, value, invested) : "");
                }

                if (attributeRowEffects != null && i < attributeRowEffects.Length && attributeRowEffects[i] != null)
                {
                    attributeRowEffects[i].SetContent(hasScore
                        ? AbilityEffectDescriptions.Describe(score, _scores, _weaponScaling, _skillScaling)
                        : "");
                }

                if (attributeRowPluses != null && i < attributeRowPluses.Length && attributeRowPluses[i] != null)
                {
                    attributeRowPluses[i].gameObject.SetActive(hasScore && canSpend);
                }

                if (attributeRowMinuses != null && i < attributeRowMinuses.Length && attributeRowMinuses[i] != null)
                {
                    attributeRowMinuses[i].gameObject.SetActive(hasScore && canRefundAny && invested > 0);
                }
            }
        }

        // Paints the panel's own list. Talent-granted only -- the same set
        // skillsCount already counts in RefreshIdentity -- so the row's "N
        // known" and the pane's row count can never disagree; there is no
        // second, wider notion of "skills" this screen could show without a
        // fight session in hand (TalentGrantedSkillsFor's own header).
        private void RefreshSkills()
        {
            var squad = Squad();
            var character = (_index >= 0 && _index < squad.Count) ? squad[_index] : null;
            var skills = character == null
                ? (IReadOnlyList<SkillDefinition>)System.Array.Empty<SkillDefinition>()
                : ContentDatabase.TalentGrantedSkillsFor(character);

            if (skillsEmptyHint != null) skillsEmptyHint.SetActive(skills.Count == 0);

            if (skillNames != null)
            {
                for (int i = 0; i < skillNames.Length; i++)
                {
                    bool present = i < skills.Count;
                    skillNames[i].gameObject.SetActive(present);
                    if (skillDescriptions != null && i < skillDescriptions.Length)
                    {
                        skillDescriptions[i].gameObject.SetActive(present);
                    }

                    if (!present) continue;

                    skillNames[i].SetContent(skills[i].Data.DisplayName);
                    if (skillDescriptions != null && i < skillDescriptions.Length)
                    {
                        skillDescriptions[i].SetContent(skills[i].Data.Description);
                    }
                }
            }

            // WHICH ROWS ARE ACTIVE changed above (a character swap resizes
            // the list; opening/closing the panel does too), so the links do
            // -- the same "one method every path goes through" rule
            // RefreshSpells' own identical call states.
            RefreshNavigation();
        }

        private void Step(int by)
        {
            var squad = Squad();
            if (squad.Count == 0) return;

            _index = (_index + by + squad.Count) % squad.Count;

            // The refusal named a character. Paging is leaving them.
            _alreadyKnownRefusal = false;
            Refresh();
        }

        public void StepSection(int direction) => Step(direction);

        private static List<Character> Squad()
        {
            var save = SaveSlotManager.CurrentSave;
            return save == null ? new List<Character>() : save.ActiveSquad().Where(c => c != null).ToList();
        }

        public void Refresh()
        {
            var squad = Squad();
            if (squad.Count == 0) return;

            if (_index >= squad.Count) _index = 0;
            var character = squad[_index];

            // The paging arrows, hidden when there is nobody to page to.
            //
            // These were always live and Step wrapped a one-element list, so
            // pressing either did nothing at all -- a control that answers a
            // click by not moving is worse than one that is not there. The same
            // reasoning as TalentController's pair, and the same threshold: at
            // two characters they come back on their own.
            bool canPage = squad.Count > 1;

            if (prevCharacterButton != null
                && prevCharacterButton.gameObject.activeSelf != canPage)
            {
                prevCharacterButton.gameObject.SetActive(canPage);
            }
            if (nextCharacterButton != null
                && nextCharacterButton.gameObject.activeSelf != canPage)
            {
                nextCharacterButton.gameObject.SetActive(canPage);
            }

            var stats = ContentDatabase.EffectiveStats(character);
            var scores = ContentDatabase.EffectiveAbilityScores(character);

            RefreshIdentity(character);
            _scores = scores;
            _weaponScaling = ContentDatabase.EffectiveWeaponScaling(character);
            _skillScaling = ContentDatabase.EffectiveSkillScaling(character);
            var primaryPool = PrimaryPoolFor(character);
            _primaryPoolRule = primaryPool?.CapacityRule ?? PoolCapacityRule.WisdomDerived;
            _primaryPoolName = string.IsNullOrWhiteSpace(primaryPool?.DisplayName)
                ? ManaDisplayNameFallback
                : primaryPool.DisplayName;
            _canHoldSpellBooks = SpellBooks.CanHold(primaryPool);
            RefreshAttributes(scores, character.definitionId);

            // AFTER RefreshAttributes, not inside RefreshIdentity where it
            // started. _cellOrder is rebuilt in there, and on the first open it
            // is still empty -- so a "+" painted before it would hide on every
            // cell for a character who has points to spend, which is precisely
            // the bug this whole feature exists to end.
            PaintStatSpending(character);
            PaintAttributesPanel(character);
            RefreshStats(character, stats, scores);
            RefreshSlots(character);
            RefreshPack();
            RefreshSpells();
            RefreshSkills();
        }

        // The bag, through the SAME BagView the old sheet sorted with -- the
        // ordering rules are not worth a second opinion.
        private IReadOnlyList<BagItem> _bag = new List<BagItem>();

        // Which ordering the player chose, and how far down the list the
        // window sits. Both survive a refresh: equipping something must not
        // throw the pack back to the top or re-sort it under the cursor.
        private BagSortKey _sortKey = BagSortKey.Tier;
        private int _scroll;

        // Exposed so a test can state the scroll rule without reaching into
        // private state -- "the window never runs off the end of the list" is
        // the whole of it.
        public int ScrollOffset => _scroll;

        public BagSortKey SortKey => _sortKey;

        public void SortBy(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= BagSort.All.Length) return;

            if (BagSort.All[keyIndex] == _sortKey) return;

            _sortKey = BagSort.All[keyIndex];

            // BACK TO THE TOP, because the item under the cursor is not the
            // same item any more. Keeping the offset across a re-sort leaves
            // the player looking at an arbitrary slice of a list they just
            // reordered.
            _scroll = 0;
            RefreshPack();
        }

        // SCROLLING REBINDS, IT DOES NOT REBUILD.
        //
        // Both of these used to call RefreshPack, which re-reads the save,
        // resolves every entry through ContentDatabase and re-sorts the whole
        // bag -- to move a window by two indices, at mouse-wheel frequency.
        // The bag has not changed; only which slice of it is on screen has.
        public void Scroll(int rows)
        {
            int before = _scroll;
            _scroll = ClampScroll(_scroll + rows * (int)DossierLayout.PackColumns);

            // Nothing to redraw at either end of the list, which is where a
            // wheel spends most of its time.
            if (_scroll != before) BindPackWindow();
        }

        private void ScrollToFraction(float t)
        {
            int rows = MaxScrollRows();

            // The bar reads left-to-right and the list runs top-to-bottom, so
            // the fraction is inverted: the top of the track is offset zero.
            int row = rows <= 0 ? 0 : Mathf.RoundToInt((1f - Mathf.Clamp01(t)) * rows);

            int before = _scroll;
            _scroll = ClampScroll(row * (int)DossierLayout.PackColumns);
            if (_scroll != before) BindPackWindow();
        }

        private int MaxScrollRows()
        {
            int columns = (int)DossierLayout.PackColumns;
            int totalRows = (_bag.Count + columns - 1) / columns;
            int rows = totalRows - DossierLayout.PackVisibleRows;
            return rows < 0 ? 0 : rows;
        }

        private int ClampScroll(int offset)
        {
            int max = MaxScrollRows() * (int)DossierLayout.PackColumns;
            if (offset > max) offset = max;
            return offset < 0 ? 0 : offset;
        }

        // The wheel, while the pack is open and the pointer is over it. Read
        // here rather than through an event so it works over any part of the
        // panel rather than only over the bar.
        private void Update()
        {
            if (packPanel == null || !packPanel.activeSelf) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            Scroll(wheel > 0f ? -1 : 1);
        }

        private void RefreshPack()
        {
            var save = SaveSlotManager.CurrentSave;
            var entries = save?.stockpiledItems ?? new List<InventoryEntry>();

            // ORDERED BY WHAT THE PLAYER CHOSE. BagSort.By defers to
            // BagView.Sorted for the default, so the pack's long-standing order
            // is still exactly one implementation.
            _bag = BagSort.By(entries.Select(ToBagItem).Where(i => i.Count > 0), _sortKey);

            // The window can be left past the end by anything that shortens the
            // bag -- equipping the last item, or a sort that ran on a longer
            // list. Clamped here rather than at each of those call sites.
            _scroll = ClampScroll(_scroll);

            BindPackWindow();

            // CAPACITY IS NO LONGER A THING THIS SCREEN HAS. The grid used to be
            // 24 cells against a save that could hold more, so the footer had to
            // say "24 of 27" and admit that three were unreachable. The list
            // scrolls now, so every item is reachable and the footer is simply
            // how many are carried.
            if (carriedValue != null) carriedValue.SetContent(_bag.Count.ToString());
        }

        // Puts _bag's current window onto the cells. The only thing scrolling
        // has to do, and deliberately free of anything that reads the save.
        private void BindPackWindow()
        {
            for (int i = 0; i < packCells.Length; i++)
            {
                // THE WINDOW, and this one line is the whole of the scrolling.
                // The cells stay put; which item each shows moves.
                int index = _scroll + i;
                var item = index < _bag.Count ? _bag[index] : (BagItem?)null;

                if (packIcons != null && i < packIcons.Length)
                {
                    bool shown = item.HasValue &&
                        ItemIcons.Apply(packIcons[i], icons, item.Value.Id);
                    packIcons[i].gameObject.SetShown(shown);
                }

                // The rarity TICK, not a border or a glow -- an 8px corner mark,
                // which is the handover's rule and the one that survives an icon
                // being busy.
                if (packRarityTicks != null && i < packRarityTicks.Length)
                {
                    packRarityTicks[i].gameObject.SetShown(item.HasValue);
                    if (item.HasValue) packRarityTicks[i].color = TierColour(item.Value.Tier);
                }

                // ITEM-MODIFIER PLAN PHASE E: see RefreshSlots' identical
                // block for why this is its own node rather than a repaint
                // of packRarityTicks above.
                if (packRiftGlows != null && i < packRiftGlows.Length)
                {
                    bool glows = item.HasValue && RiftTierColors.ShouldGlow(item.Value.RiftTier);
                    packRiftGlows[i].gameObject.SetShown(glows);
                    if (glows) packRiftGlows[i].color = RiftTierColors.For(item.Value.RiftTier);
                }

                // THE NAME, which is what two abreast bought. Through
                // RarityColors so a Legendary reads the same here as everywhere
                // else, and carrying its own plus so two stacks of the same item
                // are told apart without hovering either.
                if (packNames != null && i < packNames.Length)
                {
                    packNames[i].gameObject.SetShown(item.HasValue);
                    if (item.HasValue)
                    {
                        // Through RarityColors.Wrap, the one place an item's
                        // name becomes coloured and plus-suffixed, so a
                        // Legendary reads the same here as on every other
                        // screen. A save naming content that no longer exists
                        // falls back to the bare name BagView already carries
                        // rather than drawing nothing.
                        var definition = ContentDatabase.GetItem(item.Value.Id);
                        packNames[i].SetContent(definition != null
                            ? RarityColors.Wrap(definition, item.Value.Plus)
                            : item.Value.Name);
                    }
                }

                // A count only where there is more than one, so a single item
                // does not carry a redundant "1".
                if (packCounts != null && i < packCounts.Length)
                {
                    bool stacked = item.HasValue && item.Value.Count > 1;
                    packCounts[i].gameObject.SetShown(stacked);
                    if (stacked) packCounts[i].SetContent(item.Value.Count.ToString());
                }
            }

            RefreshSortMarkers();
            RefreshScrollThumb();

            // WHICH CELLS A MOVE MAY LAND ON changed with the window, so the
            // links do too. Here rather than in Refresh() because this is the
            // one method every path that moves the window goes through --
            // Refresh, a sort, a wheel, a drag of the thumb -- and the roster
            // pager's own visibility (the other thing the declaration reads)
            // is settled before Refresh ever reaches RefreshPack.
            RefreshNavigation();
        }

        private void RefreshSortMarkers()
        {
            if (packSortUnderlines == null) return;

            int live = BagSort.IndexOf(_sortKey);
            for (int i = 0; i < packSortUnderlines.Length; i++)
            {
                packSortUnderlines[i].SetShown(i == live);
            }
        }

        // The thumb's LENGTH says how much of the list is on screen and its
        // POSITION says where. A fixed-length thumb would answer only half of
        // that, and on a pack of a hundred items the half it drops is the one
        // that matters.
        private void RefreshScrollThumb()
        {
            if (packScrollThumb == null) return;

            float height = DossierLayout.PackThumbHeight(_bag.Count);
            packScrollThumb.sizeDelta =
                new Vector2(DossierLayout.PackScrollbarWidth, height);

            int maxRows = MaxScrollRows();
            float travel = DossierLayout.PackTrackHeight - height;

            float t = maxRows <= 0
                ? 0f
                : (_scroll / (float)DossierLayout.PackColumns) / maxRows;

            // Measured from the track's own centre, downwards: offset zero is
            // the top of the track.
            packScrollThumb.anchoredPosition = new Vector2(
                packScrollThumb.anchoredPosition.x,
                DossierLayout.PackTrackCentreY + travel * 0.5f - travel * Mathf.Clamp01(t));

            // Nothing to scroll means nothing to drag. Hidden rather than shown
            // full-length, so the bar's presence itself says there is more.
            packScrollThumb.gameObject.SetShown(maxRows > 0);
            if (packScrollTrack != null) packScrollTrack.gameObject.SetShown(maxRows > 0);
        }

        private static BagItem ToBagItem(InventoryEntry entry)
        {
            var item = ContentDatabase.GetItem(entry.itemId);
            if (item == null)
            {
                // A save naming content that no longer exists is shown as itself
                // rather than dropped, so the player sees what happened instead
                // of watching the bag quietly shrink.
                return new BagItem(entry.itemId, entry.itemId, 0, EquipmentSlot.Weapon1,
                    0, entry.plus, entry.count, "", false);
            }

            return new BagItem(item.id, RarityColors.NameOf(item, entry.plus), (int)item.kind,
                item.equipSlot, item.tier, entry.plus, entry.count, item.iconPath, item.IsEquippable,
                entry.modifierIds, entry.riftTier);
        }

        private static Color TierColour(int tier) => Hex(RarityBands.HexColorForTier(tier));

        // WHAT TO CALL THIS CHARACTER ON SCREEN, falling back to the id so a
        // definition that has gone missing still names something a bug report
        // can be written about. Two readers now -- the header and the
        // no-spell-books line -- so it is a method rather than two copies of
        // the same three-way expression.
        private static string DisplayNameOf(Character character)
        {
            var definition = ContentDatabase.GetCharacter(character?.definitionId);
            return definition == null || string.IsNullOrWhiteSpace(definition.Data.DisplayName)
                ? character?.definitionId ?? ""
                : definition.Data.DisplayName;
        }

        private void RefreshIdentity(Character character)
        {
            string name = DisplayNameOf(character);

            if (characterName != null) characterName.SetContent(name);
            if (subLine != null) subLine.SetContent($"Level {character.level}");

            // The face, not a mannequin.
            //
            // A MISS LEAVES THE STAND STANDING. The tree has already put an
            // armour stand in this slot as the placeholder, so blanking or
            // disabling the Image (what ItemIcons.Apply would do, correctly,
            // for an item cell) would turn graceful degradation into a hole in
            // the layout.
            var face = CharacterPortraits.For(character.definitionId);
            if (portrait != null && face != null)
            {
                portrait.sprite = face;
                portrait.enabled = true;
                portrait.preserveAspect = true;

                // AND THE TINT COMES OFF. The tree dims this slot to a faint
                // violet so the armour-stand placeholder recedes, and an Image
                // tint MULTIPLIES -- so that same dimming was landing on the
                // painted portrait, which arrived grey-pink at a third opacity
                // and read as a bad illustration rather than as a tint that was
                // never meant for it.
                portrait.color = Color.white;
            }

            int next = Character.ExpToNextLevel(character.level);
            if (xpFill != null)
            {
                float fraction = next <= 0 ? 0f : Mathf.Clamp01((float)character.exp / next);

                // WIDTH, against a left pivot -- not anchors.
                //
                // This drove anchorMax.x to the fraction and zeroed sizeDelta,
                // which looks equivalent and is not: ANCHORS ARE RELATIVE TO
                // THE PARENT. The parent here is column A, not the 226px track,
                // so a bar meant to cross the track crossed that fraction of
                // the whole column instead -- out past its own "258 left"
                // caption and on into the loadout stage beside it.
                //
                // The Options slider's fill records the identical mistake in
                // its own comment, made and fixed while this one was still
                // live. The tree already authors this node at the track's width
                // with a left pivot, so setting width is all that was ever
                // needed, and DossierXpBarTests holds the fill inside the track
                // by measuring both rather than by trusting the mechanism.
                xpFill.sizeDelta = new Vector2(
                    DossierLayout.XpTrackWidth * fraction, xpFill.sizeDelta.y);
            }

            if (xpRemaining != null) xpRemaining.SetContent(Mathf.Max(0, next - character.exp) + " left");

            PaintTrackLine(character);
            if (skillsCount != null)
            {
                // Talent-granted only, which is what this screen can actually
                // count without a fight session in hand.
                skillsCount.SetContent(ContentDatabase.TalentGrantedSkillsFor(character).Count + " known");
            }
        }

        private void ShowTrack()
        {
            if (trackPanel == null) return;

            // TOLD WHICH CHARACTER BEFORE IT OPENS. ShowFor only stores the
            // id; the panel's own OnEnable->Refresh is what resolves it
            // against the save, so calling this before SetActive is what
            // makes that first Refresh already correct (docs/PLAN_REWARD_
            // TRACKS.md §8) rather than one frame of showing whoever the
            // panel last painted.
            if (trackScreen != null)
            {
                var squad = Squad();
                if (_index >= 0 && _index < squad.Count && squad[_index] != null)
                {
                    trackScreen.ShowFor(squad[_index].definitionId);
                }

                // THE HANDOFF BACK, since the panel's own controller has no
                // idea trackRow exists (DossierPackPanel's identical gap,
                // ShowSpells' own fix comment: "ShowPack does not do this
                // either -- left alone here"). Segment 8 of docs/
                // GAMEPAD_NAVIGATION_PLAN.md's journey found this same gap
                // live on the track row specifically -- reachable by a real
                // Submit press with nothing selected once inside, which
                // strands the pad rather than merely reading as untidy. The
                // panel selects its OWN entry on open (RewardTrackController.
                // OnEnable); this is the other half, assigned fresh each open
                // so it always points at THIS row rather than whichever one
                // last opened the panel.
                trackScreen.Closed = () => EventSystem.current?.SetSelectedGameObject(trackRow?.gameObject);
            }

            // SetActive rather than a toggle: the row is a door in, and the
            // panel's own CLOSE is the way out. A row that also closed it would
            // be a second control for one state, hidden behind the panel that
            // covers it.
            trackPanel.SetActive(true);
        }

        // Places one of this character's unspent points into the score sitting
        // in cell `index`.
        //
        // THE CELL IS NOT THE SCORE. The six cells are filled highest-first per
        // character, so cell 0 is whatever this character's best ability
        // happens to be. Resolving through _cellOrder at click time is what
        // makes that safe -- binding the score at wire time would spend into
        // whatever was strongest when the screen was built, which is a bug that
        // only appears after stepping to a second character.
        //
        // Character.Invest refuses when there is nothing to spend and returns
        // false, so the guard here is about not repainting rather than about
        // correctness.
        //
        // THROUGH TalentOps, NOT THROUGH Character DIRECTLY. Investing into
        // Constitution moves max health by 20, and the run stores current
        // health as an ABSOLUTE number -- so a point placed mid-descent left
        // the run holding the old figure against a maximum that had just
        // risen, growing the permanently-empty tail RunEncounter.
        // ScaleCarriedHealth's header describes. This is reachable on purpose:
        // Spend guards on lockedForFight alone, while Refund below also guards
        // on InDescent. TalentOps.Invest is the Core half that wraps the
        // measure/rescale pair, the same one Kindle and Respec ride.
        private void Spend(int index)
        {
            if (lockedForFight) return;
            if (!TryResolveCell(index, out var character, out var score)) return;

            if (!TalentOps.Invest(character, score)) return;

            // Written immediately. A placed point is permanent until a respec,
            // so losing one to a crash costs the player a level's reward.
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // Takes one point back out of the score sitting in cell `index`. Same
        // cell-not-score resolution as Spend, and the same reason for it.
        //
        // A hidden button is still EventSystem-reachable, so this guards
        // itself rather than trusting that the minus was inactive when
        // clicked -- Spend guards inside the method for the same reason.
        private void Refund(int index)
        {
            if (lockedForFight || InDescent) return;
            if (!TryResolveCell(index, out var character, out var score)) return;

            if (!TalentOps.Refund(character, score)) return;

            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // THE CELL IS NOT THE SCORE (see Spend's own header). Resolves cell
        // `index` against the squad member on screen and _cellOrder's
        // click-time sort, or refuses -- shared by Spend and Refund so the
        // resolution rule can only drift out of sync with itself once,
        // rather than once per caller.
        private bool TryResolveCell(int index, out Character character, out AbilityScore score)
        {
            character = null;
            score = default;

            var squad = Squad();
            if (squad.Count == 0) return false;
            if (_index < 0 || _index >= squad.Count) return false;

            character = squad[_index];
            if (character == null) return false;
            if (index < 0 || index >= _cellOrder.Count) return false;

            score = _cellOrder[index];
            return true;
        }

        // Shows the "+" on every cell when there is a point to spend, and says
        // how many.
        //
        // HIDDEN rather than disabled when there are none, because six greyed
        // plus signs on a character with nothing to spend is six pieces of
        // furniture. The count label is what makes the whole mechanic
        // discoverable -- a 22px "+" in the corner of a cell is easy to have
        // and never notice, which is most of how levelling came to hand out
        // something no player could see.
        // Same hazard Core/OptionsController.SetNoNavigation already flags:
        // UnityEngine.UI.Navigation spelled out in full, because
        // PrincesPalace.Navigation (the map/graph state type) sits in this
        // same namespace and would otherwise shadow it.
        //
        // The row's own steppers never take a nav target and are never
        // selected by a mouse click either -- clicking one still fires its
        // onClick (independent of the selection side effect Navigation.
        // Mode.None suppresses) and leaves the ROW's own remembered focus
        // alone, same as Options' stepper buttons beside their row.
        private static void SetNoNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            var nav = selectable.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            selectable.navigation = nav;
        }

        // Shared by the compact grid's own UnspentPoints label and the
        // attributes panel's AttributesUnspentPoints -- one wording, so the
        // two can never say something different about the same count.
        private static string UnspentPointsLabel(int points, bool canSpend)
        {
            if (!canSpend) return "";
            return points == 1 ? "1 POINT TO SPEND" : $"{points} POINTS TO SPEND";
        }

        private void PaintStatSpending(Character character)
        {
            int points = character?.unspentStatPoints ?? 0;
            bool canSpend = points > 0 && !lockedForFight;

            if (unspentPoints != null)
            {
                unspentPoints.SetContent(UnspentPointsLabel(points, canSpend));
            }

            if (attributePluses == null) return;

            for (int i = 0; i < attributePluses.Length; i++)
            {
                if (attributePluses[i] == null) continue;

                // Only cells that actually hold a score. _cellOrder is rebuilt
                // per character and a content pass could leave it short.
                attributePluses[i].gameObject.SetActive(canSpend && i < _cellOrder.Count);
            }

            // The minus is gated PER CELL, where the plus above is gated
            // GLOBALLY, and that is the whole reason this is not a copy-paste
            // of the loop above. An unspent point can go into any of the six
            // scores, so one condition answers all six pluses. A refund can
            // only come back out of a score that has something invested in
            // it, which is a different condition on every cell -- and it is
            // exactly what makes Character.Refund return true for that cell,
            // so the button and the model can never disagree about it.
            if (attributeMinuses == null) return;

            bool canRefund = !lockedForFight && !InDescent;
            for (int i = 0; i < attributeMinuses.Length; i++)
            {
                if (attributeMinuses[i] == null) continue;

                bool hasInvested = character != null
                    && i < _cellOrder.Count
                    && character.investedAbilityScores[_cellOrder[i]] > 0;
                attributeMinuses[i].gameObject.SetActive(canRefund && hasInvested);
            }
        }

        // What this character's next level is worth, under the bar that says
        // how far away it is.
        //
        // THE TRACK WAS PAYABLE BEFORE IT WAS LEGIBLE. Levels grant Favor, stat
        // points, extra starting relics and a wider item offer, and until this
        // line existed nothing on any screen said so -- a player could pass
        // level 25 and find themselves drafting two relics with no way to learn
        // why, which reads as a bug rather than as a reward.
        //
        // Reads `level` directly rather than the claim watermark, because it is
        // describing the track and not the payout: what level 30 gives is the
        // same sentence whether or not this character has been paid yet.
        private void PaintTrackLine(Character character)
        {
            if (trackNext == null) return;

            var track = RewardTracks.For(character);
            int next = track.NextRewardLevel(character.level);
            if (next <= 0)
            {
                // The end of the track is a real state, not an empty one. A
                // blank line here would read as a missing value.
                trackNext.SetContent("REWARD TRACK COMPLETE");
                return;
            }

            string reward = RewardTrackNames.Of(track.At(next));
            if (string.IsNullOrEmpty(reward))
            {
                // A reward kind with no name is a content-shaped gap rather
                // than an error -- say where it lands and stay quiet about
                // what it is, the same graceful-degradation posture the rest
                // of this screen takes for missing art.
                trackNext.SetContent($"NEXT REWARD AT LEVEL {next}");
                return;
            }

            trackNext.SetContent($"LEVEL {next}: {reward}");
        }

        // Highest first, so the top row IS the build.
        private void RefreshAttributes(AbilityScoreBlock scores, string characterId)
        {
            // SORTED ONCE PER CHARACTER, NOT ONCE PER REFRESH.
            //
            // The order is highest-first so the shape of a build reads off the
            // top row -- but spending a stat point CHANGES that order, and
            // Spend() refreshes. Re-sorting here meant the grid reshuffled
            // under the player's cursor mid-click: raise a score that was tied
            // with the one above it and the two swap cells, so pressing "+"
            // twice in the same place put the two points into two DIFFERENT
            // abilities. Nothing on screen warned them.
            //
            // The cost of freezing is that the row can be a little stale after
            // equipping something that moves a score. That is cosmetic -- every
            // value shown is still correct, only their arrangement is older --
            // and it re-sorts the moment the player steps to another character
            // and back.
            if (_cellOrder.Count == 0 || _cellOrderFor != characterId)
            {
                _cellOrderFor = characterId;
                _cellOrder.Clear();
                _cellOrder.AddRange(AbilityScores.All
                    .OrderByDescending(s => ValueOf(scores, s))
                    .ThenBy(s => (int)s));
            }

            // ONE tint, never two. A tie goes to the first declared, because two
            // highlighted attributes stop being a cue at all.
            AbilityScore dominant = _cellOrder[0];

            for (int i = 0; i < attributeCells.Length && i < _cellOrder.Count; i++)
            {
                var score = _cellOrder[i];
                bool isDominant = score == dominant;

                if (attributeValues[i] != null)
                {
                    attributeValues[i].SetContent(ValueOf(scores, score).ToString());
                    attributeValues[i].color = isDominant ? AccentHi : Neutral;
                }

                if (attributeKeys[i] != null)
                {
                    attributeKeys[i].SetContent(AbilityScores.ShortName(score));
                    attributeKeys[i].color = isDominant ? Accent : Dim;
                }
            }
        }

        private static int ValueOf(AbilityScoreBlock scores, AbilityScore score) => scores[score];

        private void RefreshStats(Character character, StatBlock stats, AbilityScoreBlock scores)
        {
            for (int i = 0; i < SheetStats.Derived.Length && i < statValues.Length; i++)
            {
                var stat = SheetStats.Derived[i];
                if (statValues[i] == null) continue;

                statValues[i].SetContent(DisplayValue(character, stat, stats, scores));
                statHighlights[i].SetShown(false);

                // THE ONE ROW WHOSE LABEL IS NOT AUTHORED COPY. Written on
                // every refresh rather than only when it differs, because the
                // dossier pages between characters in place: a label left
                // standing from the previous sheet is exactly the drift
                // UiStrings exists to stop, and the write is one string per
                // open.
                if (stat == SheetStat.MaxMana && Has(statNames, i))
                {
                    statNames[i].Set(UiStrings.StatMaxPool, _primaryPoolName);
                }
            }
        }

        private static bool Has<T>(T[] array, int index) where T : UnityEngine.Object =>
            array != null && index >= 0 && index < array.Length && array[index] != null;

        // Two rows do not live on StatBlock -- SheetStats.ValueOf returns 0 for
        // them by design rather than inventing a number, so they are resolved
        // here where the Character is in hand.
        private static string DisplayValue(Character character, SheetStat stat,
                                           StatBlock stats, AbilityScoreBlock scores) =>
            TryCurrentValue(character, stat, stats, scores, out int value) ? SheetStats.DisplayText(stat, value) : "-";

        // What a stat reads as RIGHT NOW, from the model.
        //
        // The equip preview needs the same number the row is showing, and it
        // used to get it by int.TryParse-ing the label's rendered text. That
        // works only for as long as every stat renders as a bare integer: the
        // moment one gains a separator, a unit or a percent sign the parse
        // fails silently, falls back to 0, and the preview prints a confident
        // wrong total. Signature gain ALREADY renders as "-" when a character
        // has no signature resource, so the failure case was live.
        //
        // False means there is no number to show, which is a different thing
        // from zero and the reason this is not just an int.
        private static bool TryCurrentValue(Character character, SheetStat stat,
                                            StatBlock stats, AbilityScoreBlock scores, out int value)
        {
            if (stat == SheetStat.MaxMana)
            {
                value = ContentDatabase.EffectiveMaxMana(character);
                return true;
            }

            if (stat == SheetStat.SignatureGain)
            {
                var signature = ContentDatabase.BuildSignatureResource(character);
                value = signature?.GainPerTurn ?? 0;
                return signature != null;
            }

            value = SheetStats.ValueOf(stat, stats, scores);
            return true;
        }

        // WHICH CAPACITY RULE THIS CHARACTER'S POOL PLAYS BY. Resolved
        // through ContentDatabase rather than read off a field on Character,
        // because the pool is content and a save carries only the id.
        // WisdomDerived when nothing resolves, which is the shipped answer
        // and the one that keeps the sheet reading as it always has.
        private static ResolvedPool PrimaryPoolFor(Character character) =>
            ContentDatabase.PrimaryPoolOf(character?.definitionId);

        // THE mechanic: light the rows this attribute actually feeds, and dim
        // the rest so the answer is unmissable.
        private void LightRowsFor(int cell, bool entered)
        {
            if (statHighlights == null || cell < 0 || cell >= _cellOrder.Count) return;

            var fed = entered
                ? new HashSet<SheetStat>(SheetStats.Feeds(_cellOrder[cell], _primaryPoolRule))
                : new HashSet<SheetStat>();

            for (int i = 0; i < SheetStats.Derived.Length && i < statHighlights.Length; i++)
            {
                bool lit = fed.Contains(SheetStats.Derived[i]);
                statHighlights[i].SetShown(lit);

                if (statValues[i] != null)
                {
                    statValues[i].color = !entered || lit ? Neutral : Dim;
                }
            }
        }

        private void RefreshSlots(Character character)
        {
            var loadout = ContentDatabase.ActiveLoadout(character);

            for (int i = 0; i < EquipmentSlots.All.Length && i < slotCells.Length; i++)
            {
                var slot = EquipmentSlots.All[i];

                if (slotLabels[i] != null)
                {
                    slotLabels[i].SetContent(EquipmentSlots.DisplayName(slot).ToUpperInvariant());
                }

                string itemId = character.equipment == null ? null : character.equipment.Get(slot);
                var item = string.IsNullOrEmpty(itemId) ? null : ContentDatabase.GetItem(itemId);

                // ACTIVATE the icon object, not just its Image. The node is
                // declared Inactive so an empty slot draws nothing, and
                // ItemIcons.Apply only sets `enabled` -- on an inactive
                // GameObject that is invisible either way, which is why the
                // slots came up bare with items equipped.
                bool hasArt = ItemIcons.Apply(slotIcons[i], icons, item?.id);
                slotIcons[i].gameObject.SetShown(hasArt);

                if (slotRarityTicks != null && i < slotRarityTicks.Length)
                {
                    slotRarityTicks[i].gameObject.SetShown(item != null);
                    if (item != null) slotRarityTicks[i].color = TierColour(item.tier);
                }

                // ITEM-MODIFIER PLAN PHASE E: the RiftTier ring, a SEPARATE
                // node from the rarity tick above so the two axes never
                // fight for one colour. Hidden whenever the roll is Ordinary
                // -- see RiftTierColors.ShouldGlow's own header -- which is
                // what keeps every item that has never touched this system
                // drawing exactly as it did before it existed.
                if (slotRiftGlows != null && i < slotRiftGlows.Length)
                {
                    var riftTier = (RiftTier)(character.equipment?.GetRiftTier(slot) ?? 0);
                    bool glows = item != null && RiftTierColors.ShouldGlow(riftTier);
                    slotRiftGlows[i].gameObject.SetShown(glows);
                    if (glows) slotRiftGlows[i].color = RiftTierColors.For(riftTier);
                }

                // A slot a two-hander has taken must never read as merely empty
                // -- the player has to see WHY it cannot be used.
                // INERT, not empty. A slot a two-hander has taken carries an
                // entry that is not live, which is exactly the case the player
                // must be shown a reason for rather than a blank square.
                bool blocked = item == null
                    && loadout.InertEntries != null
                    && loadout.InertEntries.Any(e => e.Entry.slot == slot);
                slotBlockedCaptions[i].SetShown(blocked);
            }
        }

        // ---- putting gear on and taking it off -----------------------------------
        //
        // Every RULE about what can be worn lives in EquipMove; this only picks
        // which item and then writes the save. Re-implementing the rules here is
        // how a screen ends up disagreeing with the one that already had them.

        private void EquipFromPack(int index)
        {
            var squad = Squad();
            if (squad.Count == 0 || index >= _bag.Count) return;

            var character = squad[_index];
            var save = SaveSlotManager.CurrentSave;
            if (character?.equipment == null || save == null) return;

            var item = _bag[index];

            // modifierIds/riftTier travel through the same way plus does --
            // InventoryOps.TryRemoveAt inside TryEquip keys on the full
            // (itemId, plus, modifierIds, riftTier) stack (see
            // RunOrchestrator.AutoEquipIntoAnEmptySlot's identical note),
            // so omitting them here would look for the wrong stack and
            // silently strip a rolled item's affixes the moment it is worn
            // from the pack.
            //
            // The "measure max health before the swap, rescale carried health
            // after it" pair that used to sit around this call is inside
            // EquipmentOps.Equip now -- three screens and the bot each had
            // their own copy of it. Nothing about the move changed.
            if (!EquipmentOps.Equip(save, character, item.Id, item.Slot, item.IsEquippable,
                                    plus: item.Plus,
                                    modifierIds: item.ModifierIds?.ToList(),
                                    riftTier: (int)item.RiftTier))
            {
                return;
            }

            // WRITTEN IMMEDIATELY. Gear that vanishes because the game closed
            // between an equip and a save is the least forgivable thing this
            // screen could do -- the old sheet said so and it still holds.
            SaveSlotManager.SaveCurrent();
            Refresh();

            // The pointer has not moved, but what is under it HAS: the item just
            // equipped left the pack and everything after it shifted up. Leaving
            // the tooltip alone left it naming an item that was no longer there,
            // beside a preview of a swap that had already happened. Re-asking
            // the hover handler for this cell is the whole fix -- it either
            // describes the new occupant or clears itself if the pack ran out.
            OnPackHover(index, entered: true);
        }

        private void UnequipSlot(int index)
        {
            var squad = Squad();
            if (squad.Count == 0 || index >= EquipmentSlots.All.Length) return;

            var character = squad[_index];
            var save = SaveSlotManager.CurrentSave;
            if (character?.equipment == null || save == null) return;

            // Scales carried health both ways -- see EquipmentOps.Unequip,
            // which carries the symmetry note this call site used to.
            if (!EquipmentOps.Unequip(save, character, EquipmentSlots.All[index]))
            {
                return;
            }

            SaveSlotManager.SaveCurrent();
            ClearPreview();
            HideTooltip();
            Refresh();
        }

        // ---- the equip preview ------------------------------------------------------
        //
        // Hovering a pack item writes the value each stat WOULD take beside it.
        // Computed by the same hypothetical-equip pass the Reckoning uses --
        // ItemDescription.Compare clones the character, equips the candidate and
        // re-resolves -- so the two screens cannot disagree about an item.

        private void ShowPreviewFor(BagItem entry)
        {
            var squad = Squad();
            var item = ContentDatabase.GetItem(entry.Id);
            if (squad.Count == 0 || item == null) { ClearPreview(); return; }

            var character = squad[_index];
            var comparison = ItemDescription.Compare(character, item, entry.Plus);
            _previewScores = _scores + comparison.ScoreDelta;

            var stats = ContentDatabase.EffectiveStats(character);

            // Bounded by BOTH arrays, not just one. They are built together and
            // are the same length, but half a guard is the shape of an
            // IndexOutOfRange that only shows up once somebody adds a stat.
            int count = System.Math.Min(SheetStats.Derived.Length,
                System.Math.Min(statPreviews.Length, statValues.Length));

            for (int i = 0; i < count; i++)
            {
                var stat = SheetStats.Derived[i];
                int delta = DeltaFor(stat, comparison);

                // ONLY the stats that actually move, and only those that have a
                // number to move. A column of arrows against unchanged numbers
                // buries the two rows that did change.
                if (delta == 0 || !TryCurrentValue(character, stat, stats, _scores, out int now))
                {
                    if (statPreviews[i] != null) statPreviews[i].SetContent("");
                    if (statValues[i] != null) statValues[i].color = Neutral;
                    continue;
                }

                if (statPreviews[i] != null)
                {
                    statPreviews[i].SetContent("> " + (now + delta));
                    statPreviews[i].color = delta > 0 ? Good : Bad;
                }

                // The current figure steps back so the new one leads.
                if (statValues[i] != null) statValues[i].color = Dim;
            }
        }

        private void ClearPreview()
        {
            if (statPreviews == null) return;

            for (int i = 0; i < statPreviews.Length; i++)
            {
                if (statPreviews[i] != null) statPreviews[i].SetContent("");
                if (statValues != null && i < statValues.Length && statValues[i] != null)
                {
                    statValues[i].color = Neutral;
                }
            }
        }

        private int DeltaFor(SheetStat stat, ItemComparison comparison)
        {
            switch (stat)
            {
                case SheetStat.MaxHealth: return comparison.StatDelta.maxHealth;
                case SheetStat.Attack: return comparison.StatDelta.attack;
                case SheetStat.Speed: return comparison.StatDelta.speed;
                case SheetStat.ManaRegen: return comparison.StatDelta.manaRegen;
                case SheetStat.PhysicalDefense: return comparison.StatDelta.physicalDefense;
                case SheetStat.MagicalDefense: return comparison.StatDelta.magicalDefense;

                // These two are derived from WISDOM and CHARISMA, so an item
                // that shifts an ability score shifts them as well -- which a
                // StatDelta alone would miss entirely.
                //
                // UNLESS THE POOL IS Fixed, in which case Wisdom moves its
                // capacity by nothing and the honest preview is 0. Reading
                // the same rule the row's own Wisdom link reads, so the
                // highlight and the preview cannot disagree.
                case SheetStat.MaxMana:
                    return _primaryPoolRule != PoolCapacityRule.WisdomDerived
                        ? 0
                        : AbilityDerivation.MaxManaBonus(_previewScores)
                          - AbilityDerivation.MaxManaBonus(_scores);
                case SheetStat.SignatureGain:
                    return AbilityDerivation.SignatureGainBonus(_previewScores)
                         - AbilityDerivation.SignatureGainBonus(_scores);
                default: return 0;
            }
        }

        // ---- the shared tooltip -------------------------------------------------
        //
        // ONE instance for the whole screen, as the handover specifies. The
        // DERIVATION lives here rather than on the row: printing "396 from CON"
        // beside every stat would double the width of column C to say something
        // the player only wants once.

        private void OnAttributeHover(int cell, bool entered)
        {
            LightRowsFor(cell, entered);

            if (!entered || cell >= _cellOrder.Count) { HideTooltip(); return; }

            var score = _cellOrder[cell];
            ShowTooltip(AbilityScores.ShortName(score),
                       SheetStats.PerPointSummary(_scores, score, _primaryPoolRule),
                       RectOf(attributeCells, cell));
        }

        private void OnSlotHover(int index, bool entered)
        {
            if (!entered || index >= EquipmentSlots.All.Length) { HideTooltip(); return; }

            var squad = Squad();
            if (squad.Count == 0) { HideTooltip(); return; }

            var slot = EquipmentSlots.All[index];
            var character = squad[_index];
            string itemId = character.equipment?.Get(slot);
            var item = string.IsNullOrEmpty(itemId) ? null : ContentDatabase.GetItem(itemId);

            if (item == null)
            {
                ShowTooltip(EquipmentSlots.DisplayName(slot), "Nothing equipped.", RectOf(slotCells, index));
                return;
            }

            // PLUS, NOT JUST THE ITEM: found while wiring the DMG line
            // (D7.2) -- this used to call CardSummary(item) with no plus at
            // all, so an equipped slot's OWN hover silently showed the
            // unhoned figure for every stat, weapon included. Pre-existing
            // and out of D7's scope to have gone looking for, but the DMG
            // line this phase adds would have made it worse (a honed sword's
            // card understating its own damage), not merely stayed wrong.
            int plus = character.equipment?.GetPlus(slot) ?? 0;

            // ITEM-MODIFIER PLAN PHASE E: read BEFORE anything displaces this
            // entry, same rule EquipmentLoadout.GetModifierIds/GetRiftTier's
            // own header states for GetPlus.
            var modifierIds = character.equipment?.GetModifierIds(slot);
            var riftTier = (RiftTier)(character.equipment?.GetRiftTier(slot) ?? 0);

            ShowTooltip(RarityColors.NameOf(item),
                ItemDescription.CardSummary(item, plus, character, riftTier, modifierIds),
                RectOf(slotCells, index));
        }

        private void OnPackHover(int index, bool entered)
        {
            if (!entered || index >= _bag.Count) { HideTooltip(); ClearPreview(); return; }

            var entry = _bag[index];
            var item = ContentDatabase.GetItem(entry.Id);
            var squad = Squad();

            // COMPARISON, not the bare card. "ATK +3" on its own is a number
            // with no context -- the player cannot tell whether that is
            // meaningful or trivial without knowing what they already have.
            // ComparisonBody is the same VS.-EQUIPPED breakdown the stat sheet
            // preview already computes; this just also puts it in words in the
            // tooltip itself, since the live preview only covers the derived
            // stat ROWS and not everything a body of text can say (cascade
            // notes, requirement lines).
            string body = item == null ? ""
                : squad.Count > 0 ? ItemDescription.ComparisonBody(squad[_index], item, entry.Plus, entry.RiftTier, entry.ModifierIds)
                : ItemDescription.CardSummary(item, entry.Plus, riftTier: entry.RiftTier, modifierIds: entry.ModifierIds);

            ShowTooltip(entry.Name, body, RectOf(packCells, index));
            ShowPreviewFor(entry);
        }

        private void ShowTooltip(string title, string body, RectTransform near)
        {
            if (tooltip == null) return;

            if (tooltipTitle != null) tooltipTitle.SetContent(title ?? "");
            if (tooltipBody != null) tooltipBody.SetContent(body ?? "");
            PlaceTooltip(near);
            tooltip.SetShown(true);
        }

        // The tooltip follows what it describes.
        //
        // It was authored at a FIXED position -- the dossier's own centre --
        // which put it squarely over the mannequin for every hover. That is the
        // worst possible place for it: the whole point of hovering a pack item
        // is to weigh it against what is currently worn, and the box answering
        // the question was covering the evidence. It also hid the Weapon 1 slot
        // exactly when an equip landed there.
        //
        // Beside the cell if there is room on the right, flipped to the left if
        // there is not, and clamped so it never leaves the panel.
        //
        // The arithmetic moved to TooltipPlacement when the Reckoning needed
        // the same answer for its offer cards. What is left here is the part
        // that is genuinely this screen's: which rect is being hovered, and
        // what space it has to be expressed in.
        private void PlaceTooltip(RectTransform near)
        {
            var self = tooltip == null ? null : tooltip.transform as RectTransform;
            var parent = self == null ? null : self.parent as RectTransform;
            if (self == null || parent == null || near == null) return;

            // Through world space, because the pack cells live inside the pack
            // panel and the tooltip does not; their local coordinates are not
            // the same space and treating them as one is how the pack children
            // ended up 339px off the panel they belong to.
            Vector2 local = parent.InverseTransformPoint(near.TransformPoint(Vector3.zero));

            const float Margin = 8f;
            var at = TooltipPlacement.Beside(
                local.x, local.y, near.rect.width, near.rect.height,
                self.sizeDelta.x, self.sizeDelta.y,
                interiorLeft: -DossierLayout.HalfWidth + Margin,
                interiorRight: DossierLayout.HalfWidth - Margin,
                interiorBottom: -DossierLayout.HalfHeight + Margin,
                interiorTop: DossierLayout.HalfHeight - Margin);

            self.anchoredPosition = new Vector2(at.X, at.Y);
        }

        private static RectTransform RectOf(Button[] cells, int index)
        {
            if (cells == null || index < 0 || index >= cells.Length || cells[index] == null) return null;
            return cells[index].transform as RectTransform;
        }

        private void HideTooltip() => tooltip.SetShown(false);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;

            }
}
