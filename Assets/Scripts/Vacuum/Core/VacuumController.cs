using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>
    /// Orchestrates the four vacuum subsystems: the nozzle (where the air goes), the detector (what
    /// it can reach), the suction field (how hard it pulls) and the capture system (what happens at
    /// the mouth).
    /// </summary>
    [DisallowMultipleComponent]
    public class VacuumController : MonoBehaviour
    {
        [Header("Subsystems")]
        [SerializeField] private VacuumNozzle nozzle;
        [SerializeField] private VacuumSettings settings;
        [SerializeField] private VacuumDetector detector;
        [SerializeField] private VacuumSuctionField suctionField;
        [SerializeField] private VacuumCaptureSystem captureSystem;
        [SerializeField] private VacuumCollection collection;

        [Header("Debug")]
        [SerializeField] private bool drawConeGizmo = true;
        [SerializeField] private bool logCaptures;

        private readonly List<VacuumTargetInfo> activeTargets = new List<VacuumTargetInfo>();
        private readonly List<VacuumTargetInfo> scanBuffer = new List<VacuumTargetInfo>();
        private readonly HashSet<IVacuumAffectable> activeSet = new HashSet<IVacuumAffectable>();
        private readonly HashSet<IVacuumAffectable> scanSet = new HashSet<IVacuumAffectable>();
        private readonly List<IVacuumAffectable> releaseScratch = new List<IVacuumAffectable>();

        private VacuumSettings runtimeSettings;
        private VacuumState state = VacuumState.Idle;
        private float intensity;
        private float load;
        private float rescanTimer;
        private float pruneTimer;

        public VacuumState State => state;

        public bool IsSucking => state == VacuumState.Sucking;

        public float Intensity => intensity;

        public float Load => load;

        public VacuumNozzle Nozzle => nozzle;

        public VacuumCollection Collection => collection;

        public VacuumCaptureSystem CaptureSystem => captureSystem;

        public IReadOnlyList<VacuumTargetInfo> ActiveTargets => activeTargets;

        public int ActiveTargetCount => activeTargets.Count;

        public VacuumSettings Settings
        {
            get
            {
                if (settings != null)
                {
                    return settings;
                }

                if (runtimeSettings == null)
                {
                    runtimeSettings = VacuumSettings.CreateDefault();
                }

                return runtimeSettings;
            }
            set => settings = value;
        }

        public event Action<VacuumState> StateChanged;
        public event Action<IVacuumAffectable> TargetAcquired;
        public event Action<IVacuumAffectable, VacuumReleaseReason> TargetLost;
        public event Action<IVacuumAffectable> ObjectCaptured;

        private void Reset()
        {
            ResolveSubsystems();
        }

        private void Awake()
        {
            ResolveSubsystems();
        }

        private void OnEnable()
        {
            if (captureSystem != null)
            {
                captureSystem.CaptureCompleted += HandleCaptureCompleted;
            }
        }

        private void OnDisable()
        {
            if (captureSystem != null)
            {
                captureSystem.CaptureCompleted -= HandleCaptureCompleted;
            }

            ReleaseEverything(VacuumReleaseReason.SourceDisabled);
            intensity = 0f;
            load = 0f;
            SetState(VacuumState.Idle);
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            VacuumSettings active = Settings;

            UpdateIntensity(active, deltaTime);

            pruneTimer -= deltaTime;
            if (pruneTimer <= 0f)
            {
                VacuumRegistry.Prune();
                pruneTimer = 5f;
            }

            if (state != VacuumState.Sucking)
            {
                // Still finish any swallow that was already underway when not cancelling them.
                captureSystem?.Tick(this, nozzle, active, deltaTime);
                load = Mathf.MoveTowards(load, 0f, deltaTime / Mathf.Max(0.01f, active.spinDownTime));
                return;
            }

            if (nozzle == null || detector == null || suctionField == null)
            {
                return;
            }

            rescanTimer -= deltaTime;
            if (rescanTimer <= 0f)
            {
                detector.Scan(nozzle, active, scanBuffer);
                SyncTargets(active, deltaTime);
                rescanTimer = active.rescanInterval;
            }
            else
            {
                RevalidateTargets(active);
            }

            float rawLoad = suctionField.Apply(this, nozzle, active, activeTargets, intensity, deltaTime);
            load = Mathf.Lerp(load, rawLoad, 1f - Mathf.Exp(-8f * deltaTime));

            if (captureSystem != null)
            {
                captureSystem.Evaluate(this, nozzle, active, activeTargets);
                captureSystem.Tick(this, nozzle, active, deltaTime);
            }
        }

        public void BeginSuction()
        {
            if (state == VacuumState.Sucking)
            {
                return;
            }

            rescanTimer = 0f;
            SetState(VacuumState.Sucking);
        }

        public void EndSuction()
        {
            if (state == VacuumState.Idle)
            {
                return;
            }

            SetState(VacuumState.Idle);
            ReleaseEverything(VacuumReleaseReason.SuctionStopped);

            if (captureSystem != null && Settings.cancelCapturesOnRelease)
            {
                captureSystem.CancelAll(this, nozzle);
            }
        }

        public void SetSuction(bool active)
        {
            if (active)
            {
                BeginSuction();
            }
            else
            {
                EndSuction();
            }
        }

        public void ToggleSuction()
        {
            SetSuction(state != VacuumState.Sucking);
        }

        public void AimAt(Vector3 worldPoint)
        {
            nozzle?.AimAtPoint(worldPoint);
        }

        private void ResolveSubsystems()
        {
            if (nozzle == null)
            {
                nozzle = GetComponentInChildren<VacuumNozzle>();
            }

            if (detector == null)
            {
                detector = GetComponent<VacuumDetector>();
                if (detector == null)
                {
                    detector = gameObject.AddComponent<VacuumDetector>();
                }
            }

            if (suctionField == null)
            {
                suctionField = GetComponent<VacuumSuctionField>();
                if (suctionField == null)
                {
                    suctionField = gameObject.AddComponent<VacuumSuctionField>();
                }
            }

            if (captureSystem == null)
            {
                captureSystem = GetComponent<VacuumCaptureSystem>();
                if (captureSystem == null)
                {
                    captureSystem = gameObject.AddComponent<VacuumCaptureSystem>();
                }
            }

            if (collection == null)
            {
                collection = GetComponentInParent<VacuumCollection>();
            }
        }

        private void UpdateIntensity(VacuumSettings active, float deltaTime)
        {
            float target = state == VacuumState.Sucking ? 1f : 0f;
            float rampTime = target > intensity ? active.spinUpTime : active.spinDownTime;
            intensity = Mathf.MoveTowards(intensity, target, deltaTime / Mathf.Max(0.001f, rampTime));
        }

        private void SetState(VacuumState next)
        {
            if (state == next)
            {
                return;
            }

            state = next;
            StateChanged?.Invoke(state);
        }

        private void SyncTargets(VacuumSettings active, float deltaTime)
        {
            scanSet.Clear();
            for (int i = 0; i < scanBuffer.Count; i++)
            {
                scanSet.Add(scanBuffer[i].Affectable);
            }

            releaseScratch.Clear();
            foreach (IVacuumAffectable tracked in activeSet)
            {
                if (tracked == null || tracked.Transform == null || !scanSet.Contains(tracked))
                {
                    releaseScratch.Add(tracked);
                }
            }

            for (int i = 0; i < releaseScratch.Count; i++)
            {
                IVacuumAffectable lost = releaseScratch[i];
                VacuumReleaseReason reason = VacuumReleaseReason.LeftCone;

                if (lost != null && lost.Transform != null)
                {
                    reason = ToReleaseReason(detector.Evaluate(lost, nozzle, active, out _));
                }

                ReleaseTarget(lost, reason);
            }

            activeTargets.Clear();
            for (int i = 0; i < scanBuffer.Count; i++)
            {
                VacuumTargetInfo info = scanBuffer[i];
                activeTargets.Add(info);

                if (!activeSet.Add(info.Affectable))
                {
                    continue;
                }

                VacuumSuctionContext context = suctionField.BuildContext(
                    this, nozzle, active, info, intensity, deltaTime);
                info.Affectable.OnSuctionBegin(context);
                TargetAcquired?.Invoke(info.Affectable);
            }
        }

        private void RevalidateTargets(VacuumSettings active)
        {
            for (int i = activeTargets.Count - 1; i >= 0; i--)
            {
                IVacuumAffectable affectable = activeTargets[i].Affectable;
                if (affectable == null || affectable.Transform == null)
                {
                    activeTargets.RemoveAt(i);
                    ReleaseTarget(affectable, VacuumReleaseReason.OutOfRange);
                    continue;
                }

                VacuumEvaluation evaluation = detector.Evaluate(affectable, nozzle, active, out VacuumTargetInfo info);
                if (evaluation == VacuumEvaluation.Accepted)
                {
                    activeTargets[i] = info;
                    continue;
                }

                activeTargets.RemoveAt(i);
                ReleaseTarget(affectable, ToReleaseReason(evaluation));
            }
        }

        private void ReleaseTarget(IVacuumAffectable affectable, VacuumReleaseReason reason)
        {
            if (affectable == null)
            {
                activeSet.Remove(null);
                return;
            }

            activeSet.Remove(affectable);
            captureSystem?.ForgetRejection(affectable);

            if (affectable.Transform != null)
            {
                affectable.OnSuctionEnd(reason);
            }

            TargetLost?.Invoke(affectable, reason);
        }

        private void ReleaseEverything(VacuumReleaseReason reason)
        {
            releaseScratch.Clear();
            foreach (IVacuumAffectable tracked in activeSet)
            {
                releaseScratch.Add(tracked);
            }

            activeSet.Clear();
            activeTargets.Clear();

            for (int i = 0; i < releaseScratch.Count; i++)
            {
                IVacuumAffectable affectable = releaseScratch[i];
                if (affectable == null)
                {
                    continue;
                }

                captureSystem?.ForgetRejection(affectable);

                if (affectable.Transform != null)
                {
                    affectable.OnSuctionEnd(reason);
                }

                TargetLost?.Invoke(affectable, reason);
            }

            releaseScratch.Clear();
        }

        private void HandleCaptureCompleted(IVacuumAffectable affectable)
        {
            if (affectable != null)
            {
                activeSet.Remove(affectable);

                for (int i = activeTargets.Count - 1; i >= 0; i--)
                {
                    if (activeTargets[i].Affectable == affectable)
                    {
                        activeTargets.RemoveAt(i);
                    }
                }
            }

            if (logCaptures && affectable != null && affectable.Transform != null)
            {
                Debug.Log($"[Vacuum] captured {affectable.Transform.name} ({affectable.Category})", this);
            }

            ObjectCaptured?.Invoke(affectable);
        }

        private static VacuumReleaseReason ToReleaseReason(VacuumEvaluation evaluation)
        {
            switch (evaluation)
            {
                case VacuumEvaluation.OutOfRange:
                    return VacuumReleaseReason.OutOfRange;
                case VacuumEvaluation.Occluded:
                    return VacuumReleaseReason.Occluded;
                case VacuumEvaluation.OutOfCone:
                    return VacuumReleaseReason.LeftCone;
                case VacuumEvaluation.NotVacuumable:
                case VacuumEvaluation.CategoryRejected:
                    return VacuumReleaseReason.Rejected;
                default:
                    return VacuumReleaseReason.LeftCone;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawConeGizmo || nozzle == null)
            {
                return;
            }

            VacuumSettings active = settings != null ? settings : runtimeSettings;
            if (active == null)
            {
                return;
            }

            Vector3 origin = nozzle.Origin;
            Quaternion rotation = nozzle.Rotation;
            float distance = active.maxDistance;
            float halfWidth = VacuumNozzle.ConeRadiusAt(distance, active.HorizontalHalfAngle);
            float halfHeight = VacuumNozzle.ConeRadiusAt(distance, active.VerticalHalfAngle);

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.85f);

            Vector3 forward = rotation * Vector3.forward * distance;
            Vector3 right = rotation * Vector3.right * halfWidth;
            Vector3 up = rotation * Vector3.up * halfHeight;

            Vector3 topLeft = origin + forward - right + up;
            Vector3 topRight = origin + forward + right + up;
            Vector3 bottomLeft = origin + forward - right - up;
            Vector3 bottomRight = origin + forward + right - up;

            Gizmos.DrawLine(origin, topLeft);
            Gizmos.DrawLine(origin, topRight);
            Gizmos.DrawLine(origin, bottomLeft);
            Gizmos.DrawLine(origin, bottomRight);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(origin, active.captureRadius);

            // The near-field intake: inside this, anything in front of the mouth is drawn in
            // regardless of the cone.
            if (active.intakeRadius > 0f)
            {
                Gizmos.color = new Color(0.5f, 1f, 0.7f, 0.35f);
                Gizmos.DrawWireSphere(origin, active.intakeRadius);
            }
        }
    }
}
