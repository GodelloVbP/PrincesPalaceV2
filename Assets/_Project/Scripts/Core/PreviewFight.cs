using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // WHAT tools/preview.ps1 -Spell AND -Character DECIDE, decided once.
    //
    // Both routes into a preview -- the Editor playing the fight (-Launch,
    // through PreviewRequestWatcher and FightBootstrap) and the headless one
    // photographing it (PreviewCaptureTests) -- have to set the fight up the
    // same way, or the picture is evidence about something the author cannot
    // reproduce by pressing play. So the setup lives here and both call it,
    // rather than each growing its own copy of "who casts this and what has
    // to be true before they can".
    //
    // THE POSTURE, and it is the whole design: an unsupported setup is
    // REPORTED BY NAME AND REFUSED, never approximated. A preview that
    // quietly substitutes a different caster, a different target count or a
    // different effect is worse than no preview, because the author looks at
    // it and believes it. Every accommodation this does make (mana filled, an
    // ability requirement raised, a wounded party) is listed in Notes and
    // printed, so nothing about the fight on screen is silently untrue.
    //
    // NOTHING HERE TOUCHES CONTENT OR A SAVE. The forced skill is appended to
    // the kit this one fight is built with; skills.json is not edited, the
    // asset is not edited, SaveSystem is never opened. See FightBootstrap's
    // DevForced* block for the session-state half.
    public static class PreviewFight
    {
        // THE FORMATION VOCABULARY, owned here and named rather than typed.
        // "lone" and "full" used to be typed out at every site that reads or
        // writes FightBootstrap.DevForcedFormation (this file, FightBootstrap
        // itself, and PreviewRequestWatcher) -- a typo at any one of them
        // silently falls through to the lone branch, since nothing there is
        // an enum with a default case to refuse it. tools/preview.ps1 sends
        // this over SessionState as plain text (it is PowerShell; it cannot
        // reference a C# const), so the literals stay pinned at the wire in
        // DevForcedPreviewKeyTests rather than disappearing entirely.
        public const string FormationLone = "lone";
        public const string FormationFull = "full";

        // Which SkillEffects the preview knows how to stand a fight up for.
        //
        // Deliberately a short list, and deliberately not "everything the
        // session can resolve". Each member here has a stage arrangement that
        // makes its cast visible -- a target to hit, a wound to heal, a slot
        // to summon into. The rest (Ward, Shatter, the three Gifts, Provoke,
        // RestorePartyMana) need a talent tree, an existing ward or a drained
        // party before they show anything at all, and faking one would make
        // the picture a picture of the fake.
        private static readonly SkillEffect[] Supported =
        {
            SkillEffect.DamageSingle,
            SkillEffect.DamageAll,
            SkillEffect.HealSelf,
            SkillEffect.HealParty,
            SkillEffect.BuffParty,
            SkillEffect.Summon,
            SkillEffect.Transform,
        };

        // WHICH ELEMENT A PREVIEW CASTS, and what its art therefore is.
        //
        // THE ONE THE AUTHOR ASKED FOR, and failing that THE FIRST ONE THAT
        // DRAWS SOMETHING, falling back to the first. A preview exists to
        // photograph art, and an elemental spell may author art on one element
        // and none on the others -- prismatic_orb had four elements and one
        // Water block. Taking element[0] unconditionally photographed Earth,
        // which drew nothing, and the picture was an empty stage with a damage
        // number: evidence that the pilot does not work, produced by a preview
        // that never cast it.
        //
        // The default rule survived that fix and then became the whole problem
        // once the other three were drawn: with four elements that all draw,
        // "the first that draws" always means Earth, and photographing Wind
        // meant reordering elements[] in skills.json, capturing, and putting
        // the order back (AUDIT #107). `requested` is that ask, and an element
        // the skill does not offer is refused by name rather than approximated
        // -- see ElementRefusal, which is the sentence an author reads.
        //
        // DERIVED FROM CONTENT, not from an id. "Has art" is a question of the
        // presentation (SpellPresentation.HasArt); no spell is named here and
        // a second elemental spell gets the same treatment for free.
        //
        // HERE rather than in the controller because BOTH preview routes need
        // the same answer: the Editor one presses the row, and the headless
        // one has to know which presentation to time its samples against. Two
        // copies of this choice would photograph one element and caption it
        // with another's timing.
        public static ElementChoice PreviewElementOf(ResolvedSkill skill) =>
            PreviewElementOf(skill, null);

        public static ElementChoice PreviewElementOf(ResolvedSkill skill, string requested)
        {
            var elements = skill?.Elements;
            if (elements == null || elements.Length == 0) return null;

            if (!string.IsNullOrWhiteSpace(requested))
            {
                // NULL RATHER THAN A FALLBACK when the ask cannot be honoured.
                // Every caller reaches this through a Plan that ElementRefusal
                // has already screened, so the only way here is a caller that
                // skipped the screen -- and quietly casting a different element
                // than the one on the command line is exactly the picture this
                // whole flag exists to stop being taken.
                return elements.FirstOrDefault(
                    c => c != null &&
                         string.Equals(c.Type.ToString(), requested.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            foreach (var choice in elements)
            {
                if (choice != null && choice.Vfx != null && choice.Vfx.HasArt) return choice;
            }

            return elements[0];
        }

        // WHY AN ASKED-FOR ELEMENT CANNOT BE CAST, or null when it can.
        //
        // Both refusals name the element the author typed and list what the
        // skill actually offers, because the whole cost of getting this wrong
        // is one Unity boot: tools/preview.ps1 runs the same check against
        // skills.json before anything starts, and this is the copy that catches
        // a request arriving from anywhere else (the Editor route, a stale
        // request file, a capture fixture run by hand).
        public static string ElementRefusal(ResolvedSkill skill, string requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) return null;

            string asked = requested.Trim();
            var elements = skill?.Elements ?? Array.Empty<ElementChoice>();
            var offered = elements.Where(c => c != null).Select(c => c.Type.ToString()).ToList();

            if (offered.Count == 0)
            {
                return $"'{skill?.Id}' offers no element choice at all, so it cannot be cast as '{asked}'. " +
                       "Drop -Element, or preview a skill whose elements[] is authored.";
            }

            if (PreviewElementOf(skill, asked) != null) return null;

            return $"'{skill.Id}' does not offer element '{asked}'. It offers: " +
                   string.Join(", ", offered) + ".";
        }

        // The presentation a preview cast of `skill` actually plays: the chosen
        // element's when it has one, the skill's own otherwise. What the
        // headless capture times its samples against.
        public static SpellPresentation PreviewPresentationOf(ResolvedSkill skill) =>
            PreviewPresentationOf(skill, null);

        public static SpellPresentation PreviewPresentationOf(ResolvedSkill skill, string requested)
        {
            if (skill == null) return SpellPresentation.None;

            var chosen = PreviewElementOf(skill, requested);
            return chosen != null && chosen.Vfx != null && chosen.Vfx.HasArt
                ? chosen.Vfx
                : skill.Vfx ?? SpellPresentation.None;
        }

        // What one preview fight is, once every question about it is settled.
        // A plan with a Refusal is a plan that must not be built: the caller
        // prints the reason and stops.
        public sealed class Plan
        {
            // Non-null means "do not build this fight". The reason names the
            // id or the effect, so the author reads what is wrong rather than
            // that something is.
            public string Refusal;

            public ResolvedSkill Skill;

            // The character definition id who casts it.
            public string CasterId;

            // WHICH ELEMENT THE CAST PRESSES, as the DamageType's own name, or
            // empty for "whichever PreviewElementOf picks". Carried on the plan
            // rather than re-derived at each of the three places that need it
            // (the forced press, the presentation the capture times against,
            // the line the author reads) because those three disagreeing is
            // the failure that looks right: a Wind picture timed off Earth.
            public string Element;

            // "lone" or "full", in FightBootstrap.DevForcedFormation's own
            // vocabulary. Not a preference: Summon needs a free slot and
            // DamageAll needs more than one thing to hit, so the formation is
            // derived from the effect rather than left to the author to get
            // right.
            public string Formation = FormationLone;

            // HealSelf/HealParty need something to heal. Half, not a sliver:
            // a heal capped by missing health shows its real number, and a
            // party at 1hp would make every heal look identical.
            public bool PartyStartsWounded;

            // Every accommodation made, in the author's words. Printed by
            // whoever ran the preview.
            public readonly List<string> Notes = new List<string>();

            public bool Ok => Refusal == null;
        }

        // The fraction of max health HealSelf/HealParty previews open on.
        private const float WoundedFraction = 0.5f;

        // ---- the spell plan --------------------------------------------------

        public static Plan ForSpell(string skillId) => ForSpell(skillId, null);

        // `element` is tools/preview.ps1 -Element, by the DamageType's own name
        // and case-insensitively. Empty means today's rule unchanged.
        public static Plan ForSpell(string skillId, string element)
        {
            var plan = new Plan();

            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            if (definition == null)
            {
                plan.Refusal = $"no skill '{skillId}' in the content database -- rebuild content, " +
                               "or check the id against ContentData/skills.json";
                return plan;
            }

            var skill = definition.Data;
            plan.Skill = skill;

            if (!Supported.Contains(skill.Effect))
            {
                plan.Refusal =
                    $"'{skillId}' resolves as {skill.Effect}, which the spell preview does not know how to " +
                    "stand a fight up for. It needs something the preview cannot fabricate honestly (a talent " +
                    "tree, an existing ward, a drained party). Supported: " +
                    string.Join(", ", Supported.Select(e => e.ToString())) + ". " +
                    "Cast it from a real run instead of being shown an approximation of it.";
                return plan;
            }

            // BEFORE THE CASTER LADDER, because an element the skill does not
            // offer is wrong about the command line rather than about content:
            // reporting "no character carries resource X" for a typo'd element
            // would send the author to characters.json.
            string elementRefusal = ElementRefusal(skill, element);
            if (elementRefusal != null)
            {
                plan.Refusal = elementRefusal;
                return plan;
            }

            var asked = PreviewElementOf(skill, element);
            if (asked != null && !string.IsNullOrWhiteSpace(element))
            {
                plan.Element = asked.Type.ToString();

                // AND WHETHER IT DRAWS, said here rather than discovered in the
                // picture. An element with no art is a legal thing to ask for
                // -- "does Wind have art yet" is a real question -- and the
                // answer arriving as an empty stage with a damage number reads
                // as a broken preview instead of an unauthored element.
                plan.Notes.Add(asked.Vfx != null && asked.Vfx.HasArt
                    ? $"cast as {plan.Element} because -Element asked for it, not because it is the " +
                      "first element that draws"
                    : $"cast as {plan.Element} because -Element asked for it -- and {plan.Element} " +
                      "authors no art, so the stage shows the damage number and nothing else");
            }

            plan.CasterId = ChooseCaster(skill, Roster(), plan);
            if (plan.CasterId == null) return plan;

            // THE FORMATION IS THE EFFECT'S, not the author's. A Summon into a
            // full stage has no slot and reports a skipped ability; DamageAll
            // against one enemy is indistinguishable from DamageSingle.
            if (skill.Effect == SkillEffect.Summon)
            {
                plan.Formation = FormationLone;
                plan.Notes.Add("one enemy fielded, so the summon has a slot to arrive in");
            }
            else if (skill.Effect == SkillEffect.DamageAll || skill.Targeting == SkillTargeting.AllEnemies)
            {
                plan.Formation = FormationFull;
                plan.Notes.Add("three enemies fielded, so an all-target cast has more than one thing to hit");
            }

            if (skill.Effect == SkillEffect.HealSelf || skill.Effect == SkillEffect.HealParty)
            {
                plan.PartyStartsWounded = true;
                plan.Notes.Add("the party opens at half health, so a heal has something to restore");
            }

            if (definition.Data.UnlockLevel > 1)
            {
                plan.Notes.Add($"unlock level {definition.Data.UnlockLevel} bypassed for this fight only");
            }

            if (definition.Data.BookOnly)
            {
                plan.Notes.Add("book ownership bypassed for this fight only");
            }

            return plan;
        }

        // THE ROSTER, as records. The catalogue is asked for its characters
        // exactly here, so every decision below is made over data rather than
        // over the content database.
        private static IReadOnlyList<ResolvedCharacter> Roster() =>
            ContentDatabase.Characters
                .Where(c => c != null && c.Data != null)
                .Select(c => c.Data)
                .ToList();

        // WHO CASTS IT, in the order the author would guess.
        //
        // 1. The character the skill is authored against. Anything else would
        //    be a lie about whose ability this is.
        // 2. Failing that -- an unowned or book skill -- whoever in the roster
        //    can actually PAY for it. A skill that spends a signature resource
        //    is uncastable by a character who has none, and handing it to one
        //    anyway produces a fight where the row is permanently greyed and
        //    the author is left guessing why.
        // 3. Failing that, the roster lead with battle art, because the point
        //    of the exercise is to look at something.
        //
        // PUBLIC, AND THE ROSTER IS AN ARGUMENT. Both refusals this ladder can
        // reach -- "no character in content carries resource X" and "there are
        // no characters at all" -- are UNREACHABLE against the content in this
        // repo: Shawn carries Wool and the roster is never empty. A refusal
        // nothing can provoke is a refusal nobody has read, and the message IS
        // the deliverable here (an author's whole experience of a refused
        // preview is the sentence it prints). Taking the roster as a parameter
        // is what lets PreviewFightRefusalTests walk every rung with a
        // hand-built one; it costs exactly one call site, ForSpell above.
        public static string ChooseCaster(
            ResolvedSkill skill, IReadOnlyList<ResolvedCharacter> roster, Plan plan)
        {
            roster = roster ?? Array.Empty<ResolvedCharacter>();

            if (!string.IsNullOrWhiteSpace(skill.CharacterId))
            {
                var owner = roster.FirstOrDefault(c => c != null && c.Id == skill.CharacterId);
                if (owner != null) return owner.Id;

                plan.Notes.Add($"'{skill.CharacterId}' is not in the roster, so a stand-in casts it");
            }

            if (skill.CostsResource)
            {
                // WHICH resource: the one the authored owner carries, when
                // there is an authored owner to ask. A skill costs "resource"
                // in the abstract -- ResolvedSkill has a cost and no id, since
                // a character has exactly one -- so the only way to name the
                // one it means is through whoever it belongs to.
                string wanted = SignatureIdOf(roster, skill.CharacterId);

                var payer = roster.FirstOrDefault(
                    c => c != null && c.HasSignatureResource &&
                         (wanted == null || c.SignatureId == wanted));

                if (payer != null)
                {
                    plan.Notes.Add($"cast by '{payer.Id}', who carries the " +
                                   $"{payer.SignatureDisplayName ?? payer.SignatureId} it spends");
                    return payer.Id;
                }

                plan.Refusal = $"no character in content carries resource {wanted ?? "(any signature resource)"}, " +
                               $"which '{skill.Id}' spends ({skill.ResourceCost} " +
                               (skill.SpendsAllResource ? "and all of it" : "point(s)") + "). " +
                               "Author a character with that signature resource, or preview a skill that does " +
                               "not spend one -- a caster who cannot pay shows a greyed row and nothing else.";
                return null;
            }

            var lead = roster.FirstOrDefault(
                           c => c != null && !string.IsNullOrWhiteSpace(c.BattleSpritePath))
                       ?? roster.FirstOrDefault(c => c != null);

            if (lead == null)
            {
                plan.Refusal = "there are no characters in the content database at all, so nothing can cast it.";
                return null;
            }

            if (lead.Id != skill.CharacterId)
            {
                plan.Notes.Add($"cast by the roster lead '{lead.Id}' -- the skill names no owner in the roster");
            }

            return lead.Id;
        }

        private static string SignatureIdOf(IReadOnlyList<ResolvedCharacter> roster, string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return null;
            var definition = roster.FirstOrDefault(c => c != null && c.Id == characterId);
            return definition != null && definition.HasSignatureResource ? definition.SignatureId : null;
        }

        // ---- the character plan ----------------------------------------------

        // tools/preview.ps1 -Character <id>. Far less to decide than a spell:
        // the squad is this one character, and turn one is whatever their own
        // kit puts first.
        public static Plan ForCharacter(string characterId)
        {
            var plan = new Plan();

            var definition = ContentDatabase.Characters.FirstOrDefault(c => c != null && c.id == characterId);
            if (definition == null)
            {
                plan.Refusal = $"no character '{characterId}' in the content database -- rebuild content, " +
                               "or check the id against ContentData/characters.json";
                return plan;
            }

            plan.CasterId = definition.id;

            // NOT A REFUSAL. A character with no battle art still has a
            // portrait and a dossier worth looking at, and the fallback plate
            // on the stage IS the report -- refusing would hide the very thing
            // the author most likely wants to see the state of.
            if (string.IsNullOrWhiteSpace(definition.Data.BattleSpritePath))
            {
                plan.Notes.Add("no battleSpritePath, so the stage shows a fallback plate rather than art");
            }

            if (string.IsNullOrWhiteSpace(definition.Data.PortraitPath))
            {
                plan.Notes.Add("no portraitPath, so the dossier keeps its armour-stand placeholder");
            }

            string opener = FirstSelectableSkillFor(definition.id);
            if (opener == null)
            {
                plan.Notes.Add("no skill they can press at level 1, so turn one is left to whoever is watching");
            }
            else
            {
                var skill = ContentDatabase.Skills.FirstOrDefault(sk => sk != null && sk.id == opener);
                plan.Skill = skill?.Data;
                plan.Notes.Add($"turn one casts '{opener}', the first row on their own kit");
            }

            return plan;
        }

        // THEIR FIRST ROW, through the one function that already knows what
        // "this character can press this" means.
        //
        // ContentDatabase.AvailableSkillsFor rather than a fourth hand-rolled
        // union of PlayerSelectable/CharacterId/UnlockLevel: the in-run kit
        // asks exactly this question through exactly this door, and the last
        // time somebody wrote their own copy of it the level filter went
        // missing and a level-1 Shawn walked in holding the whole talent tree
        // (see FightEncounterAdapter.KitFor's own header). A bare level-1
        // Character with no talents is what the preview's placeholder kit is
        // built at, so the answers agree.
        public static string FirstSelectableSkillFor(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return null;

            var available = ContentDatabase.AvailableSkillsFor(new Character(characterId));
            return available.Count > 0 ? available[0].id : null;
        }

        // WHO IS ON THE OTHER SIDE. Art-filtered, because a preview exists to
        // be looked at and a party of fallback plates defeats it; ordered by
        // sortOrder only as a tie-break, exactly as FightBootstrap's own pick
        // always did.
        //
        // Shared rather than copied so the headless capture and the Editor
        // route field the SAME monsters. A picture of a fight the author
        // cannot reproduce by pressing play is not evidence about anything.
        public static List<string> EnemiesWithArt(int count)
        {
            var withArt = ContentDatabase.Enemies
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Data.SpritePath))
                .OrderBy(e => e.SortOrder)
                .Take(count)
                .Select(e => e.id)
                .ToList();

            if (withArt.Count > 0) return withArt;

            return ContentDatabase.Enemies.Take(count).Select(e => e.id).ToList();
        }

        // How many of them a formation means. This file's own vocabulary
        // (FormationLone/FormationFull above), read here so "lone" cannot
        // come to mean one thing on one route and something else on another.
        public static int EnemyCountFor(string formation, int fallback) =>
            formation == FormationLone ? 1 : fallback;

        // ---- standing the fight up -------------------------------------------

        // Everything that has to be true of the built fight before the cast is
        // worth photographing, applied to the PREVIEW'S OWN CombatantState --
        // never to a record, an asset or a save.
        //
        // Cooldowns need no clearing here and that is worth saying rather than
        // leaving as an absence: a FightSession opens with an empty cooldown
        // table, so "cooldowns start clear" is already true of every fight and
        // a line zeroing them would be a line describing nothing.
        public static void Prepare(FightEncounterAdapter.BuiltFight built, Plan plan)
        {
            if (built?.Session == null || plan == null || !plan.Ok) return;

            var caster = built.Session.Encounter.LivingPlayerParty.FirstOrDefault()
                         ?? built.Party?.FirstOrDefault();
            if (caster == null) return;

            // MANA AND RESOURCE FULL. A preview that opens on an unaffordable
            // row photographs a greyed button, and the author reads it as the
            // spell being broken rather than the caster being poor.
            caster.CurrentMana = caster.MaxMana;
            if (caster.Signature != null)
            {
                caster.Signature.Current = caster.Signature.Max;
                plan.Notes.Add($"{caster.Signature.DisplayName} filled to " +
                               $"{caster.Signature.Max} for the cast");
            }

            // REQUIREMENTS WAIVED, ONE SCORE AT A TIME AND ONLY THE UNMET ONES.
            //
            // The alternative -- a blanket high block, or a session-wide
            // "ignore requirements" flag -- would change the damage the popup
            // shows, because ability scores feed the scaling the cast rides.
            // Raising exactly the scores the skill demands, to exactly what it
            // demands, is the smallest lie that lets the row be pressed, and
            // every point of it is named below.
            var required = plan.Skill == null
                ? default(AbilityScoreBlock)
                : RequirementCurve.Apply(plan.Skill.Requirements);

            if (!caster.AbilityScores.Meets(required))
            {
                var raised = new List<string>();
                var scores = caster.AbilityScores;

                foreach (AbilityScore score in Domain.Stats.AbilityScores.All)
                {
                    if (scores[score] >= required[score]) continue;
                    raised.Add($"{score} {scores[score]}->{required[score]}");
                    scores = scores.With(score, required[score]);
                }

                caster.AbilityScores = scores;
                plan.Notes.Add("ability requirements waived for the preview cast: " + string.Join(", ", raised) +
                               " (this raises the scores the cast scales on, so the damage number is the " +
                               "waived one, not what a real caster would roll)");
            }

            if (plan.PartyStartsWounded)
            {
                foreach (var member in built.Session.Encounter.LivingPlayerParty)
                {
                    member.CurrentHealth = Math.Max(1, (int)(member.MaxHealth * WoundedFraction));
                }
            }
        }

        // The one line the author reads back. Kept here rather than at each
        // call site so the Editor console and the capture log say the same
        // thing about the same fight.
        public static string Describe(Plan plan)
        {
            if (plan == null) return "no preview plan";
            if (!plan.Ok) return "REFUSED: " + plan.Refusal;

            string notes = plan.Notes.Count == 0 ? "nothing waived" : string.Join("; ", plan.Notes);

            // A character plan may carry no skill at all -- see ForCharacter's
            // "no skill they can press at level 1" note -- so the subject of
            // the sentence is the caster either way and the spell is the
            // optional half.
            string subject = plan.Skill == null
                ? $"'{plan.CasterId}'"
                : $"'{plan.Skill.Id}' ({plan.Skill.Effect}) cast by '{plan.CasterId}'";

            return $"{subject} against a {plan.Formation} formation -- {notes}";
        }
    }
}
