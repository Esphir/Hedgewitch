using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat
{
    /// <summary>
    /// How a body physically answers a hit: it is shoved along the hit, knocked up if the hit
    /// says so, flashes, and loses control of itself for the stagger time.
    /// </summary>
    /// Losing control is done by switching off the behaviours listed in <see cref="disableWhileStaggered"/>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class HitReaction : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Body that is shoved. Found on this object if left empty.")]
        [SerializeField] private Rigidbody body;

        [Tooltip("0 takes knockback in full, 1 ignores it. Big enemies shrug more of it off.")]
        [Range(0f, 1f)] [SerializeField] private float knockbackResistance;

        [Tooltip("Scales how long hits stagger this body.")]
        [Min(0f)] [SerializeField] private float staggerMultiplier = 1f;

        [Tooltip("Behaviours switched off while staggered, typically the AI brain, so it cannot steer out of a knockback.")]
        [SerializeField] private Behaviour[] disableWhileStaggered = new Behaviour[0];

        [Header("Flash")]
        [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.85f, 1f);

        [Min(0f)] [SerializeField] private float flashDuration = 0.09f;

        [Tooltip("Renderers that flash. Every renderer under this object if left empty.")]
        [SerializeField] private Renderer[] flashRenderers = new Renderer[0];

        private readonly List<Behaviour> disabledByUs = new List<Behaviour>();
        private Health health;
        private MaterialPropertyBlock flashBlock;
        private MaterialPropertyBlock clearBlock;
        private float staggerTimer;
        private float flashTimer;

        public bool IsStaggered => staggerTimer > 0f;

        public float StaggerRemaining => Mathf.Max(0f, staggerTimer);

        public float KnockbackResistance
        {
            get => knockbackResistance;
            set => knockbackResistance = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (flashRenderers == null || flashRenderers.Length == 0)
            {
                flashRenderers = GetComponentsInChildren<Renderer>(true);
            }

            flashBlock = new MaterialPropertyBlock();
            clearBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            EndStagger();
            EndFlash();
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    EndFlash();
                }
            }

            if (staggerTimer > 0f)
            {
                staggerTimer -= Time.deltaTime;
                if (staggerTimer <= 0f)
                {
                    EndStagger();
                }
            }
        }

        /// <summary>Set the behaviours switched off while staggered</summary>
        public void SetStaggerBehaviours(params Behaviour[] behaviours)
        {
            EndStagger();
            disableWhileStaggered = behaviours ?? new Behaviour[0];
        }

        /// <summary>Apply the movement part of a hit directly</summary>
        public void ApplyImpact(in DamageInfo info)
        {
            Shove(info);
            Stagger(info.Stagger);
        }

        public void Stagger(float seconds)
        {
            seconds *= staggerMultiplier;
            if (seconds <= 0f)
            {
                return;
            }

            staggerTimer = Mathf.Max(staggerTimer, seconds);

            for (int i = 0; i < disableWhileStaggered.Length; i++)
            {
                Behaviour behaviour = disableWhileStaggered[i];
                if (behaviour != null && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    disabledByUs.Add(behaviour);
                }
            }
        }

        private void HandleDamaged(DamageInfo info, DamageResult result)
        {
            Shove(info);
            Stagger(info.Stagger);
            StartFlash();
        }

        private void Shove(in DamageInfo info)
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            float take = 1f - knockbackResistance;
            if (take <= 0f)
            {
                return;
            }

            Vector3 direction = info.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-6f)
            {
                direction.Normalize();
            }

            Vector3 velocity = body.linearVelocity;

            if (info.Knockback > 0f && direction.sqrMagnitude > 0f)
            {
                // Replace the horizontal velocity rather than adding to it
                Vector3 planar = direction * (info.Knockback * take);
                velocity.x = planar.x;
                velocity.z = planar.z;
            }

            if (info.Launch > 0f)
            {
                velocity.y = Mathf.Max(velocity.y, info.Launch * take);
            }

            body.linearVelocity = velocity;
            body.WakeUp();
        }

        private void EndStagger()
        {
            staggerTimer = 0f;
            for (int i = 0; i < disabledByUs.Count; i++)
            {
                if (disabledByUs[i] != null)
                {
                    disabledByUs[i].enabled = true;
                }
            }

            disabledByUs.Clear();
        }

        private void StartFlash()
        {
            if (flashDuration <= 0f || flashRenderers == null)
            {
                return;
            }

            flashTimer = flashDuration;
            flashBlock.SetColor(BaseColorId, flashColor);
            flashBlock.SetColor(ColorId, flashColor);

            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] != null && !(flashRenderers[i] is ParticleSystemRenderer))
                {
                    flashRenderers[i].SetPropertyBlock(flashBlock);
                }
            }
        }

        private void EndFlash()
        {
            flashTimer = 0f;
            if (flashRenderers == null)
            {
                return;
            }

            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] != null && !(flashRenderers[i] is ParticleSystemRenderer))
                {
                    flashRenderers[i].SetPropertyBlock(clearBlock);
                }
            }
        }
    }
}
