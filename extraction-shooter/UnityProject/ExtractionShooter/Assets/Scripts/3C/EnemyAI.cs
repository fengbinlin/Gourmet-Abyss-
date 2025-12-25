using UnityEngine;
using UnityEngine.AI;
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
    [Header("导航设置")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private float patrolRadius = 10f;           // 巡逻半径
    [SerializeField] private float minPatrolDistance = 3f;      // 最小巡逻距离
    [SerializeField] private float stoppingDistance = 0.5f;     // 停止距离
    
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
    [SerializeField] private Color currentPathColor = Color.green;
    
    [Header("AI设置")]
    [SerializeField] private float agentWarpDistanceThreshold = 0.5f; // 如果离地面太远，强制Warp的距离阈值
    
    // 私有变量
    private EnemyState currentState = EnemyState.Idle;
    private Vector3 currentPatrolPoint = Vector3.zero;
    private float lookAroundTimer = 0f;
    private float idleTimer = 0f;
    private float hitTimer = 0f;
    private bool hasReachedDestination = false;
    private EnemyHealth enemyHealth;
    private Vector3 spawnPosition; // 记录生成位置
    private float lastNavMeshCheckTime = 0f;
    private const float NAVMESH_CHECK_INTERVAL = 2f; // 每2秒检查一次是否在NavMesh上
    
    // 动画参数哈希
    private int isWalkingHash;
    private int isLookingAroundHash;
    
    private void Awake()
    {
        // 获取组件
        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();
            
        if (animator == null)
            animator = GetComponent<Animator>();
            
        enemyHealth = GetComponent<EnemyHealth>();
        
        // 记录生成位置
        spawnPosition = transform.position;
        
        // 初始化导航代理
        if (navMeshAgent != null)
        {
            navMeshAgent.stoppingDistance = stoppingDistance;
            navMeshAgent.autoBraking = true;
            navMeshAgent.autoRepath = true;
        }
        
        // 缓存动画参数哈希
        isWalkingHash = Animator.StringToHash("IsWalking");
        isLookingAroundHash = Animator.StringToHash("IsLookingAround");
    }
    
    private void Start()
    {
        // 确保敌人在NavMesh上
        EnsureOnNavMesh();
        
        // 初始状态为闲置
        SetState(EnemyState.Idle);
    }
    
    private void Update()
    {
        // 定期检查是否在NavMesh上
        if (Time.time - lastNavMeshCheckTime > NAVMESH_CHECK_INTERVAL)
        {
            EnsureOnNavMesh();
            lastNavMeshCheckTime = Time.time;
        }
        
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
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            // 如果代理无效或不在NavMesh上，返回闲置状态
            if (currentState == EnemyState.Patrol)
            {
                SetState(EnemyState.Idle);
            }
            return;
        }
        
        // 检查是否已到达目标点
        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            if (!hasReachedDestination)
            {
                hasReachedDestination = true;
                
                // 到达目标点，切换到张望状态
                SetState(EnemyState.LookAround);
            }
        }
        else
        {
            hasReachedDestination = false;
        }
        
        // 额外检查：如果代理停止移动但未到达目的地，重新计算路径
        if (navMeshAgent.hasPath && !navMeshAgent.pathPending && navMeshAgent.velocity.sqrMagnitude < 0.1f)
        {
            // 检查是否卡住了
            StartCoroutine(CheckIfStuck());
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
        
        // 如果新状态是巡逻状态，确保在NavMesh上
        if (newState == EnemyState.Patrol && !IsAgentValid())
        {
            Debug.LogWarning($"无法切换到巡逻状态，NavMeshAgent无效或不在NavMesh上: {gameObject.name}");
            return;
        }
        
        // 退出当前状态
        ExitState(currentState);
        
        // 更新当前状态
        currentState = newState;
        
        // 进入新状态
        EnterState(newState);
        
        if (showDebugInfo)
        {
            //Debug.Log($"{gameObject.name} 状态切换: {currentState}");
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
        
        // 停止导航
        if (IsAgentValid())
        {
            navMeshAgent.isStopped = true;
        }
    }
    
    private void ExitIdleState()
    {
        // 清理闲置状态
    }
    
    private void EnterPatrolState()
    {
        hasReachedDestination = false;
        
        // 确保代理在NavMesh上
        if (!EnsureOnNavMesh())
        {
            //Debug.LogError($"{gameObject.name} 无法进入巡逻状态，因为不在NavMesh上");
            SetState(EnemyState.Idle);
            return;
        }
        
        // 设置巡逻目标点
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            Vector3 randomPoint = GetRandomPointOnNavMesh();
            if (randomPoint != Vector3.zero)
            {
                currentPatrolPoint = randomPoint;
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(currentPatrolPoint);
                
                if (showDebugInfo)
                {
                    //Debug.Log($"{gameObject.name} 巡逻目标点: {currentPatrolPoint}");
                }
            }
            else
            {
                // 如果找不到有效点，返回闲置状态
                SetState(EnemyState.Idle);
            }
        }
        else
        {
            // 代理无效，返回闲置状态
            SetState(EnemyState.Idle);
        }
    }
    
    private void ExitPatrolState()
    {
        // 清理巡逻状态
    }
    
    private void EnterLookAroundState()
    {
        lookAroundTimer = 0f;
        
        // 停止导航
        if (IsAgentValid())
        {
            navMeshAgent.isStopped = true;
        }
    }
    
    private void ExitLookAroundState()
    {
        // 清理张望状态
    }
    
    private void EnterHitState()
    {
        hitTimer = 0f;
        
        // 停止导航
        if (IsAgentValid())
        {
            navMeshAgent.isStopped = true;
        }
        
        // 停止所有协程
        StopAllCoroutines();
        
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
        // 停止导航
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.enabled = false;
        }
        
        // 停止所有协程
        StopAllCoroutines();
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} 进入死亡状态");
        }
    }
    
    #endregion
    
    #region 工具方法
    
    private Vector3 GetRandomPointOnNavMesh()
    {
        Vector3 randomPoint = Vector3.zero;
        
        for (int i = 0; i < 30; i++)  // 最多尝试30次
        {
            // 在巡逻半径内随机一个点
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += transform.position;
            
            // 尝试在NavMesh上找到最近的点
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
            {
                // 计算到目标点的距离
                float distance = Vector3.Distance(transform.position, hit.position);
                
                // 如果距离大于最小巡逻距离，返回这个点
                if (distance >= minPatrolDistance)
                {
                    randomPoint = hit.position;
                    break;
                }
            }
        }
        
        // 如果找不到有效点，尝试在生成位置附近寻找
        if (randomPoint == Vector3.zero)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPosition, out hit, patrolRadius, NavMesh.AllAreas))
            {
                randomPoint = hit.position;
            }
        }
        
        return randomPoint;
    }
    
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
    
    // 检查代理是否有效且在NavMesh上
    private bool IsAgentValid()
    {
        return navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;
    }
    
    // 确保敌人在NavMesh上
    private bool EnsureOnNavMesh()
    {
        if (navMeshAgent == null)
        {
            Debug.LogError($"{gameObject.name} NavMeshAgent为空");
            return false;
        }
        
        if (!navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
        
        // 如果已经在NavMesh上，直接返回true
        if (navMeshAgent.isOnNavMesh)
        {
            return true;
        }
        
        // 尝试将代理放置到NavMesh上
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, agentWarpDistanceThreshold, NavMesh.AllAreas))
        {
            navMeshAgent.Warp(hit.position);
            //Debug.Log($"{gameObject.name} 已被Warp到NavMesh上: {hit.position}");
            return true;
        }
        else
        {
            // 如果当前位置不行，尝试在生成位置附近寻找
            if (NavMesh.SamplePosition(spawnPosition, out hit, agentWarpDistanceThreshold * 2f, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(hit.position);
                transform.position = hit.position;
                //Debug.Log($"{gameObject.name} 已被Warp到生成位置附近的NavMesh上: {hit.position}");
                return true;
            }
            else
            {
                //Debug.LogError($"{gameObject.name} 无法放置在NavMesh上，请检查NavMesh烘焙和敌人位置");
                return false;
            }
        }
    }
    
    // 检查是否卡住
    private IEnumerator CheckIfStuck()
    {
        Vector3 startPosition = transform.position;
        yield return new WaitForSeconds(2f); // 等待2秒
        
        if (currentState == EnemyState.Patrol && navMeshAgent != null && navMeshAgent.enabled)
        {
            float distanceMoved = Vector3.Distance(startPosition, transform.position);
            if (distanceMoved < 0.5f && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                // 如果移动距离很小但仍有剩余距离，重新计算路径
                Debug.Log($"{gameObject.name} 可能卡住了，重新计算路径");
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
                
                // 重新设置目标点
                Vector3 newRandomPoint = GetRandomPointOnNavMesh();
                if (newRandomPoint != Vector3.zero)
                {
                    navMeshAgent.SetDestination(newRandomPoint);
                    navMeshAgent.isStopped = false;
                }
                else
                {
                    SetState(EnemyState.Idle);
                }
            }
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
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && currentState == EnemyState.Patrol)
        {
            currentPatrolPoint = point;
            navMeshAgent.SetDestination(point);
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
        
        // 绘制导航路径
        if (navMeshAgent != null && navMeshAgent.hasPath)
        {
            Gizmos.color = currentPathColor;
            for (int i = 0; i < navMeshAgent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(navMeshAgent.path.corners[i], navMeshAgent.path.corners[i + 1]);
                Gizmos.DrawSphere(navMeshAgent.path.corners[i], 0.1f);
            }
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
    
    // 调试方法：在Inspector中显示当前是否在NavMesh上
    private void OnValidate()
    {
        if (navMeshAgent != null && Application.isPlaying)
        {
            Debug.Log($"{gameObject.name} 在NavMesh上: {navMeshAgent.isOnNavMesh}");
        }
    }
    
    #endregion
}