using PrincesPalace.Domain.Content;

namespace PrincesPalace.Content
{
    // One skill of a character's strip with the route it arrived by.
    public readonly struct SourcedSkill
    {
        public readonly SkillDefinition Skill;
        public readonly SkillSource Source;

        public SourcedSkill(SkillDefinition skill, SkillSource source)
        {
            Skill = skill;
            Source = source;
        }
    }
}
