// Base class for everything the witch can hold
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat
{
    public abstract class Weapon : MonoBehaviour
    {
        [SerializeField] private string displayName = "Weapon";

        [Tooltip("Objects shown only while this weapon is in hand.")]
        [SerializeField] private GameObject[] visuals = new GameObject[0];

        private readonly List<IWeaponModifier> modifiers = new List<IWeaponModifier>();
        private WeaponStats cachedStats = WeaponStats.Default;
        private bool statsDirty = true;

        public string DisplayName => displayName;

        public abstract WeaponSlot Slot { get; }

        public WeaponInventory Owner { get; private set; }

        public bool IsEquipped { get; private set; }

        // Never blocks switching
        public virtual bool IsBusy => false;

        // Read when attacking not at Awake
        public WeaponStats Stats
        {
            get
            {
                if (statsDirty)
                {
                    RecalculateStats();
                }

                return cachedStats;
            }
        }

        public IReadOnlyList<IWeaponModifier> Modifiers => modifiers;

        public event Action<Weapon> Attacked;
        public event Action<Weapon, IDamageable, DamageInfo, DamageResult> HitLanded;
        public event Action<Weapon, IDamageable, DamageInfo> Defeated;
        public event Action<Weapon> StatsChanged;

        protected ICombatMotor Motor => Owner != null ? Owner.Motor : null;

        protected ICombatAim Aim => Owner != null ? Owner.Aim : null;

        // The player carrying this weapon so hits skip it
        protected Transform OwnerRoot => Owner != null ? Owner.transform : transform.root;

        protected virtual void Awake()
        {
            SetVisualsActive(false);
        }

        internal void Bind(WeaponInventory owner)
        {
            Owner = owner;
        }

        internal void Equip()
        {
            if (IsEquipped)
            {
                return;
            }

            IsEquipped = true;
            SetVisualsActive(true);
            OnEquipped();
        }

        internal void Unequip()
        {
            if (!IsEquipped)
            {
                return;
            }

            IsEquipped = false;
            OnUnequipped();
            SetVisualsActive(false);
        }

        public abstract void TickInput(in CombatInputFrame input);

        public void AddModifier(IWeaponModifier modifier)
        {
            if (modifier == null || modifiers.Contains(modifier))
            {
                return;
            }

            modifiers.Add(modifier);
            MarkStatsDirty();
        }

        public bool RemoveModifier(IWeaponModifier modifier)
        {
            if (!modifiers.Remove(modifier))
            {
                return false;
            }

            MarkStatsDirty();
            return true;
        }

        public void ClearModifiers()
        {
            if (modifiers.Count == 0)
            {
                return;
            }

            modifiers.Clear();
            MarkStatsDirty();
        }

        // Call when an attached modifier changes its numbers
        public void MarkStatsDirty()
        {
            statsDirty = true;
            StatsChanged?.Invoke(this);
        }

        protected virtual void OnEquipped()
        {
        }

        protected virtual void OnUnequipped()
        {
        }

        protected void RaiseAttacked()
        {
            Attacked?.Invoke(this);
        }

        // Use this instead of hitting the target directly so listeners hear about it
        protected DamageResult DealDamage(IDamageable target, DamageInfo info)
        {
            if (target == null || !target.IsAlive)
            {
                return DamageResult.Ignored;
            }

            info.Source = Owner != null ? Owner.gameObject : transform.root.gameObject;
            info.Weapon = this;

            DamageResult result = target.ApplyDamage(info);
            if (!result.Applied)
            {
                return result;
            }

            HitLanded?.Invoke(this, target, info, result);
            if (result.Defeated)
            {
                Defeated?.Invoke(this, target, info);
            }

            return result;
        }

        // Defeats without damage like the vacuum swallowing a spirit
        protected void RaiseDefeated(IDamageable target, DamageInfo info)
        {
            Defeated?.Invoke(this, target, info);
        }

        // Falls back to the owner facing
        protected Vector3 FlatAimDirection(Vector3 from)
        {
            ICombatAim aim = Aim;
            if (aim != null && aim.HasAim)
            {
                Vector3 toAim = aim.AimPoint - from;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.01f)
                {
                    return toAim.normalized;
                }
            }

            Vector3 forward = OwnerRoot.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        }

        protected static bool IsPartOf(Transform candidate, Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }

        private void RecalculateStats()
        {
            WeaponStats stats = WeaponStats.Default;
            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i]?.ModifyStats(this, ref stats);
            }

            stats.AttackSpeedMultiplier = Mathf.Max(0.05f, stats.AttackSpeedMultiplier);
            stats.ReloadSpeedMultiplier = Mathf.Max(0.05f, stats.ReloadSpeedMultiplier);
            stats.DamageMultiplier = Mathf.Max(0f, stats.DamageMultiplier);
            stats.KnockbackMultiplier = Mathf.Max(0f, stats.KnockbackMultiplier);
            stats.RangeMultiplier = Mathf.Max(0.05f, stats.RangeMultiplier);
            stats.ExtraShots = Mathf.Max(0, stats.ExtraShots);

            cachedStats = stats;
            statsDirty = false;
        }

        private void SetVisualsActive(bool active)
        {
            if (visuals == null)
            {
                return;
            }

            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                {
                    visuals[i].SetActive(active);
                }
            }
        }
    }
}
