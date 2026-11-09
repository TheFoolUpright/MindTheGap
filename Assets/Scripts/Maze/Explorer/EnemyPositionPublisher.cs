using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MazeRefresh), typeof(EnemySpawn))]
public class EnemyPositionPublisher : MonoBehaviour
{
    [SerializeField] private float publishInterval = 0.1f;
    [SerializeField] private float minimumMovement = 0.05f;
    [SerializeField] private float minimumTurnDegrees = 2f;

    private readonly List<(int id, Transform root)> activeEnemies = new();
    private readonly List<NetworkEnemyPose> poses = new();
    private MazeRefresh mazeRefresh;
    private EnemySpawn enemySpawn;
    private MazeNetworkState mazeState;
    private float nextPublishTime;

    private void Awake()
    {
        mazeRefresh = GetComponent<MazeRefresh>();
        enemySpawn = GetComponent<EnemySpawn>();
    }

    private void Update()
    {
        Transform mazeRoot = mazeRefresh.CurrentRoot;
        MazeLayoutData layout = mazeRefresh.CurrentLayoutData;
        if (mazeRoot == null || layout == null || Time.unscaledTime < nextPublishTime)
            return;

        nextPublishTime = Time.unscaledTime + publishInterval;
        if (mazeState == null || !mazeState.IsSpawned)
            mazeState = FindAnyObjectByType<MazeNetworkState>();

        if (mazeState == null || !mazeState.IsSpawned || !mazeState.IsServer)
            return;

        enemySpawn.CollectActiveEnemies(activeEnemies);
        poses.Clear();

        foreach (var (id, root) in activeEnemies)
        {
            Vector3 localPosition = mazeRoot.InverseTransformPoint(root.position);
            Vector2 cellPosition = new Vector2(localPosition.x, localPosition.z) / layout.CellSize;
            Vector3 localForward = mazeRoot.InverseTransformDirection(root.forward);
            float facing = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;

            poses.Add(new NetworkEnemyPose
            {
                Id = id,
                Generation = mazeRefresh.CurrentGeneration,
                CellPosition = cellPosition,
                FacingDegrees = facing
            });
        }

        mazeState.PublishEnemyPoses(mazeRefresh.CurrentGeneration, poses, 
            minimumMovement / layout.CellSize, minimumTurnDegrees);}

    private void OnEnable()
    {
        nextPublishTime = 0f;
    }

    private void OnDisable()
    {
        if (mazeState == null || mazeRefresh == null)
            return;

        poses.Clear();
        mazeState.PublishEnemyPoses(mazeRefresh.CurrentGeneration, poses, 0f, 0f);
    }
}
