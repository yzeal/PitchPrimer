using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ============================================================================
// AUDIO TRIGGER SYSTEM DOCUMENTATION
// ============================================================================
// 
// CRITICAL: Audio triggers IMMEDIATELY when delay cubes reach the focal point!
// The "delay" is for VISUAL compensation only, NOT for audio timing.
//
// EVENT FLOW:
// 1. Loop 0 (Initial): Audio triggers when FIRST delay cube reaches focal point
//    - At totalElapsedCubes = 0 (immediate start)
//    - If no initial delay: triggers when first audio cube reaches focal point
//
// 2. Loop 1+ (Subsequent): Audio triggers when FIRST loop delay cube reaches focal point  
//    - After silence period of previous repetition ends
//    - If no loop delay: triggers when first audio cube of new repetition reaches focal point
//
// DELAY CUBE PURPOSE:
// - Initial Delay Cubes: Compensate Unity audio start latency (visual sync)
// - Loop Delay Cubes: Compensate Unity audio loop latency (visual sync)
// - These cubes ensure audio and visuals stay synchronized
// - Audio itself starts IMMEDIATELY without additional delay
//
// TRIGGER TIMING:
// - Audio triggers when relevant cubes ARRIVE at focal point
// - NOT after they pass through or finish
// - The delay compensation is built into the visual timeline
// ============================================================================

