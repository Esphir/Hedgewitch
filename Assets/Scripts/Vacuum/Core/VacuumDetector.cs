using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Outcome of testing one candidate against the suction cone.</summary>
    public enum VacuumEvaluation
    {
        Accepted,
        NotVacuumable,
        CategoryRejected,
        OutOfRange,
        OutOfCone,
        Occluded
    }

    /// <summary>Finds what the airflow can currently reach.</summary>
    [DisallowMultipleComponent]
    public class VacuumDetector : MonoBehaviour
    {
        private Collider[] overlapBuffer = new Collider[64];
        private readonly HashSet<IVacuumAffectable> seen = new HashSet<IVacuumAffectable>();

        public int Scan(VacuumNozzle nozzle, VacuumSettings settings, List<VacuumTargetInfo> results)
        {
            results.Clear();
            if (nozzle == null || settings == null)
            {
                return 0;
            }

            EnsureBufferCapacity(settings.maxTrackedTargets * 4);

            Vector3 origin = nozzle.Origin;

            // Hold the array locally: growing the field mid-pass would throw away these hits.
            Collider[] buffer = overlapBuffer;

            // Reach slightly past the nominal range so objects being held by the hysteresis margin
            // are still found by the broadphase and not dropped on the next scan.
            float searchRadius = settings.maxDistance * (1f + settings.coneHysteresis * 0.4f);

            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                searchRadius,
                buffer,
                settings.detectionLayers,
                QueryTriggerInteraction.Collide);

            seen.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = buffer[i];

                if (!VacuumRegistry.TryResolve(collider, out IVacuumAffectable affectable))
                {
                    continue;
                }

                if (!seen.Add(affectable))
                {
                    continue;
                }

                if (Evaluate(affectable, nozzle, settings, out VacuumTargetInfo info) != VacuumEvaluation.Accepted)
                {
                    continue;
                }

                results.Add(info);
                if (results.Count >= settings.maxTrackedTargets)
                {
                    break;
                }
            }

            // Never hold on to collider references between passes.
            for (int i = 0; i < hitCount; i++)
            {
                buffer[i] = null;
            }

            // The buffer came back full, so there may be colliders this pass never saw.
            if (hitCount == buffer.Length)
            {
                EnsureBufferCapacity(buffer.Length * 2);
            }

            return results.Count;
        }

        public VacuumEvaluation Evaluate(
            IVacuumAffectable affectable,
            VacuumNozzle nozzle,
            VacuumSettings settings,
            out VacuumTargetInfo info)
        {
            info = default;

            if (affectable == null || affectable.Transform == null)
            {
                return VacuumEvaluation.NotVacuumable;
            }

            if (!affectable.IsVacuumable)
            {
                return VacuumEvaluation.NotVacuumable;
            }

            if ((settings.acceptedCategories & affectable.Category) == 0)
            {
                return VacuumEvaluation.CategoryRejected;
            }

            Vector3 origin = nozzle.Origin;
            Vector3 samplePoint = affectable.GetSuctionSamplePoint(origin);
            Vector3 toNozzle = origin - samplePoint;
            float distance = toNozzle.magnitude;

            // An object already in the airflow is held with slightly wider limits than it was
            // caught with.
            float hysteresis = affectable.IsBeingSucked ? settings.coneHysteresis : 0f;

            if (distance > settings.maxDistance * (1f + hysteresis * 0.4f))
            {
                return VacuumEvaluation.OutOfRange;
            }

            float effectiveDistance = Mathf.Max(distance, settings.minDistance);
            Vector3 direction = distance > 1e-5f ? toNozzle / distance : -nozzle.Direction;

            // Cone test in nozzle space, with independent horizontal and vertical limits.
            Vector3 local = Quaternion.Inverse(nozzle.Rotation) * (samplePoint - origin);

            // Near the mouth, air converges on the intake from every direction in front of it
            // rather than only down the cone.
            float nearFieldFactor = 0f;
            if (settings.intakeRadius > 0f && local.z > 0f && distance <= settings.intakeRadius)
            {
                nearFieldFactor = Mathf.Lerp(0.15f, 1f, 1f - Mathf.Clamp01(distance / settings.intakeRadius));
            }

            float coneFactor;

            if (local.z <= 0f)
            {
                // Behind the mouth: kept only while touching the nozzle, so something
                // sliding past the player is not still being pulled.
                if (distance > settings.minDistance * 2f)
                {
                    return VacuumEvaluation.OutOfCone;
                }

                coneFactor = 1f;
            }
            else
            {
                float yaw = Mathf.Atan2(Mathf.Abs(local.x), local.z) * Mathf.Rad2Deg;
                float horizontalExtent = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                float pitch = Mathf.Atan2(Mathf.Abs(local.y), Mathf.Max(horizontalExtent, 1e-5f)) * Mathf.Rad2Deg;

                float yawRatio = yaw / Mathf.Max(settings.HorizontalHalfAngle * (1f + hysteresis), 0.01f);
                float pitchRatio = pitch / Mathf.Max(settings.VerticalHalfAngle * (1f + hysteresis), 0.01f);
                float coneRatio = Mathf.Max(yawRatio, pitchRatio);

                if (coneRatio > 1f)
                {
                    if (nearFieldFactor > 0f)
                    {
                        coneFactor = nearFieldFactor;
                    }
                    else if (distance <= settings.minDistance * 2f)
                    {
                        // Something already at the mouth stays grabbed even if it drifts off axis.
                        coneFactor = 1f;
                    }
                    else
                    {
                        return VacuumEvaluation.OutOfCone;
                    }
                }
                else
                {
                    coneFactor = Mathf.Max(1f - Mathf.Clamp01(coneRatio), nearFieldFactor);
                }
            }

            if (settings.requireLineOfSight && !HasLineOfSight(origin, samplePoint, affectable, settings))
            {
                return VacuumEvaluation.Occluded;
            }

            info = new VacuumTargetInfo
            {
                Affectable = affectable,
                SamplePoint = samplePoint,
                DirectionToNozzle = direction,
                Distance = effectiveDistance,
                DistanceNormalized = Mathf.Clamp01(effectiveDistance / settings.maxDistance),
                ConeFactor = coneFactor
            };

            return VacuumEvaluation.Accepted;
        }

        public bool HasLineOfSight(
            Vector3 origin,
            Vector3 samplePoint,
            IVacuumAffectable affectable,
            VacuumSettings settings)
        {
            Vector3 delta = samplePoint - origin;
            float distance = delta.magnitude - settings.occlusionSkin;
            if (distance <= 0f)
            {
                return true;
            }

            if (!Physics.Raycast(
                    origin,
                    delta.normalized,
                    out RaycastHit hit,
                    distance,
                    settings.occlusionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // Hitting the target itself (or one of its own colliders) is not an obstruction.
            Transform targetTransform = affectable.Transform;
            if (hit.transform == targetTransform || hit.transform.IsChildOf(targetTransform))
            {
                return true;
            }

            if (!VacuumRegistry.TryResolve(hit.collider, out IVacuumAffectable blocker))
            {
                // Plain world geometry: this is a wall, and the airflow stops here.
                return false;
            }

            if (blocker == affectable)
            {
                return true;
            }

            // Another vacuumable object is in the way.
            return !settings.vacuumablesBlockAirflow;
        }

        private void EnsureBufferCapacity(int capacity)
        {
            capacity = Mathf.Max(16, capacity);
            if (overlapBuffer.Length < capacity)
            {
                overlapBuffer = new Collider[capacity];
            }
        }
    }
}
