# Handoff: Battle UI — nested command system

## Overview

Replacement combat HUD for a turn-based roguelike dungeon crawler (Unity 6, legacy
uGUI). It replaces a flat row of five identical gold buttons plus loose debug text
overlays with a **three-depth nested command system**: verb → choice → target.

The design goal: at every moment the player can see *what depth they are at*, *what
they picked*, *what it costs*, and *where it will land* — without any element covering
the battlefield or the character sprites.

`reference_current_battle_ui.png` in this folder is the screen being replaced.

## About the Design Files

`Battle UI.dc.html` is a **design reference created in HTML** — a working prototype of
the intended look and behaviour, not production code to port line by line. It runs in
a browser (open it directly; `support.js` must sit beside it) and is fully
interactive, so it is the authoritative source for *behaviour* as well as *layout*.

The task is to **recreate this design in the target codebase's existing environment**
— here Unity 6 + legacy uGUI — using that project's established patterns: `Image`
components with sprites, `Button` components, `RectTransform` anchoring, and whatever
existing panel/menu prefabs the project already has. Do not attempt to embed the HTML.

Where the prototype uses CSS gradients, blurs and glows, the engine equivalent is a
**baked sprite** — the project has no shader or particle budget for UI, so gradients,
inner shadows and glows must be authored into the panel PNGs. This is called out per
component below.

## Fidelity

**High-fidelity.** Colours, typography, spacing, sizes and interaction states are
final and specified exactly below. Recreate them faithfully. The two exceptions:

- Character/enemy sprites and portraits are **placeholders** (dashed boxes labelled
  `HERO SPRITE`, `ENEMY GROUP SPRITES`, `PORTRAIT`). Real art goes in those rects.
- The battle background is a placeholder (the user is replacing it separately). The UI
  is designed to sit over a dark, low-contrast field; every panel carries its own
  translucent dark fill so it survives a brighter background.

## Screen: Battle HUD

**Purpose:** the player reads party and enemy state, then issues one command per turn.

**Canvas:** 1920 × 1080 reference resolution, `CanvasScaler` = `ScaleWithScreenSize` /
`Expand`. All coordinates below are in canvas pixels. Anchoring is given per region
because non-16:9 displays stretch the canvas non-uniformly.

### Layout regions

| Region | Anchor | Position / size |
|---|---|---|
| Turn timeline | top-left | x 40, y 34 (from top), auto width, 78px chip + label |
| Dialogue bark | top-centre | y 34 from top, max-width 840, centred |
| Enemy plates | top-right | x 40 from right, y 150 from top, width 400, column gap 12 |
| Party plate | bottom-left | x 40, y 40 from bottom, width 452 |
| Command columns | bottom, between | left 524, right 472, y 40 from bottom, height 300 |
| Target prompt | bottom-centre | y 364 from bottom, centred, pointer-events none |

The command area is deliberately bounded by the party plate on the left and the enemy
plates on the right, so a longer submenu grows **upward** and never collides with the
dialogue bark or overlaps a sprite.

---

### 1. Turn timeline (top-left)

A horizontal run of square portrait chips showing turn order.

- **Acting chip:** 78 × 78, border `2px #ffe0a8`, fill = portrait sprite,
  `box-shadow: 0 0 26px rgba(255,206,128,.45)` (bake as glow on the frame sprite).
  Inner hairline `1px rgba(255,255,255,.14)` inset.
  Label below: `NOW`, Chakra Petch 600 11px, letter-spacing `.2em`, `#ffe0a8`,
  gently pulsing opacity .5 → 1 over 1.8s ease-in-out.
