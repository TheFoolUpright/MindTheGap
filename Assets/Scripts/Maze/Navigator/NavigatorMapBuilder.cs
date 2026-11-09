
using UnityEngine;

public class NavigatorMapBuilder : MonoBehaviour
{
    [Header("Maze Layout")]
    [SerializeField] private MazeLayoutData layout;

    [Header("UI References")]
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private GameObject lightPrefab;

    [Header("Map Appearance")]
    [SerializeField, Min(1f)] private float unitsPerCell = 24f;
    [SerializeField, Min(1f)] private float wallThickness = 4f;

    // Set at runtime by NavigatorMazeController after a map swap.
    // Not serialized, so Editor generation always uses 'layout'.
    private MazeLayoutData runtimeLayout;

    public MazeLayoutData Layout =>
        runtimeLayout != null ? runtimeLayout : layout;
    public float UnitsPerCell => unitsPerCell;

    public void SetRuntimeLayout(MazeLayoutData newLayout)
    {
        runtimeLayout = newLayout;
    }

    public Transform GeneratedRoot =>
        mapContent != null
            ? mapContent.Find("Generated Map")
            : null;

#if UNITY_EDITOR

    private void GenerateWalls()
    {
        if (Application.isPlaying)
            return;

        if (layout == null || !layout.IsConfigured ||
            mapContent == null || wallPrefab == null)
        {
            Debug.LogError("Assign the layout, MapContent, and WallUI prefab.");
            return;
        }

        if (wallPrefab.GetComponent<RectTransform>() == null)
        {
            Debug.LogError("WallUI needs a RectTransform.");
            return;
        }

        // Remove the previously generated walls.
        Transform previous = mapContent.Find("Generated Walls");

        if (previous != null)
            UnityEditor.Undo.DestroyObjectImmediate(previous.gameObject);

        // Create a parent for the new walls.
        GameObject root = new GameObject(
            "Generated Walls",
            typeof(RectTransform)
        );

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            root, "Generate Navigator Walls"
        );

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(mapContent, false);

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;

        Vector2 mapSize = new Vector2(
            layout.Width * unitsPerCell,
            layout.Height * unitsPerCell
        );

        rootRect.sizeDelta = mapSize;

        UnityEditor.Undo.RecordObject(mapContent, "Resize Navigator Map");

        mapContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, mapSize.x
        );

        mapContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, mapSize.y
        );

        int wallCount = 0;

        // Vertical walls run along the UI Y axis.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x <= layout.Width; x++)
            {
                if (layout.GetVerticalEdge(x, y) != MazeEdgeType.Wall)
                    continue;

                Vector2 position = new Vector2(
                    x * unitsPerCell,
                    (y + 0.5f) * unitsPerCell
                );

                Vector2 size = new Vector2(
                    wallThickness,
                    unitsPerCell + wallThickness
                );

                CreateWall(rootRect, $"Wall_V_{x}_{y}", position, size);
                wallCount++;
            }
        }

        // Horizontal walls run along the UI X axis.
        for (int y = 0; y <= layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.GetHorizontalEdge(x, y) != MazeEdgeType.Wall)
                    continue;

                Vector2 position = new Vector2(
                    (x + 0.5f) * unitsPerCell,
                    y * unitsPerCell
                );

                Vector2 size = new Vector2(
                    unitsPerCell + wallThickness,
                    wallThickness
                );

                CreateWall(rootRect, $"Wall_H_{x}_{y}", position, size);
                wallCount++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );

        Debug.Log($"Generated {wallCount} Navigator walls.");
    }

    private void CreateWall(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        // Instantiate the UI prefab while preserving its prefab connection.
        GameObject wall = UnityEditor.PrefabUtility.InstantiatePrefab(
            wallPrefab, gameObject.scene
        ) as GameObject;

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            wall, "Create Navigator Wall"
        );

        wall.transform.SetParent(parent, false);
        wall.name = name;

        RectTransform rect = wall.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        UnityEditor.PrefabUtility
    .RecordPrefabInstancePropertyModifications(rect);
    }


    private void GenerateDoors()
    {
        if (Application.isPlaying)
            return;

        if (layout == null || !layout.IsConfigured ||
            mapContent == null || doorPrefab == null)
        {
            Debug.LogError("Assign the layout, MapContent, and DoorUI prefab.");
            return;
        }

        // Remove previously generated doors.
        Transform previous = mapContent.Find("Generated Doors");

        if (previous != null)
            UnityEditor.Undo.DestroyObjectImmediate(previous.gameObject);

        // Create a parent for all doors.
        GameObject root = new GameObject(
            "Generated Doors",
            typeof(RectTransform)
        );

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            root, "Generate Navigator Doors"
        );

        RectTransform rootRect = root.GetComponent<RectTransform>();

        rootRect.SetParent(mapContent, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;

        rootRect.sizeDelta = new Vector2(
            layout.Width * unitsPerCell,
            layout.Height * unitsPerCell
        );

        int doorCount = 0;

        // Vertical door edges.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x <= layout.Width; x++)
            {
                if (layout.GetVerticalEdge(x, y) != MazeEdgeType.Door)
                    continue;

                Vector2 position = new Vector2(
                    x * unitsPerCell,
                    (y + 0.5f) * unitsPerCell
                );

                CreateDoor(
                    rootRect,
                    $"Door_V_{x}_{y}",
                    position,
                    true,
                    x,
                    y
                );

                doorCount++;
            }
        }

        // Horizontal door edges.
        for (int y = 0; y <= layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.GetHorizontalEdge(x, y) != MazeEdgeType.Door)
                    continue;

                Vector2 position = new Vector2(
                    (x + 0.5f) * unitsPerCell,
                    y * unitsPerCell
                );

                CreateDoor(
                    rootRect,
                    $"Door_V_{x}_{y}",
                    position,
                    false,
                    x,
                    y
                );

                doorCount++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );

        Debug.Log($"Generated {doorCount} Navigator doors.");
    }


    private void CreateDoor(Transform parent,
        string name,
        Vector2 position,
        bool isVertical,
        int edgeX,
        int edgeY)
    {
        GameObject door = UnityEditor.PrefabUtility.InstantiatePrefab(
            doorPrefab, gameObject.scene
        ) as GameObject;

        if (door == null)
        {
            Debug.LogError("Could not instantiate DoorUI prefab.");
            return;
        }

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            door, "Create Navigator Door"
        );

        door.transform.SetParent(parent, false);
        door.name = name;

        // Position the entire door prefab.
        RectTransform rect = door.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * unitsPerCell;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        // Resize the line without resizing the circular node.
        RectTransform segment = door.transform
            .Find("Segment")
            .GetComponent<RectTransform>();

        float doorLength = Mathf.Max(1f, unitsPerCell - wallThickness);

        segment.anchorMin = new Vector2(0.5f, 0.5f);
        segment.anchorMax = new Vector2(0.5f, 0.5f);
        segment.pivot = new Vector2(0.5f, 0.5f);
        segment.anchoredPosition = Vector2.zero;

        segment.sizeDelta = isVertical
            ? new Vector2(wallThickness, doorLength)
            : new Vector2(doorLength, wallThickness);

        NavigatorDoorNode node =
    door.GetComponent<NavigatorDoorNode>();

        if (node == null)
        {
            Debug.LogError(
                $"DoorUI prefab is missing NavigatorDoorNode: {name}"
            );
            return;
        }

        UnityEditor.Undo.RecordObject(node, "Configure Door Identity");

        node.Configure(edgeX, edgeY, isVertical);

        UnityEditor.PrefabUtility
            .RecordPrefabInstancePropertyModifications(node);

        UnityEditor.PrefabUtility
    .RecordPrefabInstancePropertyModifications(rect);

        UnityEditor.PrefabUtility
            .RecordPrefabInstancePropertyModifications(segment);
    }


    private void GenerateLights()
    {
        if (Application.isPlaying)
            return;

        // Check that everything we need is assigned.
        if (layout == null || !layout.IsConfigured ||
            mapContent == null || lightPrefab == null)
        {
            Debug.LogError(
                "Assign the layout, MapContent, and LightUI prefab.",
                this
            );
            return;
        }

        if (lightPrefab.GetComponent<RectTransform>() == null ||
            lightPrefab.GetComponent<NavigatorLightNode>() == null)
        {
            Debug.LogError(
                "LightUI needs a RectTransform and NavigatorLightNode.",
                this
            );
            return;
        }

        // Remove the previously generated lights.
        Transform previous = mapContent.Find("Generated Lights");

        if (previous != null)
        {
            UnityEditor.Undo.DestroyObjectImmediate(
                previous.gameObject
            );
        }

        // Create a parent for the new lights.
        GameObject root = new GameObject(
            "Generated Lights",
            typeof(RectTransform)
        );

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            root, "Generate Navigator Lights"
        );

        RectTransform rootRect = root.GetComponent<RectTransform>();

        rootRect.SetParent(mapContent, false);

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.localScale = Vector3.one;

        rootRect.sizeDelta = new Vector2(
            layout.Width * unitsPerCell,
            layout.Height * unitsPerCell
        );

        int lightCount = 0;

        // Create one UI object for each light in the layout.
        foreach (MazeLightData light in layout.Lights)
        {
            Vector2 position = new Vector2(
                (light.cell.x + 0.5f) * unitsPerCell,
                (light.cell.y + 0.5f) * unitsPerCell
            );

            CreateLight(
                rootRect,
                $"Light_{light.id}",
                position,
                light.id
            );

            lightCount++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );

        Debug.Log(
            $"Generated {lightCount} Navigator lights.",
            this
        );
    }


    private void CreateLight(
        Transform parent,
        string name,
        Vector2 position,
        int lightId)
    {
        // Instantiate the prefab while keeping its prefab connection.
        GameObject light = UnityEditor.PrefabUtility.InstantiatePrefab(
            lightPrefab,
            gameObject.scene
        ) as GameObject;

        if (light == null)
        {
            Debug.LogError($"Could not create {name}.");
            return;
        }

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            light, "Create Navigator Light"
        );

        light.transform.SetParent(parent, false);
        light.name = name;

        // Position the UI element within the map.
        RectTransform rect = light.GetComponent<RectTransform>();

        UnityEditor.Undo.RecordObject(
            rect, "Position Navigator Light"
        );

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        // Preserve the prefab's original size.
        // We don't want to stretch the circular node.

        // Assign the light's identity.
        NavigatorLightNode node =
            light.GetComponent<NavigatorLightNode>();

        UnityEditor.Undo.RecordObject(
            node, "Configure Navigator Light"
        );

        node.Configure(lightId);

        // Save the modifications to this prefab instance.
        UnityEditor.PrefabUtility
            .RecordPrefabInstancePropertyModifications(rect);

        UnityEditor.PrefabUtility
            .RecordPrefabInstancePropertyModifications(node);
    }


    public void GenerateInEditor()
    {
        if (Application.isPlaying)
            return;

        // Validate all references before removing the existing map.
        if (layout == null || !layout.IsConfigured ||
            mapContent == null ||
            wallPrefab == null ||
            doorPrefab == null ||
            lightPrefab == null)
        {
            Debug.LogError(
                "Assign a valid layout, MapContent, and all three UI prefabs.",
                this
            );
            return;
        }

        // Remove any previously generated map.
        ClearInEditor();

        // Reuse our existing, tested generation methods.
        GenerateWalls();
        GenerateDoors();
        GenerateLights();

        Transform walls = mapContent.Find("Generated Walls");
        Transform doors = mapContent.Find("Generated Doors");
        Transform lights = mapContent.Find("Generated Lights");

        if (walls == null || doors == null || lights == null)
        {
            Debug.LogError("One or more map sections failed to generate.", this);
            return;
        }

        // Create one common parent for the map.
        GameObject root = new GameObject(
            "Generated Map",
            typeof(RectTransform)
        );

        UnityEditor.Undo.RegisterCreatedObjectUndo(
            root, "Generate Navigator Map"
        );

        RectTransform rootRect = root.GetComponent<RectTransform>();

        rootRect.SetParent(mapContent, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.localScale = Vector3.one;

        rootRect.sizeDelta = new Vector2(
            layout.Width * unitsPerCell,
            layout.Height * unitsPerCell
        );

        // Organize the generated sections under our common root.
        UnityEditor.Undo.SetTransformParent(
            walls, rootRect, "Group Navigator Walls"
        );

        UnityEditor.Undo.SetTransformParent(
            doors, rootRect, "Group Navigator Doors"
        );

        UnityEditor.Undo.SetTransformParent(
            lights, rootRect, "Group Navigator Lights"
        );

        walls.name = "Walls";
        doors.name = "Doors";
        lights.name = "Lights";

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );

        Debug.Log(
            $"Generated complete Navigator map for layout {layout.LayoutId}.",
            this
        );
    }


    public void ClearInEditor()
    {
        if (Application.isPlaying || mapContent == null)
            return;

        // Includes the old groups from our incremental development.
        string[] generatedNames =
        {
        "Generated Map",
        "Generated Walls",
        "Generated Doors",
        "Generated Lights"
    };

        foreach (string name in generatedNames)
        {
            Transform existing = mapContent.Find(name);

            if (existing != null)
            {
                UnityEditor.Undo.DestroyObjectImmediate(
                    existing.gameObject
                );
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );
    }


#endif
}
