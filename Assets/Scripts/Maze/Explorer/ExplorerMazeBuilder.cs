using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Editor-only geometry authoring tool. It NEVER creates maze geometry in Play Mode.
// The generated root can be saved as a prefab with the existing custom Inspector.
public class ExplorerMazeBuilder : MonoBehaviour
{
    [Header("Imported Tiled layout")]
    [SerializeField] private MazeLayoutData layout;

    [Header("Reusable prefabs (unit-sized roots)")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private GameObject lightPrefab;

    [Header("Geometry (1 Unity unit = 1 metre)")]
    [SerializeField, Min(0.1f)] private float wallHeight = 3f;
    [SerializeField, Min(0.01f)] private float wallThickness = 0.2f;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.2f;
    [SerializeField, Min(0f)] private float lightOffsetFromCeiling = 0.3f;

    public MazeLayoutData Layout => layout;
    public Transform GeneratedRoot => transform.Find("Generated Maze");

#if UNITY_EDITOR
    public void GenerateInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Generate the maze outside Play Mode.", this);
            return;
        }

        if (layout == null || !layout.IsConfigured)
        {
            Debug.LogError("Assign a valid imported MazeLayoutData asset.", this);
            return;
        }

        if (floorPrefab == null || wallPrefab == null || doorPrefab == null || lightPrefab == null)
        {
            Debug.LogError("Assign Floor, Wall, Door and Light prefabs before generating.", this);
            return; // Do not destroy previously generated maze when prefab assignments are incomplete.
        }

        if (transform.lossyScale != Vector3.one)
            Debug.LogWarning("Keep the ExplorerMaze parent at scale (1,1,1) for correct maze sizes.", this);

        ClearInEditor();

        var root = new GameObject("Generated Maze");
        Undo.RegisterCreatedObjectUndo(root, "Generate Explorer Maze");
        root.transform.SetParent(transform, false);

        Transform floors = CreateGroup("Floors", root.transform);
        Transform walls = CreateGroup("Walls", root.transform);
        Transform doors = CreateGroup("Doors", root.transform);
        Transform lights = CreateGroup("Lights", root.transform);

        CreateFloor(floors);

        int wallCount = 0;
        int doorCount = 0;
        float cell = layout.CellSize;
        float centerHeight = wallHeight * 0.5f;

        // Vertical edges: along world Z. Prefabs are authored lengthwise along local X.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x <= layout.Width; x++)
            {
                MazeEdgeType edge = layout.GetVerticalEdge(x, y);
                if (edge == MazeEdgeType.Open) continue;

                Vector3 position = new Vector3(x * cell, centerHeight, (y + 0.5f) * cell);
                CreateEdge(edge, $"{edge}_Vertical_{x}_{y}", position,
                    Quaternion.Euler(0f, 90f, 0f), cell, walls, doors);

                if (edge == MazeEdgeType.Wall) wallCount++; else doorCount++;
            }
        }

        // Horizontal edges: along world X.
        for (int y = 0; y <= layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                MazeEdgeType edge = layout.GetHorizontalEdge(x, y);
                if (edge == MazeEdgeType.Open) continue;

                Vector3 position = new Vector3((x + 0.5f) * cell, centerHeight, y * cell);
                CreateEdge(edge, $"{edge}_Horizontal_{x}_{y}", position,
                    Quaternion.identity, cell, walls, doors);

                if (edge == MazeEdgeType.Wall) wallCount++; else doorCount++;
            }
        }

        foreach (MazeLightData light in layout.Lights)
        {
            Vector3 position = layout.GetCellCenterLocal(light.cell.x, light.cell.y);
            position.y = wallHeight - lightOffsetFromCeiling;
            SpawnPrefab(lightPrefab, lights, $"Light_{light.id}_Code_{light.controlCode}",
                position, Quaternion.identity, Vector3.one, false);
        }

        // A single NavMeshSurface gathers the completed floor/wall geometry.
        // Do not bake here; use the NavMeshSurface Inspector after saving the maze.
        var surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"Editor maze generated: {wallCount} walls, {doorCount} doors, " +
                  $"{layout.Lights.Count} lights. Bake the NavMesh AFTER generation.", this);
    }

    public void ClearInEditor()
    {
        if (Application.isPlaying) return;
        Transform existing = GeneratedRoot;
        if (existing == null) return;
        Undo.DestroyObjectImmediate(existing.gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private static Transform CreateGroup(string name, Transform parent)
    {
        var group = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(group, $"Create {name} group");
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private void CreateFloor(Transform parent)
    {
        float width = layout.Width * layout.CellSize;
        float depth = layout.Height * layout.CellSize;
        Vector3 position = new Vector3(width / 2f, -floorThickness / 2f, depth / 2f);

        SpawnPrefab(floorPrefab, parent, "Floor", position, Quaternion.identity,
            new Vector3(width, floorThickness, depth), true);
    }

    private void CreateEdge(MazeEdgeType edge, string name, Vector3 position,
                            Quaternion rotation, float cellSize,
                            Transform walls, Transform doors)
    {
        bool door = edge == MazeEdgeType.Door;
        GameObject prefab = door ? doorPrefab : wallPrefab;
        Transform parent = door ? doors : walls;

        float length = door
            ? Mathf.Max(0.1f, cellSize - wallThickness)
            : cellSize + wallThickness; // Retain corner-overlap fix.

        // Author wall and door prototype meshes with local X as the long axis.
        SpawnPrefab(prefab, parent, name, position, rotation,
            new Vector3(length, wallHeight, wallThickness), !door);
    }

    private static GameObject SpawnPrefab(GameObject prefab, Transform parent,
                                         string name, Vector3 position,
                                         Quaternion rotation, Vector3 scale,
                                         bool isStatic)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
            throw new System.InvalidOperationException($"Could not instantiate prefab: {prefab.name}");

        Undo.RegisterCreatedObjectUndo(instance, $"Create {name}");
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation;
        instance.transform.localScale = scale;
        instance.isStatic = isStatic;
        return instance;
    }
#endif
}
