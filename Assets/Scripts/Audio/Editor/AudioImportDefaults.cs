// First import defaults for clips in the Audio folder that only touch meta files and never override later changes
using System;
using UnityEditor;
using UnityEngine;

namespace Hedgewitch.Audio.EditorTools
{
    public class AudioImportDefaults : AssetPostprocessor
    {
        public const string AudioFolder = "Assets/Audio/";

        private void OnPreprocessAudio()
        {
            // Only brand new files so anything the engineer set by hand stays
            if (!assetImporter.importSettingsMissing || !IsInAudioFolder(assetPath))
            {
                return;
            }

            ApplyDefaults((AudioImporter)assetImporter, assetPath);
        }

        public static bool IsInAudioFolder(string path)
        {
            return path.Replace('\\', '/').StartsWith(AudioFolder, StringComparison.OrdinalIgnoreCase);
        }

        // Music anywhere in the path streams and everything else is treated as a short effect
        public static bool IsMusic(string path)
        {
            return path.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void ApplyDefaults(AudioImporter importer, string path)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            if (IsMusic(path))
            {
                settings.loadType = AudioClipLoadType.Streaming;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.loadInBackground = false;
            }

            importer.defaultSampleSettings = settings;
        }
    }
}
