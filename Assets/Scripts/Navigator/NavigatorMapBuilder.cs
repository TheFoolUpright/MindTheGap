using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Editor-bakes a 2D uGUI map from the SAME MazeLayoutData used by ExplorerMazeBuilder.
/// Does not generate UI objects at runtime. Generated objects remain in the scene
/// and can be saved as a prefab. All coordinates are relative to the maze's
/// southwest corner (Unity X east, Unity Z north).
/// </summary>
public class NavigatorMapBuilder : MonoBehaviour
{
    [Header("Same imported asset used by the Explorer")]
    [SerializeField] private MazeLayoutData layout;

    [Header("UI layout")]
    [SerializeField] private RectTransform mapContent;
    [SerializeField, Min(8f)] private float pixelsPerCell = 32f;
    [SerializeField, Min(1f)] private float lineThickness = 4f;
    [SerializeField, Min(8f)] private float nodeDiameter = 20f;

    [Header("UI prefab roots")]
    [SerializeField] private RectTransform wallPrefab;        // UI Image
    [SerializeField] private NavigatorMapNode doorPrefab;    // UI root + Segment + Node images
    [SerializeField] private NavigatorMapNode lightPrefab;   // UI root + Node image
    [SerializeField] private RectTransform explorerMarkerPrefab; // optional arrow Image

    [Header("Code information display")]
    [SerializeField] private NavigatorCodesPanel codesPanel;

    public MazeLayoutData Layout => layout;
    public RectTransform MapContent => mapContent;
    public RectTransform GeneratedRoot => mapContent != null
        ? mapContent.Find("Generated Map") as RectTransform : null;

    private void Start()
    {
        // Editor-baked map objects already exist; only the scene UI reference
        // is connected at runtime. This does not build or instantiate anything.
        if (GeneratedRoot == null) return;
        foreach (NavigatorMapNode node in GeneratedRoot.GetComponentsInChildren<NavigatorMapNode>(true))
            node.BindCodesPanel(codesPanel);
    }

    private Vector2 MapPosition(float cellX, float cellY)
    {
        // Screen UI Y increases upward, just like the Unity maze's local Z.
        return new Vector2(
            (cellX - layout.Width * 0.5f) * pixelsPerCell,
            (cellY - layout.Height * 0.5f) * pixelsPerCell);
    }

    /// <summary>
    /// Future network hook. The host will send position LOCAL TO THE MAZE ROOT,
    /// not global world coordinates. Yaw 0 degrees points north (+Unity Z).
    /// </summary>
    public void ShowExplorerPosition(Vector3 mazeLocalPosition, float yawDegrees)
    {
        if (layout == null || !layout.IsConfigured) return;
        RectTransform root = GeneratedRoot;
        if (root == null) return;
        Transform marker = root.Find("Markers/Explorer");
        if (marker == null) return;

        RectTransform markerRect = marker as RectTransform;
        markerRect.anchoredPosition = MapPosition(
            mazeLocalPosition.x / layout.CellSize,
            mazeLocalPosition.z / layout.CellSize);
        markerRect.localRotation = Quaternion.Euler(0f, 0f, -yawDegrees);
        markerRect.gameObject.SetActive(true);
    }

    public void HideExplorerPosition()
    {
        Transform marker = GeneratedRoot != null
            ? GeneratedRoot.Find("Markers/Explorer") : null;
        if (marker != null) marker.gameObject.SetActive(false);
    }

    /// <summary>Useful later when the host tells the Navigator a light changed state.</summary>
    public bool SetLightState(int lightId, NavigatorLightState state)
    {
        Transform lightGroup = GeneratedRoot != null
            ? GeneratedRoot.Find("Lights") : null;
        if (lightGroup == null) return false;
        foreach (NavigatorMapNode node in lightGroup.GetComponentsInChildren<NavigatorMapNode>(true))
        {
            if (node.LightId != lightId) continue;
            node.SetLightState(state);
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    public void GenerateInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Generate Navigator maps outside Play Mode.", this);
            return;
        }

        if (layout == null || !layout.IsConfigured || mapContent == null ||
            wallPrefab == null || doorPrefab == null || lightPrefab == null ||
            codesPanel == null)
        {
            Debug.LogError("Assign a configured MazeLayoutData, MapContent, " +
                           "Wall/Door/Light prefabs, and CodesPanel first.", this);
            return;
        }
        if (mapContent.GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("MapContent must be under a Unity UI Canvas.", this);
            return;
        }
        if (!doorPrefab.CanRenderDoor || !lightPrefab.CanRenderLight)
        {
            Debug.LogError("Assign Node and Segment Image references on the door prefab, " +
                           "and a Node Image on the light prefab.", this);
            return;
        }
        if (wallPrefab.GetComponent<Image>() == null)
        {
            Debug.LogError("WallPrefab must have a UI Image component on its root.", this);
            return;
        }
        if (lineThickness >= pixelsPerCell)
        {
            Debug.LogError("Line thickness must be smaller than pixels per cell.", this);
            return;
        }

        ClearInEditor();

        Vector2 size = new Vector2(layout.Width * pixelsPerCell,
                                   layout.Height * pixelsPerCell);
        // MapContent should NOT be under a LayoutGroup that drives its position.
        SetRect(mapContent, Vector2.zero, size);

        RectTransform root = NewRect("Generated Map", mapContent, size);
        RectTransform walls = NewRect("Walls", root, size);
        RectTransform doors = NewRect("Doors", root, size);
        RectTransform lights = NewRect("Lights", root, size);
        RectTransform markers = NewRect("Markers", root, size);

        int wallCount = 0;
        int doorCount = 0;

        // Vertical edges follow world Z / UI Y.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x <= layout.Width; x++)
            {
                MazeEdgeType edge = layout.GetVerticalEdge(x, y);
                if (edge == MazeEdgeType.Open) continue;

                string key = $"V_{x}_{y}";
                Vector2 point = MapPosition(x, y + 0.5f);
                if (edge == MazeEdgeType.Wall)
                {
                    SpawnWall(walls, "Wall_" + key, point,
                        new Vector2(lineThickness, pixelsPerCell + lineThickness));
                    wallCount++;
                }
                else
                {
                    SpawnDoor(doors, key, point, vertical: true);
                    doorCount++;
                }
            }
        }

