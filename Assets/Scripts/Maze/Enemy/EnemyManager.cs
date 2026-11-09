using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyPatrol))]
public class EnemyManager : MonoBehaviour
{
    public enum State
    {
        SpawnHold,
        Patrol
    }

    [SerializeField] private State currentState;
    public State CurrentState => currentState;

    private const float SpawnHoldTime = 3f;

    private NavMeshAgent agent;
    private EnemyPatrol patrol;
    private float patrolStartTime;
    private bool initialized;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patrol = GetComponent<EnemyPatrol>();
    }

    public void Initialize(MazeLayoutData layout, Transform layoutRoot)
    {
        patrol.Initialize(agent, layout, layoutRoot);
        agent.ResetPath();
        agent.isStopped = true;
        currentState = State.SpawnHold;
        patrolStartTime = Time.time + SpawnHoldTime;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        if (currentState == State.SpawnHold)
        {
            if (Time.time < patrolStartTime)
                return;

            agent.isStopped = false;
            currentState = State.Patrol;
        }

        patrol.Tick();
    }
}
