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

        public Vector3 AimPointAtHeight(float height) => aim != null ? aim.AimPointAtHeight(height) : AimPoint;

        public bool TryGetAimSurface(out Vector3 point, out Vector3 normal)
        {
            if (aim != null)
            {
                return aim.TryGetAimSurface(out point, out normal);
            }

            point = AimPoint;
            normal = Vector3.up;
            return false;
        }

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
                CycleSlot = input.CycleDirection,
                DashPressed = input.DashPressed,
                MoveDirection = controller != null ? controller.MoveIntent : Vector3.zero
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

        public void Dash(Vector3 velocity, float seconds)
        {
            controller?.Dash(velocity, seconds);
        }
    }
}
