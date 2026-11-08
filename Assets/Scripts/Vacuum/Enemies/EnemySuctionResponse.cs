using UnityEngine;
using Hedgewitch.Vacuum.Objects;

namespace Hedgewitch.Vacuum.Enemies
{
    /// <summary>Where an enemy stands in the tug of war with the vacuum.</summary>
    public enum EnemySuctionStage
    {
        Free,
        Alerted,
        Resisting,
        Overpowered,
        Captured
    }

    /// <summary>An enemy's response to suction.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemySuctionResponse : VacuumAffectableBase
    {
        [Header("Resistance")]
        [Tooltip("Counter-acceleration the enemy can produce while fully rested, in m/s^2. Compare against the vacuum's baseForce.")]
        [Min(0f)] [SerializeField] private float enemyStrength = 45f;

        [Tooltip("Seconds of resistance before the enemy is worn out. Drains in real seconds while braced, so this number means what it says.")]
        [Min(0.1f)] [SerializeField] private float stamina = 2.5f;

        [Tooltip("Stamina recovered per second once out of the airflow.")]
        [Min(0f)] [SerializeField] private float staminaRecovery = 0.6f;

        [Tooltip("Resistance left at zero stamina, as a fraction of enemyStrength.")]
        [Range(0f, 1f)] [SerializeField] private float exhaustedResistance = 0.15f;

        [Tooltip("Extra linear damping while braced, so a resisting enemy feels planted.")]
        [Min(0f)] [SerializeField] private float braceDamping = 2.5f;

        [Header("Grip")]
        [Tooltip("How fast the vacuum's advantage converts into grip, per second at full overpower.")]
        [Min(0.01f)] [SerializeField] private float gripBuildRate = 1.2f;

        [Tooltip("How fast grip bleeds away when the enemy is winning.")]
        [Min(0f)] [SerializeField] private float gripDecayRate = 0.5f;

        [Tooltip("Grip needed before the enemy can be swallowed at all.")]
        [Range(0f, 1f)] [SerializeField] private float captureGripThreshold = 0.85f;

        [Header("Escape")]
        [Tooltip("Seconds between break-free attempts while still resisting.")]
        [Min(0.1f)] [SerializeField] private float escapeInterval = 1.1f;

        [Tooltip("Instant velocity change of an escape lunge, in m/s. Keep it modest: a lunge is a scramble for cover, and a large value throws the enemy clean out of the cone so the struggle never progresses.")]
        [Min(0f)] [SerializeField] private float escapeLungeSpeed = 2.5f;

        [Tooltip("Grip the enemy must have lost before it starts scrambling. While it is comfortably winning it just braces.")]
        [Range(0f, 1f)] [SerializeField] private float escapeGripThreshold = 0.12f;

        [Tooltip("Grip removed by a successful lunge.")]
        [Range(0f, 1f)] [SerializeField] private float escapeGripRelief = 0.25f;

        [Tooltip("Stamina cost of one lunge, in seconds of resistance.")]
        [Min(0f)] [SerializeField] private float escapeStaminaCost = 0.5f;

        [Header("Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string pressureParameter = "VacuumPressure";
        [SerializeField] private string vacuumedParameter = "IsVacuumed";
        [SerializeField] private string struggleParameter = "Struggle";

        [Tooltip("Turn the enemy to face the nozzle while it is being pulled.")]
        [SerializeField] private bool faceNozzleWhileSucked = true;

        [Min(0f)] [SerializeField] private float faceNozzleTurnSpeed = 420f;

        [Header("Capture")]
        [SerializeField] private VacuumCaptureOutcome captureOutcome = VacuumCaptureOutcome.Deactivate;

        private Rigidbody body;
        private IVacuumAwareAgent agent;
        private EnemySuctionStage stage = EnemySuctionStage.Free;
        private float grip;
        private float currentStamina;
        private float escapeTimer;
        private float originalLinearDamping;
        private bool originalIsKinematic;
        private bool originalDetectCollisions;
        private bool physicsStateStored;
        private bool hasPressureParameter;
        private bool hasVacuumedParameter;
        private bool hasStruggleParameter;

        public float Grip => grip;

        public float StaminaNormalized => Mathf.Clamp01(currentStamina / Mathf.Max(0.0001f, stamina));

        public EnemySuctionStage Stage => stage;

        public float EnemyStrength
        {
            get => enemyStrength;
            set => enemyStrength = Mathf.Max(0f, value);
        }

        public override bool CanBeCaptured => canBeCaptured && grip >= captureGripThreshold;

        public override float SuctionResistance
        {
            get
            {
                // Resistance is reported to the vacuum so heavy VFX and audio strain react to an
                // enemy digging in, but the real push-back is applied as a force below.
                float resolve = Mathf.Lerp(exhaustedResistance, 1f, StaminaNormalized);
                return Mathf.Clamp01(suctionResistance + 0.35f * resolve * (1f - grip));
            }
        }

        protected override void Awake()
        {
            body = GetComponent<Rigidbody>();
            agent = GetComponentInParent<IVacuumAwareAgent>();
            currentStamina = stamina;
            vacuumCategory = VacuumCategory.Enemy;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            CacheAnimatorParameters();
            base.Awake();
        }

        private void Update()
        {
            // Recover out of the airflow.
            if (IsBeingSucked || IsCaptured)
            {
                return;
            }

            if (currentStamina < stamina)
            {
                currentStamina = Mathf.Min(stamina, currentStamina + staminaRecovery * Time.deltaTime);
            }

            if (grip > 0f)
            {
                grip = Mathf.Max(0f, grip - gripDecayRate * Time.deltaTime);
            }
        }

        protected override void OnFirstSuctionSource(in VacuumSuctionContext context)
        {
            if (body != null)
            {
                if (!physicsStateStored)
                {
                    originalLinearDamping = body.linearDamping;
                    originalIsKinematic = body.isKinematic;
                    originalDetectCollisions = body.detectCollisions;
                    physicsStateStored = true;
                }

                body.linearDamping = originalLinearDamping + braceDamping;
                body.WakeUp();
            }

            SetStage(EnemySuctionStage.Alerted);
            escapeTimer = escapeInterval * 0.5f;

            if (hasVacuumedParameter)
            {
                animator.SetBool(vacuumedParameter, true);
            }

            agent?.OnVacuumTargeted(context);
        }

        protected override void OnLastSuctionSourceRemoved(VacuumReleaseReason reason)
        {
            if (body != null && physicsStateStored)
            {
                body.linearDamping = originalLinearDamping;
            }

            SetStage(EnemySuctionStage.Free);

            if (hasVacuumedParameter)
            {
                animator.SetBool(vacuumedParameter, false);
            }

            if (hasPressureParameter)
            {
                animator.SetFloat(pressureParameter, 0f);
            }

            agent?.OnVacuumReleased(reason);
        }

        public override void OnSuctionTick(in VacuumSuctionContext context)
        {
            if (body == null || body.isKinematic || IsCaptured)
            {
                return;
            }

            float deltaTime = context.DeltaTime;
            Vector3 toNozzle = context.DirectionToNozzle;

            // The airflow pulls.
            body.AddForce(toNozzle * context.Pull, ForceMode.Acceleration);

            // The enemy pulls back, as long as it has the stamina and the will to.
            bool steering = agent == null || agent.WantsMovementControl;
            float resolve = Mathf.Lerp(exhaustedResistance, 1f, StaminaNormalized) * (steering ? 1f : 0.35f);
            float resistAcceleration = enemyStrength * resolve * (1f - grip * 0.75f);

            if (resistAcceleration > 0f)
            {
                // Bracing is anchoring, not shoving.
                float braceAcceleration = Mathf.Min(resistAcceleration, context.Pull);
                body.AddForce(-toNozzle * braceAcceleration, ForceMode.Acceleration);

                // Drain in real seconds, so stamina means the number of seconds it says.
                currentStamina = Mathf.Max(0f, currentStamina - deltaTime);
            }

            // Whoever *could* apply more force is winning, and that moves the grip meter.
            float advantage = (context.Pull - resistAcceleration) / Mathf.Max(1f, context.Pull);
            if (advantage > 0f)
            {
                grip = Mathf.Clamp01(grip + advantage * gripBuildRate * deltaTime);
                SetStage(grip >= captureGripThreshold ? EnemySuctionStage.Overpowered : EnemySuctionStage.Resisting);
            }
            else
            {
                grip = Mathf.Max(0f, grip - gripDecayRate * deltaTime);
                SetStage(EnemySuctionStage.Resisting);
            }

            TryEscape(context, deltaTime);
            FaceNozzle(context, deltaTime);

            if (hasPressureParameter)
            {
                animator.SetFloat(pressureParameter, grip);
            }

            agent?.OnVacuumPressure(grip, context);
        }

        public override bool TryBeginCapture(in VacuumCaptureContext context)
        {
            // Reaching the nozzle is not enough: the enemy has to have actually lost the struggle.
            if (grip < captureGripThreshold)
            {
                return false;
            }

            if (!base.TryBeginCapture(context))
            {
                return false;
            }

            SetStage(EnemySuctionStage.Captured);
            return true;
        }

        public override void OnCaptureCancelled(in VacuumCaptureContext context)
        {
            base.OnCaptureCancelled(context);
            grip = Mathf.Min(grip, captureGripThreshold * 0.6f);
            SetStage(EnemySuctionStage.Resisting);
        }

        public void ApplyStaminaDrain(float seconds)
        {
            currentStamina = Mathf.Max(0f, currentStamina - Mathf.Max(0f, seconds));
        }

        public void AddGrip(float amount)
        {
            grip = Mathf.Clamp01(grip + amount);
        }

        private void TryEscape(in VacuumSuctionContext context, float deltaTime)
        {
            if (grip >= captureGripThreshold || currentStamina <= 0f)
            {
                return;
            }

            // An enemy that is holding its ground has no reason to scramble.
            if (grip < escapeGripThreshold)
            {
                return;
            }

            escapeTimer -= deltaTime;
            if (escapeTimer > 0f)
            {
                return;
            }

            escapeTimer = escapeInterval;

            // Lunge sideways and away from the mouth rather than straight back, which reads as a
            // creature scrambling for cover instead of a rope snapping.
            Vector3 lateral = Vector3.Cross(Vector3.up, context.DirectionToNozzle).normalized;
            if (Random.value < 0.5f)
            {
                lateral = -lateral;
            }

            // Weighted toward sideways: the believable escape from a cone is out of its edge, and
            // it reads as a scramble rather than as the vacuum repelling the enemy.
            Vector3 escapeDirection = (-context.DirectionToNozzle * 0.45f + lateral * 0.85f + Vector3.up * 0.15f).normalized;
            body.AddForce(escapeDirection * escapeLungeSpeed, ForceMode.VelocityChange);

            // A lunge only buys back ground while the enemy still has something left.
            grip = Mathf.Max(0f, grip - escapeGripRelief * Mathf.Max(0.15f, StaminaNormalized));
            currentStamina = Mathf.Max(0f, currentStamina - escapeStaminaCost);

            if (hasStruggleParameter)
            {
                animator.SetTrigger(struggleParameter);
            }

            agent?.OnVacuumEscapeAttempt(StaminaNormalized);
        }

        private void FaceNozzle(in VacuumSuctionContext context, float deltaTime)
        {
            if (!faceNozzleWhileSucked)
            {
                return;
            }

            Vector3 flat = context.DirectionToNozzle;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion desired = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, faceNozzleTurnSpeed * deltaTime);
        }

        protected override void EnterCaptureState(in VacuumCaptureContext context)
        {
            if (body == null)
            {
                return;
            }

            if (!physicsStateStored)
            {
                originalLinearDamping = body.linearDamping;
                originalIsKinematic = body.isKinematic;
                originalDetectCollisions = body.detectCollisions;
                physicsStateStored = true;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        protected override void ExitCaptureState(in VacuumCaptureContext context)
        {
            if (body == null)
            {
                return;
            }

            body.isKinematic = originalIsKinematic;
            body.detectCollisions = originalDetectCollisions;
            body.linearDamping = originalLinearDamping;
        }

        protected override void ApplyCaptureOutcome(in VacuumCaptureContext context)
        {
            if (hasVacuumedParameter)
            {
                animator.SetBool(vacuumedParameter, false);
            }

            switch (captureOutcome)
            {
                case VacuumCaptureOutcome.Destroy:
                    Destroy(gameObject);
                    break;
                case VacuumCaptureOutcome.Deactivate:
                    ResetStruggle();
                    gameObject.SetActive(false);
                    break;
                case VacuumCaptureOutcome.KeepInWorld:
                    ResetStruggle();
                    break;
            }
        }

        private void ResetStruggle()
        {
            grip = 0f;
            currentStamina = stamina;
            stage = EnemySuctionStage.Free;

            if (body == null)
            {
                return;
            }

            body.isKinematic = originalIsKinematic;
            body.detectCollisions = originalDetectCollisions;
            body.linearDamping = originalLinearDamping;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void SetStage(EnemySuctionStage next)
        {
            if (stage == next)
            {
                return;
            }

            stage = next;

            if (next == EnemySuctionStage.Overpowered)
            {
                agent?.OnVacuumOverpowered();
            }
        }

        private void CacheAnimatorParameters()
        {
            hasPressureParameter = HasParameter(pressureParameter, AnimatorControllerParameterType.Float);
            hasVacuumedParameter = HasParameter(vacuumedParameter, AnimatorControllerParameterType.Bool);
            hasStruggleParameter = HasParameter(struggleParameter, AnimatorControllerParameterType.Trigger);
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName) || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
