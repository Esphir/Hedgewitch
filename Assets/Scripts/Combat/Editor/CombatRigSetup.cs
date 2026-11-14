// Editor tool that adds weapons to the player and health to enemies in the open scene and is safe to rerun
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hedgewitch.Combat.Demo;
using Hedgewitch.Vacuum;
using Hedgewitch.Vacuum.Demo;
using Hedgewitch.Vacuum.Enemies;

namespace Hedgewitch.Combat.EditorTools
{
    public static class CombatRigSetup
    {
        private const string DemoScenePath = "Assets/Scenes/VacuumDemo.unity";
        private const string MovesetFolder = "Assets/Settings/Combat";
        private const string MovesetPath = MovesetFolder + "/BroomMoveset.asset";
        private const string MaterialFolder = "Assets/Demo/Materials";
        private const string AirflowMaterialPath = MaterialFolder + "/VacuumAirflow.mat";

        private const float RegularEnemyHealth = 60f;
        private const float StrongEnemyHealth = 120f;
        private const float StrongEnemyThreshold = 60f;

        [MenuItem("Tools/Hedgewitch/Set Up Combat In Open Scene", false, 41)]
        public static void SetUpOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Apply(out string report))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(report);
        }

        // Batch mode entry point for executeMethod
        public static void SetUpDemoScene()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            if (Apply(out string report))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report);
        }

        private static bool Apply(out string report)
        {
            IsometricPlayerController player = Object.FindAnyObjectByType<IsometricPlayerController>();
            if (player == null)
            {
                report = "[Combat] No IsometricPlayerController in the open scene; nothing to set up.";
                return false;
            }

            List<string> log = new List<string>();
            Transform root = player.transform;

            Materials materials = EnsureMaterials(log);
            MeleeMoveset moveset = EnsureMoveset(log);

            Transform weaponsRoot = FindOrCreateChild(root, "Weapons", Vector3.zero);

            BroomWeapon broom = root.GetComponentInChildren<BroomWeapon>(true);
            if (broom == null)
            {
                broom = BuildBroom(weaponsRoot, moveset, materials);
                log.Add("added the broom (light string on LMB, heavy string on RMB)");
            }

            MusketWeapon musket = root.GetComponentInChildren<MusketWeapon>(true);
            if (musket == null)
            {
                musket = BuildMusket(weaponsRoot, materials);
                log.Add("added the musket");
            }

            VacuumWeapon vacuumWeapon = root.GetComponentInChildren<VacuumWeapon>(true);
            if (vacuumWeapon == null)
            {
                vacuumWeapon = BuildVacuumWeapon(weaponsRoot, root);
                log.Add("put the vacuum on the weapon belt");
            }

            WeaponInventory inventory = root.GetComponent<WeaponInventory>();
            if (inventory == null)
            {
                inventory = root.gameObject.AddComponent<WeaponInventory>();
                SerializedObject serialized = new SerializedObject(inventory);
                SerializedProperty list = serialized.FindProperty("weapons");
                list.arraySize = 3;
                list.GetArrayElementAtIndex(0).objectReferenceValue = broom;
                list.GetArrayElementAtIndex(1).objectReferenceValue = musket;
                list.GetArrayElementAtIndex(2).objectReferenceValue = vacuumWeapon;
                serialized.FindProperty("startingSlot").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                log.Add("added WeaponInventory: 1 broom, 2 musket, 3 vacuum");
            }

            if (root.GetComponent<DemoPlayerCombatBridge>() == null)
            {
                root.gameObject.AddComponent<DemoPlayerCombatBridge>();
                log.Add("added DemoPlayerCombatBridge (input, aim and movement for weapons)");
            }

            SetUpEnemies(log);

            if (Object.FindAnyObjectByType<CombatDebugHud>() == null)
            {
                VacuumDebugHud vacuumHud = Object.FindAnyObjectByType<VacuumDebugHud>();
                GameObject host = vacuumHud != null ? vacuumHud.gameObject : new GameObject("Combat Debug HUD");
                host.AddComponent<CombatDebugHud>();

                if (vacuumHud != null)
                {
                    // The combat panel lists the controls so hide the vacuum one
                    SerializedObject serialized = new SerializedObject(vacuumHud);
                    serialized.FindProperty("showControls").boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                log.Add($"added CombatDebugHud to '{host.name}'");
            }

            bool changed = log.Count > 0;
            report = "[Combat] setup:" + (changed ? "\n  " + string.Join("\n  ", log) : "\n  already up to date");
            return changed;
        }

        private static BroomWeapon BuildBroom(Transform parent, MeleeMoveset moveset, Materials materials)
        {
            GameObject host = new GameObject("Broom");
            host.transform.SetParent(parent, false);

            Transform pivot = FindOrCreateChild(host.transform, "Broom Pivot", new Vector3(0.35f, 0.2f, 0.1f));
            pivot.localRotation = Quaternion.Euler(moveset.restPose);

            CreatePart(PrimitiveType.Cylinder, "Handle", pivot,
                new Vector3(0f, 0f, 0.65f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.65f, 0.06f), materials.BroomHandle);

            Transform head = CreatePart(PrimitiveType.Cube, "Bristles", pivot,
                new Vector3(0f, 0f, 1.45f), Vector3.zero, new Vector3(0.34f, 0.1f, 0.36f), materials.BroomBristles);

            GameObject trailHost = new GameObject("Swing Trail");
            trailHost.transform.SetParent(head, false);
            TrailRenderer trail = trailHost.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.03f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 0f));
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = materials.Trail;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            BroomWeapon broom = host.AddComponent<BroomWeapon>();
            SerializedObject serialized = new SerializedObject(broom);
            serialized.FindProperty("displayName").stringValue = "Broom";
            SetObjectArray(serialized.FindProperty("visuals"), pivot.gameObject);
            serialized.FindProperty("moveset").objectReferenceValue = moveset;
            serialized.FindProperty("swingPivot").objectReferenceValue = pivot;
            serialized.FindProperty("swingTrail").objectReferenceValue = trail;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return broom;
        }

        private static MusketWeapon BuildMusket(Transform parent, Materials materials)
        {
            GameObject host = new GameObject("Musket");
            host.transform.SetParent(parent, false);

            Transform model = FindOrCreateChild(host.transform, "Musket Model", new Vector3(0.3f, 0.15f, 0.2f));

            CreatePart(PrimitiveType.Cylinder, "Barrel", model,
                new Vector3(0f, 0.02f, 0.55f), new Vector3(90f, 0f, 0f), new Vector3(0.045f, 0.55f, 0.045f), materials.MusketMetal);
            CreatePart(PrimitiveType.Cube, "Stock", model,
                new Vector3(0f, -0.04f, -0.05f), Vector3.zero, new Vector3(0.09f, 0.15f, 0.5f), materials.MusketWood);

            Transform muzzle = FindOrCreateChild(model, "Muzzle", new Vector3(0f, 0.02f, 1.12f));

            Light flash = muzzle.gameObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 5f;
            flash.color = new Color(1f, 0.72f, 0.38f);
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            MusketWeapon musket = host.AddComponent<MusketWeapon>();
            SerializedObject serialized = new SerializedObject(musket);
            serialized.FindProperty("displayName").stringValue = "Musket";
            SetObjectArray(serialized.FindProperty("visuals"), model.gameObject);
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("model").objectReferenceValue = model;
            serialized.FindProperty("muzzleFlash").objectReferenceValue = flash;
            serialized.FindProperty("projectileMaterial").objectReferenceValue = materials.MusketMetal;
            serialized.FindProperty("trailMaterial").objectReferenceValue = materials.Trail;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return musket;
        }

        private static VacuumWeapon BuildVacuumWeapon(Transform parent, Transform playerRoot)
        {
            GameObject host = new GameObject("Vacuum");
            host.transform.SetParent(parent, false);

            VacuumController controller = playerRoot.GetComponentInChildren<VacuumController>(true);
            VacuumWeapon weapon = host.AddComponent<VacuumWeapon>();

            SerializedObject serialized = new SerializedObject(weapon);
            serialized.FindProperty("displayName").stringValue = "Vacuum";
            serialized.FindProperty("controller").objectReferenceValue = controller;

            // Hide the nozzle model while another weapon is out
            Transform nozzleMesh = controller != null ? FindDescendant(controller.transform, "Nozzle Mesh") : null;
            if (nozzleMesh != null)
            {
                SetObjectArray(serialized.FindProperty("visuals"), nozzleMesh.gameObject);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return weapon;
        }

        private static void SetUpEnemies(List<string> log)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (EnemySuctionResponse enemy in sceneRoot.GetComponentsInChildren<EnemySuctionResponse>(true))
                {
                    bool strong = enemy.EnemyStrength > StrongEnemyThreshold;
                    GameObject go = enemy.gameObject;

                    if (go.GetComponent<Health>() == null)
                    {
                        Health health = go.AddComponent<Health>();
                        SerializedObject serialized = new SerializedObject(health);
                        serialized.FindProperty("maxHealth").floatValue = strong ? StrongEnemyHealth : RegularEnemyHealth;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        log.Add($"'{go.name}': Health {(strong ? StrongEnemyHealth : RegularEnemyHealth):0}");
                    }

                    if (go.GetComponent<HitReaction>() == null)
                    {
                        HitReaction reaction = go.AddComponent<HitReaction>();
                        SerializedObject serialized = new SerializedObject(reaction);
                        serialized.FindProperty("knockbackResistance").floatValue = strong ? 0.35f : 0f;

                        BasicEnemyAgent brain = go.GetComponentInParent<BasicEnemyAgent>();
                        if (brain != null)
                        {
                            SetObjectArray(serialized.FindProperty("disableWhileStaggered"), brain);
                        }

                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        log.Add($"'{go.name}': HitReaction (knockback resistance {(strong ? 0.35f : 0f):0.##})");
                    }
                }
            }
        }

        private struct Materials
        {
            public Material BroomHandle;
            public Material BroomBristles;
            public Material MusketWood;
            public Material MusketMetal;
            public Material Trail;
        }

        private static Materials EnsureMaterials(List<string> log)
        {
            return new Materials
            {
                BroomHandle = EnsureLitMaterial("CombatBroomHandle", new Color(0.45f, 0.29f, 0.16f), 0.2f, log),
                BroomBristles = EnsureLitMaterial("CombatBroomBristles", new Color(0.86f, 0.72f, 0.38f), 0.1f, log),
                MusketWood = EnsureLitMaterial("CombatMusketWood", new Color(0.36f, 0.2f, 0.11f), 0.35f, log),
                MusketMetal = EnsureLitMaterial("CombatMusketMetal", new Color(0.32f, 0.33f, 0.36f), 0.65f, log),
                Trail = EnsureTrailMaterial(log)
            };
        }

        private static Material EnsureLitMaterial(string name, Color color, float smoothness, List<string> log)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            log.Add($"created {path}");
            return material;
        }

        private static Material EnsureTrailMaterial(List<string> log)
        {
            string path = $"{MaterialFolder}/CombatTrail.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            // Reuse the URP airflow material as a plain white streak
            if (AssetDatabase.LoadAssetAtPath<Material>(AirflowMaterialPath) != null
                && AssetDatabase.CopyAsset(AirflowMaterialPath, path))
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = "CombatTrail";
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", null);
            }

            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.8f));
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.8f));
            EditorUtility.SetDirty(material);

            log.Add($"created {path}");
            return material;
        }

        private static MeleeMoveset EnsureMoveset(List<string> log)
        {
            MeleeMoveset moveset = AssetDatabase.LoadAssetAtPath<MeleeMoveset>(MovesetPath);
            if (moveset != null)
            {
                return moveset;
            }

            if (!AssetDatabase.IsValidFolder(MovesetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Combat");
            }

            moveset = ScriptableObject.CreateInstance<MeleeMoveset>();
            moveset.FillWithBroomDefaults();
            AssetDatabase.CreateAsset(moveset, MovesetPath);
            log.Add($"created {MovesetPath}");
            return moveset;
        }

        private static Transform CreatePart(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;

            // Weapon models are visual only and their colliders would block the player
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Transform transform = part.transform;
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(localEuler);
            transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string name, Vector3 localPosition)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetObjectArray(SerializedProperty property, Object value)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }
    }
}
