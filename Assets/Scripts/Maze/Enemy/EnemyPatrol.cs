using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPatrol : MonoBehaviour
{
    [SerializeField] private ExplorerMazeBuilder mazeBuilder;
    [SerializeField] private int minimumCellDistance = 2;
    [SerializeField] private float pauseDuration = 0.5f;
    [SerializeField] private float retryDelay = 1f;

    private const float CenterSampleRadius = 0.25f;
    private const float CenterTolerance = 0.01f;

    // Each offset matches the MazeLayoutData edge in the same direction.
    private static readonly (Vector2Int Offset, MazeDirection Direction)[] Directions =
    {
        (Vector2Int.up, MazeDirection.North),
        (Vector2Int.right, MazeDirection.East),
        (Vector2Int.down, MazeDirection.South),
        (Vector2Int.left, MazeDirection.West)
    };

    // All enemies on the active maze can reuse the same centers and connections.
    private static PatrolGrid sharedGrid;

    // BFS state is unique to each enemy, so they can plan different routes.
    private readonly List<Vector2Int> route = new List<Vector2Int>();
    // The BFS search finds all reachable cells, but only the ones at the configured
    private readonly List<Vector2Int> destinations = new List<Vector2Int>();
    // BFS state is cleared after each route is planned.
    private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
    private readonly Dictionary<Vector2Int, Vector2Int> previousCells =
        new Dictionary<Vector2Int, Vector2Int>();

    private NavMeshAgent agent;
    private PatrolGrid grid;
    private int waypointIndex;
    private float nextPlanTime;
    private bool layoutInitializationRequested;
    private bool warnedAboutRoute;

    private void Start()
    {
        if (!layoutInitializationRequested)
            InitializeForLayout();
    }

    // Call on all enemies before removing the old maze or its NavMesh.
    public void PrepareForLayoutChange()
    {
        layoutInitializationRequested = true;
        route.Clear();
        grid = null;
        sharedGrid = null;
    }

    // Call after the new maze and NavMesh are ready and this enemy is on the new surface.
    // Pass the new builder if the layout uses a different ExplorerMazeBuilder object.
    public bool InitializeForLayout(ExplorerMazeBuilder newMazeBuilder = null)
    {
        layoutInitializationRequested = true;
        route.Clear();
        grid = null;

        if (newMazeBuilder != null)
            mazeBuilder = newMazeBuilder;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogError("Enemy Patrol needs an enabled NavMesh Agent on the baked NavMesh.", this);
            return false;
        }

        if (mazeBuilder == null || mazeBuilder.Layout == null ||
            !mazeBuilder.Layout.IsConfigured || mazeBuilder.GeneratedRoot == null)
        {
            Debug.LogError("Assign the Explorer Maze Builder and generate the maze.", this);
            return false;
        }

        MazeLayoutData layout = mazeBuilder.Layout;
        Transform root = mazeBuilder.GeneratedRoot;
        if (sharedGrid == null || sharedGrid.Layout != layout || sharedGrid.Root != root ||
            sharedGrid.AgentTypeId != agent.agentTypeID || sharedGrid.AreaMask != agent.areaMask)
        {
            sharedGrid = new PatrolGrid(layout, root, agent.agentTypeID, agent.areaMask);
        }

        agent.ResetPath();
        Vector2Int start = sharedGrid.GetCell(agent.nextPosition);
        if (!CanReachCenter(sharedGrid, start))
        {
            Debug.LogError($"The center of starting cell {start} is not reachable on the NavMesh.", this);
            return false;
        }

        grid = sharedGrid;
        nextPlanTime = Time.time;
        warnedAboutRoute = false;
        return true;
    }

    private void Update()
    {
        if (grid == null || grid.Root == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (route.Count > 0)
        {
            FollowRoute();
            return;
        }

        if (Time.time < nextPlanTime || TryPlanRoute())
            return;

        nextPlanTime = Time.time + retryDelay;
        if (!warnedAboutRoute)
        {
            Debug.LogWarning("Enemy Patrol found no reachable destination at the configured distance.", this);
            warnedAboutRoute = true;
        }
    }

    // Breadth first search visits the nearest cells first. The first time a
    // cell is visited, its parent gives the shortest route in number of steps.
    private bool TryPlanRoute()
    {
        Vector2Int start = grid.GetCell(agent.nextPosition);
        if (!CanReachCenter(grid, start))
            return false;

        frontier.Clear();
        previousCells.Clear();
        destinations.Clear();

        // The start points to itself so it counts as already visited.
        previousCells.Add(start, start);
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();

            // Manhattan distance preserves the minimum distance setting from the old patrol.
            int distance = Mathf.Abs(cell.x - start.x) + Mathf.Abs(cell.y - start.y);
            if (cell != start && distance >= minimumCellDistance)
                destinations.Add(cell);

            foreach (Vector2Int neighbor in grid.Neighbors[cell.x, cell.y])
            {
                if (previousCells.ContainsKey(neighbor))
                    continue;

                previousCells.Add(neighbor, cell);
                frontier.Enqueue(neighbor);
            }
        }

        if (destinations.Count == 0)
            return false;

        // All candidates are already reachable. Choose one with equal probability.
        Vector2Int waypoint = destinations[Random.Range(0, destinations.Count)];

        // Follow parents from goal to start, then reverse the list for movement.
        route.Add(waypoint);
        while (waypoint != start)
        {
            waypoint = previousCells[waypoint];
            route.Add(waypoint);
        }

        route.Reverse();
        waypointIndex = 0;
        warnedAboutRoute = false;
        return true;
    }

    // Visit one center at a time, so the enemy turns after reaching the corner cell.
    private void FollowRoute()
    {
        Vector2Int cell = route[waypointIndex];
        Vector3 center = grid.Centers[cell.x, cell.y];
        Vector3 offset = center - agent.nextPosition;
        offset.y = 0f;

        if (offset.sqrMagnitude <= CenterTolerance * CenterTolerance)
        {
            waypointIndex++;
            if (waypointIndex == route.Count)
                EndRoute(pauseDuration);

            return;
        }

        // Recheck the active segment in case navigation changed after planning.
        if (agent.Raycast(center, out _))
        {
            EndRoute(retryDelay);
            return;
        }

        // Clamp the movement to the remaining distance so it cannot overshoot.
        Vector3 movement = Vector3.ClampMagnitude(offset, agent.speed * Time.deltaTime);
        agent.Move(movement);
    }

    private void EndRoute(float wait)
    {
        route.Clear();
        nextPlanTime = Time.time + wait;
    }

    private bool CanReachCenter(PatrolGrid patrolGrid, Vector2Int cell)
    {
        return patrolGrid.IsInside(cell) && patrolGrid.WalkableCenters[cell.x, cell.y] &&
               !agent.Raycast(patrolGrid.Centers[cell.x, cell.y], out _);
    }

    // Shared maze data. Each enemy keeps its own BFS and movement state.
    private sealed class PatrolGrid
    {
        public readonly MazeLayoutData Layout;
        public readonly Transform Root;
        public readonly int AgentTypeId;
        public readonly int AreaMask;

        // The indices [x, y] are grid column and row.
        // Vector3 stores world X, height Y, and world Z for each cell center.
        public readonly Vector3[,] Centers;

        // True means the center is on the baked surface within the sampling tolerance.
        public readonly bool[,] WalkableCenters;

        // Only four cardinal directions are considered. Each list stores legal neighbors.
        public readonly List<Vector2Int>[,] Neighbors;

        public PatrolGrid(MazeLayoutData layout, Transform root, int agentTypeId, int areaMask)
        {
            Layout = layout;
            Root = root;
            AgentTypeId = agentTypeId;
            AreaMask = areaMask;

            Centers = new Vector3[layout.Width, layout.Height];
            WalkableCenters = new bool[layout.Width, layout.Height];
            Neighbors = new List<Vector2Int>[layout.Width, layout.Height];

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = areaMask
            };

            CacheCenters(filter);
            CacheNeighbors(filter);
        }

        // Keep the exact horizontal center and take its height from the NavMesh.
        // A nearby sample off to the side does not make the center walkable.
        private void CacheCenters(NavMeshQueryFilter filter)
        {
            for (int x = 0; x < Layout.Width; x++)
            {
                for (int y = 0; y < Layout.Height; y++)
                {
                    Vector3 center = Root.TransformPoint(Layout.GetCellCenterLocal(x, y));
                    if (NavMesh.SamplePosition(center, out NavMeshHit hit, CenterSampleRadius, filter))
                    {
                        Vector2 drift = new Vector2(hit.position.x - center.x, hit.position.z - center.z);
                        if (drift.sqrMagnitude <= CenterTolerance * CenterTolerance)
                        {
                            center.y = hit.position.y;
                            WalkableCenters[x, y] = true;
                        }
                    }

                    Centers[x, y] = center;
                }
            }
        }

        // A connection needs an open side in the layout and a straight, walkable
        // line between both centers. Walls and doors are blocked for this patrol.
        private void CacheNeighbors(NavMeshQueryFilter filter)
        {
            for (int x = 0; x < Layout.Width; x++)
            {
                for (int y = 0; y < Layout.Height; y++)
                {
                    if (!WalkableCenters[x, y])
                        continue;

                    var neighbors = new List<Vector2Int>(4);
                    Neighbors[x, y] = neighbors;
                    Vector2Int cell = new Vector2Int(x, y);

                    foreach (var direction in Directions)
                    {
                        Vector2Int next = cell + direction.Offset;
                        if (!IsInside(next) || !WalkableCenters[next.x, next.y])
                            continue;

                        if (Layout.GetEdge(x, y, direction.Direction) != MazeEdgeType.Open)
                            continue;

                        if (!NavMesh.Raycast(Centers[x, y], Centers[next.x, next.y], out _, filter))
                            neighbors.Add(next);
                    }
                }
            }
        }

        // Local X and Z become a column and a row. Floor picks the containing cell.
        public Vector2Int GetCell(Vector3 worldPosition)
        {
            Vector3 local = Root.InverseTransformPoint(worldPosition);
            return new Vector2Int(Mathf.FloorToInt(local.x / Layout.CellSize),
                                  Mathf.FloorToInt(local.z / Layout.CellSize));
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Layout.Width && cell.y >= 0 && cell.y < Layout.Height;
        }
    }
}
