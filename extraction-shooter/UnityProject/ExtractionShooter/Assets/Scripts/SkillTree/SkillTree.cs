using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class SkillTree : MonoBehaviour
{
    public static SkillTree Instance;
    [Header("技能树设置")]
    public string treeName = "技能树";
    public List<SkillNode> allSkillNodes = new List<SkillNode>();

    [Header("连线设置")]
    public LineRenderer lineRendererPrefab;
    public Color notEnoughResourceColor = Color.red;
    public Color unlockedLineColor = Color.green;
    public Color learnableLineColor = Color.yellow;
    public Color lockedLineColor = Color.gray;
    public Color hiddenLineColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
    public float lineWidth = 0.1f;
    public float lineAnimationSpeed = 2f;
    public float lineRevealDuration = 0.5f;
    public Ease lineRevealEase = Ease.OutCubic;

    [Header("展开动画")]
    public float nodeRevealDelay = 0.1f;
    public Ease revealEase = Ease.OutBack;
    public float revealScale = 1.2f;

    [Header("根节点设置")]
    public bool revealRootNodesImmediately = true;
    public float rootNodeRevealDelay = 0.2f;

    [Header("事件")]
    public UnityEvent onSkillTreeInitialized;
    public UnityEvent<SkillNode> onSkillLearned;

    private List<LineRenderer> connectionLines = new List<LineRenderer>();
    private List<SkillConnection> connections = new List<SkillConnection>();
    private Dictionary<LineRenderer, float> lineRevealProgress = new Dictionary<LineRenderer, float>();
    private HashSet<SkillNode> revealedNodes = new HashSet<SkillNode>();
    private bool isUpdatingNodes = false;

    [System.Serializable]
    public class SkillConnection
    {
        public SkillNode fromNode;
        public Transform fromPoint;
        public SkillNode toNode;
        public Transform toPoint;
        public LineRenderer lineRenderer;
    }

    public int learnedSkillNum = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CollectAllNodes();
        BuildConnections();
        InitializeNodesVisibility();
        StartCoroutine(RevealTreeAnimation());
    }

    private void OnEnable()
    {
        if (GameValManager.Instance != null)
        {
            GameValManager.Instance.OnResourceChanged.AddListener(OnResourceChanged);
        }
        RecalculateVisibleNodes();
    }
    private void RecalculateVisibleNodes()
    {
        revealedNodes.Clear();

        foreach (var node in allSkillNodes)
        {
            // 根节点 或 前置满足 或 已学习 —— 应该可见
            if (node.prerequisites.Count == 0 || node.ArePrerequisitesMet() || node.skillData.isLearned)
            {
                if (!revealedNodes.Contains(node))
                    revealedNodes.Add(node);

                node.gameObject.SetActive(true);
                node.SetVisibility(true, true);
                node.UpdateAvailability(true);
            }
            else
            {
                node.gameObject.SetActive(false);
                node.SetVisibility(false, true);
            }
        }

        UpdateAllLines();
    }
    private void OnDisable()
    {
        if (GameValManager.Instance != null)
        {
            GameValManager.Instance.OnResourceChanged.RemoveListener(OnResourceChanged);
        }
    }

    private void CollectAllNodes()
    {
        allSkillNodes = GetComponentsInChildren<SkillNode>(true).ToList();
        print($"技能节点数量: {allSkillNodes.Count}");
        foreach (var node in allSkillNodes)
        {
            node.onNodeClicked.AddListener(OnSkillNodeClicked);
            node.onNodeLearned.AddListener(OnSkillLearned);
        }
    }

    public void InitializeNodesVisibility()
    {
        List<SkillNode> rootNodes = allSkillNodes.Where(n => n.prerequisites.Count == 0).ToList();

        if (revealRootNodesImmediately)
        {
            foreach (var rootNode in rootNodes)
            {
                rootNode.gameObject.SetActive(true);
                rootNode.SetVisibility(true, true);
                rootNode.UpdateAvailability(true);
                revealedNodes.Add(rootNode);
            }
        }

        foreach (var node in allSkillNodes)
        {
            if (!rootNodes.Contains(node))
            {
                node.gameObject.SetActive(false);
                node.SetVisibility(false, true);
            }
        }
    }

    public void BuildConnections()
    {
        foreach (var line in connectionLines)
        {
            Destroy(line.gameObject);
        }
        connectionLines.Clear();
        connections.Clear();
        lineRevealProgress.Clear();

        foreach (var node in allSkillNodes)
        {
            foreach (var prerequisite in node.prerequisites)
            {
                if (prerequisite.node != null)
                {
                    var connection = new SkillConnection
                    {
                        fromNode = prerequisite.node,
                        toNode = node
                    };

                    connection.fromPoint = FindBestConnectionPoint(prerequisite.node, node.transform.position);
                    connection.toPoint = FindBestConnectionPoint(node, prerequisite.node.transform.position);

                    connections.Add(connection);
                }
            }
        }

        foreach (var connection in connections)
        {
            if (connection.fromPoint != null && connection.toPoint != null)
            {
                var lineRenderer = Instantiate(lineRendererPrefab, transform);
                connection.lineRenderer = lineRenderer;
                SetupLineRenderer(lineRenderer, connection);
                connectionLines.Add(lineRenderer);
                lineRevealProgress[lineRenderer] = 0f;
            }
        }

        UpdateAllLines(true);
    }

    private Transform FindBestConnectionPoint(SkillNode node, Vector3 targetPosition)
    {
        if (node.connectionPoints != null && node.connectionPoints.Length > 0)
        {
            Transform bestPoint = node.connectionPoints[0];
            float minDistance = Vector3.Distance(bestPoint.position, targetPosition);

            for (int i = 1; i < node.connectionPoints.Length; i++)
            {
                float distance = Vector3.Distance(node.connectionPoints[i].position, targetPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestPoint = node.connectionPoints[i];
                }
            }

            return bestPoint;
        }

        return node.transform;
    }

    private void SetupLineRenderer(LineRenderer lineRenderer, SkillConnection connection)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.textureMode = LineTextureMode.Tile;
    }

    private void UpdateAllLines(bool immediate = false)
    {
        foreach (var connection in connections)
        {
            var lineRenderer = connection.lineRenderer;
            if (lineRenderer == null || connection.fromPoint == null || connection.toPoint == null)
                continue;

            Vector3 startPos = connection.fromPoint.position;
            Vector3 endPos = connection.toPoint.position;

            float progress = lineRevealProgress.ContainsKey(lineRenderer) ? lineRevealProgress[lineRenderer] : 0f;

            if (immediate)
            {
                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, endPos);
            }
            else
            {
                Vector3 currentEndPos = Vector3.Lerp(startPos, endPos, progress);
                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, currentEndPos);
            }

            // 找出当前连线的前置定义
            var prereqData = connection.toNode.prerequisites
                .FirstOrDefault(p => p.node == connection.fromNode);

            bool levelEnough = prereqData != null &&
                               prereqData.node.skillData.currentLevel >= prereqData.requiredLevel;

            Color lineColor = hiddenLineColor;
            float alpha = 0f;

            if (levelEnough)
            {
                alpha = 1f;

                bool resourceEnough = true;
                if (GameValManager.Instance != null)
                {
                    resourceEnough = GameValManager.Instance.HasEnoughResource(
                        connection.toNode.skillData.costType,
                        connection.toNode.skillData.costAmount);
                }

                // 按目标节点状态决定颜色
                if (connection.fromNode.State == SkillNodeState.Learned &&
                    connection.toNode.State == SkillNodeState.Learned)
                {
                    lineColor = unlockedLineColor; // 两端都学完 → 绿色
                }
                else if (connection.toNode.State == SkillNodeState.Unlocked)
                {
                    lineColor = resourceEnough ? learnableLineColor : notEnoughResourceColor; // 可学 / 缺资源
                }
                else if (connection.toNode.State == SkillNodeState.Locked)
                {
                    // 节点锁定分两种：
                    if (connection.toNode.ArePrerequisitesMet())
                    {
                        // 前置全部满足 → 用可学 / 缺资源颜色
                        lineColor = resourceEnough ? learnableLineColor : notEnoughResourceColor;
                    }
                    else
                    {
                        // 部分前置没满足 → 灰色半透明
                        lineColor = unlockedLineColor;
                        alpha = 1f;
                    }
                }
            }
            else
            {
                alpha = 0f; // 等级未满足 → 不显示
            }

            lineColor.a = alpha;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            if (alpha <= 0.01f)
            {
                lineRenderer.SetPosition(1, startPos);
                if (lineRevealProgress.ContainsKey(lineRenderer))
                {
                    lineRevealProgress[lineRenderer] = 0f;
                }
            }

            var material = lineRenderer.material;
            if (material != null)
            {
                material.mainTextureOffset = new Vector2(Time.time * lineAnimationSpeed, 0);
            }
        }
    }

    public void UpdateAllNodes()
    {
        if (isUpdatingNodes) return;

        isUpdatingNodes = true;
        var nodesToUpdate = new HashSet<SkillNode>();

        foreach (var node in allSkillNodes)
        {
            if (revealedNodes.Contains(node))
            {
                nodesToUpdate.Add(node);
            }
        }

        foreach (var node in allSkillNodes)
        {
            foreach (var prereq in node.prerequisites)
            {
                if (prereq.node != null && revealedNodes.Contains(prereq.node))
                {
                    nodesToUpdate.Add(node);
                }
            }
        }

        foreach (var node in nodesToUpdate)
        {
            node.UpdateAvailability();
        }

        UpdateAllLines();
        isUpdatingNodes = false;
        foreach (var node in allSkillNodes)
        {
            // 条件：前置满足 & 节点未学习 & 节点可见但还没揭示（或者刚被揭示）
            if (node.ArePrerequisitesMet() && !node.skillData.isLearned)
            {
                // 确保节点显示
                if (!revealedNodes.Contains(node))
                {
                    revealedNodes.Add(node);
                    node.gameObject.SetActive(true);
                    node.RevealWithAnimation(); // 显示节点动画
                    node.UpdateAvailability(true);
                }

                // 绘制所有指向这个节点的连线
                var incomingConnections = connections.Where(c => c.toNode == node).ToList();
                foreach (var connection in incomingConnections)
                {
                    if (connection.lineRenderer != null)
                    {
                        // 如果希望直接完成绘制（无动画），用下面三行：
                        // lineRevealProgress[connection.lineRenderer] = 1f;
                        // connection.lineRenderer.SetPosition(0, connection.fromPoint.position);
                        // connection.lineRenderer.SetPosition(1, connection.toPoint.position);

                        // 如果需要动画：
                        if (!lineRevealProgress.ContainsKey(connection.lineRenderer) ||
                            lineRevealProgress[connection.lineRenderer] < 0.99f)
                        {
                            StartCoroutine(AnimateLineReveal(connection.lineRenderer, connection, false));
                        }
                    }
                }
            }
        }
    }

    private void UpdateConnectedNodes(SkillNode learnedNode)
    {
        learnedNode.UpdateAvailability(true);

        // 更新所有以该节点为前置的节点
        foreach (var node in allSkillNodes)
        {
            foreach (var prereq in node.prerequisites)
            {
                if (prereq.node == learnedNode)
                {
                    node.UpdateAvailability(true);
                    // 如果节点已经解锁，检查是否需要显示
                    if (node.ArePrerequisitesMet() && !revealedNodes.Contains(node))
                    {
                        revealedNodes.Add(node);
                        node.gameObject.SetActive(true);
                        node.RevealWithAnimation();
                    }
                    break;
                }
            }
        }

        UpdateAllLines();
    }

    private void OnSkillNodeClicked(SkillNode clickedNode)
    {
        Debug.Log($"点击技能节点: {clickedNode.skillData.skillName}");
    }

    private void OnSkillLearned(SkillNode learnedNode)
    {
        Debug.Log($"技能已学习: {learnedNode.skillData.skillName}");

        // 立即更新节点状态
        learnedNode.UpdateAvailability(true);

        // 立即显示所有指向这个已学习节点的连线
        RevealAllLinesToNodeImmediately(learnedNode);

        // 更新所有连线
        UpdateAllLines();

        onSkillLearned?.Invoke(learnedNode);
        ApplySkillEffects(learnedNode);

        // 然后显示从该节点出发的连线和后续节点
        StartCoroutine(RevealLinesFromNode(learnedNode));
    }

    private void RevealAllLinesToNodeImmediately(SkillNode node)
    {
        // 查找所有指向这个节点的连线（前置节点的连线）
        var incomingLines = connections.Where(c => c.toNode == node).ToList();

        Debug.Log($"找到 {incomingLines.Count} 条指向节点 {node.skillData.skillName} 的连线");

        foreach (var connection in incomingLines)
        {
            if (connection.lineRenderer != null && connection.fromNode.State == SkillNodeState.Learned)
            {
                // 立即将连线进度设置为完成
                lineRevealProgress[connection.lineRenderer] = 1f;

                // 立即更新连线位置
                Vector3 startPos = connection.fromPoint.position;
                Vector3 endPos = connection.toPoint.position;
                connection.lineRenderer.SetPosition(0, startPos);
                connection.lineRenderer.SetPosition(1, endPos);

                Debug.Log($"立即显示连线: {connection.fromNode.skillData.skillName} -> {connection.toNode.skillData.skillName}");
            }
        }
    }

    private IEnumerator RevealLinesFromNode(SkillNode node)
    {
        // 查找所有从这个节点出发的连线（到后续节点的连线）
        var outgoingLines = connections.Where(c => c.fromNode == node).ToList();

        var linesToAnimate = new List<SkillConnection>();
        var nodesToReveal = new List<SkillNode>();

        Debug.Log($"找到 {outgoingLines.Count} 条从节点 {node.skillData.skillName} 出发的连线");

        foreach (var connection in outgoingLines)
        {
            if (connection.lineRenderer != null)
            {
                if (connection.toNode.ArePrerequisitesMet())
                {
                    linesToAnimate.Add(connection);

                    // 如果目标节点还没有显示，添加到显示列表
                    if (!revealedNodes.Contains(connection.toNode))
                    {
                        nodesToReveal.Add(connection.toNode);
                        Debug.Log($"准备显示节点: {connection.toNode.skillData.skillName}");
                    }
                }
            }
        }

        // 播放所有从当前节点出发的连线动画
        var lineAnimations = new List<IEnumerator>();
        foreach (var connection in linesToAnimate)
        {
            // 如果连线已经显示过，跳过动画
            if (lineRevealProgress.ContainsKey(connection.lineRenderer) &&
                lineRevealProgress[connection.lineRenderer] >= 0.99f)
            {
                continue;
            }

            lineAnimations.Add(AnimateLineReveal(connection.lineRenderer, connection));
        }

        yield return StartCoroutine(RunAnimationsInParallel(lineAnimations));

        // 显示所有符合条件的节点
        foreach (var nodeToReveal in nodesToReveal)
        {
            if (!revealedNodes.Contains(nodeToReveal))
            {
                revealedNodes.Add(nodeToReveal);
                nodeToReveal.gameObject.SetActive(true);
                nodeToReveal.RevealWithAnimation();
                nodeToReveal.UpdateAvailability(true);
                Debug.Log($"已显示节点: {nodeToReveal.skillData.skillName}");
            }
        }
    }

    private IEnumerator RunAnimationsInParallel(List<IEnumerator> animations)
    {
        var runningCoroutines = new List<Coroutine>();
        foreach (var animation in animations)
        {
            runningCoroutines.Add(StartCoroutine(animation));
        }
        foreach (var coroutine in runningCoroutines)
        {
            yield return coroutine;
        }
    }

    private IEnumerator AnimateLineReveal(LineRenderer lineRenderer, SkillConnection connection, bool alsoRevealNode = false)
    {
        if (lineRenderer == null || connection.fromPoint == null || connection.toPoint == null)
            yield break;

        // 检查是否应该显示这条连线
        if (connection.toNode == null) yield break;

        float startProgress = 0f;
        if (lineRevealProgress.ContainsKey(lineRenderer))
        {
            startProgress = lineRevealProgress[lineRenderer];
            if (startProgress >= 1f)
            {
                // 如果已经完成，跳过动画
                yield break;
            }
        }
        else
        {
            lineRevealProgress[lineRenderer] = 0f;
        }

        Vector3 startPos = connection.fromPoint.position;
        Vector3 endPos = connection.toPoint.position;

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, startPos);

        float elapsedTime = 0f;

        while (elapsedTime < lineRevealDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / lineRevealDuration);
            t = DOVirtual.EasedValue(0, 1, t, lineRevealEase);

            float progress = Mathf.Lerp(startProgress, 1f, t);
            lineRevealProgress[lineRenderer] = progress;

            Vector3 currentEndPos = Vector3.Lerp(startPos, endPos, progress);
            lineRenderer.SetPosition(1, currentEndPos);

            // 在动画过程中更新颜色
            Color lineColor = lockedLineColor;
            if (connection.fromNode.State == SkillNodeState.Learned)
            {
                bool resourceEnough = true;
                if (GameValManager.Instance != null)
                {
                    resourceEnough = GameValManager.Instance.HasEnoughResource(
                        connection.toNode.skillData.costType,
                        connection.toNode.skillData.costAmount);
                }

                if (connection.fromNode.State == SkillNodeState.Learned && connection.toNode.State == SkillNodeState.Learned)
                {
                    lineColor = unlockedLineColor;
                }
                else if (connection.fromNode.State == SkillNodeState.Learned && connection.toNode.State == SkillNodeState.Unlocked)
                {
                    lineColor = resourceEnough ? learnableLineColor : notEnoughResourceColor;
                }
                else
                {
                    lineColor = lockedLineColor;
                }
            }

            lineColor.a = progress;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            yield return null;
        }

        lineRevealProgress[lineRenderer] = 1f;
        lineRenderer.SetPosition(1, endPos);

        // 最终更新连线颜色
        UpdateAllLines();
    }

    private void ApplySkillEffects(SkillNode node)
    {
        var data = node.skillData;
        Debug.Log($"应用技能效果: {data.skillName}");
    }

    private void OnResourceChanged(ResourceType type, int oldCount, int newCount)
    {
        if (oldCount != newCount)
        {
            UpdateAllNodes();
        }
    }

    public void LearnAllAvailableSkills()
    {
        foreach (var node in allSkillNodes)
        {
            if (node.CanLearn())
            {
                node.TryLearn();
            }
        }
    }

    public SkillNode GetSkillNode(string skillID)
    {
        return allSkillNodes.Find(node => node.skillData.skillID == skillID);
    }

    public List<SkillNode> GetLearnedSkills()
    {
        return allSkillNodes.Where(node => node.skillData.isLearned).ToList();
    }

    public List<SkillNode> GetLearnableSkills()
    {
        return allSkillNodes.Where(node => node.CanLearn()).ToList();
    }

    public void ResetAllSkills()
    {
        foreach (var node in allSkillNodes)
        {
            node.skillData.currentLevel = 0;
            node.skillData.isLearned = false;
            node.ResetNode();
        }

        foreach (var lineRenderer in connectionLines)
        {
            if (lineRenderer != null && lineRevealProgress.ContainsKey(lineRenderer))
            {
                lineRevealProgress[lineRenderer] = 0f;
            }
        }

        revealedNodes.Clear();
        InitializeNodesVisibility();
        UpdateAllLines();
    }

    public void SaveSkills()
    {
        foreach (var node in allSkillNodes)
        {
            PlayerPrefs.SetInt($"Skill_{node.skillData.skillID}_Level", node.skillData.currentLevel);
            PlayerPrefs.SetInt($"Skill_{node.skillData.skillID}_Learned", node.skillData.isLearned ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    public void LoadSkills()
    {
        foreach (var node in allSkillNodes)
        {
            node.skillData.currentLevel = PlayerPrefs.GetInt($"Skill_{node.skillData.skillID}_Level", 0);
            node.skillData.isLearned = PlayerPrefs.GetInt($"Skill_{node.skillData.skillID}_Learned", 0) == 1;
        }

        revealedNodes.Clear();
        foreach (var node in allSkillNodes)
        {
            if (node.prerequisites.Count == 0)
            {
                node.gameObject.SetActive(true);
                node.SetVisibility(true, true);
                revealedNodes.Add(node);
            }
            else if (node.ArePrerequisitesMet() || node.skillData.isLearned)
            {
                node.gameObject.SetActive(true);
                node.SetVisibility(true, true);
                revealedNodes.Add(node);
            }
            else
            {
                node.gameObject.SetActive(false);
                node.SetVisibility(false, true);
            }
        }

        UpdateAllNodes();
    }

    private IEnumerator RevealTreeAnimation()
    {
        yield return new WaitForEndOfFrame();
        isUpdatingNodes = true;

        foreach (var lineRenderer in connectionLines)
        {
            if (lineRenderer != null)
            {
                Color transparentColor = hiddenLineColor;
                transparentColor.a = 0f;
                lineRenderer.startColor = transparentColor;
                lineRenderer.endColor = transparentColor;
            }
        }

        yield return StartCoroutine(RevealRootNodesAnimation());
        isUpdatingNodes = false;
        onSkillTreeInitialized?.Invoke();
    }

    private IEnumerator RevealRootNodesAnimation()
    {
        List<SkillNode> rootNodes = allSkillNodes.Where(n => n.prerequisites.Count == 0).ToList();
        var rootAnimations = new List<IEnumerator>();
        foreach (var rootNode in rootNodes)
        {
            if (rootNode.gameObject.activeSelf)
            {
                rootAnimations.Add(rootNode.RevealWithAnimationCoroutine());
            }
        }
        yield return StartCoroutine(RunAnimationsInParallel(rootAnimations));
    }

    public List<SkillNode> GetReachableSkills(SkillNode fromNode)
    {
        var reachable = new List<SkillNode>();
        var visited = new HashSet<SkillNode>();
        var queue = new Queue<SkillNode>();

        visited.Add(fromNode);
        queue.Enqueue(fromNode);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();
            var nextNodes = connections
                .Where(c => c.fromNode == currentNode && !visited.Contains(c.toNode))
                .Select(c => c.toNode)
                .ToList();

            foreach (var nextNode in nextNodes)
            {
                if (!visited.Contains(nextNode))
                {
                    visited.Add(nextNode);
                    reachable.Add(nextNode);
                    queue.Enqueue(nextNode);
                }
            }
        }

        return reachable;
    }

    #region 新增的重新播放动画功能
    public void ReplayRevealAnimation()
    {
        StartCoroutine(ReplayRevealAnimationCoroutine());
    }

    private IEnumerator ReplayRevealAnimationCoroutine()
    {
        isUpdatingNodes = true;
        foreach (var node in allSkillNodes)
        {
            if (node.gameObject.activeSelf)
            {
                node.SetVisibility(false, true);
                node.transform.localScale = Vector3.zero;
            }
        }

        foreach (var connection in connections)
        {
            if (connection.lineRenderer != null)
            {
                lineRevealProgress[connection.lineRenderer] = 0f;
                if (connection.fromPoint != null && connection.toPoint != null)
                {
                    Vector3 startPos = connection.fromPoint.position;
                    connection.lineRenderer.SetPosition(0, startPos);
                    connection.lineRenderer.SetPosition(1, startPos);
                }
                Color transparentColor = hiddenLineColor;
                transparentColor.a = 0f;
                connection.lineRenderer.startColor = transparentColor;
                connection.lineRenderer.endColor = transparentColor;
            }
        }

        var previouslyRevealedNodes = new HashSet<SkillNode>(revealedNodes);
        revealedNodes.Clear();
        yield return new WaitForSeconds(0.2f);

        List<SkillNode> rootNodes = allSkillNodes.Where(n => n.prerequisites.Count == 0).ToList();
        foreach (var rootNode in rootNodes)
        {
            if (previouslyRevealedNodes.Contains(rootNode))
            {
                rootNode.gameObject.SetActive(true);
                revealedNodes.Add(rootNode);
                StartCoroutine(AnimateNodeReveal(rootNode, 0f));
            }
        }

        yield return new WaitForSeconds(rootNodeRevealDelay);
        yield return StartCoroutine(RevealNodesByLayers(previouslyRevealedNodes));
        isUpdatingNodes = false;
        UpdateAllNodes();
    }

    private IEnumerator RevealNodesByLayers(HashSet<SkillNode> nodesToReveal)
    {
        Dictionary<SkillNode, int> nodeDepth = CalculateNodeDepths();
        int maxDepth = nodeDepth.Values.DefaultIfEmpty(0).Max();

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            var nodesAtDepth = nodeDepth
                .Where(kvp => kvp.Value == depth && nodesToReveal.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            if (nodesAtDepth.Count > 0)
            {
                var lineAnimations = new List<IEnumerator>();
                foreach (var node in nodesAtDepth)
                {
                    var parentConnections = connections.Where(c => c.toNode == node).ToList();
                    foreach (var connection in parentConnections)
                    {
                        if (connection.lineRenderer != null && revealedNodes.Contains(connection.fromNode))
                        {
                            lineRevealProgress[connection.lineRenderer] = 0f;
                            lineAnimations.Add(AnimateLineReveal(connection.lineRenderer, connection, false));
                        }
                    }
                }

                foreach (var animation in lineAnimations)
                {
                    StartCoroutine(animation);
                }

                yield return new WaitForSeconds(lineRevealDuration * 0.3f);
                foreach (var node in nodesAtDepth)
                {
                    StartCoroutine(DelayedNodeReveal(node, 0f));
                }
                yield return new WaitForSeconds(lineRevealDuration * 0.7f + nodeRevealDelay);
            }
        }
    }

    private IEnumerator DelayedNodeReveal(SkillNode node, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!revealedNodes.Contains(node))
        {
            revealedNodes.Add(node);
            node.gameObject.SetActive(true);
            StartCoroutine(AnimateNodeReveal(node, 0f));
            node.UpdateAvailability(true);
        }
    }

    private IEnumerator AnimateNodeReveal(SkillNode node, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        node.transform.localScale = Vector3.zero;
        node.SetVisibility(true, true);
        node.transform.DOScale(Vector3.one * revealScale, lineRevealDuration)
            .SetEase(revealEase)
            .OnComplete(() =>
            {
                node.transform.DOScale(Vector3.one, lineRevealDuration * 0.5f)
                    .SetEase(Ease.OutBack);
            });
    }

    private Dictionary<SkillNode, int> CalculateNodeDepths()
    {
        var depths = new Dictionary<SkillNode, int>();
        var visited = new HashSet<SkillNode>();
        var queue = new Queue<(SkillNode node, int depth)>();

        foreach (var node in allSkillNodes.Where(n => n.prerequisites.Count == 0))
        {
            queue.Enqueue((node, 0));
            visited.Add(node);
            depths[node] = 0;
        }

        while (queue.Count > 0)
        {
            var (currentNode, currentDepth) = queue.Dequeue();
            foreach (var connection in connections.Where(c => c.fromNode == currentNode))
            {
                if (!visited.Contains(connection.toNode))
                {
                    visited.Add(connection.toNode);
                    depths[connection.toNode] = currentDepth + 1;
                    queue.Enqueue((connection.toNode, currentDepth + 1));
                }
            }
        }

        return depths;
    }

    public void ReplayBranchRevealAnimation(SkillNode fromNode = null)
    {
        if (fromNode == null) ReplayRevealAnimation();
        else StartCoroutine(ReplayBranchAnimationCoroutine(fromNode));
    }

    private IEnumerator ReplayBranchAnimationCoroutine(SkillNode fromNode)
    {
        isUpdatingNodes = true;
        var reachableNodes = GetReachableSkills(fromNode);
        reachableNodes.Insert(0, fromNode);
        foreach (var node in reachableNodes)
        {
            if (node.gameObject.activeSelf)
            {
                node.SetVisibility(false, true);
                node.transform.localScale = Vector3.zero;
            }
        }

        var branchConnections = connections
            .Where(c => reachableNodes.Contains(c.fromNode) || reachableNodes.Contains(c.toNode))
            .ToList();

        foreach (var connection in branchConnections)
        {
            if (connection.lineRenderer != null && lineRevealProgress.ContainsKey(connection.lineRenderer))
            {
                lineRevealProgress[connection.lineRenderer] = 0f;
            }
        }

        yield return new WaitForSeconds(0.2f);
        StartCoroutine(AnimateNodeReveal(fromNode, 0f));
        yield return new WaitForSeconds(nodeRevealDelay);
        var nodeDepths = CalculateBranchDepths(fromNode, reachableNodes);
        int maxDepth = nodeDepths.Values.DefaultIfEmpty(0).Max();

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            var nodesAtDepth = nodeDepths
                .Where(kvp => kvp.Value == depth)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var node in nodesAtDepth)
            {
                var parentConnections = branchConnections.Where(c => c.toNode == node).ToList();
                foreach (var connection in parentConnections)
                {
                    StartCoroutine(AnimateLineReveal(connection.lineRenderer, connection, false));
                }
                StartCoroutine(DelayedNodeReveal(node, nodeRevealDelay * 0.5f));
            }
            yield return new WaitForSeconds(lineRevealDuration + nodeRevealDelay);
        }

        isUpdatingNodes = false;
        UpdateAllNodes();
    }

    private Dictionary<SkillNode, int> CalculateBranchDepths(SkillNode startNode, List<SkillNode> branchNodes)
    {
        var depths = new Dictionary<SkillNode, int>();
        var visited = new HashSet<SkillNode>();
        var queue = new Queue<(SkillNode node, int depth)>();

        queue.Enqueue((startNode, 0));
        visited.Add(startNode);
        depths[startNode] = 0;

        while (queue.Count > 0)
        {
            var (currentNode, currentDepth) = queue.Dequeue();
            var nextNodes = connections
                .Where(c => c.fromNode == currentNode && branchNodes.Contains(c.toNode))
                .Select(c => c.toNode)
                .ToList();

            foreach (var nextNode in nextNodes)
            {
                if (!visited.Contains(nextNode))
                {
                    visited.Add(nextNode);
                    depths[nextNode] = currentDepth + 1;
                    queue.Enqueue((nextNode, currentDepth + 1));
                }
            }
        }

        return depths;
    }
    #endregion
}