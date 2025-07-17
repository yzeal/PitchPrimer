using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Modular visualization system for pitch data
// Supports both real-time and pre-rendered visualizations
// Designed for chorusing with dual-track display
// UPDATED: Removed InitialAudioTriggerOffset - only Loop triggers remain

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
    public int maxCubes = 30; // Only used for User Recording
    public Vector3 trackOffset = Vector3.zero;
    
    [Header("Native Track Repetitions")]
    [Tooltip("Number of clip repetitions visible at once (3-10)")]
    public int nativeRepetitions = 5;
    
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
    
    [Header("Audio Loop Trigger Settings")]
    [Tooltip("Cubes before focal point to trigger audio loops")]
    public int loopAudioTriggerOffset = 1;
}

// NEW: Repetition data structure
[System.Serializable]
public class RepetitionData
{
    public List<GameObject> cubes = new List<GameObject>();
    public float startPosition;
    public int repetitionIndex;
    public bool isInSilencePeriod;
    
    public RepetitionData(float startPos, int repIndex)
    {
        startPosition = startPos;
        repetitionIndex = repIndex;
        isInSilencePeriod = false;
    }
}

public class PitchVisualizer : MonoBehaviour
{
    [SerializeField] private VisualizationSettings settings;
    
    // Audio trigger events (OnInitialAudioTrigger removed)
    public System.Action OnAudioLoopTrigger;
    
    // User Recording (unchanged)
    private Queue<GameObject> activeCubes;
    
    // Native Recording - NEW SYSTEM
    private List<RepetitionData> activeRepetitions = new List<RepetitionData>();
    private float repetitionTotalLength; // Audio + Silence duration
    private float scrollSpeed; // Cubes per interval
    private float currentSilenceDuration = 0.6f; // Default fallback
    
    // Statistics for debugging (NO automatic adaptation)
    private List<float> observedPitches = new List<float>();
    private int cubeCreationCount = 0;
    
    // Existing variables (mostly unchanged)
    private bool isNativeTrack = false;
    private float lastPlaybackTime = 0f;
    private List<PitchDataPoint> originalNativePitchData;
    private float nativeClipDuration = 0f;
    private GameObject focalIndicator;
    private float focalPointLocalX = 0f;
    
    // Audio trigger tracking (only for loops now)
    private HashSet<int> triggeredLoops = new HashSet<int>();
    private float totalElapsedCubes = 0f; // Track scrolling progress
    
    // FIXED: Add missing variable for legacy compatibility
    private int currentPlaybackIndex = 0;
    
    // LEGACY: Remove these after testing (keep for now to avoid errors)
    private List<GameObject> preRenderedCubes; // Keep for transition
    
    private enum CubeState
    {
        Played, Current, Future
    }
    
    void Awake()
    { 
        EnsureInitialization();
    }
    
