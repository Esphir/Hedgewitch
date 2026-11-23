// Enemies that can trigger fight music while they are alive
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Audio
{
    public static class MusicThreats
    {
        private static readonly List<Transform> threats = new List<Transform>();

        public static int Count => threats.Count;

        public static void Register(Transform threat)
        {
            if (threat != null && !threats.Contains(threat))
            {
                threats.Add(threat);
            }
        }

        public static void Unregister(Transform threat)
        {
            threats.Remove(threat);
        }

        public static bool AnyWithin(Vector3 point, float range)
        {
            float rangeSquared = range * range;
            for (int i = threats.Count - 1; i >= 0; i--)
            {
                Transform threat = threats[i];
                if (threat == null)
                {
                    threats.RemoveAt(i);
                    continue;
                }

                if ((threat.position - point).sqrMagnitude <= rangeSquared)
                {
                    return true;
                }
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            threats.Clear();
        }
    }
}
