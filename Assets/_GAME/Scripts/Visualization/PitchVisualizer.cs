using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Modular visualization system for pitch data
// Supports both real-time and pre-rendered visualizations
// Designed for chorusing with dual-track display
// UPDATED: Personal pitch range system for individual voice calibration (NO automatic adaptation)

[System.Serializable]
public class PersonalPitchRange
{
    [Header("Individual Voice Range")]
    [Tooltip("Minimum pitch for this specific voice/speaker (Hz)")]
    public float personalMinPitch = 100f;
    
    [Tooltip("Maximum pitch for this specific voice/speaker (Hz)")]
    public float personalMaxPitch = 300f;
    
    [Header("Visual Mapping")]
    [Tooltip("Minimum cube height (when at personalMinPitch)")]
    public float minCubeHeight = 0.1f;
    
    [Tooltip("Maximum cube height (when at personalMaxPitch)")]
    public float maxCubeHeight = 5.0f;
    
    [Header("Out-of-Range Handling")]
    [Tooltip("How to handle pitches outside personal range")]
    public OutOfRangeMode outOfRangeMode = OutOfRangeMode.Clamp;
    
    [Tooltip("Color for out-of-range pitches (for feedback only)")]
    public Color outOfRangeColor = Color.red;
    
    [Header("Debug Info")]
    [Tooltip("Show range statistics in console")]
    public bool showRangeDebug = false;
}

public enum OutOfRangeMode
{
    Clamp,      // Clamp to min/max height (recommended)
    Hide,       // Don't show cube
    Highlight   // Show in special color for feedback
}

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
    public Vector3 trackOffset = Vector3.zero;
    
    [Header("Focal Point System")]
    public Transform focalPointTransform;
    public bool showFocalIndicator = true;
    public GameObject focalIndicatorPrefab;
    
    [Header("Personal Pitch Range")]
    public PersonalPitchRange pitchRange;
    
    [Header("Native Track States")]
    public float playedBrightness = 1.0f;
    public float currentBrightness = 1.2f;
    public float futureBrightness = 0.3f;
    public float playedAlpha = 0.9f;
    public float currentAlpha = 1.0f;
    public float futureAlpha = 0.4f;
    
    [Header("Legacy Color Mapping")]
    [Tooltip("Used only for color mapping, not height")]
    public float minFrequency = 80f;
    [Tooltip("Used only for color mapping, not height")]
    public float maxFrequency = 800f;
    public float silenceCubeHeight = 0.05f;
    
    [Header("Colors")]
    public Color silenceColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
    public bool useHSVColorMapping = true;
    public float saturation = 0.8f;
    public float brightness = 1f;
    
    [Header("Synchronization")]
    public float analysisInterval = 0.1f;
}

public class PitchVisualizer : MonoBehaviour
{
    [SerializeField] private VisualizationSettings settings;
    
    private Queue<GameObject> activeCubes;
    private List<GameObject> preRenderedCubes;
    private int currentPlaybackIndex = 0;
    
    // Statistics for debugging (NO automatic adaptation)
    private List<float> observedPitches = new List<float>();
    private int cubeCreationCount = 0;
    
    // Existing variables
    private bool isNativeTrack = false;
    private float lastPlaybackTime = 0f;
    private float nativeCubeOffset = 0f;
    private List<PitchDataPoint> originalNativePitchData;
    private float nativeClipDuration = 0f;
    private int visibleCubeStartIndex = 0;
    private GameObject focalIndicator;
    private float focalPointLocalX = 0f;
    
    private enum CubeState
    {
        Played, Current, Future
    }
    
    void Awake()
    { 
        EnsureInitialization();
        InitializePersonalPitchRange();
    }
    
    void Start()
    {
        EnsureInitialization();
        UpdateFocalPoint();
        CreateFocalIndicator();
    }
    
