using UnityEngine;

namespace PrincesPalace
{
    // One id and the sprite that belongs to it, as a single serialized thing.
    //
    // WHAT THIS REPLACES, and why the reasoning that produced it was one step
    // short. Five controllers carried `string[] iconIds` beside `Sprite[]
    // iconSprites`, wired at five sites in ScreenRegistry as two
    // `.Select().ToArray()` calls over one collection. Same length, same order,
    // forever, held together by nothing but both lines being written at once.
    //
    // ItemIcons' header stated the constraint that produced that shape, and it
    // is true as far as it goes:
    //
    //     Two parallel arrays rather than a dictionary, because a scene
    //     serialises arrays and does not serialise dictionaries.
    //
    // Dictionary versus array is real. The conclusion skipped the option
    // between them: Unity serialises an array of a [Serializable] struct
    // perfectly well. That keeps everything the original argument wanted --
    // it serialises, the registry binds it at build time, the wiring sweep can
    // see it -- while making "same length, same order" a property of the type
    // rather than a rule five files have to keep.
    //
    // A STRUCT, not a class: it is two fields with no identity of its own, it
    // is never null-checked as a whole, and an array of them is one allocation
    // instead of one per entry.
    [System.Serializable]
    public struct IconEntry
    {
        public string Id;
        public Sprite Sprite;

        public IconEntry(string id, Sprite sprite)
        {
            Id = id;
            Sprite = sprite;
        }
    }
}
