using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public enum potState
{
    unUsed, //未被使用
    Used, //被占用
}
public enum potType
{
    saucepan,//炖锅
    skillet, //平底煎锅
    hotpot //火锅
}

[System.Serializable]
public class DishRecipe
{
    public int dishID;                      //菜ID
    public string dishName;                 // 菜名
    public Sprite dishIcon;                 // 菜图标
    public List<DishIngredient> ingredients = new List<DishIngredient>();  // 所需食材
    public List<potType> acceptablePot = new List<potType>(); // 可以接受的锅
    public float cookTime = 10f;            // 烹饪时间（秒）
    [Tooltip("顾客就坐后用餐时长（秒，基础值）。实际时间 = sellTime / WeaponStatsManager.sellTimeMultiplier")]
    public float sellTime = 5f;
    public float baseDishPrice = 1;         // 基本菜价格
    public DishCategory category = DishCategory.MainCourse; // 菜肴分类
    public bool locked = true;              // 是否锁定（未解锁的菜不显示/不可用）
}

// 添加菜肴分类枚举
public enum DishCategory
{
    Appetizer,      // 前菜
    MainCourse,     // 主菜
    Dessert,        // 甜品
    Drink,          // 饮品
    Snack           // 小吃
}

[System.Serializable]
public class DishIngredient
{
    public ResourceType resourceType;       // 食材类型
    public int requiredCount;               // 所需数量
}
public class dishItemPrefabs : MonoBehaviour
{
    public Text disName;
    public Image dishItem;
    public Transform dishFoodParent;
    public Text dishPrice;
    public Image DishBG;
    public GameObject cookDishButton;
    [Tooltip("选中时 DishBG 使用的 Sprite；未选中时用 dishNormalBGSprite 或首次缓存的图")]
    public Sprite dishSelectedBG;
    [Tooltip("未选中时的背景；为空则用进入场景时 DishBG 的 sprite")]
    public Sprite dishNormalBGSprite;
    public GameObject LockImage;
    public DishRecipe recipeData;

    private RestaurantPanel _owner;
    private int _itemIndex;
    private Sprite _cachedNormalBgSprite;
    private Tweener _scaleTween;
    private Sequence _scaleSequence;
    private Vector3 _baseLocalScale;
    private bool _cachedSprites;
    // 由策划在 Inspector 手动绑定按钮回调；代码不再自动注册监听，避免重复触发

    /// <summary>首次布局完成后缓存的 Graphic 与预制体上的初始颜色（选中时原样恢复，不强行改色）。</summary>
    private bool _capturedInitialColors;
    private readonly List<Graphic> _graphicsForTint = new List<Graphic>();
    private readonly List<Color> _initialGraphicColors = new List<Color>();

