# The run map: first realm

Status: draft 2026-09-26, not yet sent to the artist. Live copy: https://claude.ai/artifact/2U4i9HGW3AxVivJeDahamH

*Prince's Palace · art brief · run map · 2026-09-26*

This brief covers the screen where the party picks its next room between fights. It asks for one complete realm, the Hush, plus the node, reward and trail pieces every realm will share. The game's mechanics may still change, so the pieces are kept modular.

## 1 · What the screen is

After each fight, the player sees the whole current realm. The rooms ahead sit along trails that branch from left to right. The player picks the next room from the two or three connected to where they stand, and every choice closes other trails. Each room shows its type and, for fights, what it pays and how strong it is. At the far right waits the realm's heart, the boss. Beating it moves the party one realm deeper.

The engine draws the rooms, the trails, the highlights and all the text on top of your painting. **You paint the place. The game places the rooms.** The graph changes every run, so the painting cannot depend on where the rooms land.

## 2 · The world, for the artist

> **Confidential. This is not player-facing, and the owner decides whether to share it.** Everything in the game is the imagination and emotions of one person, a depressed man with PTSD. Prince, his cat, pulls the souls of dead animals into his palace and sends them to fight his owner's demons. The souls, and the player, are never told what they're really doing. Clearing a realm helps the man.

What that means for the painting:

- **Fantasy, never literal.** A realm is a feeling given the form of a place. No real-life objects standing in for a room, and no hospitals, pills or anything clinical.
- **It reads as a strange realm full of monsters.** Someone who has never played should see a haunted fantasy place.
- **It eases when cleared.** Every realm is painted twice, as found and as eased. The eased version reads as the feeling loosening its grip: light, colour and air coming back. It must not read as a triumphant victory.
- **Melancholic and uncanny, not horror.** Tender more than grim. No gore.

## 3 · Realm 1: the Hush

**The feeling is numbness.** It's the surface layer, the first place the souls are sent. The current game art is a forest, so the Hush keeps a forest but drains it: a wood where colour, sound and wind have stopped. Trees stand too still. Mist lies flat. Light comes from nowhere in particular. Everything is slightly desaturated toward one cold grey-violet, with a few things that still hold a trace of colour, as if they'd forgotten to fade.

- **As found:** still, muffled, flat light, cold grey-violet dominant.
- **As eased:** the same composition with warmth returning at the edges of things: a low sun breaking through, the mist lifting and moving, faint colour in the leaves. Still quiet, but the quiet of rest instead of absence.
- **The heart (right edge):** a single landmark the whole realm leads toward, where the stillness is thickest. For example, a great hollow tree or a dark still pool. Your call, as long as it reads as "the destination" at a glance.
- **Existing house references:** `forest_map_background.png`, `Fight.png`, `forest_mob_elite_fight.png`, `forest_mob_boss_fight.png` in `Art/Backgrounds/`.

## 4 · Composition and safe areas

**Figure: layout zones on the 3840×2160 backdrop master.** The figure shows the master canvas at 3840 x 2160, drawn at a quarter scale for the brief. It marks:

- A **top band** covering roughly the top 12% of the height, where the HUD panel sits — keep it low-detail.
- A **bottom band** covering roughly the bottom 22% of the height, where the room detail card sits — keep it low-detail.
- A **route band** across the middle, between the two bands, where the engine draws rooms and trails. It needs a calm ground plane, mid values, soft texture, so nodes pop.
- A **4:3 safe area** of 2880 px centred horizontally (teal dashed outline), inside which anything the 4:3 crop would otherwise cut off must still read.
- A **21:9 crop** marked with red dashed lines about 257 px in from the top and bottom edges — the 21:9 aspect ratio removes roughly that much from the top and bottom of the master.
- **Start**, marked with a small circle near the left edge of the route band, where the party enters.
- **The heart**, marked with a diamond near the right edge of the route band, where the boss waits.

The caption: "Zones on the 3840 × 2160 backdrop master, drawn at a quarter of the size. The game shows the painting scaled to fill the screen. On 4:3 screens the sides are cut, and on 21:9 screens the top and bottom are cut. Anything that must be seen stays inside both."

