using UnityEngine;
using System.Collections.Generic;
using System.Linq; // NEW: Für LINQ-Operationen

// COPILOT CONTEXT: Main controller for chorusing exercises
// Coordinates native recording playbook with user microphone input
// Manages dual-track visualization and synchronization
// UPDATED: InitialAudioDelay system - Audio starts immediately, visual delays

public class ChorusingManager : MonoBehaviour
{
    [Header("Native Recording Data")]
    [SerializeField] private NativeRecording currentRecording; // Ersetzt AudioClip nativeClip
    
    [Header("Components")]
    [SerializeField] private MicAnalysisRefactored micAnalysis;
    [SerializeField] private PitchVisualizer userVisualizer;
    [SerializeField] private PitchVisualizer nativeVisualizer;
    [SerializeField] private AudioSource nativeAudioSource;

    [Header("Analysis Settings")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;
    [SerializeField] private float analysisInterval = 0.1f; // FIXED: Re-added missing field

    [Header("Audio Timing")]
    [Tooltip("Delay before starting cube movement to compensate for Unity audio start delay")]
    [SerializeField] private float initialAudioDelay = 0.5f;
    [Tooltip("Add silence cubes equivalent to InitialAudioDelay to align visual with actual audio")]
    [SerializeField] private bool compensateDelayWithSilence = true;

    [Header("Native Recording Silence")]
    [Tooltip("Requested silence duration between native recording loops (seconds)")]
    [SerializeField] private float requestedSilenceDuration = 0.6f;
    [Tooltip("Actual silence duration (quantized to analysis intervals)")]
    [SerializeField] private float quantizedSilenceDuration = 0f; // Read-only, calculated

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    [Header("User Recording Control")]
    [SerializeField] private UserRecordingInputManager inputManager;
    [SerializeField] private bool enableUserRecordingControl = true;

    private List<PitchDataPoint> nativePitchData;
    private bool isChorusingActive = false;
    private float chorusingStartTime;
    private float audioStartTime;

    // Audio control flags - SIMPLIFIED
    private bool hasAudioStarted = false;

    void Start()
    {
        InitializeComponents();
        
        // NEW: Verwende voranalysierte Daten
        if (currentRecording != null)
        {
            LoadNativeRecording();
        }
    }

    void Update()
    {
        if (isChorusingActive)
        {
            UpdateSimpleVisualization();
        }
    }

    public void StartChorusing()
    {
        if (nativePitchData == null || nativePitchData.Count == 0)
        {
            Debug.LogError("No native recording data available!");
            return;
        }

        // Calculate quantized silence (includes delay compensation)
        CalculateQuantizedSilence();

        // Start microphone analysis
        if (micAnalysis != null)
        {
            micAnalysis.StartAnalysis();
        }

        // Setup visualization with delay compensation
        if (nativeVisualizer != null)
        {
            nativeVisualizer.PreRenderNativeTrack(nativePitchData, quantizedSilenceDuration);
            nativeVisualizer.ResetAudioTriggers();
            
            // Subscribe to ALL audio events (initial AND loops)
            nativeVisualizer.OnAudioLoopTrigger += TriggerAudio;
        }

        // Setup audio source but DON'T start immediately
        if (nativeAudioSource != null)
        {
            nativeAudioSource.clip = currentRecording.AudioClip;
            nativeAudioSource.loop = false;
            // REMOVED: nativeAudioSource.Play(); - Audio starts via events now!
            audioStartTime = Time.time;
            DebugLog("?? Audio configured - waiting for visual trigger");
        }

        isChorusingActive = true;
        chorusingStartTime = Time.time;

        DebugLog($"Chorusing started - using delay cube compensation only");
    }

    // RENAMED: Handle both initial and loop audio
    private void TriggerAudio()
    {
        if (nativeAudioSource != null)
        {
            nativeAudioSource.Play();
            hasAudioStarted = true;
            DebugLog("?? Audio triggered by visualizer (delay-compensated)!");
        }
    }

    // SIMPLIFIED: Remove visual delay logic
    private void UpdateSimpleVisualization()
    {
        if (nativeVisualizer != null && nativePitchData != null)
        {
            // SIMPLIFIED: No more delay logic - visual starts immediately
            float visualElapsedTime = Time.time - chorusingStartTime;
            nativeVisualizer.UpdateNativeTrackPlayback(visualElapsedTime, nativePitchData);
        }
    }

    public void StopChorusing()
    {
        isChorusingActive = false;

        if (micAnalysis != null)
        {
            micAnalysis.StopAnalysis();
        }

        if (nativeAudioSource != null)
        {
            nativeAudioSource.Stop();
        }

        // Unsubscribe from unified audio events
        if (nativeVisualizer != null)
        {
            nativeVisualizer.OnAudioLoopTrigger -= TriggerAudio;
        }

        // Clear visualizations
        if (userVisualizer != null)
        {
            userVisualizer.ClearAll();
        }

        if (nativeVisualizer != null)
        {
            nativeVisualizer.ClearAll();
        }

        hasAudioStarted = false;
        DebugLog("Chorusing stopped!");
    }

    // KEPT: Quantized silence calculation (still needed for visual sync)
    // ENHANCED: Quantized silence calculation with delay compensation
    private void CalculateQuantizedSilence()
    {
        if (currentRecording?.AudioClip == null || nativePitchData == null)
        {
            DebugLog("No native clip or pitch data available for quantization calculation");
            quantizedSilenceDuration = 0f;
            return;
        }

        int actualAudioCubes = nativePitchData.Count;
        int requestedSilenceCubes = Mathf.RoundToInt(requestedSilenceDuration / analysisInterval);
        
        // NEW: Add delay compensation cubes SEPARATELY
        int delayCubes = 0;
        if (compensateDelayWithSilence && initialAudioDelay > 0f)
        {
            delayCubes = Mathf.RoundToInt(initialAudioDelay / analysisInterval);
            DebugLog($"DELAY COMPENSATION: Adding {delayCubes} delay cubes for {initialAudioDelay:F3}s");
        }
        
        if (requestedSilenceCubes == 0)
        {
            requestedSilenceCubes = 1;
            DebugLog("ANTI-DRIFT: Minimum 1 silence cube enforced");
        }
        
        // Calculate total silence including both types
        int totalSilenceCubes = requestedSilenceCubes + delayCubes;
        int totalCubes = actualAudioCubes + totalSilenceCubes;
        float visualAudioTime = actualAudioCubes * analysisInterval;
        float visualTotalTime = totalCubes * analysisInterval;
        float actualSilenceNeeded = visualTotalTime - currentRecording.AudioClip.length;
        quantizedSilenceDuration = actualSilenceNeeded;
        
        if (quantizedSilenceDuration < 0f)
        {
            quantizedSilenceDuration += analysisInterval;
            totalCubes += 1;
            totalSilenceCubes += 1;
            visualTotalTime = totalCubes * analysisInterval;
        }
        
        DebugLog($"QUANTIZATION: Audio={actualAudioCubes}, Requested={requestedSilenceCubes}, Delay={delayCubes}, Total={visualTotalTime:F3}s");
    }

    public void SetSilenceDuration(float newSilenceDuration)
    {
        requestedSilenceDuration = newSilenceDuration;
        
        if (isChorusingActive)
        {
            CalculateQuantizedSilence();
            DebugLog($"Silence updated during playback to: {quantizedSilenceDuration:F3}s");
        }
    }

    public void SetInitialAudioDelay(float newDelay)
    {
        initialAudioDelay = newDelay;
        DebugLog($"Initial audio delay set to: {initialAudioDelay:F3}s");
    }

    private void InitializeComponents()
    {
        // Setup Event-Integration mit MicAnalysisRefactored
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected += OnUserPitchDetected;
            DebugLog("Subscribed to MicAnalysisRefactored events");
        }
        else
        {
            Debug.LogError("MicAnalysisRefactored component not assigned!");
        }

        // Setup AudioSource
        if (nativeAudioSource == null)
        {
            nativeAudioSource = gameObject.AddComponent<AudioSource>();
        }
        nativeAudioSource.playOnAwake = false;
        
        InitializeInputManager();
        
        DebugLog("ChorusingManager components initialized");
    }

