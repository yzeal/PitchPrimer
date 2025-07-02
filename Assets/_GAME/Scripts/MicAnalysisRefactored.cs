using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Refactored microphone analysis for chorusing system
// Uses shared PitchAnalyzer core and modular visualization
// Integrates with ChorusingManager for synchronized dual-track display

[RequireComponent(typeof(AudioSource))]
public class MicAnalysisRefactored : MonoBehaviour
{
    [Header("Analysis")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;
    [SerializeField] private float analysisInterval = 0.1f;
    
    [Header("Microphone")]
    [SerializeField] private string deviceName;
    [SerializeField] private float minAudioLevel = 0.001f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;
    
    // Events für lose Kopplung
    public System.Action<PitchDataPoint> OnPitchDetected;
    
    private AudioSource audioSource;
    private AudioClip microphoneClip;
    private float[] audioBuffer;
    private float lastAnalysisTime;
    private bool isAnalyzing = false;
    
    // Noise Gate (simplified version)
    private float ambientNoiseLevel = 0f;
    private List<float> calibrationSamples;
    private bool isCalibrating = true;
    private float calibrationStartTime;
    private float calibrationDuration = 2f;
    
    void Start()
    {
        InitializeComponents();
        DebugLog("MicAnalysisRefactored initialized");
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
            isCalibrating = true;
            calibrationStartTime = Time.time;
            calibrationSamples.Clear();
            ambientNoiseLevel = 0f;
            
            DebugLog($"Analysis started successfully. Calibrating for {calibrationDuration}s...");
        }
        else
        {
            DebugLog("Failed to initialize microphone!");
        }
    }
    
    public void StopAnalysis()
    {
        DebugLog($"Stopping analysis - was analyzing: {isAnalyzing}");
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
        
        // Noise Gate während Kalibrierung
        if (isCalibrating)
        {
            calibrationSamples.Add(pitchData.audioLevel);
            
            if (Time.time - calibrationStartTime >= calibrationDuration)
            {
                // Berechne Ambient-Niveau als Durchschnitt der unteren 70%
                var sortedSamples = calibrationSamples.OrderBy(x => x).ToList();
                int sampleCount = Mathf.FloorToInt(sortedSamples.Count * 0.7f);
                ambientNoiseLevel = sortedSamples.Take(sampleCount).Average();
                
                isCalibrating = false;
                DebugLog($"Calibration complete. Ambient noise: {ambientNoiseLevel:F4}");
            }
            return; // Keine Analyse während Kalibrierung
        }
        
        // Noise Gate Check
        float noiseGateThreshold = ambientNoiseLevel * 3f; // 3x Ambient als Threshold
        if (pitchData.audioLevel < Mathf.Max(minAudioLevel, noiseGateThreshold))
        {
            pitchData = new PitchDataPoint(timestamp, 0f, 0f, pitchData.audioLevel);
        }
        
        // Event feuern
        OnPitchDetected?.Invoke(pitchData);
        
        // Optional: Debug für interessante Pitches
        if (enableDebugLogging && pitchData.HasPitch)
        {
            DebugLog($"Pitch detected: {pitchData.frequency:F1}Hz");
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
    
    // Public getters für Status
    public bool IsAnalyzing => isAnalyzing;
    public bool IsCalibrating => isCalibrating;
    public float AmbientNoiseLevel => ambientNoiseLevel;
    public string CurrentDevice => deviceName;
}