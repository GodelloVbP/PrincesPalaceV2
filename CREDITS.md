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

### What this obliges

CC BY 3.0 requires attribution wherever the work is distributed. A build of this
game must therefore surface the credit above somewhere a player can reach it —
a credits screen, an about panel, or a bundled text file. **That surface does not
exist yet**, so shipping a build today would not satisfy the licence. Recorded
here rather than left to be discovered at release.
