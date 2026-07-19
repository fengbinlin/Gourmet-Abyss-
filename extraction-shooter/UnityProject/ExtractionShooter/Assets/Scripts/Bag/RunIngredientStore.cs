using System;
using System.Collections.Generic;

/// <summary>Pure data model for ingredients collected during one dungeon run.</summary>
public sealed class RunIngredientStore
{
    private readonly Dictionary<ResourceType, int> counts = new Dictionary<ResourceType, int>();

    public event Action<ResourceType, int, int> Changed;

    public int Count { get { return counts.Count; } }

    public bool Add(ResourceType type, int amount)
    {
        if (amount <= 0) return false;

        int oldCount = GetCount(type);
        int newCount = (int)Math.Min(int.MaxValue, (long)oldCount + amount);
        if (newCount == oldCount) return false;

        counts[type] = newCount;
        if (Changed != null)
            Changed(type, oldCount, newCount);
        return true;
    }

    public int GetCount(ResourceType type)
    {
        int count;
        return counts.TryGetValue(type, out count) ? count : 0;
    }

    public Dictionary<ResourceType, int> GetSnapshot()
    {
        return new Dictionary<ResourceType, int>(counts);
    }

    public void Clear()
    {
        if (counts.Count == 0) return;

        Dictionary<ResourceType, int> snapshot = GetSnapshot();
        counts.Clear();
        foreach (KeyValuePair<ResourceType, int> entry in snapshot)
        {
            if (Changed != null)
                Changed(entry.Key, entry.Value, 0);
        }
    }
}
