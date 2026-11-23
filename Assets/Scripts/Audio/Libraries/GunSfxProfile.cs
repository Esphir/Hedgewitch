// Musket sounds for equipping shooting reloading and dry fire
using UnityEngine;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Gun Sfx", fileName = "GunSfx", order = 22)]
    public class GunSfxProfile : SfxLibrary
    {
        [SerializeField] private SoundEvent equip;

        [Tooltip("Played when the reload finishes and the gun is ready again.")]
        [SerializeField] private SoundEvent reload;

        [SerializeField] private SoundEvent shoot;

        [Tooltip("Trigger pulled while still reloading. Can reuse the reload clip with a higher pitch offset.")]
        [SerializeField] private SoundEvent empty;

        public SoundEvent Equip => equip;
        public SoundEvent Reload => reload;
        public SoundEvent Shoot => shoot;
        public SoundEvent Empty => empty;
    }
}
