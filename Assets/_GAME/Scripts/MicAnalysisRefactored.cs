using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ? RECOMMENDED CLASS - Use this for all new development ?
// This is the modern, event-driven replacement for the deprecated MicAnalysis class

// COPILOT CONTEXT: Refactored microphone analysis for chorusing system
// Uses shared PitchAnalyzer core and modular visualization
// Integrates with ChorusingManager for synchronized dual-track display

// MIGRATION FROM MicAnalysis:
// • Replace GetCurrentPitch() calls with OnPitchDetected event subscription
// • Use PitchDataPoint struct instead of raw float values
// • Benefit from advanced pitch range filtering and noise gate improvements
// • Access real-time statistics and voice type presets

[RequireComponent(typeof(AudioSource))]
public class MicAnalysisRefactored : MonoBehaviour
{
    [Header("? MODERN IMPLEMENTATION - Preferred over deprecated MicAnalysis")]
    [Space(10)]
    [Header("Analysis")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;
    [SerializeField] private float analysisInterval = 0.1f;
    
    [Header("Microphone")]
    [SerializeField] private string deviceName;
    [SerializeField] private float minAudioLevel = 0.001f;
    
    [Header("Noise Gate Settings")]
    [SerializeField] private bool enableNoiseGate = true;
    [SerializeField] private float noiseGateMultiplier = 3.0f; // Gate öffnet bei 3x Ambient Level
    [SerializeField] private float ambientCalibrationTime = 2.0f; // Sekunden für Ambient-Messung
    [SerializeField] private float ambientSamplePercentage = 0.7f; // Verwende untere 70% für Ambient-Berechnung
    
    [Header("Pitch Range Filter")]
    [SerializeField] private bool enablePitchRangeFilter = true;
    [Tooltip("Minimum acceptable pitch in Hz (below this = noise)")]
    [SerializeField] private float minAcceptablePitch = 60f; // Lower than typical human voice
    [Tooltip("Maximum acceptable pitch in Hz (above this = noise like fans)")]
    [SerializeField] private float maxAcceptablePitch = 600f; // Higher than typical human voice
    [SerializeField] private bool debugPitchFilter = false; // Separate pitch filter debugging
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;
    [SerializeField] private bool debugNoiseGate = false; // Separate noise gate debugging
    
    // ? MODERN EVENT-DRIVEN ARCHITECTURE
    // Subscribe to this event instead of polling GetCurrentPitch()
    // Example: micAnalysis.OnPitchDetected += (pitchData) => { /* handle pitch data */ };
    public System.Action<PitchDataPoint> OnPitchDetected;
    
    private AudioSource audioSource;
    private AudioClip microphoneClip;
    private float[] audioBuffer;
    private float lastAnalysisTime;
    private bool isAnalyzing = false;
    
    // Noise Gate variables
    private float ambientNoiseLevel = 0f;
    private List<float> calibrationSamples;
    private bool isCalibrating = true;
    private float calibrationStartTime;
    
    // NEW: Pitch filter statistics
    private int totalPitchesDetected = 0;
    private int pitchesFilteredByRange = 0;
    
    void Start()
    {
        InitializeComponents();
        DebugLog("MicAnalysisRefactored initialized - ? Modern implementation in use");
    }
    
    void Update()
    {
        if (isAnalyzing && audioSource != null && audioSource.isPlaying && 
            Time.time - lastAnalysisTime >= analysisInterval)
        {
            AnalyzePitch();
            lastAnalysisTime = Time.time;
        }
    }
    
    private void InitializeComponents()
    {
        audioBuffer = new float[analysisSettings.bufferLength];
        calibrationSamples = new List<float>();
        
        // AudioSource Setup
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.mute = true;
        audioSource.volume = 0f;
        
        // NEW: Validate pitch range settings
        ValidatePitchRangeSettings();
    }
    
    // NEW: Validate and adjust pitch range settings
    private void ValidatePitchRangeSettings()
    {
        if (enablePitchRangeFilter)
        {
            // Ensure min < max
            if (minAcceptablePitch >= maxAcceptablePitch)
            {
                Debug.LogWarning($"[MicAnalysisRefactored] Invalid pitch range: min ({minAcceptablePitch}) >= max ({maxAcceptablePitch}). Adjusting...");
                maxAcceptablePitch = minAcceptablePitch + 100f;
            }
            
            // Warn if range seems too narrow
            float range = maxAcceptablePitch - minAcceptablePitch;
            if (range < 100f)
            {
                Debug.LogWarning($"[MicAnalysisRefactored] Pitch range very narrow ({range:F1}Hz). This may filter out valid voice data.");
            }
            
            DebugLog($"Pitch range filter: {minAcceptablePitch:F1}-{maxAcceptablePitch:F1}Hz");
        }
    }
    
    public void SetMicrophone(string microphoneName)
    {
        deviceName = microphoneName;
        DebugLog($"Microphone set to: {deviceName}");
    }
    
    public void StartAnalysis()
    {
        DebugLog($"Starting analysis with device: {deviceName}");
        
        if (string.IsNullOrEmpty(deviceName))
        {
            Debug.LogError("No microphone device specified!");
            return;
        }
        
        StopAnalysis();
        
        if (InitializeMicrophone())
        {
            isAnalyzing = true;
            
            // Reset calibration
            if (enableNoiseGate)
            {
                isCalibrating = true;
                calibrationStartTime = Time.time;
                calibrationSamples.Clear();
                ambientNoiseLevel = 0f;
                DebugLog($"Analysis started with noise gate. Calibrating for {ambientCalibrationTime}s...");
            }
            else
            {
                isCalibrating = false;
                DebugLog("Analysis started without noise gate");
            }
            
            // NEW: Reset pitch filter statistics
            totalPitchesDetected = 0;
            pitchesFilteredByRange = 0;
        }
        else
        {
            DebugLog("Failed to initialize microphone!");
        }
    }
    
    public void StopAnalysis()
    {
        DebugLog($"Stopping analysis - was analyzing: {isAnalyzing}");
        
        // NEW: Log pitch filter statistics
        if (enablePitchRangeFilter && totalPitchesDetected > 0)
        {
            float filterPercentage = (pitchesFilteredByRange / (float)totalPitchesDetected) * 100f;
            DebugLog($"Pitch filter stats: {pitchesFilteredByRange}/{totalPitchesDetected} pitches filtered ({filterPercentage:F1}%)");
        }
        
        isAnalyzing = false;
        
        if (!string.IsNullOrEmpty(deviceName) && Microphone.IsRecording(deviceName))
        {
            Microphone.End(deviceName);
            DebugLog($"Stopped recording from: {deviceName}");
        }
        
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
    
    private bool InitializeMicrophone()
    {
        // Prüfe ob Mikrofon existiert
        bool microphoneExists = false;
        foreach (string device in Microphone.devices)
        {
            if (device == deviceName)
            {
                microphoneExists = true;
                break;
            }
        }
        
        if (!microphoneExists)
        {
            Debug.LogError($"Microphone '{deviceName}' not found!");
            return false;
        }
        
        try
        {
            // Starte Mikrofonaufnahme
            microphoneClip = Microphone.Start(deviceName, true, 10, analysisSettings.sampleRate);
            if (microphoneClip == null)
            {
                Debug.LogError($"Failed to start microphone: {deviceName}");
                return false;
            }
            
            audioSource.clip = microphoneClip;
            audioSource.loop = true;
            
            // Warte auf Mikrofonstart
            int timeout = 0;
            while (!(Microphone.GetPosition(deviceName) > 0) && timeout < 1000)
            {
                timeout++;
                System.Threading.Thread.Sleep(1);
            }
            
            if (timeout >= 1000)
            {
                Debug.LogError("Microphone startup timeout!");
                return false;
            }
            
            audioSource.Play();
            DebugLog($"Microphone initialized: {deviceName} at {analysisSettings.sampleRate}Hz");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize microphone: {e.Message}");
            return false;
        }
    }
    
    private void AnalyzePitch()
    {
        if (microphoneClip == null) return;
        
        // Audio-Daten abrufen
        int micPosition = Microphone.GetPosition(deviceName);
        if (micPosition < analysisSettings.bufferLength) return;
        
        int startPosition = micPosition - analysisSettings.bufferLength;
        if (startPosition < 0)
            startPosition = microphoneClip.samples + startPosition;
        
        microphoneClip.GetData(audioBuffer, startPosition);
        
        // Verwende geteilte Analyse-Engine
        float timestamp = Time.time;
        PitchDataPoint pitchData = PitchAnalyzer.AnalyzeAudioBuffer(audioBuffer, timestamp, analysisSettings);
        
        // Apply noise gate if enabled
        bool shouldPass = UpdateNoiseGate(pitchData.audioLevel);
        
        if (!shouldPass)
        {
            if (debugNoiseGate && enableDebugLogging)
                DebugLog($"Audio blocked by noise gate - Level: {pitchData.audioLevel:F4}");
            pitchData = new PitchDataPoint(timestamp, 0f, 0f, pitchData.audioLevel);
        }
        else
        {
            // NEW: Apply pitch range filter after noise gate
            pitchData = ApplyPitchRangeFilter(pitchData);
        }
        
        // ? MODERN EVENT-DRIVEN ARCHITECTURE: Fire event instead of storing in variables
        OnPitchDetected?.Invoke(pitchData);
        
        // Optional: Debug für interessante Pitches
        if (enableDebugLogging && pitchData.HasPitch)
        {
            DebugLog($"Pitch detected: {pitchData.frequency:F1}Hz");
        }
    }
    
    // NEW: Apply pitch range filter to remove fan noise and other unwanted frequencies
    private PitchDataPoint ApplyPitchRangeFilter(PitchDataPoint originalData)
    {
        if (!enablePitchRangeFilter || !originalData.HasPitch)
        {
            return originalData; // No filtering needed
        }
        
        totalPitchesDetected++;
        
        // Check if pitch is within acceptable range
        bool isInRange = originalData.frequency >= minAcceptablePitch && 
                        originalData.frequency <= maxAcceptablePitch;
        
        if (!isInRange)
        {
            pitchesFilteredByRange++;
            
            if (debugPitchFilter && enableDebugLogging)
            {
                string reason = originalData.frequency < minAcceptablePitch ? "too low" : "too high";
                DebugLog($"Pitch filtered: {originalData.frequency:F1}Hz ({reason}) - Range: {minAcceptablePitch:F1}-{maxAcceptablePitch:F1}Hz");
            }
            
            // Return modified data point with no pitch
            return new PitchDataPoint(originalData.timestamp, 0f, 0f, originalData.audioLevel);
        }
        
        // Pitch is in acceptable range
        return originalData;
    }
    
    private bool UpdateNoiseGate(float currentAudioLevel)
    {
        if (!enableNoiseGate)
        {
            return currentAudioLevel >= minAudioLevel; // Fallback to simple threshold
        }
        
        // Calibration phase - learn ambient noise level
        if (isCalibrating)
        {
            calibrationSamples.Add(currentAudioLevel);
            
            if (Time.time - calibrationStartTime >= ambientCalibrationTime)
            {
                // Calculate ambient noise as average of lower X% of samples
                var sortedSamples = calibrationSamples.OrderBy(x => x).ToList();
                int sampleCount = Mathf.FloorToInt(sortedSamples.Count * ambientSamplePercentage);
                ambientNoiseLevel = sortedSamples.Take(sampleCount).Average();
                
                isCalibrating = false;
                DebugLog($"Noise gate calibrated - Ambient: {ambientNoiseLevel:F4}, Multiplier: {noiseGateMultiplier}x");
            }
            return false; // Don't analyze during calibration
        }
        
        // Apply noise gate
        float noiseGateThreshold = ambientNoiseLevel * noiseGateMultiplier;
        bool shouldPass = currentAudioLevel >= Mathf.Max(minAudioLevel, noiseGateThreshold);
        
        if (debugNoiseGate && enableDebugLogging && Time.frameCount % 60 == 0) // Every second at 60fps
        {
            DebugLog($"NoiseGate - Audio: {currentAudioLevel:F4}, Gate: {noiseGateThreshold:F4}, " +
                    $"Ambient: {ambientNoiseLevel:F4}, Pass: {shouldPass}");
        }
        
        return shouldPass;
    }
    
    // NEW: Manual recalibration method
    public void RecalibrateNoiseGate()
    {
        if (enableNoiseGate && isAnalyzing)
        {
            isCalibrating = true;
            calibrationStartTime = Time.time;
            calibrationSamples.Clear();
            ambientNoiseLevel = 0f;
            DebugLog($"Manual recalibration started - will take {ambientCalibrationTime}s");
        }
        else
        {
            DebugLog("Cannot recalibrate - noise gate disabled or not analyzing");
        }
    }
    
    // NEW: Manual pitch range adjustment methods
    public void SetPitchRange(float minPitch, float maxPitch)
    {
        minAcceptablePitch = minPitch;
        maxAcceptablePitch = maxPitch;
        ValidatePitchRangeSettings();
        DebugLog($"Pitch range updated: {minAcceptablePitch:F1}-{maxAcceptablePitch:F1}Hz");
    }
    
    public void SetPitchRangeForVoiceType(string voiceType)
    {
        switch (voiceType.ToLower())
        {
            case "male":
                SetPitchRange(80f, 300f);
                break;
            case "female":
                SetPitchRange(120f, 400f);
                break;
            case "child":
                SetPitchRange(200f, 600f);
                break;
            case "speech":
                SetPitchRange(60f, 500f); // General speech range
                break;
            default:
                DebugLog($"Unknown voice type: {voiceType}. Use 'male', 'female', 'child', or 'speech'");
                break;
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[MicAnalysisRefactored] {message}");
        }
    }
    
    void OnDestroy()
    {
        StopAnalysis();
    }
    
    // ? MODERN PROPERTY-BASED API - Use these instead of deprecated methods
    public bool IsAnalyzing => isAnalyzing;
    public bool IsCalibrating => isCalibrating;
    public float AmbientNoiseLevel => ambientNoiseLevel;
    public string CurrentDevice => deviceName;
    public bool NoiseGateEnabled => enableNoiseGate;
    public float NoiseGateThreshold => enableNoiseGate ? ambientNoiseLevel * noiseGateMultiplier : minAudioLevel;
    
    // NEW: Public getters for pitch filter status
    public bool PitchRangeFilterEnabled => enablePitchRangeFilter;
    public float MinAcceptablePitch => minAcceptablePitch;
    public float MaxAcceptablePitch => maxAcceptablePitch;
    public float PitchFilterEfficiency => totalPitchesDetected > 0 ? (pitchesFilteredByRange / (float)totalPitchesDetected) * 100f : 0f;
    public int TotalPitchesDetected => totalPitchesDetected;
    public int PitchesFilteredByRange => pitchesFilteredByRange;
}