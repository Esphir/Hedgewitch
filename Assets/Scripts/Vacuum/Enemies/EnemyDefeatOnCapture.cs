using UnityEngine;
using UnityEngine.Events;

namespace Hedgewitch.Vacuum.Enemies
{
    /// <summary>Capture handler that turns "swallowed by the vacuum" into "defeated".</summary>
    [DisallowMultipleComponent]
    public class EnemyDefeatOnCapture : MonoBehaviour, IVacuumCaptureHandler
    {
        [Tooltip("Value banked when this enemy is swallowed.")]
        [SerializeField] private int bounty = 10;

        [Tooltip("Optional prefab spawned where the enemy was swallowed, e.g. a loot drop or a puff.")]
        [SerializeField] private GameObject captureEffectPrefab;

        [SerializeField] private UnityEvent defeated;

        public bool AllowCapture(in VacuumCaptureContext context)
        {
            return true;
        }

        public void OnCaptureBegin(in VacuumCaptureContext context)
        {
        }

        public void OnCaptureCompleted(in VacuumCaptureContext context)
        {
            VacuumCollection collection = context.Controller != null ? context.Controller.Collection : null;
            collection?.Add(VacuumCategory.Enemy, 1, bounty);

            if (captureEffectPrefab != null)
            {
                Instantiate(captureEffectPrefab, context.NozzleOrigin, Quaternion.identity);
            }

            defeated?.Invoke();
        }

        public void OnCaptureCancelled(in VacuumCaptureContext context)
        {
        }
    }
}
