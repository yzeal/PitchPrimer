using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(AudioSource))]
public class MicAnalysis : MonoBehaviour
{
    [Header("Microphone Settings")]
    [SerializeField] private string deviceName;
    [SerializeField] private int sampleRate = 44100;
    [SerializeField] private int bufferLength = 4096; // Power of 2 for FFT
    
    [Header("Pitch Analysis")]
    [SerializeField] private float minFrequency = 80f;   // Minimum human voice
    [SerializeField] private float maxFrequency = 800f;  // Maximum for pitch accent analysis
    [SerializeField] private float analysisInterval = 0.1f; // 100ms intervals
    [SerializeField] private float correlationThreshold = 0.1f; // Reduced from 0.3f
    [SerializeField] private float minAudioLevel = 0.001f; // Minimum audio level to analyze
    
    [Header("Visualization")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Transform cubeParent;
    [SerializeField] private int maxCubes = 30; // Weniger Würfel für bessere Performance
    [SerializeField] private float cubeSpacing = 0.8f; // Engerer Abstand
    [SerializeField] private float pitchScaleMultiplier = 1.5f; // Guter Startwert
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private bool showAudioLevels = true;
    [SerializeField] private bool debugCorrelation = true; // New
    
    // Audio components
    private AudioSource audioSource;
    private AudioClip microphoneClip;
    private float[] audioBuffer;
    private float[] windowBuffer;
    
    // Pitch analysis
    private List<float> pitchHistory;
    private float lastAnalysisTime;
    private float currentPitch;
    private bool isAnalyzing = false;
    
    // Visualization
    private Queue<GameObject> pitchCubes;
    
    // Debug counters
    private int updateCallCount = 0;
    private int analysisCallCount = 0;
    private int cubesCreated = 0;
    
    void Start()
    {
        InitializeComponents();
        DebugLog("MicAnalysis Start() called");
        audioSource = GetComponent<AudioSource>();
    }
    
    void Update()
    {
        updateCallCount++;
        
        if (updateCallCount % 100 == 0) // Log every 100 updates
        {
            DebugLog($"Update #{updateCallCount} - isAnalyzing: {isAnalyzing}, audioSource: {(audioSource != null ? "OK" : "NULL")}, isPlaying: {(audioSource != null ? audioSource.isPlaying.ToString() : "N/A")}");
        }
        
        if (isAnalyzing && audioSource != null && audioSource.isPlaying && 
            Time.time - lastAnalysisTime >= analysisInterval)
        {
            AnalyzePitch();
            UpdateVisualization();
            lastAnalysisTime = Time.time;
        }
    }
    
    private void InitializeComponents()
    {
        InitializeVisualization();
        pitchHistory = new List<float>();
        pitchCubes = new Queue<GameObject>();
        audioBuffer = new float[bufferLength];
        windowBuffer = new float[bufferLength];
        
        // Apply Hann window for better frequency analysis
        for (int i = 0; i < bufferLength; i++)
        {
            windowBuffer[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / (bufferLength - 1)));
        }
        
        DebugLog("Components initialized successfully");
    }
    
    public void SetMicrophone(string microphoneName)
    {
        deviceName = microphoneName;
        DebugLog($"Microphone set to: {deviceName}");
    }
    
    public void StartAnalysis()
    {
        DebugLog($"StartAnalysis() called with device: {deviceName}");
        
        if (string.IsNullOrEmpty(deviceName))
        {
            Debug.LogError("No microphone device specified!");
            return;
        }
        
        StopAnalysis(); // Stop any existing recording
        
        if (InitializeMicrophone())
        {
            isAnalyzing = true;
            DebugLog($"Started microphone analysis with: {deviceName} - isAnalyzing: {isAnalyzing}");
        }
        else
        {
            DebugLog("Failed to initialize microphone!");
        }
    }
    
    public void StopAnalysis()
    {
        DebugLog($"StopAnalysis() called - was analyzing: {isAnalyzing}");
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
        DebugLog("InitializeMicrophone() started");
        
        // Setup AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            DebugLog("Created new AudioSource component");
        }
        
        audioSource.mute = true; // Prevent feedback
        audioSource.volume = 0f;
        
        // Check if microphone exists
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
            DebugLog($"Starting microphone recording: {deviceName}");
            
            // Start microphone recording
            microphoneClip = Microphone.Start(deviceName, true, 10, sampleRate);
            if (microphoneClip == null)
            {
                Debug.LogError($"Failed to start microphone: {deviceName}");
                return false;
            }
            
            DebugLog($"Microphone clip created - length: {microphoneClip.length}s, samples: {microphoneClip.samples}");
            
            audioSource.clip = microphoneClip;
            audioSource.loop = true;
            
