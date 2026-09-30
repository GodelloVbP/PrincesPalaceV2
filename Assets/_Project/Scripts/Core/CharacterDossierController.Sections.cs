using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // ATTRIBUTES and SKILLS, and the character facts every section reads.
    //
    // ATTRIBUTES holds the rows the attributes modal had, now as a section
    // (phase 5d redesigns it): one row per score with its effect line and a
    // "-" / "+" pair, and the derived stats beside them. A "+" or "-" commits
    // on press (plan C5: attribute points have no pending state).
    //
    // SKILLS is its own partial (CharacterDossierController.Skills.cs).
    public partial class CharacterDossierController
    {
        [SerializeField] internal TMP_Text attributesUnspent;
        [SerializeField] internal TMP_Text attributesLockNote;
        [SerializeField] internal TMP_Text[] attributeNames;
        [SerializeField] internal TMP_Text[] attributeValues;
        [SerializeField] internal TMP_Text[] attributeInvested;
        [SerializeField] internal TMP_Text[] attributeEffects;
        [SerializeField] internal Button[] attributePluses;
        [SerializeField] internal Button[] attributeMinuses;

        // Indexed by SheetStats.Derived.
        [SerializeField] internal TMP_Text[] statNames;
        [SerializeField] internal TMP_Text[] statValues;
        [SerializeField] internal GameObject[] statHighlights;

        // Which score sits in which row: highest first, so the shape of a build
        // reads off the top. FROZEN while one character is on screen, so
        // spending a point cannot reshuffle the rows under the cursor and put a
        // second press into a different score.
        private readonly List<AbilityScore> _rowOrder = new List<AbilityScore>();
        private string _rowOrderFor;

        // The shown character's live scores and scaling, read by the effect
        // lines and the compare column -- the same ContentDatabase.Effective*
        // calls the fight makes.
        private AbilityScoreBlock _scores;
        private ScalingSet _weaponScaling;
        private ScalingSet _skillScaling;

        // What the shown character's primary pool is and does: its capacity
        // rule (whether Wisdom moves it), its own name ("Max Fury" rather than
        // a resource they do not have), and whether it reads spell books.
        private PoolCapacityRule _primaryPoolRule = PoolCapacityRule.WisdomDerived;
        private string _primaryPoolName = ManaDisplayNameFallback;
        private bool _canHoldSpellBooks = true;

        // The character's signature resource, by its own name ("Wool"): what a
        // skill's cost and the per-turn row are written in. Blank when they
        // have none, and the per-turn row goes with it.
        private bool _hasSignature;
        private string _signatureName = "";

        // What the fight is showing about the shown character, or null outside
        // a fight and for someone who is not in it (plan C4). Re-read on every
        // refresh: the HUD repaints between the menu's opening and its paints.
        private FightMemberFacts _fight;

        private const string ManaDisplayNameFallback = "Mana";

        private static readonly Color StepperLive = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Text);
        private static readonly Color StepperIdle = Hex(Domain.UiKit.Screens.CharacterDossierScreen.TextMuted);
        private static readonly Color StatPlain = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Text);

        private void ResolveCharacter(Character character)
        {
            _scores = ContentDatabase.EffectiveAbilityScores(character);
            _weaponScaling = ContentDatabase.EffectiveWeaponScaling(character);
            _skillScaling = ContentDatabase.EffectiveSkillScaling(character);

            var pool = ContentDatabase.PrimaryPoolOf(character.definitionId);
            _primaryPoolRule = pool?.CapacityRule ?? PoolCapacityRule.WisdomDerived;
            _primaryPoolName = string.IsNullOrWhiteSpace(pool?.DisplayName) ? ManaDisplayNameFallback : pool.DisplayName;
            _canHoldSpellBooks = SpellBooks.CanHold(pool);

            _fight = FightFactsFor(character.definitionId);

            var definition = ContentDatabase.GetCharacter(character.definitionId);
            _hasSignature = definition?.Data != null && definition.Data.HasSignatureResource;
            _signatureName = !_hasSignature ? ""
                : string.IsNullOrWhiteSpace(definition.Data.SignatureDisplayName)
                    ? UiStrings.SignatureNameFallback.Format()
                    : definition.Data.SignatureDisplayName;

            if (_rowOrder.Count == 0 || _rowOrderFor != character.definitionId)
            {
                _rowOrderFor = character.definitionId;
                _rowOrder.Clear();
                _rowOrder.AddRange(AbilityScores.All.OrderByDescending(s => _scores[s]).ThenBy(s => (int)s));
            }
        }

        // ---- ATTRIBUTES ---------------------------------------------------------------

        private void WireAttributes()
        {
            for (int i = 0; attributePluses != null && i < attributePluses.Length; i++)
            {
                int row = i;
                attributePluses[i].onClick.AddListener(() => Spend(row));
                WatchRow(attributePluses[i], row, +1);
            }

            for (int i = 0; attributeMinuses != null && i < attributeMinuses.Length; i++)
            {
                int row = i;
                attributeMinuses[i].onClick.AddListener(() => Refund(row));
                WatchRow(attributeMinuses[i], row, -1);
            }
        }

        // Focus or the pointer on a row's minus or plus lights the stats that
        // score actually feeds and shows what a press would make of them, the
        // sheet's one mechanic.
        private void WatchRow(Button stepper, int row, int step)
        {
            var go = stepper.gameObject;
            var select = go.GetComponent<SelectIndex>() ?? go.AddComponent<SelectIndex>();
            select.Index = row;
            select.Changed = (r, entered) => PreviewStep(r, step, entered);

            var hover = go.GetComponent<HoverIndex>() ?? go.AddComponent<HoverIndex>();
            hover.Index = row;
            hover.Changed = (r, entered) => PreviewStep(r, step, entered);
        }

        private void RefreshAttributes(Character character)
        {
            int points = character.unspentStatPoints;
            bool canSpend = MenuPermissions.Allows(MenuAction.SpendAttribute, Context);
            bool canRefund = MenuPermissions.Allows(MenuAction.RefundAttribute, Context);

            if (attributesUnspent != null)
            {
                if (points <= 0) attributesUnspent.SetContent("");
                else if (points == 1) attributesUnspent.Set(UiStrings.AttributesUnspentOne);
                else attributesUnspent.Set(UiStrings.AttributesUnspent, points);
            }

            // What the menu refuses here, in MenuPermissions' own words: a
            // descent refunds at camp, a fight changes nothing. Empty when
            // nothing is refused.
            if (attributesLockNote != null)
            {
                var refused = MenuPermissions.Reason(MenuAction.SpendAttribute, Context);
                if (string.IsNullOrEmpty(refused.Key)) refused = MenuPermissions.Reason(MenuAction.RefundAttribute, Context);

                if (string.IsNullOrEmpty(refused.Key)) attributesLockNote.SetContent("");
                else attributesLockNote.Set(refused);
            }

            for (int i = 0; i < SheetStats.Abilities.Length; i++)
            {
                bool has = i < _rowOrder.Count;
                var score = has ? _rowOrder[i] : AbilityScore.Strength;
                int invested = has ? character.investedAbilityScores[score] : 0;

                if (Has(attributeNames, i)) attributeNames[i].SetContent(has ? AbilityScores.ShortName(score) : "");
                if (Has(attributeValues, i)) attributeValues[i].SetContent(has ? _scores[score].ToString() : "");
                if (Has(attributeInvested, i))
                {
                    if (has && invested > 0) attributeInvested[i].Set(UiStrings.AttributesInvested, invested);
                    else attributeInvested[i].SetContent("");
                }

                if (Has(attributeEffects, i))
                {
                    attributeEffects[i].SetContent(has
                        ? AbilityEffectDescriptions.Describe(score, _scores, _weaponScaling, _skillScaling)
                        : "");
                }

                // Always drawn and always focusable (plan C3): a press that
                // cannot do anything says why. Only the weight says whether it
                // can.
                if (Has(attributePluses, i)) PaintStepper(attributePluses[i], canSpend && points > 0);
                if (Has(attributeMinuses, i)) PaintStepper(attributeMinuses[i], canRefund && invested > 0);
            }

            RefreshStats(character);
            RepaintPreviewForSelection();
        }

        private static void PaintStepper(Button stepper, bool live)
        {
            var label = stepper.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (label != null) label.color = live ? StepperLive : StepperIdle;
        }

        // Only in a fight, only for someone the HUD is showing: the source is
        // the fight's own record of what it has painted, never the run's
        // carried health (plan C4).
        private FightMemberFacts FightFactsFor(string characterId)
        {
            if (Context != MenuContext.Fight || menu == null || menu.FightSource == null) return null;

            return menu.FightSource.TryGetMember(characterId, out var facts) ? facts : null;
        }

        private void RefreshStats(Character character)
        {
            var stats = ContentDatabase.EffectiveStats(character);

            // Rows that do not apply are not drawn and leave no gap: the rest
            // close up in order, so a character with no regen row reads as a
            // shorter list rather than a list with a hole in it.
            int slot = 0;
            for (int i = 0; i < SheetStats.Derived.Length; i++)
            {
                var stat = SheetStats.Derived[i];
                if (StatApplies(stat)) PlaceStatRow(i, slot++);

                if (Has(statValues, i))
                {
                    statValues[i].SetContent(DisplayValue(character, stat, stats, _scores));
                    statValues[i].color = StatPlain;
                }

                if (Has(statHighlights, i)) statHighlights[i].SetShown(false);

                // The pool and signature rows carry the character's own words;
                // a row that does not apply to them is not drawn.
                bool applies = StatApplies(stat);
                if (Has(statNames, i))
                {
                    statNames[i].gameObject.SetShown(applies);
                    if (applies && (stat == SheetStat.MaxMana || stat == SheetStat.SignatureGain))
                    {
                        statNames[i].SetContent(StatLabel(stat));
                    }
                }

                if (Has(statValues, i)) statValues[i].gameObject.SetShown(applies);
            }
        }

        private void PlaceStatRow(int row, int slot)
        {
            float y = DossierLayout.DerivedRowCentreY(slot);
            SetRowY(Has(statHighlights, row) ? statHighlights[row].transform : null, y);
            SetRowY(Has(statNames, row) ? statNames[row].transform : null, y);
            SetRowY(Has(statValues, row) ? statValues[row].transform : null, y);
        }

        private static void SetRowY(Transform node, float y)
        {
            if (node is RectTransform rect) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        // THE mechanic: light the rows this score actually feeds, and while a
        // stepper that would do something is under the focus, write what each
        // becomes, "29 (22%) -> 31 (23%)". A stepper that would do nothing (no
        // points, refused here, nothing invested) lights the rows and previews
        // no change: a number that will not happen is a lie.
        private void PreviewStep(int row, int step, bool entered)
        {
            if (statHighlights == null || row < 0 || row >= _rowOrder.Count) return;

            var character = Current();
            if (character == null) return;

            var score = _rowOrder[row];
            var fed = entered
                ? new HashSet<SheetStat>(SheetStats.Feeds(score, _primaryPoolRule))
                : new HashSet<SheetStat>();

            bool live = entered && StepWouldHappen(character, score, step);
            var after = _scores.With(score, _scores[score] + step);
            var stats = ContentDatabase.EffectiveStats(character);

            for (int i = 0; i < SheetStats.Derived.Length && i < statHighlights.Length; i++)
            {
                var stat = SheetStats.Derived[i];
                bool lit = fed.Contains(stat) && StatApplies(stat);
                if (statHighlights[i] != null) statHighlights[i].SetShown(lit);
                if (!Has(statValues, i)) continue;

                if (!lit || !live || !TryCurrentValue(character, stat, stats, _scores, out int now))
                {
                    statValues[i].SetContent(DisplayValue(character, stat, stats, _scores));
                    statValues[i].color = StatPlain;
                    continue;
                }

                int delta = SheetStats.ScoreDelta(stat, _scores, after, _primaryPoolRule);
                statValues[i].SetContent(UiStrings.StatPreview.Format(
                    SheetStats.DisplayText(stat, now), SheetStats.DisplayText(stat, now + delta)));
                statValues[i].color = delta > 0 ? Good : delta < 0 ? Bad : StatPlain;
            }
        }

        private bool StepWouldHappen(Character character, AbilityScore score, int step) =>
            step > 0
                ? MenuPermissions.Allows(MenuAction.SpendAttribute, Context) && character.unspentStatPoints > 0
                : MenuPermissions.Allows(MenuAction.RefundAttribute, Context) && character.investedAbilityScores[score] > 0;

        // A press moved the numbers under a stepper that still has the focus,
        // and no select event will come to say so: show the preview again.
        private void RepaintPreviewForSelection()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return;

            int plus = IndexIn(attributePluses, selected);
            if (plus >= 0) { PreviewStep(plus, +1, entered: true); return; }

            int minus = IndexIn(attributeMinuses, selected);
            if (minus >= 0) PreviewStep(minus, -1, entered: true);
        }

        // Places one of this character's unspent points into the score in
        // row `row`, through TalentOps so carried health follows a
        // Constitution point.
        private void Spend(int row)
        {
            if (SystemMenuController.Refuses(menu, MenuAction.SpendAttribute)) return;

            var character = Current();
            if (character == null || row < 0 || row >= _rowOrder.Count) return;

            if (character.unspentStatPoints <= 0)
            {
                menu?.ShowNotice(UiStrings.AttributesNoPoints);
                return;
            }

            if (!TalentOps.Invest(Context, character, _rowOrder[row])) return;

            // Written immediately: a placed point is permanent until a respec.
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        private void Refund(int row)
        {
            if (SystemMenuController.Refuses(menu, MenuAction.RefundAttribute)) return;

            var character = Current();
            if (character == null || row < 0 || row >= _rowOrder.Count) return;

            if (character.investedAbilityScores[_rowOrder[row]] <= 0)
            {
                menu?.ShowNotice(UiStrings.AttributesNothingInvested);
                return;
            }

            if (!TalentOps.Refund(Context, character, _rowOrder[row])) return;

            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        private Selectable AttributesEntry() => attributePluses != null && attributePluses.Length > 0 ? attributePluses[0] : null;

        // Rows are a list; "-" and "+" are a rail within the row, "-" on the
        // left. Left off "-" is the rail, Up off the first row the tab.
        private void DeclareAttributes(List<UiNavGroup<Selectable>> groups, List<UiNavLink<Selectable>?> links)
        {
            var rail = SectionRow(CharacterSection.Attributes);
            int rows = Mathf.Min(attributePluses?.Length ?? 0, attributeMinuses?.Length ?? 0);

            for (int i = 0; i < rows; i++)
            {
                var minus = attributeMinuses[i];
                var plus = attributePluses[i];

                links.Add(RuntimeNavWiring.Link(minus, UiNavDirection.Left, rail));
                links.Add(RuntimeNavWiring.Link(minus, UiNavDirection.Right, plus));
                links.Add(RuntimeNavWiring.Link(plus, UiNavDirection.Left, minus));

                links.Add(RuntimeNavWiring.Link(minus, UiNavDirection.Up, i > 0 ? attributeMinuses[i - 1] : Tab));
                links.Add(RuntimeNavWiring.Link(plus, UiNavDirection.Up, i > 0 ? attributePluses[i - 1] : Tab));
                if (i < rows - 1)
                {
                    links.Add(RuntimeNavWiring.Link(minus, UiNavDirection.Down, attributeMinuses[i + 1]));
                    links.Add(RuntimeNavWiring.Link(plus, UiNavDirection.Down, attributePluses[i + 1]));
                }
            }
        }

        // ---- the numbers, shared by ATTRIBUTES and the compare column ------------------------

        // A stat's name as the sheet says it: the pool row carries the pool's
        // own name, everything else is authored copy.
        private string StatLabel(SheetStat stat)
        {
            switch (stat)
            {
                case SheetStat.MaxMana: return UiStrings.StatMaxPool.Format(_primaryPoolName);
                case SheetStat.SignatureGain: return UiStrings.StatSignaturePerTurn.Format(_signatureName);
                default: return SheetStats.LabelFor(stat).Format();
            }
        }

        // Whether this character has the row at all. A signature row for
        // someone with no signature would read "-" beside a name that is not
        // theirs, and regen for a Fixed pool would name an income that pool
        // never takes from any source (its gain is authored, pools.json), the
        // same rule SheetStats.FedBy states for Wisdom.
        private bool StatApplies(SheetStat stat)
        {
            switch (stat)
            {
                case SheetStat.SignatureGain: return _hasSignature;
                case SheetStat.ManaRegen: return _primaryPoolRule != PoolCapacityRule.Fixed;
                default: return true;
            }
        }

        // The value as the row prints it. In a fight, a stat the fight has
        // moved reads "sheet -> now (source)" (plan C4), from the same base
        // the sheet shows and the delta the HUD's own buff badges report.
        private string DisplayValue(Character character, SheetStat stat, StatBlock stats, AbilityScoreBlock scores)
        {
            if (!TryCurrentValue(character, stat, stats, scores, out int value)) return "-";

            if (_fight != null && _fight.Modifies(stat))
            {
                return UiStrings.StatEffective.Format(value, Mathf.Max(0, value + _fight.DeltaFor(stat)), _fight.SourcesFor(stat));
            }

            return SheetStats.DisplayText(stat, value);
        }

        // What a stat reads as RIGHT NOW, from the model. False means there
        // is no number to show, which is not the same thing as zero.
        private static bool TryCurrentValue(Character character, SheetStat stat,
                                            StatBlock stats, AbilityScoreBlock scores, out int value)
        {
            if (stat == SheetStat.MaxMana)
            {
                value = ContentDatabase.EffectiveMaxMana(character);
                return true;
            }

            if (stat == SheetStat.SignatureGain)
            {
                var signature = ContentDatabase.BuildSignatureResource(character);
                value = signature?.GainPerTurn ?? 0;
                return signature != null;
            }

            value = SheetStats.ValueOf(stat, stats, scores);
            return true;
        }

        // How far an equip would move a stat. The two pool rows are derived
        // from Wisdom and Charisma, so an item that shifts a score shifts them
        // too -- unless the pool is Fixed, when Wisdom moves nothing.
        private int DeltaFor(SheetStat stat, ItemComparison comparison, AbilityScoreBlock previewScores)
        {
            switch (stat)
            {
                case SheetStat.MaxHealth: return comparison.StatDelta.maxHealth;
                case SheetStat.Attack: return comparison.StatDelta.attack;
                case SheetStat.Speed: return comparison.StatDelta.speed;
                case SheetStat.ManaRegen: return comparison.StatDelta.manaRegen;
                case SheetStat.PhysicalDefense: return comparison.StatDelta.physicalDefense;
                case SheetStat.MagicalDefense: return comparison.StatDelta.magicalDefense;
                case SheetStat.MaxMana:
                case SheetStat.SignatureGain:
                    return SheetStats.ScoreDelta(stat, _scores, previewScores, _primaryPoolRule);
                default: return 0;
            }
        }
    }
}
