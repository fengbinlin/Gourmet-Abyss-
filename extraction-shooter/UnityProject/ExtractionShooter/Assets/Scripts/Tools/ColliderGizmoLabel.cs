using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 在编辑器 Scene 视图中显示碰撞体范围和文字标签。
/// 挂在任意物体上即可使用。
/// </summary>
[DisallowMultipleComponent]
public class ColliderGizmoLabel : MonoBehaviour
{
    [Header("标签设置")]
    [Tooltip("显示在碰撞体附近的文字")]
    [SerializeField] private string labelText = "Collider";
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.25f, 0f);

    [Header("绘制设置")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.9f);
    [SerializeField] private bool drawWhenNotSelected = true;

    private Collider cached3DCollider;
    private Collider2D cached2DCollider;

    private void OnValidate()
    {
        CacheCollider();
    }

    private void Reset()
    {
        CacheCollider();
    }

    private void Awake()
    {
        CacheCollider();
    }

    private void OnDrawGizmos()
    {
        if (!drawWhenNotSelected)
        {
            return;
        }

        DrawColliderGizmoAndLabel();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawWhenNotSelected)
        {
            return;
        }

        DrawColliderGizmoAndLabel();
    }

    private void CacheCollider()
    {
        cached3DCollider = GetComponent<Collider>();
        cached2DCollider = GetComponent<Collider2D>();
    }

    private void DrawColliderGizmoAndLabel()
    {
        if (cached3DCollider == null && cached2DCollider == null)
        {
            CacheCollider();
        }

        Gizmos.color = gizmoColor;

        if (cached3DCollider != null)
        {
            Draw3DCollider(cached3DCollider);
            DrawLabel(cached3DCollider.bounds.center + labelOffset);
            return;
        }

        if (cached2DCollider != null)
        {
            Draw2DCollider(cached2DCollider);
            DrawLabel(cached2DCollider.bounds.center + labelOffset);
        }
    }

    private void Draw3DCollider(Collider col)
    {
        if (col is BoxCollider box)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
            return;
        }

        if (col is SphereCollider sphere)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            Gizmos.matrix = oldMatrix;
            return;
        }

        if (col is CapsuleCollider capsule)
        {
            DrawCapsuleApprox(capsule);
            return;
        }

        // 兜底：其它类型使用 AABB 显示范围
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }

    private void Draw2DCollider(Collider2D col2D)
    {
        Bounds b = col2D.bounds;
        Vector3 center = b.center;
        Vector3 size = b.size;
        size.z = 0.05f;
        Gizmos.DrawWireCube(center, size);
    }

    // 用线段近似绘制胶囊外轮廓，避免复杂网格生成
    private void DrawCapsuleApprox(CapsuleCollider capsule)
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = capsule.center;
        float radius = capsule.radius;
        float height = Mathf.Max(capsule.height, radius * 2f);
        float halfBody = Mathf.Max(0f, (height * 0.5f) - radius);

        if (capsule.direction == 0) // X
        {
            Vector3 a = center + Vector3.left * halfBody;
            Vector3 b = center + Vector3.right * halfBody;
            Gizmos.DrawWireSphere(a, radius);
            Gizmos.DrawWireSphere(b, radius);
            Gizmos.DrawLine(a + Vector3.up * radius, b + Vector3.up * radius);
            Gizmos.DrawLine(a + Vector3.down * radius, b + Vector3.down * radius);
            Gizmos.DrawLine(a + Vector3.forward * radius, b + Vector3.forward * radius);
            Gizmos.DrawLine(a + Vector3.back * radius, b + Vector3.back * radius);
        }
        else if (capsule.direction == 1) // Y
        {
            Vector3 a = center + Vector3.down * halfBody;
            Vector3 b = center + Vector3.up * halfBody;
            Gizmos.DrawWireSphere(a, radius);
            Gizmos.DrawWireSphere(b, radius);
            Gizmos.DrawLine(a + Vector3.left * radius, b + Vector3.left * radius);
            Gizmos.DrawLine(a + Vector3.right * radius, b + Vector3.right * radius);
            Gizmos.DrawLine(a + Vector3.forward * radius, b + Vector3.forward * radius);
            Gizmos.DrawLine(a + Vector3.back * radius, b + Vector3.back * radius);
        }
        else // Z
        {
            Vector3 a = center + Vector3.back * halfBody;
            Vector3 b = center + Vector3.forward * halfBody;
            Gizmos.DrawWireSphere(a, radius);
            Gizmos.DrawWireSphere(b, radius);
            Gizmos.DrawLine(a + Vector3.left * radius, b + Vector3.left * radius);
            Gizmos.DrawLine(a + Vector3.right * radius, b + Vector3.right * radius);
            Gizmos.DrawLine(a + Vector3.up * radius, b + Vector3.up * radius);
            Gizmos.DrawLine(a + Vector3.down * radius, b + Vector3.down * radius);
        }

        Gizmos.matrix = oldMatrix;
    }

    private void DrawLabel(Vector3 worldPos)
    {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = gizmoColor;
        Handles.Label(worldPos, labelText, style);
#endif
    }
}
