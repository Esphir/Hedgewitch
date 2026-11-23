// Default backend that plays sound events through a pool of AudioSources with per event voice limits
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Hedgewitch.Audio
{
    public class UnityAudioBackend : IAudioBackend, ITickableAudioBackend
    {
        private sealed class Voice : IAudioVoice
        {
            public AudioSource Source;
            public SoundEvent Sound;
            public Transform Follow;
            public bool Following;
            public bool Active;
            public int PlayId;
            public int StartFrame;
            public float StartTime;
            public float BaseVolume;
            public float FadeDuration = -1f;
            public float FadeElapsed;

            public bool Fading => FadeDuration >= 0f;

            public bool IsPlaying(int playId)
            {
                return Active && playId == PlayId;
            }

            public void Stop(int playId, float fadeTime)
            {
                if (!IsPlaying(playId))
                {
                    return;
                }

                if (fadeTime <= 0f)
                {
                    Release();
                    return;
                }

                // A shorter fade can cut an existing one short but never lengthen it
                float remaining = Fading ? FadeDuration - FadeElapsed : float.MaxValue;
                if (fadeTime < remaining)
                {
                    BaseVolume = Source != null ? Source.volume : BaseVolume;
                    FadeDuration = fadeTime;
                    FadeElapsed = 0f;
                }
            }

            public void Release()
            {
                if (Source != null)
                {
                    Source.Stop();
                    Source.clip = null;
                }

                Active = false;
                Sound = null;
                Follow = null;
                Following = false;
                FadeDuration = -1f;
            }
        }

        private const float LostFollowFade = 0.15f;

        private readonly Transform root;
        private readonly AudioMixer mixer;
        private readonly int maxVoices;
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<SoundEvent, float> lastStartTimes = new Dictionary<SoundEvent, float>();
        private readonly HashSet<AudioBus> warnedBuses = new HashSet<AudioBus>();
        private bool warnedNoMixer;
        private int playCounter;

        public UnityAudioBackend(Transform root, int initialVoices, int maxVoices, AudioMixer mixer = null)
        {
            this.root = root;
            this.mixer = mixer;
            this.maxVoices = Mathf.Max(1, maxVoices);

            int warm = Mathf.Clamp(initialVoices, 0, this.maxVoices);
            for (int i = 0; i < warm; i++)
            {
                CreateVoice();
            }
        }

        public int PoolSize => voices.Count;

        public int ActiveVoiceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < voices.Count; i++)
                {
                    if (voices[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int CountActive(SoundEvent sound)
        {
            int count = 0;
            for (int i = 0; i < voices.Count; i++)
            {
                if (voices[i].Active && voices[i].Sound == sound)
                {
                    count++;
                }
            }

            return count;
        }

        public AudioHandle Play(SoundEvent sound, Vector3? position)
        {
            return sound != null ? Start(sound, position, null, sound.Loop) : AudioHandle.None;
        }

        public AudioHandle PlayLoop(SoundEvent sound, Transform follow)
        {
            Vector3? position = follow != null ? follow.position : (Vector3?)null;
            return Start(sound, position, follow, true);
        }

        public void StopAll(float fadeTime)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                if (voices[i].Active)
                {
                    voices[i].Stop(voices[i].PlayId, fadeTime);
                }
            }
        }

        public void SetBusVolume(AudioBus bus, float linear)
        {
            if (mixer == null)
            {
                if (!warnedNoMixer)
                {
                    warnedNoMixer = true;
                    Debug.LogWarning("[Audio] AudioManager has no mixer assigned so volume settings do nothing.");
                }

                return;
            }

            string parameter = AudioVolume.ParameterName(bus);
            if (!mixer.SetFloat(parameter, AudioVolume.LinearToDecibels(linear)) && warnedBuses.Add(bus))
            {
                Debug.LogWarning($"[Audio] Mixer {mixer.name} has no exposed parameter called {parameter}.", mixer);
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (!voice.Active)
                {
                    continue;
                }

                if (voice.Source == null)
                {
                    voice.Release();
                    continue;
                }

                if (voice.Following)
                {
                    if (voice.Follow != null)
                    {
                        voice.Source.transform.position = voice.Follow.position;
                    }
                    else
                    {
                        // Owner was destroyed so loops fade instead of playing forever
                        voice.Following = false;
                        if (voice.Source.loop)
                        {
                            voice.Stop(voice.PlayId, LostFollowFade);
                        }
                    }
                }

                if (voice.Fading)
                {
                    voice.FadeElapsed += unscaledDeltaTime;
                    float t = voice.FadeDuration > 0f ? voice.FadeElapsed / voice.FadeDuration : 1f;
                    if (t >= 1f)
                    {
                        voice.Release();
                        continue;
                    }

                    voice.Source.volume = voice.BaseVolume * (1f - t);
                }

                // Give a clip one frame to start before treating it as finished
                if (!voice.Source.isPlaying && !AudioListener.pause && Time.frameCount > voice.StartFrame)
                {
                    voice.Release();
                }
            }
        }

        private AudioHandle Start(SoundEvent sound, Vector3? position, Transform follow, bool loop)
        {
            if (sound == null)
            {
                return AudioHandle.None;
            }

            if (!sound.HasClips)
            {
                AudioWarnings.WarnEmptyOnce(sound);
                return AudioHandle.None;
            }

            float now = Time.unscaledTime;
            if (sound.MinRetriggerInterval > 0f
                && lastStartTimes.TryGetValue(sound, out float last)
                && now - last < sound.MinRetriggerInterval)
            {
                return AudioHandle.None;
            }

            LimitInstances(sound);

            Voice voice = TakeVoice();
            AudioSource source = voice.Source;

            source.clip = sound.PickClip();
            source.outputAudioMixerGroup = sound.Output;
            source.loop = loop;
            source.pitch = sound.PickPitch();
            source.spatialBlend = position.HasValue ? sound.SpatialBlend : 0f;
            source.minDistance = sound.MinDistance;
            source.maxDistance = sound.MaxDistance;
            source.transform.position = position ?? root.position;

            voice.Sound = sound;
            voice.Follow = follow;
            voice.Following = follow != null;
            voice.BaseVolume = sound.PickVolume();
            voice.FadeDuration = -1f;
            voice.FadeElapsed = 0f;
            voice.StartTime = now;
            voice.StartFrame = Time.frameCount;
            voice.PlayId = ++playCounter;
            voice.Active = true;

            source.volume = voice.BaseVolume;
            source.Play();

            lastStartTimes[sound] = now;
            return new AudioHandle(voice, voice.PlayId);
        }

        // Stops the oldest copies so a new one fits under the limit
        private void LimitInstances(SoundEvent sound)
        {
            while (CountActive(sound) >= sound.MaxInstances)
            {
                Voice oldest = null;
                for (int i = 0; i < voices.Count; i++)
                {
                    Voice voice = voices[i];
                    if (!voice.Active || voice.Sound != sound)
                    {
                        continue;
                    }

                    if (oldest == null || IsBetterSteal(voice, oldest))
                    {
                        oldest = voice;
                    }
                }

                if (oldest == null)
                {
                    return;
                }

                oldest.Release();
            }
        }

        private Voice TakeVoice()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                if (!voices[i].Active && voices[i].Source != null)
                {
                    return voices[i];
                }
            }

            if (voices.Count < maxVoices)
            {
                return CreateVoice();
            }

            // Pool is full so steal the oldest one shot before touching loops
            Voice steal = null;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.Source == null)
                {
                    continue;
                }

                bool voiceIsLoop = voice.Source.loop;
                bool stealIsLoop = steal != null && steal.Source.loop;
                if (steal == null || (stealIsLoop && !voiceIsLoop) || (stealIsLoop == voiceIsLoop && IsBetterSteal(voice, steal)))
                {
                    steal = voice;
                }
            }

            if (steal == null)
            {
                voices.RemoveAll(v => v.Source == null);
                return CreateVoice();
            }

            steal.Release();
            return steal;
        }

        // Fading voices go first then the oldest
        private static bool IsBetterSteal(Voice candidate, Voice current)
        {
            if (candidate.Fading != current.Fading)
            {
                return candidate.Fading;
            }

            return candidate.StartTime < current.StartTime;
        }

        private Voice CreateVoice()
        {
            GameObject host = new GameObject($"Voice {voices.Count}");
            host.transform.SetParent(root, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;

            Voice voice = new Voice { Source = source };
            voices.Add(voice);
            return voice;
        }
    }
}
