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
            EnsureLibraries(mixer);
            EnsureSystemPrefab(mixer);
            AssetDatabase.SaveAssets();
            Debug.Log("[Audio] Audio assets are set up.");
        }

        public const string EventsRoot = Root + "/Events";
        public const string LibrariesRoot = Root + "/Libraries";

        private struct EventSpec
        {
            public string Name;
            public string Group;
            public float Blend;
            public bool Loop;
            public int Instances;
            public float Retrigger;
            public float Pitch;
            public float PitchVariation;
            public float VolumeVariation;

            public EventSpec(string name, string group, float blend, int instances, float retrigger, bool loop = false)
            {
                Name = name;
                Group = group;
                Blend = blend;
                Instances = instances;
                Retrigger = retrigger;
                Loop = loop;
                Pitch = 0f;
                PitchVariation = 0f;
                VolumeVariation = 0f;
            }

            public EventSpec Varied(float pitchVariation, float volumeVariation)
            {
                PitchVariation = pitchVariation;
                VolumeVariation = volumeVariation;
                return this;
            }

            public EventSpec Shifted(float semitones)
            {
                Pitch = semitones;
                return this;
            }
        }

        // Library asset then each serialized field and the event it gets
        private static readonly (Type type, string asset, (string field, EventSpec spec)[] slots)[] Libraries =
        {
            (typeof(PlayerSfx), "PlayerSfx", new[]
            {
                ("footsteps", new EventSpec("Player_Footsteps", "Player", 1f, 3, 0.08f).Varied(1f, 0.15f)),
                ("dash", new EventSpec("Player_Dash", "Player", 1f, 2, 0.1f).Varied(0.5f, 0f)),
                ("openInventory", new EventSpec("Player_OpenInventory", "UI", 0f, 1, 0.1f)),
                ("hurt", new EventSpec("Player_Hurt", "Player", 1f, 2, 0.1f).Varied(1f, 0f)),
                ("death", new EventSpec("Player_Death", "Player", 1f, 1, 0.5f))
            }),
            (typeof(BroomSfxProfile), "BroomSfx", new[]
            {
                ("equip", new EventSpec("Broom_Equip", "Weapons", 1f, 1, 0.05f)),
                ("attack", new EventSpec("Broom_Attack", "Weapons", 1f, 3, 0.05f).Varied(1.5f, 0.1f)),
                ("hit", new EventSpec("Broom_Hit", "Weapons", 1f, 4, 0.03f).Varied(1f, 0.1f))
            }),
            (typeof(GunSfxProfile), "GunSfx", new[]
            {
                ("equip", new EventSpec("Gun_Equip", "Weapons", 1f, 1, 0.05f)),
                ("reload", new EventSpec("Gun_Reload", "Weapons", 1f, 1, 0.1f)),
                ("shoot", new EventSpec("Gun_Shoot", "Weapons", 1f, 2, 0.05f).Varied(0.5f, 0.05f)),
                ("empty", new EventSpec("Gun_Empty", "Weapons", 1f, 1, 0.1f).Shifted(5f))
            }),
            (typeof(VacuumSfxProfile), "VacuumSfx", new[]
            {
                ("switchClick", new EventSpec("Vacuum_SwitchClick", "Weapons", 1f, 2, 0.03f)),
                ("powerOn", new EventSpec("Vacuum_PowerOn", "Weapons", 1f, 1, 0.05f)),
                ("runningLoop", new EventSpec("Vacuum_RunningLoop", "Weapons", 1f, 1, 0f, true)),
                ("suckUpObject", new EventSpec("Vacuum_SuckUpObject", "Weapons", 1f, 6, 0.03f).Varied(2f, 0.1f)),
                ("powerOff", new EventSpec("Vacuum_PowerOff", "Weapons", 1f, 1, 0.05f))
            }),
            (typeof(EnemySfxProfile), "EnemySfx_Basic", new[]
            {
                ("footsteps", new EventSpec("Enemy_Basic_Footsteps", "Enemies", 1f, 6, 0.05f).Varied(1f, 0.15f)),
                ("attack", new EventSpec("Enemy_Basic_Attack", "Enemies", 1f, 4, 0.1f).Varied(1f, 0f)),
                ("hurt", new EventSpec("Enemy_Basic_Hurt", "Enemies", 1f, 4, 0.05f).Varied(1f, 0f)),
                ("death", new EventSpec("Enemy_Basic_Death", "Enemies", 1f, 4, 0.05f).Varied(1f, 0f))
            }),
            (typeof(EnemySfxProfile), "EnemySfx_Bruiser", new[]
            {
                ("footsteps", new EventSpec("Enemy_Bruiser_Footsteps", "Enemies", 1f, 6, 0.05f).Varied(1f, 0.15f)),
                ("attack", new EventSpec("Enemy_Bruiser_Attack", "Enemies", 1f, 4, 0.1f).Varied(1f, 0f)),
                ("hurt", new EventSpec("Enemy_Bruiser_Hurt", "Enemies", 1f, 4, 0.05f).Varied(1f, 0f)),
                ("death", new EventSpec("Enemy_Bruiser_Death", "Enemies", 1f, 4, 0.05f).Varied(1f, 0f))
            }),
            (typeof(MusicLibrary), "MusicLibrary", new[]
            {
                ("titleTrack", new EventSpec("Music_Title", "Music", 0f, 1, 0f, true)),
                ("levelExplore", new EventSpec("Music_LevelExplore", "Music", 0f, 1, 0f, true)),
                ("levelFight", new EventSpec("Music_LevelFight", "Music", 0f, 1, 0f, true))
            })
        };

        public static T LoadLibrary<T>(string assetName) where T : SfxLibrary
        {
            return AssetDatabase.LoadAssetAtPath<T>($"{LibrariesRoot}/{assetName}.asset");
        }

        // Creates missing events and libraries and fills empty slots without touching anything already set
        public static void EnsureLibraries(AudioMixer mixer)
        {
            EnsureFolder(LibrariesRoot);

            foreach ((Type type, string assetName, (string field, EventSpec spec)[] slots) in Libraries)
            {
                string libraryPath = $"{LibrariesRoot}/{assetName}.asset";
                SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(libraryPath);
                if (library == null)
                {
                    library = (SfxLibrary)ScriptableObject.CreateInstance(type);
                    AssetDatabase.CreateAsset(library, libraryPath);
                }

                SerializedObject data = new SerializedObject(library);
                foreach ((string field, EventSpec spec) in slots)
                {
                    SerializedProperty slot = data.FindProperty(field);
                    if (slot.objectReferenceValue == null)
                    {
                        slot.objectReferenceValue = EnsureEvent(spec, mixer);
                    }
                }

                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static SoundEvent EnsureEvent(EventSpec spec, AudioMixer mixer)
        {
            string category = spec.Name.Substring(0, spec.Name.IndexOf('_'));
            string folder = $"{EventsRoot}/{category}";
            string path = $"{folder}/{spec.Name}.asset";

            SoundEvent existing = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(folder);
            SoundEvent sound = ScriptableObject.CreateInstance<SoundEvent>();
            AssetDatabase.CreateAsset(sound, path);

            SerializedObject data = new SerializedObject(sound);
            data.FindProperty("output").objectReferenceValue = FindGroup(mixer, spec.Group);
            data.FindProperty("spatialBlend").floatValue = spec.Blend;
            data.FindProperty("loop").boolValue = spec.Loop;
            data.FindProperty("maxInstances").intValue = spec.Instances;
            data.FindProperty("minRetriggerInterval").floatValue = spec.Retrigger;
            data.FindProperty("pitchOffset").floatValue = spec.Pitch;
            data.FindProperty("pitchVariation").floatValue = spec.PitchVariation;
            data.FindProperty("volumeVariation").floatValue = spec.VolumeVariation;
            data.ApplyModifiedPropertiesWithoutUndo();
            return sound;
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

                MusicManager music = GetOrAdd<MusicManager>(contents);
                SerializedObject musicData = new SerializedObject(music);
                if (musicData.FindProperty("library").objectReferenceValue == null)
                {
                    musicData.FindProperty("library").objectReferenceValue = LoadLibrary<MusicLibrary>("MusicLibrary");
                    musicData.ApplyModifiedPropertiesWithoutUndo();
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