            // Wait for microphone to start
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
            DebugLog($"Microphone initialized: {deviceName} at {sampleRate}Hz - AudioSource playing: {audioSource.isPlaying}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize microphone: {e.Message}");
            return false;
        }
    }
    
    private void InitializeVisualization()
    {
        DebugLog($"InitializeVisualization() - cubePrefab: {(cubePrefab != null ? "OK" : "NULL")}, cubeParent: {(cubeParent != null ? "OK" : "NULL")}");
        
        if (cubePrefab == null)
        {
            // Create a simple cube if no prefab provided
            cubePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubePrefab.SetActive(false);
            DebugLog("Created default cube prefab");
        }
        
        if (cubeParent == null)
        {
            GameObject parent = new GameObject("PitchVisualization");
            cubeParent = parent.transform;
            DebugLog("Created default cube parent");
        }
    }
    
    private void AnalyzePitch()
    {
        analysisCallCount++;
        
        if (microphoneClip == null) 
        {
            if (analysisCallCount % 10 == 1) // Log every 10th call
                DebugLog("AnalyzePitch: microphoneClip is null");
            return;
        }
        
        // Get current audio data
        int micPosition = Microphone.GetPosition(deviceName);
        if (micPosition < bufferLength) 
        {
            if (analysisCallCount % 10 == 1)
                DebugLog($"AnalyzePitch: micPosition ({micPosition}) < bufferLength ({bufferLength})");
            return;
        }
        
        // Get the most recent audio data
        int startPosition = micPosition - bufferLength;
        if (startPosition < 0)
            startPosition = microphoneClip.samples + startPosition;
            
        microphoneClip.GetData(audioBuffer, startPosition);
        
        // Calculate audio level for debugging
        float audioLevel = 0f;
        float maxSample = 0f;
        for (int i = 0; i < audioBuffer.Length; i++)
        {
            float abs = Mathf.Abs(audioBuffer[i]);
            audioLevel += abs;
            if (abs > maxSample) maxSample = abs;
        }
        audioLevel /= audioBuffer.Length;
        
        if (showAudioLevels && analysisCallCount % 5 == 1)
        {
            DebugLog($"Analysis #{analysisCallCount} - Audio Level: {audioLevel:F4}, Max Sample: {maxSample:F4}, Mic Position: {micPosition}");
        }
        
        // Skip analysis if audio level is too low
        if (audioLevel < minAudioLevel)
        {
            if (analysisCallCount % 20 == 1)
                DebugLog($"Audio level too low: {audioLevel:F4} < {minAudioLevel:F4}");
            currentPitch = 0;
            pitchHistory.Add(currentPitch);
            if (pitchHistory.Count > 10) 
                pitchHistory.RemoveAt(0);
            return;
        }
        
        // Apply window function to reduce spectral leakage
        for (int i = 0; i < bufferLength; i++)
        {
            audioBuffer[i] *= windowBuffer[i];
        }
        
        // Analyze pitch using autocorrelation method
        currentPitch = AnalyzePitchAutocorrelation(audioBuffer);
        
        if (currentPitch > 0)
        {
            DebugLog($"Pitch detected: {currentPitch:F1} Hz (Analysis #{analysisCallCount})");
        }
        else if (analysisCallCount % 10 == 1)
        {
            DebugLog($"No pitch detected in analysis #{analysisCallCount}");
        }
        
        // Add to history for smoothing
        pitchHistory.Add(currentPitch);
        if (pitchHistory.Count > 10) // Keep last 10 measurements for smoothing
            pitchHistory.RemoveAt(0);
    }
    
    private float AnalyzePitchAutocorrelation(float[] buffer)
    {
        int minPeriod = Mathf.FloorToInt(sampleRate / maxFrequency);
        int maxPeriod = Mathf.FloorToInt(sampleRate / minFrequency);
        
        if (debugCorrelation && analysisCallCount % 20 == 1)
        {
            DebugLog($"Autocorrelation - minPeriod: {minPeriod}, maxPeriod: {maxPeriod}, bufferLength: {buffer.Length}");
        }
        
        float bestPeriod = 0;
        float maxCorrelation = 0;
        
        // Calculate RMS for normalization
        float rms = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            rms += buffer[i] * buffer[i];
        }
        rms = Mathf.Sqrt(rms / buffer.Length);
        
        if (rms < 0.001f) // Too quiet
        {
            if (debugCorrelation && analysisCallCount % 20 == 1)
                DebugLog($"RMS too low: {rms:F6}");
            return 0;
        }
        
        // Calculate autocorrelation for different periods
        for (int period = minPeriod; period <= maxPeriod && period < buffer.Length / 2; period++)
        {
            float correlation = 0;
            float energy1 = 0;
            float energy2 = 0;
            
            int numSamples = buffer.Length - period;
            
            for (int i = 0; i < numSamples; i++)
            {
                correlation += buffer[i] * buffer[i + period];
                energy1 += buffer[i] * buffer[i];
                energy2 += buffer[i + period] * buffer[i + period];
            }
            
            // Normalized correlation coefficient
            float normalizedCorrelation = 0;
            if (energy1 > 0 && energy2 > 0)
            {
                normalizedCorrelation = correlation / Mathf.Sqrt(energy1 * energy2);
            }
            
            if (normalizedCorrelation > maxCorrelation)
            {
                maxCorrelation = normalizedCorrelation;
                bestPeriod = period;
            }
        }
        
        if (debugCorrelation && analysisCallCount % 20 == 1)
        {
            DebugLog($"Best correlation: {maxCorrelation:F4} at period {bestPeriod} (threshold: {correlationThreshold:F4})");
        }
        
        // Convert period to frequency
        if (bestPeriod > 0 && maxCorrelation > correlationThreshold)
        {
            float frequency = sampleRate / bestPeriod;
            if (debugCorrelation)
                DebugLog($"Frequency calculated: {frequency:F1} Hz");
            return frequency;
        }
        
        return 0; // No pitch detected
    }
    
    private void UpdateVisualization()
    {
        // Smooth the pitch using moving average
        float smoothedPitch = pitchHistory.Count > 0 ? pitchHistory.Average() : 0;
        
        if (analysisCallCount % 10 == 1)
            DebugLog($"UpdateVisualization - smoothedPitch: {smoothedPitch:F1}, pitchHistory count: {pitchHistory.Count}");
        
        // Create new cube for this pitch measurement
        if (smoothedPitch > 0)
        {
            if (cubePrefab == null)
            {
                DebugLog("ERROR: cubePrefab is null in UpdateVisualization!");
                return;
            }
            
            if (cubeParent == null)
            {
                DebugLog("ERROR: cubeParent is null in UpdateVisualization!");
                return;
            }
            
            GameObject newCube = Instantiate(cubePrefab, cubeParent);
            newCube.SetActive(true);
            cubesCreated++;
            
            // Position cube
            float xPosition = pitchCubes.Count * cubeSpacing;
            newCube.transform.localPosition = new Vector3(xPosition, 0, 0);
            
            // VERBESSERTE SKALIERUNG mit besseren Grenzen
            float pitchScale = Mathf.Log(smoothedPitch / minFrequency) * pitchScaleMultiplier;
            pitchScale = Mathf.Clamp(pitchScale, 0.2f, 5f); // Bessere min/max Werte
            newCube.transform.localScale = new Vector3(0.8f, pitchScale, 0.8f); // Schmalere Würfel
            
            DebugLog($"Created cube #{cubesCreated} - Pitch: {smoothedPitch:F1}Hz, Scale: {pitchScale:F2}, Position: ({xPosition}, 0, 0)");
            
            // VERBESSERTE FARBKODIERUNG mit mehr Kontrast
            Renderer renderer = newCube.GetComponent<Renderer>();
            if (renderer != null)
            {
                float normalizedPitch = (smoothedPitch - minFrequency) / (maxFrequency - minFrequency);
                // Erweiterte Farbpalette für bessere Unterscheidung
                Color cubeColor = Color.HSVToRGB(normalizedPitch * 0.8f, 0.8f, 1f);
                renderer.material.color = cubeColor;
                
                // Debug: Zeige Farbwerte
                if (cubesCreated % 5 == 1)
                    DebugLog($"Cube color - Pitch: {smoothedPitch:F1}Hz, Normalized: {normalizedPitch:F2}, HSV: {normalizedPitch * 0.8f * 360:F0}°");
            }
            
            pitchCubes.Enqueue(newCube);
        }
        else
        {
            if (analysisCallCount % 20 == 1)
                DebugLog("No cube created - smoothedPitch is 0");
        }
        
        // Remove old cubes
        while (pitchCubes.Count > maxCubes)
        {
            GameObject oldCube = pitchCubes.Dequeue();
            DestroyImmediate(oldCube);
        }
        
        // Shift remaining cubes
        int index = 0;
        foreach (GameObject cube in pitchCubes)
        {
            cube.transform.localPosition = new Vector3(index * cubeSpacing, cube.transform.localPosition.y, 0);
            index++;
        }
    }
    
    void OnDestroy()
    {
        DebugLog("OnDestroy() called");
        StopAnalysis();
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[MicAnalysis] {message}");
        }
    }
    
    // Public methods for accessing pitch data
    public float GetCurrentPitch() => currentPitch;
    public float GetSmoothedPitch() => pitchHistory.Count > 0 ? pitchHistory.Average() : 0;
    public List<float> GetPitchHistory() => new List<float>(pitchHistory);
    public bool IsAnalyzing() => isAnalyzing;
}
