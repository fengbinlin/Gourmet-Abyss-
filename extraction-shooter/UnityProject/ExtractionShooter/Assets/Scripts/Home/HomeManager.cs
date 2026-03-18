using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomeManager : MonoBehaviour
{
    public static HomeManager instance;
    public Table table;
    public Chair myChair;
    public Chair guestChair;

    [Header("家园建造：当前正在拖拽的建筑")]
    public BuildController currentDraggingUnit;
    // Start is called before the first frame update
    void Start()
    {
        instance=this;
    }

    public bool TryBeginDrag(BuildController controller)
    {
        if (controller == null) return false;
        if (currentDraggingUnit != null && currentDraggingUnit != controller) return false;
        currentDraggingUnit = controller;
        return true;
    }

    public void EndDrag(BuildController controller)
    {
        if (controller == null) return;
        if (currentDraggingUnit == controller) currentDraggingUnit = null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
