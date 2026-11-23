// Safe reference to a playing sound that goes stale once its voice is reused
namespace Hedgewitch.Audio
{
    // Implemented by whatever voice a backend hands out
    public interface IAudioVoice
    {
        bool IsPlaying(int playId);

        void Stop(int playId, float fadeTime);
    }

    public readonly struct AudioHandle
    {
        private readonly IAudioVoice voice;
        private readonly int playId;

        public AudioHandle(IAudioVoice voice, int playId)
        {
            this.voice = voice;
            this.playId = playId;
        }

        // Returned when nothing played so callers never need null checks
        public static AudioHandle None => default;

        public bool IsPlaying => voice != null && voice.IsPlaying(playId);

        public void Stop(float fadeTime = 0f)
        {
            voice?.Stop(playId, fadeTime);
        }
    }
}