- **Divider:** 1 × 62px, `rgba(200,170,230,.2)`, 8px down from the top of the row.
- **Upcoming chips:** 64 × 64 (0.82 scale of the acting chip — the size difference is
  what makes the current turn unmistakable), border `1px rgba(200,170,230,.22)`,
  numeric label `2…6` in Chakra Petch 600 11px, letter-spacing `.18em`, `#7f6f98`.
  The **third** upcoming chip in the mock is emphasised (`.42` border alpha, label
  `#c3b0d8`, soft violet glow) purely to demonstrate the "enemy turn" emphasis state —
  wire that emphasis to whichever chips are hostile.
- Chip padding: 14px horizontal on the acting chip, 11px on upcoming ones (i.e. gap of
  22–28px between chips).
- Chip fill placeholder in the mock is a 45° 7px stripe pattern; replace with the
  portrait sprite.

**Engine:** one 78² chip sprite, scaled to 64 for upcoming. Two frame sprites
(`chip_now` gold, `chip_next` violet).

### 2. Dialogue bark (top-centre)

- Container: max-width 840, padding `14px 24px 14px 14px`, border
  `1px rgba(200,170,230,.22)`, radius 4, fill
  `linear-gradient(rgba(24,14,32,.88), rgba(12,7,18,.9))`.
- Speaker portrait: 56 × 56, border `1px rgba(200,170,230,.3)`, 18px gap to text.
- Text: Chakra Petch 400 22px / 1.3, `#efe4fb`, `text-wrap: pretty`.
- `pointer-events: none` — it must never eat a click.

The bark text is **state-driven**, not decorative. See *State Management*.

### 3. Enemy plates (right) — doubles as the target picker

Header row above the column: `ENEMIES` (Chakra Petch 600 12px, letter-spacing `.26em`,
`#9d8db4`) left, status hint right (`3 STANDING` normally, `CLICK TO CONFIRM` while
targeting; 600 12px, `.14em`, `#7f6f98`).

Each plate, width 400, padding `14px 16px`:

| State | Border | Extra |
|---|---|---|
| Idle (not targeting) | `1px rgba(224,120,110,.26)` | — |
| Targeting, not hovered | `1px rgba(255,196,90,.5)` | glow `0 0 24px rgba(231,140,60,.2)`, reticle visible |
| Targeting, hovered | `1px #ffc45a` | same glow, `transform: scale(1.03)`, brighter reticle |

- Fill: `linear-gradient(100deg, rgba(38,16,18,.86), rgba(16,8,14,.88))`.
- Name: Chakra Petch 600 17px, letter-spacing `.03em`, `#f0dcd8` (`#fff0d8` hovered).
- HP text: Chakra Petch 600 14px, `#e0a89c`, right-aligned, `nowrap`.
- HP bar: 9px below the name row, height 11, border `1px rgba(220,140,140,.3)`, track
  `rgba(14,7,12,.85)`, fill `linear-gradient(#e07a62, #8e3226)` at `hp/max` width.
- Status tags: 9px below the bar, flex row gap 8, wrap. Each tag: padding `5px 9px`,
  border `1px rgba(200,170,230,.26)`, Chakra Petch 500 10px, letter-spacing `.16em`,
  `#c3b0d8`. Mock content: `CASTING: HEX BOLT`, `SLOWED`.
- **Reticle** (targeting only): 14 × 14 diamond (square rotated 45°) pinned at
  `left: -13px`, vertically centred, `#ffc45a` when hovered else
  `rgba(255,196,90,.5)`, pulsing opacity .55 → 1 / scale 1 → 1.05 over 1.6s.

Mock data: two `Elite Bog Witch` (250/250 and 188/250) and one `Bog Acolyte` (64/120).

### 4. Party plate (bottom-left)

Width 452, padding `22px 24px`, border `1px rgba(231,178,92,.42)`, radius 4, fill
`linear-gradient(rgba(34,22,18,.9), rgba(14,8,16,.92))`, shadow
`0 0 30px rgba(0,0,0,.6)` + inset top highlight `0 1px 0 rgba(255,220,170,.14)`.

