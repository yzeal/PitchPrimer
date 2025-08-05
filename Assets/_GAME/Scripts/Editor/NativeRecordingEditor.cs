using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NativeRecording))]
public class NativeRecordingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        NativeRecording recording = (NativeRecording)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Analysis Tools", EditorStyles.boldLabel);
        
        // Status anzeigen
        if (recording.IsAnalyzed)
        {
            EditorGUILayout.HelpBox($"? Analyzed: {recording.CachedDataPoints} data points", MessageType.Info);
            EditorGUILayout.LabelField($"Pitch Range: {recording.PitchRangeMin:F1}Hz - {recording.PitchRangeMax:F1}Hz");
        }
        else
        {
            EditorGUILayout.HelpBox("?? Not analyzed yet", MessageType.Warning);
        }
        
        EditorGUILayout.Space();
        
        // Analyse Buttons
        GUI.enabled = recording.AudioClip != null;
        
        if (GUILayout.Button("?? Analyze Audio (0.1s interval)"))
        {
            recording.AnalyzeAudio(0.1f);
            EditorUtility.SetDirty(recording);
        }
        
        if (GUILayout.Button("?? Analyze Audio (0.05s interval - High Quality)"))
        {
            recording.AnalyzeAudio(0.05f);
            EditorUtility.SetDirty(recording);
        }
        
        GUI.enabled = recording.IsAnalyzed;
        
        if (GUILayout.Button("?? Force Re-analyze"))
        {
            recording.ForceReanalyze(0.1f);
            EditorUtility.SetDirty(recording);
        }
        
        if (GUILayout.Button("??? Clear Cached Data"))
        {
            recording.ClearCachedData();
            EditorUtility.SetDirty(recording);
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        
        // Manual Pitch Range Override
        if (recording.IsAnalyzed)
        {
            EditorGUILayout.LabelField("Manual Pitch Range Override", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            float newMin = EditorGUILayout.FloatField("Min Hz:", recording.PitchRangeMin);
            float newMax = EditorGUILayout.FloatField("Max Hz:", recording.PitchRangeMax);
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("?? Set Manual Pitch Range"))
            {
                recording.SetManualPitchRange(newMin, newMax);
                EditorUtility.SetDirty(recording);
            }
        }
    }
}