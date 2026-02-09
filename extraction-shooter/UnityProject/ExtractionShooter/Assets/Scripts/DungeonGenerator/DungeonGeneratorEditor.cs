using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonGenerator))]
public class DungeonGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        DungeonGenerator generator = (DungeonGenerator)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("调试信息", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"总房间数: {generator.totalRooms}");
        EditorGUILayout.LabelField($"主房间数: {generator.mainRoomsCount}");
        EditorGUILayout.LabelField($"走廊格子数: {generator.corridorsCount}");
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("生成地牢"))
        {
            generator.GenerateDungeon();
        }
        
        if (GUILayout.Button("清空地牢"))
        {
            generator.ClearDungeon();
        }
    }
}