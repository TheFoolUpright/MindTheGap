
using System.Collections;
using UnityEngine;

public class ExplorerPositionPublisher : MonoBehaviour
{
    [Header("Explorer References")]
    [SerializeField] private Transform explorerHead;
    [SerializeField] private Transform generatedMazeRoot;

    [Header("Network Settings")]
    [SerializeField, Min(0.02f)]
    private float publishInterval = 0.1f;

    [SerializeField, Min(0f)]
    private float minimumMovement = 0.05f;

    private MazeNetworkState mazeNetworkState;

    private float nextPublishTime;
    private Vector2 lastPublishedPosition;
    private bool hasPublished;

    private void OnEnable()
    {
        StartCoroutine(FindNetworkState());
    }

    private IEnumerator FindNetworkState()
    {
        while (mazeNetworkState == null ||
               !mazeNetworkState.IsSpawned)
        {
            MazeNetworkState found =
                FindAnyObjectByType<MazeNetworkState>();

            if (found != null && found.IsSpawned)
            {
                mazeNetworkState = found;
                break;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private void Update()
    {
        if (explorerHead == null || generatedMazeRoot == null)
            return;

        if (mazeNetworkState == null ||
            !mazeNetworkState.IsSpawned ||
            !mazeNetworkState.IsServer)
            return;

        if (Time.unscaledTime < nextPublishTime)
            return;

        nextPublishTime = Time.unscaledTime + publishInterval;

        // Convert the headset's world position into
        // coordinates relative to the generated maze.
        Vector3 localPosition =
            generatedMazeRoot.InverseTransformPoint(
                explorerHead.position
            );

        // The map uses X and Z, not height (Y).
        Vector2 mapPosition = new Vector2(
            localPosition.x,
            localPosition.z
        );

        // Avoid sending insignificant movement.
        if (hasPublished &&
            Vector2.Distance(mapPosition, lastPublishedPosition)
                < minimumMovement)
        {
            return;
        }

        mazeNetworkState.PublishExplorerPosition(mapPosition);

        lastPublishedPosition = mapPosition;
        hasPublished = true;
    }
}