- Portrait: 64 × 64, border `1px rgba(231,178,92,.5)`, 16px gap.
- Name: `SHEEP`, Chakra Petch 600 24px, letter-spacing `.06em`, `#fff3de`.
  Class line right-aligned: `LV 7 · SHEPHERD`, 500 13px, `.2em`, `#c8a879`.
- **HP row** (12px below the name row): label `HP` fixed 26px wide, 600 12px, `.14em`,
  `#e08a7a`; bar flex-1, height 16, border `1px rgba(224,138,122,.38)`, track
  `rgba(14,7,12,.85)`, fill `linear-gradient(#e07a62,#9a3a2c)`; value 78px wide,
  right-aligned, 600 14px, `#f0c8bc`. Mock: `189/360` (52.5%).
- **MP row** (8px below): label `MP`, `#8ab4e0`; bar border
  `1px rgba(138,180,224,.34)`, track `rgba(8,10,20,.85)`, fill
  `linear-gradient(#7ea8e6,#3a5a9a)`; value `#c4d8f2`. Mock: `42/42`.
- **MP cost preview** — the important bit. When the player hovers an MP-costing skill,
  a lighter segment is drawn at the *right end of the filled portion*, width
  `cost / mpMax`, fill `linear-gradient(rgba(200,226,255,.9), rgba(120,160,220,.9))`,
  glow `0 0 12px rgba(180,214,255,.6)`. It shows what the skill would consume, inside
  the resource, so cost is legible without reading a number.
- **Wool row:** 18px below, separated by a 16px-padded top border
  `1px rgba(231,178,92,.2)`. Label `WOOL` (600 12px, `.24em`, `#9d8db4`), then 16 pips
  in a flex row gap 5, then `3/16` (600 15px; `3` in `#e6dcf0`, `/16` in `#7f6f98`).
  Each pip is 13 × 20: **filled** = border `1px rgba(240,232,255,.7)` + fill
  `linear-gradient(#fbf7ff,#c8bcdc)`; **empty** = border
  `1px rgba(160,140,190,.24)` + fill `rgba(14,8,20,.7)`.

### 5. Command columns (bottom-centre) — the nested system

Three columns, all **bottom-aligned at y = 40** so lists grow upward.

#### Column A — root verbs (x 524, width 300)

Rendered bottom-up (`ATTACK` at the bottom, nearest the thumb/cursor), rows 52 tall,
gap 10. Below the column sits the **breadcrumb**: Chakra Petch 600 11px,
letter-spacing `.26em`, `#7f6f98`, values `COMMAND` /
`COMMAND › SKILL` / `COMMAND › SKILL › TARGET`.

Row anatomy: hotkey (20px wide, ui-monospace 600 12px) · label (flex-1) · caret `›`
(18px, only on rows that nest).

| Row | Hotkey | Nests | Border | Fill | Label |
|---|---|---|---|---|---|
| `ATTACK` (primary) | 1 | no → jumps to target | `rgba(231,178,92,.7)` | `linear-gradient(100deg,rgba(70,38,18,.9),rgba(32,17,10,.92))` | 600 **19px**, `.16em`, `#ffd9a2` |
| `SKILL` | 2 | yes | `rgba(200,170,230,.34)` | `linear-gradient(100deg,rgba(30,18,42,.9),rgba(15,8,22,.92))` | 600 17px, `#ddd0ee` |
| `ITEM` | 3 | yes | same as SKILL | same as SKILL | same as SKILL |
| `RUN` (quiet) | 4 | no | `rgba(180,150,210,.2)` | `rgba(16,9,24,.78)` | 600 17px, `#8a7aa0` |
| `HOLD BACK` (quiet) | 5 | no | same as RUN | same as RUN | same as RUN |

Active (branch open) state: border `#ffe0a8`, fill
`linear-gradient(100deg,rgba(88,50,22,.95),rgba(44,24,12,.95))`, label `#ffeccc`,
glow `0 0 26px rgba(231,140,60,.3)`, caret `#ffe0a8`.
Hotkey colour: `rgba(255,220,170,.55)`, or `#6d5f85` on quiet rows.
Primary rows carry glow `0 0 20px rgba(231,140,60,.18)`.

