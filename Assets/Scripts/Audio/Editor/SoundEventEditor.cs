// Inspector for sound events with buttons to audition them in or out of play mode
using UnityEditor;
using UnityEngine;

namespace Hedgewitch.Audio.EditorTools
{
    [CustomEditor(typeof(SoundEvent))]
    [CanEditMultipleObjects]
    public class SoundEventEditor : Editor
    {
        private static AudioSource previewSource;
        private AudioHandle playModeHandle;

        public override void OnInspectorGUI()
        {
            SoundEvent sound = (SoundEvent)target;

            if (targets.Length != 1)
            {
                DrawDefaultInspector();
                return;
            }

            EditorGUILayout.LabelField("Event name", sound.EventName, EditorStyles.boldLabel);

            if (!sound.HasClips)
            {
                EditorGUILayout.HelpBox("No clips yet. Drag AudioClips into Clips below. Until then this event plays nothing.", MessageType.Warning);
            }

            if (sound.Output == null)
            {
                EditorGUILayout.HelpBox("No mixer group set so this sound skips the mixer volumes.", MessageType.Info);
            }

            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!sound.HasClips))
                {
                    if (GUILayout.Button("▶ Preview", GUILayout.Height(26)))
                    {
                        Preview(sound);
                    }
                }

                if (GUILayout.Button("■ Stop", GUILayout.Height(26)))
                {
                    StopPreview();
                }
            }
        }

        private void OnDisable()
        {
            StopPreview();
        }

        private void Preview(SoundEvent sound)
        {
            StopPreview();

            // In play mode go through the real system so voice limits and the mixer apply
            if (Application.isPlaying)
            {
                playModeHandle = sound.Loop ? AudioManager.PlayLoop(sound, null) : AudioManager.Play(sound);
                return;
            }

            if (previewSource == null)
            {
                GameObject host = EditorUtility.CreateGameObjectWithHideFlags("Sound Event Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                previewSource = host.GetComponent<AudioSource>();
                previewSource.playOnAwake = false;
                previewSource.spatialBlend = 0f;
            }

            previewSource.clip = sound.PickClip();
            previewSource.outputAudioMixerGroup = sound.Output;
            previewSource.volume = sound.PickVolume();
            previewSource.pitch = sound.PickPitch();
            previewSource.loop = sound.Loop;
            previewSource.Play();
        }

        private void StopPreview()
        {
            playModeHandle.Stop();
            playModeHandle = AudioHandle.None;

            if (previewSource != null)
            {
                previewSource.Stop();
            }
        }
    }
}
