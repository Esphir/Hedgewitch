using UnityEngine;

namespace Hedgewitch.Vacuum.Objects
{
    /// <summary>Banks an object into the vacuum's VacuumCollection when it is swallowed.</summary>
    [DisallowMultipleComponent]
    public class VacuumCollectibleOnCapture : MonoBehaviour, IVacuumCaptureHandler
    {
        [Tooltip("Value banked on capture. Leave at 0 to use the value from the VacuumableBody.")]
        [SerializeField] private int valueOverride;

        [Tooltip("Category banked. None means use the object's own category.")]
        [SerializeField] private VacuumCategory categoryOverride = VacuumCategory.None;

        [Tooltip("Optional one-shot sound played at the nozzle on capture.")]
        [SerializeField] private AudioClip captureSound;

        [Range(0f, 1f)] [SerializeField] private float captureSoundVolume = 0.8f;

        public bool AllowCapture(in VacuumCaptureContext context)
        {
            return true;
        }

        public void OnCaptureBegin(in VacuumCaptureContext context)
        {
        }

        public void OnCaptureCompleted(in VacuumCaptureContext context)
        {
            VacuumCategory category = categoryOverride != VacuumCategory.None
                ? categoryOverride
                : context.Affectable != null ? context.Affectable.Category : VacuumCategory.None;

            int value = valueOverride;
            if (value == 0)
            {
                VacuumableBody body = GetComponent<VacuumableBody>();
                if (body != null)
                {
                    value = body.CollectionValue;
                }
            }

            VacuumCollection collection = context.Controller != null ? context.Controller.Collection : null;
            collection?.Add(category, 1, value);

            if (captureSound != null)
            {
                AudioSource.PlayClipAtPoint(captureSound, context.NozzleOrigin, captureSoundVolume);
            }
        }

        public void OnCaptureCancelled(in VacuumCaptureContext context)
        {
        }
    }
}
