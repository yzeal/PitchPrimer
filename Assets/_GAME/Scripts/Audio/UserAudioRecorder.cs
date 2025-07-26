using UnityEngine;
using System.Collections.Generic;
using System.IO;

// COPILOT CONTEXT: Event-based user audio recording for scoring preparation
// Uses existing MicAnalysisRefactored pipeline - NO separate microphone access
// Records audio samples from OnPitchDetected events into circular buffer

public class UserAudioRecorder : MonoBehaviour
{
    [Header("?? USER AUDIO RECORDER - Conflict-Free Implementation")]
    [Space(10)]
    [Header("Recording Settings")]
    [SerializeField] private int sampleRate = 44100;
    [SerializeField] private string recordingFileName = "user_recording.wav";
    [SerializeField] private bool overwritePreviousRecording = true;
    
    [Header("Integration")]
    [SerializeField] private MicAnalysisRefactored micAnalysis;
    [SerializeField] private UserRecordingInputManager inputManager;
    [SerializeField] private ChorusingManager chorusingManager;
    
    [Header("Recording Duration")]
    [Tooltip("Recording duration will be auto-calculated from native clip + silence")]
    [SerializeField] private float recordingDuration = 5.0f; // Fallback value
    [SerializeField] private bool autoCalculateDuration = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    
    // Events
    public System.Action<string> OnRecordingSaved; // Passes file path
    public System.Action<bool> OnRecordingStateChanged; // True = recording, False = stopped
    
    // Recording state
    private bool isRecording = false;
    private CircularAudioBuffer audioBuffer;
    private string recordingFilePath;
    private List<PitchDataPoint> collectedPitchData;
    private float recordingStartTime;
    
    void Start()
    {
        InitializeComponents();
        CalculateRecordingDuration();
        InitializeAudioBuffer();
    }
    
    private void InitializeComponents()
    {
        // Auto-find components if not assigned
        if (micAnalysis == null)
            micAnalysis = FindFirstObjectByType<MicAnalysisRefactored>();
        
        if (inputManager == null)
            inputManager = FindFirstObjectByType<UserRecordingInputManager>();
        
        if (chorusingManager == null)
            chorusingManager = FindFirstObjectByType<ChorusingManager>();
        
        // Subscribe to events
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected += OnPitchDataReceived;
            DebugLog("? Subscribed to MicAnalysisRefactored.OnPitchDetected");
        }
        else
        {
            Debug.LogError("[UserAudioRecorder] MicAnalysisRefactored not found!");
        }
        
        if (inputManager != null)
        {
            inputManager.OnRecordingStarted += StartRecording;
            inputManager.OnRecordingStopped += StopRecordingAndSave;
            DebugLog("? Subscribed to UserRecordingInputManager events");
        }
        else
        {
            Debug.LogWarning("[UserAudioRecorder] UserRecordingInputManager not found! Manual control only.");
        }
        
