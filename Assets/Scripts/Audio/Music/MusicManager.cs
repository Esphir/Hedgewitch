// Crossfades title explore and fight music and moves to fight while an enemy is near the player
using UnityEngine;

namespace Hedgewitch.Audio
{
    public enum MusicState
    {
        None,
        Title,
        Level
    }

    [DisallowMultipleComponent]
    public class MusicManager : MonoBehaviour
    {
        [SerializeField] private MusicLibrary library;

        [Header("Crossfade")]
        [Tooltip("Seconds every track change takes to crossfade.")]
        [Min(0f)] [SerializeField] private float fadeTime = 1.5f;

        [Header("Fight music")]
        [Tooltip("Fight music starts when an alive enemy is this close to the player.")]
        [Min(0f)] [SerializeField] private float fightRange = 10f;

        [Tooltip("Seconds with no enemy in range before going back to explore music. Stops flipping at the edge of the range.")]
        [Min(0f)] [SerializeField] private float leaveFightDelay = 4f;

        [Tooltip("Seconds between enemy distance checks.")]
        [Min(0.02f)] [SerializeField] private float checkInterval = 0.2f;

        [Header("Sync")]
        [Tooltip("Start the incoming explore or fight track at the same point as the outgoing one. Use when both share tempo and length.")]
        [SerializeField] private bool syncExploreAndFight;

        private static MusicManager instance;
        private static bool warnedMissing;

        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly SoundEvent[] playing = new SoundEvent[2];
        private readonly float[] gains = new float[2];
        private readonly float[] targets = new float[2];
        private readonly float[] trackVolumes = new float[2];
        private int active;
        private MusicState state;
        private bool inFight;
        private float lastThreatTime = float.NegativeInfinity;
        private float nextCheckTime;

        public static MusicManager Instance => instance;

        public MusicState State => state;

        public bool InFight => inFight;

        public SoundEvent CurrentTrack => sources[active] != null && sources[active].isPlaying ? playing[active] : null;

        public int CurrentTimeSamples => sources[active] != null ? sources[active].timeSamples : 0;

        public static void SetState(MusicState next)
        {
            if (instance != null)
            {
                instance.Play(next);
                return;
            }

            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("[Audio] No MusicManager in the scene so music is off. Add the AudioSystem prefab.");
            }
        }

        public void Play(MusicState next)
        {
            state = next;

            switch (next)
            {
                case MusicState.Title:
                    inFight = false;
                    CrossfadeTo(library != null ? library.TitleTrack : null, false);
                    break;

                case MusicState.Level:
                    inFight = ThreatInRange();
                    lastThreatTime = inFight ? Time.unscaledTime : float.NegativeInfinity;
                    nextCheckTime = Time.unscaledTime + checkInterval;
                    CrossfadeTo(LevelTrack(), false);
                    break;

                default:
                    inFight = false;
                    FadeOutAll();
                    break;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(MusicLibrary tracks, float fade, float range, float leaveDelay, float interval, bool sync)
        {
            library = tracks;
            fadeTime = fade;
            fightRange = range;
            leaveFightDelay = leaveDelay;
            checkInterval = interval;
            syncExploreAndFight = sync;
        }
#endif

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            for (int i = 0; i < sources.Length; i++)
            {
                GameObject host = new GameObject(i == 0 ? "Music A" : "Music B");
                host.transform.SetParent(transform, false);

                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.priority = 0;
                source.volume = 0f;
                sources[i] = source;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (state == MusicState.Level && now >= nextCheckTime)
            {
                nextCheckTime = now + checkInterval;
                UpdateFight(now);
            }

            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                gains[i] = Mathf.MoveTowards(gains[i], targets[i], step);
                source.volume = gains[i] * trackVolumes[i];

                if (gains[i] <= 0f && targets[i] <= 0f && source.isPlaying)
                {
                    source.Stop();
                    playing[i] = null;
                }
            }
        }

        private void UpdateFight(float now)
        {
            if (ThreatInRange())
            {
                lastThreatTime = now;
                if (!inFight)
                {
                    inFight = true;
                    CrossfadeTo(LevelTrack(), syncExploreAndFight);
                }
            }
            else if (inFight && now - lastThreatTime >= leaveFightDelay)
            {
                inFight = false;
                CrossfadeTo(LevelTrack(), syncExploreAndFight);
            }
        }

        private bool ThreatInRange()
        {
            Transform focus = AudioFocus.Target;
            return focus != null && MusicThreats.AnyWithin(focus.position, fightRange);
        }

        private SoundEvent LevelTrack()
        {
            if (library == null)
            {
                return null;
            }

            return inFight ? library.LevelFight : library.LevelExplore;
        }

        private void CrossfadeTo(SoundEvent track, bool sync)
        {
            if (track == null || !track.HasClips)
            {
                AudioWarnings.WarnEmptyOnce(track);
                FadeOutAll();
                return;
            }

            int current = active;
            int other = 1 - active;

            if (playing[current] == track && sources[current].isPlaying)
            {
                targets[current] = 1f;
                targets[other] = 0f;
                return;
            }

            // Turning back mid fade reuses the outgoing source so the music never restarts
            if (playing[other] == track && sources[other].isPlaying)
            {
                active = other;
                targets[other] = 1f;
                targets[current] = 0f;
                return;
            }

            AudioSource incoming = sources[other];
            AudioSource outgoing = sources[current];
            AudioClip clip = track.PickClip();

            incoming.Stop();
            incoming.clip = clip;
            incoming.outputAudioMixerGroup = track.Output;
            incoming.pitch = Mathf.Pow(2f, track.PitchOffset / 12f);
            incoming.volume = 0f;
            incoming.Play();

            if (sync && outgoing.isPlaying && outgoing.clip != null && clip.samples > 0)
            {
                long scaled = (long)outgoing.timeSamples * clip.frequency / Mathf.Max(1, outgoing.clip.frequency);
                incoming.timeSamples = (int)(scaled % clip.samples);
            }

            playing[other] = track;
            trackVolumes[other] = track.Volume;
            gains[other] = 0f;
            targets[other] = 1f;
            targets[current] = 0f;
            active = other;
        }

        private void FadeOutAll()
        {
            targets[0] = 0f;
            targets[1] = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            warnedMissing = false;
        }
    }
}
