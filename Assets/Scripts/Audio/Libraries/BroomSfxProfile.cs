// Broom sounds for equipping swinging and landing hits
using UnityEngine;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Broom Sfx", fileName = "BroomSfx", order = 21)]
    public class BroomSfxProfile : SfxLibrary
    {
        [SerializeField] private SoundEvent equip;

        [Tooltip("Every swing in the light and heavy strings.")]
        [SerializeField] private SoundEvent attack;

        [Tooltip("Played at the impact point of each enemy hit.")]
        [SerializeField] private SoundEvent hit;

        [Tooltip("Layered over the player dash when riding the broom like a flying broom.")]
        [SerializeField] private SoundEvent dashRide;

        [Tooltip("Layered over the player dash when sweeping forward standing on the broom.")]
        [SerializeField] private SoundEvent dashSweep;

        public SoundEvent Equip => equip;
        public SoundEvent Attack => attack;
        public SoundEvent Hit => hit;
        public SoundEvent DashRide => dashRide;
        public SoundEvent DashSweep => dashSweep;
    }
}
