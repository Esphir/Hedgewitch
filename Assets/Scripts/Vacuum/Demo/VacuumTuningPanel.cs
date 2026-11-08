using System.Text;
using UnityEngine;
using Hedgewitch.Vacuum.Enemies;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hedgewitch.Vacuum.Demo
{
    /// <summary>Live tuning for the vacuum's feel.</summary>
    [DisallowMultipleComponent]
    public class VacuumTuningPanel : MonoBehaviour
    {
        [SerializeField] private VacuumController vacuum;

        [Tooltip("Show the panel from the start.")]
        [SerializeField] private bool visible = true;

        [Tooltip("Width of the panel in pixels.")]
        [Min(220f)] [SerializeField] private float panelWidth = 330f;

        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private Vector2 scroll;
        private readonly StringBuilder builder = new StringBuilder(1024);

        private void Awake()
        {
            if (vacuum == null)
            {
                vacuum = FindAnyObjectByType<VacuumController>();
            }
        }

        private void Update()
        {
            if (WasTogglePressed())
            {
                visible = !visible;
            }
        }

        private static bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        private void OnGUI()
        {
            if (vacuum == null)
            {
                return;
            }

            EnsureStyles();

            float width = Mathf.Min(panelWidth, Screen.width - 24f);
            float height = Mathf.Min(620f, Screen.height - 24f);
            Rect area = new Rect(Screen.width - width - 12f, 12f, width, visible ? height : 34f);

            GUILayout.BeginArea(area, GUIContent.none, boxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(visible ? "Vacuum tuning  (F1)" : "Vacuum tuning hidden  (F1)", headerStyle);
            if (GUILayout.Button(visible ? "hide" : "show", GUILayout.Width(50f)))
            {
                visible = !visible;
            }

            GUILayout.EndHorizontal();

            if (!visible)
            {
                GUILayout.EndArea();
                return;
            }

            VacuumSettings settings = vacuum.Settings;
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("Reach", headerStyle);
            settings.maxDistance = Slider("Max distance", settings.maxDistance, 1f, 20f, "m");
            settings.horizontalConeAngle = Slider("Cone width", settings.horizontalConeAngle, 5f, 170f, "deg");
            settings.verticalConeAngle = Slider("Cone height", settings.verticalConeAngle, 5f, 170f, "deg");
            settings.intakeRadius = Slider("Intake radius", settings.intakeRadius, 0f, 4f, "m");
            GUILayout.Label("Intake radius grabs anything in front of the mouth regardless of the\ncone. Raise it if you have to aim down at small objects.", headerStyle);
            settings.coneHysteresis = Slider("Hold margin", settings.coneHysteresis, 0f, 1f, "x");

            GUILayout.Space(6f);
            GUILayout.Label("Force", headerStyle);
            settings.baseForce = Slider("Base force", settings.baseForce, 5f, 200f, "m/s2");
            settings.gravityCompensation = Slider("Lift (gravity cancel)", settings.gravityCompensation, 0f, 1f, "x");
            settings.swirlForce = Slider("Swirl", settings.swirlForce, 0f, 20f, "");
            settings.wobbleTorque = Slider("Wobble", settings.wobbleTorque, 0f, 40f, "");

            GUILayout.Space(6f);
            GUILayout.Label("Arrival", headerStyle);
            settings.lateralDamping = Slider("Funnel (cross-flow)", settings.lateralDamping, 0f, 40f, "/s");
            GUILayout.Label("Funnel stops objects slinging past the mouth. 0 lets them fly by.", headerStyle);
            settings.maxApproachSpeed = Slider("Speed at range", settings.maxApproachSpeed, 1f, 30f, "m/s");
            settings.approachSpeedAtMouth = Slider("Speed at mouth", settings.approachSpeedAtMouth, 0.2f, 15f, "m/s");
            settings.approachBrakingDistance = Slider("Braking distance", settings.approachBrakingDistance, 0.1f, 8f, "m");
            settings.captureRadius = Slider("Capture radius", settings.captureRadius, 0.1f, 3f, "m");

            GUILayout.Space(6f);
            GUILayout.Label("Enemies in scene", headerStyle);
            DrawEnemySection();

            GUILayout.Space(6f);
            GUILayout.Label($"state: {(vacuum.IsSucking ? "SUCKING" : "idle")}   " +
                            $"spool {vacuum.Intensity:P0}   load {vacuum.Load:P0}   " +
                            $"targets {vacuum.ActiveTargetCount}", headerStyle);

            if (GUILayout.Button("Log values"))
            {
                LogValues(settings);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawEnemySection()
        {
            EnemySuctionResponse[] enemies = FindObjectsByType<EnemySuctionResponse>();
            if (enemies.Length == 0)
            {
                GUILayout.Label("none", headerStyle);
                return;
            }

            // One slider drives every enemy, which is what you want while finding a value.
            float strength = enemies[0].EnemyStrength;
            float adjusted = Slider("Enemy strength", strength, 0f, 200f, "m/s2");

            if (!Mathf.Approximately(adjusted, strength))
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    enemies[i].EnemyStrength = adjusted;
                }
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemySuctionResponse enemy = enemies[i];
                GUILayout.Label(
                    $"{enemy.name}: {enemy.Stage}  grip {enemy.Grip:P0}  stamina {enemy.StaminaNormalized:P0}",
                    headerStyle);
            }
        }

        private float Slider(string label, float value, float min, float max, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, headerStyle, GUILayout.Width(panelWidth * 0.42f));
            float result = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(
                string.IsNullOrEmpty(unit) ? result.ToString("0.##") : $"{result:0.##} {unit}",
                headerStyle,
                GUILayout.Width(panelWidth * 0.22f));
            GUILayout.EndHorizontal();
            return result;
        }

        private void LogValues(VacuumSettings settings)
        {
            builder.Clear();
            builder.AppendLine("[Vacuum] current tuning");
            builder.AppendLine($"  maxDistance            {settings.maxDistance:0.##}");
            builder.AppendLine($"  horizontalConeAngle    {settings.horizontalConeAngle:0.##}");
            builder.AppendLine($"  verticalConeAngle      {settings.verticalConeAngle:0.##}");
            builder.AppendLine($"  intakeRadius           {settings.intakeRadius:0.##}");
            builder.AppendLine($"  coneHysteresis         {settings.coneHysteresis:0.##}");
            builder.AppendLine($"  baseForce              {settings.baseForce:0.##}");
            builder.AppendLine($"  gravityCompensation    {settings.gravityCompensation:0.##}");
            builder.AppendLine($"  swirlForce             {settings.swirlForce:0.##}");
            builder.AppendLine($"  wobbleTorque           {settings.wobbleTorque:0.##}");
            builder.AppendLine($"  lateralDamping         {settings.lateralDamping:0.##}");
            builder.AppendLine($"  maxApproachSpeed       {settings.maxApproachSpeed:0.##}");
            builder.AppendLine($"  approachSpeedAtMouth   {settings.approachSpeedAtMouth:0.##}");
            builder.AppendLine($"  approachBrakingDistance {settings.approachBrakingDistance:0.##}");
            builder.AppendLine($"  captureRadius          {settings.captureRadius:0.##}");

            EnemySuctionResponse[] enemies = FindObjectsByType<EnemySuctionResponse>();
            if (enemies.Length > 0)
            {
                builder.AppendLine($"  enemyStrength          {enemies[0].EnemyStrength:0.##}");
            }

            Debug.Log(builder.ToString());
        }

        private void EnsureStyles()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = false };
            }
        }
    }
}