    private void InitializePersonalPitchRange()
    {
        if (settings.pitchRange == null)
        {
            settings.pitchRange = new PersonalPitchRange();
        }
        
        // Set sensible defaults based on track type
        if (isNativeTrack)
        {
            settings.pitchRange.personalMinPitch = 120f;
            settings.pitchRange.personalMaxPitch = 280f;
        }
        else
        {
            settings.pitchRange.personalMinPitch = 100f;
            settings.pitchRange.personalMaxPitch = 350f;
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Personal pitch range initialized: {settings.pitchRange.personalMinPitch:F0}-{settings.pitchRange.personalMaxPitch:F0}Hz");
    }
    
    // Manual calibration methods
    public void SetPersonalPitchRange(float minPitch, float maxPitch)
    {
        settings.pitchRange.personalMinPitch = minPitch;
        settings.pitchRange.personalMaxPitch = maxPitch;
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Manual pitch range set: {minPitch:F1}-{maxPitch:F1}Hz");
        
        if (settings.pitchRange.showRangeDebug)
        {
            ShowRangeStatistics();
        }
    }
    
    public void SetMaleVoiceRange()
    {
        SetPersonalPitchRange(80f, 250f);
    }
    
    public void SetFemaleVoiceRange()
    {
        SetPersonalPitchRange(120f, 350f);
    }
    
    public void SetChildVoiceRange()
    {
        SetPersonalPitchRange(200f, 500f);
    }
    
    // Calculate cube height using personal pitch range
    private float CalculatePitchScale(PitchDataPoint pitchData)
    {
        if (!pitchData.HasPitch)
        {
            return settings.silenceCubeHeight;
        }
        
        float pitch = pitchData.frequency;
        var range = settings.pitchRange;
        
        // Store for statistics (but DON'T adapt automatically)
        if (observedPitches.Count < 1000)
        {
            observedPitches.Add(pitch);
        }
        
        // Handle out-of-range pitches
        bool isOutOfRange = pitch < range.personalMinPitch || pitch > range.personalMaxPitch;
        
        if (isOutOfRange)
        {
            switch (range.outOfRangeMode)
            {
                case OutOfRangeMode.Hide:
                    return 0f;
                    
                case OutOfRangeMode.Clamp:
                    pitch = Mathf.Clamp(pitch, range.personalMinPitch, range.personalMaxPitch);
                    break;
                    
                case OutOfRangeMode.Highlight:
                    pitch = Mathf.Clamp(pitch, range.personalMinPitch, range.personalMaxPitch);
                    break;
            }
        }
        
        // Map personal pitch range to cube height range
        float normalizedPitch = (pitch - range.personalMinPitch) / (range.personalMaxPitch - range.personalMinPitch);
        normalizedPitch = Mathf.Clamp01(normalizedPitch);
        
        float cubeHeight = Mathf.Lerp(range.minCubeHeight, range.maxCubeHeight, normalizedPitch);
        
        // Debug info
        cubeCreationCount++;
        if (range.showRangeDebug && cubeCreationCount % 50 == 0)
        {
            Debug.Log($"[PitchVisualizer] {gameObject.name} Cube #{cubeCreationCount}: Input={pitchData.frequency:F1}Hz, Mapped={pitch:F1}Hz, Height={cubeHeight:F2}, OutOfRange={isOutOfRange}");
        }
        
        return cubeHeight;
    }
    
    private Color GetCubeColor(PitchDataPoint pitchData)
    {
        if (!pitchData.HasPitch)
        {
            return settings.silenceColor;
        }
        
        float pitch = pitchData.frequency;
        var range = settings.pitchRange;
        
        // Check for out-of-range highlighting
        if (range.outOfRangeMode == OutOfRangeMode.Highlight && 
            (pitch < range.personalMinPitch || pitch > range.personalMaxPitch))
        {
            return range.outOfRangeColor;
        }
        
        // Use legacy frequency range for color mapping (broader spectrum)
        if (settings.useHSVColorMapping)
        {
            float normalizedPitch = (pitch - settings.minFrequency) / (settings.maxFrequency - settings.minFrequency);
            normalizedPitch = Mathf.Clamp01(normalizedPitch);
            return Color.HSVToRGB(normalizedPitch * 0.8f, settings.saturation, settings.brightness);
        }
        
        return Color.white;
    }
    
    // Get statistics without adaptation
    public (int sampleCount, float minObserved, float maxObserved, float currentRangeMin, float currentRangeMax) GetRangeStatistics()
    {
        float minObserved = observedPitches.Count > 0 ? observedPitches.Min() : 0f;
        float maxObserved = observedPitches.Count > 0 ? observedPitches.Max() : 0f;
        
        return (observedPitches.Count, minObserved, maxObserved, 
                settings.pitchRange.personalMinPitch, settings.pitchRange.personalMaxPitch);
    }
    
    public void ShowRangeStatistics()
    {
        var stats = GetRangeStatistics();
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} RANGE STATISTICS:");
        Debug.Log($"  Current Range: {stats.currentRangeMin:F1}-{stats.currentRangeMax:F1}Hz");
        Debug.Log($"  Observed Range: {stats.minObserved:F1}-{stats.maxObserved:F1}Hz ({stats.sampleCount} samples)");
        
        if (stats.sampleCount > 10)
        {
            float coverage = (stats.maxObserved - stats.minObserved) / (stats.currentRangeMax - stats.currentRangeMin);
            Debug.Log($"  Range Coverage: {coverage * 100f:F1}%");
            
            if (coverage < 0.5f)
            {
                Debug.Log($"  SUGGESTION: Consider narrower range for better visual resolution");
            }
            else if (coverage > 1.2f)
            {
                Debug.Log($"  SUGGESTION: Consider wider range to accommodate all pitches");
            }
        }
    }
    