    private void Awake()
    {
        _baseLocalScale = transform.localScale;
        CacheDefaultSprites();
        if (cookDishButton != null)
        {
            cookDishButton.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _scaleTween?.Kill();
        _scaleSequence?.Kill();
    }

    private void CacheDefaultSprites()
    {
        if (_cachedSprites) return;
        if (dishNormalBGSprite != null)
            _cachedNormalBgSprite = dishNormalBGSprite;
        else if (DishBG != null)
            _cachedNormalBgSprite = DishBG.sprite;
        _cachedSprites = true;
    }

    /// <summary>在列表生成并填好文案/食材子物体后调用一次，记录 Inspector 里配置的原始颜色。</summary>
    private void CaptureInitialGraphicColorsIfNeeded()
    {
        if (_capturedInitialColors) return;
        CacheDefaultSprites();
        _graphicsForTint.Clear();
        _initialGraphicColors.Clear();

        if (DishBG != null) AddGraphicSnapshot(DishBG);
        if (dishItem != null) AddGraphicSnapshot(dishItem);
        if (disName != null) AddGraphicSnapshot(disName);
        if (dishPrice != null) AddGraphicSnapshot(dishPrice);

        if (dishFoodParent != null)
        {
            for (int i = 0; i < dishFoodParent.childCount; i++)
            {
                foreach (Graphic g in dishFoodParent.GetChild(i).GetComponentsInChildren<Graphic>(true))
                    AddGraphicSnapshot(g);
            }
        }

        _capturedInitialColors = true;
    }

    private void AddGraphicSnapshot(Graphic g)
    {
        if (g == null) return;
        _graphicsForTint.Add(g);
        _initialGraphicColors.Add(g.color);
    }

    public void SetOwner(RestaurantPanel owner, int index)
    {
        _owner = owner;
        _itemIndex = index;
    }

    public void SetRecipeData(DishRecipe recipe)
    {
        recipeData = recipe;
    }

    /// <summary>点击整行选中（由 Inspector 手动绑定任意可点击按钮）。</summary>
    public void OnDishRowClicked()
    {
        if (_owner == null) return;

        bool wasSelected = _owner.IsDishSelected(_itemIndex);
        _owner.SelectDishItemByIndex(_itemIndex);

        // 已选中时再次点击整行，等同点击“烹饪”按钮
        if (wasSelected)
            OnCookButtonClicked();
    }

    public void OnCookButtonClicked()
    {
        if (recipeData == null)
        {
            Debug.LogWarning("菜谱数据为空！");
            return;
        }
        if (recipeData.locked)
        {
            Debug.Log("该菜品尚未解锁，无法烹饪。");
            return;
        }

        if (RestaurantPanel.instance == null)
        {
            Debug.LogError("RestaurantPanel.instance 未初始化");
            return;
        }

        if (RestaurantPanel.instance.TryEnqueueDishForCooking(recipeData))
        {
            PlayCookButtonRowPulse();
            Debug.Log($"已加入烹饪队列：{recipeData.dishName}");
        }
        else
            RefreshUI();
    }

    /// <summary>点击烹饪成功：整行菜单项缩放反馈。</summary>
    public void PlayCookButtonRowPulse()
    {
        _scaleTween?.Kill();
        _scaleSequence?.Kill();
        transform.localScale = _baseLocalScale;
        const float peak = 1.24f;
        const float mid = 1.08f;
        _scaleSequence = DOTween.Sequence();
        _scaleSequence.Append(transform.DOScale(_baseLocalScale * peak, 0.1f).SetEase(Ease.OutQuad));
        _scaleSequence.Append(transform.DOScale(_baseLocalScale * mid, 0.09f).SetEase(Ease.InOutSine));
        _scaleSequence.Append(transform.DOScale(_baseLocalScale, 0.16f).SetEase(Ease.OutBack, 1.45f));
    }

    private void RefreshUI()
    {
        if (RestaurantPanel.instance != null)
        {
            RestaurantPanel.instance.GenerateFoodItems();
            RestaurantPanel.instance.GenerateDishList();
        }
    }

    /// <summary>选中且可做：恢复初始色；选中但不可做：仍变暗；未选中：按可做/不可做变暗。烹饪按钮仅选中且可做时显示。</summary>
    public void ApplyVisual(bool selected, bool canCook)
    {
        CaptureInitialGraphicColorsIfNeeded();
        bool isLocked = recipeData != null && recipeData.locked;

        _scaleTween?.Kill();
        _scaleSequence?.Kill();

        if (selected)
        {
            transform.localScale = _baseLocalScale;
            const float peak = 1.22f;
            const float dip = 1.08f;
            const float settle = 1.12f;
            _scaleSequence = DOTween.Sequence();
            _scaleSequence.Append(transform.DOScale(_baseLocalScale * peak, 0.08f).SetEase(Ease.OutQuad));
            _scaleSequence.Append(transform.DOScale(_baseLocalScale * dip, 0.07f).SetEase(Ease.InOutSine));
            _scaleSequence.Append(transform.DOScale(_baseLocalScale * settle, 0.12f).SetEase(Ease.OutBack, 1.45f));
        }
        else
        {
            _scaleTween = transform
                .DOScale(_baseLocalScale, 0.1f)
                .SetEase(Ease.InBack);
        }

        if (DishBG != null)
        {
            if (selected && dishSelectedBG != null)
                DishBG.sprite = dishSelectedBG;
            else if (_cachedNormalBgSprite != null)
                DishBG.sprite = _cachedNormalBgSprite;
        }

        float mul = isLocked ? 0.62f : GetColorMultiplier(selected, canCook);
        for (int i = 0; i < _graphicsForTint.Count; i++)
        {
            Graphic g = _graphicsForTint[i];
            if (g == null) continue;
            Color baseC = _initialGraphicColors[i];
            g.color = new Color(
                baseC.r * mul,
                baseC.g * mul,
                baseC.b * mul,
                baseC.a);
        }

        if (cookDishButton != null)
        {
            bool showCook = !isLocked && selected && canCook;
            cookDishButton.SetActive(showCook);
            if (cookDishButton != null)
                cookDishButton.GetComponent<Button>().interactable = showCook;
        }

        if (LockImage != null)
            LockImage.SetActive(isLocked);
        if (disName != null)
            disName.gameObject.SetActive(!isLocked);
        if (dishPrice != null)
            dishPrice.gameObject.SetActive(!isLocked);
    }

    private static float GetColorMultiplier(bool selected, bool canCook)
    {
        if (canCook) return 1f;
        return 0.42f;
    }
}