    void Start()
    {
        EnsureInitialization();
        UpdateFocalPoint();
        CreateFocalIndicator();
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
    
    private void UpdateFocalPoint()
    {
        if (settings.focalPointTransform != null && settings.cubeParent != null)
        {
            Vector3 localPos = settings.cubeParent.InverseTransformPoint(settings.focalPointTransform.position);
            focalPointLocalX = localPos.x;
        }
        else
        {
            // FIXED: For repetitions system, use a fixed focal point
            if (isNativeTrack)
            {
                focalPointLocalX = 12f; // Fixed position for native tracks
            }
            else
            {
                focalPointLocalX = (settings.maxCubes * settings.cubeSpacing) * 0.4f; // User tracks
            }
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
    
    public void PreRenderNativeTrack(List<PitchDataPoint> pitchDataList, float silenceDuration = 0.6f)
    {
        EnsureInitialization();
        isNativeTrack = true;
        
        if (pitchDataList == null || pitchDataList.Count == 0)
        {
            Debug.LogWarning($"[PitchVisualizer] {gameObject.name} - No pitch data to pre-render");
            return;
        }
        
        currentSilenceDuration = silenceDuration;
        
        originalNativePitchData = new List<PitchDataPoint>(pitchDataList);
        nativeClipDuration = pitchDataList.Count > 0 ? pitchDataList[pitchDataList.Count - 1].timestamp : 0f;
        
        // FIXED: Use externally provided silence duration
        float silenceCubes = silenceDuration / settings.analysisInterval;
        repetitionTotalLength = (originalNativePitchData.Count + silenceCubes) * settings.cubeSpacing;
        scrollSpeed = settings.cubeSpacing / settings.analysisInterval;
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} REPETITIONS SYSTEM:");
        Debug.Log($"  Audio cubes: {originalNativePitchData.Count}, Silence cubes: {silenceCubes:F0}");
        Debug.Log($"  Silence duration: {silenceDuration:F3}s (external)");
        Debug.Log($"  Repetition length: {repetitionTotalLength:F1} units, Total repetitions: {settings.nativeRepetitions}");
        
        ClearNativeRepetitions();
        CreateInitialRepetitions(silenceDuration);
        
        // Reset audio trigger tracking (only for loops)
        ResetAudioTriggers();
        
        lastPlaybackTime = 0f;
    }
    
    // Reset audio triggers for new sessions (only loop triggers)
    public void ResetAudioTriggers()
    {
        triggeredLoops.Clear();
        totalElapsedCubes = 0f;
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Audio triggers reset");
    }
    
    private void CreateInitialRepetitions(float silenceDuration)
    {
        if (originalNativePitchData == null) return;
        
        activeRepetitions.Clear();
        
        // Create initial repetitions spanning the visible area
        for (int rep = 0; rep < settings.nativeRepetitions; rep++)
        {
            float repStartPos = focalPointLocalX + (rep * repetitionTotalLength);
            CreateSingleRepetition(repStartPos, rep, silenceDuration);
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Created {activeRepetitions.Count} initial repetitions");
    }
    
    private void CreateSingleRepetition(float startPosition, int repetitionIndex, float silenceDuration)
    {
        var repetition = new RepetitionData(startPosition, repetitionIndex);
        
        // Create audio cubes (unchanged)
        for (int i = 0; i < originalNativePitchData.Count; i++)
        {
            var pitchData = originalNativePitchData[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                float cubeX = startPosition + (i * settings.cubeSpacing);
                Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                cube.transform.localPosition = pos;
                
                repetition.cubes.Add(cube);
                SetNativeCubeState(cube, i, 0, i);
            }
        }
        
        // FIXED: Create silence cubes using external silence duration
        int silenceCubeCount = Mathf.RoundToInt(silenceDuration / settings.analysisInterval);
        for (int s = 0; s < silenceCubeCount; s++)
        {
            var silencePitchData = new PitchDataPoint(0f, 0f, 0f, 0f);
            GameObject silenceCube = CreateCube(silencePitchData, true, -1);
            
            if (silenceCube != null)
            {
                float cubeX = startPosition + ((originalNativePitchData.Count + s) * settings.cubeSpacing);
                Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                silenceCube.transform.localPosition = pos;
                
                repetition.cubes.Add(silenceCube);
            }
        }
        
        activeRepetitions.Add(repetition);
    }
    
    public void UpdateNativeTrackPlayback(float playbackTime, List<PitchDataPoint> pitchDataList)
    {
        EnsureInitialization();
        
        if (activeRepetitions == null || originalNativePitchData == null) return;
        
        float loopedPlaybackTime = nativeClipDuration > 0 ? playbackTime % nativeClipDuration : playbackTime;
        
        float deltaTime = loopedPlaybackTime - lastPlaybackTime;
        if (deltaTime < 0) deltaTime += nativeClipDuration;
        
        if (deltaTime >= settings.analysisInterval)
        {
            ScrollAllRepetitions();
            ManageRepetitions();
            UpdateAllRepetitionStates(loopedPlaybackTime);
            
            lastPlaybackTime = loopedPlaybackTime;
        }
    }
    
    private void ScrollAllRepetitions()
    {
        float scrollDistance = settings.cubeSpacing;
        
        foreach (var repetition in activeRepetitions)
        {
            repetition.startPosition -= scrollDistance;
            
            foreach (var cube in repetition.cubes)
            {
                if (cube != null)
                {
                    Vector3 pos = cube.transform.localPosition;
                    pos.x -= scrollDistance;
                    cube.transform.localPosition = pos;
                }
            }
        }
        
        // Update total elapsed and check for audio triggers (only loops)
        totalElapsedCubes += 1f; // One cube scrolled
        CheckForLoopTriggers();
    }
    
    // Check for loop triggers only (no initial trigger)
    private void CheckForLoopTriggers()
    {
        if (!isNativeTrack) return; // Only for native tracks
        
        // Calculate cubes per loop
        float cubesPerLoop = repetitionTotalLength / settings.cubeSpacing;
        
        // Calculate which loop we're approaching (considering the offset)
        float adjustedCubes = totalElapsedCubes + settings.loopAudioTriggerOffset;
        int approachingLoop = Mathf.FloorToInt(adjustedCubes / cubesPerLoop);
        
        // Trigger if we haven't triggered this loop yet and it's not the initial loop
        if (approachingLoop > 0 && !triggeredLoops.Contains(approachingLoop))
        {
            triggeredLoops.Add(approachingLoop);
            OnAudioLoopTrigger?.Invoke();
            Debug.Log($"[PitchVisualizer] {gameObject.name} Loop {approachingLoop} triggered at {totalElapsedCubes:F1} cubes (adjusted: {adjustedCubes:F1})");
        }
    }
    
    private void ManageRepetitions()
    {
        // Remove repetitions that scrolled off-screen (left side)
        float removeThreshold = focalPointLocalX - repetitionTotalLength;
        
        for (int i = activeRepetitions.Count - 1; i >= 0; i--)
        {
            var rep = activeRepetitions[i];
            if (rep.startPosition < removeThreshold)
            {
                // Destroy all cubes in this repetition
                foreach (var cube in rep.cubes)
                {
                    if (cube != null) DestroyImmediate(cube);
                }
                activeRepetitions.RemoveAt(i);
                
                Debug.Log($"[PitchVisualizer] {gameObject.name} Removed repetition {rep.repetitionIndex} at pos {rep.startPosition:F1}");
            }
        }
        
        // Add new repetitions on the right side if needed
        while (activeRepetitions.Count < settings.nativeRepetitions)
        {
            float lastRepEndPos = activeRepetitions.Count > 0 ? 
                activeRepetitions[activeRepetitions.Count - 1].startPosition + repetitionTotalLength :
                focalPointLocalX;
                
            int newRepIndex = activeRepetitions.Count > 0 ? 
                activeRepetitions[activeRepetitions.Count - 1].repetitionIndex + 1 : 0;
                
            CreateSingleRepetition(lastRepEndPos, newRepIndex, currentSilenceDuration);
            
            Debug.Log($"[PitchVisualizer] {gameObject.name} Added repetition {newRepIndex} at pos {lastRepEndPos:F1}");
        }
    }
    
    private void UpdateAllRepetitionStates(float playbackTime)
    {
        int focalIndex = GetFocalCubeIndex();
        
        foreach (var repetition in activeRepetitions)
        {
            for (int i = 0; i < repetition.cubes.Count && i < originalNativePitchData.Count; i++)
            {
                var cube = repetition.cubes[i];
                if (cube == null) continue;
                
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
                
                SetNativeCubeStateByType(cube, i, state);
            }
        }
    }
    
    private void SetNativeCubeStateByType(GameObject cube, int dataIndex, CubeState state)
    {
        var renderer = cube.GetComponent<Renderer>();
        if (renderer == null) return;
        
        // FIXED: Handle silence cubes safely
        Color baseColor;
        if (dataIndex >= 0 && dataIndex < originalNativePitchData.Count)
        {
            var pitchData = originalNativePitchData[dataIndex];
            baseColor = GetCubeColor(pitchData);
        }
        else
        {
            // Silence cube
            baseColor = settings.silenceColor;
        }
        
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
        int dataIndex = globalDataIndex >= 0 ? globalDataIndex : cubeIndex % originalNativePitchData.Count;
        
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
    
    private GameObject CreateCube(PitchDataPoint pitchData, bool isPreRendered, int index = -1)
    {
        if (settings.cubePrefab == null || settings.cubeParent == null) 
            return null;
        
        GameObject newCube = Instantiate(settings.cubePrefab, settings.cubeParent);
        newCube.SetActive(true);
        
        // Only position user cubes automatically, pre-rendered cubes are positioned by caller
        if (!isPreRendered)
        {
            Vector3 position = new Vector3(focalPointLocalX, 0, 0) + settings.trackOffset;
            newCube.transform.localPosition = position;
        }
        
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
        
        // FIXED: Reset variables properly
        currentPlaybackIndex = 0;
        lastPlaybackTime = 0f;
    }
    
    private void ClearNativeRepetitions()
    {
        if (activeRepetitions != null)
        {
            foreach (var repetition in activeRepetitions)
            {
                foreach (var cube in repetition.cubes)
                {
                    if (cube != null) DestroyImmediate(cube);
                }
            }
            activeRepetitions.Clear();
        }
    }
    
    public void ClearAll()
    {
        // User cubes
        if (activeCubes != null)
        {
            while (activeCubes.Count > 0)
            {
                GameObject cube = activeCubes.Dequeue();
                if (cube != null) DestroyImmediate(cube);
            }
        }
        
        // Native repetitions
        ClearNativeRepetitions();
        
        // Legacy cleanup (remove after testing)
        ClearPreRenderedCubes();
        
        if (focalIndicator != null)
        {
            DestroyImmediate(focalIndicator);
            focalIndicator = null;
        }
        
        originalNativePitchData = null;
        nativeClipDuration = 0f;
        
        // Reset audio triggers
        ResetAudioTriggers();
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