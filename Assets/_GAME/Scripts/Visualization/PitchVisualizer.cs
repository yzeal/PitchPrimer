using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Modular visualization system for pitch data
// Supports both real-time and pre-rendered visualizations
// Designed for chorusing with dual-track display

[System.Serializable]
public class VisualizationSettings
{
    [Header("Cube Settings")]
    public GameObject cubePrefab;
    public Transform cubeParent;
    public float cubeSpacing = 0.8f;
    public Vector3 cubeScale = new Vector3(0.8f, 1f, 0.1f);
    
    [Header("Layout")]
    public int maxCubes = 30;
    public Vector3 trackOffset = Vector3.zero; // Offset für zweite Spur
    
    [Header("Focal Point System")]
    public Transform focalPointTransform; // ? NEW: Drag GameObject here!
    public bool showFocalIndicator = true;
    public GameObject focalIndicatorPrefab; // Optional visual marker
    
    [Header("Native Track States")]
    public float playedBrightness = 1.0f; // Full brightness for played cubes
    public float currentBrightness = 1.2f; // Extra bright for focal cube  
    public float futureBrightness = 0.3f; // Dim for future cubes
    public float playedAlpha = 0.9f;
    public float currentAlpha = 1.0f;
    public float futureAlpha = 0.4f;
    
    [Header("Pitch Mapping")]
    public float pitchScaleMultiplier = 1.5f;
    public float minFrequency = 80f;
    public float maxFrequency = 800f;
    public float silenceCubeHeight = 0.05f;
    
    [Header("Colors")]
    public Color silenceColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
    public bool useHSVColorMapping = true;
    public float saturation = 0.8f;
    public float brightness = 1f;
    
    [Header("Synchronization")]
    public float analysisInterval = 0.1f; // ADDED: For scroll speed calculation
}

public class PitchVisualizer : MonoBehaviour
{
    [SerializeField] private VisualizationSettings settings;
    
    private Queue<GameObject> activeCubes;
    private List<GameObject> preRenderedCubes; // Für native Aufnahmen
    private int currentPlaybackIndex = 0;
    
    // NEW: Variables for synchronized scrolling
    private bool isNativeTrack = false;
    private float lastPlaybackTime = 0f;
    private float nativeCubeOffset = 0f;
    
    // NEW: Variables for looping native track
    private List<PitchDataPoint> originalNativePitchData;
    private float nativeClipDuration = 0f;
    private int visibleCubeStartIndex = 0;
    
    // NEW: Focal point system
    private GameObject focalIndicator;
    private float focalPointLocalX = 0f; // Cached focal point in local space
    
    // NEW: Enum for cube states
    private enum CubeState
    {
        Played,   // Past - bright
        Current,  // At focal point - extra bright
        Future    // Future - dim
    }
    
    // FIXED: Add Awake method to ensure initialization
    void Awake()
    { 
        Debug.Log($"[PitchVisualizer] {gameObject.name} Awake() - cubeScale: {settings.cubeScale}");
        EnsureInitialization();
    }
    
    // FIXED: Add Start method as fallback
    void Start()
    {
        Debug.Log($"[PitchVisualizer] {gameObject.name} Start() - cubeScale: {settings.cubeScale}");
        EnsureInitialization();
        UpdateFocalPoint(); // Calculate focal point position
        CreateFocalIndicator(); // Create visual marker
    }
    
