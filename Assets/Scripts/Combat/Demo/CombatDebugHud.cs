// Debug HUD for the belt reload broom string and enemy health
using System.Text;
using UnityEngine;

namespace Hedgewitch.Combat.Demo
{
    [DisallowMultipleComponent]
    public class CombatDebugHud : MonoBehaviour
    {
        [SerializeField] private WeaponInventory inventory;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool showControls = true;
        [SerializeField] private bool showHealthBars = true;
        [SerializeField] private Vector2 panelPosition = new Vector2(12f, 284f);
        [SerializeField] private int fontSize = 13;

        [Tooltip("Height above an object's pivot that its health bar floats at, in metres.")]
        [SerializeField] private float healthBarHeight = 1.4f;

        private readonly StringBuilder builder = new StringBuilder(256);
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private Texture2D pixel;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = FindAnyObjectByType<WeaponInventory>();
            }
        }

        private void OnDestroy()
        {
            if (pixel != null)
            {
                Destroy(pixel);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (showHealthBars)
            {
                DrawHealthBars();
            }

            if (inventory == null)
            {
                return;
            }

            builder.Clear();
            AppendBelt();
            AppendCurrentWeapon();

            if (showControls)
            {
                builder.AppendLine();
                builder.AppendLine("1 broom · 2 musket · 3 vacuum (LB/RB cycle)");
                builder.AppendLine("LMB/F light · fire · suck");
                builder.AppendLine("RMB heavy");
            }

            GUILayout.BeginArea(new Rect(panelPosition.x, panelPosition.y, 320f, 170f), GUIContent.none, boxStyle);
            GUILayout.Label(builder.ToString(), labelStyle);
            GUILayout.EndArea();
        }

        private void AppendBelt()
        {
            for (int i = 0; i < inventory.Weapons.Count; i++)
            {
                Weapon weapon = inventory.Weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                bool inHand = i == inventory.CurrentIndex;
                builder.Append(inHand ? "[" : " ");
                builder.Append(i + 1).Append(' ').Append(weapon.DisplayName);
                builder.Append(inHand ? "] " : "  ");
            }

            builder.AppendLine();
        }

        private void AppendCurrentWeapon()
        {
            Weapon current = inventory.Current;
            if (current == null)
            {
                builder.AppendLine("Empty-handed");
                return;
            }

            switch (current)
            {
                case BroomWeapon broom:
                {
                    MeleeAttackStep step = broom.CurrentStep;
                    MeleeAttackStep[] combo = broom.Moveset.GetCombo(broom.IsHeavyString);
                    int length = combo != null ? combo.Length : 0;
                    if (step != null)
                    {
                        builder.Append(broom.IsHeavyString ? "Heavy " : "Light ");
                        builder.Append(broom.ComboIndex + 1).Append('/').Append(length).Append(": ").Append(step.name);
                        if (step.finisher)
                        {
                            builder.Append("  (finisher)");
                        }

                        builder.AppendLine();
                    }
                    else
                    {
                        builder.AppendLine(broom.ComboIndex >= 0
                            ? $"String open: {broom.ComboIndex + 1}/{length}"
                            : "Ready");
                    }

                    break;
                }

                case MusketWeapon musket:
                    builder.AppendLine(musket.IsLoaded
                        ? "Loaded"
                        : $"Reloading {Bar(musket.ReloadProgress)} {musket.ReloadProgress:P0}");
                    break;

                case VacuumWeapon vacuum:
                    builder.AppendLine(vacuum.IsBusy ? "Sucking" : "Ready");
                    break;
            }
        }

        private void DrawHealthBars()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }
            }

            const float width = 54f;
            const float height = 6f;

            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health health = Health.Active[i];
                if (health == null || !health.IsAlive)
                {
                    continue;
                }

                Vector3 screen = worldCamera.WorldToScreenPoint(health.transform.position + Vector3.up * healthBarHeight);
                if (screen.z <= 0f)
                {
                    continue;
                }

                // GUI space runs top down and screen space bottom up
                Rect back = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height, width, height);
                Rect fill = new Rect(back.x + 1f, back.y + 1f, (width - 2f) * health.Normalized, height - 2f);

                DrawRect(back, new Color(0f, 0f, 0f, 0.65f));
                DrawRect(fill, Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.4f), health.Normalized));
            }
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private static string Bar(float value01)
        {
            const int width = 12;
            int filled = Mathf.Clamp(Mathf.RoundToInt(value01 * width), 0, width);
            return "[" + new string('|', filled) + new string('.', width - filled) + "]";
        }

        private void EnsureStyles()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, richText = false, wordWrap = false };
            }
        }
    }
}
