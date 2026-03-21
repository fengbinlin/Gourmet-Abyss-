using UnityEngine;

/// <summary>
/// 挂在魔法扫帚根物体上：在放置时的世界坐标附近做轻微上下浮动，可选缓慢自转，形成原地悬浮感。
/// 若有 Kinematic Rigidbody，会用 MovePosition / MoveRotation，避免与物理系统冲突。
/// </summary>
public class MagicBroomHover : MonoBehaviour
{
    [Header("上下浮动")]
    [Tooltip("相对初始高度上下摆动的幅度（米）")]
    [SerializeField] private float bobAmplitude = 0.08f;

    [Tooltip("摆动频率，越大越快")]
    [SerializeField] private float bobSpeed = 1.2f;

    [Tooltip("随机错开相位，多把扫帚同场景时可避免同步摆动")]
    [SerializeField] private float phaseOffset = 0f;

    [Header("自转（可选）")]
    [SerializeField] private bool enableYawRotation = false;

    [Tooltip("绕世界 Y 轴每秒旋转角度")]
    [SerializeField] private float yawDegreesPerSecond = 12f;


    private Vector3 _baseWorldPosition;
    private Quaternion _baseWorldRotation;
    private float _yawAngle;
    private Rigidbody _rb;
    private bool _useKinematicRb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _useKinematicRb = _rb != null && _rb.isKinematic;
    }

    private void Start()
    {
        _baseWorldPosition = transform.position;
        _baseWorldRotation = transform.rotation;
        _yawAngle = 0f;
    }

    private void LateUpdate()
    {
        float t = Time.time * bobSpeed + phaseOffset;
        float offsetY = Mathf.Sin(t) * bobAmplitude;
        Vector3 pos = _baseWorldPosition + Vector3.up * offsetY;

        Quaternion rot = _baseWorldRotation;
        if (enableYawRotation)
        {
            _yawAngle += yawDegreesPerSecond * Time.deltaTime;
            rot = _baseWorldRotation * Quaternion.AngleAxis(_yawAngle, Vector3.up);
        }

        if (_useKinematicRb)
        {
            _rb.MovePosition(pos);
            _rb.MoveRotation(rot);
        }
        else
        {
            transform.SetPositionAndRotation(pos, rot);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _baseWorldPosition : transform.position;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(center, bobAmplitude);
        Gizmos.DrawLine(center + Vector3.up * bobAmplitude, center - Vector3.up * bobAmplitude);
    }
#endif
}
