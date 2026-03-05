using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ResourceBarController : MonoBehaviour
{
    public Animator mainUIAnimator;
    [Header("����������")]
    [SerializeField] private Image progressBarImage;
    [SerializeField] private Transform barTransform;  // ��������Ч��

    [Header("��ɫ����")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowResourceColor = Color.red;
    [SerializeField] [Range(0f, 1f)] private float lowResourceThreshold = 0.3f; // ���ڴ���ֵ��ʼ��˸

    [Header("��ɫ��˸����")]
    [SerializeField] private float colorBlinkSpeed = 2f; // ��ɫ��˸�ٶ�
    [SerializeField] private float minAlpha = 0.5f;      // ��С͸����
    [SerializeField] private float maxAlpha = 1f;        // ���͸����

    [Header("����Ч������")]
    [SerializeField] private float pulseSpeed = 8f;      // �����ٶ�
    [SerializeField] private float minPulseScale = 0.9f; // ��С����
    [SerializeField] private float maxPulseScale = 1.1f; // �������
    [SerializeField] [Range(0f, 1f)] public float pulseThreshold = 0.2f; // ���ڴ���ֵ��ʼ����

    [Header("״̬")]
    [SerializeField] private float currentPercentage = 1f;

    [SerializeField]private Color originalColor;
    [SerializeField]private Vector3 originalScale;
    private Coroutine pulseCoroutine;
    private Coroutine colorBlinkCoroutine;
    private bool isPulsing = false;
    private bool isColorBlinking = false;

    // ��¼�Ƿ�������״̬
    public bool IsPulsing => isPulsing;
    public bool IsColorBlinking => isColorBlinking;

    private void Awake()
    {
        // ��¼��ʼ״̬
        if (progressBarImage != null)
        {
            originalColor = progressBarImage.color;
            print("初始颜色"+originalColor);
        }

        if (barTransform != null)
        {
            originalScale = barTransform.localScale;
        }
        else
        {
            originalScale = transform.localScale;
        }

        // ȷ��normalColor�ǳ�ʼ��ɫ
        normalColor = originalColor;

        // ��ʼ��Ϊ100%
        currentPercentage = 1f;
    }

    private void Start()
    {
        // ȷ����ʼʱ����ȷ��״̬
        ResetBar();
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        isPulsing = false;
        isColorBlinking = false;
        ResetScale();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetScale();
    }

    /// <summary>
    /// ���ý���������ʼ״̬
    /// </summary>
    public void ResetBar()
    {
        // ֹͣ����Э��
        StopAllCoroutines();
        isPulsing = false;
        isColorBlinking = false;

        // ������ɫ
        if (progressBarImage != null)
        {
            progressBarImage.color = originalColor;
        }

        // ��������
        ResetScale();

        // ���ðٷֱ�
        currentPercentage = 1f;
    }

    /// <summary>
    /// ���½�����
    /// </summary>
    /// <param name="percentage">��ǰ�ٷֱ� (0-1)</param>
    public void UpdateProgress(float percentage)
    {
        if (percentage < 0) percentage = 0;
        if (percentage > 1) percentage = 1;

        currentPercentage = percentage;

        // ������ɫ��˸Ч��
        ControlColorBlinkEffect(percentage);

        // ��������Ч��
        ControlPulseEffect(percentage);
    }

    /// <summary>
    /// ������ɫ��˸Ч��
    /// </summary>
    private void ControlColorBlinkEffect(float percentage)
    {
        if (percentage <= lowResourceThreshold && !isColorBlinking)
        {
            // ��ʼ��ɫ��˸
            StartColorBlink();
        }
        else if (percentage > lowResourceThreshold && isColorBlinking)
        {
            // ֹͣ��ɫ��˸
            StopColorBlink();
        }
    }

    /// <summary>
    /// ��������Ч��
    /// </summary>
    private void ControlPulseEffect(float percentage)
    {
        if (percentage <= pulseThreshold && !isPulsing)
        {
            // ��ʼ����
            StartPulse();
        }
        else if (percentage > pulseThreshold && isPulsing)
        {
            // ֹͣ����
            StopPulse();
        }
    }

    /// <summary>
    /// ��ʼ��ɫ��˸Ч��
    /// </summary>
    private void StartColorBlink()
    {
        if (isColorBlinking || progressBarImage == null) return;

        isColorBlinking = true;
        if (colorBlinkCoroutine != null)
        {
            StopCoroutine(colorBlinkCoroutine);
        }
        colorBlinkCoroutine = StartCoroutine(ColorBlinkAnimation());
    }

    /// <summary>
    /// ֹͣ��ɫ��˸Ч��
    /// </summary>
    private void StopColorBlink()
    {
        if (!isColorBlinking) return;

        isColorBlinking = false;
        if (colorBlinkCoroutine != null)
        {
            StopCoroutine(colorBlinkCoroutine);
            colorBlinkCoroutine = null;
        }

        // ������ɫ
        if (progressBarImage != null)
        {
            progressBarImage.color = normalColor;
        }
    }

    /// <summary>
    /// ��ʼ����Ч��
    /// </summary>
    private void StartPulse()
    {
        if (isPulsing) return;

        isPulsing = true;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }
        pulseCoroutine = StartCoroutine(PulseAnimation());
    }

    /// <summary>
    /// ֹͣ����Ч��
    /// </summary>
    private void StopPulse()
    {
        if (!isPulsing) return;

        isPulsing = false;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        ResetScale();
    }

    /// <summary>
    /// ��ɫ��˸����Э��
    /// </summary>
    private IEnumerator ColorBlinkAnimation()
    {
        if (progressBarImage == null) yield break;

        float timer = 0f;
        Color startColor = Color.white; // �Ӱ�ɫ��ʼ
        Color targetColor = lowResourceColor; // ������Դ��ɫ

        while (isColorBlinking)
        {
            // �������Ҳ�ֵ����0-1֮��仯
            float t = (Mathf.Sin(timer * colorBlinkSpeed) + 1f) * 0.5f;

            // �ڰ�ɫ�͵���Դ��ɫ֮���ֵ
            progressBarImage.color = Color.Lerp(startColor, targetColor, t);

            // ����ͬʱ����͸���ȱ仯�������Ҫ�Ļ�
            // Color lerpedColor = Color.Lerp(startColor, targetColor, t);
            // lerpedColor.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            // progressBarImage.color = lerpedColor;

            timer += Time.deltaTime;
            yield return null;
        }

        // ȷ��ֹͣ��˸����ɫ�ָ�����
        progressBarImage.color = normalColor;
    }

    /// <summary>
    /// ��������Э��
    /// </summary>
    private IEnumerator PulseAnimation()
    {


        float timer = 0f;
        Transform targetTransform = barTransform != null ? barTransform : transform;

        while (isPulsing)
        {
            // ʹ�����Ҳ���������Ч��
            float pulseValue = Mathf.Sin(timer * pulseSpeed);
            float scaleFactor = Mathf.Lerp(minPulseScale, maxPulseScale, (pulseValue + 1f) / 2f);

            // Ӧ������
            targetTransform.localScale = originalScale * scaleFactor;
            if (mainUIAnimator != null)
            {
                mainUIAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        ResetScale();
    }

    /// <summary>
    /// ��������
    /// </summary>
    private void ResetScale()
    {
        Transform targetTransform = barTransform != null ? barTransform : transform;
        targetTransform.localScale = originalScale;
    }

    /// <summary>
    /// ���ý�������ɫ
    /// </summary>
    public void SetNormalColor(Color color)
    {
        normalColor = color;
        if (currentPercentage > lowResourceThreshold)
        {
            progressBarImage.color = normalColor;
        }
    }

    /// <summary>
    /// ��ȡԭʼ��ɫ
    /// </summary>
    public Color GetOriginalColor()
    {
        return originalColor;
    }

    /// <summary>
    /// ���õ���Դ��ɫ
    /// </summary>
    public void SetLowResourceColor(Color color)
    {
        lowResourceColor = color;
        // �����ǰ������˸����Ҫ���¿�ʼ��˸��ʹ������ɫ
        if (isColorBlinking)
        {
            StopColorBlink();
            StartColorBlink();
        }
    }

    /// <summary>
    /// ������ɫ��˸�ٶ�
    /// </summary>
    public void SetColorBlinkSpeed(float speed)
    {
        colorBlinkSpeed = speed;
    }

    /// <summary>
    /// �ֶ�������ɫ��˸
    /// </summary>
    public void SetColorBlink(bool active)
    {
        if (active && !isColorBlinking)
        {
            StartColorBlink();
        }
        else if (!active && isColorBlinking)
        {
            StopColorBlink();
        }
    }
}