// Linear 0 to 1 volume sliders saved to PlayerPrefs and sent to the mixer in decibels
using UnityEngine;

namespace Hedgewitch.Audio
{
    // Matches the mixer groups and their exposed parameters named like MasterVolume
    public enum AudioBus
    {
        Master,
        Music,
        SFX,
        Player,
        Weapons,
        Enemies,
        UI,
        VO
    }

    public static class AudioVolume
    {
        public const float MinDecibels = -80f;
        private const string PrefsPrefix = "Hedgewitch.Audio.Volume.";

        public static string ParameterName(AudioBus bus)
        {
            return bus + "Volume";
        }

        public static float LinearToDecibels(float linear)
        {
            linear = Mathf.Clamp01(linear);
            return linear <= 0.0001f ? MinDecibels : Mathf.Max(MinDecibels, 20f * Mathf.Log10(linear));
        }

        public static float DecibelsToLinear(float decibels)
        {
            return decibels <= MinDecibels ? 0f : Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }

        public static float Get(AudioBus bus)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsPrefix + bus, 1f));
        }

        // Safe to call every frame from a slider
        public static void Set(AudioBus bus, float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(PrefsPrefix + bus, linear);
            AudioManager.Backend?.SetBusVolume(bus, linear);
        }

        // Writes to disk for when a settings menu closes
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static void ApplyAll()
        {
            IAudioBackend backend = AudioManager.Backend;
            if (backend == null)
            {
                return;
            }

            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
            {
                backend.SetBusVolume(bus, Get(bus));
            }
        }

        public static void ResetAll()
        {
            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
            {
                PlayerPrefs.DeleteKey(PrefsPrefix + bus);
            }

            ApplyAll();
        }
    }
}
