using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Maze")] // Delete and move to EnemyManager when ready!!!
    [SerializeField] private ExplorerMazeBuilder mazeBuilder;

    [Header("Patrol")]
    [SerializeField] private float pauseDuration = 1f;
    [SerializeField] private float retryDelay = 1f;
    [SerializeField] private int destinationAttempts = 20;

    private NavMeshAgent agent;
    private NavMeshPath patrolPath;

    private bool isMoving;
    private float waitTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolPath = new NavMeshPath();
    }

    private void Start()
    {
        if (mazeBuilder == null || mazeBuilder.Layout == null || !mazeBuilder.Layout.IsConfigured)
        {
            Debug.LogError("Assign a maze builder with a valid layout.", this);
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Place the enemy on the baked NavMesh.", this);
        }
    }

    private void Update()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        if (isMoving)
        {
            UpdateMovement();
        }
        else
        {
            UpdateWaiting();
        }
    }

    private void UpdateMovement()
    {
        if (agent.pathPending)
            return;

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            StopAndWait(retryDelay);
            return;
        }

        bool reachedDestination = agent.remainingDistance <= agent.stoppingDistance;

        if (!agent.hasPath || reachedDestination)
            StopAndWait(pauseDuration);
    }

    private void UpdateWaiting()
    {
        waitTimer -= Time.deltaTime;

        if (waitTimer > 0f)
            return;

        if (!TryStartPatrol())
            waitTimer = retryDelay;
    }

    private bool TryStartPatrol()
    {
        for (int attempt = 0; attempt < destinationAttempts; attempt++) // Try to find a valid destination multiple times.
        {
            Vector3 destination = GetRandomCellPosition();

            if (!TryCreatePath(destination))
                continue;

            agent.isStopped = false;

            if (!agent.SetPath(patrolPath))
                continue;

            isMoving = true;
            return true;
        }

        return false;
    }

    private Vector3 GetRandomCellPosition() // Get a random position from the existing layout, in world space.
    {
        MazeLayoutData layout = mazeBuilder.Layout;

        // Randomly select a cell within the layout's dimensions.
        int column = Random.Range(0, layout.Width);
        int row = Random.Range(0, layout.Height);

        // Get the local position of the cell's center and convert it to world space.
        Vector3 localPosition = layout.GetCellCenterLocal(column, row);

        return mazeBuilder.transform.TransformPoint(localPosition);
    }

    private bool TryCreatePath(Vector3 destination)
    {
        // Find a nearby point on the NavMesh.
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 0.5f, agent.areaMask))
        {
            return false;
        }

        // Avoid choosing a destination too close to the enemy.
        float distance = Vector3.Distance(agent.nextPosition, hit.position);

        if (distance < 1f)
            return false;

        // Accept only routes that reach the destination completely.
        if (!agent.CalculatePath(hit.position, patrolPath))
            return false;

        return patrolPath.status == NavMeshPathStatus.PathComplete;
    }

    private void StopAndWait(float duration)
    {
        agent.ResetPath();

        isMoving = false;
        waitTimer = duration;
    }

    private void OnDisable()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();

        isMoving = false;
        waitTimer = 0f;
    }
}