using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>
    /// Shared plumbing for anything the vacuum can grab: the designer-facing properties, collider
    /// registration, capture-handler discovery and a transform-only swallow animation.
    /// </summary>
    public abstract class VacuumAffectableBase : MonoBehaviour, IVacuumAffectable
    {
        [Header("Vacuum")]
        [Tooltip("Master switch. False removes this object from the vacuum entirely.")]
        [SerializeField] protected bool vacuumable = true;

        [SerializeField] protected VacuumCategory vacuumCategory = VacuumCategory.SmallProp;

        [Tooltip("How hard this is to pull, independent of the rigidbody mass. Paper 0.1, coin 0.2, chair 20, wardrobe 400.")]
        [Min(0.0001f)] [SerializeField] protected float vacuumMass = 1f;

        [Tooltip("0 = no resistance, 1 = immune to suction.")]
        [Range(0f, 1f)] [SerializeField] protected float suctionResistance;

        [Tooltip("Extra capture radius on top of the nozzle's own.")]
        [Min(0f)] [SerializeField] protected float captureRadiusBonus;

        [Tooltip("Whether reaching the mouth swallows this object.")]
        [SerializeField] protected bool canBeCaptured = true;

        [Tooltip("Use the nearest point on the collider bounds instead of the pivot, so big objects are pulled from their near edge.")]
        [SerializeField] protected bool sampleFromColliderBounds = true;

        protected readonly List<Collider> ownColliders = new List<Collider>();
        protected readonly List<IVacuumCaptureHandler> captureHandlers = new List<IVacuumCaptureHandler>();

        private int suctionSourceCount;
        private bool isCaptured;
        private Vector3 captureStartPosition;
        private Quaternion captureStartRotation;
        private Vector3 preCaptureScale = Vector3.one;

        public Transform Transform => this != null ? transform : null;

        public VacuumCategory Category => vacuumCategory;

        public virtual bool IsVacuumable => vacuumable && enabled && !isCaptured;

        public virtual bool CanBeCaptured => canBeCaptured;

        public virtual float VacuumMass => Mathf.Max(0.0001f, vacuumMass);

        public virtual float SuctionResistance => Mathf.Clamp01(suctionResistance);

        public virtual float CaptureRadius => captureRadiusBonus;

        public bool IsBeingSucked => suctionSourceCount > 0;

        public bool IsCaptured => isCaptured;

        public virtual Vector3 SuctionCenter => transform.position;

        protected Vector3 PreCaptureScale => preCaptureScale;

        protected virtual void Awake()
        {
            preCaptureScale = transform.localScale;
            CollectColliders();
            CollectCaptureHandlers();
        }

        protected virtual void OnEnable()
        {
            VacuumRegistry.Register(this, ownColliders);
        }

        protected virtual void OnDisable()
        {
            VacuumRegistry.Unregister(this);
            suctionSourceCount = 0;
        }

        public virtual Vector3 GetSuctionSamplePoint(Vector3 nozzleOrigin)
        {
            if (!sampleFromColliderBounds || ownColliders.Count == 0)
            {
                return SuctionCenter;
            }

            Collider primary = ownColliders[0];
            if (primary == null)
            {
                return SuctionCenter;
            }

            return primary.bounds.ClosestPoint(nozzleOrigin);
        }

        public virtual void OnSuctionBegin(in VacuumSuctionContext context)
        {
            suctionSourceCount++;
            if (suctionSourceCount == 1)
            {
                OnFirstSuctionSource(context);
            }
        }

        public abstract void OnSuctionTick(in VacuumSuctionContext context);

        public virtual void OnSuctionEnd(VacuumReleaseReason reason)
        {
            suctionSourceCount = Mathf.Max(0, suctionSourceCount - 1);
            if (suctionSourceCount == 0)
            {
                OnLastSuctionSourceRemoved(reason);
            }
        }

        protected virtual void OnFirstSuctionSource(in VacuumSuctionContext context)
        {
        }

        protected virtual void OnLastSuctionSourceRemoved(VacuumReleaseReason reason)
        {
        }

        public virtual bool TryBeginCapture(in VacuumCaptureContext context)
        {
            if (!canBeCaptured || isCaptured)
            {
                return false;
            }

            for (int i = 0; i < captureHandlers.Count; i++)
            {
                if (captureHandlers[i] != null && !captureHandlers[i].AllowCapture(context))
                {
                    return false;
                }
            }

            isCaptured = true;
            captureStartPosition = transform.position;
            captureStartRotation = transform.rotation;
            preCaptureScale = transform.localScale;

            EnterCaptureState(context);

            for (int i = 0; i < captureHandlers.Count; i++)
            {
                captureHandlers[i]?.OnCaptureBegin(context);
            }

            VacuumRegistry.Unregister(this);
            return true;
        }

        public virtual void OnCaptureProgress(in VacuumCaptureContext context)
        {
            float eased = Mathf.SmoothStep(0f, 1f, context.Progress);
            ApplyCaptureMotion(context, eased);
        }

        public virtual void OnCaptureCompleted(in VacuumCaptureContext context)
        {
            for (int i = 0; i < captureHandlers.Count; i++)
            {
                captureHandlers[i]?.OnCaptureCompleted(context);
            }

            transform.localScale = preCaptureScale;
            ApplyCaptureOutcome(context);
        }

        public virtual void OnCaptureCancelled(in VacuumCaptureContext context)
        {
            isCaptured = false;
            transform.localScale = preCaptureScale;
            ExitCaptureState(context);

            for (int i = 0; i < captureHandlers.Count; i++)
            {
                captureHandlers[i]?.OnCaptureCancelled(context);
            }

            if (isActiveAndEnabled)
            {
                VacuumRegistry.Register(this, ownColliders);
            }
        }

        protected virtual void EnterCaptureState(in VacuumCaptureContext context)
        {
        }

        protected virtual void ExitCaptureState(in VacuumCaptureContext context)
        {
        }

        protected virtual void ApplyCaptureMotion(in VacuumCaptureContext context, float eased)
        {
            Vector3 position = Vector3.Lerp(captureStartPosition, context.NozzleOrigin, eased);
            Quaternion spin = Quaternion.AngleAxis(eased * 540f, context.NozzleRotation * Vector3.forward);
            transform.SetPositionAndRotation(position, spin * captureStartRotation);
            transform.localScale = Vector3.Lerp(preCaptureScale, preCaptureScale * 0.05f, eased);
        }

        protected virtual void ApplyCaptureOutcome(in VacuumCaptureContext context)
        {
            gameObject.SetActive(false);
        }

        protected void CollectColliders()
        {
            ownColliders.Clear();
            Collider[] candidates = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (OwnsCollider(candidates[i]))
                {
                    ownColliders.Add(candidates[i]);
                }
            }
        }

        protected void CollectCaptureHandlers()
        {
            captureHandlers.Clear();
            GetComponentsInChildren(true, captureHandlers);
        }

        private bool OwnsCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform cursor = collider.transform;
            while (cursor != null)
            {
                if (cursor == transform)
                {
                    return true;
                }

                IVacuumAffectable other = cursor.GetComponent<IVacuumAffectable>();
                if (other != null && !ReferenceEquals(other, this))
                {
                    return false;
                }

                cursor = cursor.parent;
            }

            return false;
        }
    }
}
