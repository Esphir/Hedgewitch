// Plays the dash animation clips on the witch and her broom and places the wind and dust effects
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat
{
    // The animator runs only during a dash so between dashes the broom's swing code owns the broom
    [DisallowMultipleComponent]
    public class DashAnimator : MonoBehaviour
    {
        [Header("Wiring (found on this object if left empty)")]
        [SerializeField] private PlayerDash dash;
        [SerializeField] private WeaponInventory inventory;

        [Tooltip("Visual-only body the clips move. Must not hold the player's collider.")]
        [SerializeField] private Transform body;

        [Header("Animation")]
        [Tooltip("Plays the dash clips. Switched on only while dashing. Without one the poses are worked out in code.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animator state played for the musket vacuum and empty-handed dash.")]
        [SerializeField] private string dashState = "Dash";

        [SerializeField] private string broomRideState = "DashBroomRide";
        [SerializeField] private string broomSweepState = "DashBroomSweep";

        [Tooltip("Speeds each clip up or down to last exactly as long as the dash, so changing dash times needs no new clips.")]
        [SerializeField] private bool matchDashDuration = true;

        [Header("Effects")]
        [Tooltip("Streaks blown off the back of the riding broom.")]
        [SerializeField] private ParticleSystem wind;

        [Tooltip("Dust kicked up by the sweeping bristles and the regular dash.")]
        [SerializeField] private ParticleSystem dust;

        [Min(0)] [SerializeField] private int regularDustBurst = 10;

        [Header("Poses")]
        [Tooltip("The shapes the clips are baked from, also used when there is no animator.")]
        [SerializeField] private DashPoseSettings poses = new DashPoseSettings();

        private readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        private DashRig rig;
        private Transform broomPivot;
        private Vector3 broomRestPosition;
        private bool posing;

        public bool IsPosing => posing;

        public bool UsesClips => animator != null && animator.runtimeAnimatorController != null;

        public Animator Animator => animator;

        public DashPoseSettings Poses => poses;

        // Where the broom's bristle end is right now for effects and tests
        public Vector3 BristleTip => broomPivot != null ? broomPivot.TransformPoint(0f, 0f, poses.bristleTipDistance) : transform.position;

        public Transform Body => body;

        public ParticleSystem Wind => wind;

        public ParticleSystem Dust => dust;

        public string StateFor(DashStyle style)
        {
            switch (style)
            {
                case DashStyle.BroomRide:
                    return broomRideState;
                case DashStyle.BroomSweep:
                    return broomSweepState;
                default:
                    return dashState;
            }
        }

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

            BroomWeapon broom = inventory != null ? inventory.GetWeapon<BroomWeapon>() : GetComponentInChildren<BroomWeapon>(true);
            broomPivot = broom != null ? broom.SwingPivot : null;
            if (broomPivot != null)
            {
                broomRestPosition = broomPivot.localPosition;
            }

            rig = DashRig.Capture(transform, body, broomPivot);

            if (animator != null)
            {
                animator.enabled = false;
                if (animator.runtimeAnimatorController != null)
                {
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    {
                        if (clip != null)
                        {
                            clipLengths[clip.name] = clip.length;
                        }
                    }
                }
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
            if (UsesClips)
            {
                PlayClip(style, source.Duration);
            }

            if (style == DashStyle.Regular && dust != null && regularDustBurst > 0)
            {
                dust.transform.position = transform.TransformPoint(0f, rig.FootHeight + 0.05f, 0f);
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

        private void PlayClip(DashStyle style, float duration)
        {
            string state = StateFor(style);
            animator.enabled = true;
            animator.speed = matchDashDuration && duration > 0f && clipLengths.TryGetValue(state, out float length)
                ? length / duration
                : 1f;
            animator.Play(state, 0, 0f);
            animator.Update(0f);
        }

        // Runs after the animator so effects follow the clip-posed broom
        private void LateUpdate()
        {
            if (dash == null || !dash.IsDashing || !posing)
            {
                return;
            }

            if (!UsesClips)
            {
                ApplyPose(DashPoseSolver.Evaluate(dash.Style, dash.Elapsed, dash.Duration, poses, rig));
            }

            if (dash.Style == DashStyle.BroomRide)
            {
                PlaceEffect(wind, BristleTip, -dash.Direction);
            }
            else if (dash.Style == DashStyle.BroomSweep)
            {
                PlaceEffect(dust, BristleTip, -dash.Direction);
            }
        }

        private void ApplyPose(in DashPose pose)
        {
            if (body != null)
            {
                body.localPosition = pose.BodyPosition;
                body.localRotation = pose.BodyRotation;
                body.localScale = pose.BodyScale;
            }

            if (pose.MovesBroom && broomPivot != null && broomPivot.gameObject.activeInHierarchy)
            {
                broomPivot.SetPositionAndRotation(transform.TransformPoint(pose.BroomPosition), transform.rotation * pose.BroomRotation);
            }
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

        // Clips end on the rest pose but a dash cut short by a hit needs putting back by hand
        private void Restore()
        {
            if (!posing)
            {
                return;
            }

            posing = false;
            if (animator != null)
            {
                animator.enabled = false;
            }

            if (body != null)
            {
                body.localPosition = rig.BodyPosition;
                body.localRotation = rig.BodyRotation;
                body.localScale = rig.BodyScale;
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
