// Builds the AI and hazard test assets and scene and is safe to rerun
using UnityEditor;
using UnityEngine;
using Hedgewitch.Vacuum.Demo;

namespace Hedgewitch.Combat.EditorTools
{
    public static partial class TestArenaSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const float PlayerHealth = 100f;
        private const float PlayerInvulnerability = 0.5f;

        // Health and knockback on the witch so enemies and hazards can hurt her
        [MenuItem("Tools/Hedgewitch/Test Arena/Let The Player Take Damage", false, 60)]
        public static void EnsurePlayerCanTakeDamage()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                contents.tag = "Player";

                Health health = GetOrAdd<Health>(contents, out bool addedHealth);
                if (addedHealth)
                {
                    SerializedObject data = new SerializedObject(health);
                    data.FindProperty("maxHealth").floatValue = PlayerHealth;
                    data.FindProperty("invulnerabilityAfterHit").floatValue = PlayerInvulnerability;
                    data.FindProperty("defeatOutcome").enumValueIndex = (int)Health.DefeatOutcome.None;
                    data.FindProperty("defeatDelay").floatValue = 0f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }

                HitReaction reaction = GetOrAdd<HitReaction>(contents, out bool addedReaction);
                if (addedReaction)
                {
                    // Movement is paused during the short stagger so input cannot cancel the shove
                    SerializedObject data = new SerializedObject(reaction);
                    data.FindProperty("body").objectReferenceValue = contents.GetComponent<Rigidbody>();
                    SerializedProperty paused = data.FindProperty("disableWhileStaggered");
                    paused.arraySize = 1;
                    paused.GetArrayElementAtIndex(0).objectReferenceValue = contents.GetComponent<IsometricPlayerController>();
                    data.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Debug.Log("[TestArena] Player can take damage.");
        }

        private static T GetOrAdd<T>(GameObject target, out bool added) where T : Component
        {
            T found = target.GetComponent<T>();
            added = found == null;
            return added ? target.AddComponent<T>() : found;
        }
    }
}
