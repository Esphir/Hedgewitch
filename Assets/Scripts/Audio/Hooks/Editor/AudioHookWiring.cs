// Adds the audio hooks to the Player prefab and the demo scene and is safe to rerun
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hedgewitch.Audio.EditorTools;
using Hedgewitch.Vacuum.Enemies;

namespace Hedgewitch.Audio.Hooks.EditorTools
{
    public static class AudioHookWiring
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string DemoScenePath = "Assets/Scenes/VacuumDemo.unity";

        [MenuItem("Tools/Hedgewitch/Audio/Wire Audio Hooks")]
        public static void WireAll()
        {
            AudioAssetSetup.SetUpAll();
            WirePlayerPrefab();
            WireScene(DemoScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Audio] Audio hooks are wired.");
        }

        public static void WirePlayerPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerAudio player = AudioAssetSetup.GetOrAdd<PlayerAudio>(contents);
                AssignIfEmpty(player, "sounds", AudioAssetSetup.LoadLibrary<PlayerSfx>("PlayerSfx"));

                WeaponAudio weapons = AudioAssetSetup.GetOrAdd<WeaponAudio>(contents);
                AssignIfEmpty(weapons, "broom", AudioAssetSetup.LoadLibrary<BroomSfxProfile>("BroomSfx"));
                AssignIfEmpty(weapons, "gun", AudioAssetSetup.LoadLibrary<GunSfxProfile>("GunSfx"));
                AssignIfEmpty(weapons, "vacuum", AudioAssetSetup.LoadLibrary<VacuumSfxProfile>("VacuumSfx"));

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static void WireScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnemySfxProfile basic = AudioAssetSetup.LoadLibrary<EnemySfxProfile>("EnemySfx_Basic");

            foreach (BasicEnemyAgent enemy in Object.FindObjectsByType<BasicEnemyAgent>(FindObjectsInactive.Include))
            {
                EnemyAudio audio = enemy.GetComponent<EnemyAudio>();
                if (audio == null)
                {
                    audio = Undo.AddComponent<EnemyAudio>(enemy.gameObject);
                }

                AssignIfEmpty(audio, "profile", basic);
            }

            if (Object.FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include) == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioAssetSetup.SystemPrefabPath);
                PrefabUtility.InstantiatePrefab(prefab, scene);
            }

            if (Object.FindAnyObjectByType<MusicCue>(FindObjectsInactive.Include) == null)
            {
                new GameObject("Music Cue (Level)").AddComponent<MusicCue>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AssignIfEmpty(Object target, string field, Object value)
        {
            SerializedObject data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
