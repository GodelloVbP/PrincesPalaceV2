namespace PrincesPalace.Domain.Combat.Session
{
    // WHAT THE STAGE SHOULD SHOW FOR A PLANTED SHIELD, as a plain look.
    //
    // A read model over CombatantState.PlantedShield (Domain/Combat/
    // PlantedShield.cs), which is the only truth: nothing here mutates it and
    // nothing draws from it but the fight view. Wall is the Shieldwall
    // placement (IsShieldwall) standing over the whole party; Broken is the
    // moment after a hit emptied the ward.
    public enum ShieldLook { None, Intact, Cracked, Broken, Wall }

    // ONE WATCH PER HOLDER, fed the shield each time the view repaints.
    //
    // WHY A WATCH AND NOT A PURE FUNCTION OF THE SHIELD. A break ends the
    // placement in the same instant (PlantedShield.End clears Ward), so by the
    // time the view looks there is no ward left to read a "broken" state off.
    // The watch keeps the ward it last saw; when that ward is gone and had been
    // driven to zero, the shield broke, and when it is gone with points left it
    // expired or was spent by Shield Bash, which are not breaks and draw
    // nothing. A break is reported once, on the first observation after it.
    public sealed class PlantedShieldWatch
    {
        // The ward is "cracked" once it has lost half its placed size. At or
        // below, so a shield with exactly half left is already cracked.
        public const int CrackedAtPercent = 50;

        private ActiveStatus _seenWard;
        private bool _seenWall;

        public ShieldLook Observe(PlantedShield shield)
        {
            if (shield == null) return ShieldLook.None;

            if (shield.IsPlaced)
            {
                _seenWard = shield.Ward;
                _seenWall = shield.IsShieldwall;

                if (shield.IsShieldwall) return ShieldLook.Wall;
                return IsCracked(shield.Ward.Magnitude, shield.PlacedPoints)
                    ? ShieldLook.Cracked
                    : ShieldLook.Intact;
            }

            if (_seenWard == null) return ShieldLook.None;

            bool broke = _seenWard.Magnitude <= 0;
            _seenWard = null;
            _seenWall = false;
            return broke ? ShieldLook.Broken : ShieldLook.None;
        }

        // True while the last observation was a Shieldwall placement.
        public bool SawWall => _seenWall;

        public static bool IsCracked(int magnitude, int placedPoints) =>
            placedPoints > 0 && (long)magnitude * 100 <= (long)placedPoints * CrackedAtPercent;
    }

    // Where each look's art lives, Resources-relative and extension-free (the
    // StatusBadgeIcons convention). Sliced from Art/Sheets/bjorn_planted_shield.png
    // (intact/cracked/broken on one shared canvas, so the tip stays on one
    // ground line across the three) and Art/Sheets/bjorn_shield_wall.png:
    //   python tools/slice_actor_sheet.py --sheet <sheet> --actor Characters/<x> \
    //       --grid 3x1 --stances intact,cracked,broken   (wall: --grid 1x1 --stances wall)
    //       --key white_flood --out-root <scratch>
    // then copied under Resources/Props/planted_shield/. The slicer's actor
    // kinds are only Enemies/Characters, so the output is moved by hand.
    public static class PlantedShieldArt
    {
        public const string Folder = "Props/planted_shield/";

        // The slicer's measured ground line for both sheets: the shield's tip
        // (and the wall's feet) sit this many canvas pixels above the bottom
        // edge, the same "ground line" the stance manifest records for actors.
        public const float GroundLinePixels = 8f;

        // The shield's height as a fraction of the figure it stands beside,
        // measured ground to the top of the idle drawing: chest high.
        public const float ChestFraction = 0.6f;

        public static string ResourceFor(ShieldLook look)
        {
            switch (look)
            {
                case ShieldLook.Intact: return Folder + "intact";
                case ShieldLook.Cracked: return Folder + "cracked";
                case ShieldLook.Broken: return Folder + "broken";
                case ShieldLook.Wall: return Folder + "wall";
                default: return null;
            }
        }
    }
}
