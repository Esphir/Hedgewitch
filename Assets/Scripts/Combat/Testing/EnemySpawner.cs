// Spawns test enemies from Inspector buttons or hotkeys and keeps count of the living ones
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Hedgewitch.Combat.Testing
{
    [DisallowMultipleComponent]
    public class EnemySpawner : MonoBehaviour
    {
        [Serializable]
        public class SpawnOption
        {
            public string name = "Enemy";
            public GameObject prefab;

            [Tooltip("Spawns one at the mouse cursor. None turns the hotkey off.")]
            public Key hotkey = Key.None;
        }

        [SerializeField] private List<SpawnOption> options = new List<SpawnOption>();

        [Tooltip("Buttons spawn at a random one of these.")]
        [SerializeField] private Transform[] spawnPoints = new Transform[0];

        [Min(1)] [SerializeField] private int maxAlive = 20;

        [SerializeField] private Key killAllKey = Key.K;

        [Tooltip("What the mouse cursor can spawn on.")]
        [SerializeField] private LayerMask groundLayers = 1;

        private readonly List<GameObject> alive = new List<GameObject>();
        private bool warnedFull;

        public IReadOnlyList<SpawnOption> Options => options;

        public int MaxAlive => maxAlive;

        public int AliveCount
        {
            get
            {
                alive.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);
                return alive.Count;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(int cap, Transform[] points, params SpawnOption[] spawnOptions)
        {
            maxAlive = cap;
            spawnPoints = points;
            options = new List<SpawnOption>(spawnOptions);
        }
#endif

        public GameObject Spawn(int optionIndex)
        {
            Transform picked = spawnPoints.Length > 0 ? spawnPoints[Random.Range(0, spawnPoints.Length)] : null;
            return SpawnAt(optionIndex, picked != null ? picked.position : transform.position);
        }

        public void SpawnMany(int optionIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Spawn(optionIndex) == null)
                {
                    return;
                }
            }
        }

        public GameObject SpawnAt(int optionIndex, Vector3 point)
        {
            if (optionIndex < 0 || optionIndex >= options.Count || options[optionIndex].prefab == null)
            {
                return null;
            }

            if (AliveCount >= maxAlive)
            {
                if (!warnedFull)
                {
                    warnedFull = true;
                    Debug.Log($"[Spawner] {maxAlive} enemies alive. Kill some before spawning more.", this);
                }

                return null;
            }

            warnedFull = false;
            GameObject prefab = options[optionIndex].prefab;

            // Snap to the navmesh then lift so the capsule's feet sit on the floor
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                point = hit.position;
            }

            CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
            point.y += capsule != null ? capsule.height * 0.5f * prefab.transform.localScale.y : 1f;

            Quaternion facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject enemy = Instantiate(prefab, point, facing);
            enemy.name = $"{options[optionIndex].name} {alive.Count + 1}";
            alive.Add(enemy);
            return enemy;
        }

        // Lethal damage so death sounds and outcomes play and anything that refuses is removed
        public void KillAll()
        {
            foreach (GameObject enemy in alive.ToArray())
            {
                if (enemy == null)
                {
                    continue;
                }

                IDamageable health = enemy.GetComponent<IDamageable>();
                DamageResult result = health != null
                    ? health.ApplyDamage(new DamageInfo { Amount = 1e6f, Point = enemy.transform.position, Source = gameObject })
                    : DamageResult.Ignored;

                if (!result.Applied)
                {
                    Destroy(enemy);
                }
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].hotkey != Key.None && keyboard[options[i].hotkey].wasPressedThisFrame && TryGetCursorPoint(out Vector3 point))
                {
                    SpawnAt(i, point);
                }
            }

            if (killAllKey != Key.None && keyboard[killAllKey].wasPressedThisFrame)
            {
                KillAll();
            }
        }

        private bool TryGetCursorPoint(out Vector3 point)
        {
            point = default;
            Camera view = Camera.main;
            if (view == null || Mouse.current == null)
            {
                return false;
            }

            Ray ray = view.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            point = hit.point;
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.6f);
                }
            }
        }
    }
}
