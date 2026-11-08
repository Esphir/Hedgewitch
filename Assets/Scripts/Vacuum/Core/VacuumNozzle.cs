using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>The mouth of the vacuum.</summary>
    [DisallowMultipleComponent]
    public class VacuumNozzle : MonoBehaviour
    {
        [Header("Mouth")]
        [Tooltip("Local offset from this transform to the actual mouth of the nozzle.")]
        [SerializeField] private Vector3 mouthOffset = new Vector3(0f, 0f, 0.35f);

        [Tooltip("Optional transform used as the mouth instead of this transform plus the offset.")]
        [SerializeField] private Transform mouthOverride;

        [Header("Aiming")]
        [Tooltip("Degrees per second the nozzle can swing toward a requested aim direction.")]
        [Min(1f)] [SerializeField] private float turnSpeed = 720f;

        [Tooltip("Lowest the nozzle may point, in degrees below the horizon.")]
        [Range(-89f, 89f)] [SerializeField] private float minPitch = -70f;

        [Tooltip("Highest the nozzle may point, in degrees above the horizon.")]
        [Range(-89f, 89f)] [SerializeField] private float maxPitch = 55f;

        [Tooltip("When no aim is requested, drift back to the rest rotation of the parent.")]
        [SerializeField] private bool returnToRestWhenIdle = true;

        [Min(1f)] [SerializeField] private float restReturnSpeed = 240f;

        [Tooltip("How long an aim request keeps steering the nozzle. Long enough that callers aiming from FixedUpdate, or at any rate below the frame rate, hold the aim steadily; short enough that a caller going quiet hands the nozzle back to its rest pose.")]
        [Min(0.02f)] [SerializeField] private float aimHoldTime = 0.25f;

        private Quaternion restLocalRotation;
        private Vector3 requestedDirection;
        private bool hasAimRequest;
        private float aimRequestAge;

        public Vector3 Origin => mouthOverride != null ? mouthOverride.position : transform.TransformPoint(mouthOffset);

        public Vector3 Direction => mouthOverride != null ? mouthOverride.forward : transform.forward;

        public Vector3 Up => mouthOverride != null ? mouthOverride.up : transform.up;

        public Vector3 Right => mouthOverride != null ? mouthOverride.right : transform.right;

        public Quaternion Rotation => Quaternion.LookRotation(Direction, Up);

        private void Awake()
        {
            restLocalRotation = transform.localRotation;
            requestedDirection = Direction;
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;

            if (hasAimRequest)
            {
                Quaternion desired = Quaternion.LookRotation(requestedDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * deltaTime);

                // The request stays live for a short window rather than a single frame, so the
                // nozzle does not stutter back toward rest between aim updates.
                aimRequestAge += deltaTime;
                if (aimRequestAge > aimHoldTime)
                {
                    hasAimRequest = false;
                }
            }
            else if (returnToRestWhenIdle)
            {
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, restLocalRotation, restReturnSpeed * deltaTime);
            }
        }

        public void AimTowards(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 1e-6f)
            {
                return;
            }

            requestedDirection = ClampPitch(worldDirection.normalized);
            hasAimRequest = true;
            aimRequestAge = 0f;
        }

        public void AimAtPoint(Vector3 worldPoint)
        {
            AimTowards(worldPoint - Origin);
        }

        public void ClearAim()
        {
            hasAimRequest = false;
            aimRequestAge = 0f;
        }

        public static float ConeRadiusAt(float distance, float halfAngleDegrees)
        {
            return Mathf.Tan(Mathf.Clamp(halfAngleDegrees, 0.01f, 89f) * Mathf.Deg2Rad) * distance;
        }

        private Vector3 ClampPitch(Vector3 direction)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = new Vector3(Direction.x, 0f, Direction.z);
                if (flat.sqrMagnitude < 1e-6f)
                {
                    flat = Vector3.forward;
                }
            }

            flat.Normalize();

            float pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            float radians = pitch * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians)).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(Origin, 0.08f);
            Gizmos.DrawLine(Origin, Origin + Direction * 1.5f);
        }
    }
}
