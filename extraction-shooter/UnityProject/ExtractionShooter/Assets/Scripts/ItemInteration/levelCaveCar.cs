// levelCaveCar.cs
using System.Collections;
using UnityEngine;

public class levelCaveCar : MonoBehaviour
{
    public static levelCaveCar instance;
    public bool canUse = true;
    public string levelName = "Layer1";
    
    private bool isPlayerInTrigger = false;
    private GameObject player;
    
    // 颜色过渡组件引用
    private VehicleColorTransition colorTransition;

    [Header("交互反馈-波动")]
    [SerializeField] private float pulseDuration = 0.12f;
    [SerializeField] private float pulseScaleMultiplier = 1.12f;
    private Vector3 originalScale;
    private Coroutine pulseCoroutine;

    [Header("返回地面特效")]
    [SerializeField] private GameObject returnToHomeVfxPrefab;
    [SerializeField] private Transform returnToHomeVfxSpawnPoint;
    [SerializeField] private bool useSpawnPointRotation = false;
    [SerializeField] private Vector3 returnToHomeVfxPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 returnToHomeVfxRotationOffset = Vector3.zero;
    
    private void Awake()
    {
        instance = this;
    }
    
    private void Start()
    {
        // 获取颜色过渡组件
        colorTransition = GetComponent<VehicleColorTransition>();
        originalScale = transform.localScale;
    }
    
    private void Update()
    {
        if (isPlayerInTrigger && canUse && Input.GetKeyDown(KeyCode.E))
        {
            PlayPulse();
            AudioManager.Instance.PlayAudio("3");
            ToHome();
        }
    }
    
    public void ToHome()
    {
        if (LevelManager.instance == null || LevelManager.instance.IsTransitioning())
            return;

        SpawnReturnToHomeVfx();
            
        LevelManager.instance.FromLevelToHome(levelName);
        
        canUse = false;
        if (HomeCavecar.homeCavecar != null)
        {
            HomeCavecar.homeCavecar.canUse = true;
        }
        
        if (player != null)
        {
            player.GetComponent<TopDownController>().enabled = false;
        }
    }

    private void SpawnReturnToHomeVfx()
    {
        if (returnToHomeVfxPrefab == null) return;

        Transform spawnPoint = returnToHomeVfxSpawnPoint != null ? returnToHomeVfxSpawnPoint : transform;
        Vector3 spawnPosition = spawnPoint.TransformPoint(returnToHomeVfxPositionOffset);
        Quaternion baseRotation = useSpawnPointRotation ? spawnPoint.rotation : Quaternion.identity;
        Quaternion spawnRotation = baseRotation * Quaternion.Euler(returnToHomeVfxRotationOffset);
        Instantiate(returnToHomeVfxPrefab, spawnPosition, spawnRotation);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            player = other.gameObject;
            PlayPulse();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            player = null;
        }
    }

    private void PlayPulse()
    {
        if (!isActiveAndEnabled) return;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        transform.localScale = originalScale;
        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        float duration = Mathf.Max(0.01f, pulseDuration);
        float half = duration * 0.5f;
        float mul = Mathf.Max(1f, pulseScaleMultiplier);
        Vector3 peak = originalScale * mul;

        float t = 0f;
        while (t < half)
        {
            float k = t / half;
            transform.localScale = Vector3.Lerp(originalScale, peak, k);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            float k = t / half;
            transform.localScale = Vector3.Lerp(peak, originalScale, k);
            t += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
        pulseCoroutine = null;
    }
}