using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;
using Unity.Burst;
using Cinemachine;
using UnityEngine.Profiling;
using System.Collections.Generic;

public class ARRoomManager : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    
    [Header("Room Configuration")]
    [SerializeField] private float roomWidth = 2.44f; // 8 feet in meters
    [SerializeField] private float roomHeight = 2.44f; // 8 feet in meters
    [SerializeField] private float roomDepth = 2.44f; // 8 feet in meters
    
    [Header("Room Elements")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject ceilingPrefab;
    [SerializeField] private GameObject wallPrefab;
    
    [Header("Materials")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;
    [SerializeField] private Material wallMaterial;
    
    [Header("Post Processing")]
    [SerializeField] private Volume postProcessingVolume;
    [SerializeField] private VolumeProfile indoorProfile;
    [SerializeField] private VolumeProfile outdoorProfile;
    
    [Header("Android Specific")]
    [SerializeField] private bool useAndroidOptimizations = true;
    [SerializeField] private int androidTargetFrameRate = 60;
    
    // Room components
    private GameObject floor;
    private GameObject ceiling;
    private GameObject[] walls = new GameObject[4];
    private bool roomCreated = false;
    private Vector3 roomCenter;
    private Bloom bloomEffect;
    private Vignette vignetteEffect;
    private bool isIndoor = true;
    
    void Start()
    {
        // Android-specific settings
        if (Application.platform == RuntimePlatform.Android && useAndroidOptimizations)
        {
            Application.targetFrameRate = androidTargetFrameRate;
            QualitySettings.vSyncCount = 0;
        }
        
        // Ensure we have references to required components
        if (planeManager == null) planeManager = FindObjectOfType<ARPlaneManager>();
        if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();
        if (arCamera == null) arCamera = Camera.main;
        if (virtualCamera == null) virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        
        // Initialize post processing
        if (postProcessingVolume == null) postProcessingVolume = FindObjectOfType<Volume>();
        SetupPostProcessing();
        
        // Subscribe to plane detection events
        planeManager.planesChanged += OnPlanesChanged;
    }
    
    void OnDestroy()
    {
        if (planeManager != null)
            planeManager.planesChanged -= OnPlanesChanged;
    }
    
    private void SetupPostProcessing()
    {
        if (postProcessingVolume != null && postProcessingVolume.profile != null)
        {
            if (postProcessingVolume.profile.TryGet<Bloom>(out bloomEffect))
            {
                bloomEffect.intensity.value = 0.2f;
                bloomEffect.threshold.value = 0.9f;
                bloomEffect.active = true;
            }
            
            if (postProcessingVolume.profile.TryGet<Vignette>(out vignetteEffect))
            {
                vignetteEffect.intensity.value = 0.2f;
                vignetteEffect.smoothness.value = 0.4f;
                vignetteEffect.active = true;
            }
        }
    }
    
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Only create the room once
        if (roomCreated) return;
        
        // Look for a horizontal plane (floor)
        foreach (ARPlane newPlane in args.added)
        {
            if (newPlane.alignment == PlaneAlignment.HorizontalUp)
            {
                // Found a horizontal plane, create the room
                CreateRoom(newPlane);
                break;
            }
        }
    }
    
    private void CreateRoom(ARPlane floorPlane)
    {
        // Get the center of the detected floor plane
        roomCenter = floorPlane.center;
        
        // Create room elements
        CreateFloor(roomCenter);
        CreateCeiling(roomCenter);
        CreateWalls(roomCenter);
        
        roomCreated = true;
        
        // Update virtual camera position
        UpdateCameraPosition();
        
        // Disable plane detection to save performance
        planeManager.enabled = false;
        
        Debug.Log("AR Room created at position: " + roomCenter);
    }
    
    private void UpdateCameraPosition()
    {
        if (virtualCamera != null)
        {
            virtualCamera.LookAt = null;
            virtualCamera.Follow = null;
            virtualCamera.transform.position = arCamera.transform.position;
            virtualCamera.transform.rotation = arCamera.transform.rotation;
        }
    }
    
    private void CreateFloor(Vector3 center)
    {
        if (floorPrefab != null)
        {
            floor = Instantiate(floorPrefab, center, Quaternion.identity);
            floor.transform.localScale = new Vector3(roomWidth, 0.1f, roomDepth);
            
            if (floorMaterial != null)
                floor.GetComponent<Renderer>().material = floorMaterial;
        }
        else
        {
            // Create a basic plane if no prefab is provided
            GameObject floorPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorPlane.transform.position = center;
            floorPlane.transform.localScale = new Vector3(roomWidth / 10f, 1f, roomDepth / 10f);
            floor = floorPlane;
            
            if (floorMaterial != null)
                floor.GetComponent<Renderer>().material = floorMaterial;
        }
    }
    
    private void CreateCeiling(Vector3 center)
    {
        Vector3 ceilingPosition = center + Vector3.up * roomHeight;
        
        if (ceilingPrefab != null)
        {
            ceiling = Instantiate(ceilingPrefab, ceilingPosition, Quaternion.identity);
            ceiling.transform.localScale = new Vector3(roomWidth, 0.1f, roomDepth);
            
            if (ceilingMaterial != null)
                ceiling.GetComponent<Renderer>().material = ceilingMaterial;
        }
        else
        {
            // Create a basic plane if no prefab is provided
            GameObject ceilingPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceilingPlane.transform.position = ceilingPosition;
            ceilingPlane.transform.localScale = new Vector3(roomWidth / 10f, 1f, roomDepth / 10f);
            ceilingPlane.transform.Rotate(180f, 0f, 0f); // Flip to face down
            ceiling = ceilingPlane;
            
            if (ceilingMaterial != null)
                ceiling.GetComponent<Renderer>().material = ceilingMaterial;
        }
    }
    
    private void CreateWalls(Vector3 center)
    {
        // Create four walls
        float halfWidth = roomWidth / 2f;
        float halfDepth = roomDepth / 2f;
        float halfHeight = roomHeight / 2f;
        
        // Front wall
        walls[0] = CreateWall(center + Vector3.forward * halfDepth, Quaternion.identity, "Front Wall");
        
        // Right wall
        walls[1] = CreateWall(center + Vector3.right * halfWidth, Quaternion.Euler(0, 90, 0), "Right Wall");
        
        // Back wall
        walls[2] = CreateWall(center + Vector3.back * halfDepth, Quaternion.Euler(0, 180, 0), "Back Wall");
        
        // Left wall
        walls[3] = CreateWall(center + Vector3.left * halfWidth, Quaternion.Euler(0, 270, 0), "Left Wall");
    }
    
    private GameObject CreateWall(Vector3 position, Quaternion rotation, string name)
    {
        GameObject wall;
        
        if (wallPrefab != null)
        {
            wall = Instantiate(wallPrefab, position + Vector3.up * (roomHeight / 2f), rotation);
            wall.transform.localScale = new Vector3(roomWidth, roomHeight, 0.1f);
            
            if (wallMaterial != null)
                wall.GetComponent<Renderer>().material = wallMaterial;
        }
        else
        {
            // Create a basic cube if no prefab is provided
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = position + Vector3.up * (roomHeight / 2f);
            wall.transform.rotation = rotation;
            wall.transform.localScale = new Vector3(roomWidth, roomHeight, 0.1f);
            
            if (wallMaterial != null)
                wall.GetComponent<Renderer>().material = wallMaterial;
        }
        
        wall.name = name;
        return wall;
    }
    
    // New method for switching post processing profiles
    public void SwitchPostProcessingProfile(bool isIndoor)
    {
        this.isIndoor = isIndoor;
        
        if (postProcessingVolume != null)
        {
            if (isIndoor && indoorProfile != null)
            {
                postProcessingVolume.profile = indoorProfile;
            }
            else if (!isIndoor && outdoorProfile != null)
            {
                postProcessingVolume.profile = outdoorProfile;
            }
        }
    }
    
    // Optional: Method to manually trigger room creation
    public void TryCreateRoom()
    {
        if (roomCreated) return;
        
        // Try to find a horizontal plane
        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane.alignment == PlaneAlignment.HorizontalUp)
            {
                CreateRoom(plane);
                break;
            }
        }
    }
    
    // Optional: Method to destroy the room
    public void DestroyRoom()
    {
        if (floor != null) Destroy(floor);
        if (ceiling != null) Destroy(ceiling);
        
        foreach (GameObject wall in walls)
        {
            if (wall != null) Destroy(wall);
        }
        
        roomCreated = false;
        
        // Re-enable plane detection
        planeManager.enabled = true;
    }
    
    // Helper method to check if room is created
    public bool IsRoomCreated()
    {
        return roomCreated;
    }
    
    // Get the center position of the room
    public Vector3 GetRoomCenter()
    {
        return roomCenter;
    }
}
