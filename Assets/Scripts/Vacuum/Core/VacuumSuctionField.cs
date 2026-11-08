using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Turns detection results into force.</summary>
    [DisallowMultipleComponent]
    public class VacuumSuctionField : MonoBehaviour
    {
        private const float MaxMassAdvantage = 64f;

        public float Apply(
            VacuumController controller,
            VacuumNozzle nozzle,
            VacuumSettings settings,
            List<VacuumTargetInfo> targets,
            float intensity,
            float deltaTime)
        {
            if (nozzle == null || settings == null || targets == null || intensity <= 0f)
            {
                return 0f;
            }

            Vector3 origin = nozzle.Origin;
            Vector3 direction = nozzle.Direction;
            Vector3 up = nozzle.Up;
            float loadMass = 0f;

            for (int i = 0; i < targets.Count; i++)
            {
                VacuumTargetInfo info = targets[i];
                if (!info.IsValid)
                {
                    continue;
                }

                IVacuumAffectable affectable = info.Affectable;

                // Objects the capture system has taken over are no longer in the airflow.
                if (affectable.IsCaptured)
                {
                    continue;
                }

                float resistance = Mathf.Clamp01(affectable.SuctionResistance);
                float distanceFalloff = settings.EvaluateDistanceFalloff(info.DistanceNormalized);
                float coneFalloff = settings.EvaluateConeFalloff(info.ConeFactor);
                float falloff = distanceFalloff * coneFalloff;

                float vacuumMass = Mathf.Max(0.0001f, affectable.VacuumMass);
                float massAdvantage = Mathf.Min(settings.referenceMass / vacuumMass, MaxMassAdvantage);

                float pull = settings.baseForce * falloff * intensity * (1f - resistance) * massAdvantage;

                VacuumSuctionContext context = new VacuumSuctionContext
                {
                    Controller = controller,
                    NozzleOrigin = origin,
                    NozzleDirection = direction,
                    NozzleUp = up,
                    SamplePoint = info.SamplePoint,
                    DirectionToNozzle = info.DirectionToNozzle,
                    Distance = info.Distance,
                    DistanceNormalized = info.DistanceNormalized,
                    ConeFactor = info.ConeFactor,
                    Falloff = falloff,
                    Intensity = intensity,
                    Pull = pull,
                    DeltaTime = deltaTime
                };

                affectable.OnSuctionTick(context);

                // Heavy things straining against the motor are what make it growl.
                loadMass += vacuumMass * falloff * (1f - resistance * 0.5f);
            }

            return Mathf.Clamp01(loadMass / Mathf.Max(0.01f, settings.loadReference));
        }

        public VacuumSuctionContext BuildContext(
            VacuumController controller,
            VacuumNozzle nozzle,
            VacuumSettings settings,
            in VacuumTargetInfo info,
            float intensity,
            float deltaTime)
        {
            float resistance = info.IsValid ? Mathf.Clamp01(info.Affectable.SuctionResistance) : 0f;
            float vacuumMass = info.IsValid ? Mathf.Max(0.0001f, info.Affectable.VacuumMass) : 1f;
            float falloff = settings.EvaluateDistanceFalloff(info.DistanceNormalized)
                            * settings.EvaluateConeFalloff(info.ConeFactor);
            float massAdvantage = Mathf.Min(settings.referenceMass / vacuumMass, MaxMassAdvantage);

            return new VacuumSuctionContext
            {
                Controller = controller,
                NozzleOrigin = nozzle.Origin,
                NozzleDirection = nozzle.Direction,
                NozzleUp = nozzle.Up,
                SamplePoint = info.SamplePoint,
                DirectionToNozzle = info.DirectionToNozzle,
                Distance = info.Distance,
                DistanceNormalized = info.DistanceNormalized,
                ConeFactor = info.ConeFactor,
                Falloff = falloff,
                Intensity = intensity,
                Pull = settings.baseForce * falloff * intensity * (1f - resistance) * massAdvantage,
                DeltaTime = deltaTime
            };
        }
    }
}
