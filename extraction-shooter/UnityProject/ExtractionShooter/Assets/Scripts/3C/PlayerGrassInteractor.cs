using UnityEngine;
using System.Collections.Generic;

public class PlayerGrassInteractor : MonoBehaviour
{
    [Header("交互设置")]
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private LayerMask grassLayer = -1;
    [SerializeField] private float updateRate = 0.1f;
    
    [Header("碰撞效果")]
    [SerializeField] private float baseStrength = 0.8f;
    [SerializeField] private float velocityMultiplier = 0.3f;
    [SerializeField] private float radiusMultiplier = 1.2f;
    
    [Header("移动检测")]
    [SerializeField] private float minSpeedToInteract = 0.5f;
    [SerializeField] private float footOffset = 0.2f;  // 脚步偏移，使交互点在玩家前面一点

    private Collider[] nearbyColliders = new Collider[20];
    private List<GrassInteractionController> grassList = new List<GrassInteractionController>();
    private float updateTimer = 0f;
    private Vector3 lastPosition;
    private Vector3 velocity;
    
    private void Start()
    {
        lastPosition = transform.position;
    }
    
    private void Update()
    {
        // 计算速度
        velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
        
        updateTimer += Time.deltaTime;
        
        if (updateTimer >= updateRate)
        {
            updateTimer = 0f;
            UpdateNearbyGrass();
            
            if (velocity.magnitude > minSpeedToInteract)
            {
                ApplyInteraction();
            }
        }
    }
    
    private void UpdateNearbyGrass()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            interactionRadius,
            nearbyColliders,
            grassLayer
        );
        
        grassList.Clear();
        
        for (int i = 0; i < count; i++)
        {
            var grass = nearbyColliders[i].GetComponent<GrassInteractionController>();
            if (grass != null && !grassList.Contains(grass))
            {
                float distance = Vector3.Distance(transform.position, grass.transform.position);
                if (distance <= maxDistance)
                {
                    grassList.Add(grass);
                }
            }
        }
    }
    
    private void ApplyInteraction()
    {
        float speedFactor = Mathf.Clamp01(velocity.magnitude / 5f);
        
        foreach (var grass in grassList)
        {
            // 计算交互位置（在移动方向前面一点）
            Vector3 interactionPos = transform.position + velocity.normalized * footOffset;
            
            // 计算强度
            float strength = baseStrength + speedFactor * velocityMultiplier;
            
            // 计算半径
            float radius = 1f + speedFactor * radiusMultiplier;
            
            grass.AddInteraction(interactionPos, strength, radius);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        var grass = other.GetComponent<GrassInteractionController>();
        if (grass != null && !grassList.Contains(grass))
        {
            grassList.Add(grass);
            
            // 进入时给予一个冲击
            Vector3 toGrass = (grass.transform.position - transform.position).normalized;
            toGrass.y = 0;
            
            grass.AddInteraction(transform.position, baseStrength * 1.5f, 1.5f);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        var grass = other.GetComponent<GrassInteractionController>();
        if (grass != null && grassList.Contains(grass))
        {
            grassList.Remove(grass);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 0.8f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, interactionRadius);
    }
}