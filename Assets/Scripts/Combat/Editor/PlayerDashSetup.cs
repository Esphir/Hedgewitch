// Adds the dash its animation clips and its wind and dust effects to the Player prefab and is safe to rerun
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hedgewitch.Combat.EditorTools
{
    public static class PlayerDashSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string MaterialFolder = "Assets/Demo/Materials";
        private const string FxMaterialPath = MaterialFolder + "/DashFx.mat";
        private const string UrpParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        public const string AnimationFolder = "Assets/Animations/Player";
        public const string ControllerPath = AnimationFolder + "/PlayerDash.controller";
        private const float BakeFrameRate = 60f;

        [MenuItem("Tools/Hedgewitch/Player/Set Up Dash", false, 50)]
        public static void SetUpPlayerDash()
        {
            Build(false);
        }

        // Replaces the clips with fresh bakes of the pose settings and loses any hand edits
        [MenuItem("Tools/Hedgewitch/Player/Rebake Dash Clips", false, 51)]
        public static void RebakeDashClips()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Rebake dash clips",
                    "This overwrites Dash, DashBroomRide and DashBroomSweep with fresh bakes from the Dash Animator's pose settings. Any edits made in the Animation window are lost.",
                    "Rebake", "Cancel"))
            {
                return;
            }

            Build(true);
        }

        private static void Build(bool overwriteClips)
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

                Animator clipPlayer = EnsureAnimator(contents, animator, body, overwriteClips);
                data.Update();
                data.FindProperty("animator").objectReferenceValue = clipPlayer;
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

        // Off by default because DashAnimator switches it on only while dashing
        private static Animator EnsureAnimator(GameObject root, DashAnimator look, Transform body, bool overwriteClips)
        {
            PlayerDash dash = root.GetComponent<PlayerDash>();
            BroomWeapon broom = root.GetComponentInChildren<BroomWeapon>(true);
            Transform pivot = broom != null ? broom.SwingPivot : null;
            DashRig rig = DashRig.Capture(root.transform, body, pivot);

            EnsureFolder(AnimationFolder);
            Dictionary<DashStyle, AnimationClip> clips = new Dictionary<DashStyle, AnimationClip>();
            foreach (DashStyle style in new[] { DashStyle.Regular, DashStyle.BroomRide, DashStyle.BroomSweep })
            {
                string name = look.StateFor(style);
                string path = $"{AnimationFolder}/{name}.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool fresh = clip == null;
                if (fresh)
                {
                    clip = new AnimationClip { name = name };
                    AssetDatabase.CreateAsset(clip, path);
                }

                if (fresh || overwriteClips)
                {
                    BakeClip(clip, style, dash.MoveFor(style).duration, look.Poses, rig, root.transform, body, pivot);
                }

                clips[style] = clip;
            }

            AnimatorController controller = EnsureController(look, clips);

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = false;
            return animator;
        }

        // Samples the shared pose maths into keys the Animation window can edit
        private static void BakeClip(AnimationClip clip, DashStyle style, float duration, DashPoseSettings settings, DashRig rig,
            Transform root, Transform body, Transform pivot)
        {
            clip.ClearCurves();
            clip.frameRate = BakeFrameRate;

            string bodyPath = AnimationUtility.CalculateTransformPath(body, root);
            bool animateBroom = style != DashStyle.Regular && pivot != null;
            string broomPath = animateBroom ? AnimationUtility.CalculateTransformPath(pivot, root) : null;

            // Broom keys are local to the pivot's parent rather than the player root
            Vector3 parentPosition = Vector3.zero;
            Quaternion parentRotation = Quaternion.identity;
            if (animateBroom && pivot.parent != null)
            {
                parentPosition = root.InverseTransformPoint(pivot.parent.position);
                parentRotation = Quaternion.Inverse(root.rotation) * pivot.parent.rotation;
            }

            Quaternion inverseParent = Quaternion.Inverse(parentRotation);
            CurveSet curves = new CurveSet();
            int frames = Mathf.Max(2, Mathf.CeilToInt(duration * BakeFrameRate));
            for (int i = 0; i <= frames; i++)
            {
                float time = duration * i / frames;
                DashPose pose = DashPoseSolver.Evaluate(style, time, duration, settings, rig);

                curves.Position(bodyPath, time, pose.BodyPosition);
                curves.Rotation(bodyPath, time, pose.BodyRotation);
                curves.Scale(bodyPath, time, pose.BodyScale);

                if (animateBroom)
                {
                    curves.Position(broomPath, time, inverseParent * (pose.BroomPosition - parentPosition));
                    curves.Rotation(broomPath, time, inverseParent * pose.BroomRotation);
                }
            }

            curves.WriteTo(clip);
            clip.EnsureQuaternionContinuity();

            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
        }

        // One state per clip and no transitions because the code plays states directly
        private static AnimatorController EnsureController(DashAnimator look, Dictionary<DashStyle, AnimationClip> clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idle = FindState(machine, "Idle") ?? machine.AddState("Idle", new Vector3(260f, 0f, 0f));
            idle.writeDefaultValues = false;
            machine.defaultState = idle;

            float row = 80f;
            foreach (KeyValuePair<DashStyle, AnimationClip> entry in clips)
            {
                string name = look.StateFor(entry.Key);
                AnimatorState state = FindState(machine, name) ?? machine.AddState(name, new Vector3(260f, row, 0f));
                row += 70f;

                // Write defaults off so a clip never touches what it does not animate
                state.writeDefaultValues = false;
                if (state.motion == null)
                {
                    state.motion = entry.Value;
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // Collects keys per property then writes each as a curve with straight-line tangents
        private class CurveSet
        {
            private readonly Dictionary<(string path, string property), List<Keyframe>> keys =
                new Dictionary<(string path, string property), List<Keyframe>>();

            private readonly Dictionary<string, Quaternion> lastRotation = new Dictionary<string, Quaternion>();

            public void Position(string path, float time, Vector3 value)
            {
                Add(path, "m_LocalPosition.x", time, value.x);
                Add(path, "m_LocalPosition.y", time, value.y);
                Add(path, "m_LocalPosition.z", time, value.z);
            }

            public void Scale(string path, float time, Vector3 value)
            {
                Add(path, "m_LocalScale.x", time, value.x);
                Add(path, "m_LocalScale.y", time, value.y);
                Add(path, "m_LocalScale.z", time, value.z);
            }

            // Keeps each key on the same side as the last so the roll never spins the long way
            public void Rotation(string path, float time, Quaternion value)
            {
                if (lastRotation.TryGetValue(path, out Quaternion previous) && Quaternion.Dot(previous, value) < 0f)
                {
                    value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
                }

                lastRotation[path] = value;
                Add(path, "m_LocalRotation.x", time, value.x);
                Add(path, "m_LocalRotation.y", time, value.y);
                Add(path, "m_LocalRotation.z", time, value.z);
                Add(path, "m_LocalRotation.w", time, value.w);
            }

            private void Add(string path, string property, float time, float value)
            {
                if (!keys.TryGetValue((path, property), out List<Keyframe> list))
                {
                    list = new List<Keyframe>();
                    keys[(path, property)] = list;
                }

                list.Add(new Keyframe(time, value));
            }

            public void WriteTo(AnimationClip clip)
            {
                foreach (KeyValuePair<(string path, string property), List<Keyframe>> entry in keys)
                {
                    List<Keyframe> list = entry.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        Keyframe key = list[i];
                        float before = i > 0 ? Slope(list[i - 1], key) : 0f;
                        float after = i < list.Count - 1 ? Slope(key, list[i + 1]) : before;
                        key.inTangent = i > 0 ? before : after;
                        key.outTangent = after;
                        list[i] = key;
                    }

                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(entry.Key.path, typeof(Transform), entry.Key.property);
                    AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(list.ToArray()));
                }
            }

            private static float Slope(Keyframe a, Keyframe b)
            {
                float span = b.time - a.time;
                return span > 1e-6f ? (b.value - a.value) / span : 0f;
            }
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
