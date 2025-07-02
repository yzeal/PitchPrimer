using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Core pitch analysis engine for Japanese pitch accent trainer
// Shared between microphone input and audio clip analysis
// Single source of truth for all pitch detection logic

[System.Serializable]
public class PitchAnalysisSettings
{
    [Header("Analysis Parameters")]
    public float minFrequency = 80f;
    public float maxFrequency = 800f;
    public float correlationThreshold = 0.1f;
    public int sampleRate = 44100;
    public int bufferLength = 4096;

    [Header("Smoothing")]
    public int historySize = 10;
    public bool useSmoothing = true;

    [Header("Debug")]
    public bool enableDebugLogging = false;
}

[System.Serializable]
public struct PitchDataPoint
{
    public float timestamp;     // Zeit in Sekunden
    public float frequency;     // Pitch in Hz (0 = Stille)
    public float confidence;    // Korrelationskoeffizient (0-1)
    public float audioLevel;    // Lautstärke (0-1)

    public bool HasPitch => frequency > 0;

    public PitchDataPoint(float time, float freq, float conf, float level)
    {
        timestamp = time;
        frequency = freq;
        confidence = conf;
        audioLevel = level;
    }

    public override string ToString()
    {
        return $"PitchData[{timestamp:F2}s]: {frequency:F1}Hz (conf:{confidence:F2}, level:{audioLevel:F3})";
    }
}

public static class PitchAnalyzer
{
    private static float[] windowBuffer;
    private static bool windowInitialized = false;
    private static int analysisCount = 0;

    #region Private Methods - MOVED TO TOP FOR ACCESSIBILITY

    private static float[] ConvertToMono(float[] stereoData, int channels)
    {
        if (channels == 1) return stereoData;

        int monoLength = stereoData.Length / channels;
        float[] monoData = new float[monoLength];

        for (int i = 0; i < monoLength; i++)
        {
            float sum = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                sum += stereoData[i * channels + ch];
            }
            monoData[i] = sum / channels;
        }

