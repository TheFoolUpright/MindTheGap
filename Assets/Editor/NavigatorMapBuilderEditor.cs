using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavigatorMapBuilder))]
public class NavigatorMapBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("EDITOR MAP TOOLS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bake the 2D UI map in Edit Mode. Changes to Tiled layouts require " +
            "re-importing MazeLayoutData and regenerating this map.",
            MessageType.Info);

        NavigatorMapBuilder builder = (NavigatorMapBuilder)target;
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Generate Navigator Map in Editor", GUILayout.Height(32f)))
                builder.GenerateInEditor();
            if (GUILayout.Button("Clear Generated Navigator Map"))
            {
                if (EditorUtility.DisplayDialog("Clear Navigator map?",
                    "Delete only the Generated Map hierarchy?", "Clear", "Cancel"))
                    builder.ClearInEditor();
            }
        }
    }
}
