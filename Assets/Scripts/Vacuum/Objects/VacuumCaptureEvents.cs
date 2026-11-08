using UnityEngine;
using UnityEngine.Events;

namespace Hedgewitch.Vacuum.Objects
{
    /// <summary>
    /// Exposes the capture lifecycle as UnityEvents, so designers can hook particles, sounds, doors
    /// or quest counters onto a swallowed object without writing a script.
    /// </summary>
    [DisallowMultipleComponent]
    public class VacuumCaptureEvents : MonoBehaviour, IVacuumCaptureHandler
    {
        [Tooltip("Blocks capture entirely while true. Use for objects that must be unlocked first.")]
        [SerializeField] private bool blockCapture;

        [SerializeField] private UnityEvent captureBegan;
        [SerializeField] private UnityEvent captureCompleted;
        [SerializeField] private UnityEvent captureCancelled;

        public bool BlockCapture
        {
            get => blockCapture;
            set => blockCapture = value;
        }

        public bool AllowCapture(in VacuumCaptureContext context)
        {
            return !blockCapture;
        }

        public void OnCaptureBegin(in VacuumCaptureContext context)
        {
            captureBegan?.Invoke();
        }

        public void OnCaptureCompleted(in VacuumCaptureContext context)
        {
            captureCompleted?.Invoke();
        }

        public void OnCaptureCancelled(in VacuumCaptureContext context)
        {
            captureCancelled?.Invoke();
        }
    }
}
