using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Preview
{
    // WHAT STAGE A SPELL PREVIEW STANDS UP, decided from the skill alone.
    //
    // PreviewFight (Core) owns the content lookups -- which skill, which
    // roster, which enemies have art -- and asks this for everything that
    // follows from the skill's own shape: whether the preview can show it at
    // all, how many enemies it is cast against, how many squadmates stand
    // beside the caster, and whether the party has to be hurt first. Kept in
    // Domain so those rules run under the dotnet host with a hand-built skill
    // (PreviewStageTests) rather than only inside a Unity boot.
    //
    // THE POSTURE IS PreviewFight's: a skill this cannot stage honestly is
    // refused by name, never approximated.
    public sealed class PreviewStage
    {
        // Which SkillEffects the preview knows how to stand a fight up for.
        //
        // Deliberately a short list, and deliberately not "everything the
        // session can resolve". Each member here has a stage arrangement that
        // makes its cast visible -- a target to hit, a wound to heal, a slot
        // to summon into, a squadmate to trade places with. The rest (Ward,
        // Shatter, the three Gifts, Provoke, RestorePartyMana, Reclaim,
        // Hasten) need a talent tree, an existing ward, a drained party, a
        // status already on the target or a particular turn schedule before
        // they show anything at all, and faking one would make the picture a
        // picture of the fake.
        private static readonly SkillEffect[] SupportedEffects =
        {
            SkillEffect.DamageSingle,
            SkillEffect.DamageAll,
            SkillEffect.HealSelf,
            SkillEffect.HealParty,
            // Mend, and any other single-ally heal: a wounded party is a
            // stage arrangement this already knows how to build, which is
            // the whole test for membership here.
            SkillEffect.HealSingle,
            SkillEffect.BuffParty,
            SkillEffect.Summon,
            SkillEffect.Transform,
            // Court needs no fabricated prerequisite: a normal three-enemy
            // field shows Fear and the caster's drawback, while the focused
            // domain test owns the authored-boss fallback branch.
            SkillEffect.Enthrall,
            // Velvet Shackles, Censer of Embers, Thorn Tithe: one enemy and
            // the row's own status, which is DamageSingle's stage exactly.
            // The status lands on a fresh target with nothing fabricated.
            SkillEffect.Afflict,
            // Palace Passage: two picks on the caster's own side, so the
            // stage needs a second body there -- see PartySize. Nothing about
            // the swap itself is faked; either pick being Rooted is the only
            // refusal and a fresh fight roots nobody.
            SkillEffect.SwapAllies,
        };

        public static IReadOnlyList<SkillEffect> Supported => SupportedEffects;

        // Non-null means "do not build this fight". Names the skill and its
        // effect, and lists what would work.
        public string Refusal;

        public bool Ok => Refusal == null;

        // Three enemies rather than one. DamageAll against one enemy is
        // indistinguishable from DamageSingle; Summon needs a free slot, so it
        // is always one.
        public bool FullFormation;

        // How many characters stand on the player side, caster included.
        //
        // AS MANY AS THE CAST PICKS ALLIES. SkillEffects.PicksRequired is the
        // one answer to "how many picks" (the menu, the session and the bot
        // all read it), and a preview party smaller than that leaves the
        // harness's second pick with nobody to press -- the cast stays open on
        // the rack and the photograph is of a menu. One for everything that
        // does not pick allies, which is every other supported effect.
        public int PartySize = 1;

        // HealSelf/HealParty/HealSingle need something to heal.
        public bool PartyStartsWounded;

        // Every accommodation, in the author's words.
        public readonly List<string> Notes = new List<string>();

        public static bool IsSupported(SkillEffect effect) => Array.IndexOf(SupportedEffects, effect) >= 0;

        public static PreviewStage For(ResolvedSkill skill)
        {
            var stage = new PreviewStage();
            if (skill == null)
            {
                stage.Refusal = "no skill to stage";
                return stage;
            }

            if (!IsSupported(skill.Effect))
            {
                stage.Refusal =
                    $"'{skill.Id}' resolves as {skill.Effect}, which the spell preview does not know how to " +
                    "stand a fight up for. It needs something the preview cannot fabricate honestly (a talent " +
                    "tree, an existing ward, a drained party, a status already on the target, a particular " +
                    "turn schedule). Supported: " +
                    string.Join(", ", SupportedEffects.Select(e => e.ToString())) + ". " +
                    "Cast it from a real run instead of being shown an approximation of it.";
                return stage;
            }

            // THE FORMATION IS THE EFFECT'S, not the author's.
            if (skill.Effect == SkillEffect.Summon)
            {
                stage.FullFormation = false;
                stage.Notes.Add("one enemy fielded, so the summon has a slot to arrive in");
            }
            else if (skill.Effect == SkillEffect.DamageAll || skill.Targeting == SkillTargeting.AllEnemies)
            {
                stage.FullFormation = true;
                stage.Notes.Add("three enemies fielded, so an all-target cast has more than one thing to hit");
            }

            if (skill.Targeting == SkillTargeting.SingleAlly)
            {
                stage.PartySize = Math.Max(1, SkillEffects.PicksRequired(skill.Effect));
                if (stage.PartySize > 1)
                {
                    stage.Notes.Add($"{stage.PartySize} on the player side, so each of the cast's " +
                                    $"{stage.PartySize} ally picks has somebody to land on");
                }
            }

            if (skill.Effect == SkillEffect.HealSelf || skill.Effect == SkillEffect.HealParty
                || skill.Effect == SkillEffect.HealSingle)
            {
                stage.PartyStartsWounded = true;
                stage.Notes.Add("the party opens at half health, so a heal has something to restore");
            }

            return stage;
        }

        // WHO STANDS BESIDE THE CASTER, caster first -- the adapter gives the
        // preview skill to the first member only, and PreviewFight.Prepare
        // fills that member's pools.
        //
        // SQUADMATES WHO DO NOT OUTPACE THE CASTER. The forced press fires on
        // the first player turn, and a squadmate who opens the player side
        // gets a turn whose kit does not hold the spell: the press is refused
        // and consumed, and nothing is cast. Ties go to the caster, who is
        // added to the turn order first and keeps that place through its
        // stable sort. Among those, art first (a preview exists to be looked
        // at), then roster order.
        //
        // Null, with stage.Refusal set, when the roster cannot supply enough
        // of them -- a party short of what the cast picks is the half-made
        // cast PartySize exists to prevent.
        public static List<string> Squad(
            PreviewStage stage, string casterId, IReadOnlyList<ResolvedCharacter> roster)
        {
            var squad = new List<string> { casterId };
            if (stage == null || stage.PartySize <= 1) return squad;

            roster = roster ?? Array.Empty<ResolvedCharacter>();
            var caster = roster.FirstOrDefault(c => c != null && c.Id == casterId);
            int casterSpeed = caster == null
                ? int.MaxValue
                : AbilityDerivation.BaseSpeed(caster.BaseStats, caster.AbilityScores);

            var others = roster.Where(c => c != null && c.Id != casterId).ToList();
            var slowEnough = others
                .Where(c => AbilityDerivation.BaseSpeed(c.BaseStats, c.AbilityScores) <= casterSpeed)
                .OrderBy(c => string.IsNullOrWhiteSpace(c.BattleSpritePath) ? 1 : 0)
                .Take(stage.PartySize - 1)
                .ToList();

            if (slowEnough.Count < stage.PartySize - 1)
            {
                stage.Refusal =
                    $"the cast needs {stage.PartySize} on the player side, and the roster has only " +
                    $"{slowEnough.Count} other character(s) no faster than '{casterId}' " +
                    $"(of {others.Count} in all). A faster squadmate would take the first player turn, and " +
                    "the forced press would land on a kit that does not hold the spell.";
                return null;
            }

            foreach (var mate in slowEnough)
            {
                squad.Add(mate.Id);
                if (string.IsNullOrWhiteSpace(mate.BattleSpritePath))
                {
                    stage.Notes.Add($"squadmate '{mate.Id}' has no battle art, so stands as a fallback plate");
                }
            }

            stage.Notes.Add("squadmate(s) " + string.Join(", ", slowEnough.Select(c => $"'{c.Id}'")) +
                            $" stand beside '{casterId}', none of them faster, so the caster opens the player side");
            return squad;
        }
    }
}
