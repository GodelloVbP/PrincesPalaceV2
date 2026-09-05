using System;

namespace PrincesPalace.Domain.Content
{
    // One sentence describing what a content field means, read by reflection
    // when docs/CONTENT_SCHEMA.md is generated (see ContentSchema.cs).
    //
    // Deliberately separate from the field's own `//` comment rather than a
    // replacement for it: the comment is free to run long, cite a formula, or
    // explain a design decision (SkillEntryResolver's own constants, why Wool
    // absorbs at 1) -- exactly the prose a generator cannot know and that a
    // JSON _readme now keeps. This attribute is the one-line version a table
    // cell can hold, and it is the thing that goes stale the moment a field's
    // meaning changes without this being touched -- which is the entire
    // reason ContentSchemaTests checks every Raw*Entry field carries one.
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ContentDocAttribute : Attribute
    {
        public string Description { get; }

        public ContentDocAttribute(string description)
        {
            Description = description ?? "";
        }
    }
}
