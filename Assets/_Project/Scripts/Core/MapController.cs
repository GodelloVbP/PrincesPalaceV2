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

        // The painted room icons. Bound by the wiring step rather than loaded
        // here: an "Assets/..." path is an editor-only address, and Core cannot
        // see the editor assembly.
        [SerializeField] internal Sprite fightIcon;
        [SerializeField] internal Sprite eliteIcon;
        [SerializeField] internal Sprite bossIcon;
        [SerializeField] internal Sprite restIcon;
        [SerializeField] internal Sprite eventIcon;
        [SerializeField] internal Sprite treasureIcon;

        // What state a room is in, which is the only thing its tint says. Five
        // rather than the three the flat plates used: "cleared" and "never
        // reachable" used to render identically dim, so a leg's history was
        // unreadable the moment it had one.
        private enum TileState { Current, Walked, Open, Ahead, Closed }

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
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < nodeButtons.Length; i++)
            {
                int index = i;
                nodeButtons[i].onClick.AddListener(() => OnNodePressed(index));
            }

            abandonButton.onClick.AddListener(() =>
            {
                // EndRun settles the books itself. This call used to discard
                // the run without paying it, so walking away from a descent
                // threw away every ember its bosses had earned -- silently,
                // because a discarded snapshot looks the same either way.
                RunManager.EndRun();
                Navigation.Go(Navigation.Hub);
            });
        }

        // ---- the character sheet, between rooms --------------------------------

        // The same two keys as the hub and the fight, so "what am I wearing" is
        // one gesture wherever the player is standing.
        private void Update()
        {
            if (characterSheetPanel == null) return;

            if (Input.GetKeyDown(KeyCode.C)) ToggleCharacterSheet(inventory: false);
            else if (Input.GetKeyDown(KeyCode.I)) ToggleCharacterSheet(inventory: true);
            // Escape belongs to SystemMenuController now, because this panel IS
            // that menu. Closing it here as well would race: Unity does not
            // order Update between components, so the menu's own handler could
            // run after this one, see a closed menu and nothing owning Escape,
            // and reopen it in the same frame the player closed it.
        }

        // Separated from the key for the reason HubController documents:
        // legacy Input cannot be simulated headlessly, so a test that had to
        // press C could not exist. Tests drive these directly.
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
                return;
            }

            var reachable = new HashSet<int>(RunManager.Choices().Select(n => n.Id));
            var cleared = new HashSet<int>(RunManager.Run.clearedNodeIds);

            depthLabel.Set(UiStrings.MapDepth, RunManager.Run.step, RunManager.Run.floor);
            goldLabel.Set(UiStrings.MapGold, RunManager.Run.gold);

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

            LayoutWood(map.DepthCount);

            var occupied = new HashSet<int>();

            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                var column = map.AtDepth(depth).ToList();

                for (int slot = 0; slot < column.Count && slot < MapLayout.Rows; slot++)
                {
                    var node = column[slot];
                    int index = MapLayout.IndexFor(depth, slot);
                    if (index >= nodeButtons.Length) continue;

                    occupied.Add(index);
                    PaintNode(index, node,
                        StateOf(node, current, reachable, cleared),
                        isReachable: reachable.Contains(node.Id));
                }
            }

            // Everything the leg did not use. Hidden rather than dimmed: an
            // empty slot is not a room the player failed to reach, it is a room
            // that does not exist.
            for (int i = 0; i < nodeButtons.Length; i++)
            {
                if (!occupied.Contains(i)) nodeButtons[i].gameObject.SetShown(false);
            }

            PaintTrails(map, current.Id, cleared, reachable);

            // Not while a walk is in flight: the walk owns both the figure and
            // the camera until it arrives, and a refresh from anything else
            // would snap them to a room the party has not reached yet.
            if (!IsWalking)
            {
                PlaceWalker(current);
                FollowCurrent(MapLayout.ColumnX(current.Depth));
            }
        }

        // Which of the five a room is in. Order matters: a cleared room the
        // party is standing in is CURRENT, and a room deeper than the party is
        // AHEAD whether or not a trail to it is open yet, while one at or behind
        // their own depth that was never entered is a road not taken.
        private static TileState StateOf(DescentNode node, DescentNode current,
                                         HashSet<int> reachable, HashSet<int> cleared)
        {
            if (node.Id == current.Id) return TileState.Current;
            if (cleared.Contains(node.Id)) return TileState.Walked;
            if (reachable.Contains(node.Id)) return TileState.Open;
            return node.Depth > current.Depth ? TileState.Ahead : TileState.Closed;
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
        private void PaintTrails(DescentMap map, int currentId, HashSet<int> cleared, HashSet<int> reachable)
        {
            if (trailSegments == null || trailSegments.Length == 0) return;

            var positions = NodePositions(map);
            var seeds = LinkSeeds(map);
            int segment = 0;

            foreach (var node in map.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var from)) continue;

                foreach (int nextId in node.Next)
                {
                    if (!positions.TryGetValue(nextId, out var to)) continue;

                    // LOOKED UP, not counted. This used to be a counter
                    // incremented per link, which the walk then had to
                    // reproduce exactly -- including that a node with no
                    // position skips its links WITHOUT advancing the count.
                    // Two loops agreeing by coincidence is not agreement.
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

        // Where every room in this leg actually sits, by the same MapLayout the
        // nodes were placed with -- so a trail can never land somewhere its
        // room is not.
        private static Dictionary<int, UiVec> NodePositions(DescentMap map)
        {
            var positions = new Dictionary<int, UiVec>();

            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                var column = map.AtDepth(depth).ToList();
                for (int slot = 0; slot < column.Count && slot < MapLayout.Rows; slot++)
                {
                    positions[column[slot].Id] =
                        new UiVec(MapLayout.ColumnX(depth), MapLayout.RowY(slot));
                }
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

        private void PaintNode(int index, DescentNode node, TileState state, bool isReachable)
        {
            var button = nodeButtons[index];
            button.gameObject.SetShown(true);

            // The tile is a BOUNDING BOX, not a stretch target: the tree keeps
            // its own proportions inside an elite's or a boss's bigger box
            // rather than distorting to fill it.
            float width = MapScreen.TileWidthFor(node.Type);
            float height = width + MapLayout.TileHeightBonus;

            var tint = TintFor(state);

            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, height);

            var tree = button.targetGraphic as Image;
            if (tree != null)
            {
                tree.preserveAspect = true;
                tree.color = tint;
            }

            PaintIcon(index, node.Type, tint, width, height);

            nodeLabels[index].SetContent(Caption(node.Type));

            // Only what the party can actually walk to. The map is the authority
            // on adjacency and RunManager refuses anything else anyway, but a
            // button that visibly does nothing is worse than one that is plainly
            // not offered.
            button.interactable = isReachable;

            nodeMarkers[index].gameObject.SetShown(state == TileState.Current);
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

        private static Color TintFor(TileState state)
        {
            switch (state)
            {
                case TileState.Current: return CurrentTint;
                case TileState.Walked: return WalkedTint;
                case TileState.Open: return OpenTint;
                case TileState.Ahead: return AheadTint;
                default: return ClosedTint;
            }
        }

        private static string Caption(RoomType type)
        {
            switch (type)
            {
                case RoomType.Entry: return "START";
                case RoomType.Fight: return "FIGHT";
                case RoomType.EliteFight: return "ELITE";
                case RoomType.Boss: return "BOSS";
                case RoomType.Treasure: return "TREASURE";
                case RoomType.Rest: return "REST";
                case RoomType.Shop: return "SHOP";
                case RoomType.Event: return "EVENT";
                default: return type.ToString().ToUpperInvariant();
            }
        }

        private void OnNodePressed(int index)
        {
            var map = RunManager.Map;
            if (map == null) return;

            // One walk at a time. A second click mid-walk would otherwise start
            // a step from a room the party is halfway out of, and the two
            // coroutines would fight over the same figure.
            if (IsWalking) return;

            int depth = index / MapLayout.Rows;
            int slot = index % MapLayout.Rows;
            var node = map.AtDepth(depth).ElementAtOrDefault(slot);
            if (node == null) return;

            // Legality is checked BEFORE the walk rather than by MoveTo after
            // it. MoveTo is still the authority and still refuses -- but a
            // rejected move used to cost nothing, and now it would cost a walk
            // to a room the party is not allowed to enter, followed by a
            // silent refusal on arrival.
            if (!RunManager.Choices().Any(choice => choice.Id == node.Id)) return;

            _walk = StartCoroutine(WalkAndArrive(node));
        }

            }
}
