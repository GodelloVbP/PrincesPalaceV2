using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // Painting. Everything here reads a model and writes a component, and
    // nothing here decides anything.
    //
    // That is the whole division: FightHudModel works out what a row SAYS and
    // whether it is affordable, FightMenuState works out what is open, and this
    // file turns those answers into text and colour. v1's equivalent did both at
    // once across 698 lines, which is why "is the column open" got asked twice
    // and answered differently.
    public partial class FightController
    {
        // The palette, parsed once. Domain speaks hex because it cannot name a
        // Color; parsing per repaint would be a string parse per label per
        // frame for no reason.
        private static readonly Color CostAffordable = Hex(FightHudPalette.GoldLight);
        private static readonly Color CostUnaffordable = Hex(FightHudPalette.TextDisabled);
        private static readonly Color PipFilled = Hex(FightHudPalette.PipFilled);
        private static readonly Color PipEmpty = Hex(FightHudPalette.PipEmpty);
        private static readonly Color CaretActive = Hex(FightHudPalette.GoldLight);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.magenta;

        // Dimmed rather than removed. Alpha on the two text lines only -- the
        // row's own frame keeps its alpha so an unaffordable entry still reads
        // as a row rather than as a gap in the list.
        private const float DimmedAlpha = 0.45f;

        // ---- the whole screen ----------------------------------------------

        public void RefreshUi()
        {
            // Drained HERE, before anything paints.
            //
            // A refusal ("No items to use", "out of reach behind the front
            // rank") is appended by a click that resolves NOTHING, so it never
            // reaches AfterResolution and never gets a beat to ride on. Without
            // this the session would hold those lines forever and the player
            // would click a verb and watch nothing happen -- which is precisely
            // the failure a refusal exists to prevent.
            if (_session != null)
            {
                foreach (var line in _session.DrainImmediateMessages()) PushLogLine(line);
            }

            RefreshMenuChrome();
            RefreshEnemyPlates();
            RefreshPcPlates();
            RefreshInitiative();
            RefreshStage();
            RefreshLowHpVignette();

            // AFTER both status surfaces have repainted (RefreshStage's
            // enemy row and RefreshPcPlates' three plate rows) -- see
            // RefreshHoveredStatusTooltip's own comment for why this cannot
            // just live inside one of them.
            RefreshHoveredStatusTooltip();
        }

        // HYSTERESIS, not one threshold -- a party sitting right at 25% would
        // otherwise flicker the vignette on and off every regen tick and every
        // poison tick, which reads as a broken effect rather than a danger
        // signal. Activates below 25%, clears only above 30%: the 5-point gap
        // is the whole fix, and it is why this needs a field at all rather
        // than a pure function of the current snapshot.
        private bool _lowHpActive;
        private const float LowHpActivate = 0.25f;
        private const float LowHpClear = 0.30f;

        private void RefreshLowHpVignette()
        {
            if (lowHpVignette == null || _session == null) return;

            float lowest = 1f;
            bool any = false;
            foreach (var member in _session.Encounter.LivingPlayerParty)
            {
                if (member.MaxHealth <= 0) continue;
                any = true;
                float fraction = member.CurrentHealth / (float)member.MaxHealth;
                if (fraction < lowest) lowest = fraction;
            }

            if (!any)
            {
                _lowHpActive = false;
            }
            else if (_lowHpActive)
            {
                // Already showing: only a recovery past the CLEAR line turns
                // it off, not merely past the lower ACTIVATE one.
                if (lowest >= LowHpClear) _lowHpActive = false;
            }
            else if (lowest < LowHpActivate)
            {
                _lowHpActive = true;
            }

            lowHpVignette.SetShown(_lowHpActive);
        }

        // Just the menu-derived panels -- verbs, submenu, detail card, target
        // prompt -- none of which read anything FightBeatPlayer paces. Split
        // out so AfterResolution can hide them the instant the player commits,
        // without touching the plates/stage/initiative a beat hasn't painted
        // yet. See AfterResolution's own comment for why that distinction
        // matters.
        private void RefreshMenuChrome()
        {
            // CLEARED HERE, the one seam both repaint entry points share --
            // RefreshUi calls this first thing, and AfterResolution calls it
            // directly (see AfterResolution's own comment on why it skips
            // the rest of RefreshUi). Everything below that asks the session
            // for the acting character's skill options -- RefreshSubmenu,
            // RefreshDetail and RefreshTargetPrompt here, plus
            // RefreshEnemyPlates and RefreshInitiative later in the same
            // RefreshUi pass -- goes through SkillOptions() instead of
            // FightSession.SkillOptionsFor directly, so one dropped-menu
            // click that used to recompute the same filtered, allocated list
            // five to eight times now computes it once and everyone downstream
            // of this method reads that. Clearing here rather than caching it
            // for longer is what keeps it from ever surviving into a repaint
            // where the turn, cooldowns or mana have moved on: nothing
            // between one RefreshMenuChrome and the next is allowed to leave
            // the memo standing.
            _skillOptionsMemoActor = null;
            _skillOptionsMemo = null;

            RefreshVerbs();
            RefreshSubmenu();
            RefreshDetail();
            RefreshTargetPrompt();
        }

        // The one place FightSession.SkillOptionsFor is actually called from
        // this file. Every other call site below reads through here so a
        // single RefreshMenuChrome pass pays for the list once regardless of
        // how many of RefreshSubmenu/RefreshDetail/RefreshTargetPrompt/
        // RefreshEnemyPlates/RefreshInitiative ask for it. Keyed on actor
        // reference rather than unconditionally trusted, because
        // ActingCharacter() can hand back a different combatant than
        // _session.Current (see its own comment) and a memo answering for
        // the wrong actor is worse than no memo at all.
        private CombatantState _skillOptionsMemoActor;
        private IReadOnlyList<ResolvedSkillOption> _skillOptionsMemo;

        private IReadOnlyList<ResolvedSkillOption> SkillOptions(CombatantState actor)
        {
            if (_session == null || actor == null) return Array.Empty<ResolvedSkillOption>();

            if (_skillOptionsMemo == null || !ReferenceEquals(_skillOptionsMemoActor, actor))
            {
                _skillOptionsMemoActor = actor;
                _skillOptionsMemo = _session.SkillOptionsFor(actor);
            }

            return _skillOptionsMemo;
        }

        private void RefreshVerbs()
        {
            if (verbButtons == null) return;

            // HIDDEN DURING THE FIGHT PHASE, same reason the detail card,
            // submenu and target prompt already are (see AfterResolution's own
            // comment) -- ATTACK/SKILL/ITEM/HOLD BACK describe choices the
            // player is not currently making while a beat is animating, and
            // leaving them up read as though the menu were still live.
            // RefreshVerbs runs from RefreshMenuChrome, which AfterResolution
            // already calls the instant _isBusy goes true, so this needs no
            // new call site -- just an additional reason to hide, alongside
            // the ones RefreshMenuChrome's other three calls already act on.
            bool visible = !_isBusy;
            for (int i = 0; i < verbButtons.Length; i++)
            {
                if (verbButtons[i] != null) verbButtons[i].gameObject.SetShown(visible);
            }

            if (!visible) return;

            int active = _menu.ActiveVerbIndex;
            for (int i = 0; i < verbButtons.Length; i++)
            {
                // Gamepad focus shares the branch-open tint rather than
                // growing a fourth colour -- the two are mutually
                // exclusive in practice (focus only means anything while
                // nothing is open yet), so one look serves both "this is
                // what Submit presses" and "this is what is open".
                bool highlighted = i == active || (active < 0 && i == _focusedVerb);

                // THEMED VERBS drive ThemedButtonState.SetMenuState (through
                // ApplySelection) instead of targetGraphic.color -- Open/
                // Primary/Idle is the same three-way distinction this method
                // has always made, just painted on Glow/Plate instead of a
                // flat Image tint. Every verb is themed today, so there is no
                // unthemed fallback left to keep.
                var verbState = highlighted ? ThemedMenuState.Open
                    : i == 0 ? ThemedMenuState.Primary
                    : ThemedMenuState.Idle;
                ThemedButtonState.ApplySelection(verbButtons[i], verbState != ThemedMenuState.Idle, verbState);

                // Only the two nesting verbs have a live caret; the others were
                // built with theirs deactivated and it stays that way.
                if (verbCarets != null && i < verbCarets.Length && verbCarets[i] != null)
                {
                    var caret = verbCarets[i].GetComponent<TMPro.TMP_Text>();
                    if (caret != null) caret.color = i == active ? CaretActive : CostUnaffordable;
                }
            }

            // NOTHING IS OVERWRITTEN ON A VERB LABEL ANY MORE. The fourth
            // row used to have its banked-action count written in here every
            // repaint, because Hold Back built a resource nothing else showed.
            // Move builds none: what it costs (the turn) and what it can do
            // (which directions are open) are both on its own submenu rows,
            // so the label is exactly what the screen tree authored.
            breadcrumb.SetContent(FightHudModel.Breadcrumb(_menu));
        }

        // Column B, for whichever branch is open.
        private void RefreshSubmenu()
        {
            submenuColumn.SetShown(_menu.SubmenuOpen);
            if (!_menu.SubmenuOpen || submenuRows == null)
            {
                // The scroll belongs to the list that is open. Closing the
                // column is what makes the next open start at the top again --
                // see ForgetSubmenuScroll.
                ForgetSubmenuScroll();
                return;
            }

            var rows = CurrentRows();

            // ELEMENT DEPTH TITLES ITSELF, ahead of the branch: the column is
            // still the Skill branch's, but what is in it is a list of
            // elements, and a header reading SKILLS over four element names is
            // the header describing the depth the player just left.
            submenuTitle.Set(SubmenuTitleFor(CurrentSubmenuKind()));
            // The truncation is reported HERE, beside the list it truncates,
            // rather than into the combat log. It used to AppendMessage on every
            // refresh, so the bark spent the fight repeating "9 more entr(y/ies)
            // than the column can show" over whatever the fight was actually
            // saying. The warning was right and the channel was wrong: a fact
            // about the menu does not belong in the narration of the battle, and
            // a line re-sent every frame stops being read at all.
            int hidden = rows.Count - submenuRows.Length;
            if (hidden > 0)
            {
                submenuHint.SetContent($"SHOWING {submenuRows.Length} OF {rows.Count}");
            }
            else
            {
                submenuHint.Set(UiStrings.SubmenuHint);
            }

            // Through the SAME layout function the build used, which is the
            // whole reason FightSubmenuLayout exists in Domain.
            AnchorSubmenuRows(rows.Count);

            for (int i = 0; i < submenuRows.Length; i++)
            {
                if (i >= rows.Count) continue;

                var row = rows[i];
                submenuNames[i].SetContent(row.Name);

                // interactable BEFORE SetMenuState, not after: SetMenuState
                // ends in Refresh(), which reads Button.interactable RIGHT
                // THEN to pick DisabledTint vs the state's own plate tint
                // (ThemedButtonState.Refresh/MenuStatePlateTint). Setting it
                // afterward means a skill that just became unaffordable
                // still paints this repaint's plate off the STALE
                // interactable flag from before -- one frame of a plate
                // that reads "castable" for a row `row.Affordable` already
                // says is not.
                submenuRows[i].interactable = row.Affordable;

                // THEMED ROWS drive ThemedButtonState.SetMenuState (through
                // ApplySelection) instead of targetGraphic.color, same split
                // RefreshVerbs already makes for the verb column -- Open for
                // the selected row, Idle for every other one. Called AFTER
                // the interactable write above, per ApplySelection's own
                // ordering contract.
                // RowSelection, not Selection: at Element depth these rows are
                // the element list and Selection is still pointing at the skill
                // row that opened it -- see FightMenuState's own note on why
                // the two indices are kept apart.
                ThemedButtonState.ApplySelection(submenuRows[i], i == _menu.RowSelection);

                // The row's whole state, in one channel. It used to carry the
                // name at full strength, the meta dimmed and the cost
                // recoloured; there is one thing on the row now, so dimming it
                // is the entire vocabulary a name-only list has. What the
                // player cannot afford is still hovered, and hovering is what
                // fills the detail column with the cost they are short of.
                SetAlpha(submenuNames[i], row.Affordable ? 1f : DimmedAlpha);
            }

            // Still never a silent truncation -- the count moved to the hint
            // above, where it sits beside the list it is about.
        }

        // Column C. Describes whatever the submenu has selected, or the
        // synthetic Strike entry when the branch is ATTACK.
        private void RefreshDetail()
        {
            detailColumn.SetShown(_menu.DetailOpen);
            if (!_menu.DetailOpen) return;

            var panel = CurrentDetail();
            detailName.SetContent(panel.Name);
            detailKind.SetContent(panel.Kind);
            detailBody.SetContent(panel.Body);

            for (int i = 0; i < detailStatValues.Length; i++)
            {
                bool has = i < panel.Stats.Count;
                detailStatValues[i].SetContent(has ? panel.Stats[i].Value : "");

                // The key goes with it. A stat row is a pair, and half a pair is
                // a label pointing at nothing.
                if (detailStatKeys != null && i < detailStatKeys.Length && detailStatKeys[i] != null)
                {
                    detailStatKeys[i].gameObject.SetShown(has);
                }
            }

            RefreshDetailDamageType(panel.DamageType);
        }

        // "Fire", "Poison", a "/"-joined "Fire/Ice" or "" -- see FightHudModel.
        // DamageTypeLabel's own header for where the string comes from. Only
        // the FIRST element of a joined list gets a colour; CombatBeat (and
        // so this card) has no way to show two colours on one word, and the
        // label itself still names every element the skill authors.
        private void RefreshDetailDamageType(string label)
        {
            if (detailDamageType == null) return;

            bool has = !string.IsNullOrEmpty(label);
            detailDamageType.gameObject.SetShown(has);
            if (!has) return;

            detailDamageType.SetContent(label.ToUpperInvariant());

            string firstType = label.Split('/')[0];
            var color = System.Enum.TryParse(firstType, out DamageType parsed)
                ? Hex(FightHudPalette.ForDamageType(parsed))
                : Hex(FightHudPalette.TextMuted);
            detailDamageType.color = color;
        }

        private void RefreshTargetPrompt()
        {
            bool targeting = _menu.IsTargeting;
            targetPrompt.SetShown(targeting);
            if (!targeting) return;

            string name = CurrentDetail().Name;
            if (TargetingIsGroup()) targetPromptLabel.Set(UiStrings.TargetPromptGroup, name);
            else targetPromptLabel.Set(UiStrings.TargetPrompt, name);
        }

        // Whether the skill resolving at Target depth hits every enemy at
        // once, for the prompt's wording. Party never reaches Target depth
        // at all (see OnRowPressed), so "not SingleEnemy" here can only mean
        // AllEnemies -- and the basic spell, which carries no authored
        // Targeting of its own, is always single-target.
        private bool TargetingIsGroup()
        {
            if (_menu.Branch != MenuBranch.Skill || _session?.Current == null) return false;

            var options = SkillOptions(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return false;

            return options[row].Skill.Targeting == Domain.Combat.SkillTargeting.AllEnemies;
        }

        // THE REACH the click resolving at Target depth would carry. Attack
        // is always the plain Strike (Reach.Melee); a skill carries its own,
        // which is Reach.Any for everything ranged or magical. Mirrors the
        // same lookup OnEnemyPressed uses to gate the click itself -- this is
        // the half that paints the same answer onto the plate, not a second
        // rule.
        private Domain.Combat.Reach TargetingReach()
        {
            if (_menu.Branch == MenuBranch.Attack) return Domain.Combat.Reach.Melee;
            if (_menu.Branch != MenuBranch.Skill || _session?.Current == null)
            {
                return Domain.Combat.Reach.Any;
            }

            var options = SkillOptions(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return Domain.Combat.Reach.Any;

            return options[row].Skill.Reach;
        }

        // Alpha rather than a colour swap -- the same "dim, don't hide"
        // choice the submenu's own unaffordable rows already make (see
        // DimmedAlpha there). A blocked plate is still information; it is
        // just not this click's business right now.
        private const float OutOfReachAlpha = 0.5f;

        // Elite/boss dressing -- a tint on the existing plate and name,
        // never a new node. The plate carries three text rows in 64px
        // already (see PlateW/PlateH's own header); a "BOSS"/"ELITE" tag
        // squeezed in beside an 86px-wide name box would either clip a
        // longer name or need the box widened, and neither is a colour
        // change's business. Read straight off _encounterClass -- see its
        // own comment for why that is enough without a per-combatant flag.
        private static readonly Color EliteBossPlateTint = Hex(FightHudPalette.BorderGold);
        private static readonly Color EliteBossNameTint = Hex(FightHudPalette.GoldText);
        private static readonly Color EnemyNameNormal = Hex(FightHudPalette.EnemyName);

        private void RefreshEnemyPlates()
        {
            if (enemyPlates == null) return;

            var enemies = Enemies;
            enemiesHint.Set(UiStrings.StandingCount,
                _session == null ? 0 : FightHudModel.StandingCount(_session.Encounter));

            // OVER THE WHOLE LIST INCLUDING THE DEAD, so a rat keeps its number
            // when the rat beside it falls. See FightHudModel.DisplayNames.
            var names = FightHudModel.DisplayNames(enemies);

            // Computed ONCE per refresh, not once per plate -- the same
            // question asked of the same click for every plate in the loop.
            var targetingReach = TargetingReach();

            // An elite room fields a squad drawn entirely from the elite pool,
            // so every living plate reads it; a boss room fields exactly the
            // one declared boss, so only the front slot does -- see
            // _encounterClass's own comment.
            var frontEnemy = _session?.Encounter.FrontEnemy;

            for (int i = 0; i < enemyPlates.Length; i++)
            {
                // IsOnStage, not just IsAlive -- the exact "spawns before the
                // roar" bug StageVisuals' own _confirmedPresent comment
                // describes, on the plate instead of the sprite. A round
                // resolves in full before a single beat plays, so a summon
                // exists in Encounter.Enemies from the top of the round; left
                // ungated, its plate appeared several beats before the roar
                // that called it in, while the stage sprite (already gated)
                // correctly waited. Same rule, same source of truth, so the
                // two cannot disagree about when something has arrived.
                bool present = i < enemies.Count && enemies[i].IsAlive && IsOnStage(enemies[i]);
                enemyPlates[i].SetShown(present);

                // BLOCKED: reachable by the click OnEnemyPressed will actually
                // accept, but not by THIS one. Dimmed, reticle greyed, and not
                // interactable -- the plate stops being a button rather than
                // staying one that silently refuses, which is the visual half
                // of the fix Phase 1 already made to the click itself.
                bool blocked = present && _menu.IsTargeting && _session != null
                    && !_session.CanReach(_session.Current, targetingReach, enemies[i]);

                bool dressed = present && (
                    _encounterClass == EncounterClass.Elite
                    || (_encounterClass == EncounterClass.Boss && ReferenceEquals(enemies[i], frontEnemy)));

                if (present)
                {
                    // enemyPlateFrames[i], not enemyPlates[i].targetGraphic:
                    // the button is NoChrome now (owner's HQ-kit instruction,
                    // 2026-09-07), so its own Image is a permanently
                    // transparent click target -- the Crimson container
                    // frame under it is what the player actually sees, and
                    // is what the elite/boss dress and the out-of-reach dim
                    // have to tint instead.
                    if (Has(enemyPlateFrames, i))
                    {
                        var colour = dressed ? EliteBossPlateTint : Color.white;
                        colour.a = blocked ? OutOfReachAlpha : 1f;
                        enemyPlateFrames[i].color = colour;
                    }
                    enemyPlates[i].interactable = !blocked;

                    if (Has(enemyPlateNames, i))
                    {
                        enemyPlateNames[i].color = dressed ? EliteBossNameTint : EnemyNameNormal;
                    }
                }

                enemyPlateReticles[i].SetShown(present && _menu.IsTargeting);
                if (Has(enemyPlateReticles, i) && present)
                {
                    var reticleImage = enemyPlateReticles[i].GetComponent<Image>();
                    if (reticleImage != null)
                    {
                        var colour = reticleImage.color;
                        colour.a = blocked ? OutOfReachAlpha : 1f;
                        reticleImage.color = colour;
                    }
                }

                // THE FIGURE IS A TARGET ONLY WHILE ONE IS BEING CHOSEN. Left
                // live it is a rectangle over the battlefield eating clicks
                // meant for whatever is behind it.
                if (Has(enemyHitAreas, i))
                {
                    enemyHitAreas[i].gameObject.SetShown(present && _menu.IsTargeting);
                }

                if (!present) continue;

                var enemy = enemies[i];
                enemyPlateNames[i].SetContent(i < names.Count ? names[i] : enemy.Name);
                enemyPlateHps[i].Set(UiStrings.HealthValue, enemy.CurrentHealth, enemy.MaxHealth);
                SetFill(enemyPlateHpFills[i], enemy.CurrentHealth, enemy.MaxHealth);
                enemyPlateTags[i].SetContent(FightHudModel.EnemyStatusLine(enemy, _session));

                // Only an elite or boss carries a BreakShield at all -- the
                // track stays hidden for everything else rather than showing
                // an always-full meter nobody can deplete.
                if (Has(enemyPlateBreakTracks, i))
                {
                    var shield = enemy.BreakShield;
                    enemyPlateBreakTracks[i].SetShown(shield != null);
                    if (shield != null) SetFill(enemyPlateBreakFills[i], shield.Current, shield.Max);
                }

                // The actor's own idle art, fitted into the plate. Reusing the
                // stage sprite rather than authoring plate icons is what makes
                // the two impossible to disagree.
                if (Has(enemyPlateIcons, i))
                {
                    var art = StanceSpriteFor(enemy, FightSession.Stances.Idle);
                    enemyPlateIcons[i].gameObject.SetShown(art != null);
                    if (art != null)
                    {
                        enemyPlateIcons[i].sprite = art;
                        enemyPlateIcons[i].preserveAspect = true;
                    }
                }
            }
        }

        private static bool Has<T>(T[] array, int index) where T : UnityEngine.Object =>
            array != null && index >= 0 && index < array.Length && array[index] != null;

        // ---- the HUD column: three PC plates ---------------------------------
        //
        // ONE LOOP, ONE PLATE AT A TIME, and the only thing that varies
        // between iterations is whether this plate's occupant is the one
        // acting. That is the whole point of the 2026-09-10 model change:
        // there used to be RefreshPartyPlate (the acting member, on a taller
        // card with its own node names) and RefreshRoster (everybody else, on
        // smaller ones), which meant every fact about a party member had two
        // implementations that had to be kept in step by hand -- and the
        // roster half was structurally unable to show things the party half
        // could.
        //
        // ORDER IS THE FIELD'S ORDER, not "acting first" and not the
        // fight-long slot's. The column paints the party as it STANDS --
        // front, middle, rear, in the order Encounter.PlayerParty holds at
        // the moment of this repaint -- so a Move that trades two members'
        // places trades their cards with them. A plate does NOT belong to a
        // character for the whole fight; it belongs to whoever is standing
        // at that rank now, and the whole card moves at once (art, name,
        // theme, badges and both meters all come off `member` below).
        //
        // THAT IS A DECISION, not a default (AUDIT #144, owner 2026-09-11).
        // 4c4bddc3 made this loop slot-indexed while fixing PaintVitals and
        // named the consequence in its own message -- "the HUD column no
        // longer reorders itself when a Move reorders the field" -- as a side
        // effect of a correctness fix rather than something the brief asked
        // for. The owner's call is that the column follows the field: Move
        // costs a whole turn and buys nothing but position, so the column
        // that lists the party by rank has to show that the rank changed.
        //
        // WHAT REPLACES THE SLOT IS A PAINTED-OCCUPANCY RECORD, not a
        // re-read of the live list at paint time. _plateOccupants is written
        // here and READ by PaintVitals, which is what keeps F7's second half
        // fixed: a round has finished resolving before its first beat plays
        // (AfterResolution repaints the menu chrome ONLY, deliberately), so
        // for the length of that playback the live list is already in the
        // new order while the cards are still in the old one. Painting a
        // beat by live index lands the two moved members' numbers on each
        // other's portraits, which is the bug; painting by fight-long slot
        // avoids it by freezing the column, which is what #144 rejected.
        // Painting by what is CURRENTLY ON THE CARD does neither.
        private void RefreshPcPlates()
        {
            if (pcPlates == null || _session == null) return;

            var party = _session.Encounter.PlayerParty;
            var acting = ActingCharacter();

            // CLEARED HERE, not in the tick: this method is the one entry
            // point for a full column repaint, so the pulse list is rebuilt
            // exactly once per repaint and holds only the meters whose pool
            // actually asked for it.
            _pulsingMeters.Clear();

            // Rewritten whole on every repaint, never patched: it records
            // what THIS repaint put on the column, and a plate with no
            // occupant at all (a party of two) has to be recorded as empty
            // rather than left holding a departed ally.
            if (_plateOccupants == null || _plateOccupants.Length != pcPlates.Length)
            {
                _plateOccupants = new CombatantState[pcPlates.Length];
            }

            for (int i = 0; i < pcPlates.Length; i++)
            {
                var member = party != null && i < party.Count ? party[i] : null;

                _plateOccupants[i] = member;
                RefreshPcPlate(i, member, member != null && member == acting);
            }

            RefreshSecondLifeBadge();
        }

        // WHICH MEMBER EACH CARD IS CURRENTLY SHOWING -- the painted-occupancy
        // record of AUDIT #144, and the source of truth for the question
        // "which plate do this member's numbers go on", which is neither the
        // live party list (it reorders before the beats that describe the
        // reorder have played) nor _slotOf (which never reorders at all).
        // Written only by RefreshPcPlates above, read only by PaintVitals.
        private CombatantState[] _plateOccupants;

        // OVER PlayerParty RATHER THAN LivingPlayerParty, deliberately: a
        // downed ally is still worth showing here, at 0 HP, rather than
        // vanishing from the column the moment they fall. A plate with no
        // occupant at all (a party of two) is hidden outright -- there is no
        // meaningful "empty plate" drawing, since the plate art IS a
        // character.
        private void RefreshPcPlate(int i, CombatantState member, bool isActing)
        {
            pcPlates[i].SetShown(member != null);

            // A MISSING MEMBER STILL GETS ITS BADGE ROW PAINTED, with an
            // empty list -- that is what deactivates all five rather than
            // leaving them wearing a departed ally's last statuses.
            var rows = member != null ? FightHudModel.StatusRowsFor(_session, member) : EmptyStatusRows;
            PaintStatusRow(PcStatusBase + i * PcStatusBadgesPerPlate, PcStatusBadgesPerPlate,
                reserveOverflowSlot: true, member, rows, showCounter: true);

            if (member == null) return;

            if (Has(pcNames, i)) pcNames[i].SetContent(member.Name);

            if (Has(pcHpValues, i))
            {
                pcHpValues[i].Set(UiStrings.HealthValue, member.CurrentHealth, member.MaxHealth);
            }
            if (Has(pcHpFills, i)) SetFill(pcHpFills[i], member.CurrentHealth, member.MaxHealth);

            // THE TAG COMES OFF THE POOL, not out of UiStrings (plan P7). A
            // member with no pool cannot happen -- PrimaryPool is never null
            // on a CombatantState -- but an empty shortTag would print a
            // leading space, so it degrades to the numbers alone rather than
            // to a literal "MP" that could be wrong.
            if (Has(pcMpValues, i))
            {
                pcMpValues[i].Set(UiStrings.PoolNamedValue,
                    member.PrimaryPool?.ShortTag ?? "", member.CurrentMana, member.MaxMana);
            }
            if (Has(pcMpFills, i)) SetFill(pcMpFills[i], member.CurrentMana, member.MaxMana);

            // NEITHER A TAG NOR A VALUE, and both nulls are decisions.
            //
            // `tag`: there is no separate tag label on a plate -- the tag is
            // the first argument of UiStrings.PoolNamedValue, drawn on the
            // bar with the numbers, so RefreshPcPlate above writes it as part
            // of the caption.
            //
            // `value`: the caption is drawn ON the fill this pool colours, so
            // tinting it to match that fill is how it disappears. It stays
            // FightHudPalette.TextPrimary for every pool -- see
            // FightScreen's own note where the label is built. Passing the
            // label here (which the first cut of this method did) painted
            // "280/280" in pale pink on a full salmon HP bar and "MP 34/34"
            // in pale blue on a full blue one; both were legible in the tree
            // and neither was legible on screen.
            ApplyPoolTheme(member.PrimaryPool,
                Has(pcMpFills, i) ? pcMpFills[i] : null,
                Has(pcMpShades, i) ? pcMpShades[i] : null,
                pcMpRims, i * RimsPerCard,
                tag: null, value: null);

            RefreshPcSignature(i, member);

            var kit = _session.KitFor(member);
            ApplyPlateIdentity(i, kit, isActing);
        }

        // THE PLATE'S OWN ART AND ITS ACTING STATE, which are one job because
        // both answer "whose plate is this and is it their turn".
        //
        // THE ART IS CONTENT (characters.json's plateArt, carried in on the
        // kit) and cannot be baked: the three slots learn their occupant per
        // encounter. THE HIGHLIGHT is the acting signal -- the plate's own
        // silhouette as a halo, tinted with the occupant's identity colour --
        // and the name brightens to PcTheme.Name with it. Exactly one plate
        // in the column shows one at a time, which is what replaced moving
        // the acting character onto a bigger card.
        //
        // GRACEFUL ON EVERY MISS, the house style: a null kit, an unbound
        // array or a plate whose art did not load leaves what the scene baked
        // rather than clearing the graphic. A kit is nullable (a session with
        // no kit for a combatant), and a fixture-built kit authors no plate.
        private void ApplyPlateIdentity(int i, PlayerKit kit, bool isActing)
        {
            if (Has(pcPlateArts, i) && kit != null)
            {
                var art = PcPlateSprites.For(kit.PlateArt);
                if (art != null) pcPlateArts[i].sprite = art;
            }

            var colours = PcTheme.For(kit != null ? kit.PlateTheme : ButtonTheme.Blue);

            if (Has(pcPlateHighlights, i))
            {
                pcPlateHighlights[i].gameObject.SetShown(isActing);

                // FULL ALPHA on the rim hex, which carries 0.70 of its own --
                // the halo is the loudest thing this column says and it says
                // it for one character at a time, so it is drawn at the
                // colour's full strength rather than at the rim's.
                if (isActing) pcPlateHighlights[i].color = Opaque(Hex(colours.Rim));
            }

            if (Has(pcNames, i))
            {
                pcNames[i].color = isActing ? Hex(colours.Name) : Hex(FightHudPalette.TextPrimary);
            }
        }

        private static Color Opaque(Color colour) => new Color(colour.r, colour.g, colour.b, 1f);

        // The member's own signature resource as one line ("Wool 3/10"), or
        // nothing at all.
        //
        // A NUMBER RATHER THAN SIXTEEN PIPS, as of 2026-09-10. The pip row
        // was this column's one bespoke widget, it only ever existed on the
        // acting card, and the owner's mock-ups never had it -- so a
        // transformed or charged ALLY could not be read at all while the
        // acting character got a meter nobody asked for. One template on
        // every plate says the same thing in a tenth of the width.
        //
        // HIDDEN RATHER THAN BLANKED when the member carries no signature,
        // which is two thirds of the party: a label set to "" still occupies
        // its cell for UiAudit's purposes and reads as a missing value rather
        // than as a character who simply has no meter.
        private void RefreshPcSignature(int index, CombatantState member)
        {
            if (!Has(pcSignatures, index)) return;

            var signature = member?.SignaturePool;
            pcSignatures[index].gameObject.SetShown(signature != null);
            if (signature == null) return;

            pcSignatures[index].Set(UiStrings.SignatureNamedValue,
                signature.DisplayName, signature.Current, signature.Max);
        }

        // ---- the second meter, painted from the pool its holder carries ------
        //
        // WHY THIS IS A RUNTIME WRITE AT ALL. Ui.Meter bakes its four colours
        // into the scene at build time and Ui.Label bakes its tag, which was
        // right for as long as the second meter WAS mana. It is not any more:
        // a combatant's PrimaryPool is built from a pools.json row
        // (ContentDatabase.BuildPrimaryPool), and the row owns the tag and
        // the three hexes. A baked meter can only be one pool's meter, so a
        // second pool would have meant a second meter, hidden behind the
        // first -- which is a special case wearing a prefab.
        //
        // The bake is not wasted: it is MANA's palette, the pool everyone
        // shipped today holds, so an unrefreshed scene reads true and this
        // method writes back the identical values for them. Mana therefore
        // renders pixel-for-pixel as it did before this change; the meter
        // only looks different the day a row authors different numbers.
        //
        // RIM AND SHADE ARE DERIVED, NOT AUTHORED (RawPoolEntry.deepHex says
        // so): deepHex at 0.70 and 0.44, which is exactly what
        // FightHudPalette.MpRim/MpShade are relative to MpDeep. Two authored
        // fields fewer, and no way for a row to author a rim that disagrees
        // with its own bar.
        //
        // GRACEFUL ON EVERY MISS, the house style: a null pool, an unparseable
        // hex or a short array leaves the baked value standing rather than
        // clearing the graphic to magenta.
        private const float MeterRimAlpha = 179f / 255f;    // #..B3, FightHudPalette.MpRim
        private const float MeterShadeAlpha = 112f / 255f;  // #..70, FightHudPalette.MpShade

        private void ApplyPoolTheme(ResourcePool pool, Image fill, Image shade, Image[] rims, int baseIndex,
                                    TMP_Text tag, TMP_Text value)
        {
            if (pool == null) return;

            if (TryHex(pool.BrightHex, out var bright))
            {
                if (fill != null)
                {
                    fill.color = bright;

                    // ENROLLED ONLY IF THE ROW ASKED. A pool with pulse false
                    // is never written again after this line -- see
                    // RefreshPoolPulse for why that matters more than it
                    // looks.
                    if (pool.Pulse) _pulsingMeters.Add(new PulsingMeter { Fill = fill, Base = bright });
                }

                if (tag != null) tag.color = bright;
            }

            if (TryHex(pool.DeepHex, out var deep))
            {
                if (shade != null) shade.color = WithAlpha(deep, MeterShadeAlpha);

                var rim = WithAlpha(deep, MeterRimAlpha);
                for (int e = 0; e < RimsPerCard; e++)
                {
                    if (Has(rims, baseIndex + e)) rims[baseIndex + e].color = rim;
                }
            }

            if (tag != null) tag.Set(UiStrings.PoolTag, pool.ShortTag);
            if (value != null && TryHex(pool.TextHex, out var text)) value.color = text;
        }

        private static bool TryHex(string hex, out Color colour)
        {
            colour = default;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out colour);
        }

        private static Color WithAlpha(Color colour, float alpha) =>
            new Color(colour.r, colour.g, colour.b, alpha);

        // ---- the heartbeat ---------------------------------------------------
        //
        // A pool whose row says pulse:true breathes. Arithmetic over the clock
        // on Update, not a coroutine -- the same shape RewardTrackController.
        // Motion's own idle animations take, and for the same reason: a
        // coroutine per meter would need starting, stopping and restarting
        // every time the acting character changed, which is three lifecycles
        // to get wrong for a value that is a pure function of the time.
        //
        // UNSCALED, DELIBERATELY. The battle-speed preset scales the beat
        // clock so a player who set 3x watches the fight resolve faster; a
        // resting heartbeat is not part of the fight's resolution, it is
        // ambient state on a card, and speeding it up with the combat would
        // make the HUD read as agitated at exactly the moment the player
        // chose to hurry. It also keeps beating while a beat is paused, which
        // is the whole point of an idle animation.
        //
        // ALPHA, NOT SCALE OR WIDTH. SetFill owns the fill's anchorMax (that
        // is the drain), so anything touching the RectTransform here would
        // fight it every frame; colour is the one channel nothing else on
        // this meter writes per frame.
        //
        // A REST IS PART OF THE SHAPE. Two thumps close together, the second
        // weaker, then most of the loop at the floor -- lub-dub, pause. That
        // means two arbitrary samples can legitimately read the same alpha,
        // which is why the test that proves it moves samples across a whole
        // loop rather than twice.
        private const float PoolPulseLoop = 1.1f;
        private const float PoolPulseFloorAlpha = 0.7f;
        private const float PoolPulseSecondBeatPhase = 0.22f;
        private const float PoolPulseSecondBeatWeight = 0.72f;
        private const float PoolPulseBeatHalfWidth = 0.11f;

        private struct PulsingMeter
        {
            public Image Fill;

            // The colour the pool asked for, kept beside the Image because the
            // tick multiplies its alpha and must not compound: reading the
            // alpha back off the Image would make every frame a fraction of
            // the last one and fade the bar out entirely.
            public Color Base;
        }

        private readonly List<PulsingMeter> _pulsingMeters = new List<PulsingMeter>();

        private void RefreshPoolPulse()
        {
            // THE EARLY RETURN IS THE CONTRACT, not an optimisation. Mana's
            // row says pulse:false, so nothing enrols, so this method writes
            // to no Image on any frame of any fight shipped today -- there is
            // no per-frame cost and no per-frame colour write to collide with
            // a repaint.
            if (_pulsingMeters.Count == 0) return;

            float phase = Mathf.Repeat(Time.unscaledTime / PoolPulseLoop, 1f);
            float beat = Mathf.Max(
                Thump(phase, 0f),
                Thump(phase, PoolPulseSecondBeatPhase) * PoolPulseSecondBeatWeight);
            float alpha = Mathf.Lerp(PoolPulseFloorAlpha, 1f, beat);

            for (int i = 0; i < _pulsingMeters.Count; i++)
            {
                var meter = _pulsingMeters[i];
                if (meter.Fill == null) continue;
                meter.Fill.color = WithAlpha(meter.Base, meter.Base.a * alpha);
            }
        }

        // One raised-cosine bump centred on `centre`, measured around the
        // loop so a beat at phase 0 does not clip at the seam.
        private static float Thump(float phase, float centre)
        {
            float d = Mathf.Abs(Mathf.Repeat(phase - centre + 0.5f, 1f) - 0.5f);
            if (d >= PoolPulseBeatHalfWidth) return 0f;
            return 0.5f * (1f + Mathf.Cos(Mathf.PI * d / PoolPulseBeatHalfWidth));
        }

        // RefreshTransformStrip is GONE (2026-09-09). The strip it painted was
        // a whole panel that said one sentence about the ACTING character and
        // could therefore never show a transformed ally sitting in the roster
        // at all. A transformation is a StatusRow now
        // (FightHudModel.StatusRowsFor -> StatusHud.TransformRow), so every
        // surface that already paints status badges -- all three PC plates
        // and the hover tooltip that names it in full -- picks
        // it up through PaintStatusRow with no code here at all. That is the
        // whole point of folding it in rather than adding a fourth writer.

        // The one badge on this screen that reads FightSession rather than
        // the acting character -- a Second Life charge belongs to the whole
        // party's run, not to whoever happens to be acting this turn. It has
        // no per-character home for that reason, and sits in the free band at
        // the foot of the bottom plate rather than moving with the turn --
        // see FightScreen.PcSecondLifeY.
        private void RefreshSecondLifeBadge()
        {
            if (secondLifeBadge == null || _session == null) return;

            bool available = _session.SecondLivesSpent < _session.SecondLifeCharges;
            secondLifeBadge.SetShown(available);
        }

        // ---- status badges (enemy stage rows, PC plates) ----------------------
        //
        // PLAN_STATUS_EFFECT_UI.md sections 1, 3, 6 and 7 -- ONE anatomy
        // (FightScreen.BuildStatusBadge's Glyph/Code/Counter three-child
        // button), one paint routine (PaintStatusRow), three call sites: the
        // enemy row from FightController.StageVisuals.cs's RefreshStage,
        // right beside RefreshIntentIcons so a status and an intent always
        // paint off the same beat; each PC plate from RefreshPcPlate above.
        // Replaces
        // RefreshPartyBuffs outright -- BuffBadge/BuffBadgesFor/StatusBadge/
        // PillCode/PillCategory/CategoryOf are gone with it (Package A,
        // section 4/11).
        //
        // GREEN for something helping the character, RED for something
        // hurting them -- IntentHeal and HpBright, the same two tokens the
        // enemy-intent badges already use for the identical helping/hurting
        // split, reused rather than a third palette for one idea the HUD
        // already has a colour language for.
        private static readonly Color BuffPositive = Hex(FightHudPalette.IntentHeal);
        private static readonly Color BuffNegative = Hex(FightHudPalette.HpBright);

        // The "+N" overflow chip's tint -- neither polarity, since it is a
        // mixed bag by definition. See PaintOverflowChip's own comment for
        // why TextSecondary is the token used.
        private static readonly Color ChipNeutral = Hex(FightHudPalette.TextSecondary);

        // ---- last-tick emphasis (PLAN_STATUS_EFFECT_UI.md section 3/9 Phase 3) --
        //
        // STATIC, on purpose -- the plan asks for "a static emphasised counter
        // patch at 1 remaining, no pulse". A badge whose own status is about
        // to expire gets a brighter, bolder digit and a slightly bigger patch
        // behind it; every other count keeps the ordinary TextPrimary digit
        // this badge always drew, no separate "emphasised" palette needed for
        // any other value.
        private static readonly Color CounterDefault = Hex(FightHudPalette.TextPrimary);
        private static readonly Color CounterEmphasis = Hex(FightHudPalette.HpBright);
        private const float CounterPatchEmphasisScale = 1.3f;

        // ---- the appearance pop (PLAN_STATUS_EFFECT_UI.md section 6/D7 lineage) --
        //
        // GameSettings (checked before adding this) carries no reduced-motion
        // switch -- display and audio only. Rather than invent a setting
        // nobody can reach from the Options screen yet, this is the literal
        // "gate it behind a single const bool" the plan asks for instead:
        // flip to false to turn the pop off project-wide with no other code
        // touched. Restrained on purpose -- 150ms, one shrink, no bounce.
        private const bool StatusBadgeAppearancePopEnabled = true;
        private const float StatusBadgePopFromScale = 1.3f;
        private const float StatusBadgePopSeconds = 0.15f;

        private static readonly List<FightHudModel.StatusRow> EmptyStatusRows =
            new List<FightHudModel.StatusRow>();

        // EnemyStatusBadgesPerRow/RosterStatusBadgesPerRow/EnemyStatusBadgeCount/
        // RosterStatusBadgeCount/PartyStatusBadgeCount moved to the root
        // FightController.cs (S9's review, CODE_STANDARDS.md section 4: a
        // MonoBehaviour's consts and runtime state fields belong on the root
        // partial-class file, methods only here).

        // Mirrors FightScreen's own EnemyStatusBadgeSize/EnemyStatusPitch/
        // EnemyStatusStripPadX, same reasoning as EnemyStatusBadgesPerRow
        // above -- needed here because FitEnemyStatusRow re-derives the
        // strip's RUNTIME width from however many badges are actually
        // shown, rather than the fixed 5-slot width UiAudit checks at build
        // time (section 1/3: FightScreen.BuildEnemyStatusRows' own header).
        private const float EnemyStatusBadgeSize = 36f;
        private const float EnemyStatusPitch = 40f;
        private const float EnemyStatusStripPadX = 12f;

        // ONE FLAT INDEX SPACE across both surfaces (the enemy stage rows
        // first, then the three PC plates) rather than a HoverIndex handler
        // per surface -- one OnHoverStatusBadge, one tooltip cache, one
        // "who is currently hovered" field. It used to span THREE surfaces;
        // the party plate and the roster rows became one uniform PC column
        // on 2026-09-10, so the second and third ranges collapsed into one.
        private const int EnemyStatusBase = 0;
        private const int PcStatusBase = EnemyStatusBadgeCount;
        private const int TotalStatusBadges = PcStatusBase + PcStatusBadgeCount;

        // One layer per named child BuildStatusBadge actually built (Glyph/
        // Code/Counter), plus the ROOT's own Image -- every Ui.Button gets
        // one whether chromeless or not (UiEmitter.EmitButton), and
        // BuildStatusBadge leaves it fully transparent, which is exactly an
        // unused Image sitting there for the polarity frame (section 3) to
        // move into.
        //
        // UNIFORM ACROSS ALL THREE SURFACES, as of the fix for the first
        // capture's worst defect: PartyBuff{i} used to be a plain chromeless
        // Ui.Button (root Image doubling as both frame and glyph, Code
        // carrying the counter folded into its own text, "PSN·2") because
        // package B's original pass left the party plate predating this
        // feature untouched. That workaround is what the capture showed as
        // six tinted smudges with text stacked on top -- FightScreen now
        // rebuilds PartyBuff{i} through BuildStatusBadge's own three-child
        // anatomy, so all three surfaces wire and paint the same way and
        // this struct needs no fallback-sprite field or party-only branch.
        private struct StatusBadgeParts
        {
            public GameObject Root;
            public Image Frame;
            public Image Glyph;
            public TMPro.TMP_Text Code;
            public TMPro.TMP_Text Counter;
            public Image CounterPatch;
        }

        // _statusBadgeParts, _statusBadgeTooltip, _statusRowActiveCodes,
        // _statusRowScratchCodes, _statusBadgePopRoutines, _hoveredStatusBadge,
        // _statusTooltipCanvas, _enemyStatusStripHome and _enemyStatusBadgeHome
        // all moved to the root FightController.cs (S9's review) -- see them
        // there for what each one is and why.

        private void WireAllStatusBadges()
        {
            _statusBadgeParts = new StatusBadgeParts[TotalStatusBadges];

            WireStatusBadgeGroup(enemyStatusBadges, EnemyStatusBase);
            WireStatusBadgeGroup(pcStatusBadges, PcStatusBase);
            CaptureEnemyStatusRowHomePositions();

            // CACHED ONCE HERE (S8's review), not re-fetched by
            // PlaceStatusTooltip on every single hover -- the ancestor
            // chain a GetComponentInParent walk climbs never changes after
            // the scene is built, so paying that walk per hover bought
            // nothing a one-time lookup at wiring time doesn't already have.
            _statusTooltipCanvas = statusTooltip != null ? statusTooltip.GetComponentInParent<Canvas>() : null;

            if (statusTooltip != null) statusTooltip.SetShown(false);
        }

        private void CaptureEnemyStatusRowHomePositions()
        {
            _enemyStatusStripHome = enemyStatusStrips?
                .Select(s => s != null ? ((RectTransform)s.transform).anchoredPosition : Vector2.zero)
                .ToArray();

            _enemyStatusBadgeHome = enemyStatusBadges?
                .Select(b => b != null ? ((RectTransform)b.transform).anchoredPosition : Vector2.zero)
                .ToArray();
        }

        private void WireStatusBadgeGroup(GameObject[] badges, int baseIndex)
        {
            if (badges == null) return;

            for (int i = 0; i < badges.Length; i++)
            {
                var badge = badges[i];
                if (badge == null) continue;

                int flat = baseIndex + i;
                if (flat < 0 || flat >= _statusBadgeParts.Length) continue;

                _statusBadgeParts[flat] = WireExistingStatusBadge(badge);

                var hover = badge.GetComponent<HoverIndex>();
                if (hover == null) hover = badge.AddComponent<HoverIndex>();
                hover.Index = flat;
                hover.Changed = OnHoverStatusBadge;
            }
        }

        // Every surface's badges: BuildStatusBadge's own three named
        // children, found by name rather than GetComponentInChildren so the
        // button's own synthesised (and otherwise-empty) caption label never
        // gets mistaken for one of them. PartyBuff{i} is built through the
        // same BuildStatusBadge call the enemy and roster rows use (see
        // FightScreen.BuildPartyBuffIcons), so this one wiring path now
        // serves all three.
        private static StatusBadgeParts WireExistingStatusBadge(GameObject badge)
        {
            var parts = new StatusBadgeParts { Root = badge, Frame = badge.GetComponent<Image>() };

            var glyph = badge.transform.Find("Glyph");
            if (glyph != null) parts.Glyph = glyph.GetComponent<Image>();

            var code = badge.transform.Find("Code");
            if (code != null) parts.Code = code.GetComponent<TMPro.TMP_Text>();

            var counter = badge.transform.Find("Counter");
            if (counter != null) parts.Counter = counter.GetComponent<TMPro.TMP_Text>();

            var counterPatch = badge.transform.Find("CounterPatch");
            if (counterPatch != null) parts.CounterPatch = counterPatch.GetComponent<Image>();

            return parts;
        }

        // ---- the polarity frame (section 3) ------------------------------------
        //
        // A smooth rounded outline for a benefit, clipped corners with a top
        // notch for a detriment -- geometry, not colour, so both survive
        // greyscale. B could not bake a proc: sprite from this worktree
        // (ProceduralSpriteBaker is an Editor menu item -- see
        // FightScreen.BuildStatusBadge's own comment), so this bakes the
        // same idea at RUNTIME instead: once per polarity, ever, cached in
        // these two statics -- never per badge, never per repaint. Neither
        // rotates the glyph's RectTransform -- B suggested that as a
        // stopgap and section 3 rejects it; this is a separate Image
        // instead.
        private const int FrameTextureSize = 36;
        private const float FrameRingThickness = 2f;

        private static Sprite _positiveFrameSprite;
        private static Sprite _negativeFrameSprite;

        private static Sprite PositiveFrameSprite => _positiveFrameSprite ??= BuildFrameSprite(detriment: false);
        private static Sprite NegativeFrameSprite => _negativeFrameSprite ??= BuildFrameSprite(detriment: true);

        private static Sprite BuildFrameSprite(bool detriment)
        {
            var texture = new Texture2D(FrameTextureSize, FrameTextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float half = FrameTextureSize * 0.5f;
            var pixels = new Color32[FrameTextureSize * FrameTextureSize];
            for (int y = 0; y < FrameTextureSize; y++)
            {
                for (int x = 0; x < FrameTextureSize; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    bool onRing = detriment ? OnClippedRing(px, py, half) : OnRoundedRing(px, py, half);
                    pixels[y * FrameTextureSize + x] = onRing
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, FrameTextureSize, FrameTextureSize),
                new Vector2(0.5f, 0.5f), 100f);
        }

        // Rounded-rectangle signed distance: <= 0 is inside. The ring is
        // between the outer boundary and one inset by FrameRingThickness.
        private static bool OnRoundedRing(float x, float y, float half)
        {
            const float margin = 1.5f;
            const float radius = 8f;
            float outer = RoundedRectSdf(x, y, half - margin, radius);
            float inner = RoundedRectSdf(x, y, half - margin - FrameRingThickness,
                Mathf.Max(radius - FrameRingThickness, 0f));
            return outer <= 0f && inner > 0f;
        }

        private static float RoundedRectSdf(float x, float y, float halfExtent, float radius)
        {
            float qx = Mathf.Abs(x) - (halfExtent - radius);
            float qy = Mathf.Abs(y) - (halfExtent - radius);
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // Four straight corner cuts plus a small rectangular bite out of the
        // top edge -- the detriment shape section 3 asks for, drawn as a
        // ring the same way OnRoundedRing is.
        private static bool OnClippedRing(float x, float y, float half)
        {
            const float margin = 1.5f;
            const float cut = 7f;
            const float notchHalfWidth = 3f;
            const float notchDepth = 4f;

            bool outer = InsideClippedRect(x, y, half - margin, cut);
            bool inner = InsideClippedRect(x, y, half - margin - FrameRingThickness,
                Mathf.Max(cut - FrameRingThickness, 0f));
            bool inNotch = Mathf.Abs(x) <= notchHalfWidth && y >= half - margin - notchDepth;
            return outer && !inner && !inNotch;
        }

        private static bool InsideClippedRect(float x, float y, float halfExtent, float cut)
        {
            if (Mathf.Abs(x) > halfExtent || Mathf.Abs(y) > halfExtent) return false;
            float cx = Mathf.Abs(x) - (halfExtent - cut);
            float cy = Mathf.Abs(y) - (halfExtent - cut);
            return cx <= 0f || cy <= 0f || (cx + cy) <= cut;
        }

        // ---- shared icon cache --------------------------------------------------
        //
        // The exact same system twice: a sprite resolved once per key via
        // Resources.Load and kept in a dictionary that only ever grows, with
        // a MISS cached right alongside a HIT so a key with no authored art
        // stops asking Resources for a file that will never exist.
        // StatusRowSprites below and IntentSprites in
        // FightController.StageVisuals.cs both go through one instance of
        // this generic cache.
        //
        // Applying the result to an Image is the ONE place the two systems
        // still deliberately disagree, so that stays a parameter rather
        // than getting folded in here too: an enemy-intent badge with no art
        // is a BUG and disables its Image outright (a blank slot beats a
        // filled white rectangle -- see RefreshIntentIcons' own comment); a
        // status badge with no art (twelve of fourteen entries today) is
        // that badge's INTENDED steady state and falls back to Code instead
        // -- see PaintBadge.
        private class IconCache<TKind>
        {
            private readonly Dictionary<TKind, Sprite> _sprites = new Dictionary<TKind, Sprite>();

            public Sprite Resolve(TKind kind, System.Func<TKind, string> resourcePath)
            {
                if (_sprites.TryGetValue(kind, out var cached)) return cached;

                var loaded = Resources.Load<Sprite>(resourcePath(kind));
                _sprites[kind] = loaded;
                return loaded;
            }

            public static void Apply(Image image, Sprite art, bool disableOnMiss, Sprite defaultSprite = null)
            {
                if (image == null) return;

                if (disableOnMiss)
                {
                    image.sprite = art;
                    image.enabled = art != null;
                }
                else
                {
                    image.sprite = art != null ? art : defaultSprite;
                }
            }
        }

        // Keyed by SLUG rather than by the whole StatusRow -- a row's
        // Tooltip and Counter change every tick, and a struct key would mint
        // a new dictionary entry (and a new Resources.Load) every turn a
        // status counts down, when the art behind it never does. The two
        // speed presentations have no StatusEffectType to key on instead
        // (section 4), which is exactly why this is slug-keyed and the old
        // StatusEffectType-keyed cache is gone -- every caller now holds a
        // StatusRow already.
        private static readonly IconCache<string> StatusRowSprites = new IconCache<string>();

        private static Sprite StatusSpriteFor(FightHudModel.StatusRow row) =>
            StatusRowSprites.Resolve(row.Slug, _ => FightHudModel.StatusBadgeIcons.ResourceFor(row));

        // ---- painting one row ---------------------------------------------------

        // Fills up to `realCapacity` slots from `rows`, in order. Once rows
        // outgrow that capacity, the LAST of the `slotCount` physical slots
        // becomes the "+N" chip (section 6). `reserveOverflowSlot` is what
        // decides whether that capacity is `slotCount - 1` (the enemy and
        // roster rows: 5 physical slots, only 4 ever show a real status --
        // FightScreen.BuildStatusBadge's own comment: "4 statuses plus the
        // +N overflow chip is 5 nodes per row", section 1's "Slots: 4"
        // against a 5-node build) or `slotCount` itself (the party plate: 6
        // physical slots, all 6 usable, the 6th doubling as the chip "at
        // 7+" the moment a 7th status arrives). One bool rather than a
        // second explicit int (S5's review) -- the derived capacity is
        // always one of exactly these two shapes, never an arbitrary third
        // number, so there was nothing the extra parameter could say that
        // "does this row reserve a slot for overflow" doesn't already.
        //
        // Deactivates every unused slot, which is what section 7's "a row
        // with no statuses renders nothing" reduces to when `rows` is
        // empty: every slot in range hits the i >= rows.Count branch below.
        //
        // `holder` is who this physical row currently shows -- null for a
        // roster/enemy slot with nobody in it this refresh. The appearance
        // pop is keyed off THIS, not off baseIndex (C4's review): a null
        // holder paints no rows worth popping anyway, and a real one carries
        // its own remembered code set across whichever seat it occupies from
        // one refresh to the next.
        private void PaintStatusRow(int baseIndex, int slotCount, bool reserveOverflowSlot, CombatantState holder,
            IReadOnlyList<FightHudModel.StatusRow> rows, bool showCounter)
        {
            if (_statusBadgeParts == null) WireAllStatusBadges();
            if (_statusBadgeParts == null) return;

            int realCapacity = reserveOverflowSlot ? slotCount - 1 : slotCount;
            int overflowSlot = rows.Count > realCapacity ? slotCount - 1 : -1;

            // The set THIS HOLDER showed as of last refresh -- see
            // _statusRowActiveCodes' own header. Missing entirely on a
            // holder's first paint (a fresh WireAllStatusBadges, or a
            // combatant appearing for the first time this fight), which is
            // correct: every badge shown for the first time really is going
            // from inactive to active. Read BEFORE the scratch buffer below
            // touches anything, and never mutated here -- PaintBadge's popIn
            // check below has to compare against what this holder showed
            // last time, not against whatever this same call has painted so
            // far.
            HashSet<string> previousCodes = holder != null && _statusRowActiveCodes.TryGetValue(holder, out var found)
                ? found
                : null;

            // C4/S5: ONE scratch set, reused every call rather than a fresh
            // HashSet<string> allocated and thrown away each refresh. Safe
            // because painting is synchronous and single-threaded -- nothing
            // else can be mid-fill of this buffer at the same time.
            _statusRowScratchCodes.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                int flat = baseIndex + i;
                if (flat < 0 || flat >= _statusBadgeParts.Length) continue;

                var parts = _statusBadgeParts[flat];
                if (parts.Root == null) continue;

                if (i == overflowSlot)
                {
                    ResetBadgeMotion(flat, parts.Root);
                    PaintOverflowChip(parts, rows, overflowSlot, flat);
                    continue;
                }

                if (i >= rows.Count)
                {
                    ResetBadgeMotion(flat, parts.Root);
                    parts.Root.SetShown(false);
                    _statusBadgeTooltip[flat] = null;
                    continue;
                }

                var row = rows[i];
                _statusRowScratchCodes.Add(row.Code);
                bool popIn = StatusBadgeAppearancePopEnabled && holder != null
                    && (previousCodes == null || !previousCodes.Contains(row.Code));
                PaintBadge(parts, row, showCounter, flat, popIn);
            }

            if (holder == null) return;

            // COMMIT, reusing the holder's own stored set rather than handing
            // it a new one -- the only allocation left is the one-time `new
            // HashSet<string>` the first time THIS holder is ever painted,
            // exactly like any other dictionary entry's first insert.
            if (previousCodes != null)
            {
                previousCodes.Clear();
                previousCodes.UnionWith(_statusRowScratchCodes);
            }
            else
            {
                _statusRowActiveCodes[holder] = new HashSet<string>(_statusRowScratchCodes);
            }
        }

        // ONE PATH for both surfaces -- enemy rows and PC plates alike
        // now share BuildStatusBadge's Glyph/Code/Counter anatomy (see
        // StatusBadgeParts' own header), so there is no longer a dedicated-
        // frame-or-not branch here. Glyph stays untinted (section 3: art is
        // painted, not recoloured, so Chilled and Rooted's own colours are
        // never fought by a tint on top of them); polarity paints the Frame
        // and the Code text instead. Code is blanked on an icon hit and the
        // Counter carries the number on its own dedicated node -- the party
        // plate no longer folds it into "PSN·2" text, because it has a real
        // Counter node to write to like everything else.
        private void PaintBadge(StatusBadgeParts parts, FightHudModel.StatusRow row, bool showCounter, int flat, bool popIn)
        {
            parts.Root.SetShown(true);

            var polarity = row.IsPositive ? BuffPositive : BuffNegative;

            if (parts.Frame != null)
            {
                parts.Frame.sprite = row.IsPositive ? PositiveFrameSprite : NegativeFrameSprite;
                parts.Frame.enabled = parts.Frame.sprite != null;
                parts.Frame.color = polarity;
            }

            // ICON FIRST, same priority RefreshIntentIcons already uses (and
            // the retired RefreshPartyBuffs used before this replaced it).
            // All fourteen (twelve statuses plus both speed presentations)
            // ship real art today under Resources/Status/ (S3's review --
            // StatusBadgeIconTests), so the three-letter Code fallback below
            // is now only for a future status/presentation added before its
            // own art lands.
            var art = StatusSpriteFor(row);
            if (parts.Glyph != null)
            {
                parts.Glyph.color = Color.white;
                parts.Glyph.sprite = art;
                parts.Glyph.enabled = art != null;
            }

            bool showNumber = showCounter && row.Counter >= 0;

            // LAST-TICK EMPHASIS, static -- section 2/9 Phase 3. Only the
            // exact value 1 gets the brighter/bolder digit and the bigger
            // patch; every other count (including "no counter drawn at all")
            // falls through to the ordinary look, so a status ticking from 3
            // to 2 to 1 changes on the one frame that matters and nowhere else.
            bool emphasise = showNumber && row.Counter == 1;
            if (parts.Code != null)
            {
                parts.Code.color = polarity;
                parts.Code.SetContent(art == null ? row.Code : "");
            }
            if (parts.Counter != null)
            {
                parts.Counter.SetContent(showNumber ? row.Counter.ToString() : "");
                parts.Counter.color = emphasise ? CounterEmphasis : CounterDefault;
                parts.Counter.fontStyle = emphasise ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            }
            if (parts.CounterPatch != null)
            {
                parts.CounterPatch.gameObject.SetShown(showNumber);
                ((RectTransform)parts.CounterPatch.transform).localScale =
                    Vector3.one * (emphasise ? CounterPatchEmphasisScale : 1f);
            }

            _statusBadgeTooltip[flat] = row.Tooltip;

            // THE POP, LAST -- after everything else about this badge is
            // already painted, so a pop that starts mid-way through a
            // repaint never races the very values it is popping in.
            if (popIn) BeginAppearancePop(flat, parts.Root);
            else ResetBadgeMotion(flat, parts.Root);
        }

        // ---- the appearance pop --------------------------------------------------

        private void ResetBadgeMotion(int flat, GameObject root)
        {
            if (flat >= 0 && flat < _statusBadgePopRoutines.Length && _statusBadgePopRoutines[flat] != null)
            {
                StopCoroutine(_statusBadgePopRoutines[flat]);
                _statusBadgePopRoutines[flat] = null;
            }

            if (root != null) ((RectTransform)root.transform).localScale = Vector3.one;
        }

        private void BeginAppearancePop(int flat, GameObject root)
        {
            if (root == null || flat < 0 || flat >= _statusBadgePopRoutines.Length) return;

            if (_statusBadgePopRoutines[flat] != null) StopCoroutine(_statusBadgePopRoutines[flat]);

            // AUDIT #106, and the fix shape is the one that finding names.
            //
            // A screen being torn down repaints ONCE more on its way out --
            // the engine deactivates the outgoing scene, FightBeatPlayer.
            // OnDisable ends the fight, its Flush fires OnPlaybackFinished,
            // and that calls RefreshUi on a panel that is already inactive.
            // StartCoroutine on an inactive GameObject is a hard engine error
            // rather than a no-op, so a status badge that happened to be new
            // on that last repaint logged "Coroutine couldn't be started
            // because the game object 'FightPanel' is inactive!" -- cosmetic
            // in the game (nothing pops on a screen nobody will see again)
            // and not cosmetic in a test host, where an unexpected error
            // fails whichever unrelated fixture loaded the second scene.
            //
            // THE SCENE'S OWN FLAG, not just isActiveAndEnabled, and that is
            // a correction to the fix shape #106 proposed. Measured: at the
            // moment of that teardown repaint isActiveAndEnabled still reads
            // TRUE -- nothing called SetActive, the whole SCENE is being
            // unloaded -- and StartCoroutine refuses anyway, because the
            // scheduler will not take an object whose scene is on its way
            // out. Scene.isLoaded is the flag that has already flipped, and
            // it is the one Unity's own "was this destroyed by a scene
            // unload" idiom uses.
            //
            // The final scale directly, which is the same graceful-
            // degradation rule the rest of this file follows: the badge ends
            // where the pop would have left it.
            if (!isActiveAndEnabled || !gameObject.scene.isLoaded)
            {
                _statusBadgePopRoutines[flat] = null;
                ((RectTransform)root.transform).localScale = Vector3.one;
                return;
            }

            _statusBadgePopRoutines[flat] = StartCoroutine(AppearancePopRoutine((RectTransform)root.transform));
        }

        // Scaled by FightBeatPlayer.BeatSpeedMultiplier, the same test seam
        // every other fight-HUD animation already answers to -- a PlayMode
        // capture that runs the fight at 60x would otherwise sit through a
        // real 150ms per badge for no reason a screenshot can see.
        private IEnumerator AppearancePopRoutine(RectTransform rect)
        {
            rect.localScale = Vector3.one * StatusBadgePopFromScale;

            float duration = FightBeatPlayer.Scaled(StatusBadgePopSeconds);
            if (duration <= 0f)
            {
                rect.localScale = Vector3.one;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float t = Easing.OutCubic(Mathf.Clamp01(elapsed / duration));
                rect.localScale = Vector3.one * Mathf.Lerp(StatusBadgePopFromScale, 1f, t);
            }

            rect.localScale = Vector3.one;
        }

        // Section 6's whole overflow feature: the chip carries no polarity
        // of its own (it is a mixed bag by definition) and hovering it
        // lists every hidden entry, one per line, rather than opening a
        // second, scrollable inspector.
        //
        // THE FRAME STAYS ON, tinted NEUTRAL rather than either polarity --
        // the first capture shipped this chip with no frame at all
        // (`Frame.enabled = false`), which is what made it read as loose
        // text floating beside the real badges instead of a badge itself.
        // ChipNeutral is FightHudPalette.TextSecondary: the archived
        // battle_ui spec (docs/handoffs/archive/battle_ui/README.md) named
        // this exact chip "neutral violet border, no category colour", and
        // TextSecondary (#BFB0D4) is the one token in this palette that
        // already reads as a muted violet-grey rather than a hue with its
        // own meaning -- the rounded (benefit) frame shape is used rather
        // than the clipped-detriment one arbitrarily, since neither shape
        // says "mixed bag" more than the other.
        private void PaintOverflowChip(StatusBadgeParts parts, IReadOnlyList<FightHudModel.StatusRow> rows,
            int chipIndex, int flat)
        {
            parts.Root.SetShown(true);

            if (parts.Frame != null)
            {
                parts.Frame.sprite = PositiveFrameSprite;
                parts.Frame.enabled = parts.Frame.sprite != null;
                parts.Frame.color = ChipNeutral;
            }

            if (parts.Glyph != null) parts.Glyph.enabled = false;
            if (parts.Counter != null) parts.Counter.SetContent("");
            if (parts.CounterPatch != null) parts.CounterPatch.gameObject.SetShown(false);

            int hidden = rows.Count - chipIndex;
            if (parts.Code != null)
            {
                parts.Code.color = ChipNeutral;
                parts.Code.SetContent($"+{hidden}");
            }

            var lines = new List<string>(hidden);
            for (int i = chipIndex; i < rows.Count; i++) lines.Add($"{rows[i].Code} · {rows[i].Tooltip}");
            _statusBadgeTooltip[flat] = string.Join("\n", lines);
        }

        // ---- the two call sites -------------------------------------------------
        //
        // The PC column's own call is inside RefreshPcPlate, one plate at a
        // time, because a plate's badges belong to that plate's occupant and
        // nothing else on the column knows who that is.

        // Called from FightController.StageVisuals.cs's RefreshStage, right
        // beside RefreshIntentIcons, so a status row and an intent badge
        // always paint off the same beat. `onStage` is RefreshStage's own
        // count of enemies actually shown -- see the reposition comment
        // below for why this method needs it too.
        private void RefreshEnemyStatusRows()
        {
            if (enemyStatusBadges == null || _session == null) return;

            var enemies = _session.Encounter.Enemies;
            int slots = enemyStatusStrips != null ? enemyStatusStrips.Length : 0;

            for (int slot = 0; slot < slots; slot++)
            {
                // BUSY-AWARE ALIVE, mirroring StanceOf's own `defeated` check
                // (FightController.StageVisuals.cs) rather than reading
                // IsAlive straight -- C1's review. FightSession resolves a
                // whole round before any beat plays, so live IsAlive already
                // reflects a kill from LATER in the round while this refresh
                // is only allowed to know about beats already painted; this
                // is what stopped the row from going blank the instant a
                // later beat's kill lands in the model, several beats before
                // the death animation the player is actually watching plays.
                bool alive = slot < enemies.Count &&
                    (_isBusy ? !_confirmedDefeated.Contains(enemies[slot]) : enemies[slot].IsAlive);
                var enemy = alive && IsOnStage(enemies[slot]) ? enemies[slot] : null;
                var rows = enemy != null ? FightHudModel.StatusRowsFor(_session, enemy) : EmptyStatusRows;

                // Section 7: a row with no statuses renders nothing at all,
                // backing strip included -- the common case on most opening
                // turns.
                if (Has(enemyStatusStrips, slot)) enemyStatusStrips[slot].SetShown(rows.Count > 0);

                // RE-SPREAD, the same way AnchorOne re-spreads the
                // FIGURE itself. FightScreen.BuildEnemyStatusRows baked this
                // row's position against the FIXED FightHudSpec.
                // StageSlotsPerSide slot geometry (it has to: it runs at
                // build time, before any encounter exists to count), but
                // AnchorOne "spreads however many actors are
                // ACTUALLY on this side across the whole depth range,
                // instead of filling the first N of three fixed slots" (its
                // own header). With fewer than three enemies those two
                // disagree about where slot 1 sits, and a row left at its
                // baked position draws over whichever OTHER row the spread
                // happens to have moved there -- found by capturing this
                // fixture's own screenshot with two enemies and watching
                // the second one's row land on the first one's figure.
                //
                // TAKEN FROM WHERE THE FIGURE ACTUALLY WENT, not recomputed
                // from the slot index. This used to read SlotOffset(slot,
                // onStage) and be right, because a slot's index WAS its rank;
                // A3 separated the two, so a survivor that closed up over a
                // faded corpse stands at a rank its slot index no longer
                // names -- and this row would have stayed behind on the empty
                // ground the corpse left.
                if (enemy != null && slot < _enemyMarks.Length)
                {
                    var staticOffset = FightStageAnchors.SlotOffset(slot, FightHudSpec.StageSlotsPerSide, mirrored: false);
                    var delta = _enemyMarks[slot] - new Vector2(staticOffset.X, staticOffset.Y);
                    RepositionEnemyStatusRow(slot, delta);
                }

                // AFTER the re-spread, so it starts from the row's correct
                // slot-X for this refresh rather than the stale one from
                // last refresh's enemy count.
                FitEnemyStatusRow(slot, rows.Count);

                PaintStatusRow(EnemyStatusBase + slot * EnemyStatusBadgesPerRow, EnemyStatusBadgesPerRow,
                    reserveOverflowSlot: true, enemy, rows, showCounter: true);
            }
        }

        // Section 3's fix: the build-time strip is always the full 196px
        // (4 badges + the "+N" chip, all five physical slots) because
        // UiAudit solves the screen once, before any encounter exists to
        // say how many statuses a given enemy will actually carry -- see
        // FightScreen.BuildEnemyStatusRows' own header, "the audited layout
        // is the full 5-node row and stays as is". At RUNTIME the strip
        // shrinks to however many badges this refresh actually shows,
        // centred on the same slot X the full-width row was built around
        // (read back from the strip's OWN current position, already
        // resolved by RepositionEnemyStatusRow above), so two badges on a
        // golem no longer leave the strip's other three slots' worth of
        // empty backing hanging off to their right.
        private void FitEnemyStatusRow(int slot, int rowCount)
        {
            if (!Has(enemyStatusStrips, slot)) return;

            int visible = rowCount <= EnemyStatusBadgesPerRow - 1
                ? rowCount
                : EnemyStatusBadgesPerRow; // the "+N" chip takes the row's last physical slot

            if (visible <= 0) return; // the strip is hidden -- nothing to size

            var stripRect = (RectTransform)enemyStatusStrips[slot].transform;
            float centreX = stripRect.anchoredPosition.x;

            float rowWidth = (visible - 1) * EnemyStatusPitch + EnemyStatusBadgeSize;
            stripRect.sizeDelta = new Vector2(rowWidth + EnemyStatusStripPadX * 2f, stripRect.sizeDelta.y);

            int first = slot * EnemyStatusBadgesPerRow;
            for (int i = 0; i < visible; i++)
            {
                int flat = first + i;
                if (!Has(enemyStatusBadges, flat)) continue;

                float x = centreX + (i - (visible - 1) * 0.5f) * EnemyStatusPitch;
                var badgeRect = (RectTransform)enemyStatusBadges[flat].transform;
                badgeRect.anchoredPosition = new Vector2(x, badgeRect.anchoredPosition.y);
            }
        }

        // From the CAPTURED build-time position, always -- never from
        // wherever the row is sitting right now. See
        // _enemyStatusStripHome/_enemyStatusBadgeHome's own comment.
        private void RepositionEnemyStatusRow(int slot, Vector2 delta)
        {
            if (Has(enemyStatusStrips, slot) && _enemyStatusStripHome != null && slot < _enemyStatusStripHome.Length)
            {
                ((RectTransform)enemyStatusStrips[slot].transform).anchoredPosition =
                    _enemyStatusStripHome[slot] + delta;
            }

            if (enemyStatusBadges == null || _enemyStatusBadgeHome == null) return;

            int first = slot * EnemyStatusBadgesPerRow;
            for (int i = 0; i < EnemyStatusBadgesPerRow; i++)
            {
                int flat = first + i;
                if (!Has(enemyStatusBadges, flat) || flat >= _enemyStatusBadgeHome.Length) continue;

                ((RectTransform)enemyStatusBadges[flat].transform).anchoredPosition =
                    _enemyStatusBadgeHome[flat] + delta;
            }
        }

        // ---- hover ----------------------------------------------------------------

        private void OnHoverStatusBadge(int flat, bool entered)
        {
            if (entered)
            {
                _hoveredStatusBadge = flat;
                ShowStatusTooltip(flat);
                return;
            }

            // Only the badge CURRENTLY showing the tooltip gets to close it.
            // Every refresh calls SetShown(false) on every badge with
            // nothing to draw, which fires HoverIndex.OnDisable
            // unconditionally (see its own comment) -- an unrelated badge
            // two slots over deactivating must not swallow the tooltip that
            // belongs to the one still under the pointer.
            if (flat == _hoveredStatusBadge)
            {
                _hoveredStatusBadge = -1;
                if (statusTooltip != null) statusTooltip.SetShown(false);
            }
        }

        // Re-validates the open tooltip against this refresh's freshly
        // painted text -- called once from RefreshUi, after all three
        // surfaces have repainted. Covers the case HoverIndex's own exit
        // event does not: a badge that STAYS active but now shows a
        // DIFFERENT row (a re-sort, an expiring status handing its slot to
        // the next one) fires no enter/exit at all, and a stale tooltip
        // would otherwise keep reading the previous occupant's text.
        private void RefreshHoveredStatusTooltip()
        {
            if (_hoveredStatusBadge >= 0) ShowStatusTooltip(_hoveredStatusBadge);
        }

        private void ShowStatusTooltip(int flat)
        {
            if (statusTooltip == null || _statusBadgeParts == null) return;
            if (flat < 0 || flat >= _statusBadgeTooltip.Length) return;

            string text = _statusBadgeTooltip[flat];
            if (string.IsNullOrEmpty(text))
            {
                // The row this badge was showing is gone -- hide, rather
                // than show whatever the same index now holds instead.
                _hoveredStatusBadge = -1;
                statusTooltip.SetShown(false);
                return;
            }

            if (statusTooltipText != null) statusTooltipText.SetContent(text);
            statusTooltip.SetShown(true);

            // Grows the ONE shared tooltip past its build-time one-line
            // height for the "+N" chip's multi-line body (section 6) --
            // TooltipFit.ToBody (Core/TooltipFit.cs), shared with
            // ReckoningController's identical offer-card version (S1's
            // review). MUST RUN BEFORE PlaceStatusTooltip, which reads
            // tooltipRect.rect.height to clamp inside the canvas -- sizing
            // after placing would place against the wrong box.
            var statusTooltipRect = statusTooltip == null ? null : statusTooltip.transform as RectTransform;
            TooltipFit.ToBody(statusTooltipRect, statusTooltipText,
                FightScreen.StatusTooltipWidth, FightScreen.StatusTooltipPad,
                FightScreen.StatusTooltipOneLineHeight, FightScreen.StatusTooltipMaxHeight);

            var anchor = flat < _statusBadgeParts.Length && _statusBadgeParts[flat].Root != null
                ? _statusBadgeParts[flat].Root.transform as RectTransform
                : null;
            PlaceStatusTooltip(flat, anchor);
        }

        // Which physical badge slots share a row with `flat`. Both surfaces
        // are now several independent rows of a fixed width -- the enemy
        // stage rows of EnemyStatusBadgesPerRow, the PC plates of
        // PcStatusBadgesPerPlate -- so this is two cases where it used to be
        // three, and the third (the party plate's one long double line) is
        // gone with the card that carried it. PlaceStatusTooltip needs this
        // to widen its anchor past the single hovered badge to the whole row
        // it sits in.
        private static void GetStatusRowRange(int flat, out int start, out int count)
        {
            if (flat < PcStatusBase)
            {
                int slot = flat / EnemyStatusBadgesPerRow;
                start = slot * EnemyStatusBadgesPerRow;
                count = EnemyStatusBadgesPerRow;
            }
            else
            {
                int plate = (flat - PcStatusBase) / PcStatusBadgesPerPlate;
                start = PcStatusBase + plate * PcStatusBadgesPerPlate;
                count = PcStatusBadgesPerPlate;
            }
        }

        // TooltipPlacement.Beside works in ONE local coordinate space; the
        // anchor and the tooltip do not share one here -- the enemy row
        // sits under the stage panel, a PC row under its own plate, and
        // StatusTooltip is a
        // top-level HUD child. The anchor's centre is carried through WORLD
        // space first, the one conversion that is correct regardless of how
        // deep the anchor happens to be nested.
        //
        // THE INTERIOR BOX IS tooltipRect.parent.rect ITSELF (S2's review
        // reverses the earlier workaround). The first capture's defect really
        // did look like "parent.rect (FightHud, a NestedCanvas meant to fill
        // the whole screen) is far smaller than the canvas", which is what
        // the original fix clamped against UiFrames.Reference instead to
        // dodge -- but the actual cause was reading the rect on the SAME
        // FRAME SetShown(true) ran, before layout had caught up to it, not
        // the rect itself being wrong. Forcing a canvas rebuild first
        // (immediately above) and then reading parent.rect measured (1920,
        // 1440) at a 4:3 capture -- matching the real canvas, not a
        // degenerate box -- so the fixed 1920x1080 reference frame is deleted
        // in favour of the actual one, which is only ever as-or-more generous
        // than the reference on any wider or taller real canvas.
        private void PlaceStatusTooltip(int flat, RectTransform anchor)
        {
            var tooltipRect = statusTooltip != null ? statusTooltip.transform as RectTransform : null;
            if (tooltipRect == null || anchor == null) return;

            var parent = tooltipRect.parent as RectTransform;
            if (parent == null) return;

            // FORCED BEFORE READING parent.rect (S2's review). Without this,
            // reading the rect on the same frame SetShown(true) ran measured
            // whatever the layout last happened to be, not what it is now --
            // the original bug this method's own header used to blame on
            // parent.rect itself. Measured directly: at a 4:3 capture,
            // parent.rect.size read (1920, 1440) once forced, matching the
            // real canvas rather than "far smaller" than it.
            Canvas.ForceUpdateCanvases();

            var canvas = _statusTooltipCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, anchor.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var anchorLocal))
            {
                return;
            }

            // Widen past the single hovered badge to the WHOLE row it shares
            // a parent with -- Beside only ever sees one anchor width, and
            // every one of the three surfaces places several badges under
            // one parent (the enemy strip, the PC plate
            // plate). Without this, flipping beside the first of several
            // badges lands the tooltip on top of the second rather than
            // beside the row (caught in this gate's own capture: the
            // Regen/Protect pair on the far enemy slot).
            GetStatusRowRange(flat, out int rowStart, out int rowCount);
            var rowParent = anchor.parent as RectTransform;
            float leftLocal = anchor.anchoredPosition.x - anchor.rect.width * 0.5f;
            float rightLocal = anchor.anchoredPosition.x + anchor.rect.width * 0.5f;
            float rowY = anchor.anchoredPosition.y;

            if (rowParent != null && _statusBadgeParts != null)
            {
                int end = Mathf.Min(rowStart + rowCount, _statusBadgeParts.Length);
                for (int i = rowStart; i < end; i++)
                {
                    var root = _statusBadgeParts[i].Root;
                    if (root == null || !root.activeSelf) continue;
                    var rt = root.transform as RectTransform;
                    if (rt == null || rt.parent != rowParent) continue;

                    float half = rt.rect.width * 0.5f;
                    leftLocal = Mathf.Min(leftLocal, rt.anchoredPosition.x - half);
                    rightLocal = Mathf.Max(rightLocal, rt.anchoredPosition.x + half);
                }
            }

            float anchorWidth = anchor.rect.width;
            float anchorXInParent = anchorLocal.x;

            if (rowParent != null && rightLocal > leftLocal)
            {
                Vector3 leftWorld = rowParent.TransformPoint(new Vector3(leftLocal, rowY, 0f));
                Vector3 rightWorld = rowParent.TransformPoint(new Vector3(rightLocal, rowY, 0f));

                Vector2 leftScreen = RectTransformUtility.WorldToScreenPoint(camera, leftWorld);
                Vector2 rightScreen = RectTransformUtility.WorldToScreenPoint(camera, rightWorld);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, leftScreen, camera, out var leftInParent) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, rightScreen, camera, out var rightInParent))
                {
                    anchorWidth = Mathf.Abs(rightInParent.x - leftInParent.x);
                    anchorXInParent = (leftInParent.x + rightInParent.x) * 0.5f;
                }
            }

            const float Margin = 8f;
            var interior = parent.rect;
            var at = TooltipPlacement.Beside(
                anchorXInParent, anchorLocal.y, anchorWidth,
                tooltipRect.rect.width, tooltipRect.rect.height,
                interiorLeft: interior.xMin + Margin, interiorRight: interior.xMax - Margin,
                interiorBottom: interior.yMin + Margin, interiorTop: interior.yMax - Margin);

            tooltipRect.anchoredPosition = new Vector2(at.X, at.Y);
        }

        // The ordinary chip tint, restored every refresh so a preview from a
        // moment ago can never leave a chip stuck highlighted after the
        // mouse has moved on.
        private static readonly Color InitiativeNormal = Color.white;

        // A push/pull skill's ghost preview -- which chip is amber. Set on
        // the icon AND the ring rather than a new node, because the only
        // thing changing is which slot a combatant now occupies, and every
        // chip already has a graphic to tint for that.
        private static readonly Color InitiativeGhost = Hex(FightHudPalette.TargetAmber);

        // WHICH MOMENT THE TRACKER IS DRAWING: the turn queue a beat recorded,
        // or null for "whatever is live". PaintFormation's twin for the row of
        // chips, set from the same place in playback and cleared at the same
        // one -- see FightBeatPlayer.PaintTurnOrder.
        //
        // THE BUG IT EXISTS FOR. A whole round -- the player's action and every
        // enemy reply -- resolves in one synchronous pass before a single beat
        // is drawn, and RefreshInitiative read Encounter.UpcomingTurns. So the
        // row showed the order the ROUND ENDED on from the moment the player
        // clicked: the monster that was about to swing had already dropped off
        // the front of the queue while its swing was still animating, and NOW
        // pointed at whoever acts after all of it. FightSession has recorded
        // the per-beat order since the beats existed (CombatBeat.TurnOrder) and
        // nothing outside its own tests had ever read it.
        private IReadOnlyList<CombatantState> _playingTurnOrder;

        internal void PaintTurnOrder(IReadOnlyList<CombatantState> turnOrder)
        {
            _playingTurnOrder = turnOrder;
            RefreshInitiative();
        }

        private void RefreshInitiative()
        {
            if (initiativeIcons == null || _session == null) return;

            var live = _session.IsOver
                ? new List<CombatantState>()
                : new List<CombatantState>(_session.Encounter.UpcomingTurns(initiativeIcons.Length));

            // The beat being played owns the row while one is; live state owns
            // it the rest of the time. CombatBeat.QueueToShow states the whole
            // of that rule, including what a beat that recorded nothing falls
            // back to.
            IReadOnlyList<CombatantState> upcoming = CombatBeat.QueueToShow(_playingTurnOrder, live);

            // THE GHOST PREVIEW. Only while a push-flagged skill is both
            // selected (Target depth) AND a specific enemy plate is under the
            // cursor -- QueuePushSlots only ever means anything on a
            // SingleEnemy skill (SkillEntryResolver's own rule), so there is
            // no target to preview a push against before one plate is
            // actually being pointed at. Nothing here commits anything: the
            // real queue is untouched, see TurnOrder.ProjectWith's own header.
            //
            // AND ONLY OVER THE LIVE QUEUE. A projection is a question about
            // what the player is ABOUT to do, so it has no meaning painted on
            // top of a beat that already happened -- and UpcomingTurnsPushed
            // answers from live state, which during playback is a different
            // round from the one on screen. IsTargeting alone covers that:
            // targeting is only entered through OnRowPressed, which CanAct
            // gates on !_isBusy, and _isBusy is true for the whole playback
            // window.
            var previewed = upcoming;
            int pushSlots = SelectedSkillPushSlots();
            if (pushSlots > 0 && _menu.IsTargeting && _hoveredEnemyIndex >= 0)
            {
                var enemies = Enemies;
                if (_hoveredEnemyIndex < enemies.Count && enemies[_hoveredEnemyIndex].IsAlive)
                {
                    previewed = new List<CombatantState>(_session.Encounter.UpcomingTurnsPushed(
                        enemies[_hoveredEnemyIndex], pushSlots, initiativeIcons.Length));
                }
            }

            for (int i = 0; i < initiativeIcons.Length; i++)
            {
                bool filled = i < previewed.Count;

                // THE LINE THE v2 PORT DROPPED. v1's tracker read as "who is
                // next" because every chip carried that combatant's own idle
                // pose -- v1 FightController.Hud.cs RefreshInitiativeTracker,
                // `icon.sprite = LoadStanceSprite(combatant, StanceIdle)`,
                // under a comment saying exactly that. v2 declared the Image
                // with a null sprite key (FightScreen.BuildInitiativeTracker)
                // and then never assigned one anywhere, and ShowSprite refuses
                // to switch on an Image with nothing to draw -- so no chip has
                // ever been visible in this tree, and the whole tracker
                // rendered as a row of bare capital letters.
                //
                // Read through StanceSpriteFor, the same lookup the enemy
                // plates already use one method up, so a chip and a plate
                // cannot start disagreeing about what a monster looks like.
                //
                // ASSIGNED EVEN WHEN NULL: a slot that showed real art last
                // repaint and resolves to nothing this one must drop the stale
                // drawing, or ShowSprite would happily keep painting the wrong
                // combatant.
                //
                // NEVER MIRRORED, deliberately -- the queue is a reading aid,
                // and a row where half the figures face left is harder to scan
                // than one where they all face the same way. v1 made the same
                // call in the same place.
                //
                // ONLY THE SPRITE. preserveAspect and raycastTarget are the
                // chip's shape and are declared on the tree
                // (FightScreen.BuildInitiativeTracker), not re-set per repaint.
                var art = filled ? StanceSpriteFor(previewed[i], FightSession.Stances.Idle) : null;
                if (initiativeIcons[i] != null) initiativeIcons[i].sprite = art;

                // The ring's art is baked by the tree (proc:ring_hairline,
                // FightScreen.BuildInitiativeTracker); this only decides which
                // slot shows it.
                ShowSprite(initiativeIcons[i], filled);
                ShowSprite(initiativeRings[i], filled && i == 0);

                // The initial, and ONLY as the fallback its own comment always
                // said it was. It is layered over the middle of the chip, so
                // now that a chip can actually carry art, writing the letter
                // unconditionally would stamp a capital over every portrait.
                // A combatant whose idle art is missing still gets one, which
                // is the case it was added for.
                initiativeLabels[i].SetContent(filled && art == null && previewed[i].Name.Length > 0
                    ? previewed[i].Name.Substring(0, 1).ToUpperInvariant()
                    : "");

                // CHANGED FROM THE UNPUSHED SCHEDULE, not "is the pushed
                // combatant" -- a push moves everyone charged less than it
                // down by one slot too, and the player benefits from seeing
                // the whole reshuffle, not just where the target landed.
                bool moved = ReferenceEquals(previewed, upcoming) == false
                    && (i >= upcoming.Count || !ReferenceEquals(upcoming[i], previewed[i]));
                var tint = moved ? InitiativeGhost : InitiativeNormal;
                if (initiativeIcons[i] != null) initiativeIcons[i].color = tint;
                if (initiativeRings[i] != null && i == 0) initiativeRings[i].color = tint;
            }
        }

        // The confirmed or hovered skill's QueuePushSlots, 0 for everything
        // else -- the same "look up whatever OnEnemyPressed would actually
        // cast" shape TargetingIsMelee/TargetingIsGroup already share, so a
        // fourth copy of that lookup does not quietly start disagreeing with
        // the first three about which skill is live.
        private int SelectedSkillPushSlots()
        {
            if (_menu.Branch != MenuBranch.Skill || _session?.Current == null) return 0;

            var options = SkillOptions(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return 0;

            return options[row].Skill.QueuePushSlots;
        }

        // ---- what the model needs -------------------------------------------

        // WHICH SUBMENU LIST IS OPEN, read off (Depth, Branch) once so
        // CurrentRows and the submenu title agree by construction rather than
        // by two copies of the same condition tree staying in sync. A fifth
        // depth or branch that needs its own submenu is one arm added here,
        // not one arm added in each of the two switches below.
        private enum SubmenuKind { Element, Item, Move, Skill }

        private SubmenuKind CurrentSubmenuKind()
        {
            if (_menu.Depth == MenuDepth.Element) return SubmenuKind.Element;
            if (_menu.Branch == MenuBranch.Item) return SubmenuKind.Item;
            if (_menu.Branch == MenuBranch.Move) return SubmenuKind.Move;
            return SubmenuKind.Skill;
        }

        private static UiString SubmenuTitleFor(SubmenuKind kind)
        {
            switch (kind)
            {
                case SubmenuKind.Element: return UiStrings.SubmenuElementTitle;
                case SubmenuKind.Item: return UiStrings.SubmenuItemsTitle;
                case SubmenuKind.Move: return UiStrings.SubmenuMoveTitle;
                default: return UiStrings.SubmenuSkillsTitle;
            }
        }

        private IReadOnlyList<SubmenuRow> CurrentRows()
        {
            switch (CurrentSubmenuKind())
            {
                case SubmenuKind.Element: return FightHudModel.ElementRows(SelectedSkill());
                // THE SAME POOL DetailPanelForSelection HANDS DetailForItem, ~90
                // lines below. Both readers describe one satchel stack to one
                // character, so they ask the one predicate the same question --
                // the row was the last of the three readers still asking it
                // without an actor, which is what let a mana potion advertise
                // MANA to a Fury holder and then be refused on press.
                case SubmenuKind.Item: return FightHudModel.ItemRows(_satchel, ActingCharacter()?.PrimaryPool);
                case SubmenuKind.Move: return FightHudModel.MoveRows(_session, ActingCharacter());
                default: return FightHudModel.SkillRows(SkillOptions(ActingCharacter()), ActingCharacter());
            }
        }

        // The skill the SUB row selection names, or null when the open branch
        // is not Skill or the selection points at nothing. The element list,
        // the element press and the detail card all need the same answer, and
        // this is the one place any of them asks for it.
        private Domain.Content.ResolvedSkill SelectedSkill()
        {
            if (_menu.Branch != MenuBranch.Skill || _session?.Current == null) return null;

            var options = SkillOptions(ActingCharacter());
            int row = _menu.Selection;
            return row >= 0 && row < options.Count ? options[row].Skill : null;
        }

        // THE SKILL AS IT WOULD ACTUALLY BE CAST RIGHT NOW: retyped to whatever
        // element is in hand, so the POWER row, the SCALES row and the damage
        // type all describe the click rather than the menu entry.
        //
        // Two elements can be "in hand": the one being HOVERED at Element depth
        // (nothing committed yet, the card previews it) and the one already
        // CHOSEN at Target depth. Neither is a different rule -- both are "the
        // element this cast would carry" -- so they resolve through one call.
        internal static Domain.Content.ResolvedSkill AsChosen(
            Domain.Content.ResolvedSkill skill, Domain.Combat.Session.FightMenuState menu)
        {
            if (skill == null || !skill.HasElementChoice) return skill;

            if (menu.ChosenElement.HasValue && skill.Offers(menu.ChosenElement.Value))
            {
                return skill.AsElement(menu.ChosenElement.Value);
            }

            int row = menu.ElementSelection;
            if (menu.Depth == Domain.Combat.Session.MenuDepth.Element
                && row >= 0 && row < skill.Elements.Length && skill.Elements[row] != null)
            {
                return skill.AsElement(skill.Elements[row].Type);
            }

            return skill;
        }

        // Same seam as HoveredEnemyIndexForTest/FocusedVerbForTest
        // (FightController.Input.cs) -- the click handler stays private
        // because production never calls it from outside an EventTrigger,
        // but a test driving the real submenu needs the same hover a mouse
        // would send without standing up pointer events for it.
        public void HoverRowForTest(int index) => OnRowHovered(index);

        // What Column C is actually showing right now, for a test to read
        // the same panel RefreshDetail paints from.
        public DetailPanel CurrentDetailForTest() => CurrentDetail();

        // The rows Column B is BUILT from, same door, same reason. The row's
        // Meta never reaches the scene -- the tree paints a mark and a name and
        // nothing else (FightScreen.BuildSubmenuRows) -- so the only way to
        // check that this branch asked about the right character's pool is to
        // read what the controller handed the view.
        public IReadOnlyList<SubmenuRow> CurrentRowsForTest() => CurrentRows();

        private DetailPanel CurrentDetail()
        {
            var actor = ActingCharacter();
            if (_menu.Branch == MenuBranch.Attack) return FightHudModel.DetailForStrike(actor);

            // THE ELEMENT LIST DESCRIBES THE SKILL, not the row. There is
            // nothing to say about "Fire" on its own; what the player is
            // weighing is the same cast with a different element on it, which
            // is exactly what AsChosen hands over.
            if (_menu.Depth == MenuDepth.Element)
            {
                var chosen = AsChosen(SelectedSkill(), _menu);
                return chosen == null
                    ? FightHudModel.DetailForNoSelection(_menu.Branch)
                    : FightHudModel.DetailForSkill(_session, actor, chosen, actor?.SignaturePool?.DisplayName);
            }

            var rows = CurrentRows();
            int index = _menu.Selection;

            // NOT DetailForStrike. Falling back to it here described the ATTACK
            // verb -- "Strike", "A plain swing at one enemy in reach" -- while
            // the SKILL list was open above it and the breadcrumb read
            // COMMAND > SKILL. A panel that is merely empty is honest; one that
            // confidently describes a different command is not.
            if (index < 0 || index >= rows.Count) return FightHudModel.DetailForNoSelection(_menu.Branch);

            if (_menu.Branch == MenuBranch.Item)
            {
                return FightHudModel.DetailForItem(_satchel[index], actor?.PrimaryPool);
            }

            // THROUGH THE OPTION'S OWN INDEX, not the row position: a row can
            // sit at a different position than its skill's kit.Skills slot the
            // moment any skill's Requirements aren't met (SkillOptionsFor
            // filters those out before FightHudModel builds rows from the
            // same sequence). FightController.Input.cs's CastSkill calls
            // already dispatch through options[row].Index for this reason --
            // reading kit.Skills[index] directly here would show a different
            // skill's detail than the row actually selected the moment one
            // exists.
            var options = _session != null ? SkillOptions(actor) : null;
            if (options != null && index < options.Count)
            {
                var skill = AsChosen(options[index].Skill, _menu);
                return FightHudModel.DetailForSkill(_session, actor, skill, actor?.SignaturePool?.DisplayName);
            }

            // Every skill-branch row now has a ResolvedSkill behind it --
            // the basic spell's row, which did not, is gone (docs/PLAN_SHOP.md
            // §4 Phase E). Reaching here means the index does not match an
            // option after all; the same empty panel DetailForNoSelection
            // already gives an out-of-range Column C selection.
            return FightHudModel.DetailForNoSelection(_menu.Branch);
        }

        // Whoever the plate is describing: the acting character while it is the
        // player's turn, and the first living party member otherwise, so the
        // plate never blanks mid-round while a monster is swinging.
        private CombatantState ActingCharacter()
        {
            if (_session == null) return null;
            if (_session.IsPlayerTurn && _session.Current != null) return _session.Current;

            foreach (var member in _session.Encounter.LivingPlayerParty) return member;
            return null;
        }

        // Test-only door, named ...ForTest per house convention
        // (FightBeatPlayer.WireStageForTest, FightController.StageShakesForTest)
        // since Core's InternalsVisibleTo names only the Editor assembly and a
        // PlayMode test sits outside that grant.
        //
        // WHY A TEST NEEDS IT AT ALL: with three fixed plates, WHICH one is
        // the acting one is a speed question the fixture does not control, so
        // PartyFormationCaptureTests has to ask rather than assume. Re-deriving
        // "who is acting" in the test would be a second copy of this method
        // and would pass on a controller that highlighted the wrong plate --
        // which is the bug it exists to find.
        public CombatantState ActingCharacterForTest() => ActingCharacter();

        // ---- small painting helpers -------------------------------------------

        // Shows an Image ONLY if it has something to draw.
        //
        // The rule this project keeps re-learning: an Image with no sprite does
        // not render as nothing, it renders as a SOLID WHITE QUAD. Several nodes
        // are deliberately built inactive with a null sprite and filled at
        // runtime, so "make it visible" and "it has art" are two conditions and
        // both have to hold. Activating on the first alone is what put six white
        // boxes across the top-left corner where the initiative portraits go.
        private static void ShowSprite(UnityEngine.UI.Image image, bool wanted)
        {
            if (image == null) return;

            bool visible = wanted && image.sprite != null;
            image.gameObject.SetShown(visible);
            image.enabled = visible;
        }

                private static void SetAlpha(TMPro.TMP_Text label, float alpha)
        {
            if (label == null) return;
            var colour = label.color;
            colour.a = alpha;
            label.color = colour;
        }

        // The fill is anchor-stretched at build time, so moving its RIGHT
        // ANCHOR is what makes the bar move.
        //
        // Not localScale, which was the obvious first answer and is wrong: a
        // stretched rect scales about its own centre, so a half-full bar would
        // shrink away from both ends and float in the middle of its track.
        // Not sizeDelta either, which fights the anchors rather than using them.
        private static void SetFill(UnityEngine.UI.Image fill, int current, int max)
        {
            if (fill == null) return;

            float fraction = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
            var rect = (RectTransform)fill.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(fraction, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
