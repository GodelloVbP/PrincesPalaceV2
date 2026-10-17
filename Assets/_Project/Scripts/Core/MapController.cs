using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.Ambience;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The descent map, painted from the run.
    //
    // The tree built a pool slot for every node a realm COULD have; this
    // switches on the ones the current realm actually has, gives each the art
    // and the grammar its room earns (type icon, strength pips, prize chip),
    // and scrolls the whole wood so the party's own room sits near the top.
    //
    // Nothing here recomputes a coordinate. Every position, every size and the
    // scroll itself come out of MapGraph and MapLayout, which is the same
    // arithmetic the declared tree was built from -- so a room, its trail and
    // the box the layout test holds clear of its neighbours cannot disagree.
    public partial class MapController : MonoBehaviour
    {
        // The scrolling half. `viewport` is the fixed masked window; `content`
        // is the tall rect that slides under it.
        [SerializeField] internal RectTransform viewport;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal Image[] backdrops;
        [SerializeField] internal RectTransform fog;

        // The party's figure. See MapController.Walk.cs for everything it does.
        [SerializeField] internal Image walker;

        [SerializeField] internal Button[] nodeButtons;
        [SerializeField] internal Image[] nodeGlows;
        [SerializeField] internal Image[] nodePips;
        [SerializeField] internal Image[] nodeChips;
        [SerializeField] internal Image[] nodeVeils;
        [SerializeField] internal Image[] trailSegments;
        [SerializeField] internal Image[] trailCores;

        // The roots from each anchor to the heart, and the anchors standing
        // there (MapController.Heart.cs).
        [SerializeField] internal Image[] rootSegments;
        [SerializeField] internal Image[] rootCores;
        [SerializeField] internal Image[] heartPortraits;
        [SerializeField] internal TMP_Text[] heartNames;

        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text overviewLabel;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Image goldIcon;
        [SerializeField] internal TMP_Text bankedLabel;
        [SerializeField] internal Image bankedIcon;

        // What the room the party just walked into did. Empty between rooms,
        // rather than left showing the last one's result under a new heading.
        [SerializeField] internal TMP_Text roomMessageLabel;

        // The character sheet, mounted here too. UNLOCKED: the map between two
        // rooms is exactly where changing gear is supposed to happen, without a
        // walk back to the hub.
        [SerializeField] internal GameObject characterSheetPanel;

        // The in-run shop, mounted here too (F10, docs/PLAN_SHOP.md) --
        // entered from the map, no scene of its own, opened and closed by
        // MapController.Walk.cs's Arrive() rather than by Navigation.Go.
        [SerializeField] internal ShopController shop;

        // An event room's panel, mounted the same way; opened through
        // ShowEventPanel (MapController.Event.cs).
        [SerializeField] internal EventController eventPanel;

        // The honing modal a Rest room opens (OpenRest).
        [SerializeField] internal HoneController honePanel;

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
        [SerializeField] internal Sprite shopIcon;
        [SerializeField] internal Sprite cacheIcon;
        [SerializeField] internal Sprite entryIcon;

        // The prize chip's icons, one per prize kind.
        [SerializeField] internal Sprite chipGoldIcon;
        [SerializeField] internal Sprite chipPotionIcon;
        [SerializeField] internal Sprite chipGearIcon;
        [SerializeField] internal Sprite chipRelicIcon;
        [SerializeField] internal Sprite chipBookIcon;

        // The lair under the heart (MapController.cs LayoutWood).
        [SerializeField] internal Image heartBackdrop;

        // The wood is dimmed so the graph drawn on it reads: the painting's
        // clearings, undimmed, look like places a room should stand.
        private static readonly Color BackdropTint = new Color(0.5f, 0.5f, 0.5f, 1f);

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

        private const string OverviewButton = "Jump";

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

            // A debug grant can move the gold, the pending-book count and the
            // leg itself; the map repaints after each one rather than on the
            // next room.
            var debug = debugMenuPanel != null ? debugMenuPanel.GetComponent<DebugMenuController>() : null;
            if (debug != null) debug.Changed = RefreshAndShowLiveEvent;

            RefreshAndShowLiveEvent();
            BeginOpening();
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
            else if (RunOrchestrator.RestIsOpen) OpenRest();
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

                // The veil follows pointer and pad focus alike, through
                // the router that already decides which of the two owns focus.
                _preview.Register(button, shown => ShowPreview(index, shown));
                var hover = node.GetComponent<HoverIndex>() ?? node.AddComponent<HoverIndex>();
                hover.Changed = (_, entered) => _preview.Pointer(node, entered);

                // A press on a room the party cannot enter never reaches
                // onClick, so it is heard here and answered on the tile (Refuse).
                var click = node.GetComponent<ClickIndex>() ?? node.AddComponent<ClickIndex>();
                click.Clicked = _ => RefuseIfNotOffered(index);
            }

            // A press and drag on the wood (or on a room) pans the map. The
            // EventSystem's own drag threshold decides when a press has become
            // a drag, and a press that never does still clicks its room.
            var drag = viewport.GetComponent<MapDragScroll>() ?? viewport.gameObject.AddComponent<MapDragScroll>();
            drag.Dragged = ScrollBy;

        }

        // ---- the character sheet, between rooms --------------------------------

        // The map's own base context, mirroring HubController's -- pushed
        // once, never popped while this scene is loaded. RefreshNavLinks
        // declares its Selectables (the node buttons). Start and the menu
        // hotkeys reach the system menu through it; its Cancel does nothing,
        // and a modal that pushes its own context over it absorbs all of them.
        private NavContext _navContext;

        // The overarching menu this screen's Cancel opens, assigned by
        // ScreenRegistry.WireMap from the same WireSystemMenu call that
        // builds it -- the hub holds its copy the same way.
        [SerializeField] internal SystemMenuController systemMenu;

        private void Update()
        {
            PulseGlows();
            RefreshOverviewHintIfDeviceChanged();

            if (NavigationInputModule.Contexts != null && !NavigationInputModule.Contexts.IsTop(_navContext))
            {
                return;
            }

            // Y on a pad (the Input Manager's Jump button, which Space shares):
            // the zoomed-out overview. Read here rather than through the nav
            // module because the map is the only screen that answers it;
            // tests drive ToggleOverview directly.
            if (Input.GetButtonDown(OverviewButton)) ToggleOverview();

            ReadScrollInput();

            // Gated on the KEY, not on SetDebugMenu, so PlayMode can still
            // drive the methods -- see HubController's F1 for the same split.
            if (Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F1))
            {
                ToggleDebugMenu();
                return;
            }

            // Cancel/Escape is not read here -- see the context comment above.
        }

        private void RegisterNavContext()
        {
            if (_navContext != null) return;

            // systemMenu, NOT cancel -- see HandleSystemMenu below.
            _navContext = new NavContext(entry: null, selectables: null,
                cancel: null, systemMenu: HandleSystemMenu,
                menuHotkey: hotkey => MenuHotkeys.Press(systemMenu, hotkey));

            // No room is chosen for the player: nothing is focused on open or
            // after a move, and the first navigation press focuses the room
            // nearest the walker (RefreshNavLinks picks it as the entry).
            _navContext.LazyEntry = true;
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

        private void OnDestroy()
        {
            _preview.Detach();
            ReleaseEasingMaterial();

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

        // Called from MapController.Walk.cs's Arrive(): a nested panel, so no
        // Navigation.Go, and closing it is the shop's own LEAVE button rather
        // than anything this method does.
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

        // A Rest room's page: Heal or one free hone. Reopened on a Map start
        // that finds the room still unspent.
        public void OpenRest()
        {
            if (honePanel == null || !RunOrchestrator.RestIsOpen) return;

            honePanel.Finished = Refresh;
            honePanel.Open(HoneMode.Rest);
        }

        public void Refresh()
        {
            var map = RunManager.Map;
            var current = RunManager.CurrentNode;

            // A repaint ends the opening shot: the camera is about to be put
            // where the party stands.
            SkipOpening();
            EndRefusal();

            // No run: every node off, and the labels say nothing rather than
            // lying about a descent that is not happening.
            if (map == null || current == null)
            {
                foreach (var button in nodeButtons) button.gameObject.SetShown(false);
                HideTrails(0);
                HideRoots(0);
                HideHeart();
                HideBackdrops(0);
                if (easedWood != null) easedWood.gameObject.SetShown(false);
                if (heartBackdrop != null) heartBackdrop.gameObject.SetShown(false);
                fog.gameObject.SetShown(false);
                walker.gameObject.SetShown(false);
                RefreshNavLinks(null, null);
                return;
            }

            // A repaint can change what the card describes (a room cleared, a
            // grant from the debug menu), so it closes rather than going stale.
            _preview.ForceClose();

            var run = RunManager.Run;
            var graph = Graph(map);
            var choices = RunManager.Choices();
            var reachable = new HashSet<int>(choices.Select(n => n.Id));
            var ahead = map.AllReachableFrom(current.Id);
            var cleared = new HashSet<int>(run.clearedNodeIds);

            titleLabel.Set(UiStrings.MapTitle, run.floor, run.roomsWalked);
            goldLabel.Set(UiStrings.MapGold, run.gold);
            if (bankedLabel != null) bankedLabel.Set(UiStrings.MapGold, run.bankedGold);
            PlaceGoldIcon();
            RefreshOverviewLabel();

            // Painted from the LAST resolved room, which Arrive has already
            // written by the time this runs. A fight clears it on the way out,
            // so a stash line cannot survive under a room that did not pay it.
            //
            // Hidden rather than blanked when there is nothing to say: an empty
            // label is still a box the text-fit audit has to reason about, and
            // a stale-looking gap under the top-right stack reads as a missing
            // string rather than as a room that had nothing to report.
            if (roomMessageLabel != null)
            {
                bool hasMessage = RoomResolver.TryMessage(RoomResolver.Last, out var message, out var args);
                roomMessageLabel.gameObject.SetShown(hasMessage);
                if (hasMessage) roomMessageLabel.Set(message, args);
            }

            LayoutWood(graph);

            // Graceful rather than silent: a designed profile whose realm
            // outgrows the node pool draws what fits and says so.
            if (graph.Overflow.Count > 0)
            {
                Debug.LogWarning($"MapController: {graph.Overflow.Count} room(s) do not fit the node pool " +
                                 $"({MapLayout.MaxNodes}); MapLayout.MaxNodes needs raising");
            }

            _current = current;
            _cleared = cleared;
            _reachable = reachable;
            _veiled = new HashSet<int>();

            var occupied = new HashSet<int>();
            foreach (var node in graph.Nodes)
            {
                int index = graph.IndexOf(node.Id);
                if (index < 0 || index >= nodeButtons.Length) continue;

                occupied.Add(index);
                PaintNode(index, node, graph, run,
                    StateOf(node, current, reachable, ahead, cleared),
                    isReachable: reachable.Contains(node.Id));
            }

            // Everything the realm did not use. Hidden rather than dimmed: an
            // empty slot is not a room the player failed to reach, it is a room
            // that does not exist.
            for (int i = 0; i < nodeButtons.Length; i++)
            {
                if (!occupied.Contains(i)) nodeButtons[i].gameObject.SetShown(false);
            }

            PaintTrails(map, graph);
            PaintHeart(map, graph, run);
            PaintEasing(graph, run, cleared, current);

            // Not while a walk is in flight: the walk owns both the figure and
            // the camera until it arrives, and a refresh from anything else
            // would snap them to a room the party has not reached yet.
            if (!IsWalking)
            {
                PlaceWalker(current);
                ApplyCamera(graph, graph.PositionOf(current.Id).Y);
            }

            ClearRoomFocus();
            RefreshNavLinks(current, choices);
        }

        // Nothing stays chosen across a repaint (a move, the overview, a
        // grant): the player's next navigation press focuses a room afresh.
        // Only a room's button is cleared; a modal's own selection stays.
        private void ClearRoomFocus()
        {
            var system = EventSystem.current;
            var selected = system != null ? system.currentSelectedGameObject : null;
            if (selected == null) return;

            foreach (var button in nodeButtons)
            {
                if (button != null && button.gameObject == selected)
                {
                    system.SetSelectedGameObject(null);
                    return;
                }
            }
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
        // PLAY: the edges ARE RunManager.Choices(). Left and Right step through
        // the choices in screen order (left to right), Down goes from the
        // party's own room to the first of them and Up comes back. No wrap:
        // "Map follows reachability" -- there is nothing to wrap TO.
        //
        // OVERVIEW: every room is selectable and the d-pad moves to the nearest
        // room in the direction pressed (OverviewLinks), so a room that is not
        // a choice can still be inspected.
        //
        // Entry is the choice nearest the walker, not current itself: current's own
        // button is never interactable (isReachable is false for it,
        // PaintNode's own `button.interactable = isReachable`), so selecting
        // it would land on a Submit that does nothing.
        private void RefreshNavLinks(RealmNode current, IReadOnlyList<RealmNode> choices)
        {
            if (_navContext == null) return;

            if (current == null || choices == null || choices.Count == 0)
            {
                // Screen-level fallback (plan section 6): nothing to select;
                // Start still opens the system menu.
                _navContext.Reconfigure(entry: null, selectables: null);
                return;
            }

            // Wiped first: Apply only rewrites the buttons a declaration names,
            // so a node that is not a choice any more would otherwise keep
            // leading wherever it led before the repaint.
            foreach (var button in nodeButtons)
            {
                button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            }

            var graph = Graph(RunManager.Map);
            var currentButton = ButtonFor(current);

            var links = new List<UiNavLink<Selectable>?>();
            var selectables = new Dictionary<string, object>();
            GameObject entry;

            if (_overview)
            {
                links.AddRange(OverviewLinks(graph));
                foreach (var node in graph.Nodes)
                {
                    var button = ButtonFor(node);
                    if (button != null && button.gameObject.activeSelf) selectables[$"node{node.Id}"] = button.gameObject;
                }

                entry = (currentButton != null ? currentButton : ButtonFor(choices[0]))?.gameObject;
            }
            else
            {
                // Screen order: left to right, ties by id.
                var ordered = choices.OrderBy(n => graph.PositionOf(n.Id).X).ThenBy(n => n.Id).ToList();
                var choiceButtons = ordered.Select(ButtonFor).Where(b => b != null).ToList();

                if (choiceButtons.Count > 0)
                {
                    links.AddRange(RuntimeNavWiring.LinkBoth(currentButton, UiNavDirection.Down, choiceButtons[0]));

                    for (int i = 1; i < choiceButtons.Count; i++)
                    {
                        // Right steps to the next choice; Left steps back to
                        // the one before it. LinkBoth makes writing only one
                        // direction of this pair impossible.
                        links.AddRange(RuntimeNavWiring.LinkBoth(choiceButtons[i - 1], UiNavDirection.Right, choiceButtons[i]));
                        links.Add(RuntimeNavWiring.Link(choiceButtons[i], UiNavDirection.Up, currentButton));
                    }
                }

                if (currentButton != null) selectables["current"] = currentButton.gameObject;
                for (int i = 0; i < choiceButtons.Count; i++) selectables[$"choice{i}"] = choiceButtons[i].gameObject;
                var nearest = NearestChoice(ordered, graph, current);
                entry = (nearest != null ? ButtonFor(nearest) : null)?.gameObject;
            }

            RuntimeNavWiring.Apply(System.Array.Empty<UiNavGroup<Selectable>>(), links);
            _navContext.Reconfigure(entry: entry, selectables);
        }

        // The room the first navigation press focuses: the choice closest to
        // the walker, the leftmost on a tie.
        private static RealmNode NearestChoice(List<RealmNode> ordered, MapGraph graph, RealmNode current)
        {
            var at = graph.PositionOf(current.Id);
            RealmNode best = null;
            float bestDistance = float.MaxValue;
            foreach (var node in ordered)
            {
                var other = graph.PositionOf(node.Id);
                float dx = other.X - at.X;
                float dy = other.Y - at.Y;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }

            return best;
        }

        // For each shown room and each direction, the nearest shown room that
        // way: distance along the direction plus a penalty for drifting off its
        // axis, so Down from a spine room reaches the next spine room rather
        // than a pocket room a row lower and far to the side.
        private IEnumerable<UiNavLink<Selectable>?> OverviewLinks(MapGraph graph)
        {
            var nodes = graph.Nodes.Where(n => ButtonFor(n) != null && ButtonFor(n).gameObject.activeSelf).ToList();
            var directions = new[] { UiNavDirection.Up, UiNavDirection.Down, UiNavDirection.Left, UiNavDirection.Right };

            foreach (var from in nodes)
            {
                var at = graph.PositionOf(from.Id);
                foreach (var direction in directions)
                {
                    RealmNode best = null;
                    float bestScore = float.MaxValue;
                    foreach (var to in nodes)
                    {
                        if (to.Id == from.Id) continue;

                        var other = graph.PositionOf(to.Id);
                        float dx = other.X - at.X;
                        float dy = other.Y - at.Y;
                        float along;
                        float across;
                        switch (direction)
                        {
                            case UiNavDirection.Up: along = dy; across = dx; break;
                            case UiNavDirection.Down: along = -dy; across = dx; break;
                            case UiNavDirection.Left: along = -dx; across = dy; break;
                            default: along = dx; across = dy; break;
                        }

                        if (along < 20f) continue;

                        float score = along + 2f * Mathf.Abs(across);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = to;
                        }
                    }

                    if (best != null) yield return RuntimeNavWiring.Link(ButtonFor(from), direction, ButtonFor(best));
                }
            }
        }

        private Button ButtonFor(RealmNode node)
        {
            if (node == null) return null;
            int index = Graph(RunManager.Map).IndexOf(node.Id);
            return index >= 0 && index < nodeButtons.Length ? nodeButtons[index] : null;
        }

        // The placement for this realm, built once per map rather than per
        // query: Refresh, the walk and every hover ask it.
        private MapGraph _graph;
        private RealmMap _graphMap;

        private MapGraph Graph(RealmMap map)
        {
            if (_graph == null || !ReferenceEquals(_graphMap, map))
            {
                _graph = MapGraph.Of(map);
                _graphMap = map;
            }

            return _graph;
        }

        // What the last Refresh painted from, kept so a focus change can
        // repaint the trails under a veil without recomputing the run.
        private RealmNode _current;
        private HashSet<int> _cleared = new HashSet<int>();
        private HashSet<int> _reachable = new HashSet<int>();
        private HashSet<int> _veiled = new HashSet<int>();

        // Which of the five a room is in. Order matters: a cleared room the
        // party is standing in is CURRENT, and a room the party can still
        // reach by any route is AHEAD whether or not a trail to it is open
        // yet, while one no route reaches any more is a road not taken.
        private static MapTileState StateOf(RealmNode node, RealmNode current, HashSet<int> reachable,
                                            HashSet<int> ahead, HashSet<int> cleared)
        {
            if (node.Id == current.Id) return MapTileState.Current;
            // The entry is never "cleared" (there is nothing in it to resolve;
            // the opening shot reads that to know the realm is fresh), but a
            // party that has left it has walked it. Left Closed it dims to
            // nothing against the wood.
            if (cleared.Contains(node.Id) || node.Role == RealmNodeRole.Entry) return MapTileState.Walked;
            if (reachable.Contains(node.Id)) return MapTileState.Open;
            return ahead.Contains(node.Id) ? MapTileState.Ahead : MapTileState.Closed;
        }

        // The wood itself: how much of it exists, how tall the leg is, and
        // where it stops. All per-realm facts, so they are re-stated on every
        // refresh rather than baked into the tree.
        private void LayoutWood(MapGraph graph)
        {
            content.sizeDelta = new Vector2(viewport.rect.width, graph.ContentHeight);

            int needed = MapLayout.BackgroundTilesFor(graph.ContentHeight);
            if (needed > backdrops.Length)
            {
                // Graceful rather than silent: the wood runs out at the bottom
                // instead of the realm failing to draw, and the log says which
                // number to raise.
                Debug.LogWarning(
                    $"MapController: a {graph.ContentHeight:F0}-high realm needs {needed} forest tiles, the pool holds {backdrops.Length}");
                needed = backdrops.Length;
            }

            for (int i = 0; i < backdrops.Length; i++)
            {
                var tile = backdrops[i];
                tile.gameObject.SetShown(i < needed);
                tile.color = BackdropTint;
                tile.rectTransform.anchoredPosition = new Vector2(0f, MapLayout.BackgroundTileY(i));
            }

            // Hung from the content's bottom edge, so the heart row lands in
            // the lair's middle. Its top edge is baked to fade into the strip.
            heartBackdrop.gameObject.SetShown(true);
            heartBackdrop.color = BackdropTint;
            heartBackdrop.rectTransform.anchoredPosition = new Vector2(
                0f, -(graph.ContentHeight - MapLayout.HeartBackdropHeight));

            fog.gameObject.SetShown(true);
            fog.sizeDelta = new Vector2(viewport.rect.width, fog.sizeDelta.y);
            fog.anchoredPosition = new Vector2(0f, -(graph.ContentHeight - fog.sizeDelta.y * 0.5f));
        }

        // ---- the camera ---------------------------------------------------------

        // Where the camera rests: the party's room near the top of the window,
        // or in the overview the whole realm shrunk to fit (and, past the
        // smallest legible size, following the focused room).
        private void ApplyCamera(MapGraph graph, float focusY)
        {
            if (_overview)
            {
                float scale = OverviewScale(graph);
                content.localScale = new Vector3(scale, scale, 1f);
                content.anchoredPosition = new Vector2(0f, OverviewScroll(graph, scale, focusY));
                return;
            }

            content.localScale = Vector3.one;
            content.anchoredPosition = new Vector2(0f,
                MapLayout.Scroll(focusY, content.sizeDelta.y, viewport.rect.height));
        }

        // Past this the tiles stop being legible; a taller realm scrolls with
        // the focus instead of shrinking further.
        private const float OverviewMinScale = 0.4f;

        private float OverviewScale(MapGraph graph) =>
            Mathf.Max(OverviewMinScale, MapLayout.OverviewScale(graph.ContentHeight, viewport.rect.height));

        // Keeps `focusY` (content-local, negative) at the window's middle,
        // clamped to the content.
        private float OverviewScroll(MapGraph graph, float scale, float focusY)
        {
            // The realm sits between the top-right stack's band and the bottom inset: a realm
            // that fits is pushed DOWN by the top inset (a negative scroll).
            float height = viewport.rect.height;
            float min = -MapLayout.OverviewTopInset;
            float max = Mathf.Max(min, graph.ContentHeight * scale - (height - MapLayout.OverviewBottomInset));
            float target = -height * 0.5f - focusY * scale;
            return Mathf.Clamp(target, min, max);
        }

        // Y: the whole realm at a glance. Read-only -- the d-pad inspects every
        // room, and a press walks nowhere.
        public bool InOverview => _overview;

        private bool _overview;

        public void ToggleOverview() => SetOverview(!_overview);

        public void SetOverview(bool on)
        {
            if (IsWalking || _overview == on || RunManager.Map == null) return;

            _overview = on;

            // Refresh also clears the room focus: the selection may sit on a
            // room that is not a choice any more.
            Refresh();
        }

        // ---- the opening shot ---------------------------------------------------

        // A fresh realm opens on the heart and travels up to the party, so the
        // first thing the player sees is what the realm is about. 2.2 s, and any
        // press or click ends it.
        private const float OpeningSeconds = 2.2f;

        // THE TEST SEAM, same shape as HubController.MotionSpeedMultiplier: on
        // outside a test, off before every test (TestGlobals.ResetMapOpening),
        // because a camera sweeping up from the heart while a mouse test aims
        // at the entry moves the entry out from under the click.
        public static bool OpeningEnabled = true;

        private Coroutine _opening;

        public bool IsOpening => _opening != null;

        // The shot's span, false when there is nothing to sweep (a realm no
        // taller than the window). Decided before the coroutine starts: a
        // coroutine that ends on its first step finishes inside StartCoroutine,
        // before its handle is stored, so it cannot clear IsOpening itself.
        private bool TryOpeningSpan(out float from, out float to)
        {
            from = to = 0f;
            var map = RunManager.Map;
            var current = RunManager.CurrentNode;
            if (map == null || current == null) return false;

            var graph = Graph(map);
            float height = viewport.rect.height;
            from = MapLayout.ScrollToBottom(content.sizeDelta.y, height);
            to = MapLayout.Scroll(graph.PositionOf(current.Id).Y, content.sizeDelta.y, height);
            return !Mathf.Approximately(from, to);
        }

        private IEnumerator Opening(float from, float to)
        {
            for (float elapsed = 0f; elapsed < OpeningSeconds; elapsed += Time.deltaTime)
            {
                if (Input.anyKeyDown) break;

                float t = elapsed / OpeningSeconds;
                content.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
                yield return null;
            }

            content.anchoredPosition = new Vector2(0f, to);
            _opening = null;
        }

        // Starts the shot, only on the first room of a realm.
        private void BeginOpening()
        {
            var run = RunManager.Run;
            var current = RunManager.CurrentNode;
            if (!OpeningEnabled || run == null || current == null) return;
            if (current.Role != RealmNodeRole.Entry || IsWalking || _overview) return;
            if (run.clearedNodeIds.Contains(current.Id)) return;

            if (TryOpeningSpan(out float from, out float to)) _opening = StartCoroutine(Opening(from, to));
        }

        // Ends the shot where it stands; the caller puts the camera where it
        // belongs.
        public void SkipOpening()
        {
            if (_opening == null) return;

            StopCoroutine(_opening);
            _opening = null;
            if (RunManager.Map != null && RunManager.CurrentNode != null && !IsWalking)
            {
                var graph = Graph(RunManager.Map);
                ApplyCamera(graph, graph.PositionOf(RunManager.CurrentNode.Id).Y);
            }
        }

        // ---- trails and roots ---------------------------------------------------

        // The paths between rooms, which is what makes this a map rather than a
        // grid of buttons. Without them nothing on screen says which room leads
        // to which, and a fork is indistinguishable from two unrelated rooms
        // that happen to sit in the same row.
        private void PaintTrails(RealmMap map, MapGraph graph)
        {
            if (trailSegments == null || trailSegments.Length == 0) return;

            var seeds = LinkSeeds(map);
            int segment = 0;

            foreach (var node in map.Nodes)
            {
                if (!graph.Contains(node.Id)) continue;
                var from = graph.PositionOf(node.Id);

                foreach (int nextId in node.Next)
                {
                    if (!graph.Contains(nextId)) continue;
                    var to = graph.PositionOf(nextId);

                    // LOOKED UP, not counted: a shared seed dictionary means
                    // the walk and the trail-build agree on a link's jitter
                    // by construction rather than by two loops incrementing
                    // a counter identically.
                    int seed = seeds.TryGetValue(LinkKey(node.Id, nextId), out var s) ? s : 1;

                    // The pool is sized to the worst case a realm can generate,
                    // so running out means the generator changed shape rather
                    // than that this needs a bigger number. Stop rather than
                    // draw half a trail.
                    if (segment + MapLayout.SegmentsPerLink > trailSegments.Length) break;

                    // WALKED means both ends are behind you. Colouring a trail
                    // by its destination alone would light up every path INTO a
                    // cleared room, including ones never taken.
                    bool taken = _cleared.Contains(node.Id) && _cleared.Contains(nextId);
                    bool fromHere = node.Id == _current.Id;
                    bool veiled = _veiled.Contains(node.Id) || _veiled.Contains(nextId);

                    Color colour;
                    Color core;
                    float width;
                    if (veiled)
                    { colour = TrailClosed; core = CoreClosed; width = MapScreen.TrailWidthClosed; }
                    else if (taken)
                    { colour = TrailTaken; core = CoreTaken; width = MapScreen.TrailWidthTaken; }
                    else if (fromHere)
                    { colour = TrailOpen; core = CoreOpen; width = MapScreen.TrailWidthOpen; }
                    else if (_reachable.Contains(node.Id) || _cleared.Contains(node.Id))
                    { colour = TrailAhead; core = CoreAhead; width = MapScreen.TrailWidthAhead; }
                    else
                    { colour = TrailClosed; core = CoreClosed; width = MapScreen.TrailWidthClosed; }

                    for (int i = 0; i < MapLayout.SegmentsPerLink; i++)
                    {
                        var piece = MapLayout.SegmentAt(from, to, seed, i);
                        PlaceSegment(trailSegments[segment], trailCores, segment, piece, colour, core, width);
                        segment++;
                    }
                }
            }

            HideTrails(segment);
        }

        private static void PlaceSegment(Image image, Image[] cores, int index, MapLayout.PathSegment piece,
                                         Color colour, Color core, float width)
        {
            image.gameObject.SetShown(true);
            image.color = colour;

            // Indexed off the SAME counter as its verge, so the two halves of
            // one road can never describe two different states.
            if (cores != null && index < cores.Length) cores[index].color = core;

            var rect = image.rectTransform;
            rect.sizeDelta = new Vector2(piece.Length, width);
            rect.anchoredPosition = new Vector2(piece.Centre.X, piece.Centre.Y);
            rect.localRotation = Quaternion.Euler(0f, 0f, piece.AngleDegrees);
        }

        private void HideTrails(int from)
        {
            if (trailSegments == null) return;
            for (int i = from; i < trailSegments.Length; i++)
            {
                if (trailSegments[i] != null) trailSegments[i].gameObject.SetShown(false);
            }
        }

        private void HideRoots(int from)
        {
            if (rootSegments == null) return;
            for (int i = from; i < rootSegments.Length; i++)
            {
                if (rootSegments[i] != null) rootSegments[i].gameObject.SetShown(false);
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

        // ---- a node -------------------------------------------------------------

        private void PaintNode(int index, RealmNode node, MapGraph graph, RunSnapshot run, MapTileState state,
                               bool isReachable)
        {
            var button = nodeButtons[index];
            button.gameObject.SetShown(true);

            // The tile is a BOUNDING BOX, not a stretch target: the tree keeps
            // its own proportions inside an elite's or a boss's bigger box
            // rather than distorting to fill it.
            float width = MapLayout.TileWidthFor(node.Type);
            float height = MapLayout.TileHeightFor(node.Type);

            var tint = TintFor(state);

            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, height);
            var at = graph.PositionOf(node.Id);
            rect.anchoredPosition = new Vector2(at.X, at.Y);

            // The medallion is the plate: its sprite follows the room type.
            var medallion = IconFor(node.Type) ?? fightIcon;
            var plate = button.targetGraphic as Image;
            if (plate != null)
            {
                plate.sprite = medallion;
                plate.preserveAspect = true;
                plate.color = tint;
            }

            PaintGrammar(index, node, run, width, height, tint);

            var veil = nodeVeils[index];
            veil.sprite = medallion;
            veil.preserveAspect = true;
            veil.color = VeilColour;
            veil.gameObject.SetShown(false);

            // Only what the party can actually walk to. The map is the authority
            // on adjacency and RunManager refuses anything else anyway, but a
            // button that visibly does nothing is worse than one that is plainly
            // not offered. The overview is the exception: there every room must
            // be selectable to be inspected, and a press is ignored.
            button.interactable = isReachable || _overview;

            // The hover/focus rim answers only where a press does something;
            // the focus veil still follows every room (ShowPreview).
            var rim = button.GetComponent<HoverBox>();
            if (rim != null) rim.enabled = isReachable && !_overview;

            // The soft ring on a room that can be entered (PulseGlows animates
            // it); none on a room that cannot.
            nodeGlows[index].gameObject.SetShown(isReachable && !_overview);
        }

        // The fixed node grammar (plan section 7): the medallion is the type
        // icon, the pips are the strength, and one chip names the prize, its
        // size following the strength. Only a fight and an anchor have either.
        private void PaintGrammar(int index, RealmNode node, RunSnapshot run, float width, float height, Color tint)
        {
            bool fights = node.Type == RoomType.Fight || node.Type == RoomType.EliteFight;
            int strength = fights ? RealmOpening.StrengthOf(run, node.Id) : 0;
            var chip = fights ? RealmPrizes.ChipOf(run, node) : default;

            for (int p = 0; p < NodeStrength.Max; p++)
            {
                var pip = nodePips[index * NodeStrength.Max + p];
                pip.gameObject.SetShown(p < strength);
                pip.color = PipColour * tint;
                pip.rectTransform.anchoredPosition = new Vector2(
                    (p - (NodeStrength.Max - 1) * 0.5f) * MapLayout.PipPitch, height * 0.5f - 18f);
            }

            bool hasChip = fights && chip.Kind != PrizeKind.None;
            var chipPlate = nodeChips[index];
            chipPlate.gameObject.SetShown(hasChip);
            if (!hasChip) return;

            float chipSize = MapLayout.ChipHeightFor(strength);
            chipPlate.rectTransform.sizeDelta = new Vector2(chipSize, chipSize);
            chipPlate.rectTransform.anchoredPosition = new Vector2(width * 0.5f + MapLayout.ChipGap + chipSize * 0.5f, -20f);
            chipPlate.sprite = ChipIconFor(chip.Kind);
            chipPlate.preserveAspect = true;
        }

        private Sprite ChipIconFor(PrizeKind kind)
        {
            switch (kind)
            {
                case PrizeKind.Gold: return chipGoldIcon;
                case PrizeKind.Consumable: return chipPotionIcon;
                case PrizeKind.Item: return chipGearIcon;
                case PrizeKind.Relic: return chipRelicIcon;
                case PrizeKind.Book: return chipBookIcon;
                default: return null;
            }
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
                case RoomType.Shop: return shopIcon;
                case RoomType.Cache: return cacheIcon;
                case RoomType.Entry: return entryIcon;
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

        private RealmNode NodeAt(int index)
        {
            var map = RunManager.Map;
            if (map == null) return null;

            return Graph(map).At(index);
        }

        // ---- the focused room -------------------------------------------------------

        private readonly TooltipFocusRouter _preview = new TooltipFocusRouter();

        // The focused room veils the rooms its choice would close off, under
        // fog, and in the overview scrolls to it. Nothing is written out: the
        // room's own icon, chip and veil carry what it holds.
        private void ShowPreview(int index, bool shown)
        {
            var node = shown ? NodeAt(index) : null;
            if (node == null || RunManager.Run == null)
            {
                ApplyVeil(null);
                return;
            }

            var map = RunManager.Map;
            var current = RunManager.CurrentNode;
            var choices = RunManager.Choices();
            bool isChoice = !_overview && current != null && choices.Any(c => c.Id == node.Id);

            var closed = isChoice ? map.ClosedBy(current.Id, node.Id) : new HashSet<int>();
            ApplyVeil(closed);

            if (_overview) FocusOverviewOn(node);
        }

        // Fog over the rooms the focused choice would close off, trails
        // included, and only when something does close.
        private void ApplyVeil(HashSet<int> closed)
        {
            var map = RunManager.Map;
            if (map == null || _current == null) return;

            _veiled = closed ?? new HashSet<int>();
            var graph = Graph(map);
            for (int i = 0; i < nodeVeils.Length; i++) nodeVeils[i].gameObject.SetShown(false);
            foreach (int id in _veiled)
            {
                int index = graph.IndexOf(id);
                if (index >= 0 && index < nodeVeils.Length) nodeVeils[index].gameObject.SetShown(true);
            }

            PaintTrails(map, graph);
        }

        // In the overview the camera follows the focus once the realm is taller
        // than the window at its smallest legible size.
        private void FocusOverviewOn(RealmNode node)
        {
            var graph = Graph(RunManager.Map);
            ApplyCamera(graph, graph.PositionOf(node.Id).Y);
        }

        // ---- free scrolling -------------------------------------------------------

        // The normal map view only. Drag, wheel, right stick and PageUp/PageDown
        // slide the content within its own extent; the next move pans the camera
        // back to its pin on the party's room (MapController.Walk.cs PanTo).
        // Pure camera: nothing here reads or writes Time.timeScale.
        private const string ScrollAxis = "MapScroll";

        private bool CanScroll => !_overview && !IsWalking && !IsOpening && RunManager.Map != null;

        public float ScrollY => content.anchoredPosition.y;

        // The deepest the content can slide: the heart at the window's bottom.
        public float MaxScroll => Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);

        // `units` is how far the content moves UP (positive) on screen; the
        // result stays between the leg's top and the heart at its bottom.
        public void ScrollBy(float units)
        {
            if (!CanScroll || units == 0f) return;

            content.anchoredPosition = new Vector2(content.anchoredPosition.x,
                Mathf.Clamp(content.anchoredPosition.y + units, 0f, MaxScroll));
        }

        private void ReadScrollInput()
        {
            if (!CanScroll) return;

            float keys = (Input.GetKey(KeyCode.PageDown) ? 1f : 0f) - (Input.GetKey(KeyCode.PageUp) ? 1f : 0f);
            ScrollBy(MapLayout.ScrollUnits(Input.mouseScrollDelta.y, Input.GetAxisRaw(ScrollAxis), keys,
                Time.unscaledDeltaTime));
        }

        // ---- the ring on a room that can be entered --------------------------------

        private void PulseGlows()
        {
            if (nodeGlows == null || nodeGlows.Length == 0) return;

            var colour = GlowColour;
            colour.a = MapLayout.GlowAlpha(
                AmbienceCurves.BeaconProgressAt(Time.time, MapLayout.GlowPeriodSeconds, 0f));
            foreach (var glow in nodeGlows)
            {
                if (glow != null && glow.gameObject.activeSelf) glow.color = colour;
            }
        }

        // ---- the top-right stack ---------------------------------------------------

        // The coin sits just left of the right-aligned amount; the banked pair
        // (cache icon, amount) sits left of that, and is hidden at zero.
        private void PlaceGoldIcon()
        {
            if (goldIcon == null || goldLabel == null) return;

            float left = PlaceIconBeforeText(goldLabel, goldIcon);

            bool banked = bankedLabel != null && bankedIcon != null && RunManager.Run != null
                && RunManager.Run.bankedGold > 0;
            if (bankedLabel != null) bankedLabel.gameObject.SetShown(banked);
            if (bankedIcon != null) bankedIcon.gameObject.SetShown(banked);
            if (!banked) return;

            var box = bankedLabel.rectTransform;
            box.anchoredPosition = new Vector2(left - BankedGap - box.rect.width * 0.5f, box.anchoredPosition.y);
            PlaceIconBeforeText(bankedLabel, bankedIcon);
        }

        private const float BankedGap = 24f;

        // Returns the icon's left edge.
        private static float PlaceIconBeforeText(TMP_Text label, Image icon)
        {
            label.ForceMeshUpdate();
            var box = label.rectTransform;
            float textWidth = label.GetPreferredValues(label.text).x;
            float right = box.anchoredPosition.x + box.rect.width * 0.5f;
            float size = icon.rectTransform.rect.width;
            float x = right - textWidth - size * 0.5f - 6f;
            icon.rectTransform.anchoredPosition = new Vector2(x, box.anchoredPosition.y);
            return x - size * 0.5f;
        }

        // The overview hint names the key of the device in the player's hands.
        // A pad counts only while one is plugged in: the last-input flag starts
        // true, which would show a pad button to a keyboard player.
        private PromptDevice? _hintDevice;

        private PromptDevice CurrentDevice() =>
            PromptGlyphs.DeviceFor(NavigationInputModule.LastInputWasPad && PadConnected());

        // GetJoystickNames allocates an array per call and Update asks every
        // frame while the last input was a pad, so the answer is held for a
        // second.
        private const float PadPollSeconds = 1f;
        private float _padPolledAt = float.NegativeInfinity;
        private bool _padConnected;

        private bool PadConnected()
        {
            if (Time.unscaledTime - _padPolledAt < PadPollSeconds) return _padConnected;

            _padPolledAt = Time.unscaledTime;
            _padConnected = false;
            foreach (var name in Input.GetJoystickNames())
            {
                if (!string.IsNullOrEmpty(name)) { _padConnected = true; break; }
            }

            return _padConnected;
        }

        private void RefreshOverviewLabel()
        {
            if (overviewLabel == null) return;

            _hintDevice = CurrentDevice();
            string text = MapNodeCard.OverviewText(_overview, _hintDevice.Value);
            overviewLabel.gameObject.SetShown(text.Length > 0);
            if (text.Length > 0) overviewLabel.SetContent(text);
        }

        private void RefreshOverviewHintIfDeviceChanged()
        {
            if (_hintDevice.HasValue && _hintDevice.Value != CurrentDevice() && RunManager.Run != null)
            {
                RefreshOverviewLabel();
            }
        }

        // ---- refused presses ------------------------------------------------------

        // A press that goes nowhere answers on the tile itself, in no words: a
        // flash toward red that fades out while the tile shakes side to side.
        internal const float RefusalSeconds = 0.35f;
        private const float RefusalShakePixels = 7f;
        private const float RefusalShakeRate = 70f;

        private static readonly Color CaptionColour = Hex(MapScreen.CaptionHex);
        private static readonly Color RefusalColour = Hex(MapScreen.RefusalHex);
        private static readonly Color PipColour = Hex(MapScreen.PipHex);
        private static readonly Color GlowColour = Hex(MapScreen.GlowHex);
        private static readonly Color VeilColour = Hex(MapScreen.VeilHex);

        private Coroutine _refusal;
        private int _refusedIndex = -1;
        private Color _refusedColour;
        private Vector2 _refusedAt;

        private void Refuse(int index)
        {
            if (index < 0 || index >= nodeButtons.Length) return;

            var plate = nodeButtons[index].targetGraphic as Image;
            if (plate == null) return;

            EndRefusal();
            _refusedIndex = index;
            _refusedColour = plate.color;
            _refusedAt = ((RectTransform)nodeButtons[index].transform).anchoredPosition;
            _refusal = StartCoroutine(RefusalCue(index, plate));
        }

        private IEnumerator RefusalCue(int index, Image plate)
        {
            var rect = (RectTransform)nodeButtons[index].transform;
            for (float elapsed = 0f; elapsed < RefusalSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float fade = 1f - elapsed / RefusalSeconds;
                plate.color = Color.Lerp(_refusedColour, RefusalColour, fade);
                rect.anchoredPosition = _refusedAt +
                    new Vector2(Mathf.Sin(elapsed * RefusalShakeRate) * RefusalShakePixels * fade, 0f);
                yield return null;
            }

            EndRefusal();
        }

        // Puts the tile back exactly as it was before the cue touched it.
        private void EndRefusal()
        {
            if (_refusal != null) StopCoroutine(_refusal);
            _refusal = null;

            if (_refusedIndex >= 0 && _refusedIndex < nodeButtons.Length)
            {
                var plate = nodeButtons[_refusedIndex].targetGraphic as Image;
                if (plate != null) plate.color = _refusedColour;
                ((RectTransform)nodeButtons[_refusedIndex].transform).anchoredPosition = _refusedAt;
            }

            _refusedIndex = -1;
        }

        // The pointer's half: a click on a room that is not offered.
        private void RefuseIfNotOffered(int index)
        {
            if (!_overview && !nodeButtons[index].IsInteractable()) RefuseIllegal(index);
        }

        private void RefuseIllegal(int index)
        {
            if (NodeAt(index) == null || RunManager.CurrentNode == null) return;
            Refuse(index);
        }

        private void OnNodePressed(int index)
        {
            var map = RunManager.Map;
            if (map == null) return;

            // Read-only: the overview inspects, it does not walk.
            if (_overview) return;

            // One walk at a time. A second click mid-walk would otherwise start
            // a step from a room the party is halfway out of, and the two
            // coroutines would fight over the same figure. Flashed on the tile
            // rather than ignored: the press was on a room that IS offered.
            if (IsWalking)
            {
                Refuse(index);
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

            SkipOpening();
            ApplyVeil(null);
            _walk = StartCoroutine(WalkAndArrive(node));
        }
    }
}
