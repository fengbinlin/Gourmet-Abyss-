using System.Collections.Generic;
using Game.Core;
using UnityEngine;

[DefaultExecutionOrder(-120)] // 比大多数管理器更早初始化
public class AudioManager : PersistentMonoSingleton<AudioManager>
{
    [System.Serializable]
    public class AudioData
    {
        public string audioID;
        public AudioClip audioClip;
    }

    [System.Serializable]
    public class AudioConfig
    {
        [Header("音频配置列表")]
        public List<AudioData> audioDatas = new List<AudioData>();

        [Header("播放限制设置")]
        [Tooltip("最大同时播放音频数量，0表示无限制")]
        public int maxSimultaneousAudio = 0; // 0表示无限制

        [Tooltip("达到最大数量时的处理策略")]
        public OverflowStrategy overflowStrategy = OverflowStrategy.RejectNew;

        [Tooltip("对象池初始大小")]
        public int initialPoolSize = 20;

        [Tooltip("是否在Awake时预加载所有音频")]
        public bool preloadAllAudio = true;
    }

    public enum OverflowStrategy
    {
        RejectNew,      // 拒绝新的播放请求
        StopOldest,     // 停止最早播放的音频
        StopQuietest,   // 停止音量最小的音频
        StopSameType   // 停止相同类型的音频(如果存在)
    }

    [SerializeField] private AudioConfig config = new AudioConfig();

    private Dictionary<string, AudioClip> audioDictionary = new Dictionary<string, AudioClip>();
    private Queue<AudioSource> audioSourcePool = new Queue<AudioSource>();
    private List<ActiveAudioInfo> activeAudioSources = new List<ActiveAudioInfo>();
    private float cleanupTimer = 0f;
    private const float CLEANUP_INTERVAL = 1f; // 每1秒清理一次

    // 活跃音频信息
    private class ActiveAudioInfo
    {
        public AudioSource audioSource;
        public string audioID;
        public float startTime;
        public float volume;

        public ActiveAudioInfo(AudioSource source, string id, float vol)
        {
            audioSource = source;
            audioID = id;
            startTime = Time.time;
            volume = vol;
        }
    }

    protected override void OnAwake()
    {
        Initialize();
    }

    private void Initialize()
    {
        InitializeAudioDictionary();
        InitializeObjectPool();
    }

    private void InitializeAudioDictionary()
    {
        audioDictionary.Clear();
        foreach (var audioData in config.audioDatas)
        {
            if (!audioDictionary.ContainsKey(audioData.audioID))
            {
                audioDictionary.Add(audioData.audioID, audioData.audioClip);
            }
            else
            {
                Debug.LogWarning($"音频ID重复: {audioData.audioID}");
            }
        }
    }

    private void InitializeObjectPool()
    {
        for (int i = 0; i < config.initialPoolSize; i++)
        {
            CreatePooledAudioSource();
        }
    }

    private void CreatePooledAudioSource()
    {
        GameObject audioSourceObj = new GameObject($"PooledAudioSource");
        audioSourceObj.transform.SetParent(transform);
        AudioSource audioSource = audioSourceObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSourceObj.SetActive(false);
        audioSourcePool.Enqueue(audioSource);
    }

