using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridManager))]
public class GridManagerEditor : Editor
{
    private Vector2 _matrixScroll;
    private const float CellPx = 22f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var gm = (GridManager)target;
        gm.SyncBlockMaskSize();
        serializedObject.Update();

        DrawDefaultInspector();

        SerializedProperty blockMask = serializedObject.FindProperty("blockPlacementMaskFlat");
        SerializedProperty wProp = serializedObject.FindProperty("gridWidth");
        SerializedProperty hProp = serializedObject.FindProperty("gridHeight");

        if (blockMask == null || wProp == null || hProp == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        int w = Mathf.Max(1, wProp.intValue);
        int h = Mathf.Max(1, hProp.intValue);
        int expected = w * h;
        if (blockMask.arraySize != expected)
        {
            gm.SyncBlockMaskSize();
            serializedObject.Update();
            blockMask = serializedObject.FindProperty("blockPlacementMaskFlat");
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("禁摆区域矩阵", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("与 BuildingUnit 掩码类似：点击格子切换。橙色格 = 禁止摆放；绿色格 = 可摆放。与运行时家具占用无关。", MessageType.Info);

        float tableWidth = w * CellPx + 24f;
        const float maxScrollHeight = 420f;
        _matrixScroll = EditorGUILayout.BeginScrollView(_matrixScroll, GUILayout.Height(maxScrollHeight), GUILayout.MaxWidth(tableWidth + 40f));

        for (int gy = h - 1; gy >= 0; gy--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int gx = 0; gx < w; gx++)
            {
                int idx = gy * w + gx;
                if (idx < 0 || idx >= blockMask.arraySize)
                    break;

                SerializedProperty cell = blockMask.GetArrayElementAtIndex(idx);
                bool blocked = cell.boolValue;

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = blocked ? new Color(0.82f, 0.38f, 0.35f) : new Color(0.42f, 0.68f, 0.48f);

                string label = blocked ? "×" : "·";
                var content = new GUIContent(label, blocked ? "禁止摆放" : "可摆放");
                if (GUILayout.Button(content, GUILayout.Width(CellPx), GUILayout.Height(CellPx)))
                {
                    Undo.RecordObject(gm, "Toggle Grid Block Cell");
                    cell.boolValue = !cell.boolValue;
                    serializedObject.ApplyModifiedProperties();
                }

                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField($"尺寸 {w} × {h}，共 {expected} 格", EditorStyles.miniLabel);

        serializedObject.ApplyModifiedProperties();
    }
}
