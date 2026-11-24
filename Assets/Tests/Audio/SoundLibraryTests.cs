// Play mode tests for finding library sounds by name and for the Sound Emitter component
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hedgewitch.Audio.Tests
{
    public class SoundLibraryTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            AudioWarnings.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in spawned.Where(o => o != null))
            {
                Object.Destroy(item);
            }

            spawned.Clear();
        }

        private T Make<T>(string objectName) where T : ScriptableObject
        {
            T made = ScriptableObject.CreateInstance<T>();
            made.name = objectName;
            spawned.Add(made);
            return made;
        }

        private SoundEvent MakeEvent(string eventName, bool loop)
        {
            AudioClip clip = AudioClip.Create(eventName, 44100, 1, 44100, false);
            spawned.Add(clip);
            SoundEvent sound = Make<SoundEvent>(eventName);
            sound.EditorConfigure(null, 0f, loop, 2, 0f);
            sound.EditorSetClips(clip);
            return sound;
        }

        [Test]
        public void BuiltInSlotsAreFoundByNameInAnyCase()
        {
            MusicLibrary music = Make<MusicLibrary>("MusicLibrary");
            SoundEvent fight = Make<SoundEvent>("Music_LevelFight");
            music.EditorSetTracks(null, null, fight);

            Assert.AreSame(fight, music.Find("LevelFight"));
            Assert.AreSame(fight, music.Find("levelfight"));
            CollectionAssert.AreEqual(new[] { "TitleTrack", "LevelExplore", "LevelFight" }, music.SlotNames().ToArray());
        }

        [Test]
        public void ExtraSoundsAreAddedToAnyLibrary()
        {
            SoundLibrary ambience = Make<SoundLibrary>("Ambience");
            SoundEvent wind = Make<SoundEvent>("Ambience_Wind");
            ambience.AddExtraSound("Wind", wind);

            MusicLibrary music = Make<MusicLibrary>("MusicLibrary");
            SoundEvent sting = Make<SoundEvent>("Music_Sting");
            music.AddExtraSound("Sting", sting);

            Assert.AreSame(wind, ambience.Find("Wind"));
            Assert.AreSame(sting, music.Find("Sting"));
            Assert.IsTrue(music.HasSlot("sting"));
            Assert.IsNull(ambience.Find("Rain"));
        }

        [UnityTest]
        public IEnumerator EmitterPlaysALoopOnStartAndFadesItWhenDisabled()
        {
            GameObject listener = new GameObject("Listener", typeof(AudioListener));
            GameObject manager = new GameObject("Manager", typeof(AudioManager));
            spawned.Add(listener);
            spawned.Add(manager);

            SoundLibrary ambience = Make<SoundLibrary>("Ambience");
            SoundEvent hum = MakeEvent("Ambience_Hum", true);
            ambience.AddExtraSound("Hum", hum);

            GameObject host = new GameObject("Hum Source");
            spawned.Add(host);
            SoundEmitter emitter = host.AddComponent<SoundEmitter>();
            emitter.EditorConfigure(ambience, "Hum", SoundEmitter.PlayTrigger.Start);
            yield return null;

            UnityAudioBackend backend = (UnityAudioBackend)AudioManager.Backend;
            Assert.AreEqual(1, backend.CountActive(hum));

            emitter.Play();
            Assert.AreEqual(1, backend.CountActive(hum), "playing a running loop again must not stack it");

            host.SetActive(false);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(0, backend.CountActive(hum));
        }

        [UnityTest]
        public IEnumerator EmitterWarnsOnceForAMissingName()
        {
            spawned.Add(new GameObject("Listener", typeof(AudioListener)));
            SoundLibrary ambience = Make<SoundLibrary>("Ambience");
            GameObject host = new GameObject("Bad Source");
            spawned.Add(host);
            SoundEmitter emitter = host.AddComponent<SoundEmitter>();
            emitter.EditorConfigure(ambience, "Rain", SoundEmitter.PlayTrigger.Manual);
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex("asks for Rain"));
            emitter.Play();
            emitter.Play();
            yield return null;
        }
    }
}
