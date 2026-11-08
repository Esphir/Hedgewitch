using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Anything the vacuum can pull on.</summary>
    public interface IVacuumAffectable
    {
        Transform Transform { get; }

        VacuumCategory Category { get; }

        bool IsVacuumable { get; }

        bool CanBeCaptured { get; }

        float VacuumMass { get; }

        float SuctionResistance { get; }

        float CaptureRadius { get; }

        bool IsBeingSucked { get; }

        bool IsCaptured { get; }

        Vector3 SuctionCenter { get; }

        Vector3 GetSuctionSamplePoint(Vector3 nozzleOrigin);

        void OnSuctionBegin(in VacuumSuctionContext context);

        void OnSuctionTick(in VacuumSuctionContext context);

        void OnSuctionEnd(VacuumReleaseReason reason);

        bool TryBeginCapture(in VacuumCaptureContext context);

        void OnCaptureProgress(in VacuumCaptureContext context);

        void OnCaptureCompleted(in VacuumCaptureContext context);

        void OnCaptureCancelled(in VacuumCaptureContext context);
    }
}
