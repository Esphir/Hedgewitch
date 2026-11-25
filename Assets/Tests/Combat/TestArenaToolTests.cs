// Play mode tests for the enemy spawner cap and kill all and for respawning the player
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.TestTools;
using Hedgewitch.Combat.Testing;

namespace Hedgewitch.Combat.Tests
{
    public class TestArenaToolTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in spawned)
            {
                if (item != null)
                {
                    Object.Destroy(item);
                }
            }

            spawned.Clear();
        }

        private void BuildFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            NavMeshSurface surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private EnemySpawner MakeSpawner(int cap)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Bruiser.prefab");
#else
            GameObject prefab = null;
#endif
            GameObject host = new GameObject("Spawner");
            spawned.Add(host);
            Transform point = new GameObject("Point").transform;
            point.SetParent(host.transform);
            point.position = new Vector3(5f, 0f, 5f);

            EnemySpawner spawner = host.AddComponent<EnemySpawner>();
            spawner.EditorConfigure(cap, new[] { point }, new EnemySpawner.SpawnOption { name = "Bruiser", prefab = prefab });
            return spawner;
        }

        [UnityTest]
        public IEnumerator SpawnsUpToTheCapThenKillAllClearsThem()
        {
            BuildFloor();
            EnemySpawner spawner = MakeSpawner(3);

            spawner.SpawnMany(0, 5);
            yield return null;
            Assert.AreEqual(3, spawner.AliveCount);

            GameObject first = Object.FindAnyObjectByType<BruiserAgent>().gameObject;
            Assert.AreEqual(1.1f, first.transform.position.y, 0.2f, "should stand on the floor");

            spawner.KillAll();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, spawner.AliveCount);
            Assert.IsNull(Object.FindAnyObjectByType<BruiserAgent>());
        }

        [UnityTest]
        public IEnumerator PlayerRespawnsAtTheStartWithFullHealth()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spawned.Add(player);
            player.tag = "Player";
            player.transform.position = new Vector3(2f, 1f, 3f);
            player.AddComponent<Rigidbody>().isKinematic = true;
            Health health = player.AddComponent<Health>();
            UnityEditorSetOutcomeNone(health);

            GameObject rules = new GameObject("Rules");
            spawned.Add(rules);
            PlayerRespawner respawner = rules.AddComponent<PlayerRespawner>();
            respawner.EditorConfigure(health, 0.3f);
            yield return null;

            player.transform.position = new Vector3(-8f, 1f, -8f);
            health.ApplyDamage(new DamageInfo { Amount = 1000f });
            Assert.IsFalse(health.IsAlive);
            Assert.IsTrue(respawner.IsRespawning);

            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(health.Max, health.Current);
            Assert.AreEqual(new Vector3(2f, 1f, 3f), player.transform.position);
        }

        // The player's Health never deactivates itself on death
        private static void UnityEditorSetOutcomeNone(Health health)
        {
#if UNITY_EDITOR
            UnityEditor.SerializedObject data = new UnityEditor.SerializedObject(health);
            data.FindProperty("defeatOutcome").enumValueIndex = (int)Health.DefeatOutcome.None;
            data.ApplyModifiedPropertiesWithoutUndo();
#endif
        }
    }
}