        // Setup file path
        recordingFilePath = Path.Combine(Application.persistentDataPath, recordingFileName);
        DebugLog($"Recording path: {recordingFilePath}");
    }
    
    private void CalculateRecordingDuration()
    {
        if (!autoCalculateDuration || chorusingManager == null)
        {
            DebugLog($"Using manual recording duration: {recordingDuration:F1}s");
            return;
        }
        
        // Get duration from ChorusingManager
        var nativePitchData = chorusingManager.GetNativePitchData();
        float silenceDuration = chorusingManager.GetQuantizedSilenceDuration();
        
        if (nativePitchData != null && nativePitchData.Count > 0)
        {
            // Calculate duration: (dataPoints * analysisInterval) + silence
            float nativeClipDuration = nativePitchData.Count * 0.1f; // 0.1s per analysis interval
            recordingDuration = nativeClipDuration + silenceDuration;
            
            DebugLog($"? Auto-calculated duration: {recordingDuration:F1}s " +
                    $"(Native: {nativeClipDuration:F1}s + Silence: {silenceDuration:F1}s)");
        }
        else
        {
            DebugLog($"?? Could not auto-calculate duration, using fallback: {recordingDuration:F1}s");
        }
    }
    
    private void InitializeAudioBuffer()
    {
        // Create circular buffer for the calculated duration
        // Estimate samples needed: duration * sampleRate / analysisInterval
        int estimatedSamples = Mathf.RoundToInt(recordingDuration * sampleRate / 0.1f) * 100; // Safety margin
        audioBuffer = new CircularAudioBuffer(estimatedSamples);
        collectedPitchData = new List<PitchDataPoint>();
        
        DebugLog($"? Audio buffer initialized: {estimatedSamples} sample capacity");
    }
    
    // Event handler for pitch data from MicAnalysisRefactored
    private void OnPitchDataReceived(PitchDataPoint pitchData)
    {
        if (!isRecording) return;
        
        // Store pitch data point
        collectedPitchData.Add(pitchData);
        
        // Convert pitch data to audio sample approximation
        // NOTE: This is a simplified approach - we're storing timing and pitch info
        // Real audio reconstruction would need the actual audio samples
        float audioSample = pitchData.HasPitch ? 
            Mathf.Sin(2 * Mathf.PI * pitchData.frequency * (pitchData.timestamp - recordingStartTime)) * pitchData.audioLevel :
            0f;
        
        audioBuffer.AddSample(audioSample);
        
        if (enableDebugLogging && pitchData.HasPitch)
        {
            DebugLog($"?? Recorded pitch: {pitchData.frequency:F1}Hz at {pitchData.timestamp:F2}s");
        }
    }
    
    public void StartRecording()
    {
        if (isRecording) return;
        
        DebugLog("??? Starting user audio recording");
        
        // Clear previous data
        audioBuffer.Clear();
        collectedPitchData.Clear();
        recordingStartTime = Time.time;
        
        isRecording = true;
        OnRecordingStateChanged?.Invoke(true);
        
        DebugLog($"? Recording started - duration target: {recordingDuration:F1}s");
    }
    
    public void StopRecordingAndSave()
    {
        if (!isRecording) return;
        
        DebugLog("?? Stopping recording and saving to disk");
        
        isRecording = false;
        OnRecordingStateChanged?.Invoke(false);
        
        // Save the collected data
        if (collectedPitchData.Count > 0 || audioBuffer.HasData)
        {
            SaveRecordingToWAV();
        }
        else
        {
            Debug.LogWarning("[UserAudioRecorder] No audio data to save!");
        }
    }
    
    private void SaveRecordingToWAV()
    {
        // Get the last X seconds of audio data
        float[] audioData = audioBuffer.GetLastSeconds(recordingDuration);
        
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogWarning("[UserAudioRecorder] No audio data in buffer!");
            return;
        }
        
        // Delete previous recording if configured
        if (overwritePreviousRecording && File.Exists(recordingFilePath))
        {
            File.Delete(recordingFilePath);
            DebugLog("??? Previous recording overwritten");
        }
        
        // Save using WAV exporter
        bool success = WAVExporter.SaveWAV(audioData, recordingFilePath, sampleRate, 1);
        
        if (success)
        {
            float actualDuration = audioData.Length / (float)sampleRate;
            DebugLog($"? Recording saved: {recordingFilePath}");
            DebugLog($"?? File info: {audioData.Length} samples, {actualDuration:F1}s, {collectedPitchData.Count} pitch points");
            
            OnRecordingSaved?.Invoke(recordingFilePath);
        }
        else
        {
            Debug.LogError("[UserAudioRecorder] Failed to save recording!");
        }
    }
    
    // Public API
    public bool IsRecording => isRecording;
    public string RecordingFilePath => recordingFilePath;
    public float RecordingDuration => recordingDuration;
    public bool HasSavedRecording => File.Exists(recordingFilePath);
    public int CollectedPitchPoints => collectedPitchData?.Count ?? 0;
    
    // Manual control for testing
    public void StartRecordingManual()
    {
        StartRecording();
    }
    
    public void StopRecordingManual()
    {
        StopRecordingAndSave();
    }
    
    // Configuration
    public void SetRecordingDuration(float duration)
    {
        recordingDuration = duration;
        autoCalculateDuration = false;
        InitializeAudioBuffer(); // Reinitialize with new duration
        DebugLog($"?? Recording duration set to: {duration:F1}s");
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[UserAudioRecorder] {message}");
        }
    }
    
    void OnDestroy()
    {
        // Cleanup subscriptions
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected -= OnPitchDataReceived;
        }
        
        if (inputManager != null)
        {
            inputManager.OnRecordingStarted -= StartRecording;
            inputManager.OnRecordingStopped -= StopRecordingAndSave;
        }
        
        // Save any pending recording
        if (isRecording)
        {
            StopRecordingAndSave();
        }
    }
}