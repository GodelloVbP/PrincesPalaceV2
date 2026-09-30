using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE FIGHT'S ANSWER TO THE SYSTEM MENU (plan C4, IFightMenuSource).
    //
    // WHAT THE HUD PAINTED, RECORDED WHERE IT PAINTS. The party plates take
    // their numbers from two places -- RefreshPcPlate (live state, once a
    // round has finished playing) and PaintVitals (a beat's recorded moment,
    // mid-playback) -- and each of those writes the record below beside its
    // labels. The menu reads the record, so it can only ever say what a plate
    // says. Reading the session instead would be a round ahead of the screen
    // for the whole length of a playback and would spoil a hit or a death the
    // player has not yet watched.
    //
    // The statuses and speed changes are recorded at the same moments as the
    // plate they belong to, for the same reason: the status badges are painted
    // from the session on a repaint, and the menu must not name a buff the
    // badge row has not shown yet.
    public partial class FightController : IFightMenuSource
    {
        private sealed class PaintedMember
        {
            public Vitals Vitals;
            public IReadOnlyList<FightStatModifier> Modifiers = FightMemberFacts.NoModifiers;
        }

        private readonly Dictionary<CombatantState, PaintedMember> _painted =
            new Dictionary<CombatantState, PaintedMember>();

        // Called from Bind. A new fight starts with nothing painted, and the
        // scene's menu is told where to ask.
        private void RegisterMenuSource()
        {
            _painted.Clear();
            if (systemMenu != null) systemMenu.FightSource = this;
        }

        // RefreshPcPlate's write: the plate has just been drawn from live
        // state, statuses included.
        private void RecordPaintedPlate(CombatantState member)
        {
            var signature = member.SignaturePool;
            _painted[member] = new PaintedMember
            {
                Vitals = new Vitals(member.CurrentHealth, member.CurrentMana, signature != null ? signature.Current : 0,
                    member.MaxHealth, member.MaxMana),
                Modifiers = FightModifiers.For(_session, member),
            };
        }

        // PaintVitals' write: a beat's recorded health and primary pool went
        // on the card. The signature is not on a beat's card, so it stays what
        // the last plate repaint showed.
        private void RecordPaintedVitals(CombatantState member, Vitals recorded, int maxHealth, int maxPrimary)
        {
            if (!_painted.TryGetValue(member, out var painted))
            {
                painted = new PaintedMember();
                _painted[member] = painted;
            }

            painted.Vitals = new Vitals(recorded.Health, recorded.Primary, painted.Vitals.Signature, maxHealth, maxPrimary);
        }

        public IReadOnlyList<FightMemberFacts> Members
        {
            get
            {
                var members = new List<FightMemberFacts>();
                var occupants = _plateOccupants;
                for (int plate = 0; occupants != null && plate < occupants.Length; plate++)
                {
                    var facts = FactsFor(occupants[plate]);
                    if (facts != null) members.Add(facts);
                }

                return members;
            }
        }

        public bool TryGetMember(string characterId, out FightMemberFacts facts)
        {
            facts = null;
            if (string.IsNullOrEmpty(characterId)) return false;

            foreach (var member in Members)
            {
                if (member.CharacterId != characterId) continue;

                facts = member;
                return true;
            }

            return false;
        }

        // Null for a plate with nobody on it, or someone the HUD has not
        // painted yet (there is nothing displayed to report).
        private FightMemberFacts FactsFor(CombatantState member)
        {
            if (member == null || _session == null || !_painted.TryGetValue(member, out var painted)) return null;

            var kit = _session.KitFor(member);
            if (kit == null) return null;

            var signature = member.SignaturePool;
            return new FightMemberFacts(
                kit.Id,
                painted.Vitals.Health, painted.Vitals.MaxHealth,
                member.PrimaryPool?.DisplayName, painted.Vitals.Primary, painted.Vitals.MaxPrimary,
                signature != null ? signature.DisplayName ?? "" : null, painted.Vitals.Signature, signature != null ? signature.Max : 0,
                painted.Modifiers);
        }
    }
}
