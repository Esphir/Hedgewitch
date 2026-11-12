// Broom with light and heavy strings whose finishers knock targets up and back
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat
{
    // Presses are buffered and chain at each step cancel point
    public class BroomWeapon : Weapon
    {
        private enum Phase
        {
            Idle,
            Windup,
            Active,
            Recovery
        }

        private enum Press
        {
            None,
            Light,
            Heavy
        }

        [Header("Moveset")]
        [Tooltip("Swing data. A built-in default moveset is used if left empty.")]
        [SerializeField] private MeleeMoveset moveset;

        [Header("Hit volume")]
        [Tooltip("Height above the owner's pivot that swings are measured from.")]
        [SerializeField] private float hitCentreHeight;

        [Tooltip("How far above or below the hit centre a target may be and still be struck.")]
        [Min(0.1f)] [SerializeField] private float hitHeightTolerance = 1.6f;

        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Static geometry that stops a swing reaching through it.")]
        [SerializeField] private LayerMask obstructionLayers = 1;

        [Tooltip("Seconds a press is remembered while the broom is busy.")]
        [Min(0f)] [SerializeField] private float inputBufferTime = 0.35f;

        [Header("Look")]
        [Tooltip("Transform rotated through each swing. Its children are the broom model.")]
        [SerializeField] private Transform swingPivot;

        [Tooltip("Optional trail on the broom head, emitting only while a swing can hit.")]
        [SerializeField] private TrailRenderer swingTrail;

        [Min(0.1f)] [SerializeField] private float restReturnSpeed = 10f;

        private readonly HashSet<IDamageable> struckThisSwing = new HashSet<IDamageable>();
        private readonly HashSet<Rigidbody> sweptThisSwing = new HashSet<Rigidbody>();
        private readonly Collider[] overlapBuffer = new Collider[64];

        private MeleeMoveset runtimeMoveset;
        private Phase phase = Phase.Idle;
        private MeleeAttackStep step;
        private bool heavyString;
        private int stepIndex = -1;
        private float elapsed;
        private float speed = 1f;
        private float comboWindowTimer;
        private Vector3 attackDirection = Vector3.forward;
        private Quaternion windupStartRotation = Quaternion.identity;
        private bool connectedThisSwing;
        private Press buffered = Press.None;
        private float bufferAge;

        public override WeaponSlot Slot => WeaponSlot.Melee;

        public override bool IsBusy => phase != Phase.Idle;

        public MeleeMoveset Moveset
        {
            get
            {
                if (moveset != null)
                {
                    return moveset;
                }

                if (runtimeMoveset == null)
                {
                    runtimeMoveset = MeleeMoveset.CreateDefault();
                }

                return runtimeMoveset;
            }
            set => moveset = value;
        }

        public MeleeAttackStep CurrentStep => phase != Phase.Idle ? step : null;

        // -1 when the string has lapsed
        public int ComboIndex => stepIndex;

        public bool IsHeavyString => heavyString;

        public bool IsHitWindowOpen => phase == Phase.Active;

        public event Action<BroomWeapon, MeleeAttackStep> SwingStarted;

        protected override void Awake()
        {
            base.Awake();
            SetTrail(false);
        }

        public override void TickInput(in CombatInputFrame input)
        {
            if (input.SecondaryPressed)
            {
                Buffer(Press.Heavy);
            }
            else if (input.PrimaryPressed)
            {
                Buffer(Press.Light);
            }
        }

        public void PressLight() => Buffer(Press.Light);

        public void PressHeavy() => Buffer(Press.Heavy);

        protected override void OnEquipped()
        {
            ResetString();
            if (swingPivot != null)
            {
                swingPivot.localRotation = Quaternion.Euler(Moveset.restPose);
            }
        }

        protected override void OnUnequipped()
        {
            ResetString();
        }

        private void Update()
        {
            if (!IsEquipped)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            if (buffered != Press.None)
            {
                bufferAge += deltaTime;
                if (bufferAge > inputBufferTime)
                {
                    buffered = Press.None;
                }
            }

            if (phase == Phase.Idle)
            {
                if (comboWindowTimer > 0f)
                {
                    comboWindowTimer -= deltaTime;
                    if (comboWindowTimer <= 0f)
                    {
                        stepIndex = -1;
                    }
                }

                if (buffered != Press.None)
                {
                    StartNextSwing();
                }
            }
            else
            {
                AdvanceSwing(deltaTime);
            }

            UpdatePose(deltaTime);
        }

        private void Buffer(Press press)
        {
            buffered = press;
            bufferAge = 0f;
        }

        private void AdvanceSwing(float deltaTime)
        {
            elapsed += deltaTime * speed;

            float activeStart = step.windup;
            float activeEnd = step.windup + step.active;
            float end = step.TotalDuration;

            if (elapsed < activeStart)
            {
                phase = Phase.Windup;
                return;
            }

            if (elapsed < activeEnd)
            {
                if (phase != Phase.Active)
                {
                    phase = Phase.Active;
                    SetTrail(true);
                }

                Strike();
                return;
            }

            if (phase == Phase.Active || phase == Phase.Windup)
            {
                // A long frame can skip the active window so strike once anyway
                if (phase == Phase.Windup)
                {
                    Strike();
                }

                phase = Phase.Recovery;
                SetTrail(false);
            }

            bool finished = elapsed >= end;
            bool canCancel = elapsed >= activeEnd + step.recovery * step.cancelPoint;

            if (buffered != Press.None && canCancel && !step.finisher)
            {
                StartNextSwing();
                return;
            }

            if (finished)
            {
                phase = Phase.Idle;
                comboWindowTimer = step.finisher ? 0f : step.comboWindow;
                if (step.finisher)
                {
                    stepIndex = -1;
                }
            }
        }

        private void StartNextSwing()
        {
            bool heavy = buffered == Press.Heavy;
            buffered = Press.None;

            MeleeAttackStep[] combo = Moveset.GetCombo(heavy);
            if (combo == null || combo.Length == 0)
            {
                return;
            }

            int next = 0;
            bool continuing = stepIndex >= 0 && heavy == heavyString && (phase != Phase.Idle || comboWindowTimer > 0f);
            if (continuing && stepIndex < combo.Length - 1)
            {
                next = stepIndex + 1;
            }

            BeginSwing(heavy, next, combo[next]);
        }

        private void BeginSwing(bool heavy, int index, MeleeAttackStep next)
        {
            heavyString = heavy;
            stepIndex = index;
            step = next;
            elapsed = 0f;
            phase = Phase.Windup;
            comboWindowTimer = 0f;
            connectedThisSwing = false;
            struckThisSwing.Clear();
            sweptThisSwing.Clear();
            SetTrail(false);

            speed = Stats.AttackSpeedMultiplier;
            attackDirection = FlatAimDirection(OwnerRoot.position);
            windupStartRotation = swingPivot != null ? swingPivot.localRotation : Quaternion.identity;

            float duration = step.TotalDuration / speed;
            ICombatMotor motor = Motor;
            if (motor != null)
            {
                motor.HoldFacing(attackDirection, duration);
                motor.HoldMoveScale(step.moveScale, duration);
                if (step.lunge > 0f)
                {
                    motor.Lunge(attackDirection * step.lunge, (step.windup + step.active) / speed);
                }
            }

            RaiseAttacked();
            SwingStarted?.Invoke(this, step);
        }

        private void Strike()
        {
            Transform owner = OwnerRoot;
            Vector3 centre = owner.position + Vector3.up * hitCentreHeight;
            WeaponStats stats = Stats;
            float range = step.range * stats.RangeMultiplier;
            float halfArc = step.arc * 0.5f;

            int count = Physics.OverlapSphereNonAlloc(
                centre, range, overlapBuffer, hitLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                overlapBuffer[i] = null;

                if (collider == null || IsPartOf(collider.transform, owner))
                {
                    continue;
                }

                Vector3 nearest = collider.bounds.ClosestPoint(centre);
                Vector3 offset = nearest - centre;
                if (Mathf.Abs(offset.y) > hitHeightTolerance)
                {
                    continue;
                }

                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > range)
                {
                    continue;
                }

                // Anything right against the witch is hit whatever her facing
                if (distance > 0.35f && Vector3.Angle(attackDirection, offset) > halfArc)
                {
                    continue;
                }

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    if (target.IsAlive && struckThisSwing.Add(target)
                        && !IsObstructed(centre, collider.bounds.center, target.Transform))
                    {
                        HitTarget(target, nearest, stats);
                    }

                    continue;
                }

                Rigidbody prop = collider.attachedRigidbody;
                if (prop != null && !prop.isKinematic && sweptThisSwing.Add(prop)
                    && !IsObstructed(centre, collider.bounds.center, prop.transform))
                {
                    SweepProp(prop, stats);
                }
            }
        }

        private void HitTarget(IDamageable target, Vector3 point, WeaponStats stats)
        {
            Vector3 away = target.Transform.position - OwnerRoot.position;
            away.y = 0f;
            Vector3 direction = away.sqrMagnitude > 0.01f ? away.normalized : attackDirection;

            DamageTags tags = DamageTags.Melee;
            if (heavyString)
            {
                tags |= DamageTags.Heavy;
            }

            if (step.finisher)
            {
                tags |= DamageTags.Finisher;
            }

            DamageInfo info = new DamageInfo
            {
                Amount = step.damage * stats.DamageMultiplier,
                Point = point,
                Direction = direction,
                Knockback = step.knockback * stats.KnockbackMultiplier,
                Launch = step.launch * stats.KnockbackMultiplier,
                Stagger = step.stagger,
                Tags = tags
            };

            DamageResult result = DealDamage(target, info);
            if (result.Applied && !connectedThisSwing)
            {
                connectedThisSwing = true;
                HitStop.Request(step.hitStop);
            }
        }

        private void SweepProp(Rigidbody prop, WeaponStats stats)
        {
            Vector3 away = prop.worldCenterOfMass - OwnerRoot.position;
            away.y = 0f;
            Vector3 direction = away.sqrMagnitude > 0.01f
                ? Vector3.Slerp(attackDirection, away.normalized, 0.5f).normalized
                : attackDirection;

            // Scale by square root of mass so light things fly and heavy things budge
            float scale = 1f / Mathf.Max(1f, Mathf.Sqrt(prop.mass));
            float lift = step.launch > 0f ? step.launch * 0.6f : 0.8f;
            Vector3 velocity = (direction * step.propSweep + Vector3.up * lift) * (scale * stats.KnockbackMultiplier);

            prop.WakeUp();
            prop.AddForce(velocity, ForceMode.VelocityChange);
        }

        private bool IsObstructed(Vector3 from, Vector3 to, Transform target)
        {
            if (!Physics.Linecast(from, to, out RaycastHit hit, obstructionLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (IsPartOf(hit.transform, target) || IsPartOf(hit.transform, OwnerRoot))
            {
                return false;
            }

            // Loose props do not shield but walls and fixed furniture do
            return hit.rigidbody == null || hit.rigidbody.isKinematic;
        }

        private void UpdatePose(float deltaTime)
        {
            if (swingPivot == null)
            {
                return;
            }

            Quaternion rest = Quaternion.Euler(Moveset.restPose);

            if (phase == Phase.Idle || step == null)
            {
                swingPivot.localRotation = Quaternion.Slerp(
                    swingPivot.localRotation, rest, 1f - Mathf.Exp(-restReturnSpeed * deltaTime));
                return;
            }

            Quaternion from = Quaternion.Euler(step.swingFrom);
            Quaternion to = Quaternion.Euler(step.swingTo);

            if (elapsed < step.windup)
            {
                float t = step.windup > 0f ? elapsed / step.windup : 1f;
                swingPivot.localRotation = Quaternion.Slerp(windupStartRotation, from, EaseOut(t));
            }
            else if (elapsed < step.windup + step.active)
            {
                float t = (elapsed - step.windup) / step.active;
                swingPivot.localRotation = SlerpEuler(step.swingFrom, step.swingTo, EaseOut(t));
            }
            else
            {
                // Hold the follow through then drift home over the back half of recovery
                float t = step.recovery > 0f ? (elapsed - step.windup - step.active) / step.recovery : 1f;
                float home = Mathf.Clamp01((t - 0.5f) * 2f);
                swingPivot.localRotation = Quaternion.Slerp(to, rest, home * home);
            }
        }

        // Lerps euler angles so wide swings go round the front not behind
        private static Quaternion SlerpEuler(Vector3 from, Vector3 to, float t)
        {
            return Quaternion.Euler(Vector3.Lerp(from, to, t));
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private void ResetString()
        {
            phase = Phase.Idle;
            step = null;
            stepIndex = -1;
            elapsed = 0f;
            comboWindowTimer = 0f;
            buffered = Press.None;
            SetTrail(false);
        }

        private void SetTrail(bool emitting)
        {
            if (swingTrail == null)
            {
                return;
            }

            swingTrail.emitting = emitting;
            if (!emitting && phase == Phase.Idle)
            {
                swingTrail.Clear();
            }
        }
    }
}
