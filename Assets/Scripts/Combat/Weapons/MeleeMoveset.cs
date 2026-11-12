// Melee strings and their tunable attack steps
using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    [Serializable]
    public class MeleeAttackStep
    {
        public string name = "Swing";

        [Header("Damage")]
        [Min(0f)] public float damage = 8f;

        [Tooltip("The last hit of a string: knocks the target up and back.")]
        public bool finisher;

        [Header("Timing (seconds)")]
        [Tooltip("Wind-up before the swing can hit.")]
        [Min(0f)] public float windup = 0.07f;

        [Tooltip("How long the swing can hit for.")]
        [Min(0.01f)] public float active = 0.09f;

        [Tooltip("Follow-through after the hit window.")]
        [Min(0f)] public float recovery = 0.2f;

        [Tooltip("Fraction of the recovery after which a buffered next swing may cut in.")]
        [Range(0f, 1f)] public float cancelPoint = 0.45f;

        [Tooltip("Time after recovery during which pressing again still continues the string.")]
        [Min(0f)] public float comboWindow = 0.35f;

        [Header("Reach")]
        [Min(0.1f)] public float range = 1.9f;

        [Tooltip("Total width of the swing in degrees, centred on the aim.")]
        [Range(10f, 360f)] public float arc = 130f;

        [Header("Impact")]
        [Tooltip("Horizontal speed given to whatever is hit, in m/s.")]
        [Min(0f)] public float knockback = 1.2f;

        [Tooltip("Upward speed given to whatever is hit, in m/s. Above zero knocks it up.")]
        [Min(0f)] public float launch;

        [Tooltip("Seconds the target loses control.")]
        [Min(0f)] public float stagger = 0.25f;

        [Tooltip("Speed loose props are swept with, in m/s for a 1 kg object.")]
        [Min(0f)] public float propSweep = 3f;

        [Tooltip("Near-frozen time when this swing connects. Keep it to a few hundredths.")]
        [Min(0f)] public float hitStop;

        [Header("Body")]
        [Tooltip("Speed the witch steps into the swing, in m/s.")]
        [Min(0f)] public float lunge = 2.5f;

        [Tooltip("Walking speed while swinging, as a fraction of normal.")]
        [Range(0f, 1f)] public float moveScale = 0.25f;

        [Header("Look")]
        [Tooltip("Local rotation of the broom pivot when the swing starts (pitch, yaw, roll).")]
        public Vector3 swingFrom = new Vector3(10f, 75f, 0f);

        [Tooltip("Local rotation of the broom pivot when the swing ends.")]
        public Vector3 swingTo = new Vector3(10f, -75f, 0f);

        public float TotalDuration => windup + active + recovery;

        public MeleeAttackStep Clone()
        {
            return (MeleeAttackStep)MemberwiseClone();
        }
    }

    [CreateAssetMenu(menuName = "Hedgewitch/Combat/Melee Moveset", fileName = "MeleeMoveset")]
    public class MeleeMoveset : ScriptableObject
    {
        [Tooltip("Pressed with the primary button. The last step should be a finisher.")]
        public MeleeAttackStep[] lightCombo = new MeleeAttackStep[0];

        [Tooltip("Pressed with the secondary button. The last step should be a finisher.")]
        public MeleeAttackStep[] heavyCombo = new MeleeAttackStep[0];

        [Tooltip("Local rotation of the broom pivot when not swinging.")]
        public Vector3 restPose = new Vector3(35f, 40f, 0f);

        public MeleeAttackStep[] GetCombo(bool heavy) => heavy ? heavyCombo : lightCombo;

        // Fallback broom moveset when no asset is assigned
        public static MeleeMoveset CreateDefault()
        {
            MeleeMoveset moveset = CreateInstance<MeleeMoveset>();
            moveset.name = "Broom Moveset (Runtime Default)";
            moveset.FillWithBroomDefaults();
            return moveset;
        }

        public void FillWithBroomDefaults()
        {
            restPose = new Vector3(35f, 40f, 0f);

            lightCombo = new[]
            {
                new MeleeAttackStep
                {
                    name = "Sweep",
                    damage = 8f,
                    windup = 0.07f, active = 0.09f, recovery = 0.18f, cancelPoint = 0.4f, comboWindow = 0.35f,
                    range = 1.9f, arc = 130f,
                    knockback = 1.2f, launch = 0f, stagger = 0.25f, propSweep = 3f,
                    lunge = 2.5f, moveScale = 0.3f,
                    swingFrom = new Vector3(15f, 80f, 0f), swingTo = new Vector3(15f, -80f, 0f)
                },
                new MeleeAttackStep
                {
                    name = "Back-sweep",
                    damage = 9f,
                    windup = 0.06f, active = 0.09f, recovery = 0.2f, cancelPoint = 0.4f, comboWindow = 0.35f,
                    range = 1.9f, arc = 130f,
                    knockback = 1.2f, launch = 0f, stagger = 0.25f, propSweep = 3f,
                    lunge = 2.5f, moveScale = 0.3f,
                    swingFrom = new Vector3(15f, -80f, 0f), swingTo = new Vector3(15f, 80f, 0f)
                },
                new MeleeAttackStep
                {
                    name = "Flick",
                    damage = 15f,
                    finisher = true,
                    windup = 0.12f, active = 0.1f, recovery = 0.34f, cancelPoint = 1f, comboWindow = 0f,
                    range = 2.1f, arc = 150f,
                    knockback = 5f, launch = 3.5f, stagger = 0.8f, propSweep = 6f, hitStop = 0.06f,
                    lunge = 4f, moveScale = 0.1f,
                    // Low and forward then up and over to scoop the target
                    swingFrom = new Vector3(55f, 10f, 0f), swingTo = new Vector3(-60f, -10f, 0f)
                }
            };

            heavyCombo = new[]
            {
                new MeleeAttackStep
                {
                    name = "Clout",
                    damage = 18f,
                    windup = 0.22f, active = 0.12f, recovery = 0.32f, cancelPoint = 0.5f, comboWindow = 0.45f,
                    range = 2.3f, arc = 180f,
                    knockback = 2f, launch = 0f, stagger = 0.45f, propSweep = 5f, hitStop = 0.03f,
                    lunge = 3f, moveScale = 0.15f,
                    swingFrom = new Vector3(5f, 110f, 0f), swingTo = new Vector3(5f, -110f, 0f)
                },
                new MeleeAttackStep
                {
                    name = "Wallop",
                    damage = 28f,
                    finisher = true,
                    windup = 0.3f, active = 0.12f, recovery = 0.5f, cancelPoint = 1f, comboWindow = 0f,
                    range = 2.4f, arc = 130f,
                    knockback = 6.5f, launch = 4.2f, stagger = 0.95f, propSweep = 8f, hitStop = 0.08f,
                    lunge = 4.5f, moveScale = 0f,
                    swingFrom = new Vector3(70f, -20f, 0f), swingTo = new Vector3(-80f, 15f, 0f)
                }
            };
        }
    }
}
