using System;
using System.Collections.Generic;
using UnityEngine;

public enum MazeEdgeType : byte
{
    Open,
    Wall,
    Door
}

public enum MazeDirection : byte
{
    North,
    East,
    South,
    West
}

[Serializable]
public struct MazeLightData
{
    public int id;
    public string name;
    public Vector2Int cell;
    public int controlCode;
}

[CreateAssetMenu(fileName = "MazeLayout", menuName = "Mind the Gap/Maze Layout")]
public class MazeLayoutData : ScriptableObject
{
    [Header("Layout Information")]
    [SerializeField] private int layoutId = 1;
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;
    [SerializeField] private float cellSize = 2f;

    // Each shared edge is stored exactly once.
    [SerializeField, HideInInspector] private MazeEdgeType[] verticalEdges;
    [SerializeField, HideInInspector] private MazeEdgeType[] horizontalEdges;

    [Header("Imported Objects")]
    [SerializeField] private MazeLightData[] lights = Array.Empty<MazeLightData>();
    [SerializeField] private bool hasExplorerSpawn;
    [SerializeField] private Vector2Int explorerSpawnCell;

    [Header("Spawn Locations")]
    [SerializeField]
    private Vector2Int[] enemySpawnCells =
    Array.Empty<Vector2Int>();

    [SerializeField]
    private Vector2Int[] resourceSpawnCells =
        Array.Empty<Vector2Int>();

    [Header("Maze Entrance")]
    [SerializeField] private bool hasEntrance;
    [SerializeField] private Vector2Int entranceCell;

    [Tooltip("Length of the walled corridor built outside the entrance, in cells. " +
             "The corridor mouth is what lines up with MazeExit. Not touched by " +
             "re-importing from Tiled. Regenerate the maze prefab after changing it.")]
    [SerializeField, Min(0)] private int entranceCorridorCells = 1;

    public bool HasEntrance => hasEntrance;
    public Vector2Int EntranceCell => entranceCell;
    public int EntranceCorridorCells => Mathf.Max(0, entranceCorridorCells);
    public float EntranceCorridorLength => EntranceCorridorCells * cellSize;

    // Middle of the corridor's outer end (or the entrance edge itself when
    // there is no corridor), at floor level, in layout space.
    public bool TryGetEntranceMouth(out Vector3 mouthLocal, out Vector3 inwardLocal)
    {
        if (!TryGetEntranceOpening(out Vector3 edge, out inwardLocal))
        {
            mouthLocal = Vector3.zero;
            return false;
        }

        mouthLocal = edge - inwardLocal * EntranceCorridorLength;
        return true;
    }

    public void SetEntranceCell(Vector2Int cell)
    {
        entranceCell = cell;
        hasEntrance = true;
    }

    public int LayoutId => layoutId;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public IReadOnlyList<MazeLightData> Lights => lights ?? Array.Empty<MazeLightData>();
    public IReadOnlyList<Vector2Int> EnemySpawnCells =>
    enemySpawnCells ?? Array.Empty<Vector2Int>();

    public IReadOnlyList<Vector2Int> ResourceSpawnCells =>
        resourceSpawnCells ?? Array.Empty<Vector2Int>();

    public bool IsConfigured =>
        width > 0 && height > 0 && cellSize > 0f &&
        verticalEdges != null && horizontalEdges != null &&
        verticalEdges.Length == (width + 1) * height &&
        horizontalEdges.Length == width * (height + 1);



    public MazeEdgeType GetVerticalEdge(int x, int y)
    {
        EnsureConfigured();
        if (x < 0 || x > width || y < 0 || y >= height)
            throw new ArgumentOutOfRangeException(nameof(x),
                $"Invalid vertical edge ({x}, {y})");

        return verticalEdges[y * (width + 1) + x];
    }

    public MazeEdgeType GetHorizontalEdge(int x, int y)
    {
        EnsureConfigured();
        if (x < 0 || x >= width || y < 0 || y > height)
            throw new ArgumentOutOfRangeException(nameof(x),
                $"Invalid horizontal edge ({x}, {y})");

        return horizontalEdges[y * width + x];
    }

