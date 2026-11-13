using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Host only. Picks the next layout from the MazePrefabLibrary, swaps the
// Explorer maze, resets door/light state, and tells clients through
// MazeRefreshNetworkState (Preparing -> Ready).
[DisallowMultipleComponent]
public class MazeRefresh : MonoBehaviour
{
    // Raised after the new maze is active (same frame as the old one is
    // removed). EnemySpawn and CrateBarrelSpawn listen to this.
    public event System.Action<GameObject, Transform> LayoutChanged;

    // Raised before the old maze is deactivated. ResourceSpawn listens to this.
    public event System.Action<Transform> LayoutWillRefresh;

    public MazeLayoutData CurrentLayoutData { get; private set; }
    public MazePrefabEntry CurrentEntry { get; private set; }
    public int CurrentGeneration { get; private set; }
    public Transform CurrentRoot =>
        activeLayout != null ? activeLayout.transform : null;
    public bool IsRefreshing => refreshRoutine != null;
    public bool TimerPaused => timerPaused;
    public float SecondsRemaining => secondsRemaining;
    public float RefreshInterval => refreshInterval;

    [Header("Layouts")]
    [SerializeField] private MazePrefabLibrary library;

    [Header("Timer")]
    [SerializeField] private float refreshInterval = 60f;
    [SerializeField] private float secondsRemaining;
    [Tooltip("Set by ExpeditionController. The R key still refreshes while paused.")]
    [SerializeField] private bool timerPaused;

    [Header("Entrance Alignment")]
    [Tooltip("The MazeExit doorway. Each new maze is placed so its entrance " +
             "opening sits here, facing along the anchor's +Z. Leave empty to " +
             "spawn mazes at this object's origin.")]
    [SerializeField] private Transform entranceAnchor;

    [Tooltip("Put the maze floor at the bottom of the anchor's BoxCollider " +
             "instead of at the anchor's pivot.")]
    [SerializeField] private bool alignFloorToAnchorBottom = true;

    [Header("Explorer Bindings (optional)")]
    [Tooltip("Re-pointed at each new maze. Can live on any persistent object.")]
    [SerializeField] private MazeDoorNetworkBinding doorBinding;
    [SerializeField] private MazeLightNetworkBinding lightBinding;
    [SerializeField] private ExplorerPositionPublisher positionPublisher;

    // Shuffled play order. The first entry stays first in every cycle.
    private readonly List<MazePrefabEntry> cycle = new List<MazePrefabEntry>();

    private MazeRefreshNetworkState refreshState;
    private MazeNetworkState mazeState;
    private GameObject activeLayout;
    private int currentIndex = -1;
    private Coroutine refreshRoutine;

    private void Start()
    {
        NetworkManager network = NetworkManager.Singleton;

        // The Explorer host is responsible for maze selection.
        if (network == null || !network.IsServer)
        {
            Debug.Log("MazeRefresh disabled: not running on the host.", this);
            enabled = false;
            return;
        }

        refreshState = MazeRefreshNetworkState.Instance;
        mazeState = FindAnyObjectByType<MazeNetworkState>();

        if (refreshState == null || !refreshState.IsSpawned ||
            mazeState == null || !mazeState.IsSpawned)
        {
            Debug.LogError(
                "MazeRefreshNetworkState or MazeNetworkState is not ready. " +
                "Is GameSession loaded?",
                this
            );
            enabled = false;
            return;
        }

        if (library == null)
        {
            Debug.LogError("Assign the MazePrefabLibrary.", this);
            enabled = false;
            return;
        }

        BuildCycle();

        if (cycle.Count == 0)
        {
            Debug.LogError("MazeRefresh has no valid layouts.", this);
            enabled = false;
            return;
        }

        ShuffleFrom(0);
        refreshRoutine = StartCoroutine(RefreshRoutine(0));
    }

    private void BuildCycle()
    {
        var usedIds = new HashSet<int>();

        foreach (MazePrefabEntry entry in library.Layouts)
        {
            if (entry == null || entry.layoutData == null ||
                entry.explorerPrefab == null || entry.navigatorPrefab == null)
            {
                Debug.LogWarning("Skipping an incomplete maze library entry.", this);
                continue;
            }

            if (!entry.layoutData.IsConfigured)
            {
                Debug.LogWarning(
                    $"Skipping layout {entry.LayoutId}: MazeLayoutData is not configured.",
                    this
                );
                continue;
            }

            if (!usedIds.Add(entry.LayoutId))
            {
                Debug.LogWarning(
                    $"Skipping duplicate layout ID {entry.LayoutId} " +
                    $"('{entry.explorerPrefab.name}').",
                    this
                );
                continue;
            }

            GameObject prefab = entry.explorerPrefab;

            // Enemies need the baked NavMesh, but the refresh itself doesn't,
            // so a missing bake is a warning rather than a reason to skip.
            if (!prefab.TryGetComponent(out NavMeshSurface surface) ||
                !surface.enabled || surface.navMeshData == null)
            {
                Debug.LogWarning(
                    $"{prefab.name} has no baked NavMesh on its root NavMeshSurface. " +
                    "The maze will load, but enemies can't spawn or move in it. " +
                    "Open the prefab, select the root and press Bake on NavMeshSurface.",
                    this
                );
            }

            cycle.Add(entry);
        }
    }

