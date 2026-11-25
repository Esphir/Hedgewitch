// Play mode tests for the bruiser chasing on the navmesh telegraphing and hitting the player
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hedgewitch.Combat.Tests
{
    public class BruiserTests
    {
        private const string BruiserPrefabPath = "Assets/Prefabs/Enemies/Bruiser.prefab";
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

        private GameObject Box(Transform parent, Vector3 position, Vector3 scale, int layer = 0)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.layer = layer;
            return box;
        }

        // Floor plus an optional see through wall then a baked navmesh
        private void BuildArena(bool wall, float size = 30f)
        {
            GameObject room = new GameObject("Test Room");
            spawned.Add(room);
            Box(room.transform, new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size));
            if (wall)
            {
                // Ignore Raycast keeps line of sight so only pathfinding can get round it
                Box(room.transform, new Vector3(0f, 1.5f, 0f), new Vector3(16f, 3f, 1f), 2);
            }

            NavMeshSurface surface = room.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private Health SpawnPlayer(Vector3 position)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spawned.Add(player);
            player.name = "Test Player";
            player.tag = "Player";
            player.transform.position = position;
            Rigidbody body = player.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            Health health = player.AddComponent<Health>();
            player.AddComponent<HitReaction>();
            return health;
        }

        private BruiserAgent SpawnBruiser(Vector3 position)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BruiserPrefabPath);
            Assert.IsNotNull(prefab, "Bruiser prefab is missing");
            GameObject bruiser = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(bruiser);
            return bruiser.GetComponent<BruiserAgent>();
#else
            return null;
#endif
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ChasesTelegraphsAndHitsThePlayer()
        {
            BuildArena(false);
            Health player = SpawnPlayer(new Vector3(0f, 1f, 4f));
            BruiserAgent bruiser = SpawnBruiser(new Vector3(0f, 1.1f, -6f));

            int windUps = 0;
            bool landed = false;
            bruiser.AttackStarted += _ => windUps++;
            bruiser.AttackLanded += (_, __) => landed = true;
            float startZ = player.transform.position.z;

            yield return WaitUntil(() => landed, 8f);

            Assert.Greater(windUps, 0, "never wound up an attack");
            Assert.IsTrue(landed, "the swing never landed");
            Assert.Less(player.Current, player.Max);
            yield return new WaitForSeconds(0.15f);
            Assert.Greater(player.transform.position.z, startZ, "the player should be knocked away from the bruiser");
        }

        [UnityTest]
        public IEnumerator PathsAroundAWallItCannotWalkThrough()
        {
            BuildArena(true);
            Health player = SpawnPlayer(new Vector3(0f, 1f, 6f));
            BruiserAgent bruiser = SpawnBruiser(new Vector3(0f, 1.1f, -6f));

            yield return WaitUntil(() => player.Current < player.Max, 14f);

            Assert.Less(player.Current, player.Max, "the bruiser never reached the player behind the wall");
            Assert.Greater(bruiser.transform.position.z, 0f, "it should have gone round to the player's side");
        }

        [UnityTest]
        public IEnumerator LocksOnWhenShotFromBeyondItsDetectionRange()
        {
            BuildArena(false, 80f);
            Health player = SpawnPlayer(new Vector3(0f, 1f, 25f));
            BruiserAgent bruiser = SpawnBruiser(new Vector3(0f, 1.1f, -25f));
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(BruiserAgent.State.Wander, bruiser.CurrentState, "50 m away it should not notice the player yet");

            float startDistance = Vector3.Distance(bruiser.transform.position, player.transform.position);
            bruiser.GetComponent<Health>().ApplyDamage(new DamageInfo
            {
                Amount = 5f,
                Point = bruiser.transform.position,
                Direction = Vector3.back,
                Tags = DamageTags.Ranged,
                Source = player.gameObject
            });

            Assert.IsTrue(bruiser.IsProvoked);
            yield return new WaitForSeconds(3f);

            Assert.AreEqual(BruiserAgent.State.Chase, bruiser.CurrentState, "should still be chasing past its give up range");
            Assert.AreSame(player.transform, bruiser.Target);
            Assert.Less(Vector3.Distance(bruiser.transform.position, player.transform.position), startDistance - 8f, "should be closing in");
        }

        [UnityTest]
        public IEnumerator DamageFromSomethingElseDoesNotProvoke()
        {
            BuildArena(false, 80f);
            SpawnPlayer(new Vector3(0f, 1f, 25f));
            BruiserAgent bruiser = SpawnBruiser(new Vector3(0f, 1.1f, -25f));
            GameObject thorns = new GameObject("Thorns");
            spawned.Add(thorns);
            yield return null;

            bruiser.GetComponent<Health>().ApplyDamage(new DamageInfo { Amount = 5f, Point = bruiser.transform.position, Source = thorns });
            yield return new WaitForSeconds(0.3f);

            Assert.IsFalse(bruiser.IsProvoked);
            Assert.AreEqual(BruiserAgent.State.Wander, bruiser.CurrentState);
        }

        [UnityTest]
        public IEnumerator IgnoresAPlayerOutOfAggroRange()
        {
            BuildArena(false);
            SpawnPlayer(new Vector3(12f, 1f, 12f));
            BruiserAgent bruiser = SpawnBruiser(new Vector3(-12f, 1.1f, -12f));

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(BruiserAgent.State.Wander, bruiser.CurrentState);
        }
    }
}
