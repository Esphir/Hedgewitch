// Adds the dash its placeholder poses and its wind and dust effects to the Player prefab and is safe to rerun
using UnityEditor;
using UnityEngine;

namespace Hedgewitch.Combat.EditorTools
{
    public static class PlayerDashSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string MaterialFolder = "Assets/Demo/Materials";
        private const string FxMaterialPath = MaterialFolder + "/DashFx.mat";
        private const string UrpParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        [MenuItem("Tools/Hedgewitch/Player/Set Up Dash", false, 50)]
        public static void SetUpPlayerDash()
        {
            Material fxMaterial = EnsureFxMaterial();

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform body = EnsureBody(contents);
                Transform fxRoot = FindOrCreate(contents.transform, "Dash FX");
                ParticleSystem wind = EnsureWind(fxRoot, fxMaterial);
                ParticleSystem dust = EnsureDust(fxRoot, fxMaterial);

                if (contents.GetComponent<PlayerDash>() == null)
                {
                    contents.AddComponent<PlayerDash>();
                }

                DashAnimator animator = contents.GetComponent<DashAnimator>();
                if (animator == null)
                {
                    animator = contents.AddComponent<DashAnimator>();
                }

                SerializedObject data = new SerializedObject(animator);
                data.FindProperty("body").objectReferenceValue = body;
                data.FindProperty("wind").objectReferenceValue = wind;
                data.FindProperty("dust").objectReferenceValue = dust;
                data.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Dash] Player dash is set up.");
        }

        // Moves the capsule look onto a child so poses can tilt it without moving the collider
        private static Transform EnsureBody(GameObject root)
        {
            Transform body = root.transform.Find("Body");
            MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
            MeshFilter rootFilter = root.GetComponent<MeshFilter>();

            if (body == null)
            {
                GameObject created = new GameObject("Body");
                body = created.transform;
                body.SetParent(root.transform, false);
                body.SetSiblingIndex(0);

                MeshFilter filter = created.AddComponent<MeshFilter>();
                filter.sharedMesh = rootFilter != null && rootFilter.sharedMesh != null
                    ? rootFilter.sharedMesh
                    : Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx");

                MeshRenderer renderer = created.AddComponent<MeshRenderer>();
                if (rootRenderer != null)
                {
                    renderer.sharedMaterials = rootRenderer.sharedMaterials;
                    renderer.shadowCastingMode = rootRenderer.shadowCastingMode;
                }
            }

            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            return body;
        }

        private static ParticleSystem EnsureWind(Transform parent, Material material)
        {
            ParticleSystem system = EnsureSystem(parent, "Dash Wind", out bool created);
            if (!created)
            {
                return system;
            }

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(1f, 1f, 1f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 80f;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.12f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, 0.8f);

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static ParticleSystem EnsureDust(Transform parent, Material material)
        {
            ParticleSystem system = EnsureSystem(parent, "Dash Dust", out bool created);
            if (!created)
            {
                return system;
            }

            Color dust = new Color(0.66f, 0.58f, 0.46f, 0.7f);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = dust;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 50f;
            emission.enabled = false;

            // Spray back and up from the bristles
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-30f, 0f, 0f);

            ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, 0.9f);

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static ParticleSystem EnsureSystem(Transform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            created = existing == null || existing.GetComponent<ParticleSystem>() == null;
            GameObject host = existing != null ? existing.gameObject : new GameObject(name);
            host.transform.SetParent(parent, false);
            ParticleSystem system = host.GetComponent<ParticleSystem>();
            if (system == null)
            {
                system = host.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            return system;
        }

        private static ParticleSystem.MinMaxGradient Fade(Color color, float startAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        // Soft round particle in URP that tints by particle colour
        private static Material EnsureFxMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FxMaterialPath);
            if (material != null)
            {
                return material;
            }

            Material source = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterial);
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                material = new Material(shader);
            }

            material.name = "DashFx";
            AssetDatabase.CreateAsset(material, FxMaterialPath);
            return material;
        }

        private static Transform FindOrCreate(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
