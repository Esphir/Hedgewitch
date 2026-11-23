// Logs each empty sound event once so unfinished hooks never spam the console
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Audio
{
    public static class AudioWarnings
    {
        private static readonly HashSet<SoundEvent> warned = new HashSet<SoundEvent>();

        // True the first time an event is reported
        public static bool WarnEmptyOnce(SoundEvent sound)
        {
            if (sound == null || !warned.Add(sound))
            {
                return false;
            }

            Debug.LogWarning($"[Audio] {sound.EventName} has no clips assigned so it plays nothing. Drag clips into it or run Tools > Hedgewitch > Audio Validator.", sound);
            return true;
        }

        public static void Reset()
        {
            warned.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            warned.Clear();
        }
    }
}
