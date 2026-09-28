using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Events;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // Rolls the three items a won fight offers.
    //
    // Every RULE here already existed and had no caller: RarityTable decides
    // what a depth and an encounter class are worth, ItemOfferTable picks which
    // items given a target tier, and both are pure, injected-randomness and
    // fully tested. What was missing was the one Core-side step neither can
    // take -- turning ContentDatabase's ItemDefinitions into candidates -- so
    // the whole reward table sat unreachable behind it.
    //
    // Kept out of the controller because "what does a fight drop" is a rule
    // about the game, not about a screen, and the Reckoning is not the only
    // thing that will ever want to ask.
    public static class ItemOfferRoll
    {
        // The ceiling the tier roll clamps against, taken from content rather
        // than declared. A content pass that adds a tier 11 set should widen
        // the roll without anyone remembering to update a constant here.
        public static int MaxTier =>
            ContentDatabase.Items.Count == 0 ? 0 : ContentDatabase.Items.Max(i => i.tier);

        // WHAT THE GAME OFFERS AS A REWARD, and that is not the same set as
        // "everything wearable".
        //
        // A "choose one of three" that can offer a health potion is not a
        // choice, it is a tax on the one player who reads carefully. Potions
        // come from the shop and from drops; the fight-reward slot is where
        // gear comes from. That much was always here -- what was missing is
        // the other half of the same argument, which ContentDatabase.Offerable
        // has stated for months: the hand-authored one-offs in items.json
        // "have their own routes in and would otherwise turn up as a 'reward'
        // the player already owns six of". The filter here read IsEquippable,
        // so all six starting-kit items sat in the floor-1 band and the first
        // fight of a run could hand you the helm on your own head.
        //
        // Offerable's predicate is "was this generated with a tier", not the
        // kind, because the offer screen ranks what it shows by run depth and
        // an item with no tier cannot be placed on that axis at all. ASKED
        // RATHER THAN RESTATED: this is the only reward-side caller, the
        // shop's gear shelf and the Reckoning both come through here, and a
        // second copy of the predicate is how the two drifted apart in the
        // first place.
        public static IReadOnlyList<ItemOffer> Candidates()
        {
            return ContentDatabase.Offerable
                .Where(i => i != null && !string.IsNullOrEmpty(i.id))
                .Select(i => new ItemOffer(i.id, i.tier))
                .ToList();
        }

        // Every modifier id in the pool that reads as OFFENSIVE (weaponPool
        // true, staves included -- see ModifierTable.IsOffensiveModifier's
        // own header) or DEFENSIVE (weaponPool false), authored order. The
        // Core-side bridge ModifierTable.PickModifiers needs -- Domain
        // cannot see ContentDatabase or ModifierDefinition, the same reason
        // Candidates() exists above for items rather than ItemOfferTable
        // reading ContentDatabase itself.
        //
        // TWO POOLS, NOT ONE FILTERED PER-DRAW: a staff rolling out of the
        // same flat list as a breastplate is the bug this split closes (see
        // ModifierTable's own header) -- picking the RIGHT list once per
        // offer, before PickModifiers ever draws, is what keeps a weapon's
        // three rolled slots from ever seeing a defensive id at all, rather
        // than merely making one less likely.
        //
        // A modifier with no resolved effect (a raw entry the resolver
        // rejected, or content mid-edit) reads as defensive by
        // IsOffensiveModifier's own tolerant default, so it never appears in
        // the weapon pool -- the safer direction for a still-forming id to
        // fail into.
        private static IReadOnlyList<string> ModifierPool(bool weaponPool)
        {
            return ContentDatabase.Modifiers
                .Where(m => m != null && !string.IsNullOrEmpty(m.id))
                .Where(m =>
                {
                    var effects = m.Data.Effects;
                    var type = effects != null && effects.Length > 0
                        ? effects[0].Type
                        : ModifierEffectType.None;
                    return ModifierTable.IsOffensiveModifier(type) == weaponPool;
                })
                .Select(m => m.id)
                .ToList();
        }

        // What one character's Favor is worth: what they were AUTHORED with,
        // plus whatever their CURRENTLY EQUIPPED gear grants LIVE right now
        // (Fortunate).
        //
        // The authored part is content, rebuilt from characters.json by
        // ContentBuilder and the same for every save. The live part is never
        // stored anywhere -- it is read fresh off
        // ContentDatabase.ModifierEffects(character) every time this is
        // called, which already reflects only the character's currently
        // equipped, currently LIVE loadout (ActiveLoadout). Unequip
        // Fortunate and the very next call to this method already sees it
        // gone; nothing has to be reset, decremented or expired.
        //
        // TWO SOURCES ONLY. Nothing on the reward track adds a third, earned
        // part -- Favor is authored-plus-live, full stop.
        //
        // Tolerant of every side being missing, the house style: a character
        // whose definition has gone (content edited under a live save) still
        // contributes what their gear grants, and a character with nothing
        // equipped still contributes what they were authored with.
        public static int FavorOf(Character character, CharacterDefinition definition)
        {
            int authored = definition == null ? 0 : definition.Data.PrincesFavor;
            int liveBonus = character == null
                ? 0
                : ContentDatabase.ModifierEffects(character).Best(ModifierEffectType.FortunateFavorBonusFlat);

            int total = authored + liveBonus;
            return total < 0 ? 0 : total;
        }

        // The squad's Prince's Favor: the HIGHEST among the fielded party,
        // never the sum.
        //
        // Highest rather than total because Favor is meant to be a reason to
        // FIELD a particular character, not a reason to field five. Summing
        // would make the stat scale with squad size, so the real decision
        // would become "bring more bodies" -- which is not a decision about
        // Favor at all.
        //
        // Takes the per-member totals, so that this rule and FavorOf's rule
        // above are two separate facts in two separate functions: "where
        // does a member's Favor come from" and "how does a squad combine
        // it" can change independently.
        public static int SquadFavor(IEnumerable<int> memberFavors)
        {
            if (memberFavors == null) return 0;

            int best = 0;
            foreach (int favor in memberFavors)
            {
                if (favor > best) best = favor;
            }

            return best;
        }

        // The fielded squad's Favor, read off the save.
        //
        // Here rather than at the call site so the rule -- which squad, and
        // highest-not-sum -- lives with the roll it feeds, and so the fight
        // controller does not have to learn how a definitionId maps to a
        // definition.
        //
        // PLUS THE RUN'S EVENT FAVOR, added AFTER the squad max and never
        // inside it: an event's "+10 Prince's favor" is a gift to the run,
        // not to a member, so it must not change which member is the best
        // and must not vanish when that member is benched
        // (docs/PLAN_PETTING_ZOO.md, Fawns). SquadFavor itself stays the
        // pure max-not-sum rule.
        public static int CurrentSquadFavor()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return 0;

            int squad = SquadFavor(save.ActiveSquad()
                .Select(c => FavorOf(c, ContentDatabase.Characters
                    .FirstOrDefault(d => d != null && d.id == c.definitionId))));

            var run = save.activeRun;
            int fromEvents = run != null && run.hasRun
                ? EventBuffs.ActiveAmount(run.eventBuffs, EventBuffs.PrincesFavor, run.legStartStep)
                : 0;

            return squad + fromEvents;
        }

        // WHAT "ONE ROLLED COPY" MEANS, WITH TWO CALLERS.
        //
        // The reward path (Roll, below) and the shop's gear shelf
        // (ShopStock.RollGear) must not drift on this: a shop card and a
        // fight reward are the same object rolled the same way, and a second
        // copy of the three axes would be a second rulebook (docs/PLAN_SHOP.md
        // §2d). Extracted rather than reimplemented, so the extraction is a
        // pure move -- the draw ORDER inside it (plus, then rift tier, then
        // which modifiers) is what a seeded stream reproduces, and reordering
        // it would renumber every reward every existing seed has ever rolled.
        //
        // The pools come in as a Context rather than being looked up per
        // item: they are read once per offer set, ContentDatabase does not
        // change mid-roll, and building them per item would be the same
        // answer computed N times.
        public readonly struct RollContext
        {
            public readonly IReadOnlyList<string> WeaponModifiers;
            public readonly IReadOnlyList<string> ArmorModifiers;
            public readonly IReadOnlyDictionary<string, ItemKind> KindById;

            public RollContext(IReadOnlyList<string> weaponModifiers, IReadOnlyList<string> armorModifiers,
                IReadOnlyDictionary<string, ItemKind> kindById)
            {
                WeaponModifiers = weaponModifiers;
                ArmorModifiers = armorModifiers;
                KindById = kindById;
            }
        }

        public static RollContext BuildRollContext()
        {
            return new RollContext(
                ModifierPool(weaponPool: true),
                ModifierPool(weaponPool: false),
                ContentDatabase.Items
                    .Where(i => i != null && !string.IsNullOrEmpty(i.id))
                    .ToDictionary(i => i.id, i => i.kind));
        }

        // One candidate, honed. WHICH POOL, not whether to roll at all -- a
        // staff (kind == ItemKind.Weapon) draws only offensive ids,
        // everything else equippable only defensive ones. An id this roll
        // cannot find in ContentDatabase.Items (should not happen -- every
        // candidate came from ContentDatabase.Items itself) defaults to the
        // defensive pool, the same "unknown reads as armour" posture
        // ModifierPool's own tolerant default takes.
        public static ItemOffer RollOne(ItemOffer offer, RollContext context, EncounterClass encounter,
            int favor, Func<int, int> nextIndex)
        {
            int plus = RarityTable.RollPlus(encounter, favor, nextIndex);
            var riftTier = ModifierTable.RollRiftTier(encounter, favor, nextIndex);

            bool isWeapon = context.KindById != null
                && context.KindById.TryGetValue(offer.ItemId, out var kind)
                && kind == ItemKind.Weapon;
            var modifierPool = isWeapon ? context.WeaponModifiers : context.ArmorModifiers;
            var modifiers = ModifierTable.PickModifiers(modifierPool, (int)riftTier, nextIndex);

            return offer.WithPlus(plus).WithModifiers(riftTier, modifiers);
        }

        // The offers, each with its own independently rolled plus.
        //
        // `nextIndex` is upper-bound-exclusive and injected, matching the shape
        // both Domain tables already take -- so a caller under test can hand in
        // a seeded stand-in and get the same items every time.
        //
        // `count` defaults to ItemOfferTable.OfferCount, which is what every
        // live caller wants; the parameter exists for a caller under test
        // that wants a specific count instead.
        public static List<ItemOffer> Roll(EncounterClass encounter, int depthStep, int favor, Func<int, int> nextIndex,
            int count = ItemOfferTable.OfferCount)
        {
            var offers = new List<ItemOffer>();
            if (nextIndex == null) return offers;

            var candidates = Candidates();
            if (candidates.Count == 0) return offers;

            int maxTier = MaxTier;
            int targetTier = RarityTable.RollTier(encounter, depthStep, maxTier, favor, nextIndex);

            // Built once per offer set rather than per item -- same reason
            // ModifierPool() itself is a Select().ToList() rather than a
            // live query: it is read once per offer below, not once per
            // effect, and ContentDatabase.Items/Modifiers do not change
            // mid-roll.
            var context = BuildRollContext();

            // Tier is rolled ONCE for the offer set and plus/RiftTier/which-
            // modifiers are rolled PER ITEM. Rolling tier per item would
            // quietly widen the spread ItemOfferTable.TierSpread already
            // controls, and make the three offers three separate difficulty
            // statements rather than one; RiftTier is a property of the one
            // copy on the card, exactly like plus, so it belongs at the same
            // granularity as plus rather than tier's.
            foreach (var offer in ItemOfferTable.Choose(candidates, targetTier, maxTier, nextIndex, count))
            {
                offers.Add(RollOne(offer, context, encounter, favor, nextIndex));
            }

            return offers;
        }
    }
}
