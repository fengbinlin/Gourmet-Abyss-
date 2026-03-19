using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-20)] // 早一点初始化，方便其它系统引用
public class CookManager : MonoBehaviour
{
    public static CookManager cookManager;
    public List<CustomerNPC> curCookList = new List<CustomerNPC>();
    public Transform kitchenLeftPoint;
    public Transform kitchenRightPoint;

    private void Awake()
    {
        cookManager = this;
    }

    // 获取一个空闲锅，如果要支持厨师Buff就可调用这个函数
    public Pot GetAvailableCookingPot()
    {
        if (RestaurantPanel.instance == null) return null;
        foreach (Pot pot in RestaurantPanel.instance.potsList)
        {
            if (pot != null && pot.IsAvailable()) return pot;
        }
        return null;
    }

    // 雇佣指定顾客为厨师
    public void RecruitCook(CustomerNPC npc)
    {
        if (npc == null) return;
        npc.ConvertToCook();
    }

    // 解雇指定厨师
    public void FireCook(CustomerNPC npc)
    {
        if (npc == null) return;
        npc.FireCook();
    }

    // 返回所有厨师Data
    public List<CustomerData> GetAllCookData()
    {
        List<CustomerData> list = new List<CustomerData>();
        foreach (var npc in curCookList)
        {
            list.Add(npc.data);
        }
        return list;
    }

    // 获取一个空闲厨师（你可以之后扩展条件，例如距离锅最近）
    public CustomerNPC GetAvailableCook()
    {
        foreach (CustomerNPC cook in curCookList)
        {
            if (cook == null || cook.data == null) continue;

            if (cook.data.isCook && !cook.isCookingNow)
            {
                return cook;  // 找第一个空闲厨师
            }
        }
        return null;
    }

    // 根据厨师添加Buff，返回 Buff 后的菜谱副本
    public DishRecipe ApplyCookBuff(DishRecipe originalRecipe, CustomerNPC cook)
    {
        if (originalRecipe == null || cook == null || cook.data == null)
            return originalRecipe;

        // 创建副本，不直接修改原菜谱（防止被全局污染）
        DishRecipe modified = new DishRecipe();
        modified.dishID = originalRecipe.dishID;
        modified.dishName = originalRecipe.dishName;
        modified.dishIcon = originalRecipe.dishIcon;
        modified.ingredients = new List<DishIngredient>(originalRecipe.ingredients);
        modified.acceptablePot = new List<potType>(originalRecipe.acceptablePot);
        modified.category = originalRecipe.category;

        // ⭐ Buff 应用
        modified.cookTime = originalRecipe.cookTime * cook.data.timeReductionRate;   // 例如 timeReductionRate = 0.8 表示烹饪时间减少 20%
        modified.baseDishPrice = originalRecipe.baseDishPrice * cook.data.priceIncreaseRate;
        // 产出加成：你可以在装盘时让每盘菜数量增加，这里先记个字段
        return modified;
    }
}