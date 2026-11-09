using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hedgewitch.Vacuum.Demo;

namespace Hedgewitch.Vacuum.EditorTools
{
    /// <summary>Brings an existing scene's vacuum rig up to date without rebuilding it.</summary>
    public static class VacuumRigUpdater
    {
        private const string DemoScenePath = "Assets/Scenes/VacuumDemo.unity";
        private const string SettingsPath = "Assets/Settings/Vacuum/VacuumSettings.asset";

        private static readonly Vector3 NozzleLocalPosition = new Vector3(0.28f, 0f, 0.5f);

        private static readonly Vector3 NozzleLocalEuler = new Vector3(15f, 0f, 0f);

        private const float RigLocalHeight = -0.05f;

        [MenuItem("Tools/Hedgewitch/Update Vacuum Rig In Open Scene", false, 40)]
        public static void UpdateOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Apply(out string report))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(report);
        }

        public static void UpdateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            bool changed = Apply(out string report);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(report);
        }

        private static bool Apply(out string report)
        {
            VacuumController controller = Object.FindAnyObjectByType<VacuumController>();
            if (controller == null)
            {
                report = "[Vacuum] No VacuumController in the open scene; nothing to update.";
                return false;
            }

            bool changed = false;
            string log = "[Vacuum] rig update:";

            // 1. Cone angles, which an existing asset cannot pick up from new C# defaults
            //    because they are already serialized in it.
            VacuumSettings settings = AssetDatabase.LoadAssetAtPath<VacuumSettings>(SettingsPath);
            if (settings != null)
            {
                if (!Mathf.Approximately(settings.horizontalConeAngle, 55f)
                    || !Mathf.Approximately(settings.verticalConeAngle, 50f))
                {
                    log += $"\n  cone {settings.horizontalConeAngle:0.#}x{settings.verticalConeAngle:0.#}"
                           + " -> 55x50 deg";
                    settings.horizontalConeAngle = 55f;
                    settings.verticalConeAngle = 50f;
                    changed = true;
                }

                if (settings.intakeRadius <= 0f)
                {
                    log += "\n  intakeRadius -> 1.6 m";
                    settings.intakeRadius = 1.6f;
                    changed = true;
                }

                log += $"\n  intakeRadius now {settings.intakeRadius:0.##} m,"
                       + $" hold margin {settings.coneHysteresis:0.##}";

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            else
            {
                log += $"\n  settings asset not found at {SettingsPath}; skipped";
            }

            // 2. Nozzle placement. A mouth at chest height looks out over everything on the
            //    floor, which is what forces the player to aim down at small objects.
            VacuumNozzle nozzle = controller.Nozzle;
            if (nozzle != null)
            {
                Transform nozzleTransform = nozzle.transform;
                if (Vector3.Distance(nozzleTransform.localPosition, NozzleLocalPosition) > 0.001f
                    || Quaternion.Angle(nozzleTransform.localRotation, Quaternion.Euler(NozzleLocalEuler)) > 0.5f)
                {
                    log += $"\n  nozzle {nozzleTransform.localPosition} -> {NozzleLocalPosition},"
                           + $" tilt -> {NozzleLocalEuler.x:0.#} deg down";
                    nozzleTransform.localPosition = NozzleLocalPosition;
                    nozzleTransform.localRotation = Quaternion.Euler(NozzleLocalEuler);
                    changed = true;
                }

                Transform rig = controller.transform;
                if (!Mathf.Approximately(rig.localPosition.y, RigLocalHeight))
                {
                    Vector3 position = rig.localPosition;
                    log += $"\n  rig height {position.y:0.##} -> {RigLocalHeight:0.##}";
                    position.y = RigLocalHeight;
                    rig.localPosition = position;
                    changed = true;
                }
            }
            else
            {
                log += "\n  controller has no nozzle assigned; placement skipped";
            }

            // 3. The tuning panel, so the feel can be dialled in during play.
            if (Object.FindAnyObjectByType<VacuumTuningPanel>() == null)
            {
                VacuumDebugHud hud = Object.FindAnyObjectByType<VacuumDebugHud>();
                GameObject host = hud != null ? hud.gameObject : new GameObject("Vacuum Debug HUD");
                host.AddComponent<VacuumTuningPanel>();
                log += $"\n  added VacuumTuningPanel to '{host.name}' (F1 in play mode)";
                changed = true;
            }

            if (!changed)
            {
                log += "\n  already up to date";
            }

            report = log;
            return changed;
        }
    }
}
