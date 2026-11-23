// Play mode tests for sound event picking voice limits missing clips and loop handles
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hedgewitch.Audio.Tests
{
    public class AudioCoreTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GameObject host;
        private UnityAudioBackend backend;

        [SetUp]
        public void SetUp()
        {
            AudioWarnings.Reset();
            host = new GameObject("Test Audio Host");
            host.AddComponent<AudioListener>();
            spawned.Add(host);
            backend = new UnityAudioBackend(host.transform, 2, 8);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.Destroy(spawned[i]);
                }
            }

            spawned.Clear();
        }

        private AudioClip MakeClip(string clipName, float seconds = 1f)
        {
            AudioClip clip = AudioClip.Create(clipName, Mathf.CeilToInt(44100 * seconds), 1, 44100, false);
            spawned.Add(clip);
            return clip;
        }

        private SoundEvent MakeEvent(int maxInstances = 4, float retrigger = 0f, bool loop = false, params AudioClip[] clips)
        {
            SoundEvent sound = ScriptableObject.CreateInstance<SoundEvent>();
            sound.name = "Test_Event";
            sound.EditorConfigure(null, 0f, loop, maxInstances, retrigger);
            sound.EditorSetClips(clips);
            spawned.Add(sound);
            return sound;
        }

        [Test]
        public void PickClipNeverRepeatsWithTwoOrMoreClips()
        {
            SoundEvent sound = MakeEvent(4, 0f, false, MakeClip("a"), MakeClip("b"), MakeClip("c"));

            AudioClip previous = sound.PickClip();
            for (int i = 0; i < 200; i++)
            {
                AudioClip next = sound.PickClip();
                Assert.AreNotSame(previous, next);
                previous = next;
            }
        }

        [Test]
        public void PickClipSkipsEmptySlotsAndRepeatsASingleClip()
        {
            AudioClip only = MakeClip("only");
            SoundEvent sound = MakeEvent(4, 0f, false, null, only, null);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(only, sound.PickClip());
            }
        }

        [Test]
        public void PitchOffsetRaisesPitchBySemitones()
        {
            SoundEvent sound = ScriptableObject.CreateInstance<SoundEvent>();
            spawned.Add(sound);
            sound.EditorConfigure(null, 0f, false, 1, 0f, 12f);
            Assert.AreEqual(2f, sound.PickPitch(), 1e-4f);
        }

        [Test]
        public void EmptyEventWarnsOnceAndPlaysNothing()
        {
            SoundEvent sound = MakeEvent(4, 0f, false);

            LogAssert.Expect(LogType.Warning, new Regex("Test_Event has no clips"));
            AudioHandle first = backend.Play(sound, null);
            AudioHandle second = backend.Play(sound, Vector3.zero);
            backend.PlayLoop(sound, host.transform);

            Assert.IsFalse(first.IsPlaying);
            Assert.IsFalse(second.IsPlaying);
            Assert.AreEqual(0, backend.ActiveVoiceCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NullEventIsIgnoredQuietly()
        {
            Assert.IsFalse(backend.Play(null, null).IsPlaying);
            Assert.IsFalse(backend.PlayLoop(null, null).IsPlaying);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void VoiceLimitStopsTheOldestCopy()
        {
            SoundEvent sound = MakeEvent(2, 0f, false, MakeClip("a"), MakeClip("b"));

            AudioHandle first = backend.Play(sound, null);
            AudioHandle second = backend.Play(sound, null);
            AudioHandle third = backend.Play(sound, null);

            Assert.AreEqual(2, backend.CountActive(sound));
            Assert.IsFalse(first.IsPlaying);
            Assert.IsTrue(second.IsPlaying);
            Assert.IsTrue(third.IsPlaying);
        }

        [Test]
        public void RetriggerIntervalBlocksImmediateRepeats()
        {
            SoundEvent sound = MakeEvent(4, 0.5f, false, MakeClip("a"));

            Assert.IsTrue(backend.Play(sound, null).IsPlaying);
            Assert.IsFalse(backend.Play(sound, null).IsPlaying);
            Assert.AreEqual(1, backend.CountActive(sound));
        }

        [Test]
        public void PoolGrowsToItsCapThenSteals()
        {
            SoundEvent sound = MakeEvent(100, 0f, false, MakeClip("a"));

            for (int i = 0; i < 12; i++)
            {
                backend.Play(sound, null);
            }

            Assert.AreEqual(8, backend.PoolSize);
            Assert.AreEqual(8, backend.ActiveVoiceCount);
        }

        [Test]
        public void StaleHandleCannotStopAReusedVoice()
        {
            SoundEvent sound = MakeEvent(1, 0f, false, MakeClip("a"));

            AudioHandle stale = backend.Play(sound, null);
            stale.Stop();
            AudioHandle fresh = backend.Play(sound, null);
            stale.Stop();

            Assert.IsFalse(stale.IsPlaying);
            Assert.IsTrue(fresh.IsPlaying);
        }

        [UnityTest]
        public IEnumerator LoopFadesOutAndReleasesItsVoice()
        {
            SoundEvent sound = MakeEvent(1, 0f, true, MakeClip("loop"));
            AudioHandle handle = backend.PlayLoop(sound, host.transform);
            Assert.IsTrue(handle.IsPlaying);

            handle.Stop(0.1f);
            Assert.IsTrue(handle.IsPlaying, "a fading loop still counts as playing");

            float end = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < end)
            {
                backend.Tick(Time.unscaledDeltaTime);
                yield return null;
            }

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, backend.CountActive(sound));
        }

        [UnityTest]
        public IEnumerator LoopStopsWhenItsFollowTargetIsDestroyed()
        {
            SoundEvent sound = MakeEvent(1, 0f, true, MakeClip("loop"));
            GameObject owner = new GameObject("Loop Owner");
            AudioHandle handle = backend.PlayLoop(sound, owner.transform);

            Object.Destroy(owner);
            float end = Time.unscaledTime + 0.6f;
            while (Time.unscaledTime < end)
            {
                backend.Tick(Time.unscaledDeltaTime);
                yield return null;
            }

            Assert.IsFalse(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ManagerPlaysThroughTheStaticApi()
        {
            GameObject managerHost = new GameObject("Test AudioManager");
            spawned.Add(managerHost);
            managerHost.AddComponent<AudioManager>();
            yield return null;

            SoundEvent sound = MakeEvent(4, 0f, false, MakeClip("a"));
            Assert.IsTrue(AudioManager.HasInstance);
            Assert.IsTrue(AudioManager.Play(sound, Vector3.one).IsPlaying);
            Assert.IsFalse(AudioManager.Play(null).IsPlaying);
        }
    }
}
