// Play mode tests for slider to decibel conversion and mixer volume parameters
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace Hedgewitch.Audio.Tests
{
    public class AudioVolumeTests
    {
        private const string SystemPrefabPath = "Assets/Prefabs/Audio/AudioSystem.prefab";

        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.0206f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0f, -80f)]
        [TestCase(2f, 0f)]
        public void LinearConvertsToDecibels(float linear, float decibels)
        {
            Assert.AreEqual(decibels, AudioVolume.LinearToDecibels(linear), 1e-3f);
        }

        [Test]
        public void DecibelsRoundTripToLinear()
        {
            Assert.AreEqual(0.25f, AudioVolume.DecibelsToLinear(AudioVolume.LinearToDecibels(0.25f)), 1e-4f);
            Assert.AreEqual(0f, AudioVolume.DecibelsToLinear(-80f));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EveryBusHasAnExposedMixerParameter()
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SystemPrefabPath);
            Assert.IsNotNull(prefab, "AudioSystem prefab is missing");

            GameObject system = Object.Instantiate(prefab);
            yield return null;

            AudioMixer mixer = AudioManager.Mixer;
            Assert.IsNotNull(mixer, "AudioSystem has no mixer");

            float saved = AudioVolume.Get(AudioBus.Music);
            try
            {
                foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
                {
                    Assert.IsTrue(mixer.GetFloat(AudioVolume.ParameterName(bus), out _), $"{bus} is not exposed");
                }

                AudioVolume.Set(AudioBus.Music, 0.5f);
                Assert.IsTrue(mixer.GetFloat("MusicVolume", out float decibels));
                Assert.AreEqual(-6.02f, decibels, 0.05f);
                Assert.AreEqual(0.5f, AudioVolume.Get(AudioBus.Music), 1e-4f);
            }
            finally
            {
                AudioVolume.Set(AudioBus.Music, saved);
                Object.Destroy(system);
            }
        }
#endif
    }
}
