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
            // RefreshStatusBox's own comment for why this cannot just live
            // inside one of them.
            RefreshStatusBox();
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
                // OPEN MEANS OPEN. It used to also mean "the pad's focus is
                // here", via `active < 0 && i == _focusedVerb`, and that one
                // clause is the whole of the owner's hardware finding:
                // "attack is always seeming to be hovered over (not a gamepad
                // bug) and makes it difficult to notice if you hover/select
                // it."
                //
                // _focusedVerb is 0 from the moment the controller wakes and
                // is never -1 -- Fight's model keeps a focused verb at all
                // times by design, because Submit has to have something to
                // press. So at rest, with no branch open, `highlighted` was
                // unconditionally true for index 0, and ATTACK wore the
                // branch-is-open plate before the player had touched
                // anything. The two states were never "mutually exclusive in
                // practice" as the old comment claimed; one of them was just
                // always on.
                //
                // Pad focus is the marker's job now (Core/FocusMarker.cs),
                // and this is a DRAWING change only: _focusedVerb, MoveFocus
                // and ConfirmFocus are untouched, and so are their direct-
                // call tests.
                //
                // AND INDEX 0 NO LONGER WEARS Primary EITHER (owner, 2026-09-19:
                // "the attack button in the fight menu is still glowing
                // always"). AUDIT.md #171 left this open as a design call
                // rather than a bug, because a recommended default is a real
                // thing to signal; the owner has made it. The resting column
                // shows no ring on any verb, and "ATTACK is the default" is
                // carried by the hotkey number beside it -- which costs no
                // glow, cannot be mistaken for focus or hover, and is already
                // on screen.
                //
                // THEMED VERBS drive ThemedButtonState.SetMenuState (through
                // ApplySelection) instead of targetGraphic.color -- Open or
                // Idle, painted on Glow/Plate instead of a flat Image tint.
                // Every verb is themed today, so there is no unthemed fallback
                // left to keep. Hover and press are ThemedButtonState's own,
                // driven by the pointer, and are not touched here at all.
                var verbState = i == active ? ThemedMenuState.Open : ThemedMenuState.Idle;
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

            // THE SKILL OPTION LIST, ONLY WHEN A ROW ACTUALLY NAMES A SKILL.
            // FightHudModel.SkillRows builds `rows` 1:1 off this exact list
            // (one SubmenuRow per option, in order, nothing filtered) so row
            // index i and options[i] agree without a second lookup -- see its
            // own header. Element/Item/Move rows are not skills (an element
            // choice, a satchel stack, a direction), so there is no skill id
            // for the loop below to paint a mark from, and leaving `options`
            // null there is what stops a mark painted at Skill depth from
            // surviving into the Element list the same rows get reused for
            // (the "one row pool serves both branches" pool BuildSubmenuColumn
            // describes) -- fixing bug 2026-09-19, "art for spell book is not
            // shown in the select spell screen".
            var options = CurrentSubmenuKind() == SubmenuKind.Skill ? SkillOptions(ActingCharacter()) : null;

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

                // THE ROW'S BOOK ART, painted from its skill id through the
                // same lookup-and-assign idiom every other icon in the game
                // uses (ItemIcons.Apply's own header). `options` is null
                // outside Skill depth, and Apply(_, _, null) already returns
                // false and disables the Image -- Find's own null-id guard --
                // so a stale mark from the Skill list cannot survive into the
                // Element list these same pooled nodes get reused for. A
                // skill with no authored iconPath (every non-book skill
                // today) disables the mark the identical way a gear card
                // with no art already does.
                if (submenuMarks != null && i < submenuMarks.Length)
                {
                    string skillId = options != null && i < options.Count ? options[i].Skill?.Id : null;
                    if (ItemIcons.Apply(submenuMarks[i], skillArt, skillId))
                    {
                        // Same DimmedAlpha channel the name already carries --
                        // an unaffordable row dims as one thing, not a mark
                        // that stays bright over a greyed-out name.
                        var markColor = submenuMarks[i].color;
                        markColor.a = row.Affordable ? 1f : DimmedAlpha;
                        submenuMarks[i].color = markColor;
                    }
                }

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
        //
        // 2026-09-23 ICON REWORK, PASS 2: paints from panel.Icons now, not
        // panel.Stats/.Body (see FightHudModel.DetailPanel's own header for
        // why both are still populated), AND resizes the card to fit --
        // "a dynamic box", not a static six-row reservation drawn small.
        // Every position/size below comes from FightScreen's own pure
        // Domain functions (DetailHFor, DetailCentreYFor, DetailFrameTopFor/
        // BottomFor, DetailNameYFor/KindYFor, DetailIconRowYFor) -- this
        // method's whole job is writing their output onto the RectTransforms
        // FightScreen built, the same "Domain computes, Core paints" split
        // FightController.AnchorSubmenuRows already follows for the submenu.
        private void RefreshDetail()
        {
            detailColumn.SetShown(_menu.DetailOpen);
            if (!_menu.DetailOpen) return;

            var panel = CurrentDetail();
            detailName.SetContent(panel.Name);
            // Kind plus the panel's Note -- a skill's or Strike's power, an
            // item's effect -- the facts the icon rows have no picture for.
            detailKind.SetContent(FightHudModel.DetailKindLine(panel));

            int shown = panel.Icons.Count;

            // THE HERO ICON (owner playtest, "the skill icon ... as the
            // card's hero, beside the title" -- FightScreen.DetailHeroIconSize's
            // own header): the Element row, when the current hover has one,
            // paints beside the title instead of in the row stack. It is
            // found by Kind, not a fixed index, because Icons is compact --
            // which pool slot holds it depends on which earlier facts (mana,
            // cost, cooldown) this particular skill happened to carry.
            int heroIndex = -1;
            for (int i = 0; i < panel.Icons.Count; i++)
            {
                if (panel.Icons[i].Kind == DetailIconKind.Element) { heroIndex = i; break; }
            }
            bool hasHero = heroIndex >= 0;

            // The row stack sizes/positions around this, not `shown` --
            // the hero does not reserve a row of its own (DetailListRowCountFor's
            // own header).
            int listRows = FightScreen.DetailListRowCountFor(shown, hasHero);
            ResizeDetailColumn(listRows);

            float nameLeft = FightScreen.DetailNameLeftFor(hasHero);
            float nameWidth = FightScreen.DetailNameWidthFor(hasHero);
            MoveTo(detailName.rectTransform, nameLeft, FightScreen.DetailNameYFor(listRows));
            SetWidth(detailName.rectTransform, nameWidth);
            MoveTo(detailKind.rectTransform, nameLeft, FightScreen.DetailKindYFor(listRows));
            SetWidth(detailKind.rectTransform, nameWidth);

            // `rowSlot` packs the non-hero rows tight, same as `i` used to
            // when every active icon occupied its own row -- the hero icon
            // (if any) is skipped here so the row beneath it does not leave
            // a gap where the hero used to sit in the old one-slot-per-row
            // layout.
            int rowSlot = 0;
            for (int i = 0; i < detailIconImages.Length; i++)
            {
                bool has = i < shown;
                bool isHero = has && i == heroIndex;

                if (detailIconImages[i] != null)
                {
                    if (has)
                    {
                        detailIconImages[i].gameObject.SetShown(true);
                        var iconRect = detailIconImages[i].rectTransform;
                        if (isHero)
                        {
                            SetSize(iconRect, FightScreen.DetailHeroIconSize);
                            MoveTo(iconRect, FightScreen.DetailHeroIconXFor(), FightScreen.DetailHeroIconYFor(listRows));
                        }
                        else
                        {
                            SetSize(iconRect, FightScreen.DetailIconSize);
                            MoveTo(iconRect, FightScreen.DetailContentLeft + FightScreen.DetailIconSize * 0.5f,
                                FightScreen.DetailIconRowYFor(listRows, rowSlot));
                        }

                        IconCache<string>.Apply(detailIconImages[i], DetailIconSpriteFor(panel.Icons[i]), disableOnMiss: true);
                    }
                    else
                    {
                        detailIconImages[i].gameObject.SetShown(false);
                    }
                }

                // NO VALUE LABEL FOR A ROW WITH NOTHING TO SAY -- Defense/
                // Reach/AreaOfEffect carry their fact through the icon alone
                // (FillDetailIcons' own "ONE ICON, COMPACT" header) and were
                // built with Value == "", but the label object used to stay
                // active anyway, an empty TMP_Text taking its place in the
                // row and reading as a bare icon adrift in blank space. The
                // icon is not "genuinely nothing" (it carries the fact by
                // itself) so the ROW stays; only the label that has nothing
                // to print is hidden. The hero icon never gets a value label
                // either -- its sprite alone identifies the skill.
                if (i < detailIconValues.Length && detailIconValues[i] != null)
                {
                    bool hasValue = has && !isHero && !string.IsNullOrEmpty(panel.Icons[i].Value);
                    detailIconValues[i].gameObject.SetShown(hasValue);
                    if (hasValue)
                    {
                        detailIconValues[i].SetContent(panel.Icons[i].Value);
                        MoveToY(detailIconValues[i].rectTransform, FightScreen.DetailIconRowYFor(listRows, rowSlot));
                    }
                }

                if (has && !isHero) rowSlot++;
            }
        }

        private static void SetWidth(RectTransform rect, float width)
        {
            if (rect == null) return;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static void SetSize(RectTransform rect, float size)
        {
            if (rect == null) return;
            rect.sizeDelta = new Vector2(size, size);
        }

        // THE FIVE PIECES THAT DRAW THE CARD'S OWN BOX, resized for
        // `listRows` (RIGHT rows only -- the hero icon does not reserve a
        // row of its own, FightScreen.DetailListRowCountFor's own header)
        // and repositioned so the card's BOTTOM EDGE NEVER MOVES
        // (FightScreen.DetailCentreYFor's own header) -- the outer column
        // (its rect is what the click-through/audit geometry and
        // detailColumn.SetShown above both reason about), the flat fill
        // (LOCAL position stays at the column's own origin -- both share one
        // centre, so only its OWN size has to change), and the four Rim
        // edges (Top/Bottom move, Left/Right resize -- Ui.Rim's own yield
        // order, see detailColumnRim's header).
        private void ResizeDetailColumn(int listRows)
        {
            float height = FightScreen.DetailHFor(listRows);

            var columnRect = detailColumn != null ? detailColumn.transform as RectTransform : null;
            if (columnRect != null)
            {
                columnRect.sizeDelta = new Vector2(columnRect.sizeDelta.x, height);
                MoveToY(columnRect, FightScreen.DetailCentreYFor(listRows));
            }

            if (detailColumnFill != null)
            {
                var rect = detailColumnFill.rectTransform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }

            if (detailColumnRim == null || detailColumnRim.Length < 4) return;

            float top = height * 0.5f - 0.5f;
            float bottom = -height * 0.5f + 0.5f;

            if (detailColumnRim[0] != null) MoveToY(detailColumnRim[0].rectTransform, top);
            if (detailColumnRim[1] != null) MoveToY(detailColumnRim[1].rectTransform, bottom);

            for (int i = 2; i <= 3; i++)
            {
                if (detailColumnRim[i] == null) continue;
                var rect = detailColumnRim[i].rectTransform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }
        }

        // detailIcons, keyed by the SAME "<slot>_<key>" string DetailIcon's
        // Kind+IconKey pair names -- one cache rather than a per-Kind
        // dictionary, since every icon (Mana, Cost, Cooldown, Element x11,
        // Defense x3, Reach x2, AreaOfEffect x2, Scaling x6) is a distinct
        // file under Resources/Icons/Ability/ and none of them share a key
        // across kinds. Same IconCache<TKind> StatusRowSprites already uses
        // (that class's own header).
        //
        // DetailIcon/DetailIconKind ARE NOT NESTED in FightHudModel --
        // FightHudModel.cs declares them at namespace scope (Domain.Combat.
        // Session), alongside the static FightHudModel class, not inside it.
        // `FightHudModel.DetailIcon` (CS0426) went unnoticed through this
        // session's own dotnet verification because the dotnet host only
        // compiles Domain -- this file is Core, Unity-only, and the Unity
        // gate is what actually caught it. Unqualified is correct, the same
        // way this file already reads DetailPanel unqualified.
        private static readonly IconCache<string> DetailIconSprites = new IconCache<string>();

        private static Sprite DetailIconSpriteFor(DetailIcon icon) =>
            DetailIconSprites.Resolve(DetailIconResourceKey(icon), DetailIconResourcePath);

        // ONE SPELLING, matched against tools/make_detail_card_icons.py's
        // own file names (element_fire.png, defense_magical.png, reach_
        // front.png, target_aoe.png, scale_str.png, resource_mana.png) --
        // FightHudModel.FillDetailIcons/ElementIconKey is the only writer of
        // a DetailIcon.IconKey, and this is the only reader, so the two
        // halves of this string cannot drift from each other without a
        // build error, only from the art on disk. A miss here (a twelfth
        // DamageType with no matching element_*.png yet) is applied through
        // IconCache.Apply(disableOnMiss: true), same as an enemy intent with
        // no art -- a blank slot beats a filled white rectangle (that
        // method's own header).
        private static string DetailIconResourceKey(DetailIcon icon) => icon.Kind switch
        {
            DetailIconKind.Mana => "resource_mana",
            DetailIconKind.Cost => "resource_cost",
            DetailIconKind.Cooldown => "resource_cooldown",
            DetailIconKind.Element => "element_" + icon.IconKey,
            DetailIconKind.Defense => "defense_" + icon.IconKey,
            DetailIconKind.Reach => "reach_" + icon.IconKey,
            DetailIconKind.AreaOfEffect => "target_" + icon.IconKey,
            DetailIconKind.Scaling => "scale_" + icon.IconKey,
            _ => "",
        };

        private static string DetailIconResourcePath(string key) => "Icons/Ability/" + key;

        private void RefreshTargetPrompt()
        {
            bool targeting = _menu.IsTargeting;
            targetPrompt.SetShown(targeting);
            if (!targeting) return;

            string name = CurrentDetail().Name;
            if (_menu.IsPickingAlly && _menu.RequiredPicks > 1)
            {
                // WHICH PRESS THIS IS, because nothing else on the rack says
                // so: the plates look the same for pick 1 and pick 2, and the
                // first press of a Palace Passage is otherwise indistinguish-
                // able from a press that did nothing (plan 1.12).
                targetPromptLabel.Set(UiStrings.TargetPromptAllyOfMany, name,
                    (_menu.PickCount + 1).ToString(), _menu.RequiredPicks.ToString());
            }
            else if (_menu.IsPickingAlly) targetPromptLabel.Set(UiStrings.TargetPromptAlly, name);
            else if (TargetingIsGroup()) targetPromptLabel.Set(UiStrings.TargetPromptGroup, name);
            else targetPromptLabel.Set(UiStrings.TargetPrompt, name);
        }

        // Whether the skill resolving at Target depth hits every enemy at
        // once, for the prompt's wording. Asked only once the ally pick has
        // been ruled out above, and reads AllEnemies by name rather than
        // "not SingleEnemy" -- which was the same thing while SingleEnemy and
        // AllEnemies were the only two targetings that reached Target depth,
        // and stopped being once SingleAlly did. The basic spell, which
        // carries no authored Targeting of its own, is always single-target.
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

        // ---- ward + ghost fill surfaces (health bar polish pass) --------------
        //
        // BOUND BY FIELD NAME off FightScreen's NodeRef lists of the same
        // name, exactly the mechanism FightController.cs's own header on
        // pcPlates describes for every other plate surface -- UiAutoBind
        // matches an Image[] to a list called the same thing with no
        // ScreenRegistry code needed. Declared here rather than in
        // FightController.cs because this pass's edit surface is this file;
        // partial classes make the physical file a filing choice, not a
        // binding one.
        [SerializeField] internal Image[] enemyPlateGhostFills;
        [SerializeField] internal Image[] enemyPlateWardFills;
        [SerializeField] internal Image[] pcGhostFills;
        [SerializeField] internal Image[] pcWardFills;

        // "+N" beside the HP value, TMP_Text like pcHpValues -- bound off
        // FightScreen.PcWardValues the same way every field above is bound
        // off its own PascalCase NodeRef list.
        [SerializeField] internal TMP_Text[] pcWardValues;

        // Cropped-to-the-head copies of whatever StanceSpriteFor hands back,
        // keyed by the SOURCE sprite so two enemies sharing one idle art
        // (two rats) share one crop rather than each repaint minting a new
        // Sprite object. Never cleared: the whole run's monster roster is a
        // small, content-bounded set, so the cache's ceiling is "one entry
        // per distinct enemy idle sprite this session has drawn", not
        // "one per repaint".
        private readonly Dictionary<Sprite, Sprite> _enemyIconHeadCrops = new Dictionary<Sprite, Sprite>();

        // THE CROP ITSELF -- the top PcPlateArt.EnemyIconHeadZoneFrac of the
        // sprite's own height, full width, as a NEW Sprite over the SAME
        // texture. Sprite.Create needs no Read/Write-enabled texture for
        // this: it references a sub-rect of the existing texture for
        // rendering, the same trick a spritesheet slicer uses, so nothing
        // about the source asset's import settings has to change.
        private Sprite EnemyIconHeadCrop(Sprite full)
        {
            if (full == null) return null;

            if (_enemyIconHeadCrops.TryGetValue(full, out var cropped) && cropped != null)
            {
                return cropped;
            }

            var rect = full.rect;
            float headHeight = Mathf.Max(1f, rect.height * PcPlateArt.EnemyIconHeadZoneFrac);

            // Texture space is Y-up from the BOTTOM of the rect, so the top
            // of the sprite is the last `headHeight` pixels of it.
            var cropRect = new Rect(rect.x, rect.yMax - headHeight, rect.width, headHeight);
            cropped = Sprite.Create(full.texture, cropRect, new Vector2(0.5f, 1f), full.pixelsPerUnit);

            _enemyIconHeadCrops[full] = cropped;
            return cropped;
        }

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

            // THE ENEMY RACK IS LIVE ONLY WHILE ENEMIES ARE BEING PICKED
            // (AUDIT #147). It used to be _menu.IsTargeting, which was the
            // same thing while there was only one rack to point at; with an
            // ally pick open, a live enemy plate is a button offering a cast
            // that would be refused, which is exactly the state `blocked`
            // exists to keep off the screen.
            bool picking = _menu.IsPickingEnemy;

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
                bool blocked = present && picking && _session != null
                    && !_session.CanReachEnemy(_session.Current, targetingReach, enemies[i]);

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
                    enemyPlates[i].interactable = picking && !blocked;

                    if (Has(enemyPlateNames, i))
                    {
                        enemyPlateNames[i].color = dressed ? EliteBossNameTint : EnemyNameNormal;
                    }
                }

                enemyPlateReticles[i].SetShown(present && picking);
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

                // THE FIGURE IS A TARGET ONLY WHILE ONE IS BEING CHOSEN, and
                // it is now also a MARKER PERCH while the pad is inspecting
                // -- shown but raycast-dead, so it is still not a rectangle
                // over the battlefield eating clicks meant for whatever is
                // behind it. ShowFigureTarget (FightController.Input.cs) is
                // the one place that split lives.
                if (Has(enemyHitAreas, i))
                {
                    ShowFigureTarget(enemyHitAreas[i], present && picking,
                        present && IsPerchedOn(enemies[i]));
                }

                if (!present) continue;

                var enemy = enemies[i];
                enemyPlateNames[i].SetContent(i < names.Count ? names[i] : enemy.Name);
                enemyPlateHps[i].Set(UiStrings.HealthValue, enemy.CurrentHealth, enemy.MaxHealth);
                SetFill(enemyPlateHpFills[i], enemy.CurrentHealth, enemy.MaxHealth);

                if (Has(enemyPlateGhostFills, i))
                {
                    EnsureGhostRoutineArray(ref _enemyGhostRoutines, enemyPlates.Length);
                    UpdateGhostFill(enemyPlateGhostFills[i], enemy.CurrentHealth, enemy.MaxHealth,
                        ref _enemyGhostRoutines[i]);
                }

                int enemyWard = StatusEffects.WardPoints(enemy);
                if (Has(enemyPlateWardFills, i))
                {
                    SetWardFill(enemyPlateWardFills[i], enemy.CurrentHealth, enemy.MaxHealth, enemyWard);
                }

                // EnemyPlateTagLine, not EnemyStatusLine -- BRK still wins the
                // slot when both are true (that method's own header), but a
                // shielded enemy with no BRK now gets its "+N" where BRK used
                // to leave the Tags node blank.
                enemyPlateTags[i].SetContent(FightHudModel.EnemyPlateTagLine(enemy, _session, enemyWard));

                // Only an elite or boss carries a BreakShield at all -- the
                // track stays hidden for everything else rather than showing
                // an always-full meter nobody can deplete.
                if (Has(enemyPlateBreakTracks, i))
                {
                    var shield = enemy.BreakShield;
                    enemyPlateBreakTracks[i].SetShown(shield != null);
                    if (shield != null) SetFill(enemyPlateBreakFills[i], shield.Current, shield.Max);
                }

                // The actor's own idle art, fitted into the plate -- but
                // CROPPED to its head zone first (PcPlateArt.
                // EnemyIconHeadZoneFrac's own header), not the whole stance
                // squashed into a 34x34 square. Reusing the stage sprite
                // rather than authoring plate icons is what makes the two
                // impossible to disagree; the crop is the one thing the
                // stage sprite does not already have to do for itself.
                if (Has(enemyPlateIcons, i))
                {
                    var art = StanceSpriteFor(enemy, FightSession.Stances.Idle);
                    var headArt = EnemyIconHeadCrop(art);
                    enemyPlateIcons[i].gameObject.SetShown(headArt != null);
                    if (headArt != null)
                    {
                        enemyPlateIcons[i].sprite = headArt;
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

            // THE ALLY PICK, resolved ONCE for the whole column rather than
            // once per plate -- the same "same question asked of the same
            // click for every plate" reasoning RefreshEnemyPlates' own
            // targetingReach carries.
            //
            // EligibleAllies, not "alive": a gift the caster cannot give
            // himself must read as unavailable on his own plate, and that
            // rule belongs to Domain (AllyTargeting) rather than to a second
            // copy of it here. An empty list is the answer for every state
            // that is not an open ally pick, which is what leaves the whole
            // column inert the rest of the time.
            bool picking = _menu.IsPickingAlly;
            var eligible = picking
                ? _session.EligibleAllies(_session.Current, SelectedSkill())
                : EmptyAllies;

            // EVERY FIGURE TARGET DOWN FIRST, then the loop below raises the
            // ones this pick accepts. The loop walks PLATES and the hit areas
            // are indexed by SLOT, so a slot whose occupant has fallen (or
            // who is on no plate at all) is never visited -- left to the
            // loop alone it would keep the last pick's rectangle live over
            // the battlefield, which is the blocker bug FightScreen.
            // BuildStage's own header records on the enemy side.
            if (partyHitAreas != null)
            {
                foreach (var area in partyHitAreas)
                {
                    if (area != null) area.gameObject.SetShown(false);
                }
            }

            for (int i = 0; i < pcPlates.Length; i++)
            {
                var member = party != null && i < party.Count ? party[i] : null;

                _plateOccupants[i] = member;

                // AN ALLY ALREADY PICKED READS AS UNPICKABLE, which is how a
                // two-pick cast shows the player what they have chosen so far
                // without a new node in the screen tree (plan 1.12's third
                // illegal case). HasPicked is false for every one-pick cast
                // ever made, so this line changes nothing for them.
                //
                // THE LIMITATION, stated rather than left to be noticed: the
                // first pick goes INERT rather than lighting up as "chosen".
                // A distinct chosen-marker is a fourth plate state and a
                // screen-tree change, which this milestone deliberately did
                // not take -- the prompt carries the count instead.
                bool pickable = member != null && eligible.Contains(member) && !_menu.HasPicked(member);
                RefreshPcPlate(i, member, member != null && member == acting, picking, pickable);
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
        private static readonly CombatantState[] EmptyAllies = new CombatantState[0];

        private void RefreshPcPlate(int i, CombatantState member, bool isActing,
            bool picking, bool pickable)
        {
            pcPlates[i].SetShown(member != null);

            // ---- the ally-pick states, mirroring RefreshEnemyPlates --------
            //
            // INTERACTABLE ONLY WHILE A PICK IS OPEN, which is stricter than
            // the enemy rack was before this pass and is now the rule on both
            // (see `picking` there). A plate that takes clicks outside a pick
            // is a button whose press does nothing.
            //
            // BLOCKED: on screen and in the pick, but not a candidate for
            // THIS cast -- a downed squadmate, or the caster's own plate
            // under a gift. Dimmed, reticle greyed and not interactable, the
            // same three things an out-of-reach enemy plate gets, so the
            // player learns one visual language rather than two.
            bool blocked = picking && !pickable;
            pcPlates[i].interactable = picking && pickable;

            if (Has(pcPlateReticles, i))
            {
                pcPlateReticles[i].SetShown(member != null && picking);

                var reticleImage = pcPlateReticles[i].GetComponent<Image>();
                if (reticleImage != null)
                {
                    var colour = reticleImage.color;
                    colour.a = blocked ? OutOfReachAlpha : 1f;
                    reticleImage.color = colour;
                }
            }

            // THE LEATHER IS WHAT DIMS, because the leather is the card: this
            // column has no separate frame Image the way the enemy plates do
            // (their own dim goes on enemyPlateFrames for exactly that
            // reason). Alpha only, so a blocked plate is still readable --
            // "dim, don't hide", the rule the unaffordable submenu rows and
            // the out-of-reach enemy plates both already follow.
            if (Has(pcPlateArts, i))
            {
                var art = pcPlateArts[i].color;
                art.a = blocked ? OutOfReachAlpha : 1f;
                pcPlateArts[i].color = art;
            }

            // THE FIGURE IS A TARGET ONLY WHILE ONE IS BEING CHOSEN -- and by
            // STAGE SLOT, not by plate index. The two are the same number on
            // the enemy side and are not here: a Move reorders the column
            // without moving anybody's fight-long slot.
            //
            // AND A MARKER PERCH WHILE THE PAD IS READING THIS ONE, on a
            // condition that deliberately does NOT include `pickable`:
            // that is "the open cast accepts this squadmate", a question only
            // a cast can ask. Reading somebody's buffs asks nothing of them,
            // so every LIVING member is a stop on the inspect ring
            // (FightController.Input.cs's InspectRing) and each needs its own
            // figure to stand on. Raycast-dead there -- see ShowFigureTarget.
            int slot = SlotIndexOf(member);
            if (Has(partyHitAreas, slot))
            {
                ShowFigureTarget(partyHitAreas[slot], picking && pickable, IsPerchedOn(member));
            }

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

            if (Has(pcGhostFills, i))
            {
                EnsureGhostRoutineArray(ref _pcGhostRoutines, pcPlates.Length);
                UpdateGhostFill(pcGhostFills[i], member.CurrentHealth, member.MaxHealth, ref _pcGhostRoutines[i]);
            }

            int pcWard = StatusEffects.WardPoints(member);
            if (Has(pcWardFills, i))
            {
                SetWardFill(pcWardFills[i], member.CurrentHealth, member.MaxHealth, pcWard);
            }
            if (Has(pcWardValues, i))
            {
                bool wardShown = pcWard > 0;
                pcWardValues[i].gameObject.SetShown(wardShown);
                if (wardShown) pcWardValues[i].SetContent($"+{pcWard}");
            }

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

            ApplyPlateIdentityStretch(i, kit);
        }

        // LEVELS 31 TO 40, ON THE PLATE: the line of words under the bars, the
        // rim round the leather, and the metal the head is struck in.
        //
        // READ LIVE OFF THE SAVE, unlike the Level printed beside the name --
        // which the kit carries precisely so a level-up mid-round cannot change
        // the plate before the beat that earned it has played. The identity
        // stretch is the opposite case and wants the opposite rule: it is
        // COLLECTED by hand, from the system menu, inside this fight, and the
        // whole point of a cosmetic reward is that it appears when you take it.
        // Nothing here touches a combat number, so there is no beat for it to
        // get ahead of.
        //
        // GRACEFUL ON EVERY MISS, like ApplyPlateIdentity above it: no kit, no
        // save, no such character on the roster, or nothing collected all
        // resolve to "hide the line and the rim", never to a half-painted
        // plate.
        private void ApplyPlateIdentityStretch(int i, PlayerKit kit)
        {
            var look = CharacterIdentity.LookFor(RosterCharacter(kit?.Id));

            if (Has(pcIdentityLines, i))
            {
                bool any = look.PlateWord.Length > 0;
                pcIdentityLines[i].gameObject.SetShown(any);
                if (any) pcIdentityLines[i].SetContent(look.PlateWord);
            }

            if (Has(pcPlateRims, i))
            {
                string rim = IdentityMetals.RimFor(look.RimMetal);
                pcPlateRims[i].gameObject.SetShown(rim != null);
                if (rim != null) pcPlateRims[i].color = Hex(rim);
            }

            // THE EMBOSS IS A TINT ON THE LEATHER ITSELF, and it is written
            // last on purpose: RefreshPcPlate has already set this Image's
            // ALPHA for the ally-pick dim, so this takes the colour it finds
            // and keeps its alpha rather than assigning a fresh opaque one.
            // Writing a flat colour here would light a blocked plate back up.
            //
            // WHITE IS "NO EMBOSS", not a colour -- Image.color multiplies, so
            // white is the identity and every metal below it darkens the strip
            // slightly toward its own hue.
            if (Has(pcPlateArts, i))
            {
                string emboss = IdentityMetals.EmbossFor(look.EmbossMetal);
                var tint = emboss != null ? Hex(emboss) : Color.white;
                tint.a = pcPlateArts[i].color.a;
                pcPlateArts[i].color = tint;
            }
        }

        // The save's own entry for a kit, or null. Resolved fresh per paint
        // rather than held: a slot load can replace the save, and this is
        // three lookups over a roster of a handful.
        private static Character RosterCharacter(string definitionId)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || string.IsNullOrEmpty(definitionId)) return null;

            return save.roster.FirstOrDefault(c => c != null && c.definitionId == definitionId);
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

        // ---- the ward segment ---------------------------------------------------
        //
        // A SECOND FILL ON THE SAME BAR, sized ward/maxHP and clamped, drawn
        // OVER the HP fill -- starting at the HP fill's own right edge when
        // there is empty track to draw into, and sliding LEFT to sit on top
        // of the fill itself once there is not (a full-health bar has no
        // empty track past 100%, which is exactly when a shield is most
        // often cast). StatusEffects.WardPoints is the pool every Ward
        // status (Magical Shield, the Fragile Lamb's Ward, every other
        // shield -- see WardTests) shares; this reads it fresh every repaint
        // the same way the HP fill reads CurrentHealth fresh, rather than
        // caching anything.
        //
        // PLAYTEST 2026-09-23: "shield is not shown on the health bar". Root
        // cause was this formula, not the call sites (both PC and enemy
        // plates already called this every repaint) -- endFrac was
        // Clamp01(hpFrac + wardFrac) while startFrac stayed hpFrac
        // unconditionally, so at full health (hpFrac already 1) endFrac
        // clamped to the SAME 1 and the segment's own width came out zero:
        // a real shield, rendering nothing. startFrac now reads
        // Clamp01(endFrac - wardFrac) -- the same hpFrac whenever endFrac
        // wasn't clamped (case A, "starts at the fill's right edge"), but
        // when endFrac WAS clamped to 1 it slides startFrac left by exactly
        // the clamped amount (case B, "over the fill"), so the segment's
        // width is always wardFrac and it is never silently zeroed.
        private static void SetWardFill(Image ward, int current, int max, int wardPoints)
        {
            if (ward == null) return;

            if (max <= 0 || wardPoints <= 0)
            {
                ward.gameObject.SetShown(false);
                return;
            }

            float hpFrac = Mathf.Clamp01(current / (float)max);
            float wardFrac = Mathf.Clamp01(wardPoints / (float)max);
            float endFrac = Mathf.Clamp01(hpFrac + wardFrac);
            float startFrac = Mathf.Clamp01(endFrac - wardFrac);

            bool visible = endFrac > startFrac;
            ward.gameObject.SetShown(visible);
            if (!visible) return;

            var rect = (RectTransform)ward.transform;
            rect.anchorMin = new Vector2(startFrac, 0f);
            rect.anchorMax = new Vector2(endFrac, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---- the "recent damage" ghost fill -------------------------------------
        //
        // A trailing fill that STAYS at the pre-hit fraction and eases down to
        // meet the live one over GhostFillDecaySeconds, so a hit reads as a
        // CHUNK taken out of the bar rather than the fill simply snapping to
        // its new, smaller size. The live HP fill always jumps immediately
        // (SetFill, unchanged) -- this is purely the afterimage behind it,
        // drawn UNDER the live fill in the node tree (see FightScreen's own
        // ordering) so the chunk reads as "what was just lost", not as a
        // preview of anything.
        //
        // A COROUTINE PER PLATE rather than a per-frame Update() entry
        // (compare RefreshPoolPulse's arithmetic-over-the-clock shape just
        // above): this is a one-shot ease that starts and stops with a hit
        // rather than a continuous idle loop, and every call site that would
        // drive an Update() tick for it (FightController.Input.cs) sits
        // outside this pass's edit surface. StartCoroutine needs nothing
        // from that file -- it is a MonoBehaviour method available on this
        // partial class already.
        private const float GhostFillDecaySeconds = 0.4f;

        private Coroutine[] _enemyGhostRoutines;
        private Coroutine[] _pcGhostRoutines;

        private static void EnsureGhostRoutineArray(ref Coroutine[] routines, int length)
        {
            if (routines == null || routines.Length != length)
            {
                routines = new Coroutine[length];
            }
        }

        // `shown` is read straight off the ghost Image's OWN current anchor
        // rather than tracked in a parallel field: whatever the ghost is
        // visually at right now (mid-ease from an earlier hit, or resting at
        // the live value) is the only honest starting point for a new one,
        // and a separate "last target" float would drift from it the moment
        // two hits landed close together.
        private void UpdateGhostFill(Image ghost, int current, int max, ref Coroutine routine)
        {
            if (ghost == null) return;

            float liveFraction = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
            float shown = ((RectTransform)ghost.transform).anchorMax.x;

            // A DROP: the ghost holds the higher, pre-hit value and eases
            // down to meet the new one. Restarted rather than queued if a
            // second hit lands before the first ease finishes -- the ghost
            // keeps easing from wherever it visually is toward the newest
            // target, which is what "recent damage" means for a flurry.
            //
            // TWO GUARDS, NOT ONE -- isActiveAndEnabled catches the ordinary
            // case (a plate hidden or a controller disabled), and
            // gameObject.scene.isLoaded catches the one FightTeardown
            // LifecycleTests actually exercises: FightBeatPlayer.OnDisable's
            // own teardown (AUDIT #106 -- see its own header) fires ONE
            // last RefreshUi from FightBeatPlayer.EndFight while the OLD
            // scene is unloading UNDERNEATH a "FightPanel" that Unity has
            // not flagged activeInHierarchy=false for yet (destruction, not
            // SetActive(false), is what is happening to it) -- so
            // isActiveAndEnabled alone still reads true right here.
            // Scene.isLoaded flips the moment the unload STARTS, which is
            // the earlier and, for this exact race, the correct signal.
            // Unity refuses StartCoroutine on either state and logs an
            // error the teardown tests do not expect; snapping straight to
            // the live value is the graceful-degradation answer this house
            // already gives everywhere else: no ease to show, no player
            // watching it.
            if (liveFraction < shown - 0.0005f)
            {
                if (!isActiveAndEnabled || !gameObject.scene.isLoaded)
                {
                    if (routine != null) routine = null;
                    SetFillFraction(ghost, liveFraction);
                    return;
                }

                if (routine != null) StopCoroutine(routine);
                routine = StartCoroutine(EaseGhostFill(ghost, shown, liveFraction));
                return;
            }

            // A HEAL, or the first paint of a fresh occupant: nothing to
            // trail, so the ghost simply tracks the live value with no lag.
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            SetFillFraction(ghost, liveFraction);
        }

        private IEnumerator EaseGhostFill(Image ghost, float from, float to)
        {
            float elapsed = 0f;

            while (elapsed < GhostFillDecaySeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / GhostFillDecaySeconds);

                // Ease OUT: fast at first (the chunk reads immediately),
                // settling into the new level rather than arriving at a
                // constant rate.
                float eased = 1f - (1f - t) * (1f - t);
                SetFillFraction(ghost, Mathf.Lerp(from, to, eased));

                yield return null;
            }

            SetFillFraction(ghost, to);
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

        // ---- the appearance pop (PLAN_STATUS_EFFECT_UI.md section 6, package C) --
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

        // _statusBadgeParts, _statusBadgeCode, _statusBadgeHolder,
        // _statusRowActiveCodes, _statusRowScratchCodes,
        // _statusBadgePopRoutines, _inspectedActor,
        // _inspectedCode, _statusBoxCanvas, _enemyStatusStripHome and
        // _enemyStatusBadgeHome all moved to the root FightController.cs
        // (S9's review) -- see them there for what each one is and why.

        private void WireAllStatusBadges()
        {
            _statusBadgeParts = new StatusBadgeParts[TotalStatusBadges];

            WireStatusBadgeGroup(enemyStatusBadges, EnemyStatusBase);
            WireStatusBadgeGroup(pcStatusBadges, PcStatusBase);
            CaptureEnemyStatusRowHomePositions();

            // CACHED ONCE HERE (S8's review), not re-fetched by
            // PlaceStatusBox on every single hover -- the ancestor chain a
            // GetComponentInParent walk climbs never changes after the scene
            // is built, so paying that walk per hover bought nothing a
            // one-time lookup at wiring time doesn't already have.
            _statusBoxCanvas = statusBox != null ? statusBox.GetComponentInParent<Canvas>() : null;

            if (statusBox != null) statusBox.SetShown(false);
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

                // WHO THIS SLOT BELONGS TO, written for every slot in range
                // including the ones about to be hidden: a badge that goes
                // out while the pointer is on it fires HoverIndex.OnDisable,
                // and the exit path has to be able to name the same actor the
                // enter path did or it cannot tell whether the box it is
                // closing is still its own.
                _statusBadgeHolder[flat] = holder;

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
                    _statusBadgeCode[flat] = null;
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

            _statusBadgeCode[flat] = row.Code;

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

        // Section 6's whole overflow feature: the chip carries no polarity of
        // its own (it is a mixed bag by definition). What hovering it does is
        // no longer special -- it opens the status box, which lists every
        // entry on this actor whether the row had a slot for it or not, so
        // the chip's old job of carrying the hidden ones in a tooltip of its
        // own is gone with the tooltip.
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

            // No single status to emphasise: the chip stands for several, so
            // the box it opens highlights none of its rows.
            _statusBadgeCode[flat] = null;
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

        // ---- inspection: one box, one actor ---------------------------------------
        //
        // WHO IS BEING ASKED ABOUT is the whole model. Every surface that can
        // name one combatant answers that question and nothing else: a status
        // badge (on a plate, or under a figure), an enemy plate, the intent
        // icon standing over a monster's head, a PC plate, and -- with no
        // pointer at all -- the pad's own focus. None of them owns a tooltip
        // of its own any more; they set _inspectedActor and this file draws
        // one box under whoever that is.
        //
        // WHY THE OLD SHAPE COULD NOT BE GROWN INTO THIS ONE. The tooltip was
        // keyed on a BADGE: it showed that badge's line, it was placed beside
        // that badge, and a surface with no badge (a figure, a plate, a pad
        // focus) had nothing to open. Keying on the ACTOR instead is what
        // makes every one of those surfaces an answer to the same question
        // rather than four features.

        // A pointer arriving on, or leaving, something that names an actor.
        // `code` is the one status the surface points at, or null when it
        // points at the whole actor.
        //
        // ONLY THE SURFACE CURRENTLY HOLDING THE BOX MAY CLOSE IT -- see
        // InspectSource for why that is keyed on the surface and not on the
        // actor. Every refresh also calls SetShown(false) on every badge with
        // nothing to draw, which fires HoverIndex.OnDisable unconditionally
        // (see its own comment), so a badge two slots over going out must not
        // swallow the box that belongs to whatever is still under the
        // pointer.
        private void SetInspected(CombatantState actor, bool entered, string code,
            InspectSource from, int index)
        {
            if (entered)
            {
                if (actor == null) return;
                _inspectedActor = actor;
                _inspectedCode = code;
                _inspectedFrom = from;
                _inspectedFromIndex = index;
                RefreshStatusBox();
                return;
            }

            if (_inspectedFrom != from || _inspectedFromIndex != index) return;

            _inspectedActor = null;
            _inspectedCode = null;
            _inspectedFrom = InspectSource.None;
            _inspectedFromIndex = -1;
            RefreshStatusBox();
        }

        // Every status surface's own entry point: the badge knows its flat
        // slot, and the slot knows both who it belongs to and which status it
        // is showing, because PaintStatusRow wrote both down.
        private void OnHoverStatusBadge(int flat, bool entered)
        {
            if (flat < 0 || flat >= _statusBadgeHolder.Length) return;

            SetInspected(_statusBadgeHolder[flat], entered, _statusBadgeCode[flat],
                InspectSource.StatusBadge, flat);
        }

        // The figure's own hover, from the stage: the intent icon over a
        // monster's head is the only always-live node standing on an enemy
        // (the hit areas come up ONLY while a target is being picked -- see
        // RefreshEnemyPlates), so it is what "hovering the mob" reaches.
        internal void InspectEnemyAt(int index, bool entered)
        {
            var enemies = Enemies;
            var enemy = index >= 0 && index < enemies.Count ? enemies[index] : null;
            SetInspected(enemy, entered, null, InspectSource.Enemy, index);
        }

        internal void InspectAllyOnPlate(int plate, bool entered) =>
            SetInspected(PartyMemberOnPlate(plate), entered, null, InspectSource.AllyPlate, plate);

        // Called once from RefreshUi, after all three status surfaces have
        // repainted, AND directly by every hover that opens or closes the
        // box. The refresh call is what covers the case a hover event cannot:
        // a status expiring, or a new one landing, while the box is already
        // open fires no enter or exit at all, and the box would otherwise go
        // on reading the list it was opened with.
        private void RefreshStatusBox()
        {
            if (statusBox == null) return;

            // THE POINTER WINS OVER THE PAD, and only because it is the more
            // recent deliberate act: a player holding a mouse over a monster
            // has said which one they mean, while the pad's focus is wherever
            // the last stick press left it and is never absent.
            var actor = _inspectedActor ?? FocusedActor();

            var rows = actor != null && _session != null
                ? FightHudModel.StatusRowsFor(_session, actor)
                : null;

            // AN EMPTY BOX IS NOT SHOWN AT ALL, the same rule the badge rows
            // follow (PLAN_STATUS_EFFECT_UI section 7): an actor with no
            // statuses has no answer to give, and an empty violet panel under
            // a monster reads as a fault rather than as "nothing here".
            if (rows == null || rows.Count == 0)
            {
                statusBox.SetShown(false);
                return;
            }

            // SHOWN BEFORE MEASURING. TMP measures a mesh it has actually
            // laid out, and the box spends most of its life inactive -- the
            // tooltip this replaced learned the same lesson one layer up (see
            // PlaceStatusBox's forced canvas update).
            statusBox.SetShown(true);

            // The badge's own status is only highlighted while the POINTER is
            // what opened the box: the pad names an actor, never a row.
            float height = PaintStatusBox(rows, _inspectedActor != null ? _inspectedCode : null);

            PlaceStatusBox(actor, height);
        }

        // The row the pointer's own badge names, against every other row.
        // ONE extra colour rather than a second backing panel per row: the
        // box is already a list on a dark ground, and a highlight that has to
        // survive eight rows of it is better spent on the text than on more
        // furniture (PLAN_STATUS_EFFECT_UI section 7's sludge budget).
        private static readonly Color StatusBoxRowText = Hex(FightHudPalette.TextPrimary);
        private static readonly Color StatusBoxRowHighlight = Hex(FightHudPalette.TargetAmber);

        // Fills the box from ONE actor's rows and returns the height it now
        // needs. Rows are MEASURED, never assumed: a row is one line until
        // its own text does not fit one, and the box is the sum of what its
        // rows actually took.
        private float PaintStatusBox(IReadOnlyList<FightHudModel.StatusRow> rows, string highlightCode)
        {
            int slots = statusBoxTexts != null ? statusBoxTexts.Length : 0;
            if (slots == 0) return FightScreen.StatusBoxMaxHeight;

            slots = Mathf.Min(slots, _statusBoxRowHeights.Length);

            // THE LAST SLOT BECOMES "+N more" when the list is longer than
            // the box has rows -- the same overflow rule the badge rows
            // already use, so a player never has to learn a second one. It is
            // reachable only past section 6's counted worst case of eight.
            int overflowRow = rows.Count > slots ? slots - 1 : -1;
            int shown = Mathf.Min(rows.Count, slots);

            float used = 0f;

            for (int i = 0; i < slots; i++)
            {
                var text = Has(statusBoxTexts, i) ? statusBoxTexts[i] : null;
                var icon = Has(statusBoxIcons, i) ? statusBoxIcons[i] : null;

                if (i >= shown)
                {
                    if (text != null) text.gameObject.SetShown(false);
                    if (icon != null) icon.gameObject.SetShown(false);
                    _statusBoxRowHeights[i] = 0f;
                    continue;
                }

                bool isOverflow = i == overflowRow;
                var row = rows[i];

                float height = FightScreen.StatusBoxRowMinHeight;

                if (text != null)
                {
                    text.gameObject.SetShown(true);
                    text.SetContent(isOverflow ? $"+{rows.Count - overflowRow} more" : row.Tooltip);
                    text.color = !isOverflow && highlightCode != null && highlightCode == row.Code
                        ? StatusBoxRowHighlight
                        : StatusBoxRowText;

                    // GetPreferredValues, not preferredHeight, for the reason
                    // Core/TooltipFit.cs states: the latter answers for the
                    // last laid-out mesh, and the body was set one line ago.
                    float wanted = text.GetPreferredValues(text.text, FightScreen.StatusBoxTextWidth, 0f).y;
                    height = Mathf.Clamp(wanted, FightScreen.StatusBoxRowMinHeight,
                        FightScreen.StatusBoxRowMaxHeight);

                    text.rectTransform.sizeDelta = new Vector2(FightScreen.StatusBoxTextWidth, height);
                }

                // The overflow row stands for several statuses, so it gets no
                // glyph -- exactly what the "+N" chip does on the badge rows.
                var art = isOverflow ? null : StatusSpriteFor(row);
                if (icon != null)
                {
                    icon.gameObject.SetShown(true);
                    icon.sprite = art;

                    // Disabled rather than left sprite-less: an Image with no
                    // sprite renders as a solid white quad (ItemIcons' own
                    // rule, CODE_STANDARDS section 2).
                    icon.enabled = art != null;
                }

                _statusBoxRowHeights[i] = height;
                used += height + (i > 0 ? FightScreen.StatusBoxRowGap : 0f);
            }

            float boxHeight = FightScreen.StatusBoxPad * 2f + used;

            var boxRect = statusBox.transform as RectTransform;
            if (boxRect != null)
            {
                boxRect.sizeDelta = new Vector2(FightScreen.StatusBoxWidth, boxHeight);
            }

            // SECOND PASS, because a row's centre is measured from the box's
            // own height and the height is not known until every row has been
            // measured. Both passes -- and the build's own stacking in
            // FightScreen.BuildStatusBox -- go through StatusBoxRowCentreY,
            // so there is one statement of where row i sits.
            float above = 0f;
            for (int i = 0; i < shown; i++)
            {
                float height = _statusBoxRowHeights[i];
                float centre = FightScreen.StatusBoxRowCentreY(boxHeight, above, height);

                if (Has(statusBoxTexts, i))
                {
                    var rect = statusBoxTexts[i].rectTransform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, centre);
                }

                if (Has(statusBoxIcons, i))
                {
                    var rect = statusBoxIcons[i].rectTransform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, centre);
                }

                above += height + FightScreen.StatusBoxRowGap;
            }

            return boxHeight;
        }

        // UNDER THE ACTOR IT DESCRIBES -- the arithmetic is
        // FightScreen.StatusBoxAt (pure, EditMode-tested against this
        // screen's real geometry); what lives here is only the measuring.
        //
        // THREE RECTS DO NOT SHARE ONE LOCAL SPACE. A stage slot is several
        // levels down and carries a WithScale, its badge strip is a child of
        // the stage panel, and the box is a top-level HUD child. Every span
        // below is therefore carried through WORLD space into the box's own
        // parent, the one conversion that is correct whatever depth the rect
        // happens to sit at -- the same route the tooltip this replaced took
        // for the same reason.
        private void PlaceStatusBox(CombatantState actor, float boxHeight)
        {
            var boxRect = statusBox != null ? statusBox.transform as RectTransform : null;
            var parent = boxRect != null ? boxRect.parent as RectTransform : null;
            var slot = SlotFor(actor);
            if (boxRect == null || parent == null || slot == null) return;

            // FORCED BEFORE READING ANY RECT. Reading parent.rect on the same
            // frame SetShown(true) ran measures whatever the layout last
            // happened to be rather than what it is now -- the defect
            // the tooltip this replaced recorded, and it applies twice
            // as hard here because the sizeDelta this reads back was written
            // a few lines ago.
            Canvas.ForceUpdateCanvases();

            var canvas = _statusBoxCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!SpanInParent(slot, parent, camera, out float centreX, out float bottom, out float top))
            {
                return;
            }

            // AN ENEMY'S BADGE ROW HANGS BELOW ITS FIGURE and is not a child
            // of it (FightScreen.EnemyStatusRowDrop, 60 below the ground
            // line), so a box placed off the figure alone would land on top
            // of the very badges it is expanding. The party's badges are on
            // its plates instead, so there is nothing to extend past there --
            // one rule, with the side's own geometry answering it.
            var strip = EnemyStatusStripFor(actor);
            if (strip != null && strip.activeInHierarchy
                && SpanInParent(strip.transform as RectTransform, parent, camera,
                    out _, out float stripBottom, out _))
            {
                bottom = Mathf.Min(bottom, stripBottom);
            }

            const float Margin = 8f;
            var interior = parent.rect;

            var at = FightScreen.StatusBoxAt(centreX, bottom, top,
                boxRect.rect.width, boxHeight,
                interiorLeft: interior.xMin + Margin, interiorRight: interior.xMax - Margin,
                interiorBottom: interior.yMin + Margin, interiorTop: interior.yMax - Margin);

            boxRect.anchoredPosition = new Vector2(at.X, at.Y);
        }

        // The backing strip under an ENEMY's badge row, or null for anyone
        // else -- the party has no strip by design (its badges sit on painted
        // plates, PLAN_STATUS_EFFECT_UI section 7).
        private GameObject EnemyStatusStripFor(CombatantState actor)
        {
            if (actor == null || actor.IsPlayerSide || enemyStatusStrips == null) return null;

            int slot = SlotIndexOf(actor);
            return slot >= 0 && slot < enemyStatusStrips.Length ? enemyStatusStrips[slot] : null;
        }

        // One rect's centre-x, bottom and top, in `parent`'s local space.
        private static bool SpanInParent(RectTransform rect, RectTransform parent, Camera camera,
                                         out float centreX, out float bottom, out float top)
        {
            centreX = 0f;
            bottom = 0f;
            top = 0f;
            if (rect == null || parent == null) return false;

            var box = rect.rect;
            Vector3 lowWorld = rect.TransformPoint(new Vector3(box.center.x, box.yMin, 0f));
            Vector3 highWorld = rect.TransformPoint(new Vector3(box.center.x, box.yMax, 0f));

            Vector2 lowScreen = RectTransformUtility.WorldToScreenPoint(camera, lowWorld);
            Vector2 highScreen = RectTransformUtility.WorldToScreenPoint(camera, highWorld);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, lowScreen, camera, out var low) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, highScreen, camera, out var high))
            {
                return false;
            }

            centreX = (low.x + high.x) * 0.5f;
            bottom = Mathf.Min(low.y, high.y);
            top = Mathf.Max(low.y, high.y);
            return true;
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
            if (pushSlots > 0 && _menu.IsPickingEnemy && _hoveredEnemyIndex >= 0)
            {
                var enemies = Enemies;
                if (_hoveredEnemyIndex < enemies.Count && enemies[_hoveredEnemyIndex].IsAlive)
                {
                    previewed = new List<CombatantState>(_session.Encounter.UpcomingTurnsPushed(
                        enemies[_hoveredEnemyIndex], pushSlots, initiativeIcons.Length));
                }
            }

            // THE SAME GHOST ON THE ALLY RACK, for an advance (plan 1.9,
            // 2.7). An advance is the one thing in this game whose entire
            // effect IS the tracker, so a player who cannot see where the
            // cast would land before committing it is being asked to buy
            // something invisible.
            //
            // ONE HOVERED ALLY, the same gate the push side uses: advanceSlots
            // only means anything on a SingleAlly skill, so there is nothing
            // to preview until a plate is actually being pointed at. Nothing
            // here commits anything -- ProjectPulled runs on a Snapshot (plan
            // 1.13).
            int advanceSlots = SelectedSkillAdvanceSlots();
            if (advanceSlots > 0 && _menu.IsPickingAlly && _hoveredAllyIndex >= 0)
            {
                var hovered = PartyMemberOnPlate(_hoveredAllyIndex);
                if (hovered != null && hovered.IsAlive)
                {
                    previewed = new List<CombatantState>(_session.Encounter.UpcomingTurnsPulled(
                        hovered, advanceSlots, initiativeIcons.Length));
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

        // The ally-side twin of SelectedSkillPushSlots, read the same way off
        // the same selected row so the two ghosts cannot disagree about which
        // skill the player is holding.
        private int SelectedSkillAdvanceSlots()
        {
            if (_menu.Branch != MenuBranch.Skill || _session?.Current == null) return 0;

            var options = SkillOptions(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return 0;

            return options[row].Skill.AdvanceSlots;
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
        // nothing else (FightScreen.BuildSubmenuColumn) -- so the only way to
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
            SetFillFraction(fill, fraction);
        }

        // THE RAW HALF OF SetFill ABOVE, split out so the ghost fill (which
        // has no current/max of its own -- it eases toward a FRACTION, not a
        // pair of live numbers) can share the same anchor-stretch mechanics
        // rather than a second copy of them.
        private static void SetFillFraction(UnityEngine.UI.Image fill, float fraction)
        {
            if (fill == null) return;

            var rect = (RectTransform)fill.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
