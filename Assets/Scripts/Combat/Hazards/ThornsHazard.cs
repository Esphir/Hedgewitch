// Thorn patch that hurts and shoves whatever its hitbox catches on a short repeat
using System.Collections.Generic;
using UnityEngine;
using Hedgewitch.Audio;

namespace Hedgewitch.Combat
{
    [DisallowMultipleComponent]
    public class ThornsHazard : MonoBehaviour
    {
        [Tooltip("Damage each time the thorns bite.")]
        [Min(0f)] [SerializeField] private float damage = 5f;

        [Tooltip("Speed in m/s the victim is pushed away from the thorns.")]
        [Min(0f)] [SerializeField] private float knockback = 4.5f;

        [Tooltip("Seconds the victim loses control after a bite.")]
        [Min(0f)] [SerializeField] private float stagger = 0.15f;

        [Tooltip("Seconds before the thorns can bite the same target again while it stays inside.")]
        [Min(0.05f)] [SerializeField] private float repeatInterval = 0.75f;

        [Tooltip("Only objects with this tag are hurt.")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Also hurt enemies and anything else with health.")]
        [SerializeField] private bool hurtEnemies;

        [Tooltip("Played where the thorns bite.")]
        [SerializeField] private SoundEvent hitSound;

        private readonly Dictionary<IDamageable, float> nextBiteTime = new Dictionary<IDamageable, float>();
        private Collider[] hitboxes;

        public event System.Action<ThornsHazard, IDamageable> Bit;

        private void Awake()
        {
            hitboxes = GetComponentsInChildren<Collider>();
        }

        private void OnTriggerEnter(Collider other) => TryBite(other);

        private void OnTriggerStay(Collider other) => TryBite(other);

        private void OnCollisionEnter(Collision collision) => TryBite(collision.collider);

        private void OnCollisionStay(Collision collision) => TryBite(collision.collider);

#if UNITY_EDITOR
        public void EditorConfigure(float biteDamage, float push, float interval, bool alsoEnemies)
        {
            damage = biteDamage;
            knockback = push;
            repeatInterval = interval;
            hurtEnemies = alsoEnemies;
        }
#endif

        private void TryBite(Collider other)
        {
            IDamageable victim = other.GetComponentInParent<IDamageable>();
            if (victim == null || !victim.IsAlive || !Affects(victim.Transform))
            {
                return;
            }

            if (nextBiteTime.TryGetValue(victim, out float next) && Time.time < next)
            {
                return;
            }

            nextBiteTime[victim] = Time.time + repeatInterval;

            Vector3 victimPosition = victim.Transform.position;
            Vector3 contact = ClosestHitboxPoint(victimPosition);
            Vector3 away = victimPosition - contact;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
            {
                // Standing in the middle of a trigger so push out from the centre
                away = victimPosition - transform.position;
                away.y = 0f;
            }

            DamageResult result = victim.ApplyDamage(new DamageInfo
            {
                Amount = damage,
                Point = contact,
                Direction = away.sqrMagnitude > 1e-4f ? away.normalized : -victim.Transform.forward,
                Knockback = knockback,
                Stagger = stagger,
                Source = gameObject
            });

            if (result.Applied)
            {
                AudioManager.Play(hitSound, contact);
                Bit?.Invoke(this, victim);
            }
        }

        private bool Affects(Transform target)
        {
            return hurtEnemies || (!string.IsNullOrEmpty(targetTag) && target.CompareTag(targetTag));
        }

        private Vector3 ClosestHitboxPoint(Vector3 point)
        {
            Vector3 best = transform.position;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] == null || !hitboxes[i].enabled)
                {
                    continue;
                }

                Vector3 candidate = hitboxes[i].ClosestPoint(point);
                float distance = (candidate - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
