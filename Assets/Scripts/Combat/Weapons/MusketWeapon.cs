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

        public event Action<MusketWeapon> Fired;
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

            Vector3 direction = FlatAimDirection(OwnerRoot.position);
            ICombatMotor motor = Motor;
            if (motor != null)
            {
                motor.HoldFacing(direction, ignitionTimer + 0.25f);
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

            Transform owner = OwnerRoot;
            Vector3 origin = muzzle != null
                ? muzzle.position
                : owner.position + Vector3.up * 0.3f + owner.forward * 0.8f;

            // First sweep starts on the witch centre line so close targets are not skipped
            Vector3 sweepStart = new Vector3(owner.position.x, origin.y, owner.position.z);
            Vector3 direction = FlatAimDirection(sweepStart);

            WeaponStats stats = Stats;
            int shots = 1 + stats.ExtraShots;
            float range = maxRange * stats.RangeMultiplier;

            for (int i = 0; i < shots; i++)
            {
                float angle = (i - (shots - 1) * 0.5f) * extraShotSpread;
                Vector3 shotDirection = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                SpawnBall(origin, sweepStart, shotDirection, range);
            }

            ICombatMotor motor = Motor;
            if (motor != null)
            {
                motor.HoldFacing(direction, 0.25f);
                motor.HoldMoveScale(moveScaleAfterShot, slowAfterShotDuration);
                if (recoilSpeed > 0f)
                {
                    motor.Lunge(-direction * recoilSpeed, recoilDuration);
                }
            }

            flashTimer = muzzleFlashDuration;
            SetFlash(muzzleFlashIntensity);
            kick = 1f;

            RaiseAttacked();
            Fired?.Invoke(this);
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
