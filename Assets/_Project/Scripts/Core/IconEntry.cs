using UnityEngine;

namespace PrincesPalace
{
    // One id and the sprite that belongs to it, as a single serialized thing.
    //
    // A scene serialises arrays and does not serialise dictionaries, but an
    // array of a [Serializable] struct is fine. So the id and its sprite
    // travel together: it serialises, the registry binds it at build time, the
    // wiring sweep can see it, and "same length, same order" is a property of
    // the type rather than a rule every controller has to keep with two
    // parallel arrays.
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
