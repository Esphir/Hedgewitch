using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat
{
    /// <summary>
    /// Hit points for anything that can be fought. Reaching zero is "defeated" rather than
    /// "killed": in this game a spirit is tidied up and sent on, not destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        public enum DefeatOutcome
        {
            Deactivate,
            Destroy,
            None
        }

        private static readonly List<Health> ActiveList = new List<Health>();

        [Min(1f)] [SerializeField] private float maxHealth = 60f;

        [Tooltip("Seconds after a hit during which further hits are ignored. 0 lets every hit land.")]
        [Min(0f)] [SerializeField] private float invulnerabilityAfterHit;

        [Tooltip("What happens to this object when it is defeated.")]
        [SerializeField] private DefeatOutcome defeatOutcome = DefeatOutcome.Deactivate;

        [Tooltip("Seconds between being defeated and the outcome, so the final hit's knock-up reads.")]
        [Min(0f)] [SerializeField] private float defeatDelay = 0.35f;

        private float current;
        private float invulnerableUntil;
        private bool defeated;
        private float defeatTimer = -1f;

        /// <summary>Every enabled Health, for HUDs and AI. Cheaper than searching the scene.</summary>
        public static IReadOnlyList<Health> Active => ActiveList;

        public Transform Transform => this != null ? transform : null;

        public float Max => maxHealth;

        public float Current => current;

        public float Normalized => maxHealth > 0f ? Mathf.Clamp01(current / maxHealth) : 0f;

        public bool IsAlive => !defeated;

        public event Action<DamageInfo, DamageResult> Damaged;

        public event Action<DamageInfo> Defeated;

        private void Awake()
        {
            current = maxHealth;
        }

        private void OnEnable()
        {
            ActiveList.Add(this);
        }

        private void OnDisable()
        {
            ActiveList.Remove(this);
        }

        private void Update()
        {
            if (defeatTimer < 0f)
            {
                return;
            }

            defeatTimer -= Time.deltaTime;
            if (defeatTimer <= 0f)
            {
                defeatTimer = -1f;
                ApplyOutcome();
            }
        }

        public DamageResult ApplyDamage(in DamageInfo info)
        {
            if (defeated || !isActiveAndEnabled || info.Amount <= 0f)
            {
                return DamageResult.Ignored;
            }

            if (Time.time < invulnerableUntil)
            {
                return DamageResult.Ignored;
            }

            float dealt = Mathf.Min(current, info.Amount);
            current -= dealt;
            invulnerableUntil = Time.time + invulnerabilityAfterHit;

            DamageResult result = new DamageResult
            {
                Applied = true,
                Dealt = dealt,
                Defeated = current <= 0f
            };

            Damaged?.Invoke(info, result);

            if (result.Defeated)
            {
                defeated = true;
                Defeated?.Invoke(info);

                if (defeatDelay > 0f)
                {
                    defeatTimer = defeatDelay;
                }
                else
                {
                    ApplyOutcome();
                }
            }

            return result;
        }

        public void Heal(float amount)
        {
            if (defeated || amount <= 0f)
            {
                return;
            }

            current = Mathf.Min(maxHealth, current + amount);
        }

        /// <summary>Back to full and fighting fit, e.g. when an enemy is reused from a pool.</summary>
        public void ResetHealth()
        {
            current = maxHealth;
            defeated = false;
            defeatTimer = -1f;
            invulnerableUntil = 0f;
        }

        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            current = refill ? maxHealth : Mathf.Min(current, maxHealth);
        }

        private void ApplyOutcome()
        {
            switch (defeatOutcome)
            {
                case DefeatOutcome.Deactivate:
                    gameObject.SetActive(false);
                    break;
                case DefeatOutcome.Destroy:
                    Destroy(gameObject);
                    break;
            }
        }
    }
}
