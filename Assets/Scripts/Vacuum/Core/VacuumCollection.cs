using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Per-vacuum tally of everything swallowed.</summary>
    [DisallowMultipleComponent]
    public class VacuumCollection : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public VacuumCategory category;
            public int count;
            public int value;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private int totalValue;
        [SerializeField] private int totalCount;

        public int TotalValue => totalValue;

        public int TotalCount => totalCount;

        public IReadOnlyList<Entry> Entries => entries;

        public event Action<VacuumCategory, int, int> Collected;

        public void Add(VacuumCategory category, int count = 1, int value = 0)
        {
            bool merged = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].category != category)
                {
                    continue;
                }

                Entry existing = entries[i];
                existing.count += count;
                existing.value += value;
                entries[i] = existing;
                merged = true;
                break;
            }

            if (!merged)
            {
                entries.Add(new Entry { category = category, count = count, value = value });
            }

            totalCount += count;
            totalValue += value;
            Collected?.Invoke(category, count, value);
        }

        public int GetCount(VacuumCategory category)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].category == category)
                {
                    return entries[i].count;
                }
            }

            return 0;
        }

        public void ResetCollection()
        {
            entries.Clear();
            totalCount = 0;
            totalValue = 0;
        }
    }
}