    public MazeEdgeType GetEdge(int cellX, int cellY, MazeDirection direction)
    {
        EnsureConfigured();
        if (cellX < 0 || cellX >= width || cellY < 0 || cellY >= height)
            throw new ArgumentOutOfRangeException(nameof(cellX),
                $"Invalid cell ({cellX}, {cellY})");

        switch (direction)
        {
            case MazeDirection.North: return GetHorizontalEdge(cellX, cellY + 1);
            case MazeDirection.East: return GetVerticalEdge(cellX + 1, cellY);
            case MazeDirection.South: return GetHorizontalEdge(cellX, cellY);
            case MazeDirection.West: return GetVerticalEdge(cellX, cellY);
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    // Finds the outer edge of the entrance cell that leads out of the maze.
    // edgeCenterLocal: middle of that edge at floor level, in layout space.
    // inwardLocal: unit direction from the edge into the maze.
    // Prefers a perimeter edge that isn't a wall (corner cells have two).
    public bool TryGetEntranceOpening(out Vector3 edgeCenterLocal, out Vector3 inwardLocal)
    {
        edgeCenterLocal = Vector3.zero;
        inwardLocal = Vector3.forward;

        if (!hasEntrance || !IsConfigured)
            return false;

        int x = entranceCell.x;
        int y = entranceCell.y;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        MazeDirection? chosen = null;
        MazeDirection[] order =
        {
            MazeDirection.North, MazeDirection.East,
            MazeDirection.South, MazeDirection.West
        };

        foreach (MazeDirection direction in order)
        {
            bool onPerimeter =
                (direction == MazeDirection.North && y == height - 1) ||
                (direction == MazeDirection.South && y == 0) ||
                (direction == MazeDirection.East && x == width - 1) ||
                (direction == MazeDirection.West && x == 0);

            if (!onPerimeter)
                continue;

            if (chosen == null)
                chosen = direction;

            if (GetEdge(x, y, direction) != MazeEdgeType.Wall)
            {
                chosen = direction;
                break;
            }
        }

        if (chosen == null)
            return false; // Entrance is not on the outside of the maze.

        float cx = (x + 0.5f) * cellSize;
        float cy = (y + 0.5f) * cellSize;

        switch (chosen.Value)
        {
            case MazeDirection.North:
                edgeCenterLocal = new Vector3(cx, 0f, height * cellSize);
                inwardLocal = Vector3.back;
                break;
            case MazeDirection.South:
                edgeCenterLocal = new Vector3(cx, 0f, 0f);
                inwardLocal = Vector3.forward;
                break;
            case MazeDirection.East:
                edgeCenterLocal = new Vector3(width * cellSize, 0f, cy);
                inwardLocal = Vector3.left;
                break;
            default:
                edgeCenterLocal = new Vector3(0f, 0f, cy);
                inwardLocal = Vector3.right;
                break;
        }

        return true;
    }

    public Vector3 GetCellCenterLocal(int x, int y)
    {
        EnsureConfigured();
        if (x < 0 || x >= width || y < 0 || y >= height)
            throw new ArgumentOutOfRangeException(nameof(x));

        return new Vector3((x + 0.5f) * cellSize, 0f,
                           (y + 0.5f) * cellSize);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("MazeLayoutData is not configured.");
    }

#if UNITY_EDITOR
    // Called only by the Tiled importer after it has validated all incoming data.

    public void SetImportedData(
        int id,
        int newWidth,
        int newHeight,
        float newCellSize,
        MazeEdgeType[] newVertical,
        MazeEdgeType[] newHorizontal,
        MazeLightData[] newLights,
        Vector2Int[] newEnemySpawnCells,
        Vector2Int[] newResourceSpawnCells)
    {
        layoutId = id;
        width = newWidth;
        height = newHeight;
        cellSize = newCellSize;

        verticalEdges = (MazeEdgeType[])newVertical.Clone();
        horizontalEdges = (MazeEdgeType[])newHorizontal.Clone();
        lights = (MazeLightData[])newLights.Clone();

        enemySpawnCells =
            (Vector2Int[])newEnemySpawnCells.Clone();

        resourceSpawnCells =
            (Vector2Int[])newResourceSpawnCells.Clone();

    }



    // Legacy helper retained only so the old CreateTestMazeLayout editor script
    // still compiles. Use the Tiled importer for new layouts.
    public void InitializeTestLayout()
    {
        layoutId = 1;
        width = 5;
        height = 5;
        cellSize = 2f;
        verticalEdges = new MazeEdgeType[(width + 1) * height];
        horizontalEdges = new MazeEdgeType[width * (height + 1)];
        lights = Array.Empty<MazeLightData>();
        enemySpawnCells = Array.Empty<Vector2Int>();
        resourceSpawnCells = Array.Empty<Vector2Int>();
        hasEntrance = false;
        entranceCell = Vector2Int.zero;

        for (int y = 0; y < height; y++)
        {
            verticalEdges[y * (width + 1)] = MazeEdgeType.Wall;
            verticalEdges[y * (width + 1) + width] = MazeEdgeType.Wall;
            verticalEdges[y * (width + 1) + 2] =
                y == 2 ? MazeEdgeType.Door : MazeEdgeType.Wall;
        }

        for (int x = 0; x < width; x++)
        {
            horizontalEdges[x] = MazeEdgeType.Wall;
            horizontalEdges[height * width + x] = MazeEdgeType.Wall;
        }

        horizontalEdges[2 * width] = MazeEdgeType.Wall;
        horizontalEdges[3 * width + 3] = MazeEdgeType.Wall;
        horizontalEdges[width + 4] = MazeEdgeType.Wall;
    }
#endif
}
