using UnityEngine;

namespace Hedgewitch.Vacuum.Feedback
{
    /// <summary>Makes the airflow visible.</summary>
    [DisallowMultipleComponent]
    public class VacuumVisuals : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private VacuumController controller;

        [Tooltip("Funnel of air drawn flowing into the mouth. Should be a child of the nozzle, facing back at it.")]
        [SerializeField] private ParticleSystem airflowParticles;

        [Tooltip("Dust and grit torn off the floor, emitted near the mouth.")]
        [SerializeField] private ParticleSystem debrisParticles;

        [Tooltip("Extra effect that only appears when the motor is straining.")]
        [SerializeField] private ParticleSystem strainParticles;

        [Tooltip("Visual mesh of the nozzle, pulsed and shaken with load.")]
        [SerializeField] private Transform nozzleVisual;

        [SerializeField] private Light nozzleLight;

        [Tooltip("Optional animator on the nozzle. Driven with the two parameters below.")]
        [SerializeField] private Animator nozzleAnimator;

        [SerializeField] private string animatorIntensityParameter = "VacuumIntensity";
        [SerializeField] private string animatorLoadParameter = "VacuumLoad";

        [Header("Airflow shaping")]
        [Tooltip("Rebuild the particle funnel from the vacuum settings at startup.")]
        [SerializeField] private bool autoConfigureAirflow = true;

        [Tooltip("Seconds a particle takes to travel the length of the cone.")]
        [Min(0.05f)] [SerializeField] private float airflowTravelTime = 0.4f;

        [Min(0f)] [SerializeField] private float airflowRateAtFullPower = 160f;

        [Min(0f)] [SerializeField] private float debrisRateAtFullPower = 40f;

        [Header("Load response")]
        [Tooltip("How much the nozzle swells when fully loaded.")]
        [Range(0f, 0.5f)] [SerializeField] private float nozzleSwell = 0.08f;

        [Tooltip("Shake amplitude at full load, in metres.")]
        [Range(0f, 0.2f)] [SerializeField] private float shakeAmplitude = 0.025f;

        [Min(0f)] [SerializeField] private float shakeFrequency = 28f;

        [SerializeField] private float lightIntensityAtFullPower = 2.5f;

        [Min(0.01f)] [SerializeField] private float smoothing = 10f;

        private ParticleSystem.EmissionModule airflowEmission;
        private ParticleSystem.EmissionModule debrisEmission;
        private ParticleSystem.EmissionModule strainEmission;
        private Vector3 nozzleVisualBaseScale = Vector3.one;
        private Vector3 nozzleVisualBasePosition;
        private float smoothedIntensity;
        private float smoothedLoad;
        private int intensityParameterHash;
        private int loadParameterHash;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<VacuumController>();
            }

            if (nozzleVisual != null)
            {
                nozzleVisualBaseScale = nozzleVisual.localScale;
                nozzleVisualBasePosition = nozzleVisual.localPosition;
            }

            intensityParameterHash = Animator.StringToHash(animatorIntensityParameter);
            loadParameterHash = Animator.StringToHash(animatorLoadParameter);

            if (airflowParticles != null)
            {
                airflowEmission = airflowParticles.emission;
            }

            if (debrisParticles != null)
            {
                debrisEmission = debrisParticles.emission;
            }

            if (strainParticles != null)
            {
                strainEmission = strainParticles.emission;
            }

            if (autoConfigureAirflow)
            {
                ConfigureAirflow();
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            smoothedIntensity = Mathf.Lerp(smoothedIntensity, controller.Intensity, blend);
            smoothedLoad = Mathf.Lerp(smoothedLoad, controller.Load, blend);

            UpdateParticles();
            UpdateNozzle();
            UpdateLight();
            UpdateAnimator();
        }

        public void ConfigureAirflow()
        {
            if (airflowParticles == null || controller == null)
            {
                return;
            }

            VacuumSettings settings = controller.Settings;
            VacuumNozzle nozzle = controller.Nozzle;
            if (settings == null)
            {
                return;
            }

            float range = settings.maxDistance;
            float radius = VacuumNozzle.ConeRadiusAt(range, Mathf.Max(settings.HorizontalHalfAngle, settings.VerticalHalfAngle));

            // Sit the emitter at the far end of the cone, looking back down the barrel.
            if (nozzle != null && airflowParticles.transform.parent != null)
            {
                Transform emitter = airflowParticles.transform;
                emitter.position = nozzle.Origin + nozzle.Direction * range;
                emitter.rotation = Quaternion.LookRotation(-nozzle.Direction, nozzle.Up);
            }

            ParticleSystem.MainModule main = airflowParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = airflowTravelTime;
            main.startSpeed = range / airflowTravelTime;
            main.startSize = Mathf.Max(0.05f, radius * 0.12f);

            ParticleSystem.ShapeModule shape = airflowParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.05f, radius);
            shape.radiusThickness = 1f;

            // A negative radial velocity pulls the stream onto the axis as it arrives, which is
            // what makes it read as a funnel instead of a curtain.
            ParticleSystem.VelocityOverLifetimeModule velocity = airflowParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(-radius / airflowTravelTime);

            ParticleSystem.SizeOverLifetimeModule size = airflowParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        private void UpdateParticles()
        {
            if (airflowParticles != null)
            {
                airflowEmission.rateOverTime = airflowRateAtFullPower * smoothedIntensity;
                SetPlaying(airflowParticles, smoothedIntensity > 0.01f);
            }

            if (debrisParticles != null)
            {
                float debrisScale = smoothedIntensity * (0.4f + 0.6f * smoothedLoad);
                debrisEmission.rateOverTime = debrisRateAtFullPower * debrisScale;
                SetPlaying(debrisParticles, debrisScale > 0.01f);
            }

            if (strainParticles != null)
            {
                strainEmission.rateOverTime = 60f * Mathf.Max(0f, smoothedLoad - 0.2f) * smoothedIntensity;
                SetPlaying(strainParticles, smoothedLoad > 0.2f && smoothedIntensity > 0.05f);
            }
        }

        private void UpdateNozzle()
        {
            if (nozzleVisual == null)
            {
                return;
            }

            float swell = 1f + nozzleSwell * smoothedLoad * smoothedIntensity;
            nozzleVisual.localScale = nozzleVisualBaseScale * swell;

            if (shakeAmplitude > 0f)
            {
                float time = Time.time * shakeFrequency;
                float amount = shakeAmplitude * smoothedLoad * smoothedIntensity;
                Vector3 offset = new Vector3(
                    (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f,
                    0f) * amount;

                nozzleVisual.localPosition = nozzleVisualBasePosition + offset;
            }
        }

        private void UpdateLight()
        {
            if (nozzleLight == null)
            {
                return;
            }

            nozzleLight.intensity = lightIntensityAtFullPower * smoothedIntensity * (0.7f + 0.3f * smoothedLoad);
            nozzleLight.enabled = smoothedIntensity > 0.02f;
        }

        private void UpdateAnimator()
        {
            if (nozzleAnimator == null || nozzleAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            nozzleAnimator.SetFloat(intensityParameterHash, smoothedIntensity);
            nozzleAnimator.SetFloat(loadParameterHash, smoothedLoad);
        }

        private static void SetPlaying(ParticleSystem system, bool shouldPlay)
        {
            if (shouldPlay)
            {
                if (!system.isPlaying)
                {
                    system.Play();
                }
            }
            else if (system.isPlaying)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
