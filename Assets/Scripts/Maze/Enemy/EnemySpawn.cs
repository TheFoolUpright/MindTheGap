using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawn : MonoBehaviour
{
    private class SpawnPoint
    {
        public Transform marker;
        public GameObject enemy;
    }

    [SerializeField] private MazeRefresh mazeRefresh;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject spawnPointPrefab;
    [SerializeField] private float firstSpawnDelay = 5f;

    /*
    [SerializeField] private Transform player;
    [SerializeField] private float minPlayerDistance = 4f;
    [SerializeField] private float maxPlayerDistance = 20f;
    [SerializeField] private float minEnemyDistance = 2f;
    [SerializeField] private float maxEnemyDistance = 16f;
    */

    private const int EnemyLimit = 4;
    private const float SpawnDelayIncrease = 5f;
    private const float NavMeshSearchRadius = 0.35f;

    private readonly List<SpawnPoint> points = new List<SpawnPoint>();
    private NavMeshAgent prefabAgent;
    private float spawnDelay;
    private float nextSpawnTime;

    private void Awake()
    {
        if (enemyPrefab != null)
        {
            prefabAgent = enemyPrefab.GetComponent<NavMeshAgent>();
            if (!enemyPrefab.TryGetComponent<EnemyManager>(out _))
            {
                Debug.LogError("Enemy prefab needs an EnemyManager on its root.", this);
                prefabAgent = null;
            }
        }

        spawnDelay = firstSpawnDelay;
        nextSpawnTime = Time.time + spawnDelay;
    }

    private void OnEnable()
    {
        if (mazeRefresh != null)
            mazeRefresh.LayoutChanged += SetLayout;
    }

    private void OnDisable()
    {
        if (mazeRefresh != null)
            mazeRefresh.LayoutChanged -= SetLayout;
    }

    private void Update()
    {
        if (points.Count == 0 || CountEnemies() >= EnemyLimit || Time.time < nextSpawnTime)
            return;

        if (!TrySpawnEnemy())
            nextSpawnTime = Time.time + 1f;
    }

    // SetLayout is called when the maze layout changes, and it updates the spawn points accordingly.
    // Also attempts to restore count of enemies that were present before the layout change.
    private void SetLayout(GameObject _, Transform layoutRoot)
    {
        int enemiesToRestore = CountEnemies();
        points.Clear();

        if (prefabAgent == null || spawnPointPrefab == null)
            return;

        MazeLayoutData data = mazeRefresh.CurrentLayoutData;

        if (data == null || !data.IsConfigured)
            return;

        var usedCells = new HashSet<Vector2Int>(); // Keep track of which cells have already been used to avoid duplicates

        foreach (Vector2Int cell in data.EnemySpawnCells)
        {
            if (cell.x < 0 || cell.x >= data.Width || cell.y < 0 || cell.y >= data.Height ||
                !usedCells.Add(cell))
                continue;

            Vector3 center = layoutRoot.TransformPoint(data.GetCellCenterLocal(cell.x, cell.y));
            GameObject marker = Instantiate(spawnPointPrefab, center, layoutRoot.rotation, layoutRoot);
            marker.name = $"EnemySpawnPoint ({cell.x}, {cell.y})";
            points.Add(new SpawnPoint { marker = marker.transform });
        }

        for (int i = 0; i < enemiesToRestore; i++)
        {
            if (!TrySpawnEnemy())
                break;
        }
    }

    private int CountEnemies()
    {
        int count = 0;
        foreach (SpawnPoint point in points)
        {
            // The previous maze is already inactive when LayoutChanged is raised.
            if (point.enemy != null && point.enemy.activeSelf)
                count++;
        }

        return count;
    }

    private bool TrySpawnEnemy()
    {
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = prefabAgent.agentTypeID,
            areaMask = prefabAgent.areaMask
        }; // Use the same agent type and area mask as the prefab to ensure valid spawn positions

        int firstIndex = Random.Range(0, points.Count);
        for (int offset = 0; offset < points.Count; offset++)
        {
            SpawnPoint point = points[(firstIndex + offset) % points.Count];
            if (point.enemy != null && point.enemy.activeSelf)
                continue;

            if (!NavMesh.SamplePosition(point.marker.position, out NavMeshHit hit,
                    NavMeshSearchRadius, filter))
                continue;

            // Uncomment this check and the distance fields and method when testing with a player!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // if (!PassesDistanceChecks(hit.position))
            //     continue;

            GameObject enemy = Instantiate(enemyPrefab, hit.position, point.marker.rotation, point.marker);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (!agent.isOnNavMesh)
            {
                Destroy(enemy);
                continue;
            }

            enemy.GetComponent<EnemyManager>().Initialize(
                mazeRefresh.CurrentLayoutData, mazeRefresh.CurrentRoot);
            point.enemy = enemy;

            spawnDelay += SpawnDelayIncrease;
            nextSpawnTime = Time.time + spawnDelay;
            Debug.Log($"Spawned {enemyPrefab.name} at {point.marker.name}.", this);
            return true;
        }

        return false;
    }

    /*
    private bool PassesDistanceChecks(Vector3 position)
    {
        if (player == null)
            return false;

        float playerDistance = Vector3.Distance(position, player.position);
        if (playerDistance < minPlayerDistance || playerDistance > maxPlayerDistance)
            return false;

        float nearestEnemyDistance = float.PositiveInfinity;
        foreach (SpawnPoint point in points)
        {
            if (point.enemy == null || !point.enemy.activeSelf)
                continue;

            float distance = Vector3.Distance(position, point.enemy.transform.position);
            nearestEnemyDistance = Mathf.Min(nearestEnemyDistance, distance);
        }

        // The first enemy has no other enemy to measure against.
        return float.IsPositiveInfinity(nearestEnemyDistance) ||
            (nearestEnemyDistance >= minEnemyDistance && nearestEnemyDistance <= maxEnemyDistance);
    }
    */
}
