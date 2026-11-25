// Brings the player back at the start with full health a moment after they die
using System.Collections;
using UnityEngine;

namespace Hedgewitch.Combat.Testing
{
    [DisallowMultipleComponent]
    public class PlayerRespawner : MonoBehaviour
    {
        [Tooltip("Found by the Player tag if left empty.")]
        [SerializeField] private Health player;

        [Tooltip("Where the player comes back. Their starting spot if left empty.")]
        [SerializeField] private Transform respawnPoint;

        [Min(0f)] [SerializeField] private float delay = 2f;

        [Tooltip("Switched off while dead, such as movement and the weapon belt.")]
        [SerializeField] private Behaviour[] disableWhileDead = new Behaviour[0];

        private Vector3 startPosition;
        private Quaternion startRotation;

        public bool IsRespawning { get; private set; }

#if UNITY_EDITOR
        public void EditorConfigure(Health target, float seconds)
        {
            player = target;
            delay = seconds;
        }
#endif

        private void Start()
        {
            if (player == null)
            {
                GameObject tagged = GameObject.FindWithTag("Player");
                player = tagged != null ? tagged.GetComponent<Health>() : null;
            }

            if (player == null)
            {
                Debug.LogWarning("[Respawner] No player with Health found.", this);
                enabled = false;
                return;
            }

            startPosition = player.transform.position;
            startRotation = player.transform.rotation;
            player.Defeated += HandleDefeated;
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.Defeated -= HandleDefeated;
            }
        }

        private void HandleDefeated(DamageInfo info)
        {
            if (!IsRespawning)
            {
                StartCoroutine(Respawn());
            }
        }

        private IEnumerator Respawn()
        {
            IsRespawning = true;
            SetBehaviours(false);
            yield return new WaitForSeconds(delay);

            Vector3 position = respawnPoint != null ? respawnPoint.position : startPosition;
            Quaternion rotation = respawnPoint != null ? respawnPoint.rotation : startRotation;
            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.position = position;
                body.rotation = rotation;
            }

            player.transform.SetPositionAndRotation(position, rotation);
            player.ResetHealth();
            SetBehaviours(true);
            IsRespawning = false;
        }

        private void SetBehaviours(bool on)
        {
            foreach (Behaviour behaviour in disableWhileDead)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = on;
                }
            }
        }
    }
}
