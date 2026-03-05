using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using JetBrains.Annotations;

public enum PointType
{
    Entrance,
    Restaurant,
    Queue,
    Plate
}

public enum CustomerState
{
    Spawning,         // 刚生成
    WalkingToQueue,   // 正在走向队尾
    Queueing,         // 排队中
    InsideRestaurant, // 在餐厅消费
    Leaving           // 离开餐厅
}

[System.Serializable]
public class ScenePoint
{
    public PointType type;
    public Transform pointTransform;
}



[System.Serializable]
public class CustomerData
{
    public string id;
    public string customerName;
    public bool isMan;
    public float buyprobability = 0.2f;
    public int AffectionLevel = 0;
    public List<float> AffectionLevelNeed;
    public List<int> likePeopleList;
    public List<int> dislikePeopleList;
    public List<string> SpawningWords;
    public List<string> WalkingToQueueWords;
    public List<string> QueueingWords;
    public List<string> InsideRestaurantQueueingWords;
    public List<string> InsideRestaurantConsumingWords;
    public List<string> LeavingRestaurantWords;
    public List<string> noPlateFoodWords;
    public List<int> favouriteFood;
    //Todo爱心事件
    //TODO 喜欢的物品
    //TODO 喜欢的家具
    //TODO 雇佣后能力


    public bool wantToBuyDish;
    public float moveSpeed;

    public CustomerData(string name, bool wantBuy, float speed)
    {
        customerName = name;
        wantToBuyDish = wantBuy;
        moveSpeed = speed;
    }
}

public class CustomerNPC : MonoBehaviour
{
    public CustomerData data;
    public CustomerState state = CustomerState.Spawning;
    public GameObject bubble;         // 气泡对象
    public Text bubbleText;           // 气泡文本组件
    private Vector3 targetPosition;
    private Plate targetPlate;
    private bool isConsuming = false;
    private CustomerManager manager;

    public Transform coinSpawnPoint;
    // 气泡相关变量
    private Coroutine bubbleRoutineCoroutine;  // 循环更新协程
    private Coroutine bubbleHideCoroutine;     // 气泡隐藏协程
    private float bubbleDuration = 4f;         // 气泡显示持续时间
    private float bubbleInterval = 3f;         // 气泡更新间隔
    private float bubbleRandomness = 2f;       // 气泡随机性

    public List<GameObject> playerModelList;
    public Animator animator;

    public float rotationSpeed = 5.0f; // 控制旋转平滑速度

    public void Init()
    {

        manager = CustomerManager.instance;

        // 初始化气泡
        if (bubble != null)
        {
            bubble.SetActive(false);
        }

        // 随机选择模型
        // int activateModelIndex = Random.Range(0, playerModelList.Count);
        // for (int i = 0; i < playerModelList.Count; i++)
        // {
        //     playerModelList[i].SetActive(i == activateModelIndex);
        // }
        // animator = playerModelList[activateModelIndex].GetComponent<Animator>();

        // 启动气泡协程
        StartBubbleRoutine();
    }

    void Update()
    {
        if (!isConsuming)
        {
            MoveToTarget();

            // 如果正在离开且到达目标，销毁自己
            if (state == CustomerState.Leaving && Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                manager?.RemoveCustomer(this);
                Destroy(gameObject);
            }
        }

        // 更新动画
        animator.SetBool("isRunning", Vector3.Distance(transform.position, targetPosition) >= 0.1f);

        // 更新气泡位置（使其跟随角色）
        UpdateBubblePosition();
    }

    private void UpdateBubblePosition()
    {

    }

    private void StartBubbleRoutine()
    {
        if (bubbleRoutineCoroutine != null)
            StopCoroutine(bubbleRoutineCoroutine);

        bubbleRoutineCoroutine = StartCoroutine(BubbleUpdateCoroutine());
    }

