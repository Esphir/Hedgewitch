// Puts the vacuum on the weapon belt so it only sucks while in hand
using UnityEngine;
using Hedgewitch.Vacuum;

namespace Hedgewitch.Combat
{
    // Swallowed spirits count as defeats in the weapon events
    public class VacuumWeapon : Weapon
    {
        [SerializeField] private VacuumController controller;

        public override WeaponSlot Slot => WeaponSlot.Vacuum;

        public override bool IsBusy => controller != null && controller.IsSucking;

        public VacuumController Controller => controller;

        protected override void Awake()
        {
            base.Awake();

            if (controller == null)
            {
                controller = GetComponentInParent<VacuumController>();
                if (controller == null && transform.root != null)
                {
                    controller = transform.root.GetComponentInChildren<VacuumController>();
                }
            }
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.ObjectCaptured += HandleObjectCaptured;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.ObjectCaptured -= HandleObjectCaptured;
                controller.SetSuction(false);
            }
        }

        public override void TickInput(in CombatInputFrame input)
        {
            if (controller != null)
            {
                controller.SetSuction(input.PrimaryHeld);
            }
        }

        protected override void OnUnequipped()
        {
            if (controller != null)
            {
                controller.SetSuction(false);
            }
        }

        private void HandleObjectCaptured(IVacuumAffectable affectable)
        {
            if (affectable == null || affectable.Category != VacuumCategory.Enemy || affectable.Transform == null)
            {
                return;
            }

            IDamageable damageable = affectable.Transform.GetComponentInParent<IDamageable>();
            Vector3 nozzle = controller.Nozzle != null ? controller.Nozzle.Origin : transform.position;

            RaiseDefeated(damageable, new DamageInfo
            {
                Amount = 0f,
                Point = nozzle,
                Direction = nozzle - affectable.Transform.position,
                Tags = DamageTags.Vacuum,
                Source = Owner != null ? Owner.gameObject : transform.root.gameObject,
                Weapon = this
            });
        }
    }
}
