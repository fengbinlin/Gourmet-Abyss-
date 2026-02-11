using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyState
{
    Idle,           // 闲置状态
    Patrol,         // 巡逻状态
    LookAround,     // 四处张望状态
    Hit,            // 受击状态
    Dead            // 死亡状态
}

public class EnemyAI : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 3.5f;           // 移动速度
    [SerializeField] private float patrolRadius = 10f;           // 巡逻半径
    [SerializeField] private float minPatrolDistance = 3f;      // 最小巡逻距离
    [SerializeField] private float stoppingDistance = 0.5f;     // 停止距离
    [SerializeField] private float raycastDistance = 5f;        // 射线检测距离
    
    [Header("层设置")]
    [SerializeField] private LayerMask walkableLayer;           // 可行走的地板层
    [SerializeField] private LayerMask obstacleLayer;          // 障碍物层
    
    [Header("动画设置")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleAnimationState = "Idle";
    [SerializeField] private string walkAnimationState = "Walk";
    [SerializeField] private string lookAroundAnimationState = "LookAround";
    [SerializeField] private float animationTransitionTime = 0.1f;  // 动画过渡时间
    
    [Header("行为时间设置")]
    [SerializeField] private float minLookAroundTime = 1.5f;    // 最小张望时间
    [SerializeField] private float maxLookAroundTime = 3f;      // 最大张望时间
    [SerializeField] private float minIdleTime = 0.5f;          // 最小闲置时间
    [SerializeField] private float maxIdleTime = 1.5f;          // 最大闲置时间
    [SerializeField] private float hitRecoveryTime = 0.8f;      // 受击后恢复时间
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugInfo = true;         // 显示调试信息
    [SerializeField] private Color patrolPointColor = Color.blue;
    [SerializeField] private Color raycastColor = Color.green;
    [SerializeField] private Color blockedRayColor = Color.red;
    
    [Header("AI设置")]
    [SerializeField] private float raycastHeight = 0.2f;       // 射线发射的高度偏移
    
    // 八个方向向量
    private static readonly Vector3[] Directions = new Vector3[]
    {
        Vector3.forward,           // 前
        Vector3.back,              // 后
        Vector3.left,              // 左
        Vector3.right,             // 右
        (Vector3.forward + Vector3.left).normalized,    // 左前
        (Vector3.forward + Vector3.right).normalized,   // 右前
        (Vector3.back + Vector3.left).normalized,      // 左后
        (Vector3.back + Vector3.right).normalized       // 右后
    };

    // 私有变量
    private EnemyState currentState = EnemyState.Idle;
    private Vector3 currentPatrolPoint = Vector3.zero;
    private Vector3 moveDirection = Vector3.zero;
    private float lookAroundTimer = 0f;
    private float idleTimer = 0f;
    private float hitTimer = 0f;
    private float patrolTimer = 0f;
    private float patrolDuration = 0f;
    private bool hasReachedDestination = false;
    private EnemyHealth enemyHealth;
    private Vector3 spawnPosition; // 记录生成位置
    
    // 动画参数哈希
    private int isWalkingHash;
    private int isLookingAroundHash;
    
    private void Awake()
    {
        // 获取组件
        if (animator == null)
            animator = GetComponent<Animator>();
            
        enemyHealth = GetComponent<EnemyHealth>();
        
        // 记录生成位置
        spawnPosition = transform.position;
        
        // 缓存动画参数哈希
        isWalkingHash = Animator.StringToHash("IsWalking");
        isLookingAroundHash = Animator.StringToHash("IsLookingAround");
    }
    
    private void Start()
    {
        // 初始状态为闲置
        SetState(EnemyState.Idle);
    }
    
    private void Update()
    {
        // 根据当前状态执行对应逻辑
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
                
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;
                
            case EnemyState.LookAround:
                UpdateLookAroundState();
                break;
                
            case EnemyState.Hit:
                UpdateHitState();
                break;
                
            case EnemyState.Dead:
                // 死亡状态不执行任何操作
                break;
        }
        
        // 更新动画
        UpdateAnimations();
    }
    
    #region 状态更新方法
    
    private void UpdateIdleState()
    {
        idleTimer += Time.deltaTime;
        
        // 检查是否应该结束闲置状态
        float targetIdleTime = Random.Range(minIdleTime, maxIdleTime);
        
        if (idleTimer >= targetIdleTime)
        {
            // 闲置时间结束，开始巡逻
            SetState(EnemyState.Patrol);
        }
    }
    
    private void UpdatePatrolState()
    {
        patrolTimer += Time.deltaTime;
        
        // 检查是否到达巡逻时间
        if (patrolTimer >= patrolDuration)
        {
            // 巡逻时间结束，到达目标点
            hasReachedDestination = true;
            SetState(EnemyState.LookAround);
            return;
        }
        
        // 移动敌人
        MoveTowardsDestination();
        
        // 检查是否提前到达目标点（如果目标点很近）
        float distanceToTarget = Vector3.Distance(transform.position, currentPatrolPoint);
        if (distanceToTarget <= stoppingDistance)
        {
            hasReachedDestination = true;
            SetState(EnemyState.LookAround);
        }
        
        // 检查是否卡住了
        if (patrolTimer > 1f && Vector3.Distance(transform.position, currentPatrolPoint) > patrolRadius)
        {
            // 如果移动了1秒但离目标点还是很远，可能卡住了，重新选择方向
            SetState(EnemyState.Patrol);
        }
    }
    
    private void UpdateLookAroundState()
    {
        lookAroundTimer += Time.deltaTime;
        
        // 检查张望时间是否结束
        float targetLookAroundTime = Random.Range(minLookAroundTime, maxLookAroundTime);
        
        if (lookAroundTimer >= targetLookAroundTime)
        {
            // 张望时间结束，开始巡逻
            SetState(EnemyState.Patrol);
        }
    }
    
    private void UpdateHitState()
    {
        hitTimer += Time.deltaTime;
        
        // 检查受击恢复时间是否结束
        if (hitTimer >= hitRecoveryTime)
        {
            // 恢复后进入张望状态
            SetState(EnemyState.LookAround);
        }
    }
    
    #endregion
    
    #region 状态切换方法
    
    public void SetState(EnemyState newState)
    {
        // 如果新状态与当前状态相同，不做处理
        if (currentState == newState)
            return;
        
        // 退出当前状态
        ExitState(currentState);
        
        // 更新当前状态
        currentState = newState;
        
        // 进入新状态
        EnterState(newState);
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} 状态切换: {currentState}");
        }
    }
    
    private void EnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                EnterIdleState();
                break;
                
            case EnemyState.Patrol:
                EnterPatrolState();
                break;
                
            case EnemyState.LookAround:
                EnterLookAroundState();
                break;
                
            case EnemyState.Hit:
                EnterHitState();
                break;
                
            case EnemyState.Dead:
                EnterDeadState();
                break;
        }
    }
    
    private void ExitState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                ExitIdleState();
                break;
                
            case EnemyState.Patrol:
                ExitPatrolState();
                break;
                
            case EnemyState.LookAround:
                ExitLookAroundState();
                break;
                
            case EnemyState.Hit:
                ExitHitState();
                break;
        }
    }
    
    private void EnterIdleState()
    {
        idleTimer = 0f;
        hasReachedDestination = false;
    }
    
    private void ExitIdleState()
    {
        // 清理闲置状态
    }
    
    private void EnterPatrolState()
    {
        hasReachedDestination = false;
        patrolTimer = 0f;
        
        // 获取可行走的方向
        Vector3[] walkableDirections = GetWalkableDirections();
        
        if (walkableDirections.Length == 0)
        {
            // 如果没有可行走的方向，进入闲置状态
            Debug.LogWarning($"{gameObject.name} 没有找到可行走的方向，进入闲置状态");
            SetState(EnemyState.Idle);
            return;
        }
        
        // 从可行走方向中随机选择一个
        int randomIndex = Random.Range(0, walkableDirections.Length);
        //print("随机序号"+randomIndex);
        moveDirection = walkableDirections[randomIndex];
        
        // 计算移动距离（在最小巡逻距离和巡逻半径之间随机）
        float moveDistance = Random.Range(minPatrolDistance, patrolRadius);
        
        // 计算目标点
        currentPatrolPoint = transform.position + moveDirection * moveDistance;
        
        // 计算预计的巡逻时间
        patrolDuration = moveDistance / moveSpeed;
        
        if (showDebugInfo)
        {
            //Debug.Log($"{gameObject.name} 巡逻目标点: {currentPatrolPoint}, 方向: {moveDirection}, 距离: {moveDistance}");
        }
    }
    
    private void ExitPatrolState()
    {
        // 清理巡逻状态
    }
    
    private void EnterLookAroundState()
    {
        lookAroundTimer = 0f;
    }
    
    private void ExitLookAroundState()
    {
        // 清理张望状态
    }
    
    private void EnterHitState()
    {
        hitTimer = 0f;
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} 受到攻击，进入受击状态");
        }
    }
    
    private void ExitHitState()
    {
        // 清理受击状态
    }
    
    private void EnterDeadState()
    {
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} 进入死亡状态");
        }
    }
    
    #endregion
    
    #region 移动和检测方法
    
    // 获取所有可行走的方向
    private Vector3[] GetWalkableDirections()
    {
        List<Vector3> walkableDirs = new List<Vector3>();
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
        
        foreach (Vector3 dir in Directions)
        {
            // 发射射线检测障碍物
            if (!Physics.Raycast(rayOrigin, dir, raycastDistance, obstacleLayer))
            {
            
                walkableDirs.Add(dir);
            }
        }
        print(walkableDirs.Count);
        return walkableDirs.ToArray();
    }
    
    // 向目标点移动
    private void MoveTowardsDestination()
    {
        if (currentPatrolPoint == Vector3.zero)
            return;
        
        // 计算移动方向
        Vector3 direction = (currentPatrolPoint - transform.position).normalized;
        
        // 保持Y轴不变
        direction.y = 0;
        
        if (direction.magnitude > 0.1f)
        {
            // 移动
            transform.position += direction * moveSpeed * Time.deltaTime;
            
            // 转向移动方向
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }
    
    #endregion
    
    #region 工具方法
    
    private void UpdateAnimations()
    {
        if (animator == null)
            return;
        
        // 根据当前状态设置动画参数
        switch (currentState)
        {
            case EnemyState.Idle:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, false);
                break;
                
            case EnemyState.Patrol:
                animator.SetBool(isWalkingHash, true);
                //animator.SetBool(isLookingAroundHash, false);
                break;
                
            case EnemyState.LookAround:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, true);
                break;
                
            case EnemyState.Hit:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, false);
                break;
                
            case EnemyState.Dead:
                animator.SetBool(isWalkingHash, false);
                //animator.SetBool(isLookingAroundHash, false);
                break;
        }
    }
    
    #endregion
    
    #region 公共方法
    
    // 被攻击时调用
    public void OnHit()
    {
        // 只有非死亡状态才能进入受击状态
        if (currentState != EnemyState.Dead)
        {
            SetState(EnemyState.Hit);
        }
    }
    
    // 死亡时调用
    public void OnDeath()
    {
        SetState(EnemyState.Dead);
    }
    
    // 获取当前状态
    public EnemyState GetCurrentState()
    {
        return currentState;
    }
    
    // 强制设置巡逻点（调试用）
    public void SetPatrolPoint(Vector3 point)
    {
        if (currentState == EnemyState.Patrol)
        {
            currentPatrolPoint = point;
            moveDirection = (point - transform.position).normalized;
        }
    }
    
    #endregion
    
    #region 调试方法
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo)
            return;
        
        // 绘制巡逻范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
        
        // 绘制当前巡逻点
        if (currentPatrolPoint != Vector3.zero)
        {
            Gizmos.color = patrolPointColor;
            Gizmos.DrawSphere(currentPatrolPoint, 0.3f);
            Gizmos.DrawLine(transform.position, currentPatrolPoint);
        }
        
        // 绘制射线检测
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
        
        foreach (Vector3 dir in Directions)
        {
            // 检查是否为可行走方向
            bool isWalkable = false;
            
            if (!Physics.Raycast(rayOrigin, dir, raycastDistance, obstacleLayer))
            {
                Vector3 targetPos = transform.position + dir * raycastDistance;
                RaycastHit groundHit;
                
                if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out groundHit, 3f, walkableLayer))
                {
                    float angle = Vector3.Angle(groundHit.normal, Vector3.up);
                    if (angle < 45f)
                    {
                        isWalkable = true;
                    }
                }
            }
            
            // 根据是否可行走设置颜色
            Gizmos.color = isWalkable ? raycastColor : blockedRayColor;
            Gizmos.DrawRay(rayOrigin, dir * raycastDistance);
        }
        
        // 绘制状态标签
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        Vector3 labelPosition = transform.position + Vector3.up * 2f;
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPosition, $"状态: {currentState}", style);
        #endif
    }
    
    #endregion
}