    private void Update()
    {
        if (IsRefreshing || activeLayout == null)
            return;

        if (!timerPaused)
            secondsRemaining -= Time.deltaTime;

        if ((!timerPaused && secondsRemaining <= 0f) ||
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
        {
            RefreshNow();
        }
    }

    public void SetTimerPaused(bool paused)
    {
        if (timerPaused == paused)
            return;

        timerPaused = paused;
        Debug.Log(
            $"Maze refresh timer {(paused ? "paused" : "resumed")} " +
            $"({secondsRemaining:0.0}s left).",
            this
        );
    }

    public void RestartTimer()
    {
        secondsRemaining = refreshInterval;
    }

    // World pose for a layout root so its entrance opening lands on the
    // anchor, with the maze extending along the anchor's +Z.
    public bool TryGetAlignedPose(
        MazeLayoutData layout, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;

        if (entranceAnchor == null || layout == null ||
            !layout.TryGetEntranceMouth(out Vector3 edgeLocal, out Vector3 inwardLocal))
            return false;

        Vector3 anchorPoint = entranceAnchor.position;

        if (alignFloorToAnchorBottom &&
            entranceAnchor.TryGetComponent(out BoxCollider anchorBox))
        {
            anchorPoint = entranceAnchor.TransformPoint(
                anchorBox.center - new Vector3(0f, anchorBox.size.y * 0.5f, 0f));
        }

        Vector3 into = Vector3.ProjectOnPlane(entranceAnchor.forward, Vector3.up);

        if (into.sqrMagnitude < 0.0001f)
            into = Vector3.forward;

        float yaw = Vector3.SignedAngle(inwardLocal, into.normalized, Vector3.up);
        rotation = Quaternion.Euler(0f, yaw, 0f);
        position = anchorPoint - rotation * edgeLocal;
        return true;
    }

    [ContextMenu("Refresh Now")]
    public void RefreshNow()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (!Application.isPlaying || !isActiveAndEnabled ||
            IsRefreshing || cycle.Count == 0)
            return;

        int nextIndex = currentIndex + 1;

        if (nextIndex >= cycle.Count)
        {
            // Keep the first layout of the game at the start of every cycle.
            ShuffleFrom(1);
            nextIndex = 0;
        }

        refreshRoutine = StartCoroutine(RefreshRoutine(nextIndex));
    }

