using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuildingUnit))]
public class BuildingUnitEditor : Editor
{
    private BuildingUnit unit;

    private void OnEnable()
    {
        unit = (BuildingUnit)target;
    }

    public override void OnInspectorGUI()
    {
        // 绘制默认的属性
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("占用掩码编辑", EditorStyles.boldLabel);

        if (unit.occupyMaskFlat == null || unit.occupyMaskFlat.Length != unit.size * unit.size)
        {
            unit.OnValidate();
        }

        // 画网格编辑器
        for (int y = unit.size - 1; y >= 0; y--) // 上到下绘制
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < unit.size; x++)
            {
                bool current = unit.GetOccupy(x, y);

                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.textColor = Color.white;

                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = current ? Color.green : Color.red;

                if (GUILayout.Button(current ? "1" : "0", style, GUILayout.Width(25), GUILayout.Height(25)))
                {
                    unit.SetOccupy(x, y, !current);
                    EditorUtility.SetDirty(unit); // 标记脏数据，确保保存
                }

                GUI.backgroundColor = prevColor;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}