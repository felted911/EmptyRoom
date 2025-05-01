using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Profiling;
using System;
using System.Text;

public class UserInteractionHandler : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private ARRoomManager roomManager;
    [SerializeField] private Camera arCamera;

    [Header("User Position Tracking")]
    [SerializeField] private bool trackUserPosition = true;
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Android Logging")]
    [SerializeField] private bool useAndroidLogcat = true;
    [SerializeField] private string logTag = "ARRoomApp";

    [Header("Performance Monitoring")]
    [SerializeField] private bool enablePerformanceMonitoring = true;
    [SerializeField] private float performanceUpdateInterval = 2.0f;

    private Vector3 initialUserPosition;
    private bool isInitialized = false;
    private float updateTimer = 0f;
    private float performanceTimer = 0f;
    private StringBuilder logBuilder = new StringBuilder();

    // Performance metrics
    private int frameCount = 0;
    private float deltaTimeAccumulator = 0f;
    private float memoryUsage = 0f;

    // Event for other components to subscribe to
    public event Action<float> OnUserMovedFarFromRoom;

    void Start()
    {
        // Find components if not assigned
        if (arSession == null) arSession = FindObjectOfType<ARSession>();
        if (roomManager == null) roomManager = FindObjectOfType<ARRoomManager>();
        if (arCamera == null) arCamera = Camera.main;

        // Subscribe to session state change events
        ARSession.stateChanged += OnARSessionStateChanged;

        // Initialize Android logging
        if (useAndroidLogcat && Application.platform == RuntimePlatform.Android)
        {
            InitializeAndroidLogging();
        }

        LogMessage("UserInteractionHandler initialized");
    }

    void OnDestroy()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;

        if (useAndroidLogcat && Application.platform == RuntimePlatform.Android)
        {
            CleanupAndroidLogging();
        }
    }

    void Update()
    {
        if (!isInitialized || !trackUserPosition) return;

        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            UpdateUserPosition();
            updateTimer = 0f;
        }

        // Performance monitoring
        if (enablePerformanceMonitoring)
        {
            UpdatePerformanceMetrics();
        }
    }

    private void InitializeAndroidLogging()
    {
        // Setup Android LogCat integration
        LogMessage("Initializing Android LogCat");
        Debug.LogFormat("[{0}] AR Room App Started", logTag);
    }

    private void CleanupAndroidLogging()
    {
        LogMessage("Cleaning up Android LogCat");
    }

    private void LogMessage(string message)
    {
        if (useAndroidLogcat && Application.platform == RuntimePlatform.Android)
        {
            Debug.LogFormat("[{0}] {1}", logTag, message);
        }
        else
        {
            Debug.Log(message);
        }
    }

    private void LogError(string error)
    {
        if (useAndroidLogcat && Application.platform == RuntimePlatform.Android)
        {
            Debug.LogErrorFormat("[{0}] {1}", logTag, error);
        }
        else
        {
            Debug.LogError(error);
        }
    }

    private void UpdatePerformanceMetrics()
    {
        frameCount++;
        deltaTimeAccumulator += Time.deltaTime;
        performanceTimer += Time.deltaTime;

        if (performanceTimer >= performanceUpdateInterval)
        {
            // Calculate FPS
            float fps = frameCount / deltaTimeAccumulator;

            // Get memory usage
            memoryUsage = Profiler.GetTotalAllocatedMemoryLong() / 1048576f; // Convert to MB

            // Log performance metrics
            logBuilder.Clear();
            logBuilder.AppendFormat("FPS: {0:F1}, ", fps);
            logBuilder.AppendFormat("Memory: {0:F2} MB", memoryUsage);

            LogMessage($"Performance: {logBuilder.ToString()}");

            // Reset counters
            frameCount = 0;
            deltaTimeAccumulator = 0f;
            performanceTimer = 0f;
        }
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        switch (args.state)
        {
            case ARSessionState.SessionInitializing:
                LogMessage("AR Session Initializing...");
                break;

            case ARSessionState.SessionTracking:
                if (!isInitialized)
                {
                    InitializeUserPosition();
                }
                LogMessage("AR Session Tracking Started");
                break;

            case ARSessionState.None:
            case ARSessionState.Unsupported:
                LogError("AR Session failed to start or is unsupported");
                break;
        }
    }

    private void InitializeUserPosition()
    {
        if (arCamera != null)
        {
            initialUserPosition = arCamera.transform.position;
            isInitialized = true;
            LogMessage($"User position initialized at: {initialUserPosition}");
        }
    }

    private void UpdateUserPosition()
    {
        if (arCamera == null) return;

        Vector3 currentPosition = arCamera.transform.position;
        Vector3 positionDelta = currentPosition - initialUserPosition;

        // Log significant position changes
        if (positionDelta.magnitude > 0.1f)
        {
            LogMessage($"User moved by: {positionDelta.magnitude} meters");
        }

        // Check if user has moved too far from room center
        if (roomManager != null && roomManager.IsRoomCreated())
        {
            float distanceFromCenter = Vector3.Distance(currentPosition, roomManager.GetRoomCenter());

            // If user moves too far, you could trigger room recalibration or show a warning
            if (distanceFromCenter > 5f) // 5 meters threshold
            {
                LogMessage($"Warning: User moved far from room center. Distance: {distanceFromCenter}m");
                OnUserMovedFarFromRoom?.Invoke(distanceFromCenter);
            }
        }
    }

    // Public method to manually get user position
    public Vector3 GetUserPosition()
    {
        if (arCamera != null)
            return arCamera.transform.position;
        return Vector3.zero;
    }

    // Public method to check if user is initialized
    public bool IsUserInitialized()
    {
        return isInitialized;
    }

    // Public method to reset user position
    public void ResetUserPosition()
    {
        if (arCamera != null)
        {
            initialUserPosition = arCamera.transform.position;
            LogMessage($"User position reset to: {initialUserPosition}");
        }
    }

    // Android-specific error handling
    public void HandleAndroidError(Exception e)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            LogError($"Android Error: {e.Message}\nStackTrace: {e.StackTrace}");

            // Send error to analytics or crash reporting service
            // Analytics.LogCrash(e);
        }
    }

    // Performance diagnostics for Android
    public void DiagnosePerformanceIssues()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            StringBuilder diagnostic = new StringBuilder();
            diagnostic.AppendLine("Android Performance Diagnostics:");
            diagnostic.AppendLine($"Battery Level: {SystemInfo.batteryLevel}");
            diagnostic.AppendLine($"System Memory: {SystemInfo.systemMemorySize} MB");
            diagnostic.AppendLine($"Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
            diagnostic.AppendLine($"Device Model: {SystemInfo.deviceModel}");
            diagnostic.AppendLine($"Operating System: {SystemInfo.operatingSystem}");

            LogMessage(diagnostic.ToString());
        }
    }
}