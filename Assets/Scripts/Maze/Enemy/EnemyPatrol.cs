using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPatrol : MonoBehaviour
{
    [SerializeField] private ExplorerMazeBuilder mazeBuilder;

    // Same order as MazeDirection: North, East, South, West.
    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    }; // Directions to neighboring cells in the maze.
       // We are using Vector2Int and not Vector2, because we are dealing with discrete maze cells, not continuous positions.
       // We need not float, but integer coordinates to represent the maze grid:3...

    private readonly List<Vector2Int> exits = new List<Vector2Int>(4);

    private NavMeshAgent agent;
    private MazeLayoutData layout;
    private Vector2Int targetCell;
    private Vector2Int previousCell;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (mazeBuilder == null || mazeBuilder.Layout == null ||
            !mazeBuilder.Layout.IsConfigured || !agent.isOnNavMesh)
        {
            Debug.LogError("Assign a valid maze and place the enemy on its NavMesh.", this);
            enabled = false;
            return;
        }

        layout = mazeBuilder.Layout;

        Vector3 localPosition = mazeBuilder.transform.InverseTransformPoint(agent.nextPosition);

        targetCell = new Vector2Int(Mathf.FloorToInt(localPosition.x / layout.CellSize),
                                    Mathf.FloorToInt(localPosition.z / layout.CellSize)); // Convert world position to maze cell coordinates.

        if (!IsInsideMaze(targetCell) || agent.Raycast(GetCenter(targetCell), out _))
        {
            Debug.LogError("Place the enemy near the center of a walkable cell.", this);
            enabled = false;
            return;
        }

        previousCell = targetCell;
        agent.ResetPath();
    }

    private void Update()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        // Calculate the offset from the agent's current position to the center of the target cell.
        Vector3 offset = GetCenter(targetCell) - agent.nextPosition;
        // Ignore vertical offset to keep the agent on the same plane.
        offset.y = 0f;

        if (offset.sqrMagnitude <= 0.0001f)
        {
            ChooseNextCell();
            return;
        }

        agent.Move(Vector3.ClampMagnitude(offset, agent.speed * Time.deltaTime));
        // he likes to move it move it
    }

    private void ChooseNextCell()
    {
        exits.Clear(); // Clear the list of exits before finding new ones.

        // Check all four neighboring cells to see if they are reachable and add them to the list of exits.
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int neighbor = targetCell + directions[i];

            if (!IsInsideMaze(neighbor))
                continue;

            if (layout.GetEdge(targetCell.x, targetCell.y, (MazeDirection)i) != MazeEdgeType.Open)
                continue; // Check if the edge in the direction of the neighbor is open.

            if (!agent.Raycast(GetCenter(neighbor), out _))
                exits.Add(neighbor); // Add the neighbor to the list of exits if it is reachable by the agent (no obstacles in the way).
        }

        if (exits.Count == 0)
            return;

        if (exits.Count > 1)
            exits.Remove(previousCell);

        previousCell = targetCell;
        targetCell = exits[Random.Range(0, exits.Count)];
    }

    // finding Center of the cell in world space
    private Vector3 GetCenter(Vector2Int cell)
    {
        Vector3 localCenter = layout.GetCellCenterLocal(cell.x, cell.y);
        return mazeBuilder.transform.TransformPoint(localCenter);
    }

    private bool IsInsideMaze(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < layout.Width &&
               cell.y >= 0 && cell.y < layout.Height;
    }
}