# Handoff: Run Map — forest floor, image-based nodes

## Overview

Floor map for a turn-based roguelike dungeon crawler (Unity 6, legacy uGUI). Replaces
a coloured-block node grid with **painted-clearing nodes**: every room shares one
forest backdrop, and room type reads from a foreground silhouette layered in front of
it (nothing / a mob face / a bigger meaner mob face / campfire / chest / rune / boss
face+aura). Paths between rooms are a curved dirt trail, not a straight line.

Floor is **endless**: every 8th room is an Elite cap, every 16th is a Boss cap, then it
repeats. The map view scrolls horizontally and auto-follows the player.

## ⚠️ ART NEEDED — read this before building

**Every visual asset in this prototype is a placeholder built out of flat SVG shapes
(ellipses, rectangles, polygons) — colored circles standing in for a mob's face, a
teardrop standing in for a campfire flame, etc. None of this is meant to ship.**

The user's plan is to **generate the real art with ChatGPT (image generation)** before
or during your implementation pass. Please:

1. **Do not hand-draw, vectorize, or otherwise "finish" the SVG placeholder shapes
   into permanent game art.** They exist only to prove the layout and the
   silhouette-in-front-of-backdrop concept.
2. **Flag back to the user** (don't silently proceed with placeholder art baked into
   sprites) that the following assets still need to be generated, and offer to write
   the image-generation prompts for each once you know the target art style:
   - `clearing_forest` — the shared tree backdrop tile (one asset, used behind every
     node regardless of type)
   - `face_mob`, `face_elite`, `face_boss` — the three-tier creature silhouette shown
     in front of the trees (same "family," escalating in size/menace)
   - `prop_campfire`, `prop_chest`, `prop_rune` — the rest / shop / event overlays
   - `path_dirt` — a tileable/9-slice trail texture (wide brown tread + lighter
     worn centerline) to skin the connector between nodes
   - Optionally a `path_dirt_walked` gold-tinted variant, since walked trail is
     currently just a recolor of the same shapes
3. Build the layout and state logic against the placeholders as-is (sizes/positions
   below are final), then swap in real art once supplied — the node prefab and trail
   renderer should treat art as a drop-in sprite reference, not something to
   regenerate per state (see *State tinting*, which is a filter pass, not separate art
   per state).

## About the Design Files

`Run Map.dc.html` is a **design reference built in HTML** — a working, interactive
prototype of layout and behaviour, not production code to port line by line. Open it
directly in a browser (`support.js` must sit beside it). Recreate it in the target
codebase's existing uGUI patterns — `Image`/`Button` components, `RectTransform`
anchoring, whatever panel/menu prefabs the project already has.

## Fidelity

**Layout, state logic, and interaction are final** — node positions, sizes, scroll
behaviour, edge routing, cadence rule (8/16), legend, and info panel. **All node and
path art is placeholder**, per the ART NEEDED section above — do not treat colors/
shapes in the SVGs as final, only their size and position as the slot the real art
will fill.

## Screen: Run Map

**Canvas:** 1920 × 1080 reference resolution. Node graph lives in a horizontally
scrolling sub-region; everything else (header, HUD, info panel, legend, buttons) is
pinned to the fixed frame.

### Layout regions

| Region | Position |
|---|---|
| Floor title / room counter | top-left, x 56 y 44 |
| Gold / relic HUD | top-right, x -40 y 36, 380×90 |
| Progress rail | full width, y 150, 3px track + gold fill |
| **Scrolling map canvas** | x 0, y 170, 1920×820, `overflow-x:auto` `overflow-y:hidden`, auto-scrolls to keep the current node ~700px from the left edge |
| Info panel (hover/current room) | bottom-left, x 56, 520×132 min |
| Legend + Reset/Retreat buttons | bottom-right |
| Fog fade + "THE WOOD CONTINUES" | right edge of the scroll canvas, signals the floor keeps generating past what's rendered |

### Node prefab

Base tile 120×150 (portrait — trees read taller than wide), anchored so the **ground
line sits at the row's Y** (edges connect at tile-top-y + 24, i.e. roughly where a
figure would stand). Elite scales the whole tile ×1.16, Boss ×1.4, both from
`transform-origin: bottom center` so bigger tiers grow upward, not into the ground.

Every tile = backdrop + optional foreground overlay:

- **Backdrop (`clearing_forest`), always present:** 5 trees (trunk rect + canopy
  ellipse), same asset regardless of room type.
