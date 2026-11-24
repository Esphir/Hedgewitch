// Base for the category assets with built in slots plus extra named sounds the engineer adds
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hedgewitch.Audio
{
    [Serializable]
    public struct SoundSlot
    {
        [Tooltip("Unique name used by Sound Emitters and code to find this sound.")]
        public string name;

        public SoundEvent sound;

        public SoundSlot(string name, SoundEvent sound)
        {
            this.name = name;
            this.sound = sound;
        }
    }

    public abstract class SfxLibrary : ScriptableObject
    {
        [Tooltip("Your own sounds. Give each one a unique name so a Sound Emitter or code can find it.")]
        [SerializeField] private List<SoundSlot> extraSounds = new List<SoundSlot>();

        private static readonly Dictionary<Type, FieldInfo[]> builtInFields = new Dictionary<Type, FieldInfo[]>();

        public IReadOnlyList<SoundSlot> ExtraSounds => extraSounds;

        // Built in slots by name such as Footsteps then extra sounds by name
        public SoundEvent Find(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return null;
            }

            foreach (FieldInfo field in BuiltInFields(GetType()))
            {
                if (string.Equals(field.Name, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    return field.GetValue(this) as SoundEvent;
                }
            }

            for (int i = 0; i < extraSounds.Count; i++)
            {
                if (string.Equals(extraSounds[i].name, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    return extraSounds[i].sound;
                }
            }

            return null;
        }

        public bool HasSlot(string slotName)
        {
            foreach (string existing in SlotNames())
            {
                if (string.Equals(existing, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<string> SlotNames()
        {
            foreach (FieldInfo field in BuiltInFields(GetType()))
            {
                yield return char.ToUpperInvariant(field.Name[0]) + field.Name.Substring(1);
            }

            for (int i = 0; i < extraSounds.Count; i++)
            {
                if (!string.IsNullOrEmpty(extraSounds[i].name))
                {
                    yield return extraSounds[i].name;
                }
            }
        }

        public void AddExtraSound(string slotName, SoundEvent sound)
        {
            extraSounds.Add(new SoundSlot(slotName, sound));
        }

        // Serialized SoundEvent fields declared on the concrete library types
        public static FieldInfo[] BuiltInFields(Type type)
        {
            if (builtInFields.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }

            var found = new List<FieldInfo>();
            for (Type current = type; current != null && current != typeof(SfxLibrary); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    bool serialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                    if (serialized && field.FieldType == typeof(SoundEvent))
                    {
                        found.Add(field);
                    }
                }
            }

            cached = found.ToArray();
            builtInFields[type] = cached;
            return cached;
        }
    }
}
