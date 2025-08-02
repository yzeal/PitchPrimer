using UnityEngine;
using System.Collections.Generic;

// COPILOT CONTEXT: Scoring-specific extensions for PitchVisualizer
// Adds static display capabilities for completed recordings in scoring screen

public partial class PitchVisualizer : MonoBehaviour
{
    // Public method for creating static displays in scoring screen
    public void DisplayStaticPitchData(List<PitchDataPoint> pitchData, int maxDisplayPoints = 100)
    {
        if (pitchData == null || pitchData.Count == 0)
        {
            DebugLog("No pitch data provided for static display");
            return;
        }
        
        DebugLog($"?? Creating static display: {pitchData.Count} points (max: {maxDisplayPoints})");
        
        // Clear existing visualization
        ClearAll();
        
        // Sample data if needed
        var displayData = SampleDataForDisplay(pitchData, maxDisplayPoints);
        
        // Create static cubes
        CreateStaticCubes(displayData);
        
        DebugLog($"? Static display complete: {displayData.Count} cubes created");
    }
    
    private List<PitchDataPoint> SampleDataForDisplay(List<PitchDataPoint> originalData, int maxPoints)
    {
        if (originalData.Count <= maxPoints)
            return new List<PitchDataPoint>(originalData);
        
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
        
        DebugLog($"?? Sampled data: {originalData.Count} ? {sampled.Count} points");
        return sampled;
    }
    
    private void CreateStaticCubes(List<PitchDataPoint> pitchData)
    {
        if (settings.cubePrefab == null || settings.cubeParent == null)
        {
            Debug.LogError("[PitchVisualizer] Missing cube prefab or parent for static display");
            return;
        }
        
        float totalWidth = pitchData.Count * settings.cubeSpacing;
        float startX = -totalWidth / 2f; // Center the display
        
        for (int i = 0; i < pitchData.Count; i++)
        {
            var pitchPoint = pitchData[i];
            GameObject cube = CreateCube(pitchPoint, true, i);
            
            if (cube != null)
            {
                // Position cubes in a line from left to right
                float cubeX = startX + (i * settings.cubeSpacing);
                Vector3 position = new Vector3(cubeX, 0, 0) + settings.trackOffset;
                cube.transform.localPosition = position;
                
                // Set static appearance (no state-based coloring)
                SetStaticCubeAppearance(cube, pitchPoint);
            }
        }
    }
    
    private void SetStaticCubeAppearance(GameObject cube, PitchDataPoint pitchData)
    {
        var renderer = cube.GetComponent<Renderer>();
        if (renderer == null) return;
        
        // Use standard coloring without state modifications
        Color cubeColor = GetCubeColor(pitchData);
        
        // For static display, use full opacity and standard brightness
        cubeColor.a = 1f;
        
        renderer.material.color = cubeColor;
    }
    
    // Public method specifically for scoring screen dual display
    public static void SetupScoringDisplay(PitchVisualizer nativeVisualizer, PitchVisualizer userVisualizer, 
        List<PitchDataPoint> nativeData, List<PitchDataPoint> userData)
    {
        if (nativeVisualizer != null && nativeData != null)
        {
            nativeVisualizer.DisplayStaticPitchData(nativeData);
            Debug.Log($"?? Native visualization setup: {nativeData.Count} points");
        }
        
        if (userVisualizer != null && userData != null)
        {
            userVisualizer.DisplayStaticPitchData(userData);
            Debug.Log($"?? User visualization setup: {userData.Count} points");
        }
    }
    
    // Helper to create cube with public access for static displays
    public GameObject CreateStaticCube(PitchDataPoint pitchData, Vector3 position)
    {
        GameObject cube = CreateCube(pitchData, true, -1);
        if (cube != null)
        {
            cube.transform.localPosition = position;
            SetStaticCubeAppearance(cube, pitchData);
        }
        return cube;
    }
    
    private void DebugLog(string message)
    {
        Debug.Log($"[PitchVisualizer-Scoring] {gameObject.name}: {message}");
    }
}