- **Foreground overlay, by type:**
  | Type | Overlay | Notes |
  |---|---|---|
  | Forest (plain) | none (two faint firefly dots only) | safe waypoint, no encounter |
  | Mob | small red face, plain | standard fight |
  | Elite | bigger face, 3-spike crown, brighter gold trim | caps every 8th room |
  | Boss | biggest face, 6-spike crown, outer aura ring | caps every 16th room |
  | Rest | campfire (crossed logs + flame) + teal aura | heal-or-forge choice |
  | Shop | chest (lid arc + latch) + gold aura | mid-run spend |
  | Event | glowing orb + spiral rune + violet aura | no-combat choice |
- **Ground glow:** soft accent-colored ellipse under the tile, tinted per room type —
  reuse of the "glow floor" language from the hub screen; keep this even after real
  art lands, it's what sells a tile as clickable.
- **Label:** room name, centered 20px below the tile.

### State tinting (not separate art per state)

Applied as a CSS filter + opacity pass on the whole tile — one prefab, one art set,
five looks:

| State | Treatment |
|---|---|
| Closed / unreachable | `grayscale(.6) brightness(.5)`, opacity .55 |
| Ahead (visible, not yet reachable) | `grayscale(.25) brightness(.8)`, opacity .8 |
| Open (clickable now) | full color, pulsing radial beacon glow behind the tile |
| Walked | `brightness(.9) sepia(.22)`, opacity .95 |
| Current | `brightness(1.22)`, gold drop-shadow rim |

Port this as a material/tint pass (or a small set of pre-tinted material instances) on
the node prefab — do not ask the art pipeline for five versions of every icon.

### Path (edges)

Each connector is two stacked curved strokes between tile ground-points (cubic bezier,
slight organic jitter so it doesn't look ruler-straight):

- **Outer tread:** wide, dark brown, rounded caps — the packed dirt.
- **Inner tread:** thinner, lighter tan, dashed — worn footing / pebbles.

| State | Outer | Inner | Notes |
|---|---|---|---|
| Walked | brown 16px | gold dash 5px | fully opaque, this is where the player has been |
| Open (leaves current node) | brown 14px | pale tan dash 4px | dash animates (crawling offset) to hint direction of travel |
| Ahead | dim brown 10px | faint dash 2px | ~55% opacity |
| Closed | near-black 6px | none | ~30% opacity, fades into underbrush |

Once `path_dirt` art exists, this becomes a 9-slice or spline-texture ribbon following
the same control points — the curve math doesn't change, only the stroke becomes a
textured strip instead of a flat colored line.

### Cadence rule

```
roomIndex % 16 === 0  → Boss cap
else roomIndex % 8 === 0 → Elite cap
else → roll Forest / Mob / Rest / Shop / Event
```

The prototype renders two cycles (rooms 1–16) with a fog fade + "THE WOOD CONTINUES"
hint past room 16 — the real map generator repeats this pattern indefinitely; the view
just keeps scrolling.

### Interactions

- Click an **open** (reachable) node → advances `path`, re-derives which nodes are now
  walked/open/ahead/closed, and auto-scrolls the canvas to center the new current node.
- Hover any node (reachable or not) → fills the bottom-left info panel with that room's
  name/description; does not move the player.
- **RESET** clears back to the entry node. **RETREAT** is a stub button (wire to
  whatever "abandon run" flow exists elsewhere).

## Design Tokens

**Type:** Chakra Petch throughout, same weights/letter-spacing conventions as the
other screens in this project (see the Battle UI handoff's token table if you need the
full scale — this screen reuses it, just retinted green/brown for the forest theme).

**Palette shift from the palace screens:** base gradient moved from deep purple to
mossy green/brown (`#1e2f18` → `#07040c`), while gold accents (`#e7b25c`, `#ffe0a8`)
and the violet event/relic tint (`#b48cff`) stayed constant — keep gold and violet
pinned across every floor theme so currency and "event" always read the same
regardless of biome skin.

**Accent per room type** (used for ground glow, aura rings, legend chips):

| Type | Hex |
|---|---|
| Forest | `#7fc25a` |
| Mob | `#e0596a` |
| Elite | `#ff6f4a` |
| Boss | `#ff9a4a` |
| Rest | `#7ce0d6` |
| Shop | `#e7b25c` |
| Event | `#b48cff` |

## Files

| File | What it is |
|---|---|
| `Run Map.dc.html` | The design prototype. Open in a browser; fully interactive. Authoritative for layout, state logic and behaviour — **not** for final art (see ART NEEDED). |
| `support.js` | Runtime the prototype needs. Must sit beside the HTML. |

## What this fixes, for context

1. Room type used to be a letter-coded colored square — now it's a readable scene
   (trees + a creature/prop) that teaches itself without a legend.
2. Threat tiers (mob → elite → boss) now scale one asset family instead of needing
   three unrelated icons.
3. Paths look like paths instead of UI connector lines.
4. The floor no longer implies a fixed end — the fog fade and repeating cadence make
   the "this goes on" structure visible in the map itself.
