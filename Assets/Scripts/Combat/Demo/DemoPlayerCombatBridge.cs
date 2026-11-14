// Connects the demo player to the combat interfaces and is the only combat class that knows about it
using UnityEngine;
using Hedgewitch.Vacuum.Demo;

namespace Hedgewitch.Combat.Demo
{
    [DisallowMultipleComponent]
    public class DemoPlayerCombatBridge : MonoBehaviour, ICombatInput, ICombatAim, ICombatMotor
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private VacuumAimController aim;
        [SerializeField] private IsometricPlayerController controller;

        public bool HasAim => aim != null && aim.HasAim;

        public Vector3 AimPoint => aim != null ? aim.AimPoint : transform.position + transform.forward;

        private void Awake()
        {
            if (input == null)
            {
                input = GetComponentInChildren<PlayerInputReader>();
                if (input == null)
                {
                    input = FindAnyObjectByType<PlayerInputReader>();
                }
            }

            if (aim == null)
            {
                aim = GetComponentInChildren<VacuumAimController>();
            }

            if (controller == null)
            {
                controller = GetComponentInChildren<IsometricPlayerController>();
            }

            // The weapon belt decides when the vacuum runs
            if (aim != null)
            {
                aim.DriveSuctionFromInput = false;
            }
        }

        public CombatInputFrame ReadCombatInput()
        {
            if (input == null)
            {
                return new CombatInputFrame { SelectSlot = -1 };
            }

            return new CombatInputFrame
            {
                PrimaryPressed = input.PrimaryPressed,
                PrimaryHeld = input.PrimaryHeld,
                SecondaryPressed = input.SecondaryPressed,
                SecondaryHeld = input.SecondaryHeld,
                SelectSlot = input.SelectedSlot,
                CycleSlot = input.CycleDirection
            };
        }

        public void HoldFacing(Vector3 direction, float seconds)
        {
            controller?.HoldFacing(direction, seconds);
        }

        public void HoldMoveScale(float scale, float seconds)
        {
            controller?.HoldMoveScale(scale, seconds);
        }

        public void Lunge(Vector3 velocity, float seconds)
        {
            controller?.Lunge(velocity, seconds);
        }
    }
}
