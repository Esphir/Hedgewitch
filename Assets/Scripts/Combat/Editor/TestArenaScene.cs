// Builds the 60 m AI and hazard test scene with a baked navmesh
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Hedgewitch.Audio;
using Hedgewitch.Audio.EditorTools;
using Hedgewitch.Combat.Demo;
using Hedgewitch.Combat.Testing;
using Hedgewitch.Vacuum.Demo;

namespace Hedgewitch.Combat.EditorTools
{
    public static partial class TestArenaSetup
    {
        public const string ScenePath = "Assets/Scenes/AIHazardTest.unity";
        private const string NavMeshFolder = "Assets/Scenes/AIHazardTest";
        private const string NavMeshPath = NavMeshFolder + "/NavMesh-Room.asset";
        public const float RoomSize = 60f;
        private const float WallHeight = 3f;
        public static readonly Vector3 PlayerStart = new Vector3(0f, 1.1f, -22f);

        [MenuItem("Tools/Hedgewitch/Test Arena/Build AI Hazard Test Scene", false, 40)]
        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponent<Health>() == null)
            {
                EnsurePlayerCanTakeDamage();
            }

            EnsureEnemyPrefabs();
            EnsureHazardPrefabs();

            // Camera and light are copied from the demo so the look matches
            Scene demo = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            GameObject light = CopyFromDemo(demo, scene, "Directional Light");
            GameObject cameraObject = CopyFromDemo(demo, scene, "Isometric Camera");

            Transform room = BuildRoom();
            GameObject player = Instance(PlayerPrefabPath, scene, PlayerStart, null);
            player.name = "Player";

            SerializedObject rig = new SerializedObject(cameraObject.GetComponent<IsometricCameraRig>());
            rig.FindProperty("target").objectReferenceValue = player.transform;
            rig.ApplyModifiedPropertiesWithoutUndo();
            // The rig takes over in play mode and this just frames the player in the Game view before then
            cameraObject.transform.position = PlayerStart + new Vector3(-10f, 12f, -10f);
            cameraObject.transform.LookAt(player.transform);

            BuildHazards(room);
            Transform[] spawnPoints = BuildSpawnPoints();
            BuildRules(player, spawnPoints);
            Instance(BruiserPrefabPath, scene, new Vector3(-14f, 1.1f, 12f), null).name = "Bruiser A";
            Instance(BruiserPrefabPath, scene, new Vector3(14f, 1.1f, 4f), null).name = "Bruiser B";

            Instance(AudioAssetSetup.SystemPrefabPath, scene, Vector3.zero, null);
            new GameObject("Music Cue (Level)").AddComponent<MusicCue>();
            new GameObject("Combat Debug HUD").AddComponent<CombatDebugHud>();

            BakeNavMesh(room.GetComponent<NavMeshSurface>());

            EditorSceneManager.CloseScene(demo, true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[TestArena] Built {ScenePath} ({light.name} and {cameraObject.name} copied from the demo).");
        }

        private static GameObject CopyFromDemo(Scene demo, Scene target, string rootName)
        {
            GameObject source = demo.GetRootGameObjects().First(go => go.name == rootName);
            GameObject copy = Object.Instantiate(source);
            copy.name = rootName;
            SceneManager.MoveGameObjectToScene(copy, target);
            return copy;
        }

        private static Transform BuildRoom()
        {
            Material floor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/VacuumFloor.mat");
            Material wall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/VacuumWall.mat");

            GameObject room = new GameObject("Room");
            float half = RoomSize * 0.5f;
            Solid("Floor", room.transform, new Vector3(0f, -0.5f, 0f), new Vector3(RoomSize, 1f, RoomSize), floor);
            Solid("Wall North", room.transform, new Vector3(0f, WallHeight * 0.5f, half), new Vector3(RoomSize, WallHeight, 1f), wall);
            Solid("Wall South", room.transform, new Vector3(0f, WallHeight * 0.5f, -half), new Vector3(RoomSize, WallHeight, 1f), wall);
            Solid("Wall East", room.transform, new Vector3(half, WallHeight * 0.5f, 0f), new Vector3(1f, WallHeight, RoomSize), wall);
            Solid("Wall West", room.transform, new Vector3(-half, WallHeight * 0.5f, 0f), new Vector3(1f, WallHeight, RoomSize), wall);

            // Pillars to break line of sight and make the bruisers path around things
            Transform pillars = new GameObject("Pillars").transform;
            pillars.SetParent(room.transform, false);
            Vector3[] spots =
            {
                new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 10f),
                new Vector3(-18f, 0f, -8f), new Vector3(18f, 0f, -8f), new Vector3(-6f, 0f, 20f)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                BlockNavMesh(Solid($"Pillar {i + 1}", pillars, spots[i] + Vector3.up * WallHeight * 0.5f, new Vector3(2f, WallHeight, 2f), wall));
            }

            BlockNavMesh(Solid("Long Wall", pillars, new Vector3(-14f, WallHeight * 0.5f, 16f), new Vector3(10f, WallHeight, 1f), wall));

