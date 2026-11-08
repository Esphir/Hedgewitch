using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Owns what happens once something actually reaches the mouth.</summary>
    [DisallowMultipleComponent]
    public class VacuumCaptureSystem : MonoBehaviour
    {
        private struct CaptureOperation
        {
            public IVacuumAffectable Affectable;
            public float Elapsed;
            public float Duration;
        }

        private readonly List<CaptureOperation> operations = new List<CaptureOperation>();
        private readonly HashSet<IVacuumAffectable> rejected = new HashSet<IVacuumAffectable>();

        public event Action<IVacuumAffectable> CaptureStarted;
        public event Action<IVacuumAffectable> CaptureCompleted;
        public event Action<IVacuumAffectable> CaptureCancelled;

        public int ActiveCaptureCount => operations.Count;

        public bool IsCapturing(IVacuumAffectable affectable)
        {
            for (int i = 0; i < operations.Count; i++)
            {
                if (operations[i].Affectable == affectable)
                {
                    return true;
                }
            }

            return false;
        }

        public void Evaluate(
            VacuumController controller,
            VacuumNozzle nozzle,
            VacuumSettings settings,
            List<VacuumTargetInfo> targets)
        {
            if (nozzle == null || settings == null || targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                VacuumTargetInfo info = targets[i];
                if (!info.IsValid)
                {
                    continue;
                }

                IVacuumAffectable affectable = info.Affectable;
                if (affectable.IsCaptured || !affectable.CanBeCaptured)
                {
                    continue;
                }

                // An object that already said no is not asked again until it leaves the airflow.
                if (rejected.Contains(affectable))
                {
                    continue;
                }

                float threshold = settings.captureRadius + Mathf.Max(0f, affectable.CaptureRadius);
                if (info.Distance > threshold)
                {
                    continue;
                }

                if (IsCapturing(affectable))
                {
                    continue;
                }

                VacuumCaptureContext context = new VacuumCaptureContext
                {
                    Controller = controller,
                    NozzleOrigin = nozzle.Origin,
                    NozzleRotation = nozzle.Rotation,
                    Progress = 0f,
                    Affectable = affectable
                };

                if (!affectable.TryBeginCapture(context))
                {
                    rejected.Add(affectable);
                    continue;
                }

                operations.Add(new CaptureOperation
                {
                    Affectable = affectable,
                    Elapsed = 0f,
                    Duration = Mathf.Max(0.01f, settings.captureDuration)
                });

                CaptureStarted?.Invoke(affectable);
            }
        }

        public void Tick(
            VacuumController controller,
            VacuumNozzle nozzle,
            VacuumSettings settings,
            float deltaTime)
        {
            if (nozzle == null || operations.Count == 0)
            {
                return;
            }

            Vector3 origin = nozzle.Origin;
            Quaternion rotation = nozzle.Rotation;

            for (int i = operations.Count - 1; i >= 0; i--)
            {
                CaptureOperation operation = operations[i];
                IVacuumAffectable affectable = operation.Affectable;

                if (affectable == null || affectable.Transform == null)
                {
                    operations.RemoveAt(i);
                    continue;
                }

                operation.Elapsed += deltaTime;
                float progress = Mathf.Clamp01(operation.Elapsed / operation.Duration);
                operations[i] = operation;

                VacuumCaptureContext context = new VacuumCaptureContext
                {
                    Controller = controller,
                    NozzleOrigin = origin,
                    NozzleRotation = rotation,
                    Progress = progress,
                    Affectable = affectable
                };

                affectable.OnCaptureProgress(context);

                if (progress < 1f)
                {
                    continue;
                }

                operations.RemoveAt(i);
                affectable.OnCaptureCompleted(context);
                CaptureCompleted?.Invoke(affectable);
            }
        }

        public void CancelAll(VacuumController controller, VacuumNozzle nozzle)
        {
            if (operations.Count == 0)
            {
                rejected.Clear();
                return;
            }

            Vector3 origin = nozzle != null ? nozzle.Origin : transform.position;
            Quaternion rotation = nozzle != null ? nozzle.Rotation : transform.rotation;

            for (int i = operations.Count - 1; i >= 0; i--)
            {
                IVacuumAffectable affectable = operations[i].Affectable;
                operations.RemoveAt(i);

                if (affectable == null || affectable.Transform == null)
                {
                    continue;
                }

                VacuumCaptureContext context = new VacuumCaptureContext
                {
                    Controller = controller,
                    NozzleOrigin = origin,
                    NozzleRotation = rotation,
                    Progress = 0f,
                    Affectable = affectable
                };

                affectable.OnCaptureCancelled(context);
                CaptureCancelled?.Invoke(affectable);
            }

            rejected.Clear();
        }

        public void ForgetRejection(IVacuumAffectable affectable)
        {
            if (affectable != null)
            {
                rejected.Remove(affectable);
            }
        }
    }
}
