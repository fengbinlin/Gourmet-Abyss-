using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class SelectPlayerController : MonoBehaviour
{
    [Header("玩家设置")]
    public string playerID = "Player1"; // "Player1" 或 "Player2"
    
    [Header("移动设置")]
    public float moveSpeed = 5f; // 移动速度
    
    [Header("视觉设置")]
    public SpriteRenderer spriteRenderer;
    public Color player1Color = Color.blue;
    public Color player2Color = Color.red;
    
    private Rigidbody2D rb;
    private Vector2 moveInput;
    
    // 选择和摆放相关
    [HideInInspector]
    public BuildingUnit selectedUnit; // 当前选择的道具
    [HideInInspector]
    public bool hasPlacedUnit = false; // 是否已摆放道具
    
    void Start()
    {
        InitializePlayer();
    }
    
    void OnEnable()
    {
        // 每次激活时都重新初始化
        Debug.Log($"🔄 {playerID} SelectPlayerController OnEnable 被调用");
        if (rb != null) // 如果已经初始化过，确保状态正确
        {
            InitializePlayer();
        }
    }
    
    private void InitializePlayer()
    {
        // 获取Rigidbody2D并配置
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        
        // 获取SpriteRenderer
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        // // 设置玩家颜色
        // if (spriteRenderer != null)
        // {
        //     spriteRenderer.color = playerID == "Player1" ? player1Color : player2Color;
        // }
        
        Debug.Log($"✅ {playerID} SelectPlayerController 已初始化，位置: {transform.position}");
    }
    
    void Update()
    {
        HandleMovementInput();
    }
    
    void FixedUpdate()
    {
        // 应用平滑移动
        if (rb != null)
        {
            rb.velocity = moveInput * moveSpeed;
        }
        
        // 根据移动方向翻转 sprite
        UpdateSpriteDirection();
    }
    
    private void UpdateSpriteDirection()
    {
        // 只在有水平移动时更新方向
        if (moveInput.x > 0.01f)
        {
            // 向右移动，scale.x = 1
            transform.localScale = new Vector3(1f, transform.localScale.y, transform.localScale.z);
        }
        else if (moveInput.x < -0.01f)
        {
            // 向左移动，scale.x = -1
            transform.localScale = new Vector3(-1f, transform.localScale.y, transform.localScale.z);
        }
    }
    
    private void HandleMovementInput()
    {
        moveInput = Vector2.zero;
        
        // 根据玩家ID选择不同的控制键
        if (playerID == "Player1")
        {
            // WSAD 控制
            if (Input.GetKey(KeyCode.W))
                moveInput.y = 1f;
            else if (Input.GetKey(KeyCode.S))
                moveInput.y = -1f;
            
            if (Input.GetKey(KeyCode.A))
                moveInput.x = -1f;
            else if (Input.GetKey(KeyCode.D))
                moveInput.x = 1f;
        }
        else if (playerID == "Player2")
        {
            // 方向键控制
            if (Input.GetKey(KeyCode.UpArrow))
                moveInput.y = 1f;
            else if (Input.GetKey(KeyCode.DownArrow))
                moveInput.y = -1f;
            
            if (Input.GetKey(KeyCode.LeftArrow))
                moveInput.x = -1f;
            else if (Input.GetKey(KeyCode.RightArrow))
                moveInput.x = 1f;
        }
        
        // 归一化对角线移动速度
        if (moveInput.magnitude > 1f)
        {
            moveInput.Normalize();
        }
    }
    
    /// <summary>碰撞触发 - 在选择阶段自动选择道具</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只在选择阶段处理
        if (AllGameManager.Instance == null || 
            AllGameManager.Instance.currentPhase != AllGameManager.GamePhase.Selection)
            return;
        
        // 检测BuildingUnit
        BuildingUnit unit = other.GetComponent<BuildingUnit>();
        if (unit != null && !unit.isSelected)
        {
            // 通知SelectManager处理选择
            if (SelectManager.Instance != null)
            {
                SelectManager.Instance.TrySelectUnit(playerID, unit);
            }
        }
    }
}