            NavMeshSurface surface = room.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            return room.transform;
        }

        // The bake treats boxes as hollow so this clears the unreachable pockets inside and on top
        private static void BlockNavMesh(GameObject solid)
        {
            NavMeshModifierVolume volume = solid.AddComponent<NavMeshModifierVolume>();
            volume.size = new Vector3(1f, 1.2f, 1f);
            volume.center = Vector3.zero;
            volume.area = NotWalkableArea;
        }

        private static void BuildHazards(Transform room)
        {
            Transform hazards = new GameObject("Hazards").transform;
            hazards.SetParent(room, false);

            // A walk in strip across the route north from the start
            for (int i = 0; i < 4; i++)
            {
                Instance(ThornsPrefabPath, room.gameObject.scene, new Vector3(-3f + i * ThornTileSize, 0f, -14f), hazards).name = $"Thorn Strip {i + 1}";
            }

            // A solid hedge to bump into
            for (int i = 0; i < 4; i++)
            {
                Instance(ThornHedgePrefabPath, room.gameObject.scene, new Vector3(10f + i * ThornTileSize, 0f, -18f), hazards).name = $"Thorn Hedge {i + 1}";
            }

            // A big patch in the north east corner
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    Vector3 spot = new Vector3(16f + x * ThornTileSize, 0f, 16f + z * ThornTileSize);
                    Instance(ThornsPrefabPath, room.gameObject.scene, spot, hazards).name = $"Thorn Patch {x * 5 + z + 1}";
                }
            }
        }

        private static Transform[] BuildSpawnPoints()
        {
            Transform parent = new GameObject("Spawn Points").transform;
            float edge = RoomSize * 0.5f - 4f;
            Vector3[] spots =
            {
                new Vector3(-edge, 0f, edge), new Vector3(0f, 0f, edge), new Vector3(edge * 0.5f, 0f, edge),
                new Vector3(-edge, 0f, 0f), new Vector3(edge, 0f, 4f),
                new Vector3(-edge, 0f, -edge), new Vector3(edge, 0f, -edge * 0.5f), new Vector3(-edge * 0.5f, 0f, -edge)
            };

            var points = new List<Transform>();
            for (int i = 0; i < spots.Length; i++)
            {
                Transform point = new GameObject($"Spawn Point {i + 1}").transform;
                point.SetParent(parent, false);
                point.position = spots[i];
                points.Add(point);
            }

            return points.ToArray();
        }

        private static void BuildRules(GameObject player, Transform[] spawnPoints)
        {
            GameObject spawnerObject = new GameObject("Enemy Spawner");
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            SerializedObject spawnerData = new SerializedObject(spawner);
            SerializedProperty options = spawnerData.FindProperty("options");
            options.arraySize = 2;
            SetOption(options.GetArrayElementAtIndex(0), "Bruiser", BruiserPrefabPath, Key.B);
            SetOption(options.GetArrayElementAtIndex(1), "Basic Enemy", BasicEnemyPrefabPath, Key.N);
            SerializedProperty points = spawnerData.FindProperty("spawnPoints");
            points.arraySize = spawnPoints.Length;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }

            spawnerData.ApplyModifiedPropertiesWithoutUndo();

            Transform start = new GameObject("Player Start").transform;
            start.position = PlayerStart;

            PlayerRespawner respawner = new GameObject("Player Respawner").AddComponent<PlayerRespawner>();
            SerializedObject respawnData = new SerializedObject(respawner);
            respawnData.FindProperty("player").objectReferenceValue = player.GetComponent<Health>();
            respawnData.FindProperty("respawnPoint").objectReferenceValue = start;
            SerializedProperty paused = respawnData.FindProperty("disableWhileDead");
            Behaviour[] behaviours = { player.GetComponent<IsometricPlayerController>(), player.GetComponentInChildren<WeaponInventory>() };
            paused.arraySize = behaviours.Length;
            for (int i = 0; i < behaviours.Length; i++)
            {
                paused.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];
            }

            respawnData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetOption(SerializedProperty option, string optionName, string prefabPath, Key hotkey)
        {
            option.FindPropertyRelative("name").stringValue = optionName;
            option.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            option.FindPropertyRelative("hotkey").intValue = (int)hotkey;
        }

        private static void BakeNavMesh(NavMeshSurface surface)
        {
            surface.BuildNavMesh();
            AudioAssetSetup.EnsureFolder(NavMeshFolder);
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static GameObject Instance(string prefabPath, Scene scene, Vector3 position, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            instance.transform.position = position;
            return instance;
        }

        private static GameObject Solid(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solid.name = objectName;
            solid.transform.SetParent(parent, false);
            solid.transform.position = position;
            solid.transform.localScale = scale;
            if (material != null)
            {
                solid.GetComponent<Renderer>().sharedMaterial = material;
            }

            solid.isStatic = true;
            return solid;
        }
    }
}
