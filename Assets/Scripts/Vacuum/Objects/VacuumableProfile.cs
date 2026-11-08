using UnityEngine;

namespace Hedgewitch.Vacuum.Objects
{
    /// <summary>A reusable material-and-weight preset for vacuumable props.</summary>
    [CreateAssetMenu(menuName = "Hedgewitch/Vacuum/Vacuumable Profile", fileName = "VacuumableProfile")]
    public class VacuumableProfile : ScriptableObject
    {
        [Header("Identity")]
        public VacuumCategory category = VacuumCategory.SmallProp;

        [Tooltip("Score or currency banked when this object is swallowed.")]
        public int collectionValue;

        [Header("Suction response")]
        [Tooltip("How hard this is to pull. Lower is lighter.")]
        [Min(0.0001f)] public float vacuumMass = 1f;

        [Range(0f, 1f)] public float suctionResistance;

        public bool canBeCaptured = true;

        [Min(0f)] public float captureRadiusBonus;

        [Tooltip("Scales how much of gravity the airflow cancels for this object. Paper floats, anvils do not.")]
        [Range(0f, 2f)] public float liftMultiplier = 1f;

        [Tooltip("Scales the spiralling tangential force.")]
        [Range(0f, 3f)] public float swirlMultiplier = 1f;

        [Tooltip("Scales the tumbling torque. Paper flaps wildly, a crate barely turns.")]
        [Range(0f, 4f)] public float wobbleMultiplier = 1f;

        [Tooltip("Scales the cross-flow damping that funnels this object onto the nozzle. Higher tracks the mouth more tightly; 0 lets it sling past on its own momentum.")]
        [Range(0f, 3f)] public float funnelMultiplier = 1f;

        [Header("Rigidbody")]
        [Tooltip("Apply the rigidbody values below when this profile is applied.")]
        public bool overrideRigidbody = true;

        [Min(0.0001f)] public float rigidbodyMass = 1f;

        [Min(0f)] public float linearDamping;

        [Min(0f)] public float angularDamping = 0.05f;

        [Header("Outcome")]
        public VacuumCaptureOutcome captureOutcome = VacuumCaptureOutcome.Destroy;

        public void ApplyTo(VacuumableBody body)
        {
            if (body != null)
            {
                body.ApplyProfile(this);
            }
        }
    }
}
