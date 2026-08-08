using System.Collections.Generic;
using UnityEngine;

public enum RotationMode
{
    Rotate90,     // 旋转 90°
    MirrorHorizontal // 水平镜像（左右翻转）
}

[RequireComponent(typeof(BuildingUnit))]
[RequireComponent(typeof(Collider2D))]
public class BuildController : MonoBehaviour
{
    // 同一时刻只允许一个建筑进入鼠标拖拽，避免多物体同时被拖动
    private static BuildController ActiveMouseDragController;

    private BuildingUnit unit;
    private GridManager gm;
    private Camera cam;
    private bool isDragging = false;
    private bool wasPlaced = false;
    private bool isKeyboardControlled = false; // 键盘控制状态
    private string controllingPlayerID = ""; // 控制此建筑的玩家ID

    private Vector3 offset;
    private Vector3 originalPos;
    private Vector2Int lastPlacedGridPos;
    private Vector2Int currentKeyboardGridPos; // 键盘控制时的当前网格位置

    private SpriteRenderer[] renderers;
    private Color normalColor = Color.white;
    private Color invalidColor = Color.red;

    private bool rotateInputProcessed = false;
    private float keyRepeatDelay = 0.15f; // 按键重复延迟
    private float lastKeyPressTime = 0f;

    // 点击进入拖拽前的轻微缩放反馈（对子物体 SpriteRenderer 缩放）
    private Vector3[] spriteOriginalScales;

    // 从家具 UI 生成的建筑，需要在放置失败/移出网格时回收入背包
    private bool spawnedFromFurnitureUI = false;
    private ResourceType sourceResourceType = ResourceType.None;

    private bool coreInitialized = false;
    private bool visualsInitialized = false;
    // 该建筑的魅力是否已经计入过总魅力（避免移动时重复累加）
    private bool charmCountedInTotal = false;

    [Header("网格步进缩放波动（交互反馈）")]
    [SerializeField] private float gridStepPulsePeak = 1.06f;   // 轻微放大峰值
    [SerializeField] private float gridStepPulseHalfTime = 0.06f; // 半程时长（放大/缩小各一段）

    private Vector2Int lastGridPosForPulse;
    private bool hasLastGridPosForPulse = false;
    private Coroutine gridStepPulseCoroutine;

    private void SetDragGridOverlayVisible(bool visible)
    {
        if (gm == null) return;
        gm.SetGameViewGridVisible(visible);
    }

    private void EnsureCoreInitialized()
    {
        if (coreInitialized) return;

        unit = GetComponent<BuildingUnit>();
        gm = GridManager.GetById(unit != null ? unit.gridId : 0);
        if (gm == null) gm = GridManager.Instance; // 兼容旧逻辑

        cam = Camera.main;

        coreInitialized = true;
    }

