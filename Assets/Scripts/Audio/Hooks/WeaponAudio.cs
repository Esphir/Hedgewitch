// Plays broom musket and vacuum sounds from the weapon events on the witch's belt
using System.Collections;
using UnityEngine;
using Hedgewitch.Combat;
using Hedgewitch.Vacuum;

namespace Hedgewitch.Audio.Hooks
{
    [DisallowMultipleComponent]
    public class WeaponAudio : MonoBehaviour
    {
        [SerializeField] private BroomSfxProfile broom;
        [SerializeField] private GunSfxProfile gun;
        [SerializeField] private VacuumSfxProfile vacuum;

        private WeaponInventory inventory;
        private PlayerDash dash;
        private BroomWeapon[] brooms = new BroomWeapon[0];
        private MusketWeapon[] muskets = new MusketWeapon[0];
        private VacuumController vacuumController;
        private Coroutine vacuumSequence;
        private AudioHandle vacuumLoop;
        private bool vacuumPowered;
        private bool ready;

        private void Awake()
        {
            inventory = GetComponentInChildren<WeaponInventory>(true);
            dash = GetComponentInChildren<PlayerDash>(true);
            brooms = GetComponentsInChildren<BroomWeapon>(true);
            muskets = GetComponentsInChildren<MusketWeapon>(true);
            vacuumController = GetComponentInChildren<VacuumController>(true);
        }

        private void OnEnable()
        {
            ready = false;

            if (inventory != null)
            {
                inventory.WeaponChanged += HandleWeaponChanged;
            }

            if (dash != null)
            {
                dash.Started += HandleDash;
            }

            foreach (BroomWeapon weapon in brooms)
            {
                weapon.SwingStarted += HandleSwing;
                weapon.HitLanded += HandleBroomHit;
            }

            foreach (MusketWeapon weapon in muskets)
            {
                weapon.Fired += HandleShot;
                weapon.DryFired += HandleDryFire;
                weapon.Reloaded += HandleReloaded;
            }

            if (vacuumController != null)
            {
                vacuumController.StateChanged += HandleVacuumState;
                vacuumController.ObjectCaptured += HandleCaptured;
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.WeaponChanged -= HandleWeaponChanged;
            }

            if (dash != null)
            {
                dash.Started -= HandleDash;
            }

            foreach (BroomWeapon weapon in brooms)
            {
                weapon.SwingStarted -= HandleSwing;
                weapon.HitLanded -= HandleBroomHit;
            }

            foreach (MusketWeapon weapon in muskets)
            {
                weapon.Fired -= HandleShot;
                weapon.DryFired -= HandleDryFire;
                weapon.Reloaded -= HandleReloaded;
            }

            if (vacuumController != null)
            {
                vacuumController.StateChanged -= HandleVacuumState;
                vacuumController.ObjectCaptured -= HandleCaptured;
            }

            vacuumSequence = null;
            vacuumPowered = false;
            vacuumLoop.Stop(0.1f);
        }

        // Every Start has run by the first Update so the belt's opening equip stays silent
        private void Update()
        {
            ready = true;
        }

        private void HandleWeaponChanged(Weapon previous, Weapon next)
        {
            if (!ready || next == null)
            {
                return;
            }

            if (next is BroomWeapon)
            {
                Play(broom != null ? broom.Equip : null, next.transform.position);
            }
            else if (next is MusketWeapon)
            {
                Play(gun != null ? gun.Equip : null, next.transform.position);
            }
        }

        private void HandleDash(PlayerDash source, DashStyle style)
        {
            if (broom == null || style == DashStyle.Regular)
            {
                return;
            }

            Play(style == DashStyle.BroomRide ? broom.DashRide : broom.DashSweep, source.transform.position);
        }

        private void HandleSwing(BroomWeapon weapon, MeleeAttackStep step)
        {
            Play(broom != null ? broom.Attack : null, weapon.transform.position);
        }

        private void HandleBroomHit(Weapon weapon, IDamageable target, DamageInfo info, DamageResult result)
        {
            Play(broom != null ? broom.Hit : null, info.Point);
        }

        private void HandleShot(MusketWeapon weapon)
        {
            Play(gun != null ? gun.Shoot : null, weapon.transform.position);
        }

        private void HandleDryFire(MusketWeapon weapon)
        {
            Play(gun != null ? gun.Empty : null, weapon.transform.position);
        }

        private void HandleReloaded(MusketWeapon weapon)
        {
            Play(gun != null ? gun.Reload : null, weapon.transform.position);
        }

        private void HandleCaptured(IVacuumAffectable affectable)
        {
            Play(vacuum != null ? vacuum.SuckUpObject : null, NozzlePosition());
        }

        private void HandleVacuumState(VacuumState state)
        {
            if (vacuum == null || !isActiveAndEnabled)
            {
                return;
            }

            if (vacuumSequence != null)
            {
                StopCoroutine(vacuumSequence);
            }

            vacuumSequence = StartCoroutine(state == VacuumState.Sucking ? StartVacuum() : StopVacuum());
        }

        // Switch click then power on then the running loop
        private IEnumerator StartVacuum()
        {
            Play(vacuum.SwitchClick, NozzlePosition());

            // Turned back on before the loop stopped so keep it running
            if (vacuumLoop.IsPlaying)
            {
                vacuumPowered = true;
                vacuumSequence = null;
                yield break;
            }

            yield return Delayed(
                vacuum.PowerOnDelay, () =>
                {
                    vacuumPowered = true;
                    Play(vacuum.PowerOn, NozzlePosition());
                },
                vacuum.LoopStartDelay, () => vacuumLoop = AudioManager.PlayLoop(vacuum.RunningLoop, LoopAnchor()));

            vacuumSequence = null;
        }

        // Switch click then power off then the loop fades
        private IEnumerator StopVacuum()
        {
            Play(vacuum.SwitchClick, NozzlePosition());

            yield return Delayed(
                vacuum.PowerOffDelay, () =>
                {
                    if (vacuumPowered)
                    {
                        Play(vacuum.PowerOff, NozzlePosition());
                    }

                    vacuumPowered = false;
                },
                vacuum.LoopStopDelay, () =>
                {
                    // Forget the fading loop so a quick restart begins a fresh one
                    vacuumLoop.Stop(vacuum.LoopFadeOut);
                    vacuumLoop = AudioHandle.None;
                });

            vacuumSequence = null;
        }

        // Runs two actions at their own delays from now in time order
        private static IEnumerator Delayed(float firstDelay, System.Action first, float secondDelay, System.Action second)
        {
            bool firstEarlier = firstDelay <= secondDelay;
            float early = firstEarlier ? firstDelay : secondDelay;
            float late = firstEarlier ? secondDelay : firstDelay;

            if (early > 0f)
            {
                yield return new WaitForSecondsRealtime(early);
            }

            (firstEarlier ? first : second)();

            if (late - early > 0f)
            {
                yield return new WaitForSecondsRealtime(late - early);
            }

            (firstEarlier ? second : first)();
        }

        private Vector3 NozzlePosition()
        {
            if (vacuumController == null)
            {
                return transform.position;
            }

            return vacuumController.Nozzle != null ? vacuumController.Nozzle.Origin : vacuumController.transform.position;
        }

        private Transform LoopAnchor()
        {
            if (vacuumController == null)
            {
                return transform;
            }

            return vacuumController.Nozzle != null ? vacuumController.Nozzle.transform : vacuumController.transform;
        }

        private static void Play(SoundEvent sound, Vector3 position)
        {
            AudioManager.Play(sound, position);
        }
    }
}
