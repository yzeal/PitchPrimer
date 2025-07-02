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
    
    [Header("Noise Gate Settings")]
    [SerializeField] private bool enableNoiseGate = true;
    [SerializeField] private float noiseGateMultiplier = 3.0f; // Gate öffnet bei 3x Ambient Level
    [SerializeField] private float ambientCalibrationTime = 3.0f; // Sekunden für Ambient-Messung
    [SerializeField] private float gateAttackTime = 0.05f; // Schnelles Öffnen (50ms)
    [SerializeField] private float gateReleaseTime = 0.2f; // Langsameres Schließen (200ms)
    [SerializeField] private float ambientUpdateRate = 0.5f; // Update alle 500ms
    
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
    [SerializeField] private bool debugNoiseGate = true; // New
    
    // COPILOT CONTEXT: This is a Japanese pitch accent trainer
    // Current implementation: Real-time pitch detection with cube visualization
    // Working parameters: 80-800Hz range, 1.5f scale multiplier, 0.1f correlation threshold
    // Next steps: Add audio clip comparison and scoring system

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
    
    // Noise Gate variables
    private float ambientNoiseLevel = 0f;
    private bool isCalibrating = true;
    private float calibrationStartTime;
    private float lastAmbientUpdate;
    private float currentGateLevel = 0f;
    private float targetGateLevel = 0f;
    private List<float> ambientSamples;
    private bool gateIsOpen = false;
    
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
        ambientSamples = new List<float>();
        
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
            // Reset noise gate calibration
            isCalibrating = true;
            calibrationStartTime = Time.time;
            ambientNoiseLevel = 0f;
            ambientSamples.Clear();
            
            DebugLog($"Started microphone analysis with: {deviceName} - isAnalyzing: {isAnalyzing}");
            if (enableNoiseGate)
            {
                DebugLog($"Noise gate calibration started - will calibrate for {ambientCalibrationTime}s");
            }
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
        float audioLevel = CalculateAudioLevel(audioBuffer);
        float maxSample = 0f;
        for (int i = 0; i < audioBuffer.Length; i++)
        {
            float abs = Mathf.Abs(audioBuffer[i]);
            if (abs > maxSample) maxSample = abs;
        }
        
        if (showAudioLevels && analysisCallCount % 5 == 1)
        {
            DebugLog($"Analysis #{analysisCallCount} - Audio Level: {audioLevel:F4}, Max Sample: {maxSample:F4}, Mic Position: {micPosition}");
        }
        
            // Update noise gate
        bool shouldAnalyze = UpdateNoiseGate(audioLevel);
        
        if (!shouldAnalyze)
        {
            if (analysisCallCount % 20 == 1)
                DebugLog($"Audio blocked by noise gate - Level: {audioLevel:F4}, Gate: {currentGateLevel:F4}");
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
    
    private float CalculateAudioLevel(float[] buffer)
    {
        float level = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            level += Mathf.Abs(buffer[i]);
        }
        return level / buffer.Length;
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
            ambientSamples.Add(currentAudioLevel);
            
            if (Time.time - calibrationStartTime >= ambientCalibrationTime)
            {
                // Calculate ambient noise as average of lower 70% of samples
                var sortedSamples = ambientSamples.OrderBy(x => x).ToList();
                int sampleCount = Mathf.FloorToInt(sortedSamples.Count * 0.7f);
                ambientNoiseLevel = sortedSamples.Take(sampleCount).Average();
                
                isCalibrating = false;
                targetGateLevel = ambientNoiseLevel * noiseGateMultiplier;
                currentGateLevel = targetGateLevel;
                
                DebugLog($"Noise gate calibrated - Ambient: {ambientNoiseLevel:F4}, Gate: {targetGateLevel:F4}");
            }
            return false; // Don't analyze during calibration
        }
        
        // Update ambient noise level periodically
        if (Time.time - lastAmbientUpdate >= ambientUpdateRate)
        {
            // Only update if gate is closed (to avoid including speech)
            if (!gateIsOpen && currentAudioLevel < targetGateLevel)
            {
                // Slowly adapt ambient level
                ambientNoiseLevel = Mathf.Lerp(ambientNoiseLevel, currentAudioLevel, 0.1f);
                targetGateLevel = ambientNoiseLevel * noiseGateMultiplier;
            }
            lastAmbientUpdate = Time.time;
        }
        
        // Gate control with attack/release
        if (currentAudioLevel > targetGateLevel)
        {
            // Attack - fast opening
            currentGateLevel = Mathf.Lerp(currentGateLevel, currentAudioLevel, 
                Time.deltaTime / gateAttackTime);
            gateIsOpen = true;
        }
        else
        {
            // Release - slow closing
            currentGateLevel = Mathf.Lerp(currentGateLevel, targetGateLevel, 
                Time.deltaTime / gateReleaseTime);
            
            if (currentGateLevel <= targetGateLevel * 1.1f) // Small hysteresis
            {
                gateIsOpen = false;
            }
        }
        
        bool shouldPass = currentAudioLevel >= currentGateLevel;
        
        if (debugNoiseGate && analysisCallCount % 20 == 1)
        {
            DebugLog($"NoiseGate - Audio: {currentAudioLevel:F4}, Gate: {currentGateLevel:F4}, " +
                    $"Target: {targetGateLevel:F4}, Ambient: {ambientNoiseLevel:F4}, " +
                    $"Open: {gateIsOpen}, Pass: {shouldPass}");
        }
        
        return shouldPass;
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
        
        // WICHTIGE ÄNDERUNG: Erstelle IMMER einen Würfel für konstante Synchronisation
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
        
        // Position cube - konstanter Abstand für gleichmäßige Bewegung
        float xPosition = pitchCubes.Count * cubeSpacing;
        newCube.transform.localPosition = new Vector3(xPosition, 0, 0);
        
        // Skalierung basierend auf Pitch - auch bei Stille (Höhe = 0)
        float pitchScale;
        Color cubeColor;
        
        if (smoothedPitch > 0)
        {
            // Pitch erkannt - normale Skalierung
            pitchScale = Mathf.Log(smoothedPitch / minFrequency) * pitchScaleMultiplier;
            pitchScale = Mathf.Clamp(pitchScale, 0.2f, 20f);
            
            // Normale Farbkodierung für Pitch
            float normalizedPitch = (smoothedPitch - minFrequency) / (maxFrequency - minFrequency);
            cubeColor = Color.HSVToRGB(normalizedPitch * 0.8f, 0.8f, 1f);
            
            DebugLog($"Created pitch cube #{cubesCreated} - Pitch: {smoothedPitch:F1}Hz, Scale: {pitchScale:F2}");
        }
        else
        {
            // Stille - minimale Höhe für Synchronisation
            pitchScale = 0.05f; // Sehr flacher Würfel für stille Abschnitte
            
            // Graue Farbe für stille Abschnitte
            cubeColor = new Color(0f, 0f, 0f, 0f); // schwarze Farbe

            if (cubesCreated % 10 == 1) // Weniger Debug-Spam
                DebugLog($"Created silence cube #{cubesCreated} - maintaining sync");
        }
        
        // Anwenden der Skalierung
        newCube.transform.localScale = new Vector3(0.8f, pitchScale, 0.8f);
        
        // Anwenden der Farbe
        Renderer renderer = newCube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = cubeColor;
        }
        
        pitchCubes.Enqueue(newCube);
        
        // Remove old cubes - konstante Anzahl für gleichmäßige Bewegung
        while (pitchCubes.Count > maxCubes)
        {
            GameObject oldCube = pitchCubes.Dequeue();
            DestroyImmediate(oldCube);
        }
        
        // Shift remaining cubes - konstante Bewegungsgeschwindigkeit
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
    
    // Public methods for noise gate info
    public float GetAmbientNoiseLevel() => ambientNoiseLevel;
    public float GetCurrentGateLevel() => currentGateLevel;
    public bool IsGateOpen() => gateIsOpen;
    public bool IsCalibrating() => isCalibrating;
}
