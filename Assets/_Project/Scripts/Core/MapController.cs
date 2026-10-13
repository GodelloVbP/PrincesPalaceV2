using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The descent map, painted from the run.
    //
    // The tree built a pool at every position a leg COULD use; this switches on
    // the ones the current leg actually has, gives each the art its room type
    // earns, and scrolls the whole wood so the party's own room sits on the
    // left painted clearing.
    //
    // Nothing here recomputes a coordinate. Every x, every width and the scroll
    // itself come out of MapLayout, which is the same arithmetic the declared
    // tree was built from -- so a room, its trail and the clearing it stands in
    // cannot disagree.
    public partial class MapController : MonoBehaviour
    {
        // The scrolling half. `viewport` is the fixed masked window; `content`
        // is the wide rect that slides under it.
        [SerializeField] internal RectTransform viewport;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal Image[] backdrops;
        [SerializeField] internal RectTransform fog;

        // The party's figure. See MapController.Walk.cs for everything it does.
        [SerializeField] internal Image walker;

        [SerializeField] internal Button[] nodeButtons;
        [SerializeField] internal Image[] nodeIcons;
        [SerializeField] internal TMP_Text[] nodeLabels;
        [SerializeField] internal Image[] nodeMarkers;
        [SerializeField] internal Image[] trailSegments;
        [SerializeField] internal Image[] trailCores;
        [SerializeField] internal TMP_Text depthLabel;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal TMP_Text attentionLabel;
        [SerializeField] internal GameObject attentionBadge;

        // The card for the hovered or focused room (MapNodeCard.Body), shown
        // through _preview so pad focus outranks the pointer.
        [SerializeField] internal GameObject previewPanel;
        [SerializeField] internal TMP_Text previewTitle;
        [SerializeField] internal TMP_Text previewBody;

        // What the room the party just walked into did. Empty between rooms,
        // rather than left showing the last one's result under a new heading.
        [SerializeField] internal TMP_Text roomMessageLabel;

        // The character sheet, mounted here too. UNLOCKED: the map between two
        // rooms is exactly where changing gear is supposed to happen, and
        // walking back to the hub to swap a breastplate was what made having it
        // in the fight only half an answer.
        [SerializeField] internal GameObject characterSheetPanel;
        [SerializeField] internal Button abandonButton;

        // The in-run shop, mounted here too (F10, docs/PLAN_SHOP.md) --
        // entered from the map, no scene of its own, opened and closed by
        // MapController.Walk.cs's Arrive() rather than by Navigation.Go.
        [SerializeField] internal ShopController shop;

        // An event room's panel, mounted the same way; opened through
        // ShowEventPanel (MapController.Event.cs).
        [SerializeField] internal EventController eventPanel;

        // The debug menu, F1 in the editor or a development build only -- the
        // same gate and the same reason as HubController's copy.
        [SerializeField] internal GameObject debugMenuPanel;

        // The painted room icons. Bound by the wiring step rather than loaded
        // here: an "Assets/..." path is an editor-only address, and Core cannot
        // see the editor assembly.
        [SerializeField] internal Sprite fightIcon;
        [SerializeField] internal Sprite eliteIcon;
        [SerializeField] internal Sprite bossIcon;
        [SerializeField] internal Sprite restIcon;
        [SerializeField] internal Sprite eventIcon;
        [SerializeField] internal Sprite treasureIcon;

        // A whole-tile colour MULTIPLY, applied to the painted tree and its icon
        // at once.
        //
        // Current is deliberately ABOVE 1. Under the stock UI shader that
        // brightens past the sprite's own colour, and it is the only thing that
        // makes "you are here" read as LIT rather than merely as un-dimmed --
        // which matters because every tile is the same painted tree, so
        // brightness is all there is to say it with. This is also why these are
        // Colors here and not hexes in MapScreen alongside the trail palette: a
        // hex string cannot express a channel above 1.
        private static readonly Color CurrentTint = new Color(1.16f, 1.08f, 0.90f, 1f);
        private static readonly Color WalkedTint = new Color(0.95f, 0.85f, 0.66f, 0.95f);
        private static readonly Color OpenTint = Color.white;
        private static readonly Color AheadTint = new Color(0.55f, 0.50f, 0.62f, 0.80f);
        private static readonly Color ClosedTint = new Color(0.32f, 0.30f, 0.36f, 0.55f);

        // Parsed from the SCREEN's own constants rather than restated here, so
        // the declared tree and the runtime cannot disagree about what a walked
        // path looks like.
        //
        // ColorUtility, not SceneBuilder.ParseHex: that one lives in the Editor
        // assembly and this is Core, which cannot see it. Unity's own parser
        // takes the same "#RRGGBBAA" the DSL writes.
        private static readonly Color TrailTaken = Hex(MapScreen.TrailTakenHex);
        private static readonly Color TrailOpen = Hex(MapScreen.TrailOpenHex);
        private static readonly Color TrailAhead = Hex(MapScreen.TrailAheadHex);
        private static readonly Color TrailClosed = Hex(MapScreen.TrailClosedHex);

        // The track half of each road, parsed from the screen's constants for
        // the same reason the verge half is.
        private static readonly Color CoreTaken = Hex(MapScreen.TrailCoreTakenHex);
        private static readonly Color CoreOpen = Hex(MapScreen.TrailCoreOpenHex);
        private static readonly Color CoreAhead = Hex(MapScreen.TrailCoreAheadHex);
        private static readonly Color CoreClosed = Hex(MapScreen.TrailCoreClosedHex);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.white;

        private bool _wired;

        private void Start()
        {
            Wire();
            // Pushed BEFORE the first Refresh(): RefreshNavLinks (called from
            // Refresh) reconfigures _navContext rather than creating it, so a
            // Refresh with no context yet to reconfigure would silently wire
            // nothing on the very first paint.
            RegisterNavContext();
            _preview.Attach(NavigationInputModule.Contexts);

            if (systemMenu != null) systemMenu.Closed += RefreshAttention;

            // A debug grant can move the gold, the pending-book count and the
            // leg itself; the map repaints after each one rather than on the
            // next room.
            var debug = debugMenuPanel != null ? debugMenuPanel.GetComponent<DebugMenuController>() : null;
            if (debug != null) debug.Changed = RefreshAndShowLiveEvent;

            RefreshAndShowLiveEvent();
        }

        // THE RELOAD HALF OF AN EVENT ROOM (plan contract 2), and new
        // behaviour rather than copied: the shop does NOT do this -- OpenShop
        // has one caller, Walk.Arrive, so a Map scene that starts with a shop
        // open shows the map. An event open on the node the party stands on
        // reopens its panel here, on the same event and page, because both
        // are in the save (RunSnapshot.eventId / eventPageId).
        //
        // Also the debug menu's repaint, so "open event" from the Tools tab
        // shows the panel it just opened.
        private void RefreshAndShowLiveEvent()
        {
            Refresh();
            if (RunOrchestrator.EventIsOpen) OpenEvent();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < nodeButtons.Length; i++)
            {
                int index = i;
                var button = nodeButtons[i];
                var node = button.gameObject;
                button.onClick.AddListener(() => OnNodePressed(index));

                // The detail card follows pointer and pad focus alike, through
                // the router that already decides which of the two owns a card.
                _preview.Register(button, shown => ShowPreview(index, shown));
                var hover = node.GetComponent<HoverIndex>() ?? node.AddComponent<HoverIndex>();
                hover.Changed = (_, entered) => _preview.Pointer(node, entered);

                // A press on a room the party cannot enter never reaches
                // onClick, so it is heard here and answered on the caption.
                var click = node.GetComponent<ClickIndex>() ?? node.AddComponent<ClickIndex>();
                click.Clicked = _ => RefuseIfNotOffered(index);
            }

            // Opens the menu on its Exit tab rather than ending the run: the
            // Exit pane's hold-to-abandon is the one gesture that gives up a
            // descent (AUDIT #219; the hub's corner button does the same).
            abandonButton.onClick.AddListener(OpenExitTab);
        }

        internal void OpenExitTab()
        {
            if (!SystemMenuController.OpenFromRoot(systemMenu)) return;
            systemMenu.Select(SystemMenuTab.Exit);
        }

        // ---- the character sheet, between rooms --------------------------------

        // The map's own base context, mirroring HubController's -- pushed
        // once, never popped while this scene is loaded. No Selectables yet
        // (node-button navigation is the Graph group RefreshNavLinks below
        // gives Map). Start and the menu hotkeys reach the system menu through
        // it; its Cancel does nothing, and a modal that pushes its own context
        // over it absorbs all of them.
        private NavContext _navContext;

        // The overarching menu this screen's Cancel opens, assigned by
        // ScreenRegistry.WireMap from the same WireSystemMenu call that
        // builds it -- the hub holds its copy the same way.
        [SerializeField] internal SystemMenuController systemMenu;

        private void Update()
        {
            // The hint names a key, and the key follows the device in hand.
            if (attentionLabel != null && attentionLabel.gameObject.activeSelf
                && _attentionPad != NavigationInputModule.LastInputWasPad)
            {
                RefreshAttention();
            }

            if (NavigationInputModule.Contexts != null && !NavigationInputModule.Contexts.IsTop(_navContext))
            {
                return;
            }

            // Gated on the KEY, not on SetDebugMenu, so PlayMode can still
            // drive the methods -- see HubController's F1 for the same split.
            if (Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F1))
            {
                ToggleDebugMenu();
                return;
            }

            // Cancel/Escape is not read here at all any more -- see the
            // context comment above.
        }

        private void RegisterNavContext()
        {
            if (_navContext != null) return;

            // systemMenu, NOT cancel -- see HandleSystemMenu below.
            _navContext = new NavContext(entry: null, selectables: null,
                cancel: null, systemMenu: HandleSystemMenu,
                menuHotkey: hotkey => MenuHotkeys.Press(systemMenu, hotkey));
            NavigationInputModule.Contexts?.Push(_navContext);
        }

        // Map's START, called once a frame by the ONE dispatch point when
        // this context is top. The map has nothing of its own to back out
        // of -- there is no submenu depth here the way there is in a fight
        // -- so its Cancel is nothing at all, and this is the only way in.
        // Once the menu
        // IS open its own context sits above this one, so a second Start
        // reaches the menu's own handler, never this.
        private void HandleSystemMenu() => SystemMenuController.OpenFromRoot(systemMenu);

        // THE MAP'S HINT THAT A CHARACTER WANTS LOOKING AT (plan C20): what
        // waits, from the same model and the same feed as the strip's pips, so
        // it names only what the player will find lit. Neither the shop nor the
        // menu is guaranteed to be the next thing opened after a purchase or a
        // drop, so the map says so; it is repainted when the menu closes
        // because that is when the answer moves, and when the last input
        // changes device because the key it names changes with it.
        private void RefreshAttention()
        {
            if (attentionLabel == null) return;

            var run = RunManager.Run;
            var reasons = run == null
                ? AttentionReasons.None
                : CharacterAttentionFeed.ReasonsFor(SaveSlotManager.CurrentSave, run);

            _attentionPad = NavigationInputModule.LastInputWasPad;
            attentionLabel.gameObject.SetActive(reasons != AttentionReasons.None);
            if (attentionBadge != null) attentionBadge.SetActive(reasons != AttentionReasons.None);
            if (reasons != AttentionReasons.None)
            {
                attentionLabel.Set(UiStrings.MapAttention, AttentionHint.Named(reasons),
                    AttentionHint.Key(PromptGlyphs.DeviceFor(_attentionPad)));
            }
        }

        private bool _attentionPad;

        private void OnDestroy()
        {
            if (systemMenu != null) systemMenu.Closed -= RefreshAttention;
            _preview.Detach();

            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        // Separated from the key for the reason HubController documents:
        // legacy Input cannot be simulated headlessly, so a test that had to
        // press C could not exist. Tests drive these directly.
        public bool DebugMenuIsOpen => debugMenuPanel != null && debugMenuPanel.activeSelf;

        public void ToggleDebugMenu() => SetDebugMenu(!DebugMenuIsOpen);

        public void SetDebugMenu(bool open)
        {
            if (debugMenuPanel == null) return;
            debugMenuPanel.SetActive(open);
        }

        public bool CharacterSheetIsOpen => SheetPanel.IsOpen(characterSheetPanel);

        public void ToggleCharacterSheet(bool inventory = false) =>
            SheetPanel.Toggle(characterSheetPanel, inventory);

        public void SetCharacterSheet(bool open, bool inventory = false) =>
            SheetPanel.Set(characterSheetPanel, open, inventory);

        // Gate 2's replacement for the gate-1 shim (MapController.Walk.cs's
        // Arrive()): a nested panel, so no Navigation.Go, and closing it is
        // the shop's own LEAVE button rather than anything this method does.
        public void OpenShop()
        {
            if (shop == null) return;

            shop.Finished = Refresh;
            shop.Open();
        }

        // The event room's panel, opened on arrival (Walk.Arrive) and on a
        // Map start that finds one open (RefreshAndShowLiveEvent).
        //
        // PHASE 3 PLUGS THE PANEL IN THROUGH ShowEventPanel: a partial method
        // with no body until MapController.Event.cs implements it, so until
        // then the call compiles away and walking into an event shows the map
        // with the event open in the save. That is the null-safe hook -- no
        // field to leave unwired, nothing to null-check. The panel reads
        // RunOrchestrator.CurrentEvent(), calls ChooseEventOption / LeaveEvent,
        // and hands Refresh back through Finished exactly as OpenShop does.
        public void OpenEvent()
        {
            if (!RunOrchestrator.EventIsOpen) return;
            ShowEventPanel();
        }

        partial void ShowEventPanel();

        public void Refresh()
        {
            var map = RunManager.Map;
            var current = RunManager.CurrentNode;

            // No run: every node off, and the labels say nothing rather than
            // lying about a descent that is not happening.
            if (map == null || current == null)
            {
                foreach (var button in nodeButtons) button.gameObject.SetShown(false);
                HideTrails(0);
                HideBackdrops(0);
                fog.gameObject.SetShown(false);
                walker.gameObject.SetShown(false);
                if (attentionLabel != null) attentionLabel.gameObject.SetActive(false);
                if (attentionBadge != null) attentionBadge.SetActive(false);
                RefreshNavLinks(null, null);
                return;
            }

            // A repaint can change what the card describes (a room cleared, a
            // grant from the debug menu), so it closes rather than going stale.
            _preview.ForceClose();

            var reachable = new HashSet<int>(RunManager.Choices().Select(n => n.Id));
            var ahead = map.AllReachableFrom(current.Id);
            var cleared = new HashSet<int>(RunManager.Run.clearedNodeIds);
            var columns = Columns(map);

            depthLabel.Set(UiStrings.MapDepth, RunManager.Run.roomsWalked, RunManager.Run.floor);
            goldLabel.Set(UiStrings.MapGold, RunManager.Run.gold);

            RefreshAttention();

            // Painted from the LAST resolved room, which Arrive has already
            // written by the time this runs. A fight clears it on the way out,
            // so a stash line cannot survive under a room that did not pay it.
            //
            // Hidden rather than blanked when there is nothing to say: an empty
            // label is still a box the text-fit audit has to reason about, and
            // a stale-looking gap under the depth line reads as a missing
            // string rather than as a room that had nothing to report.
            if (roomMessageLabel != null)
            {
                bool hasMessage = RoomResolver.TryMessage(RoomResolver.Last, out var message, out var args);
                roomMessageLabel.gameObject.SetShown(hasMessage);
                if (hasMessage) roomMessageLabel.Set(message, args);
            }

            LayoutWood(MapColumns.Count);

            // Graceful rather than silent: a designed profile whose pockets
            // outgrow the column pool draws what fits and says so.
            if (columns.Overflow.Count > 0)
            {
                Debug.LogWarning($"MapController: {columns.Overflow.Count} room(s) do not fit the column pool " +
                                 $"({MapLayout.Rows} rows); MapColumns.MaxRows needs raising");
            }

            var occupied = new HashSet<int>();

            foreach (var node in map.Nodes)
            {
                int index = columns.IndexOf(node.Id);
                if (index < 0 || index >= nodeButtons.Length) continue;

                occupied.Add(index);
                PaintNode(index, node, columns,
                    StateOf(node, current, reachable, ahead, cleared),
                    isReachable: reachable.Contains(node.Id));
            }

            // Everything the leg did not use. Hidden rather than dimmed: an
            // empty slot is not a room the player failed to reach, it is a room
            // that does not exist.
            for (int i = 0; i < nodeButtons.Length; i++)
            {
                if (!occupied.Contains(i)) nodeButtons[i].gameObject.SetShown(false);
            }

            PaintTrails(map, columns, current.Id, cleared, reachable);

            // Not while a walk is in flight: the walk owns both the figure and
            // the camera until it arrives, and a refresh from anything else
            // would snap them to a room the party has not reached yet.
            if (!IsWalking)
            {
                PlaceWalker(current);
                FollowCurrent(MapLayout.ColumnX(columns.ColumnOf(current.Id)));
            }

            RefreshNavLinks(current, RunManager.Choices());
        }

        // ---- gamepad navigation: the Graph shape (docs/GAMEPAD_NAVIGATION_PLAN.md
        // section 5's "Graph (Map, links from MapController.
        // Refresh())") -------------------------------------------------------
        //
        // UiNavSpec has no Graph enum member -- UiNavLinkBuilder's own header
        // already says why: "Map's Graph shape has no groups at all and is
        // entirely this loop [Links]". So this is EXPLICIT LINKS ONLY, no
        // RuntimeNavWiring.Group at all, run through the builder's existing
        // inter-group link mechanism rather than a new code path.
        //
        // The edges ARE RunManager.Choices() (`:245` above, the same call
        // this method already made for `reachable`): current -> its first
        // choice via Right (deeper is rightward, MapLayout's own column
        // convention), each further choice reached by stepping Down from the
        // one before it in screen order (MapColumns: column, then row top to
        // bottom), and Left from every choice back to current. No wrap: "Map
        // follows reachability" -- there is nothing to wrap TO, since a
        // realm's branching factor is whatever the generator gave it here.
        //
        // Entry is the current node's first reachable choice, not current
        // itself: current's own button is never interactable (isReachable is
        // false for it, PaintNode's own `button.interactable = isReachable`),
        // so selecting it would land on a Submit that does nothing.
        private void RefreshNavLinks(RealmNode current, IReadOnlyList<RealmNode> choices)
        {
            if (_navContext == null) return;

            if (current == null || choices == null || choices.Count == 0)
            {
                // Screen-level fallback (plan section 6): nothing to select,
                // Cancel stays live through the context's own Cancel handler.
                _navContext.Reconfigure(entry: null, selectables: null);
                return;
            }

            // Screen order: the nearer column first, then top to bottom.
            var columns = Columns(RunManager.Map);
            var ordered = choices.OrderBy(n => columns.ColumnOf(n.Id)).ThenBy(n => columns.RowOf(n.Id)).ToList();
            var currentButton = ButtonFor(current);
            var choiceButtons = ordered.Select(ButtonFor).Where(b => b != null).ToList();

            var links = new System.Collections.Generic.List<UiNavLink<Selectable>?>();
            if (choiceButtons.Count > 0)
            {
                links.AddRange(RuntimeNavWiring.LinkBoth(currentButton, UiNavDirection.Right, choiceButtons[0]));

                for (int i = 1; i < choiceButtons.Count; i++)
                {
                    // Down steps to the next choice; Up steps back to the one
                    // before it. LinkBoth makes writing only one direction of
                    // this pair impossible.
                    links.AddRange(RuntimeNavWiring.LinkBoth(choiceButtons[i - 1], UiNavDirection.Down, choiceButtons[i]));
                    links.Add(RuntimeNavWiring.Link(choiceButtons[i], UiNavDirection.Left, currentButton));
                }
            }

            RuntimeNavWiring.Apply(System.Array.Empty<UiNavGroup<Selectable>>(), links);

            var selectables = new System.Collections.Generic.Dictionary<string, object>();
            if (currentButton != null) selectables["current"] = currentButton.gameObject;
            for (int i = 0; i < choiceButtons.Count; i++) selectables[$"choice{i}"] = choiceButtons[i].gameObject;

            _navContext.Reconfigure(entry: choiceButtons.Count > 0 ? choiceButtons[0].gameObject : null, selectables);
        }

        private Button ButtonFor(RealmNode node)
        {
            if (node == null) return null;
            int index = Columns(RunManager.Map).IndexOf(node.Id);
            return index >= 0 && index < nodeButtons.Length ? nodeButtons[index] : null;
        }

        // The projection for this realm, built once per map rather than per
        // query: Refresh, the walk and every hover ask it.
        private MapColumns _columns;
        private RealmMap _columnsMap;

        // Each caption's place as the tree built it, read before any repaint
        // moves one.
        private float[] _captionRestY;

        private MapColumns Columns(RealmMap map)
        {
            if (_columns == null || !ReferenceEquals(_columnsMap, map))
            {
                _columns = MapColumns.Of(map);
                _columnsMap = map;
            }

            return _columns;
        }

        // Which of the five a room is in. Order matters: a cleared room the
        // party is standing in is CURRENT, and a room the party can still
        // reach by any route is AHEAD whether or not a trail to it is open
        // yet, while one no route reaches any more is a road not taken.
        private static MapTileState StateOf(RealmNode node, RealmNode current, HashSet<int> reachable,
                                            HashSet<int> ahead, HashSet<int> cleared)
        {
            if (node.Id == current.Id) return MapTileState.Current;
            if (cleared.Contains(node.Id)) return MapTileState.Walked;
            if (reachable.Contains(node.Id)) return MapTileState.Open;
            return ahead.Contains(node.Id) ? MapTileState.Ahead : MapTileState.Closed;
        }

        // The wood itself: how much of it exists, how far it repeats, and where
        // it stops. All three are per-leg facts, so they are re-stated on every
        // refresh rather than baked into the tree.
        private void LayoutWood(int depthCount)
        {
            float viewportWidth = viewport.rect.width;

            content.sizeDelta = new Vector2(
                MapLayout.ContentWidth(depthCount, viewportWidth), content.sizeDelta.y);

            int needed = MapLayout.BackgroundTilesFor(depthCount);
            if (needed > backdrops.Length)
            {
                // Graceful rather than silent: the wood runs out at the right
                // edge instead of the leg failing to draw, and the log says
                // which number to raise.
                Debug.LogWarning(
                    $"MapController: a {depthCount}-column leg needs {needed} forest tiles, the pool holds {backdrops.Length}");
                needed = backdrops.Length;
            }

            for (int i = 0; i < backdrops.Length; i++)
            {
                backdrops[i].gameObject.SetShown(i < needed);
            }

            fog.gameObject.SetShown(true);
            fog.anchoredPosition = new Vector2(
                MapLayout.FogX(depthCount) + MapLayout.FogWidth * 0.5f, fog.anchoredPosition.y);
        }

        // Auto-scroll. Keeps the party's own room pinned to the left painted
        // clearing, which puts the column they are choosing into on the right
        // one -- the two-clearing pair the background paints is exactly the
        // decision in front of them.
        private void FollowCurrent(float currentX)
        {
            content.anchoredPosition = new Vector2(
                MapLayout.Scroll(currentX, content.sizeDelta.x, viewport.rect.width),
                content.anchoredPosition.y);
        }

        // The paths between rooms, which is what makes this a map rather than a
        // grid of buttons. Without them nothing on screen says which room leads
        // to which, and a fork is indistinguishable from two unrelated rooms
        // that happen to sit in the same column.
        private void PaintTrails(RealmMap map, MapColumns columns, int currentId, HashSet<int> cleared,
                                 HashSet<int> reachable)
        {
            if (trailSegments == null || trailSegments.Length == 0) return;

            var positions = NodePositions(map, columns);
            var seeds = LinkSeeds(map);
            int segment = 0;

            foreach (var node in map.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var from)) continue;

                foreach (int nextId in node.Next)
                {
                    if (!positions.TryGetValue(nextId, out var to)) continue;

                    // LOOKED UP, not counted: a shared seed dictionary means
                    // the walk and the trail-build agree on a link's jitter
                    // by construction rather than by two loops incrementing
                    // a counter identically.
                    int seed = seeds.TryGetValue(LinkKey(node.Id, nextId), out var s) ? s : 1;

                    // The pool is sized to the worst case a leg can generate,
                    // so running out means the generator changed shape rather
                    // than that this needs a bigger number. Stop rather than
                    // draw half a trail.
                    if (segment + MapLayout.SegmentsPerLink > trailSegments.Length) break;

                    // WALKED means both ends are behind you. Colouring a trail
                    // by its destination alone would light up every path INTO a
                    // cleared room, including ones never taken.
                    bool taken = cleared.Contains(node.Id) && cleared.Contains(nextId);
                    bool fromHere = node.Id == currentId;

                    Color colour;
                    Color core;
                    float width;
                    if (taken)
                    { colour = TrailTaken; core = CoreTaken; width = MapScreen.TrailWidthTaken; }
                    else if (fromHere)
                    { colour = TrailOpen; core = CoreOpen; width = MapScreen.TrailWidthOpen; }
                    else if (reachable.Contains(node.Id) || cleared.Contains(node.Id))
                    { colour = TrailAhead; core = CoreAhead; width = MapScreen.TrailWidthAhead; }
                    else
                    { colour = TrailClosed; core = CoreClosed; width = MapScreen.TrailWidthClosed; }

                    for (int i = 0; i < MapLayout.SegmentsPerLink; i++)
                    {
                        var piece = MapLayout.SegmentAt(from, to, seed, i);
                        var image = trailSegments[segment++];

                        image.gameObject.SetShown(true);
                        image.color = colour;

                        // Indexed off the SAME counter as its verge, so the two
                        // halves of one road can never describe two different
                        // states.
                        if (trailCores != null && segment - 1 < trailCores.Length)
                        {
                            trailCores[segment - 1].color = core;
                        }

                        var rect = image.rectTransform;
                        rect.sizeDelta = new Vector2(piece.Length, width);
                        rect.anchoredPosition = new Vector2(piece.Centre.X, piece.Centre.Y);
                        rect.localRotation = Quaternion.Euler(0f, 0f, piece.AngleDegrees);
                    }
                }
            }

            HideTrails(segment);
        }

        // Where every room in this realm actually sits, by the same projection
        // the nodes are placed with -- so a trail can never land somewhere its
        // room is not.
        private static Dictionary<int, UiVec> NodePositions(RealmMap map, MapColumns columns)
        {
            var positions = new Dictionary<int, UiVec>();
            foreach (var node in map.Nodes)
            {
                if (columns.Contains(node.Id)) positions[node.Id] = columns.PositionOf(node.Id);
            }

            return positions;
        }

        private void HideTrails(int from)
        {
            if (trailSegments == null) return;
            for (int i = from; i < trailSegments.Length; i++)
            {
                if (trailSegments[i] != null) trailSegments[i].gameObject.SetShown(false);
            }
        }

        private void HideBackdrops(int from)
        {
            if (backdrops == null) return;
            for (int i = from; i < backdrops.Length; i++)
            {
                if (backdrops[i] != null) backdrops[i].gameObject.SetShown(false);
            }
        }

        private void PaintNode(int index, RealmNode node, MapColumns columns, MapTileState state, bool isReachable)
        {
            var button = nodeButtons[index];
            button.gameObject.SetShown(true);

            // The tile is a BOUNDING BOX, not a stretch target: the tree keeps
            // its own proportions inside an elite's or a boss's bigger box
            // rather than distorting to fill it. A crowded column shrinks
            // every tile in it (MapLayout.TileScale).
            float scale = MapLayout.TileScale(columns.CountIn(columns.ColumnOf(node.Id)));
            float width = MapScreen.TileWidthFor(node.Type) * scale;
            float height = width + MapLayout.TileHeightBonus * scale;

            var tint = TintFor(state);

            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, height);
            var at = columns.PositionOf(node.Id);
            rect.anchoredPosition = new Vector2(at.X, at.Y);

            // The caption hangs below its tile; in a shrunk column it follows
            // the smaller tile up so it does not sit on the room beneath, and
            // returns to where the tree put it when the slot is reused in a
            // roomier column.
            var caption = nodeLabels[index].rectTransform;
            _captionRestY ??= nodeLabels.Select(l => l.rectTransform.anchoredPosition.y).ToArray();
            caption.anchoredPosition = new Vector2(caption.anchoredPosition.x,
                scale < 1f ? -height * 0.5f - 16f : _captionRestY[index]);

            var tree = button.targetGraphic as Image;
            if (tree != null)
            {
                tree.preserveAspect = true;
                tree.color = tint;
            }

            PaintIcon(index, node.Type, tint, width, height);

            PaintCaption(index, node.Type);

            // Only what the party can actually walk to. The map is the authority
            // on adjacency and RunManager refuses anything else anyway, but a
            // button that visibly does nothing is worse than one that is plainly
            // not offered.
            button.interactable = isReachable;

            // The hover/focus rim answers only where a press does something;
            // the detail card still follows every room (ShowPreview).
            var rim = button.GetComponent<HoverBox>();
            if (rim != null) rim.enabled = isReachable;

            nodeMarkers[index].gameObject.SetShown(state == MapTileState.Current);
        }

        // The type-specific foreground: what turns a shared tree into "this one
        // has a fight in it" rather than "this one is a shrine". Types with no
        // painted icon yet -- Entry, Shop, ItemSpawn, Unknown -- show a bare
        // tree, same as the room you came from, rather than a missing-sprite
        // quad.
        private void PaintIcon(int index, RoomType type, Color tint, float width, float height)
        {
            var icon = nodeIcons[index];
            var sprite = IconFor(type);

            if (sprite == null)
            {
                icon.gameObject.SetShown(false);
                return;
            }

            float box = width * MapScreen.IconFractionFor(type);

            icon.gameObject.SetShown(true);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.color = tint;
            icon.rectTransform.sizeDelta = new Vector2(box, box);
            icon.rectTransform.anchoredPosition = new Vector2(0f, -height * 0.08f);
        }

        private Sprite IconFor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Fight: return fightIcon;
                case RoomType.EliteFight: return eliteIcon;
                case RoomType.Boss: return bossIcon;
                case RoomType.Rest: return restIcon;
                case RoomType.Event: return eventIcon;
                case RoomType.Treasure: return treasureIcon;
                default: return null;
            }
        }

        private static Color TintFor(MapTileState state)
        {
            switch (state)
            {
                case MapTileState.Current: return CurrentTint;
                case MapTileState.Walked: return WalkedTint;
                case MapTileState.Open: return OpenTint;
                case MapTileState.Ahead: return AheadTint;
                default: return ClosedTint;
            }
        }

        private void PaintCaption(int index, RoomType type)
        {
            var label = nodeLabels[index];
            label.SetContent(MapNodeCard.Caption(type));
            label.color = CaptionColour;
        }

        private RealmNode NodeAt(int index)
        {
            var map = RunManager.Map;
            if (map == null) return null;

            return Columns(map).At(index);
        }

        // ---- the detail card ------------------------------------------------------

        private readonly TooltipFocusRouter _preview = new TooltipFocusRouter();

        private void ShowPreview(int index, bool shown)
        {
            if (previewPanel == null) return;

            var node = shown ? NodeAt(index) : null;
            if (node == null || RunManager.Run == null)
            {
                previewPanel.SetActive(false);
                return;
            }

            var facts = MapNodePreview.For(RunManager.Run, node);
            previewTitle.SetContent(MapNodeCard.Title(facts));
            previewBody.SetContent(MapNodeCard.Body(facts));
            previewPanel.SetActive(true);
        }

        // ---- refused presses ------------------------------------------------------

        // How long a refusal stays on the caption before the room's name returns.
        internal const float RefusalSeconds = 1.2f;

        private static readonly Color CaptionColour = Hex(MapScreen.CaptionHex);
        private static readonly Color RefusalColour = Hex(MapScreen.RefusalHex);

        private Coroutine _refusal;
        private int _refusedIndex = -1;

        // The caption says why, in the refusal colour, then goes back. No modal:
        // the player's eyes are already on the room they pressed.
        private void Refuse(int index, string reason)
        {
            if (index < 0 || index >= nodeLabels.Length) return;

            if (_refusal != null) StopCoroutine(_refusal);
            RestoreCaption(_refusedIndex);

            _refusedIndex = index;
            nodeLabels[index].SetContent(reason);
            nodeLabels[index].color = RefusalColour;
            _refusal = StartCoroutine(RestoreCaptionAfterRefusal(index));
        }

        private System.Collections.IEnumerator RestoreCaptionAfterRefusal(int index)
        {
            yield return new WaitForSeconds(RefusalSeconds);

            RestoreCaption(index);
            _refusal = null;
            _refusedIndex = -1;
        }

        private void RestoreCaption(int index)
        {
            if (index < 0 || index >= nodeLabels.Length) return;

            var node = NodeAt(index);
            if (node != null) PaintCaption(index, node.Type);
        }

        // The pointer's half: a click on a room that is not offered.
        private void RefuseIfNotOffered(int index)
        {
            if (!nodeButtons[index].IsInteractable()) RefuseIllegal(index);
        }

        private void RefuseIllegal(int index)
        {
            var node = NodeAt(index);
            var current = RunManager.CurrentNode;
            if (node == null || current == null) return;

            var state = StateOf(node, current,
                new HashSet<int>(RunManager.Choices().Select(n => n.Id)),
                RunManager.Map.AllReachableFrom(current.Id),
                new HashSet<int>(RunManager.Run.clearedNodeIds));
            Refuse(index, MapNodeCard.Refusal(state));
        }

        private void OnNodePressed(int index)
        {
            var map = RunManager.Map;
            if (map == null) return;

            // One walk at a time. A second click mid-walk would otherwise start
            // a step from a room the party is halfway out of, and the two
            // coroutines would fight over the same figure. Said on the caption
            // rather than ignored: the press was on a room that IS offered.
            if (IsWalking)
            {
                Refuse(index, MapNodeCard.Refusal(MapTileState.Open));
                return;
            }

            // An empty slot has no visible button to press, so this stays silent.
            var node = NodeAt(index);
            if (node == null) return;

            // Legality is checked BEFORE the walk rather than by MoveTo after
            // it. MoveTo is still the authority and still refuses -- but
            // checking after the walk would cost a walk to a room the party
            // is not allowed to enter, followed by a silent refusal on
            // arrival.
            if (!RunManager.Choices().Any(choice => choice.Id == node.Id))
            {
                RefuseIllegal(index);
                return;
            }

            _walk = StartCoroutine(WalkAndArrive(node));
        }
    }
}