The hierarchy is deliberate: **ATTACK loud, SKILL/ITEM neutral, RUN/HOLD BACK quiet** —
read order matches use frequency. This is the main fix over the old five-identical-
buttons row.

#### Column B — submenu (x 844, width 404)

Opens when `SKILL` or `ITEM` is chosen; animates in with
`translateX(-10px) → 0` + opacity 0 → 1 over 140ms ease-out.

Header row: title left (600 11px, `.26em`, tinted with the current accent) — `SKILLS`
or `SATCHEL`; hint right (500 11px, `.14em`, `#7f6f98`) — `MP 42/42` or
`COUNTS SHOWN`.

Rows 66 tall, gap 8, padding `0 16px`:

- **Mark:** 36 × 36, border `1px <tint>99`, fill = the ability icon (placeholder in the
  mock is a 45° stripe in the tint).
- **Name:** Chakra Petch 600 18px, `.03em`, `#e9dff8` (`#fff` when selected), ellipsis
  on overflow.
- **Meta:** 7px below, 500 11px, letter-spacing `.18em`, `<tint>cc` — e.g.
  `PHYSICAL · SINGLE`, `SHOCK · SINGLE`, `SUPPORT · SELF`.
- **Cost:** right, 600 14px, `.1em`, `#ffe0a8` when affordable else `#8a7aa0`.
- Idle border `1px rgba(200,170,230,.28)`, fill `rgba(18,10,26,.82)`.
- Selected border `1px <tint>`, fill
  `linear-gradient(100deg,rgba(40,24,54,.95),rgba(18,10,26,.95))`, glow
  `0 0 24px <tint>33`.
- **Unaffordable:** border `1px rgba(140,120,170,.16)`, `opacity: .45`,
  `cursor: not-allowed`. Never hidden — the player should learn what exists.
- **BACK row** at the bottom of the column (2px extra top margin): hotkey chip `ESC`
  (ui-monospace 600 12px, `#7f6f98`) + `BACK` (500 13px, `.16em`, `#a695bc`), height
  ~44 (padding `11px 16px`), border `1px rgba(180,150,210,.24)`, fill
  `rgba(16,9,24,.8)`.

**Skill list (mock content, tints in brackets):**

| Name | Cost | Meta | Description | Stats |
|---|---|---|---|---|
| Shear `#e7b25c` | 3 WOOL | PHYSICAL · SINGLE | Strip wool for a heavy cut. Damage scales with wool spent; spends 3 of your 16. | DAMAGE 84–96 · COST 3 WOOL · TARGET ONE ENEMY |
| Lightning Bolt `#8ab4e0` | 5 MP | SHOCK · SINGLE | A forked strike. Chains to a second enemy for half damage if the first is already shocked. | DAMAGE 62–70 · COST 5 MP · TARGET ONE ENEMY |
| Frost Flare `#7ce0d6` | 5 MP | FROST · GROUP | Cold burst across the enemy line. Slows the next turn of anything it hits. | DAMAGE 34–41 · COST 5 MP · TARGET ALL ENEMIES |
| Woolgathering `#b48cff` | FREE | SUPPORT · SELF | Skip your strike to regrow 4 wool and shrug off one hex. | RESTORES +4 WOOL · COST FREE · TARGET SELF |
| Thunderhoof `#e07a62` | 14 MP | PHYSICAL · GROUP | Stamp the ground. Hits everything, including allies stood too close. | DAMAGE 70–88 · COST 14 MP · TARGET ALL |

**Item list:**

