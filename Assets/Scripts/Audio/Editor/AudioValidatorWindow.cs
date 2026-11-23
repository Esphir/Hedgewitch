// Window listing every sound event and library slot that still needs work
using UnityEditor;
using UnityEngine;

namespace Hedgewitch.Audio.EditorTools
{
    public class AudioValidatorWindow : EditorWindow
    {
        private AudioValidator.Report report;
        private Vector2 scroll;
        private bool hasReport;

        [MenuItem("Tools/Hedgewitch/Audio Validator")]
        public static void Open()
        {
            AudioValidatorWindow window = GetWindow<AudioValidatorWindow>("Audio Validator");
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void Refresh()
        {
            report = AudioValidator.Run();
            hasReport = true;
            Repaint();
        }

        private void OnGUI()
        {
            if (!hasReport)
            {
                Refresh();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"{report.EventCount - report.EmptyEventCount} of {report.EventCount} sound events have clips", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Refresh();
                }
            }

            if (report.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Every sound event has clips and every slot is filled.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            AudioValidator.Severity? heading = null;

            foreach (AudioValidator.Issue issue in report.Issues)
            {
                if (heading != issue.Severity)
                {
                    heading = issue.Severity;
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(Heading(issue.Severity), EditorStyles.boldLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(issue.Message, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = issue.Asset;
                        EditorGUIUtility.PingObject(issue.Asset);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static string Heading(AudioValidator.Severity severity)
        {
            switch (severity)
            {
                case AudioValidator.Severity.NeedsClips:
                    return "Needs clips";
                case AudioValidator.Severity.Unassigned:
                    return "Empty library slots";
                default:
                    return "Setup problems";
            }
        }
    }
}
