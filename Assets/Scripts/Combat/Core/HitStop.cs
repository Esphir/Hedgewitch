using UnityEngine;

namespace Hedgewitch.Combat
{
    /// <summary>
    /// A few hundredths of a second of near-frozen time when a heavy blow lands. Small as it is,
    /// it is a large part of why a hit feels like it connected.
    /// </summary>
    public sealed class HitStop : MonoBehaviour
    {
        /// <summary>Global switch, so tests and tools can turn the effect off.</summary>
        public static bool Enabled = true;

        private const float FrozenScale = 0.05f;

        private static HitStop instance;

        private float remaining;
        private float restoreScale = 1f;

        public static bool IsActive => instance != null && instance.remaining > 0f;

        public static void Request(float seconds)
        {
            if (!Enabled || seconds <= 0f || !Application.isPlaying)
            {
                return;
            }

            if (instance == null)
            {
                GameObject host = new GameObject("[HitStop]");
                host.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(host);
                instance = host.AddComponent<HitStop>();
            }

            instance.Begin(seconds);
        }

        private void Begin(float seconds)
        {
            if (remaining <= 0f)
            {
                restoreScale = Time.timeScale;
            }

            remaining = Mathf.Max(remaining, seconds);
            Time.timeScale = Mathf.Min(restoreScale, FrozenScale);
        }

        private void Update()
        {
            if (remaining <= 0f)
            {
                return;
            }

            // Real time, since game time is what is being frozen.
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                Time.timeScale = restoreScale;
            }
        }

        private void OnDestroy()
        {
            if (remaining > 0f)
            {
                Time.timeScale = restoreScale;
            }

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
