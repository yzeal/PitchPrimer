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
    private float nativeCubeOffset = 0f; // NEW: Track total scroll offset
    
    // NEW: Variables for looping native track
    private List<PitchDataPoint> originalNativePitchData; // Store original data for looping
    private float nativeClipDuration = 0f; // Duration of native clip
    private int visibleCubeStartIndex = 0; // Where the visible window starts in the data
    
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
    
    public void Initialize(VisualizationSettings visualSettings)
    {
        // ADDED: Debug what settings are being passed in
        if (!isNativeTrack) Debug.Log($"[PitchVisualizer] {gameObject.name} Initialize() called - OLD cubeScale: {settings.cubeScale}, NEW cubeScale: {visualSettings.cubeScale}");
        
        settings = visualSettings;
        EnsureInitialization(); // Use safe initialization

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
    /// Für Real-Time Mikrofonaufnahme
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
        UpdateCubePositions();
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
    
    // NEW: Create initial window of native cubes
    private void CreateInitialNativeWindow()
    {
        if (originalNativePitchData == null) return;
        
        // Create cubes for the visible window (maxCubes worth)
        for (int i = 0; i < settings.maxCubes && i < originalNativePitchData.Count; i++)
        {
            var pitchData = originalNativePitchData[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                preRenderedCubes.Add(cube);
                SetCubeInactive(cube, pitchData);
                
                Debug.Log($"[PitchVisualizer] {gameObject.name} Created native cube {i}: pitch={pitchData.frequency:F1}Hz, pos={cube.transform.localPosition}");
            }
        }
    }
    
    // NEW: Set cube to inactive appearance
    private void SetCubeInactive(GameObject cube, PitchDataPoint pitchData)
    {
        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color cubeColor = GetCubeColor(pitchData);
            cubeColor = Color.Lerp(cubeColor, Color.gray, 0.8f); // Very dark
            cubeColor.a = 0.3f; // Very transparent
            renderer.material.color = cubeColor;
        }
    }
    
    /// <summary>
    /// SIMPLE FIX: Force movement every frame regardless of data
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
            // Always move one step, regardless of data density
            ScrollNativeCubesDiscrete(1);
            
            // Update cubes
            AddSimpleNativeCube(loopedPlaybackTime);
            RemoveOffscreenNativeCubes();
            
            // Activate current cube
            int targetIndex = FindIndexByTime(loopedPlaybackTime, originalNativePitchData);
            ActivateNativeCubeAtCurrentPosition(targetIndex);
            
            lastPlaybackTime = loopedPlaybackTime;
            
            Debug.Log($"[PitchVisualizer] {gameObject.name} Simple move at time {loopedPlaybackTime:F2}");
        }
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
            SetCubeInactive(newCube, newPitchData);
            
            Debug.Log($"[PitchVisualizer] {gameObject.name} Added new cube at right edge: dataIndex={newDataIndex}, pitch={newPitchData.frequency:F1}Hz");
        }
        
        // Update visible window start index
        visibleCubeStartIndex = (visibleCubeStartIndex + 1) % originalNativePitchData.Count;
    }
    
    private void ActivateNativeCubeAtCurrentPosition(int targetIndex)
    {
        if (originalNativePitchData == null || targetIndex >= originalNativePitchData.Count) return;
        
        var currentPitchData = originalNativePitchData[targetIndex];
        
        // Find which cube in the visible window corresponds to this playback position
        // This is typically around the left side of the visible area (where activation happens)
        int activationCubeIndex = 5; // Activate cube at position 5 (adjust as needed for visual preference)
        
        if (activationCubeIndex < preRenderedCubes.Count)
        {
            var cubeToActivate = preRenderedCubes[activationCubeIndex];
            if (cubeToActivate != null)
            {
                var renderer = cubeToActivate.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color activeColor = GetCubeColor(currentPitchData);
                    activeColor = Color.Lerp(activeColor, activeColor, settings.saturation);
                    activeColor.a = 0.8f; // Semi-transparent to distinguish from user
                    renderer.material.color = activeColor;
                }
            }
        }
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
    
    // FIXED: Return type changed from void to GameObject
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
            // Native cubes: spread them out initially based on their index
            xPosition = index * settings.cubeSpacing;
        }
        else
        {
            // User cubes: position based on queue count
            xPosition = (activeCubes?.Count ?? 0) * settings.cubeSpacing;
        }
        
        Vector3 position = new Vector3(xPosition, 0, 0) + settings.trackOffset;
        newCube.transform.localPosition = position;
        
        // Scale basierend auf Pitch
        float pitchScale = CalculatePitchScale(pitchData);
        Vector3 scale = settings.cubeScale;
        scale.y = pitchScale;
        
        // ADDED: Comprehensive debug logging for scale tracking
        Debug.Log($"[PitchVisualizer] {gameObject.name} CreateCube - isPreRendered: {isPreRendered}, " +
                  $"settings.cubeScale: {settings.cubeScale}, " +
                  $"pitchScale: {pitchScale}, " +
                  $"final scale: {scale}, " +
                  $"pitch: {pitchData.frequency:F1}Hz");
        
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
    
    private void ActivateNativeCube(int index, PitchDataPoint pitchData)
    {
        if (preRenderedCubes == null || index < 0 || index >= preRenderedCubes.Count) return;
        
        var cube = preRenderedCubes[index];
        if (cube == null) return;
        
        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Aktiviere mit voller Farbe und Helligkeit (but still dimmed compared to user)
            Color activeColor = GetCubeColor(pitchData);
            activeColor = Color.Lerp(activeColor, activeColor, settings.saturation); // Use track saturation
            activeColor.a = 0.8f; // Semi-transparent to distinguish from user
            renderer.material.color = activeColor;
        }
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
    
    private void UpdateCubePositions()
    {
        if (activeCubes == null) return;
        
        int index = 0;
        foreach (GameObject cube in activeCubes)
        {
            if (cube != null)
            {
                Vector3 newPos = new Vector3(index * settings.cubeSpacing, cube.transform.localPosition.y, 0) + settings.trackOffset;
                cube.transform.localPosition = newPos;
            }
            index++;
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
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} UpdateSettings() completed - FINAL cubeScale: {settings.cubeScale}");
    }
}