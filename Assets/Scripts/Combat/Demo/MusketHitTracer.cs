// Debug tracer that draws each musket shot and where the ball landed so misses can be read at a glance
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat.Demo
{
    // Yellow is the real ball path and cyan runs to the aim point
    [DisallowMultipleComponent]
    public class MusketHitTracer : MonoBehaviour
    {
        [Tooltip("Seconds each shot stays drawn.")]
        [Min(0.1f)] [SerializeField] private float lifetime = 4f;

        [Tooltip("Most shots kept on screen at once.")]
        [Range(1, 32)] [SerializeField] private int maxTraces = 8;

        [SerializeField] private bool showLabels = true;

        [SerializeField] private bool showLegend = true;

        [Header("Colours")]
        [SerializeField] private Color pathColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color aimColor = new Color(0.3f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color damageColor = new Color(0.35f, 1f, 0.35f, 1f);
        [SerializeField] private Color blockedColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color missColor = new Color(1f, 0.25f, 0.25f, 1f);

        [SerializeField] private Camera worldCamera;

        private const float FadeTime = 1f;
        private const int IgnoreRaycastLayer = 2;

        private class Trace
        {
            public GameObject Root;
            public MusketBall Ball;
            public Vector3 SweepStart;
            public Vector3 Muzzle;
            public Vector3 Direction;
            public Vector3 AimTarget;
            public Vector3 CursorPoint;
            public Vector3 End;
            public Color EndColor;
            public bool Landed;
            public string Label;
            public float Age;
            public LineRenderer Path;
            public LineRenderer AimLine;
            public Renderer AimMarker;
            public Renderer CursorMarker;
            public Renderer ImpactMarker;
        }

        private static Material sharedMaterial;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<Trace> traces = new List<Trace>();
        private readonly List<MusketWeapon> muskets = new List<MusketWeapon>();
        private MaterialPropertyBlock block;
        private GUIStyle labelStyle;

        private void OnEnable()
        {
            block ??= new MaterialPropertyBlock();

            MusketWeapon[] found = FindObjectsByType<MusketWeapon>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
            {
                found[i].BallLaunched += HandleBallLaunched;
                muskets.Add(found[i]);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < muskets.Count; i++)
            {
                if (muskets[i] != null)
                {
                    muskets[i].BallLaunched -= HandleBallLaunched;
                }
            }

            muskets.Clear();

            while (traces.Count > 0)
            {
                RemoveTrace(0);
            }
        }

        private void HandleBallLaunched(MusketWeapon musket, MusketBall ball)
        {
            if (traces.Count >= maxTraces)
            {
                RemoveTrace(0);
            }

            GameObject root = new GameObject("Musket Trace");
            root.layer = IgnoreRaycastLayer;
            root.transform.SetParent(transform, false);

            Trace trace = new Trace
            {
                Root = root,
                Ball = ball,
                SweepStart = ball.SweepStart,
                Muzzle = ball.LaunchPosition,
                Direction = ball.Velocity.normalized,
                AimTarget = musket.LastAimTarget,
                CursorPoint = musket.LastCursorPoint,
                End = ball.LaunchPosition,
                Path = CreateLine(root.transform, "Ball Path", 0.05f),
                AimLine = CreateLine(root.transform, "Cursor Line", 0.025f),
                AimMarker = CreateMarker(root.transform, "Aim Point", 0.2f),
                CursorMarker = CreateMarker(root.transform, "Cursor Surface Point", 0.1f)
            };

            trace.AimMarker.transform.position = trace.AimTarget;
            trace.CursorMarker.transform.position = trace.CursorPoint;

            ball.Landed += (_, point, hit) => Land(trace, point, hit);
            traces.Add(trace);
        }

        private void Land(Trace trace, Vector3 point, Collider hit)
        {
            if (trace.Root == null)
            {
                return;
            }

            trace.Landed = true;
            trace.End = point;

            IDamageable target = hit != null ? hit.GetComponentInParent<IDamageable>() : null;
            string what;
            if (hit == null)
            {
                trace.EndColor = missColor;
                what = "Miss";
            }
            else if (target != null)
            {
                trace.EndColor = damageColor;
                what = "Hit " + (target is Component component ? component.name : hit.name);
            }
            else
            {
                trace.EndColor = blockedColor;
                what = "Blocked by " + hit.name;
            }

            float distance = Vector3.Distance(trace.Muzzle, point);
            trace.Label = $"{what}\n{distance:0.0} m  path off aim point by {PathOffset(trace):0.00} m";

            trace.ImpactMarker = CreateMarker(trace.Root.transform, "Impact", 0.3f);
            trace.ImpactMarker.transform.position = point;
        }

        // Closest the ball path came to the aim point in 3D
        private static float PathOffset(Trace trace)
        {
            Vector3 toAim = trace.AimTarget - trace.Muzzle;
            return (toAim - trace.Direction * Vector3.Dot(toAim, trace.Direction)).magnitude;
        }

        private void Update()
        {
            // Unscaled so hit stop does not hold the tracers
            float deltaTime = Time.unscaledDeltaTime;

            for (int i = traces.Count - 1; i >= 0; i--)
            {
                Trace trace = traces[i];
                if (trace.Root == null)
                {
                    traces.RemoveAt(i);
                    continue;
                }

                if (!trace.Landed)
                {
                    if (trace.Ball != null)
                    {
                        trace.End = trace.Ball.transform.position;
                    }
                    else
                    {
                        Land(trace, trace.End, null);
                    }
                }

                trace.Age += deltaTime;
                if (trace.Age >= lifetime)
                {
                    RemoveTrace(i);
                    continue;
                }

                float alpha = Mathf.Clamp01((lifetime - trace.Age) / FadeTime);
                DrawTrace(trace, alpha);
            }
        }

        private void DrawTrace(Trace trace, float alpha)
        {
            trace.Path.positionCount = 3;
            trace.Path.SetPosition(0, trace.SweepStart);
            trace.Path.SetPosition(1, trace.Muzzle);
            trace.Path.SetPosition(2, trace.End);
            SetLineColor(trace.Path, Fade(pathColor, alpha));

            trace.AimLine.positionCount = 2;
            trace.AimLine.SetPosition(0, trace.SweepStart);
            trace.AimLine.SetPosition(1, trace.AimTarget);
            SetLineColor(trace.AimLine, Fade(aimColor, alpha));

            SetMarkerColor(trace.AimMarker, Fade(aimColor, alpha));
            SetMarkerColor(trace.CursorMarker, Fade(aimColor, alpha * 0.6f));
            if (trace.ImpactMarker != null)
            {
                SetMarkerColor(trace.ImpactMarker, Fade(trace.EndColor, alpha));
            }

            // Scene view copy
            Debug.DrawLine(trace.Muzzle, trace.End, pathColor);
            Debug.DrawLine(trace.SweepStart, trace.AimTarget, aimColor);
        }

        private void OnGUI()
        {
            if (!showLabels && !showLegend)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };

            if (showLegend)
            {
                Rect legend = new Rect(10f, Screen.height - 26f, 700f, 20f);
                DrawShadowed(legend, "Musket tracer   yellow ball path   cyan aim point   green hit   orange blocked   red miss", Color.white);
            }

            if (!showLabels)
            {
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }
            }

            for (int i = 0; i < traces.Count; i++)
            {
                Trace trace = traces[i];
                if (!trace.Landed || string.IsNullOrEmpty(trace.Label))
                {
                    continue;
                }

                Vector3 screen = worldCamera.WorldToScreenPoint(trace.End + Vector3.up * 0.5f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                // GUI space runs top down and screen space bottom up
                Rect rect = new Rect(screen.x + 8f, Screen.height - screen.y - 16f, 320f, 40f);
                float alpha = Mathf.Clamp01((lifetime - trace.Age) / FadeTime);
                DrawShadowed(rect, trace.Label, Fade(trace.EndColor, alpha));
            }
        }

        private void DrawShadowed(Rect rect, string text, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, labelStyle);
            GUI.color = color;
            GUI.Label(rect, text, labelStyle);
            GUI.color = previous;
        }

        private void RemoveTrace(int index)
        {
            Trace trace = traces[index];
            traces.RemoveAt(index);
            if (trace.Root != null)
            {
                Destroy(trace.Root);
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name, float width)
        {
            GameObject host = new GameObject(name);
            host.layer = IgnoreRaycastLayer;
            host.transform.SetParent(parent, false);

            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetMaterial();
            return line;
        }

        private static Renderer CreateMarker(Transform parent, string name, float size)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.layer = IgnoreRaycastLayer;
            marker.transform.SetParent(parent, false);
            marker.transform.localScale = Vector3.one * size;

            // Removed now so aim rays and balls never touch a marker
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = GetMaterial();
            return renderer;
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private void SetMarkerColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static Color Fade(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private static Material GetMaterial()
        {
            if (sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                sharedMaterial = new Material(shader) { name = "Musket Tracer (Runtime)" };
            }

            return sharedMaterial;
        }
    }
}