    private void InitializeInputManager()
    {
        if (inputManager != null && enableUserRecordingControl)
        {
            // Input manager will automatically control user visualizer visibility
            DebugLog("User recording input control enabled");
        }
        else if (enableUserRecordingControl)
        {
            Debug.LogWarning("[ChorusingManager] UserRecordingInputManager not found! User recording control disabled.");
        }
    }

    // Ersetzt PreAnalyzeNativeRecording()
    private void LoadNativeRecording()
    {
        if (currentRecording == null || !currentRecording.IsValid())
        {
            Debug.LogError("No valid native recording assigned!");
            return;
        }
        
        DebugLog($"Loading native recording: {currentRecording.RecordingName}");
        DebugLog($"Speaker: {currentRecording.GetSpeakerInfo()}");
        DebugLog($"Text: {currentRecording.KanjiText}");
        
        // Setup AudioSource
        if (nativeAudioSource != null)
        {
            nativeAudioSource.clip = currentRecording.AudioClip;
        }
        
        // Use cached pitch data - NO analysis needed!
        nativePitchData = currentRecording.GetPitchData(analysisInterval); // FIXED: Pass analysisInterval
        
        if (nativePitchData != null && nativePitchData.Count > 0)
        {
            DebugLog($"Loaded {nativePitchData.Count} cached pitch data points");
            DebugLog($"Pitch range: {currentRecording.PitchRangeMin:F1}Hz - {currentRecording.PitchRangeMax:F1}Hz");
        }
        else
        {
            Debug.LogWarning("No pitch data available - recording may need analysis");
        }
    }

