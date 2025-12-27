using UnityEngine;
using System.Collections.Generic;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class TopDownController : MonoBehaviour
{
    [Header("掉落设置")]
    [Tooltip("掉落物品的预制体列表")]
    public List<GameObject> dropItems = new List<GameObject>();
    [Tooltip("掉落数量 (0表示掉落所有物品)")]
    [Range(0, 10)]
    public int dropCount = 3;

    [Tooltip("掉落半径")]
    [Range(0f, 5f)]
    public float dropRadius = 2f;

    [Tooltip("掉落力量")]
    [Range(0f, 20f)]
    public float dropForce = 8f;

    [Tooltip("向上弹跳的力量")]
    [Range(0f, 10f)]
    public float upwardForce = 3f;
    [Tooltip("随机旋转掉落物品")]
    public bool randomRotation = true;
    #region --- 1. 基础设置 ---
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("旋转设置")]
    [Tooltip("数值越小旋转越平滑，越大越跟手")]
    [SerializeField] private float turnSpeed = 15f;
    #endregion

    #region --- 2. 状态系统 ---
    [Header("战斗状态")]
    [SerializeField] private bool isInCombat = true; // 是否处于战斗状态
    [SerializeField] private KeyCode toggleCombatKey = KeyCode.F; // 切换战斗状态的按键

    [Header("非战斗状态设置")]
    [Tooltip("非战斗状态下是否允许鼠标控制旋转")]
    [SerializeField] private bool allowMouseInNonCombat = false;
    [Tooltip("非战斗状态下是否允许鼠标右键瞄准")]
    [SerializeField] private bool allowAimingInNonCombat = false;
    #endregion

    #region --- 3. 武器引用 ---
    [Header("武器引用")]
    [SerializeField] private PrimaryWeapon primaryWeapon;
    [SerializeField] private SecondaryWeapon secondaryWeapon;
    #endregion

    #region --- 4. 瞄准系统 ---
    [Header("瞄准修正 (防止打地板)")]
    [Tooltip("鼠标能检测到的所有层级 (包括地面、墙、敌人)")]
    [SerializeField] private LayerMask aimLayerMask;
    [Tooltip("仅属于地面的层级 (打在这里时会抬高准星)")]
    [SerializeField] private LayerMask groundLayerMask;
    [Tooltip("准星抬高偏移量 (通常设为 1.0 - 1.5，对应胸口高度)")]
    [SerializeField] private float aimHeightOffset = 1.3f;
    #endregion

    #region --- 5. 动画与组件 ---
    [Header("动画参数")]
    [SerializeField] private string speedParamName = "Speed";
    [SerializeField] private string combatParamName = "IsInCombat";

    [Header("组件引用")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator animator;
    #endregion

    #region --- 6. 足迹粒子效果 ---
    [Header("足迹粒子效果")]
    [Tooltip("足迹粒子系统")]
    [SerializeField] private ParticleSystem footstepParticles;
    [Tooltip("移动阈值，当移动速度大于此值时开始发射粒子")]
    [SerializeField] private float movementThreshold = 0.1f;
    [Tooltip("粒子发射速率，根据移动速度调整")]
    [SerializeField] private float emissionRate = 20f;
    [Tooltip("停止移动后延迟关闭粒子的时间")]
    [SerializeField] private float stopParticleDelay = 0.2f;

    private ParticleSystem.EmissionModule particleEmission;
    private float currentEmissionRate = 0f;
    private float stopTimer = 0f;
    private bool isMoving = false;
    #endregion

    // 内部变量
    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector3 currentAimPoint;

    // 新增：鼠标活动检测
    private bool mouseIsActive = false;
    private Vector3 lastMousePosition;
    private float mouseInactiveTimer = 0f;
    private bool isDead = false;
    [SerializeField] private float mouseInactiveThreshold = 0.1f; // 鼠标静止多久后算不活动

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponent<Animator>();


        // 初始化武器
        if (primaryWeapon != null)
        {
            primaryWeapon.Initialize(animator, mainCamera, this);
        }

        if (secondaryWeapon != null)
        {
            secondaryWeapon.Initialize(animator, mainCamera, this);
        }

        // 初始化鼠标位置
        lastMousePosition = Input.mousePosition;

        // 初始化战斗状态动画参数
        if (animator != null && !string.IsNullOrEmpty(combatParamName))
        {
            animator.SetBool(combatParamName, isInCombat);
        }
        // 订阅氧气耗尽事件
        if (BattleValManager.Instance != null)
        {
            BattleValManager.Instance.OnOxygenDepleted += HandleOxygenDepleted;
        }
        // 初始化足迹粒子系统
        InitializeFootstepParticles();
    }
    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }
    private void HandleOxygenDepleted()
    {
        // 只有在战斗状态才执行死亡
        if (isInCombat)
        {
            Die();
        }
    }
    public void Die()
    {
        if (isDead) return; // 防止重复调用
        isDead = true;

        // 触发动画
        if (animator != null)
        {
            animator.SetTrigger("Dead");
        }

        // 停止移动
        moveInput = Vector3.zero;
        rb.velocity = Vector3.zero;

        // 停止武器射击
        if (primaryWeapon != null) primaryWeapon.SetShooting(false);
        if (secondaryWeapon != null) secondaryWeapon.SetShooting(false);

        // 可以执行额外的函数，比如游戏结束或UI提示
        OnPlayerDead();
    }

    private void OnPlayerDead()
    {
        Debug.Log("玩家死亡，执行额外逻辑");
        InventoryManager.instance.ClearBackpackByPercentage(1);
        DropItemsOnDeath();
        Invoke("TOHome", 1f);

        // 在这里执行你的其他逻辑，比如：
        // GameManager.Instance.GameOver();
    }
    public void DropItemsOnDeath()
    {
        if (dropItems == null || dropItems.Count == 0)
        {
            Debug.LogWarning("没有设置掉落物品！");
            return;
        }

        // 确定实际掉落数量
        int actualDropCount = dropCount;
        if (dropCount == 0 || dropCount > dropItems.Count)
        {
            actualDropCount = dropItems.Count;
        }

        // 如果掉落数量少于列表总数，随机选择掉落的物品
        List<GameObject> itemsToDrop = new List<GameObject>();
        if (actualDropCount < dropItems.Count)
        {
            // 创建临时列表进行随机选择
            List<GameObject> tempList = new List<GameObject>(dropItems);
            for (int i = 0; i < actualDropCount; i++)
            {
                int randomIndex = Random.Range(0, tempList.Count);
                itemsToDrop.Add(tempList[randomIndex]);
                tempList.RemoveAt(randomIndex);
            }
        }
        else
        {
            // 掉落所有物品
            itemsToDrop = new List<GameObject>(dropItems);
        }

        // 实例化并掉落每个物品
        foreach (GameObject itemPrefab in itemsToDrop)
        {
            if (itemPrefab == null) continue;

            // 在玩家位置实例化物品
            Vector3 dropPosition = transform.position;
            GameObject droppedItem = Instantiate(itemPrefab, dropPosition, Quaternion.identity, gameObject.transform);

            // 添加悬浮旋转脚本
            ItemFloatAndRotate floatScript = droppedItem.AddComponent<ItemFloatAndRotate>();

            // 设置随机位置（在掉落半径内）
            Vector3 randomOffset = new Vector3(
                Random.Range(-dropRadius, dropRadius),
                0.2f, // 稍微抬高一点，避免嵌入地面
                Random.Range(-dropRadius, dropRadius)
            );

            droppedItem.transform.position = dropPosition + randomOffset;

            // 如果是3D物体，添加物理效果
            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = droppedItem.AddComponent<Rigidbody>();
            }
            // rb.isKinematic = true; // 防止物理引擎立即作用导致爆开
            // rb.useGravity = false; // 防止重力下落
            // rb.velocity = Vector3.zero;
            // rb.angularVelocity = Vector3.zero;
            // 添加掉落力
            if (rb != null)
            {
                Vector3 forceDirection = randomOffset.normalized;
                rb.AddForce(forceDirection * dropForce + Vector3.up * upwardForce, ForceMode.Impulse);

                // 添加随机旋转
                if (randomRotation)
                {
                    float randomTorque = Random.Range(1f, 5f);
                    rb.AddTorque(
                        Random.Range(-randomTorque, randomTorque),
                        Random.Range(-randomTorque, randomTorque),
                        Random.Range(-randomTorque, randomTorque),
                        ForceMode.Impulse
                    );
                }
            }



        }

        Debug.Log($"死亡掉落了 {itemsToDrop.Count} 个物品");
    }
    public void TOHome()
    {
        levelCaveCar.instance.ToHome();
    }

    // 初始化足迹粒子效果
    private void InitializeFootstepParticles()
    {
        if (footstepParticles != null)
        {
            // 获取发射模块
            particleEmission = footstepParticles.emission;

            // 初始时关闭粒子发射
            particleEmission.rateOverTime = 0f;
            currentEmissionRate = 0f;
            isMoving = false;
        }
    }

    void Update()
    {
        if (isDead) return; // 角色死亡后不再执行输入、战斗等逻辑
        // 0. 检查战斗状态切换
        CheckCombatToggle();

        // 1. 输入处理
        HandleMovementInput();

        // 只有在战斗状态下才处理射击输入
        if (isInCombat)
        {
            // 发送开火命令给对应的武器
            HandleWeaponInput();
        }
        else
        {
            // 非战斗状态，确保射击动画停止
            if (animator != null)
            {
                if (primaryWeapon != null)
                    animator.SetBool(primaryWeapon.shootBoolName, false);
                //if (secondaryWeapon != null)
                // animator.SetBool(secondaryWeapon.shootBoolName, false);
            }

            // 通知武器停止射击
            if (primaryWeapon != null) primaryWeapon.SetShooting(false);
            if (secondaryWeapon != null) secondaryWeapon.SetShooting(false);
        }

        // 2. 检测鼠标活动状态
        CheckMouseActivity();

        // 3. 状态更新
        UpdateAnimation();
        UpdateEffects();

        // 4. 武器更新
        if (primaryWeapon != null) primaryWeapon.UpdateWeapon();
        if (secondaryWeapon != null) secondaryWeapon.UpdateWeapon();
    }

    void FixedUpdate()
    {
        if (isDead) return; // 角色死亡后不再执行输入、战斗等逻辑
        // 物理移动和旋转建议在 FixedUpdate 中进行
        Move();
        Turn();

        // 更新粒子效果
        UpdateParticleEffects();
    }

    #region --- 足迹粒子效果控制 ---
    private void UpdateParticleEffects()
    {
        if (footstepParticles == null) return;

        // 计算当前移动速度
        float currentSpeed = rb.velocity.magnitude;
        float targetEmissionRate = 0f;

        // 检查是否在移动
        bool wasMoving = isMoving;
        isMoving = currentSpeed > movementThreshold && moveInput.magnitude > 0.1f;

        if (isMoving)
        {
            // 在移动，重置停止计时器
            stopTimer = 0f;

            // 根据移动速度计算发射速率
            float speedRatio = Mathf.Clamp01(currentSpeed / moveSpeed);
            targetEmissionRate = emissionRate * speedRatio;

            // 平滑过渡到目标发射速率
            currentEmissionRate = emissionRate;

            // 应用发射速率
            particleEmission.rateOverTime = currentEmissionRate;

            // 如果之前不在移动，现在开始移动，确保粒子系统在播放
            if (!wasMoving && !footstepParticles.isPlaying)
            {
                footstepParticles.Play();
            }
        }
        else
        {
            // 不在移动，增加停止计时器
            stopTimer += Time.fixedDeltaTime;

            // 如果超过延迟时间，平滑减少发射速率
            if (stopTimer >= stopParticleDelay)
            {
                currentEmissionRate = Mathf.Lerp(currentEmissionRate, 0f, 10f * Time.fixedDeltaTime);
                particleEmission.rateOverTime = currentEmissionRate;

                // 当发射速率接近0时停止粒子系统
                if (currentEmissionRate < 0.1f)
                {
                    particleEmission.rateOverTime = 0f;
                    if (footstepParticles.isPlaying)
                    {
                        footstepParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }

        // 调试信息
        // Debug.Log($"Speed: {currentSpeed:F2}, Moving: {isMoving}, Emission: {particleEmission.rateOverTime.constant:F1}");
    }

    // 强制停止粒子效果
    public void StopFootstepParticles()
    {
        if (footstepParticles != null)
        {
            particleEmission.rateOverTime = 0f;
            footstepParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            isMoving = false;
            currentEmissionRate = 0f;
        }
    }

    // 强制开始粒子效果
    public void StartFootstepParticles()
    {
        if (footstepParticles != null)
        {
            isMoving = true;
            currentEmissionRate = emissionRate;
            particleEmission.rateOverTime = currentEmissionRate;
            if (!footstepParticles.isPlaying)
            {
                footstepParticles.Play();
            }
        }
    }

    // 设置粒子系统引用
    public void SetFootstepParticles(ParticleSystem particles)
    {
        footstepParticles = particles;
        InitializeFootstepParticles();
    }
    #endregion

    #region --- 武器输入处理 ---
    private void HandleWeaponInput()
    {
        // 主武器开火
        if (primaryWeapon != null)
        {
            bool isFiring = Input.GetButton("Fire1");
            primaryWeapon.SetShooting(isFiring);

            if (isFiring)
            {
                //print("主武器开火");
                primaryWeapon.HandleShooting(currentAimPoint, mouseIsActive);
            }
        }

        // 副武器开火
        if (secondaryWeapon != null)
        {
            bool isFiringSecondary = Input.GetButton("Fire2");
            secondaryWeapon.SetShooting(isFiringSecondary);

            if (isFiringSecondary)
            {
                secondaryWeapon.HandleShooting(currentAimPoint, mouseIsActive);
            }
        }
    }
    #endregion

    #region --- 公共方法 ---

    // 获取战斗状态
    public bool GetCombatState()
    {
        return isInCombat;
    }

    // 获取鼠标活动状态
    public bool IsMouseActive()
    {
        return mouseIsActive;
    }

    // 获取瞄准点
    public Vector3 GetAimPoint()
    {
        return currentAimPoint;
    }

    // 获取角色朝向
    public Vector3 GetCharacterForward()
    {
        return transform.forward;
    }

    // 获取枪口位置
    public Vector3 GetFirePointPosition(bool isPrimary = true)
    {
        if (isPrimary && primaryWeapon != null)
            return primaryWeapon.GetFirePoint().position;
        else if (!isPrimary && secondaryWeapon != null)
            return secondaryWeapon.GetFirePoint().position;

        return transform.position + transform.forward;
    }

    // 获取是否在移动
    public bool IsMoving()
    {
        return isMoving;
    }

    #endregion

    #region --- 原有的移动、旋转、状态管理逻辑（保持不变）---

    // 新增：检查战斗状态切换
    private void CheckCombatToggle()
    {
        if (Input.GetKeyDown(toggleCombatKey))
        {
            ToggleCombatState();
        }
    }

    // 新增：切换战斗状态
    public void ToggleCombatState()
    {
        isInCombat = !isInCombat;

        // 更新动画参数
        if (animator != null && !string.IsNullOrEmpty(combatParamName))
        {
            animator.SetBool(combatParamName, isInCombat);
        }

        Debug.Log("战斗状态: " + (isInCombat ? "开启" : "关闭"));
    }

    // 新增：设置战斗状态
    public void SetCombatState(bool combatState)
    {
        isInCombat = combatState;

        // 更新动画参数
        if (animator != null && !string.IsNullOrEmpty(combatParamName))
        {
            animator.SetBool(combatParamName, isInCombat);
        }
    }

    // --- 鼠标活动检测 ---
    private void CheckMouseActivity()
    {
        // 非战斗状态下，根据设置决定鼠标是否激活
        if (!isInCombat)
        {
            if (!allowMouseInNonCombat)
            {
                mouseIsActive = false;
                return;
            }

            // 如果非战斗状态下允许鼠标控制，但限制为右键瞄准
            if (allowAimingInNonCombat && Input.GetMouseButton(1))
            {
                // 右键按下时激活鼠标
                mouseIsActive = true;
                mouseInactiveTimer = 0f;
            }
            else
            {
                mouseIsActive = false;
            }
            return;
        }

        // 以下是战斗状态下的鼠标检测逻辑
        Vector3 currentMousePos = Input.mousePosition;

        // 检查鼠标是否移动
        if (Vector3.Distance(currentMousePos, lastMousePosition) > 0.1f)
        {
            mouseIsActive = true;
            mouseInactiveTimer = 0f;
        }
        // 检查鼠标是否被按下
        else if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            mouseIsActive = true;
            mouseInactiveTimer = 0f;
        }
        else
        {
            // 鼠标没有活动，增加计时器
            mouseInactiveTimer += Time.deltaTime;
            if (mouseInactiveTimer > mouseInactiveThreshold)
            {
                mouseIsActive = false;
            }
        }

        lastMousePosition = currentMousePos;
    }

    // --- 移动逻辑 ---
    private void HandleMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 非战斗状态下，只允许水平移动
        if (!isInCombat)
        {
            v = 0f;
        }

        // 让移动方向相对于摄像机视角，而不是世界坐标
        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        camForward.y = 0;
        camRight.y = 0;

        moveInput = (camForward.normalized * v + camRight.normalized * h).normalized;
    }

    private void Move()
    {
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }

    // --- 旋转与瞄准逻辑 (核心 3D 修正) ---
    private void Turn()
    {
        // 计算鼠标的世界位置
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, aimLayerMask))
        {
            // 检查是否打在地面层
            if ((groundLayerMask.value & (1 << hit.collider.gameObject.layer)) > 0)
            {
                // 如果是地板，目标点抬高
                currentAimPoint = hit.point + Vector3.up * aimHeightOffset;
            }
            else
            {
                // 如果是墙壁或敌人，指哪打哪
                currentAimPoint = hit.point;
            }
        }
        else
        {
            // 兜底：如果鼠标指到地图外，用数学平面
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            if (groundPlane.Raycast(ray, out float rayLength))
            {
                currentAimPoint = ray.GetPoint(rayLength) + Vector3.up * aimHeightOffset;
            }
        }

        // 角色旋转逻辑
        if (isInCombat)
        {
            // 战斗状态：跟随鼠标旋转
            if (mouseIsActive)
            {
                // 计算鼠标到角色的水平方向
                Vector3 lookPos = currentAimPoint;
                lookPos.y = transform.position.y;

                Vector3 direction = lookPos - transform.position;

                // 只有当方向有效时才旋转
                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
                }
            }
            else if (moveInput.magnitude > 0.1f)
            {
                // 鼠标不活动但角色在移动：转向移动方向
                Vector3 direction = moveInput;
                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
                }
            }
            // 否则保持当前旋转
        }
        else
        {
            // 非战斗状态：只根据移动方向旋转
            if (moveInput.magnitude > 0.1f)
            {
                Vector3 direction = moveInput;
                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
                }
            }
            // 否则保持当前旋转
        }
    }

    // --- 动画与特效 ---
    private void UpdateAnimation()
    {
        if (animator == null) return;
        // 简单的移动混合树控制
        animator.SetFloat(speedParamName, moveInput.magnitude, 0.1f, Time.deltaTime);
    }

    private void UpdateEffects()
    {
        // 如果需要，可以在这里添加其他通用特效更新
    }

    // 新增：在禁用时清理武器
    private void OnDisable()
    {
        if (secondaryWeapon != null)
        {
            secondaryWeapon.OnControllerDisabled();
        }
    }

    // 新增：在销毁时清理武器
    private void OnDestroy()
    {
        if (BattleValManager.Instance != null)
        {
            BattleValManager.Instance.OnOxygenDepleted -= HandleOxygenDepleted;
        }

        if (secondaryWeapon != null)
        {
            secondaryWeapon.OnControllerDestroyed();
        }
    }
    #endregion
}