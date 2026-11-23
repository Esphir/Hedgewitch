// Edit mode tests for audio import defaults and the validator using a throwaway folder outside Assets/Audio
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Hedgewitch.Audio.EditorTools;

namespace Hedgewitch.Audio.Editor.Tests
{
    public class AudioEditorTests
    {
        private const string TempFolder = "Assets/__AudioEditorTestTemp";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
        }

        private static string WriteWav(string fileName)
        {
            Directory.CreateDirectory(TempFolder);
            string path = $"{TempFolder}/{fileName}";

            const int sampleRate = 22050;
            const int samples = sampleRate / 10;
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    writer.Write((short)(Mathf.Sin(i * 0.1f) * 8000f));
                }
            }

            AssetDatabase.ImportAsset(path);
            return path;
        }

        [TestCase("Assets/Audio/SFX/Gun_Shoot.wav", true)]
        [TestCase("assets/audio/x.ogg", true)]
        [TestCase("Assets/AudioOther/x.wav", false)]
        [TestCase("Assets/Settings/Audio/x.wav", false)]
        public void OnlyTheAudioFolderGetsDefaults(string path, bool expected)
        {
            Assert.AreEqual(expected, AudioImportDefaults.IsInAudioFolder(path));
        }

        [Test]
        public void MusicStreamsAndEffectsDecompressOnLoad()
        {
            string path = WriteWav("clip.wav");
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);

            AudioImportDefaults.ApplyDefaults(importer, "Assets/Audio/Music/Level_Explore.wav");
            Assert.AreEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType);

            AudioImportDefaults.ApplyDefaults(importer, "Assets/Audio/Weapons/Gun_Shoot.wav");
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType);
        }

        [Test]
        public void ClipsOutsideTheAudioFolderKeepUnityDefaults()
        {
            string path = WriteWav("music_outside.wav");
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.AreNotEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType);
        }

        [Test]
        public void ValidatorFindsEveryLibraryAndEvent()
        {
            AudioValidator.Report report = AudioValidator.Run();
            Assert.GreaterOrEqual(report.LibraryCount, 6);
            Assert.GreaterOrEqual(report.EventCount, 24);
            Assert.IsFalse(report.Issues.Exists(i => i.Severity == AudioValidator.Severity.Unassigned), "a library slot is empty");
            Assert.IsFalse(report.Issues.Exists(i => i.Severity == AudioValidator.Severity.Setup), "a sound event is misconfigured");
        }
    }
}
