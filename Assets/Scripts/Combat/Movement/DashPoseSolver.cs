// The dash poses as plain maths so the clip baker and the no-animator fallback make the same moves
using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    [Serializable]
    public class DashPoseSettings
    {
        [Header("Blending")]
        [Min(0.001f)] public float blendIn = 0.05f;
        [Min(0.001f)] public float blendOut = 0.08f;

        [Header("Dash")]
        public float rollDegrees = 360f;
        [Range(0.3f, 1f)] public float rollCrouch = 0.72f;

        [Header("Broom ride")]
        [Tooltip("How high the broom floats while riding.")]
        public float rideLift = 0.35f;
        [Range(0.3f, 1f)] public float rideSitSquash = 0.8f;
        public float rideLean = 14f;
        public float rideBobHeight = 0.04f;
        public float rideBobRate = 18f;

        [Header("Broom sweep")]
        [Tooltip("How far in front of the player's centre the bristles touch the ground.")]
        public float sweepForward = 0.15f;

        [Tooltip("Degrees the upright handle tips forward so it clears the body.")]
        public float sweepHandleTilt = 14f;

        [Tooltip("How far behind the bristle head the body stands.")]
        public float sweepStandBack = 0.2f;
        [Min(0.5f)] public float sweepStrokes = 2f;
        public float sweepAngle = 14f;
        public float sweepLean = 6f;

        [Header("Broom model")]
        [Tooltip("Distance along the broom from its pivot to where the bristles join the wood.")]
        public float bristleTopDistance = 1.27f;

        [Tooltip("Distance along the broom from its pivot to the bristle tips.")]
        public float bristleTipDistance = 1.63f;
    }

    // Rest pose of the witch in the player's local space
    public struct DashRig
    {
        public Vector3 BodyPosition;
        public Quaternion BodyRotation;
        public Vector3 BodyScale;
        public float BodyHalfHeight;
        public float FootHeight;
        public bool HasBroom;
        public Vector3 BroomPosition;
        public Quaternion BroomRotation;

        public static DashRig Capture(Transform root, Transform body, Transform broomPivot)
        {
            DashRig rig = new DashRig
            {
                BodyRotation = Quaternion.identity,
                BodyScale = Vector3.one,
                BodyHalfHeight = 1f,
                FootHeight = -1f,
                BroomRotation = Quaternion.identity
            };

            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                rig.FootHeight = capsule.center.y - capsule.height * 0.5f;
            }

            if (body != null)
            {
                rig.BodyPosition = body.localPosition;
                rig.BodyRotation = body.localRotation;
                rig.BodyScale = body.localScale;
                MeshFilter mesh = body.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    rig.BodyHalfHeight = mesh.sharedMesh.bounds.extents.y * rig.BodyScale.y;
                }
            }

            if (broomPivot != null)
            {
                rig.HasBroom = true;
                rig.BroomPosition = root.InverseTransformPoint(broomPivot.position);
                rig.BroomRotation = Quaternion.Inverse(root.rotation) * broomPivot.rotation;
            }

            return rig;
        }
    }

    // Body pose is local to the player root and so is the broom pose
    public struct DashPose
    {
        public Vector3 BodyPosition;
        public Quaternion BodyRotation;
        public Vector3 BodyScale;
        public bool MovesBroom;
        public Vector3 BroomPosition;
        public Quaternion BroomRotation;
    }

    public static class DashPoseSolver
    {
        // Blends in from rest and back out so the first and last frames match the rest pose
        public static DashPose Evaluate(DashStyle style, float elapsed, float duration, DashPoseSettings settings, in DashRig rig)
        {
            float weight = Mathf.Clamp01(elapsed / settings.blendIn) * Mathf.Clamp01((duration - elapsed) / settings.blendOut);
            weight = weight * weight * (3f - 2f * weight);
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            DashPose target;
            switch (style)
            {
                case DashStyle.BroomRide:
                    target = Ride(elapsed, settings, rig);
                    break;
                case DashStyle.BroomSweep:
                    target = Sweep(progress, settings, rig);
                    break;
                default:
                    target = Roll(progress, settings, rig);
                    break;
            }

            return new DashPose
            {
                BodyPosition = Vector3.LerpUnclamped(rig.BodyPosition, target.BodyPosition, weight),
                BodyRotation = Quaternion.SlerpUnclamped(rig.BodyRotation, target.BodyRotation, weight),
                BodyScale = Vector3.LerpUnclamped(rig.BodyScale, target.BodyScale, weight),
                MovesBroom = target.MovesBroom && rig.HasBroom,
                BroomPosition = Vector3.LerpUnclamped(rig.BroomPosition, target.BroomPosition, weight),
                BroomRotation = Quaternion.SlerpUnclamped(rig.BroomRotation, target.BroomRotation, weight)
            };
        }

        private static DashPose Roll(float progress, DashPoseSettings s, in DashRig rig)
        {
            float eased = progress * progress * (3f - 2f * progress);

            // Lower the squashed body so it still touches the ground
            return new DashPose
            {
                BodyPosition = rig.BodyPosition + Vector3.up * (rig.BodyHalfHeight * (s.rollCrouch - 1f)),
                BodyRotation = Quaternion.AngleAxis(eased * s.rollDegrees, Vector3.right) * rig.BodyRotation,
                BodyScale = Vector3.Scale(rig.BodyScale, new Vector3(1.05f, s.rollCrouch, 1.05f)),
                BroomPosition = rig.BroomPosition,
                BroomRotation = rig.BroomRotation
            };
        }

        private static DashPose Ride(float elapsed, DashPoseSettings s, in DashRig rig)
        {
            float lift = s.rideLift + Mathf.Sin(elapsed * s.rideBobRate) * s.rideBobHeight;
            float seatHeight = rig.BodyPosition.y + lift - rig.BodyHalfHeight * s.rideSitSquash;

            // Handle tip out front and bristles trailing behind the seat
            return new DashPose
            {
                BodyPosition = rig.BodyPosition + Vector3.up * lift,
                BodyRotation = Quaternion.AngleAxis(s.rideLean, Vector3.right) * rig.BodyRotation,
                BodyScale = Vector3.Scale(rig.BodyScale, new Vector3(1.05f, s.rideSitSquash, 1.05f)),
                MovesBroom = true,
                BroomPosition = new Vector3(0f, seatHeight, 0.8f),
                BroomRotation = Quaternion.Euler(-4f, 180f, 0f)
            };
        }

        private static DashPose Sweep(float progress, DashPoseSettings s, in DashRig rig)
        {
            // Bristle tip on the ground with the handle tipped forward away from the body
            Quaternion turn = Quaternion.Euler(90f + s.sweepHandleTilt, 0f, 0f);
            Vector3 contact = new Vector3(0f, rig.FootHeight + 0.02f, s.sweepForward);
            Vector3 pivot = contact - turn * new Vector3(0f, 0f, s.bristleTipDistance);
            Vector3 bristleTop = pivot + turn * new Vector3(0f, 0f, s.bristleTopDistance);

            // Feet on the wood the bristles hang from
            Vector3 standing = new Vector3(
                rig.BodyPosition.x,
                rig.BodyPosition.y + bristleTop.y - rig.FootHeight + 0.02f,
                bristleTop.z - s.sweepStandBack);

            // The whole rig rocks about where the bristles touch the ground
            float stroke = Mathf.Sin(progress * s.sweepStrokes * Mathf.PI * 2f) * s.sweepAngle;
            Quaternion rock = Quaternion.AngleAxis(stroke, Vector3.right);
            Quaternion lean = Quaternion.AngleAxis(s.sweepLean, Vector3.right);

            return new DashPose
            {
                BodyPosition = contact + rock * (standing - contact),
                BodyRotation = rock * lean * rig.BodyRotation,
                BodyScale = rig.BodyScale,
                MovesBroom = true,
                BroomPosition = contact + rock * (pivot - contact),
                BroomRotation = rock * turn
            };
        }
    }
}