        return monoData;
    }

    private static void InitializeWindow(int bufferLength)
    {
        windowBuffer = new float[bufferLength];

        // Hann Window für bessere Frequenzanalyse
        for (int i = 0; i < bufferLength; i++)
        {
            windowBuffer[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / (bufferLength - 1)));
        }

        windowInitialized = true;
    }

    private static float[] ApplyWindow(float[] buffer)
    {
        float[] windowed = new float[buffer.Length];
        for (int i = 0; i < buffer.Length; i++)
        {
            windowed[i] = buffer[i] * windowBuffer[i];
        }
        return windowed;
    }

    private static float CalculateAudioLevel(float[] buffer)
    {
        float level = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            level += Mathf.Abs(buffer[i]);
        }
        return level / buffer.Length;
    }

    private static (float frequency, float confidence) AnalyzePitchAutocorrelation(float[] buffer, PitchAnalysisSettings settings)
    {
        int minPeriod = Mathf.FloorToInt(settings.sampleRate / settings.maxFrequency);
        int maxPeriod = Mathf.FloorToInt(settings.sampleRate / settings.minFrequency);

        // Validierung
        if (minPeriod >= buffer.Length / 2)
        {
            LogDebug($"Buffer too small for analysis: {buffer.Length} vs required {minPeriod * 2}", settings);
            return (0f, 0f);
        }

        float bestPeriod = 0;
        float maxCorrelation = 0;

        // RMS Check für Gesamtenergie
        float rms = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            rms += buffer[i] * buffer[i];
        }
        rms = Mathf.Sqrt(rms / buffer.Length);

        if (rms < 0.001f) // Zu leise
        {
            return (0f, 0f);
        }

        // Autocorrelation-Analyse
        for (int period = minPeriod; period <= maxPeriod && period < buffer.Length / 2; period++)
        {
            float correlation = 0;
            float energy1 = 0;
            float energy2 = 0;

            int numSamples = buffer.Length - period;

            // Berechne Korrelation und Energien
            for (int i = 0; i < numSamples; i++)
            {
                correlation += buffer[i] * buffer[i + period];
                energy1 += buffer[i] * buffer[i];
                energy2 += buffer[i + period] * buffer[i + period];
            }

            // Normalisierte Korrelation (bessere Ergebnisse)
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

        // Resultat-Validierung
        if (bestPeriod > 0 && maxCorrelation > settings.correlationThreshold)
        {
            float frequency = settings.sampleRate / bestPeriod;

            // Plausibilitätsprüfung
            if (frequency >= settings.minFrequency && frequency <= settings.maxFrequency)
            {
                return (frequency, maxCorrelation);
            }
        }

        return (0f, maxCorrelation);
    }

    private static void LogDebug(string message, PitchAnalysisSettings settings)
    {
        if (settings.enableDebugLogging)
        {
            Debug.Log($"[PitchAnalyzer] {message}");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Analysiert Audio-Buffer und gibt Pitch-Daten zurück
    /// </summary>
    public static PitchDataPoint AnalyzeAudioBuffer(float[] audioBuffer, float timestamp, PitchAnalysisSettings settings)
    {
        analysisCount++;

        // Validiere Input
        if (audioBuffer == null || audioBuffer.Length == 0)
        {
            LogDebug("Invalid audio buffer", settings);
            return new PitchDataPoint(timestamp, 0f, 0f, 0f);
        }

        // Initialisiere Windowing-Funktion bei Bedarf
        if (!windowInitialized || windowBuffer == null || windowBuffer.Length != audioBuffer.Length)
        {
            InitializeWindow(audioBuffer.Length);
            LogDebug($"Initialized window for buffer length: {audioBuffer.Length}", settings);
        }

        // Berechne Audio Level zuerst
        float audioLevel = CalculateAudioLevel(audioBuffer);

        // Zu leise? Früh returnen
        if (audioLevel < 0.0001f) // Sehr niedriger Threshold
        {
            if (analysisCount % 50 == 1) // Weniger Spam
                LogDebug($"Audio too quiet: {audioLevel:F6}", settings);
            return new PitchDataPoint(timestamp, 0f, 0f, audioLevel);
        }

        // Wende Windowing-Funktion an
        float[] windowedBuffer = ApplyWindow(audioBuffer);

        // Pitch-Analyse
        var result = AnalyzePitchAutocorrelation(windowedBuffer, settings);

        var pitchData = new PitchDataPoint(timestamp, result.frequency, result.confidence, audioLevel);

        if (result.frequency > 0 && settings.enableDebugLogging)
        {
            LogDebug($"Analysis #{analysisCount}: {pitchData}", settings);
        }

        return pitchData;
    }

    /// <summary>
    /// Pre-analysiert kompletten AudioClip für native Aufnahmen
    /// </summary>
    public static List<PitchDataPoint> PreAnalyzeAudioClip(AudioClip clip, PitchAnalysisSettings settings, float analysisInterval)
    {
        if (clip == null)
        {
            Debug.LogError("PitchAnalyzer: AudioClip is null!");
            return new List<PitchDataPoint>();
        }

        var results = new List<PitchDataPoint>();

        LogDebug($"Pre-analyzing AudioClip: {clip.name} ({clip.length:F1}s, {clip.samples} samples)", settings);

        int samplesPerInterval = Mathf.FloorToInt(analysisInterval * settings.sampleRate);
        float[] buffer = new float[settings.bufferLength];
        float[] clipData = new float[clip.samples * clip.channels]; // Support für Stereo

        clip.GetData(clipData, 0);

        // Konvertiere zu Mono falls nötig
        if (clip.channels > 1)
        {
            clipData = ConvertToMono(clipData, clip.channels);
        }

        int totalSamples = clipData.Length;
        int analysisPoints = 0;

        for (int i = 0; i + settings.bufferLength <= totalSamples; i += samplesPerInterval)
        {
            // Kopiere Buffer-Segment
            System.Array.Copy(clipData, i, buffer, 0, settings.bufferLength);

            float timestamp = (float)i / settings.sampleRate;
            var pitchData = AnalyzeAudioBuffer(buffer, timestamp, settings);

            results.Add(pitchData);
            analysisPoints++;

            // Progress-Logging
            if (analysisPoints % 50 == 0)
            {
                float progress = (float)i / totalSamples * 100f;
                LogDebug($"Pre-analysis progress: {progress:F1}% ({analysisPoints} points)", settings);
            }
        }

        LogDebug($"Pre-analysis complete: {results.Count} data points", settings);
        return results;
    }

    /// <summary>
    /// Glättet Pitch-Daten mit Moving Average
    /// </summary>
    public static List<PitchDataPoint> SmoothPitchData(List<PitchDataPoint> rawData, int windowSize)
    {
        if (rawData == null || !rawData.Any() || windowSize <= 1)
            return rawData ?? new List<PitchDataPoint>();

        var smoothed = new List<PitchDataPoint>();

        for (int i = 0; i < rawData.Count; i++)
        {
            int start = Mathf.Max(0, i - windowSize / 2);
            int end = Mathf.Min(rawData.Count - 1, i + windowSize / 2);

            var window = rawData.Skip(start).Take(end - start + 1);
            var pitchValues = window.Where(p => p.HasPitch).Select(p => p.frequency);

            float smoothedPitch = pitchValues.Any() ? pitchValues.Average() : 0f;
            float avgConfidence = window.Average(p => p.confidence);
            float avgLevel = window.Average(p => p.audioLevel);

            smoothed.Add(new PitchDataPoint(rawData[i].timestamp, smoothedPitch, avgConfidence, avgLevel));
        }

        return smoothed;
    }

    /// <summary>
    /// Hilfsmethode für Statistiken
    /// </summary>
    public static PitchStatistics CalculateStatistics(List<PitchDataPoint> data)
    {
        if (data == null || !data.Any())
            return new PitchStatistics();

        var pitchValues = data.Where(p => p.HasPitch).Select(p => p.frequency).ToList();

        if (!pitchValues.Any())
            return new PitchStatistics { TotalDataPoints = data.Count };

        return new PitchStatistics
        {
            TotalDataPoints = data.Count,
            PitchDataPoints = pitchValues.Count,
            SilenceDataPoints = data.Count - pitchValues.Count,
            MinPitch = pitchValues.Min(),
            MaxPitch = pitchValues.Max(),
            AveragePitch = pitchValues.Average(),
            AverageConfidence = data.Where(p => p.HasPitch).Average(p => p.confidence),
            AverageAudioLevel = data.Average(p => p.audioLevel)
        };
    }

    #endregion
}

[System.Serializable]
public struct PitchStatistics
{
    public int TotalDataPoints;
    public int PitchDataPoints;
    public int SilenceDataPoints;
    public float MinPitch;
    public float MaxPitch;
    public float AveragePitch;
    public float AverageConfidence;
    public float AverageAudioLevel;

    public float PitchPercentage => TotalDataPoints > 0 ? (float)PitchDataPoints / TotalDataPoints * 100f : 0f;

    public override string ToString()
    {
        return $"PitchStats: {PitchDataPoints}/{TotalDataPoints} points ({PitchPercentage:F1}% pitch), " +
               $"Range: {MinPitch:F0}-{MaxPitch:F0}Hz, Avg: {AveragePitch:F0}Hz";
    }
}