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
    public Vector3 trackOffset = Vector3.zero; // Offset f�r zweite Spur
    
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
}

public class PitchVisualizer : MonoBehaviour
{
    [SerializeField] private VisualizationSettings settings;
    
    private Queue<GameObject> activeCubes;
    private List<GameObject> preRenderedCubes; // F�r native Aufnahmen
    private int currentPlaybackIndex = 0;
    
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
    
    /// <summary>
    /// F�r Real-Time Mikrofonaufnahme
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
    /// Pre-rendert alle W�rfel f�r native Aufnahme (dunkel)
    /// </summary>
    public void PreRenderNativeTrack(List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization(); // CRITICAL: Ensure initialization before use
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} PreRenderNativeTrack called with {pitchDataList?.Count ?? 0} data points");
        
        if (pitchDataList == null || pitchDataList.Count == 0)
        {
            Debug.LogWarning($"[PitchVisualizer] {gameObject.name} - No pitch data to pre-render");
            return;
        }
        
        ClearPreRenderedCubes();
        
        for (int i = 0; i < pitchDataList.Count && i < settings.maxCubes; i++)
        {
            var pitchData = pitchDataList[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                preRenderedCubes.Add(cube);
                
                // Dunklere, inaktive Darstellung
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color cubeColor = GetCubeColor(pitchData);
                    cubeColor = Color.Lerp(cubeColor, Color.gray, 0.7f); // Dunkler
                    cubeColor.a = 0.5f; // Transparent
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
        Debug.Log($"[PitchVisualizer] {gameObject.name} Pre-rendered {preRenderedCubes.Count} cubes");
    }
    
    /// <summary>
    /// Aktiviert native W�rfel synchron zum Playback
    /// </summary>
    public void UpdateNativeTrackPlayback(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization(); // Safety check
        
        if (preRenderedCubes == null || pitchDataList == null) return;
        
        // Finde aktuellen Index basierend auf Playback-Zeit
        int targetIndex = FindIndexByTime(playbackTime, pitchDataList);
        
        // Aktiviere W�rfel bis zum aktuellen Index
        for (int i = currentPlaybackIndex; i <= targetIndex && i < preRenderedCubes.Count; i++)
        {
            ActivateNativeCube(i, pitchDataList[i]);
        }
        
        currentPlaybackIndex = Mathf.Max(currentPlaybackIndex, targetIndex);
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
        float xPosition = isPreRendered ? index * settings.cubeSpacing : (activeCubes?.Count ?? 0) * settings.cubeSpacing;
        Vector3 position = new Vector3(xPosition, 0, 0) + settings.trackOffset;
        newCube.transform.localPosition = position;
        
        // Scale basierend auf Pitch
        float pitchScale = CalculatePitchScale(pitchData);
        Vector3 scale = settings.cubeScale;
        scale.y = pitchScale;
        newCube.transform.localScale = scale;
        
        // Farbe (nur f�r Real-Time Cubes)
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
            // Aktiviere mit voller Farbe und Helligkeit
            Color activeColor = GetCubeColor(pitchData);
            activeColor.a = 1f;
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
                Vector3 newPos = new Vector3(index * settings.cubeSpacing, cube.transform.localPosition.y, 0);
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