// COPILOT CONTEXT: Modular visualization system for pitch data
// Supports both real-time and pre-rendered visualizations
// Designed for chorusing with dual-track display
// UPDATED: Added static display capabilities for scoring screen

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
    // REMOVED: loopAudioTriggerOffset is no longer needed with delay cube system
    
    // NEW: User Recording Visibility
    [Header("User Recording Visibility")]
    [Tooltip("User cubes start invisible until recording input is pressed")]
    public bool enableVisibilityControl = true;
    [Tooltip("Alpha value for invisible user cubes (0 = fully invisible)")]
    public float invisibleAlpha = 0f;
    [Tooltip("Alpha value for visible user cubes")]
    public float visibleAlpha = 1f;
    [Tooltip("Transition speed between invisible and visible states")]
    public float visibilityTransitionSpeed = 5f;
    
    [Header("Debug Visualization")]
    [Tooltip("Show delay compensation cubes with special coloring (for testing only)")]
    public bool showDelayCompensationCubes = true;
    [Tooltip("Color for delay compensation cubes")]
    public Color delayCompensationColor = new Color(0.2f, 0.4f, 0.8f, 0.3f); // Light blue, transparent
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
    [SerializeField] private bool isNativeTrack = false;
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
    
    // NEW: User recording visibility control
    private bool userRecordingVisible = false;
    private bool isTransitioningVisibility = false;
    
    // NEW: Static display cubes for scoring screen
    private List<GameObject> staticDisplayCubes = new List<GameObject>();
    
    // NEW: Delay cube tracking
    private int initialDelayCubeCount = 0;
    private int loopDelayCubeCount = 0;
    private bool delayCompensationEnabled = false;

    private enum CubeState
    {
        Played, Current, Future
    }
    
    void Awake()
    { 
        EnsureInitialization();
        
        // NEW: Initialize visibility control
        InitializeVisibilityControl();
    }
    
    void Start()
    {
        EnsureInitialization();
        UpdateFocalPoint();
        CreateFocalIndicator();
    }

    void Update()
    {
        // Existing update logic...

        // NEW: Handle visibility transitions
        if (isTransitioningVisibility)
        {
            UpdateCubeVisibility();
        }
    }

    // NEW: Static display method for scoring screen
    public void DisplayStaticPitchData(List<PitchDataPoint> pitchData, int maxDisplayPoints = 100)
    {
        if (pitchData == null || pitchData.Count == 0)
        {
            Debug.LogWarning($"[PitchVisualizer] {gameObject.name}: No pitch data provided for static display");
            return;
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name}: Creating static display with {pitchData.Count} points");
        
        // Clear existing visualization
        ClearAll();
        
        // Sample data if needed for performance
        var displayData = pitchData;
        if (pitchData.Count > maxDisplayPoints)
        {
            displayData = SampleDataForDisplay(pitchData, maxDisplayPoints);
            Debug.Log($"[PitchVisualizer] {gameObject.name}: Sampled {pitchData.Count} ? {displayData.Count} points");
        }
        
        // Create static cubes
        CreateStaticCubes(displayData);
    }
    
    private List<PitchDataPoint> SampleDataForDisplay(List<PitchDataPoint> originalData, int maxPoints)
    {
        var sampled = new List<PitchDataPoint>();
        float step = (float)originalData.Count / maxPoints;
        
        for (int i = 0; i < maxPoints; i++)
        {
            int index = Mathf.RoundToInt(i * step);
            if (index < originalData.Count)
            {
                sampled.Add(originalData[index]);
            }
        }
        
        return sampled;
    }
    
    private void CreateStaticCubes(List<PitchDataPoint> pitchData)
    {
        if (settings.cubePrefab == null || settings.cubeParent == null)
        {
            Debug.LogError($"[PitchVisualizer] {gameObject.name}: Missing cube prefab or parent for static display");
            return;
        }
        
        // Clear any existing static cubes
        ClearStaticCubes();
        
        float totalWidth = pitchData.Count * settings.cubeSpacing;
        float startX = -totalWidth / 2f; // Center the display
        
        for (int i = 0; i < pitchData.Count; i++)
        {
            var pitchPoint = pitchData[i];
            GameObject cube = CreateCube(pitchPoint, true, i);
            
            if (cube != null)
            {
                // Position cubes in a line from left to right
                float cubeX = startX + (i * settings.cubeSpacing);
                Vector3 position = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                cube.transform.localPosition = position;
                
                // Set standard appearance (no state-based modifications)
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color cubeColor = GetCubeColor(pitchPoint);
                    cubeColor.a = 1f; // Full opacity for static display
                    renderer.material.color = cubeColor;
                }
                
                // Add to static display list for cleanup
                staticDisplayCubes.Add(cube);
            }
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name}: Created {pitchData.Count} static cubes");
    }
    
    private void ClearStaticCubes()
    {
        foreach (var cube in staticDisplayCubes)
        {
            if (cube != null)
            {
                DestroyImmediate(cube);
            }
        }
        staticDisplayCubes.Clear();
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
        
        // NEW: Use PersonalPitchRange for color mapping instead of legacy frequency range
        if (settings.useHSVColorMapping)
        {
            // Clamp pitch to personal range for consistent color mapping
            float clampedPitch = Mathf.Clamp(pitch, range.personalMinPitch, range.personalMaxPitch);
            
            // Map personal pitch range to color spectrum (0.0 to 0.8 on hue wheel)
            float normalizedPitch = (clampedPitch - range.personalMinPitch) / (range.personalMaxPitch - range.personalMinPitch);
            normalizedPitch = Mathf.Clamp01(normalizedPitch);
            
            // Convert to HSV: 0.0 = red (low pitch), 0.8 = purple (high pitch)
            // This gives a nice progression: red ? orange ? yellow ? green ? cyan ? blue ? purple
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
        
        if (staticDisplayCubes == null)
        {
            staticDisplayCubes = new List<GameObject>();
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
        
        // NEW: Get both delay types from ChorusingManager
        var chorusingManager = FindFirstObjectByType<ChorusingManager>();
        if (chorusingManager != null)
        {
            initialDelayCubeCount = chorusingManager.GetInitialDelayCubeCount();
            loopDelayCubeCount = chorusingManager.GetLoopDelayCubeCount();
            delayCompensationEnabled = chorusingManager.IsDelayCompensationEnabled();
        }
        else
        {
            initialDelayCubeCount = 0;
            loopDelayCubeCount = 0;
            delayCompensationEnabled = false;
        }
        
        originalNativePitchData = new List<PitchDataPoint>(pitchDataList);
        nativeClipDuration = pitchDataList.Count > 0 ? pitchDataList[pitchDataList.Count - 1].timestamp : 0f;
        
        // Calculate total length using MAXIMUM delay cubes for consistent spacing
        int maxDelayCubes = Mathf.Max(initialDelayCubeCount, loopDelayCubeCount);
        float totalSilenceCubes = silenceDuration / settings.analysisInterval;
        repetitionTotalLength = (originalNativePitchData.Count + totalSilenceCubes) * settings.cubeSpacing;
        scrollSpeed = settings.cubeSpacing / settings.analysisInterval;
        
        // ENHANCED DEBUG: Show repetition length calculation
        Debug.Log($"[PitchVisualizer] REPETITION LENGTH DEBUG:");
        Debug.Log($"  originalNativePitchData.Count: {originalNativePitchData.Count}");
        Debug.Log($"  totalSilenceCubes: {totalSilenceCubes:F1}");
        Debug.Log($"  maxDelayCubes: {maxDelayCubes}");
        Debug.Log($"  settings.cubeSpacing: {settings.cubeSpacing:F3}");
        Debug.Log($"  repetitionTotalLength: {repetitionTotalLength:F1} units");
        Debug.Log($"  Expected cube counts: Rep0={originalNativePitchData.Count + totalSilenceCubes + initialDelayCubeCount}, Rep1+={originalNativePitchData.Count + totalSilenceCubes + loopDelayCubeCount}");
        
        ClearNativeRepetitions();
        CreateInitialRepetitions(silenceDuration);
        
        // Reset audio trigger tracking
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
        
        // ENHANCED DEBUG: Show positioning for each repetition
        Debug.Log($"[PitchVisualizer] POSITIONING DEBUG for {settings.nativeRepetitions} repetitions:");
        
        // Create initial repetitions with CORRECT individual positioning
        for (int rep = 0; rep < settings.nativeRepetitions; rep++)
        {
            float repStartPos;
            
            if (rep == 0)
            {
                // First repetition starts at focal point
                repStartPos = focalPointLocalX;
            }
            else
            {
                // Subsequent repetitions: Calculate ACTUAL end position of previous repetition
                var previousRep = activeRepetitions[rep - 1];
                int previousDelayCubes = (rep - 1 == 0) ? initialDelayCubeCount : loopDelayCubeCount;
                int previousSilenceCubes = Mathf.RoundToInt(silenceDuration / settings.analysisInterval);
                int previousTotalCubes = previousDelayCubes + originalNativePitchData.Count + previousSilenceCubes;
                
                repStartPos = previousRep.startPosition + (previousTotalCubes * settings.cubeSpacing);
            }
            
            // ENHANCED DEBUG: Show exact positioning
            Debug.Log($"  Rep {rep}: startPos={repStartPos:F1}");
            if (rep > 0)
            {
                int currentDelayCubes = (rep == 0) ? initialDelayCubeCount : loopDelayCubeCount;
                int currentSilenceCubes = Mathf.RoundToInt(silenceDuration / settings.analysisInterval);
                int currentTotalCubes = currentDelayCubes + originalNativePitchData.Count + currentSilenceCubes;
                Debug.Log($"    Expected length: {currentTotalCubes} cubes = {currentTotalCubes * settings.cubeSpacing:F1} units");
                Debug.Log($"    Will end at: {repStartPos + (currentTotalCubes * settings.cubeSpacing):F1}");
            }
            
            CreateSingleRepetition(repStartPos, rep, silenceDuration);
        }
        
        Debug.Log($"[PitchVisualizer] {gameObject.name} Created {activeRepetitions.Count} initial repetitions with correct positioning");
    }
    
    private void CreateSingleRepetition(float startPosition, int repetitionIndex, float silenceDuration)
    {
        var repetition = new RepetitionData(startPosition, repetitionIndex);
        
        // NEW: Use different delay cube counts based on repetition type
        int delayCubeCount = (repetitionIndex == 0) ? initialDelayCubeCount : loopDelayCubeCount;
        
        // Create delay cubes FIRST (if compensation enabled)
        if (delayCompensationEnabled && delayCubeCount > 0)
        {
            for (int d = 0; d < delayCubeCount; d++)
            {
                var delaySilenceData = new PitchDataPoint(0f, 0f, 0f, 0f);
                GameObject delayCube = CreateCube(delaySilenceData, true, -2); // Special index for delay cubes
                
                if (delayCube != null)
                {
                    float cubeX = startPosition + (d * settings.cubeSpacing);
                    Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                    delayCube.transform.localPosition = pos;
                    
                    // Visual distinction between initial and loop delay cubes
                    if (settings.showDelayCompensationCubes)
                    {
                        var renderer = delayCube.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            Color delayColor = (repetitionIndex == 0) ?
                                settings.delayCompensationColor : // Initial = blue
                                new Color(0.8f, 0.4f, 0.2f, 0.3f); // Loop = orange
                            renderer.material.color = delayColor;
                        }
                        
                        delayCube.name = $"{(repetitionIndex == 0 ? "InitialDelay" : "LoopDelay")}Cube_{d}";
                    }
                    
                    repetition.cubes.Add(delayCube);
                }
            }
        }
        
        // Create audio cubes (offset by current delay cubes)
        int audioOffset = (delayCompensationEnabled && delayCubeCount > 0) ? delayCubeCount : 0;
        for (int i = 0; i < originalNativePitchData.Count; i++)
        {
            var pitchData = originalNativePitchData[i];
            GameObject cube = CreateCube(pitchData, true, i);
            
            if (cube != null)
            {
                float cubeX = startPosition + ((audioOffset + i) * settings.cubeSpacing);
                Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                cube.transform.localPosition = pos;
                
                repetition.cubes.Add(cube);
                SetNativeCubeState(cube, i, 0, i);
            }
        }
        
        // FIXED: Create regular silence cubes (NO double subtraction!)
        int regularSilenceCubes = Mathf.RoundToInt(silenceDuration / settings.analysisInterval);
        
        // REMOVED: Double subtraction bug - silenceDuration already accounts for delay cubes!
        // if (delayCompensationEnabled && delayCubeCount > 0)
        // {
        //     regularSilenceCubes -= delayCubeCount; // BUG: Already subtracted in ChorusingManager!
        // }
        
        // ENHANCED DEBUG: Show what's happening
        Debug.Log($"SILENCE DEBUG: silenceDuration={silenceDuration:F3}s, analysisInterval={settings.analysisInterval:F3}s");
        Debug.Log($"  regularSilenceCubes calculation: {silenceDuration:F3} ÷ {settings.analysisInterval:F3} = {regularSilenceCubes}");
        Debug.Log($"  delayCubeCount: {delayCubeCount}");
        Debug.Log($"  FIXED: No double subtraction - silence cubes stay consistent at {regularSilenceCubes}");
        
        for (int s = 0; s < regularSilenceCubes; s++)
        {
            var silencePitchData = new PitchDataPoint(0f, 0f, 0f, 0f);
            GameObject silenceCube = CreateCube(silencePitchData, true, -1);
            
            if (silenceCube != null)
            {
                float cubeX = startPosition + ((audioOffset + originalNativePitchData.Count + s) * settings.cubeSpacing);
                Vector3 pos = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                silenceCube.transform.localPosition = pos;
                
                repetition.cubes.Add(silenceCube);
            }
        }
        
        activeRepetitions.Add(repetition);
        
        string delayType = (repetitionIndex == 0) ? "initial" : "loop";
        DebugLog($"Created repetition {repetitionIndex}: {delayCubeCount} {delayType} delay + {originalNativePitchData.Count} audio + {regularSilenceCubes} silence cubes");
    }
    
    // ============================================================================
    // AUDIO TRIGGER LOGIC - FINAL FIX
    // ============================================================================
    // CRITICAL: Must match EXACTLY what CreateSingleRepetition() actually creates!
    // ============================================================================
    private void CheckForLoopTriggers()
    {
        if (!isNativeTrack) return;
        
        // Calculate which loop we should be approaching based on elapsed visual time
        int approachingLoop = GetApproachingLoopNumber(totalElapsedCubes);
        
        // Calculate when this specific loop should trigger
        float targetTriggerPoint = CalculateTriggerPointForLoop(approachingLoop);
        
        // DEBUG: Show trigger calculation details (reduced frequency)
        if (totalElapsedCubes > 0 && Mathf.FloorToInt(totalElapsedCubes) % 10 == 0) // Every 10 cubes
        {
            Debug.Log($"?? TRIGGER DEBUG (Cube {totalElapsedCubes:F1}):");
            Debug.Log($"  approachingLoop: {approachingLoop}");
            Debug.Log($"  targetTriggerPoint for Loop {approachingLoop}: {targetTriggerPoint:F1}");
            Debug.Log($"  totalElapsedCubes: {totalElapsedCubes:F1}");
            Debug.Log($"  Should trigger? {totalElapsedCubes >= targetTriggerPoint && !triggeredLoops.Contains(approachingLoop)}");
            Debug.Log($"  triggeredLoops: [{string.Join(", ", triggeredLoops)}]");
            
            // ENHANCED: Show what's actually being created
            Debug.Log($"  ACTUAL CUBE COMPOSITION:");
            Debug.Log($"    Loop 0: {GetActualDelayCubes(0)} delay + {originalNativePitchData?.Count ?? 0} audio + {GetActualSilenceCubes()} silence = {GetActualRepetitionCubes(0)} total cubes");
            Debug.Log($"    Loop 1+: {GetActualDelayCubes(1)} delay + {originalNativePitchData?.Count ?? 0} audio + {GetActualSilenceCubes()} silence = {GetActualRepetitionCubes(1)} total cubes");
        }
        
        // FIXED: Audio triggers when first relevant cube reaches focal point
        if (totalElapsedCubes >= targetTriggerPoint && !triggeredLoops.Contains(approachingLoop))
        {
            triggeredLoops.Add(approachingLoop);
            
            // IMMEDIATE AUDIO START: No additional delay, audio plays now!
            OnAudioLoopTrigger?.Invoke();
            
            string eventType = approachingLoop == 0 ? "INITIAL" : $"LOOP {approachingLoop}";
            Debug.Log($"?? [PitchVisualizer] *** {gameObject.name} {eventType} AUDIO TRIGGERED ***");
            Debug.Log($"  IMMEDIATE START: Audio plays now at totalElapsedCubes: {totalElapsedCubes:F1}");
            Debug.Log($"  Target trigger point was: {targetTriggerPoint:F1}");
            Debug.Log($"  Using delay type: {(approachingLoop == 0 ? "INITIAL" : "LOOP")}");
            Debug.Log($"  Visual compensation ensures sync with scrolling cubes");
        }
    }

    // FIXED: Calculate trigger points based on ACTUAL cube creation
    private float CalculateTriggerPointForLoop(int loopNumber)
    {
        if (loopNumber == 0)
        {
            // LOOP 0 (Initial): Trigger immediately when visualization starts
            return 0f; // Immediate trigger at start
        }
        else
        {
            // LOOP 1+ (Subsequent): Calculate cumulative length based on ACTUAL cube counts
            
            // Start with ACTUAL Loop 0 length
            float cumulativeLength = GetActualRepetitionCubes(0) * settings.cubeSpacing;
            
            // Add ACTUAL lengths for Loop 1 through (loopNumber-1)
            if (loopNumber > 1)
            {
                float rep1PlusLength = GetActualRepetitionCubes(1) * settings.cubeSpacing;
                cumulativeLength += (loopNumber - 1) * rep1PlusLength;
            }
            
            return cumulativeLength;
        }
    }

    // NEW: Get ACTUAL delay cubes for specific repetition (matches CreateSingleRepetition exactly)
    private int GetActualDelayCubes(int repetitionIndex)
    {
        if (!delayCompensationEnabled) return 0;
        return (repetitionIndex == 0) ? initialDelayCubeCount : loopDelayCubeCount;
    }

    // NEW: Get ACTUAL silence cubes (matches CreateSingleRepetition exactly)
    private int GetActualSilenceCubes()
    {
        return Mathf.RoundToInt(currentSilenceDuration / settings.analysisInterval);
    }

    // NEW: Get ACTUAL total cubes for specific repetition (matches CreateSingleRepetition exactly)
    private int GetActualRepetitionCubes(int repetitionIndex)
    {
        int delayCubes = GetActualDelayCubes(repetitionIndex);
        int audioCubes = originalNativePitchData?.Count ?? 0;
        int silenceCubes = GetActualSilenceCubes();
        
        return delayCubes + audioCubes + silenceCubes;
    }
    
    // UPDATED: Use actual cube counts (keep for compatibility but delegate to actual methods)
    private float GetRepetitionLength(int repetitionIndex)
    {
        return GetActualRepetitionCubes(repetitionIndex) * settings.cubeSpacing;
    }
    
    // FIXED: Use actual cube counts for loop determination
    private int GetApproachingLoopNumber(float elapsedCubes)
    {
        // Check if we're still in Loop 0 (which has different length)
        float rep0LengthInCubes = GetActualRepetitionCubes(0);
        
        if (elapsedCubes < rep0LengthInCubes)
        {
            return 0; // Still in first repetition
        }
        
        // We're past Loop 0, now calculate which subsequent loop we're in
        float remainingCubes = elapsedCubes - rep0LengthInCubes;
        float rep1PlusLengthInCubes = GetActualRepetitionCubes(1);
        
        if (rep1PlusLengthInCubes <= 0)
        {
            Debug.LogWarning($"[PitchVisualizer] Invalid rep1PlusLengthInCubes: {rep1PlusLengthInCubes}");
            return 1; // Fallback
        }
        
        // Loop 1, 2, 3, etc. all have uniform rep1PlusLengthInCubes
        int subsequentLoopNumber = Mathf.FloorToInt(remainingCubes / rep1PlusLengthInCubes);
        
        return 1 + subsequentLoopNumber;
    }
    
    private void ManageRepetitions()
    {
        // NEW: Remove repetitions that are 2+ repetitions behind the focal point
        float extendedRemoveThreshold = focalPointLocalX - (2.0f * repetitionTotalLength);
        
        for (int i = activeRepetitions.Count - 1; i >= 0; i--)
        {
            var rep = activeRepetitions[i];
            if (rep.startPosition < extendedRemoveThreshold)
            {
                // Destroy all cubes in this repetition
                foreach (var cube in rep.cubes)
                {
                    if (cube != null) DestroyImmediate(cube);
                }
                activeRepetitions.RemoveAt(i);
                
                Debug.Log($"[PitchVisualizer] {gameObject.name} Removed repetition {rep.repetitionIndex} at pos {rep.startPosition:F1} (extended threshold: {extendedRemoveThreshold:F1})");
            }
        }
        
        // FIXED: Add new repetitions with CORRECT positioning
        while (activeRepetitions.Count < settings.nativeRepetitions)
        {
            float lastRepEndPos;
            int newRepIndex;
            
            if (activeRepetitions.Count > 0)
            {
                // Calculate ACTUAL end position of last repetition
                var lastRep = activeRepetitions[activeRepetitions.Count - 1];
                int lastRepDelayCubes = (lastRep.repetitionIndex == 0) ? initialDelayCubeCount : loopDelayCubeCount;
                int lastRepSilenceCubes = Mathf.RoundToInt(currentSilenceDuration / settings.analysisInterval);
                int lastRepTotalCubes = lastRepDelayCubes + originalNativePitchData.Count + lastRepSilenceCubes;
                
                lastRepEndPos = lastRep.startPosition + (lastRepTotalCubes * settings.cubeSpacing);
                newRepIndex = lastRep.repetitionIndex + 1;
                
                Debug.Log($"[PitchVisualizer] DYNAMIC POSITIONING: Last rep {lastRep.repetitionIndex} ends at {lastRepEndPos:F1}");
            }
            else
            {
                lastRepEndPos = focalPointLocalX;
                newRepIndex = 0;
            }
            
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
    
    // NEW: User recording visibility control
    private void InitializeVisibilityControl()
    {
        if (!settings.enableVisibilityControl || isNativeTrack)
            return;
            
        // Start with invisible user cubes
        userRecordingVisible = false;
        
        // Find and subscribe to input manager
        var inputManager = FindFirstObjectByType<UserRecordingInputManager>();
        if (inputManager != null)
        {
            inputManager.OnRecordingStarted += ShowUserRecording;
            inputManager.OnRecordingStopped += HideUserRecording;
            
            Debug.Log("Subscribed to recording input events");
        }
        else
        {
            Debug.LogWarning("[PitchVisualizer] UserRecordingInputManager not found! User recording visibility control disabled.");
        }
    }
    
    // NEW: Show user recording cubes
    private void ShowUserRecording()
    {
        if (!settings.enableVisibilityControl || isNativeTrack)
            return;
            
        userRecordingVisible = true;
        isTransitioningVisibility = true;
        
        Debug.Log("User recording cubes becoming visible");
    }
    
    // NEW: Hide user recording cubes
    private void HideUserRecording()
    {
        if (!settings.enableVisibilityControl || isNativeTrack)
            return;
            
        userRecordingVisible = false;
        isTransitioningVisibility = true;
        
        Debug.Log("User recording cubes becoming invisible");
    }
    
    // NEW: Update cube visibility (call this in Update() if transitioning)
    private void UpdateCubeVisibility()
    {
        if (!isTransitioningVisibility || activeCubes == null || isNativeTrack)
            return;
            
        float targetAlpha = userRecordingVisible ? settings.visibleAlpha : settings.invisibleAlpha;
        bool transitionComplete = true;
        
        foreach (GameObject cube in activeCubes)
        {
            if (cube == null) continue;
            
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color currentColor = renderer.material.color;
                float newAlpha = Mathf.MoveTowards(currentColor.a, targetAlpha, 
                    settings.visibilityTransitionSpeed * Time.deltaTime);
                
                currentColor.a = newAlpha;
                renderer.material.color = currentColor;
                
                // Check if transition is complete
                if (Mathf.Abs(newAlpha - targetAlpha) > 0.01f)
                {
                    transitionComplete = false;
                }
            }
        }
        
        // Stop transitioning when all cubes reach target alpha
        if (transitionComplete)
        {
            isTransitioningVisibility = false;
            Debug.Log($"Visibility transition complete - Alpha: {targetAlpha:F2}");
        }
    }
    
    // NEW: Apply initial visibility to newly created user cubes
    private void ApplyInitialVisibility(GameObject cube)
    {
        if (!settings.enableVisibilityControl || isNativeTrack || cube == null)
            return;
            
        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color cubeColor = renderer.material.color;
            cubeColor.a = userRecordingVisible ? settings.visibleAlpha : settings.invisibleAlpha;
            renderer.material.color = cubeColor;
        }
    }
    
    // MODIFIED: Update the existing CreateCube method to apply initial visibility
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
        
        // Apply colors and visibility
        var renderer = newCube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color cubeColor;
            
            // NEW: Special handling for delay cubes (testing visualization)
            if (index == -2 && settings.showDelayCompensationCubes)
            {
                cubeColor = settings.delayCompensationColor;
                
                // Optional: Add name for easier identification in hierarchy
                newCube.name = $"DelayCube_{Time.time:F1}";
            }
            else
            {
                cubeColor = GetCubeColor(pitchData);
            }
            
            renderer.material.color = cubeColor;
            
            // Apply initial visibility for user cubes
            if (!isPreRendered)
            {
                ApplyInitialVisibility(newCube);
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
        
        // Static display cubes
        ClearStaticCubes();
        
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
    
    // NEW: Cleanup method for OnDestroy()
    private void CleanupVisibilityControl()
    {
        var inputManager = FindFirstObjectByType<UserRecordingInputManager>();
        if (inputManager != null)
        {
            inputManager.OnRecordingStarted -= ShowUserRecording;
            inputManager.OnRecordingStopped -= HideUserRecording;
        }
    }
    
    void OnDestroy()
    {
        ClearAll();
        
        // NEW: Cleanup visibility control
        CleanupVisibilityControl();
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

    // ADD: Missing UpdateNativeTrackPlayback method
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

    // ADD: Missing ScrollAllRepetitions method
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

    // ADD: Missing DebugLog method
    private void DebugLog(string message)
    {
        Debug.Log($"[PitchVisualizer] {gameObject.name}: {message}");
    }
}