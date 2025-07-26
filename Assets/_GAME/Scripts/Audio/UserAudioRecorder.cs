using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class UserAudioRecorder : MonoBehaviour
{
    [Header("?? USER AUDIO RECORDER - Real Microphone Recording")]
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
    [SerializeField] private float recordingDuration = 5.0f;
    [SerializeField] private bool autoCalculateDuration = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    
    // Events
    public System.Action<string> OnRecordingSaved;
    public System.Action<bool> OnRecordingStateChanged;
    
    // Recording state
    private bool isRecording = false;
    private CircularAudioBuffer audioBuffer;
    private string recordingFilePath;
    private List<PitchDataPoint> collectedPitchData;
    private float recordingStartTime;
    
    // ? NEW: Real microphone recording instead of synthetic audio
    private string microphoneDevice;
    private AudioClip microphoneClip;
    private int lastMicrophonePosition = 0;
    
    void Start()
    {
        InitializeComponents();
        CalculateRecordingDuration();
        InitializeAudioBuffer();
    }
    
    void Update()
    {
        // ? NEW: Record real microphone audio while recording
        if (isRecording)
        {
            RecordRealMicrophoneAudio();
        }
    }
    
    // ? NEW: Record actual microphone audio data
    private void RecordRealMicrophoneAudio()
    {
        if (microphoneClip == null || !Microphone.IsRecording(microphoneDevice))
            return;
        
        int currentPosition = Microphone.GetPosition(microphoneDevice);
        
        if (currentPosition < lastMicrophonePosition)
        {
            // Microphone position wrapped around
            lastMicrophonePosition = 0;
        }
        
        if (currentPosition > lastMicrophonePosition)
        {
            // Get new samples since last update
            int sampleCount = currentPosition - lastMicrophonePosition;
            float[] newSamples = new float[sampleCount];
            
            microphoneClip.GetData(newSamples, lastMicrophonePosition);
            
            // Add real microphone samples to buffer
            audioBuffer.AddSamples(newSamples);
            
            lastMicrophonePosition = currentPosition;
            
            if (enableDebugLogging && Time.frameCount % 60 == 0) // Every second
            {
                DebugLog($"?? Recording real audio: {audioBuffer.CurrentSize} samples, Mic pos: {currentPosition}");
            }
        }
    }
    
    // ? IMPROVED: Simplified pitch data handler (only for metadata)
    private void OnPitchDataReceived(PitchDataPoint pitchData)
    {
        if (!isRecording) return;
        
        // Store pitch data point for metadata only
        collectedPitchData.Add(pitchData);
        
        if (enableDebugLogging && pitchData.HasPitch)
        {
            DebugLog($"?? Pitch detected: {pitchData.frequency:F1}Hz, Audio samples: {audioBuffer.CurrentSize}");
        }
    }
    
    public void StartRecording()
    {
        if (isRecording) return;
        
        // Try to get better duration calculation
        CalculateRecordingDuration();
        
        DebugLog("??? Starting real microphone recording");
        
        // Clear previous data
        audioBuffer.Clear();
        collectedPitchData.Clear();
        recordingStartTime = Time.time;
        lastMicrophonePosition = 0;
        
        // ? NEW: Start real microphone recording
        if (!StartMicrophoneRecording())
        {
            Debug.LogError("[UserAudioRecorder] Failed to start microphone recording!");
            return;
        }
        
        isRecording = true;
        OnRecordingStateChanged?.Invoke(true);
        
        DebugLog($"? Real microphone recording started - duration target: {recordingDuration:F1}s");
    }
    
    // ? NEW: Start actual microphone recording
    private bool StartMicrophoneRecording()
    {
        // Get microphone device from MicAnalysisRefactored
        if (micAnalysis != null && !string.IsNullOrEmpty(micAnalysis.CurrentDevice))
        {
            microphoneDevice = micAnalysis.CurrentDevice;
        }
        else
        {
            // Fallback to default microphone
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
            }
            else
            {
                Debug.LogError("[UserAudioRecorder] No microphone devices found!");
                return false;
            }
        }
        
        try
        {
            // Start recording with a long buffer (60 seconds)
            microphoneClip = Microphone.Start(microphoneDevice, true, 60, sampleRate);
            
            if (microphoneClip == null)
            {
                Debug.LogError($"[UserAudioRecorder] Failed to start microphone: {microphoneDevice}");
                return false;
            }
            
            // Wait for microphone to start
            int timeout = 0;
            while (!(Microphone.GetPosition(microphoneDevice) > 0) && timeout < 1000)
            {
                timeout++;
                System.Threading.Thread.Sleep(1);
            }
            
            if (timeout >= 1000)
            {
                Debug.LogError("[UserAudioRecorder] Microphone startup timeout!");
                return false;
            }
            
            DebugLog($"? Microphone recording started: {microphoneDevice} at {sampleRate}Hz");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UserAudioRecorder] Failed to start microphone: {e.Message}");
            return false;
        }
    }
    
    public void StopRecordingAndSave()
    {
        if (!isRecording) return;
        
        DebugLog("?? Stopping recording and saving to disk");
        
        isRecording = false;
        OnRecordingStateChanged?.Invoke(false);
        
        // ? NEW: Stop microphone recording
        if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
            DebugLog($"?? Stopped microphone recording: {microphoneDevice}");
        }
        
        DebugLog($"?? Total real audio samples collected: {audioBuffer.CurrentSize}, Target: {recordingDuration * sampleRate:F0}");
        
        // Save the collected data
        if (audioBuffer.HasData)
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
        // Get the last X seconds of real audio data
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
            DebugLog($"? Real audio recording saved: {recordingFilePath}");
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
        if (!autoCalculateDuration)
        {
            DebugLog($"Using manual recording duration: {recordingDuration:F1}s");
            return;
        }
        
        // ? FIX: Only calculate if we don't have a valid duration yet
        if (recordingDuration > 0f && recordingDuration != 5.0f) // 5.0f is our fallback value
        {
            DebugLog($"Duration already calculated: {recordingDuration:F1}s");
            return;
        }
        
        // ? FIX: Find ChorusingManager if not found yet
        if (chorusingManager == null)
        {
            chorusingManager = FindFirstObjectByType<ChorusingManager>();
        }
        
        if (chorusingManager == null)
        {
            DebugLog($"?? ChorusingManager not found, using fallback: {recordingDuration:F1}s");
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
            
            // ? FIX: Reinitialize buffer with correct size
            InitializeAudioBuffer();
        }
        else
        {
            DebugLog($"?? Native pitch data not ready yet, keeping fallback: {recordingDuration:F1}s");
        }
    }
    
    private void InitializeAudioBuffer()
    {
        // Create circular buffer for the calculated duration
        int bufferSamples = Mathf.RoundToInt(recordingDuration * sampleRate);
        audioBuffer = new CircularAudioBuffer(bufferSamples);
        collectedPitchData = new List<PitchDataPoint>();
        
        DebugLog($"? Audio buffer initialized: {bufferSamples} sample capacity for real microphone recording");
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
        
        // Stop microphone recording
        if (isRecording)
        {
            StopRecordingAndSave();
        }
    }
}