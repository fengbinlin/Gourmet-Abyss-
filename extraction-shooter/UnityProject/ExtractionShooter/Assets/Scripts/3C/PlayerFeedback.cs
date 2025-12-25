using UnityEngine;
using System.Collections;

public class PlayerFeedback : MonoBehaviour
{
 [Header("缩放弹动效果")]
    [SerializeField] private float bumpScaleMultiplier = 1.2f;  // 放大倍数
    [SerializeField] private float bumpDuration = 0.3f;         // 弹动总时长
    
    [Header("弹动曲线")]
    [SerializeField] private AnimationCurve bumpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("音效")]
    [SerializeField] private AudioClip[] collectSounds;          // 收集音效
    [SerializeField] private float soundVolume = 0.5f;          // 音效音量
    
    [Header("粒子效果")]
    [SerializeField] private ParticleSystem collectParticleEffect; // 收集粒子效果
    
    private Vector3 originalScale;    // 原始缩放
    private Coroutine bumpCoroutine;  // 弹动协程
    private AudioSource audioSource;  // 音频源
    
    private void Awake()
    {
        // 记录原始缩放
        originalScale = transform.localScale;
        
        // 确保有AudioSource组件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 设置为2D音效
            audioSource.volume = soundVolume;
        }
    }
    
    // 当收集到物品时调用这个方法
    public void OnItemCollected()
    {
        // 触发缩放弹动效果
        TriggerBumpEffect();
        
        // 播放音效
        PlayCollectionSound();
        
        // 播放粒子效果
        PlayParticleEffect();
    }
    
    // 触发弹动效果
    private void TriggerBumpEffect()
    {
        // 如果已经有弹动效果在进行，先停止
        if (bumpCoroutine != null)
        {
            StopCoroutine(bumpCoroutine);
        }
        
        // 开始新的弹动效果
        bumpCoroutine = StartCoroutine(BumpEffectRoutine());
    }
    
    // 弹动效果协程
    private IEnumerator BumpEffectRoutine()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < bumpDuration)
        {
            float t = elapsedTime / bumpDuration;
            
            // 使用曲线控制弹动过程
            // 0-0.5倍时长：放大
            // 0.5-1倍时长：缩小回原样
            float scaleProgress = 0f;
            if (t < 0.5f)
            {
                // 放大阶段
                scaleProgress = t * 2f; // 将0-0.5映射到0-1
                float scaleMultiplier = Mathf.Lerp(1f, bumpScaleMultiplier, bumpCurve.Evaluate(scaleProgress));
                transform.localScale = originalScale * scaleMultiplier;
            }
            else
            {
                // 缩小阶段
                scaleProgress = (t - 0.5f) * 2f; // 将0.5-1映射到0-1
                float scaleMultiplier = Mathf.Lerp(bumpScaleMultiplier, 1f, bumpCurve.Evaluate(scaleProgress));
                transform.localScale = originalScale * scaleMultiplier;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 确保最终恢复到原始大小
        transform.localScale = originalScale;
        bumpCoroutine = null;
    }
    
    // 播放收集音效
    private void PlayCollectionSound()
    {
        if (collectSounds != null && collectSounds.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, collectSounds.Length);
            audioSource.PlayOneShot(collectSounds[randomIndex]);
        }
    }
    
    // 播放粒子效果
    private void PlayParticleEffect()
    {
        if (collectParticleEffect != null)
        {
            collectParticleEffect.Play();
        }
    }
    
    // 简单的弹动效果（更简洁的版本）
    public void SimpleBump()
    {
        if (bumpCoroutine != null)
        {
            StopCoroutine(bumpCoroutine);
        }
        
        bumpCoroutine = StartCoroutine(SimpleBumpRoutine());
    }
    
    private IEnumerator SimpleBumpRoutine()
    {
        // 快速放大
        float scaleUpTime = bumpDuration * 0.3f;
        float timer = 0f;
        
        while (timer < scaleUpTime)
        {
            float t = timer / scaleUpTime;
            transform.localScale = originalScale * Mathf.Lerp(1f, bumpScaleMultiplier, t);
            timer += Time.deltaTime;
            yield return null;
        }
        
        // 快速缩小
        float scaleDownTime = bumpDuration * 0.7f;
        timer = 0f;
        
        while (timer < scaleDownTime)
        {
            float t = timer / scaleDownTime;
            transform.localScale = originalScale * Mathf.Lerp(bumpScaleMultiplier, 1f, t);
            timer += Time.deltaTime;
            yield return null;
        }
        
        // 确保恢复到原始大小
        transform.localScale = originalScale;
        bumpCoroutine = null;
    }
}