    // ADDED: Safe initialization method
    private void EnsureInitialization()
    {
        if (activeCubes == null)
        {
            activeCubes = new Queue<GameObject>();
        }
        
        if (preRenderedCubes == null)
        {
            preRenderedCubes = new List<GameObject>();
        }
        
        ValidateSettings();

        // ADDED: Debug cube scale at initialization
        if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} initialized - cubeScale: {settings.cubeScale}");
    }
    
    // NEW: Calculate focal point position in local space
    private void UpdateFocalPoint()
    {
        if (settings.focalPointTransform != null && settings.cubeParent != null)
        {
            // Convert world position to local position relative to cubeParent
            Vector3 localPos = settings.cubeParent.InverseTransformPoint(settings.focalPointTransform.position);
            focalPointLocalX = localPos.x;
            
            Debug.Log($"[PitchVisualizer] {gameObject.name} Focal point updated - Local X: {focalPointLocalX:F2}");
        }
        else
        {
            // Fallback to middle of visible area
            focalPointLocalX = (settings.maxCubes * settings.cubeSpacing) * 0.4f; // 40% into the visible area
            Debug.Log($"[PitchVisualizer] {gameObject.name} Using fallback focal point: {focalPointLocalX:F2}");
        }
    }
    
    // NEW: Create visual indicator for focal point
    private void CreateFocalIndicator()
    {
        if (!settings.showFocalIndicator || settings.cubeParent == null) return;
        
        // Clean up existing indicator
        if (focalIndicator != null)
        {
            DestroyImmediate(focalIndicator);
        }
        
        // Create new indicator
        if (settings.focalIndicatorPrefab != null)
        {
            focalIndicator = Instantiate(settings.focalIndicatorPrefab, settings.cubeParent);
        }
        else
        {
            // Create default indicator
            focalIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            focalIndicator.name = "FocalPointIndicator";
            focalIndicator.transform.SetParent(settings.cubeParent);
            
            // Style the indicator
            focalIndicator.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var renderer = focalIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 1f, 0f, 0.7f); // Yellow, semi-transparent
            }
        }
        
        // Position at focal point
        Vector3 focalPos = new Vector3(focalPointLocalX, 0f, 0f) + settings.trackOffset;
        focalPos.y += 2f; // Slightly above cubes
        focalIndicator.transform.localPosition = focalPos;
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Created focal indicator at: {focalPos}");
    }
    
    // NEW: Get focal point index in cube array
    private int GetFocalCubeIndex()
    {
        return Mathf.RoundToInt(focalPointLocalX / settings.cubeSpacing);
    }
    
    public void Initialize(VisualizationSettings visualSettings)
    {
        // ADDED: Debug what settings are being passed in
        if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} Initialize() called - OLD cubeScale: {settings.cubeScale}, NEW cubeScale: {visualSettings.cubeScale}");
        
        settings = visualSettings;
        EnsureInitialization(); // Use safe initialization
        UpdateFocalPoint(); // Recalculate focal point
        CreateFocalIndicator(); // Recreate indicator

        if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} manually initialized with settings - FINAL cubeScale: {settings.cubeScale}");
    }
    
    // NEW: Set track type for different behavior
    public void SetAsNativeTrack(bool isNative)
    {
        isNativeTrack = isNative;
        Debug.Log($"[PitchVisualizer] {gameObject.name} set as native track: {isNative}");
    }
    
    // NEW: Set analysis interval for synchronized scrolling
    public void SetAnalysisInterval(float interval)
    {
        // ADDED: Debug if this affects cube scale
        if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} SetAnalysisInterval - BEFORE cubeScale: {settings.cubeScale}");
        
        settings.analysisInterval = interval;

        if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} analysis interval set to: {interval} - AFTER cubeScale: {settings.cubeScale}");
    }
    
    /// <summary>
    /// ENHANCED: User cubes spawn at focal point and scroll left
    /// </summary>
    public void AddRealtimePitchData(PitchDataPoint pitchData)
    {
        EnsureInitialization(); // Safety check
        
        // ADDED: Debug cube scale during first user cube creation
        if (activeCubes.Count == 0)
        {
            if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} AddRealtimePitchData - First cube creation, cubeScale: {settings.cubeScale}");
        }
        
        GameObject cube = CreateCube(pitchData, false);
        if (cube != null)
        {
            activeCubes.Enqueue(cube);
        }
        MaintainCubeCount();
        UpdateUserCubePositions(); // NEW: Updated method for focal point spawning
    }
    
    // FIXED: User cubes now spawn at focal point and move left consistently
    private void UpdateUserCubePositions()
    {
        if (activeCubes == null) return;
        
        var cubeArray = activeCubes.ToArray();
        
        for (int i = 0; i < cubeArray.Length; i++)
        {
            var cube = cubeArray[i];
            if (cube != null)
            {
                // FIXED: Newest cube (last in queue) at focal point, older cubes move left
                int age = cubeArray.Length - 1 - i; // 0 = newest, higher = older
                float cubeX = focalPointLocalX - (age * settings.cubeSpacing);
                Vector3 newPos = new Vector3(cubeX, cube.transform.localPosition.y, 0) + settings.trackOffset;
                cube.transform.localPosition = newPos;
                
                Debug.Log($"[PitchVisualizer] {gameObject.name} User cube {i} (age {age}) positioned at X: {cubeX:F2}");
            }
        }
    }
    
    /// <summary>
    /// Pre-rendert initial window für looping native Aufnahme
    /// </summary>
    public void PreRenderNativeTrack(List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization(); // CRITICAL: Ensure initialization before use
        isNativeTrack = true; // Mark as native track
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} PreRenderNativeTrack called with {pitchDataList?.Count ?? 0} data points");
        
        if (pitchDataList == null || pitchDataList.Count == 0)
        {
            Debug.LogWarning($"[PitchVisualizer] {gameObject.name} - No pitch data to pre-render");
            return;
        }
        
        // Store original data for looping
        originalNativePitchData = new List<PitchDataPoint>(pitchDataList);
        nativeClipDuration = pitchDataList.Count > 0 ? pitchDataList[pitchDataList.Count - 1].timestamp : 0f;
        
        ClearPreRenderedCubes();
        
        // Pre-render initial visible window (maxCubes worth)
        CreateInitialNativeWindow();
        
        currentPlaybackIndex = 0;
        lastPlaybackTime = 0f;
        nativeCubeOffset = 0f;
        visibleCubeStartIndex = 0;
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Pre-rendered {preRenderedCubes.Count} cubes, clip duration: {nativeClipDuration}s");
    }
    
    // FIXED: Native cubes positioned relative to focal point
    private void CreateInitialNativeWindow()
    {
        if (originalNativePitchData == null) return;
        
        int focalIndex = GetFocalCubeIndex();
        
        // Create cubes for the visible window, centered around focal point
        for (int i = 0; i < settings.maxCubes && i < originalNativePitchData.Count; i++)
        {
            var pitchData = originalNativePitchData[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                // FIXED: Position relative to focal point
                float cubeX = focalIndex * settings.cubeSpacing + (i - focalIndex) * settings.cubeSpacing;
                Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                cube.transform.localPosition = pos;
                
                preRenderedCubes.Add(cube);
                SetNativeCubeState(cube, i, 0, i); // Initial state (all future)
                
                Debug.Log($"[PitchVisualizer] {gameObject.name} Created native cube {i}: pitch={pitchData.frequency:F1}Hz, pos={cube.transform.localPosition}");
            }
        }
    }
    
    /// <summary>
    /// FIXED: Native track synchronized with user movement and focal point
    /// </summary>
    public void UpdateNativeTrackPlayback(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization();
        
        if (preRenderedCubes == null || originalNativePitchData == null) return;
        
        // Handle looping
        float loopedPlaybackTime = nativeClipDuration > 0 ? playbackTime % nativeClipDuration : playbackTime;
        
        // SIMPLE: Always move if enough time has passed
        float deltaTime = loopedPlaybackTime - lastPlaybackTime;
        if (deltaTime < 0) deltaTime += nativeClipDuration; // Handle loop wrap
        
        if (deltaTime >= settings.analysisInterval)
        {
            // FIXED: Move all cubes left (same direction as user cubes)
            ScrollNativeCubesDiscrete(1);
            
            // Update cubes - add new ones at the RIGHT edge (they'll scroll to focal point)
            AddSimpleNativeCube(loopedPlaybackTime);
            RemoveOffscreenNativeCubes();
            
            // Find current playback index and update all cube states
            int targetIndex = FindIndexByTime(loopedPlaybackTime, originalNativePitchData);
            UpdateAllNativeCubeStates(targetIndex);
            
            lastPlaybackTime = loopedPlaybackTime;
            
            Debug.Log($"[PitchVisualizer] {gameObject.name} Native update - PlaybackIndex: {targetIndex}, FocalIndex: {GetFocalCubeIndex()}");
        }
    }
    
    // FIXED: Update native cube states based on focal point position
    private void UpdateAllNativeCubeStates(int currentPlaybackIndex)
    {
        int focalIndex = GetFocalCubeIndex();
        
        for (int i = 0; i < preRenderedCubes.Count; i++)
        {
            var cube = preRenderedCubes[i];
            if (cube == null) continue;
            
            // Calculate the actual data index for this cube
            int globalDataIndex = (visibleCubeStartIndex + i) % originalNativePitchData.Count;
            
            // FIXED: Determine state based on cube's position relative to focal point
            float cubeLocalX = cube.transform.localPosition.x - settings.trackOffset.x;
            int cubeVisualIndex = Mathf.RoundToInt(cubeLocalX / settings.cubeSpacing);
            
            // State logic: cubes at/near focal point are "current"
            CubeState state;
            if (cubeVisualIndex < focalIndex - 1)
            {
                state = CubeState.Played; // Past (left of focal)
            }
            else if (cubeVisualIndex >= focalIndex - 1 && cubeVisualIndex <= focalIndex + 1)
            {
                state = CubeState.Current; // Current (at focal point)
            }
            else
            {
                state = CubeState.Future; // Future (right of focal)
            }
            
            SetNativeCubeStateByType(cube, globalDataIndex, state);
        }
    }
    
    // NEW: Set cube state by type
    private void SetNativeCubeStateByType(GameObject cube, int dataIndex, CubeState state)
    {
        var renderer = cube.GetComponent<Renderer>();
        if (renderer == null) return;
        
        var pitchData = originalNativePitchData[dataIndex];
        Color baseColor = GetCubeColor(pitchData);
        
        switch (state)
        {
            case CubeState.Played:
                baseColor *= settings.playedBrightness;
                baseColor.a = settings.playedAlpha;
                break;
            case CubeState.Current:
                baseColor *= settings.currentBrightness;
                baseColor.a = settings.currentAlpha;
                break;
            case CubeState.Future:
                baseColor *= settings.futureBrightness;
                baseColor.a = settings.futureAlpha;
                break;
        }
        
        renderer.material.color = baseColor;
    }
    
    // NEW: Set native cube state (compatibility method)
    private void SetNativeCubeState(GameObject cube, int cubeIndex, int currentPlaybackIndex, int globalDataIndex = -1)
    {
        // Determine state based on playback position
        int dataIndex = globalDataIndex >= 0 ? globalDataIndex : (visibleCubeStartIndex + cubeIndex) % originalNativePitchData.Count;
        
        CubeState state;
        if (dataIndex < currentPlaybackIndex)
        {
            state = CubeState.Played;
        }
        else if (dataIndex == currentPlaybackIndex)
        {
            state = CubeState.Current;
        }
        else
        {
            state = CubeState.Future;
        }
        
        SetNativeCubeStateByType(cube, dataIndex, state);
    }
    
    // NEW: Discrete stepping movement (like user cubes)
    private void ScrollNativeCubesDiscrete(int steps)
    {
        if (preRenderedCubes == null || steps <= 0) return;
        
        float scrollDistance = steps * settings.cubeSpacing;
        
        foreach (var cube in preRenderedCubes)
        {
            if (cube != null)
            {
                Vector3 pos = cube.transform.localPosition;
                pos.x -= scrollDistance;
                cube.transform.localPosition = pos;
            }
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Scrolled {steps} steps, distance: {scrollDistance}");
    }
    
    // NEW: Add simple native cube
    private void AddSimpleNativeCube(float loopedPlaybackTime)
    {
        if (originalNativePitchData == null) return;
        
        // Calculate which data index should appear at the right edge
        int newDataIndex = (visibleCubeStartIndex + settings.maxCubes) % originalNativePitchData.Count;
        var newPitchData = originalNativePitchData[newDataIndex];
        
        // Create new cube at the right edge
        GameObject newCube = CreateCube(newPitchData, true, settings.maxCubes - 1);
        if (newCube != null)
        {
            // Position at right edge
            Vector3 pos = new Vector3((settings.maxCubes - 1) * settings.cubeSpacing, 0, 0) + settings.trackOffset;
            newCube.transform.localPosition = pos;
            
            preRenderedCubes.Add(newCube);
            SetNativeCubeState(newCube, settings.maxCubes - 1, currentPlaybackIndex, newDataIndex);
            
            Debug.Log($"[PitchVisualizer] {gameObject.name} Added new cube at right edge: dataIndex={newDataIndex}, pitch={newPitchData.frequency:F1}Hz");
        }
        
        // Update visible window start index
        visibleCubeStartIndex = (visibleCubeStartIndex + 1) % originalNativePitchData.Count;
    }
    
    // NEW: Remove cubes that scrolled off the left side
    private void RemoveOffscreenNativeCubes()
    {
        if (preRenderedCubes == null) return;
        
        for (int i = preRenderedCubes.Count - 1; i >= 0; i--)
        {
            var cube = preRenderedCubes[i];
            if (cube != null && cube.transform.localPosition.x < -settings.cubeSpacing)
            {
                DestroyImmediate(cube);
                preRenderedCubes.RemoveAt(i);
                Debug.Log($"[PitchVisualizer] {gameObject.name} Removed offscreen cube at index {i}");
            }
        }
    }
    
    // ENHANCED: Create cube with proper focal point positioning
    private GameObject CreateCube(PitchDataPoint pitchData, bool isPreRendered, int index = -1)
    {
        if (settings.cubePrefab == null || settings.cubeParent == null) 
        {
            Debug.LogError($"[PitchVisualizer] {gameObject.name} - Missing cubePrefab or cubeParent!");
            return null;
        }
        
        GameObject newCube = Instantiate(settings.cubePrefab, settings.cubeParent);
        newCube.SetActive(true);
        
        // Position
        float xPosition;
        if (isPreRendered)
        {
            // Native cubes: will be positioned by CreateInitialNativeWindow
            xPosition = index * settings.cubeSpacing; // Temporary, will be repositioned
        }
        else
        {
            // User cubes: spawn at focal point (will be repositioned by UpdateUserCubePositions)
            xPosition = focalPointLocalX;
        }
        
        Vector3 position = new Vector3(xPosition, 0, 0) + settings.trackOffset;
        newCube.transform.localPosition = position;
        
        // Scale basierend auf Pitch
        float pitchScale = CalculatePitchScale(pitchData);
        Vector3 scale = settings.cubeScale;
        scale.y = pitchScale;
        
        newCube.transform.localScale = scale;
        
        // Farbe (nur für Real-Time Cubes)
        if (!isPreRendered)
        {
            var renderer = newCube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = GetCubeColor(pitchData);
            }
        }
        
        return newCube;
    }
    
    private float CalculatePitchScale(PitchDataPoint pitchData)
    {
        if (!pitchData.HasPitch)
        {
            return settings.silenceCubeHeight;
        }
        
        float pitchScale = Mathf.Log(pitchData.frequency / settings.minFrequency) * settings.pitchScaleMultiplier;
        return Mathf.Clamp(pitchScale, 0.2f, 20f);
    }
    
    private Color GetCubeColor(PitchDataPoint pitchData)
    {
        if (!pitchData.HasPitch)
        {
            return settings.silenceColor;
        }
        
        if (settings.useHSVColorMapping)
        {
            float normalizedPitch = (pitchData.frequency - settings.minFrequency) / 
                                  (settings.maxFrequency - settings.minFrequency);
            return Color.HSVToRGB(normalizedPitch * 0.8f, settings.saturation, settings.brightness);
        }
        
        return Color.white;
    }
    
    private int FindIndexByTime(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        if (pitchDataList == null || pitchDataList.Count == 0) return 0;
        
        for (int i = 0; i < pitchDataList.Count; i++)
        {
            if (pitchDataList[i].timestamp > playbackTime)
            {
                return Mathf.Max(0, i - 1);
            }
        }
        return pitchDataList.Count - 1;
    }
    
    private void MaintainCubeCount()
    {
        if (activeCubes == null) return;
        
        while (activeCubes.Count > settings.maxCubes)
        {
            GameObject oldCube = activeCubes.Dequeue();
            if (oldCube != null)
            {
                DestroyImmediate(oldCube);
            }
        }
    }
    
    private void ClearPreRenderedCubes()
    {
        // FIXED: Add null check
        if (preRenderedCubes == null) 
        {
            Debug.LogWarning($"[PitchVisualizer] {gameObject.name} - preRenderedCubes is null in ClearPreRenderedCubes");
            return;
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Clearing {preRenderedCubes.Count} pre-rendered cubes");
        
        foreach (var cube in preRenderedCubes)
        {
            if (cube != null)
            {
                DestroyImmediate(cube);
            }
        }
        preRenderedCubes.Clear();
        currentPlaybackIndex = 0;
        lastPlaybackTime = 0f;
        nativeCubeOffset = 0f; // Reset offset
        visibleCubeStartIndex = 0;
    }
    
    private void ValidateSettings()
    {
        if (settings.cubePrefab == null)
        {
            settings.cubePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            settings.cubePrefab.SetActive(false);
            Debug.Log($"[PitchVisualizer] {gameObject.name} Created default cube prefab");
        }
        
        if (settings.cubeParent == null)
        {
            GameObject parent = new GameObject("PitchVisualization");
            settings.cubeParent = parent.transform;
            Debug.Log($"[PitchVisualizer] {gameObject.name} Created default cube parent");
        }
    }
    
    public void ClearAll()
    {
        Debug.Log($"[PitchVisualizer] {gameObject.name} ClearAll called");
        
        if (activeCubes != null)
        {
            while (activeCubes.Count > 0)
            {
                GameObject cube = activeCubes.Dequeue();
                if (cube != null) DestroyImmediate(cube);
            }
        }
        
        ClearPreRenderedCubes();
        
        // Clear focal indicator
        if (focalIndicator != null)
        {
            DestroyImmediate(focalIndicator);
            focalIndicator = null;
        }
        
        // Clear looping data
        originalNativePitchData = null;
        nativeClipDuration = 0f;
    }
    
    void OnDestroy()
    {
        ClearAll();
    }

    // ADDED: Getter for settings (read-only access)
    public VisualizationSettings GetSettings()
    {
        return settings;
    }

    // ADDED: Method to update settings at runtime if needed
    public void UpdateSettings(VisualizationSettings newSettings)
    {
        // ADDED: Debug settings changes
        Debug.Log($"[PitchVisualizer] {gameObject.name} UpdateSettings() called - OLD cubeScale: {settings.cubeScale}, NEW cubeScale: {newSettings.cubeScale}");
        
        settings = newSettings;
        ValidateSettings(); // Re-validate after update
        UpdateFocalPoint(); // Recalculate focal point
        CreateFocalIndicator(); // Recreate indicator
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} UpdateSettings() completed - FINAL cubeScale: {settings.cubeScale}");
    }
}