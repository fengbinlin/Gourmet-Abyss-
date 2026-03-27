using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryProgressData", menuName = "Story/Story Progress Data")]
public class StoryProgressData : ScriptableObject
{
    [Serializable]
    public class StoryProgressEntry
    {
        public string storyId;
        public string sceneName;
        public bool cleared;
    }

    [SerializeField] private List<StoryProgressEntry> entries = new List<StoryProgressEntry>();

    public bool TryGetCleared(string storyId, string sceneName, out bool cleared)
    {
        StoryProgressEntry entry = entries.Find(e => e.storyId == storyId && e.sceneName == sceneName);
        if (entry != null)
        {
            cleared = entry.cleared;
            return true;
        }

        cleared = false;
        return false;
    }

    public void SetCleared(string storyId, string sceneName, bool value)
    {
        StoryProgressEntry entry = entries.Find(e => e.storyId == storyId && e.sceneName == sceneName);
        if (entry == null)
        {
            entry = new StoryProgressEntry
            {
                storyId = storyId,
                sceneName = sceneName,
                cleared = value
            };
            entries.Add(entry);
            return;
        }

        entry.cleared = value;
    }
}
