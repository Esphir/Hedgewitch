using System.Text;
using UnityEngine;
using Hedgewitch.Vacuum.Enemies;

namespace Hedgewitch.Vacuum.Demo
{
    /// <summary>
    /// Immediate-mode debug readout for the vacuum: state, spool, motor load, how many objects are
    /// in the airflow, what has been collected and how the nearest enemy's struggle is going.
    /// </summary>
    [DisallowMultipleComponent]
    public class VacuumDebugHud : MonoBehaviour
    {
        [SerializeField] private VacuumController vacuum;
        [SerializeField] private bool showControls = true;
        [SerializeField] private int fontSize = 13;

        private readonly StringBuilder builder = new StringBuilder(512);
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;

        private void Awake()
        {
            if (vacuum == null)
            {
                vacuum = FindAnyObjectByType<VacuumController>();
            }
        }

        private void OnGUI()
        {
            if (vacuum == null)
            {
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 260f), GUIContent.none, boxStyle);

            builder.Clear();
            builder.AppendLine(vacuum.IsSucking ? "VACUUM: SUCKING" : "VACUUM: idle");
            builder.AppendLine($"Spool     {Bar(vacuum.Intensity)} {vacuum.Intensity:P0}");
            builder.AppendLine($"Load      {Bar(vacuum.Load)} {vacuum.Load:P0}");
            builder.AppendLine($"In airflow: {vacuum.ActiveTargetCount}");

            VacuumCollection collection = vacuum.Collection;
            if (collection != null)
            {
                builder.AppendLine($"Collected: {collection.TotalCount} items, {collection.TotalValue} value");
                builder.AppendLine($"  coins {collection.GetCount(VacuumCategory.Coin)}" +
                                   $"  paper {collection.GetCount(VacuumCategory.Paper)}" +
                                   $"  debris {collection.GetCount(VacuumCategory.Debris)}");
                builder.AppendLine($"  props {collection.GetCount(VacuumCategory.SmallProp)}" +
                                   $"  enemies {collection.GetCount(VacuumCategory.Enemy)}");
            }

            AppendStruggle(builder);

            if (showControls)
            {
                builder.AppendLine();
                builder.AppendLine("WASD move · mouse aim · LMB/F suck");
                builder.AppendLine("Space jump · Q/E turn cam · scroll zoom");
            }

            GUILayout.Label(builder.ToString(), labelStyle);
            GUILayout.EndArea();
        }

        private void AppendStruggle(StringBuilder target)
        {
            EnemySuctionResponse nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < vacuum.ActiveTargetCount; i++)
            {
                VacuumTargetInfo info = vacuum.ActiveTargets[i];
                if (!info.IsValid)
                {
                    continue;
                }

                if (info.Affectable is EnemySuctionResponse enemy && info.Distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = info.Distance;
                }
            }

            if (nearest == null)
            {
                return;
            }

            target.AppendLine();
            target.AppendLine($"Enemy: {nearest.Stage}");
            target.AppendLine($"  grip    {Bar(nearest.Grip)} {nearest.Grip:P0}");
            target.AppendLine($"  stamina {Bar(nearest.StaminaNormalized)} {nearest.StaminaNormalized:P0}");
        }

        private static string Bar(float value01)
        {
            const int width = 12;
            int filled = Mathf.Clamp(Mathf.RoundToInt(value01 * width), 0, width);
            return "[" + new string('|', filled) + new string('.', width - filled) + "]";
        }

        private void EnsureStyles()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    richText = false,
                    wordWrap = false
                };
            }
        }
    }
}
