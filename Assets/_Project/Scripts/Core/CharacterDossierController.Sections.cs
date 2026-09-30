using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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
    // SKILLS lists the character's talent-granted skills (phase 5c widens it
    // to every source).
    public partial class CharacterDossierController
    {
        [SerializeField] internal TMP_Text attributesUnspent;
        [SerializeField] internal GameObject attributesLockNote;
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

        [SerializeField] internal TMP_Text[] skillNames;
        [SerializeField] internal TMP_Text[] skillDescriptions;
        [SerializeField] internal GameObject skillsEmptyHint;

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

        private const string ManaDisplayNameFallback = "Mana";

        private static readonly Color StepperLive = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Text);
        private static readonly Color StepperIdle = Hex(Domain.UiKit.Screens.CharacterDossierScreen.TextMuted);

        private void ResolveCharacter(Character character)
        {
            _scores = ContentDatabase.EffectiveAbilityScores(character);
            _weaponScaling = ContentDatabase.EffectiveWeaponScaling(character);
            _skillScaling = ContentDatabase.EffectiveSkillScaling(character);

            var pool = ContentDatabase.PrimaryPoolOf(character.definitionId);
            _primaryPoolRule = pool?.CapacityRule ?? PoolCapacityRule.WisdomDerived;
            _primaryPoolName = string.IsNullOrWhiteSpace(pool?.DisplayName) ? ManaDisplayNameFallback : pool.DisplayName;
            _canHoldSpellBooks = SpellBooks.CanHold(pool);

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
                WatchRow(attributePluses[i], row);
            }

            for (int i = 0; attributeMinuses != null && i < attributeMinuses.Length; i++)
            {
                int row = i;
                attributeMinuses[i].onClick.AddListener(() => Refund(row));
                WatchRow(attributeMinuses[i], row);
            }
        }

        // Focus or the pointer on a row's "-" or "+" lights the stats that
        // score actually feeds, the sheet's one mechanic.
        private void WatchRow(Button stepper, int row)
        {
            var go = stepper.gameObject;
            var select = go.GetComponent<SelectIndex>() ?? go.AddComponent<SelectIndex>();
            select.Index = row;
            select.Changed = LightRowsFor;

            var hover = go.GetComponent<HoverIndex>() ?? go.AddComponent<HoverIndex>();
            hover.Index = row;
            hover.Changed = LightRowsFor;
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

            // The refund lock, said rather than left to a quiet "-".
            if (attributesLockNote != null) attributesLockNote.SetShown(!canRefund);

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
        }

        private static void PaintStepper(Button stepper, bool live)
        {
            var label = stepper.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (label != null) label.color = live ? StepperLive : StepperIdle;
        }

        private void RefreshStats(Character character)
        {
            var stats = ContentDatabase.EffectiveStats(character);

            for (int i = 0; i < SheetStats.Derived.Length; i++)
            {
                var stat = SheetStats.Derived[i];
                if (Has(statValues, i)) statValues[i].SetContent(DisplayValue(character, stat, stats, _scores));
                if (Has(statHighlights, i)) statHighlights[i].SetShown(false);
                if (stat == SheetStat.MaxMana && Has(statNames, i)) statNames[i].SetContent(StatLabel(stat));
            }
        }

        // THE mechanic: light the rows this score actually feeds.
        private void LightRowsFor(int row, bool entered)
        {
            if (statHighlights == null || row < 0 || row >= _rowOrder.Count) return;

            var fed = entered
                ? new HashSet<SheetStat>(SheetStats.Feeds(_rowOrder[row], _primaryPoolRule))
                : new HashSet<SheetStat>();

            for (int i = 0; i < SheetStats.Derived.Length && i < statHighlights.Length; i++)
            {
                if (statHighlights[i] != null) statHighlights[i].SetShown(fed.Contains(SheetStats.Derived[i]));
            }
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

        // ---- SKILLS -----------------------------------------------------------------------

        // Talent-granted only, which is what this pane can count without a
        // fight session in hand (ContentDatabase.TalentGrantedSkillsFor).
        private void RefreshSkills(Character character)
        {
            var skills = ContentDatabase.TalentGrantedSkillsFor(character);

            if (skillsEmptyHint != null) skillsEmptyHint.SetShown(skills.Count == 0);

            for (int i = 0; skillNames != null && i < skillNames.Length; i++)
            {
                bool present = i < skills.Count;
                skillNames[i].gameObject.SetShown(present);
                if (Has(skillDescriptions, i)) skillDescriptions[i].gameObject.SetShown(present);
                if (!present) continue;

                skillNames[i].SetContent(skills[i].Data.DisplayName);
                if (Has(skillDescriptions, i)) skillDescriptions[i].SetContent(skills[i].Data.Description);
            }
        }

        // ---- the numbers, shared by ATTRIBUTES and the compare column ------------------------

        // A stat's name as the sheet says it: the pool row carries the pool's
        // own name, everything else is authored copy.
        private string StatLabel(SheetStat stat) =>
            stat == SheetStat.MaxMana
                ? UiStrings.StatMaxPool.Format(_primaryPoolName)
                : SheetStats.LabelFor(stat).Format();

        private static string DisplayValue(Character character, SheetStat stat, StatBlock stats, AbilityScoreBlock scores) =>
            TryCurrentValue(character, stat, stats, scores, out int value) ? SheetStats.DisplayText(stat, value) : "-";

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
                    return _primaryPoolRule != PoolCapacityRule.WisdomDerived
                        ? 0
                        : AbilityDerivation.MaxManaBonus(previewScores) - AbilityDerivation.MaxManaBonus(_scores);
                case SheetStat.SignatureGain:
                    return AbilityDerivation.SignatureGainBonus(previewScores) - AbilityDerivation.SignatureGainBonus(_scores);
                default: return 0;
            }
        }
    }
}
