// The transform the mix is centred on which is normally the player
using UnityEngine;

namespace Hedgewitch.Audio
{
    public static class AudioFocus
    {
        public static Transform Target { get; private set; }

        public static void Set(Transform target)
        {
            Target = target;
        }

        // Only clears when the caller still owns the focus
        public static void Clear(Transform target)
        {
            if (Target == target)
            {
                Target = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Target = null;
        }
    }
}
