// Flintlock musket with an ignition delay one hard shot and a long reload
using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    public class MusketWeapon : Weapon
    {
        [Header("Shot")]
        [Min(0f)] [SerializeField] private float damage = 40f;

        [Tooltip("Horizontal speed given to whatever is hit, in m/s.")]
        [Min(0f)] [SerializeField] private float knockback = 3f;

        [Tooltip("Seconds the target loses control.")]
        [Min(0f)] [SerializeField] private float stagger = 0.35f;

        [Min(1f)] [SerializeField] private float projectileSpeed = 70f;

        [Min(1f)] [SerializeField] private float maxRange = 35f;

        [Min(0.001f)] [SerializeField] private float projectileRadius = 0.12f;

        [Tooltip("Upward facing surfaces this close to the witch's feet count as floor, so aiming at them fires a level shot.")]
        [Min(0f)] [SerializeField] private float floorTolerance = 0.25f;

        [Tooltip("Steepest the shot tilts up or down to reach what is under the cursor, in degrees.")]
        [Range(0f, 80f)] [SerializeField] private float maxShotPitch = 45f;

        [Tooltip("Degrees between balls when more than one is fired per shot.")]
        [Range(0f, 30f)] [SerializeField] private float extraShotSpread = 5f;

        [Tooltip("Speed a 1 kg loose prop is knocked at when struck, in m/s.")]
        [Min(0f)] [SerializeField] private float propImpact = 9f;

        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Timing")]
        [Tooltip("Delay between pulling the trigger and the shot: the flash in the pan.")]
        [Min(0f)] [SerializeField] private float ignitionDelay = 0.08f;

        [Min(0.05f)] [SerializeField] private float reloadTime = 1.6f;

        [Tooltip("Keep reloading while another weapon is in hand.")]
        [SerializeField] private bool reloadWhileHolstered = true;

        [Header("Body")]
        [Tooltip("Speed the shot pushes the witch backward, in m/s.")]
        [Min(0f)] [SerializeField] private float recoilSpeed = 3f;

        [Min(0f)] [SerializeField] private float recoilDuration = 0.1f;

        [Tooltip("Walking speed just after firing, as a fraction of normal.")]
        [Range(0f, 1f)] [SerializeField] private float moveScaleAfterShot = 0.35f;

        [Min(0f)] [SerializeField] private float slowAfterShotDuration = 0.3f;

        [Header("Look")]
        [Tooltip("Where the ball leaves the barrel. Its forward is ignored; the shot follows the aim.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Model transform kicked on firing and tilted while reloading.")]
        [SerializeField] private Transform model;

        [SerializeField] private Light muzzleFlash;

        [Min(0f)] [SerializeField] private float muzzleFlashIntensity = 8f;

        [Min(0.01f)] [SerializeField] private float muzzleFlashDuration = 0.07f;

        [Tooltip("Optional prefab for the ball. A plain ball is built if left empty.")]
        [SerializeField] private MusketBall projectilePrefab;

        [SerializeField] private Material projectileMaterial;

        [SerializeField] private Material trailMaterial;

        [SerializeField] private Color trailColor = new Color(1f, 0.95f, 0.85f, 0.8f);

        private bool loaded = true;
        private float reloadProgress = 1f;
        private float ignitionTimer = -1f;
        private float flashTimer;
        private float kick;
        private Quaternion modelRestRotation = Quaternion.identity;
        private Vector3 modelRestPosition;

        public override WeaponSlot Slot => WeaponSlot.Ranged;

        public override bool IsBusy => ignitionTimer >= 0f;

        public bool IsLoaded => loaded;

        public bool IsReloading => !loaded;

        // 0 just after firing and 1 when loaded
        public float ReloadProgress => loaded ? 1f : reloadProgress;

        public float Damage
        {
            get => damage;
            set => damage = Mathf.Max(0f, value);
        }

        public float ReloadTime
        {
            get => reloadTime;
            set => reloadTime = Mathf.Max(0.05f, value);
        }

        public float ProjectileSpeed
        {
            get => projectileSpeed;
            set => projectileSpeed = Mathf.Max(1f, value);
        }

        // Where the cursor crossed muzzle height on the last shot
        public Vector3 LastAimTarget { get; private set; }

        // The surface point under the cursor on the last shot
        public Vector3 LastCursorPoint { get; private set; }

        public event Action<MusketWeapon> Fired;
        public event Action<MusketWeapon, MusketBall> BallLaunched;
        public event Action<MusketWeapon> DryFired;
        public event Action<MusketWeapon> Reloaded;

        protected override void Awake()
        {
            base.Awake();

            if (model != null)
            {
                modelRestRotation = model.localRotation;
                modelRestPosition = model.localPosition;
            }

            SetFlash(0f);
        }

        public override void TickInput(in CombatInputFrame input)
        {
            if (input.PrimaryPressed)
            {
                PullTrigger();
            }
        }

        public void PullTrigger()
        {
            if (!IsEquipped || ignitionTimer >= 0f)
            {
                return;
            }

            if (!loaded)
            {
                DryFired?.Invoke(this);
                return;
            }

            float speed = Stats.AttackSpeedMultiplier;
            ignitionTimer = ignitionDelay / speed;

            Vector3 facing = ShotFacing(MuzzlePosition(), out _, out _);
            ICombatMotor motor = Motor;
            if (motor != null)
            {
                motor.HoldFacing(facing, ignitionTimer + 0.25f);
                motor.HoldMoveScale(0.5f, ignitionTimer);
            }

            if (ignitionTimer <= 0f)
            {
                Fire();
            }
        }

        // Finishes the reload instantly
        public void Reload()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            reloadProgress = 1f;
            Reloaded?.Invoke(this);
        }

        protected override void OnUnequipped()
        {
            // Lowering the gun mid ignition fizzles the shot but stays loaded
            ignitionTimer = -1f;
            kick = 0f;
            SetFlash(0f);
            if (model != null)
            {
                model.localRotation = modelRestRotation;
                model.localPosition = modelRestPosition;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (!loaded && ignitionTimer < 0f && (IsEquipped || reloadWhileHolstered))
            {
                reloadProgress += deltaTime * Stats.ReloadSpeedMultiplier / reloadTime;
                if (reloadProgress >= 1f)
                {
                    Reload();
                }
            }

            if (ignitionTimer >= 0f && IsEquipped)
            {
                ignitionTimer -= deltaTime;
                if (ignitionTimer <= 0f)
                {
                    Fire();
                }
            }

            if (flashTimer > 0f)
            {
                flashTimer -= deltaTime;
                SetFlash(Mathf.Clamp01(flashTimer / muzzleFlashDuration) * muzzleFlashIntensity);
            }

            UpdateModelPose(deltaTime);
        }

        private void Fire()
        {
            ignitionTimer = -1f;
            loaded = false;
            reloadProgress = 0f;

            Vector3 origin = MuzzlePosition();
            Vector3 sweepStart = SweepStart(origin);
            Vector3 facing = ShotFacing(origin, out Vector3 target, out bool hasTarget);
            Vector3 direction = facing;

            // Fire from where the muzzle really is so the shot crosses the target even mid turn
            if (hasTarget)
            {
                Vector3 fromMuzzle = target - origin;
                Vector3 flat = new Vector3(fromMuzzle.x, 0f, fromMuzzle.z);
                if (flat.sqrMagnitude > 0.01f && Vector3.Dot(flat, facing) > 0f)
                {
                    direction = ClampPitch(fromMuzzle, maxShotPitch);
                }
            }

            WeaponStats stats = Stats;
            int shots = 1 + stats.ExtraShots;
            float range = maxRange * stats.RangeMultiplier;

            LastAimTarget = hasTarget ? target : sweepStart + direction * range;
            LastCursorPoint = hasTarget ? Aim.AimPoint : LastAimTarget;

            for (int i = 0; i < shots; i++)
            {
                float angle = (i - (shots - 1) * 0.5f) * extraShotSpread;
                Vector3 shotDirection = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                SpawnBall(origin, sweepStart, shotDirection, range);
            }

            ICombatMotor motor = Motor;
            if (motor != null)
            {
                motor.HoldFacing(facing, 0.25f);
                motor.HoldMoveScale(moveScaleAfterShot, slowAfterShotDuration);
                if (recoilSpeed > 0f)
                {
                    motor.Lunge(-facing * recoilSpeed, recoilDuration);
                }
            }

            flashTimer = muzzleFlashDuration;
            SetFlash(muzzleFlashIntensity);
            kick = 1f;

            RaiseAttacked();
            Fired?.Invoke(this);
        }

        private Vector3 MuzzlePosition()
        {
            Transform owner = OwnerRoot;
            return muzzle != null
                ? muzzle.position
                : owner.position + Vector3.up * 0.3f + owner.forward * 0.8f;
        }

        // Facing that puts the side mounted barrel line through the shot target
        private Vector3 ShotFacing(Vector3 muzzlePosition, out Vector3 target, out bool hasTarget)
        {
            hasTarget = TryGetShotTarget(muzzlePosition, out target);

            Transform owner = OwnerRoot;
            if (!hasTarget)
            {
                return FlatAimDirection(SweepStart(muzzlePosition));
            }

            Vector3 toTarget = target - owner.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.01f)
            {
                return FlatAimDirection(SweepStart(muzzlePosition));
            }

            toTarget /= distance;

            // Sideways offset of the muzzle from the witch centre and positive is her right
            float side = (Quaternion.Inverse(owner.rotation) * (muzzlePosition - owner.position)).x;

            // Too close to line up so face the target and let the point blank sweep do the work
            if (distance <= Mathf.Abs(side) + 0.05f)
            {
                return toTarget;
            }

            float turn = Mathf.Asin(side / distance) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(-turn, Vector3.up) * toTarget;
        }

        // The isometric camera looks down so the cursor ray meets things at many heights
        // Anything standing under the cursor is aimed at directly and open floor gives a level shot
        private bool TryGetShotTarget(Vector3 muzzlePosition, out Vector3 target)
        {
            target = Vector3.zero;
            ICombatAim aim = Aim;
            if (aim == null || !aim.HasAim)
            {
                return false;
            }

            if (aim.TryGetAimSurface(out Vector3 point, out Vector3 normal) && !IsFloor(point, normal))
            {
                target = point;
                return true;
            }

            target = aim.AimPointAtHeight(muzzlePosition.y);
            return true;
        }

        private bool IsFloor(Vector3 point, Vector3 normal)
        {
            return normal.y > 0.7f && point.y <= FeetHeight() + floorTolerance;
        }

        private float FeetHeight()
        {
            Transform owner = OwnerRoot;
            Collider body = owner.GetComponent<Collider>();
            return body != null ? body.bounds.min.y : owner.position.y;
        }

        private static Vector3 ClampPitch(Vector3 direction, float maxPitch)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            float flatLength = flat.magnitude;
            float pitch = Mathf.Atan2(direction.y, flatLength) * Mathf.Rad2Deg;
            if (Mathf.Abs(pitch) <= maxPitch)
            {
                return direction.normalized;
            }

            float clamped = Mathf.Sign(pitch) * maxPitch * Mathf.Deg2Rad;
            return (flat / flatLength * Mathf.Cos(clamped) + Vector3.up * Mathf.Sin(clamped)).normalized;
        }

        // First sweep starts on the witch centre line so close targets are not skipped
        private Vector3 SweepStart(Vector3 muzzlePosition)
        {
            Vector3 owner = OwnerRoot.position;
            return new Vector3(owner.x, muzzlePosition.y, owner.z);
        }

        private void SpawnBall(Vector3 origin, Vector3 sweepStart, Vector3 direction, float range)
        {
            MusketBall ball = projectilePrefab != null
                ? Instantiate(projectilePrefab)
                : MusketBall.CreateDefault(projectileMaterial, trailMaterial, trailColor);

            ball.Launch(
                origin,
                sweepStart,
                direction,
                projectileSpeed,
                range,
                projectileRadius,
                hitLayers,
                OwnerRoot,
                HandleBallHit);

            BallLaunched?.Invoke(this, ball);
        }

        private void HandleBallHit(RaycastHit hit, Vector3 direction)
        {
            IDamageable target = hit.collider != null ? hit.collider.GetComponentInParent<IDamageable>() : null;
            WeaponStats stats = Stats;

            if (target != null)
            {
                DealDamage(target, new DamageInfo
                {
                    Amount = damage * stats.DamageMultiplier,
                    Point = hit.point,
                    Direction = direction,
                    Knockback = knockback * stats.KnockbackMultiplier,
                    Launch = 0f,
                    Stagger = stagger,
                    Tags = DamageTags.Ranged
                });
                return;
            }

            Rigidbody prop = hit.rigidbody;
            if (prop != null && !prop.isKinematic)
            {
                float scale = 1f / Mathf.Max(1f, Mathf.Sqrt(prop.mass));
                prop.WakeUp();
                prop.AddForceAtPosition(direction * (propImpact * scale * prop.mass), hit.point, ForceMode.Impulse);
            }
        }

        private void UpdateModelPose(float deltaTime)
        {
            if (model == null || !IsEquipped)
            {
                return;
            }

            kick = Mathf.MoveTowards(kick, 0f, deltaTime * 6f);

            // Kick back on firing and tip the barrel up while reloading
            float reloadTilt = loaded ? 0f : Mathf.Sin(Mathf.Clamp01(reloadProgress) * Mathf.PI) * 55f;
            Quaternion pose = Quaternion.Euler(-reloadTilt - kick * 25f, 0f, 0f);

            model.localRotation = modelRestRotation * pose;
            model.localPosition = modelRestPosition + Vector3.back * (kick * 0.12f);
        }

        private void SetFlash(float intensity)
        {
            if (muzzleFlash == null)
            {
                return;
            }

            muzzleFlash.intensity = intensity;
            muzzleFlash.enabled = intensity > 0.01f;
        }
    }
}
