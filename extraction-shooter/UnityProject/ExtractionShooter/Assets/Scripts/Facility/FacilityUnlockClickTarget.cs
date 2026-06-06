using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 挂在 UnlockClick 子物体上（与 Collider 同级）。
/// 点击由 <see cref="PlayerInteractionController"/> 射线或本脚本的 OnMouseDown 触发。
/// </summary>
[DisallowMultipleComponent]
public class FacilityUnlockClickTarget : MonoBehaviour
{
    [SerializeField] private bool logClickDebug;

    private FacilityUnlockable _owner;

    private void Awake()
    {
        _owner = GetComponentInParent<FacilityUnlockable>();
        if (_owner == null)
            Debug.LogWarning($"[FacilityUnlockClickTarget] {name} 未找到父级 FacilityUnlockable（请挂在 Plate/Pot/Table 根物体的子级下）。", this);

        EnsureColliderExists();
    }

    private void OnMouseDown()
    {
        if (IsPointerOverUI())
            return;
        TryHandleClick();
    }

    public void OnClicked()
    {
        if (IsScreenPositionBlockedByUI(Input.mousePosition))
            return;
        TryHandleClick();
    }

    private void TryHandleClick()
    {
        if (_owner == null)
            _owner = GetComponentInParent<FacilityUnlockable>();

        if (_owner == null)
        {
            Debug.LogWarning($"[FacilityUnlockClickTarget] 点击 {name} 但未找到 FacilityUnlockable。", this);
            return;
        }

        if (logClickDebug)
            Debug.Log($"[FacilityUnlockClickTarget] 点击 {name} -> {_owner.DisplayName}", this);

        _owner.OnUnlockClickTargetClicked();
    }

    /// <summary>从屏幕坐标检测 3D/2D 碰撞体上的解锁点击区。</summary>
    public static bool TryHandleScreenClick(Vector2 screenPosition, Camera camera, bool logDebug = false)
    {
        if (IsScreenPositionBlockedByUI(screenPosition))
        {
            if (logDebug)
                Debug.Log("[FacilityUnlockClickTarget] 点击被 UI 挡住，跳过设施解锁。");
            return false;
        }

        if (camera == null)
            camera = Camera.main;
        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits3D = Physics.RaycastAll(
            ray, 10000f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits3D, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits3D.Length; i++)
        {
            if (hits3D[i].collider == null) continue;
            FacilityUnlockClickTarget target = hits3D[i].collider.GetComponent<FacilityUnlockClickTarget>();
            if (target == null)
                target = hits3D[i].collider.GetComponentInParent<FacilityUnlockClickTarget>();
            if (target != null)
            {
                if (logDebug)
                    Debug.Log($"[FacilityUnlockClickTarget] 3D 命中 {target.name}");
                target.OnClicked();
                return true;
            }
        }

        Vector3 world = camera.ScreenToWorldPoint(screenPosition);
        Collider2D[] hits2D = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
        float bestDist = float.MaxValue;
        FacilityUnlockClickTarget best2D = null;
        for (int i = 0; i < hits2D.Length; i++)
        {
            Collider2D col = hits2D[i];
            if (col == null) continue;
            FacilityUnlockClickTarget target = col.GetComponent<FacilityUnlockClickTarget>();
            if (target == null)
                target = col.GetComponentInParent<FacilityUnlockClickTarget>();
            if (target == null) continue;

            float dist = Vector3.SqrMagnitude(target.transform.position - world);
            if (dist < bestDist)
            {
                bestDist = dist;
                best2D = target;
            }
        }

        if (best2D != null)
        {
            if (logDebug)
                Debug.Log($"[FacilityUnlockClickTarget] 2D 命中 {best2D.name}");
            best2D.OnClicked();
            return true;
        }

        return false;
    }

    private void EnsureColliderExists()
    {
        if (GetComponent<Collider>() != null || GetComponent<Collider2D>() != null)
            return;

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = false;
        Debug.LogWarning($"[FacilityUnlockClickTarget] {name} 缺少 Collider，已自动添加 BoxCollider。", this);
    }

    private static bool IsPointerOverUI()
    {
        return IsScreenPositionBlockedByUI(Input.mousePosition);
    }

    /// <summary>屏幕坐标是否点在可拦截射线的 UI 上（Overlay / World Space 均适用）。</summary>
    public static bool IsScreenPositionBlockedByUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        if (EventSystem.current.IsPointerOverGameObject(-1))
            return true;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
                continue;

            Graphic graphic = hitObject.GetComponent<Graphic>();
            if (graphic != null && graphic.enabled && graphic.raycastTarget)
                return true;
        }

        return false;
    }

    private void Reset()
    {
        Collider col3D = GetComponent<Collider>();
        if (col3D != null)
            col3D.isTrigger = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Collider col3D = GetComponent<Collider>();
        if (col3D != null && col3D.isTrigger)
            Debug.LogWarning($"[FacilityUnlockClickTarget] {name} 的 Collider 勾选了 Is Trigger，3D 射线可能点不到，建议取消勾选。", this);
    }
#endif
}
