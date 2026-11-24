// Creates sound events for a library so the audio engineer can add sounds with one button
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Hedgewitch.Audio.EditorTools
{
    public static class SoundLibraryTools
    {
        // Letters digits and underscores only so names work as Wwise event names
        public static string CleanName(string raw)
        {
            string cleaned = Regex.Replace((raw ?? "").Trim(), @"\s+", "_");
            return Regex.Replace(cleaned, @"[^A-Za-z0-9_]", "");
        }

        public static AudioMixerGroup[] MixerGroups()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioAssetSetup.MixerPath);
            return mixer != null ? mixer.FindMatchingGroups(string.Empty) : new AudioMixerGroup[0];
        }

        public static string EventFolder(SfxLibrary library)
        {
            return $"{AudioAssetSetup.EventsRoot}/{CleanName(library.name)}";
        }

        // Returns null and explains why in error when the name cannot be used
        public static SoundEvent CreateSound(SfxLibrary library, string slotName, AudioMixerGroup group, bool loop, out string error)
        {
            error = null;
            string cleaned = CleanName(slotName);

            if (library == null)
            {
                error = "No library selected.";
                return null;
            }

            if (cleaned.Length == 0)
            {
                error = "Give the sound a name using letters, numbers or underscores.";
                return null;
            }

            if (library.HasSlot(cleaned))
            {
                error = $"{library.name} already has a sound called {cleaned}.";
                return null;
            }

            string folder = EventFolder(library);
            AudioAssetSetup.EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{CleanName(library.name)}_{cleaned}.asset");

            SoundEvent sound = ScriptableObject.CreateInstance<SoundEvent>();
            AssetDatabase.CreateAsset(sound, path);

            bool flat = group != null && (group.name == "Music" || group.name == "UI");
            SerializedObject data = new SerializedObject(sound);
            data.FindProperty("output").objectReferenceValue = group;
            data.FindProperty("loop").boolValue = loop;
            data.FindProperty("spatialBlend").floatValue = flat ? 0f : 1f;
            data.FindProperty("maxInstances").intValue = loop ? 1 : 4;
            data.ApplyModifiedPropertiesWithoutUndo();

            Undo.RecordObject(library, "Add Sound");
            library.AddExtraSound(cleaned, sound);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return sound;
        }

        // Names that are blank or used twice so the inspector and validator can flag them
        public static List<string> NameProblems(SfxLibrary library)
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (System.Reflection.FieldInfo field in SfxLibrary.BuiltInFields(library.GetType()))
            {
                seen.Add(field.Name);
            }

            for (int i = 0; i < library.ExtraSounds.Count; i++)
            {
                string slotName = library.ExtraSounds[i].name;
                if (string.IsNullOrWhiteSpace(slotName))
                {
                    problems.Add($"Sound {i + 1} in {library.name} has no name");
                }
                else if (!seen.Add(slotName))
                {
                    problems.Add($"{library.name} has more than one sound called {slotName}");
                }
            }

            return problems;
        }
    }
}
