// Sounds for one enemy type and assigned on that enemy so new types only need a new asset
using UnityEngine;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Enemy Sfx Profile", fileName = "EnemySfx_NewEnemy", order = 24)]
    public class EnemySfxProfile : SfxLibrary
    {
        [SerializeField] private SoundEvent footsteps;
        [SerializeField] private SoundEvent attack;
        [SerializeField] private SoundEvent hurt;

        [Tooltip("Also played when the enemy is swallowed by the vacuum.")]
        [SerializeField] private SoundEvent death;

        public SoundEvent Footsteps => footsteps;
        public SoundEvent Attack => attack;
        public SoundEvent Hurt => hurt;
        public SoundEvent Death => death;
    }
}
