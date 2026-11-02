using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExplorerMazeBuilder))]
public class ExplorerMazeBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Editor-only maze tools", EditorStyles.boldLabel);

        ExplorerMazeBuilder builder = (ExplorerMazeBuilder)target;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Generate Maze in Editor", GUILayout.Height(32)))
                builder.GenerateInEditor();

            using (new EditorGUI.DisabledScope(builder.GeneratedRoot == null))
            {
                if (GUILayout.Button("Save Generated Maze as Prefab"))
                {
                    string defaultName = builder.Layout != null
                        ? $"Maze_{builder.Layout.LayoutId:00}" : "Maze_Baked";

                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save baked maze Prefab", defaultName, "prefab",
                        "Choose where to save your baked maze prefab.");

                    if (!string.IsNullOrEmpty(path))
                    {
                        bool success;
                        PrefabUtility.SaveAsPrefabAssetAndConnect(
                            builder.GeneratedRoot.gameObject, path,
                            InteractionMode.UserAction, out success);

                        if (success)
                            Debug.Log($"Saved baked maze prefab to {path}", builder);
                        else
                            Debug.LogError($"Failed to save maze prefab to {path}", builder);
                    }
                }

                if (GUILayout.Button("Clear Generated Maze"))
                    builder.ClearInEditor();
            }
        }

        EditorGUILayout.HelpBox(
            "No maze is generated at runtime. Save the scene or prefab after generating. " +
            "Generated static geometry should be switched, not rebuilt, during a refresh.",
            MessageType.Info);
    }
}