        // Horizontal edges follow world X / UI X.
        for (int y = 0; y <= layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                MazeEdgeType edge = layout.GetHorizontalEdge(x, y);
                if (edge == MazeEdgeType.Open) continue;

                string key = $"H_{x}_{y}";
                Vector2 point = MapPosition(x + 0.5f, y);
                if (edge == MazeEdgeType.Wall)
                {
                    SpawnWall(walls, "Wall_" + key, point,
                        new Vector2(pixelsPerCell + lineThickness, lineThickness));
                    wallCount++;
                }
                else
                {
                    SpawnDoor(doors, key, point, vertical: false);
                    doorCount++;
                }
            }
        }

        foreach (MazeLightData light in layout.Lights)
        {
            NavigatorMapNode node = SpawnNode(lightPrefab, lights,
                $"Light_{light.id}",
                MapPosition(light.cell.x + 0.5f, light.cell.y + 0.5f));
            node.ConfigureLight(light.id, light.controlCode, codesPanel);
        }

        // A marker will become visible only once an actual Explorer position arrives.
        if (explorerMarkerPrefab != null)
        {
            RectTransform marker = SpawnPrefab(explorerMarkerPrefab.gameObject, markers)
                .GetComponent<RectTransform>();
            marker.name = "Explorer";
            SetRect(marker, Vector2.zero,
                new Vector2(nodeDiameter, nodeDiameter));
            marker.gameObject.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"Navigator map baked: {layout.Width}x{layout.Height} cells, " +
                  $"{wallCount} walls, {doorCount} doors, " +
                  $"{layout.Lights.Count} lights. No runtime UI creation.", this);
    }

    public void ClearInEditor()
    {
        if (Application.isPlaying || mapContent == null) return;
        RectTransform previous = GeneratedRoot;
        if (previous == null) return;
        Undo.DestroyObjectImmediate(previous.gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void SpawnWall(RectTransform parent, string name,
                           Vector2 position, Vector2 size)
    {
        RectTransform rect = SpawnPrefab(wallPrefab.gameObject, parent)
            .GetComponent<RectTransform>();
        rect.name = name;
        SetRect(rect, position, size);
        // Walls don't participate in pointer raycasts; only nodes do.
        rect.GetComponent<Image>().raycastTarget = false;
    }

    private void SpawnDoor(RectTransform parent, string key,
                           Vector2 position, bool vertical)
    {
        NavigatorMapNode node = SpawnNode(doorPrefab, parent, "Door_" + key, position);
        node.ConfigureDoor(key, vertical,
            Mathf.Max(1f, pixelsPerCell - lineThickness), lineThickness, codesPanel);
    }

    private NavigatorMapNode SpawnNode(NavigatorMapNode prefab, RectTransform parent,
                                        string name, Vector2 position)
    {
        NavigatorMapNode node = SpawnPrefab(prefab.gameObject, parent)
            .GetComponent<NavigatorMapNode>();
        node.name = name;
        SetRect((RectTransform)node.transform, position,
            new Vector2(nodeDiameter, nodeDiameter));
        return node;
    }

    private static GameObject SpawnPrefab(GameObject prefab, RectTransform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
            throw new System.InvalidOperationException("Could not instantiate prefab " + prefab.name);
        Undo.RegisterCreatedObjectUndo(instance, "Create Navigator UI element");
        return instance;
    }

    private static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetRect(rt, Vector2.zero, size);
        return rt;
    }

    private static void SetRect(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }
#endif
}
