using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The "bolt"/"hero" test skill, built in one place.
    //
    // Six EditMode files each declared their own private ResolvedSkill
    // builder for the same shape -- a single-target damage spell called
    // "bolt", cast by "hero" -- because ResolvedSkill's constructor takes
    // eighteen-plus positional arguments and every test only cares about
    // two or three of them. One factory here means ResolvedSkill growing a
    // parameter is a one-file fix instead of a six-file hunt.
    //
    // Every variant below reproduces the exact skill its original caller
    // built -- same power/flat/cooldown/packets/targeting -- not a
    // cleverer API. A caller whose skill cannot be expressed through these
    // keeps its own private helper.
    internal static class TestSkills
    {
        // The scaled bolt: FlatAmount rides Power/scaling rather than a
        // fixed packet, and ignoresDefense=true so a base amount reads as
        // exactly its flatAmount -- what lets callers name literal numbers.
        // Targeting follows the effect because DamageAll callers (the
        // fourth-epic-relic sweep tests) need AllEnemies, not SingleEnemy.
        public static ResolvedSkill Bolt(
            int flat = 0, SkillEffect effect = SkillEffect.DamageSingle, int cooldown = 0) =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1, effect,
                effect == SkillEffect.DamageAll ? SkillTargeting.AllEnemies : SkillTargeting.SingleEnemy,
                0, 0, false, 0, flat, ignoresDefense: true,
                null, SpellPresentation.None, 0, cooldownTurns: cooldown);

        // The ranged bolt: a FIXED damage packet rather than a scaled
        // amount, so its output does not ride the caster's Attack stat and
        // callers can dial it below or above Attack on purpose. Used by the
        // bot's ranged-reach policy tests, where the point is a foe the
        // party cannot melee.
        public static ResolvedSkill RangedPacket(int fixedDamage = 100) =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Physical, fixedDamage) },
                SpellPresentation.None, 0);

        // The castable bolt: Power=100 rather than FlatAmount, and defence
        // is not bypassed. Used by the magic-marker tests, where what
        // matters is that the skill is castable at all, not its exact
        // output.
        public static ResolvedSkill CastableSkill() =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 100, 0, false,
                null, SpellPresentation.None, 0);
    }
}
