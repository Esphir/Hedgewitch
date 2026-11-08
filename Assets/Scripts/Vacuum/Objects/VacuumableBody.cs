using UnityEngine;

namespace Hedgewitch.Vacuum.Objects
{
    /// <summary>What a swallowed object does with itself.</summary>
    public enum VacuumCaptureOutcome
    {
        Destroy,

        Deactivate,

        KeepInWorld
    }

    /// <summary>A rigidbody prop the vacuum can pull on.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VacuumableBody : VacuumAffectableBase
    {
        [Header("Profile")]
        [Tooltip("Optional preset. When set, it overwrites the fields below at Awake.")]
        [SerializeField] private VacuumableProfile profile;

        [Header("Response tuning")]
        [Tooltip("Scales how much of gravity the airflow cancels for this object.")]
        [Range(0f, 2f)] [SerializeField] private float liftMultiplier = 1f;

        [Tooltip("Scales the spiralling tangential force.")]
        [Range(0f, 3f)] [SerializeField] private float swirlMultiplier = 1f;

        [Tooltip("Scales the tumbling torque.")]
        [Range(0f, 4f)] [SerializeField] private float wobbleMultiplier = 1f;

        [Tooltip("Scales the cross-flow damping that funnels this object onto the nozzle. Higher tracks the mouth more tightly; 0 lets it sling past on its own momentum.")]
        [Range(0f, 3f)] [SerializeField] private float funnelMultiplier = 1f;

        [Header("Capture")]
        [SerializeField] private VacuumCaptureOutcome captureOutcome = VacuumCaptureOutcome.Destroy;

        [Tooltip("Score or currency banked when swallowed.")]
        [SerializeField] private int collectionValue;

        private Rigidbody body;
        private float noiseSeed;

        private float originalLinearDamping;
        private float originalAngularDamping;
        private float originalMaxAngularVelocity;
        private bool originalDetectCollisions;
        private bool originalIsKinematic;
        private bool physicsStateStored;

        public Rigidbody Body => body;

        public int CollectionValue => collectionValue;

        public VacuumCaptureOutcome CaptureOutcome
        {
            get => captureOutcome;
            set => captureOutcome = value;
        }

        public override Vector3 SuctionCenter => body != null ? body.worldCenterOfMass : transform.position;

        protected override void Awake()
        {
            body = GetComponent<Rigidbody>();
            noiseSeed = Random.value * 128f;

            if (profile != null)
            {
                ApplyProfile(profile);
            }

            base.Awake();
        }

        public void ApplyProfile(VacuumableProfile source)
        {
            if (source == null)
            {
                return;
            }

            profile = source;
            vacuumCategory = source.category;
            vacuumMass = Mathf.Max(0.0001f, source.vacuumMass);
            suctionResistance = Mathf.Clamp01(source.suctionResistance);
            canBeCaptured = source.canBeCaptured;
            captureRadiusBonus = Mathf.Max(0f, source.captureRadiusBonus);
            liftMultiplier = source.liftMultiplier;
            swirlMultiplier = source.swirlMultiplier;
            wobbleMultiplier = source.wobbleMultiplier;
            funnelMultiplier = source.funnelMultiplier;
            captureOutcome = source.captureOutcome;
            collectionValue = source.collectionValue;

            if (!source.overrideRigidbody)
            {
                return;
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body != null)
            {
                body.mass = Mathf.Max(0.0001f, source.rigidbodyMass);
                body.linearDamping = source.linearDamping;
                body.angularDamping = source.angularDamping;
            }
        }

        protected override void OnFirstSuctionSource(in VacuumSuctionContext context)
        {
            if (body == null)
            {
                return;
            }

            if (!physicsStateStored)
            {
                originalLinearDamping = body.linearDamping;
                originalAngularDamping = body.angularDamping;
                originalMaxAngularVelocity = body.maxAngularVelocity;
                originalDetectCollisions = body.detectCollisions;
                originalIsKinematic = body.isKinematic;
                physicsStateStored = true;
            }

            VacuumSettings settings = ResolveSettings(context.Controller);
            if (settings != null)
            {
                // Extra damping while in the airflow is what stops objects building runaway speed
                // or vibrating against the nozzle.
                body.linearDamping = originalLinearDamping + settings.suckedLinearDamping;
                body.angularDamping = originalAngularDamping + settings.suckedAngularDamping;
                body.maxAngularVelocity = Mathf.Max(originalMaxAngularVelocity, settings.maxAngularVelocity);
            }

            // A prop resting on the floor has gone to sleep; without this it would ignore the first
            // few frames of suction.
            body.WakeUp();
        }

        protected override void OnLastSuctionSourceRemoved(VacuumReleaseReason reason)
        {
            RestorePhysicsState();
        }

        public override void OnSuctionTick(in VacuumSuctionContext context)
        {
            if (body == null || body.isKinematic || IsCaptured)
            {
                return;
            }

            VacuumSettings settings = ResolveSettings(context.Controller);
            Vector3 toNozzle = context.DirectionToNozzle;

            // Primary attraction. Acceleration rather than Force, because vacuumMass is
            // already folded into the pull.
            body.AddForce(toNozzle * context.Pull, ForceMode.Acceleration);

            float strength = Mathf.Clamp01(context.Falloff * context.Intensity);

            if (settings == null)
            {
                return;
            }

            // Lift, so light props actually leave the ground instead of scraping along it.
            if (body.useGravity && settings.gravityCompensation > 0f)
            {
                float lift = settings.gravityCompensation * strength * liftMultiplier;
                body.AddForce(-Physics.gravity * lift, ForceMode.Acceleration);
            }

            // Swirl: a tangential push around the nozzle axis so objects spiral into the mouth.
            if (settings.swirlForce > 0f && swirlMultiplier > 0f)
            {
                Vector3 swirlAxis = Vector3.Cross(toNozzle, context.NozzleUp);
                if (swirlAxis.sqrMagnitude < 1e-5f)
                {
                    swirlAxis = Vector3.Cross(toNozzle, context.NozzleDirection);
                }

                if (swirlAxis.sqrMagnitude > 1e-5f)
                {
                    float swirl = settings.swirlForce * strength * swirlMultiplier;
                    body.AddForce(swirlAxis.normalized * swirl, ForceMode.Acceleration);
                }
            }

            // Tumbling. Perlin noise rather than Random so the wobble is smooth, not a jitter.
            if (settings.wobbleTorque > 0f && wobbleMultiplier > 0f)
            {
                float time = Time.time * 1.7f;
                Vector3 torque = new Vector3(
                    Mathf.PerlinNoise(noiseSeed, time) - 0.5f,
                    Mathf.PerlinNoise(noiseSeed + 11.3f, time) - 0.5f,
                    Mathf.PerlinNoise(noiseSeed + 27.7f, time) - 0.5f) * 2f;

                body.AddTorque(torque * (settings.wobbleTorque * strength * wobbleMultiplier), ForceMode.Acceleration);
            }

            ShapeApproach(settings, toNozzle, strength, context.Distance, context.DeltaTime);
        }

        private void ShapeApproach(
            VacuumSettings settings,
            Vector3 toNozzle,
            float strength,
            float distance,
            float deltaTime)
        {
            Vector3 velocity = body.linearVelocity;
            float approach = Vector3.Dot(velocity, toNozzle);
            Vector3 lateral = velocity - toNozzle * approach;

            bool changed = false;

            if (settings.lateralDamping > 0f && funnelMultiplier > 0f && lateral.sqrMagnitude > 1e-6f)
            {
                float decay = Mathf.Exp(-settings.lateralDamping * funnelMultiplier * strength * deltaTime);
                lateral *= decay;
                changed = true;
            }

            float allowed = settings.EvaluateAllowedApproachSpeed(distance);
            if (approach > allowed)
            {
                approach = allowed;
                changed = true;
            }

            if (changed)
            {
                body.linearVelocity = lateral + toNozzle * approach;
            }
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
                originalAngularDamping = body.angularDamping;
                originalMaxAngularVelocity = body.maxAngularVelocity;
                originalDetectCollisions = body.detectCollisions;
                originalIsKinematic = body.isKinematic;
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
            RestorePhysicsState();
        }

        protected override void ApplyCaptureMotion(in VacuumCaptureContext context, float eased)
        {
            if (body == null)
            {
                base.ApplyCaptureMotion(context, eased);
                return;
            }

            // Kinematic move rather than a transform write, so the capture still goes through the
            // physics engine and interpolation stays smooth.
            Vector3 position = Vector3.Lerp(transform.position, context.NozzleOrigin, Mathf.Clamp01(eased * 0.6f + 0.2f));
            Quaternion spin = Quaternion.AngleAxis(eased * 540f, context.NozzleRotation * Vector3.forward);

            body.MovePosition(position);
            body.MoveRotation(spin * transform.rotation);
            transform.localScale = Vector3.Lerp(PreCaptureScale, PreCaptureScale * 0.05f, eased);
        }

        protected override void ApplyCaptureOutcome(in VacuumCaptureContext context)
        {
            switch (captureOutcome)
            {
                case VacuumCaptureOutcome.Destroy:
                    Destroy(gameObject);
                    break;
                case VacuumCaptureOutcome.Deactivate:
                    RestoreForReuse();
                    gameObject.SetActive(false);
                    break;
                case VacuumCaptureOutcome.KeepInWorld:
                    RestoreForReuse();
                    break;
            }
        }

        private void RestoreForReuse()
        {
            if (body != null)
            {
                body.isKinematic = originalIsKinematic;
                body.detectCollisions = originalDetectCollisions;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            RestorePhysicsState();
        }

        private void RestorePhysicsState()
        {
            if (body == null || !physicsStateStored)
            {
                return;
            }

            body.linearDamping = originalLinearDamping;
            body.angularDamping = originalAngularDamping;
            body.maxAngularVelocity = originalMaxAngularVelocity;
        }

        private static VacuumSettings ResolveSettings(VacuumController controller)
        {
            return controller != null ? controller.Settings : null;
        }
    }
}
