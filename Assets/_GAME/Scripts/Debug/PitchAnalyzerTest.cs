using UnityEngine;
using System.Collections.Generic;

// COPILOT CONTEXT: Test script for PitchAnalyzer functionality
// Tests both real-time analysis and AudioClip pre-analysis

public class PitchAnalyzerTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private PitchAnalysisSettings testSettings;
    [SerializeField] private AudioClip testClip;
    [SerializeField] private bool autoRunTest = false;
    
    [Header("Results")]
    [SerializeField] private int totalDataPoints;
    [SerializeField] private int pitchDataPoints;
    [SerializeField] private float averagePitch;
    
    void Start()
    {
        if (autoRunTest && testClip != null)
        {
            TestAudioClipAnalysis();
        }
    }
    
    [ContextMenu("Test AudioClip Analysis")]
    public void TestAudioClipAnalysis()
    {
        if (testClip == null)
        {
            Debug.LogError("No test clip assigned!");
            return;
        }
        
        Debug.Log($"Testing PitchAnalyzer with clip: {testClip.name}");
        
        var pitchData = PitchAnalyzer.PreAnalyzeAudioClip(testClip, testSettings, 0.1f);
        var stats = PitchAnalyzer.CalculateStatistics(pitchData);
        
        totalDataPoints = stats.TotalDataPoints;
        pitchDataPoints = stats.PitchDataPoints;
        averagePitch = stats.AveragePitch;
        
        Debug.Log($"Analysis complete: {stats}");
        
        // Zeige erste 10 Datenpunkte
        for (int i = 0; i < Mathf.Min(10, pitchData.Count); i++)
        {
            Debug.Log($"  {i}: {pitchData[i]}");
        }
    }
    
    [ContextMenu("Test Smoothing")]
    public void TestSmoothing()
    {
        if (testClip == null) return;
        
        var rawData = PitchAnalyzer.PreAnalyzeAudioClip(testClip, testSettings, 0.1f);
        var smoothedData = PitchAnalyzer.SmoothPitchData(rawData, 5);
        
        Debug.Log("Raw vs Smoothed (first 10 points):");
        for (int i = 0; i < Mathf.Min(10, rawData.Count); i++)
        {
            Debug.Log($"  {i}: {rawData[i].frequency:F1}Hz -> {smoothedData[i].frequency:F1}Hz");
        }
    }
}