// Damage data passed from weapons to whatever they hit
using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    // Hit tags that charms and modifiers filter on
    [Flags]
    public enum DamageTags
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1,
        Vacuum = 1 << 2,
        Heavy = 1 << 3,
        Finisher = 1 << 4
    }

    public struct DamageInfo
    {
        public float Amount;

        public Vector3 Point;

        // Knockback uses the horizontal part
        public Vector3 Direction;

        // Horizontal knockback speed in m/s
        public float Knockback;

        // Upward speed in m/s and above zero knocks up
        public float Launch;

        // Seconds the target loses control
        public float Stagger;

        public DamageTags Tags;

        // Usually the player root
        public GameObject Source;

        public UnityEngine.Object Weapon;

        public bool HasTag(DamageTags tag) => (Tags & tag) != 0;
    }

    public struct DamageResult
    {
        public bool Applied;
        public float Dealt;
        public bool Defeated;

        public static DamageResult Ignored => default;
    }
}
