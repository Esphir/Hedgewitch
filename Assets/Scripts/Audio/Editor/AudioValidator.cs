// Finds sound events and library slots that still need clips so the audio engineer can see what is left
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hedgewitch.Audio.EditorTools
{
    public static class AudioValidator
    {
        public enum Severity
        {
            NeedsClips,
            Unassigned,
            Setup
        }

        public struct Issue
        {
            public Severity Severity;
            public Object Asset;
            public string Where;
            public string Message;
        }

        public struct Report
        {
            public List<Issue> Issues;
            public int EventCount;
            public int EmptyEventCount;
            public int LibraryCount;
        }

        // Slots whose events should keep playing until stopped
        private static readonly HashSet<string> LoopingSlots = new HashSet<string>
        {
            "runningLoop", "titleTrack", "levelExplore", "levelFight"
        };

        public static Report Run()
        {
            Report report = new Report { Issues = new List<Issue>() };

            foreach (string guid in AssetDatabase.FindAssets("t:SoundEvent"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SoundEvent sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
                if (sound == null)
                {
                    continue;
                }

                report.EventCount++;
                if (!sound.HasClips)
                {
                    report.EmptyEventCount++;
                    report.Issues.Add(new Issue
                    {
                        Severity = Severity.NeedsClips,
                        Asset = sound,
                        Where = path,
                        Message = $"{sound.EventName} has no clips"
                    });
                }

                if (sound.Output == null)
                {
                    report.Issues.Add(new Issue
                    {
                        Severity = Severity.Setup,
                        Asset = sound,
                        Where = path,
                        Message = $"{sound.EventName} has no mixer group"
                    });
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:SfxLibrary"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(path);
                if (library == null)
                {
                    continue;
                }

                report.LibraryCount++;
                SerializedProperty property = new SerializedObject(library).GetIterator();
                for (bool enter = true; property.NextVisible(enter); enter = false)
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                    {
                        continue;
                    }

                    string slot = $"{library.name} > {property.displayName}";
                    SoundEvent sound = property.objectReferenceValue as SoundEvent;

                    if (sound == null)
                    {
                        report.Issues.Add(new Issue
                        {
                            Severity = Severity.Unassigned,
                            Asset = library,
                            Where = path,
                            Message = $"{slot} has no sound event"
                        });
                    }
                    else if (LoopingSlots.Contains(property.name) && !sound.Loop)
                    {
                        report.Issues.Add(new Issue
                        {
                            Severity = Severity.Setup,
                            Asset = sound,
                            Where = path,
                            Message = $"{slot} uses {sound.EventName} which should have Loop ticked"
                        });
                    }
                }

                foreach (string problem in SoundLibraryTools.NameProblems(library))
                {
                    report.Issues.Add(new Issue { Severity = Severity.Setup, Asset = library, Where = path, Message = problem });
                }

                foreach (SoundSlot extra in library.ExtraSounds)
                {
                    if (extra.sound == null && !string.IsNullOrWhiteSpace(extra.name))
                    {
                        report.Issues.Add(new Issue
                        {
                            Severity = Severity.Unassigned,
                            Asset = library,
                            Where = path,
                            Message = $"{library.name} > {extra.name} has no sound event"
                        });
                    }
                }
            }

            report.Issues.Sort((a, b) => a.Severity != b.Severity ? a.Severity.CompareTo(b.Severity) : string.CompareOrdinal(a.Message, b.Message));
            return report;
        }

        // Prints the report to the console for batch runs
        [MenuItem("Tools/Hedgewitch/Audio/Log Validation Report")]
        public static void LogReport()
        {
            Report report = Run();
            StringBuilder text = new StringBuilder();
            text.AppendLine($"[Audio] {report.EmptyEventCount} of {report.EventCount} sound events still need clips. {report.Issues.Count} issues across {report.LibraryCount} libraries.");
            foreach (Issue issue in report.Issues)
            {
                text.AppendLine($"  {issue.Severity}: {issue.Message}");
            }

            Debug.Log(text.ToString());
        }
    }
}
