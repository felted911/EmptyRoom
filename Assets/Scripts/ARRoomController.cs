using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cinemachine;

public class ARRoomController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private ARRoomManager roomManager;
    [SerializeField] private MaterialManager materialManager;
    [SerializeField] private UserInteractionHandler userInteractionHandler;

    [Header("UI Elements")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button destroyRoomButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider postProcessingIntensitySlider;

    [Header("Debug Options")]
    [SerializeField] private bool enableDebugText = true;
    [SerializeField] private bool showPerformanceStats = true;

    [Header("Camera Control")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private float cameraZoomSpeed = 1.0f;

    [Header("Post Processing")]
    [SerializeField] private Volume postProcessingVolume;
    private Bloom bloomEffect;
    private Vignette vignetteEffect;

    void Start()
    {
        // Find components if not assigned
        if (roomManager == null) roomManager = FindObjectOfType<ARRoomManager>();
        if (materialManager == null) materialManager = FindObjectOfType<MaterialManager>();
        if (userInteractionHandler == null) userInteractionHandler = FindObjectOfType<UserInteractionHandler>();
        if (virtualCamera == null) virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (postProcessingVolume == null) postProcessingVolume = FindObjectOfType<Volume>();

        // Setup UI
        SetupUI();

        // Subscribe to events
        if (userInteractionHandler != null)
            userInteractionHandler.OnUserMovedFarFromRoom += HandleUserMovedFar;

        // Initialize post processing effects
        InitializePostProcessing();
    }

    void OnDestroy()
    {
        if (userInteractionHandler != null)
            userInteractionHandler.OnUserMovedFarFromRoom -= HandleUserMovedFar;
    }

    private void InitializePostProcessing()
    {
        if (postProcessingVolume != null && postProcessingVolume.profile != null)
        {
            postProcessingVolume.profile.TryGet<Bloom>(out bloomEffect);
            postProcessingVolume.profile.TryGet<Vignette>(out vignetteEffect);
        }
    }

    private void SetupUI()
    {
        // Setup create room button
        if (createRoomButton != null)
        {
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            createRoomButton.gameObject.SetActive(true);
        }

        // Setup destroy room button
        if (destroyRoomButton != null)
        {
            destroyRoomButton.onClick.AddListener(OnDestroyRoomClicked);
            destroyRoomButton.gameObject.SetActive(false);
        }

        // Setup post processing intensity slider
        if (postProcessingIntensitySlider != null)
        {
            postProcessingIntensitySlider.onValueChanged.AddListener(OnPostProcessingIntensityChanged);
            postProcessingIntensitySlider.value = 0.2f; // Default intensity
        }

        // Initialize status text
        UpdateStatusText("Ready to create AR Room");
    }

    private void OnCreateRoomClicked()
    {
        if (roomManager != null)
        {
            roomManager.TryCreateRoom();
            UpdateStatusText("Scanning for floor...");

            // Update button visibility
            if (createRoomButton != null) createRoomButton.gameObject.SetActive(false);
            if (destroyRoomButton != null) destroyRoomButton.gameObject.SetActive(true);
        }
    }

    private void OnDestroyRoomClicked()
    {
        if (roomManager != null)
        {
            roomManager.DestroyRoom();
            UpdateStatusText("Room destroyed");

            // Update button visibility
            if (createRoomButton != null) createRoomButton.gameObject.SetActive(true);
            if (destroyRoomButton != null) destroyRoomButton.gameObject.SetActive(false);

            // Reset camera if needed
            if (virtualCamera != null)
            {
                virtualCamera.Priority = 0;
            }
        }
    }

    private void OnPostProcessingIntensityChanged(float value)
    {
        if (bloomEffect != null)
        {
            bloomEffect.intensity.value = value;
            bloomEffect.threshold.value = 1.0f - value;
        }

        if (vignetteEffect != null)
        {
            vignetteEffect.intensity.value = value * 0.5f;
        }

        UpdateStatusText($"Post processing intensity: {value:F2}");
    }

    private void HandleUserMovedFar(float distance)
    {
        UpdateStatusText($"Warning: Moved {distance:F1}m from room center");

        // Update virtual camera to follow user
        if (virtualCamera != null && distance > 3.0f)
        {
            virtualCamera.Priority = 20;
            // Optional: Trigger camera transition
        }
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null && enableDebugText)
        {
            statusText.text = message;
            Debug.Log($"Status: {message}");
        }
    }

    void Update()
    {
        // Update status based on room manager state
        if (roomManager != null && statusText != null && enableDebugText)
        {
            if (roomManager.IsRoomCreated())
            {
                Vector3 roomCenter = roomManager.GetRoomCenter();

                if (showPerformanceStats)
                {
                    // Show performance stats
                    float fps = 1.0f / Time.deltaTime;
                    UpdateStatusText($"Room at: {roomCenter}\nFPS: {fps:F1}\nUser: {(userInteractionHandler?.IsUserInitialized() ?? false)}");
                }
                else
                {
                    UpdateStatusText($"Room created at: {roomCenter}");
                }
            }
        }

        // Optional: Add camera zoom control
        if (virtualCamera != null)
        {
            // Example: Handle pinch zoom or other camera controls
            if (Input.touchCount == 2)
            {
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
                float currentMagnitude = (touch0.position - touch1.position).magnitude;

                float difference = currentMagnitude - prevMagnitude;

                // Adjust camera zoom based on pinch
                AdjustCameraZoom(difference * cameraZoomSpeed * Time.deltaTime);
            }
        }
    }

    private void AdjustCameraZoom(float zoomDelta)
    {
        if (virtualCamera != null)
        {
            var lens = virtualCamera.m_Lens;
            lens.FieldOfView = Mathf.Clamp(lens.FieldOfView - zoomDelta, 20f, 90f);
            virtualCamera.m_Lens = lens;
        }
    }

    // Android-specific method for optimizing performance
    public void OptimizeForAndroid()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            // Reduce post processing intensity for better performance
            if (postProcessingIntensitySlider != null)
            {
                postProcessingIntensitySlider.value = 0.1f;
            }

            // Disable non-essential features
            showPerformanceStats = false;

            // Set camera to lower quality settings
            if (virtualCamera != null)
            {
                virtualCamera.m_Lens.FieldOfView = 60f;
            }
        }
    }

    // Method to switch between indoor and outdoor modes
    public void SetEnvironmentMode(bool isIndoor)
    {
        if (roomManager != null)
        {
            roomManager.SwitchPostProcessingProfile(isIndoor);
        }

        UpdateStatusText($"Environment: {(isIndoor ? "Indoor" : "Outdoor")}");
    }

    // Diagnostic method for debugging
    public void RunDiagnostics()
    {
        if (userInteractionHandler != null)
        {
            userInteractionHandler.DiagnosePerformanceIssues();
        }

        // Also run material manager diagnostics if needed
        if (materialManager != null && Application.platform == RuntimePlatform.Android)
        {
            materialManager.OptimizeForAndroid();
        }
    }
}