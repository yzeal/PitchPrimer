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
}

public class PitchVisualizer : MonoBehaviour
{
    [SerializeField] private VisualizationSettings settings;
    
    private Queue<GameObject> activeCubes;
    private List<GameObject> preRenderedCubes; // Für native Aufnahmen
    private int currentPlaybackIndex = 0;
    
    public void Initialize(VisualizationSettings visualSettings)
    {
        settings = visualSettings;
        activeCubes = new Queue<GameObject>();
        preRenderedCubes = new List<GameObject>();
        
        ValidateSettings();
    }
    
    /// <summary>
    /// Für Real-Time Mikrofonaufnahme
    /// </summary>
    public void AddRealtimePitchData(PitchDataPoint pitchData)
    {
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
            }
        }
        
        currentPlaybackIndex = 0;
    }
    
    /// <summary>
    /// Aktiviert native Würfel synchron zum Playback
    /// </summary>
    public void UpdateNativeTrackPlayback(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        // Finde aktuellen Index basierend auf Playback-Zeit
        int targetIndex = FindIndexByTime(playbackTime, pitchDataList);
        
        // Aktiviere Würfel bis zum aktuellen Index
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
            return null;
        
        GameObject newCube = Instantiate(settings.cubePrefab, settings.cubeParent);
        newCube.SetActive(true);
        
        // Position
        float xPosition = isPreRendered ? index * settings.cubeSpacing : activeCubes.Count * settings.cubeSpacing;
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
        if (index >= 0 && index < preRenderedCubes.Count)
        {
            var cube = preRenderedCubes[index];
            var renderer = cube.GetComponent<Renderer>();
            
            if (renderer != null)
            {
                // Aktiviere mit voller Farbe und Helligkeit
                Color activeColor = GetCubeColor(pitchData);
                activeColor.a = 1f;
                renderer.material.color = activeColor;
            }
        }
    }
    
    private int FindIndexByTime(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
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
        }
        
        if (settings.cubeParent == null)
        {
            GameObject parent = new GameObject("PitchVisualization");
            settings.cubeParent = parent.transform;
        }
    }
    
    public void ClearAll()
    {
        if(activeCubes == null) return;

        while (activeCubes.Count > 0)
        {
            GameObject cube = activeCubes.Dequeue();
            if (cube != null) DestroyImmediate(cube);
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