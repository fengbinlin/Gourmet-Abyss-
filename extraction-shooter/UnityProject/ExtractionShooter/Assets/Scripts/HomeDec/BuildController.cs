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
    private BuildingUnit unit;
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

    private void Start()
    {
        AllGameManager.OnDeploymentPhaseCompleted += cancelBox;
        unit = GetComponent<BuildingUnit>();
        cam = Camera.main;
        renderers = GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length > 0)
            normalColor = renderers[0].color;

        // 初始化旋转状态
        transform.rotation = Quaternion.Euler(0, 0, unit.isRotated ? -90f : 0f);
    }
    void cancelBox()
    {
        GetComponent<BoxCollider2D>().enabled = false;
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
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("鼠标按下");

            // 方法1：从屏幕点发射射线
            Vector2 mouseScreenPos = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(mouseScreenPos);

            // 使用 RaycastHit2D 进行 2D 射线检测
            RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction, Mathf.Infinity);

            // 按距离排序，获取最近的点击
            if (hits.Length > 0)
            {
                // 对结果按距离排序
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider != null)
                    {
                        Debug.Log($"检测到碰撞体: {hit.collider.gameObject.name}, 距离: {hit.distance}");

                        if (hit.collider.gameObject == gameObject)
                        {
                            Debug.Log("建筑被点击");
                            isDragging = true;

                            // 计算偏移量（从建筑中心到点击点的偏移）
                            Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(cam.transform.position.z)));
                            mouseWorld.z = 0;
                            offset = transform.position - mouseWorld;

                            originalPos = transform.position;

                            // 开始拖动时，如果之前已经放置过，先清除占用状态
                            if (wasPlaced)
                            {
                                GridManager.Instance.RemoveUnit(lastPlacedGridPos, unit);
                                wasPlaced = false;
                            }

                            SetColor(new Color(normalColor.r, normalColor.g, normalColor.b, 0.7f));
                            break; // 找到目标后停止检测
                        }
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
            currentGridPos = GridManager.Instance.WorldToGrid(targetWorld);
            transform.position = GetSnappedPosition(currentGridPos);

            bool canPlace = GridManager.Instance.CanPlace(currentGridPos, unit);
            SetColor(canPlace ? new Color(normalColor.r, normalColor.g, normalColor.b, 0.7f)
                             : new Color(invalidColor.r, invalidColor.g, invalidColor.b, 0.7f));
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            SnapToGrid(currentGridPos);
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
            originalPos = transform.position;
            currentKeyboardGridPos = GridManager.Instance.WorldToGrid(transform.position);

            // 如果之前已放置，先移除
            if (wasPlaced)
            {
                GridManager.Instance.RemoveUnit(lastPlacedGridPos, unit);
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

                // 检查合法性并更新颜色
                UpdateColorBasedOnPlacement();

                Debug.Log($"[{controllingPlayerID}] 移动到网格位置: {currentKeyboardGridPos}，可放置: {GridManager.Instance.CanPlace(currentKeyboardGridPos, unit)}");
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
        bool canPlace = GridManager.Instance.CanPlace(currentKeyboardGridPos, unit);
        SetColor(canPlace ? normalColor : invalidColor);
    }

    private bool IsWithinGridBounds(Vector2Int gridPos)
    {
        // 获取网格管理器的边界信息
        GridManager gm = GridManager.Instance;

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

        bool canPlace = GridManager.Instance.CanPlace(currentKeyboardGridPos, unit);

        if (canPlace)
        {
            // 放置成功
            GridManager.Instance.PlaceUnit(currentKeyboardGridPos, unit);
            wasPlaced = true;
            lastPlacedGridPos = currentKeyboardGridPos;
            SetColor(normalColor);
            isKeyboardControlled = false;
            Debug.Log($"键盘放置成功于: {currentKeyboardGridPos}");

            // 通知PlaceManager该玩家已完成摆放
            if (!string.IsNullOrEmpty(controllingPlayerID) && PlaceManager.Instance != null)
            {
                PlaceManager.Instance.PlayerPlacedUnit(controllingPlayerID, unit);
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
            GridManager.Instance.PlaceUnit(lastPlacedGridPos, unit);
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
        Vector2Int gridPos = GridManager.Instance.WorldToGrid(transform.position);

        bool canPlace = GridManager.Instance.CanPlace(gridPos, unit);
        SetColor(canPlace ? normalColor : invalidColor);

        Debug.Log($"[{controllingPlayerID}] 旋转建筑，当前位置: {gridPos}，可放置: {canPlace}");
    }

    /// <summary>获取网格对齐的位置（考虑锚点）</summary>
    private Vector3 GetSnappedPosition(Vector2Int gridPos)
    {
        Vector3 worldPos = GridManager.Instance.GridToWorld(gridPos);
        return worldPos;
    }

    private void SnapToGrid(Vector2Int gridPos)
    {
        // 可放置检查
        bool canPlace = GridManager.Instance.CanPlace(gridPos, unit);

        if (canPlace)
        {
            GridManager.Instance.PlaceUnit(gridPos, unit);
            wasPlaced = true;
            lastPlacedGridPos = gridPos;
            transform.position = GetSnappedPosition(gridPos); // 确保位置吸附
            SetColor(normalColor);
            Debug.Log("放置成功");
        }
        else
        {
            if (wasPlaced)
            {
                GridManager.Instance.PlaceUnit(lastPlacedGridPos, unit);
                transform.position = GetSnappedPosition(lastPlacedGridPos);
                SetColor(normalColor);
            }
            else
            {
                transform.position = originalPos;
                SetColor(invalidColor);
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

    /// <summary>启用键盘控制模式（供PlaceManager调用）</summary>
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
}