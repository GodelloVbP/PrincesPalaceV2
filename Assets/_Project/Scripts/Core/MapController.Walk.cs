using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The party's figure, and the walk between rooms.
    //
    // Picking a room used to resolve it on the same frame as the click, which
    // meant the map's whole job -- deciding where to go -- happened with no
    // sense of GOING anywhere. Now the click starts a walk along the trail that
    // was already drawn between the two rooms, and the room's content fires on
    // ARRIVAL.
    //
    // The room resolution itself is untouched and deliberately still lives in
    // one place: Arrive() is exactly what OnNodePressed used to do, so Event,
    // Shop and ItemSpawn will wire into it when they have screens without the
    // movement code learning anything about them.
    public partial class MapController
    {
        // Which character is drawn, resolved from the squad rather than fixed.
        // The stance library and the manifest are the fight stage's, reused:
        // one actor-art pipeline, so a character that works in a battle works
        // here on the same content entry.
        private const string WalkStance = "idle";

        private Coroutine _walk;

        public bool IsWalking => _walk != null;

        // Where the figure stands right now, in content coordinates. Kept so a
        // refresh mid-descent can put it back without re-deriving which slot
        // the party is in.
        private UiVec _standing;

        private void PlaceWalker(DescentNode node)
        {
            if (walker == null || node == null) return;

            ResolveWalkerArt();

            _standing = MapWalk.Standing(PositionOf(node));
            walker.gameObject.SetShown(true);
            walker.rectTransform.anchoredPosition = new Vector2(_standing.X, _standing.Y);
        }

        // The figure's art, from whoever is actually leading the squad.
        //
        // Resolved every refresh rather than cached: the party can change
        // between descents, and StanceAnimationLibrary caches the load itself,
        // so this costs a dictionary hit rather than a Resources probe.
        private void ResolveWalkerArt()
        {
            var save = SaveSlotManager.CurrentSave;
            var squad = save?.ActiveSquad();
            var leader = squad != null && squad.Count > 0 ? squad[0] : null;
            var definition = leader != null ? ContentDatabase.GetCharacter(leader.definitionId) : null;

            string folder = definition != null ? definition.data.BattleSpritePath : null;
            if (string.IsNullOrWhiteSpace(folder)) return;

            var sprite = StanceAnimationLibrary.Resolve(folder, WalkStance);
            if (sprite == null) return;

            walker.sprite = sprite;
            walker.preserveAspect = true;

            // The BOX follows the art, not the other way round. Sizing every
            // character into Shawn's 540x370 box would squash anyone drawn
            // taller than wide -- and preserveAspect would then letterbox them
            // into a figure standing in the middle of a wide empty rect, which
            // reads as the wrong standing position rather than as the wrong
            // size.
            float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            walker.rectTransform.sizeDelta =
                new Vector2(MapWalk.FigureHeight * aspect, MapWalk.FigureHeight);
        }

        private static UiVec PositionOf(DescentNode node)
        {
            var column = RunManager.Map.AtDepth(node.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == node.Id);
            if (slot < 0) slot = 0;

            return new UiVec(MapLayout.ColumnX(node.Depth), MapLayout.RowY(slot));
        }

        // One walk, then whatever the room turns out to be.
        private IEnumerator WalkAndArrive(DescentNode target)
        {
            var current = RunManager.CurrentNode;
            var from = PositionOf(current);
            var to = PositionOf(target);

            int seed = LinkSeeds(RunManager.Map).TryGetValue(LinkKey(current.Id, target.Id), out var s) ? s : 1;
            float duration = MapWalk.DurationFor(from, to);

            // Flipped by SCALE rather than by a mirrored sprite: the descent
            // runs left to right so this never fires today, and the alternative
            // is a second art file per character for a case the map does not
            // have yet.
            var scale = walker.rectTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (MapWalk.FacesLeft(from, to) ? -1f : 1f);
            walker.rectTransform.localScale = scale;

            // THE CAMERA HOLDS STILL WHILE HE WALKS.
            //
            // It followed him at first, which sounds right and looks wrong: the
            // scroll is pinned to the room offset, so tracking the figure pins
            // the FIGURE to a fixed screen position and slides the entire wood
            // past him. Everything moves except the thing that is moving.
            //
            // Held instead, he crosses the gap between the two painted
            // clearings in full view -- which is the one thing this animation
            // exists to show -- and the world catches up afterwards.
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var point = MapWalk.PositionAt(from, to, seed, elapsed / duration);
                walker.rectTransform.anchoredPosition = new Vector2(point.X, point.Y);
                yield return null;
            }

            _standing = MapWalk.Standing(to);
            walker.rectTransform.anchoredPosition = new Vector2(_standing.X, _standing.Y);

            yield return PanTo(to.X);

            _walk = null;
            Arrive(target);
        }

        // The camera catching up, once the party has arrived.
        //
        // Eased rather than cut: the pan is exactly one column-gap, and a jump
        // that size reads as the map having been replaced rather than as
        // having moved one step along it. Smoothstepped, so it leaves and
        // arrives at rest -- a linear pan of the whole screen stops dead.
        private IEnumerator PanTo(float roomX)
        {
            float viewportWidth = viewport.rect.width;
            float from = content.anchoredPosition.x;
            float to = MapLayout.Scroll(roomX, content.sizeDelta.x, viewportWidth);

            if (Mathf.Approximately(from, to)) yield break;

            for (float elapsed = 0f; elapsed < MapWalk.PanSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / MapWalk.PanSeconds;
                content.anchoredPosition = new Vector2(
                    Mathf.Lerp(from, to, t * t * (3f - 2f * t)), content.anchoredPosition.y);
                yield return null;
            }

            content.anchoredPosition = new Vector2(to, content.anchoredPosition.y);
        }

        // What the room does. Byte for byte what the click used to do the
        // instant it was pressed -- the only change is WHEN.
        //
        // The room's own rules (move, reset the message, resolve, clear) moved
        // to RunOrchestrator.ArriveAt so the balance bot walks into rooms
        // through the same code a player does -- docs/PLAN_BALANCE_BOT.md F2.
        // What is left here is the two things a bot has no use for: which
        // scene to load, and repainting the map.
        private void Arrive(DescentNode target)
        {
            switch (RunOrchestrator.ArriveAt(target))
            {
                case RunOrchestrator.Arrival.Refused:
                    return;

                case RunOrchestrator.Arrival.Fight:
                    Navigation.Go(Navigation.Fight);
                    return;

                case RunOrchestrator.Arrival.Shop:
                    // A nested panel, like the fight is a scene -- no
                    // Navigation.Go, and the room stays uncleared until the
                    // shop's own LEAVE button calls RunOrchestrator.LeaveShop
                    // (ShopController.Leave -> MapController.Refresh via
                    // Finished). Replaces gate 1's shim, which had no screen
                    // to open and left immediately instead.
                    OpenShop();
                    return;

                case RunOrchestrator.Arrival.Resolved:
                    Refresh();
                    return;

                default:
                    // EXPLICIT, and the default arm is now the one that
                    // SHOUTS rather than the one that swallows. It used to
                    // be the catch-all every non-fight arrival fell through,
                    // which meant a new Arrival value would silently
                    // clear-and-redraw -- the wrong failure, and an invisible
                    // one (docs/PLAN_SHOP.md F3).
                    Debug.LogWarning(
                        "[MapController] Arrival value not handled; the map redrew and the room may not have " +
                        "been resolved. A new Arrival case needs an arm here.");
                    Refresh();
                    return;
            }
        }

        // The jitter seed for every link, built once and shared.
        //
        // The trail and the walk MUST agree on this or the figure crosses open
        // canopy beside the path it is supposed to be on. They used to agree by
        // both counting links in the same order, which is a coincidence waiting
        // to be broken by anything that reorders a loop; now there is one count
        // and two readers.
        private static Dictionary<long, int> LinkSeeds(DescentMap map)
        {
            var seeds = new Dictionary<long, int>();
            int seed = 0;

            foreach (var node in map.Nodes)
            {
                foreach (int nextId in node.Next)
                {
                    seed++;
                    seeds[LinkKey(node.Id, nextId)] = seed;
                }
            }

            return seeds;
        }

        private static long LinkKey(int from, int to) => ((long)from << 32) | (uint)to;
    }
}
