// Plays a library sound or sound event with no code such as ambience a door or a UI button
using UnityEngine;

namespace Hedgewitch.Audio
{
    [AddComponentMenu("Hedgewitch/Audio/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        public enum PlayTrigger
        {
            Manual,
            Start,
            Enable,
            TriggerEnter,
            CollisionEnter
        }

        [Tooltip("Library to pick the sound from.")]
        [SerializeField] private SfxLibrary library;

        [Tooltip("Name of the sound in the library.")]
        [SerializeField] private string slot = "";

        [Tooltip("Plays this sound event directly and ignores the library.")]
        [SerializeField] private SoundEvent sound;

        [Tooltip("What starts the sound. Manual waits for Play to be called from a UI button or animation event.")]
        [SerializeField] private PlayTrigger playOn = PlayTrigger.Start;

        [Tooltip("For trigger and collision starts. Only objects with this tag count. Leave empty for anything.")]
        [SerializeField] private string requiredTag = "";

        [Tooltip("Looping sounds move with this object.")]
        [SerializeField] private bool followThisObject = true;

        [Tooltip("Seconds a looping sound takes to fade when stopped or when this object is turned off.")]
        [Min(0f)] [SerializeField] private float fadeOut = 0.3f;

        private AudioHandle loopHandle;
        private bool warnedMissingSlot;

        public SoundEvent Resolve()
        {
            if (sound != null)
            {
                return sound;
            }

            return library != null ? library.Find(slot) : null;
        }

        // Hook this up to a UI button or an animation event
        public void Play()
        {
            SoundEvent resolved = Resolve();
            if (resolved == null)
            {
                WarnMissingSlot();
                return;
            }

            if (resolved.Loop)
            {
                if (!loopHandle.IsPlaying)
                {
                    loopHandle = AudioManager.PlayLoop(resolved, followThisObject ? transform : null);
                }

                return;
            }

            AudioManager.Play(resolved, transform.position);
        }

        public void Stop()
        {
            loopHandle.Stop(fadeOut);
            loopHandle = AudioHandle.None;
        }

#if UNITY_EDITOR
        public void EditorConfigure(SfxLibrary fromLibrary, string slotName, PlayTrigger trigger)
        {
            library = fromLibrary;
            slot = slotName;
            playOn = trigger;
        }
#endif

        private void Start()
        {
            if (playOn == PlayTrigger.Start)
            {
                Play();
            }
        }

        private void OnEnable()
        {
            if (playOn == PlayTrigger.Enable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (playOn == PlayTrigger.TriggerEnter && Matches(other.gameObject))
            {
                Play();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (playOn == PlayTrigger.CollisionEnter && Matches(collision.gameObject))
            {
                Play();
            }
        }

        private bool Matches(GameObject other)
        {
            return string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);
        }

        private void WarnMissingSlot()
        {
            // An existing slot that is just empty is the validator's job
            if (warnedMissingSlot || library == null || library.HasSlot(slot))
            {
                return;
            }

            warnedMissingSlot = true;
            Debug.LogWarning($"[Audio] {name} asks for {slot} but {library.name} has no sound with that name.", this);
        }
    }
}
