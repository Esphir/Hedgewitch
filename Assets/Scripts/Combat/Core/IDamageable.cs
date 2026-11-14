// Anything a weapon can hurt
using UnityEngine;

namespace Hedgewitch.Combat
{
    public interface IDamageable
    {
        Transform Transform { get; }

        bool IsAlive { get; }

        DamageResult ApplyDamage(in DamageInfo info);
    }
}
