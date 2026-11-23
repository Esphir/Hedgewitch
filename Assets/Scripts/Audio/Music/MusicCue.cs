// Drop one in a scene to pick its music such as Title on the title screen and Level in a level
using UnityEngine;

namespace Hedgewitch.Audio
{
    [DisallowMultipleComponent]
    public class MusicCue : MonoBehaviour
    {
        [SerializeField] private MusicState music = MusicState.Level;

        private void Start()
        {
            MusicManager.SetState(music);
        }
    }
}
