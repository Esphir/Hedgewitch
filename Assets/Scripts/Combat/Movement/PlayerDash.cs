// Dash on the dash button whose look depends on the weapon in hand
using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    public enum DashStyle
    {
        // Musket vacuum or empty hands
        Regular,

        // Sitting on a horizontal floating broom
        BroomRide,

        // Standing on the bristle head of an upright broom
        BroomSweep
    }

    [Serializable]
    public struct DashMove
    {
        [Min(0f)] public float distance;
        [Min(0.05f)] public float duration;

        public DashMove(float distance, float duration)
        {
            this.distance = distance;
            this.duration = duration;
        }

        public float Speed => duration > 0f ? distance / duration : 0f;
    }

    [DisallowMultipleComponent]
    public class PlayerDash : MonoBehaviour
    {
        [Header("Wiring (found on this object if left empty)")]
        [SerializeField] private WeaponInventory inventory;
        [SerializeField] private Health health;
        [SerializeField] private HitReaction hitReaction;

        [Header("Moves")]
        [Tooltip("Musket vacuum or empty hands.")]
        [SerializeField] private DashMove regular = new DashMove(4.5f, 0.28f);

        [SerializeField] private DashMove broomRide = new DashMove(6f, 0.34f);

        [SerializeField] private DashMove broomSweep = new DashMove(5.5f, 0.36f);

        [Header("Timing")]
        [Tooltip("Seconds from the start of each dash during which hits are ignored.")]
        [Min(0f)] [SerializeField] private float invulnerableTime = 0.2f;

        [Tooltip("Seconds after a dash ends before another can start.")]
        [Min(0f)] [SerializeField] private float cooldown = 0.3f;

        [Tooltip("Seconds a dash press is remembered during cooldown.")]
        [Min(0f)] [SerializeField] private float inputBufferTime = 0.15f;

        private ICombatInput input;
        private ICombatMotor motor;
        private float elapsed;
        private float duration;
        private float cooldownTimer;
        private float bufferTimer;
        private Vector3 bufferedDirection;

        public bool IsDashing { get; private set; }

        public DashStyle Style { get; private set; }

        public Vector3 Direction { get; private set; } = Vector3.forward;

        public float Elapsed => elapsed;

        public float Duration => duration;

        // 0 to 1 through the current dash
        public float Progress => IsDashing && duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        // The broom look used next since the two alternate
        public DashStyle NextBroomStyle { get; private set; } = DashStyle.BroomRide;

        public bool CanDash => !IsDashing && cooldownTimer <= 0f
                               && (health == null || health.IsAlive)
                               && (hitReaction == null || !hitReaction.IsStaggered);

        public event Action<PlayerDash, DashStyle> Started;
        public event Action<PlayerDash, DashStyle> Ended;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = GetComponent<WeaponInventory>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (hitReaction == null)
            {
                hitReaction = GetComponent<HitReaction>();
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            Stop(false);
        }

        private void Update()
        {
            ResolveSources();
            float deltaTime = Time.deltaTime;

            if (IsDashing)
            {
                elapsed += deltaTime;
                if (elapsed >= duration)
                {
                    Stop(false);
                }
            }
            else if (cooldownTimer > 0f)
            {
                cooldownTimer -= deltaTime;
            }

            if (input != null)
            {
                CombatInputFrame frame = input.ReadCombatInput();
                if (frame.DashPressed)
                {
                    bufferTimer = inputBufferTime;
                    bufferedDirection = frame.MoveDirection;
                }
            }

            if (bufferTimer > 0f)
            {
                bufferTimer -= deltaTime;
                if (TryDash(bufferedDirection))
                {
                    bufferTimer = 0f;
                }
            }
        }

        // Dashes along direction or facing when it is zero and returns false if a dash cannot start now
        public bool TryDash(Vector3 direction)
        {
            ResolveSources();
            if (!CanDash || motor == null)
            {
                return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
            Style = PickStyle();
            DashMove move = MoveFor(Style);

            if (inventory != null)
            {
                inventory.Current?.CancelAction();
                inventory.InputSuspended = true;
            }

            health?.GrantInvulnerability(invulnerableTime);
            motor.Dash(Direction * move.Speed, move.duration);

            elapsed = 0f;
            duration = move.duration;
            IsDashing = true;
            Started?.Invoke(this, Style);
            return true;
        }

        public DashMove MoveFor(DashStyle style)
        {
            switch (style)
            {
                case DashStyle.BroomRide:
                    return broomRide;
                case DashStyle.BroomSweep:
                    return broomSweep;
                default:
                    return regular;
            }
        }

        private DashStyle PickStyle()
        {
            if (inventory == null || !(inventory.Current is BroomWeapon))
            {
                return DashStyle.Regular;
            }

            DashStyle style = NextBroomStyle;
            NextBroomStyle = style == DashStyle.BroomRide ? DashStyle.BroomSweep : DashStyle.BroomRide;
            return style;
        }

        // A hit that gets through after the invulnerable start ends the dash so the knockback reads
        private void HandleDamaged(DamageInfo info, DamageResult result)
        {
            if (IsDashing)
            {
                Stop(true);
            }
        }

        private void Stop(bool interrupted)
        {
            if (!IsDashing)
            {
                return;
            }

            IsDashing = false;
            cooldownTimer = cooldown;

            if (interrupted)
            {
                motor?.Dash(Vector3.zero, 0f);
            }

            if (inventory != null)
            {
                inventory.InputSuspended = false;
            }

            Ended?.Invoke(this, Style);
        }

        private void ResolveSources()
        {
            if (input != null && motor != null)
            {
                return;
            }

            input ??= inventory != null && inventory.Input != null ? inventory.Input : GetComponent<ICombatInput>();
            motor ??= inventory != null && inventory.Motor != null ? inventory.Motor : GetComponent<ICombatMotor>();
        }
    }
}
