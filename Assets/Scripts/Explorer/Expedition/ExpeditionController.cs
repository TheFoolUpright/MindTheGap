using System;
using UnityEngine;
using UnityEngine.Events;

// When the maze refresh timer is allowed to count down.
public enum ExpeditionRefreshTimerMode
{
    // Timer pauses at base and runs while the Explorer is out in the maze.
    RunOnlyDuringExpedition,

    // Expeditions don't affect the timer.
    AlwaysRun
}

// Tracks whether the Explorer is at base or out on an expedition, and tells
// expedition-specific systems when that changes. TunnelPassage drives it;
// other systems subscribe to the events (C# or the UnityEvents below).
[DisallowMultipleComponent]
public class ExpeditionController : MonoBehaviour
{
    public static ExpeditionController Instance { get; private set; }

    public bool IsOnExpedition { get; private set; }
    public int ExpeditionCount { get; private set; }

    // Time.time when the current/last expedition started.
    public float ExpeditionStartTime { get; private set; }
    public float ExpeditionDuration =>
        IsOnExpedition ? Time.time - ExpeditionStartTime : 0f;

    public event Action ExpeditionStarted;
    public event Action ExpeditionEnded;

    [Header("Start State")]
    [Tooltip("Tick if the Explorer spawns inside the maze instead of at base.")]
    [SerializeField] private bool startOnExpedition;

    [Header("Maze Refresh Timer")]
    [SerializeField] private MazeRefresh mazeRefresh;
    [SerializeField] private ExpeditionRefreshTimerMode refreshTimer =
        ExpeditionRefreshTimerMode.RunOnlyDuringExpedition;

    [Tooltip("Restart the countdown from the full interval whenever the timer resumes.")]
    [SerializeField] private bool restartTimerOnResume;

    [Header("Inspector Hooks")]
    [SerializeField] private UnityEvent onExpeditionStarted = new UnityEvent();
    [SerializeField] private UnityEvent onExpeditionEnded = new UnityEvent();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one ExpeditionController in the scene.", this);
            return;
        }

        Instance = this;

        if (mazeRefresh == null)
            mazeRefresh = FindAnyObjectByType<MazeRefresh>();

        IsOnExpedition = startOnExpedition;

        if (IsOnExpedition)
            ExpeditionStartTime = Time.time;
    }

    private void Start()
    {
        ApplyRefreshTimer(isResume: false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Called when the Explorer passes from base into the maze.
    [ContextMenu("Begin Expedition")]
    public void BeginExpedition()
    {
        if (IsOnExpedition)
            return;

        IsOnExpedition = true;
        ExpeditionCount++;
        ExpeditionStartTime = Time.time;

        Debug.Log($"[Expedition] #{ExpeditionCount} started.", this);

        ApplyRefreshTimer(isResume: true);
        ExpeditionStarted?.Invoke();
        onExpeditionStarted.Invoke();
    }

    // Called when the Explorer passes from the maze back into base.
    [ContextMenu("End Expedition")]
    public void EndExpedition()
    {
        if (!IsOnExpedition)
            return;

        float duration = ExpeditionDuration;
        IsOnExpedition = false;

        Debug.Log(
            $"[Expedition] #{ExpeditionCount} ended after {duration:0.0}s.",
            this
        );

        ApplyRefreshTimer(isResume: true);
        ExpeditionEnded?.Invoke();
        onExpeditionEnded.Invoke();
    }

    private void ApplyRefreshTimer(bool isResume)
    {
        if (mazeRefresh == null)
            return;

        bool paused =
            refreshTimer == ExpeditionRefreshTimerMode.RunOnlyDuringExpedition &&
            !IsOnExpedition;

        bool wasPaused = mazeRefresh.TimerPaused;
        mazeRefresh.SetTimerPaused(paused);

        if (isResume && restartTimerOnResume && wasPaused && !paused)
            mazeRefresh.RestartTimer();
    }
}
