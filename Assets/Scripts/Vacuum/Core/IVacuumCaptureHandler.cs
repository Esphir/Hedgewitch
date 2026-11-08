namespace Hedgewitch.Vacuum
{
    /// <summary>
    /// Decides what actually happens to an object once the vacuum has swallowed it.
    /// </summary>
    public interface IVacuumCaptureHandler
    {
        bool AllowCapture(in VacuumCaptureContext context);

        void OnCaptureBegin(in VacuumCaptureContext context);

        void OnCaptureCompleted(in VacuumCaptureContext context);

        void OnCaptureCancelled(in VacuumCaptureContext context);
    }
}
