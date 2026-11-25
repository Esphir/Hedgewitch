// Placeholder dash poses and effects on the primitive witch until real animation clips exist
using UnityEngine;

namespace Hedgewitch.Combat
{
    // Sits on the player root and poses the body and broom in LateUpdate after the broom has posed itself
    [DisallowMultipleComponent]
    public class DashAnimator : MonoBehaviour
    {
        [Header("Wiring (found on this object if left empty)")]
        [SerializeField] private PlayerDash dash;
        [SerializeField] private WeaponInventory inventory;

        [Tooltip("Visual-only body that leans rolls and lifts. Must not hold the player's collider.")]
        [SerializeField] private Transform body;

        [Header("Animator (optional)")]
        [Tooltip("When set each dash fires the trigger for its style so real clips can take over.")]
        [SerializeField] private Animator animator;

        [SerializeField] private string dashTrigger = "Dash";
        [SerializeField] private string broomRideTrigger = "DashBroomRide";
        [SerializeField] private string broomSweepTrigger = "DashBroomSweep";

        [Tooltip("Turn off once animation clips drive the body and broom.")]
        [SerializeField] private bool proceduralPoses = true;

        [Header("Effects")]
        [Tooltip("Streaks blown off the back of the riding broom.")]
        [SerializeField] private ParticleSystem wind;

        [Tooltip("Dust kicked up by the sweeping bristles and the regular dash.")]
        [SerializeField] private ParticleSystem dust;

        [Min(0)] [SerializeField] private int regularDustBurst = 10;

        [Header("Blending")]
        [Min(0.001f)] [SerializeField] private float blendIn = 0.05f;
        [Min(0.001f)] [SerializeField] private float blendOut = 0.08f;

        [Header("Dash")]
        [SerializeField] private float rollDegrees = 360f;
        [Range(0.3f, 1f)] [SerializeField] private float rollCrouch = 0.72f;

        [Header("Broom ride")]
        [Tooltip("How high the broom floats while riding.")]
        [SerializeField] private float rideLift = 0.35f;
        [Range(0.3f, 1f)] [SerializeField] private float rideSitSquash = 0.8f;
        [SerializeField] private float rideLean = 14f;
        [SerializeField] private float rideBobHeight = 0.04f;
        [SerializeField] private float rideBobRate = 18f;

        [Header("Broom sweep")]
        [Tooltip("How far in front of the player's centre the bristles touch the ground.")]
        [SerializeField] private float sweepForward = 0.15f;

        [Tooltip("Degrees the upright handle tips forward so it clears the body.")]
        [SerializeField] private float sweepHandleTilt = 14f;

        [Tooltip("How far behind the bristle head the body stands.")]
        [SerializeField] private float sweepStandBack = 0.2f;
        [Min(0.5f)] [SerializeField] private float sweepStrokes = 2f;
        [SerializeField] private float sweepAngle = 14f;
        [SerializeField] private float sweepLean = 6f;

        // Distances along the broom from its pivot matching the built broom model
        [Header("Broom model")]
        [SerializeField] private float bristleTopDistance = 1.27f;
        [SerializeField] private float bristleTipDistance = 1.63f;

        private Vector3 bodyRestPosition;
        private Quaternion bodyRestRotation = Quaternion.identity;
        private Vector3 bodyRestScale = Vector3.one;
        private float bodyHalfHeight = 1f;
        private float footHeight = -1f;

        private Transform broomPivot;
        private Vector3 broomRestPosition;
        private bool posing;

        public bool IsPosing => posing;

        // Where the broom's bristle end is right now for effects and tests
        public Vector3 BristleTip => broomPivot != null ? broomPivot.TransformPoint(0f, 0f, bristleTipDistance) : transform.position;

        public Transform Body => body;

        public ParticleSystem Wind => wind;

        public ParticleSystem Dust => dust;

        private void Awake()
        {
            if (dash == null)
            {
                dash = GetComponent<PlayerDash>();
            }

            if (inventory == null)
            {
                inventory = GetComponent<WeaponInventory>();
            }

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                footHeight = capsule.center.y - capsule.height * 0.5f;
            }

