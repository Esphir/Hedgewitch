// Musket ball that sphere casts its whole path each frame so it never tunnels
using System;
using UnityEngine;

namespace Hedgewitch.Combat
{
    public class MusketBall : MonoBehaviour
    {
        private static Material fallbackTrailMaterial;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        private Vector3 velocity;
        private float remainingRange;
        private float radius;
        private LayerMask hitMask;
        private Transform ignoreRoot;
        private Action<RaycastHit, Vector3> onHit;
        private Vector3 firstSweepStart;
        private bool hasFirstSweep;
        private TrailRenderer trail;
        private bool finished;

        public Vector3 Velocity => velocity;

        // Sweep starts on the shooter centre line so point blank enemies are hit
        public void Launch(
            Vector3 position,
            Vector3 sweepStart,
            Vector3 direction,
            float speed,
            float range,
            float ballRadius,
            LayerMask mask,
            Transform ignore,
            Action<RaycastHit, Vector3> hitCallback)
        {
            transform.position = position;
            velocity = direction.normalized * Mathf.Max(0.1f, speed);
            remainingRange = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.001f, ballRadius);
            hitMask = mask;
            ignoreRoot = ignore;
            onHit = hitCallback;
            firstSweepStart = sweepStart;
            hasFirstSweep = true;
            finished = false;

            if (trail == null)
            {
                trail = GetComponentInChildren<TrailRenderer>();
            }

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        private void Update()
        {
            if (finished)
            {
                return;
            }

            Vector3 from = hasFirstSweep ? firstSweepStart : transform.position;
            Vector3 to = transform.position + velocity * Time.deltaTime;
            hasFirstSweep = false;

            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-5f)
            {
                return;
            }

            Vector3 direction = delta / distance;
            int count = Physics.SphereCastNonAlloc(
                from, radius, direction, hits, distance, hitMask, QueryTriggerInteraction.Ignore);

            int nearestIndex = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == null || (ignoreRoot != null && hitTransform.IsChildOf(ignoreRoot)))
                {
                    continue;
                }

                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex >= 0)
            {
                RaycastHit hit = hits[nearestIndex];

                // A cast starting inside a collider reports zero so fall back to the start
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                {
                    hit.point = from;
                }

                transform.position = from + direction * hit.distance;
                onHit?.Invoke(hit, direction);
                Finish();
                return;
            }

            transform.position = to;
            remainingRange -= distance;
            if (remainingRange <= 0f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            finished = true;

            // Let the trail fade out instead of vanishing with the ball
            if (trail != null)
            {
                trail.emitting = false;
                trail.autodestruct = true;
                trail.transform.SetParent(null, true);
                trail = null;
            }

            Destroy(gameObject);
        }

        // Plain lead ball with a short trail built without a prefab
        public static MusketBall CreateDefault(Material ballMaterial, Material trailMaterial, Color trailColor)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Musket Ball";
            ball.transform.localScale = Vector3.one * 0.09f;

            Collider collider = ball.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = ball.GetComponent<Renderer>();
            if (renderer != null && ballMaterial != null)
            {
                renderer.sharedMaterial = ballMaterial;
            }

            GameObject trailHost = new GameObject("Trail");
            trailHost.transform.SetParent(ball.transform, false);

            TrailRenderer trailRenderer = trailHost.AddComponent<TrailRenderer>();
            trailRenderer.time = 0.09f;
            trailRenderer.minVertexDistance = 0.05f;
            trailRenderer.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sharedMaterial = trailMaterial != null ? trailMaterial : GetFallbackTrailMaterial();

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
                new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            trailRenderer.colorGradient = gradient;

            return ball.AddComponent<MusketBall>();
        }

        private static Material GetFallbackTrailMaterial()
        {
            if (fallbackTrailMaterial != null)
            {
                return fallbackTrailMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                fallbackTrailMaterial = new Material(shader) { name = "Musket Trail (Runtime)" };
            }

            return fallbackTrailMaterial;
        }
    }
}
