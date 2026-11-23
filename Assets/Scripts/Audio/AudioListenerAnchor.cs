// Keeps the one listener on the player with the camera yaw so 3D sounds pan with the isometric view
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hedgewitch.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public class AudioListenerAnchor : MonoBehaviour
    {
        [Tooltip("Height above the player the listener sits at.")]
        [SerializeField] private float heightOffset = 1.5f;

        [Tooltip("Turn off other listeners such as the one on the main camera so only this one hears.")]
        [SerializeField] private bool disableOtherListeners = true;

        private AudioListener listener;

        private void Awake()
        {
            listener = GetComponent<AudioListener>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            DisableOthers();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            Transform target = AudioFocus.Target;

            if (target != null)
            {
                transform.position = target.position + Vector3.up * heightOffset;
            }
            else if (view != null)
            {
                transform.position = view.transform.position;
            }

            if (view != null)
            {
                Vector3 forward = view.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 1e-4f)
                {
                    transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DisableOthers();
        }

        private void DisableOthers()
        {
            if (!disableOtherListeners)
            {
                return;
            }

            AudioListener[] found = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != listener)
                {
                    found[i].enabled = false;
                }
            }
        }
    }
}
