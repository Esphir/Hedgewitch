// Library inspector with built in slots first then extra sounds and a box to create new ones
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Hedgewitch.Audio.EditorTools
{
    [CustomEditor(typeof(SfxLibrary), true)]
    public class SfxLibraryEditor : Editor
    {
        private string newName = "";
        private int groupIndex = -1;
        private bool newLoop;
        private string lastError;

        public override void OnInspectorGUI()
        {
            SfxLibrary library = (SfxLibrary)target;
            bool custom = library is SoundLibrary;
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(iterator);
            }

            while (iterator.NextVisible(false))
            {
                if (iterator.name != "extraSounds")
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(custom ? "Sounds" : "Extra sounds", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(custom
                ? "Every sound in this library. A Sound Emitter can play any of them by name."
                : "Your own additions to this library. A Sound Emitter can play them by name.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("extraSounds"), new GUIContent(custom ? "Sounds" : "Extra sounds"), true);
            serializedObject.ApplyModifiedProperties();

            foreach (string problem in SoundLibraryTools.NameProblems(library))
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }

            DrawCreateBox(library);
        }

        private void DrawCreateBox(SfxLibrary library)
        {
            AudioMixerGroup[] groups = SoundLibraryTools.MixerGroups();
            var labels = new List<string> { "None" };
            int sfx = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                labels.Add(groups[i].name);
                if (groups[i].name == "SFX")
                {
                    sfx = i + 1;
                }
            }

            if (groupIndex < 0 || groupIndex >= labels.Count)
            {
                groupIndex = sfx;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Add a new sound", EditorStyles.boldLabel);
                newName = EditorGUILayout.TextField(new GUIContent("Name", "For example Door_Open. Spaces become underscores."), newName);
                groupIndex = EditorGUILayout.Popup("Mixer group", groupIndex, labels.ToArray());
                newLoop = EditorGUILayout.Toggle(new GUIContent("Loop", "Tick for ambience and other sounds that keep playing."), newLoop);

                string preview = SoundLibraryTools.CleanName(newName);
                if (preview.Length > 0)
                {
                    EditorGUILayout.LabelField($"Creates {SoundLibraryTools.EventFolder(library)}/{SoundLibraryTools.CleanName(library.name)}_{preview}", EditorStyles.wordWrappedMiniLabel);
                }

                using (new EditorGUI.DisabledScope(preview.Length == 0))
                {
                    if (GUILayout.Button("Create sound", GUILayout.Height(24)))
                    {
                        AudioMixerGroup group = groupIndex > 0 ? groups[groupIndex - 1] : null;
                        SoundEvent created = SoundLibraryTools.CreateSound(library, newName, group, newLoop, out lastError);
                        if (created != null)
                        {
                            EditorGUIUtility.PingObject(created);
                            newName = "";
                            newLoop = false;
                            GUI.FocusControl(null);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(lastError))
                {
                    EditorGUILayout.HelpBox(lastError, MessageType.Error);
                }
            }
        }
    }
}
