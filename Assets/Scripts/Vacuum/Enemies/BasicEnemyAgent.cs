using UnityEngine;

namespace Hedgewitch.Vacuum.Enemies
{
    /// <summary>
    /// A small rigidbody-driven enemy brain: wanders near its spawn, chases the player when it
    /// notices them, and when the vacuum catches it, steers away from the nozzle under its own
    /// power until it is overpowered and stops trying.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BasicEnemyAgent : MonoBehaviour, IVacuumAwareAgent
    {
        public enum AgentMode
        {
            Wander,
            Chase,
            Struggle,
            Flail
        }

        [Header("Movement")]
        [Min(0f)] [SerializeField] private float wanderSpeed = 1.8f;
        [Min(0f)] [SerializeField] private float chaseSpeed = 3.4f;
        [Min(0f)] [SerializeField] private float fleeSpeed = 4.2f;
        [Min(0.1f)] [SerializeField] private float acceleration = 14f;

        [Tooltip("Fraction of normal steering effort the enemy can muster while caught in an airflow. Full effort reads as the vacuum repelling it rather than as a creature struggling.")]
        [Range(0f, 1f)] [SerializeField] private float struggleSteerEffort = 0.4f;
        [Min(0f)] [SerializeField] private float turnSpeed = 540f;

        [Header("Perception")]
        [SerializeField] private Transform target;
        [Min(0f)] [SerializeField] private float aggroRange = 9f;
        [Min(0f)] [SerializeField] private float loseInterestRange = 14f;
        [SerializeField] private LayerMask sightBlockers = 1;

        [Header("Wander")]
        [Min(0.5f)] [SerializeField] private float wanderRadius = 5f;
        [Min(0.5f)] [SerializeField] private float repathInterval = 2.5f;

        [Header("Ground")]
        [SerializeField] private LayerMask groundLayers = 1;

        [Tooltip("How far below the feet the ground still counts as underfoot.")]
        [Min(0.01f)] [SerializeField] private float groundCheckDistance = 0.2f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private float footHeight = 0.9f;
        private float footRadius = 0.5f;
        private AgentMode mode = AgentMode.Wander;
        private Vector3 spawnPosition;
        private Vector3 wanderDestination;
        private float repathTimer;
        private Vector3 fleeDirection;
        private float vacuumPressure;

        public bool WantsMovementControl => mode != AgentMode.Flail;

        public AgentMode CurrentMode => mode;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.freezeRotation = true;

            capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
                footRadius = capsule.radius * scaleXZ;
                float halfHeight = Mathf.Max(capsule.height * 0.5f * Mathf.Abs(transform.lossyScale.y), footRadius);
                footHeight = halfHeight - footRadius;
            }

            spawnPosition = transform.position;
            wanderDestination = spawnPosition;

            if (target == null)
            {
                // Fall back to whatever is carrying a vacuum, which in practice is the player.
                VacuumController controller = FindAnyObjectByType<VacuumController>();
                if (controller != null)
                {
                    target = controller.transform.root;
                }
            }
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;

