using System;
using UnityEngine;

/// <summary>
/// 宠物类型枚举：WeaponStatsManager 的宠物状态列表会用到它。
/// </summary>
public enum PetType
{
    None = 0,
    FlyingCompanion = 1, // 对应现有的 FlyingCompanionController（飞行随从）
}

/// <summary>
/// 宠物成长数值（由 PetManager 在战斗进入时写入到“宠物系统”组件）。
/// </summary>
[Serializable]
public class PetGrowthValues
{
    [Header("攻击与射击")]
    public float attackRange = 12f;
    public float fireInterval = 0.45f;
    public float bulletDamage = 12f;

    [Header("子弹表现")]
    public float bulletMoveSpeed = 18f;
    public float bulletRotateSpeed = 540f;
    public float bulletLifeTime = 4f;
    public float bulletHitDistance = 0.35f;
}

/// <summary>
/// 宠物系统接口：PetManager 在生成预制体后会调用它，把成长数值写入到宠物逻辑里。
/// </summary>
public interface IPetSystem
{
    void ApplyGrowth(PetGrowthValues growth);
}

