using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AutoPlayFirstAnimation : MonoBehaviour
{
    private Animator animator;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }
    
    private void OnEnable()
    {
        // 启用时自动播放第一个动画
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // 获取第一个动画状态
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips.Length > 0)
            {
                animator.Play(clips[0].name, 0, 0f);
            }
        }
    }
    
    private void Reset()
    {
        // 确保 Animator 启用
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
        }
    }
}