    private IEnumerator BubbleUpdateCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(bubbleInterval - bubbleRandomness, bubbleInterval + bubbleRandomness));

            if (state == CustomerState.Leaving || bubble == null)
                break;

            // 根据状态显示不同气泡
            ShowThoughtBubble();
        }
    }

    private void ShowThoughtBubble()
    {
        if (bubble == null || bubbleText == null)
            return;

        string thoughtText = "";

        switch (state)
        {
            case CustomerState.Spawning:
                //thoughtText = GetRandomThought(new string[] { "肚子好饿...", "好想吃饭", "看看有什么好吃的" });
                break;

            case CustomerState.WalkingToQueue:
                thoughtText = GetRandomThought(data.WalkingToQueueWords.ToArray());
                break;

            case CustomerState.Queueing:
                thoughtText = GetRandomThought(data.QueueingWords.ToArray());
                break;

            case CustomerState.InsideRestaurant:
                if (isConsuming)
                    thoughtText = GetRandomThought(data.InsideRestaurantConsumingWords.ToArray());
                else
                    thoughtText = GetRandomThought(data.InsideRestaurantQueueingWords.ToArray());
                break;

            case CustomerState.Leaving:
                //thoughtText = GetRandomThought(new string[] { "吃饱了", "下次再来", "味道不错", "回家咯" });
                break;
        }
        if (thoughtText != "")
        {
            ShowBubble(thoughtText);
        }

    }

    private string GetRandomThought(string[] thoughts)
    {
        if (thoughts == null || thoughts.Length == 0)
            return "";

        int index = Random.Range(0, thoughts.Length);
        return thoughts[index];
    }

    private void ShowBubble(string text)
    {
        if (bubble == null || bubbleText == null)
            return;

        bubbleText.text = text;
        bubble.SetActive(true);

        // 启动定时关闭气泡
        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);

        bubbleHideCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    private IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSeconds(bubbleDuration);

        if (bubble != null)
            bubble.SetActive(false);

        // ⚠️ 这里不再重启 StartBubbleRoutine()，避免协程冲突
    }

    public void SetTarget(Vector3 pos)
    {
        targetPosition = pos;
    }

    public void GoToPlate(Plate plate)
    {
        if (plate == null)
        {
            //没找到有食物的碟子
            targetPlate = null;
            state = CustomerState.Leaving;
            Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
            if (exit != null)
            {
                SetTarget(exit.position);
            }
            return;
        }

        targetPlate = plate;
        state = CustomerState.InsideRestaurant;
        SetTarget(new Vector3(plate.transform.position.x, plate.transform.position.y, transform.position.z));

        ShowBubble(GetRandomThought(data.InsideRestaurantQueueingWords.ToArray()));
    }

    public void LeaveRestaurant()
    {
        targetPlate = null;
        state = CustomerState.Leaving;
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            SetTarget(exit.position);
        }
        //Debug.Log("D5555");
        ShowBubble(GetRandomThought(data.LeavingRestaurantWords.ToArray()));
    }
    public void LeaveRestaurantNoPlates()
    {
        targetPlate = null;
        state = CustomerState.Leaving;
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            SetTarget(exit.position);
        }
        //Debug.Log("D5555");
        ShowBubble(GetRandomThought(data.noPlateFoodWords.ToArray()));
    }
    public void donotWantToEat()
    {
        targetPlate = null;
        state = CustomerState.Leaving;
        Transform exit = CustomerManager.instance.GetRandomExitPoint(transform.position);
        if (exit != null)
        {
            SetTarget(exit.position);
        }

        //ShowBubble("吃饱回家咯~");
    }


    private void MoveToTarget()
    {
        targetPosition = new Vector3(targetPosition.x, 4.036f, targetPosition.z);

        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            data.moveSpeed * Time.deltaTime
        );

        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (state == CustomerState.Queueing && Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(CustomerManager.instance.queueFrontPoint.transform.position - transform.position);
            transform.rotation = targetRotation;
            return;
        }

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f && state != CustomerState.Queueing)
        {
            OnReachTarget();
        }
    }

    private void OnReachTarget()
    {
        if (state == CustomerState.InsideRestaurant && targetPlate != null)
        {
            StartCoroutine(ConsumeDishCoroutine());
        }
    }

    private IEnumerator ConsumeDishCoroutine()
    {
        if (targetPlate == null || targetPlate.currentDish == null || targetPlate.currentDish.IsEmpty())
        {
            //Debug.Log("D11111");
            LeaveRestaurant();
            yield break;
        }

        isConsuming = true;
        ShowBubble("开动了！");
        float consumeTime = targetPlate.consumeTime;

        float elapsed = 0f;
        while (elapsed < consumeTime)
        {
            if (targetPlate.currentDish == null || targetPlate.currentDish.IsEmpty())
            {
                isConsuming = false;
                //Debug.Log("D22222");
                LeaveRestaurant();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        float cost = 0;
        if (targetPlate.currentDish != null)
        {
            cost = targetPlate.currentDish.recipe.baseDishPrice;
        }



        StartCoroutine(FinishConsumeInteractionCoroutine(cost));
    }
    private IEnumerator FinishConsumeInteractionCoroutine(float cost)
    {
        // 1️⃣ 面向餐碟旋转
        Vector3 lookDir = targetPlate.transform.position - transform.position;
        lookDir.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(lookDir);
        float rotateTime = 0.5f;
        float elapsed = 0f;

        while (elapsed < rotateTime)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsed / rotateTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2️⃣ 顾客跳一下表示满意
        Vector3 originalPos = transform.position;
        float jumpHeight = 1.2f;
        float jumpDuration = 0.6f;
        float jumpElapsed = 0f;
        if (data.favouriteFood.Contains(targetPlate.currentDish.recipe.dishID)&&targetPlate.currentDish.currentAmount>=2)
        {
            ShowCustomBubble("这是我的最爱！", jumpDuration * 2 + 1f); // 气泡显示时间要长一些

            // 跳两次
            for (int i = 0; i < 2; i++)
            {
                jumpElapsed = 0f; // 重置时间
                while (jumpElapsed < jumpDuration/2)
                {
                    float progress = jumpElapsed / (jumpDuration/2);
                    float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                    transform.position = new Vector3(originalPos.x, originalPos.y + yOffset, originalPos.z);
                    jumpElapsed += Time.deltaTime;
                    yield return null;
                }
                transform.position = originalPos;

                // 每次跳跃后爆金币
                int goldAmount = Mathf.RoundToInt(cost);
                Transform moneyBox = CustomerManager.instance.moneyBoxTransform;
                if (moneyBox != null)
                {
                    StartCoroutine(SpawnMoneySmoothly(goldAmount, coinSpawnPoint, moneyBox));
                }

                // 如果不是最后一次跳跃，等待一下
                if (i < 1)
                {
                    yield return new WaitForSeconds(0.5f);
                }
                targetPlate.StartConsume();
            }
        }
        else
        {
            // 非最爱菜品，只跳一次
            ShowCustomBubble("太好吃了！", jumpDuration);
            while (jumpElapsed < jumpDuration)
            {
                float progress = jumpElapsed / jumpDuration;
                float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                transform.position = new Vector3(originalPos.x, originalPos.y + yOffset, originalPos.z);
                jumpElapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = originalPos;

            // 爆金币
            int goldAmount = Mathf.RoundToInt(cost);
            Transform moneyBox = CustomerManager.instance.moneyBoxTransform;
            if (moneyBox != null)
            {
                StartCoroutine(SpawnMoneySmoothly(goldAmount, coinSpawnPoint, moneyBox));
            }
            targetPlate.StartConsume();
        }
        yield return new WaitForSeconds(0.5f);


        // 3️⃣ 爆金币逻辑 —— 菜价换金币特效




        // 4️⃣ 顾客完成吃饭，准备离开
        isConsuming = false;
        LeaveRestaurant();
    }
    private IEnumerator SpawnMoneySmoothly(int totalAmount, Transform start, Transform target)
    {
        if (totalAmount <= 0) yield break;

        int remaining = totalAmount;

        float spawnInterval = 0f; // 每0.05秒一批
        int batchMin = 5;
        int batchMax = 5; // 每次发几个，可根据需求调节

        ProjectileLauncher launcher = CustomerManager.instance.projectileLauncher;
        if (launcher == null || target == null) yield break;

        while (remaining > 0)
        {
            int thisBatch = Mathf.Min(Random.Range(batchMin, batchMax + 1), remaining);

            launcher.SpawnProjectile(
                start,
                target,
                ResourceType.Money,
                thisBatch,
                () =>
                {
                    MoneyChest.Instance.AddMoney(thisBatch);
                }
            );

            remaining -= thisBatch;
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    public void SetState(CustomerState newState)
    {
        if (state == newState) return;

        state = newState;
        ShowThoughtBubble();
    }

    public void ShowCustomBubble(string text, float duration = 2f)
    {
        if (string.IsNullOrEmpty(text) || bubble == null) return;

        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);

        bubbleDuration = duration;
        bubbleText.text = text;
        bubble.SetActive(true);

        bubbleHideCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    void OnDestroy()
    {
        manager?.RemoveCustomer(this);

        if (bubbleRoutineCoroutine != null)
            StopCoroutine(bubbleRoutineCoroutine);

        if (bubbleHideCoroutine != null)
            StopCoroutine(bubbleHideCoroutine);
    }
}