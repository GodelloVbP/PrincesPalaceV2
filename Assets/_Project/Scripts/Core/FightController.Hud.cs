using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
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

            RefreshMenuChrome();
            RefreshEnemyPlates();
            RefreshPartyPlate();
            RefreshInitiative();
            RefreshStage();
            RefreshLowHpVignette();
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
            RefreshVerbs();
            RefreshSubmenu();
            RefreshDetail();
            RefreshTargetPrompt();
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
                var image = verbButtons[i] == null ? null : verbButtons[i].targetGraphic as UnityEngine.UI.Image;
                if (image != null)
                {
                    // Gamepad focus shares the branch-open tint rather than
                    // growing a fourth colour -- the two are mutually
                    // exclusive in practice (focus only means anything while
                    // nothing is open yet), so one look serves both "this is
                    // what Submit presses" and "this is what is open".
                    bool highlighted = i == active || (active < 0 && i == _focusedVerb);
                    image.color = highlighted ? VerbActive
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

            // HOLD BACK's own bank count, folded into the label it is already
            // one of four -- BankedActions was invisible everywhere before
            // this, including on the verb that spends a turn creating it.
            // Index 3: {Attack, Skill, Item, HoldBack} is the fixed order
            // BuildVerbColumn authored these in.
            const int HoldBackVerbIndex = 3;
            if (Has(verbLabels, HoldBackVerbIndex))
            {
                var actor = ActingCharacter();
                int banked = actor?.BankedActions ?? 0;
                verbLabels[HoldBackVerbIndex].Set(UiStrings.VerbHoldBackWithBank, banked, FightTuning.MaxBankedActions);
            }

            breadcrumb.SetContent(FightHudModel.Breadcrumb(_menu));
        }

        // Column B, for whichever branch is open.
        private void RefreshSubmenu()
        {
            submenuColumn.SetShown(_menu.SubmenuOpen);
            if (!_menu.SubmenuOpen || submenuRows == null) return;

            var rows = CurrentRows();

            submenuTitle.Set(_menu.Branch == MenuBranch.Item
                ? UiStrings.SubmenuItemsTitle
                : UiStrings.SubmenuSkillsTitle);
            // The truncation is reported HERE, beside the list it truncates,
            // rather than into the combat log. It used to AppendMessage on every
            // refresh, so the bark spent the fight repeating "9 more entr(y/ies)
            // than the column can show" over whatever the fight was actually
            // saying. The warning was right and the channel was wrong: a fact
            // about the menu does not belong in the narration of the battle, and
            // a line re-sent every frame stops being read at all.
            int hidden = CurrentRows().Count - submenuRows.Length;
            if (hidden > 0)
            {
                submenuHint.SetContent($"SHOWING {submenuRows.Length} OF {CurrentRows().Count}");
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

                var frame = submenuRows[i].targetGraphic as UnityEngine.UI.Image;
                if (frame != null) frame.color = i == _menu.Selection ? RowSelected : RowIdle;

                submenuRows[i].interactable = row.Affordable;

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

            var options = _session.SkillOptionsFor(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return false;

            return options[row].Skill.Targeting == Domain.Combat.SkillTargeting.AllEnemies;
        }

        // Whether the click resolving at Target depth is subject to the
        // front-rank rule at all -- Attack is always the plain Strike, which
        // always has been; a skill only joins it by carrying meleeReach (see
        // ResolvedSkill.MeleeReach's own comment). Mirrors the same lookup
        // OnEnemyPressed uses to gate the click itself -- this is the half
        // that paints the same answer onto the plate, not a second rule.
        private bool TargetingIsMelee()
        {
            if (_menu.Branch == MenuBranch.Attack) return true;
            if (_menu.Branch != MenuBranch.Skill || _session?.Current == null) return false;

            var options = _session.SkillOptionsFor(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return false;

            return options[row].Skill.MeleeReach;
        }

        // Alpha rather than a colour swap -- the same "dim, don't hide"
        // choice the submenu's own unaffordable rows already make (see
        // DimmedAlpha there). A blocked plate is still information; it is
        // just not this click's business right now.
        private const float MeleeBlockedAlpha = 0.5f;

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
            bool meleeTargeting = _menu.IsTargeting && TargetingIsMelee();

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
                bool blocked = present && meleeTargeting && _session != null
                    && !_session.CanMeleeReach(enemies[i]);

                bool dressed = present && (
                    _encounterClass == EncounterClass.Elite
                    || (_encounterClass == EncounterClass.Boss && ReferenceEquals(enemies[i], frontEnemy)));

                if (present)
                {
                    var plateImage = enemyPlates[i].targetGraphic as Image;
                    if (plateImage != null)
                    {
                        var colour = dressed ? EliteBossPlateTint : Color.white;
                        colour.a = blocked ? MeleeBlockedAlpha : 1f;
                        plateImage.color = colour;
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
                        colour.a = blocked ? MeleeBlockedAlpha : 1f;
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
                    var art = LoadStanceSprite(enemy, FightSession.Stances.Idle);
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
            RefreshPartyBuffs(actor);
            RefreshTransformStrip(actor);
            RefreshSecondLifeBadge();
            RefreshRoster(actor);
        }

        // Fused above the party plate, shown only while the acting character
        // is transformed -- see FightScreen.BuildTransformStrip's own
        // comment for why this is a separate panel rather than a fourth row
        // squeezed into the plate itself.
        private void RefreshTransformStrip(CombatantState actor)
        {
            if (transformStrip == null) return;

            var transformation = actor.Transformation;
            transformStrip.SetShown(transformation != null);
            if (transformation == null || transformStripText == null) return;

            if (transformation.IsPermanent)
            {
                transformStripText.Set(UiStrings.TransformStripPermanent, transformation.DisplayName);
            }
            else
            {
                transformStripText.Set(UiStrings.TransformStripTurns,
                    transformation.DisplayName, transformation.TurnsRemaining);
            }
        }

        // The one badge on this screen that reads FightSession rather than
        // the acting character -- a Second Life charge belongs to the whole
        // party's run, not to whoever happens to be acting this turn.
        private void RefreshSecondLifeBadge()
        {
            if (secondLifeBadge == null || _session == null) return;

            bool available = _session.SecondLivesSpent < _session.SecondLifeCharges;
            secondLifeBadge.SetShown(available);
        }

        // The two party members NOT currently acting, information only -- no
        // swap mechanic, see FightScreen.BuildRosterPlates' own comment. Over
        // PlayerParty rather than LivingPlayerParty deliberately: a downed
        // ally is still worth showing here, at 0 HP, rather than vanishing
        // from the roster the moment they fall.
        private void RefreshRoster(CombatantState acting)
        {
            if (rosterPlates == null || _session == null) return;

            var others = _session.Encounter.PlayerParty.Where(c => c != acting).ToList();
            for (int i = 0; i < rosterPlates.Length; i++)
            {
                bool present = i < others.Count;
                rosterPlates[i].SetShown(present);
                if (!present) continue;

                var member = others[i];
                if (Has(rosterNames, i)) rosterNames[i].SetContent(member.Name);
                if (Has(rosterHpValues, i))
                {
                    rosterHpValues[i].Set(UiStrings.HealthValue, member.CurrentHealth, member.MaxHealth);
                }
                if (Has(rosterHpFills, i)) SetFill(rosterHpFills[i], member.CurrentHealth, member.MaxHealth);
            }
        }

        // ---- buff badges ------------------------------------------------------
        //
        // GREEN for something helping the character, RED for something
        // hurting them -- IntentHeal and IntentWeaken/HpBright, the same two
        // tokens the enemy-intent badges already use for the identical
        // helping/hurting split, reused rather than a third palette for one
        // idea the HUD already has a colour language for.
        private static readonly Color BuffPositive = Hex(FightHudPalette.IntentHeal);
        private static readonly Color BuffNegative = Hex(FightHudPalette.HpBright);

        private TMPro.TMP_Text[] _partyBuffGlyphs;
        private Image[] _partyBuffImages;

        // The face each button was BUILT with -- SceneBuilder leaves
        // PartyBuff{i}'s SpriteKey unset, which UiNode's own comment says
        // means "use the shared button frame", so this is a real sprite
        // reference, not a blank one. Captured once, before anything ever
        // swaps `.sprite`, so RefreshPartyBuffs can restore it for the eight
        // statuses with no icon of their own instead of leaving the Image
        // with whatever the PREVIOUS badge in that slot happened to be
        // wearing (or, worse, null -- which paints Unity's default white
        // square, not this game's button chrome).
        private Sprite[] _partyBuffDefaultSprites;

        // ---- shared icon cache --------------------------------------------------
        //
        // The exact same system twice: a sprite resolved once per enum kind
        // via Resources.Load and kept in a dictionary that only ever grows,
        // with a MISS cached right alongside a HIT so a kind with no
        // authored art stops asking Resources for a file that will never
        // exist. StatusSprites below and IntentSprites in
        // FightController.StageVisuals.cs used to each hand-roll this; both
        // now go through one instance of this generic cache instead.
        //
        // Applying the result to an Image is the ONE place the two systems
        // still deliberately disagree, so that stays a parameter rather
        // than getting folded in here too: an enemy-intent badge with no
        // art is a BUG and disables its Image outright (a blank slot beats
        // a filled white rectangle -- see RefreshIntentIcons' own comment),
        // where eight of ten status kinds having no art is that badge's
        // INTENDED steady state and restores whatever sprite the button was
        // built with instead (see RefreshPartyBuffs' own comment). Neither
        // caller's colour or glyph-fallback logic belongs here either --
        // those differ per system and are not the part that was duplicated.
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

        // Resolved once per status kind, through the SAME cache shape
        // IntentSprites in FightController.StageVisuals.cs uses -- see
        // IconCache<TKind>'s own header just above for the one deliberate
        // behavioural difference between the two systems.
        private static readonly IconCache<StatusEffectType> StatusSprites = new IconCache<StatusEffectType>();

        private static Sprite StatusSpriteFor(StatusEffectType kind) =>
            StatusSprites.Resolve(kind, FightHudModel.StatusBadgeIcons.ResourceFor);

        private void WirePartyBuffIcons()
        {
            if (partyBuffIcons == null) return;

            _partyBuffGlyphs = new TMPro.TMP_Text[partyBuffIcons.Length];
            _partyBuffImages = new Image[partyBuffIcons.Length];
            _partyBuffDefaultSprites = new Sprite[partyBuffIcons.Length];
            for (int i = 0; i < partyBuffIcons.Length; i++)
            {
                var icon = partyBuffIcons[i];
                if (icon == null) continue;

                _partyBuffGlyphs[i] = icon.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
                _partyBuffImages[i] = icon.GetComponent<Image>();
                _partyBuffDefaultSprites[i] = _partyBuffImages[i] != null ? _partyBuffImages[i].sprite : null;

                var hover = icon.GetComponent<HoverIndex>();
                if (hover == null) hover = icon.AddComponent<HoverIndex>();
                hover.Index = i;
                hover.Changed = OnHoverPartyBuff;
            }

            if (partyBuffTooltip != null) partyBuffTooltip.SetShown(false);
        }

        private List<FightHudModel.BuffBadge> _currentPartyBuffs = new List<FightHudModel.BuffBadge>();

        private void RefreshPartyBuffs(CombatantState actor)
        {
            if (partyBuffIcons == null || _session == null) return;
            if (_partyBuffGlyphs == null) WirePartyBuffIcons();

            _currentPartyBuffs = FightHudModel.BuffBadgesFor(_session, actor);

            for (int i = 0; i < partyBuffIcons.Length; i++)
            {
                bool shown = i < _currentPartyBuffs.Count;
                partyBuffIcons[i].SetShown(shown);
                if (!shown) continue;

                var badge = _currentPartyBuffs[i];

                // ICON FIRST. badge.Kind is null for the generic "SPD" relic
                // badge and for every status StatusBadgeIcons has no art
                // for, so `art` stays null for all of those.
                Sprite art = badge.Kind.HasValue ? StatusSpriteFor(badge.Kind.Value) : null;

                var image = _partyBuffImages != null ? _partyBuffImages[i] : partyBuffIcons[i].GetComponent<Image>();
                if (image != null)
                {
                    // Unlike RefreshIntentIcons, a null `art` here does NOT
                    // mean "disable the Image" -- an enemy-intent badge with
                    // no art is a BUG (every kind is meant to load one
                    // eventually) and degrades to bare text over a blank; a
                    // buff badge with no art is eight statuses' EXISTING,
                    // intended look, a plain tinted button face, so the
                    // fallback restores the SAME sprite the button was built
                    // with (_partyBuffDefaultSprites) rather than clearing
                    // it to null, which would strip the shared button frame
                    // and paint Unity's default white square instead. See
                    // IconCache<TKind>.Apply's own header for how this
                    // `disableOnMiss: false` reads against RefreshIntentIcons'
                    // `true`.
                    Sprite fallback = _partyBuffDefaultSprites != null ? _partyBuffDefaultSprites[i] : null;
                    IconCache<StatusEffectType>.Apply(image, art, disableOnMiss: false, fallback);
                    image.color = badge.IsPositive ? BuffPositive : BuffNegative;
                }

                if (_partyBuffGlyphs != null && _partyBuffGlyphs[i] != null)
                {
                    // The glyph only when the art did not load, same
                    // icon-first/glyph-fallback priority RefreshIntentIcons
                    // uses for enemy intents.
                    _partyBuffGlyphs[i].SetContent(art == null ? badge.Glyph : "");
                }
            }
        }

        private void OnHoverPartyBuff(int index, bool entered)
        {
            if (partyBuffTooltip == null) return;

            if (!entered || index < 0 || index >= _currentPartyBuffs.Count)
            {
                partyBuffTooltip.SetShown(false);
                return;
            }

            if (partyBuffTooltipText != null)
            {
                partyBuffTooltipText.SetContent(_currentPartyBuffs[index].Tooltip);
            }

            partyBuffTooltip.SetShown(true);
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
            partyMpPreview.gameObject.SetShown(show);
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
            if (woolValue != null) woolValue.transform.parent.gameObject.SetShown(has);
            if (!has || woolPips == null) return;

            woolValue.Set(UiStrings.SignatureValue, signature.Current, signature.Max);

            for (int i = 0; i < woolPips.Length; i++)
            {
                if (woolPips[i] == null) continue;

                // Pips beyond the resource's own maximum are HIDDEN rather than
                // drawn empty: sixteen slots under a character whose meter only
                // goes to eight would read as a meter half broken.
                bool exists = i < signature.Max;
                woolPips[i].gameObject.SetShown(exists);
                if (exists) woolPips[i].color = i < signature.Current ? PipFilled : PipEmpty;
            }
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

        private void RefreshInitiative()
        {
            if (initiativeIcons == null || _session == null) return;

            var upcoming = _session.IsOver
                ? new List<CombatantState>()
                : new List<CombatantState>(_session.Encounter.UpcomingTurns(initiativeIcons.Length));

            // THE GHOST PREVIEW. Only while a push-flagged skill is both
            // selected (Target depth) AND a specific enemy plate is under the
            // cursor -- QueuePushSlots only ever means anything on a
            // SingleEnemy skill (SkillEntryResolver's own rule), so there is
            // no target to preview a push against before one plate is
            // actually being pointed at. Nothing here commits anything: the
            // real queue is untouched, see TurnOrder.ProjectWith's own header.
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

                // Both of these are built with a NULL sprite and no portrait art
                // exists yet, so neither may simply be switched on -- see
                // ShowSprite. The letter below is what actually identifies the
                // combatant today.
                ShowSprite(initiativeIcons[i], filled);
                ShowSprite(initiativeRings[i], filled && i == 0);

                // The initial, as the fallback for a combatant with no portrait
                // art. Written even when an icon exists, because the icon is
                // Resources-loaded and may legitimately not be there yet.
                initiativeLabels[i].SetContent(filled && previewed[i].Name.Length > 0
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

            var options = _session.SkillOptionsFor(_session.Current);
            int row = _menu.Selection;
            if (row < 0 || row >= options.Count) return 0;

            return options[row].Skill.QueuePushSlots;
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

            // NOT DetailForStrike. Falling back to it here described the ATTACK
            // verb -- "Strike", "A plain swing at one enemy in reach" -- while
            // the SKILL list was open above it and the breadcrumb read
            // COMMAND > SKILL. A panel that is merely empty is honest; one that
            // confidently describes a different command is not.
            if (index < 0 || index >= rows.Count) return FightHudModel.DetailForNoSelection(_menu.Branch);

            if (_menu.Branch == MenuBranch.Item) return FightHudModel.DetailForItem(_satchel[index]);

            var kit = _session?.KitFor(actor);
            if (kit != null && index < kit.Skills.Count)
            {
                return FightHudModel.DetailForSkill(_session, actor, kit.Skills[index], actor?.Signature?.DisplayName);
            }

            // The basic spell's row, which has no ResolvedSkill behind it.
            var panel = new DetailPanel
            {
                Name = rows[index].Name,
                Kind = "SPELL",
                Body = "The character's own arcane strike.",
            };
            panel.Stats.Add(("COST", rows[index].Cost));
            panel.Stats.Add(("POWER", (_session?.PreviewBasicSpellPower(actor) ?? 0).ToString()));
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
