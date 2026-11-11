using UnityEngine;

namespace Hedgewitch.Combat
{
    /// <summary>Anything a weapon can hurt.</summary>
    public interface IDamageable
    {
        Transform Transform { get; }

        bool IsAlive { get; }

        DamageResult ApplyDamage(in DamageInfo info);
    }
}
