using System;

namespace PrincesPalace.Domain.UiKit
{
    // A 2D vector. Exists because this assembly is engine-free
    // (noEngineReferences: true) and therefore cannot see UnityEngine.Vector2 —
    // which is the whole point: putting the layout vocabulary here is what lets
    // a screen be built, solved and audited from an EditMode test in under a
    // second, with no scene, no Canvas and no Unity object lifecycle.
    //
    // The emitter converts to Vector2 at the boundary and nowhere else.
    public readonly struct UiVec : IEquatable<UiVec>
    {
        public readonly float X;
        public readonly float Y;

        public UiVec(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static UiVec Zero => new UiVec(0f, 0f);
        public static UiVec One => new UiVec(1f, 1f);

        // The canvas convention this whole layer is authored in: centre origin,
        // +y UP, 1920x1080 reference frame ("authored == rendered"). Anything
        // that reads like a screen coordinate with +y DOWN is a bug at the
        // boundary, not here.
        public static UiVec Centre => new UiVec(0.5f, 0.5f);

        public UiVec WithX(float x) => new UiVec(x, Y);
        public UiVec WithY(float y) => new UiVec(X, y);

        public static UiVec operator +(UiVec a, UiVec b) => new UiVec(a.X + b.X, a.Y + b.Y);
        public static UiVec operator -(UiVec a, UiVec b) => new UiVec(a.X - b.X, a.Y - b.Y);
        public static UiVec operator *(UiVec a, float s) => new UiVec(a.X * s, a.Y * s);

        public bool Equals(UiVec other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is UiVec other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }
}