    public void ResetStatistics()
    {
        observedPitches.Clear();
        cubeCreationCount = 0;
        Debug.Log($"[PitchVisualizer] {gameObject.name} Statistics reset");
    }
    
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
    }
    
    private void UpdateFocalPoint()
    {
        if (settings.focalPointTransform != null && settings.cubeParent != null)
        {
            Vector3 localPos = settings.cubeParent.InverseTransformPoint(settings.focalPointTransform.position);
            focalPointLocalX = localPos.x;
        }
        else
        {
            focalPointLocalX = (settings.maxCubes * settings.cubeSpacing) * 0.4f;
        }
    }
    
    private void CreateFocalIndicator()
    {
        if (!settings.showFocalIndicator || settings.cubeParent == null) return;
        
        if (focalIndicator != null)
        {
            DestroyImmediate(focalIndicator);
        }
        
        if (settings.focalIndicatorPrefab != null)
        {
            focalIndicator = Instantiate(settings.focalIndicatorPrefab, settings.cubeParent);
        }
        else
        {
            focalIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            focalIndicator.name = "FocalPointIndicator";
            focalIndicator.transform.SetParent(settings.cubeParent);
            focalIndicator.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var renderer = focalIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 1f, 0f, 0.7f);
            }
        }
        
        Vector3 focalPos = new Vector3(focalPointLocalX, 0f, 0f) + settings.trackOffset;
        focalPos.y += 2f;
        focalIndicator.transform.localPosition = focalPos;
    }
    
    private int GetFocalCubeIndex()
    {
        return Mathf.RoundToInt(focalPointLocalX / settings.cubeSpacing);
    }
    
    public void Initialize(VisualizationSettings visualSettings)
    {
        settings = visualSettings;
        EnsureInitialization();
        UpdateFocalPoint();
        CreateFocalIndicator();
    }
    
    public void SetAsNativeTrack(bool isNative)
    {
        isNativeTrack = isNative;
        InitializePersonalPitchRange();
    }
    
    public void SetAnalysisInterval(float interval)
    {
        settings.analysisInterval = interval;
    }
    
    public void AddRealtimePitchData(PitchDataPoint pitchData)
    {
        EnsureInitialization();
        
        GameObject cube = CreateCube(pitchData, false);
        if (cube != null)
        {
            activeCubes.Enqueue(cube);
        }
        MaintainCubeCount();
        UpdateUserCubePositions();
    }
    
    private void UpdateUserCubePositions()
    {
        if (activeCubes == null) return;
        
        var cubeArray = activeCubes.ToArray();
        
        for (int i = 0; i < cubeArray.Length; i++)
        {
            var cube = cubeArray[i];
            if (cube != null)
            {
                int age = cubeArray.Length - 1 - i;
                float cubeX = focalPointLocalX - (age * settings.cubeSpacing);
                Vector3 newPos = new Vector3(cubeX, cube.transform.localPosition.y, 0) + settings.trackOffset;
                cube.transform.localPosition = newPos;
            }
        }
    }
    
    public void PreRenderNativeTrack(List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization();
        isNativeTrack = true;
        
        if (pitchDataList == null || pitchDataList.Count == 0)
        {
            Debug.LogWarning($"[PitchVisualizer] {gameObject.name} - No pitch data to pre-render");
            return;
        }
        
        originalNativePitchData = new List<PitchDataPoint>(pitchDataList);
        nativeClipDuration = pitchDataList.Count > 0 ? pitchDataList[pitchDataList.Count - 1].timestamp : 0f;
        
        ClearPreRenderedCubes();
        CreateInitialNativeWindow();
        
        currentPlaybackIndex = 0;
        lastPlaybackTime = 0f;
        nativeCubeOffset = 0f;
        visibleCubeStartIndex = 0;
    }
    
    private void CreateInitialNativeWindow()
    {
        if (originalNativePitchData == null) return;
        
        for (int i = 0; i < settings.maxCubes && i < originalNativePitchData.Count; i++)
        {
            var pitchData = originalNativePitchData[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                // FIXED: Position cubes so that cube 0 (timestamp=0) is AT the focal point
                // Future cubes (i > 0) go to the RIGHT of focal point
                float cubeX = focalPointLocalX + (i * settings.cubeSpacing);
                Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                cube.transform.localPosition = pos;
                
                preRenderedCubes.Add(cube);
                SetNativeCubeState(cube, i, 0, i);
                
                // Debug first few cubes
                if (i < 5)
                {
                    Debug.Log($"[PitchVisualizer] {gameObject.name} Native cube {i}: pitch={pitchData.frequency:F1}Hz, pos=({pos.x:F2}, {pos.y:F2}), focalPoint={focalPointLocalX:F2}");
                }
            }
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Initial native window created - Cube 0 (timestamp=0) at focal point {focalPointLocalX:F2}");
        
        // Debug log for native recording pitch range
        var pitchesWithFreq = originalNativePitchData.Where(p => p.HasPitch).Select(p => p.frequency);
        if (pitchesWithFreq.Any())
        {
            float minNativePitch = pitchesWithFreq.Min();
            float maxNativePitch = pitchesWithFreq.Max();
            Debug.Log($"[PitchVisualizer] {gameObject.name} Native recording pitch range: {minNativePitch:F1}-{maxNativePitch:F1}Hz (Personal range: {settings.pitchRange.personalMinPitch:F0}-{settings.pitchRange.personalMaxPitch:F0}Hz)");
        }
        else
        {
            Debug.Log($"[PitchVisualizer] {gameObject.name} Native recording contains no pitched audio");
        }
    }
    
    public void UpdateNativeTrackPlayback(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization();
        
        if (preRenderedCubes == null || originalNativePitchData == null) return;
        
        float loopedPlaybackTime = nativeClipDuration > 0 ? playbackTime % nativeClipDuration : playbackTime;
        
        float deltaTime = loopedPlaybackTime - lastPlaybackTime;
        if (deltaTime < 0) deltaTime += nativeClipDuration;
        
        if (deltaTime >= settings.analysisInterval)
        {
            ScrollNativeCubesDiscrete(1);
            AddSimpleNativeCube(loopedPlaybackTime);
            RemoveOffscreenNativeCubes();
            
            int targetIndex = FindIndexByTime(loopedPlaybackTime, originalNativePitchData);
            UpdateAllNativeCubeStates(targetIndex);
            
            lastPlaybackTime = loopedPlaybackTime;
        }
    }
    
    private void UpdateAllNativeCubeStates(int currentPlaybackIndex)
    {
        int focalIndex = GetFocalCubeIndex();
        
        for (int i = 0; i < preRenderedCubes.Count; i++)
        {
            var cube = preRenderedCubes[i];
            if (cube == null) continue;
            
            int globalDataIndex = (visibleCubeStartIndex + i) % originalNativePitchData.Count;
            
            float cubeLocalX = cube.transform.localPosition.x - settings.trackOffset.x;
            int cubeVisualIndex = Mathf.RoundToInt(cubeLocalX / settings.cubeSpacing);
            
            CubeState state;
            if (cubeVisualIndex < focalIndex - 1)
            {
                state = CubeState.Played;
            }
            else if (cubeVisualIndex >= focalIndex - 1 && cubeVisualIndex <= focalIndex + 1)
            {
                state = CubeState.Current;
            }
            else
            {
                state = CubeState.Future;
            }
            
            SetNativeCubeStateByType(cube, globalDataIndex, state);
        }
    }
    
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
    
    private void SetNativeCubeState(GameObject cube, int cubeIndex, int currentPlaybackIndex, int globalDataIndex = -1)
    {
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
    }
    
    private void AddSimpleNativeCube(float loopedPlaybackTime)
    {
        if (originalNativePitchData == null) return;
        
        int newDataIndex = (visibleCubeStartIndex + settings.maxCubes) % originalNativePitchData.Count;
        var newPitchData = originalNativePitchData[newDataIndex];
        
        GameObject newCube = CreateCube(newPitchData, true, settings.maxCubes - 1);
        if (newCube != null)
        {
            // FIXED: Calculate proper right edge position based on current cube count
            // For short audio, this ensures proper spacing without gaps
            int currentCubeCount = Mathf.Min(settings.maxCubes, originalNativePitchData.Count);
            float rightEdgeX = focalPointLocalX + ((currentCubeCount - 1) * settings.cubeSpacing);
            Vector3 pos = new Vector3(rightEdgeX, 0, 0) + settings.trackOffset;
            newCube.transform.localPosition = pos;
            
            preRenderedCubes.Add(newCube);
            SetNativeCubeState(newCube, settings.maxCubes - 1, currentPlaybackIndex, newDataIndex);
            
            // Debug for short audio
            if (originalNativePitchData.Count < settings.maxCubes)
            {
                Debug.Log($"[PitchVisualizer] {gameObject.name} Short audio: Adding cube at dataIndex={newDataIndex}, pos={pos.x:F2}, cubeCount={currentCubeCount}");
            }
        }
        
        visibleCubeStartIndex = (visibleCubeStartIndex + 1) % originalNativePitchData.Count;
    }
    
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
    
    private GameObject CreateCube(PitchDataPoint pitchData, bool isPreRendered, int index = -1)
    {
        if (settings.cubePrefab == null || settings.cubeParent == null) 
            return null;
        
        GameObject newCube = Instantiate(settings.cubePrefab, settings.cubeParent);
        newCube.SetActive(true);
        
        // FIXED: Only position user cubes automatically, pre-rendered cubes are positioned by caller
        if (!isPreRendered)
        {
            Vector3 position = new Vector3(focalPointLocalX, 0, 0) + settings.trackOffset;
            newCube.transform.localPosition = position;
        }
        // Pre-rendered cubes will be positioned by CreateInitialNativeWindow() or AddSimpleNativeCube()
        
        float pitchScale = CalculatePitchScale(pitchData);
        Vector3 scale = settings.cubeScale;
        scale.y = pitchScale;
        newCube.transform.localScale = scale;
        
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
        if (preRenderedCubes == null) return;
        
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
        nativeCubeOffset = 0f;
        visibleCubeStartIndex = 0;
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
        if (activeCubes != null)
        {
            while (activeCubes.Count > 0)
            {
                GameObject cube = activeCubes.Dequeue();
                if (cube != null) DestroyImmediate(cube);
            }
        }
        
        ClearPreRenderedCubes();
        
        if (focalIndicator != null)
        {
            DestroyImmediate(focalIndicator);
            focalIndicator = null;
        }
        
        originalNativePitchData = null;
        nativeClipDuration = 0f;
    }
    
    void OnDestroy()
    {
        ClearAll();
    }
    
    public VisualizationSettings GetSettings()
    {
        return settings;
    }
    
    public void UpdateSettings(VisualizationSettings newSettings)
    {
        settings = newSettings;
        ValidateSettings();
        UpdateFocalPoint();
        CreateFocalIndicator();
    }
}