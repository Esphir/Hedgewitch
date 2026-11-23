// Plays one enemy's sounds from its profile and counts it as a fight music threat while alive
using UnityEngine;
using Hedgewitch.Combat;
using Hedgewitch.Vacuum;
using Hedgewitch.Vacuum.Enemies;

namespace Hedgewitch.Audio.Hooks
{
    [DisallowMultipleComponent]
    public class EnemyAudio : MonoBehaviour, IVacuumCaptureHandler
    {
        [Tooltip("Sounds for this enemy type. Make a new Enemy Sfx Profile asset for each new enemy.")]
        [SerializeField] private EnemySfxProfile profile;

        [Tooltip("Metres moved per footstep. Only used until an animation event calls Footstep.")]
        [Min(0.1f)] [SerializeField] private float strideLength = 1.1f;

        private readonly FootstepTracker footsteps = new FootstepTracker();
        private Health health;
        private BasicEnemyAgent agent;
        private bool dead;

        public EnemySfxProfile Profile => profile;

        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<BasicEnemyAgent>();
        }

        private void OnEnable()
        {
            footsteps.Reset(transform.position);

            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Defeated += HandleDefeated;
                dead = !health.IsAlive;
            }

            if (!dead)
            {
                MusicThreats.Register(transform);
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Defeated -= HandleDefeated;
            }

            MusicThreats.Unregister(transform);
        }

        private void Update()
        {
            if (dead)
            {
                return;
            }

            // Being dragged by the vacuum is not walking
            bool walking = agent == null
                || agent.CurrentMode == BasicEnemyAgent.AgentMode.Wander
                || agent.CurrentMode == BasicEnemyAgent.AgentMode.Chase;

            if (footsteps.Step(transform.position, walking, strideLength))
            {
                Play(profile != null ? profile.Footsteps : null);
            }
        }

        // Animation event name for walk clips
        public void Footstep()
        {
            footsteps.MarkAnimationDriven();
            Play(profile != null ? profile.Footsteps : null);
        }

        // Ready for when enemies can attack
        public void PlayAttack()
        {
            Play(profile != null ? profile.Attack : null);
        }

        private void HandleDamaged(DamageInfo info, DamageResult result)
        {
            if (!result.Defeated)
            {
                Play(profile != null ? profile.Hurt : null);
            }
        }

        private void HandleDefeated(DamageInfo info)
        {
            Die();
        }

        private void Die()
        {
            if (dead)
            {
                return;
            }

            dead = true;
            MusicThreats.Unregister(transform);
            Play(profile != null ? profile.Death : null);
        }

        private void Play(SoundEvent sound)
        {
            AudioManager.Play(sound, transform.position);
        }

        public bool AllowCapture(in VacuumCaptureContext context)
        {
            return true;
        }

        public void OnCaptureBegin(in VacuumCaptureContext context)
        {
        }

        // Swallowed by the vacuum counts as dying
        public void OnCaptureCompleted(in VacuumCaptureContext context)
        {
            Die();
        }

        public void OnCaptureCancelled(in VacuumCaptureContext context)
        {
        }
    }
}
