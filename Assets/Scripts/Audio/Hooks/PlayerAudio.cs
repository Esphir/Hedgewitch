// Plays the witch's movement and health sounds and centres the mix on her
using UnityEngine;
using Hedgewitch.Combat;
using Hedgewitch.Vacuum.Demo;

namespace Hedgewitch.Audio.Hooks
{
    [DisallowMultipleComponent]
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private PlayerSfx sounds;

        [Tooltip("Metres walked per footstep. Only used until an animation event calls Footstep.")]
        [Min(0.1f)] [SerializeField] private float strideLength = 1.4f;

        private readonly FootstepTracker footsteps = new FootstepTracker();
        private IsometricPlayerController controller;
        private Health health;
        private PlayerDash dash;

        private void Awake()
        {
            controller = GetComponent<IsometricPlayerController>();
            health = GetComponent<Health>();
            dash = GetComponent<PlayerDash>();
        }

        private void OnEnable()
        {
            AudioFocus.Set(transform);
            footsteps.Reset(transform.position);

            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Defeated += HandleDefeated;
            }

            if (dash != null)
            {
                dash.Started += HandleDash;
            }
        }

        private void OnDisable()
        {
            AudioFocus.Clear(transform);

            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Defeated -= HandleDefeated;
            }

            if (dash != null)
            {
                dash.Started -= HandleDash;
            }
        }

        private void Update()
        {
            bool grounded = controller == null || controller.IsGrounded;
            if (footsteps.Step(transform.position, grounded, strideLength))
            {
                Play(sounds != null ? sounds.Footsteps : null);
            }
        }

        // Animation event name for walk and run clips
        public void Footstep()
        {
            footsteps.MarkAnimationDriven();
            Play(sounds != null ? sounds.Footsteps : null);
        }

        // Every dash style plays this and the broom adds its own layer
        public void PlayDash()
        {
            Play(sounds != null ? sounds.Dash : null);
        }

        // Ready for when an inventory screen exists
        public void PlayOpenInventory()
        {
            AudioManager.Play(sounds != null ? sounds.OpenInventory : null);
        }

        private void HandleDash(PlayerDash source, DashStyle style)
        {
            PlayDash();
        }

        private void HandleDamaged(DamageInfo info, DamageResult result)
        {
            if (!result.Defeated)
            {
                Play(sounds != null ? sounds.Hurt : null);
            }
        }

        private void HandleDefeated(DamageInfo info)
        {
            Play(sounds != null ? sounds.Death : null);
        }

        private void Play(SoundEvent sound)
        {
            AudioManager.Play(sound, transform.position);
        }
    }
}
