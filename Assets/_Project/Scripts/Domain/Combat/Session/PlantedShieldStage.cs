using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat.Session
{
    // WHAT THE STAGE SHOULD SHOW FOR A PLANTED SHIELD, as a plain look.
    //
    // A read model over CombatantState.PlantedShield (Domain/Combat/
    // PlantedShield.cs), which is the only truth: nothing here mutates it and
    // nothing draws from it but the fight view. Broken is the moment after a
    // hit emptied the ward. A Shieldwall has no look of its own: it is the same
    // shield at every standing ally's seat (PlantedShieldStage.Resolve).
    public enum ShieldLook { None, Intact, Cracked, Broken }

    // WHAT ONE HOLDER'S SHIELD WAS when a beat resolved, so the view can paint
    // the blow that changed it instead of the state the round ended on. Carried
    // on CombatBeat.Shields beside the vitals snapshot, for the same reason:
    // resolution runs the whole round before the first beat plays.
    //
    // READONLY, and only ever a copy of numbers: it never references the live
    // ward, which a later beat in the same chain has already emptied.
    public readonly struct ShieldSnapshot
    {
        // Something is planted. False on the beat that broke it.
        public readonly bool Placed;

        // The ward's points left and the size it went up at. For a Shieldwall
        // these are the one shared pool's.
        public readonly int Points;
        public readonly int PlacedPoints;

        // The placement (or, with Broke, the one just broken) is a Shieldwall.
        public readonly bool IsWall;

        // This beat took the shield down by emptying it. An expiry or a Shield
        // Bash ends the placement without Broke: they draw nothing.
        public readonly bool Broke;

        public ShieldSnapshot(bool placed, int points, int placedPoints, bool isWall, bool broke)
        {
            Placed = placed;
            Points = points;
            PlacedPoints = placedPoints;
            IsWall = isWall;
            Broke = broke;
        }

        // The live shield as it stands, never Broke: a break is an event, and
        // only PlantedShieldWatch can tell one from an expiry.
        public static ShieldSnapshot Of(PlantedShield shield) =>
            shield != null && shield.IsPlaced
                ? new ShieldSnapshot(true, shield.Ward.Magnitude, shield.PlacedPoints, shield.IsShieldwall, false)
                : default;

        public ShieldLook Look
        {
            get
            {
                if (Broke) return ShieldLook.Broken;
                if (!Placed) return ShieldLook.None;
                return PlantedShieldWatch.IsCracked(Points, PlacedPoints) ? ShieldLook.Cracked : ShieldLook.Intact;
            }
        }
    }

    // WHICH LOOK EACH SEAT DRAWS for one set of snapshots.
    public static class PlantedShieldStage
    {
        // A lone shield stands at its holder's seat. A Shieldwall stands at
        // every standing ally's seat, all reading the one shared pool, so the
        // wall cracks and breaks everywhere at once. Seats with nothing to draw
        // are absent from the result.
        public static Dictionary<CombatantState, ShieldLook> Resolve(
            IReadOnlyList<CombatantState> party,
            IReadOnlyDictionary<CombatantState, ShieldSnapshot> shields,
            Func<CombatantState, bool> isStanding)
        {
            var looks = new Dictionary<CombatantState, ShieldLook>();
            if (party == null || shields == null) return looks;

            foreach (var holder in party)
            {
                if (holder == null || !shields.TryGetValue(holder, out var snap)) continue;

                var look = snap.Look;
                if (look == ShieldLook.None) continue;

                if (!snap.IsWall)
                {
                    looks[holder] = look;
                    continue;
                }

                foreach (var ally in party)
                {
                    if (ally != null && isStanding(ally)) looks[ally] = look;
                }
            }

            return looks;
        }
    }

    // ONE WATCH PER HOLDER, fed the shield at each beat's commit.
    //
    // WHY A WATCH AND NOT A PURE FUNCTION OF THE SHIELD. A break ends the
    // placement in the same instant (PlantedShield.End clears Ward), so by the
    // time anything looks there is no ward left to read a "broken" state off.
    // The watch keeps the ward it last saw; when that ward is gone and had been
    // driven to zero, the shield broke, and when it is gone with points left it
    // expired or was spent by Shield Bash, which are not breaks and draw
    // nothing. A break is reported once, on the first snapshot after it.
    public sealed class PlantedShieldWatch
    {
        // The ward is "cracked" once it has lost half its placed size. At or
        // below, so a shield with exactly half left is already cracked.
        public const int CrackedAtPercent = 50;

        private ActiveStatus _seenWard;
        private bool _seenWall;

        public ShieldSnapshot Snapshot(PlantedShield shield)
        {
            if (shield == null) return default;

            if (shield.IsPlaced)
            {
                _seenWard = shield.Ward;
                _seenWall = shield.IsShieldwall;
                return ShieldSnapshot.Of(shield);
            }

            if (_seenWard == null) return default;

            bool broke = _seenWard.Magnitude <= 0;
            bool wall = _seenWall;
            _seenWard = null;
            _seenWall = false;
            return broke ? new ShieldSnapshot(false, 0, shield.PlacedPoints, wall, true) : default;
        }

        public static bool IsCracked(int magnitude, int placedPoints) =>
            placedPoints > 0 && (long)magnitude * 100 <= (long)placedPoints * CrackedAtPercent;
    }

    // Where each look's art lives, Resources-relative and extension-free (the
    // StatusBadgeIcons convention). Sliced from Art/Sheets/bjorn_planted_shield-v2.png
    // (intact/cracked/broken on one shared canvas, so the dirt mound stays on one
    // ground line across the three):
    //   python tools/slice_actor_sheet.py --sheet <sheet> --actor Characters/<x> \
    //       --grid 3x1 --stances intact,cracked,broken --key white_flood --out-root <scratch>
    // then copied under Resources/Props/planted_shield/. The slicer's actor
    // kinds are only Enemies/Characters, so the output is moved by hand.
    // Resources/Props/planted_shield/wall.png (Art/Sheets/bjorn_shield_wall.png)
    // stays on disk but nothing loads it: a Shieldwall draws the single shield
    // at every seat until a party-wide prop exists.
    public static class PlantedShieldArt
    {
        public const string Folder = "Props/planted_shield/";

        // The slicer's measured ground line: the dirt mound the shield stands
        // in sits this many canvas pixels above the bottom edge, the same
        // "ground line" the stance manifest records for actors.
        public const float GroundLinePixels = 8f;

        // The shield's height as a fraction of the figure it stands beside,
        // measured ground to the top of the idle drawing: chest high. The
        // fraction applies to the whole canvas, mound included; the mound takes
        // about a tenth of it, so 0.63 keeps the shield body at the height
        // 0.6 gave the mound-less art.
        public const float ChestFraction = 0.63f;

        public static string ResourceFor(ShieldLook look)
        {
            switch (look)
            {
                case ShieldLook.Intact: return Folder + "intact";
                case ShieldLook.Cracked: return Folder + "cracked";
                case ShieldLook.Broken: return Folder + "broken";
                default: return null;
            }
        }
    }
}
