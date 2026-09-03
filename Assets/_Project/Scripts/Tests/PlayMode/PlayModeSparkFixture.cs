using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // BasicSpell is gone (docs/PLAN_SHOP.md Gate 4) -- this is the shared
    // stand-in for it, wherever a PlayMode fixture used to lean on the free
    // "Spark" row every character got regardless of what they had learned.
    // Extracted after the same 3-line ResolvedSkill+PlayerKit block turned
    // up copy-pasted verbatim across 11 call sites in 8 files.
    internal static class PlayModeSparkFixture
    {
        internal static ResolvedSkill Skill() =>
            new ResolvedSkill("spark", "Spark", "", "shawn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 6, 0, false, 100, 0, false,
                null, SpellPresentation.None, 0);

        internal static PlayerKit Kit(DamageType? attackType = null, int level = 4) =>
            new PlayerKit("shawn", CharacterRole.Tank, new List<ResolvedSkill> { Skill() }, null, attackType, level: level);
    }
}
