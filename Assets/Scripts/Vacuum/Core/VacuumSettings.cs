using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>All tunable parameters of a vacuum.</summary>
    [CreateAssetMenu(menuName = "Hedgewitch/Vacuum/Vacuum Settings", fileName = "VacuumSettings")]
    public class VacuumSettings : ScriptableObject
    {
        [Header("Detection")]
        [Tooltip("Furthest distance from the nozzle mouth that suction can reach.")]
        [Min(0.1f)] public float maxDistance = 9f;

        [Tooltip("Objects closer than this are treated as being at the mouth, which avoids divide-by-zero jitter.")]
        [Min(0f)] public float minDistance = 0.15f;

        [Tooltip("Total width of the suction cone, in degrees.")]
        [Range(1f, 179f)] public float horizontalConeAngle = 55f;

        [Tooltip("Total height of the suction cone, in degrees. Narrow values leave a dead zone under the nozzle, where the player has to tip the mouth down at anything low and close before it is drawn in.")]
        [Range(1f, 179f)] public float verticalConeAngle = 50f;

        [Tooltip("Radius around the mouth in which anything in front of the intake is drawn in whatever the cone angle says. Close to a real nozzle, air converges from every direction, and this is what lets the player hoover a small object by their feet without aiming at it. 0 disables it and restores the hard cone.")]
        [Min(0f)] public float intakeRadius = 1.6f;

        [Tooltip("How much wider the cone and range are for an object that is already in the airflow, as a fraction. Without this margin an object is dropped the instant it crosses the boundary, and since nothing decelerates a released object, it coasts on past the player.")]
        [Range(0f, 1f)] public float coneHysteresis = 0.25f;

        [Tooltip("Layers searched for vacuumable objects.")]
        public LayerMask detectionLayers = ~0;

        [Tooltip("Layers that block airflow: walls, floors, closed doors.")]
        public LayerMask occlusionLayers = 1;

        [Tooltip("Categories this vacuum is allowed to affect.")]
        public VacuumCategory acceptedCategories = VacuumCategory.All;

        [Min(1)] public int maxTrackedTargets = 32;

        [Tooltip("Seconds between broadphase re-scans. Tracked targets are still re-validated every physics step.")]
        [Min(0f)] public float rescanInterval = 0.05f;

        public bool requireLineOfSight = true;

        [Tooltip("Occlusion rays stop this far short of the target so its own surface never blocks itself.")]
        [Min(0f)] public float occlusionSkin = 0.1f;

        [Tooltip("When off, other vacuumable objects do not cast an airflow shadow, so a pile of props all get pulled. When on, a wardrobe shields what is behind it.")]
        public bool vacuumablesBlockAirflow;

        [Header("Suction force")]
        [Tooltip("Acceleration applied to an object of referenceMass at full strength, in m/s^2.")]
        public float baseForce = 60f;

        [Tooltip("Force multiplier along the cone. X: 0 = at the mouth, 1 = max range.")]
        public AnimationCurve forceOverDistance = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 0.6f), new Keyframe(1f, 0.15f));

        [Tooltip("Force multiplier across the cone. X: 0 = dead centre, 1 = cone edge.")]
        public AnimationCurve forceOverCone = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.35f));

        [Tooltip("An object whose vacuumMass equals this receives exactly baseForce of acceleration.")]
        [Min(0.01f)] public float referenceMass = 1f;

        [Tooltip("Cap on how fast an object may travel toward the nozzle at long range, in m/s.")]
        [Min(0.1f)] public float maxApproachSpeed = 10f;

        [Tooltip("Closing speed allowed right at the mouth, in m/s. Objects decelerate to this as they arrive, so they are drawn into the intake instead of hurtling through it.")]
        [Min(0.1f)] public float approachSpeedAtMouth = 2.5f;

        [Tooltip("Distance over which the closing speed tapers from maxApproachSpeed down to approachSpeedAtMouth.")]
        [Min(0.05f)] public float approachBrakingDistance = 2.5f;

        [Tooltip("Damping, per second, of the velocity component across the airflow. This is what funnels objects onto the nozzle instead of letting them sling past it. 0 disables it.")]
        [Min(0f)] public float lateralDamping = 10f;

        [Tooltip("How much gravity is cancelled under full suction. At 1 the pull direction decides where an object goes; below that, gravity biases everything toward a flat, floor-skimming approach.")]
        [Range(0f, 1f)] public float gravityCompensation = 1f;

        [Tooltip("Tangential force that makes objects spiral into the mouth instead of flying in dead straight.")]
        public float swirlForce = 4f;

        [Tooltip("Tumbling torque applied while an object is in the airflow.")]
        public float wobbleTorque = 10f;

        [Tooltip("Extra linear damping while sucked. Keeps objects from building runaway speed.")]
        [Min(0f)] public float suckedLinearDamping = 0.5f;

        [Tooltip("Extra angular damping while sucked.")]
        [Min(0f)] public float suckedAngularDamping = 0.35f;

        [Tooltip("Angular velocity ceiling while sucked, in rad/s.")]
        [Min(1f)] public float maxAngularVelocity = 28f;

        [Header("Capture")]
        [Tooltip("Radius around the nozzle mouth in which capturable objects are swallowed.")]
        [Min(0.01f)] public float captureRadius = 0.7f;

        [Tooltip("Seconds the swallow animation takes.")]
        [Min(0.01f)] public float captureDuration = 0.2f;

        [Tooltip("If the player releases the trigger mid-swallow, hand the object back instead of eating it.")]
        public bool cancelCapturesOnRelease = true;

        [Header("Spool")]
        [Min(0.001f)] public float spinUpTime = 0.12f;
        [Min(0.001f)] public float spinDownTime = 0.22f;

        [Tooltip("Total vacuumMass under pull that counts as a fully loaded motor. Drives audio strain and heavy VFX.")]
        [Min(0.01f)] public float loadReference = 14f;

        public float HorizontalHalfAngle => horizontalConeAngle * 0.5f;

        public float VerticalHalfAngle => verticalConeAngle * 0.5f;

        private void OnEnable()
        {
            EnsureCurves();
        }

        private void OnValidate()
        {
            EnsureCurves();
            if (minDistance >= maxDistance)
            {
                minDistance = Mathf.Max(0f, maxDistance - 0.1f);
            }

            if (approachSpeedAtMouth > maxApproachSpeed)
            {
                approachSpeedAtMouth = maxApproachSpeed;
            }
        }

        public void EnsureCurves()
        {
            if (forceOverDistance == null || forceOverDistance.length == 0)
            {
                forceOverDistance = new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.35f, 0.6f), new Keyframe(1f, 0.15f));
            }

            if (forceOverCone == null || forceOverCone.length == 0)
            {
                forceOverCone = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f));
            }
        }

        public static VacuumSettings CreateDefault()
        {
            VacuumSettings settings = CreateInstance<VacuumSettings>();
            settings.name = "VacuumSettings (Runtime Default)";
            settings.EnsureCurves();
            return settings;
        }

        public float EvaluateDistanceFalloff(float distanceNormalized)
        {
            return Mathf.Max(0f, forceOverDistance.Evaluate(Mathf.Clamp01(distanceNormalized)));
        }

        public float EvaluateConeFalloff(float coneFactor)
        {
            return Mathf.Max(0f, forceOverCone.Evaluate(Mathf.Clamp01(1f - coneFactor)));
        }

        public float EvaluateAllowedApproachSpeed(float distance)
        {
            float t = Mathf.Clamp01(distance / Mathf.Max(0.05f, approachBrakingDistance));
            return Mathf.Lerp(Mathf.Min(approachSpeedAtMouth, maxApproachSpeed), maxApproachSpeed, t);
        }
    }
}