    private void EnsureVisualsInitialized()
    {
        if (visualsInitialized) return;

        if (cam == null) cam = Camera.main;
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers != null && renderers.Length > 0)
        {
            normalColor = renderers[0].color;
            normalColor.a = 1f; // 默认不透明

            spriteOriginalScales = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    spriteOriginalScales[i] = renderers[i].transform.localScale;
            }
        }

        visualsInitialized = true;
    }

    private void Start()
    {
        EnsureCoreInitialized();
        if (gm == null)
        {
            Debug.LogError($"❌ BuildController 找不到可用的 GridManager（unit={(unit != null ? unit.name : "null")}）");
            enabled = false;
            return;
        }
        EnsureVisualsInitialized();

        // 初始化旋转状态
        transform.rotation = Quaternion.Euler(0, 0, unit.isRotated ? -90f : 0f);
    }

    private void OnDestroy()
    {
        if (gm != null)
            gm.SetGameViewGridVisible(false);
    }

    private void Update()
    {
        HandleMouseInput();
        //HandleKeyboardInput();
        HandleRotationInput();

        // 确保键盘控制状态下颜色正确更新
        if (isKeyboardControlled)
        {
            UpdateColorBasedOnPlacement();
        }
    }

    private Vector2Int currentGridPos; // 拖动过程中实时记录

    private void HandleMouseInput()
    {
        // 防呆：如果没有在拖拽但还持有“当前拖拽者”，则在鼠标未按下时释放
        if (!isDragging && !Input.GetMouseButton(0))
        {
            if (HomeManager.instance != null && HomeManager.instance.currentDraggingUnit == this)
                HomeManager.instance.EndDrag(this);
            if (ActiveMouseDragController == this)
                ActiveMouseDragController = null;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("鼠标按下");

            // 如果已有其他建筑在拖拽，则忽略新的点击（直到松开）
            // 这里只做“是否被占用”的判断，不抢锁；抢锁必须等确认点到了自己之后再做
            if (HomeManager.instance != null)
            {
                if (HomeManager.instance.currentDraggingUnit != null && HomeManager.instance.currentDraggingUnit != this)
                    return;
            }
            else
            {
                if (ActiveMouseDragController != null && ActiveMouseDragController != this)
                    return;
            }

            // 使用“3D Ray 与 2D Collider 的交点”检测，适配相机有倾斜角度的情况
            Vector2 mouseScreenPos = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(mouseScreenPos);
            Collider2D selfCol = GetComponent<Collider2D>();

            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);
            if (hits.Length > 0 && selfCol != null)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (hit.collider == null) continue;

                    // 只在“最近命中的就是自己这个 Collider2D” 时才认为被点击
                    if (hit.collider == selfCol)
                    {
                        // 确认点到自己后再尝试抢占“当前拖拽者”
                        if (HomeManager.instance != null)
                        {
                            if (!HomeManager.instance.TryBeginDrag(this)) return;
                        }
                        else
                        {
                            if (ActiveMouseDragController != null && ActiveMouseDragController != this)
                                return;
                        }

                        Debug.Log("建筑被点击（Raycast 命中本体 Collider2D）");

                        // 在真正进入拖拽前做一次轻微缩放反馈：先略微缩小再恢复
                        StartCoroutine(PlayClickScaleFeedback());

                        isDragging = true;
                        hasLastGridPosForPulse = false;
                        if (HomeManager.instance == null)
                            ActiveMouseDragController = this;

                        SetDragGridOverlayVisible(true);

                        // 计算偏移量（从建筑中心到点击点的偏移）
                        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(cam.transform.position.z)));
                        mouseWorld.z = 0;
                        offset = transform.position - mouseWorld;

                        originalPos = transform.position;

                        // 开始拖动时，如果之前已经放置过，先清除占用状态
                        if (wasPlaced)
                        {
                            gm.RemoveUnit(lastPlacedGridPos, unit);
                            wasPlaced = false;
                        }

                        SetColor(normalColor);
                        break;
                    }
                    else
                    {
                        // 如果最近的命中不是自己，直接 break，认为这次点击属于别的物体
                        break;
                    }
                }
            }
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = Mathf.Abs(cam.transform.position.z);
            Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreenPos);
            mouseWorld.z = 0;

            Vector3 targetWorld = mouseWorld + offset;
            currentGridPos = gm.WorldToGrid(targetWorld);
            transform.position = GetSnappedPosition(currentGridPos);
            TryPlayGridStepPulse(currentGridPos);

            bool canPlace = gm.CanPlace(currentGridPos, unit);
            SetColor(canPlace ? normalColor : invalidColor);
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            hasLastGridPosForPulse = false;
            SetDragGridOverlayVisible(false);
            if (HomeManager.instance != null) HomeManager.instance.EndDrag(this);
            if (ActiveMouseDragController == this) ActiveMouseDragController = null;
            SnapToGrid(currentGridPos);
        }
    }

    /// <summary>
    /// 从家具 UI 生成的新建筑，立即进入拖拽状态
    /// </summary>
    public void BeginDragFromUI(ResourceType resourceType)
    {
        spawnedFromFurnitureUI = true;
        sourceResourceType = resourceType;

        EnsureCoreInitialized();
        EnsureVisualsInitialized();
        if (gm == null || unit == null)
        {
            Debug.LogWarning("BeginDragFromUI 失败：BuildController 未正确初始化 GridManager/BuildingUnit");
            return;
        }

        // 抢占拖拽控制权
        if (HomeManager.instance != null)
        {
            if (!HomeManager.instance.TryBeginDrag(this)) return;
        }
        else
        {
            if (ActiveMouseDragController != null && ActiveMouseDragController != this)
                return;
            ActiveMouseDragController = this;
        }

        isDragging = true;
        hasLastGridPosForPulse = false;
        SetDragGridOverlayVisible(true);

        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Mathf.Abs(cam.transform.position.z)));
        mouseWorld.z = 0;
        offset = transform.position - mouseWorld;

        originalPos = transform.position;
        wasPlaced = false;

        // 生成后立即给一个“波动”特效，并按当前位置合法性设置预览颜色（半透明）
        StartCoroutine(PlaySpawnPulse());

        Vector2Int gridPos = gm != null ? gm.WorldToGrid(transform.position) : Vector2Int.zero;
        bool canPlace = gm != null && unit != null && gm.CanPlace(gridPos, unit);
        Color preview = canPlace ? normalColor : invalidColor;
        SetColor(preview);
    }

    private System.Collections.IEnumerator PlaySpawnPulse()
    {
        // 轻微放大再恢复
        float half = 0.09f;
        float t = 0f;
        float peak = 1.12f;

        if (renderers == null || renderers.Length == 0 || spriteOriginalScales == null)
            yield break;

        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float s = Mathf.Lerp(1f, peak, p);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = spriteOriginalScales[i] * s;
            }
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float s = Mathf.Lerp(peak, 1f, p);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = spriteOriginalScales[i] * s;
            }
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].transform.localScale = spriteOriginalScales[i];
        }
    }

    private void TryPlayGridStepPulse(Vector2Int newGridPos)
    {
        if (renderers == null || renderers.Length == 0 || spriteOriginalScales == null) return;

        if (!hasLastGridPosForPulse)
        {
            lastGridPosForPulse = newGridPos;
            hasLastGridPosForPulse = true;
            return;
        }

        if (newGridPos == lastGridPosForPulse) return;

        lastGridPosForPulse = newGridPos;

        if (gridStepPulseCoroutine != null)
            StopCoroutine(gridStepPulseCoroutine);

        // 先恢复到原始缩放，再播放下一次波动，避免多协程叠加导致缩放错位
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].transform.localScale = spriteOriginalScales[i];
        }

        gridStepPulseCoroutine = StartCoroutine(PlayGridStepPulse());
    }

    private System.Collections.IEnumerator PlayGridStepPulse()
    {
        float half = Mathf.Max(0.01f, gridStepPulseHalfTime);
        float peak = Mathf.Max(1.001f, gridStepPulsePeak);

        // 放大阶段：1 -> peak
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float s = Mathf.Lerp(1f, peak, p);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = spriteOriginalScales[i] * s;
            }
            yield return null;
        }

        // 缩小阶段：peak -> 1
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float s = Mathf.Lerp(peak, 1f, p);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = spriteOriginalScales[i] * s;
            }
            yield return null;
        }

        // 精确恢复
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].transform.localScale = spriteOriginalScales[i];
        }
        gridStepPulseCoroutine = null;
    }

    private System.Collections.IEnumerator PlayClickScaleFeedback()
    {
        float duration = 0.08f;
        float t = 0f;
        float targetScaleMultiplier = 0.9f;

        if (renderers == null || renderers.Length == 0 || spriteOriginalScales == null)
            yield break;

        // 缩小阶段
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(1f, targetScaleMultiplier, lerp);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = spriteOriginalScales[i] * s;
            }
            yield return null;
        }

        // 复原阶段
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(targetScaleMultiplier, 1f, lerp);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = spriteOriginalScales[i] * s;
            }
            yield return null;
        }

        // 确保精确恢复每个 Sprite 的原始缩放
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].transform.localScale = spriteOriginalScales[i];
        }
    }

    private void HandleRotationInput()
    {
        bool rotateKeyPressed = false;

        if (isDragging)
        {
            // 鼠标拖拽时，任何玩家都可以用R键旋转（保持原有习惯）
            rotateKeyPressed = Input.GetKeyDown(KeyCode.R);
        }
        else if (isKeyboardControlled)
        {
            // 键盘控制时，根据玩家ID使用不同的旋转键
            if (controllingPlayerID == "Player1")
            {
                // Player1 使用 R 键旋转
                rotateKeyPressed = Input.GetKeyDown(KeyCode.R);
            }
            else if (controllingPlayerID == "Player2")
            {
                // Player2 使用 P 键旋转
                rotateKeyPressed = Input.GetKeyDown(KeyCode.P);
            }
            else
            {
                // 没有指定玩家ID时，使用R键作为默认
                rotateKeyPressed = Input.GetKeyDown(KeyCode.R);
            }
        }

        if (rotateKeyPressed && !rotateInputProcessed)
        {
            ToggleRotation();
            rotateInputProcessed = true;
        }
        else if (!rotateKeyPressed)
        {
            rotateInputProcessed = false;
        }
    }

    private void HandleKeyboardInput()
    {
        // Tab键：激活/取消键盘控制
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleKeyboardControl();
            return;
        }

        // 只在键盘控制激活时处理其他键盘输入
        if (!isKeyboardControlled)
            return;

        // 检查取消按键
        bool cancelKeyPressed = false;
        bool confirmKeyPressed = false;

        if (controllingPlayerID == "Player1")
        {
            // Player1: E键确认，ESC键取消
            confirmKeyPressed = Input.GetKeyDown(KeyCode.E);
            cancelKeyPressed = Input.GetKeyDown(KeyCode.Escape);
        }
        else if (controllingPlayerID == "Player2")
        {
            // Player2: Keypad Enter 或 RightCtrl 确认，Keypad. 取消
            confirmKeyPressed = Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.RightControl);
            cancelKeyPressed = Input.GetKeyDown(KeyCode.KeypadPeriod);
        }
        else
        {
            // 没有指定玩家ID时，使用通用按键
            confirmKeyPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            cancelKeyPressed = Input.GetKeyDown(KeyCode.Escape);
        }

        // ESC键：取消并回到原位置
        if (cancelKeyPressed)
        {
            CancelKeyboardPlacement();
            return;
        }

        // 确认放置
        if (confirmKeyPressed)
        {
            ConfirmKeyboardPlacement();
            return;
        }

        // 方向键移动
        HandleKeyboardMovement();
    }

    private void ToggleKeyboardControl()
    {
        if (!isKeyboardControlled)
        {
            // 激活键盘控制
            isKeyboardControlled = true;
            hasLastGridPosForPulse = false;
            originalPos = transform.position;
            currentKeyboardGridPos = gm.WorldToGrid(transform.position);

            // 如果之前已放置，先移除
            if (wasPlaced)
            {
                gm.RemoveUnit(lastPlacedGridPos, unit);
            }

            // 检查当前位置合法性并更新颜色
            UpdateColorBasedOnPlacement();

            Debug.Log($"键盘控制已激活 - 玩家: {controllingPlayerID}");
        }
        else
        {
            // 取消键盘控制（相当于确认放置）
            ConfirmKeyboardPlacement();
        }
    }

    private void HandleKeyboardMovement()
    {
        // 检查按键重复延迟
        if (Time.time - lastKeyPressTime < keyRepeatDelay)
            return;

        Vector2Int moveDirection = Vector2Int.zero;

        // 根据玩家ID使用不同的按键
        if (controllingPlayerID == "Player1")
        {
            // Player1 使用 WASD
            if (Input.GetKey(KeyCode.W))
                moveDirection.y = 1;
            else if (Input.GetKey(KeyCode.S))
                moveDirection.y = -1;
            else if (Input.GetKey(KeyCode.A))
                moveDirection.x = -1;
            else if (Input.GetKey(KeyCode.D))
                moveDirection.x = 1;
        }
        else if (controllingPlayerID == "Player2")
        {
            // Player2 使用方向键
            if (Input.GetKey(KeyCode.UpArrow))
                moveDirection.y = 1;
            else if (Input.GetKey(KeyCode.DownArrow))
                moveDirection.y = -1;
            else if (Input.GetKey(KeyCode.LeftArrow))
                moveDirection.x = -1;
            else if (Input.GetKey(KeyCode.RightArrow))
                moveDirection.x = 1;
        }
        else
        {
            // 如果没有指定玩家ID（例如手动按Tab激活），则支持两种按键
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                moveDirection.y = 1;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                moveDirection.y = -1;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveDirection.x = -1;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveDirection.x = 1;
        }

        // 如果有移动输入
        if (moveDirection != Vector2Int.zero)
        {
            Vector2Int newGridPos = currentKeyboardGridPos + moveDirection;

            // 检查边界（确保新位置在网格范围内）
            if (IsWithinGridBounds(newGridPos))
            {
                currentKeyboardGridPos = newGridPos;
                lastKeyPressTime = Time.time;

                // 更新物体位置
                Vector3 newWorldPos = GetSnappedPosition(currentKeyboardGridPos);
                transform.position = newWorldPos;
                TryPlayGridStepPulse(currentKeyboardGridPos);

                // 检查合法性并更新颜色
                UpdateColorBasedOnPlacement();

                Debug.Log($"[{controllingPlayerID}] 移动到网格位置: {currentKeyboardGridPos}，可放置: {gm.CanPlace(currentKeyboardGridPos, unit)}");
            }
            else
            {
                Debug.Log($"[{controllingPlayerID}] 已到达边界，无法移动到: {newGridPos}");
            }
        }
    }

    /// <summary>根据当前位置的合法性更新颜色</summary>
    private void UpdateColorBasedOnPlacement()
    {
        bool canPlace = gm.CanPlace(currentKeyboardGridPos, unit);
        SetColor(canPlace ? normalColor : invalidColor);
    }

    private bool IsWithinGridBounds(Vector2Int gridPos)
    {
        // 获取网格管理器的边界信息
        GridManager gm = this.gm;

        // 检查建筑单元的所有格子是否在边界内
        int unitSize = unit.size;

        // 检查所有格子是否在网格范围内
        for (int x = 0; x < unitSize; x++)
        {
            for (int y = 0; y < unitSize; y++)
            {
                if (!unit.GetOccupy(x, y)) continue; // 跳过不占用的格子

                Vector2Int checkPos = gridPos + new Vector2Int(x, y);

                // 检查是否超出网格边界
                if (checkPos.x < 0 || checkPos.y < 0 ||
                    checkPos.x >= gm.gridWidth || checkPos.y >= gm.gridHeight)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ConfirmKeyboardPlacement()
    {
        if (!isKeyboardControlled)
            return;

        bool canPlace = gm.CanPlace(currentKeyboardGridPos, unit);

        if (canPlace)
        {
            // 放置成功
            gm.PlaceUnit(currentKeyboardGridPos, unit);
            wasPlaced = true;
            lastPlacedGridPos = currentKeyboardGridPos;
            SetColor(normalColor);
            isKeyboardControlled = false;
            Debug.Log($"键盘放置成功于: {currentKeyboardGridPos}");

            // 只在第一次成功放置时把该建筑的魅力计入总魅力
            if (!charmCountedInTotal && FurnitureUIManager.instance != null && unit != null)
            {
                FurnitureUIManager.instance.OnFurniturePlaced(unit);
                charmCountedInTotal = true;
            }
        }
        else
        {
            // 不能放置，保持键盘控制状态
            Debug.Log("此处不可放置，请移动到有效位置或按ESC取消");
        }
    }

    private void CancelKeyboardPlacement()
    {
        if (!isKeyboardControlled)
            return;

        // 回到原始位置
        if (wasPlaced)
        {
            // 如果之前已经放置过，恢复之前的放置
            gm.PlaceUnit(lastPlacedGridPos, unit);
            transform.position = GetSnappedPosition(lastPlacedGridPos);
        }
        else
        {
            // 否则回到最初的位置
            transform.position = originalPos;
        }

        SetColor(normalColor);
        isKeyboardControlled = false;
        Debug.Log("键盘控制已取消");
    }

    private void ToggleRotation()
    {
        // 切换旋转状态
        unit.ToggleRotationMaskOnly();

        // 用当前实际吸附位置的格子坐标计算能否放置
        Vector2Int gridPos = gm.WorldToGrid(transform.position);

        bool canPlace = gm.CanPlace(gridPos, unit);
        SetColor(canPlace ? normalColor : invalidColor);

        Debug.Log($"[{controllingPlayerID}] 旋转建筑，当前位置: {gridPos}，可放置: {canPlace}");
    }

    /// <summary>获取网格对齐的位置（考虑锚点）</summary>
    private Vector3 GetSnappedPosition(Vector2Int gridPos)
    {
        Vector3 worldPos = gm.GridToWorld(gridPos);
        return worldPos;
    }

    private void SnapToGrid(Vector2Int gridPos)
    {
        // 可放置检查
        bool canPlace = gm.CanPlace(gridPos, unit);

        if (canPlace)
        {
            gm.PlaceUnit(gridPos, unit);
            wasPlaced = true;
            lastPlacedGridPos = gridPos;
            transform.position = GetSnappedPosition(gridPos); // 确保位置吸附
            SetColor(normalColor);
            Debug.Log("放置成功");

            // 只在第一次成功放置时把该建筑的魅力计入总魅力
            if (!charmCountedInTotal && FurnitureUIManager.instance != null && unit != null)
            {
                FurnitureUIManager.instance.OnFurniturePlaced(unit);
                charmCountedInTotal = true;
            }
        }
        else
        {
            if (wasPlaced)
            {
                gm.PlaceUnit(lastPlacedGridPos, unit);
                transform.position = GetSnappedPosition(lastPlacedGridPos);
                SetColor(normalColor);
            }
            else
            {
                // 尚未成功放置过：如果是从家具 UI 生成的，放置失败则回收入背包并销毁
                if (spawnedFromFurnitureUI && GameValManager.Instance != null && sourceResourceType != ResourceType.None)
                {
                    GameValManager.Instance.AddResource(sourceResourceType, 1);
                    // 回收入背包后刷新一次家具 UI
                    if (FurnitureUIManager.instance != null)
                    {
                        FurnitureUIManager.instance.GenerateItems();
                        // 如果之前曾经计入过魅力，现在彻底收入背包/销毁时要减掉一次
                        if (charmCountedInTotal && unit != null)
                        {
                            FurnitureUIManager.instance.OnFurnitureReturnedToBag(unit);
                            charmCountedInTotal = false;
                        }
                    }
                    SetDragGridOverlayVisible(false);
                    if (HomeManager.instance != null) HomeManager.instance.EndDrag(this);
                    if (ActiveMouseDragController == this) ActiveMouseDragController = null;
                    Debug.Log($"放置失败，家具已回收入背包: {sourceResourceType}");
                    Destroy(gameObject);
                    return;
                }
                else
                {
                    transform.position = originalPos;
                    // 回退到原位后，按原位重新判定颜色（原位通常是合法位置）
                    Vector2Int originalGridPos = gm.WorldToGrid(originalPos);
                    bool canPlaceAtOriginal = gm.CanPlace(originalGridPos, unit);
                    SetColor(canPlaceAtOriginal ? normalColor : invalidColor);
                }
            }
            Debug.Log("此处不可放置，已回退");
        }
    }

    private void SetColor(Color color)
    {
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("BuildController: 没有找到渲染器组件");
            return;
        }

        foreach (var r in renderers)
        {
            if (r != null)
            {
                r.color = color;
            }
        }
    }

    /// <summary>
    /// 启用键盘控制模式。原调用方 PlaceManager（双人摆放玩法）已删除，目前无调用点，
    /// 键盘控制相关字段与分支一并保留，待确认家园装修是否还需要这条路径。
    /// </summary>
    public void EnableKeyboardMode(string playerID, Vector2Int startGrid)
    {
        controllingPlayerID = playerID;

        // 移动到起始网格位置
        currentKeyboardGridPos = startGrid;
        transform.position = GetSnappedPosition(startGrid);

        // 激活键盘控制
        isKeyboardControlled = true;
        originalPos = transform.position;

        // 检查当前位置合法性并更新颜色
        UpdateColorBasedOnPlacement();

        // 根据玩家显示对应的控制说明
        string rotationKey = (playerID == "Player1") ? "R键" : "P键";
        string movementKeys = (playerID == "Player1") ? "WASD移动" : "方向键移动";
        string confirmKey = (playerID == "Player1") ? "E键确认" : "小键盘Enter确认";

        Debug.Log($"✅ {playerID} 键盘控制已启用");
        Debug.Log($"   控制说明: {movementKeys}, {rotationKey}旋转, {confirmKey}, ESC取消");
    }

    /// <summary>
    /// 由外部（例如 FurnitureUIManager）调用，标记该建筑的魅力已经计入总魅力。
    /// 用于场景一开始就已经存在的家具，避免之后再移动时重复累计。
    /// </summary>
    public void MarkCharmCountedInTotal()
    {
        charmCountedInTotal = true;
    }
}