using UnityEngine;

namespace Hedgewitch.Vacuum.Demo
{
    /// <summary>Turns aim input into a nozzle direction and drives the suction trigger.</summary>
    [DisallowMultipleComponent]
    public class VacuumAimController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private VacuumController vacuum;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private IsometricCameraRig cameraRig;

        [Header("Pointer aiming")]
        [Tooltip("Layers the aim ray can land on. Anything else falls through to the ground plane.")]
        [SerializeField] private LayerMask aimLayers = ~0;

        [Tooltip("Furthest the aim ray is traced.")]
        [Min(1f)] [SerializeField] private float maxAimDistance = 60f;

        [Header("Stick aiming")]
        [Tooltip("How far in front of the player a stick aim point is placed.")]
        [Min(1f)] [SerializeField] private float stickAimDistance = 6f;

        [Tooltip("Downward tilt applied to stick aiming, so the nozzle points at the floor in front.")]
        [Range(-45f, 45f)] [SerializeField] private float stickAimPitch = -12f;

        [Header("Behaviour")]
        [Tooltip("Keep aiming the nozzle even when the trigger is not held.")]
        [SerializeField] private bool aimWhileIdle = true;

        [Tooltip("Turn suction on and off from the primary button. Switch off when something else, such as a weapon inventory, decides when the vacuum runs.")]
        [SerializeField] private bool driveSuctionFromInput = true;

        public bool DriveSuctionFromInput
        {
            get => driveSuctionFromInput;
            set => driveSuctionFromInput = value;
        }

        public Vector3 AimPoint { get; private set; }

        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        public bool HasAim { get; private set; }

        private readonly RaycastHit[] aimHits = new RaycastHit[16];

        // Last aim ray and the distance to the first surface under the cursor
        private Ray aimRay;
        private float aimSurfaceDistance = float.PositiveInfinity;
        private Vector3 aimSurfacePoint;
        private Vector3 aimSurfaceNormal = Vector3.up;
        private bool stickAiming;
        private Vector3 stickPlanarDirection = Vector3.forward;

        private void Awake()
        {
            if (vacuum == null)
            {
                vacuum = GetComponentInChildren<VacuumController>();
            }

            if (input == null)
            {
                input = GetComponentInParent<PlayerInputReader>();
                if (input == null)
                {
                    input = FindAnyObjectByType<PlayerInputReader>();
                }
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (cameraRig == null && aimCamera != null)
            {
                cameraRig = aimCamera.GetComponent<IsometricCameraRig>();
            }
        }

        private void Update()
        {
            if (vacuum == null || input == null)
            {
                return;
            }

            bool suckHeld = driveSuctionFromInput ? input.SuckHeld : vacuum.IsSucking;
            if (driveSuctionFromInput)
            {
                vacuum.SetSuction(suckHeld);
            }

            if (!suckHeld && !aimWhileIdle)
            {
                HasAim = false;
                vacuum.Nozzle?.ClearAim();
                return;
            }

            HasAim = ResolveAimPoint(out Vector3 point);
            if (!HasAim)
            {
                return;
            }

            AimPoint = point;

            VacuumNozzle nozzle = vacuum.Nozzle;
            Vector3 origin = nozzle != null ? nozzle.Origin : transform.position;
            Vector3 direction = point - origin;
            if (direction.sqrMagnitude > 1e-4f)
            {
                AimDirection = direction.normalized;
                nozzle?.AimTowards(AimDirection);
            }
        }

        // Where the cursor crosses this height or the first surface it hits above it
        // Use this for flat shots since the floor point drifts along the camera view
        public Vector3 AimPointAtHeight(float height)
        {
            if (!HasAim)
            {
                return AimPoint;
            }

            if (stickAiming)
            {
                Vector3 start = new Vector3(transform.position.x, height, transform.position.z);
                return start + stickPlanarDirection * maxAimDistance;
            }

            Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            if (!plane.Raycast(aimRay, out float enter))
            {
                return AimPoint;
            }

            return aimRay.GetPoint(Mathf.Min(enter, aimSurfaceDistance));
        }

        // The surface under the cursor and false for stick aim or open sky
        public bool TryGetAimSurface(out Vector3 point, out Vector3 normal)
        {
            point = aimSurfacePoint;
            normal = aimSurfaceNormal;
            return HasAim && !stickAiming && !float.IsPositiveInfinity(aimSurfaceDistance);
        }

        private bool ResolveAimPoint(out Vector3 point)
        {
            point = Vector3.zero;

            VacuumNozzle nozzle = vacuum.Nozzle;
            Vector3 origin = nozzle != null ? nozzle.Origin : transform.position;

            stickAiming = !input.UsingPointer && input.AimStick.sqrMagnitude > 0.04f;
            if (stickAiming)
            {
                point = ResolveStickAim(origin);
                return true;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null)
                {
                    return false;
                }
            }

            Ray ray = aimCamera.ScreenPointToRay(input.PointerPosition);
            aimRay = ray;
            aimSurfaceDistance = float.PositiveInfinity;

            // Prefer a real surface under the cursor, so aiming at a wall or a prop points the
            // nozzle at it rather than past it.
            if (TryRaycastPastSelf(ray, out Vector3 surfacePoint, out float surfaceDistance, out Vector3 surfaceNormal))
            {
                aimSurfaceDistance = surfaceDistance;
                aimSurfacePoint = surfacePoint;
                aimSurfaceNormal = surfaceNormal;
                point = surfacePoint;
                return true;
            }

            // Otherwise meet the horizontal plane that runs through the nozzle mouth.
            Plane plane = new Plane(Vector3.up, new Vector3(0f, origin.y, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(Mathf.Min(enter, maxAimDistance));
                return true;
            }

            point = origin + aimCamera.transform.forward * stickAimDistance;
            return true;
        }

        private bool TryRaycastPastSelf(Ray ray, out Vector3 point, out float nearest, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            nearest = float.MaxValue;
            Transform self = transform.root;

            int count = Physics.RaycastNonAlloc(
                ray, aimHits, maxAimDistance, aimLayers, QueryTriggerInteraction.Ignore);

            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = aimHits[i];
                if (hit.transform == null || hit.transform.IsChildOf(self))
                {
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    point = hit.point;
                    normal = hit.normal;
                    found = true;
                }
            }

            return found;
        }

        private Vector3 ResolveStickAim(Vector3 origin)
        {
            Vector2 stick = input.AimStick.normalized;

            Vector3 forward = cameraRig != null ? cameraRig.PlanarForward : Vector3.forward;
            Vector3 right = cameraRig != null ? cameraRig.PlanarRight : Vector3.right;

            Vector3 direction = (forward * stick.y + right * stick.x).normalized;
            stickPlanarDirection = direction;
            direction = Quaternion.AngleAxis(stickAimPitch, Vector3.Cross(Vector3.up, direction)) * direction;

            return origin + direction * stickAimDistance;
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasAim)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(AimPoint, 0.2f);
        }
    }
}