- **The middle is ground, not scenery.** The route band needs a calm, walkable-looking plane with even mid values. Put the drama in the upper third (canopy, sky, silhouettes) and at the heart.
- **Left to right is the journey.** Lighter and more open at the left where you enter, drawing the eye to the heart at the right.
- **Leave no painted paths.** The engine draws the trails, and painted ones would contradict them.
- **Keep both states aligned.** "As found" and "as eased" must line up pixel for pixel, because the game cross-fades between them as the party progresses.

## 5 · Shared pieces (all realms)

### Room medallions

One medallion per room type, the same in every realm, so players learn them once. Each type needs a **distinct silhouette**, readable in greyscale at 48 px. Colour alone must never be what tells them apart. The engine adds state (current, choosable, ahead, closed) with rims and dimming, so paint each medallion once, neutral and strong in value.

| Room | Silhouette to start from | Notes |
|---|---|---|
| Fight | Round | The most common room: the calmest medallion |
| Elite | Faceted, hexagon-like | A clear step up from Fight |
| Boss (heart) | Large gate or diamond | About 1.4 times the others |
| Rest | Peaked, like a flame or tent | Warmest of the set |
| Shop | Square | |
| Event | Rounded square | Events are story moments, some of them his memories |
| Treasure | Small diamond | |
| Start | Small plain circle | Where the souls enter |

### Reward chips and pips

- Three chips that sit on a fight medallion's corner: **gold**, **spell book**, **consumable**. They must read at 20 px.
- One **strength pip** (the engine shows 1–3 of them), readable at 12 px.

### Trail and glow

- One tiling trail stroke, repeating left to right (at least 4:1). The engine tints it for walked, open, ahead and closed.
- One soft glow ring for "choosable now", painted on solid black. The engine adds it with a screen blend.

## 6 · Deliverables

| File | Size | Background |
|---|---|---|
| `realm_hush_found.png` | 3840 × 2160 | Full-frame opaque |
| `realm_hush_eased.png` | 3840 × 2160, aligned to the file above | Full-frame opaque |
| `map_node_<type>.png` × 8 | 512 × 512 master | Flat `#00FF00` |
| `map_chip_gold / spell / consumable.png` | 256 × 256 | Flat `#00FF00` |
| `map_pip_strength.png` | 128 × 128 | Flat `#00FF00` |
| `map_trail.png` | 2048 × 512, tiles left to right | Flat `#00FF00` |
| `map_glow_open.png` | 512 × 512 | Solid black (screen blend) |

These are house rules. Backgrounds are delivered opaque with no keying. Painted objects go on a flat #00FF00 field and are keyed by hue, because green appears nowhere in the game's palette. Pure glows go on black. No text, borders or frames on any asset.

## 7 · Style

- **Painting:** a rich, hand-painted style with visible brush texture, the painterly look of Hades.
- **Medallions and chips:** the same painterly hand with bolder shapes and cleaner edges. They are UI, and they must survive being shown small.
- The enemies drawn on top of all this use flat cel shading with bold outlines, as on the Giant Rat sheet. The backdrop should sit behind that style without fighting it.

## 8 · Checkpoints

1. Three small thumbnail compositions for the Hush in the "as found" state, plus one sheet of medallion silhouettes shown at 48 px.
2. The chosen composition, "as found", finished.
3. The "as eased" state, painted over the same file.
4. Medallions, chips, pip, trail and glow.

Please stop at each checkpoint for sign-off. Changing composition or silhouettes is cheap at step 1 and expensive at step 2.

## 9 · Not wanted

- Real-life objects standing in for parts of the realm.
- Hospitals, pills or anything clinical.
- Gore or jump-scare horror.
- A victorious "eased" state.
- Painted paths or room markers in the backdrop.
- Text anywhere in the art.

---

Later realms go deeper and will be briefed separately. They are listed here only so the Hush's language can extend to them: dread as an endless dusk, guilt as a place that keeps rebuilding what you break, anger as a smouldering waste. None of these is decided.
