// Creates the audio mixer and AudioSystem prefab outside the engineer's Audio folder and is safe to rerun
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Hedgewitch.Audio.EditorTools
{
    public static class AudioAssetSetup
    {
        public const string Root = "Assets/Settings/Audio";
        public const string MixerPath = Root + "/HedgewitchMixer.mixer";
        public const string SystemPrefabPath = "Assets/Prefabs/Audio/AudioSystem.prefab";

        // Parent of each group with SFX children nested under SFX
        private static readonly (string group, string parent)[] Groups =
        {
            ("Music", "Master"),
            ("SFX", "Master"),
            ("Player", "SFX"),
            ("Weapons", "SFX"),
            ("Enemies", "SFX"),
            ("UI", "Master"),
            ("VO", "Master")
        };

        [MenuItem("Tools/Hedgewitch/Audio/Set Up Audio Assets")]
        public static void SetUpAll()
        {
            AudioMixer mixer = EnsureMixer();
            EnsureSystemPrefab(mixer);
            AssetDatabase.SaveAssets();
            Debug.Log("[Audio] Audio assets are set up.");
        }

        public static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null)
            {
                return null;
            }

            AudioMixerGroup[] matches = mixer.FindMatchingGroups(groupName);
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i].name == groupName)
                {
                    return matches[i];
                }
            }

            return null;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        public static AudioMixer EnsureMixer()
        {
            AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Root);

            // Unity has no public API for building mixers so this goes through the editor's own controller
            Assembly editor = typeof(Editor).Assembly;
            Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
            Type groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
            Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
            Type parameterPathBase = editor.GetType("UnityEditor.Audio.AudioParameterPath", true);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            object controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
            object master = controllerType.GetProperty("masterGroup", all).GetValue(controller);

            var byName = new Dictionary<string, object> { { "Master", master } };
            MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", all);
            MethodInfo addChild = controllerType.GetMethod("AddChildToParent", all);

            foreach ((string group, string parent) in Groups)
            {
                object created = createGroup.Invoke(controller, new object[] { group, false });
                addChild.Invoke(controller, new[] { created, byName[parent] });
                byName[group] = created;
            }

            AddDefaultView(editor, controllerType, groupType, controller, byName.Values, all);

            MethodInfo volumeGuid = groupType.GetMethod("GetGUIDForVolume", all);
            ConstructorInfo makePath = pathType.GetConstructor(all, null, new[] { groupType, typeof(GUID) }, null);
            MethodInfo addExposed = controllerType.GetMethod("AddExposedParameter", all, null, new[] { parameterPathBase }, null);
            PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", all);

            var names = new Dictionary<GUID, string>();
            foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
            {
                object group = byName[bus.ToString()];
                GUID guid = (GUID)volumeGuid.Invoke(group, null);
                addExposed.Invoke(controller, new[] { makePath.Invoke(new[] { group, (object)guid }) });
                names[guid] = AudioVolume.ParameterName(bus);
            }

            // Rename the exposed parameters from MyExposedParam to MasterVolume and so on
            Array exposed = (Array)exposedProperty.GetValue(controller);
            Type exposedType = exposed.GetType().GetElementType();
            FieldInfo guidField = exposedType.GetField("guid", all);
            FieldInfo nameField = exposedType.GetField("name", all);
            for (int i = 0; i < exposed.Length; i++)
            {
                object entry = exposed.GetValue(i);
                if (names.TryGetValue((GUID)guidField.GetValue(entry), out string parameterName))
                {
                    nameField.SetValue(entry, parameterName);
                    exposed.SetValue(entry, i);
                }
            }

            exposedProperty.SetValue(controller, exposed);

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        // A fresh controller has no group view so the mixer window would show nothing
        private static void AddDefaultView(Assembly editor, Type controllerType, Type groupType, object controller, IEnumerable<object> groups, BindingFlags all)
        {
            Type viewType = editor.GetType("UnityEditor.Audio.MixerGroupView");
            PropertyInfo views = controllerType.GetProperty("views", all);
            PropertyInfo groupId = groupType.GetProperty("groupID", all);
            if (viewType == null || views == null || groupId == null)
            {
                Debug.LogWarning("[Audio] Could not add a default mixer view. Open the mixer and add one by hand if groups are hidden.");
                return;
            }

            var guids = new List<GUID>();
            foreach (object group in groups)
            {
                guids.Add((GUID)groupId.GetValue(group));
            }

            object view = Activator.CreateInstance(viewType);
            viewType.GetField("name", all).SetValue(view, "View");
            viewType.GetField("guids", all).SetValue(view, guids.ToArray());

            Array array = Array.CreateInstance(viewType, 1);
            array.SetValue(view, 0);
            views.SetValue(controller, array);
        }

        public static GameObject EnsureSystemPrefab(AudioMixer mixer)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(SystemPrefabPath).Replace('\\', '/'));

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(SystemPrefabPath) != null;
            GameObject contents = exists ? PrefabUtility.LoadPrefabContents(SystemPrefabPath) : new GameObject("AudioSystem");

            try
            {
                AudioManager manager = GetOrAdd<AudioManager>(contents);
                SerializedObject managerData = new SerializedObject(manager);
                if (managerData.FindProperty("mixer").objectReferenceValue == null)
                {
                    managerData.FindProperty("mixer").objectReferenceValue = mixer;
                    managerData.ApplyModifiedPropertiesWithoutUndo();
                }

                Transform listener = contents.transform.Find("Listener");
                if (listener == null)
                {
                    listener = new GameObject("Listener").transform;
                    listener.SetParent(contents.transform, false);
                }

                GetOrAdd<AudioListener>(listener.gameObject);
                GetOrAdd<AudioListenerAnchor>(listener.gameObject);

                return PrefabUtility.SaveAsPrefabAsset(contents, SystemPrefabPath);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(contents);
                }
            }
        }

        public static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T found = target.GetComponent<T>();
            return found != null ? found : target.AddComponent<T>();
        }
    }
}
