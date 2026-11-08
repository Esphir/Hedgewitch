using System;
using UnityEngine;

namespace Hedgewitch.Vacuum.Feedback
{
    /// <summary>
    /// Drives the vacuum's sound from the controller's own state: the motor spools up and down with
    /// intensity, drops in pitch and gains a strain layer as heavy objects load it, and fires a
    /// one-shot when something is swallowed.
    /// </summary>
    [DisallowMultipleComponent]
    public class VacuumAudio : MonoBehaviour
    {
        // Unity's serializer assigns these, which the C# compiler cannot see.
#pragma warning disable CS0649
        [Serializable]
        private struct CategorySound
        {
            public VacuumCategory category;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }
#pragma warning restore CS0649

        [Header("Wiring")]
        [SerializeField] private VacuumController controller;

        [Tooltip("Looping motor sound. Created automatically if left empty and a clip is supplied.")]
        [SerializeField] private AudioSource loopSource;

        [Tooltip("Second looping layer that fades in as the motor strains against heavy objects.")]
        [SerializeField] private AudioSource strainSource;

        [SerializeField] private AudioSource oneShotSource;

        [Header("Clips")]
        [SerializeField] private AudioClip loopClip;
        [SerializeField] private AudioClip strainClip;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip stopClip;
        [SerializeField] private AudioClip defaultCaptureClip;
        [SerializeField] private CategorySound[] captureClips = Array.Empty<CategorySound>();

        [Header("Motor curve")]
        [Range(0f, 1f)] [SerializeField] private float loopVolume = 0.7f;
        [SerializeField] private float idlePitch = 0.6f;
        [SerializeField] private float runPitch = 1.05f;

        [Tooltip("How far the pitch sags when the motor is fully loaded.")]
        [SerializeField] private float loadPitchDrop = 0.25f;

        [Range(0f, 1f)] [SerializeField] private float strainVolume = 0.6f;

        [Tooltip("Load below which the strain layer stays silent.")]
        [Range(0f, 1f)] [SerializeField] private float strainThreshold = 0.15f;

        [Min(0.01f)] [SerializeField] private float smoothing = 12f;

        private float smoothedIntensity;
        private float smoothedLoad;
        private bool subscribed;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<VacuumController>();
            }

            EnsureSources();
        }

        private void OnEnable()
        {
            if (controller == null || subscribed)
            {
                return;
            }

            controller.StateChanged += HandleStateChanged;
            controller.ObjectCaptured += HandleObjectCaptured;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (controller == null || !subscribed)
            {
                return;
            }

            controller.StateChanged -= HandleStateChanged;
            controller.ObjectCaptured -= HandleObjectCaptured;
            subscribed = false;

            if (loopSource != null && loopSource.isPlaying)
            {
                loopSource.Stop();
            }

            if (strainSource != null && strainSource.isPlaying)
            {
                strainSource.Stop();
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

            if (loopSource != null && loopSource.clip != null)
            {
                bool shouldPlay = smoothedIntensity > 0.005f;
                if (shouldPlay && !loopSource.isPlaying)
                {
                    loopSource.Play();
                }
                else if (!shouldPlay && loopSource.isPlaying)
                {
                    loopSource.Stop();
                }

                loopSource.volume = loopVolume * smoothedIntensity;
                loopSource.pitch = Mathf.Lerp(idlePitch, runPitch, smoothedIntensity) - loadPitchDrop * smoothedLoad;
            }

            if (strainSource != null && strainSource.clip != null)
            {
                float strain = Mathf.InverseLerp(strainThreshold, 1f, smoothedLoad);
                bool shouldPlay = strain > 0.01f && smoothedIntensity > 0.05f;

                if (shouldPlay && !strainSource.isPlaying)
                {
                    strainSource.Play();
                }
                else if (!shouldPlay && strainSource.isPlaying)
                {
                    strainSource.Stop();
                }

                strainSource.volume = strainVolume * strain * smoothedIntensity;
                strainSource.pitch = Mathf.Lerp(0.85f, 1.1f, strain);
            }
        }

        private void EnsureSources()
        {
            if (loopSource == null && loopClip != null)
            {
                loopSource = CreateSource("Vacuum Loop", loopClip, true);
            }
            else if (loopSource != null && loopSource.clip == null)
            {
                loopSource.clip = loopClip;
                loopSource.loop = true;
            }

            if (strainSource == null && strainClip != null)
            {
                strainSource = CreateSource("Vacuum Strain", strainClip, true);
            }
            else if (strainSource != null && strainSource.clip == null)
            {
                strainSource.clip = strainClip;
                strainSource.loop = true;
            }

            if (oneShotSource == null)
            {
                oneShotSource = CreateSource("Vacuum One Shots", null, false);
            }
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip, bool loop)
        {
            GameObject host = new GameObject(sourceName);
            host.transform.SetParent(transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.volume = 0f;
            return source;
        }

        private void HandleStateChanged(VacuumState state)
        {
            AudioClip clip = state == VacuumState.Sucking ? startClip : stopClip;
            if (clip != null && oneShotSource != null)
            {
                oneShotSource.PlayOneShot(clip, 0.9f);
            }
        }

        private void HandleObjectCaptured(IVacuumAffectable affectable)
        {
            if (oneShotSource == null)
            {
                return;
            }

            VacuumCategory category = affectable != null ? affectable.Category : VacuumCategory.None;

            for (int i = 0; i < captureClips.Length; i++)
            {
                if ((captureClips[i].category & category) == 0 || captureClips[i].clip == null)
                {
                    continue;
                }

                oneShotSource.PlayOneShot(captureClips[i].clip, Mathf.Max(0.05f, captureClips[i].volume));
                return;
            }

            if (defaultCaptureClip != null)
            {
                oneShotSource.PlayOneShot(defaultCaptureClip, 0.8f);
            }
        }
    }
}
