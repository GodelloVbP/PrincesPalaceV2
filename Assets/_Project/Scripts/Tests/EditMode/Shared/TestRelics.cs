using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Fixture relics carrying the SHIPPED headline numbers, pinned as
    // literals (CLAUDE.md gotcha 5): combat tests build a relic here instead
    // of reading relics.json, and RelicHeadlineNumberTests pins relics.json
    // to this same table, so a retune fails one pin rather than every
    // mechanic test downstream of it.
    internal static class TestRelics
    {
        // Every relic mechanic with a headline magnitude, at its relics.json
        // value. A mechanic missing here is a flag (catalog UsesMagnitude
        // false) or reads a threshold instead.
        internal static readonly IReadOnlyDictionary<EffectKind, int> ShippedMagnitude = new Dictionary<EffectKind, int>
        {
            [EffectKind.MagicalShield] = 20,
            [EffectKind.ChargingCrystal] = 50,
            [EffectKind.BallerinasSlippers] = 10,
            [EffectKind.TinFoilPipe] = 20,
            [EffectKind.ToothedNecklace] = 30,
            [EffectKind.BountyHunterContract] = 3,
            [EffectKind.DrownedLantern] = 50,
            [EffectKind.SaltLedger] = 1,
            [EffectKind.LongCount] = 40,
            [EffectKind.MagicMarker] = 20,
            [EffectKind.CursedIdol] = 3,
            [EffectKind.AmassingStar] = 2,
            [EffectKind.RampagingBullsHorn] = 50,
            [EffectKind.SparringSaber] = 30,
            [EffectKind.SparringBuckler] = 10,
            [EffectKind.EssenceSiphon] = 3,
            [EffectKind.BerserkersVest] = 1,
            [EffectKind.PhoenixEgg] = 3,
            [EffectKind.TransformDurationBonus] = 1,
            [EffectKind.TollOfTheFlock] = 15,
        };

        internal static readonly IReadOnlyDictionary<EffectKind, int> ShippedThreshold = new Dictionary<EffectKind, int>
        {
            [EffectKind.WorldEndersCrown] = 30,
        };

        // The relic's one mechanic effect at its shipped numbers.
        internal static Effect Mechanic(EffectKind kind)
        {
            ShippedMagnitude.TryGetValue(kind, out int magnitude);
            ShippedThreshold.TryGetValue(kind, out int threshold);
            return new Effect(kind, magnitude, threshold);
        }

        internal static ResolvedRelic Of(EffectKind kind, string bearer = "", bool draftable = true) =>
            new ResolvedRelic(kind.ToString(), kind.ToString(), "", new[] { Mechanic(kind) }, 0,
                bearer: bearer, draftable: draftable);

        // A stat-only relic, as Vampire Dentures or Thick Hide is authored.
        internal static ResolvedRelic Stats(string id, params Effect[] effects) =>
            new ResolvedRelic(id, id, "", effects, 0);

        // What the adapter hands a party member: the relics that reach them.
        internal static EffectSet EffectsFor(string characterId, IReadOnlyList<ResolvedRelic> relics) =>
            EffectSet.Empty.WithRelics(relics, characterId);

        // Stands in for FightEncounterAdapter on a hand-built fight: each
        // party member comes to hold the relics its kit carries, on top of
        // whatever the fixture already gave it. Call before Begin(), which
        // fires the open-of-combat relics. Once per member: a second call adds
        // nothing, so a relic stat is never counted twice.
        internal static void Hold(FightSession session)
        {
            foreach (var member in session.Encounter.PlayerParty)
            {
                var kit = session.KitFor(member);
                if (kit == null) continue;

                bool holdsRelics = false;
                foreach (var entry in member.Effects.Entries)
                {
                    if (entry.Holder == EffectHolder.Relic) holdsRelics = true;
                }

                if (!holdsRelics) member.Effects = member.Effects.WithRelics(kit.Relics, kit.Id);
            }
        }
    }
}
