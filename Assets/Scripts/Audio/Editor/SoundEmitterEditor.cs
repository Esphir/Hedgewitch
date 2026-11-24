// Sound Emitter inspector with a dropdown of the chosen library's sounds
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hedgewitch.Audio.EditorTools
{
    [CustomEditor(typeof(SoundEmitter))]
    public class SoundEmitterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (targets.Length != 1)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();
            SerializedProperty library = serializedObject.FindProperty("library");
            SerializedProperty slot = serializedObject.FindProperty("slot");
            SerializedProperty sound = serializedObject.FindProperty("sound");
            SerializedProperty playOn = serializedObject.FindProperty("playOn");

            EditorGUILayout.PropertyField(library);
            if (library.objectReferenceValue is SfxLibrary chosen)
            {
                List<string> names = chosen.SlotNames().ToList();
                int index = names.FindIndex(n => string.Equals(n, slot.stringValue, System.StringComparison.OrdinalIgnoreCase));
                if (index < 0 && !string.IsNullOrEmpty(slot.stringValue))
                {
                    names.Insert(0, $"{slot.stringValue} (missing)");
                    index = 0;
                }

                if (names.Count == 0)
                {
                    EditorGUILayout.HelpBox("This library has no sounds yet. Add some in the library's inspector.", MessageType.Info);
                }
                else
                {
                    int picked = EditorGUILayout.Popup(new GUIContent("Sound", slot.tooltip), Mathf.Max(index, 0), names.ToArray());
                    if (picked != index || index < 0)
                    {
                        slot.stringValue = names[picked].Replace(" (missing)", "");
                    }
                }
            }

            EditorGUILayout.PropertyField(sound, new GUIContent("Or Sound Event", sound.tooltip));
            if (library.objectReferenceValue == null && sound.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Pick a library and sound, or drag a Sound Event in.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(playOn);
            var trigger = (SoundEmitter.PlayTrigger)playOn.enumValueIndex;
            if (trigger == SoundEmitter.PlayTrigger.TriggerEnter || trigger == SoundEmitter.PlayTrigger.CollisionEnter)
            {
                SerializedProperty requiredTag = serializedObject.FindProperty("requiredTag");
                requiredTag.stringValue = EditorGUILayout.TextField(new GUIContent("Required tag", requiredTag.tooltip), requiredTag.stringValue);
                Collider collider = ((Component)target).GetComponent<Collider>();
                if (collider == null)
                {
                    EditorGUILayout.HelpBox("Needs a Collider on this object. Tick Is Trigger for Trigger Enter.", MessageType.Warning);
                }
            }
            else if (trigger == SoundEmitter.PlayTrigger.Manual)
            {
                EditorGUILayout.HelpBox("Call Play from a UI Button's On Click list or from an animation event.", MessageType.None);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("followThisObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeOut"));
            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play"))
                    {
                        ((SoundEmitter)target).Play();
                    }

                    if (GUILayout.Button("Stop"))
                    {
                        ((SoundEmitter)target).Stop();
                    }
                }
            }
        }
    }
}
