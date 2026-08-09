using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneTitle : MonoBehaviour
{
    public static SceneTitle instance;
    public float SceneOxygenCostSpeedMultiplier=1;
    public string SceneName;
    void Awake()
    {
        instance=this;
        ApplyLevelSatietyConsumptionConfig();
    }
    // Start is called before the first frame update
    void Start()
    {
        ApplyLevelSatietyConsumptionConfig();
    }

    private void ApplyLevelSatietyConsumptionConfig()
    {
        if (ExcelConfigReader.Instance == null) return;

        string sceneName = gameObject.scene.name;
        if (!ExcelConfigReader.Instance.TryGetLevelSatietyConsumptionConfig(
                sceneName,
                out LevelSatietyConsumptionConfigData config))
            return;

        SceneOxygenCostSpeedMultiplier = config.consumeEnabled
            ? config.consumeMultiplier
            : 0f;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
