using System;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Broad classification of anything the vacuum can interact with.</summary>
    [Flags]
    public enum VacuumCategory
    {
        None = 0,
        Paper = 1 << 0,
        Coin = 1 << 1,
        Debris = 1 << 2,
        SmallProp = 1 << 3,
        HeavyProp = 1 << 4,
        Furniture = 1 << 5,
        Enemy = 1 << 6,
        Special = 1 << 7,
        All = ~0
    }

    /// <summary>High level activation state of a vacuum.</summary>
    public enum VacuumState
    {
        Idle = 0,
        Sucking = 1
    }

    /// <summary>Why an object stopped being affected by suction.</summary>
    public enum VacuumReleaseReason
    {
        SuctionStopped,
        LeftCone,
        Occluded,
        OutOfRange,
        Captured,
        Rejected,
        SourceDisabled
    }

    /// <summary>Per-frame description of the suction being applied to one object.</summary>
    public struct VacuumSuctionContext
    {
        public VacuumController Controller;

        public Vector3 NozzleOrigin;

        public Vector3 NozzleDirection;

        public Vector3 NozzleUp;

        public Vector3 SamplePoint;

        public Vector3 DirectionToNozzle;

        public float Distance;

        public float DistanceNormalized;

        public float ConeFactor;

        public float Falloff;

        public float Intensity;

        public float Pull;

        public float DeltaTime;
    }

    /// <summary>Description of an in-progress capture.</summary>
    public struct VacuumCaptureContext
    {
        public VacuumController Controller;
        public Vector3 NozzleOrigin;
        public Quaternion NozzleRotation;

        public float Progress;

        public IVacuumAffectable Affectable;
    }

    /// <summary>Result of one detection pass for a single candidate.</summary>
    public struct VacuumTargetInfo
    {
        public IVacuumAffectable Affectable;
        public Vector3 SamplePoint;
        public Vector3 DirectionToNozzle;
        public float Distance;
        public float DistanceNormalized;
        public float ConeFactor;

        public bool IsValid => Affectable != null && Affectable.Transform != null;
    }
}
