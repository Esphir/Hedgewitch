// Everything gameplay audio goes through so a Wwise backend can replace the Unity one
using UnityEngine;

namespace Hedgewitch.Audio
{
    public interface IAudioBackend
    {
        // No position plays flat 2D
        AudioHandle Play(SoundEvent sound, Vector3? position);

        // Loops until the handle is stopped or follow is destroyed
        AudioHandle PlayLoop(SoundEvent sound, Transform follow);

        void StopAll(float fadeTime);
    }

    // Backends that need a per frame update from AudioManager
    public interface ITickableAudioBackend
    {
        void Tick(float unscaledDeltaTime);
    }
}
