// Vacuum sounds and the timing of its start and stop sequence
using UnityEngine;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Vacuum Sfx", fileName = "VacuumSfx", order = 23)]
    public class VacuumSfxProfile : SfxLibrary
    {
        [SerializeField] private SoundEvent switchClick;
        [SerializeField] private SoundEvent powerOn;

        [Tooltip("Must have Loop ticked. Runs while the vacuum is sucking.")]
        [SerializeField] private SoundEvent runningLoop;

        [Tooltip("Played once per object swallowed.")]
        [SerializeField] private SoundEvent suckUpObject;

        [SerializeField] private SoundEvent powerOff;

        [Header("Start timing")]
        [Tooltip("Seconds after the switch click before power on plays.")]
        [Min(0f)] [SerializeField] private float powerOnDelay = 0.06f;

        [Tooltip("Seconds after the switch click before the running loop starts.")]
        [Min(0f)] [SerializeField] private float loopStartDelay = 0.35f;

        [Header("Stop timing")]
        [Tooltip("Seconds after the switch click before power off plays.")]
        [Min(0f)] [SerializeField] private float powerOffDelay = 0.04f;

        [Tooltip("Seconds after the switch click before the running loop starts fading.")]
        [Min(0f)] [SerializeField] private float loopStopDelay = 0.08f;

        [Tooltip("How long the running loop takes to fade out.")]
        [Min(0f)] [SerializeField] private float loopFadeOut = 0.3f;

        public SoundEvent SwitchClick => switchClick;
        public SoundEvent PowerOn => powerOn;
        public SoundEvent RunningLoop => runningLoop;
        public SoundEvent SuckUpObject => suckUpObject;
        public SoundEvent PowerOff => powerOff;
        public float PowerOnDelay => powerOnDelay;
        public float LoopStartDelay => loopStartDelay;
        public float PowerOffDelay => powerOffDelay;
        public float LoopStopDelay => loopStopDelay;
        public float LoopFadeOut => loopFadeOut;
    }
}