    // NEW: Speaker filtering support
    public bool MatchesSpeakerFilter(NativeRecordingGender? genderFilter = null, string speakerFilter = null)
    {
        if (currentRecording == null) return false;
        
        if (genderFilter.HasValue && currentRecording.SpeakerGender != genderFilter.Value)
            return false;
            
        if (!string.IsNullOrEmpty(speakerFilter) && 
            !currentRecording.SpeakerName.Contains(speakerFilter, System.StringComparison.OrdinalIgnoreCase))
            return false;
            
        return true;
    }

    private void OnUserPitchDetected(PitchDataPoint pitchData)
    {
        if (isChorusingActive && userVisualizer != null)
        {
            userVisualizer.AddRealtimePitchData(pitchData);
            
            if (enableDebugLogging && pitchData.HasPitch)
            {
                DebugLog($"User pitch: {pitchData.frequency:F1}Hz at {pitchData.timestamp:F2}s");
            }
        }
    }

    // Public Methods für UI
    public void SetNativeRecording(NativeRecording recording)
    {
        currentRecording = recording;
        LoadNativeRecording();
    }

    public VisualizationSettings GetUserVisualizationSettings()
    {
        return userVisualizer != null ? userVisualizer.GetSettings() : null;
    }

    public VisualizationSettings GetNativeVisualizationSettings()
    {
        return nativeVisualizer != null ? nativeVisualizer.GetSettings() : null;
    }

    // Public getters
    public bool IsChorusingActive => isChorusingActive;
    public bool HasAudioStarted => hasAudioStarted;
    public float GetNativePlaybackTime() => nativeAudioSource != null ? nativeAudioSource.time : 0f;
    public List<PitchDataPoint> GetNativePitchData() => nativePitchData;
    public float GetQuantizedSilenceDuration() => quantizedSilenceDuration;
    public float GetInitialAudioDelay() => initialAudioDelay;
    public NativeRecording GetCurrentRecording() => currentRecording; // NEW: Public getter

    // NEW: Get delay cube count for PitchVisualizer
    public int GetDelayCubeCount()
    {
        if (!compensateDelayWithSilence || initialAudioDelay <= 0f)
            return 0;
        
        return Mathf.RoundToInt(initialAudioDelay / analysisInterval);
    }

    // NEW: Get compensation enabled flag
    public bool IsDelayCompensationEnabled()
    {
        return compensateDelayWithSilence;
    }

    // Debug Methods
    private void DebugLog(string message)
    {
    if (enableDebugLogging)
    {
        Debug.Log($"[ChorusingManager] {message}");
    }
    }

    void OnDestroy()
    {
        // IMPORTANT: Event-Subscription cleanup
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected -= OnUserPitchDetected;
        }
        
        if (nativeVisualizer != null)
        {
            nativeVisualizer.OnAudioLoopTrigger -= TriggerAudio; // Unified cleanup
        }
    }


}