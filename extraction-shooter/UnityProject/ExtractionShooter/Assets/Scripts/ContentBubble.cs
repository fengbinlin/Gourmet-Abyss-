using UnityEngine;
using UnityEngine.UI;

public class ContentBubble : MonoBehaviour
{
    public Text text; // 引用文本组件
    private RectTransform rectTransform; // 文本框的RectTransform
    private RectTransform textRectTransform; // 文本的RectTransform
    private float lastActualTextWidth; // 记录上一次实际文本宽度
    public float padding = 50f; // 可调整的填充值
    
    void Start()
    {
        // 获取当前GameObject的RectTransform组件
        rectTransform = GetComponent<RectTransform>();
        
        // 如果未手动分配Text组件，尝试从子对象中自动获取
        if (text == null)
        {
            text = GetComponentInChildren<Text>();
        }
        
        // 获取文本的RectTransform
        if (text != null)
        {
            textRectTransform = text.GetComponent<RectTransform>();
        }
        
        // 初始更新文本框大小
        UpdateBubbleSize();
        if (text != null)
        {
            lastActualTextWidth = GetActualTextWidth();
        }
    }
    
    void Update()
    {
        // 如果文本组件存在，检查实际文本宽度是否变化
        if (text != null)
        {
            float currentActualTextWidth = GetActualTextWidth();
            if (Mathf.Abs(currentActualTextWidth - lastActualTextWidth) > 0.1f)
            {
                UpdateBubbleSize();
                lastActualTextWidth = currentActualTextWidth;
            }
        }
    }
    
    float GetActualTextWidth()
    {
        // 计算文本的实际宽度：首选宽度 × 缩放比例
        if (text != null && textRectTransform != null)
        {
            return text.preferredWidth * textRectTransform.localScale.x;
        }
        return 0f;
    }
    
    void UpdateBubbleSize()
    {
        // 安全检查：确保RectTransform组件有效
        if (rectTransform != null && text != null)
        {
            // 根据文本实际宽度加上填充值，设置文本框的宽度
            float actualTextWidth = GetActualTextWidth();
            float newWidth = actualTextWidth + padding;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
        }
        else
        {
            Debug.LogWarning("ContentBubble: RectTransform或Text组件未正确设置。");
        }
    }
}