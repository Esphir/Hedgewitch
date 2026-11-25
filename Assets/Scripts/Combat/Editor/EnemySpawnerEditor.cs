// Spawn and Kill All buttons on the spawner's Inspector while the game is playing
using UnityEditor;
using UnityEngine;
using Hedgewitch.Combat.Testing;

namespace Hedgewitch.Combat.EditorTools
{
    [CustomEditor(typeof(EnemySpawner))]
    public class EnemySpawnerEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            EnemySpawner spawner = (EnemySpawner)target;

            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play and the spawn buttons appear here. In game, each option's hotkey spawns one at the mouse cursor and the Kill All key clears them.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Alive: {spawner.AliveCount} / {spawner.MaxAlive}", EditorStyles.boldLabel);
                for (int i = 0; i < spawner.Options.Count; i++)
                {
                    EnemySpawner.SpawnOption option = spawner.Options[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button($"Spawn {option.name}", GUILayout.Height(26)))
                        {
                            spawner.Spawn(i);
                        }

                        if (GUILayout.Button("x5", GUILayout.Width(44), GUILayout.Height(26)))
                        {
                            spawner.SpawnMany(i, 5);
                        }
                    }
                }

                if (GUILayout.Button("Kill All", GUILayout.Height(24)))
                {
                    spawner.KillAll();
                }
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