    private IEnumerator RefreshRoutine(int index)
    {
        MazePrefabEntry entry = cycle[index];
        string previousName = CurrentEntry != null
            ? CurrentEntry.explorerPrefab.name
            : "(none)";

        // A) Prepare: clients lock input and show the overlay.
        int generation = refreshState.BeginRefresh(entry.LayoutId);

        if (generation < 0)
        {
            refreshRoutine = null;
            yield break;
        }

        // B) Cleanup. Everything from here to LayoutChanged runs in one
        // frame: EnemySpawn counts the old enemies in LayoutChanged, and
        // they're only destroyed at the end of this frame.
        GameObject previousLayout = activeLayout;

        if (previousLayout != null)
        {
            LayoutWillRefresh?.Invoke(previousLayout.transform);
            previousLayout.SetActive(false);

            // Detach so nothing searching under this object finds the old
            // maze before Destroy runs at the end of the frame.
            previousLayout.transform.SetParent(null, true);
            Destroy(previousLayout);
        }

        // C) Activate.
        // Spawn straight at the aligned pose (not moved afterwards) so the
        // baked NavMesh registers in the right place on OnEnable.
        if (TryGetAlignedPose(entry.layoutData, out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            activeLayout = Instantiate(
                entry.explorerPrefab, spawnPosition, spawnRotation, transform);
        }
        else
        {
            if (entranceAnchor != null)
            {
                Debug.LogWarning(
                    $"Layout {entry.LayoutId} has no entrance on the maze edge; " +
                    "spawning at the MazeRefresh origin instead.",
                    this
                );
            }

            activeLayout = Instantiate(entry.explorerPrefab, transform, false);
        }

        activeLayout.name = entry.explorerPrefab.name;
        activeLayout.SetActive(true);

        currentIndex = index;
        CurrentEntry = entry;
        CurrentLayoutData = entry.layoutData;
        CurrentGeneration = generation;

        // D) Reset door/light state for the new layout and rebind.
        mazeState.ResetForLayout(entry.layoutData, generation);

        Transform root = activeLayout.transform;

        if (doorBinding != null)
            doorBinding.SetRoot(root);

        if (lightBinding != null)
            lightBinding.SetRoot(root);

        if (positionPublisher != null)
            positionPublisher.SetRoot(root, entry.layoutData.CellSize);

        // E) Spawn (enemy/resource scripts react to this event).
        LayoutChanged?.Invoke(entry.explorerPrefab, root);

        Debug.Log(
            $"Maze refresh generation {generation}: '{previousName}' -> " +
            $"'{entry.explorerPrefab.name}' (layout {entry.LayoutId}).",
            this
        );

        WarnAboutStaticMazes(root);

        // Let the door/light changes go out in an earlier network tick
        // than Ready, so clients always have them when they see Ready.
        yield return WaitForNextNetworkTick();

        // F) Resume.
        refreshState.CompleteRefresh();
        secondsRemaining = refreshInterval;
        refreshRoutine = null;
    }

    private bool warnedAboutStaticMazes;

    // A maze placed directly in the scene (e.g. inside the ExplorerMaze tool
    // prefab) overlaps the refreshed one and never changes. Point it out once.
    private void WarnAboutStaticMazes(Transform activeRoot)
    {
        if (warnedAboutStaticMazes)
            return;

        var offenders = new HashSet<string>();

        foreach (ExplorerDoor door in FindObjectsByType<ExplorerDoor>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (door.transform.IsChildOf(activeRoot) ||
                door.gameObject.scene != gameObject.scene)
                continue;

            // Report the maze root (the object with the NavMeshSurface),
            // or the door's top-level object if there isn't one.
            NavMeshSurface mazeRoot = door.GetComponentInParent<NavMeshSurface>();
            Transform top = mazeRoot != null ? mazeRoot.transform : door.transform.root;

            offenders.Add(GetPath(top));
        }

        if (offenders.Count == 0)
            return;

        warnedAboutStaticMazes = true;
        Debug.LogWarning(
            "MazeRefresh: found maze geometry in the scene that isn't the refreshed maze, " +
            "so it will overlap it and never change: " + string.Join(", ", offenders) +
            ". Delete or disable it (e.g. the maze nested inside the ExplorerMaze tool object).",
            this
        );
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    // Select the MazeRefresh object to preview where each layout will sit
    // relative to the entrance anchor.
    private void OnDrawGizmosSelected()
    {
        if (library == null || library.Layouts == null)
            return;

        foreach (MazePrefabEntry entry in library.Layouts)
        {
            MazeLayoutData data = entry != null ? entry.layoutData : null;

            if (data == null || !data.IsConfigured ||
                !TryGetAlignedPose(data, out Vector3 position, out Quaternion rotation))
                continue;

            float w = data.Width * data.CellSize;
            float d = data.Height * data.CellSize;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(w * 0.5f, 0f, d * 0.5f), new Vector3(w, 0.05f, d));

            if (data.TryGetEntranceOpening(out Vector3 edge, out Vector3 inward))
            {
                Vector3 mouth = edge - inward * data.EntranceCorridorLength;
                Gizmos.DrawLine(edge, mouth);
                Gizmos.DrawSphere(mouth, 0.25f);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private static IEnumerator WaitForNextNetworkTick()
    {
        NetworkManager network = NetworkManager.Singleton;

        if (network == null || network.NetworkTickSystem == null)
        {
            yield return null;
            yield return null;
            yield break;
        }

        int startTick = network.NetworkTickSystem.LocalTime.Tick;

        // Two ticks: the tick in progress may already have been sent.
        while (network != null && network.NetworkTickSystem != null &&
               network.NetworkTickSystem.LocalTime.Tick < startTick + 2)
        {
            yield return null;
        }
    }

    private void ShuffleFrom(int firstIndex)
    {
        for (int i = cycle.Count - 1; i > firstIndex; i--)
        {
            int otherIndex = Random.Range(firstIndex, i + 1);
            MazePrefabEntry entry = cycle[i];
            cycle[i] = cycle[otherIndex];
            cycle[otherIndex] = entry;
        }
    }
}
