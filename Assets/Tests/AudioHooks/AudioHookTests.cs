// Play mode tests that load the demo scene and check every wired hook posts the right sound event
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Hedgewitch.Combat;
using Hedgewitch.Vacuum;

namespace Hedgewitch.Audio.Hooks.Tests
{
    // Records calls instead of playing so hooks can be checked before any clips exist
    public class RecordingBackend : IAudioBackend
    {
        private sealed class FakeVoice : IAudioVoice
        {
            public bool Playing = true;

            public bool IsPlaying(int playId) => Playing;

            public void Stop(int playId, float fadeTime) => Playing = false;
        }

        public readonly List<string> Played = new List<string>();
        public readonly List<string> Loops = new List<string>();
        private readonly List<FakeVoice> loopVoices = new List<FakeVoice>();

        public bool AnyLoopPlaying => loopVoices.Any(v => v.Playing);

        public AudioHandle Play(SoundEvent sound, Vector3? position)
        {
            Played.Add(sound.EventName);
            return new AudioHandle(new FakeVoice { Playing = false }, 1);
        }

        public AudioHandle PlayLoop(SoundEvent sound, Transform follow)
        {
            Loops.Add(sound.EventName);
            FakeVoice voice = new FakeVoice();
            loopVoices.Add(voice);
            return new AudioHandle(voice, 1);
        }

        public void StopAll(float fadeTime)
        {
            loopVoices.ForEach(v => v.Playing = false);
        }

        public void SetBusVolume(AudioBus bus, float linear)
        {
        }

        public int Count(string eventName) => Played.Count(n => n == eventName);
    }

    public class AudioHookTests
    {
        private const string DemoScene = "Assets/Scenes/VacuumDemo.unity";
        private RecordingBackend recorder;
        private Scene demo;

        [UnitySetUp]
        public IEnumerator LoadDemo()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                DemoScene, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            demo = SceneManager.GetSceneByPath(DemoScene);
            SceneManager.SetActiveScene(demo);
            yield return null;
            yield return null;

            recorder = new RecordingBackend();
            AudioManager.SetBackend(recorder);
        }

        // Unload so later fixtures never run inside the demo scene
        [UnityTearDown]
        public IEnumerator Unload()
        {
            AudioManager manager = Object.FindAnyObjectByType<AudioManager>();
            if (manager != null)
            {
                Object.Destroy(manager.gameObject);
            }

            if (demo.IsValid() && demo.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(demo);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DemoSceneStartsLevelMusicAndTracksThePlayerAndEnemies()
        {
            yield return null;

            Assert.IsNotNull(MusicManager.Instance, "AudioSystem prefab missing from the scene");
            Assert.AreEqual(MusicState.Level, MusicManager.Instance.State);
            Assert.IsNotNull(AudioFocus.Target);
            Assert.IsNotNull(AudioFocus.Target.GetComponent<PlayerAudio>());
            Assert.AreEqual(Object.FindObjectsByType<EnemyAudio>().Length, MusicThreats.Count);
            Assert.Greater(MusicThreats.Count, 0);

            int enabledListeners = Object.FindObjectsByType<AudioListener>().Count(l => l.enabled);
            Assert.AreEqual(1, enabledListeners, "exactly one listener should be live");
        }

        [UnityTest]
        public IEnumerator WeaponsPostTheirEvents()
        {
            WeaponInventory inventory = Object.FindAnyObjectByType<WeaponInventory>();
            BroomWeapon broom = Object.FindAnyObjectByType<BroomWeapon>(FindObjectsInactive.Include);
            MusketWeapon musket = Object.FindAnyObjectByType<MusketWeapon>(FindObjectsInactive.Include);
            Assert.AreEqual(0, recorder.Count("Broom_Equip"), "opening equip should stay silent");

            Assert.IsTrue(inventory.Equip(WeaponSlot.Ranged));
            Assert.AreEqual(1, recorder.Count("Gun_Equip"));

            musket.PullTrigger();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, recorder.Count("Gun_Shoot"));

            musket.PullTrigger();
            Assert.AreEqual(1, recorder.Count("Gun_Empty"));

            musket.Reload();
            Assert.AreEqual(1, recorder.Count("Gun_Reload"));

            Assert.IsTrue(inventory.Equip(WeaponSlot.Melee));
            Assert.AreEqual(1, recorder.Count("Broom_Equip"));

            broom.TickInput(new CombatInputFrame { PrimaryPressed = true, PrimaryHeld = true, SelectSlot = -1 });
            yield return null;
            Assert.GreaterOrEqual(recorder.Count("Broom_Attack"), 1);
        }

        [UnityTest]
        public IEnumerator VacuumRunsItsStartAndStopSequence()
        {
            VacuumController vacuum = Object.FindAnyObjectByType<VacuumController>();
            Object.FindAnyObjectByType<WeaponInventory>().Equip(WeaponSlot.Ranged);
            yield return null;

            vacuum.SetSuction(true);
            yield return new WaitForSecondsRealtime(0.6f);

            Assert.AreEqual(1, recorder.Count("Vacuum_SwitchClick"));
            Assert.AreEqual(1, recorder.Count("Vacuum_PowerOn"));
            CollectionAssert.AreEqual(new[] { "Vacuum_RunningLoop" }, recorder.Loops);
            Assert.IsTrue(recorder.AnyLoopPlaying);

            vacuum.SetSuction(false);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(2, recorder.Count("Vacuum_SwitchClick"));
            Assert.AreEqual(1, recorder.Count("Vacuum_PowerOff"));
            Assert.IsFalse(recorder.AnyLoopPlaying);
        }

        [UnityTest]
        public IEnumerator EnemyHurtAndDeathPostEventsAndEndTheThreat()
        {
            EnemyAudio enemy = Object.FindObjectsByType<EnemyAudio>()[0];
            Health health = enemy.GetComponent<Health>();
            int threats = MusicThreats.Count;

            health.ApplyDamage(new DamageInfo { Amount = 1f, Point = enemy.transform.position });
            Assert.AreEqual(1, recorder.Count("Enemy_Basic_Hurt"));

            health.ApplyDamage(new DamageInfo { Amount = 9999f, Point = enemy.transform.position });
            Assert.AreEqual(1, recorder.Count("Enemy_Basic_Death"));
            Assert.AreEqual(threats - 1, MusicThreats.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerFootstepsFollowDistanceWalked()
        {
            PlayerAudio player = Object.FindAnyObjectByType<PlayerAudio>();
            Rigidbody body = player.GetComponent<Rigidbody>();
            int before = recorder.Count("Player_Footsteps");

            for (int i = 0; i < 30; i++)
            {
                body.position += new Vector3(0.2f, 0f, 0f);
                yield return new WaitForFixedUpdate();
                yield return null;
            }

            Assert.GreaterOrEqual(recorder.Count("Player_Footsteps") - before, 2);
        }
    }
}
