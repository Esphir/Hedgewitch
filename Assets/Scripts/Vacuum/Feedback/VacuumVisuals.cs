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

        [Tooltip("1 moves particles at a constant speed; higher values start them slow and whip them into the mouth.")]
        [Range(1f, 3f)] [SerializeField] private float airflowAcceleration = 1.6f;

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

        private ParticleSystem.Particle[] particleBuffer;
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

            // Sit the emitter on the mouth, looking down the barrel, so its local space is the
            // cone's own space: +Z along the axis, X and Y across it.
            if (nozzle != null && airflowParticles.transform.parent != null)
            {
                Transform emitter = airflowParticles.transform;
                emitter.position = nozzle.Origin;
                emitter.rotation = nozzle.Rotation;
            }

            // Particles are placed by hand every frame (see ShapeAirflowToCone), so the built-in
            // shape and velocity modules must not move them.
            ParticleSystem.MainModule main = airflowParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = airflowTravelTime;
            main.startSpeed = 0f;
            main.startSize = Mathf.Max(0.05f, radius * 0.12f);
            main.maxParticles = Mathf.Max(main.maxParticles, Mathf.CeilToInt(airflowRateAtFullPower * airflowTravelTime * 1.5f));

            ParticleSystem.ShapeModule shape = airflowParticles.shape;
            shape.enabled = false;

            ParticleSystem.VelocityOverLifetimeModule velocity = airflowParticles.velocityOverLifetime;
            velocity.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = airflowParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        private void LateUpdate()
        {
            if (autoConfigureAirflow)
            {
                ShapeAirflowToCone();
            }
        }

        /// <summary>
        /// Places every airflow particle on a straight line from a point on the far end of the
        /// suction cone to the mouth. Each particle keeps its own spot on the cone's elliptical
        /// cross-section (derived from its random seed), so the stream fills the exact cone the
        /// detector uses, follows the nozzle as it aims, and converges on the mouth.
        /// </summary>
        private void ShapeAirflowToCone()
        {
            if (airflowParticles == null || controller == null || controller.Settings == null)
            {
                return;
            }

            int count = airflowParticles.particleCount;
            if (count == 0)
            {
                return;
            }

            if (particleBuffer == null || particleBuffer.Length < count)
            {
                particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
            }

            VacuumSettings settings = controller.Settings;
            float range = settings.maxDistance;
            float halfWidth = VacuumNozzle.ConeRadiusAt(range, settings.HorizontalHalfAngle);
            float halfHeight = VacuumNozzle.ConeRadiusAt(range, settings.VerticalHalfAngle);

            count = airflowParticles.GetParticles(particleBuffer, count);
            for (int i = 0; i < count; i++)
            {
                ref ParticleSystem.Particle particle = ref particleBuffer[i];

                uint seed = particle.randomSeed;
                float angle = Hash01(seed) * Mathf.PI * 2f;
                float spread = Mathf.Sqrt(Hash01(seed ^ 0x9E3779B9u)); // uniform over the disc

                float age = 1f - particle.remainingLifetime / Mathf.Max(0.0001f, particle.startLifetime);
                float travelled = Mathf.Pow(Mathf.Clamp01(age), airflowAcceleration);
                float along = 1f - travelled; // 1 at the far end of the cone, 0 at the mouth

                particle.position = new Vector3(
                    Mathf.Cos(angle) * spread * halfWidth * along,
                    Mathf.Sin(angle) * spread * halfHeight * along,
                    range * along);
                particle.velocity = Vector3.zero;
            }

            airflowParticles.SetParticles(particleBuffer, count);
        }

        private static float Hash01(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0xFFFFFFu) / 16777216f;
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
