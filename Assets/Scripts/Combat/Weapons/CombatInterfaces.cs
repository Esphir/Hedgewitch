// Interfaces that keep weapons separate from the character controller
using UnityEngine;

namespace Hedgewitch.Combat
{
    public struct CombatInputFrame
    {
        // Light attack fire or suck
        public bool PrimaryPressed;
        public bool PrimaryHeld;

        // Heavy attack
        public bool SecondaryPressed;
        public bool SecondaryHeld;

        // Slot picked this frame or -1
        public int SelectSlot;

        // Slot cycle direction this frame or 0
        public int CycleSlot;
    }

    public interface ICombatInput
    {
        CombatInputFrame ReadCombatInput();
    }

    public interface ICombatAim
    {
        bool HasAim { get; }

        Vector3 AimPoint { get; }
    }

    // Weapon requests that each expire on their own
    public interface ICombatMotor
    {
        void HoldFacing(Vector3 direction, float seconds);

        void HoldMoveScale(float scale, float seconds);

        void Lunge(Vector3 velocity, float seconds);
    }
}
