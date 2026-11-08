using UnityEngine;

namespace Hedgewitch.Vacuum.Demo
{
    /// <summary>
    /// A rigidbody player for an isometric camera: input is interpreted in the camera's basis, the
    /// character turns to face where it is going, and while the vacuum is running it slows down and
    /// turns to face the aim direction instead, the way a Poltergust-style character braces behind
    /// the nozzle.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class IsometricPlayerController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField] private VacuumController vacuum;
        [SerializeField] private VacuumAimController aim;

        [Header("Movement")]
        [Min(0f)] [SerializeField] private float moveSpeed = 5f;
        [Min(0.1f)] [SerializeField] private float acceleration = 35f;
        [Min(0.1f)] [SerializeField] private float deceleration = 45f;

        [Tooltip("Speed multiplier while the vacuum is running.")]
        [Range(0.1f, 1f)] [SerializeField] private float vacuumingSpeedScale = 0.45f;

        [Min(0f)] [SerializeField] private float turnSpeed = 720f;

        [Header("Jump")]
        [SerializeField] private bool allowJump = true;
        [Min(0f)] [SerializeField] private float jumpSpeed = 5.5f;

        [Tooltip("Extra downward force so jumps feel snappy instead of floaty.")]
        [Min(0f)] [SerializeField] private float fallGravityBoost = 18f;

        [Header("Ground")]
        [SerializeField] private LayerMask groundLayers = 1;

        [Tooltip("How far below the feet the ground still counts as underfoot.")]
        [Min(0.01f)] [SerializeField] private float groundCheckDistance = 0.18f;

        [Min(0.01f)] [SerializeField] private float coyoteTime = 0.12f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private float footHeight = 1f;
        private float footRadius = 0.5f;
        private Vector3 planarInput;
        private bool jumpQueued;
        private float lastGroundedTime;
        private bool grounded;

        public bool IsGrounded => grounded;

        public bool IsVacuuming => vacuum != null && vacuum.IsSucking;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.freezeRotation = true;

            // Derive the foot sphere from the collider rather than assuming the pivot is at the
            // feet: a Unity capsule's pivot sits at its centre.
            capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
                footRadius = capsule.radius * scaleXZ;
                float halfHeight = Mathf.Max(capsule.height * 0.5f * Mathf.Abs(transform.lossyScale.y), footRadius);
                footHeight = halfHeight - footRadius;
            }

            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
                if (input == null)
                {
                    input = FindAnyObjectByType<PlayerInputReader>();
                }
            }

            if (vacuum == null)
            {
                vacuum = GetComponentInChildren<VacuumController>();
            }

            if (aim == null)
            {
                aim = GetComponentInChildren<VacuumAimController>();
            }

            if (cameraRig == null)
            {
                cameraRig = FindAnyObjectByType<IsometricCameraRig>();
            }
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 forward = cameraRig != null ? cameraRig.PlanarForward : Vector3.forward;
            Vector3 right = cameraRig != null ? cameraRig.PlanarRight : Vector3.right;

            planarInput = forward * move.y + right * move.x;
            planarInput.y = 0f;

            if (allowJump && input.JumpPressed)
            {
                jumpQueued = true;
            }
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;

            grounded = CheckGrounded();
            if (grounded)
            {
                lastGroundedTime = Time.time;
            }

            MoveBody(deltaTime);
            FaceDirection(deltaTime);
            HandleJump();
            ApplyFallBoost();
        }

        private void MoveBody(float deltaTime)
        {
            float speed = moveSpeed * (IsVacuuming ? vacuumingSpeedScale : 1f);
            Vector3 desired = planarInput * speed;

            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 difference = desired - planar;

            float rate = desired.sqrMagnitude > planar.sqrMagnitude ? acceleration : deceleration;
            Vector3 step = Vector3.ClampMagnitude(difference, rate * deltaTime);

            // Airborne control is deliberately weak so a jump commits to its arc.
            if (!grounded)
            {
                step *= 0.35f;
            }

            body.AddForce(step / deltaTime, ForceMode.Acceleration);
        }

        private void FaceDirection(float deltaTime)
        {
            Vector3 facing;

            if (IsVacuuming && aim != null && aim.HasAim)
            {
                facing = aim.AimDirection;
            }
            else
            {
                facing = planarInput;
            }

            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion desired = Quaternion.LookRotation(facing.normalized, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * deltaTime));
        }

        private void HandleJump()
        {
            if (!jumpQueued)
            {
                return;
            }

            jumpQueued = false;

            if (Time.time - lastGroundedTime > coyoteTime)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.y = jumpSpeed;
            body.linearVelocity = velocity;
            lastGroundedTime = -999f;
        }

        private void ApplyFallBoost()
        {
            if (grounded || fallGravityBoost <= 0f)
            {
                return;
            }

            if (body.linearVelocity.y < 0f)
            {
                body.AddForce(Vector3.down * fallGravityBoost, ForceMode.Acceleration);
            }
        }

        private bool CheckGrounded()
        {
            Vector3 origin = FootSphereCentre();
            return Physics.SphereCast(
                origin,
                footRadius * 0.95f,
                Vector3.down,
                out _,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        private Vector3 FootSphereCentre()
        {
            Vector3 centre = transform.position;
            if (capsule != null)
            {
                centre += transform.TransformVector(capsule.center);
            }

            return centre - Vector3.up * footHeight;
        }

        private void OnDrawGizmosSelected()
        {
            if (capsule == null)
            {
                capsule = GetComponent<CapsuleCollider>();
            }

            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(FootSphereCentre() + Vector3.down * groundCheckDistance, footRadius * 0.95f);
        }
    }
}
