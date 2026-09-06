# Credits — third-party assets

Everything in this file is art or audio the project did not draw itself, with
the licence it ships under. Kept at the repo root rather than buried in `docs/`
because an attribution nobody can find is an attribution that is not being made.

**If you add a third-party asset, add it here in the same commit.** A licence
obligation discovered later is a licence obligation already breached.

---

## Enemy intent icons — game-icons.net

`Assets/_Project/Resources/Intent/*.png`

| File | Original icon | Artist |
|---|---|---|
| `attack.png` | crossed-swords | Lorc |
| `poison.png` | poison | sbed |
| `stun.png` | stoned-skull | Lorc |
| `weaken.png` | health-decrease | sbed |
| `heal.png` | health-increase | sbed |
| `shield.png` | shield | sbed |
| `skill.png` | magic-swirl | Lorc |

Icons made by **Lorc** and **sbed**, available at <https://game-icons.net>.

Licensed under **Creative Commons BY 3.0** — <https://creativecommons.org/licenses/by/3.0/>.

Downloaded as white-on-transparent PNG, downscaled to 128x128 and tinted at
runtime from `FightHudPalette`. Chosen as silhouettes rather than painted
illustrations for a specific reason recorded in `EnemyIntentIcons`: the badge
is ~46px over a painted battlefield, where a full-colour illustration reads as
mud and cannot be recoloured to carry state.

### `summon.png` is not one of them

`Assets/_Project/Resources/Intent/summon.png` is drawn by this project, by
`tools/art/make_summon_icon.py`, and carries no third-party licence. game-icons
has nothing that reads as "another monster is joining" at badge size, and the
nearest candidates were all rings — which is what the existing `skill.png`
already is once it downsamples. The script's own header records why it ended up
a bare pentagram rather than the summoning circle it started as.

Regenerate it with `python tools/art/make_summon_icon.py` from the project root.
It is committed as a PNG like every other icon; the script is kept so the shape
can be retuned rather than redrawn by hand.

### Status badge icons — also not third-party

`Assets/_Project/Resources/Status/*.png` (all fourteen: the twelve
`StatusEffectType` members plus the two speed presentations, `speed.png` and
`speed_down.png`) were generated for this project from
`docs/STATUS_ICON_PROMPTS.md`, the frozen art contract that fixed each
glyph's composition, palette and size ahead of generation, and carry no
third-party licence. These are party STATUS badges, not enemy-intent badges,
hence the separate `Status/` folder rather than another row in the table
above -- see `FightHudModel.StatusBadgeIcons`.

Chilled and Rooted previously shipped as script-drawn placeholders from
`tools/art/make_status_icons.py` (white silhouette, 128x128, 4x supersample +
LANCZOS, same template as `summon.png` above); both were superseded by the
generated set above and the script was removed
(PLAN_STATUS_EFFECT_UI.md phase 2) once nothing but this file still
referenced it.

### What this obliges

CC BY 3.0 requires attribution wherever the work is distributed. A build of this
game must therefore surface the credit above somewhere a player can reach it —
a credits screen, an about panel, or a bundled text file. **That surface does not
exist yet**, so shipping a build today would not satisfy the licence. Recorded
here rather than left to be discovered at release.
