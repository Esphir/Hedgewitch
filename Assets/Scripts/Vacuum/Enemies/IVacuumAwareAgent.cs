namespace Hedgewitch.Vacuum.Enemies
{
    /// <summary>
    /// Implemented by enemy brains so they can react to being vacuumed on their own terms.
    /// </summary>
    public interface IVacuumAwareAgent
    {
        bool WantsMovementControl { get; }

        void OnVacuumTargeted(in VacuumSuctionContext context);

        void OnVacuumPressure(float pressure, in VacuumSuctionContext context);

        void OnVacuumEscapeAttempt(float remainingStamina);

        void OnVacuumOverpowered();

        void OnVacuumReleased(VacuumReleaseReason reason);
    }
}
