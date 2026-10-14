using System.Collections.Generic;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // Which worn traits do nothing because another holder already gives the
    // wearer the same mechanic (docs/PLAN_ITEMS.md 1.3). A mechanic is a Flag
    // (EffectStacking): a second holder adds nothing, so combat already reads
    // one copy; this names WHICH one the dossier marks "inactive".
    //
    // A relic reaching the wearer came first, then worn copies in slot order,
    // so the later copy is the inactive one.
    public static class TraitStacking
    {
        // `wornKinds[i]` is the trait kind on the i-th worn copy in slot order
        // (None where the copy holds no trait); `relicKinds` the mechanics
        // relics already give the wearer. The result has one entry per worn
        // copy: true where the copy's trait does nothing.
        public static bool[] Inactive(IReadOnlyList<EffectKind> wornKinds, IEnumerable<EffectKind> relicKinds)
        {
            var result = new bool[wornKinds == null ? 0 : wornKinds.Count];
            var seen = new HashSet<EffectKind>(relicKinds ?? new EffectKind[0]);
            for (int i = 0; i < result.Length; i++)
            {
                var kind = wornKinds[i];
                if (kind == EffectKind.None) continue;
                if (!seen.Add(kind)) result[i] = true;
            }

            return result;
        }
    }
}
