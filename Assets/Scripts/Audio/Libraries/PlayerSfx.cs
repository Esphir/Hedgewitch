// Sounds made by the witch herself
using UnityEngine;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Player Sfx", fileName = "PlayerSfx", order = 20)]
    public class PlayerSfx : SfxLibrary
    {
        [SerializeField] private SoundEvent footsteps;
        [SerializeField] private SoundEvent dash;
        [SerializeField] private SoundEvent openInventory;
        [SerializeField] private SoundEvent hurt;
        [SerializeField] private SoundEvent death;

        public SoundEvent Footsteps => footsteps;
        public SoundEvent Dash => dash;
        public SoundEvent OpenInventory => openInventory;
        public SoundEvent Hurt => hurt;
        public SoundEvent Death => death;
    }
}
