namespace PrincesPalace.Content
{
    // Every content type states the order it is listed in.
    //
    // WHY THIS IS A TYPE AND NOT A CONVENTION. `Resources.LoadAll` returns
    // assets in filename (alphabetical) order, never authoring order, and the
    // resulting bug is the worst available shape: content loads, nothing
    // throws, and the order is PLAUSIBLE BUT WRONG -- talents out of sequence
    // in a tree, spell tiers not climbing, a shop listing its stock by the
    // first letter of the asset name. It was written down as gotcha #4 in
    // CLAUDE.md, which is to say it was enforced by whoever remembered reading
    // that file. Adding a content type is nine or ten touch points and this was
    // the only one with nothing to catch it.
    //
    // Now `ContentDatabase.LoadOrdered<T>` is constrained on this interface and
    // is the only place in the project that calls `Resources.LoadAll` for
    // content, so a new type either says how it is ordered or does not compile.
    // ContentLoadingLintTests keeps the second half of that true.
    //
    // NOT A FIELD, deliberately. Seven types order by an authored `sortOrder`
    // int, but a spell tier orders by LEVEL and a talent by its position in the
    // grid -- both meaningful keys that a second authored int could only
    // disagree with. A property lets each type name the thing it is actually
    // sorted by instead of maintaining a copy of it.
    public interface IOrderedContent
    {
        int SortOrder { get; }
    }
}