            if (body != null)
            {
                bodyRestPosition = body.localPosition;
                bodyRestRotation = body.localRotation;
                bodyRestScale = body.localScale;
                MeshFilter mesh = body.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    bodyHalfHeight = mesh.sharedMesh.bounds.extents.y * bodyRestScale.y;
                }
            }

            BroomWeapon broom = inventory != null ? inventory.GetWeapon<BroomWeapon>() : GetComponentInChildren<BroomWeapon>(true);
            broomPivot = broom != null ? broom.SwingPivot : null;
            if (broomPivot != null)
            {
                broomRestPosition = broomPivot.localPosition;
            }

            SetEmitting(wind, false);
            SetEmitting(dust, false);
        }

        private void OnEnable()
        {
            if (dash != null)
            {
                dash.Started += HandleStarted;
                dash.Ended += HandleEnded;
            }
        }

        private void OnDisable()
        {
            if (dash != null)
            {
                dash.Started -= HandleStarted;
                dash.Ended -= HandleEnded;
            }

            Restore();
        }

        private void HandleStarted(PlayerDash source, DashStyle style)
        {
            if (animator != null)
            {
                string trigger = style == DashStyle.BroomRide ? broomRideTrigger
                    : style == DashStyle.BroomSweep ? broomSweepTrigger
                    : dashTrigger;
                if (!string.IsNullOrEmpty(trigger))
                {
                    animator.SetTrigger(trigger);
                }
            }

            if (style == DashStyle.Regular && dust != null && regularDustBurst > 0)
            {
                dust.transform.position = transform.TransformPoint(0f, footHeight + 0.05f, 0f);
                dust.Emit(regularDustBurst);
            }

            SetEmitting(wind, style == DashStyle.BroomRide);
            SetEmitting(dust, style == DashStyle.BroomSweep);
            posing = true;
        }

        private void HandleEnded(PlayerDash source, DashStyle style)
        {
            SetEmitting(wind, false);
            SetEmitting(dust, false);
            Restore();
        }

        private void LateUpdate()
        {
            if (dash == null || !dash.IsDashing || !posing)
            {
                return;
            }

            float elapsed = dash.Elapsed;
            float weight = Mathf.Clamp01(elapsed / blendIn) * Mathf.Clamp01((dash.Duration - elapsed) / blendOut);
            weight = weight * weight * (3f - 2f * weight);
            float progress = dash.Progress;

            switch (dash.Style)
            {
                case DashStyle.BroomRide:
                    PoseRide(elapsed, weight);
                    break;
                case DashStyle.BroomSweep:
                    PoseSweep(progress, weight);
                    break;
                default:
                    PoseRoll(progress, weight);
                    break;
            }
        }

        private void PoseRoll(float progress, float weight)
        {
            if (!proceduralPoses || body == null)
            {
                return;
            }

            float eased = progress * progress * (3f - 2f * progress);
            Quaternion roll = Quaternion.AngleAxis(eased * rollDegrees, Vector3.right);
            Vector3 scale = Vector3.Scale(bodyRestScale, new Vector3(1.05f, rollCrouch, 1.05f));

            // Lower the squashed body so it still touches the ground
            Vector3 position = bodyRestPosition + Vector3.up * (bodyHalfHeight * (rollCrouch - 1f));

            ApplyBody(position, roll * bodyRestRotation, scale, weight);
        }

        private void PoseRide(float elapsed, float weight)
        {
            float bob = Mathf.Sin(elapsed * rideBobRate) * rideBobHeight;
            float lift = rideLift + bob;
            float seatHeight = bodyRestPosition.y + lift - bodyHalfHeight * rideSitSquash;

            if (proceduralPoses)
            {
                if (body != null)
                {
                    Vector3 scale = Vector3.Scale(bodyRestScale, new Vector3(1.05f, rideSitSquash, 1.05f));
                    Quaternion lean = Quaternion.AngleAxis(rideLean, Vector3.right);
                    ApplyBody(bodyRestPosition + Vector3.up * lift, lean * bodyRestRotation, scale, weight);
                }

                // Handle tip out front and bristles trailing behind the seat
                Vector3 pivot = new Vector3(0f, seatHeight, 0.8f);
                Quaternion turn = Quaternion.Euler(-4f, 180f, 0f);
                ApplyBroom(pivot, turn, weight);
            }

            PlaceEffect(wind, BristleTip, -dash.Direction);
        }

        private void PoseSweep(float progress, float weight)
        {
            // Bristle tip on the ground with the handle tipped forward away from the body
            Quaternion turn = Quaternion.Euler(90f + sweepHandleTilt, 0f, 0f);
            Vector3 contact = new Vector3(0f, footHeight + 0.02f, sweepForward);
            Vector3 pivot = contact - turn * new Vector3(0f, 0f, bristleTipDistance);
            Vector3 bristleTop = pivot + turn * new Vector3(0f, 0f, bristleTopDistance);

            // Feet on the wood the bristles hang from
            Vector3 standing = new Vector3(
                bodyRestPosition.x,
                bodyRestPosition.y + bristleTop.y - footHeight + 0.02f,
                bristleTop.z - sweepStandBack);

            // The whole rig rocks about where the bristles touch the ground
            float stroke = Mathf.Sin(progress * sweepStrokes * Mathf.PI * 2f) * sweepAngle;
            Quaternion rock = Quaternion.AngleAxis(stroke, Vector3.right);

            if (proceduralPoses)
            {
                if (body != null)
                {
                    Quaternion lean = Quaternion.AngleAxis(sweepLean, Vector3.right);
                    ApplyBody(contact + rock * (standing - contact), rock * lean * bodyRestRotation, bodyRestScale, weight);
                }

                ApplyBroom(contact + rock * (pivot - contact), rock * turn, weight);
            }

            PlaceEffect(dust, BristleTip, -dash.Direction);
        }

        private void ApplyBody(Vector3 localPosition, Quaternion localRotation, Vector3 localScale, float weight)
        {
            body.localPosition = Vector3.LerpUnclamped(bodyRestPosition, localPosition, weight);
            body.localRotation = Quaternion.SlerpUnclamped(bodyRestRotation, localRotation, weight);
            body.localScale = Vector3.LerpUnclamped(bodyRestScale, localScale, weight);
        }

        // Pose is in the player's local space and blends from wherever the broom put itself this frame
        private void ApplyBroom(Vector3 localPosition, Quaternion localRotation, float weight)
        {
            if (broomPivot == null || !broomPivot.gameObject.activeInHierarchy)
            {
                return;
            }

            Transform parent = broomPivot.parent;
            Vector3 restWorld = parent != null ? parent.TransformPoint(broomRestPosition) : broomRestPosition;
            Vector3 targetWorld = transform.TransformPoint(localPosition);
            Quaternion targetRotation = transform.rotation * localRotation;

            broomPivot.position = Vector3.LerpUnclamped(restWorld, targetWorld, weight);
            broomPivot.rotation = Quaternion.SlerpUnclamped(broomPivot.rotation, targetRotation, weight);
        }

        private static void PlaceEffect(ParticleSystem effect, Vector3 position, Vector3 facing)
        {
            if (effect == null)
            {
                return;
            }

            effect.transform.position = position;
            if (facing.sqrMagnitude > 1e-4f)
            {
                effect.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
        }

        private void Restore()
        {
            if (!posing)
            {
                return;
            }

            posing = false;
            if (body != null)
            {
                body.localPosition = bodyRestPosition;
                body.localRotation = bodyRestRotation;
                body.localScale = bodyRestScale;
            }

            if (broomPivot != null)
            {
                broomPivot.localPosition = broomRestPosition;
            }
        }

        private static void SetEmitting(ParticleSystem effect, bool on)
        {
            if (effect == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = effect.emission;
            emission.enabled = on;
            if (on && !effect.isPlaying)
            {
                effect.Play();
            }
        }
    }
}
