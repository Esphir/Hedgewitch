// Play mode smoke tests that load the AI and hazard test scene and check it is wired up
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Hedgewitch.Combat.Testing;

namespace Hedgewitch.Combat.Tests
{
    public class AIHazardSceneTests
    {
        private const string ScenePath = "Assets/Scenes/AIHazardTest.unity";
        private Scene scene;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return null;
        }

        // Unload so later fixtures never run inside this scene
        [UnityTearDown]
        public IEnumerator Unload()
        {
            foreach (GameObject persistent in Object.FindObjectsByType<Transform>()
                         .Where(t => t.parent == null && t.gameObject.scene.name == "DontDestroyOnLoad")
                         .Select(t => t.gameObject))
            {
                if (persistent.name.StartsWith("AudioSystem"))
                {
                    Object.Destroy(persistent);
                }
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            yield return null;
        }

        private static Health Player() => GameObject.FindWithTag("Player").GetComponent<Health>();

        [UnityTest]
        public IEnumerator SceneHasABigRoomPlayerEnemiesAndOneListener()
        {
            yield return null;

            Renderer floor = scene.GetRootGameObjects().First(g => g.name == "Room").transform.Find("Floor").GetComponent<Renderer>();
            Assert.Greater(floor.bounds.size.x, 26f, "should be bigger than the demo room");
            Assert.AreEqual(60f, floor.bounds.size.x, 0.1f);

            Assert.IsNotNull(Player(), "player needs Health");
            Assert.AreEqual(2, Object.FindObjectsByType<BruiserAgent>().Length);
            Assert.IsNotNull(Object.FindAnyObjectByType<EnemySpawner>());
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerRespawner>());
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Count(l => l.enabled), "exactly one listener should be live");
        }

        [UnityTest]
        public IEnumerator NavMeshCoversTheFloorButNotTheThorns()
        {
            yield return null;

            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0f, -5f), out _, 0.5f, NavMesh.AllAreas), "open floor should be walkable");

            ThornsHazard thorn = Object.FindObjectsByType<ThornsHazard>().First(t => t.name.StartsWith("Thorn Patch 13"));
            bool onMesh = NavMesh.SamplePosition(thorn.transform.position, out NavMeshHit hit, 0.4f, NavMesh.AllAreas);
            Assert.IsFalse(onMesh, $"thorns should be cut out of the navmesh but found {hit.position}");
        }

        [UnityTest]
        public IEnumerator PillarsAreSolidAndCutOutOfTheNavMesh()
        {
            yield return null;

            foreach (Vector3 pillar in new[] { new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 10f) })
            {
                Assert.IsTrue(Physics.Raycast(pillar + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f));
                Assert.AreEqual(3f, hit.point.y, 0.05f, $"expected a 3 m pillar at {pillar}");
                bool found = NavMesh.SamplePosition(pillar, out NavMeshHit onMesh, 0.5f, NavMesh.AllAreas);
                Assert.IsFalse(found, $"pillar at {pillar} should be a hole in the navmesh but found {onMesh.position}");
            }
        }

        [UnityTest]
        public IEnumerator SteppingOnTheThornStripHurtsThePlayer()
        {
            Health player = Player();
            ThornsHazard strip = Object.FindObjectsByType<ThornsHazard>().First(t => t.name == "Thorn Strip 2");
            Rigidbody body = player.GetComponent<Rigidbody>();
            body.position = strip.transform.position + new Vector3(0.3f, 1.1f, 0.4f);

            float end = Time.time + 1f;
            while (player.Current >= player.Max && Time.time < end)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Less(player.Current, player.Max);
        }

        [UnityTest]
        public IEnumerator SpawnerAddsEnemiesOnTheNavMesh()
        {
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            int before = Object.FindObjectsByType<BruiserAgent>().Length;

            GameObject spawned = spawner.Spawn(0);
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.IsNotNull(spawned);
            Assert.AreEqual(before + 1, Object.FindObjectsByType<BruiserAgent>().Length);
            Assert.IsTrue(spawned.GetComponent<NavMeshAgent>().isOnNavMesh, "spawned bruiser should be on the navmesh");

            Assert.IsNotNull(spawner.Spawn(1), "basic enemy option should spawn too");
        }
    }
}
