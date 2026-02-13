using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class DishRecipe
{
    public string dishName;                 // 菜名
    public Sprite dishIcon;                 // 菜图标
    public List<DishIngredient> ingredients = new List<DishIngredient>();  // 所需食材
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
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
