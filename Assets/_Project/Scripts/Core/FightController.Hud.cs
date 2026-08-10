using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

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
        private static readonly Color VerbActive = Hex(FightHudPalette.PanelActive);
        private static readonly Color VerbIdlePrimary = Hex(FightHudPalette.PanelPrimary);
        private static readonly Color RowIdle = Hex(FightHudPalette.RowQuiet);
        private static readonly Color RowSelected = Hex(FightHudPalette.PanelActive);
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

            RefreshVerbs();
            RefreshSubmenu();
            RefreshDetail();
            RefreshTargetPrompt();
            RefreshEnemyPlates();
            RefreshPartyPlate();
            RefreshInitiative();
            RefreshStage();
        }

        private void RefreshVerbs()
        {
            if (verbButtons == null) return;

            int active = _menu.ActiveVerbIndex;
            for (int i = 0; i < verbButtons.Length; i++)
            {
                var image = verbButtons[i] == null ? null : verbButtons[i].targetGraphic as UnityEngine.UI.Image;
                if (image != null)
                {
                    image.color = i == active ? VerbActive
                        : i == 0 ? VerbIdlePrimary
                        : RowIdle;
                }

                // Only the two nesting verbs have a live caret; the others were
                // built with theirs deactivated and it stays that way.
                if (verbCarets != null && i < verbCarets.Length && verbCarets[i] != null)
                {
                    var caret = verbCarets[i].GetComponent<TMPro.TMP_Text>();
                    if (caret != null) caret.color = i == active ? CaretActive : CostUnaffordable;
                }
            }

            breadcrumb.SetContent(FightHudModel.Breadcrumb(_menu));
        }

        // Column B, for whichever branch is open.
        private void RefreshSubmenu()
        {
            SetActive(submenuColumn, _menu.SubmenuOpen);
            if (!_menu.SubmenuOpen || submenuRows == null) return;

            var rows = CurrentRows();

            submenuTitle.Set(_menu.Branch == MenuBranch.Item
                ? UiStrings.SubmenuItemsTitle
                : UiStrings.SubmenuSkillsTitle);
            submenuHint.Set(UiStrings.SubmenuHint);

            // Through the SAME layout function the build used, which is the
            // whole reason FightSubmenuLayout exists in Domain.
            AnchorSubmenuRows(rows.Count);

            for (int i = 0; i < submenuRows.Length; i++)
            {
                if (i >= rows.Count) continue;

                var row = rows[i];
                submenuNames[i].SetContent(row.Name);
                submenuMetas[i].SetContent(row.Meta);
                submenuCosts[i].SetContent(row.Cost);
                submenuCosts[i].color = row.Affordable ? CostAffordable : CostUnaffordable;

                var frame = submenuRows[i].targetGraphic as UnityEngine.UI.Image;
                if (frame != null) frame.color = i == _menu.Selection ? RowSelected : RowIdle;

                submenuRows[i].interactable = row.Affordable;

                float alpha = row.Affordable ? 1f : DimmedAlpha;
                SetAlpha(submenuNames[i], alpha);
                SetAlpha(submenuMetas[i], alpha);
            }

            // Never a silent truncation: a kit that quietly stops showing its
            // last entry reads as a skill that was taken away.
            if (rows.Count > submenuRows.Length && _session != null)
            {
                _session.AppendMessage(
                    $"{rows.Count - submenuRows.Length} more entr(y/ies) than the column can show.");
            }
        }

        // Column C. Describes whatever the submenu has selected, or the
        // synthetic Strike entry when the branch is ATTACK.
        private void RefreshDetail()
        {
            SetActive(detailColumn, _menu.DetailOpen);
            if (!_menu.DetailOpen) return;

            var panel = CurrentDetail();
            detailName.SetContent(panel.Name);
            detailKind.SetContent(panel.Kind);
            detailBody.SetContent(panel.Body);

            for (int i = 0; i < detailStatValues.Length; i++)
            {
                detailStatValues[i].SetContent(i < panel.Stats.Count ? panel.Stats[i].Value : "");
            }
        }

        private void RefreshTargetPrompt()
        {
            bool targeting = _menu.IsTargeting;
            SetActive(targetPrompt, targeting);
            if (!targeting) return;

            targetPromptLabel.Set(UiStrings.TargetPrompt, CurrentDetail().Name);
        }

        private void RefreshEnemyPlates()
        {
            if (enemyPlates == null) return;

            var enemies = Enemies;
            enemiesHint.Set(UiStrings.StandingCount,
                _session == null ? 0 : FightHudModel.StandingCount(_session.Encounter));

            for (int i = 0; i < enemyPlates.Length; i++)
            {
                bool present = i < enemies.Count && enemies[i].IsAlive;
                SetActive(enemyPlates[i] == null ? null : enemyPlates[i].gameObject, present);
                SetActive(enemyPlateReticles[i], present && _menu.IsTargeting);
                if (!present) continue;

                var enemy = enemies[i];
                enemyPlateNames[i].SetContent(enemy.Name);
                enemyPlateHps[i].Set(UiStrings.HealthValue, enemy.CurrentHealth, enemy.MaxHealth);
                SetFill(enemyPlateHpFills[i], enemy.CurrentHealth, enemy.MaxHealth);
                enemyPlateTags[i].SetContent(TagLineFor(enemy));
            }
        }

        private void RefreshPartyPlate()
        {
            var actor = ActingCharacter();
            if (actor == null) return;

            partyName.SetContent(actor.Name);
            partyHpValue.Set(UiStrings.HealthValue, actor.CurrentHealth, actor.MaxHealth);
            partyMpValue.Set(UiStrings.HealthValue, actor.CurrentMana, actor.MaxMana);
            SetFill(partyHpFill, actor.CurrentHealth, actor.MaxHealth);
            SetFill(partyMpFill, actor.CurrentMana, actor.MaxMana);

            var kit = _session == null ? null : _session.KitFor(actor);
            if (kit != null)
            {
                partyClass.Set(UiStrings.LevelAndRole, kit.Level, kit.Role.ToString().ToUpperInvariant());
            }

            RefreshManaPreview(actor);
            RefreshWool(actor);
        }

        // The cost preview: a lighter segment at the right-hand end of the
        // filled portion, showing what the hovered skill would take from INSIDE
        // the resource. Right-pivoted, so it grows leftward from wherever the
        // fill currently ends -- which is why its width is the only thing that
        // moves and its position never does.
        private void RefreshManaPreview(CombatantState actor)
        {
            if (partyMpPreview == null) return;

            int preview = _menu.ManaPreview;
            bool show = preview > 0 && actor.MaxMana > 0 && preview <= actor.CurrentMana;
            SetActive(partyMpPreview.gameObject, show);
            if (!show) return;

            float barWidth = partyMpFill == null ? 0f : ((RectTransform)partyMpFill.transform).rect.width;
            float fraction = Mathf.Clamp01(preview / (float)actor.MaxMana);
            partyMpPreview.sizeDelta = new Vector2(barWidth * fraction, partyMpPreview.sizeDelta.y);

            // Anchored to where the CURRENT fill ends, not to the bar's end: the
            // segment has to read as "this much of what you have", and pinning
            // it to the track would show a cost you could not pay as if you
            // could.
            float filled = Mathf.Clamp01(actor.CurrentMana / (float)actor.MaxMana);
            partyMpPreview.anchoredPosition = new Vector2(barWidth * filled, partyMpPreview.anchoredPosition.y);
        }

        private void RefreshWool(CombatantState actor)
        {
            var signature = actor.Signature;
            bool has = signature != null;
            SetActive(woolValue == null ? null : woolValue.transform.parent.gameObject, has);
            if (!has || woolPips == null) return;

            woolValue.Set(UiStrings.SignatureValue, signature.Current, signature.Max);

            for (int i = 0; i < woolPips.Length; i++)
            {
                if (woolPips[i] == null) continue;

                // Pips beyond the resource's own maximum are HIDDEN rather than
                // drawn empty: sixteen slots under a character whose meter only
                // goes to eight would read as a meter half broken.
                bool exists = i < signature.Max;
                SetActive(woolPips[i].gameObject, exists);
                if (exists) woolPips[i].color = i < signature.Current ? PipFilled : PipEmpty;
            }
        }

        private void RefreshInitiative()
        {
            if (initiativeIcons == null || _session == null) return;

            var upcoming = _session.IsOver
                ? new List<CombatantState>()
                : new List<CombatantState>(_session.Encounter.UpcomingTurns(initiativeIcons.Length));

            for (int i = 0; i < initiativeIcons.Length; i++)
            {
                bool filled = i < upcoming.Count;

                // Both of these are built with a NULL sprite and no portrait art
                // exists yet, so neither may simply be switched on -- see
                // ShowSprite. The letter below is what actually identifies the
                // combatant today.
                ShowSprite(initiativeIcons[i], filled);
                ShowSprite(initiativeRings[i], filled && i == 0);

                // The initial, as the fallback for a combatant with no portrait
                // art. Written even when an icon exists, because the icon is
                // Resources-loaded and may legitimately not be there yet.
                initiativeLabels[i].SetContent(filled && upcoming[i].Name.Length > 0
                    ? upcoming[i].Name.Substring(0, 1).ToUpperInvariant()
                    : "");
            }
        }

        // ---- what the model needs -------------------------------------------

        private IReadOnlyList<SubmenuRow> CurrentRows() =>
            _menu.Branch == MenuBranch.Item
                ? FightHudModel.ItemRows(_satchel)
                : FightHudModel.SkillRows(_session, ActingCharacter());

        private DetailPanel CurrentDetail()
        {
            var actor = ActingCharacter();
            if (_menu.Branch == MenuBranch.Attack) return FightHudModel.DetailForStrike(actor);

            var rows = CurrentRows();
            int index = _menu.Selection;
            if (index < 0 || index >= rows.Count) return FightHudModel.DetailForStrike(actor);

            if (_menu.Branch == MenuBranch.Item) return FightHudModel.DetailForItem(_satchel[index]);

            var kit = _session?.KitFor(actor);
            if (kit != null && index < kit.Skills.Count) return FightHudModel.DetailForSkill(kit.Skills[index]);

            // The basic spell's row, which has no ResolvedSkill behind it.
            var panel = new DetailPanel
            {
                Name = rows[index].Name,
                Kind = "SPELL",
                Body = "The character's own arcane strike.",
            };
            panel.Stats.Add(("COST", rows[index].Cost));
            panel.Stats.Add(("POWER", "-"));
            panel.Stats.Add(("TARGET", "SINGLE"));
            panel.Stats.Add(("EFFECT", "DAMAGE"));
            return panel;
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

        private static string TagLineFor(CombatantState enemy)
        {
            var tags = new List<string>();
            if (enemy.BreakShield != null && enemy.BreakShield.IsBroken) tags.Add("REELING");
            foreach (var status in enemy.Statuses) tags.Add(status.Type.ToString().ToUpperInvariant());
            return string.Join("  ·  ", tags);
        }

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
            SetActive(image.gameObject, visible);
            image.enabled = visible;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
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
