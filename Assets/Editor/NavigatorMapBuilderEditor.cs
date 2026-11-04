
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavigatorMapBuilder))]
public class NavigatorMapBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NavigatorMapBuilder builder =
            (NavigatorMapBuilder)target;

        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField(
            "Editor Map Tools",
            EditorStyles.boldLabel
        );

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button(
                "Generate Navigator Map in Editor",
                GUILayout.Height(32)))
            {
                builder.GenerateInEditor();
            }

            bool hasMap = builder.GeneratedRoot != null;

            using (new EditorGUI.DisabledScope(!hasMap))
            {
                if (GUILayout.Button(
                    "Save Generated Map as Prefab",
                    GUILayout.Height(28)))
                {
                    SaveMapPrefab(builder);
                }

                if (GUILayout.Button("Clear Generated Map"))
                {
                    builder.ClearInEditor();
                }
            }
        }
    }

    private static void SaveMapPrefab(NavigatorMapBuilder builder)
    {
        Transform generatedRoot = builder.GeneratedRoot;

        if (generatedRoot == null)
            return;

        string defaultName =
            $"NavigatorMap_{builder.Layout.LayoutId}";

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Navigator Map Prefab",
            defaultName,
            "prefab",
            "Choose where to save the Navigator map."
        );

        if (string.IsNullOrEmpty(path))
            return;

        bool success;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            generatedRoot.gameObject,
            path,
            out success
        );

        if (success)
        {
            Debug.Log($"Navigator map prefab saved: {path}");

            if (prefab != null)
                EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            Debug.LogError("Failed to save Navigator map prefab.");
        }
    }
}
