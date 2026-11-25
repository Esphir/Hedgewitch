// Builds the thorn patch and thorn hedge prefabs and their hit sound
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using Hedgewitch.Audio;
using Hedgewitch.Audio.EditorTools;

namespace Hedgewitch.Combat.EditorTools
{
    public static partial class TestArenaSetup
    {
        public const string HazardPrefabFolder = "Assets/Prefabs/Hazards";
        public const string ThornsPrefabPath = HazardPrefabFolder + "/Thorns.prefab";
        public const string ThornHedgePrefabPath = HazardPrefabFolder + "/ThornHedge.prefab";
        public const float ThornTileSize = 2f;
        private const string HazardLibraryPath = AudioAssetSetup.LibrariesRoot + "/HazardSfx.asset";
        private const string ThornsHitEventPath = AudioAssetSetup.EventsRoot + "/Hazard/Hazard_Thorns_Hit.asset";
        private const int NotWalkableArea = 1;

        [MenuItem("Tools/Hedgewitch/Test Arena/Build Hazard Prefabs", false, 62)]
        public static void EnsureHazardPrefabs()
        {
            SoundEvent hit = EnsureThornsSound();
            EnsureThornsPrefab(ThornsPrefabPath, "Thorns", 0.6f, true, hit);
            EnsureThornsPrefab(ThornHedgePrefabPath, "ThornHedge", 1.3f, false, hit);
            AssetDatabase.SaveAssets();
            Debug.Log("[TestArena] Hazard prefabs are built.");
        }

        private static SoundEvent EnsureThornsSound()
        {
            SoundEvent sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(ThornsHitEventPath);
            if (sound == null)
            {
                AudioAssetSetup.EnsureFolder(System.IO.Path.GetDirectoryName(ThornsHitEventPath).Replace('\\', '/'));
                sound = ScriptableObject.CreateInstance<SoundEvent>();
                AssetDatabase.CreateAsset(sound, ThornsHitEventPath);
                SerializedObject data = new SerializedObject(sound);
                data.FindProperty("output").objectReferenceValue = AudioAssetSetup.FindGroup(AudioAssetSetup.EnsureMixer(), "SFX");
                data.FindProperty("maxInstances").intValue = 3;
                data.FindProperty("minRetriggerInterval").floatValue = 0.1f;
                data.FindProperty("pitchVariation").floatValue = 1f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(HazardLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, HazardLibraryPath);
            }

            if (!library.HasSlot("Thorns_Hit"))
            {
                library.AddExtraSound("Thorns_Hit", sound);
                EditorUtility.SetDirty(library);
            }

            return sound;
        }

        // One 2 by 2 tile so patches of any size are laid out by tiling instead of stretching
        private static void EnsureThornsPrefab(string path, string prefabName, float height, bool trigger, SoundEvent hit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            AudioAssetSetup.EnsureFolder(HazardPrefabFolder);
            Material leaves = EnsureLitMaterial("ThornLeaves", new Color(0.16f, 0.27f, 0.13f), 0.1f);
            Material spikes = EnsureLitMaterial("ThornSpikes", new Color(0.42f, 0.3f, 0.2f), 0.25f);

            GameObject root = new GameObject(prefabName);
            try
            {
                BoxCollider hitbox = root.AddComponent<BoxCollider>();
                hitbox.isTrigger = trigger;
                hitbox.size = new Vector3(ThornTileSize, height, ThornTileSize);
                hitbox.center = new Vector3(0f, height * 0.5f, 0f);

                ThornsHazard hazard = root.AddComponent<ThornsHazard>();
                SerializedObject hazardData = new SerializedObject(hazard);
                hazardData.FindProperty("hitSound").objectReferenceValue = hit;
                hazardData.ApplyModifiedPropertiesWithoutUndo();

                // Bruisers path around thorns instead of through them
                NavMeshModifierVolume avoid = root.AddComponent<NavMeshModifierVolume>();
                avoid.size = new Vector3(ThornTileSize, height + 1f, ThornTileSize);
                avoid.center = new Vector3(0f, height * 0.5f, 0f);
                avoid.area = NotWalkableArea;

                float baseHeight = trigger ? 0.12f : height * 0.7f;
                Part(PrimitiveType.Cube, "Bush", root.transform, new Vector3(0f, baseHeight * 0.5f, 0f),
                    new Vector3(ThornTileSize * 0.96f, baseHeight, ThornTileSize * 0.96f), leaves);

                Random.State saved = Random.state;
                Random.InitState(prefabName.GetHashCode());
                const int perSide = 4;
                for (int x = 0; x < perSide; x++)
                {
                    for (int z = 0; z < perSide; z++)
                    {
                        Vector3 spot = new Vector3(
                            ((x + 0.5f) / perSide - 0.5f) * ThornTileSize + Random.Range(-0.12f, 0.12f),
                            baseHeight,
                            ((z + 0.5f) / perSide - 0.5f) * ThornTileSize + Random.Range(-0.12f, 0.12f));
                        GameObject spike = Part(PrimitiveType.Cube, "Spike", root.transform, spot,
                            new Vector3(0.07f, height - baseHeight + 0.25f, 0.07f), spikes);
                        spike.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
                        spike.transform.localPosition += spike.transform.up * spike.transform.localScale.y * 0.5f;
                    }
                }

                Random.state = saved;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
