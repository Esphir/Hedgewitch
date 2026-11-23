// Play mode tests for title to level music fight switching hysteresis and track sync
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hedgewitch.Audio.Tests
{
    public class MusicManagerTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private SoundEvent title;
        private SoundEvent explore;
        private SoundEvent fight;
        private MusicLibrary library;
        private MusicManager music;
        private Transform player;
        private Transform enemy;

        [SetUp]
        public void SetUp()
        {
            AudioWarnings.Reset();
            title = MakeTrack("Music_Title");
            explore = MakeTrack("Music_LevelExplore");
            fight = MakeTrack("Music_LevelFight");

            library = ScriptableObject.CreateInstance<MusicLibrary>();
            library.EditorSetTracks(title, explore, fight);
            spawned.Add(library);

            Spawn("Listener").AddComponent<AudioListener>();
            player = Spawn("Player").transform;
            enemy = Spawn("Enemy").transform;
            enemy.position = new Vector3(50f, 0f, 0f);
            AudioFocus.Set(player);
            MusicThreats.Register(enemy);
        }

        [TearDown]
        public void TearDown()
        {
            MusicThreats.Unregister(enemy);
            AudioFocus.Clear(player);
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.Destroy(spawned[i]);
                }
            }

            spawned.Clear();
        }

        private GameObject Spawn(string objectName)
        {
            GameObject created = new GameObject(objectName);
            spawned.Add(created);
            return created;
        }

        private SoundEvent MakeTrack(string trackName)
        {
            AudioClip clip = AudioClip.Create(trackName, 44100 * 4, 1, 44100, false);
            SoundEvent track = ScriptableObject.CreateInstance<SoundEvent>();
            track.name = trackName;
            track.EditorConfigure(null, 0f, true, 1, 0f);
            track.EditorSetClips(clip);
            spawned.Add(clip);
            spawned.Add(track);
            return track;
        }

        private IEnumerator MakeManager(float leaveDelay, bool sync)
        {
            GameObject host = Spawn("Music");
            music = host.AddComponent<MusicManager>();
            music.EditorConfigure(library, 0.1f, 5f, leaveDelay, 0.02f, sync);
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        [UnityTest]
        public IEnumerator TitleThenLevelPlaysExplore()
        {
            yield return MakeManager(0.5f, false);

            MusicManager.SetState(MusicState.Title);
            yield return null;
            Assert.AreSame(title, music.CurrentTrack);

            MusicManager.SetState(MusicState.Level);
            yield return null;
            Assert.AreSame(explore, music.CurrentTrack);
            Assert.IsFalse(music.InFight);
        }

        [UnityTest]
        public IEnumerator NearbyEnemySwitchesToFightAndLeavesAfterTheDelay()
        {
            yield return MakeManager(0.5f, false);
            MusicManager.SetState(MusicState.Level);
            yield return null;

            enemy.position = new Vector3(3f, 0f, 0f);
            yield return Wait(0.1f);
            Assert.IsTrue(music.InFight);
            Assert.AreSame(fight, music.CurrentTrack);

            // Stepping just outside the range must not drop the fight music straight away
            enemy.position = new Vector3(6f, 0f, 0f);
            yield return Wait(0.2f);
            Assert.IsTrue(music.InFight, "left fight music before the delay");

            enemy.position = new Vector3(4f, 0f, 0f);
            yield return Wait(0.1f);
            enemy.position = new Vector3(6f, 0f, 0f);
            yield return Wait(0.3f);
            Assert.IsTrue(music.InFight, "re-entering the range should restart the delay");

            yield return Wait(0.4f);
            Assert.IsFalse(music.InFight);
            Assert.AreSame(explore, music.CurrentTrack);
        }

        [UnityTest]
        public IEnumerator DeadEnemiesNoLongerCount()
        {
            yield return MakeManager(0.1f, false);
            enemy.position = new Vector3(2f, 0f, 0f);
            MusicManager.SetState(MusicState.Level);
            yield return null;
            Assert.IsTrue(music.InFight);

            MusicThreats.Unregister(enemy);
            yield return Wait(0.3f);
            Assert.IsFalse(music.InFight);
        }

        [UnityTest]
        public IEnumerator SyncStartsTheFightTrackWhereExploreWas()
        {
            yield return MakeManager(0.5f, true);
            MusicManager.SetState(MusicState.Level);
            yield return Wait(0.6f);

            int exploreSamples = music.CurrentTimeSamples;
            enemy.position = new Vector3(2f, 0f, 0f);
            yield return Wait(0.05f);

            Assert.AreSame(fight, music.CurrentTrack);
            Assert.Greater(exploreSamples, 0, "explore track never advanced");
            Assert.GreaterOrEqual(music.CurrentTimeSamples, exploreSamples);
        }

        [UnityTest]
        public IEnumerator MissingTrackWarnsOnceAndStaysQuiet()
        {
            library.EditorSetTracks(null, ScriptableObject.CreateInstance<SoundEvent>(), fight);
            yield return MakeManager(0.5f, false);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("has no clips"));
            MusicManager.SetState(MusicState.Level);
            MusicManager.SetState(MusicState.Level);
            yield return null;

            Assert.IsNull(music.CurrentTrack);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
