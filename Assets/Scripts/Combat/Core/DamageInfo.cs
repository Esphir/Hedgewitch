using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    /// <summary>
    /// What kind of hit this was. Charms and other modifiers key off these ("melee hits",
    /// "finishers"), so every hit should carry the tags that describe it.
    /// </summary>
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

    /// <summary>One hit, fully described: how much it hurts and how it moves the target.</summary>
    public struct DamageInfo
    {
        public float Amount;

        /// <summary>World position of the hit.</summary>
        public Vector3 Point;

        /// <summary>Direction the hit travels in. Knockback is applied along its horizontal part.</summary>
        public Vector3 Direction;

        /// <summary>Horizontal speed given to the target, in m/s.</summary>
        public float Knockback;

        /// <summary>Upward speed given to the target, in m/s. Above zero, the hit knocks it up.</summary>
        public float Launch;

        /// <summary>Seconds the target loses control of itself.</summary>
        public float Stagger;

        public DamageTags Tags;

        /// <summary>Whoever is ultimately responsible: usually the player's root object.</summary>
        public GameObject Source;

        /// <summary>The weapon that landed the hit, if any.</summary>
        public UnityEngine.Object Weapon;

        public bool HasTag(DamageTags tag) => (Tags & tag) != 0;
    }

    /// <summary>What a damage call actually did.</summary>
    public struct DamageResult
    {
        public bool Applied;
        public float Dealt;
        public bool Defeated;

        public static DamageResult Ignored => default;
    }
}