| Name | Held | Meta | Description | Stats |
|---|---|---|---|---|
| Health Potion `#e07a62` | ×2 | CONSUMABLE · ALLY | Restores 120 HP to one ally. Does not end your turn if used on yourself. | RESTORES 120 HP · HELD 2 · TARGET ONE ALLY |
| Ether Draught `#8ab4e0` | ×1 | CONSUMABLE · ALLY | Restores 25 MP. Bitter enough to cost you the rest of the turn. | RESTORES 25 MP · HELD 1 · TARGET ONE ALLY |
| Bog Flask `#a8c860` | ×3 | THROWN · GROUP | Shatters into caustic mist. Ignores armour, hits the whole enemy line. | DAMAGE 44 · HELD 3 · TARGET ALL ENEMIES |
| Salt Ward `#9d8db4` | ×0 | CONSUMABLE · SELF | Blocks the next hex cast at you. You are out of these. | EFFECT 1 HEX · HELD 0 · TARGET SELF |

`Salt Ward` at ×0 is the reference for the unaffordable state.

#### Column C — detail panel (x 1268, width 340)

Not a hover tooltip — a **persistent third column**, so arrowing through options lets
the player compare without re-hovering. Same 140ms slide-in.

Padding `20px 22px`, border `1px rgba(200,170,230,.26)`, fill
`linear-gradient(rgba(26,16,36,.92), rgba(12,7,18,.94))`.

- Name: Chakra Petch 600 20px / 1.15, `.04em`, `#f4ebff`.
- Kind: 8px below, 500 12px, `.2em`, accent tint.
- Body: 14px below, 400 14px / 1.55, `#bfb0d4`, `text-wrap: pretty`.
- Stats block: 16px below, separated by a 14px-padded top border
  `1px rgba(200,170,230,.16)`, rows gap 9, key/value space-between. Key 500 12px,
  `.16em`, `#8a7aa0`; value 600 15px in the accent tint.

`ATTACK` populates this column too, with a synthetic entry: **Strike** ·
`PHYSICAL · SINGLE` · "A plain swing of the crook. No cost, no flourish." ·
DAMAGE 48–56 · COST FREE · TARGET ONE ENEMY.

### 6. Target prompt (bottom-centre, above the columns)

Visible only at target depth. `pointer-events: none`. Padding `12px 22px`, border
`1px rgba(255,196,90,.55)`, fill `rgba(38,22,10,.9)`, glow
`0 0 26px rgba(231,140,60,.24)`. A 12px `#ffc45a` diamond pulsing 1.2s, then text
Chakra Petch 600 15px, `.18em`, `#ffd9a2`:

- single-target: `CHOOSE A TARGET`
- group-target: `ALL ENEMIES — CONFIRM ON ANY PLATE`

## Interactions & Behavior

### Navigation graph

```
root ──ATTACK──────────────────────────────► target   (branch = attack)
root ──SKILL / ITEM──► sub ──pick entry────► target
root ──RUN / HOLD BACK────────────────────► root  (resolves immediately, sets bark)

target ──click enemy plate──► root  (command resolves, sets bark)
target ──BACK/ESC──► sub, or root if branch was ATTACK
sub    ──BACK/ESC──► root
```

Key rules:

1. **Deepest path is three clicks, shallowest is one.** ATTACK skips depth 2; RUN and
   HOLD BACK never descend.
2. **The parent column stays on screen** while a child is open — nothing is replaced,
   only added to the right. The breadcrumb names the depth.
3. **BACK exists at every depth** and is bound to ESC.
4. **Targeting reuses the enemy plates** rather than opening a fourth column, keeping
   the player's eye on the battlefield.
5. While the submenu is open at target depth it **stays visible** with the chosen row
   still in its selected state — the player can see what they picked while aiming.

### Hover behaviour

- Hovering a submenu row sets it as the detail-panel subject *and* fires the MP cost
  preview (MP-costing skills only). Selection-by-hover is intentional: it makes the
  detail column feel instant. Confirm-by-click is a separate step.
