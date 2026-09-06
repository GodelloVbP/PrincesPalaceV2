using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Stats
{
    // WHICH ELEMENTS A COMBATANT TAKES BADLY AND WHICH IT SHRUGS OFF -- both as
    // sets, not one apiece.
    //
    // This was two plain DamageType fields on the enemy, threaded through the
    // damage pipeline as a nullable pair. That shape could only ever say "weak
    // to exactly one thing, resists exactly one thing", which turned a piece of
    // creature design into an arbitrary content decision: a troll that should
    // burn AND freeze had to pick which, and a resistance authored to cover
    // three kinds of harm had to drop two of them. The maths never needed the
    // limit -- EffectivenessMultiplier asks one question, "does this attack
    // match", and a set answers it exactly as cheaply as a single value does.
    //
    // BITMASKS rather than arrays, for three reasons that all matter on the
    // damage path: six elements fit in an int with room to spare, membership is
    // one test instead of a scan, and the struct copies with no allocation --
    // this rides along with every packet of damage in the game. The fourth is
    // the one that made the migration cheap: "no affinity at all" is
    // default(ElementalAffinity), which is precisely what the pipeline's old
    // `null` meant and what every combatant with no definition behind it (every
    // player character, every direct test fixture) now gets for free.
    //
    // OVERLAP IS NOT RESOLVED HERE. An element in both sets is a content
    // mistake; ContentDatabase.Validation rejects it at build time, and
    // CombatMath checks weakness first so a mistake that somehow reaches play
    // resolves as a weakness rather than silently cancelling out. Same
    // precedence the single-value version had, for the same reason.
    public readonly struct ElementalAffinity : IEquatable<ElementalAffinity>
    {
        private readonly int _weaknessMask;
        private readonly int _resistanceMask;

        private ElementalAffinity(int weaknessMask, int resistanceMask)
        {
            _weaknessMask = weaknessMask;
            _resistanceMask = resistanceMask;
        }

        // Weak to nothing, resistant to nothing: every attack lands at face
        // value. default(ElementalAffinity) is this, deliberately -- see the
        // header.
        public static ElementalAffinity Neutral => default;

        // THE SINGLE-ELEMENT CASE, which is still most of the roster and reads
        // better than a pair of one-item collections at every call site that
        // only wants to say "weak to fire, resists ice".
        public static ElementalAffinity Of(DamageType weakness, DamageType resistance) =>
            new ElementalAffinity(Bit(weakness), Bit(resistance));

        public static ElementalAffinity Of(
            IEnumerable<DamageType> weaknesses, IEnumerable<DamageType> resistances) =>
            new ElementalAffinity(MaskOf(weaknesses), MaskOf(resistances));

        // THE ARRAY OVERLOAD EXISTS TO NOT ALLOCATE, and that is its whole
        // reason. ResolvedEnemy.Affinity computes this on every read rather
        // than memoising it into the shared catalogue record, so it is on the
        // damage path: foreach over an IEnumerable<DamageType> boxes an
        // enumerator per call, foreach over a DamageType[] does not. Same
        // masks, same answer.
        public static ElementalAffinity Of(DamageType[] weaknesses, DamageType[] resistances) =>
            new ElementalAffinity(MaskOf(weaknesses), MaskOf(resistances));

        public bool IsWeakTo(DamageType type) => (_weaknessMask & Bit(type)) != 0;

        public bool Resists(DamageType type) => (_resistanceMask & Bit(type)) != 0;

        // Private: the only reader is ToString below. Production code asks for
        // ElementalAffinity.Neutral when it wants the value and IsWeakTo /
        // Resists when it wants an answer -- neither needs to ask "is this
        // nothing at all".
        private bool IsNeutral => _weaknessMask == 0 && _resistanceMask == 0;

        // The elements named in each set, in DamageType declaration order so a
        // glossary row reads the same every time it is built. Materialised on
        // demand because the only callers are display and validation -- nothing
        // on the damage path ever enumerates.
        public IReadOnlyList<DamageType> Weaknesses => Listed(_weaknessMask);

        public IReadOnlyList<DamageType> Resistances => Listed(_resistanceMask);

        // Elements claimed as BOTH, which is the one authoring mistake this type
        // cannot express its way out of. Returned rather than thrown on, so the
        // content validator can name every offender in one message.
        public IReadOnlyList<DamageType> Contradictions => Listed(_weaknessMask & _resistanceMask);

        // CACHED, because Enum.GetValues is a reflection call that allocates a
        // fresh array on every invocation -- and Listed is reached from content
        // validation, which asks it once per enemy per build.
        private static readonly DamageType[] AllTypes =
            (DamageType[])Enum.GetValues(typeof(DamageType));

        private static int Bit(DamageType type) => 1 << (int)type;

        private static int MaskOf(IEnumerable<DamageType> types)
        {
            if (types == null) return 0;

            int mask = 0;
            foreach (var type in types) mask |= Bit(type);
            return mask;
        }

        private static int MaskOf(DamageType[] types)
        {
            if (types == null) return 0;

            int mask = 0;
            for (int i = 0; i < types.Length; i++) mask |= Bit(types[i]);
            return mask;
        }

        private static IReadOnlyList<DamageType> Listed(int mask)
        {
            if (mask == 0) return Array.Empty<DamageType>();

            var listed = new List<DamageType>();
            foreach (var type in AllTypes)
            {
                if ((mask & Bit(type)) != 0) listed.Add(type);
            }

            return listed;
        }

        public bool Equals(ElementalAffinity other) =>
            _weaknessMask == other._weaknessMask && _resistanceMask == other._resistanceMask;

        public override bool Equals(object obj) => obj is ElementalAffinity other && Equals(other);

        public override int GetHashCode() => (_weaknessMask * 397) ^ _resistanceMask;

        public override string ToString() =>
            IsNeutral ? "neutral" : $"weak[{string.Join(",", Weaknesses)}] resist[{string.Join(",", Resistances)}]";
    }
}