            switch (mode)
            {
                case AgentMode.Wander:
                    TickWander(deltaTime);
                    break;
                case AgentMode.Chase:
                    TickChase(deltaTime);
                    break;
                case AgentMode.Struggle:
                    TickStruggle(deltaTime);
                    break;
                case AgentMode.Flail:
                    // Off its feet. No steering, physics owns the body.
                    break;
            }
        }

        private void TickWander(float deltaTime)
        {
            repathTimer -= deltaTime;
            if (repathTimer <= 0f || (transform.position - wanderDestination).sqrMagnitude < 0.6f)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                wanderDestination = spawnPosition + new Vector3(offset.x, 0f, offset.y);
                repathTimer = repathInterval;
            }

            Steer(wanderDestination - transform.position, wanderSpeed, deltaTime);

            if (CanSeeTarget(aggroRange))
            {
                mode = AgentMode.Chase;
            }
        }

        private void TickChase(float deltaTime)
        {
            if (target == null)
            {
                mode = AgentMode.Wander;
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            if (toTarget.magnitude > loseInterestRange)
            {
                mode = AgentMode.Wander;
                repathTimer = 0f;
                return;
            }

            Steer(toTarget, chaseSpeed, deltaTime);
        }

        private void TickStruggle(float deltaTime)
        {
            // Scramble for the edge of the cone.
            float panic = Mathf.Lerp(0.6f, 1f, vacuumPressure);
            Steer(fleeDirection, fleeSpeed * panic, deltaTime, faceMovement: false, effort: struggleSteerEffort);
        }

        private Vector3 ComputeFleeDirection(Vector3 directionToNozzle)
        {
            Vector3 away = -directionToNozzle;
            away.y = 0f;

            Vector3 lateral = Vector3.Cross(Vector3.up, directionToNozzle);
            lateral.y = 0f;

            if (lateral.sqrMagnitude < 1e-4f)
            {
                return away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
            }

            lateral.Normalize();

            // Break toward whichever side it is already moving.
            if (Vector3.Dot(lateral, body.linearVelocity) < 0f)
            {
                lateral = -lateral;
            }

            Vector3 blended = away * 0.5f + lateral * 0.8f;
            return blended.sqrMagnitude > 1e-4f ? blended.normalized : lateral;
        }

        private void Steer(Vector3 direction, float speed, float deltaTime, bool faceMovement = true, float effort = 1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
            {
                return;
            }

            direction.Normalize();

            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 desired = direction * speed;
            Vector3 correction = desired - planar;

            // Only push while there is ground underfoot, so the enemy cannot walk through the air.
            if (IsGrounded())
            {
                body.AddForce(correction * (acceleration * Mathf.Clamp01(effort)), ForceMode.Acceleration);
            }

            if (faceMovement)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desiredRotation, turnSpeed * deltaTime);
            }
        }

        private bool IsGrounded()
        {
            // Measured from the bottom of the capsule, not the pivot, which sits at its centre.
            Vector3 origin = transform.position;
            if (capsule != null)
            {
                origin += transform.TransformVector(capsule.center);
            }

            origin -= Vector3.up * footHeight;

            return Physics.SphereCast(
                origin,
                footRadius * 0.9f,
                Vector3.down,
                out _,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        private bool CanSeeTarget(float range)
        {
            if (target == null)
            {
                return false;
            }

            Vector3 eye = transform.position + Vector3.up * 0.8f;
            Vector3 toTarget = target.position + Vector3.up * 0.8f - eye;
            if (toTarget.magnitude > range)
            {
                return false;
            }

            return !Physics.Raycast(
                eye, toTarget.normalized, toTarget.magnitude, sightBlockers, QueryTriggerInteraction.Ignore);
        }

        public void OnVacuumTargeted(in VacuumSuctionContext context)
        {
            mode = AgentMode.Struggle;
            fleeDirection = ComputeFleeDirection(context.DirectionToNozzle);
            vacuumPressure = 0f;
        }

        public void OnVacuumPressure(float pressure, in VacuumSuctionContext context)
        {
            vacuumPressure = pressure;

            // Keep re-aiming for the cone's edge as the nozzle sweeps around.
            Vector3 escape = ComputeFleeDirection(context.DirectionToNozzle);
            if (escape.sqrMagnitude > 1e-4f)
            {
                fleeDirection = Vector3.Slerp(fleeDirection, escape, 0.15f);
            }

            if (mode == AgentMode.Struggle && pressure >= 0.99f)
            {
                mode = AgentMode.Flail;
            }
        }

        public void OnVacuumEscapeAttempt(float remainingStamina)
        {
            // Pick a fresh escape line after each lunge so repeated attempts are not identical.
            Vector3 lateral = Vector3.Cross(Vector3.up, fleeDirection);
            fleeDirection = Vector3.Slerp(fleeDirection, Random.value < 0.5f ? lateral : -lateral, 0.5f).normalized;
        }

        public void OnVacuumOverpowered()
        {
            mode = AgentMode.Flail;
        }

        public void OnVacuumReleased(VacuumReleaseReason reason)
        {
            vacuumPressure = 0f;
            mode = CanSeeTarget(aggroRange) ? AgentMode.Chase : AgentMode.Wander;
            repathTimer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(Application.isPlaying ? spawnPosition : transform.position, wanderRadius);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
    }
}
