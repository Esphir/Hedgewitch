// One sound the game can ask for with its clips and playback rules
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Sound Event", fileName = "Category_SoundName", order = 0)]
    public class SoundEvent : ScriptableObject
    {
        [Tooltip("Name a Wwise backend would post such as Gun_Shoot. Leave empty to use the asset name.")]
        [SerializeField] private string eventName = "";

        [Tooltip("One clip is picked at random each time. With two or more the last clip is never repeated.")]
        [SerializeField] private List<AudioClip> clips = new List<AudioClip>();

        [Tooltip("Mixer group this sound plays through.")]
        [SerializeField] private AudioMixerGroup output;

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        [Tooltip("Each play is quieter by a random amount up to this.")]
        [Range(0f, 1f)] [SerializeField] private float volumeVariation;

        [Header("Pitch")]
        [Tooltip("Fixed pitch shift in semitones. Reuse a clip higher or lower, such as Gun Empty being the reload click shifted up.")]
        [Range(-24f, 24f)] [SerializeField] private float pitchOffset;

        [Tooltip("Each play shifts pitch randomly up or down by up to this many semitones.")]
        [Range(0f, 12f)] [SerializeField] private float pitchVariation;

        [Header("Space")]
        [Tooltip("0 is flat 2D and 1 is fully positioned in the world.")]
        [Range(0f, 1f)] [SerializeField] private float spatialBlend = 1f;

        [Tooltip("Full volume inside this distance.")]
        [Min(0.01f)] [SerializeField] private float minDistance = 2f;

        [Tooltip("Silent beyond this distance.")]
        [Min(0.02f)] [SerializeField] private float maxDistance = 30f;

        [Header("Playback")]
        [Tooltip("Keep playing until stopped. Needed for running loops and music.")]
        [SerializeField] private bool loop;

        [Tooltip("Most copies of this sound that can play at once. The oldest stops when a new one starts.")]
        [Min(1)] [SerializeField] private int maxInstances = 4;

        [Tooltip("Seconds that must pass before this sound can start again. Stops machine gun stacking.")]
        [Min(0f)] [SerializeField] private float minRetriggerInterval = 0.03f;

        [NonSerialized] private int lastClipIndex = -1;

        public string EventName => string.IsNullOrEmpty(eventName) ? name : eventName;

        public IReadOnlyList<AudioClip> Clips => clips;

        public AudioMixerGroup Output => output;

        public float Volume => volume;

        public float SpatialBlend => spatialBlend;

        public float MinDistance => minDistance;

        public float MaxDistance => Mathf.Max(maxDistance, minDistance + 0.01f);

        public bool Loop => loop;

        public int MaxInstances => Mathf.Max(1, maxInstances);

        public float MinRetriggerInterval => minRetriggerInterval;

        public float PitchOffset => pitchOffset;

        public bool HasClips
        {
            get
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    if (clips[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Random pick that skips empty slots and never repeats the last clip
        public AudioClip PickClip()
        {
            int valid = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            bool skipLast = valid >= 2 && lastClipIndex >= 0 && lastClipIndex < clips.Count && clips[lastClipIndex] != null;
            int choice = Random.Range(0, skipLast ? valid - 1 : valid);

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] == null || (skipLast && i == lastClipIndex))
                {
                    continue;
                }

                if (choice == 0)
                {
                    lastClipIndex = i;
                    return clips[i];
                }

                choice--;
            }

            return null;
        }

        public float PickVolume()
        {
            return volume * (1f - Random.Range(0f, volumeVariation));
        }

        public float PickPitch()
        {
            float semitones = pitchOffset + Random.Range(-pitchVariation, pitchVariation);
            return Mathf.Pow(2f, semitones / 12f);
        }

#if UNITY_EDITOR
        // Lets editor setup and tests fill an event without the inspector
        public void EditorConfigure(AudioMixerGroup group, float blend, bool looping, int instances, float retrigger, float pitch = 0f)
        {
            output = group;
            spatialBlend = blend;
            loop = looping;
            maxInstances = instances;
            minRetriggerInterval = retrigger;
            pitchOffset = pitch;
        }

        public void EditorSetClips(params AudioClip[] newClips)
        {
            clips = new List<AudioClip>(newClips);
            lastClipIndex = -1;
        }
#endif

        private void OnValidate()
        {
            maxDistance = Mathf.Max(maxDistance, minDistance + 0.01f);
        }
    }
}
