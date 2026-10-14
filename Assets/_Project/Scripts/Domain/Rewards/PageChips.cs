using System.Collections.Generic;

namespace PrincesPalace.Domain.Rewards
{
    // The chips a settled fight hands the item pipeline (docs/PLAN_ITEMS.md
    // 1.1, 1.2): each Gear chip adds a choice to the one gear page, each Relic
    // chip to the one relic page. Filled by RealmPrizes.Pay while the fight's
    // own map is still the live one (a boss win advances the leg right after),
    // then read by RunOrchestrator.SettleRewardPages.
    public sealed class PageChips
    {
        public readonly List<EncounterClass> Gear = new List<EncounterClass>();
        public readonly List<EncounterClass> Relic = new List<EncounterClass>();

        // The strength of the room that was fought, for the gear page's
        // strength floors.
        public int Strength;

        // A page shows k + 2 cards and the offer row fits MaxCards, so a
        // page takes at most this many chips; a chip past it pays gold.
        public static readonly int MaxChips = UiKit.OfferRowLayout.MaxCards - 2;

        public bool GearFull => Gear.Count >= MaxChips;
        public bool RelicFull => Relic.Count >= MaxChips;

        public bool IsEmpty => Gear.Count == 0 && Relic.Count == 0;

        // A relic page exists for anchors and bosses only (rewards.json has no
        // normal_relic row), so a Relic chip on a plain room pays as an anchor's.
        public void AddRelic(EncounterClass cls) =>
            Relic.Add(cls < EncounterClass.Elite ? EncounterClass.Elite : cls);

        // A boss pays a gear page and a relic page of its own (1.1).
        public void AddBoss()
        {
            Gear.Add(EncounterClass.Boss);
            Relic.Add(EncounterClass.Boss);
        }
    }
}
