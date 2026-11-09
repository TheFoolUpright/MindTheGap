using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrol : MonoBehaviour
{
    private const int MinRouteSteps = 3;
    private const int MaxRouteSteps = 6;
    private const float RetryDelay = 0.5f;
    private const float SampleRadius = 0.35f;

    // The offsets match the order of MazeDirection.
    private static readonly Vector2Int[] CellOffsets =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> steps = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, Vector2Int> parents =
        new Dictionary<Vector2Int, Vector2Int>();
    private readonly List<Vector2Int> candidates = new List<Vector2Int>();
    private readonly List<Vector2Int> preferred = new List<Vector2Int>();
    private readonly List<Vector2Int> route = new List<Vector2Int>();

    private MazeLayoutData layout;
    private Transform layoutRoot;
    private NavMeshAgent agent;
    private NavMeshPath path;
    private NavMeshQueryFilter filter;

    private int waypointIndex;
    private Vector3 waypoint;
    private bool hasWaypoint;
    private float waypointDeadline;
    private float nextAttemptTime;
    private bool includeNearbyCells;

    public void Initialize(NavMeshAgent navAgent, MazeLayoutData data, Transform root)
    {
        agent = navAgent;
        layout = data;
        layoutRoot = root;
        path = new NavMeshPath();
        filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        agent.autoRepath = false;
        route.Clear();
        hasWaypoint = false;
        includeNearbyCells = false;
        nextAttemptTime = Time.time;
    }

    public void Tick()
    {
        if (layoutRoot == null || Time.time < nextAttemptTime)
            return;

        if (hasWaypoint)
        {
            if (IsAtWaypoint())
            {
                hasWaypoint = false;
                waypointIndex++;
            }
            else
            {
                if (!agent.hasPath || agent.isPathStale ||
                    agent.pathStatus != NavMeshPathStatus.PathComplete ||
                    Time.time >= waypointDeadline)
                {
                    Retry();
                }

                return;
            }
        }

        if (waypointIndex >= route.Count && !BuildRoute())
        {
            Retry();
            return;
        }

        if (!StartWaypoint())
            Retry();
    }

    private bool BuildRoute()
    {
        Vector2Int start = WorldToCell(agent.nextPosition);
        if (!ContainsCell(start))
            return false;

        frontier.Clear();
        steps.Clear();
        parents.Clear();
        candidates.Clear();
        preferred.Clear();

        frontier.Enqueue(start);
        steps.Add(start, 0);

        while (frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();
            int distance = steps[cell] + 1;
            if (distance > MaxRouteSteps)
                continue;

            for (int i = 0; i < CellOffsets.Length; i++)
            {
                Vector2Int next = cell + CellOffsets[i];
                if (!ContainsCell(next) || steps.ContainsKey(next) ||
                    layout.GetEdge(cell.x, cell.y, (MazeDirection)i) != MazeEdgeType.Open)
                {
                    continue;
                }

                steps.Add(next, distance);
                parents.Add(next, cell);
                frontier.Enqueue(next);
                candidates.Add(next);

                if (distance >= MinRouteSteps)
                    preferred.Add(next);
            }
        }

        if (candidates.Count == 0)
            return false;

        List<Vector2Int> choices = candidates;
        if (!includeNearbyCells && preferred.Count > 0)
            choices = preferred;

        route.Clear();
        Vector2Int target = choices[Random.Range(0, choices.Count)];
        while (target != start)
        {
            route.Add(target);
            target = parents[target];
        }

        route.Add(start);
        route.Reverse();
        waypointIndex = 0;
        includeNearbyCells = false;
        return true;
    }

    private bool StartWaypoint()
    {
        Vector2Int cell = route[waypointIndex];
        Vector3 center = layoutRoot.TransformPoint(layout.GetCellCenterLocal(cell.x, cell.y));
        if (!NavMesh.SamplePosition(center, out NavMeshHit hit, SampleRadius, filter) ||
            WorldToCell(hit.position) != cell)
        {
            return false;
        }

        waypoint = hit.position;
        if (!IsAtWaypoint())
        {
            // Adjacent cell centers must have a direct connection on the baked surface.
            if (agent.Raycast(waypoint, out _) ||
                !agent.CalculatePath(waypoint, path) ||
                path.status != NavMeshPathStatus.PathComplete ||
                !agent.SetPath(path))
            {
                return false;
            }

            float distance = Vector3.Distance(agent.nextPosition, waypoint);
            waypointDeadline = Time.time + distance / Mathf.Max(agent.speed, 0.1f) + 3f;
        }

        hasWaypoint = true;
        return true;
    }

    private bool IsAtWaypoint()
    {
        Vector3 offset = waypoint - agent.nextPosition;
        offset.y = 0f;
        float tolerance = Mathf.Max(0.12f, agent.stoppingDistance + 0.02f);
        return offset.sqrMagnitude <= tolerance * tolerance;
    }

    private void Retry()
    {
        agent.ResetPath();
        route.Clear();
        hasWaypoint = false;
        includeNearbyCells = true;
        nextAttemptTime = Time.time + RetryDelay;
    }

    private Vector2Int WorldToCell(Vector3 position)
    {
        Vector3 local = layoutRoot.InverseTransformPoint(position);
        return new Vector2Int(
            Mathf.FloorToInt(local.x / layout.CellSize),
            Mathf.FloorToInt(local.z / layout.CellSize)
        );
    }

    private bool ContainsCell(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < layout.Width &&
               cell.y >= 0 && cell.y < layout.Height;
    }
}
