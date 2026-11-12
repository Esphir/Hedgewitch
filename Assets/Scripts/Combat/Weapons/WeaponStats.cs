// Weapon slots and stats plus the modifier hook for the foraging grid
namespace Hedgewitch.Combat
{
    // Matches the rows of the planned foraging grid
    public enum WeaponSlot
    {
        Melee,
        Ranged,
        Vacuum
    }

    // Read on every attack so modifier changes apply on the next swing or shot
    public struct WeaponStats
    {
        public float DamageMultiplier;

        // Above 1 speeds up swings ignition and reloads
        public float AttackSpeedMultiplier;

        public float KnockbackMultiplier;

        public float RangeMultiplier;

        // Above 1 reloads faster
        public float ReloadSpeedMultiplier;

        // Extra projectiles fanned around the aim line
        public int ExtraShots;

        public static WeaponStats Default => new WeaponStats
        {
            DamageMultiplier = 1f,
            AttackSpeedMultiplier = 1f,
            KnockbackMultiplier = 1f,
            RangeMultiplier = 1f,
            ReloadSpeedMultiplier = 1f,
            ExtraShots = 0
        };
    }

    // Applied in the order added
    public interface IWeaponModifier
    {
        void ModifyStats(Weapon weapon, ref WeaponStats stats);
    }
}