    private void Update()
    {
        // 定期清理已播放完毕的音频
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            CleanupFinishedAudio();
            cleanupTimer = 0f;
        }
    }

    /// <summary>
    /// 清理已播放完毕的音频
    /// </summary>
    private void CleanupFinishedAudio()
    {
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            var audioInfo = activeAudioSources[i];
            if (audioInfo.audioSource == null ||
                !audioInfo.audioSource.gameObject.activeSelf ||
                !audioInfo.audioSource.isPlaying)
            {
                if (audioInfo.audioSource != null)
                {
                    ReturnAudioSourceToPool(audioInfo.audioSource);
                }
                else
                {
                    // 只有当 audioSource 为 null 时才手动移除
                    // 因为 ReturnAudioSourceToPool 不会被调用
                    activeAudioSources.RemoveAt(i);
                }
                // 删掉原来这行: activeAudioSources.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 播放音频
    /// </summary>
    /// <param name="audioID">音频ID</param>
    /// <param name="volume">音量(0-1)</param>
    /// <returns>是否播放成功</returns>
    public bool PlayAudio(string audioID, float volume = 1.0f)
    {
        if (!audioDictionary.ContainsKey(audioID))
        {
            Debug.LogError($"未找到音频ID: {audioID}");
            return false;
        }

        // 检查是否达到最大数量限制
        if (config.maxSimultaneousAudio > 0 &&
            activeAudioSources.Count >= config.maxSimultaneousAudio)
        {
            if (!HandleOverflow(audioID, volume))
            {
                Debug.LogWarning($"已达到最大同时播放音频数量({config.maxSimultaneousAudio})，拒绝播放: {audioID}");
                return false;
            }
        }

        return PlayNewAudio(audioID, volume);
    }

    /// <summary>
    /// 处理音频数量超限的情况
    /// </summary>
    /// <returns>是否处理成功</returns>
    private bool HandleOverflow(string newAudioID, float newVolume)
    {
        switch (config.overflowStrategy)
        {
            case OverflowStrategy.RejectNew:
                return false;

            case OverflowStrategy.StopOldest:
                return StopOldestAudio(newAudioID, newVolume);

            case OverflowStrategy.StopQuietest:
                return StopQuietestAudio(newAudioID, newVolume);

            case OverflowStrategy.StopSameType:
                return StopSameTypeAudio(newAudioID, newVolume);

            default:
                return false;
        }
    }

    /// <summary>
    /// 播放新音频
    /// </summary>
    private bool PlayNewAudio(string audioID, float volume)
    {
        AudioSource audioSource = GetAvailableAudioSource();
        if (audioSource == null)
        {
            // 对象池中没有可用的，创建一个新的
            CreatePooledAudioSource();
            audioSource = audioSourcePool.Dequeue();
        }

        if (audioSource == null)
        {
            Debug.LogError("无法获取AudioSource");
            return false;
        }

        audioSource.gameObject.name = $"Audio_{audioID}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        audioSource.clip = audioDictionary[audioID];
        audioSource.volume = Mathf.Clamp01(volume);
        audioSource.gameObject.SetActive(true);
        audioSource.Play();

        // 添加到活跃音频列表
        activeAudioSources.Add(new ActiveAudioInfo(audioSource, audioID, volume));

        return true;
    }

    /// <summary>
    /// 停止最早播放的音频
    /// </summary>
    private bool StopOldestAudio(string newAudioID, float newVolume)
    {
        if (activeAudioSources.Count == 0)
            return false;

        // 找到最早播放的音频
        ActiveAudioInfo oldest = null;
        float oldestTime = float.MaxValue;

        foreach (var audioInfo in activeAudioSources)
        {
            if (audioInfo.startTime < oldestTime)
            {
                oldestTime = audioInfo.startTime;
                oldest = audioInfo;
            }
        }

        if (oldest != null)
        {
            StopAndReturnAudioSource(oldest.audioSource);
            return PlayNewAudio(newAudioID, newVolume);
        }

        return false;
    }

    /// <summary>
    /// 停止音量最小的音频
    /// </summary>
    private bool StopQuietestAudio(string newAudioID, float newVolume)
    {
        if (activeAudioSources.Count == 0)
            return false;

        // 找到音量最小的音频
        ActiveAudioInfo quietest = null;
        float lowestVolume = float.MaxValue;

        foreach (var audioInfo in activeAudioSources)
        {
            if (audioInfo.volume < lowestVolume)
            {
                lowestVolume = audioInfo.volume;
                quietest = audioInfo;
            }
        }

        if (quietest != null && quietest.volume < newVolume)
        {
            StopAndReturnAudioSource(quietest.audioSource);
            return PlayNewAudio(newAudioID, newVolume);
        }

        return false;
    }

    /// <summary>
    /// 停止相同类型的音频(如果存在)
    /// </summary>
    private bool StopSameTypeAudio(string newAudioID, float newVolume)
    {
        // 首先尝试停止相同类型的音频
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            if (activeAudioSources[i].audioID == newAudioID)
            {
                StopAndReturnAudioSource(activeAudioSources[i].audioSource);
                return PlayNewAudio(newAudioID, newVolume);
            }
        }

        // 如果没有相同类型的，则停止最早播放的
        return StopOldestAudio(newAudioID, newVolume);
    }

    /// <summary>
    /// 获取可用的AudioSource
    /// </summary>
    private AudioSource GetAvailableAudioSource()
    {
        if (audioSourcePool.Count > 0)
        {
            return audioSourcePool.Dequeue();
        }
        return null;
    }

    /// <summary>
    /// 停止并返回AudioSource到对象池
    /// </summary>
    private void StopAndReturnAudioSource(AudioSource audioSource)
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            ReturnAudioSourceToPool(audioSource);
        }
    }

    /// <summary>
    /// 返回AudioSource到对象池
    /// </summary>
    private void ReturnAudioSourceToPool(AudioSource audioSource)
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.gameObject.SetActive(false);
            audioSourcePool.Enqueue(audioSource);

            // 从活跃列表中移除
            for (int i = activeAudioSources.Count - 1; i >= 0; i--)
            {
                if (activeAudioSources[i].audioSource == audioSource)
                {
                    activeAudioSources.RemoveAt(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 获取可用音频源数量
    /// </summary>
    public int GetAvailableAudioSourceCount()
    {
        return audioSourcePool.Count;
    }

    /// <summary>
    /// 获取活跃音频数量
    /// </summary>
    public int GetActiveAudioCount()
    {
        return activeAudioSources.Count;
    }

    /// <summary>
    /// 获取特定音频ID的活跃实例数量
    /// </summary>
    public int GetActiveAudioCount(string audioID)
    {
        int count = 0;
        foreach (var audioInfo in activeAudioSources)
        {
            if (audioInfo.audioID == audioID)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 停止所有音频
    /// </summary>
    public void StopAllAudio()
    {
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            if (activeAudioSources[i].audioSource != null)
            {
                StopAndReturnAudioSource(activeAudioSources[i].audioSource);
            }
        }
        activeAudioSources.Clear();
    }

    /// <summary>
    /// 停止特定音频ID的所有实例
    /// </summary>
    public void StopAudio(string audioID)
    {
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            if (activeAudioSources[i].audioID == audioID &&
                activeAudioSources[i].audioSource != null)
            {
                StopAndReturnAudioSource(activeAudioSources[i].audioSource);
            }
        }
    }

    /// <summary>
    /// 检查音频是否正在播放
    /// </summary>
    public bool IsAudioPlaying(string audioID)
    {
        foreach (var audioInfo in activeAudioSources)
        {
            if (audioInfo.audioID == audioID &&
                audioInfo.audioSource != null &&
                audioInfo.audioSource.isPlaying)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 设置最大同时播放音频数量
    /// </summary>
    public void SetMaxSimultaneousAudio(int maxCount)
    {
        config.maxSimultaneousAudio = Mathf.Max(0, maxCount);
    }

    /// <summary>
    /// 设置溢出处理策略
    /// </summary>
    public void SetOverflowStrategy(OverflowStrategy strategy)
    {
        config.overflowStrategy = strategy;
    }

    /// <summary>
    /// 预加载音频（提前创建AudioSource对象）
    /// </summary>
    public void PreloadAudioSources(int count)
    {
        for (int i = 0; i < count; i++)
        {
            CreatePooledAudioSource();
        }
    }

    // 原 OnDestroy 只做「清空 Instance」，该职责已由 MonoSingleton 基类接管。
}