- Hovering an enemy plate during targeting scales it 1.03 and brightens border + reticle.
- All buttons in the project already scale to 105% on hover / 95% on press — the
  prototype uses subtler `scale(1.02)`/`1.03` values for rows because the rows are wide
  and 105% would visibly shift text. Judge per element; keep 105/95 for the root verbs.

### Animations

| What | Property | Duration / easing |
|---|---|---|
| Column open (B and C) | opacity 0→1, translateX -10px→0 | 140ms ease-out |
| Row hover scale | transform | 100ms ease |
| Plate hover scale | transform | 120ms ease |
| `NOW` label, target diamond | opacity .5 ↔ 1 | 1.8s / 1.2s ease-in-out, infinite |
| Target reticle | opacity .55↔1, scale 1↔1.05 | 1.6s ease-in-out, infinite |

Where uGUI can't tween, a short frame sequence works — the project already plays frame
sequences from a folder for spells.

### Keyboard / gamepad

Up/down moves within the active column · right or Enter descends · Esc or B ascends ·
number keys 1–5 jump straight to a root verb. The three visual states (idle / hovered
/ selected) are shared between mouse, keyboard focus and gamepad focus — do not invent
a fourth "focused" look.

## State Management

```
depth       : "root" | "sub" | "target"
branch      : null | "attack" | "skill" | "item"
sel         : null | <skill|item|strike entry>   // subject of the detail panel
hoverEnemy  : null | enemyId
mpPreview   : number                              // MP the hovered skill would spend
bark        : string                              // dialogue bar text
```

Transitions and the bark string each sets:

| Trigger | Result | Bark |
|---|---|---|
| click ATTACK | depth=target, branch=attack, sel=Strike | `Pick your mark.` |
| click SKILL | depth=sub, branch=skill, sel=null, mpPreview=0 | `Which skill?` |
| click ITEM | depth=sub, branch=item, sel=null, mpPreview=0 | `Rummaging in the satchel.` |
| click RUN | depth=root, branch=null | `You cannot outrun a bog.` |
| click HOLD BACK | depth=root, branch=null | `Sheep braces and waits.` |
| hover submenu row | sel=row, mpPreview=cost if MP | — |
| click affordable row | depth=target, sel=row | `Pick your mark.` / `It will catch all of them.` if group |
| click unaffordable row | no depth change | `Not enough for that.` |
| click enemy plate (targeting) | reset to root, clear sel/hover/preview | `<Ability> → <Enemy>.` |
| BACK from target | sub, or root if branch=attack | previous depth's bark |
| BACK from sub | root | `Oh, that's a big one.` |

Idle bark: `Oh, that's a big one.`

Group-vs-single is derived from the entry's third stat containing `ALL` — in the real
implementation use an explicit `targetMode` enum (`SELF | ONE_ALLY | ONE_ENEMY |
ALL_ENEMIES | ALL`) rather than string matching.

Data needed per turn: acting character (HP/MP/wool/level/class/portrait), turn order
list, enemy list (name/hp/max/status tags), unlocked skills with costs, item counts.

## Design Tokens

