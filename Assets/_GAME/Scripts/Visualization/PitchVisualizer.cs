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
    public Vector3 cubeScale = new Vector3(0.8f, 1f, 0.8f);
    
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
    
    // FIXED: Add Awake method to ensure initialization
    void Awake()
    {
        EnsureInitialization();
    }
    
    // FIXED: Add Start method as fallback
    void Start()
    {
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
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} initialized - activeCubes: {activeCubes != null}, preRenderedCubes: {preRenderedCubes != null}");
    }
    
    public void Initialize(VisualizationSettings visualSettings)
    {
        settings = visualSettings;
        EnsureInitialization(); // Use safe initialization
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} manually initialized with settings");
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
        settings.analysisInterval = interval;
        Debug.Log($"[PitchVisualizer] {gameObject.name} analysis interval set to: {interval}");
    }
    
    /// <summary>
    /// Für Real-Time Mikrofonaufnahme
    /// </summary>
    public void AddRealtimePitchData(PitchDataPoint pitchData)
    {
        EnsureInitialization(); // Safety check
        
        GameObject cube = CreateCube(pitchData, false);
        if (cube != null)
        {
            activeCubes.Enqueue(cube);
        }
        MaintainCubeCount();
        UpdateCubePositions();
    }
    
    /// <summary>
    /// Pre-rendert alle Würfel für native Aufnahme (dunkel)
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
        
        ClearPreRenderedCubes();
        
        // Pre-render all cubes but initially invisible
        for (int i = 0; i < pitchDataList.Count; i++)
        {
            var pitchData = pitchDataList[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                preRenderedCubes.Add(cube);
                
                // Initially invisible/inactive
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color cubeColor = GetCubeColor(pitchData);
                    cubeColor = Color.Lerp(cubeColor, Color.gray, 0.8f); // Very dark
                    cubeColor.a = 0.3f; // Very transparent
                    renderer.material.color = cubeColor;
                }
                
                Debug.Log($"[PitchVisualizer] {gameObject.name} Created pre-rendered cube {i}: pitch={pitchData.frequency:F1}Hz, pos={cube.transform.localPosition}");
            }
            else
            {
                Debug.LogWarning($"[PitchVisualizer] {gameObject.name} Failed to create cube {i}");
            }
        }
        
        currentPlaybackIndex = 0;
        lastPlaybackTime = 0f;
        nativeCubeOffset = 0f; // Reset offset
        Debug.Log($"[PitchVisualizer] {gameObject.name} Pre-rendered {preRenderedCubes.Count} cubes");
    }
    
    /// <summary>
    /// FIXED: Synchronized discrete scrolling + activation for native track
    /// </summary>
    public void UpdateNativeTrackPlayback(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization(); // Safety check
        
        if (preRenderedCubes == null || pitchDataList == null) return;
        
        // FIXED: Use discrete stepping instead of smooth scrolling
        int targetIndex = FindIndexByTime(playbackTime, pitchDataList);
        
        // Only update when we move to a new analysis step
        if (targetIndex > currentPlaybackIndex)
        {
            // Calculate how many steps we've moved
            int stepsMoved = targetIndex - currentPlaybackIndex;
            
            // Move all cubes left by the number of steps
            ScrollNativeCubesDiscrete(stepsMoved);
            
            // Activate new cubes
            for (int i = currentPlaybackIndex; i <= targetIndex && i < preRenderedCubes.Count; i++)
            {
                ActivateNativeCube(i, pitchDataList[i]);
            }
            
            currentPlaybackIndex = targetIndex;
            
            // Debug
            Debug.Log($"[PitchVisualizer] {gameObject.name} Moved {stepsMoved} discrete steps, now at index {targetIndex}");
        }
        
        // Remove old cubes that scrolled off-screen
        RemoveOffscreenNativeCubes();
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
    
    // OLD method for comparison
    private void ScrollNativeCubesLeft(float deltaTime)
    {
        if (preRenderedCubes == null) return;
        
        // Calculate scroll speed based on analysis interval
        float scrollSpeed = settings.cubeSpacing / settings.analysisInterval; // FIXED: Use settings
        float scrollDistance = scrollSpeed * deltaTime;
        
        foreach (var cube in preRenderedCubes)
        {
            if (cube != null)
            {
                Vector3 pos = cube.transform.localPosition;
                pos.x -= scrollDistance;
                cube.transform.localPosition = pos;
            }
        }
    }
    
    // NEW: Add new native cube for longer recordings
    private void AddNewNativeCube(PitchDataPoint pitchData, int index)
    {
        GameObject cube = CreateCube(pitchData, true, preRenderedCubes.Count);
        
        if (cube != null)
        {
            preRenderedCubes.Add(cube);
            
            // Position at the right edge
            Vector3 pos = cube.transform.localPosition;
            pos.x = (settings.maxCubes - 1) * settings.cubeSpacing;
            cube.transform.localPosition = pos + settings.trackOffset;
            
            // Start inactive
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color cubeColor = GetCubeColor(pitchData);
                cubeColor = Color.Lerp(cubeColor, Color.gray, 0.8f);
                cubeColor.a = 0.3f;
                renderer.material.color = cubeColor;
            }
        }
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
            // Native cubes: spread them out initially based on their timestamp
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
        settings = newSettings;
        ValidateSettings(); // Re-validate after update
    }
}