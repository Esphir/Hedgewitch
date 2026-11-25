// Builds the Bruiser and Basic Enemy prefabs the test arena spawns
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Hedgewitch.Audio;
using Hedgewitch.Audio.EditorTools;
using Hedgewitch.Audio.Hooks;
using Hedgewitch.Vacuum.Enemies;

namespace Hedgewitch.Combat.EditorTools
{
    public static partial class TestArenaSetup
    {
        public const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";
        public const string BruiserPrefabPath = EnemyPrefabFolder + "/Bruiser.prefab";
        public const string BasicEnemyPrefabPath = EnemyPrefabFolder + "/BasicEnemy.prefab";
        private const string MaterialFolder = "Assets/Demo/Materials";
        private const string DemoScenePath = "Assets/Scenes/VacuumDemo.unity";

        private const float BruiserHeight = 2.2f;
        private const float BruiserRadius = 0.6f;

        [MenuItem("Tools/Hedgewitch/Test Arena/Build Enemy Prefabs", false, 61)]
        public static void EnsureEnemyPrefabs()
        {
            AudioAssetSetup.SetUpAll();
            EnsureBruiserPrefab();
            EnsureBasicEnemyPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[TestArena] Enemy prefabs are built.");
        }

        public static GameObject EnsureBruiserPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BruiserPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            AudioAssetSetup.EnsureFolder(EnemyPrefabFolder);
            GameObject root = new GameObject("Bruiser");
            try
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                body.constraints = RigidbodyConstraints.FreezeRotation;

                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = BruiserHeight;
                capsule.radius = BruiserRadius;

                NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
                agent.radius = BruiserRadius;
                agent.height = BruiserHeight;
                agent.baseOffset = BruiserHeight * 0.5f;
                agent.acceleration = 40f;
                agent.stoppingDistance = 1.2f;

                // Body and a dark brow block so its facing reads from the isometric camera
                Material bodyMaterial = EnsureLitMaterial("BruiserBody", new Color(0.55f, 0.18f, 0.16f), 0.2f);
                Material browMaterial = EnsureLitMaterial("BruiserBrow", new Color(0.15f, 0.1f, 0.1f), 0.1f);
                GameObject model = Part(PrimitiveType.Capsule, "Model", root.transform, Vector3.zero,
                    new Vector3(BruiserRadius * 2f, BruiserHeight * 0.5f, BruiserRadius * 2f), bodyMaterial);
                Part(PrimitiveType.Cube, "Brow", model.transform, new Vector3(0f, 0.45f, 0.42f), new Vector3(0.8f, 0.15f, 0.25f), browMaterial);
                Part(PrimitiveType.Cube, "Fist Left", model.transform, new Vector3(-0.55f, -0.05f, 0.25f), new Vector3(0.35f, 0.3f, 0.35f), browMaterial);
                Part(PrimitiveType.Cube, "Fist Right", model.transform, new Vector3(0.55f, -0.05f, 0.25f), new Vector3(0.35f, 0.3f, 0.35f), browMaterial);

                Health health = root.AddComponent<Health>();
                SerializedObject healthData = new SerializedObject(health);
                healthData.FindProperty("maxHealth").floatValue = 120f;
                healthData.FindProperty("defeatOutcome").enumValueIndex = (int)Health.DefeatOutcome.Destroy;
                healthData.FindProperty("defeatDelay").floatValue = 0.35f;
                healthData.ApplyModifiedPropertiesWithoutUndo();

                BruiserAgent brain = root.AddComponent<BruiserAgent>();
                SerializedObject brainData = new SerializedObject(brain);
                brainData.FindProperty("model").objectReferenceValue = model.transform;
                brainData.ApplyModifiedPropertiesWithoutUndo();

                HitReaction reaction = root.AddComponent<HitReaction>();
                SerializedObject reactionData = new SerializedObject(reaction);
                reactionData.FindProperty("body").objectReferenceValue = body;
                reactionData.FindProperty("knockbackResistance").floatValue = 0.5f;
                SerializedProperty paused = reactionData.FindProperty("disableWhileStaggered");
                paused.arraySize = 1;
                paused.GetArrayElementAtIndex(0).objectReferenceValue = brain;
                reactionData.ApplyModifiedPropertiesWithoutUndo();

                EnemyAudio audio = root.AddComponent<EnemyAudio>();
                SerializedObject audioData = new SerializedObject(audio);
                audioData.FindProperty("profile").objectReferenceValue = AudioAssetSetup.LoadLibrary<EnemySfxProfile>("EnemySfx_Bruiser");
                audioData.FindProperty("strideLength").floatValue = 1.5f;
                audioData.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, BruiserPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Copies the demo scene's enemy into a prefab without touching the demo scene
        public static GameObject EnsureBasicEnemyPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BasicEnemyPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            AudioAssetSetup.EnsureFolder(EnemyPrefabFolder);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            try
            {
                BasicEnemyAgent source = Object.FindObjectsByType<BasicEnemyAgent>(FindObjectsInactive.Include)[0];
                GameObject copy = Object.Instantiate(source.gameObject);
                copy.name = "BasicEnemy";
                copy.transform.position = Vector3.zero;

                Health health = copy.GetComponent<Health>();
                if (health != null)
                {
                    SerializedObject data = new SerializedObject(health);
                    data.FindProperty("defeatOutcome").enumValueIndex = (int)Health.DefeatOutcome.Destroy;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, BasicEnemyPrefabPath);
                Object.DestroyImmediate(copy);
                return prefab;
            }
            finally
            {
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }

        private static GameObject Part(PrimitiveType type, string partName, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static Material EnsureLitMaterial(string materialName, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = materialName };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