**Type** — Chakra Petch throughout (the project's existing UI font).

| Role | Spec |
|---|---|
| Verb, primary | 600 19px, letter-spacing .16em |
| Verb, secondary | 600 17px, .16em |
| Row name | 600 18px, .03em |
| Panel name | 600 20–24px, .04–.06em |
| Bark | 400 22px / 1.3 |
| Body copy | 400 14px / 1.55 |
| Section label | 600 11–13px, .2–.28em (always uppercase) |
| Meta / tag | 500 10–12px, .14–.2em |
| Value / cost | 600 14–15px, .1em |
| Hotkey | ui-monospace 600 12px |

**Neutrals**

| Token | Hex |
|---|---|
| Void (canvas floor) | `#07040c` |
| Panel dark A | `rgba(18,10,26,.82)` |
| Panel dark B | `rgba(12,7,18,.9)` |
| Panel violet fill | `linear-gradient(100deg, rgba(30,18,42,.9), rgba(15,8,22,.92))` |
| Text primary | `#f4ebff` |
| Text secondary | `#bfb0d4` |
| Text muted | `#8a7aa0` |
| Text disabled | `#7f6f98` |
| Hairline | `rgba(200,170,230,.22)` |

**Accents**

| Token | Hex | Use |
|---|---|---|
| Gold light | `#ffe0a8` | active borders, NOW label |
| Gold | `#e7b25c` | primary verb, party frame, section labels |
| Gold text | `#ffd9a2` | primary verb label, target prompt |
| Gold deep | `#8d5716` | gradient ends |
| Target amber | `#ffc45a` | reticle, hovered target |
| HP red | `#e07a62` → `#8e3226` | HP bars, physical tint |
| MP blue | `#7ea8e6` → `#3a5a9a` | MP bar, shock tint |
| Violet | `#b48cff` | support tint, upcoming-turn emphasis |
| Teal | `#7ce0d6` | frost tint |
| Bog green | `#a8c860` | thrown-item tint |
| Magenta | `#f08ad0` | reserved (elite/relic, used on sibling screens) |

**Geometry:** border radius 4 on panels, 0 on rows and bars (deliberately sharp —
squares are the house style across this game's screens). Border width 1 everywhere
except active/primary frames at 2. Gaps: 8 (submenu rows), 10 (root rows), 12 (enemy
plates), 14–18 (intra-row), 22–26 (panel padding). Panel padding `20–22px` vertical,
`22–26px` horizontal.

**Sprite budget (as designed):** four panel sprites — `panel_gold` (party plate,
primary verb), `panel_violet` (submenu, detail), `panel_red` (enemy plate),
`row_hover` — plus two chip frames and one reticle. Everything else is border tint and
text colour on those sprites.

## Assets

Nothing final ships in this bundle. Placeholders in the prototype, each to be replaced
by real art at the stated rect:

| Placeholder | Rect | Becomes |
|---|---|---|
| `HERO SPRITE` | 230 × 300 at left 340 / bottom 280 | existing hero sprite |
| `ENEMY GROUP SPRITES` | 400 × 320 at right 300 / bottom 270 | existing enemy sprites |
| `BATTLE BACKGROUND` | full canvas | new background (user is replacing it) |
| Portrait, party plate | 64 × 64 | character portrait |
| Portrait, bark | 56 × 56 | speaker portrait |
| Chip fill, timeline | 78² / 64² | turn portraits |
| Row mark, submenu | 36 × 36 | ability / item icon |

Icons are 45° stripe fills in the prototype so a missing icon is obviously missing
rather than silently blank.

## Files

| File | What it is |
|---|---|
| `Battle UI.dc.html` | The design prototype. Open in a browser; fully interactive. Authoritative for layout and behaviour. |
| `support.js` | Runtime the prototype needs. Must sit beside the HTML. Not part of the design. |
| `reference_current_battle_ui.png` | The screen being replaced, for before/after comparison. |

Reading the prototype source: the markup between `<x-dc>` and `</x-dc>` is the layout
with inline styles (every value in this README appears there literally); the
`class Component` block below it holds the skill/item/enemy data tables and the
navigation state machine described above.

## What this fixes, for context

Worth knowing so the intent survives implementation:

1. Floating debug text (`> Sheep: HP…`, `Health Potions: 2`, enemy HP printed twice)
   is gone. Party state lives in one framed plate; item counts live inside the satchel
   where they are relevant.
2. The old skill row sat *above* the verb row and overlapped the hero's HP readout.
   Submenus now open sideways, so nothing covers the battlefield centre or the sprites.
3. Five equal-weight gold buttons gave no hierarchy. Read order now matches use
   frequency.
4. Enemy HP is a plate per enemy with a bar and status tags, and it doubles as the
   target picker instead of being static text.
5. Wool reads as 16 pips — a charge meter you can see fill without reading a fraction.
