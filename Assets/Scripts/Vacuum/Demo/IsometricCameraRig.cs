using UnityEngine;

namespace Hedgewitch.Vacuum.Demo
{
    /// <summary>
    /// A fixed-angle isometric camera: holds a constant yaw and pitch, follows the target with
    /// smoothing, and can be spun in 90 degree steps or zoomed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Tooltip("Height above the target's pivot that the camera frames.")]
        [SerializeField] private float focusHeight = 1.1f;

        [Header("Angles")]
        [Tooltip("Rotation around the world up axis. 45 gives the classic isometric read.")]
        [SerializeField] private float yaw = 45f;

        [Tooltip("Downward tilt. 30 to 40 degrees is the usual isometric range.")]
        [Range(10f, 80f)] [SerializeField] private float pitch = 35f;

        [Header("Distance")]
        [Min(1f)] [SerializeField] private float distance = 16f;
        [Min(1f)] [SerializeField] private float minDistance = 7f;
        [Min(1f)] [SerializeField] private float maxDistance = 28f;
        [Min(0f)] [SerializeField] private float zoomSpeed = 3f;

        [Header("Projection")]
        [SerializeField] private bool useOrthographic = true;

        [Tooltip("Half the vertical view height when orthographic. Scales with zoom.")]
        [Min(1f)] [SerializeField] private float orthographicSizeAtBaseDistance = 7.5f;

        [Header("Feel")]
        [Min(0f)] [SerializeField] private float followSmoothTime = 0.12f;

        [Tooltip("Degrees the rig turns per step when the rotate input is tapped.")]
        [SerializeField] private float rotationStep = 45f;

        [Min(0f)] [SerializeField] private float rotationSmoothSpeed = 6f;

        [Tooltip("How far the camera leans toward where the player is aiming, in metres.")]
        [Range(0f, 4f)] [SerializeField] private float aimLeadDistance = 1.2f;

        [Header("Input")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private VacuumAimController aim;

        private Camera cameraComponent;
        private Vector3 followVelocity;
        private float currentYaw;
        private float desiredYaw;
        private float baseDistance;
        private bool rotatePressed;

        public Vector3 PlanarForward
        {
            get
            {
                Vector3 forward = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
                return forward.normalized;
            }
        }

        public Vector3 PlanarRight => Quaternion.Euler(0f, currentYaw, 0f) * Vector3.right;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            currentYaw = yaw;
            desiredYaw = yaw;
            baseDistance = distance;

            if (input == null)
            {
                input = FindAnyObjectByType<PlayerInputReader>();
            }

            if (aim == null)
            {
                aim = FindAnyObjectByType<VacuumAimController>();
            }

            ApplyProjection();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            HandleInput();

            currentYaw = Mathf.LerpAngle(currentYaw, desiredYaw, 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));

            Quaternion rotation = Quaternion.Euler(pitch, currentYaw, 0f);
            Vector3 focus = target.position + Vector3.up * focusHeight;

            // Lean toward the aim point so the player can see what they are hoovering.
            if (aimLeadDistance > 0f && aim != null && aim.HasAim)
            {
                Vector3 lead = aim.AimPoint - focus;
                lead.y = 0f;
                if (lead.sqrMagnitude > 1e-4f)
                {
                    focus += Vector3.ClampMagnitude(lead, aimLeadDistance);
                }
            }

            Vector3 desiredPosition = focus - rotation * Vector3.forward * distance;

            transform.position = followSmoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime)
                : desiredPosition;

            transform.rotation = rotation;
            ApplyProjection();
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch, currentYaw, 0f);
            Vector3 focus = target.position + Vector3.up * focusHeight;
            transform.position = focus - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
            followVelocity = Vector3.zero;
        }

        public void Rotate(int steps)
        {
            desiredYaw += rotationStep * steps;
        }

        private void HandleInput()
        {
            if (input == null)
            {
                return;
            }

            float rotate = input.CameraRotate;
            if (Mathf.Abs(rotate) > 0.5f)
            {
                if (!rotatePressed)
                {
                    Rotate(rotate > 0f ? 1 : -1);
                    rotatePressed = true;
                }
            }
            else
            {
                rotatePressed = false;
            }

            float zoom = input.CameraZoom;
            if (Mathf.Abs(zoom) > 0.01f)
            {
                distance = Mathf.Clamp(distance - zoom * zoomSpeed, minDistance, maxDistance);
            }
        }

        private void ApplyProjection()
        {
            if (cameraComponent == null)
            {
                return;
            }

            cameraComponent.orthographic = useOrthographic;
            if (useOrthographic)
            {
                cameraComponent.orthographicSize =
                    orthographicSizeAtBaseDistance * (distance / Mathf.Max(0.01f, baseDistance));
            }
        }
    }
}
