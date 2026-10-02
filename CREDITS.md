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

`Assets/_Project/Resources/Status/*.png` (the sixteen
`StatusEffectType` members' icons, the two speed presentations, `speed.png` and
`speed_down.png`, and the two engine-window badges `silenced.png` and
`disarmed.png`) were generated for this project from
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

---

## UI sound effects — JDSherbert

`Assets/_Project/Resources/Audio/Sfx/`

| File | Source clip |
|---|---|
| `menu_close.wav` | `Menu_close.wav` |
| `click_fail.wav` | `Click fail.mp3`, trimmed to 0.35s |
| `open_the_pack.wav` | `Open_the_pack.wav` |
| `swipe_1.wav` | `Swipe 1.mp3` |
| `swipe_2.wav` | `Swipe 2.mp3` |

Audio by **JDSherbert**. Licensed for commercial use with credit required; the
licence text is `LICENSE.pdf` in the source pack, kept outside the repo. The
files are converted to 44.1 kHz 16-bit wav, with the one trim noted above. Like
the icons above, this credit has no in-game surface yet: there is no credits
screen, so this file is where it currently lives.

---

## Spell sound effects -- rubberduck

`Assets/_Project/Resources/Audio/Sfx/`

| File | Source clip |
|---|---|
| `velvet_shackles_impact.wav` | `chain_01`, trimmed to 0.39s |
| `dark_chains_impact.wav` | `chain_03`, trimmed to 0.66s |
| `boulder_slam_impact.wav` | `item_stone_01`, 0.105s lead-in and tail cut to 0.385s |
| `trunk_slam_impact.wav` | `wood_03`, trimmed to 0.20s |
| `bellwether_scratch_impact.wav` | `blade_02`, trimmed to 0.254s |

"80 CC0 RPG SFX" by **rubberduck**, from OpenGameArt. CC0, so no credit is
required; it is given as a courtesy. Converted to 44.1 kHz 16-bit mono wav with
a 40ms fade-out on each trim.

---

## Spell sound effects -- Leonardo Calvo

`Assets/_Project/Resources/Audio/Sfx/`

"Magic Spell SFX" by **Leonardo Calvo**, <https://thesoundrack.itch.io/freemagicspellsfx>.
Licensed under **Creative Commons BY 4.0** -- <https://creativecommons.org/licenses/by/4.0/>.
The clips were trimmed, faded and resampled to 44.1 kHz 16-bit mono wav.

| File | Source clip |
|---|---|
| `winters_rebuke_impact.wav` | Ice `fast_impact_07` |
| `vipers_bite_impact.wav` | Acid `squish_whoosh_impact_04` |
| `spore_cloud_impact.wav` | Acid `poison_gas_02` |
| `ashen_reckoning_impact.wav` | Fire `burst_impact_short_03` |
| `censer_of_embers_impact.wav` | Fire `fireball_burst_warm_05` |
| `blackglass_spear_impact.wav` | Dark `raven_metallic_02` |
| `death_knell_impact.wav` | Dark `charge_impact_03` |
| `court_of_whispers_impact.wav` | Dark `curse_03` |
| `gale_scythe_cast.wav`, `gale_scythe_impact.wav` | Wind `short_rise_whoosh_02`, `air_sharp_shoot_02` |
| `thorn_tithe_impact.wav` | Earth `morph_rocks_02` |
| `crownfall_impact.wav` | Lightning `zap_blast_sparkle_bright_02` |
| `borrowed_moment_impact.wav` | Wind `short_spacious_whoosh_04` |
| `palace_passage_impact.wav` | Dark `warp_03` |
| `cinderfault_eruption_impact.wav` | Earth `impact_ground_rock_debris_02` |
| `prismatic_orb_earth_impact.wav` | Earth `stone_dry_impact_03` |
| `prismatic_orb_fire_impact.wav` | Fire `fireball_burst_bright_03` |
| `prismatic_orb_wind_impact.wav` | Wind `air_impact_splash_02` |
| `prismatic_orb_water_cast.wav`, `prismatic_orb_water_impact.wav` | Water `bubble_power_02`, `impact_ball_03` |
| `velvet_shackles_cast.wav` | Dark `cast_energy_riser_metallic_01` |
| `gilded_aegis_cast.wav`, `gilded_aegis_impact.wav` | Holy Spell Pack `Wave Cast 04`, `Cast Blast 04` (96 kHz) |
| `status_ward_1..3.wav` | Holy Spell Pack `Protect Cast 01..03` (96 kHz) |
| `status_regen_1..3.wav` | Holy Spell Pack `Heal Cast Bright 01..03` (96 kHz) |

The `gilded_aegis_` and `status_` rows are from Leonardo Calvo's Holy Spell Pack (<https://www.leonardocalvocomposer.com>).
The pack ships no licence text, only `Contact.txt`; it is credited here on the assumption that it
carries the same CC BY 4.0 terms as the Magic Spell SFX above. Check the pack's download page.
