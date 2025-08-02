using UnityEngine;
using System.Collections.Generic;

// COPILOT CONTEXT: Extensions for PitchVisualizer to support static display in scoring screen
// Adds methods for displaying completed recordings side-by-side for comparison

public static class PitchVisualizerExtensions
{
    /// <summary>
    /// Display a complete recording as static visualization for scoring comparison
    /// </summary>
    /// <param name="visualizer">The PitchVisualizer instance</param>
    /// <param name="pitchData">Complete pitch data to display</param>
    /// <param name="maxDisplayPoints">Maximum number of points to show (for performance)</param>
    public static void DisplayStaticRecording(this PitchVisualizer visualizer, List<PitchDataPoint> pitchData, int maxDisplayPoints = 100)
    {
        if (pitchData == null || pitchData.Count == 0)
        {
            Debug.LogWarning("[PitchVisualizerExtensions] No pitch data provided for static display");
            return;
        }
        
        // Clear existing visualization
        visualizer.ClearAll();
        
        // Sample data if too many points
        var sampledData = SamplePitchData(pitchData, maxDisplayPoints);
        
        // Create static cubes for the entire recording
        var settings = visualizer.GetSettings();
        if (settings == null)
        {
            Debug.LogError("[PitchVisualizerExtensions] No visualization settings available");
            return;
        }
        
        CreateStaticVisualization(visualizer, sampledData, settings);
        
        Debug.Log($"[PitchVisualizerExtensions] Static display created: {sampledData.Count} points from {pitchData.Count} original");
    }
    
    /// <summary>
    /// Display two recordings side-by-side for comparison
    /// </summary>
    public static void DisplayComparisonRecordings(this PitchVisualizer nativeVisualizer, PitchVisualizer userVisualizer, 
        List<PitchDataPoint> nativeData, List<PitchDataPoint> userData, int maxDisplayPoints = 100)
    {
        if (nativeVisualizer != null && nativeData != null)
        {
            nativeVisualizer.DisplayStaticRecording(nativeData, maxDisplayPoints);
        }
        
        if (userVisualizer != null && userData != null)
        {
            userVisualizer.DisplayStaticRecording(userData, maxDisplayPoints);
        }
        
        Debug.Log($"[PitchVisualizerExtensions] Comparison display created");
    }
    
    private static List<PitchDataPoint> SamplePitchData(List<PitchDataPoint> originalData, int maxPoints)
    {
        if (originalData.Count <= maxPoints)
            return originalData;
        
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
    
    private static void CreateStaticVisualization(PitchVisualizer visualizer, List<PitchDataPoint> pitchData, VisualizationSettings settings)
    {
        // Access private methods through reflection or create public wrapper methods
        // For now, we'll use the existing public API
        
        // Since we can't access private CreateCube method directly, we'll need to add a public method to PitchVisualizer
        // This is a temporary solution until we can modify PitchVisualizer directly
        
        Debug.Log($"[PitchVisualizerExtensions] Would create {pitchData.Count} static cubes");
        // TODO: Implementation depends on public API addition to PitchVisualizer
    }
}