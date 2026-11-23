// Music tracks the MusicManager crossfades between
using UnityEngine;

namespace Hedgewitch.Audio
{
    [CreateAssetMenu(menuName = "Hedgewitch/Audio/Music Library", fileName = "MusicLibrary", order = 25)]
    public class MusicLibrary : SfxLibrary
    {
        [SerializeField] private SoundEvent titleTrack;

        [Tooltip("Plays in a level while no enemy is close.")]
        [SerializeField] private SoundEvent levelExplore;

        [Tooltip("Plays in a level while an enemy is within fight range.")]
        [SerializeField] private SoundEvent levelFight;

        public SoundEvent TitleTrack => titleTrack;
        public SoundEvent LevelExplore => levelExplore;
        public SoundEvent LevelFight => levelFight;
    }
}
