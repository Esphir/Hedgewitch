// Persistent owner of the audio backend and the one entry point gameplay calls
using UnityEngine;

namespace Hedgewitch.Audio
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        [Tooltip("AudioSources made up front.")]
        [Min(0)] [SerializeField] private int initialVoices = 16;

        [Tooltip("Most sounds that can play at once across the whole game.")]
        [Min(1)] [SerializeField] private int maxVoices = 48;

        private static AudioManager instance;
        private static bool quitting;
        private static bool warnedAutoCreate;

        private IAudioBackend backend;

        public static bool HasInstance => instance != null;

        public static IAudioBackend Backend
        {
            get
            {
                AudioManager manager = GetOrCreate();
                return manager != null ? manager.backend : null;
            }
        }

        public static AudioHandle Play(SoundEvent sound, Vector3? position = null)
        {
            if (sound == null)
            {
                return AudioHandle.None;
            }

            IAudioBackend active = Backend;
            return active != null ? active.Play(sound, position) : AudioHandle.None;
        }

        public static AudioHandle PlayLoop(SoundEvent sound, Transform follow)
        {
            if (sound == null)
            {
                return AudioHandle.None;
            }

            IAudioBackend active = Backend;
            return active != null ? active.PlayLoop(sound, follow) : AudioHandle.None;
        }

        public static void StopAll(float fadeTime = 0f)
        {
            if (instance != null)
            {
                instance.backend?.StopAll(fadeTime);
            }
        }

        // Swap in another backend such as Wwise without touching gameplay code
        public static void SetBackend(IAudioBackend replacement)
        {
            AudioManager manager = GetOrCreate();
            if (manager == null || replacement == null)
            {
                return;
            }

            manager.backend?.StopAll(0f);
            manager.backend = replacement;
        }

        private static AudioManager GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            if (quitting || !Application.isPlaying)
            {
                return null;
            }

            if (!warnedAutoCreate)
            {
                warnedAutoCreate = true;
                Debug.Log("[Audio] No AudioSystem in the scene so a default AudioManager was made. Add the AudioSystem prefab to use mixer volumes and music.");
            }

            new GameObject("AudioManager (auto)").AddComponent<AudioManager>();
            return instance;
        }

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                // Deactivate first so siblings like the listener anchor never wake up
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            backend = new UnityAudioBackend(transform, initialVoices, maxVoices);
        }

        private void LateUpdate()
        {
            if (backend is ITickableAudioBackend tickable)
            {
                tickable.Tick(Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            quitting = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            quitting = false;
            warnedAutoCreate = false;
        }
    }
}
