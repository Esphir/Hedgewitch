// Brief near freeze of time when a heavy blow lands
using UnityEngine;

namespace Hedgewitch.Combat
{
    public sealed class HitStop : MonoBehaviour
    {
        // Lets tests and tools turn it off
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

            // Real time because game time is frozen
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
